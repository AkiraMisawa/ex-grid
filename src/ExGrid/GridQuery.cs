namespace ExGrid;

/// <summary>
/// The whole of what the grid asks a Grid Source for: which range, under which Sort,
/// under which Filter (<c>CONTEXT.md</c>, Query). Every part of it is a serialisable model
/// rather than a delegate, so it can cross to a server unchanged
/// (<see cref="GridFilter"/>, ADR-0002).
///
/// <para>What the Filter and the Sort <em>mean</em> is not negotiable per source:
/// <c>GridSource.From</c> is the reference implementation, and a server answering this
/// query is written to match it (ADR-0023).</para>
/// </summary>
/// <param name="Range">The rows wanted, read-ahead already included by the source.</param>
/// <param name="Sorts">Ordering, outermost first; empty means the source's own order.</param>
/// <param name="Filter">The filter in force, or null for none.</param>
public sealed record GridQuery(RowRange Range, IReadOnlyList<SortSpec> Sorts, GridFilter? Filter)
{
    /// <summary>
    /// Whether <paramref name="other"/> asks for the same thing: the same range, the same sort levels
    /// in the same order, and the same filter — the same columns, each with the same combinator and
    /// the same clauses in the same order, each with the same operator and operands of the same type
    /// and value, and the same Opaque Filter. It compares what the lists hold, not which lists hold
    /// it, so a Query read back from <see cref="GridViewStateJson"/> equals the one written (FN-13,
    /// ADR-0207).
    ///
    /// <para>An operand is the same when it is of the same type and equal by that type's own
    /// equality, as the filter compares it (ADR-0023): <c>1.50m</c> and <c>1.5m</c> are the same, and a
    /// <c>decimal</c> and a <c>double</c> of one value are not.</para>
    /// </summary>
    /// <param name="other">The Query to compare with.</param>
    public bool Equals(GridQuery? other)
        => other is not null
            && (ReferenceEquals(this, other)
                || (Range == other.Range
                    && FilterEquality.SameList(Sorts, other.Sorts, static (a, b) => Equals(a, b))
                    && FilterEquality.SameFilter(Filter, other.Filter)));

    /// <summary>A hash agreeing with <see cref="Equals(GridQuery?)"/>: of the range, the sort levels and
    /// the names of the filtered columns.</summary>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Range);
        if (Sorts is not null)
        {
            foreach (var sort in Sorts)
                hash.Add(sort);
        }
        FilterEquality.AddFilter(ref hash, Filter);
        return hash.ToHashCode();
    }
}
