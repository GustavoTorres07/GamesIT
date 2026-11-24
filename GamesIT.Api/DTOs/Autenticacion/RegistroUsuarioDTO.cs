
using System.ComponentModel.DataAnnotations;

namespace GamesIT.Api.DTOs.Autenticacion
{
    public class RegistroUsuarioDTO
    {
        // ==========================================
        // NOMBRE PÚBLICO DEL JUGADOR
        // ==========================================

        [Required(ErrorMessage = "El nombre del jugador es obligatorio.")]
        [StringLength(
            16,
            MinimumLength = 3,
            ErrorMessage = "El nombre debe tener entre 3 y 16 caracteres."
        )]
        [RegularExpression(
            @"^[\p{L}\p{N}_\- ]+$",
            ErrorMessage = "El nombre solo puede contener letras, números, espacios, guiones y guiones bajos."
        )]
        public string NombreJugador { get; set; } = string.Empty;


        // ==========================================
        // CORREO ELECTRÓNICO
        // ==========================================

        [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
        [EmailAddress(ErrorMessage = "El formato del correo electrónico no es válido.")]
        [StringLength(
            254,
            ErrorMessage = "El correo electrónico es demasiado largo."
        )]
        public string CorreoElectronico { get; set; } = string.Empty;


        // ==========================================
        // CONTRASEÑA
        // ==========================================

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [StringLength(
            128,
            MinimumLength = 8,
            ErrorMessage = "La contraseña debe tener entre 8 y 128 caracteres."
        )]
        public string Contrasena { get; set; } = string.Empty;


        // ==========================================
        // CONFIRMAR CONTRASEÑA
        // ==========================================

        [Required(ErrorMessage = "Debés confirmar la contraseña.")]
        [Compare(
            nameof(Contrasena),
            ErrorMessage = "Las contraseñas no coinciden."
        )]
        public string ConfirmarContrasena { get; set; } = string.Empty;
    }
}
