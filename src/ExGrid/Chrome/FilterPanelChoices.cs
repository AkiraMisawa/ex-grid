using System.Globalization;

namespace ExGrid.Chrome;

/// <summary>
/// What a filter panel's choices mean (ADR-0009) — decided here, once, so that the
/// built-in panel and a substituted one turn the same choices into the same
/// <see cref="FilterSpec"/>, and swapping Chrome still changes nothing about behaviour
/// (ADR-0010, FN-17). A panel lays out a value list or a condition, keeps its working
/// state, and asks this class what that state means; it decides nothing itself. Pure, so
/// every rule is testable without a component.
/// </summary>
public static class FilterPanelChoices
{
    /// <summary>
    /// The values a value list starts with chosen: the applied <c>In</c> list where one is
    /// in force, and every value otherwise — no filter chooses everything. Intersected with
    /// the domain just fetched: another column's filter can have shifted it since the list
    /// was applied, and a value the panel cannot show must not ride along chosen unseen.
    /// </summary>
    public static HashSet<object?> InitiallyChosen(FilterSpec? current, DistinctValues domain)
    {
        ArgumentNullException.ThrowIfNull(domain);
        return current is { Clauses: [{ Operator: FilterOperator.In, Values: { } applied }] }
            ? new HashSet<object?>(applied.Intersect(domain.Values))
            : new HashSet<object?>(domain.Values);
    }

    /// <summary>
    /// A value list's filter: the chosen values as one <c>In</c> clause, in the domain's
    /// order — or null, no filter at all, when every value is chosen. Set membership, never
    /// a count: a count drifts when the domain shifts under another column's filter, and a
    /// count test once removed a filter the user was looking at. Nothing chosen is a filter
    /// that keeps nothing, which is what was asked for.
    /// </summary>
    public static FilterSpec? FromValueList(DistinctValues domain, IReadOnlySet<object?> chosen)
    {
        ArgumentNullException.ThrowIfNull(domain);
        ArgumentNullException.ThrowIfNull(chosen);
        if (domain.IsTooMany)
            throw new ArgumentException("A TooMany answer has no values to choose from; the panel shows its condition form.", nameof(domain));
        if (domain.Values.All(chosen.Contains))
            return null;
        var ordered = domain.Values.Where(chosen.Contains).ToArray();
        return new FilterSpec([new FilterClause(FilterOperator.In, Values: ordered)]);
    }

    /// <summary>
    /// A value list's filter with a search in the box (ADR-0009, 2026-09-26): the checked
    /// values among those whose text matches, as Excel applies them — a checked value the
    /// search hides is not applied. With <paramref name="addToCurrent"/>, Excel's "Add
    /// current selection to filter", the values of the <c>In</c> list in force join them,
    /// including any the domain no longer shows. With no search it is
    /// <see cref="FromValueList(DistinctValues, IReadOnlySet{object?})"/>: what is checked.
    /// <paramref name="textOf"/> is the text the panel shows for a value, which is what the
    /// search is matched against.
    /// </summary>
    public static FilterSpec? FromValueList(
        DistinctValues domain, IReadOnlySet<object?> chosen, Func<object?, string> textOf,
        string search, bool addToCurrent, FilterSpec? current)
    {
        ArgumentNullException.ThrowIfNull(domain);
        ArgumentNullException.ThrowIfNull(chosen);
        ArgumentNullException.ThrowIfNull(textOf);
        ArgumentNullException.ThrowIfNull(search);
        if (search.Length == 0)
            return FromValueList(domain, chosen);
        if (domain.IsTooMany)
            throw new ArgumentException("A TooMany answer has no values to choose from; the panel shows its condition form.", nameof(domain));

        var applied = domain.Values.Where(v => chosen.Contains(v) && Matches(search, textOf(v))).ToList();
        if (addToCurrent && InForce(current) is { } inForce)
        {
            var extra = inForce.Where(v => !applied.Contains(v)).ToList();
            applied = [.. domain.Values.Where(v => applied.Contains(v) || extra.Contains(v)), .. extra.Where(v => !domain.Values.Contains(v))];
        }
        if (domain.Values.All(applied.Contains))
            return null;
        return new FilterSpec([new FilterClause(FilterOperator.In, Values: [.. applied])]);
    }

    /// <summary>Whether a value's text matches the search: contained, ignoring case, as
    /// the panels list it (ADR-0009). An empty search matches everything.</summary>
    public static bool Matches(string search, string text)
    {
        ArgumentNullException.ThrowIfNull(search);
        ArgumentNullException.ThrowIfNull(text);
        return search.Length == 0 || text.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Whether "Add current selection to filter" is offered: while a search is
    /// active on a column whose filter in force is a value list (ADR-0009, 2026-09-26).</summary>
    public static bool OffersAddToFilter(FilterSpec? current, string search)
    {
        ArgumentNullException.ThrowIfNull(search);
        return search.Length > 0 && InForce(current) is not null;
    }

    /// <summary>What "(Select All)" shows over the values the list shows now: checked when
    /// every one is chosen, clear when none is, mixed otherwise (ADR-0009, 2026-09-26).</summary>
    public static SelectAllState StateOfAll(IEnumerable<object?> shown, IReadOnlySet<object?> chosen)
    {
        ArgumentNullException.ThrowIfNull(shown);
        ArgumentNullException.ThrowIfNull(chosen);
        var any = false;
        var all = true;
        foreach (var value in shown)
        {
            if (chosen.Contains(value))
                any = true;
            else
                all = false;
        }
        return all ? SelectAllState.Checked : any ? SelectAllState.Mixed : SelectAllState.Unchecked;
    }

    /// <summary>"(Select All)" toggled: every value shown becomes chosen, or, when every
    /// one already was, none of them is. Values the search hides are left as they are.</summary>
    public static void ToggleAll(IEnumerable<object?> shown, ISet<object?> chosen)
    {
        ArgumentNullException.ThrowIfNull(shown);
        ArgumentNullException.ThrowIfNull(chosen);
        var values = shown.ToList();
        if (StateOfAll(values, (IReadOnlySet<object?>)chosen) == SelectAllState.Checked)
        {
            foreach (var value in values)
                chosen.Remove(value);
        }
        else
        {
            foreach (var value in values)
                chosen.Add(value);
        }
    }

    /// <summary>
    /// A condition form's filter with an optional second condition, as Excel's Custom
    /// AutoFilter (ADR-0009, 2026-09-26): each finished condition is a clause, joined by
    /// <paramref name="combinator"/>. A condition whose operator takes an operand and has
    /// none is unfinished and left out; one finished condition is
    /// <see cref="FromCondition"/>'s answer, and none is no filter.
    /// </summary>
    public static FilterSpec? FromConditions(
        FilterOperator first, object? firstOperand, FilterCombinator combinator,
        FilterOperator? second, object? secondOperand)
    {
        var clauses = new List<FilterClause>(2);
        foreach (var (op, operand) in new[] { ((FilterOperator?)first, firstOperand), (second, secondOperand) })
        {
            if (op is not { } oper)
                continue;
            if (FromCondition(oper, operand) is { Clauses: [var clause] })
                clauses.Add(clause);
        }
        return clauses.Count switch
        {
            0 => null,
            1 => new FilterSpec(clauses),
            _ => new FilterSpec(clauses, combinator),
        };
    }

    /// <summary>The second condition a condition form starts with, and the combinator
    /// between the two: those in force where the column's filter is two conditions; none,
    /// joined by AND, otherwise.</summary>
    public static (FilterOperator? Operator, object? Operand, FilterCombinator Combinator) InitialSecond(FilterSpec? current)
        => current is { Clauses: [{ Operator: not FilterOperator.In }, { Operator: not FilterOperator.In } second] }
            ? (second.Operator, second.Value, current.Combinator)
            : (null, null, FilterCombinator.And);

    private static IReadOnlyList<object?>? InForce(FilterSpec? current)
        => current is { Clauses: [{ Operator: FilterOperator.In, Values: { } applied }] } ? applied : null;

    /// <summary>
    /// The operator a condition form starts on: the one in force, where the column's filter
    /// is one or two conditions (the first of them); where a value list's answer was TooMany,
    /// <c>Contains</c> — the degraded form is Excel's own search box, where the type allows
    /// it; the first the column allows otherwise.
    /// </summary>
    public static FilterOperator InitialOperator(ColumnType type, FilterSpec? current, bool tooMany)
    {
        if (FirstCondition(current) is { } clause)
            return clause.Operator;
        var allowed = FilterOperators.AllowedFor(type);
        return tooMany && allowed.Contains(FilterOperator.Contains) ? FilterOperator.Contains : allowed[0];
    }

    /// <summary>The operand a condition form starts with: the one in force, where the
    /// column's filter is one or two conditions (the first's); null otherwise.</summary>
    public static object? InitialOperand(FilterSpec? current) => FirstCondition(current)?.Value;

    /// <summary>The first condition of a filter in force that is one or two conditions.</summary>
    private static FilterClause? FirstCondition(FilterSpec? current) => current switch
    {
        { Clauses: [{ Operator: not FilterOperator.In } only] } => only,
        { Clauses: [{ Operator: not FilterOperator.In } first, { Operator: not FilterOperator.In }] } => first,
        _ => null,
    };

    /// <summary>Whether the operator takes an operand — every one but <c>IsBlank</c> and
    /// <c>IsNotBlank</c>. A panel keeps Apply unavailable while one that does has none.</summary>
    public static bool TakesOperand(FilterOperator op) => op is not (FilterOperator.IsBlank or FilterOperator.IsNotBlank);

    /// <summary>
    /// A condition's filter, from its operator and its typed operand: one clause. An
    /// operator that takes an operand and has none is no filter at all (null) — the
    /// condition was cleared. <c>In</c> offered in a condition form is membership of the one
    /// value given, so its operand becomes a one-member list: a clause the engine reads from
    /// <c>Values</c> and would refuse with <c>Value</c> alone.
    /// </summary>
    public static FilterSpec? FromCondition(FilterOperator op, object? operand)
    {
        if (!TakesOperand(op))
            return new FilterSpec([new FilterClause(op)]);
        if (operand is null)
            return null;
        return op == FilterOperator.In
            ? new FilterSpec([new FilterClause(FilterOperator.In, Values: [operand])])
            : new FilterSpec([new FilterClause(op, operand)]);
    }

    /// <summary>
    /// A typed operand from what was typed, for a panel whose value field is text: the
    /// engine compares typed values (ADR-0023). Invariant first, so "1234.5" means the same
    /// number on every machine; the local convention as the fallback. Null when the text
    /// does not read as the column's type — an operand that cannot be read applies nothing,
    /// and the panel stands.
    /// </summary>
    public static object? ParseOperand(ColumnType type, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return type switch
        {
            ColumnType.Text => text,
            ColumnType.Number => decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var invariant)
                ? invariant
                : decimal.TryParse(text, out var local) ? local : null,
            ColumnType.Date => DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var isoDate)
                ? isoDate
                : DateTime.TryParse(text, out var localDate) ? localDate : null,
            ColumnType.Boolean => bool.TryParse(text, out var flag) ? flag : null,
            _ => null,
        };
    }
}
