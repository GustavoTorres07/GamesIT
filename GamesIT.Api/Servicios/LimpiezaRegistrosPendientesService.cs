using GamesIT.Api.Datos;
using Microsoft.EntityFrameworkCore;

namespace GamesIT.Api.Servicios;

/// <summary>
/// Elimina registros pendientes vencidos, sin tocar AspNetUsers ni Jugadores.
/// Cada ejecución obtiene su propio DbContext scoped.
/// </summary>
public sealed class LimpiezaRegistrosPendientesService : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(15);
    private readonly IServiceScopeFactory _fabricaScopes;
    private readonly ILogger<LimpiezaRegistrosPendientesService> _logger;

    public LimpiezaRegistrosPendientesService(
        IServiceScopeFactory fabricaScopes,
        ILogger<LimpiezaRegistrosPendientesService> logger)
    {
        _fabricaScopes = fabricaScopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await LimpiarSeguroAsync(stoppingToken);

        using var temporizador = new PeriodicTimer(Intervalo);

        try
        {
            while (await temporizador.WaitForNextTickAsync(stoppingToken))
                await LimpiarSeguroAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Detención normal de GamesIT.Api.
        }
    }

    private async Task LimpiarSeguroAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var alcance = _fabricaScopes.CreateScope();
            var db = alcance.ServiceProvider.GetRequiredService<ContextoGamesIT>();

            int eliminados = await db.RegistrosPendientes
                .Where(r => r.VenceUtc <= DateTime.UtcNow)
                .ExecuteDeleteAsync(cancellationToken);

            if (eliminados > 0)
                _logger.LogInformation(
                    "GamesIT eliminó {Cantidad} registros pendientes vencidos.",
                    eliminados);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Detención normal.
        }
        catch (Exception ex)
        {
            // No finalizar el BackgroundService por un error temporal de SQL Server.
            _logger.LogError(ex,
                "No se pudieron limpiar los registros pendientes vencidos.");
        }
    }
}
