using Microsoft.AspNetCore.Identity;

namespace GamesIT.Api.Modelos
{
    public class Usuario : IdentityUser<Guid>
    {
        // Fecha en la que se creó la cuenta.
        public DateTime FechaRegistroUtc { get; set; } = DateTime.UtcNow;

        // Indica si la cuenta está habilitada.
        public bool EstaActivo { get; set; } = true;

        // Fecha del último acceso del usuario.
        public DateTime? UltimoAccesoUtc { get; set; }
    }
}
