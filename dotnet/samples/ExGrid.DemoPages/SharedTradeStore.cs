using ExGrid;

namespace ExGrid.DemoPages;

/// <summary>
/// The Consumer's store behind the /shared page: one per process, so on a Blazor Server
/// host every user's circuit reads the same rows (ADR-0018 §5). It holds the data and
/// says when a row was replaced; each circuit builds its own Grid Source over it and
/// applies the change on its own dispatcher. On WebAssembly there is one user per
/// process and the page shows the same thing with nobody to share it with.
/// </summary>
public sealed class SharedTradeStore
{
    private readonly object _gate = new();
    private DemoTrade[] _rows = DemoData.Window(40, seed: 7);
    private event Action<DemoTrade, DemoTrade>? RowReplaced;

    /// <summary>
    /// The wrong way, kept for the refusal it demonstrates: one bundled Grid Source for
    /// every user. The second circuit to attach it is refused by name (ADR-0018 §5).
    /// </summary>
    public InMemoryGridSource<DemoTrade> OneSourceForEveryone { get; } =
        GridSource.From(DemoData.Window(40, seed: 7));

    /// <summary>
    /// The rows as they are now, and a subscription to every replacement after — taken
    /// together, so no replacement falls between the snapshot and the subscription.
    /// </summary>
    public IReadOnlyList<DemoTrade> Subscribe(Action<DemoTrade, DemoTrade> onReplaced)
    {
        lock (_gate)
        {
            RowReplaced += onReplaced;
            return _rows;
        }
    }

    public void Unsubscribe(Action<DemoTrade, DemoTrade> onReplaced)
    {
        lock (_gate)
            RowReplaced -= onReplaced;
    }

    /// <summary>
    /// Replaces a row with a new instance, 1,000 more in notional — identity, not
    /// mutation, is the change signal (ADR-0003) — and tells every subscriber.
    /// </summary>
    public void Revalue(int index)
    {
        DemoTrade old, replacement;
        Action<DemoTrade, DemoTrade>? subscribers;
        lock (_gate)
        {
            old = _rows[index];
            replacement = new DemoTrade
            {
                Book = old.Book,
                Trader = old.Trader,
                Notional = old.Notional + 1_000m,
                TradeDate = old.TradeDate,
                Confirmed = old.Confirmed,
            };
            var rows = (DemoTrade[])_rows.Clone();
            rows[index] = replacement;
            _rows = rows;
            subscribers = RowReplaced;
        }
        subscribers?.Invoke(old, replacement);
    }
}
