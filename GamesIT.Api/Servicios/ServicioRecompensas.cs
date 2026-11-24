using GamesIT.Api.Datos;
using GamesIT.Api.Modelos;
using Microsoft.EntityFrameworkCore;

namespace GamesIT.Api.Servicios;

/// <summary>
/// Acredita recompensas exclusivamente a resultados ya verificados en la API.
/// IMPORTANTE: el llamador debe iniciar una transacción de SQL Server antes
/// de finalizar la partida y mantenerla abierta hasta completar este método.
/// </summary>
public sealed class ServicioRecompensas
{
    private readonly ContextoGamesIT _db;

    public ServicioRecompensas(ContextoGamesIT db) => _db = db;

    /// <summary>EXP total mínima acumulada para alcanzar un nivel.</summary>
    private static decimal UmbralNivel(int nivel) =>
        15m * nivel * nivel + 105m * nivel;

    // Los umbrales reales de los jugadores caben en long hasta niveles
    // inmensamente superiores a los que se pueden alcanzar jugando.
    public static long ExperienciaAcumuladaMinima(int nivel)
    {
        if (nivel < 0) throw new ArgumentOutOfRangeException(nameof(nivel));
        decimal valor = UmbralNivel(nivel);
        return valor >= long.MaxValue ? long.MaxValue : (long)valor;
    }

    public static int CalcularNivel(long experienciaTotal)
    {
        if (experienciaTotal <= 0) return 0;
        int menor = 0;
        int mayor = int.MaxValue;
        while (menor < mayor)
        {
            int medio = menor + (int)(((long)mayor - menor + 1) / 2);
            if (UmbralNivel(medio) <= experienciaTotal)
                menor = medio;
            else
                mayor = medio - 1;
        }
        return menor;
    }

    private static long BonificacionNiveles(int anterior, int nuevo)
    {
        long niveles = (long)nuevo - anterior;
        long multiplos5 = nuevo / 5 - anterior / 5;
        long multiplos10 = nuevo / 10 - anterior / 10;
        return checked(niveles * 10L + multiplos5 * 40L + multiplos10 * 80L);
    }

    public async Task<RecompensaPartida?> AcreditarAsync(
        Partida partida, CancellationToken ct = default)
    {
        if (_db.Database.CurrentTransaction is null)
            throw new InvalidOperationException(
                "La acreditación requiere una transacción SQL activa.");

        if (partida.TipoJuego != TipoJuegoPartida.Ahorcado ||
            partida.Estado is not (EstadoPartida.Ganada or EstadoPartida.Perdida))
            return null;

        // Rendiciones e inactividad no otorgan recompensas.
        if (partida.MotivoFinalizacion is not ("victoria" or "errores"))
            return null;

        // Bloqueo de la fila del jugador hasta COMMIT: serializa acreditaciones
        // simultáneas de distintas partidas y evita perder actualizaciones.
        Jugador jugador = await _db.Jugadores
            .FromSqlInterpolated($"SELECT * FROM dbo.Jugadores WITH (UPDLOCK, HOLDLOCK) WHERE Id = {partida.JugadorId}")
            .SingleAsync(ct);

        // Comprobar después de adquirir el bloqueo.
        bool yaAcreditada = await _db.RecompensasPartidas
            .AsNoTracking().AnyAsync(r => r.PartidaId == partida.Id, ct);
        if (yaAcreditada)
            return null;

        DateTime ahora = DateTime.UtcNow;
        bool victoria = partida.Estado == EstadoPartida.Ganada;
        bool perfecta = victoria && partida.CantidadErrores == 0;

        long exp;
        long bitsBase;
        long puntos;

        if (victoria)
        {
            exp = perfecta ? 150L : 100L;
            bitsBase = perfecta ? 45L : 30L;
            puntos = 500L + 75L * Math.Max(0, 6 - partida.CantidadErrores);
        }
        else
        {
            // Únicamente las primeras cinco derrotas por errores de cada día UTC
            // reciben 10 EXP. Se consulta el libro de recompensas, no partidas
            // aún sin confirmar, para evitar lecturas de filas concurrentes.
            DateTime inicioDia = ahora.Date;
            DateTime finDia = inicioDia.AddDays(1);
            int derrotasPremiadas = await _db.RecompensasPartidas
                .AsNoTracking()
                .CountAsync(r => r.JugadorId == partida.JugadorId &&
                    r.Motivo == "errores" && r.ExperienciaOtorgada > 0 &&
                    r.FechaUtc >= inicioDia && r.FechaUtc < finDia, ct);
            exp = derrotasPremiadas < 5 ? 10L : 0L;
            bitsBase = 0;
            puntos = 0;
        }

        int nivelAnterior = CalcularNivel(jugador.Experiencia);
        long experienciaNueva = checked(jugador.Experiencia + exp);
        int nivelNuevo = CalcularNivel(experienciaNueva);
        long bitsPorNivel = BonificacionNiveles(nivelAnterior, nivelNuevo);
        long bitsTotal = checked(bitsBase + bitsPorNivel);

        // El progreso se actualiza en la MISMA transacción que cierra
        // la partida. Los contadores se incrementan una sola vez.
        jugador.Experiencia = experienciaNueva;
        jugador.Nivel = nivelNuevo;
        jugador.Bits = checked(jugador.Bits + bitsTotal);
        jugador.PartidasJugadas = checked(jugador.PartidasJugadas + 1);
        if (victoria)
            jugador.PartidasGanadas = checked(jugador.PartidasGanadas + 1);

        var comprobante = new RecompensaPartida
        {
            PartidaId = partida.Id,
            JugadorId = partida.JugadorId,
            Motivo = partida.MotivoFinalizacion!,
            ExperienciaOtorgada = exp,
            BitsBase = bitsBase,
            BitsPorNiveles = bitsPorNivel,
            BitsTotales = bitsTotal,
            PuntosOtorgados = puntos,
            NivelAnterior = nivelAnterior,
            NivelNuevo = nivelNuevo,
            FechaUtc = ahora
        };

        _db.RecompensasPartidas.Add(comprobante);
        await _db.SaveChangesAsync(ct);
        return comprobante;
    }
}
