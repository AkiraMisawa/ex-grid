namespace ExGrid.Rows;

/// <summary>
/// The Consumer's half of Row Marks (ADR-0043). The grid does not hold the marks — it
/// cannot name a row outside its Window, so it could not hold a set of them. It reports
/// every marking gesture here as one intent, and asks, row by row for the rows it paints,
/// whether each is marked.
///
/// <para>The contract is on the answers, not on how the marks are kept: once "mark all"
/// has arrived, every Detail row of that result answers marked until it is unmarked —
/// which is what makes a row scrolled into view afterwards paint as marked. The bundled
/// Sources implement it; a Consumer pushing its own Window implements it here.</para>
/// </summary>
public interface IRowMarks<TRow>
{
    /// <summary>Whether a row is marked. Asked for every Detail row the grid paints,
    /// on every render, so it must be cheap and must not throw for a row it has never
    /// seen — such a row is unmarked.</summary>
    bool IsMarked(TRow row);

    /// <summary>The counts the header and the count display are painted from, or null
    /// while they are not known — the grid then claims nothing rather than guess from its
    /// Window (ADR-0043).</summary>
    RowMarkCounts? Counts { get; }

    /// <summary>One marking gesture: one checkbox, the header, or Space over a
    /// selection. Never one call per row.</summary>
    Task OnMarkIntentAsync(RowMarkIntent<TRow> intent);

    /// <summary>The grid's Row Kind delegate, pushed at bind time and whenever it
    /// changes, so the marks can leave Group and Total rows out of "all" and out of the
    /// counts (ADR-0024/0043). Null means every row is a Detail row.</summary>
    void OnRowKindChanged(Func<TRow, RowKind>? rowKind);

    /// <summary>Raised after the marks or their counts changed, so the grid repaints —
    /// only the rows whose answer moved re-render.</summary>
    event Action? Changed;
}
