namespace ExGrid.DemoPages;

/// <summary>A note recorded against a trade, with the version of the trade its writer was
/// looking at — so "confirmed at 1,000" is never read as a statement about 2,000.</summary>
public sealed record TradeNote(string Text, int WrittenAgainstVersion);

/// <summary>
/// A trade of the /inspector-edits page. Immutable: every change is a new instance, which
/// is what reaches the grid (Row Identity, ADR-0003). <see cref="Version"/> counts changes
/// to the values; appending a note changes the instance but not the values, so not the
/// version.
/// </summary>
public sealed record EditableTrade(
    string Id, string Book, string Trader, decimal Notional, string Status, int Version,
    IReadOnlyList<TradeNote> Notes);

/// <summary>What the store says to a request: the trade as it now stands, or why not.</summary>
public sealed record StoreAnswer(EditableTrade? Row, string? Refusal)
{
    public bool Applied => Refusal is null;
}

/// <summary>
/// One change the store announces. <see cref="Replacement"/> is null when the trade was
/// deleted. <see cref="Origin"/> names the inspector that asked for it, so that inspector
/// can tell its own change from someone else's.
/// </summary>
public sealed record StoreChange(EditableTrade Old, EditableTrade? Replacement, Guid? Origin);

/// <summary>
/// The Consumer's store behind /inspector-edits: one per process, like
/// <see cref="SharedTradeStore"/>, so on the Server host every tab changes the same trades
/// (ADR-0018 §5). It holds the rows by the Consumer's own key (the trade id) — a key the
/// grid never hands out — and it is where the version check lives: an action decided on a
/// version that is no longer current is refused here, because only here can the race
/// between a change and its announcement be closed (docs/specs/row-inspectors).
/// </summary>
public sealed class InspectorTradeStore
{
    private const int Count = 20;

    private readonly object _gate = new();
    private readonly List<StoreChange> _held = [];
    private EditableTrade[] _rows = Seed();
    private event Action<StoreChange>? Changed;
    private event Action? WasReset;

    /// <summary>The rows as they are now, and a subscription to every change after —
    /// taken together, so no change falls between the snapshot and the subscription.</summary>
    public IReadOnlyList<EditableTrade> Subscribe(Action<StoreChange> onChanged, Action onReset)
    {
        lock (_gate)
        {
            Changed += onChanged;
            WasReset += onReset;
            return _rows;
        }
    }

    public void Unsubscribe(Action<StoreChange> onChanged, Action onReset)
    {
        lock (_gate)
        {
            Changed -= onChanged;
            WasReset -= onReset;
        }
    }

    /// <summary>The rows as they are now.</summary>
    public IReadOnlyList<EditableTrade> Rows
    {
        get { lock (_gate) return _rows; }
    }

    /// <summary>The trade as it now stands, or null when it has been deleted.</summary>
    public EditableTrade? Find(string id)
    {
        lock (_gate)
            return _rows.FirstOrDefault(row => row.Id == id);
    }

    /// <summary>Changes made and not yet announced.</summary>
    public int HeldCount
    {
        get { lock (_gate) return _held.Count; }
    }

    /// <summary>Approves the trade — if the version the approver was looking at is still
    /// the current one.</summary>
    public StoreAnswer Approve(string id, int version, Guid origin)
        => Change(id, version, origin, row => row with { Status = "Approved", Version = row.Version + 1 });

    /// <summary>Appends a note, recording the version it was written against — if that
    /// version is still the current one.</summary>
    public StoreAnswer AddNote(string id, int version, string text, Guid origin)
        => Change(id, version, origin, row => row with { Notes = [.. row.Notes, new TradeNote(text, version)] });

    /// <summary>Somebody else changes the trade's notional. Announced at once, or held
    /// until <see cref="ReleaseHeld"/> — the window in which the store has changed and
    /// nobody has been told.</summary>
    public void ChangeElsewhere(string id, bool announce)
    {
        StoreChange change;
        lock (_gate)
        {
            var index = IndexOf(id);
            if (index < 0)
                return;
            var old = _rows[index];
            var replacement = old with { Notional = old.Notional + 1_000_000m, Version = old.Version + 1 };
            _rows = With(index, replacement);
            change = new StoreChange(old, replacement, Origin: null);
            if (!announce)
            {
                _held.Add(change);
                return;
            }
        }
        Announce([change]);
    }

    /// <summary>Somebody else deletes the trade. Announced at once.</summary>
    public void DeleteElsewhere(string id)
    {
        StoreChange change;
        lock (_gate)
        {
            var index = IndexOf(id);
            if (index < 0)
                return;
            change = new StoreChange(_rows[index], null, Origin: null);
            _rows = [.. _rows.Where((_, i) => i != index)];
        }
        Announce([change]);
    }

    /// <summary>Announces the held changes, in the order they were made.</summary>
    public void ReleaseHeld()
    {
        StoreChange[] held;
        lock (_gate)
        {
            held = [.. _held];
            _held.Clear();
        }
        Announce(held);
    }

    /// <summary>Puts every trade back as it started, for the browser tests and for anyone
    /// who has changed too much to follow.</summary>
    public void Reset()
    {
        Action? subscribers;
        lock (_gate)
        {
            _rows = Seed();
            _held.Clear();
            subscribers = WasReset;
        }
        subscribers?.Invoke();
    }

    private StoreAnswer Change(string id, int version, Guid origin, Func<EditableTrade, EditableTrade> change)
    {
        StoreChange announced;
        lock (_gate)
        {
            var index = IndexOf(id);
            if (index < 0)
                return new StoreAnswer(null, $"Refused: trade {id} has been deleted.");
            var current = _rows[index];
            if (current.Version != version)
            {
                return new StoreAnswer(current,
                    $"Refused: trade {id} is at version {current.Version}, and this inspector shows version {version}. Reload before trying again.");
            }
            var replacement = change(current);
            _rows = With(index, replacement);
            announced = new StoreChange(current, replacement, origin);
        }
        Announce([announced]);
        return new StoreAnswer(announced.Replacement, null);
    }

    private void Announce(IReadOnlyList<StoreChange> changes)
    {
        Action<StoreChange>? subscribers;
        lock (_gate)
            subscribers = Changed;
        foreach (var change in changes)
            subscribers?.Invoke(change);
    }

    private int IndexOf(string id) => Array.FindIndex(_rows, row => row.Id == id);

    private EditableTrade[] With(int index, EditableTrade replacement)
    {
        var rows = (EditableTrade[])_rows.Clone();
        rows[index] = replacement;
        return rows;
    }

    private static EditableTrade[] Seed()
    {
        var source = DemoData.Blotter(Count, seed: 31);
        return [.. source.Select((trade, i) => new EditableTrade(
            $"E-{i + 1:D3}", trade.Book, trade.Trader, trade.Notional, i % 3 == 0 ? "Booked" : "Pending", 1, []))];
    }
}
