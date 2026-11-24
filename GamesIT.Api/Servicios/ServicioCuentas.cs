using GamesIT.Api.Datos;
using GamesIT.Api.DTOs.Administracion;
using GamesIT.Api.DTOs.Autenticacion;
using GamesIT.Api.Modelos;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GamesIT.Api.Servicios;

public sealed record ResultadoEliminarCuenta(bool Correcto, string Codigo, string Mensaje);

public class ServicioCuentas
{
    private const string FraseEliminar = "ELIMINAR MI CUENTA";

    private readonly ContextoGamesIT _db;
    private readonly UserManager<Usuario> _usuarios;
    private readonly SignInManager<Usuario> _sesiones;
    private readonly ILogger<ServicioCuentas> _logger;

    public ServicioCuentas(
        ContextoGamesIT db,
        UserManager<Usuario> usuarios,
        SignInManager<Usuario> sesiones,
        ILogger<ServicioCuentas> logger)
    {
        _db = db;
        _usuarios = usuarios;
        _sesiones = sesiones;
        _logger = logger;
    }

    // El usuario solo puede eliminar su propia cuenta; el ID proviene de su cookie.
    public async Task<ResultadoEliminarCuenta> EliminarMiCuentaAsync(
        ClaimsPrincipal principal,
        EliminarCuentaDTO datos)
    {
        Usuario? usuario = await _usuarios.GetUserAsync(principal);
        if (usuario == null || !usuario.EstaActivo)
            return new(false, "no_autorizado", "La sesión no es válida.");

        if (await _usuarios.IsInRoleAsync(usuario, InicializadorAdministrador.RolAdministrador))
            return new(false, "prohibido", "Las cuentas administradoras no se eliminan desde el perfil del jugador.");

        if (datos.Confirmacion != FraseEliminar)
            return new(false, "confirmacion", "Escribí exactamente ELIMINAR MI CUENTA.");

        SignInResult comprobacion = await _sesiones.CheckPasswordSignInAsync(
            usuario, datos.Contrasena, lockoutOnFailure: true);
        if (!comprobacion.Succeeded)
            return new(false, "contrasena", "No fue posible confirmar tu contraseña o la cuenta está bloqueada temporalmente.");

        await using var transaccion = await _db.Database.BeginTransactionAsync();
        try
        {
            // La relación AspNetUsers -> Jugadores tiene DeleteBehavior.Cascade.
            // Al incorporar partidas, compras o amistades, definir sus relaciones
            // y la política de eliminación antes de activar esos módulos.
            IdentityResult eliminado = await _usuarios.DeleteAsync(usuario);
            if (!eliminado.Succeeded)
            {
                await transaccion.RollbackAsync();
                return new(false, "conflicto", "No se pudo eliminar la cuenta.");
            }
            await transaccion.CommitAsync();
        }
        catch (DbUpdateException ex)
        {
            await transaccion.RollbackAsync();
            _logger.LogError(ex, "No se pudo completar la eliminación de una cuenta por restricciones de integridad.");
            return new(false, "conflicto", "No se pudo eliminar la cuenta porque todavía tiene datos asociados que requieren revisión.");
        }

        // Elimina la cookie del navegador actual. Las demás cookies se invalidan
        // al revalidarse el sello de seguridad en cada solicitud (Program.cs).
        await _sesiones.SignOutAsync();
        return new(true, "ok", "Tu cuenta fue eliminada definitivamente.");
    }

    public async Task<PaginaJugadoresDTO> ListarJugadoresAsync(
        string? busqueda, int pagina, int tamanio)
    {
        pagina = Math.Max(1, pagina);
        tamanio = Math.Clamp(tamanio, 1, 50);

        var consulta = _db.Jugadores.AsNoTracking().AsQueryable();
        string filtro = busqueda?.Trim() ?? "";
        if (!string.IsNullOrEmpty(filtro))
        {
            string filtroNormalizado = filtro.ToUpperInvariant();
            consulta = consulta.Where(j =>
                j.NombreNormalizado.Contains(filtroNormalizado) ||
                (j.Usuario.Email != null && j.Usuario.Email.Contains(filtro)));
        }

        int total = await consulta.CountAsync();
        var jugadores = await consulta
            .OrderBy(j => j.NombreNormalizado)
            .Skip((pagina - 1) * tamanio)
            .Take(tamanio)
            .Select(j => new JugadorAdministracionDTO
            {
                JugadorId = j.Id,
                NombreJugador = j.NombreJugador,
                CorreoElectronico = j.Usuario.Email ?? "",
                Nivel = j.Nivel,
                Experiencia = j.Experiencia,
                Bits = j.Bits,
                FechaRegistroUtc = j.Usuario.FechaRegistroUtc
            })
            .ToListAsync();

        return new PaginaJugadoresDTO
        {
            Pagina = pagina,
            Tamanio = tamanio,
            Total = total,
            Jugadores = jugadores
        };
    }

    public async Task<ResultadoEliminarCuenta> EliminarJugadorAsync(
        ClaimsPrincipal principal,
        EliminarJugadorDTO datos)
    {
        Usuario? admin = await _usuarios.GetUserAsync(principal);
        if (admin == null || !admin.EstaActivo ||
            !await _usuarios.IsInRoleAsync(admin, InicializadorAdministrador.RolAdministrador))
            return new(false, "prohibido", "No tenés permisos de administrador.");

        SignInResult comprobacion = await _sesiones.CheckPasswordSignInAsync(
            admin, datos.ContrasenaAdministrador, lockoutOnFailure: true);
        if (!comprobacion.Succeeded)
            return new(false, "contrasena", "La contraseña del administrador es incorrecta o la cuenta está bloqueada.");

        Jugador? jugador = await _db.Jugadores.AsNoTracking()
            .SingleOrDefaultAsync(j => j.Id == datos.JugadorId);
        if (jugador == null)
            return new(false, "no_encontrado", "El jugador ya no existe.");

        if (!string.Equals(jugador.NombreJugador, datos.NombreConfirmado, StringComparison.Ordinal))
            return new(false, "confirmacion", "Escribí exactamente el nombre del jugador para confirmar.");

        Usuario? usuario = await _usuarios.FindByIdAsync(jugador.UsuarioId.ToString());
        if (usuario == null)
            return new(false, "no_encontrado", "La cuenta ya no existe.");
        if (await _usuarios.IsInRoleAsync(usuario, InicializadorAdministrador.RolAdministrador))
            return new(false, "prohibido", "No está permitido eliminar administradores mediante esta operación.");

        await using var transaccion = await _db.Database.BeginTransactionAsync();
        try
        {
            IdentityResult resultado = await _usuarios.DeleteAsync(usuario);
            if (!resultado.Succeeded)
            {
                await transaccion.RollbackAsync();
                return new(false, "conflicto", "No se pudo eliminar el jugador.");
            }
            await transaccion.CommitAsync();
            // Solo se registran identificadores, no contraseñas.
            _logger.LogWarning("Un administrador eliminó un jugador. AdministradorId={AdminId}; JugadorId={JugadorId}",
                admin.Id, jugador.Id);
            return new(true, "ok", "Jugador eliminado definitivamente.");
        }
        catch (DbUpdateException ex)
        {
            await transaccion.RollbackAsync();
            _logger.LogError(ex, "Error de integridad al eliminar un jugador desde administración.");
            return new(false, "conflicto", "La cuenta tiene datos asociados que requieren revisión antes de eliminarla.");
        }
    }
}
