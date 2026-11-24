using System.Security.Claims;
using GamesIT.Api.Datos;
using GamesIT.Api.Modelos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GamesIT.Api.Controladores;

/// <summary>
/// EXP es global. Los puntos solo tienen significado dentro de su juego.
/// Los rankings competitivos suman las 10 mejores partidas ganadas de cada
/// jugador (histórico, hasta que se incorporen las temporadas).
/// </summary>
[ApiController]
[Route("api/estadisticas")]
public sealed class EstadisticasController : ControllerBase
{
    private readonly ContextoGamesIT _db;

    public EstadisticasController(ContextoGamesIT db) => _db = db;

    private static string NombreJuego(TipoJuegoPartida tipo) => tipo switch
    {
        TipoJuegoPartida.Ahorcado => "ahorcado",
        TipoJuegoPartida.SopaDeLetras => "sopa",
        TipoJuegoPartida.Crucigrama => "crucigrama",
        _ => "desconocido"
    };

    private static string NombreEstado(EstadoPartida estado) => estado switch
    {
        EstadoPartida.EnCurso => "en_curso",
        EstadoPartida.Ganada => "ganada",
        EstadoPartida.Perdida => "perdida",
        EstadoPartida.Abandonada => "abandonada",
        _ => "desconocido"
    };

    private static bool IntentarLeerJuego(string? juego, out TipoJuegoPartida tipo)
    {
        tipo = juego?.Trim().ToLowerInvariant() switch
        {
            "ahorcado" => TipoJuegoPartida.Ahorcado,
            "sopa" => TipoJuegoPartida.SopaDeLetras,
            "crucigrama" => TipoJuegoPartida.Crucigrama,
            _ => default
        };
        return juego is not null &&
            (juego.Equals("ahorcado", StringComparison.OrdinalIgnoreCase) ||
             juego.Equals("sopa", StringComparison.OrdinalIgnoreCase) ||
             juego.Equals("crucigrama", StringComparison.OrdinalIgnoreCase));
    }

    private Guid UsuarioId() => Guid.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier), out Guid id)
        ? id : Guid.Empty;

    // GET /api/estadisticas/mis-partidas?limite=6&pagina=1
    // Solo consulta el segmento solicitado. La totalidad del historial permanece en SQL Server.
    [Authorize]
    [HttpGet("mis-partidas")]
    public async Task<IActionResult> MisPartidas(
        [FromQuery] int limite = 6, [FromQuery] int pagina = 1,
        CancellationToken ct = default)
    {
        Guid usuarioId = UsuarioId();
        if (usuarioId == Guid.Empty) return Unauthorized();
        Response.Headers["Cache-Control"] = "no-store";
        limite = Math.Clamp(limite, 1, 30);
        pagina = Math.Max(1, pagina);

        Guid jugadorId = await _db.Jugadores.AsNoTracking()
            .Where(j => j.UsuarioId == usuarioId)
            .Select(j => j.Id)
            .SingleOrDefaultAsync(ct);
        if (jugadorId == Guid.Empty) return NotFound();

        var consulta = _db.Partidas.AsNoTracking()
            .Where(p => p.JugadorId == jugadorId);

        int totalPartidas = await consulta.CountAsync(ct);
        int totalPaginas = Math.Max(1, (int)Math.Ceiling((double)totalPartidas / limite));
        pagina = Math.Min(pagina, totalPaginas);

        var partidasDb = await consulta
            .OrderByDescending(p => p.FechaInicioUtc)
            .ThenByDescending(p => p.Id)
            .Skip((pagina - 1) * limite)
            .Take(limite)
            .Select(p => new
            {
                p.Id,
                p.TipoJuego,
                p.Estado,
                p.MotivoFinalizacion,
                p.FechaInicioUtc,
                p.FechaFinalizacionUtc,
                p.DuracionSegundos,
                p.CantidadErrores,
                p.CantidadIntentos
            })
            .ToListAsync(ct);

        // Los puntos se obtienen del comprobante persistido, jamás del cliente.
        Guid[] partidaIds = partidasDb.Select(p => p.Id).ToArray();
        var puntosPorPartida = await _db.RecompensasPartidas.AsNoTracking()
            .Where(r => partidaIds.Contains(r.PartidaId))
            .Select(r => new { r.PartidaId, r.PuntosOtorgados })
            .ToDictionaryAsync(r => r.PartidaId, r => r.PuntosOtorgados, ct);

        // Lista de juegos utilizados (máximo los tipos de juegos registrados),
        // calculada en SQL Server, sin asignar cifras ficticias desde el frontend.
        var juegosDb = await consulta
            .GroupBy(p => p.TipoJuego)
            .Select(g => new
            {
                TipoJuego = g.Key,
                UltimaPartidaUtc = g.Max(p => p.FechaInicioUtc),
                Partidas = g.Count(),
                Victorias = g.Count(p => p.Estado == EstadoPartida.Ganada)
            })
            .ToListAsync(ct);

        var partidas = partidasDb.Select(p => new
        {
            partidaId = p.Id,
            tipoJuego = NombreJuego(p.TipoJuego),
            estado = NombreEstado(p.Estado),
            motivoFinalizacion = p.MotivoFinalizacion,
            fechaInicioUtc = p.FechaInicioUtc,
            fechaFinalizacionUtc = p.FechaFinalizacionUtc,
            duracionSegundos = p.DuracionSegundos,
            cantidadErrores = p.CantidadErrores,
            cantidadIntentos = p.CantidadIntentos,
            puntosOtorgados = puntosPorPartida.GetValueOrDefault(p.Id, 0L)
        }).ToArray();

        var ultimosJuegos = juegosDb
            .OrderByDescending(j => j.UltimaPartidaUtc)
            .Select(j => new
            {
                tipoJuego = NombreJuego(j.TipoJuego),
                ultimaPartidaUtc = j.UltimaPartidaUtc,
                partidas = j.Partidas,
                victorias = j.Victorias
            }).ToArray();

        return Ok(new
        {
            totalPartidas,
            pagina,
            limite,
            totalPaginas,
            tieneAnterior = pagina > 1,
            tieneSiguiente = pagina < totalPaginas,
            partidas,
            ultimosJuegos
        });
    }

    // GET /api/estadisticas/mis-puntajes
    // Un tablero por juego; el TOP 10 del propio usuario es histórico.
    [Authorize]
    [HttpGet("mis-puntajes")]
    public async Task<IActionResult> MisPuntajes(CancellationToken ct)
    {
        Guid usuarioId = UsuarioId();
        if (usuarioId == Guid.Empty) return Unauthorized();
        Response.Headers["Cache-Control"] = "no-store";

        Guid jugadorId = await _db.Jugadores.AsNoTracking()
            .Where(j => j.UsuarioId == usuarioId)
            .Select(j => j.Id)
            .SingleOrDefaultAsync(ct);
        if (jugadorId == Guid.Empty) return NotFound();

        var partidas = await _db.Partidas.AsNoTracking()
            .Where(p => p.JugadorId == jugadorId)
            .GroupBy(p => p.TipoJuego)
            .Select(g => new
            {
                Tipo = g.Key,
                Jugadas = g.Count(),
                Ganadas = g.Count(p => p.Estado == EstadoPartida.Ganada)
            })
            .ToListAsync(ct);

        var resultados = new List<object>();
        foreach (var tipo in new[]
        {
            TipoJuegoPartida.Ahorcado,
            TipoJuegoPartida.SopaDeLetras,
            TipoJuegoPartida.Crucigrama
        })
        {
            // Una recompensa = una partida. No otorgamos puntos por derrotas.
            var puntuaciones = _db.RecompensasPartidas.AsNoTracking()
                .Join(_db.Partidas.AsNoTracking(),
                    r => r.PartidaId, p => p.Id, (r, p) => new { r, p })
                .Where(x => x.r.JugadorId == jugadorId &&
                            x.p.TipoJuego == tipo &&
                            x.p.Estado == EstadoPartida.Ganada &&
                            x.r.PuntosOtorgados > 0)
                .OrderByDescending(x => x.r.PuntosOtorgados)
                .ThenBy(x => x.r.FechaUtc)
                .Take(10)
                .Select(x => x.r.PuntosOtorgados);

            long[] mejores = await puntuaciones.ToArrayAsync(ct);
            var estadistica = partidas.FirstOrDefault(p => p.Tipo == tipo);
            resultados.Add(new
            {
                tipoJuego = NombreJuego(tipo),
                partidas = estadistica?.Jugadas ?? 0,
                victorias = estadistica?.Ganadas ?? 0,
                puntosTop10 = mejores.Sum(),
                mejorPuntaje = mejores.Length > 0 ? mejores[0] : 0L,
                partidasPuntuadas = mejores.Length
            });
        }

        return Ok(new { juegos = resultados, criterio = "mejores_10_partidas_historicas" });
    }

    // GET /api/estadisticas/clasificacion?limite=10
    // GET /api/estadisticas/clasificacion?juego=ahorcado&limite=10
    // 'global' mide EXP. Las otras opciones solo comparan puntos del juego.
    [AllowAnonymous]
    [HttpGet("clasificacion")]
    public async Task<IActionResult> Clasificacion(
        [FromQuery] string juego = "global", [FromQuery] int limite = 10,
        CancellationToken ct = default)
    {
        Response.Headers["Cache-Control"] = "no-store";
        limite = Math.Clamp(limite, 1, 20);
        string modo = juego.Trim().ToLowerInvariant();

        if (modo == "global")
        {
            var topGlobal = await _db.Jugadores.AsNoTracking()
                .Where(j => j.Usuario.EstaActivo)
                .OrderByDescending(j => j.Experiencia)
                .ThenByDescending(j => j.PartidasGanadas)
                .ThenBy(j => j.NombreNormalizado)
                .Take(limite)
                .Select(j => new
                {
                    j.NombreJugador,
                    j.Nivel,
                    j.Experiencia,
                    j.PartidasGanadas
                })
                .ToListAsync(ct);

            var jugadores = topGlobal.Select((j, indice) => new
            {
                posicion = indice + 1,
                nombreJugador = j.NombreJugador,
                nivel = j.Nivel,
                experiencia = j.Experiencia,
                partidasGanadas = j.PartidasGanadas
            }).ToArray();

            return Ok(new { criterio = "experiencia_total", juego = "global", jugadores });
        }

        if (!IntentarLeerJuego(modo, out var tipo))
            return BadRequest(new { mensaje = "Juego inválido. Usá global, ahorcado, crucigrama o sopa." });

        // Subconsulta correlacionada: diez mejores puntuaciones de ESE juego.
        // El proveedor EF Core SQL Server debe traducir Take + Sum a SQL.
        var topJuego = await _db.Jugadores.AsNoTracking()
            .Where(j => j.Usuario.EstaActivo)
            .Select(j => new
            {
                j.NombreJugador,
                j.NombreNormalizado,
                j.Nivel,
                j.Experiencia,
                VictoriasJuego = _db.Partidas.AsNoTracking().Count(p =>
                    p.JugadorId == j.Id && p.TipoJuego == tipo &&
                    p.Estado == EstadoPartida.Ganada),
                PuntosTop10 = _db.RecompensasPartidas.AsNoTracking()
                    .Where(r => r.JugadorId == j.Id && r.PuntosOtorgados > 0 &&
                        _db.Partidas.Any(p => p.Id == r.PartidaId &&
                            p.TipoJuego == tipo && p.Estado == EstadoPartida.Ganada))
                    .OrderByDescending(r => r.PuntosOtorgados)
                    .ThenBy(r => r.FechaUtc)
                    .Take(10)
                    .Sum(r => (long?)r.PuntosOtorgados) ?? 0L
            })
            .Where(j => j.PuntosTop10 > 0)
            .OrderByDescending(j => j.PuntosTop10)
            .ThenByDescending(j => j.VictoriasJuego)
            .ThenBy(j => j.NombreNormalizado)
            .Take(limite)
            .ToListAsync(ct);

        var top = topJuego.Select((j, indice) => new
        {
            posicion = indice + 1,
            nombreJugador = j.NombreJugador,
            nivel = j.Nivel,
            experiencia = j.Experiencia,
            victoriasJuego = j.VictoriasJuego,
            puntosJuego = j.PuntosTop10
        }).ToArray();

        return Ok(new { criterio = "mejores_10_partidas", juego = modo, jugadores = top });
    }
}
