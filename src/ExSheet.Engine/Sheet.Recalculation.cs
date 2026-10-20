using ExSheet.Engine.Formulas;

namespace ExSheet.Engine;

/// <summary>
/// Recalculation (ADR-0047, ADR-0124, ADR-0125), across every Sheet of the Workbook (ADR-0331): a
/// change on one Sheet recomputes what depends on it on any Sheet, in one dependency order, and the
/// new Values are published only when the whole recalculation has completed.
/// </summary>
public sealed partial class Sheet
{
    /// <summary>The Formula cells, on any Sheet of the Workbook, that read <paramref name="address"/> of this Sheet directly.</summary>
    private IEnumerable<SheetCell> DependentsInWorkbook(CellAddress address)
    {
        foreach (var dependent in Dependents(address)) yield return new SheetCell(this, dependent);
        foreach (var other in Workbook.Sheets)
        {
            if (other == this) continue;
            foreach (var (formulaCell, references) in other._remoteReferences)
            {
                foreach (var reference in references)
                {
                    if (Names(reference.SheetName, Name) && reference.Area.Contains(address))
                    {
                        yield return new SheetCell(other, formulaCell);
                        break;
                    }
                }
            }
        }
    }

    /// <summary>Whether a Reference's qualifier names a Sheet of the Workbook: none, this Sheet's name, or another's.</summary>
    private bool Resolves(string? qualifier) => IsThisSheet(qualifier) || Workbook.Find(qualifier!) is not null;

    /// <summary>
    /// One recalculation of <paramref name="changed"/> on this Sheet, across the Workbook
    /// (<see cref="RecalculateWorkbook"/>). What it changed on another Sheet is reported to the
    /// Workbook, for the Workbook operation under way.
    /// </summary>
    private SheetChange Recalculate(HashSet<CellAddress> changed, SortedSet<int> rows) =>
        Recalculate([.. changed.Select(a => new SheetCell(this, a))], rows);

    /// <summary>One recalculation of cells on any Sheet of the Workbook, started from this Sheet: what it changed here.</summary>
    private SheetChange Recalculate(HashSet<SheetCell> changed, SortedSet<int> rows)
    {
        var changes = RecalculateWorkbook(Workbook, changed, this, rows);
        SheetChange? own = null;
        foreach (var (sheet, change) in changes)
        {
            if (sheet == this) own = change;
            else Workbook.Collect(sheet, change);
        }
        return own ?? SheetChange.None;
    }

    /// <summary>
    /// One recalculation: what <paramref name="changed"/> reaches and every volatile Formula of the
    /// Workbook, the moment read once (ADR-0124); then the spills laid out, Sheet by Sheet, and what
    /// their layout changed recalculated with every volatile Formula, until a layout changes nothing.
    /// It always ends on a layout, so an Anchor shows what the last layout decided: <c>#SPILL!</c>
    /// where its Spill Range is in the way.
    /// </summary>
    /// <remarks>
    /// A volatile Formula is computed again after every layout that changed something, because a
    /// Reference <c>INDIRECT</c> or <c>OFFSET</c> computes may read what the layout changed, and no
    /// Dependent says so. Each draws and reads the clock as in the first pass (ADR-0180), and an
    /// Anchor computed again with an array of the shape the layout blocked stays <c>#SPILL!</c>
    /// (<c>RecalculatePass</c>), so computing it again changes nothing and the next layout settles.
    /// A spill blocked or freed by a change is therefore two passes: every volatile Formula is
    /// computed twice. Spills that move each other take a pass for each layout that moves one.
    /// </remarks>
    /// <returns>What it changed on each Sheet it reached; <paramref name="initiator"/>'s always, with <paramref name="initiatorRows"/>.</returns>
    private static Dictionary<Sheet, SheetChange> RecalculateWorkbook(Workbook workbook, HashSet<SheetCell> changed, Sheet initiator, SortedSet<int> initiatorRows)
    {
        foreach (var sheet in workbook.Sheets) sheet._moment = sheet.NowSource?.Invoke();
        var rows = workbook.Sheets.ToDictionary(s => s, s => s == initiator ? initiatorRows : []);
        var changes = workbook.Sheets.ToDictionary(s => s, _ => new List<SheetChange>());
        Add(RecalculatePass(workbook, changed, rows, withVolatile: true));
        var passes = 1;
        while (true)
        {
            var laidOut = new List<SheetCell>();
            foreach (var sheet in workbook.Sheets)
            {
                var cells = sheet.LayOutSpills();
                if (cells.Count == 0) continue;
                foreach (var address in cells) rows[sheet].Add(address.Row);
                changes[sheet].Add(new SheetChange(cells, [], [.. cells.Select(a => a.Row).Distinct().Order()]));
                laidOut.AddRange(cells.Select(a => new SheetCell(sheet, a)));
            }
            if (laidOut.Count == 0) break;
            var readers = new HashSet<SheetCell>(laidOut.SelectMany(c => c.Sheet.DependentsInWorkbook(c.Address)));
            if ((readers.Count == 0 && !workbook.Sheets.Any(s => s.HasVolatileFormulas)) || passes == MostPasses) break;
            Add(RecalculatePass(workbook, readers, rows, withVolatile: true));
            passes++;
        }
        initiator.RecalculationPasses = passes;
        return changes
            .Where(c => c.Key == initiator || c.Value.Count > 0)
            .ToDictionary(c => c.Key, c => SheetChange.Merge(c.Value));

        void Add(Dictionary<Sheet, SheetChange> pass)
        {
            foreach (var (sheet, change) in pass) changes[sheet].Add(change);
        }
    }

    /// <summary>
    /// Recomputes the changed cells and everything that depends on them, on any Sheet, in dependency
    /// order, into a staging set; a cycle, and everything downstream of one, is <c>#CIRC!</c>
    /// (ADR-0047). Only when every Value is computed are they published.
    /// </summary>
    /// <returns>What the pass changed on each Sheet it reached, and on every Sheet holding rows in <paramref name="rows"/>.</returns>
    private static Dictionary<Sheet, SheetChange> RecalculatePass(Workbook workbook, HashSet<SheetCell> changed, Dictionary<Sheet, SortedSet<int>> rows, bool withVolatile)
    {
        // 1. Everything the change can reach, and every volatile Formula, whatever the change (ADR-0124).
        var dirty = new HashSet<SheetCell>(changed);
        if (withVolatile)
        {
            foreach (var sheet in workbook.Sheets) dirty.UnionWith(sheet._volatileFormulas.Select(a => new SheetCell(sheet, a)));
        }
        var queue = new Queue<SheetCell>(dirty);
        while (queue.Count > 0)
        {
            var next = queue.Dequeue();
            foreach (var dependent in next.Sheet.DependentsInWorkbook(next.Address))
            {
                if (dirty.Add(dependent)) queue.Enqueue(dependent);
            }
        }

        var staged = workbook.Sheets.ToDictionary(s => s, _ => new Dictionary<CellAddress, Value?>());
        var arrays = workbook.Sheets.ToDictionary(s => s, _ => new Dictionary<CellAddress, ValueArray?>());
        var recalculated = workbook.Sheets.ToDictionary(s => s, _ => new List<CellAddress>());
        var formulas = new HashSet<SheetCell>();
        foreach (var target in dirty)
        {
            var cell = target.Sheet._cells.GetValueOrDefault(target.Address);
            var entry = cell?.Entry;
            if (entry?.Parsed is not null) formulas.Add(target);
            // A spilled cell with no Entry keeps its Value: the spill's layout gives it, after the
            // pass. One typed into takes its Entry's, and the layout then finds it in the way (ADR-0125).
            else if (entry is not null || cell?.SpilledFrom is null) staged[target.Sheet][target.Address] = entry?.Constant;
        }

        // 2. Dependency order among the Formulas to recompute (Kahn). What never becomes ready
        //    is in a cycle or downstream of one.
        var edges = new Dictionary<SheetCell, List<SheetCell>>();
        var waiting = formulas.ToDictionary(f => f, _ => 0);
        foreach (var precedent in formulas)
        {
            var targets = new List<SheetCell>();
            foreach (var dependent in precedent.Sheet.DependentsInWorkbook(precedent.Address).Distinct())
            {
                if (!formulas.Contains(dependent)) continue;
                targets.Add(dependent);
                waiting[dependent]++;
            }
            edges[precedent] = targets;
        }

        // A Reference a volatile function computes is read as it stands after this recalculation:
        // a cell this recalculation has still to compute is computed first, on demand, and a
        // Formula that reaches itself that way is #CIRC! (ADR-0124). What such a Reference reads is
        // tainted as a named one is (Taint): a Formula that met a cycle is #CIRC!, one that met a
        // waiting cell waits, and one that met a refused cell is refused, whatever it computes — a
        // function that passes over or counts an Error Value included (ADR-0047, ADR-0049; 2026-10-12,
        // decided with the user: a refusal is never caught, anywhere). Each Sheet is read through a
        // reader of its own, and what any of them meets is met by the Formula being computed.
        var inProgress = new HashSet<SheetCell>();
        var reads = new ReadState();
        var readers = new Dictionary<Sheet, (StagedReader Reader, Evaluator Evaluator)>();
        foreach (var sheet in workbook.Sheets)
        {
            var reader = new StagedReader(
                sheet, staged[sheet], address => formulas.Contains(new SheetCell(sheet, address)) ? Compute(new SheetCell(sheet, address)) : null,
                arrays[sheet], reads, name => workbook.Find(name) is { } other ? readers[other].Reader : null);
            readers[sheet] = (reader, new Evaluator(reader, sheet.Culture));
        }
        var ready = new Queue<SheetCell>(waiting.Where(w => w.Value == 0).Select(w => w.Key));
        while (ready.Count > 0)
        {
            var target = ready.Dequeue();
            if (!staged[target.Sheet].ContainsKey(target.Address)) Compute(target);
            foreach (var dependent in edges[target])
            {
                if (--waiting[dependent] == 0) ready.Enqueue(dependent);
            }
        }
        foreach (var (target, count) in waiting)
        {
            if (count > 0)
            {
                if (!staged[target.Sheet].ContainsKey(target.Address)) recalculated[target.Sheet].Add(target.Address);
                staged[target.Sheet][target.Address] = Value.FromError(ErrorValue.Circ);
            }
        }

        // 3. Publish, all at once.
        var result = new Dictionary<Sheet, SheetChange>();
        foreach (var sheet in workbook.Sheets)
        {
            var change = sheet.Publish(staged[sheet], arrays[sheet], recalculated[sheet], rows[sheet]);
            if (change is not null) result[sheet] = change;
        }
        return result;

        // One Formula, once: in dependency order from the loop above, or first, on demand, when a
        // computed Reference reads it before its turn.
        Value? Compute(SheetCell target)
        {
            var (sheet, address) = target;
            var sheetStaged = staged[sheet];
            if (sheetStaged.TryGetValue(address, out var done)) return done;
            if (!inProgress.Add(target)) return Value.FromError(ErrorValue.Circ);
            var (reader, evaluator) = readers[sheet];
            var anchor = sheet._cells[address];
            var node = anchor.Entry!.Parsed!;
            var outer = evaluator.Self;
            evaluator.Self = address;
            // The outer Formula's reads so far are its own; this one's start from none.
            var outerMet = reads.Met;
            var outerMetRefusal = reads.MetRefusal;
            reads.Met = null;
            reads.MetRefusal = false;
            Value value;
            if (sheet.Taint(node, reader) is { } tainted)
            {
                value = tainted;
                arrays[sheet][address] = null;
            }
            else
            {
                // A result of more than one Value spills (ADR-0125): the anchor holds the array, and
                // shows its first Value until the layout after the pass says whether it spills. An
                // array of the shape the last layout blocked shows #SPILL! still: only an obstacle
                // moving changes that, and the layout after the pass is what sees one move. Computed
                // again unchanged — a volatile Formula read again after a layout — the Anchor then
                // changes nothing, and the recalculation settles (RecalculateWorkbook).
                var computed = evaluator.EvaluateResult(node);
                var array = Evaluator.IsMulti(computed) ? evaluator.ToArray(computed) : null;
                var scalar = array is null ? evaluator.ScalarOf(computed) : null;
                if (reads.Met is { } met)
                {
                    // A computed Reference met a cycle or a waiting cell: tainted as Taint taints.
                    value = Value.FromError(met);
                    arrays[sheet][address] = null;
                }
                else if (reads.MetRefusal)
                {
                    // A computed Reference read a refused cell, or a Linked Table's column holding a
                    // refusal: refused, as Taint refuses a Formula that names one.
                    value = Value.Refusal;
                    arrays[sheet][address] = null;
                }
                else
                {
                    arrays[sheet][address] = array;
                    value = array is null ? scalar ?? Value.FromNumber(0)
                        : StillBlocked(anchor, array) ? Value.FromError(ErrorValue.Spill)
                        : array[0, 0] ?? Value.FromNumber(0);
                }
            }
            // A refused Anchor whose array's shape its text settles spills refusals over that shape, so that
            // each cell of it reads as refused (2026-10-13, decided with the user); where the shape could
            // depend on a Value, nothing spills (KnownShape).
            if (value.IsRefused && KnownShape.Of(node, r => sheet.Resolves(r.SheetName)) is { } shape && (shape.Rows > 1 || shape.Columns > 1))
            {
                var refusals = new ValueArray(shape.Rows, shape.Columns);
                for (var r = 0; r < shape.Rows; r++)
                {
                    for (var c = 0; c < shape.Columns; c++) refusals[r, c] = Value.Refusal;
                }
                arrays[sheet][address] = refusals;
                if (StillBlocked(anchor, refusals)) value = Value.FromError(ErrorValue.Spill);
            }
            reads.Met = outerMet;
            reads.MetRefusal = outerMetRefusal;
            evaluator.Self = outer;
            inProgress.Remove(target);
            sheetStaged[address] = value;
            recalculated[sheet].Add(address);
            return value;
        }

        static bool StillBlocked(Cell anchor, ValueArray array) =>
            anchor.SpillBlocked && anchor.Array is { } laidOut && laidOut.Rows == array.Rows && laidOut.Columns == array.Columns;
    }

    /// <summary>
    /// Publishes what a pass computed on this Sheet: the Anchors' arrays, then every staged Value.
    /// <see langword="null"/> when the pass neither reached the Sheet nor holds rows for it.
    /// </summary>
    private SheetChange? Publish(
        Dictionary<CellAddress, Value?> staged, Dictionary<CellAddress, ValueArray?> arrays, List<CellAddress> recalculated, SortedSet<int> rows)
    {
        if (staged.Count == 0 && arrays.Count == 0 && recalculated.Count == 0 && rows.Count == 0) return null;
        foreach (var (address, array) in arrays)
        {
            if (_cells.GetValueOrDefault(address) is { } anchor)
            {
                anchor.Array = array;
                if (array is null) anchor.SpillBlocked = false;
            }
        }
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
            if (!Value.Unchanged(old, value))
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
    /// a waiting cell, or calls <c>TODAY</c> before the Sheet Day is known, or draws before the Sheet
    /// has a seed, waits (ADR-0047, ADR-0049, ADR-0121, ADR-0180); and one that reads a refused cell,
    /// or a Linked Table's column that holds a refusal, is refused (<see cref="Value.Refusal"/>; 2026-10-12,
    /// decided with the user: a refusal is never caught, anywhere). None can be caught by <c>IFERROR</c>.
    /// As for a cycle, a Formula reads what its text names, whether or not the branch naming it is
    /// taken: <c>=IF(TRUE,1,B1)</c> over a refused B1 is refused. A cycle comes before a wait, and a wait
    /// before a refusal: the data on its way may change what the Formula is. A cell another Sheet
    /// holds taints as one of this Sheet's does (ADR-0331).
    /// </summary>
    private Value? Taint(Node node, StagedReader reader)
    {
        var gettingData = ReadsWaitingTable(node) || WaitsForToday(node) || WaitsForNow(node) || WaitsForSeed(node);
        var refused = ReadsRefusingTable(node);
        foreach (var reference in node.References)
        {
            if (reader.Resolve(reference.SheetName) is not StagedReader target) continue;
            foreach (var address in target.Sheet.CellsIn(reference.Area))
            {
                if (target.Read(address) is { IsError: true } value)
                {
                    if (value.Error == ErrorValue.Circ) return value;
                    if (value.Error == ErrorValue.GettingData) gettingData = true;
                    if (value.IsRefused) refused = true;
                }
            }
        }
        return gettingData ? Value.FromError(ErrorValue.GettingData) : refused ? Value.Refusal : null;
    }

    /// <summary>
    /// What the reads made for the Formula being computed have met, whichever Sheet they read: the
    /// recalculation sets it aside for each Formula it computes on demand.
    /// </summary>
    private sealed class ReadState
    {
        /// <summary>
        /// The worst a read has met while the Formula being computed was evaluated: <c>#CIRC!</c> for a
        /// cell in a cycle, or one still being computed; else <c>#GETTING_DATA</c> for a waiting cell;
        /// else <see langword="null"/>. The recalculation taints the Formula's Value with it, as
        /// <c>Taint</c> taints a named cell's.
        /// </summary>
        public ErrorValue? Met { get; set; }

        /// <summary>
        /// Whether a read has met a refusal (<see cref="Value.Refusal"/>) while the Formula being computed
        /// was evaluated: a refused cell, or a Linked Table's column that holds one. The recalculation
        /// refuses the Formula with it, as <c>Taint</c> refuses one whose text names a refused cell.
        /// </summary>
        public bool MetRefusal { get; set; }
    }

    private sealed class StagedReader(
        Sheet sheet,
        Dictionary<CellAddress, Value?> staged,
        Func<CellAddress, Value?> onDemand,
        Dictionary<CellAddress, ValueArray?> arrays,
        ReadState reads,
        Func<string, StagedReader?> other) : ICellReader
    {
        /// <summary>The Sheet this reader reads.</summary>
        public Sheet Sheet => sheet;

        /// <summary>A cell as this recalculation leaves it: staged, or computed now when it is still to be (ADR-0124), or as it stood.</summary>
        public Value? Read(CellAddress address)
        {
            var value = staged.TryGetValue(address, out var stagedValue) ? stagedValue
                : onDemand(address) is { } computed ? computed
                : sheet.GetValue(address);
            if (value is { IsError: true, Error: ErrorValue.Circ or ErrorValue.GettingData } met && reads.Met != ErrorValue.Circ) reads.Met = met.Error;
            if (value is { IsRefused: true }) reads.MetRefusal = true;
            return value;
        }

        public IEnumerable<CellAddress> NonBlankIn(Area area) =>
            sheet.CellsIn(area).Where(a => Read(a) is not null).Order();

        /// <summary>
        /// A Linked Table's column. One a Formula names waits before it is evaluated (<c>Taint</c>);
        /// one <c>INDIRECT</c>'s text names is met here, and waits the same way (ADR-0049, ADR-0124).
        /// </summary>
        public Operand TableColumn(string table, string column)
        {
            var read = sheet.TableColumn(table, column);
            if (read.IsError && read.Scalar!.Value.Error == ErrorValue.GettingData && reads.Met != ErrorValue.Circ) reads.Met = ErrorValue.GettingData;
            if (sheet.ColumnHoldsRefusal(table, column)) reads.MetRefusal = true;
            return read;
        }

        public bool IsLinkedTable(string name) => sheet.Tables.ContainsKey(name);

        public Node? FormulaAt(CellAddress address)
        {
            if (sheet._cells.GetValueOrDefault(address) is not { } cell) return null;
            if (cell.Entry is { } entry) return entry.Parsed;
            return cell.SpilledFrom is { } anchor ? sheet._cells.GetValueOrDefault(anchor)?.Entry?.Parsed : null;
        }

        public ICellReader? Resolve(string? sheetName) =>
            sheet.IsThisSheet(sheetName) ? this : other(sheetName!);

        public DateOnly? Today => sheet.Today;

        public DateTime? Now => sheet._moment;

        public ulong? Draw(CellAddress cell, int place) => sheet.Draw(cell, place);

        /// <summary>
        /// The Anchor's array as this pass computed it, or as it stood. Whether it spills is the last
        /// layout's answer; a layout that changes it changes the Anchor's Value, which recalculates
        /// whatever reads <c>A1#</c> (ADR-0125). A blank reads as the 0 its spilled cell shows.
        /// </summary>
        public Operand Spill(CellAddress anchor)
        {
            Read(anchor);
            var cell = sheet._cells.GetValueOrDefault(anchor);
            ValueArray? array = null;
            if (arrays.TryGetValue(anchor, out var computed)) array = computed;
            else if (cell is not null) array = cell.Array;
            if (array is null || cell is null || cell.SpillBlocked) return Operand.Of(ErrorValue.Ref);
            var shown = new ValueArray(array.Rows, array.Columns);
            for (var r = 0; r < array.Rows; r++)
            {
                for (var c = 0; c < array.Columns; c++) shown[r, c] = array[r, c] ?? Value.FromNumber(0);
            }
            return Operand.OfSpillRange(shown);
        }
    }
}
