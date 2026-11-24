namespace GamesIT.Api.Servicios;

/// <summary>
/// Cierra partidas inactivas incluso si el navegador se cerró y no pudo
/// notificar al servidor. Utiliza un scope EF Core nuevo por ejecución.
/// </summary>
public sealed class LimpiezaPartidasInactivasService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<LimpiezaPartidasInactivasService> _logger;

    public LimpiezaPartidasInactivasService(
        IServiceScopeFactory scopes,
        ILogger<LimpiezaPartidasInactivasService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EjecutarLimpiezaAsync(stoppingToken);
        using var temporizador = new PeriodicTimer(TimeSpan.FromMinutes(1));
        try
        {
            while (await temporizador.WaitForNextTickAsync(stoppingToken))
                await EjecutarLimpiezaAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Cierre normal del servidor.
        }
    }

    private async Task EjecutarLimpiezaAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var servicio = scope.ServiceProvider
                .GetRequiredService<ServicioPartidasAhorcado>();
            int cantidad = await servicio.VencerInactivasAsync(ct);
            if (cantidad > 0)
                _logger.LogInformation(
                    "GamesIT: {Cantidad} partidas cerradas por inactividad.", cantidad);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Cierre normal.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GamesIT: error en limpieza de partidas inactivas.");
        }
    }
}
