using System.ComponentModel.DataAnnotations;
namespace GamesIT.Api.DTOs.Autenticacion;

public class ConfirmarRegistroDTO
{
    [Required] public Guid SolicitudId { get; set; }
    [Required, RegularExpression(@"^\d{6}$", ErrorMessage = "Ingresá los seis dígitos del código.")]
    public string Codigo { get; set; } = string.Empty;
}
