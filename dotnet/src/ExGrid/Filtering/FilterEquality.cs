namespace ExGrid;

/// <summary>
/// Whether two filters are the same condition, compared by what they hold rather than by which lists
/// and dictionaries hold it, so that a filter read back from JSON is the same as the one written
/// (ADR-0207; FN-13, ST-4): the same columns, each with the same combinator and the same clauses in
/// the same order, each with the same operator and operands of the same type and value, and the same
/// Opaque Filter.
/// </summary>
internal static class FilterEquality
{
    /// <summary>Whether <paramref name="a"/> and <paramref name="b"/> filter alike; two nulls do.</summary>
    internal static bool SameFilter(GridFilter? a, GridFilter? b)
    {
        if (a is null || b is null)
            return a is null && b is null;
        if (ReferenceEquals(a, b))
            return true;
        if (a.Columns.Count != b.Columns.Count || !SameOperand(a.Opaque, b.Opaque))
            return false;
        foreach (var (column, spec) in a.Columns)
        {
            if (!b.Columns.TryGetValue(column, out var other) || !SameSpec(spec, other))
                return false;
        }
        return true;
    }

    /// <summary>Whether two lists hold the same items in the same order, each compared by
    /// <paramref name="same"/>; two nulls do.</summary>
    internal static bool SameList<T>(IReadOnlyList<T>? a, IReadOnlyList<T>? b, Func<T, T, bool> same)
    {
        if (a is null || b is null)
            return a is null && b is null;
        if (a.Count != b.Count)
            return false;
        for (var i = 0; i < a.Count; i++)
        {
            if (!same(a[i], b[i]))
                return false;
        }
        return true;
    }

    /// <summary>A hash agreeing with <see cref="SameFilter"/>: of the filtered columns' names, in any
    /// order, since a dictionary orders nothing.</summary>
    internal static void AddFilter(ref HashCode hash, GridFilter? filter)
    {
        if (filter is null)
        {
            hash.Add(-1);
            return;
        }
        var names = 0;
        foreach (var name in filter.Columns.Keys)
            names ^= StringComparer.Ordinal.GetHashCode(name);
        hash.Add(filter.Columns.Count);
        hash.Add(names);
    }

    private static bool SameSpec(FilterSpec? a, FilterSpec? b)
    {
        if (a is null || b is null)
            return a is null && b is null;
        return a.Combinator == b.Combinator && SameList(a.Clauses, b.Clauses, SameClause);
    }

    private static bool SameClause(FilterClause? a, FilterClause? b)
    {
        if (a is null || b is null)
            return a is null && b is null;
        return a.Operator == b.Operator && SameOperand(a.Value, b.Value) && SameList(a.Values, b.Values, SameOperand);
    }

    // The engine decides by an operand's type (ADR-0023): a decimal and a double of one value, or a
    // DateTime and a DateOnly of one day, are different conditions.
    private static bool SameOperand(object? a, object? b)
        => a is null || b is null ? a is null && b is null : a.GetType() == b.GetType() && a.Equals(b);
}
