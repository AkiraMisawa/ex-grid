using Microsoft.AspNetCore.SignalR;

namespace ExGrid.DemoApi;

/// <summary>
/// Says every committed change on the hub, in the order committed: <c>VersionChanged</c> and then
/// <c>TradesChanged</c> (<see cref="ITradeNotices"/>). It reads <see cref="TradeStore.Changes"/>,
/// so whatever changes the trades, the hub says it.
/// </summary>
internal sealed class TradesHubBroadcaster(
    TradeStore store, IHubContext<TradesHub, ITradeNotices> hub, ILogger<TradesHubBroadcaster> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var change in store.Changes.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await hub.Clients.All.VersionChanged(change.Version);
                    await hub.Clients.All.TradesChanged(change.Version, change.TradeIds);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    logger.LogError(e, "The hub could not say version {Version}.", change.Version);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
