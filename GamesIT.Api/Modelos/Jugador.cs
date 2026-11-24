using System.ComponentModel.DataAnnotations;

namespace GamesIT.Api.Modelos
{
    public class Jugador
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid UsuarioId { get; set; }

        public Usuario Usuario { get; set; } = null!;

        [Required, StringLength(16, MinimumLength = 3)]
        public string NombreJugador { get; set; } = string.Empty;

        [Required, MaxLength(16)]
        public string NombreNormalizado { get; set; } = string.Empty;

        // Avatar inicial. La elección se guarda en SQL Server.
        [Required, MaxLength(32)]
        public string AvatarId { get; set; } = "bytefox";

        // Persistencia real, inicialmente en cero hasta habilitar recompensas en partidas.
        public int Nivel { get; set; } = 0;
        public long Experiencia { get; set; } = 0;
        public long Bits { get; set; } = 0;
        public int PartidasJugadas { get; set; } = 0;
        public int PartidasGanadas { get; set; } = 0;
        [MaxLength(64)] public string FavoritosCsv { get; set; } = string.Empty;
    }
}
