using System.ComponentModel.DataAnnotations;
namespace GamesIT.Api.DTOs.Autenticacion;

public class ModificarFavoritoDTO
{
    [Required] public string JuegoId { get; set; } = string.Empty;
    public bool Favorito { get; set; }
}
