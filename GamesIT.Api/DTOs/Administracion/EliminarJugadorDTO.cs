using System.ComponentModel.DataAnnotations;

namespace GamesIT.Api.DTOs.Administracion;

public class EliminarJugadorDTO
{
    [Required]
    public Guid JugadorId { get; set; }

    [Required]
    public string NombreConfirmado { get; set; } = string.Empty;

    [Required]
    public string ContrasenaAdministrador { get; set; } = string.Empty;
}
