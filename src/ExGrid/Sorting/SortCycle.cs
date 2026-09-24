namespace ExGrid;

/// <summary>
/// What one header click does to the Sorts list (ADR-0012: clicking a column header
/// sorts). Pure, so the whole gesture is testable without a browser, and the component
/// has nothing to re-derive.
///
/// <para>The cycle is unsorted → ascending → descending → unsorted, and a click
/// replaces the whole list with at most that one column: the single-click gesture is
/// the frequent, single-column operation ADR-0012 gave the click to, and the
/// multi-column <c>Sorts</c> list stays reachable through the model for a Consumer or
/// a column menu that wants it (ADR-0010). The third state exists so the Consumer's
/// own order is reachable again without reloading.</para>
/// </summary>
public static class SortCycle
{
    /// <summary>The whole next Sorts list after a click on <paramref name="column"/>'s
    /// header: a single ascending sort on it becomes descending, a single descending sort
    /// becomes unsorted (an empty list), and any other state — unsorted, another column,
    /// a multi-column list — becomes ascending on that column alone.</summary>
    public static IReadOnlyList<SortSpec> Next(IReadOnlyList<SortSpec> current, string column)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentException.ThrowIfNullOrEmpty(column);

        // Only a single-column sort on this same column advances the cycle. Any other
        // state — unsorted, another column, a multi-column list a Consumer set up —
        // starts over at ascending on the clicked column: the click means "sort by
        // this", not "amend what was there".
        if (current is [{ } only] && only.Column == column)
        {
            return only.Direction == SortDirection.Ascending
                ? [new SortSpec(column, SortDirection.Descending)]
                : [];
        }
        return [new SortSpec(column, SortDirection.Ascending)];
    }
}
