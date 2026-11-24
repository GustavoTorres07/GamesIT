
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GamesIT.Api.Controllers
{
    [ApiController]
    [Route("api/seguridad")]
    public class SeguridadController : ControllerBase
    {
        // ==========================================
        // DEPENDENCIAS
        // ==========================================

        private readonly IAntiforgery _antifalsificacion;

        // ==========================================
        // CONSTRUCTOR
        // ==========================================

        public SeguridadController(
            IAntiforgery antifalsificacion)
        {
            _antifalsificacion = antifalsificacion;
        }

        // ==========================================
        // OBTENER TOKEN CSRF
        // GET: api/seguridad/token-csrf
        // ==========================================

        [AllowAnonymous]
        [HttpGet("token-csrf")]
        [ResponseCache(
            NoStore = true,
            Location = ResponseCacheLocation.None
        )]
        [ProducesResponseType(
            StatusCodes.Status200OK
        )]
        public IActionResult ObtenerTokenCsrf()
        {
            // ======================================
            // GENERAR Y ALMACENAR TOKENS
            // ======================================

            var tokens = _antifalsificacion
                .GetAndStoreTokens(HttpContext);

            // ======================================
            // DEVOLVER TOKEN DE SOLICITUD
            // ======================================

            return Ok(new
            {
                token = tokens.RequestToken
            });
        }
    }
}
