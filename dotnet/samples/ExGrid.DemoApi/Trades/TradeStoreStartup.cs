namespace ExGrid.DemoApi;

/// <summary>
/// Makes the trades ready once the server is up, so <c>GET /api/status</c> can answer with
/// generation's progress while it runs. If they cannot be made ready, the server stops with exit
/// code 1: a demo server without its data would fail every page, each in a way of its own.
/// </summary>
internal sealed class TradeStoreStartup(
    TradeStore store, IHostApplicationLifetime lifetime, ILogger<TradeStoreStartup> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Off the start-up path: copying a large file is synchronous work.
            await Task.Run(() => store.InitializeAsync(stoppingToken), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception e)
        {
            logger.LogCritical(e, "The trades could not be made ready, so the server stops.");
            Environment.ExitCode = 1;
            lifetime.StopApplication();
        }
    }
}
