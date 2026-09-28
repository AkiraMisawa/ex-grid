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

    /// <summary>
    /// A number format set on a range (<see cref="Sheet.SetFormat(CellRange, NumberFormat?)"/>);
    /// <see langword="null"/> is General. Whole columns and whole rows are recorded as one entry
    /// each, and undoing the step puts back every level exactly (ADR-0047).
    /// </summary>
    public static SheetEdit SetFormat(CellRange range, NumberFormat? format) =>
        new StyleEdit(range, new StylePatch(format ?? NumberFormat.General, null));

    /// <summary>A horizontal alignment set on a range, recorded as <see cref="SetFormat(CellRange, NumberFormat?)"/> records a format.</summary>
    public static SheetEdit SetAlignment(CellRange range, HorizontalAlignment alignment)
    {
        if (!Enum.IsDefined(alignment)) throw new ArgumentOutOfRangeException(nameof(alignment), alignment, "Not an alignment.");
        return new StyleEdit(range, new StylePatch(null, alignment));
    }

    /// <summary>
    /// A number format, an alignment or both set on several ranges as one step (ADR-0046), such as
    /// a selection of several rectangles, whole columns and whole rows among them; each range is
    /// recorded as <see cref="SetFormat(CellRange, NumberFormat?)"/> records one. Here
    /// <see langword="null"/> leaves that property as it is, and <see cref="NumberFormat.General"/>
    /// sets General. Undoing the step puts back every level exactly.
    /// </summary>
    /// <exception cref="ArgumentException">There is no range, or the style sets neither property.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The alignment is not one.</exception>
    public static SheetEdit SetStyle(IEnumerable<CellRange> ranges, NumberFormat? format = null, HorizontalAlignment? alignment = null)
    {
        var (list, patch) = Sheet.CheckStyle(ranges, format, alignment);
        return new StylesEdit(list, patch);
    }

    /// <summary>
    /// A width the user set on every column <paramref name="columns"/> spans, in characters
    /// (<see cref="Sheet.SetColumnWidth"/>): <see cref="SheetColumnWidthKind.SetByUser"/>, so an
    /// entry never widens them; <see langword="null"/> puts them back at the default width. Undoing
    /// the step puts back every column's width, and its kind, exactly (ADR-0046).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The width is not more than 0 and at most <see cref="Sheet.MaxColumnWidth"/>.</exception>
    public static SheetEdit SetColumnWidth(CellRange columns, double? width) =>
        new ColumnWidthEdit(columns, Sheet.CheckColumnWidth(width) is { } w ? new SheetColumnWidth(w, SheetColumnWidthKind.SetByUser) : null);

    /// <summary>
    /// A width an entry widened every column <paramref name="columns"/> spans to, in characters
    /// (<see cref="Sheet.SetAutomaticColumnWidth"/>): <see cref="SheetColumnWidthKind.WidenedByEntry"/>,
    /// marked custom as Excel's file marks it, and widened again by a longer entry (ADR-0046,
    /// 2026-09-28; CW-028). It is the edit a component records a typed entry's widening with.
    /// Undoing the step puts back every column's width, and its kind, exactly.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The width is not more than 0 and at most <see cref="Sheet.MaxColumnWidth"/>.</exception>
    public static SheetEdit SetAutomaticColumnWidth(CellRange columns, double width) =>
        new ColumnWidthEdit(columns, new SheetColumnWidth(Sheet.CheckColumnWidth(width)!.Value, SheetColumnWidthKind.WidenedByEntry));

    /// <summary>Rows inserted (<see cref="Sheet.InsertRows"/>).</summary>
    public static SheetEdit InsertRows(int row, int count = 1) => new StructureEdit(new StructuralEdit(SheetAxis.Rows, row, count, true));

    /// <summary>Rows deleted (<see cref="Sheet.DeleteRows"/>).</summary>
    public static SheetEdit DeleteRows(int row, int count = 1) => new StructureEdit(new StructuralEdit(SheetAxis.Rows, row, count, false));

    /// <summary>Columns inserted (<see cref="Sheet.InsertColumns"/>).</summary>
    public static SheetEdit InsertColumns(int column, int count = 1) => new StructureEdit(new StructuralEdit(SheetAxis.Columns, column, count, true));

    /// <summary>Columns deleted (<see cref="Sheet.DeleteColumns"/>).</summary>
    public static SheetEdit DeleteColumns(int column, int count = 1) => new StructureEdit(new StructuralEdit(SheetAxis.Columns, column, count, false));

    /// <summary>
    /// A block copied inside ExSheet, pasted with its top-left cell at <paramref name="origin"/>:
    /// Entries with their relative References shifted by the distance pasted (a Reference shifted
    /// off the Sheet is <c>#REF!</c>, as in Excel), formats and alignment with them (ADR-0048). A
    /// block that would run past the Sheet's edge is refused by name (ADR-0050).
    /// </summary>
    public static SheetEdit Paste(SheetBlock block, CellAddress origin)
    {
        ArgumentNullException.ThrowIfNull(block);
        return new PlacedEdit(Sheet.OffSheet(origin, block.RowCount, block.ColumnCount), () => Sheet.Place(block, origin));
    }

    /// <summary>
    /// A block pasted over <paramref name="target"/>, repeated to fill it as Excel repeats a paste
    /// over a selection that is a whole multiple of the block's size.
    /// </summary>
    /// <exception cref="ArgumentException">The target is not a whole multiple of the block in both directions.</exception>
    public static SheetEdit Paste(SheetBlock block, CellRange target)
    {
        ArgumentNullException.ThrowIfNull(block);
        if (target.RowCount % block.RowCount != 0 || target.ColumnCount % block.ColumnCount != 0)
        {
            throw new ArgumentException($"A {target.RowCount} × {target.ColumnCount} target is not a whole multiple of a {block.RowCount} × {block.ColumnCount} block.", nameof(target));
        }
        return new PlacedEdit(null, () => Tiles(block, target).SelectMany(origin => Sheet.Place(block, origin)));

        static IEnumerable<CellAddress> Tiles(SheetBlock block, CellRange target)
        {
            for (var row = target.First.Row; row <= target.Last.Row; row += block.RowCount)
            {
                for (var column = target.First.Column; column <= target.Last.Column; column += block.ColumnCount) yield return new CellAddress(row, column);
            }
        }
    }

    /// <summary>
    /// Text pasted from another program, each field taken as if the user had typed it into its cell
    /// under the Sheet's culture: <c>=A1+1</c> becomes a Formula and <c>1,234</c> a number under
    /// <c>en-US</c> (ADR-0048). A field beginning with <c>=</c> that cannot be read as a Formula is
    /// taken as text, as Excel takes a pasted <c>=1+</c>; it is not refused.
    /// <paramref name="fields"/> is rectangular, rows of columns, with the
    /// top-left field at <paramref name="origin"/>; an empty field clears its cell. A block that
    /// would run past the Sheet's edge is refused by name (ADR-0050).
    /// </summary>
    /// <exception cref="ArgumentException">The rows are not all the same length, or there are none.</exception>
    public static SheetEdit PasteText(IReadOnlyList<IReadOnlyList<string>> fields, CellAddress origin)
    {
        ArgumentNullException.ThrowIfNull(fields);
        if (fields.Count == 0 || fields[0].Count == 0) throw new ArgumentException("Nothing to paste.", nameof(fields));
        var width = fields[0].Count;
        if (fields.Any(row => row is null || row.Count != width)) throw new ArgumentException("The pasted fields are not rectangular.", nameof(fields));
        var refusal = Sheet.OffSheet(origin, fields.Count, width);
        if (refusal is not null) return new RefusedEdit(refusal);
        var typed = new List<KeyValuePair<CellAddress, string>>();
        for (var r = 0; r < fields.Count; r++)
        {
            for (var c = 0; c < width; c++) typed.Add(new(new CellAddress(origin.Row + r, origin.Column + c), fields[r][c] ?? throw new ArgumentException("A field is null.", nameof(fields))));
        }
        return PasteText(typed);
    }

    /// <summary>
    /// Text pasted from another program, given field by field, each taken as <see cref="PasteText(IReadOnlyList{IReadOnlyList{string}}, CellAddress)"/>
    /// takes it: as if typed under the Sheet's culture, except that a field that cannot be read
    /// as a Formula is taken as text (ADR-0048). One operation.
    /// </summary>
    public static SheetEdit PasteText(IEnumerable<KeyValuePair<CellAddress, string>> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var list = fields.ToList();
        return new CellsEdit(list.Select(p => p.Key), sheet => sheet.Enter(list, pasted: true));
    }

    /// <summary>
    /// A Fill Intent resolved as Excel fills (ADR-0050, item 5): <paramref name="target"/> extends
    /// <paramref name="source"/> in <paramref name="direction"/>, given as the new cells alone or as
    /// the whole extended range. Formulas are copied with their References shifted, two or more
    /// numbers continue as Excel's linear trend, a single date goes on by day, and a single number,
    /// text without a pattern, booleans, Error Values and blanks are copied. Any other pattern is
    /// refused (<see cref="SheetRefusalReason.FillPatternNotSupported"/>), never filled with copies;
    /// <see cref="Sheet.Check"/> says so before anything is written.
    /// </summary>
    public static SheetEdit Fill(CellRange source, CellRange target, FillDirection direction)
    {
        if (!Enum.IsDefined(direction)) throw new ArgumentOutOfRangeException(nameof(direction), direction, "Not a fill direction.");
        return new FillEdit(source, target, direction, copyOnly: false);
    }

    /// <summary>
    /// Excel's fill keys, Ctrl+D and Ctrl+R (ADR-0035; ADR-0050, item 5, 2026-09-28):
    /// <paramref name="target"/> extends <paramref name="source"/> in <paramref name="direction"/>
    /// as a copy of it, repeated — Formulas with their relative References shifted, constants as
    /// they are, formats and alignment with them — and never as a series. Where the fill handle
    /// continues a date or two numbers, or refuses <c>Item 1</c>, the keys copy, as Excel's do.
    /// Only a target that does not extend the source along one axis is refused.
    /// </summary>
    public static SheetEdit FillCopy(CellRange source, CellRange target, FillDirection direction)
    {
        if (!Enum.IsDefined(direction)) throw new ArgumentOutOfRangeException(nameof(direction), direction, "Not a fill direction.");
        return new FillEdit(source, target, direction, copyOnly: true);
    }

    /// <summary>
    /// The Sheet renamed (<see cref="Sheet.Rename"/>): References qualified with the old name are
    /// rewritten to the new one. Undoing it puts back the name and those Formulas exactly.
    /// </summary>
    /// <exception cref="ArgumentException">The name is not one a Sheet can have (<see cref="Sheet.IsValidName"/>).</exception>
    public static SheetEdit Rename(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return new RenameEdit(Sheet.CheckName(name, nameof(name)));
    }

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
    /// Cells written whole — Entry, format and alignment — such as a paste of Entries or a fill:
    /// the states are computed when the edit is done, and a refusal known in advance stops it.
    /// </summary>
    internal sealed class PlacedEdit(SheetRefusal? refusal, Func<IEnumerable<(CellAddress Address, CellState State)>> states) : SheetEdit
    {
        internal override SheetRefusal? Check(Sheet sheet) => refusal;

        internal override SheetStep Apply(Sheet sheet)
        {
            var placed = sheet.Settle(states());
            return new CellsEdit(placed.Select(p => p.Address), s => s.Restore(placed)).Apply(sheet);
        }
    }

    /// <summary>A fill, planned against the Sheet as it stands when it is checked or done.</summary>
    internal sealed class FillEdit(CellRange source, CellRange target, FillDirection direction, bool copyOnly) : SheetEdit
    {
        internal override SheetRefusal? Check(Sheet sheet) => sheet.PlanFill(source, target, direction, copyOnly).Refusal;

        internal override SheetStep Apply(Sheet sheet)
        {
            var (refusal, states) = sheet.PlanFill(source, target, direction, copyOnly);
            if (refusal is not null) throw new SheetRefusedException(refusal);
            var settled = sheet.Settle(states);
            return new CellsEdit(settled.Select(s => s.Address), s => s.Restore(settled)).Apply(sheet);
        }
    }

    /// <summary>
    /// A format or alignment set on a range: undone by the row and column levels and the cells it
    /// touched put back exactly, redone by setting it again.
    /// </summary>
    internal sealed class StyleEdit(CellRange range, StylePatch patch) : SheetEdit
    {
        internal override SheetStep Apply(Sheet sheet)
        {
            var outcome = sheet.ApplyStyle(range, patch);
            return new SheetStep(sheet, outcome.Change, s => s.UndoStyle(outcome), s => s.ApplyStyle(range, patch).Change);
        }
    }

    /// <summary>A style set on several ranges: undone by every range's part undone in reverse, redone by setting it again.</summary>
    internal sealed class StylesEdit(IReadOnlyList<CellRange> ranges, StylePatch patch) : SheetEdit
    {
        internal override SheetStep Apply(Sheet sheet)
        {
            var outcome = sheet.ApplyStyles(ranges, patch);
            return new SheetStep(sheet, outcome.Change, s => s.UndoStyles(outcome), s => s.ApplyStyles(ranges, patch).Change);
        }
    }

    /// <summary>A width set on columns: undone by every column's width put back, redone by the widths it left.</summary>
    internal sealed class ColumnWidthEdit(CellRange columns, SheetColumnWidth? width) : SheetEdit
    {
        internal override SheetStep Apply(Sheet sheet)
        {
            var (change, before) = sheet.ApplyColumnWidth(columns, width);
            var after = sheet.ColumnWidthsNow();
            return new SheetStep(sheet, change, s => s.RestoreColumnWidths(before), s => s.RestoreColumnWidths(after));
        }
    }

    /// <summary>A rename: undone by the old name and the Formulas it rewrote put back.</summary>
    internal sealed class RenameEdit(string name) : SheetEdit
    {
        internal override SheetStep Apply(Sheet sheet)
        {
            var outcome = sheet.ApplyRename(name);
            return new SheetStep(sheet, outcome.Change, s => s.UndoRename(outcome), s => s.ApplyRename(name).Change);
        }
    }

    /// <summary>An operation refused before anything was computed for it.</summary>
    internal sealed class RefusedEdit(SheetRefusal refusal) : SheetEdit
    {
        internal override SheetRefusal? Check(Sheet sheet) => refusal;

        internal override SheetStep Apply(Sheet sheet) => throw new SheetRefusedException(refusal);
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
