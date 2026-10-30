using ExGrid.Cells;
using ExGrid.Finding;
using ExGrid.Selection;

namespace ExGrid.Validation;

/// <summary>One rule's complaint about a row: which column wears it, and why.</summary>
public sealed record RuleViolation(string Column, string Message);

/// <summary>One cell a <see cref="Breach"/> involves: the row by its Row Key (ADR-0140), wherever it
/// stands, and the column by name.</summary>
public readonly record struct BreachCell(object RowKey, string Column);

/// <summary>
/// One set rule's complaint over the whole collection (ADR-0381): the rule's name, the sentence every
/// involved cell says, and the cells it involves. The message names the other side of the breach in the
/// Consumer's own words, a trade id rather than a row position, which a sort changes.
/// </summary>
public sealed record Breach(string Rule, string Message, IReadOnlyList<BreachCell> Cells);

/// <summary>
/// One step to the next or the previous error cell (ADR-0381 section 3), asked of the Consumer as a Find
/// step is (ADR-0055): the grid knows only the cells it paints, so a step through those alone would skip
/// every error outside the Window without saying so.
/// </summary>
public sealed record GridErrorStepRequest
{
    /// <summary>Whether this step runs backward: the previous error cell.</summary>
    public bool Backward { get; init; }

    /// <summary>The Focus the step starts from: it begins at the cell after it (before it, backward) and
    /// considers it last, so a lone error cell finds itself. Null: from the start (the end, backward).</summary>
    public CellPosition? From { get; init; }

    /// <summary>The visible columns' names in the grid's current order: position <c>i</c> here is column
    /// <c>i</c> of <see cref="From"/> and of the answer.</summary>
    public required IReadOnlyList<string> Columns { get; init; }

    /// <summary>The order the positions were read in (ADR-0011). A grid on the Script Core sends it, and
    /// discards an answer it receives under another version.</summary>
    public required int RowSequenceVersion { get; init; }
}

/// <summary>
/// The bundled ruleset (ADR-0034). <b>Ownership and driving are the Consumer's; the
/// implementation ships with the library</b> — the same arrangement as the undo stack
/// (ADR-0007) and <c>GridSource</c> (ADR-0001). The grid never references this class.
///
/// <para>It holds both kinds of rule and answers both display channels, so <b>one
/// implementation runs whether a change arrived by a single edit, by a paste, or from
/// program code</b>. Column rules are asked at the editor, through
/// <see cref="ValidatorFor"/>, which is what a <c>GridColumn</c>'s <c>Validate</c> is set
/// to. Row rules span the row and can only be judged <b>after</b> the value is applied —
/// a row that does not exist at commit time, because applying is the Consumer's — so they
/// are Flag-only by construction and are evaluated by <see cref="Evaluate"/>.</para>
///
/// <para>The map is keyed by <b>row instance</b>, and <see cref="Evaluate"/> replaces it
/// wholesale: the Consumer returns new instances when it applies (ADR-0007), so a map
/// that accumulated would answer for rows nobody can see. A ruleset made with a Row Key
/// function is keyed by Row Key instead, which is what lets a <see cref="Set"/> rule name
/// rows outside the Window (ADR-0381).</para>
/// </summary>
public sealed class GridRuleset<TRow> where TRow : class
{
    private readonly Dictionary<string, Func<TRow, string, EditVerdict>> _columnRules = [];
    private readonly List<Func<TRow, IEnumerable<RuleViolation>>> _rowRules = [];
    private readonly List<Func<IReadOnlyList<TRow>, IEnumerable<Breach>>> _setRules = [];
    private readonly Func<TRow, object>? _rowKey;
    private Dictionary<object, Dictionary<string, string>> _violations = new(ReferenceEqualityComparer.Instance);
    // The rows handed to the last Evaluate, by instance: the rows a grid paints are mostly these, and a lookup by
    // instance spares the Consumer's key function a call per cell per render.
    private Dictionary<TRow, Dictionary<string, string>?> _byInstance = new(ReferenceEqualityComparer.Instance);
    private IReadOnlyList<Breach> _breaches = [];

    /// <summary>A ruleset keyed by row instance, holding column and row rules (ADR-0034).</summary>
    public GridRuleset()
    {
    }

    /// <summary>A ruleset keyed by Row Key (ADR-0381): the same function the Consumer's Source declares
    /// (ADR-0140). It may hold <see cref="Set"/> rules, whose Breaches name rows by their keys.</summary>
    public GridRuleset(Func<TRow, object> rowKey)
    {
        ArgumentNullException.ThrowIfNull(rowKey);
        _rowKey = rowKey;
        _violations = [];
    }

    /// <summary>A rule on one column's committed text, asked at the editor. The intended
    /// split is the ADR's: an unparseable text Rejects, a parseable value a rule dislikes
    /// Flags — but which is which stays this side's convention, not the grid's.</summary>
    public GridRuleset<TRow> Column(string column, Func<TRow, string, EditVerdict> rule)
    {
        ArgumentException.ThrowIfNullOrEmpty(column);
        ArgumentNullException.ThrowIfNull(rule);
        _columnRules[column] = rule;
        return this;
    }

    /// <summary>A rule spanning the row, evaluated after the value is applied. Flag-only:
    /// there is no row to judge at commit time, so there is nothing for it to veto.</summary>
    public GridRuleset<TRow> Row(Func<TRow, IEnumerable<RuleViolation>> rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        _rowRules.Add(rule);
        return this;
    }

    /// <summary>
    /// A rule over the whole collection (ADR-0381), judged after apply by <see cref="Evaluate"/> over every
    /// row it is handed. Flag-tier by construction: the way to a valid collection often passes through
    /// invalid ones, so it never holds the Cell Editor, and its refusal is the Consumer's own save while
    /// <see cref="Breaches"/> is not empty. Every cell a Breach names wears <c>Error</c> and its message.
    /// </summary>
    /// <exception cref="InvalidOperationException">The ruleset was made without a Row Key function: a
    /// Breach names rows wherever they stand, which a row instance outside the Window cannot.</exception>
    public GridRuleset<TRow> Set(Func<IReadOnlyList<TRow>, IEnumerable<Breach>> rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (_rowKey is null)
        {
            throw new InvalidOperationException(
                "A set rule names its rows by Row Key, so the ruleset is made with the Source's Row Key function: "
                + "new GridRuleset<TRow>(rowKey) (ADR-0381).");
        }
        _setRules.Add(rule);
        return this;
    }

    /// <summary>The Breaches the last <see cref="Evaluate"/> found, in the order the set rules were declared
    /// and each rule answered them: an order that does not depend on the Window (ADR-0381 section 3). The
    /// Consumer's save refuses while this is not empty.</summary>
    public IReadOnlyList<Breach> Breaches => _breaches;

    /// <summary>What a column's <c>Validate</c> is set to, or null where no rule was
    /// declared — which the grid reads as Accept.</summary>
    public Func<TRow, string, EditVerdict>? ValidatorFor(string column)
        => _columnRules.TryGetValue(column, out var rule) ? rule : null;

    /// <summary>
    /// Re-judge the rows, after applying. Called by the Consumer with the rows it now
    /// holds — the Window, or the whole result — and it replaces what was known before.
    /// A ruleset holding <see cref="Set"/> rules is handed the whole collection: a breach
    /// between a row on screen and one far below is still a breach (ADR-0381).
    ///
    /// <para>Column rules are re-run here as well as at the editor, on the applied value:
    /// that is what makes a paste and a program-code change wear the same marks as a
    /// typed edit. A Reject arriving this way becomes a Flag, because the value is
    /// already applied and there is no editor left to hold — the partial-apply-plus-Flag
    /// default ADR-0034 chose for a paste.</para>
    /// </summary>
    public void Evaluate(IEnumerable<TRow> rows, IEnumerable<GridColumn<TRow>> columns)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(columns);
        var declared = columns as IReadOnlyList<GridColumn<TRow>> ?? [.. columns];
        var all = rows as IReadOnlyList<TRow> ?? [.. rows];
        var next = _rowKey is null
            ? new Dictionary<object, Dictionary<string, string>>(ReferenceEqualityComparer.Instance)
            : [];
        IEqualityComparer<object> sameKey = _rowKey is null ? ReferenceEqualityComparer.Instance : EqualityComparer<object>.Default;
        var keys = new HashSet<object>(sameKey);
        var keyed = new object[all.Count];
        for (var i = 0; i < all.Count; i++)
        {
            // Two rows under one key would share one set of marks, and the earlier's would be lost unsaid
            // (ADR-0140: a Row Key names one row).
            keyed[i] = KeyOf(all[i]);
            if (!keys.Add(keyed[i]) && _rowKey is not null)
                throw new ArgumentException($"Two rows handed to Evaluate have the Row Key '{keyed[i]}': a Row Key names one row (ADR-0140).");
        }
        for (var i = 0; i < all.Count; i++)
        {
            var row = all[i];
            Dictionary<string, string>? found = null;
            foreach (var column in declared)
            {
                if (ValidatorFor(column.Name) is not { } rule)
                    continue;
                // The same text the editor would show and commit for this cell — the
                // column's own formatting, not a bare ToString — so a rule written to
                // the editor's grammar reads the same grammar here (ADR-0034: one
                // implementation, by edit, paste or program code).
                var verdict = rule(row, column.Info.TextOf(row));
                if (verdict.Kind is EditVerdictKind.Accept)
                    continue;
                (found ??= [])[column.Name] = verdict.Message!;
            }
            foreach (var rule in _rowRules)
            {
                foreach (var violation in rule(row))
                    (found ??= [])[violation.Column] = violation.Message;
            }
            if (found is not null)
                next[keyed[i]] = found;
        }
        var breaches = new List<Breach>();
        if (_setRules.Count > 0)
        {
            // A Breach naming a key no row has, or a column the grid does not declare, would mark nothing, and
            // the save would refuse with no cell to show why: refused here instead (spine rule 1).
            var names = declared.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
            foreach (var rule in _setRules)
            {
                foreach (var breach in rule(all))
                {
                    ArgumentNullException.ThrowIfNull(breach);
                    ArgumentException.ThrowIfNullOrWhiteSpace(breach.Rule);
                    ArgumentException.ThrowIfNullOrWhiteSpace(breach.Message);
                    if (breach.Cells is null || breach.Cells.Count == 0)
                        throw new ArgumentException($"Breach '{breach.Rule}' involves no cell: a breach names the cells that show it (ADR-0381).");
                    foreach (var (key, column) in breach.Cells)
                    {
                        if (key is null || !keys.Contains(key))
                            throw new ArgumentException($"Breach '{breach.Rule}' names Row Key '{key}', which no row handed to Evaluate has (ADR-0381).");
                        ArgumentException.ThrowIfNullOrEmpty(column);
                        if (!names.Contains(column))
                            throw new ArgumentException($"Breach '{breach.Rule}' names the column '{column}', which none of the columns handed to Evaluate is (ADR-0381).");
                        var found = next.TryGetValue(key, out var held) ? held : next[key] = [];
                        // A cell two rules complain about says both: neither sentence outranks the other.
                        found[column] = found.TryGetValue(column, out var earlier) && earlier != breach.Message
                            ? earlier + " " + breach.Message
                            : breach.Message;
                    }
                    breaches.Add(breach);
                }
            }
        }
        var byInstance = new Dictionary<TRow, Dictionary<string, string>?>(ReferenceEqualityComparer.Instance);
        for (var i = 0; i < all.Count; i++)
            byInstance[all[i]] = next.GetValueOrDefault(keyed[i]);
        _violations = next;
        _byInstance = byInstance;
        _breaches = breaches;
        // New instances, deliberately: their identity is what tells the rows that what
        // they are painting has moved.
        State = StateOf;
        Message = MessageOf;
    }

    /// <summary>
    /// The lookups to hand the grid, replaced by every <see cref="Evaluate"/>.
    ///
    /// <para><b>Bind these, not the methods.</b> A row skips its render while the state
    /// lookup it was given is reference-identical (ADR-0003), and this class judges into a
    /// map it rewrites — so a verdict that changed without the row instance changing would
    /// never repaint. Handing over a new delegate is the signal the grid's own
    /// documentation asks for: <i>hold it in a field and hand over a new one when the
    /// metadata changes</i>.</para>
    /// </summary>
    public CellStateOf<TRow> State { get; private set; } = static (_, _) => CellState.Normal;

    /// <inheritdoc cref="State"/>
    public Func<TRow, GridColumn<TRow>, string?> Message { get; private set; } = static (_, _) => null;

    /// <summary>
    /// The next or the previous error cell (ADR-0381 section 3), for a grid's error step: every cell the
    /// last <see cref="Evaluate"/> marked, walked in Find's order (ADR-0055) through
    /// <paramref name="rowsInOrder"/>, the result in the order the grid shows it, which a Consumer reads
    /// from its Source. A marked row that is not in that order, filtered out, is not stepped to; a marked
    /// column that is not among the request's, hidden, neither.
    /// </summary>
    public GridFindResult FindError(GridErrorStepRequest request, IReadOnlyList<TRow> rowsInOrder)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(rowsInOrder);
        var columnCount = request.Columns.Count;
        if (request.From is { } given)
            StartOf(given, rowsInOrder.Count, columnCount);
        if (columnCount == 0 || rowsInOrder.Count == 0 || _violations.Count == 0)
            return GridFindResult.NotFound;
        long total = (long)rowsInOrder.Count * columnCount;
        long start = request.From is { } from
            ? StartOf(from, rowsInOrder.Count, columnCount)
            : request.Backward ? 0 : total - 1;
        // The nearest marked cell past the start, counted in steps the walk would take: one pass over the
        // rows, never one over every cell.
        long best = long.MaxValue;
        var found = GridFindResult.NotFound;
        for (var row = 0; row < rowsInOrder.Count; row++)
        {
            if (MarksOf(rowsInOrder[row]) is not { } marked)
                continue;
            for (var column = 0; column < columnCount; column++)
            {
                if (!marked.ContainsKey(request.Columns[column]))
                    continue;
                var ordinal = (long)row * columnCount + column;
                var steps = request.Backward ? start - ordinal : ordinal - start;
                steps = ((steps % total) + total) % total;
                // The start itself is considered last, so a lone error cell finds itself.
                if (steps == 0)
                    steps = total;
                if (steps < best)
                {
                    best = steps;
                    found = GridFindResult.Found(row, request.Columns[column]);
                }
            }
        }
        return found;
    }

    // The Focus as an ordinal of the walk. A Focus outside the rows handed over means they are not the rows the
    // grid shows, the unfiltered collection or a shorter page, and an answer read in them would name another row:
    // refused, not walked from the start (spine rule 1).
    private static long StartOf(CellPosition from, int rows, int columns)
    {
        if (from.Row < 0 || from.Row >= rows || from.Column < 0 || from.Column >= columns)
            throw new ArgumentOutOfRangeException(nameof(from), $"The error step starts at row {from.Row}, column {from.Column}, outside the {rows} rows "
                + $"and {columns} columns it was handed: rowsInOrder is the result in the grid's order, read from the Source (ADR-0381).");
        return (long)from.Row * columns + from.Column;
    }

    private Dictionary<string, string>? MarksOf(TRow row)
        => _violations.Count == 0 ? null
            : _byInstance.TryGetValue(row, out var held) ? held
            : _violations.GetValueOrDefault(KeyOf(row));

    private object KeyOf(TRow row)
        => _rowKey is null
            ? row
            : _rowKey(row) ?? throw new InvalidOperationException("A row has a null Row Key: a Row Key names a row (ADR-0140).");

    /// <summary>The grid's <c>CellState</c> channel — a cheap enum, consulted every
    /// render for every cell (ADR-0034).</summary>
    internal CellState StateOf(TRow row, GridColumn<TRow> column)
        => MarksOf(row) is { } found && found.ContainsKey(column.Name)
            ? CellState.Error
            : CellState.Normal;

    /// <summary>The grid's message channel — read for at most one cell at a time, at the
    /// moment a popover opens (ADR-0034).</summary>
    internal string? MessageOf(TRow row, GridColumn<TRow> column)
        => MarksOf(row) is { } found && found.TryGetValue(column.Name, out var message)
            ? message
            : null;
}
