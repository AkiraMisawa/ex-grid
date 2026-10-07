using System.Text.Json;
using Fluxor;
using FluxorGrid.Store;

namespace FluxorGrid.Grid;

/// <summary>
/// Which old store states and old trade versions are still reachable (check 6). It holds a weak
/// reference to every state the store produces, and to a sample of the trade versions each new
/// state replaced, and after a full collection counts the ones still alive. It holds the current
/// state strongly between two changes and nothing older. One per store (scoped).
///
/// <para>The weak references are pruned at each count, so what it holds itself stays a few
/// thousand small objects at most.</para>
/// </summary>
public sealed class Census : IDisposable
{
    // How many replaced versions each new state adds to the sample.
    private const int SamplePerState = 10;

    private readonly IState<TradesState> _state;
    private readonly object _gate = new();
    private readonly List<WeakReference<TradesState>> _states = [];
    private readonly List<WeakReference<Trade>> _replaced = [];
    private readonly List<(string Name, WeakReference Target)> _tracked = [];
    private TradesState? _last;
    private long _statesSeen;
    private long _replacedSampled;

    public Census(IState<TradesState> state)
    {
        _state = state;
        _state.StateChanged += OnStateChanged;
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        var next = _state.Value;
        lock (_gate)
        {
            var last = _last;
            _last = next;
            if (last is null || ReferenceEquals(last, next))
                return;
            _states.Add(new WeakReference<TradesState>(last));
            _statesSeen++;
            if (ReferenceEquals(last.Trades, next.Trades))
                return;
            // Pairs the two lists by id, as ReplaceAll does, and samples the versions replaced.
            var now = new Dictionary<string, Trade>(next.Trades.Count);
            foreach (var trade in next.Trades)
                now[trade.Id] = trade;
            var sampled = 0;
            foreach (var trade in last.Trades)
            {
                if (sampled == SamplePerState)
                    break;
                if (now.TryGetValue(trade.Id, out var newer) && !ReferenceEquals(newer, trade))
                {
                    _replaced.Add(new WeakReference<Trade>(trade));
                    _replacedSampled++;
                    sampled++;
                }
            }
        }
    }

    /// <summary>Follows one object — a page, a Grid Source — to tell whether it outlives its page.</summary>
    public void Track(string name, object target)
    {
        lock (_gate)
            _tracked.Add((name, new WeakReference(target)));
    }

    /// <summary>A full, blocking collection, then the counts and the managed heap.</summary>
    public string Count()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        var heap = GC.GetTotalMemory(forceFullCollection: true);
        lock (_gate)
        {
            _states.RemoveAll(w => !w.TryGetTarget(out _));
            _replaced.RemoveAll(w => !w.TryGetTarget(out _));
            var tracked = _tracked.GroupBy(t => t.Name).ToDictionary(g => g.Key, g => $"{g.Count(t => t.Target.IsAlive)} of {g.Count()} alive");
            return JsonSerializer.Serialize(new
            {
                tracked,
                heapBytes = heap,
                statesSeen = _statesSeen,
                oldStatesAlive = _states.Count,
                replacedSampled = _replacedSampled,
                replacedAlive = _replaced.Count,
                tradesNow = _state.Value.Trades.Count,
                version = _state.Value.Version,
            });
        }
    }

    public void Dispose() => _state.StateChanged -= OnStateChanged;
}
