using System.Data;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using GamesIT.Api.Datos;
using GamesIT.Api.DTOs.Autenticacion;
using GamesIT.Api.Modelos;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GamesIT.Api.Servicios;

public sealed record ResultadoRegistro(
    bool Correcto,
    string Mensaje,
    Guid? SolicitudId = null,
    string CodigoError = "");

public class ServicioRegistroPorCorreo
{
    private readonly ContextoGamesIT _db;
    private readonly UserManager<Usuario> _usuarios;
    private readonly ServicioCorreoSmtp _correo;
    private readonly ILogger<ServicioRegistroPorCorreo> _logger;
    private static readonly TimeSpan DuracionCodigo = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan EsperaReenvio = TimeSpan.FromSeconds(60);

    public ServicioRegistroPorCorreo(
        ContextoGamesIT db,
        UserManager<Usuario> usuarios,
        ServicioCorreoSmtp correo,
        ILogger<ServicioRegistroPorCorreo> logger)
    {
        _db = db;
        _usuarios = usuarios;
        _correo = correo;
        _logger = logger;
    }

    public async Task<ResultadoRegistro> SolicitarAsync(RegistroUsuarioDTO datos)
    {
        if (datos.Contrasena != datos.ConfirmarContrasena)
            return new(false, "Las contraseñas no coinciden.", CodigoError: "datos");

        string correo = datos.CorreoElectronico.Trim();
        string nombre = Regex.Replace(datos.NombreJugador.Trim(), @"\s+", " ");
        string normalizado = nombre.ToUpperInvariant();

        if (nombre.Length is < 3 or > 16 ||
            !Regex.IsMatch(nombre, @"^[\p{L}\p{N}_\- ]+$"))
            return new(false, "El nombre de jugador no es válido.", CodigoError: "datos");

        // Las cuentas confirmadas tienen prioridad sobre las solicitudes pendientes.
        if (await _usuarios.FindByEmailAsync(correo) != null ||
            await _db.Jugadores.AnyAsync(j => j.NombreNormalizado == normalizado))
        {
            return new(false,
                "Ese correo o nombre de jugador ya está registrado.",
                CodigoError: "duplicado");
        }

        var usuarioTemporal = new Usuario
        {
            Id = Guid.NewGuid(),
            UserName = correo,
            Email = correo
        };

        foreach (var validador in _usuarios.PasswordValidators)
        {
            IdentityResult validacion = await validador.ValidateAsync(
                _usuarios, usuarioTemporal, datos.Contrasena);
            if (!validacion.Succeeded)
                return new(false,
                    "La contraseña no cumple los requisitos de seguridad.",
                    CodigoError: "datos");
        }

        DateTime ahora = DateTime.UtcNow;

        // Libera las reservas vencidas del nombre o correo solicitado ANTES
        // de consultar la unicidad. El servicio de limpieza también lo hace
        // periódicamente, pero no queremos depender de la próxima ejecución.
        await _db.RegistrosPendientes
            .Where(r => r.VenceUtc <= ahora &&
                (r.NombreNormalizado == normalizado || r.CorreoElectronico == correo))
            .ExecuteDeleteAsync();

        // Otra dirección no puede reservar un nombre ya reservado y vigente.
        // El índice único de SQL Server evita carreras entre solicitudes.
        bool reservadoPorOtroCorreo = await _db.RegistrosPendientes.AnyAsync(r =>
            r.NombreNormalizado == normalizado &&
            r.CorreoElectronico != correo);

        if (reservadoPorOtroCorreo)
            return new(false,
                "Ese nombre de jugador está reservado temporalmente. Elegí otro.",
                CodigoError: "duplicado");

        var pendiente = await _db.RegistrosPendientes
            .SingleOrDefaultAsync(r => r.CorreoElectronico == correo);

        if (pendiente != null && ahora - pendiente.UltimoEnvioUtc < EsperaReenvio)
            return new(false,
                "Esperá un minuto antes de solicitar otro código.",
                CodigoError: "espera");

        string codigo = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

        if (pendiente == null)
        {
            pendiente = new RegistroPendiente
            {
                Id = Guid.NewGuid(),
                CorreoElectronico = correo
            };
            _db.RegistrosPendientes.Add(pendiente);
        }

        pendiente.NombreJugador = nombre;
        pendiente.NombreNormalizado = normalizado;
        pendiente.ContrasenaHash = _usuarios.PasswordHasher.HashPassword(
            usuarioTemporal, datos.Contrasena);
        pendiente.CodigoHash = _usuarios.PasswordHasher.HashPassword(
            usuarioTemporal, codigo);
        pendiente.VenceUtc = ahora.Add(DuracionCodigo);
        pendiente.UltimoEnvioUtc = ahora;
        pendiente.IntentosFallidos = 0;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (EsViolacionUnicidad(ex))
        {
            return new(false,
                "El correo o nombre de jugador ya está siendo utilizado.",
                CodigoError: "duplicado");
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "No se pudo guardar la solicitud de registro.");
            return new(false,
                "No se pudo iniciar el registro. Intentá nuevamente.",
                CodigoError: "conflicto");
        }

        // El código se registra antes del envío. Si SMTP falla, se conserva
        // el cooldown temporal para no permitir reintentos masivos.
        // En una etapa futura se podrá separar intento/aceptación de envío.
        try
        {
            await _correo.EnviarCodigoAsync(correo, codigo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo enviar un correo de verificación de GamesIT.");
            return new(false,
                "No se pudo enviar el correo. Intentá nuevamente en un minuto.",
                CodigoError: "correo");
        }

        return new(true,
            "Te enviamos un código de seis dígitos. Revisá tu correo.",
            pendiente.Id);
    }

    public async Task<ResultadoRegistro> ReenviarAsync(Guid solicitudId)
    {
        var pendiente = await _db.RegistrosPendientes.FindAsync(solicitudId);

        if (pendiente == null || pendiente.VenceUtc < DateTime.UtcNow)
            return new(false,
                "La solicitud venció. Comenzá nuevamente el registro.",
                CodigoError: "vencido");

        // Una solicitud previa puede haber quedado invalidada si otra cuenta
        // obtuvo ese mismo nombre. No debemos reenviar códigos inútiles.
        if (await _db.Jugadores.AnyAsync(
            j => j.NombreNormalizado == pendiente.NombreNormalizado) ||
            await _usuarios.FindByEmailAsync(pendiente.CorreoElectronico) != null)
        {
            return new(false,
                "Ese correo o nombre de jugador ya está registrado.",
                CodigoError: "duplicado");
        }

        if (DateTime.UtcNow - pendiente.UltimoEnvioUtc < EsperaReenvio)
            return new(false,
                "Esperá un minuto antes de pedir otro código.",
                CodigoError: "espera");

        var temporal = new Usuario { Id = Guid.NewGuid() };
        string codigo = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        pendiente.CodigoHash = _usuarios.PasswordHasher.HashPassword(temporal, codigo);
        pendiente.VenceUtc = DateTime.UtcNow.Add(DuracionCodigo);
        pendiente.UltimoEnvioUtc = DateTime.UtcNow;
        pendiente.IntentosFallidos = 0;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(false, "Intentá nuevamente.", CodigoError: "conflicto");
        }

        try
        {
            await _correo.EnviarCodigoAsync(pendiente.CorreoElectronico, codigo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al reenviar el código.");
            return new(false, "No se pudo enviar el correo.", CodigoError: "correo");
        }

        return new(true, "Te enviamos un código nuevo.", pendiente.Id);
    }

    public async Task<ResultadoRegistro> ConfirmarAsync(ConfirmarRegistroDTO datos)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        var pendiente = await _db.RegistrosPendientes.FindAsync(datos.SolicitudId);
        if (pendiente == null || pendiente.VenceUtc < DateTime.UtcNow)
            return new(false,
                "El código venció o la solicitud no existe.",
                CodigoError: "vencido");

        if (pendiente.IntentosFallidos >= 5)
            return new(false,
                "Se agotaron los intentos. Solicitá otro código.",
                CodigoError: "bloqueado");

        var temporal = new Usuario { Id = Guid.NewGuid() };
        bool valido = _usuarios.PasswordHasher.VerifyHashedPassword(
            temporal, pendiente.CodigoHash, datos.Codigo) != PasswordVerificationResult.Failed;

        if (!valido)
        {
            pendiente.IntentosFallidos++;
            try
            {
                await _db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return new(false, "Intentá nuevamente.", CodigoError: "conflicto");
            }

            return new(false,
                "Código incorrecto. Verificá los seis dígitos.",
                CodigoError: "codigo");
        }

        // Verificación final: el nombre pudo registrarse desde que
        // comenzó la solicitud. Un índice único en Jugadores refuerza esto.
        if (await _usuarios.FindByEmailAsync(pendiente.CorreoElectronico) != null ||
            await _db.Jugadores.AnyAsync(
                j => j.NombreNormalizado == pendiente.NombreNormalizado))
        {
            return new(false,
                "El correo o nombre ya está registrado.",
                CodigoError: "duplicado");
        }

        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            UserName = pendiente.CorreoElectronico,
            Email = pendiente.CorreoElectronico,
            EmailConfirmed = true,
            PasswordHash = pendiente.ContrasenaHash,
            FechaRegistroUtc = DateTime.UtcNow,
            EstaActivo = true
        };

        // Conserva el hash ya preparado para el registro pendiente.
        var resultado = await _usuarios.CreateAsync(usuario);
        if (!resultado.Succeeded)
            return new(false,
                "No se pudo crear la cuenta. Revisá los datos.",
                CodigoError: "datos");

        _db.Jugadores.Add(new Jugador
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuario.Id,
            NombreJugador = pendiente.NombreJugador,
            NombreNormalizado = pendiente.NombreNormalizado,
            AvatarId = "bytefox"
        });

        _db.RegistrosPendientes.Remove(pendiente);

        try
        {
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (DbUpdateException ex) when (EsViolacionUnicidad(ex))
        {
            await tx.RollbackAsync();
            return new(false,
                "Ese correo o nombre ya fue registrado. Intentá con otros datos.",
                CodigoError: "duplicado");
        }
        catch (DbUpdateException ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "Error al confirmar el registro.");
            return new(false,
                "No se pudo crear la cuenta. Intentá nuevamente.",
                CodigoError: "conflicto");
        }

        return new(true, "Correo verificado y cuenta creada correctamente.");
    }

    private static bool EsViolacionUnicidad(DbUpdateException ex) =>
        ex.InnerException is SqlException sql &&
        sql.Number is 2601 or 2627;
}
