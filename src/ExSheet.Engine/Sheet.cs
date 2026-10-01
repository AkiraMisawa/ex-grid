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
    }

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
        foreach (var table in document.LinkedTables) sheet.Declare(table.Name, table.Columns);
        foreach (var run in document.Columns)
        {
            for (var column = run.First; column <= run.Last; column++) sheet._columnFormats[column] = run.Level;
        }
        foreach (var run in document.ColumnWidths)
        {
            for (var column = run.First; column <= run.Last; column++) sheet._columnWidths[column] = new SheetColumnWidth(run.Width, run.Kind);
        }
        foreach (var run in document.Rows)
        {
            for (var row = run.First; row <= run.Last; row++) sheet._rowFormats[row] = run.Level;
        }
        foreach (var cell in document.Cells)
        {
            if (cell.NumberFormat is null && cell.Alignment is null && cell.Font is null && cell.Fill is null && cell.Borders is null) continue;
            sheet._cells[cell.Address] = new Cell(cell.Address)
            {
                NumberFormat = cell.NumberFormat,
                Alignment = cell.Alignment,
                Font = cell.Font,
                Fill = cell.Fill,
                Borders = cell.Borders,
            };
        }
        sheet.SetEntries(document.Cells.Where(c => c.Entry is not null).Select(c => new KeyValuePair<CellAddress, Entry?>(c.Address, c.Entry)));
        return sheet;
    }

    /// <summary>
    /// The Sheet as a Sheet Document: its culture, its name, its Linked Tables' declarations, its
    /// Entries, the Cell Formats recorded on its columns, rows and cells, and the widths set on its
    /// columns — never its Values, nor a table's rows (ADR-0046, ADR-0047, ADR-0048, ADR-0049,
    /// ADR-0063). Adjacent columns or rows formatted alike, and adjacent columns of one width, are
    /// recorded as one entry; a column at the default width records none.
    /// </summary>
    public SheetDocument ToDocument() =>
        new(Culture.Name, Name, TableDeclarations, [.. _cells.Values
            .Where(c => !c.IsEmpty)
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
        var (text, cannotShow) = GetNumberFormat(address).Format(value, Culture);
        return new CellDisplay(cannotShow ? "" : text, Resolve(GetAlignment(address), value.Kind), value.Kind == ValueKind.Number, cannotShow);
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

    /// <summary>Sets or clears (<see langword="null"/>) one cell's Entry, and recalculates.</summary>
    public SheetChange SetEntry(CellAddress address, Entry? entry) =>
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
        return Recalculate(changed, rows);
    }

    private void Register(CellAddress formulaCell, Node formula)
    {
        var areas = new List<Area>();
        foreach (var reference in formula.References)
        {
            if (!IsLocal(reference)) continue;
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
        RegisterTableReaders(formulaCell, formula);
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
        UnregisterTableReaders(formulaCell, formula);
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

    /// <summary>
    /// Recomputes the changed cells and everything that depends on them, in dependency order,
    /// into a staging set; a cycle, and everything downstream of one, is <c>#CIRC!</c>
    /// (ADR-0047). Only when every Value is computed are they published.
    /// </summary>
    private SheetChange Recalculate(HashSet<CellAddress> changed, SortedSet<int> rows)
    {
        // 1. Everything the change can reach.
        var dirty = new HashSet<CellAddress>(changed);
        var queue = new Queue<CellAddress>(changed);
        while (queue.Count > 0)
        {
            foreach (var dependent in Dependents(queue.Dequeue()))
            {
                if (dirty.Add(dependent)) queue.Enqueue(dependent);
            }
        }

        var staged = new Dictionary<CellAddress, Value?>();
        var formulas = new HashSet<CellAddress>();
        foreach (var address in dirty)
        {
            var entry = _cells.GetValueOrDefault(address)?.Entry;
            if (entry?.Parsed is not null) formulas.Add(address);
            else staged[address] = entry?.Constant;
        }

        // 2. Dependency order among the Formulas to recompute (Kahn). What never becomes ready
        //    is in a cycle or downstream of one.
        var edges = new Dictionary<CellAddress, List<CellAddress>>();
        var waiting = formulas.ToDictionary(f => f, _ => 0);
        foreach (var precedent in formulas)
        {
            var targets = new List<CellAddress>();
            foreach (var dependent in Dependents(precedent).Distinct())
            {
                if (!formulas.Contains(dependent)) continue;
                targets.Add(dependent);
                waiting[dependent]++;
            }
            edges[precedent] = targets;
        }

        var reader = new StagedReader(this, staged);
        var evaluator = new Evaluator(reader, Culture);
        var ready = new Queue<CellAddress>(waiting.Where(w => w.Value == 0).Select(w => w.Key));
        var recalculated = new List<CellAddress>();
        while (ready.Count > 0)
        {
            var address = ready.Dequeue();
            var node = _cells[address].Entry!.Parsed!;
            staged[address] = Taint(node, reader) ?? evaluator.Evaluate(node);
            recalculated.Add(address);
            foreach (var dependent in edges[address])
            {
                if (--waiting[dependent] == 0) ready.Enqueue(dependent);
            }
        }
        foreach (var (address, count) in waiting)
        {
            if (count > 0)
            {
                staged[address] = Value.FromError(ErrorValue.Circ);
                recalculated.Add(address);
            }
        }

        // 3. Publish, all at once.
        var valueChanges = new List<CellAddress>();
        foreach (var (address, value) in staged)
        {
            var cell = _cells.GetValueOrDefault(address);
            var old = cell?.Value;
            if (cell is not null)
            {
                cell.Value = value;
                if (cell.IsEmpty) _cells.Remove(address);
            }
            if (!Nullable.Equals(old, value))
            {
                valueChanges.Add(address);
                rows.Add(address.Row);
            }
        }
        valueChanges.Sort();
        recalculated.Sort();
        return new SheetChange(valueChanges, recalculated, [.. rows]);
    }

    /// <summary>
    /// A Formula that reads a cell in a cycle is <c>#CIRC!</c> whatever it computes; one that reads
    /// a waiting cell waits (ADR-0047, ADR-0049). Neither can be caught by <c>IFERROR</c>.
    /// </summary>
    private Value? Taint(Node node, StagedReader reader)
    {
        var gettingData = ReadsWaitingTable(node);
        foreach (var reference in node.References)
        {
            if (!IsLocal(reference)) continue;
            foreach (var address in CellsIn(reference.Area))
            {
                if (reader.Read(address) is { IsError: true } value)
                {
                    if (value.Error == ErrorValue.Circ) return value;
                    if (value.Error == ErrorValue.GettingData) gettingData = true;
                }
            }
        }
        return gettingData ? Value.FromError(ErrorValue.GettingData) : null;
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

    private sealed class StagedReader(Sheet sheet, Dictionary<CellAddress, Value?> staged) : ICellReader
    {
        public Value? Read(CellAddress address) =>
            staged.TryGetValue(address, out var value) ? value : sheet.GetValue(address);

        public IEnumerable<CellAddress> NonBlankIn(Area area) =>
            sheet.CellsIn(area).Where(a => Read(a) is not null).Order();

        public Operand TableColumn(string table, string column) => sheet.TableColumn(table, column);

        public bool IsLocal(Reference reference) => sheet.IsLocal(reference);
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

        /// <summary>The cell's own Font; <see langword="null"/> when it takes its row's or column's (ADR-0063).</summary>
        public CellFont? Font { get; set; }

        /// <summary>The cell's own Fill; <see langword="null"/> when it takes its row's or column's (ADR-0063).</summary>
        public CellFill? Fill { get; set; }

        /// <summary>The cell's own four sides; <see langword="null"/> when it takes its row's or column's (ADR-0063).</summary>
        public CellBorders? Borders { get; set; }

        /// <summary>Whether the cell records any part of a Cell Format of its own.</summary>
        public bool IsFormatted => NumberFormat is not null || Alignment is not null || Font is not null || Fill is not null || Borders is not null;

        public bool IsEmpty => Entry is null && !IsFormatted;

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
