using System.Security.Claims;
using GamesIT.Api.Datos;
using GamesIT.Api.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GamesIT.Api.Controladores;

[ApiController]
[Authorize]
[Route("api/progresion")]
public sealed class ProgresionController : ControllerBase
{
    private readonly ContextoGamesIT _db;
    public ProgresionController(ContextoGamesIT db) => _db = db;

    private Guid UsuarioId() => Guid.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : Guid.Empty;

    // Estado persistente para el futuro panel de EXP, Bits y puntos.
    [HttpGet("mi-resumen")]
    public async Task<IActionResult> MiResumen(CancellationToken ct)
    {
        Guid usuarioId = UsuarioId();
        if (usuarioId == Guid.Empty) return Unauthorized();
        Response.Headers["Cache-Control"] = "no-store";

        var jugador = await _db.Jugadores.AsNoTracking()
            .Where(j => j.UsuarioId == usuarioId)
            .Select(j => new
            {
                j.Id,
                j.Nivel,
                j.Experiencia,
                j.Bits,
                j.PartidasJugadas,
                j.PartidasGanadas
            })
            .SingleOrDefaultAsync(ct);
        if (jugador is null) return NotFound();

        long puntos = await _db.RecompensasPartidas.AsNoTracking()
            .Where(r => r.JugadorId == jugador.Id)
            .Select(r => (long?)r.PuntosOtorgados)
            .SumAsync(ct) ?? 0L;

        return Ok(new
        {
            nivel = jugador.Nivel,
            experiencia = jugador.Experiencia,
            bits = jugador.Bits,
            puntos,
            partidasJugadas = jugador.PartidasJugadas,
            partidasGanadas = jugador.PartidasGanadas,
            experienciaRequeridaNivelActual =
                ServicioRecompensas.ExperienciaAcumuladaMinima(jugador.Nivel),
            experienciaRequeridaSiguienteNivel =
                ServicioRecompensas.ExperienciaAcumuladaMinima(
                    checked(jugador.Nivel + 1))
        });
    }

    // El comprobante solo se entrega al dueño de la partida.
    // Una rendición o inactividad devuelve 204 (sin recompensa).
    [HttpGet("partidas/{partidaId:guid}/recompensa")]
    public async Task<IActionResult> Recompensa(
        Guid partidaId, CancellationToken ct)
    {
        Guid usuarioId = UsuarioId();
        if (usuarioId == Guid.Empty) return Unauthorized();
        Response.Headers["Cache-Control"] = "no-store";

        bool pertenece = await _db.Partidas.AsNoTracking().AnyAsync(p =>
            p.Id == partidaId && _db.Jugadores.Any(j =>
                j.Id == p.JugadorId && j.UsuarioId == usuarioId), ct);

        if (!pertenece) return NotFound();

        var comprobante = await _db.RecompensasPartidas.AsNoTracking()
            .Where(r => r.PartidaId == partidaId)
            .Select(r => new
            {
                r.PartidaId,
                r.Motivo,
                r.ExperienciaOtorgada,
                r.BitsBase,
                r.BitsPorNiveles,
                r.BitsTotales,
                r.PuntosOtorgados,
                r.NivelAnterior,
                r.NivelNuevo,
                r.FechaUtc
            })
            .SingleOrDefaultAsync(ct);

        return comprobante is null ? NoContent() : Ok(comprobante);
    }
}
