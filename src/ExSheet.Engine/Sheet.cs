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
public sealed class Sheet
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
    {
        ArgumentNullException.ThrowIfNull(culture);
        Culture = CultureInfo.ReadOnly(culture);
    }

    /// <summary>The Sheet's declared culture (ADR-0048). Formulas are in invariant syntax whatever it is (ADR-0047).</summary>
    public CultureInfo Culture { get; }

    /// <summary>
    /// Opens a Sheet Document: a Sheet in the document's culture holding its Entries, with every
    /// Value computed again (ADR-0048). The culture of the thread that opens it plays no part.
    /// </summary>
    public static Sheet Open(SheetDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var sheet = new Sheet(SheetDocument.ResolveCulture(document.Culture));
        sheet.SetEntries(document.Cells.Select(c => new KeyValuePair<CellAddress, Entry?>(c.Address, c.Entry)));
        return sheet;
    }

    /// <summary>The Sheet as a Sheet Document: its culture and its Entries, never its Values (ADR-0048).</summary>
    public SheetDocument ToDocument() =>
        new(Culture.Name, [.. _cells.Values
            .Where(c => c.Entry is not null)
            .OrderBy(c => c.Address)
            .Select(c => new SheetDocumentCell(c.Address, c.Entry!))]);

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
        return entry.Formula ?? EntryText.Write(entry.Constant!.Value, Culture);
    }

    /// <summary>Takes text as the user typed it into a cell, read under the Sheet's culture, and recalculates.</summary>
    /// <exception cref="FormulaSyntaxException">The text is a Formula that cannot be read; nothing changes.</exception>
    public SheetChange Enter(CellAddress address, string typed) =>
        Enter([new KeyValuePair<CellAddress, string>(address, typed)]);

    /// <summary>
    /// Takes several typed texts as one change, with one recalculation (a paste). If any text is a
    /// Formula that cannot be read, nothing changes.
    /// </summary>
    /// <exception cref="FormulaSyntaxException">A text is a Formula that cannot be read.</exception>
    public SheetChange Enter(IEnumerable<KeyValuePair<CellAddress, string>> typed)
    {
        ArgumentNullException.ThrowIfNull(typed);
        var entries = typed.Select(t => new KeyValuePair<CellAddress, Entry?>(t.Key, Entry.Parse(t.Value, Culture))).ToList();
        return SetEntries(entries);
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
            if (reference.SheetName is not null) continue;
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
    }

    private void Unregister(CellAddress formulaCell, Node formula)
    {
        foreach (var reference in formula.References)
        {
            var area = reference.Area;
            if (reference.SheetName is null && area.IsSingleCell)
            {
                var target = new CellAddress(area.Row1, area.Column1);
                if (_cellDependents.TryGetValue(target, out var set) && set.Remove(formulaCell) && set.Count == 0) _cellDependents.Remove(target);
            }
        }
        _areaPrecedents.Remove(formulaCell);
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
        var gettingData = false;
        foreach (var reference in node.References)
        {
            if (reference.SheetName is not null) continue;
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
    }

    private sealed class Cell(CellAddress address)
    {
        public CellAddress Address { get; } = address;

        public Entry? Entry { get; set; }

        public Value? Value { get; set; }

        public bool IsEmpty => Entry is null;
    }
}
