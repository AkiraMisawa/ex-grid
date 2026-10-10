using ExGrid;

namespace FluxorGrid.Store;

/// <summary>A price from the feed: the trade's new price, and how far its P&amp;L moved.</summary>
public sealed record PriceTick(string Id, decimal Price, decimal PnlMove);

/// <summary>One batch from the live feed, stamped by the effect that received it: a reducer is
/// pure, so the time a change is marked at travels in the action.</summary>
public sealed record TicksArrived(IReadOnlyList<PriceTick> Ticks, IReadOnlyList<Trade> Added, IReadOnlyList<string> Removed, DateTimeOffset At);

/// <summary>Starts the effect's live feed: a batch every <paramref name="IntervalMs"/>, until
/// <paramref name="MaxTicks"/> batches (0 for no end).</summary>
public sealed record StartFeed(int IntervalMs, int PerTick, int MaxTicks, int AddRemoveEvery, int Seed);

public sealed record StopFeed;

/// <summary>The feed ended, by itself or by <see cref="StopFeed"/>.</summary>
public sealed record FeedStopped;

/// <summary>An amendment from upstream to one cell (the check's F9), stamped like a tick.</summary>
public sealed record UpstreamAmend(string Id, string Column, DateTimeOffset At);

/// <summary>An upstream P&amp;L for one trade (the check's F8: a tick that moves a row under a sort
/// by P&amp;L).</summary>
public sealed record UpstreamPnl(string Id, decimal Pnl, DateTimeOffset At);

/// <summary>An upstream price for some trades with their P&amp;L unchanged (the check's F7: a tick
/// that changes values only).</summary>
public sealed record UpstreamPrices(IReadOnlyList<string> Ids, DateTimeOffset At);

/// <summary>
/// The user's write from one grid gesture — an edit, a paste, a fill or a clear — as one action,
/// so it is one step. Each cell names the trade by id and carries the version the grid judged
/// the write against (<see cref="CellEdit.BasedOn"/>), so the reducer can tell whether the
/// store moved on since.
/// </summary>
/// <param name="Replacements">W2's <c>?edits=source</c>: the instance each row was already
/// replaced by in the Grid Source, so the store holds that same instance and the source pairs it
/// as unchanged.</param>
public sealed record EditCells(IReadOnlyList<CellEdit> Edits, string Origin, IReadOnlyDictionary<string, Trade>? Replacements = null);

/// <summary>One cell of a write: null text clears it.</summary>
public sealed record CellEdit(string Id, string Column, string? Text, Trade BasedOn);

/// <summary>W1's header click: the store holds the Sort, and the page sorts the Window.</summary>
public sealed record SortChanged(IReadOnlyList<SortSpec> Sorts);

/// <summary>W1 turns on the change times it paints its Change Highlight from.</summary>
public sealed record TrackChanges(bool On);
