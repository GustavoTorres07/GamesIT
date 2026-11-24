using GamesIT.Api.Datos;
using GamesIT.Api.DTOs.Autenticacion;
using GamesIT.Api.Modelos;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace GamesIT.Api.Servicios
{
    public class ServicioAutenticacion
    {
        private readonly UserManager<Usuario> _administradorUsuarios;
        private readonly SignInManager<Usuario> _administradorInicioSesion;
        private readonly ContextoGamesIT _contexto;

        private static readonly HashSet<string> AvataresPermitidos =
            new(StringComparer.Ordinal) { "bytefox", "nexobot", "pixelowl" };

        public ServicioAutenticacion(
            UserManager<Usuario> administradorUsuarios,
            SignInManager<Usuario> administradorInicioSesion,
            ContextoGamesIT contexto)
        {
            _administradorUsuarios = administradorUsuarios;
            _administradorInicioSesion = administradorInicioSesion;
            _contexto = contexto;
        }

        // ==========================================
        // LOGIN: CORREO O NOMBRE DE JUGADOR
        // ==========================================
        public async Task<SignInResult> IniciarSesionAsync(InicioSesionDTO datos)
        {
            string identificador = Regex.Replace(datos.Identificador.Trim(), @"\s+", " ");
            Usuario? usuario;

            if (identificador.Contains('@'))
            {
                usuario = await _administradorUsuarios.FindByEmailAsync(identificador);
            }
            else
            {
                string nombreNormalizado = identificador.ToUpperInvariant();
                var jugador = await _contexto.Jugadores.AsNoTracking()
                    .FirstOrDefaultAsync(j => j.NombreNormalizado == nombreNormalizado);

                usuario = jugador == null
                    ? null
                    : await _administradorUsuarios.FindByIdAsync(jugador.UsuarioId.ToString());
            }

            // No revelar si el usuario o el correo existen.
            if (usuario == null) return SignInResult.Failed;
            if (!usuario.EstaActivo) return SignInResult.NotAllowed;

            return await _administradorInicioSesion.PasswordSignInAsync(
                usuario,
                datos.Contrasena,
                isPersistent: datos.Recordarme,
                lockoutOnFailure: true);
        }

        // ==========================================
        // PERFIL DEL USUARIO DE LA COOKIE
        // ==========================================
        public async Task<PerfilAutenticadoDTO?> ObtenerPerfilAsync(ClaimsPrincipal principal)
        {
            if (principal.Identity?.IsAuthenticated != true) return null;

            Usuario? usuario = await _administradorUsuarios.GetUserAsync(principal);
            if (usuario == null || !usuario.EstaActivo) return null;

            if (await _administradorUsuarios.IsInRoleAsync(
                usuario, InicializadorAdministrador.RolAdministrador))
            {
                return new PerfilAutenticadoDTO
                {
                    EsAdministrador = true,
                    UsuarioId = usuario.Id,
                    JugadorId = Guid.Empty,
                    NombreJugador = "Administrador",
                    CorreoElectronico = usuario.Email ?? string.Empty,
                    FechaRegistroUtc = usuario.FechaRegistroUtc,
                    AvatarId = "nexobot"
                };
            }

            Jugador? jugador = await _contexto.Jugadores.AsNoTracking()
                .FirstOrDefaultAsync(j => j.UsuarioId == usuario.Id);
            if (jugador == null) return null;

            return new PerfilAutenticadoDTO
            {
                UsuarioId = usuario.Id,
                JugadorId = jugador.Id,
                NombreJugador = jugador.NombreJugador,
                CorreoElectronico = usuario.Email ?? string.Empty,
                FechaRegistroUtc = usuario.FechaRegistroUtc,
                AvatarId = AvataresPermitidos.Contains(jugador.AvatarId)
                    ? jugador.AvatarId : "bytefox",
                Nivel = jugador.Nivel,
                Experiencia = jugador.Experiencia,
                Bits = jugador.Bits,
                PartidasJugadas = jugador.PartidasJugadas,
                PartidasGanadas = jugador.PartidasGanadas,
                JuegosFavoritos = jugador.FavoritosCsv.Split(',', StringSplitOptions.RemoveEmptyEntries)
            };
        }

        // ==========================================
        // GUARDAR AVATAR EN SQL SERVER
        // ==========================================
        public async Task<bool> CambiarAvatarAsync(ClaimsPrincipal principal, string avatarId)
        {
            if (!AvataresPermitidos.Contains(avatarId)) return false;
            if (principal.Identity?.IsAuthenticated != true) return false;

            Usuario? usuario = await _administradorUsuarios.GetUserAsync(principal);
            if (usuario == null || !usuario.EstaActivo) return false;

            Jugador? jugador = await _contexto.Jugadores
                .FirstOrDefaultAsync(j => j.UsuarioId == usuario.Id);
            if (jugador == null) return false;

            jugador.AvatarId = avatarId;
            await _contexto.SaveChangesAsync();
            return true;
        }

        // ==========================================
        // FAVORITOS: PERSISTIDOS EN SQL SERVER
        // ==========================================
        public async Task<bool> ModificarFavoritoAsync(
            ClaimsPrincipal principal, string juegoId, bool favorito)
        {
            if (juegoId is not ("crucigrama" or "ahorcado" or "sopa")) return false;
            Usuario? usuario = await _administradorUsuarios.GetUserAsync(principal);
            if (usuario == null || !usuario.EstaActivo) return false;
            Jugador? jugador = await _contexto.Jugadores
                .SingleOrDefaultAsync(j => j.UsuarioId == usuario.Id);
            if (jugador == null) return false;
            var favoritos = jugador.FavoritosCsv.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .ToHashSet(StringComparer.Ordinal);
            if (favorito) favoritos.Add(juegoId);
            else favoritos.Remove(juegoId);
            jugador.FavoritosCsv = string.Join(',', favoritos.OrderBy(x => x));
            await _contexto.SaveChangesAsync();
            return true;
        }

        // ==========================================
        // CERRAR SESIÓN ACTUAL
        // ==========================================
        public Task CerrarSesionAsync() => _administradorInicioSesion.SignOutAsync();
    }
}
