using Fluxor;

namespace FluxorGrid.Store;

/// <summary>
/// The live feed the effect drives: a timer that dispatches a batch of ticks. On Server the
/// batches are dispatched from a thread-pool thread, never the circuit's renderer, which is the
/// case check 7 is about. One per store (scoped), disposed with its circuit.
///
/// <para>Half of every batch falls on the even trades among the first twenty — the rows the grid
/// opens on — so rows in view change while the odd ones among them never do, and a check can
/// tell a row that rendered for nothing. The other half falls on trades from the fortieth on.
/// Every <c>AddRemoveEvery</c> batches a trade is added at the end and one from the fortieth on is
/// removed, both out of view.</para>
/// </summary>
public sealed class LiveFeed(IState<TradesState> state, TimeProvider clock) : IDisposable
{
    public const int BusyRows = 20;
    public const int QuietFrom = 40;

    private readonly object _gate = new();
    private CancellationTokenSource? _running;

    public void Start(StartFeed start, IDispatcher dispatcher)
    {
        CancellationTokenSource running;
        lock (_gate)
        {
            _running?.Cancel();
            _running = running = new CancellationTokenSource();
        }
        _ = Task.Run(() => RunAsync(start, dispatcher, running.Token));
    }

    /// <summary>Stops the feed; the caller says it stopped.</summary>
    public void Stop()
    {
        lock (_gate)
        {
            _running?.Cancel();
            _running = null;
        }
    }

    private async Task RunAsync(StartFeed start, IDispatcher dispatcher, CancellationToken token)
    {
        var random = new Random(start.Seed);
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(start.IntervalMs), clock);
        var sent = 0;
        try
        {
            while (await timer.WaitForNextTickAsync(token))
            {
                dispatcher.Dispatch(Batch(start, random, sent));
                sent++;
                if (start.MaxTicks > 0 && sent >= start.MaxTicks)
                    break;
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Stopped, or replaced by a newer start: whoever did it says so.
            return;
        }
        if (!token.IsCancellationRequested)
            dispatcher.Dispatch(new FeedStopped());
    }

    private TicksArrived Batch(StartFeed start, Random random, int sent)
    {
        var current = state.Value;
        var trades = current.Trades;
        var picked = new HashSet<int>();
        var busy = Math.Min(start.PerTick / 2, BusyRows / 2);
        while (picked.Count < busy)
            picked.Add(random.Next(BusyRows / 2) * 2);
        while (picked.Count < start.PerTick && trades.Count > QuietFrom)
            picked.Add(QuietFrom + random.Next(trades.Count - QuietFrom));
        var ticks = new List<PriceTick>(picked.Count);
        foreach (var at in picked)
        {
            var trade = trades[at];
            var price = Math.Round(trade.Price * (1m + (decimal)(random.NextDouble() - 0.5) / 500m), 4);
            var move = Math.Round(trade.Notional * (price - trade.Price) / 100m, 2);
            ticks.Add(new PriceTick(trade.Id, price, move));
        }
        IReadOnlyList<Trade> added = [];
        IReadOnlyList<string> removed = [];
        if (start.AddRemoveEvery > 0 && (sent + 1) % start.AddRemoveEvery == 0 && trades.Count > QuietFrom + 1)
        {
            added = [TradeData.Make(current.NextId)];
            var gone = trades[QuietFrom + random.Next(trades.Count - QuietFrom)].Id;
            // A trade ticked in this batch is not removed by it.
            if (!ticks.Exists(t => t.Id == gone))
                removed = [gone];
        }
        return new TicksArrived(ticks, added, removed, clock.GetUtcNow());
    }

    public void Dispose() => Stop();
}

/// <summary>The feed's effects: start and stop it. Instance methods, so the effect class takes the
/// scoped feed from the container.</summary>
public sealed class FeedEffects(LiveFeed feed)
{
    [EffectMethod]
    public Task OnStart(StartFeed action, IDispatcher dispatcher)
    {
        feed.Start(action, dispatcher);
        return Task.CompletedTask;
    }

    [EffectMethod(typeof(StopFeed))]
    public Task OnStop(IDispatcher dispatcher)
    {
        feed.Stop();
        dispatcher.Dispatch(new FeedStopped());
        return Task.CompletedTask;
    }
}
