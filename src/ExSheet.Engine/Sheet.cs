using System.Globalization;
using ExSheet.Engine.Formulas;

namespace ExSheet.Engine;

/// <summary>
/// The grid of cells ExSheet holds (CONTEXT.md): Excel's extent, held sparsely, each cell holding
/// an Entry, and a Value computed from it. After every change the Sheet recalculates only what
/// depends on the change, and publishes the new Values only when that recalculation has
/// completed (ADR-0047). Its declared culture decides how typed constants are read and how Values
/// are displayed (ADR-0048).
/// </summary>
/// <remarks>Not thread-safe: one Sheet is changed and read from one thread at a time.</remarks>
public sealed partial class Sheet
{
    /// <summary>Excel's row count: 1,048,576 (ADR-0046).</summary>
    public const int RowCount = 1_048_576;

    /// <summary>Excel's column count: 16,384, <c>A</c> to <c>XFD</c> (ADR-0046).</summary>
    public const int ColumnCount = 16_384;

    private readonly Dictionary<CellAddress, Cell> _cells = [];

    /// <summary>For each cell, the Formula cells that name it by a single-cell Reference.</summary>
    private readonly Dictionary<CellAddress, HashSet<CellAddress>> _cellDependents = [];

    /// <summary>For each Formula cell that reads a multi-cell rectangle, the rectangles.</summary>
    private readonly Dictionary<CellAddress, Area[]> _areaPrecedents = [];

    /// <summary>For each Formula cell that names another Sheet's cells, those References (ADR-0331).</summary>
    private readonly Dictionary<CellAddress, Reference[]> _remoteReferences = [];

    /// <summary>For each cell, the Formula cells on other Sheets that name it by a single-cell Reference (ADR-0331).</summary>
    private readonly Dictionary<CellAddress, HashSet<SheetCell>> _remoteCellDependents = [];

    /// <summary>For each Formula cell on another Sheet that reads a multi-cell rectangle of this Sheet, the rectangles (ADR-0331).</summary>
    private readonly Dictionary<SheetCell, Area[]> _remoteAreaDependents = [];

    /// <summary>Creates an empty Sheet whose constants are read and whose Values are shown under <paramref name="culture"/>.</summary>
    public Sheet(CultureInfo culture)
        : this(culture, DefaultName)
    {
    }

    /// <summary>Creates an empty Sheet named <paramref name="name"/>, whose constants are read and whose Values are shown under <paramref name="culture"/>.</summary>
    /// <exception cref="ArgumentException">The name is not one a Sheet can have (<see cref="IsValidName"/>).</exception>
    public Sheet(CultureInfo culture, string name)
    {
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(name);
        Culture = CultureInfo.ReadOnly(culture);
        Name = CheckName(name, nameof(name));
        Workbook = new Workbook(this);
    }

    /// <summary>Creates an empty Sheet named <paramref name="name"/> in <paramref name="workbook"/>, under its culture (ADR-0331).</summary>
    internal Sheet(Workbook workbook, string name)
    {
        Workbook = workbook;
        Culture = workbook.Culture;
        Name = CheckName(name, nameof(name));
    }

    /// <summary>The Workbook the Sheet belongs to: one of its own, of this Sheet alone, for a Sheet created on its own (ADR-0331).</summary>
    internal Workbook Workbook { get; }

    /// <summary>The Sheet's declared culture (ADR-0048). Formulas are in invariant syntax whatever it is (ADR-0047).</summary>
    public CultureInfo Culture { get; }

    /// <summary>
    /// Opens a Sheet Document: a Sheet in the document's culture, with its name, its Linked Tables
    /// declared and waiting for their first snapshot, and its Entries, with every Value computed
    /// again (ADR-0048, ADR-0049). The culture of the thread that opens it plays no part.
    /// </summary>
    public static Sheet Open(SheetDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var sheet = new Sheet(SheetDocument.ResolveCulture(document.Culture), document.Name);
        // Declared before any Formula is computed: a reader shows #GETTING_DATA, never #NAME? (ADR-0049).
        foreach (var table in document.LinkedTables) sheet.Declare(table.Name, table.Columns, table.KeyColumns);
        foreach (var run in document.Columns)
        {
            for (var column = run.First; column <= run.Last; column++) sheet._columnFormats[column] = sheet.Opened(run.Level);
        }
        foreach (var run in document.ColumnWidths)
        {
            for (var column = run.First; column <= run.Last; column++) sheet._columnWidths[column] = new SheetColumnWidth(run.Width, run.Kind);
        }
        foreach (var run in document.Rows)
        {
            for (var row = run.First; row <= run.Last; row++) sheet._rowFormats[row] = sheet.Opened(run.Level);
        }
        foreach (var cell in document.Cells)
        {
            if (cell.NumberFormat is null && cell.Alignment is null && cell.Font is null && cell.Fill is null && cell.Borders is null) continue;
            sheet._cells[cell.Address] = new Cell(cell.Address)
            {
                NumberFormat = cell.NumberFormat?.OpenedIn(sheet.Culture),
                Alignment = cell.Alignment,
                Font = cell.Font,
                Fill = cell.Fill,
                Borders = cell.Borders,
            };
        }
        sheet.SetEntries(document.Cells.Where(c => c.Entry is not null).Select(c => new KeyValuePair<CellAddress, Entry?>(c.Address, c.Entry)));
        // Opening is no change: the count starts from 0, and the Sheet has no seed yet (ADR-0180).
        sheet.Changes = 0;
        return sheet;
    }

    /// <summary>A column's or row's format as this Sheet opens it: its Number Format read under the Sheet's culture (<see cref="NumberFormat.OpenedIn"/>).</summary>
    private AxisFormat Opened(AxisFormat level) =>
        level.NumberFormat is { } format ? level with { NumberFormat = format.OpenedIn(Culture) } : level;

    /// <summary>
    /// The Sheet as a Sheet Document: its culture, its name, its Linked Tables' declarations, its
    /// Entries, the Cell Formats recorded on its columns, rows and cells, and the widths set on its
    /// columns — never its Values, nor a table's rows (ADR-0046, ADR-0047, ADR-0048, ADR-0049,
    /// ADR-0071). Adjacent columns or rows formatted alike, and adjacent columns of one width, are
    /// recorded as one entry; a column at the default width records none.
    /// </summary>
    public SheetDocument ToDocument() =>
        new(Culture.Name, Name, TableDeclarations, [.. _cells.Values
            .Where(c => c.Entry is not null || c.IsFormatted)
            .OrderBy(c => c.Address)
            .Select(c => new SheetDocumentCell(c.Address, c.Entry, c.NumberFormat, c.Alignment, c.Font, c.Fill, c.Borders))])
        {
            Columns = Runs(_columnFormats),
            Rows = Runs(_rowFormats),
            ColumnWidths = WidthRuns(),
        };

    /// <summary>The addresses of every cell holding an Entry, in row-major order.</summary>
    public IEnumerable<CellAddress> EntryAddresses =>
        _cells.Values.Where(c => c.Entry is not null).Select(c => c.Address).Order();

    /// <summary>
    /// The text <paramref name="number"/> would show in the cell at <paramref name="address"/>, under
    /// the cell's Number Format and the Sheet's culture — how Excel shows its status bar's figures,
    /// in the active cell's format (ADR-0130). Null where the format cannot show it (a date out of
    /// range, a number that is not finite).
    /// </summary>
    public string? FormatAs(CellAddress address, double number)
    {
        if (!double.IsFinite(number))
            return null;
        var (text, cannotShow, _) = GetNumberFormat(address).Format(Value.FromNumber(number), Culture);
        return cannotShow ? null : text;
    }

    /// <summary>The addresses of every cell holding a Value — an Entry's, or an array's spilled into it
    /// (ADR-0125) — in no particular order. The cells a Selection Summary folds in (ADR-0130).</summary>
    public IEnumerable<CellAddress> ValueAddresses =>
        _cells.Values.Where(c => c.Value is not null).Select(c => c.Address);

    /// <summary>The cell's Value, or <see langword="null"/> for a blank cell.</summary>
    public Value? GetValue(CellAddress address) => _cells.TryGetValue(address, out var cell) ? cell.Value : null;

    /// <summary>The cell's Entry, or <see langword="null"/> for a blank cell.</summary>
    public Entry? GetEntry(CellAddress address) => _cells.TryGetValue(address, out var cell) ? cell.Entry : null;

    /// <summary>
    /// The text the Cell Editor opens with: the Formula for a Formula, and the constant written
    /// under the Sheet's culture so that typing it back gives the same Entry. Empty for a blank cell.
    /// </summary>
    public string GetEntryText(CellAddress address)
    {
        if (!_cells.TryGetValue(address, out var cell) || cell.Entry is not { } entry) return "";
        return entry.Formula ?? EntryText.Write(entry.Constant!.Value, GetNumberFormat(address), Culture);
    }

    /// <summary>
    /// What the cell shows at no particular width: its Value formatted by its number format under
    /// the Sheet's culture — a number in General in full, at fifteen significant digits — with
    /// its alignment resolved (ADR-0046/0047). It is the text for a cell's accessible name and for
    /// a copy; what a column shows is <see cref="GetDisplay(CellAddress, double)"/>.
    /// </summary>
    public CellDisplay GetDisplay(CellAddress address)
    {
        if (!_cells.TryGetValue(address, out var cell) || cell.Value is not { } value)
        {
            return new CellDisplay("", Resolve(GetAlignment(address), null), false, false);
        }
        var (text, cannotShow, colour) = GetNumberFormat(address).Format(value, Culture);
        return new CellDisplay(cannotShow ? "" : text, Resolve(GetAlignment(address), value.Kind), value.Kind == ValueKind.Number, cannotShow, colour);
    }

    private static HorizontalAlignment Resolve(HorizontalAlignment alignment, ValueKind? kind) =>
        alignment != HorizontalAlignment.General ? alignment : kind switch
        {
            ValueKind.Number => HorizontalAlignment.Right,
            ValueKind.Boolean or ValueKind.Error => HorizontalAlignment.Center,
            _ => HorizontalAlignment.Left,
        };

    /// <summary>Takes text as the user typed it into a cell, read under the Sheet's culture, and recalculates.</summary>
    /// <exception cref="FormulaSyntaxException">The text is a Formula that cannot be read; nothing changes.</exception>
    public SheetChange Enter(CellAddress address, string typed) =>
        Enter([new KeyValuePair<CellAddress, string>(address, typed)]);

    /// <summary>
    /// Takes several typed texts as one change, with one recalculation (a paste). If any text is a
    /// Formula that cannot be read, nothing changes.
    /// </summary>
    /// <remarks>
    /// Typing can give a cell whose format is General a format, as Excel's does: a date or a
    /// percentage typed as one, and a Formula that reads a formatted cell in simple arithmetic
    /// (<see cref="FormatOnEntry"/>). A cell already formatted keeps its format, and a format
    /// given on entry is never changed by a later recalculation (ADR-0047).
    /// </remarks>
    /// <exception cref="FormulaSyntaxException">A text is a Formula that cannot be read.</exception>
    public SheetChange Enter(IEnumerable<KeyValuePair<CellAddress, string>> typed) => Enter(typed, pasted: false);

    /// <param name="typed">The texts, by cell.</param>
    /// <param name="pasted">
    /// Whether the texts were pasted from another program: then a text beginning with <c>=</c>
    /// that cannot be read as a Formula is taken as text, as Excel takes a pasted <c>=1+</c>
    /// (ADR-0048), where typed it is refused.
    /// </param>
    internal SheetChange Enter(IEnumerable<KeyValuePair<CellAddress, string>> typed, bool pasted)
    {
        ArgumentNullException.ThrowIfNull(typed);
        var entries = new List<KeyValuePair<CellAddress, Entry?>>();
        var implied = new List<(CellAddress Address, NumberFormat Format)>();
        foreach (var (address, text) in typed)
        {
            var (entry, format) = ReadTyped(address, text, pasted);
            entries.Add(new(address, entry));
            if (format is not null) implied.Add((address, format));
        }
        return WriteTyped(entries, implied);
    }

    /// <summary>
    /// Excel's Ctrl+Enter (ADR-0050 item 5, 2026-09-28): <paramref name="typed"/> read as entered in
    /// <paramref name="enteredAt"/>. A Formula is written there as entered and into every other cell
    /// of <paramref name="cells"/> with its relative References shifted by that cell's offset from
    /// <paramref name="enteredAt"/>, by the rule a copy shifts them (<see cref="ReferenceShift"/>);
    /// anything else is entered into every cell as typed. No format is copied.
    /// </summary>
    /// <exception cref="FormulaSyntaxException">The text is a Formula that cannot be read; nothing changes.</exception>
    internal SheetChange EnterInto(IReadOnlyList<CellAddress> cells, CellAddress enteredAt, string typed)
    {
        var (entered, _) = ReadTyped(enteredAt, typed, pasted: false);
        if (entered is not { IsFormula: true })
        {
            return Enter(cells.Select(cell => new KeyValuePair<CellAddress, string>(cell, typed)), pasted: false);
        }
        var entries = new List<KeyValuePair<CellAddress, Entry?>>(cells.Count);
        foreach (var cell in cells)
        {
            entries.Add(new(cell, ReferenceShift.Shift(entered, cell.Row - enteredAt.Row, cell.Column - enteredAt.Column)));
        }
        return WriteTyped(entries, []);
    }

    /// <summary>
    /// One typed text read as <see cref="Enter(IEnumerable{KeyValuePair{CellAddress, string}}, bool)"/>
    /// reads it into <paramref name="address"/>: its Entry, and the format typing it implies for a
    /// General cell, if any.
    /// </summary>
    private (Entry? Entry, NumberFormat? Implied) ReadTyped(CellAddress address, string text, bool pasted)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0 || text[0] == '=')
        {
            Entry? entry;
            try
            {
                entry = Entry.Parse(text, Culture);
            }
            catch (FormulaSyntaxException) when (pasted)
            {
                entry = Entry.FromValue(Value.FromText(text));
            }
            return (entry is null ? null : InOwnName(entry), null);
        }
        // A date or a percentage typed into a General cell gives the cell its format, as in Excel.
        var (value, format) = ConstantParser.ParseWithFormat(text, Culture);
        Entry? signed;
        try
        {
            signed = value.Kind == ValueKind.Text ? Entry.SignedFormula(text) : null;
        }
        catch (FormulaSyntaxException) when (pasted)
        {
            // Pasted text Excel would read as a Formula the engine does not implement is text,
            // as a pasted Formula that cannot be read is (ADR-0048); typed, it is refused.
            signed = null;
        }
        if (signed is not null) return (InOwnName(signed), null);
        // A plain number typed into a cell that shows percentages is read as a percentage,
        // Excel's automatic percent entry (on by default): 0.5 into a 0% cell is 0.005
        // (LVL-015, ADR-0047 second run). Only typed; a paste is taken as it is.
        if (!pasted && value.Kind == ValueKind.Number && GetNumberFormat(address).IsPercent && ConstantParser.IsPlainNumber(text, Culture))
        {
            value = Value.FromNumber(value.Number / 100);
        }
        return (Entry.FromValue(value), format);
    }

    /// <summary>
    /// Typed Entries written as one change: the formats typing implied given to the General cells
    /// among them, then a Formula entered into a General cell given a format from what it reads.
    /// </summary>
    private SheetChange WriteTyped(List<KeyValuePair<CellAddress, Entry?>> entries, List<(CellAddress Address, NumberFormat Format)> implied)
    {
        var rows = new SortedSet<int>();
        foreach (var (address, format) in implied)
        {
            if (!GetNumberFormat(address).IsGeneral) continue;
            var cell = _cells.TryGetValue(address, out var existing) ? existing : _cells[address] = new Cell(address);
            cell.NumberFormat = format;
            rows.Add(address.Row);
        }
        // A Formula entered into a General cell takes a format from what it reads, as in Excel
        // (ADR-0047): after the constants above, so it reads the formats they were just given.
        foreach (var (address, entry) in entries)
        {
            if (entry?.Parsed is not { } parsed || !GetNumberFormat(address).IsGeneral || FormatOnEntry(parsed) is not { } inferred) continue;
            var cell = _cells.TryGetValue(address, out var existing) ? existing : _cells[address] = new Cell(address);
            cell.NumberFormat = inferred;
            rows.Add(address.Row);
        }
        var change = SetEntries(entries);
        if (rows.Count == 0) return change;
        rows.UnionWith(change.Rows);
        return new SheetChange(change.ValueChanges, change.Recalculated, [.. rows]);
    }

    /// <summary>Sets or clears (<see langword="null"/>) one cell's Entry, and recalculates. Internal
    /// (ADR-0201): <see cref="SetEntries"/> with one pair is the same change.</summary>
    internal SheetChange SetEntry(CellAddress address, Entry? entry) =>
        SetEntries([new KeyValuePair<CellAddress, Entry?>(address, entry)]);

    /// <summary>Sets or clears several cells' Entries as one change, with one recalculation. A later pair for the same cell wins.</summary>
    public SheetChange SetEntries(IEnumerable<KeyValuePair<CellAddress, Entry?>> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var changed = new HashSet<CellAddress>();
        var rows = new SortedSet<int>();
        foreach (var (address, entry) in entries)
        {
            var cell = _cells.GetValueOrDefault(address);
            if (Equals(cell?.Entry, entry)) continue;
            if (cell?.Entry?.Parsed is not null) Unregister(address, cell.Entry.Parsed);
            if (entry is null)
            {
                cell!.Entry = null;
            }
            else
            {
                cell ??= _cells[address] = new Cell(address);
                cell.Entry = entry;
                if (entry.Parsed is not null) Register(address, entry.Parsed);
            }
            changed.Add(address);
            rows.Add(address.Row);
        }
        if (changed.Count == 0) return SheetChange.None;
        CountChange();
        return Recalculate(changed, rows);
    }

    private void Register(CellAddress formulaCell, Node formula)
    {
        var areas = new List<Area>();
        var remote = new List<Reference>();
        foreach (var reference in formula.References)
        {
            if (!IsLocal(reference))
            {
                remote.Add(reference);
                continue;
            }
            var area = reference.Area;
            if (area.IsSingleCell)
            {
                var target = new CellAddress(area.Row1, area.Column1);
                if (!_cellDependents.TryGetValue(target, out var set)) _cellDependents[target] = set = [];
                set.Add(formulaCell);
            }
            else
            {
                areas.Add(area);
            }
        }
        if (areas.Count > 0) _areaPrecedents[formulaCell] = [.. areas];
        if (remote.Count > 0)
        {
            _remoteReferences[formulaCell] = [.. remote];
            RegisterRemote(formulaCell, remote);
        }
        RegisterTableReaders(formulaCell, formula);
        RegisterTodayReader(formulaCell, formula);
        RegisterVolatile(formulaCell, formula);
        RegisterRandomReader(formulaCell, formula);
    }

    private void Unregister(CellAddress formulaCell, Node formula)
    {
        foreach (var reference in formula.References)
        {
            var area = reference.Area;
            if (IsLocal(reference) && area.IsSingleCell)
            {
                var target = new CellAddress(area.Row1, area.Column1);
                if (_cellDependents.TryGetValue(target, out var set) && set.Remove(formulaCell) && set.Count == 0) _cellDependents.Remove(target);
            }
        }
        _areaPrecedents.Remove(formulaCell);
        UnregisterRemote(formulaCell);
        UnregisterTableReaders(formulaCell, formula);
        _todayReaders.Remove(formulaCell);
        _volatileFormulas.Remove(formulaCell);
        _randomReaders.Remove(formulaCell);
    }

    /// <summary>
    /// Records a Formula cell's References to other Sheets on the Sheets they name, as this Sheet records
    /// its own; a name no Sheet has is <c>#REF!</c>, and depends on nothing.
    /// </summary>
    private void RegisterRemote(CellAddress formulaCell, List<Reference> remote)
    {
        var dependent = new SheetCell(this, formulaCell);
        foreach (var group in remote.GroupBy(r => Workbook.Find(r.SheetName!)))
        {
            if (group.Key is not { } target) continue;
            var areas = new List<Area>();
            foreach (var area in group.Select(r => r.Area))
            {
                if (!area.IsSingleCell)
                {
                    areas.Add(area);
                    continue;
                }
                var address = new CellAddress(area.Row1, area.Column1);
                if (!target._remoteCellDependents.TryGetValue(address, out var set)) target._remoteCellDependents[address] = set = [];
                set.Add(dependent);
            }
            if (areas.Count > 0) target._remoteAreaDependents[dependent] = [.. areas];
        }
    }

    /// <summary>Forgets what <see cref="RegisterRemote"/> recorded for a Formula cell.</summary>
    private void UnregisterRemote(CellAddress formulaCell)
    {
        if (!_remoteReferences.Remove(formulaCell, out var remote)) return;
        var dependent = new SheetCell(this, formulaCell);
        foreach (var reference in remote)
        {
            if (Workbook.Find(reference.SheetName!) is not { } target) continue;
            target._remoteAreaDependents.Remove(dependent);
            var area = reference.Area;
            if (!area.IsSingleCell) continue;
            var address = new CellAddress(area.Row1, area.Column1);
            if (target._remoteCellDependents.TryGetValue(address, out var set) && set.Remove(dependent) && set.Count == 0) target._remoteCellDependents.Remove(address);
        }
    }

    /// <summary>The Formula cells that read <paramref name="address"/> directly.</summary>
    private IEnumerable<CellAddress> Dependents(CellAddress address)
    {
        if (_cellDependents.TryGetValue(address, out var set))
        {
            foreach (var dependent in set) yield return dependent;
        }
        foreach (var (formulaCell, areas) in _areaPrecedents)
        {
            foreach (var area in areas)
            {
                if (area.Contains(address))
                {
                    yield return formulaCell;
                    break;
                }
            }
        }
    }

    /// <summary>The addresses inside <paramref name="area"/> that hold something, in no particular order.</summary>
    internal IEnumerable<CellAddress> CellsIn(Area area)
    {
        if (area.CellCount <= _cells.Count)
        {
            for (var row = area.Row1; row <= area.Row2; row++)
            {
                for (var column = area.Column1; column <= area.Column2; column++)
                {
                    var address = new CellAddress(row, column);
                    if (_cells.ContainsKey(address)) yield return address;
                }
            }
        }
        else
        {
            foreach (var address in _cells.Keys)
            {
                if (area.Contains(address)) yield return address;
            }
        }
    }

    private sealed class Cell(CellAddress address)
    {
        public CellAddress Address { get; } = address;

        public Entry? Entry { get; set; }

        public Value? Value { get; set; }

        /// <summary>The cell's own Number Format; <see langword="null"/> when it takes its row's or column's (ADR-0047).</summary>
        public NumberFormat? NumberFormat { get; set; }

        /// <summary>The cell's own alignment; <see langword="null"/> when it takes its row's or column's (ADR-0047).</summary>
        public HorizontalAlignment? Alignment { get; set; }

        /// <summary>The cell's own Font; <see langword="null"/> when it takes its row's or column's (ADR-0071).</summary>
        public CellFont? Font { get; set; }

        /// <summary>The cell's own Fill; <see langword="null"/> when it takes its row's or column's (ADR-0071).</summary>
        public CellFill? Fill { get; set; }

        /// <summary>The cell's own four sides; <see langword="null"/> when it takes its row's or column's (ADR-0071).</summary>
        public CellBorders? Borders { get; set; }

        /// <summary>Whether the cell records any part of a Cell Format of its own.</summary>
        public bool IsFormatted => NumberFormat is not null || Alignment is not null || Font is not null || Fill is not null || Borders is not null;

        public bool IsEmpty => Entry is null && !IsFormatted && SpilledFrom is null;

        /// <summary>For an Anchor, the array its Formula computed (ADR-0125); <see langword="null"/> for a result of one Value.</summary>
        public ValueArray? Array { get; set; }

        /// <summary>For an Anchor: whether the last layout found its Spill Range in the way, so that it shows <c>#SPILL!</c> (ADR-0125).</summary>
        public bool SpillBlocked { get; set; }

        /// <summary>For a cell of a Spill Range, the Anchor whose array gives its Value; it holds no Entry of its own.</summary>
        public CellAddress? SpilledFrom { get; set; }

        /// <summary>What the cell shows over <paramref name="inherited"/>: each part its own, else the level's.</summary>
        public CellFormat Over(CellFormat inherited) => new(
            NumberFormat ?? inherited.NumberFormat,
            Alignment ?? inherited.Alignment,
            Font ?? inherited.Font,
            Fill ?? inherited.Fill,
            Borders ?? inherited.Borders);

        /// <summary>Records the Cell Format <paramref name="other"/> records of its own, and nothing else of it.</summary>
        public void TakeFormatOf(Cell other)
        {
            NumberFormat = other.NumberFormat;
            Alignment = other.Alignment;
            Font = other.Font;
            Fill = other.Fill;
            Borders = other.Borders;
        }

        /// <summary>
        /// Records each part <paramref name="change"/> sets as <paramref name="shown"/> has it, and
        /// nothing where <paramref name="inherited"/> gives the same; whether anything changed.
        /// </summary>
        public bool Take(CellFormatChange change, CellFormat shown, CellFormat inherited)
        {
            var was = (NumberFormat, Alignment, Font, Fill, Borders);
            if (change.NumberFormat is not null) NumberFormat = shown.NumberFormat.Equals(inherited.NumberFormat) ? null : shown.NumberFormat;
            if (change.Alignment is not null) Alignment = Own(shown.Alignment, inherited.Alignment);
            if (change.SetsFont) Font = Own(shown.Font, inherited.Font);
            if (change.Fill is not null) Fill = Own(shown.Fill, inherited.Fill);
            if (change.SetsBorders) Borders = Own(shown.Borders, inherited.Borders);
            return !was.Equals((NumberFormat, Alignment, Font, Fill, Borders));
        }
    }
}
