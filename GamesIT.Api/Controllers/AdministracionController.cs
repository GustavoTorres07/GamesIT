using GamesIT.Api.DTOs.Administracion;
using GamesIT.Api.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GamesIT.Api.Controllers;

[ApiController]
[Authorize(Roles = InicializadorAdministrador.RolAdministrador)]
[Route("api/administracion")]
public class AdministracionController : ControllerBase
{
    private readonly ServicioCuentas _cuentas;

    public AdministracionController(ServicioCuentas cuentas) => _cuentas = cuentas;

    [HttpGet("jugadores")]
    public async Task<IActionResult> ListarJugadores(
        [FromQuery] string? busqueda = null,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanio = 20)
    {
        if (pagina < 1 || pagina > 1_000_000 || tamanio < 1 || tamanio > 50 ||
            (busqueda?.Length ?? 0) > 80)
            return BadRequest(new { mensaje = "Parámetros de búsqueda no válidos." });

        return Ok(await _cuentas.ListarJugadoresAsync(busqueda, pagina, tamanio));
    }

    [HttpPost("eliminar-jugador")]
    [EnableRateLimiting("eliminacion")]
    public async Task<IActionResult> EliminarJugador([FromBody] EliminarJugadorDTO datos)
    {
        var resultado = await _cuentas.EliminarJugadorAsync(User, datos);
        if (resultado.Correcto) return Ok(new { mensaje = resultado.Mensaje });
        return resultado.Codigo switch
        {
            "no_encontrado" => NotFound(new { mensaje = resultado.Mensaje }),
            "prohibido" => StatusCode(403, new { mensaje = resultado.Mensaje }),
            "conflicto" => Conflict(new { mensaje = resultado.Mensaje }),
            "contrasena" => BadRequest(new { mensaje = resultado.Mensaje }),
            _ => BadRequest(new { mensaje = resultado.Mensaje })
        };
    }
}
