using GamesIT.Api.Modelos;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GamesIT.Api.Datos;

public class ContextoGamesIT : IdentityDbContext<Usuario, IdentityRole<Guid>, Guid>
{
    public ContextoGamesIT(DbContextOptions<ContextoGamesIT> opciones)
        : base(opciones)
    {
    }

    public DbSet<Jugador> Jugadores => Set<Jugador>();
    public DbSet<RegistroPendiente> RegistrosPendientes => Set<RegistroPendiente>();
    public DbSet<HistorialDesafioAhorcado> HistorialDesafiosAhorcado
        => Set<HistorialDesafioAhorcado>();
    public DbSet<Partida> Partidas => Set<Partida>();
    public DbSet<RecompensaPartida> RecompensasPartidas => Set<RecompensaPartida>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<RegistroPendiente>(entidad =>
        {
            entidad.ToTable("RegistrosPendientes");
            entidad.HasKey(r => r.Id);

            entidad.Property(r => r.CorreoElectronico).IsRequired();
            entidad.HasIndex(r => r.CorreoElectronico).IsUnique();

            entidad.Property(r => r.NombreJugador)
                .IsRequired().HasMaxLength(16);
            entidad.Property(r => r.NombreNormalizado)
                .IsRequired().HasMaxLength(16);
            entidad.HasIndex(r => r.NombreNormalizado).IsUnique();

            entidad.Property(r => r.Version).IsRowVersion();
        });

        modelBuilder.Entity<Jugador>(entidad =>
        {
            entidad.ToTable("Jugadores");
            entidad.HasKey(j => j.Id);
            entidad.Property(j => j.NombreJugador).IsRequired().HasMaxLength(16);
            entidad.Property(j => j.NombreNormalizado).IsRequired().HasMaxLength(16);
            entidad.HasIndex(j => j.NombreNormalizado).IsUnique();

            entidad.Property(j => j.AvatarId)
                .IsRequired().HasMaxLength(32).HasDefaultValue("bytefox");
            entidad.Property(j => j.Nivel).HasDefaultValue(0);
            entidad.Property(j => j.Experiencia).HasDefaultValue(0L);
            entidad.Property(j => j.Bits).HasDefaultValue(0L);
            entidad.Property(j => j.PartidasJugadas).HasDefaultValue(0);
            entidad.Property(j => j.PartidasGanadas).HasDefaultValue(0);
            entidad.Property(j => j.FavoritosCsv)
                .HasMaxLength(64).HasDefaultValue("");

            entidad.HasOne(j => j.Usuario)
                .WithOne()
                .HasForeignKey<Jugador>(j => j.UsuarioId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<HistorialDesafioAhorcado>(entidad =>
        {
            entidad.ToTable("HistorialDesafiosAhorcado");
            entidad.HasKey(h => h.Id);

            entidad.Property(h => h.PalabraNormalizada)
                .IsRequired().HasMaxLength(15);
            entidad.Property(h => h.Pista)
                .IsRequired().HasMaxLength(200);
            entidad.Property(h => h.FechaGeneracionUtc)
                .HasColumnType("datetime2");

            // La misma palabra puede usarse para diferentes jugadores,
            // pero nunca dos veces para el mismo jugador.
            entidad.HasIndex(h => new { h.JugadorId, h.PalabraNormalizada })
                .IsUnique()
                .HasDatabaseName("UX_HistorialAhorcado_Jugador_Palabra");

            // Relación sin necesidad de modificar la clase Jugador.
            // Al eliminar una cuenta, también se elimina su historial.
            entidad.HasOne<Jugador>()
                .WithMany()
                .HasForeignKey(h => h.JugadorId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RecompensaPartida>(entidad =>
        {
            entidad.ToTable("RecompensasPartidas", tabla =>
            {
                tabla.HasCheckConstraint("CK_Recompensas_NoNegativas",
                    "[ExperienciaOtorgada] >= 0 AND [BitsBase] >= 0 " +
                    "AND [BitsPorNiveles] >= 0 AND [BitsTotales] >= 0 " +
                    "AND [PuntosOtorgados] >= 0 " +
                    "AND [NivelNuevo] >= [NivelAnterior]");
            });

            entidad.HasKey(r => r.Id);
            entidad.Property(r => r.Motivo).IsRequired().HasMaxLength(16);
            entidad.Property(r => r.FechaUtc).HasColumnType("datetime2");

            // Un solo comprobante por partida, incluso bajo concurrencia.
            entidad.HasIndex(r => r.PartidaId).IsUnique()
                .HasDatabaseName("UX_Recompensas_Partida");
            entidad.HasIndex(r => new { r.JugadorId, r.FechaUtc })
                .HasDatabaseName("IX_Recompensas_Jugador_Fecha");

            // Evitar rutas múltiples de cascada; las recompensas
            // se eliminan al eliminar la partida propietaria.
            entidad.HasOne<Partida>().WithMany()
                .HasForeignKey(r => r.PartidaId)
                .IsRequired().OnDelete(DeleteBehavior.Cascade);
            entidad.HasOne<Jugador>().WithMany()
                .HasForeignKey(r => r.JugadorId)
                .IsRequired().OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Partida>(entidad =>
        {
            entidad.ToTable("Partidas", tabla =>
            {
                tabla.HasCheckConstraint(
                    "CK_Partidas_IntentosErrores",
                    "[CantidadIntentos] >= 0 AND [CantidadErrores] >= 0 " +
                    "AND [CantidadErrores] <= [CantidadIntentos]");

                tabla.HasCheckConstraint(
                    "CK_Partidas_Duracion",
                    "[DuracionSegundos] IS NULL OR [DuracionSegundos] >= 0");

                tabla.HasCheckConstraint(
                    "CK_Partidas_Fechas",
                    "[FechaFinalizacionUtc] IS NULL OR " +
                    "[FechaFinalizacionUtc] >= [FechaInicioUtc]");

                tabla.HasCheckConstraint(
                    "CK_Partidas_DesafioAhorcado",
                    "([TipoJuego] = N'Ahorcado' AND [DesafioAhorcadoId] IS NOT NULL) " +
                    "OR ([TipoJuego] <> N'Ahorcado' AND [DesafioAhorcadoId] IS NULL)");
            });

            entidad.HasKey(p => p.Id);

            // Guardamos nombres legibles de los enumerados.
            entidad.Property(p => p.TipoJuego)
                .HasConversion<string>().HasMaxLength(20).IsRequired();
            entidad.Property(p => p.Estado)
                .HasConversion<string>().HasMaxLength(16).IsRequired();

            entidad.Property(p => p.FechaInicioUtc)
                .HasColumnType("datetime2").IsRequired();
            entidad.Property(p => p.FechaFinalizacionUtc)
                .HasColumnType("datetime2");

            entidad.Property(p => p.UltimaActividadUtc)
                .HasColumnType("datetime2").IsRequired()
                .HasDefaultValueSql("SYSUTCDATETIME()");
            entidad.Property(p => p.MotivoFinalizacion)
                .HasMaxLength(32);

            entidad.Property(p => p.LetrasIntentadas)
                .HasMaxLength(27).IsRequired().HasDefaultValue("");
            entidad.Property(p => p.CantidadIntentos).HasDefaultValue(0);
            entidad.Property(p => p.CantidadErrores).HasDefaultValue(0);

            entidad.Property(p => p.Version).IsRowVersion();

            // Consultas de estadísticas del jugador por fecha.
            entidad.HasIndex(p => new { p.JugadorId, p.FechaInicioUtc })
                .HasDatabaseName("IX_Partidas_Jugador_FechaInicio");
            entidad.HasIndex(p => new { p.Estado, p.UltimaActividadUtc })
                .HasDatabaseName("IX_Partidas_Estado_UltimaActividad");

            // Un desafío concreto no puede comenzar dos partidas.
            // SQL Server crea un índice único filtrado para valores no nulos.
            entidad.HasIndex(p => p.DesafioAhorcadoId)
                .IsUnique()
                .HasDatabaseName("UX_Partidas_DesafioAhorcado");

            entidad.HasOne<Jugador>()
                .WithMany()
                .HasForeignKey(p => p.JugadorId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            // NO ACTION evita rutas múltiples de eliminación en cascada
            // (Jugador -> Historial y Jugador -> Partidas).
            entidad.HasOne<HistorialDesafioAhorcado>()
                .WithMany()
                .HasForeignKey(p => p.DesafioAhorcadoId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }
}
