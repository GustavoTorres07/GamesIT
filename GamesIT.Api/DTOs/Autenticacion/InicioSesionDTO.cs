using System.ComponentModel.DataAnnotations;

namespace GamesIT.Api.DTOs.Autenticacion
{
    public class InicioSesionDTO
    {
        [Required(ErrorMessage = "Ingresá tu correo o nombre de jugador.")]
        [StringLength(254, ErrorMessage = "El identificador es demasiado largo.")]
        public string Identificador { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [DataType(DataType.Password)]
        public string Contrasena { get; set; } = string.Empty;

        // El usuario debe elegir expresamente mantener su sesión.
        public bool Recordarme { get; set; } = false;
    }
}
