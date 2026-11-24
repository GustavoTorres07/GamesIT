using System.ComponentModel.DataAnnotations;

namespace GamesIT.Api.DTOs.Autenticacion
{
    public class SeleccionarAvatarDTO
    {
        [Required(ErrorMessage = "Seleccioná un avatar.")]
        [RegularExpression("^(bytefox|nexobot|pixelowl)$",
            ErrorMessage = "El avatar seleccionado no está disponible.")]
        public string AvatarId { get; set; } = string.Empty;
    }
}
