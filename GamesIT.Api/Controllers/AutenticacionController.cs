using GamesIT.Api.DTOs.Autenticacion;
using GamesIT.Api.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GamesIT.Api.Controllers
{
    [ApiController]
    [Route("api/autenticacion")]
    public class AutenticacionController : ControllerBase
    {
        private readonly ServicioAutenticacion _servicio;
        private readonly ServicioRegistroPorCorreo _registro;
        private readonly ServicioCuentas _cuentas;

        public AutenticacionController(ServicioAutenticacion servicio,
            ServicioRegistroPorCorreo registro, ServicioCuentas cuentas)
        {
            _servicio = servicio;
            _registro = registro;
            _cuentas = cuentas;
        }

        // Fase 1: crea solicitud temporal y envía código; NO crea una cuenta.
        [AllowAnonymous]
        [HttpPost("registro")]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("registro")]
        public async Task<IActionResult> SolicitarRegistro([FromBody] RegistroUsuarioDTO datos)
        {
            var resultado = await _registro.SolicitarAsync(datos);
            if (resultado.Correcto)
                return Accepted(new { mensaje = resultado.Mensaje, solicitudId = resultado.SolicitudId });
            return resultado.CodigoError == "duplicado"
                ? Conflict(new { mensaje = resultado.Mensaje })
                : BadRequest(new { mensaje = resultado.Mensaje });
        }

        // Fase 2: valida los seis dígitos, crea AspNetUsers + Jugadores.
        [AllowAnonymous]
        [HttpPost("confirmar-registro")]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("verificacion")]
        public async Task<IActionResult> ConfirmarRegistro([FromBody] ConfirmarRegistroDTO datos)
        {
            var resultado = await _registro.ConfirmarAsync(datos);
            if (resultado.Correcto) return StatusCode(201, new { mensaje = resultado.Mensaje });
            return BadRequest(new { mensaje = resultado.Mensaje });
        }

        [AllowAnonymous]
        [HttpPost("reenviar-codigo")]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("registro")]
        public async Task<IActionResult> ReenviarCodigo([FromBody] ReenviarCodigoDTO datos)
        {
            var resultado = await _registro.ReenviarAsync(datos.SolicitudId);
            if (resultado.Correcto) return Ok(new { mensaje = resultado.Mensaje });
            return BadRequest(new { mensaje = resultado.Mensaje });
        }

        [AllowAnonymous]
        [HttpPost("iniciar-sesion")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> IniciarSesion([FromBody] InicioSesionDTO datos)
        {
            var resultado = await _servicio.IniciarSesionAsync(datos);
            if (resultado.Succeeded)
                return Ok(new { mensaje = "Inicio de sesión exitoso.", exitoso = true });

            return Unauthorized(new
            {
                mensaje = "No se pudo iniciar sesión. Verificá tus credenciales o intentá nuevamente más tarde.",
                exitoso = false
            });
        }

        [Authorize]
        [HttpGet("mi-perfil")]
        [ProducesResponseType(typeof(PerfilAutenticadoDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ObtenerMiPerfil()
        {
            var perfil = await _servicio.ObtenerPerfilAsync(User);
            if (perfil == null)
                return Unauthorized(new { mensaje = "No se pudo validar la sesión.", exitoso = false });

            return Ok(perfil);
        }

        [Authorize]
        [HttpPost("avatar")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CambiarAvatar([FromBody] SeleccionarAvatarDTO datos)
        {
            bool actualizado = await _servicio.CambiarAvatarAsync(User, datos.AvatarId);
            if (!actualizado)
                return Unauthorized(new { mensaje = "No se pudo actualizar el perfil." });

            return Ok(new { mensaje = "Avatar actualizado correctamente.", avatarId = datos.AvatarId });
        }

        [Authorize]
        [HttpPost("favorito")]
        public async Task<IActionResult> ModificarFavorito([FromBody] ModificarFavoritoDTO datos)
        {
            bool correcto = await _servicio.ModificarFavoritoAsync(User, datos.JuegoId, datos.Favorito);
            return correcto ? Ok(new { mensaje = "Favoritos actualizados." })
                : BadRequest(new { mensaje = "No se pudo actualizar el favorito." });
        }

        // Eliminación irreversible de la cuenta del propio jugador.
        // Se verifica identidad desde la cookie, contraseña y confirmación explícita.
        [Authorize]
        [HttpPost("eliminar-mi-cuenta")]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("eliminacion")]
        public async Task<IActionResult> EliminarMiCuenta([FromBody] EliminarCuentaDTO datos)
        {
            var resultado = await _cuentas.EliminarMiCuentaAsync(User, datos);
            if (resultado.Correcto) return Ok(new { mensaje = resultado.Mensaje });
            return resultado.Codigo switch
            {
                "no_autorizado" => Unauthorized(new { mensaje = resultado.Mensaje }),
                "prohibido" => StatusCode(403, new { mensaje = resultado.Mensaje }),
                "conflicto" => Conflict(new { mensaje = resultado.Mensaje }),
                _ => BadRequest(new { mensaje = resultado.Mensaje })
            };
        }

        [Authorize]
        [HttpPost("cerrar-sesion")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CerrarSesion()
        {
            // El filtro global CSRF de Program.cs valida el token antes de entrar aquí.
            await _servicio.CerrarSesionAsync();
            return Ok(new { mensaje = "La sesión se cerró correctamente.", exitoso = true });
        }
    }
}
