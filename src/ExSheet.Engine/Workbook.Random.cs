namespace ExSheet.Engine;

/// <summary>
/// The Workbook Seed and the count of changes (ADR-0180, ADR-0331 §9): what <c>RAND</c> and
/// <c>RANDBETWEEN</c> draw from on every Sheet, given to the Workbook as data, as the Sheet Day is
/// (ADR-0121). A change on any Sheet draws again on every Sheet, as Excel's recalculation does.
/// </summary>
public sealed partial class Workbook
{
    /// <summary>How deep the operations under way are nested: one operation counts as one change, however many steps it takes.</summary>
    private int _operationDepth;

    /// <summary>Whether the operation under way has counted its change already.</summary>
    private bool _operationCounted;

    /// <summary>
    /// The seed every draw of <c>RAND</c> and <c>RANDBETWEEN</c> on every Sheet is made from, with the
    /// Sheet's name, the cell, the call's place in its Formula and <see cref="Changes"/> (ADR-0180).
    /// <see langword="null"/> while it is not set, when every Formula that calls either is
    /// <c>#GETTING_DATA</c>, as one that calls <c>TODAY</c> waits for the Sheet Day. The engine never
    /// chooses a random number of its own: whoever runs it gives the seed. It is not part of a document.
    /// </summary>
    public ulong? Seed { get; private set; }

    /// <summary>
    /// How many changes the Workbook has had since it was made or opened (ADR-0180): each draws again,
    /// on every Sheet. An edit of an Entry, an insertion or a deletion, a Sheet added, deleted, renamed,
    /// moved or copied, the undo or redo of one of those, a Linked Table's snapshot whatever its rows,
    /// and a Linked Table's declaration that declares it or replaces it, each count once. An edit that
    /// leaves every Entry as it was, a rename to the name the Sheet has, the same declaration again, a
    /// change of format or width, a recalculation by the clock, a new Sheet Day and a new seed do not.
    /// A Workbook opened from a document starts from 0.
    /// </summary>
    public long Changes { get; private set; }

    /// <summary>
    /// Sets the seed and keeps the count of changes, and recalculates only the Formulas that draw, on
    /// every Sheet, and what reads them. Setting the seed the Workbook already has changes nothing. It is
    /// not a change, and not an operation a user undoes.
    /// </summary>
    /// <param name="seed">The seed, or <see langword="null"/> to make the draws wait again.</param>
    /// <returns>What the recalculation changed, Sheet by Sheet.</returns>
    public WorkbookChange SetSeed(ulong? seed) => SetSeed(seed, Changes);

    /// <summary>
    /// Sets the seed and the count of changes together: the same Entries, seed and count give the same
    /// draws, so a Workbook opened from a document and given the seed and count another Workbook had
    /// shows that Workbook's Values (SH-17).
    /// </summary>
    /// <param name="seed">The seed, or <see langword="null"/> to make the draws wait again.</param>
    /// <param name="changes">The count of changes to draw at.</param>
    /// <returns>What the recalculation changed, Sheet by Sheet.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The count is below 0.</exception>
    public WorkbookChange SetSeed(ulong? seed, long changes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(changes);
        var shown = _shown;
        if (seed == Seed && changes == Changes) return Collecting(shown, () => SheetChange.None);
        Seed = seed;
        Changes = changes;
        var readers = new HashSet<SheetCell>(_sheets.SelectMany(sheet => sheet.RandomReaders));
        return Collecting(shown, () => readers.Count == 0 ? SheetChange.None : shown.RecalculateCells(readers));
    }

    /// <summary>Does <paramref name="operation"/> as one operation: whatever steps it takes, on whichever Sheets, it counts as one change at most.</summary>
    internal T AsOneChange<T>(Func<T> operation)
    {
        if (_operationDepth++ == 0) _operationCounted = false;
        try
        {
            return operation();
        }
        finally
        {
            _operationDepth--;
        }
    }

    /// <summary>Counts a change (ADR-0180), before the recalculation it brings draws: once for the operation under way, or once on its own.</summary>
    internal void CountChange()
    {
        if (_operationDepth > 0)
        {
            if (_operationCounted) return;
            _operationCounted = true;
        }
        Changes++;
    }

    /// <summary>Opening is no change: the count starts from 0 (ADR-0180).</summary>
    internal void ForgetChanges() => Changes = 0;
}
