using System.Globalization;

namespace ExSheet.Engine;

/// <summary>
/// The Sheets ExSheet holds, in order, under one culture, with one dependency graph across them
/// (CONTEXT.md, ADR-0331). A Formula on one Sheet reads another's cells by qualifying a Reference
/// with its name (<c>=Sheet2!A1</c>), and a change on any Sheet recalculates what depends on it on
/// every Sheet, publishing the new Values only when that whole recalculation has completed
/// (ADR-0047). Linked Tables are declared on the Workbook, and read from any of its Sheets.
/// </summary>
/// <remarks>Not thread-safe: one Workbook is changed and read from one thread at a time.</remarks>
public sealed class Workbook
{
    private readonly List<Sheet> _sheets = [];

    /// <summary>What a Workbook operation under way has changed on the Sheets it did not start on; <see langword="null"/> when none is under way.</summary>
    private Dictionary<Sheet, List<SheetChange>>? _collected;

    /// <summary>
    /// Creates a Workbook of empty Sheets named <paramref name="sheetNames"/>, in that order, whose
    /// constants are read and whose Values are shown under <paramref name="culture"/> (ADR-0048).
    /// </summary>
    /// <exception cref="ArgumentException">
    /// No name is given, a name is not one a Sheet can have (<see cref="Sheet.IsValidName"/>), or two
    /// names are the same without regard to case, as Excel compares them.
    /// </exception>
    public Workbook(CultureInfo culture, IEnumerable<string> sheetNames)
    {
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(sheetNames);
        Culture = CultureInfo.ReadOnly(culture);
        foreach (var name in sheetNames)
        {
            ArgumentNullException.ThrowIfNull(name, nameof(sheetNames));
            Sheet.CheckName(name, nameof(sheetNames));
            if (Find(name) is not null) throw new ArgumentException($"Two Sheets cannot both be named '{name}'.", nameof(sheetNames));
            _sheets.Add(new Sheet(this, name));
        }
        if (_sheets.Count == 0) throw new ArgumentException("A Workbook holds at least one Sheet.", nameof(sheetNames));
    }

    /// <summary>The Workbook of one Sheet a Sheet created on its own belongs to.</summary>
    internal Workbook(Sheet only)
    {
        Culture = only.Culture;
        _sheets.Add(only);
    }

    /// <summary>The Workbook's declared culture, which every one of its Sheets reads and shows under (ADR-0048, ADR-0331).</summary>
    public CultureInfo Culture { get; }

    /// <summary>
    /// The Sheets, in the order their tabs stand. Change one through <see cref="Do"/>: a change made on
    /// the Sheet itself recalculates the whole Workbook too, but reports only what it changed on that Sheet.
    /// </summary>
    public IReadOnlyList<Sheet> Sheets => _sheets;

    /// <summary>The Sheet named <paramref name="name"/>, without regard to case, as a Reference's qualifier names one; <see langword="null"/> when none is.</summary>
    public Sheet? Find(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        foreach (var sheet in _sheets)
        {
            if (string.Equals(sheet.Name, name, StringComparison.OrdinalIgnoreCase)) return sheet;
        }
        return null;
    }

    /// <summary>The Linked Tables declared, in the order they were first declared (ADR-0049).</summary>
    public IReadOnlyList<LinkedTable> LinkedTables => _sheets[0].LinkedTables;

    /// <summary>The Linked Tables' state, which every Sheet of the Workbook reads (ADR-0331).</summary>
    internal Dictionary<string, Sheet.TableState> Tables { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Does <paramref name="edit"/> on the Sheet named <paramref name="sheet"/> as one operation, and
    /// returns the step that undoes it (ADR-0048, ADR-0331). A refused or unreadable operation
    /// changes nothing and returns no step.
    /// </summary>
    /// <exception cref="ArgumentException">No Sheet of the Workbook is named <paramref name="sheet"/>.</exception>
    /// <exception cref="SheetRefusedException">The Sheet refuses the operation (<see cref="Check"/> says why).</exception>
    /// <exception cref="FormulaSyntaxException">A typed Formula cannot be read.</exception>
    /// <exception cref="NotSupportedException">
    /// The edit renames the Sheet, or inserts or deletes its rows or columns, in a Workbook of several
    /// Sheets: not done yet, as it would leave the other Sheets' References to it stale. <see cref="Check"/>
    /// does not report it.
    /// </exception>
    public WorkbookStep Do(string sheet, SheetEdit edit)
    {
        var target = Named(sheet);
        ArgumentNullException.ThrowIfNull(edit);
        SheetStep? step = null;
        var change = Collecting(target, () => (step = target.Do(edit)).Change);
        return new WorkbookStep(this, step!, change);
    }

    /// <summary>Whether <see cref="Do"/> would refuse <paramref name="edit"/> on the Sheet named <paramref name="sheet"/> as it stands, and why; <see langword="null"/> when it would not.</summary>
    /// <exception cref="ArgumentException">No Sheet of the Workbook is named <paramref name="sheet"/>.</exception>
    public SheetRefusal? Check(string sheet, SheetEdit edit) => Named(sheet).Check(edit);

    /// <summary>
    /// Declares a Linked Table on the Workbook (ADR-0049, ADR-0331), as
    /// <see cref="Sheet.DeclareLinkedTable(string, IReadOnlyList{string}, IReadOnlyList{string})"/>
    /// declares one; a Formula on any Sheet reads it.
    /// </summary>
    /// <param name="name">The name Formulas read it by.</param>
    /// <param name="columns">The column names.</param>
    /// <param name="key">The key's columns; an empty list declares no key.</param>
    /// <exception cref="ArgumentException">The name, the columns or the key are not ones a table can have.</exception>
    public WorkbookChange DeclareLinkedTable(string name, IReadOnlyList<string> columns, IReadOnlyList<string> key) =>
        Collecting(_sheets[0], () => _sheets[0].DeclareLinkedTable(name, columns, key));

    /// <summary>
    /// Takes a Linked Table's snapshot (ADR-0049), as <see cref="Sheet.PushLinkedTable"/> takes one,
    /// and recalculates every Formula that reads it, on every Sheet.
    /// </summary>
    /// <param name="name">The table's name.</param>
    /// <param name="rows">The rows, each one Value per column.</param>
    /// <exception cref="InvalidOperationException">No table of that name is declared.</exception>
    /// <exception cref="RepeatedKeyException">A key repeats in the snapshot; the table waits again.</exception>
    /// <exception cref="ArgumentException">A row does not have one Value per column, or holds a state of a computation.</exception>
    public WorkbookChange PushLinkedTable(string name, IEnumerable<IReadOnlyList<Value?>> rows) =>
        Collecting(_sheets[0], () => _sheets[0].PushLinkedTable(name, rows));

    private Sheet Named(string sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        return Find(sheet) ?? throw new ArgumentException($"No Sheet of the Workbook is named '{sheet}'.", nameof(sheet));
    }

    /// <summary>
    /// Runs an operation on <paramref name="sheet"/>, and reports what it changed there together with
    /// what its recalculation changed on every other Sheet.
    /// </summary>
    internal WorkbookChange Collecting(Sheet sheet, Func<SheetChange> operation)
    {
        var outer = _collected;
        var collected = _collected = [];
        try
        {
            collected[sheet] = [operation()];
        }
        finally
        {
            _collected = outer;
        }
        if (outer is not null)
        {
            foreach (var (other, changes) in collected)
            {
                if (!outer.TryGetValue(other, out var list)) outer[other] = list = [];
                list.AddRange(changes);
            }
        }
        return new WorkbookChange(_sheets
            .Where(collected.ContainsKey)
            .Select(s => (Sheet: s, Change: SheetChange.Merge(collected[s])))
            .Where(p => p.Sheet == sheet || !ReferenceEquals(p.Change, SheetChange.None))
            .ToList());
    }

    /// <summary>Records what a recalculation changed on <paramref name="sheet"/>, which the operation under way did not start on.</summary>
    internal void Collect(Sheet sheet, SheetChange change)
    {
        if (_collected is null || ReferenceEquals(change, SheetChange.None)) return;
        if (!_collected.TryGetValue(sheet, out var list)) _collected[sheet] = list = [];
        list.Add(change);
    }
}

/// <summary>
/// What one change to a Workbook did, Sheet by Sheet, reported after its recalculation has
/// completed: every Value it names is already published, and all of them come from that one
/// recalculation (ADR-0047, ADR-0331).
/// </summary>
public sealed class WorkbookChange
{
    private readonly IReadOnlyList<(Sheet Sheet, SheetChange Change)> _changes;

    internal WorkbookChange(IReadOnlyList<(Sheet Sheet, SheetChange Change)> changes) => _changes = changes;

    /// <summary>The Sheets the change reached, in the Workbook's order: the one it was made on, and every other whose Values or formats it changed.</summary>
    public IReadOnlyList<Sheet> Sheets => [.. _changes.Select(c => c.Sheet)];

    /// <summary>What the change did on <paramref name="sheet"/>; a change that changed nothing there for a Sheet it did not reach.</summary>
    public SheetChange For(Sheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        foreach (var (s, change) in _changes)
        {
            if (s == sheet) return change;
        }
        return SheetChange.None;
    }
}

/// <summary>
/// One operation a Workbook has done on one of its Sheets, as one step of ExSheet's one undo stack
/// (ADR-0048, ADR-0331): undoing it restores what it changed on that Sheet exactly, with one
/// recalculation across the Workbook; redoing it does it again.
/// </summary>
public sealed class WorkbookStep
{
    private readonly Workbook _workbook;
    private readonly SheetStep _step;

    internal WorkbookStep(Workbook workbook, SheetStep step, WorkbookChange change)
    {
        _workbook = workbook;
        _step = step;
        Change = change;
    }

    /// <summary>The Sheet the operation was done on.</summary>
    public Sheet Sheet => _step.Sheet;

    /// <summary>What doing the operation changed.</summary>
    public WorkbookChange Change { get; }

    /// <summary>Whether the step is currently undone.</summary>
    public bool IsUndone => _step.IsUndone;

    /// <summary>Undoes the operation, and says what that changed.</summary>
    /// <exception cref="InvalidOperationException">The step is already undone.</exception>
    public WorkbookChange Undo() => _workbook.Collecting(Sheet, _step.Undo);

    /// <summary>Does the operation again after <see cref="Undo"/>, and says what that changed.</summary>
    /// <exception cref="InvalidOperationException">The step is not undone.</exception>
    public WorkbookChange Redo() => _workbook.Collecting(Sheet, _step.Redo);
}

/// <summary>A cell of one Sheet of a Workbook: what a recalculation across the Workbook keys its work by (ADR-0331).</summary>
internal readonly record struct SheetCell(Sheet Sheet, CellAddress Address);
