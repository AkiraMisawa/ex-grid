using ExSheet.Engine.Formulas;

namespace ExSheet.Engine;

/// <summary>
/// One user operation on a Sheet, described before it is done: an edit, a paste, a Cell Format, an
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

    /// <summary>A Number Format set on cells; <see langword="null"/> is General.</summary>
    public static SheetEdit SetNumberFormat(IEnumerable<CellAddress> addresses, NumberFormat? format)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        var list = addresses.ToList();
        return new CellsEdit(list, sheet => sheet.SetNumberFormat(list, format));
    }

    /// <summary>A horizontal alignment set on cells.</summary>
    public static SheetEdit SetAlignment(IEnumerable<CellAddress> addresses, HorizontalAlignment alignment)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        var list = addresses.ToList();
        return new CellsEdit(list, sheet => sheet.SetAlignment(list, alignment));
    }

    /// <summary>
    /// A Number Format set on a range (<see cref="Sheet.SetNumberFormat(CellRange, NumberFormat?)"/>);
    /// <see langword="null"/> is General. Whole columns and whole rows are recorded as one entry
    /// each, and undoing the step puts back every level exactly (ADR-0047).
    /// </summary>
    public static SheetEdit SetNumberFormat(CellRange range, NumberFormat? format) =>
        new CellFormatEdit(range, new CellFormatChange { NumberFormat = format ?? NumberFormat.General });

    /// <summary>A horizontal alignment set on a range, recorded as <see cref="SetNumberFormat(CellRange, NumberFormat?)"/> records a Number Format.</summary>
    public static SheetEdit SetAlignment(CellRange range, HorizontalAlignment alignment)
    {
        if (!Enum.IsDefined(alignment)) throw new ArgumentOutOfRangeException(nameof(alignment), alignment, "Not an alignment.");
        return new CellFormatEdit(range, new CellFormatChange { Alignment = alignment });
    }

    /// <summary>
    /// The parts <paramref name="change"/> names set on several ranges as one step (ADR-0046,
    /// ADR-0071), such as a selection of several rectangles, whole columns and whole rows among
    /// them; each range is recorded as <see cref="SetNumberFormat(CellRange, NumberFormat?)"/>
    /// records one, and every part the change does not name stays as each cell has it. The change's
    /// Borders are relative to each range, so each range gets its own outline. Undoing the step puts
    /// back every level exactly (<see cref="Sheet.SetCellFormat"/>).
    /// </summary>
    /// <exception cref="ArgumentException">There is no range, or the change sets nothing.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The alignment is not one.</exception>
    public static SheetEdit SetCellFormat(IEnumerable<CellRange> ranges, CellFormatChange change) =>
        new CellFormatsEdit(Sheet.CheckCellFormat(ranges, change), change);

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
    /// off the Sheet is <c>#REF!</c>, as in Excel), Cell Formats with them (ADR-0048, ADR-0071). A
    /// block that would run past the Sheet's edge is refused by name (ADR-0050).
    /// </summary>
    public static SheetEdit Paste(SheetBlock block, CellAddress origin)
    {
        ArgumentNullException.ThrowIfNull(block);
        var refusal = Sheet.OffSheet(origin, block.RowCount, block.ColumnCount);
        return new PlacedEdit(refusal, () => Sheet.Place(block, origin),
            refusal is null ? [new CellRange(origin, new CellAddress(origin.Row + block.RowCount - 1, origin.Column + block.ColumnCount - 1))] : []);
    }

    /// <summary>
    /// A block pasted over <paramref name="target"/>, repeated to fill it as Excel repeats a paste
    /// over a selection that is a whole multiple of the block's size.
    /// </summary>
    /// <exception cref="ArgumentException">The target is not a whole multiple of the block in both directions.</exception>
    public static SheetEdit Paste(SheetBlock block, CellRange target)
    {
        ArgumentNullException.ThrowIfNull(block);
        CheckWholeMultiple(block, target, nameof(target));
        return new PlacedEdit(null, () => Tiles(block, target).SelectMany(origin => Sheet.Place(block, origin)), [target]);
    }

    /// <summary>
    /// A block copied inside ExSheet, pasted over every range of <paramref name="targets"/> as one
    /// operation (ADR-0048, ADR-0014): a range the block's size takes it at its top-left, and a range
    /// that is a whole multiple of it takes it repeated, as <see cref="Paste(SheetBlock, CellRange)"/>
    /// repeats it. A paste onto a Selection of several ranges is one step on the undo stack, and one
    /// change, however many ranges it writes (ADR-0180). A cell two ranges share is written once, with
    /// what the later range gives it.
    /// </summary>
    /// <exception cref="ArgumentException">There is no range, or one is not a whole multiple of the block in both directions.</exception>
    public static SheetEdit Paste(SheetBlock block, IEnumerable<CellRange> targets)
    {
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(targets);
        var ranges = targets.ToList();
        if (ranges.Count == 0) throw new ArgumentException("There is no range to paste over.", nameof(targets));
        foreach (var target in ranges) CheckWholeMultiple(block, target, nameof(targets));
        return new PlacedEdit(null, () =>
        {
            var states = new Dictionary<CellAddress, CellState>();
            foreach (var (address, state) in ranges.SelectMany(target => Tiles(block, target)).SelectMany(origin => Sheet.Place(block, origin))) states[address] = state;
            return states.Select(p => (p.Key, p.Value));
        }, ranges);
    }

    private static void CheckWholeMultiple(SheetBlock block, CellRange target, string parameter)
    {
        if (target.RowCount % block.RowCount != 0 || target.ColumnCount % block.ColumnCount != 0)
        {
            throw new ArgumentException($"A {target.RowCount} × {target.ColumnCount} target is not a whole multiple of a {block.RowCount} × {block.ColumnCount} block.", parameter);
        }
    }

    /// <summary>The top-left cell of each copy of <paramref name="block"/> that fills <paramref name="target"/>.</summary>
    private static IEnumerable<CellAddress> Tiles(SheetBlock block, CellRange target)
    {
        for (var row = target.First.Row; row <= target.Last.Row; row += block.RowCount)
        {
            for (var column = target.First.Column; column <= target.Last.Column; column += block.ColumnCount) yield return new CellAddress(row, column);
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
    /// Text pasted from another program, some of whose fields carry their exact Value: the typed
    /// <paramref name="fields"/> are taken as <see cref="PasteText(IEnumerable{KeyValuePair{CellAddress, string}})"/>
    /// takes them, and <paramref name="entries"/> are set as they are, such as an invariant number
    /// with every digit its double holds (ADR-0048, ADR-0050 item 10). The fields are read first,
    /// so a format typing gives a cell is given before the Entries are set; a cell named in both
    /// takes its Entry. One operation: one step on the undo stack, and one change (ADR-0180).
    /// </summary>
    public static SheetEdit PasteText(IEnumerable<KeyValuePair<CellAddress, string>> fields, IEnumerable<KeyValuePair<CellAddress, Entry?>> entries)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(entries);
        var typed = fields.ToList();
        var exact = entries.ToList();
        return new CellsEdit(typed.Select(p => p.Key).Concat(exact.Select(p => p.Key)),
            sheet => SheetChange.Merge([sheet.Enter(typed, pasted: true), sheet.SetEntries(exact)]));
    }

    /// <summary>
    /// A Fill Intent resolved as Excel fills (ADR-0050, item 5): <paramref name="target"/> extends
    /// <paramref name="source"/> in <paramref name="direction"/>, given as the new cells alone or as
    /// the whole extended range. Formulas are copied with their References shifted, two or more
    /// numbers continue as Excel's linear trend, a single date goes on by day, a single number in a
    /// line of a fill from whole columns or whole rows goes on by one, and any other single number,
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
    /// they are, Cell Formats with them — and never as a series. Where the fill handle
    /// continues a date or two numbers, or refuses <c>Item 1</c>, the keys copy, as Excel's do.
    /// Only a target that does not extend the source along one axis is refused.
    /// </summary>
    public static SheetEdit FillCopy(CellRange source, CellRange target, FillDirection direction)
    {
        if (!Enum.IsDefined(direction)) throw new ArgumentOutOfRangeException(nameof(direction), direction, "Not a fill direction.");
        return new FillEdit(source, target, direction, copyOnly: true);
    }

    /// <summary>
    /// Excel's Ctrl+Enter (ADR-0050, item 5, 2026-09-28): <paramref name="typed"/>, read under the
    /// Sheet's culture as entered in <paramref name="enteredAt"/>, written into every cell of
    /// <paramref name="targets"/> as one operation. A Formula — <c>=</c>, or a signed text such as
    /// <c>-A1</c> that Excel reads as one — is written as entered in <paramref name="enteredAt"/>, and
    /// into every other cell with its relative References shifted by that cell's offset from
    /// <paramref name="enteredAt"/>, by the rule <see cref="FillCopy"/> shifts them: absolute parts
    /// stay, and a Reference shifted off the Sheet is <c>#REF!</c>. Anything else is entered into
    /// every cell as typed. No Cell Format is copied from <paramref name="enteredAt"/>; each
    /// cell takes what typing implies for it, as Excel's Ctrl+Enter does. Overlapping ranges write
    /// their shared cells once.
    /// </summary>
    /// <exception cref="ArgumentException">There is no target, or <paramref name="enteredAt"/> lies outside every target.</exception>
    /// <exception cref="FormulaSyntaxException">Thrown when the edit is done: the text is a Formula that cannot be read; nothing changes.</exception>
    public static SheetEdit EnterInto(IEnumerable<CellRange> targets, CellAddress enteredAt, string typed)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(typed);
        var ranges = targets.ToList();
        if (ranges.Count == 0) throw new ArgumentException("There is no cell to enter into.", nameof(targets));
        if (!ranges.Any(range => range.Contains(enteredAt)))
        {
            throw new ArgumentException($"{enteredAt} is not inside any target range.", nameof(enteredAt));
        }
        var cells = ranges.SelectMany(range => range.Cells()).Distinct().ToList();
        return new CellsEdit(cells, sheet => sheet.EnterInto(cells, enteredAt, typed), FocusFirst(ranges, enteredAt));
    }

    /// <summary>
    /// The Sheet renamed (ADR-0046): every Reference qualified with the old name is rewritten to the
    /// new one, as Excel rewrites them, and a Formula whose qualifier now names or stops naming the
    /// Sheet is recalculated. A rename is a change, so <c>RAND</c> and <c>RANDBETWEEN</c> draw again,
    /// as Excel's do (ADR-0180); a rename to the name the Sheet already has changes nothing. Undoing
    /// it puts back the name and those Formulas exactly.
    /// </summary>
    /// <exception cref="ArgumentException">The name is not one a Sheet can have (<see cref="Sheet.IsValidName"/>).</exception>
    /// <remarks>On a Sheet of a Workbook, every Reference on every Sheet qualified with the old name is rewritten; a name another Sheet has, without regard to case, is refused (<see cref="SheetRefusalReason.SheetNameTaken"/>; ADR-0331).</remarks>
    public static SheetEdit Rename(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return new RenameEdit(Sheet.CheckName(name, nameof(name)));
    }

    /// <summary>
    /// A new, empty Sheet inserted beside the Sheet the edit is done on — after it, as Excel's <c>+</c>
    /// button adds one, or before it, as Shift+F11 does — named <paramref name="name"/>, or <c>Sheet</c>
    /// and the next number of the Workbook's count of the Sheets it has made, as Excel numbers one
    /// (ADR-0331): with Sheet1 renamed Sheet5, Sheet2. Undoing it takes the Sheet out again, and gives
    /// its number back. The step's <see cref="WorkbookStep.Sheet"/> is the new Sheet.
    /// </summary>
    /// <exception cref="ArgumentException">The name is not one a Sheet can have (<see cref="Sheet.IsValidName"/>).</exception>
    public static SheetEdit InsertSheet(string? name = null, bool before = false) =>
        new InsertSheetEdit(name is null ? null : Sheet.CheckName(name, nameof(name)), before);

    /// <summary>
    /// The Sheet deleted (ADR-0331): every Reference on another Sheet qualified with its name becomes
    /// <c>#REF!</c>. Unlike Excel, nothing is asked first, and undoing it gives back the Sheet and those
    /// Formulas exactly. The Workbook's last Sheet is not deleted (<see cref="SheetRefusalReason.LastSheet"/>).
    /// </summary>
    public static SheetEdit DeleteSheet() => new DeleteSheetEdit();

    /// <summary>The Sheet moved to <paramref name="index"/> in the order of the Sheets after the move (ADR-0331).</summary>
    /// <exception cref="ArgumentOutOfRangeException">The index is negative.</exception>
    public static SheetEdit MoveSheet(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        return new MoveSheetEdit(index);
    }

    /// <summary>
    /// A copy of the Sheet — its Entries, Cell Formats and column widths — named as Excel names one,
    /// <c>Name (2)</c>, placed at <paramref name="index"/> in the order after the copy, or just after the
    /// Sheet (ADR-0331). A Reference in it qualified with the Sheet's name names the copy. The step's
    /// <see cref="WorkbookStep.Sheet"/> is the copy.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The index is negative.</exception>
    public static SheetEdit CopySheet(int? index = null)
    {
        if (index is { } at) ArgumentOutOfRangeException.ThrowIfNegative(at, nameof(index));
        return new CopySheetEdit(index);
    }

    /// <summary>Whether the Sheet would refuse this operation as it stands, and why. Changes nothing.</summary>
    internal virtual SheetRefusal? Check(Sheet sheet) => null;

    internal abstract SheetStep Apply(Sheet sheet);

    /// <summary>
    /// The ranges the operation changes on its Sheet, the one it acts from first: what an undo or a redo
    /// of it selects, as Excel's do (ADR-0331 §3). None for an operation on the Sheets themselves.
    /// </summary>
    internal virtual IReadOnlyList<CellRange> Ranges => [];

    /// <summary>The smallest range holding every one of <paramref name="cells"/>; none for no cells.</summary>
    internal static IReadOnlyList<CellRange> Bounding(IEnumerable<CellAddress> cells)
    {
        int top = int.MaxValue, left = int.MaxValue, bottom = -1, right = -1;
        foreach (var cell in cells)
        {
            top = Math.Min(top, cell.Row);
            left = Math.Min(left, cell.Column);
            bottom = Math.Max(bottom, cell.Row);
            right = Math.Max(right, cell.Column);
        }
        return bottom < 0 ? [] : [new CellRange(new CellAddress(top, left), new CellAddress(bottom, right))];
    }

    /// <summary><paramref name="ranges"/> with the one holding <paramref name="cell"/> first.</summary>
    private static IReadOnlyList<CellRange> FocusFirst(IReadOnlyList<CellRange> ranges, CellAddress cell)
    {
        var at = ranges.ToList().FindIndex(range => range.Contains(cell));
        return at <= 0 ? ranges : [ranges[at], .. ranges.Where((_, i) => i != at)];
    }

    /// <summary>
    /// An operation on cells in place: undone by putting back what those cells recorded before,
    /// redone by putting back what they recorded after.
    /// </summary>
    internal sealed class CellsEdit(IEnumerable<CellAddress> touched, Func<Sheet, SheetChange> apply, IReadOnlyList<CellRange>? ranges = null) : SheetEdit
    {
        // The cells it names, unless it was given the ranges they were named from.
        internal override IReadOnlyList<CellRange> Ranges => ranges ??= Bounding(touched);

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
    /// Cells written whole — Entry and Cell Format — such as a paste of Entries or a fill:
    /// the states are computed when the edit is done, and a refusal known in advance stops it.
    /// </summary>
    internal sealed class PlacedEdit(SheetRefusal? refusal, Func<IEnumerable<(CellAddress Address, CellState State)>> states, IReadOnlyList<CellRange> ranges) : SheetEdit
    {
        internal override SheetRefusal? Check(Sheet sheet) => refusal;

        internal override IReadOnlyList<CellRange> Ranges => ranges;

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

        // The source and what it was extended over, as Excel's fill leaves them selected.
        internal override IReadOnlyList<CellRange> Ranges =>
            [new CellRange(
                new CellAddress(Math.Min(source.First.Row, target.First.Row), Math.Min(source.First.Column, target.First.Column)),
                new CellAddress(Math.Max(source.Last.Row, target.Last.Row), Math.Max(source.Last.Column, target.Last.Column)))];

        internal override SheetStep Apply(Sheet sheet)
        {
            var plan = sheet.PlanFill(source, target, direction, copyOnly);
            if (plan.Refusal is not null) throw new SheetRefusedException(plan.Refusal);
            // From whole columns or whole rows: the target's own formats, then its cells.
            if (plan.Levels is not null) return sheet.DoFillWithLevels(plan);
            var settled = sheet.Settle(plan.States);
            return new CellsEdit(settled.Select(s => s.Address), s => s.Restore(settled)).Apply(sheet);
        }
    }

    /// <summary>
    /// A change set on a range: undone by the row and column levels and the cells it touched put
    /// back exactly, redone by setting it again.
    /// </summary>
    internal sealed class CellFormatEdit(CellRange range, CellFormatChange change) : SheetEdit
    {
        internal override IReadOnlyList<CellRange> Ranges => [range];

        internal override SheetStep Apply(Sheet sheet)
        {
            var outcome = sheet.ApplyCellFormat(range, change);
            return new SheetStep(sheet, outcome.Change, s => s.UndoCellFormat(outcome), s => s.ApplyCellFormat(range, change).Change);
        }
    }

    /// <summary>A change set on several ranges: undone by every range's part undone in reverse, redone by setting it again.</summary>
    internal sealed class CellFormatsEdit(IReadOnlyList<CellRange> ranges, CellFormatChange change) : SheetEdit
    {
        internal override IReadOnlyList<CellRange> Ranges => ranges;

        internal override SheetStep Apply(Sheet sheet)
        {
            var outcome = sheet.ApplyCellFormats(ranges, change);
            return new SheetStep(sheet, outcome.Change, s => s.UndoCellFormats(outcome), s => s.ApplyCellFormats(ranges, change).Change);
        }
    }

    /// <summary>A width set on columns: undone by every column's width put back, redone by the widths it left.</summary>
    internal sealed class ColumnWidthEdit(CellRange columns, SheetColumnWidth? width) : SheetEdit
    {
        internal override IReadOnlyList<CellRange> Ranges => [columns];

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
        internal override SheetRefusal? Check(Sheet sheet) => sheet.Workbook.NameTaken(name, sheet);

        internal override SheetStep Apply(Sheet sheet)
        {
            var outcome = sheet.ApplyRename(name);
            return new SheetStep(sheet, outcome.Change, s => s.UndoRename(outcome), s => s.ApplyRename(name).Change);
        }
    }

    /// <summary>
    /// An operation on the Workbook's Sheets rather than on one Sheet's cells (ADR-0331). Done through
    /// <see cref="Sheet.Do"/>, its step reports what it changed on that Sheet.
    /// </summary>
    internal abstract class WorkbookEdit : SheetEdit
    {
        /// <summary>What doing it changed on the Sheet it names, and how to undo and redo it.</summary>
        internal sealed record Done(Sheet Sheet, SheetChange Change, Func<SheetChange> Undo, Func<SheetChange> Redo);

        internal abstract SheetRefusal? Check(Workbook workbook, Sheet sheet);

        internal abstract Done Apply(Workbook workbook, Sheet sheet);

        internal override SheetRefusal? Check(Sheet sheet) => Check(sheet.Workbook, sheet);

        internal override SheetStep Apply(Sheet sheet)
        {
            var done = Apply(sheet.Workbook, sheet);
            return new SheetStep(done.Sheet, done.Change, _ => done.Undo(), _ => done.Redo());
        }

        /// <summary>
        /// A new Sheet's step, an insertion's or a copy's: undone, the Sheet is taken out and the count of Sheets
        /// made put back (<see cref="Workbook.UncountSheetMade"/>); redone, it is put back and counted again.
        /// </summary>
        private protected static Done Counted(Workbook workbook, Sheet made, int index, int? numbered, int counted, SheetChange change) =>
            new(made, change, () =>
            {
                workbook.UncountSheetMade(counted);
                return workbook.TakeOut(made, delete: false, []);
            }, () =>
            {
                workbook.CountSheetMade(numbered);
                return workbook.Reinsert(made, index);
            });

        private protected static int CheckIndex(int index, int count)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(index, count - 1, nameof(index));
            return index;
        }
    }

    internal sealed class InsertSheetEdit(string? name, bool before) : WorkbookEdit
    {
        internal override SheetRefusal? Check(Workbook workbook, Sheet sheet) => name is null ? null : workbook.NameTaken(name, null);

        internal override Done Apply(Workbook workbook, Sheet sheet)
        {
            var index = workbook.IndexOf(sheet) + (before ? 0 : 1);
            int? numbered = name is null ? workbook.NextSheetNumber() : null;
            var counted = workbook.CountSheetMade(numbered);
            var inserted = workbook.InsertSheet(index, name ?? Workbook.SheetName(numbered!.Value), out var change);
            return Counted(workbook, inserted, index, numbered, counted, change);
        }
    }

    internal sealed class DeleteSheetEdit : WorkbookEdit
    {
        internal override SheetRefusal? Check(Workbook workbook, Sheet sheet) =>
            workbook.Sheets.Count == 1 ? new SheetRefusal(SheetRefusalReason.LastSheet, $"'{sheet.Name}' is the Workbook's only Sheet, and a Workbook holds at least one.") : null;

        internal override Done Apply(Workbook workbook, Sheet sheet)
        {
            var index = workbook.IndexOf(sheet);
            var shown = workbook.Shown == sheet;
            var rewritten = new List<(Sheet, CellAddress, Entry)>();
            var change = workbook.TakeOut(sheet, delete: true, rewritten);
            return new Done(sheet, change, () => workbook.PutBack(sheet, index, rewritten, shown), () =>
            {
                rewritten.Clear();
                return workbook.TakeOut(sheet, delete: true, rewritten);
            });
        }
    }

    internal sealed class MoveSheetEdit(int index) : WorkbookEdit
    {
        internal override SheetRefusal? Check(Workbook workbook, Sheet sheet) => null;

        internal override Done Apply(Workbook workbook, Sheet sheet)
        {
            var to = CheckIndex(index, workbook.Sheets.Count);
            var from = workbook.IndexOf(sheet);
            if (from == to) return new Done(sheet, SheetChange.None, () => SheetChange.None, () => SheetChange.None);
            var rewritten = new List<(Sheet, CellAddress, Entry)>();
            var change = workbook.Move(sheet, to, rewritten);
            return new Done(sheet, change, () => workbook.MoveBack(sheet, from, rewritten), () =>
            {
                rewritten.Clear();
                return workbook.Move(sheet, to, rewritten);
            });
        }
    }

    internal sealed class CopySheetEdit(int? index) : WorkbookEdit
    {
        internal override SheetRefusal? Check(Workbook workbook, Sheet sheet) => null;

        internal override Done Apply(Workbook workbook, Sheet sheet)
        {
            var at = index is { } given ? CheckIndex(given, workbook.Sheets.Count + 1) : workbook.IndexOf(sheet) + 1;
            var counted = workbook.CountSheetMade(null);
            var copy = workbook.Copy(sheet, at, workbook.CopyName(sheet.Name), out var change);
            return Counted(workbook, copy, at, null, counted, change);
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

        // The whole rows or columns inserted, or deleted.
        internal override IReadOnlyList<CellRange> Ranges => edit.Axis == SheetAxis.Rows
            ? [CellRange.WholeRows(edit.Start, edit.Start + edit.Count - 1)]
            : [CellRange.WholeColumns(edit.Start, edit.Start + edit.Count - 1)];

        internal override SheetStep Apply(Sheet sheet)
        {
            var outcome = sheet.Restructure(edit);
            return new SheetStep(sheet, outcome.Change, s => s.Unrestructure(edit, outcome), s => s.Restructure(edit).Change);
        }
    }
}
