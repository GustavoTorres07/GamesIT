using System.ComponentModel.DataAnnotations;

namespace GamesIT.Api.DTOs.Autenticacion;

public class EliminarCuentaDTO
{
    [Required(ErrorMessage = "Ingresá tu contraseña para confirmar.")]
    public string Contrasena { get; set; } = string.Empty;

    [Required(ErrorMessage = "Debés escribir la frase de confirmación.")]
    public string Confirmacion { get; set; } = string.Empty;
}
