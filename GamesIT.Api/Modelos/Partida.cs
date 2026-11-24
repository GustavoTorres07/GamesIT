namespace GamesIT.Api.Modelos;

// Los valores se almacenan como texto en SQL Server.
// No cambiar sus nombres sin una migración de datos.
public enum TipoJuegoPartida
{
    Ahorcado = 1,
    SopaDeLetras = 2,
    Crucigrama = 3
}

public enum EstadoPartida
{
    EnCurso = 0,
    Ganada = 1,
    Perdida = 2,
    Abandonada = 3
}

/// <summary>
/// Partida de un jugador. Por ahora define únicamente la persistencia.
/// Los resultados y contadores se actualizarán y verificarán desde la API.
/// </summary>
public sealed class Partida
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Relación con Jugadores.Id (no con AspNetUsers.Id).
    public Guid JugadorId { get; set; }

    public TipoJuegoPartida TipoJuego { get; set; }
    public EstadoPartida Estado { get; set; } = EstadoPartida.EnCurso;

    // Obligatorio para Ahorcado; nulo en los otros juegos.
    // Relación con HistorialDesafiosAhorcado.Id.
    public Guid? DesafioAhorcadoId { get; set; }

    public DateTime FechaInicioUtc { get; set; } = DateTime.UtcNow;
    public DateTime? FechaFinalizacionUtc { get; set; }

    // Se renueva periódicamente mientras el jugador sigue en pantalla.
    // Permite detectar desconexiones y cierres sin confiar en eventos del navegador.
    public DateTime UltimaActividadUtc { get; set; } = DateTime.UtcNow;

    // victoria, errores, rendicion o inactividad.
    public string? MotivoFinalizacion { get; set; }

    // Secuencia de letras únicas elegidas durante la partida (A-Z, Ñ).
    // Los intentos se validan y se agregan EXCLUSIVAMENTE en la API.
    public string LetrasIntentadas { get; set; } = string.Empty;

    public int CantidadIntentos { get; set; }
    public int CantidadErrores { get; set; }
    public int? DuracionSegundos { get; set; }

    // Concurrencia optimista: evita sobrescribir resultados simultáneos.
    public byte[] Version { get; set; } = Array.Empty<byte>();
}
