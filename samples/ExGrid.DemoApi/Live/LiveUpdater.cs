using System.Diagnostics;

namespace ExGrid.DemoApi;

/// <summary>
/// What the live updates are set to (<c>GET</c> and <c>POST /api/live</c>). They are off until a
/// page turns them on, so a page that reads the data sees it hold still (ADR-0068).
/// </summary>
/// <param name="On">Whether the server is changing trades.</param>
/// <param name="IntervalMs">How often a tick starts, in milliseconds.</param>
/// <param name="TradesPerTick">How many trades a tick changes.</param>
internal sealed record LiveSettings(bool On, int IntervalMs, int TradesPerTick)
{
    /// <summary>Off, at four ticks a second of twenty trades each when turned on.</summary>
    public static readonly LiveSettings Default = new(false, 250, 20);

    /// <summary>The shortest interval taken.</summary>
    public const int MinIntervalMs = 10;

    /// <summary>The longest interval taken.</summary>
    public const int MaxIntervalMs = 60_000;

    /// <summary>The most trades one tick changes.</summary>
    public const int MaxTradesPerTick = 1_000;
}

/// <summary>
/// The live updates (ADR-0066, ADR-0068): while on, a tick of
/// <see cref="TradeStore.ApplyLiveChangesAsync"/> every <see cref="LiveSettings.IntervalMs"/>,
/// which <see cref="TradesHubBroadcaster"/> then says on the hub.
/// </summary>
internal sealed class LiveUpdater(TradeStore store, ILogger<LiveUpdater> logger) : BackgroundService
{
    private readonly Lock _gate = new();
    // Held for each tick, so turning the updates off can wait for one already under way.
    private readonly SemaphoreSlim _tick = new(1, 1);
    private LiveSettings _settings = LiveSettings.Default;
    private TaskCompletionSource _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>What the live updates are set to now.</summary>
    public LiveSettings Settings
    {
        get
        {
            lock (_gate)
                return _settings;
        }
    }

    /// <summary>
    /// Sets them. The loop wakes at once, so a new interval does not wait for the end of the old
    /// one. Turning them off returns only once a tick already under way has committed: from then
    /// on the data holds still, so a page that turned them off can read it and trust it stays.
    /// </summary>
    public async Task<LiveSettings> SetAsync(LiveSettings settings, CancellationToken cancellationToken)
    {
        Apply(settings);
        if (!settings.On)
        {
            await _tick.WaitAsync(cancellationToken);
            _tick.Release();
        }
        return settings;
    }

    private void Apply(LiveSettings settings)
    {
        TaskCompletionSource changed;
        lock (_gate)
        {
            _settings = settings;
            changed = _changed;
            _changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        changed.TrySetResult();
        if (settings.On)
            logger.LogInformation("Live updates on: {Trades} trades every {Interval} ms.", settings.TradesPerTick, settings.IntervalMs);
        else
            logger.LogInformation("Live updates off.");
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await store.Ready.WaitAsync(stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                LiveSettings settings;
                Task changed;
                lock (_gate)
                {
                    settings = _settings;
                    changed = _changed.Task;
                }

                if (!settings.On)
                {
                    await changed.WaitAsync(stoppingToken);
                    continue;
                }

                var started = Stopwatch.GetTimestamp();
                await _tick.WaitAsync(stoppingToken);
                try
                {
                    // Turned off since the settings were read, and SetAsync may be waiting for
                    // this tick: skip it.
                    if (!Settings.On)
                        continue;
                    await store.ApplyLiveChangesAsync(settings.TradesPerTick, stoppingToken);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    // Said, and stopped: a tick that fails once fails again, four times a second.
                    logger.LogError(e, "A live update failed, so the live updates are turned off.");
                    Apply(Settings with { On = false });
                    continue;
                }
                finally
                {
                    _tick.Release();
                }
                var wait = TimeSpan.FromMilliseconds(settings.IntervalMs) - Stopwatch.GetElapsedTime(started);
                if (wait > TimeSpan.Zero)
                    await Task.WhenAny(changed, Task.Delay(wait, stoppingToken));
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception) when (store.State == TradeStoreState.Failed)
        {
            // The trades never became ready; TradeStoreStartup has said why and stops the server.
        }
    }
}
