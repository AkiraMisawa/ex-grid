using ExSheet.Engine;

namespace ExSheet;

/// <summary>
/// One row of a Sheet as ExGrid is handed it: a position, not a thing (ADR-0046). Row 5 stays
/// row 5 whatever is put into it, so the grid's Row Sequence Version never moves; what changes
/// is the instance. ExSheet hands the grid a new instance for every row a change to the Sheet
/// names, and the same instance for every other row, so the others skip their render
/// (ADR-0003, SH-4).
/// </summary>
/// <remarks>
/// A row reads its cells from the Sheet the first time the grid asks for each one and keeps the
/// answer. That is safe because an instance is retired the moment the Sheet reports a change on
/// its row: while it is current, what it would read has not changed since it was made.
/// </remarks>
public sealed class SheetRow
{
    private readonly Sheet _sheet;
    private Dictionary<int, SheetCellText?>? _cells;

    internal SheetRow(Sheet sheet, int index)
    {
        _sheet = sheet;
        Index = index;
    }

    /// <summary>The row's position on the Sheet, from 0 (row <c>1</c> is 0).</summary>
    public int Index { get; }

    /// <summary>What the cell in <paramref name="column"/> shows, or null for a blank cell.</summary>
    internal SheetCellText? At(int column)
    {
        _cells ??= [];
        if (_cells.TryGetValue(column, out var cached)) return cached;
        var display = _sheet.GetDisplay(new CellAddress(Index, column));
        var text = SheetCellText.From(display);
        _cells[column] = text;
        return text;
    }

    /// <summary>The cell's Entry as the Cell Editor opens on it (ADR-0051).</summary>
    internal string EntryTextAt(int column) => _sheet.GetEntryText(new CellAddress(Index, column));

    /// <summary>
    /// The verdict on a commit into this row (ADR-0034): a Formula the engine cannot read is
    /// rejected with the engine's reason, and the editor holds the text so it can be corrected.
    /// Anything else is a constant, which always reads — as text if as nothing else.
    /// </summary>
    internal global::ExGrid.Cells.EditVerdict Judge(string typed)
    {
        if (typed.Length == 0 || typed[0] != '=') return global::ExGrid.Cells.EditVerdict.Accept;
        try
        {
            Entry.Parse(typed, _sheet.Culture);
            return global::ExGrid.Cells.EditVerdict.Accept;
        }
        catch (FormulaSyntaxException error)
        {
            return global::ExGrid.Cells.EditVerdict.Reject(SheetWords.FormulaUnreadable(error));
        }
    }
}
