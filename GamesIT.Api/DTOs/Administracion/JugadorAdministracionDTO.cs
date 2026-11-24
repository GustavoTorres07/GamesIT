namespace GamesIT.Api.DTOs.Administracion;

public class JugadorAdministracionDTO
{
    public Guid JugadorId { get; set; }
    public string NombreJugador { get; set; } = string.Empty;
    public string CorreoElectronico { get; set; } = string.Empty;
    public int Nivel { get; set; }
    public long Experiencia { get; set; }
    public long Bits { get; set; }
    public DateTime FechaRegistroUtc { get; set; }
}

public class PaginaJugadoresDTO
{
    public int Pagina { get; set; }
    public int Tamanio { get; set; }
    public int Total { get; set; }
    public List<JugadorAdministracionDTO> Jugadores { get; set; } = [];
}
