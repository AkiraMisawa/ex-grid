using ExSheet.Engine.Formulas;

namespace ExSheet.Engine;

/// <summary>
/// One user operation on a Sheet, described before it is done: an edit, a paste, a format, an
/// insertion or deletion, a fill. <see cref="Sheet.Do"/> does it and returns the one
/// <see cref="SheetStep"/> that undoes it (ADR-0048): a paste over a thousand cells is one step.
/// </summary>
public abstract class SheetEdit
{
    private protected SheetEdit()
    {
    }

    /// <summary>Text typed into one cell, read under the Sheet's culture (<see cref="Sheet.Enter(CellAddress, string)"/>).</summary>
    public static SheetEdit Enter(CellAddress address, string typed) =>
        Enter([new KeyValuePair<CellAddress, string>(address, typed ?? throw new ArgumentNullException(nameof(typed)))]);

    /// <summary>Several typed texts as one operation, such as a paste from another program.</summary>
    public static SheetEdit Enter(IEnumerable<KeyValuePair<CellAddress, string>> typed)
    {
        ArgumentNullException.ThrowIfNull(typed);
        var list = typed.ToList();
        return new CellsEdit(list.Select(p => p.Key), sheet => sheet.Enter(list));
    }

    /// <summary>Entries set or cleared (<see langword="null"/>) as one operation.</summary>
    public static SheetEdit SetEntries(IEnumerable<KeyValuePair<CellAddress, Entry?>> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var list = entries.ToList();
        return new CellsEdit(list.Select(p => p.Key), sheet => sheet.SetEntries(list));
    }

    /// <summary>A number format set on cells; <see langword="null"/> is General.</summary>
    public static SheetEdit SetFormat(IEnumerable<CellAddress> addresses, NumberFormat? format)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        var list = addresses.ToList();
        return new CellsEdit(list, sheet => sheet.SetFormat(list, format));
    }

    /// <summary>A horizontal alignment set on cells.</summary>
    public static SheetEdit SetAlignment(IEnumerable<CellAddress> addresses, HorizontalAlignment alignment)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        var list = addresses.ToList();
        return new CellsEdit(list, sheet => sheet.SetAlignment(list, alignment));
    }

    /// <summary>Rows inserted (<see cref="Sheet.InsertRows"/>).</summary>
    public static SheetEdit InsertRows(int row, int count = 1) => new StructureEdit(new StructuralEdit(SheetAxis.Rows, row, count, true));

    /// <summary>Rows deleted (<see cref="Sheet.DeleteRows"/>).</summary>
    public static SheetEdit DeleteRows(int row, int count = 1) => new StructureEdit(new StructuralEdit(SheetAxis.Rows, row, count, false));

    /// <summary>Columns inserted (<see cref="Sheet.InsertColumns"/>).</summary>
    public static SheetEdit InsertColumns(int column, int count = 1) => new StructureEdit(new StructuralEdit(SheetAxis.Columns, column, count, true));

    /// <summary>Columns deleted (<see cref="Sheet.DeleteColumns"/>).</summary>
    public static SheetEdit DeleteColumns(int column, int count = 1) => new StructureEdit(new StructuralEdit(SheetAxis.Columns, column, count, false));

    /// <summary>Whether the Sheet would refuse this operation as it stands, and why. Changes nothing.</summary>
    internal virtual SheetRefusal? Check(Sheet sheet) => null;

    internal abstract SheetStep Apply(Sheet sheet);

    /// <summary>
    /// An operation on cells in place: undone by putting back what those cells recorded before,
    /// redone by putting back what they recorded after.
    /// </summary>
    internal sealed class CellsEdit(IEnumerable<CellAddress> touched, Func<Sheet, SheetChange> apply) : SheetEdit
    {
        internal override SheetStep Apply(Sheet sheet)
        {
            var addresses = touched.Distinct().ToList();
            var before = sheet.Record(addresses);
            var change = apply(sheet);
            var after = sheet.Record(addresses);
            return new SheetStep(sheet, change, s => s.Restore(before), s => s.Restore(after));
        }
    }

    /// <summary>
    /// An insertion or deletion: undone by the inverse edit, then putting back the Formulas it
    /// rewrote and the cells it dropped, exactly as they were — the inverse alone does not restore
    /// a range a deletion shrank, nor a Reference it made <c>#REF!</c>.
    /// </summary>
    internal sealed class StructureEdit(StructuralEdit edit) : SheetEdit
    {
        internal override SheetRefusal? Check(Sheet sheet) => sheet.CheckStructural(edit);

        internal override SheetStep Apply(Sheet sheet)
        {
            var outcome = sheet.Restructure(edit);
            return new SheetStep(sheet, outcome.Change, s => s.Unrestructure(edit, outcome), s => s.Restructure(edit).Change);
        }
    }
}
