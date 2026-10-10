using System.Collections.Immutable;
using Fluxor;
using FluxorGrid.Grid;

namespace FluxorGrid.Store;

/// <summary>
/// The blotter's reducers. Every change replaces the trades it touches with new instances made by
/// <c>with</c>, and leaves every other trade's instance where it was, so the next list differs from
/// the last by exactly the rows that changed (ADR-0003, ADR-0141's pairing by reference).
/// </summary>
public static class Reducers
{
    /// <summary>How long W1 keeps a change time: at least the grid's ChangeHighlightDuration, so a
    /// mark is never cut short (the rule GridSource.From's ChangeTimesKeptFor states).</summary>
    public static readonly TimeSpan ChangeTimesKeptFor = TimeSpan.FromSeconds(5);

    [ReducerMethod]
    public static TradesState OnTicks(TradesState state, TicksArrived action)
    {
        var builder = state.Trades.ToBuilder();
        var index = IndexOf(builder);
        var changed = new List<(Trade Old, Trade New)>(action.Ticks.Count);
        foreach (var tick in action.Ticks)
        {
            if (!index.TryGetValue(tick.Id, out var at))
                continue;
            var old = builder[at];
            var next = old with { Price = tick.Price, Pnl = old.Pnl + tick.PnlMove };
            builder[at] = next;
            changed.Add((old, next));
        }
        if (action.Removed.Count > 0)
        {
            var removed = action.Removed.ToHashSet();
            builder.RemoveAll(t => removed.Contains(t.Id));
        }
        builder.AddRange(action.Added);
        return state with
        {
            Trades = builder.ToImmutable(),
            Version = state.Version + 1,
            Ticks = state.Ticks + 1,
            NextId = state.NextId + action.Added.Count,
            ChangedAt = Marked(state, changed, action.Added, action.Removed, action.At),
        };
    }

    [ReducerMethod]
    public static TradesState OnUpstreamAmend(TradesState state, UpstreamAmend action)
    {
        var at = state.Trades.FindIndex(t => t.Id == action.Id);
        if (at < 0)
            return state;
        var old = state.Trades[at];
        var next = action.Column switch
        {
            "Book" => old with { Book = old.Book + "*" },
            "Currency" => old with { Currency = old.Currency == "USD" ? "EUR" : "USD" },
            "TradeDate" => old with { TradeDate = old.TradeDate.AddDays(1) },
            "Notional" => old with { Notional = old.Notional + 1m },
            "Price" => old with { Price = old.Price + 0.0001m },
            _ => old with { Pnl = old.Pnl + 1m },
        };
        return Replaced(state, at, old, next, action.At);
    }

    [ReducerMethod]
    public static TradesState OnUpstreamPnl(TradesState state, UpstreamPnl action)
    {
        var at = state.Trades.FindIndex(t => t.Id == action.Id);
        if (at < 0)
            return state;
        var old = state.Trades[at];
        return Replaced(state, at, old, old with { Pnl = action.Pnl }, action.At);
    }

    [ReducerMethod]
    public static TradesState OnUpstreamPrices(TradesState state, UpstreamPrices action)
    {
        var builder = state.Trades.ToBuilder();
        var index = IndexOf(builder);
        var changed = new List<(Trade Old, Trade New)>();
        foreach (var id in action.Ids)
        {
            if (!index.TryGetValue(id, out var at))
                continue;
            var old = builder[at];
            var next = old with { Price = old.Price + 0.0001m };
            builder[at] = next;
            changed.Add((old, next));
        }
        return state with
        {
            Trades = builder.ToImmutable(),
            Version = state.Version + 1,
            ChangedAt = Marked(state, changed, [], [], action.At),
        };
    }

    /// <summary>
    /// The user's write. The grid judged it against the version it carries (ADR-0142); the store
    /// may hold a newer one by now — on a circuit, a feed batch dispatched on the timer's thread
    /// can land between the grid's judgement and this reducer. The reducer is the last place that
    /// can tell, so it writes over a newer version only where the written cell still paints what
    /// the grid judged, and refuses the cell otherwise. A user's own write marks nothing.
    /// </summary>
    [ReducerMethod]
    public static TradesState OnEdit(TradesState state, EditCells action)
    {
        var builder = state.Trades.ToBuilder();
        var index = IndexOf(builder);
        var written = 0;
        var refused = 0;
        string? refusal = null;
        var replaced = new HashSet<string>();
        foreach (var edit in action.Edits)
        {
            if (!index.TryGetValue(edit.Id, out var at))
            {
                refused++;
                refusal = $"{edit.Id} is no longer in the store";
                continue;
            }
            var current = builder[at];
            if (action.Replacements is { } replacements && replacements.TryGetValue(edit.Id, out var replacement))
            {
                // W2 ?edits=source: the source already holds this instance. Taken whole, once, if
                // the store still holds the version it was built on.
                if (replaced.Contains(edit.Id))
                    continue;
                if (ReferenceEquals(current, edit.BasedOn))
                {
                    builder[at] = replacement;
                    replaced.Add(edit.Id);
                    written++;
                    continue;
                }
            }
            if (!ReferenceEquals(current, edit.BasedOn)
                && TradeColumns.TextOf(current, edit.Column) != TradeColumns.TextOf(edit.BasedOn, edit.Column))
            {
                refused++;
                refusal = $"{edit.Id}.{edit.Column} changed in the store after the grid judged the write";
                continue;
            }
            if (TradeColumns.Written(current, edit.Column, edit.Text) is not { } next)
            {
                refused++;
                refusal = $"{edit.Id}.{edit.Column} does not take '{edit.Text}'";
                continue;
            }
            builder[at] = next;
            written++;
        }
        return state with
        {
            Trades = written > 0 ? builder.ToImmutable() : state.Trades,
            Version = state.Version + 1,
            Writes = state.Writes + written,
            LastWrite = $"{action.Origin}: {written} cells written, {refused} refused",
            Refused = state.Refused + refused,
            LastRefusal = refusal ?? state.LastRefusal,
        };
    }

    [ReducerMethod]
    public static TradesState OnSort(TradesState state, SortChanged action)
        => state with { Sorts = [.. action.Sorts], Version = state.Version + 1 };

    [ReducerMethod]
    public static TradesState OnTrack(TradesState state, TrackChanges action)
        => state.TrackChanges == action.On ? state : state with { TrackChanges = action.On, ChangedAt = ImmutableDictionary<CellKey, DateTimeOffset>.Empty };

    [ReducerMethod(typeof(StartFeed))]
    public static TradesState OnStart(TradesState state) => state with { FeedRunning = true };

    [ReducerMethod(typeof(FeedStopped))]
    public static TradesState OnStopped(TradesState state) => state with { FeedRunning = false };

    private static TradesState Replaced(TradesState state, int at, Trade old, Trade next, DateTimeOffset when) => state with
    {
        Trades = state.Trades.SetItem(at, next),
        Version = state.Version + 1,
        ChangedAt = Marked(state, [(old, next)], [], [], when),
    };

    private static Dictionary<string, int> IndexOf(ImmutableList<Trade>.Builder trades)
    {
        var index = new Dictionary<string, int>(trades.Count);
        for (var i = 0; i < trades.Count; i++)
            index[trades[i].Id] = i;
        return index;
    }

    /// <summary>
    /// W1's Change Highlight, by ADR-0141's rules, which a push Consumer has to keep itself: a cell
    /// is marked when a changed row's painted text differs from its previous version's; every cell
    /// of an added row is marked; a removed row's times go; times older than
    /// <see cref="ChangeTimesKeptFor"/> go, which is what bounds the map.
    /// </summary>
    private static ImmutableDictionary<CellKey, DateTimeOffset> Marked(
        TradesState state, List<(Trade Old, Trade New)> changed, IReadOnlyList<Trade> added, IReadOnlyList<string> removed, DateTimeOffset at)
    {
        if (!state.TrackChanges)
            return state.ChangedAt;
        var map = state.ChangedAt.ToBuilder();
        var expired = at - ChangeTimesKeptFor;
        foreach (var (key, time) in state.ChangedAt)
        {
            if (time < expired)
                map.Remove(key);
        }
        foreach (var (old, next) in changed)
        {
            foreach (var column in TradeColumns.ValueColumns)
            {
                if (column.Info.TextOf(old) != column.Info.TextOf(next))
                    map[new CellKey(next.Id, column.Name)] = at;
            }
        }
        foreach (var trade in added)
        {
            foreach (var column in TradeColumns.ValueColumns)
                map[new CellKey(trade.Id, column.Name)] = at;
        }
        foreach (var id in removed)
        {
            foreach (var column in TradeColumns.ValueColumns)
                map.Remove(new CellKey(id, column.Name));
        }
        return map.ToImmutable();
    }
}
