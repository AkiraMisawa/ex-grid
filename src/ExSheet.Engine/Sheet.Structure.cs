using ExSheet.Engine.Formulas;

namespace ExSheet.Engine;

public sealed partial class Sheet
{
    /// <summary>
    /// Inserts <paramref name="count"/> rows at <paramref name="row"/>; what was there and below
    /// moves down, and every Reference is rewritten to keep naming the same cells (ADR-0046/0047).
    /// The new rows hold no Entries; each of their cells takes the number format and alignment of
    /// the cell above it, and each row the format set on the row above, as Excel's default does
    /// (ADR-0046, ADR-0047). Rows inserted at the top take none. A format set on a row moves with
    /// it, and one pushed off the bottom edge is dropped.
    /// </summary>
    /// <exception cref="SheetRefusedException">
    /// A cell holding an Entry, or the cells a Reference names, would be pushed off the Sheet's
    /// bottom edge. Nothing changes.
    /// </exception>
    public SheetChange InsertRows(int row, int count = 1) => Restructure(new StructuralEdit(SheetAxis.Rows, row, count, true)).Change;

    /// <summary>
    /// Deletes <paramref name="count"/> rows from <paramref name="row"/>; what was below moves up. Every
    /// Reference is rewritten to keep naming the same cells, and one whose cells are all deleted
    /// becomes <c>#REF!</c> in the stored Formula, as in Excel (ADR-0047).
    /// </summary>
    public SheetChange DeleteRows(int row, int count = 1) => Restructure(new StructuralEdit(SheetAxis.Rows, row, count, false)).Change;

    /// <summary>
    /// Inserts columns, as <see cref="InsertRows"/> does rows: each new cell takes the number format
    /// and alignment of the cell to its left, and each column the format and the width set on the
    /// column to its left (ADR-0046, ADR-0047); columns inserted at <c>A</c> take none. A width set
    /// on a column moves with it, and one pushed off the right edge is dropped.
    /// </summary>
    /// <exception cref="SheetRefusedException">Something would be pushed off the Sheet's right edge. Nothing changes.</exception>
    public SheetChange InsertColumns(int column, int count = 1) => Restructure(new StructuralEdit(SheetAxis.Columns, column, count, true)).Change;

    /// <summary>Deletes columns, as <see cref="DeleteRows"/> does rows: the widths set on them go, and those to their right move left with their columns (ADR-0046).</summary>
    public SheetChange DeleteColumns(int column, int count = 1) => Restructure(new StructuralEdit(SheetAxis.Columns, column, count, false)).Change;

    /// <summary>What a structural edit did, and what undoing it needs besides the inverse edit.</summary>
    internal sealed record StructuralOutcome(
        SheetChange Change,
        IReadOnlyList<(CellAddress Address, CellState State)> Dropped,
        IReadOnlyList<(CellAddress Address, Entry Entry)> Rewritten,
        Dictionary<int, AxisStyle> RowsBefore,
        Dictionary<int, AxisStyle> ColumnsBefore,
        Dictionary<int, double> WidthsBefore);

    /// <summary>Whether <paramref name="edit"/> would be refused, and why; nothing changes either way.</summary>
    internal SheetRefusal? CheckStructural(StructuralEdit edit)
    {
        edit.Validate();
        foreach (var cell in _cells.Values)
        {
            var to = edit.Move(cell.Address);
            if (to is null)
            {
                if (edit.IsInsert && cell.Entry is not null)
                {
                    return new SheetRefusal(SheetRefusalReason.EntriesWouldLeaveSheet,
                        $"{Capitalise(edit.Describe())} would push {cell.Address}, which holds an Entry, off the Sheet.");
                }
                continue;
            }
            if (cell.Entry?.Parsed is not { } parsed) continue;
            foreach (var reference in parsed.References)
            {
                edit.Map(reference, this, out var leaves);
                if (leaves)
                {
                    return new SheetRefusal(SheetRefusalReason.ReferenceWouldLeaveSheet,
                        $"{Capitalise(edit.Describe())} would push the cells a Reference in {cell.Address} names off the Sheet.");
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Gives each cell of the inserted rows (columns) the number format and alignment of the cell
    /// above (to the left of) the insertion — never its Entry (ADR-0046).
    /// </summary>
    private void FormatInserted(StructuralEdit edit, List<Cell> moved)
    {
        var rows = edit.Axis == SheetAxis.Rows;
        foreach (var source in moved)
        {
            if ((rows ? source.Address.Row : source.Address.Column) != edit.Start - 1) continue;
            if (source.Format is null && source.Alignment is null) continue;
            for (var i = 0; i < edit.Count; i++)
            {
                var at = rows ? new CellAddress(edit.Start + i, source.Address.Column) : new CellAddress(source.Address.Row, edit.Start + i);
                _cells[at] = new Cell(at) { Format = source.Format, Alignment = source.Alignment };
            }
        }
    }

    private static string Capitalise(string text) => char.ToUpperInvariant(text[0]) + text[1..];

    /// <param name="edit">The insertion or deletion.</param>
    /// <param name="formatInserted">
    /// Whether inserted rows or columns take the formatting of the one before them (ADR-0046). An
    /// undo's inverse insertion does not: it puts back what was deleted, exactly.
    /// </param>
    internal StructuralOutcome Restructure(StructuralEdit edit, bool formatInserted = true)
    {
        if (CheckStructural(edit) is { } refusal) throw new SheetRefusedException(refusal);

        var before = Snapshot();
        var shownBefore = ShownSnapshot();
        var rowsBefore = new Dictionary<int, AxisStyle>(_rowStyles);
        var columnsBefore = new Dictionary<int, AxisStyle>(_columnStyles);
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
                var mapped = ReferenceRewriter.Rewrite(entry, r => edit.Map(r, this, out _));
                if (!ReferenceEquals(mapped, entry))
                {
                    rewritten.Add((cell.Address, entry));
                    dirty.Add(to);
                }
                else if (parsed.References.Any(r => edit.Reaches(r, this)))
                {
                    // A1:A1048576 or A:A kept its text, but what it covers moved underneath it.
                    dirty.Add(to);
                }
                entry = mapped;
            }
            moved.Add(new Cell(to) { Entry = entry, Value = cell.Value, Format = cell.Format, Alignment = cell.Alignment });
        }

        _cells.Clear();
        foreach (var cell in moved) _cells[cell.Address] = cell;
        if (edit.IsInsert && formatInserted && edit.Start > 0) FormatInserted(edit, moved);
        ShiftAxisStyles(edit, formatInserted);
        ShiftColumnWidths(edit, formatInserted);
        RebuildDependencies();
        var recalculated = dirty.Count == 0 ? [] : Recalculate(dirty, []).Recalculated;
        var change = SheetChange.Merge([Diff(before, recalculated, shownBefore), new SheetChange([], [], [], WidthsChangedSince(widthsBefore))]);
        return new StructuralOutcome(change, dropped, rewritten, rowsBefore, columnsBefore, widthsBefore);
    }
}
