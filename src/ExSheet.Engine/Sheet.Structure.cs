using ExSheet.Engine.Formulas;

namespace ExSheet.Engine;

public sealed partial class Sheet
{
    /// <summary>
    /// Inserts <paramref name="count"/> blank rows at <paramref name="row"/>; what was there and below
    /// moves down, and every Reference is rewritten to keep naming the same cells (ADR-0046/0047).
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

    /// <summary>Inserts blank columns, as <see cref="InsertRows"/> does rows.</summary>
    /// <exception cref="SheetRefusedException">Something would be pushed off the Sheet's right edge. Nothing changes.</exception>
    public SheetChange InsertColumns(int column, int count = 1) => Restructure(new StructuralEdit(SheetAxis.Columns, column, count, true)).Change;

    /// <summary>Deletes columns, as <see cref="DeleteRows"/> does rows.</summary>
    public SheetChange DeleteColumns(int column, int count = 1) => Restructure(new StructuralEdit(SheetAxis.Columns, column, count, false)).Change;

    /// <summary>What a structural edit did, and what undoing it needs besides the inverse edit.</summary>
    internal sealed record StructuralOutcome(
        SheetChange Change,
        IReadOnlyList<(CellAddress Address, CellState State)> Dropped,
        IReadOnlyList<(CellAddress Address, Entry Entry)> Rewritten);

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

    private static string Capitalise(string text) => char.ToUpperInvariant(text[0]) + text[1..];

    internal StructuralOutcome Restructure(StructuralEdit edit)
    {
        if (CheckStructural(edit) is { } refusal) throw new SheetRefusedException(refusal);

        var before = Snapshot();
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
        RebuildDependencies();
        var recalculated = dirty.Count == 0 ? [] : Recalculate(dirty, []).Recalculated;
        return new StructuralOutcome(Diff(before, recalculated), dropped, rewritten);
    }
}
