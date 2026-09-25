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
public sealed record GridQuery(RowRange Range, IReadOnlyList<SortSpec> Sorts, GridFilter? Filter);
