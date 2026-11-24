using System.ComponentModel.DataAnnotations;

namespace GamesIT.Api.Modelos;

// No representa una cuenta: se elimina al confirmar el código.
public class RegistroPendiente
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required, MaxLength(254)] public string CorreoElectronico { get; set; } = string.Empty;
    [Required, MaxLength(16)] public string NombreJugador { get; set; } = string.Empty;
    [Required, MaxLength(16)] public string NombreNormalizado { get; set; } = string.Empty;
    [Required] public string ContrasenaHash { get; set; } = string.Empty;
    [Required] public string CodigoHash { get; set; } = string.Empty;
    public DateTime VenceUtc { get; set; }
    public DateTime UltimoEnvioUtc { get; set; }
    public int IntentosFallidos { get; set; }
    [Timestamp] public byte[] Version { get; set; } = Array.Empty<byte>();
}
