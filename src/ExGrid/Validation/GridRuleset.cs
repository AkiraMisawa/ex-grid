using ExGrid.Cells;

namespace ExGrid.Validation;

/// <summary>One rule's complaint about a row: which column wears it, and why.</summary>
public sealed record RuleViolation(string Column, string Message);

/// <summary>
/// The bundled ruleset (ADR-0034). **Ownership and driving are the Consumer's; the
/// implementation ships with the library** — the same arrangement as the undo stack
/// (ADR-0007) and `GridSource` (ADR-0001). The grid never references this class.
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
/// that accumulated would answer for rows nobody can see.</para>
/// </summary>
public sealed class GridRuleset<TRow> where TRow : class
{
    private readonly Dictionary<string, Func<TRow, string, EditVerdict>> _columnRules = [];
    private readonly List<Func<TRow, IEnumerable<RuleViolation>>> _rowRules = [];
    private Dictionary<TRow, Dictionary<string, string>> _violations =
        new(ReferenceEqualityComparer.Instance as IEqualityComparer<TRow> ?? EqualityComparer<TRow>.Default);

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

    /// <summary>What a column's <c>Validate</c> is set to, or null where no rule was
    /// declared — which the grid reads as Accept.</summary>
    public Func<TRow, string, EditVerdict>? ValidatorFor(string column)
        => _columnRules.TryGetValue(column, out var rule) ? rule : null;

    /// <summary>
    /// Re-judge the rows, after applying. Called by the Consumer with the rows it now
    /// holds — the Window, or the whole result — and it replaces what was known before.
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
        var next = new Dictionary<TRow, Dictionary<string, string>>(
            ReferenceEqualityComparer.Instance as IEqualityComparer<TRow> ?? EqualityComparer<TRow>.Default);
        foreach (var row in rows)
        {
            Dictionary<string, string>? found = null;
            foreach (var column in declared)
            {
                if (ValidatorFor(column.Name) is not { } rule)
                    continue;
                // The same text the editor would show and commit for this cell — the
                // column's own formatting, not a bare ToString — so a rule written to
                // the editor's grammar reads the same grammar here (ADR-0034: one
                // implementation, by edit, paste or program code).
                var verdict = rule(row, Components.ExGridRow<TRow>.CellText(column, row));
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
                next[row] = found;
        }
        _violations = next;
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

    /// <summary>The grid's <c>CellState</c> channel — a cheap enum, consulted every
    /// render for every cell (ADR-0034).</summary>
    public CellState StateOf(TRow row, GridColumn<TRow> column)
        => _violations.TryGetValue(row, out var found) && found.ContainsKey(column.Name)
            ? CellState.Error
            : CellState.Normal;

    /// <summary>The grid's message channel — read for at most one cell at a time, at the
    /// moment a popover opens (ADR-0034).</summary>
    public string? MessageOf(TRow row, GridColumn<TRow> column)
        => _violations.TryGetValue(row, out var found) && found.TryGetValue(column.Name, out var message)
            ? message
            : null;
}
