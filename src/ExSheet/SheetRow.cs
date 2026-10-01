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
    private Dictionary<int, (int Characters, string? Text)>? _painted;
    private Dictionary<int, global::ExGrid.Cells.CellAppearance>? _appearances;
    private int _appearancesRead;

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
        var address = new CellAddress(Index, column);
        var text = SheetCellText.From(_sheet.GetDisplay(address), _sheet.GetValue(address), _sheet.GetAlignment(address));
        _cells[column] = text;
        return text;
    }

    /// <summary>
    /// What the cell paints in a column <paramref name="characters"/> wide, in Excel's unit
    /// (ADR-0047, third round; ADR-0050 item 11): for a number in General, the engine's text
    /// fitted to the width — <c>=1/3</c> reads <c>0.333333</c> in a default column — or the run
    /// of <c>#</c> that becomes <c>####</c> where not even the shortest form fits. Null where the
    /// cell paints its value's own text: text, booleans, Error Values, blanks, any other format
    /// (the grid's <c>####</c> rule decides those), and a General number the width does not
    /// shorten. The value's own text stays the accessible name and the copy.
    /// </summary>
    internal string? PaintedAt(int column, double characters)
    {
        if (At(column) is not { IsNumber: true } shown || ReferenceEquals(shown.Text, SheetCellText.Unshowable)) return null;
        // The engine counts whole characters; a width that is 8 may arrive as 7.9999999.
        var whole = (int)Math.Min(int.MaxValue, Math.Floor(Math.Max(0, characters) + 1e-9));
        _painted ??= [];
        if (_painted.TryGetValue(column, out var cached) && cached.Characters == whole) return cached.Text;
        var address = new CellAddress(Index, column);
        string? painted = null;
        if (_sheet.GetNumberFormat(address).IsGeneral)
        {
            var display = _sheet.GetDisplay(address, whole);
            var text = display.CannotShow ? SheetCellText.Unshowable : display.Text;
            painted = string.Equals(text, shown.Text, StringComparison.Ordinal) ? null : text;
        }
        _painted[column] = (whole, painted);
        return painted;
    }

    /// <summary>
    /// The cell's Font, Fill and Borders as the core paints them (ADR-0050 item 15, ADR-0063), for one
    /// <paramref name="reading"/> of the Sheet's formatting. Within a reading the answer is kept, as
    /// the cells' text is: a change the engine names retires the row, and the engine names the row
    /// across a top or bottom side it changes, since a side is read as the edge shown from both
    /// cells. A change that reaches rows the engine does not name — a whole row's or column's Cell
    /// Format, or an insertion or deletion — starts a new reading, and the row is read again
    /// (<see cref="SheetAppearance.Lookup"/>).
    /// </summary>
    internal global::ExGrid.Cells.CellAppearance AppearanceAt(int column, int reading)
    {
        if (_appearances is null || _appearancesRead != reading)
        {
            _appearances = [];
            _appearancesRead = reading;
        }
        if (_appearances.TryGetValue(column, out var cached)) return cached;
        var appearance = SheetAppearance.Of(_sheet, new CellAddress(Index, column), At(column)?.Colour);
        _appearances[column] = appearance;
        return appearance;
    }

    /// <summary>The cell's Entry as the Cell Editor opens on it (ADR-0051).</summary>
    internal string EntryTextAt(int column) => _sheet.GetEntryText(new CellAddress(Index, column));

    /// <summary>
    /// The verdict on a commit into this row (ADR-0034): a Formula the engine cannot read is
    /// rejected with the engine's reason, and the editor holds the text so it can be corrected.
    /// Text with a leading <c>+</c> or <c>-</c> is read as the engine reads it, since Excel may
    /// read it as a Formula (<c>-B2 C2</c>, which the engine refuses by name: TYPED-054).
    /// Anything else is a constant, which always reads — as text if as nothing else.
    /// </summary>
    internal global::ExGrid.Cells.EditVerdict Judge(string typed)
    {
        if (typed.Length == 0 || typed[0] is not ('=' or '+' or '-')) return global::ExGrid.Cells.EditVerdict.Accept;
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
