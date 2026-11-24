using System.Text.Json.Serialization;
using GamesIT.Api.Datos;
using GamesIT.Api.Modelos;
using Microsoft.EntityFrameworkCore;

namespace GamesIT.Api.Servicios;

// La palabra secreta solo se devuelve cuando la partida ya no está en curso.
public sealed record EstadoAhorcadoDto(
    [property: JsonPropertyName("partidaId")] Guid PartidaId,
    [property: JsonPropertyName("hint")] string Hint,
    [property: JsonPropertyName("maskedWord")] string MaskedWord,
    [property: JsonPropertyName("guessedLetters")] string[] GuessedLetters,
    [property: JsonPropertyName("errors")] int Errors,
    [property: JsonPropertyName("remainingAttempts")] int RemainingAttempts,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("revealedWord")] string? RevealedWord,
    [property: JsonPropertyName("reason")] string? Reason
);

public sealed class ServicioPartidasAhorcado
{
    private const int MaximoErrores = 6;
    private static readonly TimeSpan TiempoSinActividad = TimeSpan.FromMinutes(5);
    private readonly ContextoGamesIT _db;
    private readonly ServicioRecompensas _recompensas;

    public ServicioPartidasAhorcado(
        ContextoGamesIT db, ServicioRecompensas recompensas)
    {
        _db = db;
        _recompensas = recompensas;
    }

    private async Task<(Partida Partida, HistorialDesafioAhorcado Desafio)?>
        CargarAsync(Guid usuarioId, Guid partidaId, CancellationToken ct)
    {
        Partida? partida = await _db.Partidas
            .Where(p => p.Id == partidaId &&
                        p.TipoJuego == TipoJuegoPartida.Ahorcado &&
                        _db.Jugadores.Any(j =>
                            j.Id == p.JugadorId && j.UsuarioId == usuarioId))
            .SingleOrDefaultAsync(ct);

        if (partida?.DesafioAhorcadoId is not Guid desafioId)
            return null;

        HistorialDesafioAhorcado? desafio = await _db.HistorialDesafiosAhorcado
            .AsNoTracking()
            .SingleOrDefaultAsync(h =>
                h.Id == desafioId && h.JugadorId == partida.JugadorId, ct);

        return desafio is null ? null : (partida, desafio);
    }

    private static EstadoAhorcadoDto CrearEstado(
        Partida partida, HistorialDesafioAhorcado desafio)
    {
        string letras = partida.LetrasIntentadas ?? string.Empty;
        string palabra = desafio.PalabraNormalizada;
        string mascara = new(palabra.Select(c => letras.Contains(c) ? c : '_').ToArray());
        string estado = partida.Estado switch
        {
            EstadoPartida.Ganada => "ganada",
            EstadoPartida.Perdida => "perdida",
            EstadoPartida.Abandonada => "abandonada",
            _ => "en_curso"
        };

        return new EstadoAhorcadoDto(
            partida.Id,
            desafio.Pista,
            mascara,
            letras.Select(c => c.ToString()).ToArray(),
            partida.CantidadErrores,
            Math.Max(0, MaximoErrores - partida.CantidadErrores),
            estado,
            partida.Estado == EstadoPartida.EnCurso ? null : palabra,
            partida.MotivoFinalizacion);
    }

    // Una expiración solo incrementa los contadores cuando el UPDATE
    // condicional realmente cambia EnCurso -> Abandonada. Es idempotente.
    private async Task<bool> VencerUnaAsync(Guid partidaId, CancellationToken ct)
    {
        DateTime ahora = DateTime.UtcNow;
        DateTime limite = ahora - TiempoSinActividad;
        var pendiente = await _db.Partidas.AsNoTracking()
            .Where(p => p.Id == partidaId &&
                        p.Estado == EstadoPartida.EnCurso &&
                        p.UltimaActividadUtc <= limite)
            .Select(p => new { p.Id, p.JugadorId, p.FechaInicioUtc })
            .SingleOrDefaultAsync(ct);

        if (pendiente is null)
            return false;

        int duracion = (int)Math.Clamp(
            (ahora - pendiente.FechaInicioUtc).TotalSeconds, 0, int.MaxValue);

        await using var transaccion = await _db.Database.BeginTransactionAsync(ct);
        int cambiadas = await _db.Partidas
            .Where(p => p.Id == partidaId &&
                        p.Estado == EstadoPartida.EnCurso &&
                        p.UltimaActividadUtc <= limite)
            .ExecuteUpdateAsync(cambios => cambios
                .SetProperty(p => p.Estado, EstadoPartida.Abandonada)
                .SetProperty(p => p.MotivoFinalizacion, "inactividad")
                .SetProperty(p => p.FechaFinalizacionUtc, ahora)
                .SetProperty(p => p.DuracionSegundos, duracion), ct);

        if (cambiadas == 1)
        {
            // Cuenta como partida jugada, NO como victoria.
            int contador = await _db.Jugadores
                .Where(j => j.Id == pendiente.JugadorId)
                .ExecuteUpdateAsync(cambios => cambios
                    .SetProperty(j => j.PartidasJugadas, j => j.PartidasJugadas + 1), ct);

            if (contador != 1)
                throw new InvalidOperationException(
                    "No se pudo actualizar el contador de partidas abandonadas.");
        }

        await transaccion.CommitAsync(ct);
        return cambiadas == 1;
    }

    // Verifica expiración también cuando el usuario vuelve a abrir la página,
    // sin esperar a la próxima ejecución del servicio de limpieza.
    private async Task<(Partida Partida, HistorialDesafioAhorcado Desafio)?>
        CargarVigenteAsync(Guid usuarioId, Guid partidaId, CancellationToken ct)
    {
        var datos = await CargarAsync(usuarioId, partidaId, ct);
        if (datos is null)
            return null;

        Partida partida = datos.Value.Partida;
        if (partida.Estado == EstadoPartida.EnCurso &&
            partida.UltimaActividadUtc <= DateTime.UtcNow - TiempoSinActividad)
        {
            _db.Entry(partida).State = EntityState.Detached;
            await VencerUnaAsync(partidaId, ct);
            datos = await CargarAsync(usuarioId, partidaId, ct);
        }
        return datos;
    }

    public async Task<EstadoAhorcadoDto?> ObtenerAsync(
        Guid usuarioId, Guid partidaId, CancellationToken ct = default)
    {
        var datos = await CargarVigenteAsync(usuarioId, partidaId, ct);
        return datos is null ? null : CrearEstado(datos.Value.Partida, datos.Value.Desafio);
    }

    private async Task ActualizarContadoresAsync(Partida partida, bool victoria, CancellationToken ct)
    {
        int actualizadas = await _db.Jugadores
            .Where(j => j.Id == partida.JugadorId)
            .ExecuteUpdateAsync(cambios => cambios
                .SetProperty(j => j.PartidasJugadas, j => j.PartidasJugadas + 1)
                .SetProperty(j => j.PartidasGanadas,
                    j => j.PartidasGanadas + (victoria ? 1 : 0)), ct);

        if (actualizadas != 1)
            throw new InvalidOperationException("No se pudo actualizar el contador del jugador.");
    }

    private static void Finalizar(Partida partida, EstadoPartida estado, string motivo)
    {
        partida.Estado = estado;
        partida.MotivoFinalizacion = motivo;
        partida.FechaFinalizacionUtc = DateTime.UtcNow;
        partida.DuracionSegundos = (int)Math.Clamp(
            (partida.FechaFinalizacionUtc.Value - partida.FechaInicioUtc)
                .TotalSeconds, 0, int.MaxValue);
    }

    public async Task<EstadoAhorcadoDto?> IntentarLetraAsync(
        Guid usuarioId, Guid partidaId, string letra, CancellationToken ct = default)
    {
        string valor = (letra ?? string.Empty).Trim().ToUpperInvariant();
        if (valor.Length != 1 || !(valor[0] is >= 'A' and <= 'Z' || valor[0] == 'Ñ'))
            throw new ArgumentException("Seleccioná una única letra de A a Z o Ñ.", nameof(letra));

        var datos = await CargarVigenteAsync(usuarioId, partidaId, ct);
        if (datos is null) return null;
        var (partida, desafio) = datos.Value;

        if (partida.Estado != EstadoPartida.EnCurso ||
            partida.LetrasIntentadas.Contains(valor, StringComparison.Ordinal))
            return CrearEstado(partida, desafio);

        await using var transaccion = await _db.Database.BeginTransactionAsync(ct);
        partida.LetrasIntentadas += valor;
        partida.CantidadIntentos++;
        partida.UltimaActividadUtc = DateTime.UtcNow;
        if (!desafio.PalabraNormalizada.Contains(valor, StringComparison.Ordinal))
            partida.CantidadErrores++;

        bool victoria = desafio.PalabraNormalizada.All(c => partida.LetrasIntentadas.Contains(c));
        bool derrota = partida.CantidadErrores >= MaximoErrores;
        if (victoria || derrota)
            Finalizar(partida, victoria ? EstadoPartida.Ganada : EstadoPartida.Perdida,
                victoria ? "victoria" : "errores");

        // RowVersion detecta jugadas o expiraciones simultáneas.
        await _db.SaveChangesAsync(ct);
        if (victoria || derrota)
            await _recompensas.AcreditarAsync(partida, ct);

        await transaccion.CommitAsync(ct);
        return CrearEstado(partida, desafio);
    }

    // El jugador se rinde explícitamente: DERROTA, no Abandonada.
    public async Task<EstadoAhorcadoDto?> RendirseAsync(
        Guid usuarioId, Guid partidaId, CancellationToken ct = default)
    {
        var datos = await CargarVigenteAsync(usuarioId, partidaId, ct);
        if (datos is null) return null;
        var (partida, desafio) = datos.Value;
        if (partida.Estado != EstadoPartida.EnCurso)
            return CrearEstado(partida, desafio); // Idempotente.

        await using var transaccion = await _db.Database.BeginTransactionAsync(ct);
        Finalizar(partida, EstadoPartida.Perdida, "rendicion");
        await _db.SaveChangesAsync(ct);
        await ActualizarContadoresAsync(partida, false, ct);
        await transaccion.CommitAsync(ct);
        return CrearEstado(partida, desafio);
    }

    // Se llama mientras el juego está visible: un cierre no puede enviar
    // más latidos, así que la partida termina por inactividad en el servidor.
    public async Task<EstadoAhorcadoDto?> RenovarActividadAsync(
        Guid usuarioId, Guid partidaId, CancellationToken ct = default)
    {
        var datos = await CargarVigenteAsync(usuarioId, partidaId, ct);
        if (datos is null) return null;
        if (datos.Value.Partida.Estado != EstadoPartida.EnCurso)
            return CrearEstado(datos.Value.Partida, datos.Value.Desafio);

        DateTime ahora = DateTime.UtcNow;
        DateTime limite = ahora - TiempoSinActividad;
        await _db.Partidas
            .Where(p => p.Id == partidaId &&
                        p.Estado == EstadoPartida.EnCurso &&
                        p.UltimaActividadUtc > limite)
            .ExecuteUpdateAsync(cambios => cambios
                .SetProperty(p => p.UltimaActividadUtc, ahora), ct);

        // ExecuteUpdate no sincroniza entidades que ya están seguidas por EF.
        _db.ChangeTracker.Clear();
        return await ObtenerAsync(usuarioId, partidaId, ct);
    }

    // Lo invoca un BackgroundService. Se procesan lotes para no cargar
    // toda la tabla en memoria; las carreras se resuelven en el UPDATE.
    public async Task<int> VencerInactivasAsync(CancellationToken ct = default)
    {
        DateTime limite = DateTime.UtcNow - TiempoSinActividad;
        Guid[] ids = await _db.Partidas.AsNoTracking()
            .Where(p => p.TipoJuego == TipoJuegoPartida.Ahorcado &&
                        p.Estado == EstadoPartida.EnCurso &&
                        p.UltimaActividadUtc <= limite)
            .OrderBy(p => p.UltimaActividadUtc)
            .Take(100)
            .Select(p => p.Id)
            .ToArrayAsync(ct);

        int vencidas = 0;
        foreach (Guid id in ids)
        {
            if (await VencerUnaAsync(id, ct))
                vencidas++;
        }
        return vencidas;
    }
}
