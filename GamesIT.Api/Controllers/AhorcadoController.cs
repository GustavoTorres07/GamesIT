using System.Security.Claims;
using GamesIT.Api.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GamesIT.Api.Controladores;

public sealed record IntentarLetraAhorcadoDto(string Letra);

[ApiController]
[Authorize]
[Route("api/juegos/ahorcado")]
public sealed class AhorcadoController : ControllerBase
{
    private readonly ServicioGeneracionIA _generacion;
    private readonly ServicioPartidasAhorcado _partidas;
    private readonly ILogger<AhorcadoController> _logger;

    public AhorcadoController(
        ServicioGeneracionIA generacion,
        ServicioPartidasAhorcado partidas,
        ILogger<AhorcadoController> logger)
    {
        _generacion = generacion;
        _partidas = partidas;
        _logger = logger;
    }

    private Guid UsuarioId()
    {
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id : Guid.Empty;
    }

    // Limitar las generaciones (OpenRouter cuesta dinero), NO las letras de juego.
    [HttpPost("generar")]
    [EnableRateLimiting("generacion-ahorcado")]
    [ProducesResponseType(typeof(EstadoAhorcadoDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GenerarAsync(CancellationToken ct)
    {
        Guid usuarioId = UsuarioId();
        if (usuarioId == Guid.Empty) return Unauthorized();
        Response.Headers["Cache-Control"] = "no-store";

        try
        {
            DesafioAhorcadoGenerado generado =
                await _generacion.GenerarAhorcadoAsync(usuarioId, ct);

            // La palabra secreta permanece SOLO en SQL Server.
            var estado = await _partidas.ObtenerAsync(usuarioId, generado.PartidaId, ct);
            return estado is null ? Problem(statusCode: 500) : Ok(estado);
        }
        catch (KeyNotFoundException)
        {
            return Conflict(new { mensaje = "La cuenta no tiene jugador asociado." });
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Se agotó el tiempo al generar Ahorcado.");
            return Problem(title: "Tiempo de espera agotado", statusCode: 504);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error al consultar OpenRouter.");
            return Problem(title: "Servicio de IA no disponible", statusCode: 502);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Desafío de Ahorcado inválido.");
            return Problem(title: "No se pudo generar el desafío", statusCode: 502);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "No fue posible persistir el desafío.");
            return Problem(title: "No se pudo guardar el desafío", statusCode: 503);
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "Error SQL durante la generación de Ahorcado.");
            return Problem(title: "Base de datos no disponible", statusCode: 503);
        }
    }

    // Permite recuperar la partida desde SQL Server, por ejemplo tras un 409.
    [HttpGet("partidas/{partidaId:guid}")]
    [ProducesResponseType(typeof(EstadoAhorcadoDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> EstadoAsync(Guid partidaId, CancellationToken ct)
    {
        Guid usuarioId = UsuarioId();
        if (usuarioId == Guid.Empty) return Unauthorized();
        Response.Headers["Cache-Control"] = "no-store";
        var estado = await _partidas.ObtenerAsync(usuarioId, partidaId, ct);
        return estado is null ? NotFound() : Ok(estado);
    }

    [HttpPost("partidas/{partidaId:guid}/letras")]
    [ProducesResponseType(typeof(EstadoAhorcadoDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> IntentarAsync(
        Guid partidaId, [FromBody] IntentarLetraAhorcadoDto datos,
        CancellationToken ct)
    {
        Guid usuarioId = UsuarioId();
        if (usuarioId == Guid.Empty) return Unauthorized();
        Response.Headers["Cache-Control"] = "no-store";

        try
        {
            var estado = await _partidas.IntentarLetraAsync(
                usuarioId, partidaId, datos.Letra, ct);
            return estado is null ? NotFound() : Ok(estado);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
        catch (DbUpdateConcurrencyException)
        {
            // Otra solicitud actualizó esta partida. El cliente debe
            // recuperar GET /partidas/{id} antes de intentar de nuevo.
            return Conflict(new
            {
                mensaje = "La partida cambió. Recuperá su estado y reintentá."
            });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Fallo al registrar una jugada de Ahorcado.");
            return Problem(title: "No se pudo registrar la jugada", statusCode: 503);
        }
    }

    // Confirmar rendición voluntaria cuenta como derrota.
    [HttpPost("partidas/{partidaId:guid}/rendirse")]
    [ProducesResponseType(typeof(EstadoAhorcadoDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> RendirseAsync(Guid partidaId, CancellationToken ct)
    {
        Guid usuarioId = UsuarioId();
        if (usuarioId == Guid.Empty) return Unauthorized();
        Response.Headers["Cache-Control"] = "no-store";
        try
        {
            var estado = await _partidas.RendirseAsync(usuarioId, partidaId, ct);
            return estado is null ? NotFound() : Ok(estado);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { mensaje = "La partida cambió. Recuperá el estado actual." });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Error al rendir partida de Ahorcado.");
            return Problem(title: "No se pudo finalizar la partida", statusCode: 503);
        }
    }

    // Latido periódico mientras la partida permanece abierta y visible.
    [HttpPost("partidas/{partidaId:guid}/actividad")]
    [ProducesResponseType(typeof(EstadoAhorcadoDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ActividadAsync(Guid partidaId, CancellationToken ct)
    {
        Guid usuarioId = UsuarioId();
        if (usuarioId == Guid.Empty) return Unauthorized();
        Response.Headers["Cache-Control"] = "no-store";
        var estado = await _partidas.RenovarActividadAsync(usuarioId, partidaId, ct);
        return estado is null ? NotFound() : Ok(estado);
    }

}
