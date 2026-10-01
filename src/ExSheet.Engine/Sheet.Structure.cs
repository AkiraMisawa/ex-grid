using ExSheet.Engine.Formulas;

namespace ExSheet.Engine;

public sealed partial class Sheet
{
    /// <summary>
    /// Inserts <paramref name="count"/> rows at <paramref name="row"/>; what was there and below
    /// moves down, and every Reference is rewritten to keep naming the same cells (ADR-0046/0047).
    /// The new rows hold no Entries; each of their cells takes the Cell Format of the cell above it,
    /// and each row the Cell Format recorded on the row above, as Excel's default does (ADR-0046,
    /// ADR-0047, ADR-0063); that it takes the Borders too is a reading until the eleventh Windows
    /// run, case 12. Rows inserted at the top take none. A Cell Format recorded on a row moves with
    /// it, and one pushed off the bottom edge is dropped. A Reference whose cells are pushed off
    /// the bottom edge is cut at it, or becomes <c>#REF!</c> when none of its cells remain, as in
    /// Excel.
    /// </summary>
    /// <exception cref="SheetRefusedException">
    /// A cell holding an Entry would be pushed off the Sheet's bottom edge. Nothing changes.
    /// </exception>
    public SheetChange InsertRows(int row, int count = 1) => Restructure(new StructuralEdit(SheetAxis.Rows, row, count, true)).Change;

    /// <summary>
    /// Deletes <paramref name="count"/> rows from <paramref name="row"/>; what was below moves up. Every
    /// Reference is rewritten to keep naming the same cells, and one whose cells are all deleted
    /// becomes <c>#REF!</c> in the stored Formula, as in Excel (ADR-0047).
    /// </summary>
    public SheetChange DeleteRows(int row, int count = 1) => Restructure(new StructuralEdit(SheetAxis.Rows, row, count, false)).Change;

    /// <summary>
    /// Inserts columns, as <see cref="InsertRows"/> does rows: each new cell takes the Cell Format of
    /// the cell to its left, and each column the Cell Format and the width recorded on the column to
    /// its left, automatic or custom as it is (ADR-0046, ADR-0047, ADR-0063); columns inserted at
    /// <c>A</c> take none. A width set on a column moves with it, and one pushed off the right edge
    /// is dropped.
    /// </summary>
    /// <exception cref="SheetRefusedException">A cell holding an Entry would be pushed off the Sheet's right edge. Nothing changes.</exception>
    public SheetChange InsertColumns(int column, int count = 1) => Restructure(new StructuralEdit(SheetAxis.Columns, column, count, true)).Change;

    /// <summary>Deletes columns, as <see cref="DeleteRows"/> does rows: the widths set on them go, and those to their right move left with their columns (ADR-0046).</summary>
    public SheetChange DeleteColumns(int column, int count = 1) => Restructure(new StructuralEdit(SheetAxis.Columns, column, count, false)).Change;

    /// <summary>What a structural edit did, and what undoing it needs besides the inverse edit.</summary>
    internal sealed record StructuralOutcome(
        SheetChange Change,
        IReadOnlyList<(CellAddress Address, CellState State)> Dropped,
        IReadOnlyList<(CellAddress Address, Entry Entry)> Rewritten,
        Dictionary<int, AxisFormat> RowsBefore,
        Dictionary<int, AxisFormat> ColumnsBefore,
        Dictionary<int, SheetColumnWidth> WidthsBefore);

    /// <summary>Whether <paramref name="edit"/> would be refused, and why; nothing changes either way.</summary>
    internal SheetRefusal? CheckStructural(StructuralEdit edit)
    {
        edit.Validate();
        if (!edit.IsInsert) return null;
        foreach (var cell in _cells.Values)
        {
            // Only an Entry stops an insertion, as in Excel: a Cell Format pushed off is dropped, and a
            // Reference pushed off is cut at the edge or made #REF! (StructuralEdit.Map).
            if (cell.Entry is not null && edit.Move(cell.Address) is null)
            {
                return new SheetRefusal(SheetRefusalReason.EntriesWouldLeaveSheet,
                    $"{Capitalise(edit.Describe())} would push {cell.Address}, which holds an Entry, off the Sheet.");
            }
        }
        return null;
    }

    /// <summary>
    /// Gives each cell of the inserted rows (columns) the Cell Format of the cell above (to the left
    /// of) the insertion, every part of it — never its Entry (ADR-0046, ADR-0063).
    /// </summary>
    private void FormatInserted(StructuralEdit edit, List<Cell> moved)
    {
        var rows = edit.Axis == SheetAxis.Rows;
        foreach (var source in moved)
        {
            if ((rows ? source.Address.Row : source.Address.Column) != edit.Start - 1) continue;
            if (!source.IsFormatted) continue;
            for (var i = 0; i < edit.Count; i++)
            {
                var at = rows ? new CellAddress(edit.Start + i, source.Address.Column) : new CellAddress(source.Address.Row, edit.Start + i);
                var cell = _cells[at] = new Cell(at);
                cell.TakeFormatOf(source);
            }
        }
    }

    private static string Capitalise(string text) => char.ToUpperInvariant(text[0]) + text[1..];

    /// <param name="edit">The insertion or deletion.</param>
    /// <param name="formatInserted">
    /// Whether inserted rows or columns take the Cell Format of the one before them (ADR-0046). An
    /// undo's inverse insertion does not: it puts back what was deleted, exactly.
    /// </param>
    internal StructuralOutcome Restructure(StructuralEdit edit, bool formatInserted = true)
    {
        if (CheckStructural(edit) is { } refusal) throw new SheetRefusedException(refusal);

        var before = Snapshot();
        var shownBefore = ShownSnapshot();
        var rowsBefore = new Dictionary<int, AxisFormat>(_rowFormats);
        var columnsBefore = new Dictionary<int, AxisFormat>(_columnFormats);
        var widthsBefore = ColumnWidthsNow();
        var dropped = new List<(CellAddress, CellState)>();
        var rewritten = new List<(CellAddress, Entry)>();
        var moved = new List<Cell>();
        var dirty = new HashSet<CellAddress>();
        foreach (var cell in _cells.Values)
        {
            if (edit.Move(cell.Address) is not { } to)
            {
                dropped.Add((cell.Address, before[cell.Address]));
                continue;
            }
            var entry = cell.Entry;
            if (entry?.Parsed is { } parsed)
            {
                var mapped = ReferenceRewriter.Rewrite(entry, r => edit.Map(r, this));
                if (!ReferenceEquals(mapped, entry))
                {
                    rewritten.Add((cell.Address, entry));
                    dirty.Add(to);
                }
                else if (parsed.References.Any(r => edit.Reaches(r, this)))
                {
                    // A:A kept its text, but what it covers moved underneath it.
                    dirty.Add(to);
                }
                entry = mapped;
            }
            var movedCell = new Cell(to) { Entry = entry, Value = cell.Value };
            movedCell.TakeFormatOf(cell);
            moved.Add(movedCell);
        }

        _cells.Clear();
        foreach (var cell in moved) _cells[cell.Address] = cell;
        if (edit.IsInsert && formatInserted && edit.Start > 0) FormatInserted(edit, moved);
        ShiftAxisFormats(edit, formatInserted);
        ShiftColumnWidths(edit, formatInserted);
        RebuildDependencies();
        var recalculated = dirty.Count == 0 ? [] : Recalculate(dirty, []).Recalculated;
        var change = SheetChange.Merge([Diff(before, recalculated, shownBefore), new SheetChange([], [], [], WidthsChangedSince(widthsBefore))]);
        return new StructuralOutcome(change, dropped, rewritten, rowsBefore, columnsBefore, widthsBefore);
    }
}
