using GamesIT.Api.Modelos;
using Microsoft.AspNetCore.Identity;

namespace GamesIT.Api.Servicios;

/// <summary>
/// Crea el primer administrador SOLO mediante configuración explícita en desarrollo.
/// Nunca convierte cuentas normales existentes en administradores.
/// </summary>
public static class InicializadorAdministrador
{
    public const string RolAdministrador = "Administrador";

    public static async Task InicializarAsync(
        IServiceProvider servicios,
        IConfiguration configuracion,
        IHostEnvironment entorno)
    {
        // No habilitar el aprovisionamiento automático en un servidor público.
        if (!entorno.IsDevelopment() ||
            !configuracion.GetValue<bool>("AdminInicial:Habilitado"))
            return;

        string correo = configuracion["AdminInicial:Correo"]?.Trim() ?? "";
        string contrasena = configuracion["AdminInicial:Contrasena"] ?? "";

        if (string.IsNullOrWhiteSpace(correo) || contrasena.Length < 12)
        {
            throw new InvalidOperationException(
                "AdminInicial requiere Correo y una Contrasena de al menos 12 caracteres en los secretos de usuario.");
        }

        using IServiceScope alcance = servicios.CreateScope();
        var roles = alcance.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var usuarios = alcance.ServiceProvider.GetRequiredService<UserManager<Usuario>>();

        if (!await roles.RoleExistsAsync(RolAdministrador))
        {
            IdentityResult resultadoRol = await roles.CreateAsync(
                new IdentityRole<Guid>(RolAdministrador));
            if (!resultadoRol.Succeeded)
                throw new InvalidOperationException("No se pudo crear el rol Administrador.");
        }

        Usuario? existente = await usuarios.FindByEmailAsync(correo);
        if (existente != null)
        {
            if (!await usuarios.IsInRoleAsync(existente, RolAdministrador))
                throw new InvalidOperationException(
                    "El correo configurado pertenece a una cuenta existente de jugador. Usá otro correo para el administrador.");

            // La cuenta administradora ya existe: no modificar contraseñas ni roles al reiniciar.
            return;
        }

        var administrador = new Usuario
        {
            Id = Guid.NewGuid(),
            UserName = correo,
            Email = correo,
            EmailConfirmed = true,
            EstaActivo = true,
            FechaRegistroUtc = DateTime.UtcNow
        };

        IdentityResult creado = await usuarios.CreateAsync(administrador, contrasena);
        if (!creado.Succeeded)
            throw new InvalidOperationException("No se pudo crear el administrador: " +
                string.Join("; ", creado.Errors.Select(e => e.Description)));

        IdentityResult asignado = await usuarios.AddToRoleAsync(administrador, RolAdministrador);
        if (!asignado.Succeeded)
        {
            await usuarios.DeleteAsync(administrador);
            throw new InvalidOperationException("No se pudo asignar el rol Administrador.");
        }
    }
}
