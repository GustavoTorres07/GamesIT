using System.ComponentModel.DataAnnotations;
namespace GamesIT.Api.DTOs.Autenticacion;

public class ReenviarCodigoDTO
{
    [Required] public Guid SolicitudId { get; set; }
}
