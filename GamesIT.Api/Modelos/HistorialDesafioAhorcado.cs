namespace GamesIT.Api.Modelos;

/// <summary>
/// Desafío de Ahorcado generado y reservado para un jugador.
/// La palabra normalizada no puede repetirse para el mismo jugador.
/// </summary>
public sealed class HistorialDesafioAhorcado
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JugadorId { get; set; }
    public string PalabraNormalizada { get; set; } = string.Empty;
    public string Pista { get; set; } = string.Empty;
    public DateTime FechaGeneracionUtc { get; set; } = DateTime.UtcNow;
}
