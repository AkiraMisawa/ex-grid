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
    /// The operator a condition form starts on: the one in force, where the column's filter
    /// is a single condition; where a value list's answer was TooMany, <c>Contains</c> — the
    /// degraded form is Excel's own search box, where the type allows it; the first the
    /// column allows otherwise.
    /// </summary>
    public static FilterOperator InitialOperator(ColumnType type, FilterSpec? current, bool tooMany)
    {
        if (current is { Clauses: [{ Operator: not FilterOperator.In } clause] })
            return clause.Operator;
        var allowed = FilterOperators.AllowedFor(type);
        return tooMany && allowed.Contains(FilterOperator.Contains) ? FilterOperator.Contains : allowed[0];
    }

    /// <summary>The operand a condition form starts with: the one in force, where the
    /// column's filter is a single condition that takes one; null otherwise.</summary>
    public static object? InitialOperand(FilterSpec? current)
        => current is { Clauses: [{ Operator: not FilterOperator.In } clause] } ? clause.Value : null;

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
