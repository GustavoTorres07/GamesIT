namespace GamesIT.Api.DTOs.Autenticacion
{
    public class PerfilAutenticadoDTO
    {
        public bool EsAdministrador { get; set; }
        public Guid UsuarioId { get; set; }
        public Guid JugadorId { get; set; }
        public string NombreJugador { get; set; } = string.Empty;
        public string CorreoElectronico { get; set; } = string.Empty;
        public DateTime FechaRegistroUtc { get; set; }
        public string AvatarId { get; set; } = "bytefox";
        public int Nivel { get; set; }
        public long Experiencia { get; set; }
        public long Bits { get; set; }
        public int PartidasJugadas { get; set; }
        public int PartidasGanadas { get; set; }
        public string[] JuegosFavoritos { get; set; } = [];
    }
}
