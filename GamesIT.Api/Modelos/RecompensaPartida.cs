namespace GamesIT.Api.Modelos;

/// <summary>
/// Comprobante inmutable de EXP, Bits y puntos acreditados por una partida.
/// La restricción única de PartidaId impide registrar dos recompensas
/// para la misma partida, incluso frente a solicitudes concurrentes.
/// </summary>
public sealed class RecompensaPartida
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PartidaId { get; set; }
    public Guid JugadorId { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public long ExperienciaOtorgada { get; set; }
    public long BitsBase { get; set; }
    public long BitsPorNiveles { get; set; }
    public long BitsTotales { get; set; }
    public long PuntosOtorgados { get; set; }
    public int NivelAnterior { get; set; }
    public int NivelNuevo { get; set; }
    public DateTime FechaUtc { get; set; } = DateTime.UtcNow;
}
