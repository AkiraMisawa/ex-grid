namespace ExGrid;

/// <summary>
/// What every Grid Source has to do with a Filter it is handed: compare it structurally,
/// and keep a copy of its own.
///
/// <para>Both are needed for the same reason. <see cref="GridFilter"/>'s record equality
/// compares its collections <em>by reference</em>, and those read-only interfaces are
/// typically backed by the Consumer's own live <c>Dictionary</c> and <c>List</c>. Compared
/// by reference, a Consumer that mutates and re-hands the same object looks unchanged;
/// stored by reference, a mutation after the fact silently changes a query that has
/// already run (ADR-0023).</para>
/// </summary>
internal static class GridFilters
{
    /// <summary>A copy nothing outside can reach into. The Opaque Filter stays by
    /// reference: only the Consumer understands it, and it is passed straight through
    /// (ADR-0023).</summary>
    public static GridFilter? Snapshot(GridFilter? filter)
    {
        if (filter is null)
            return null;

        var columns = new Dictionary<string, FilterSpec>(filter.Columns.Count, StringComparer.Ordinal);
        foreach (var (name, spec) in filter.Columns)
        {
            var clauses = new FilterClause[spec.Clauses.Count];
            for (var i = 0; i < clauses.Length; i++)
            {
                var clause = spec.Clauses[i];
                clauses[i] = clause.Values is null ? clause : clause with { Values = clause.Values.ToArray() };
            }
            columns[name] = new FilterSpec(clauses, spec.Combinator);
        }

        return filter with { Columns = columns };
    }

    public static bool Equal(GridFilter? a, GridFilter? b)
    {
        if (ReferenceEquals(a, b))
            return true;
        if (a is null || b is null)
            return false;
        if (!Equals(a.Opaque, b.Opaque) || a.Columns.Count != b.Columns.Count)
            return false;
        foreach (var (name, spec) in a.Columns)
        {
            if (!b.Columns.TryGetValue(name, out var other))
                return false;
            if (spec.Combinator != other.Combinator || spec.Clauses.Count != other.Clauses.Count)
                return false;
            for (var i = 0; i < spec.Clauses.Count; i++)
            {
                var x = spec.Clauses[i];
                var y = other.Clauses[i];
                if (x.Operator != y.Operator || !Equals(x.Value, y.Value))
                    return false;
                if ((x.Values is null) != (y.Values is null))
                    return false;
                if (x.Values is not null && !x.Values.SequenceEqual(y.Values!))
                    return false;
            }
        }

        return true;
    }
}
