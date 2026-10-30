namespace ExGrid.Cells;

/// <summary>
/// A grid's place in a Pointing Scope (ADR-0058, ADR-0430): a Formula edited in one of the Scope's Sheets points at
/// this grid's cells, and a press on them is that Sheet's, not the grid's. A Scope makes one for each grid it
/// registers (<c>PointingScope.RegisterGrid</c>), and the Consumer passes it to the grid's <c>PointedAt</c>
/// parameter. The grid joins the Scope in the page, where pointing is decided by the Script Core's Scope: while a
/// Sheet points, the grid draws the Scope's dashes and column outlines, and a press on its rows or column headers is
/// written into that Sheet's Formula. The grid learns nothing of Formulas, Sheets or Linked Tables from it.
/// </summary>
/// <typeparam name="TRow">The grid's row type.</typeparam>
public sealed class GridPointedAt<TRow>
    where TRow : class
{
    /// <summary>A declaration standing where <paramref name="inBrowser"/> says in a Scope held in the browser.</summary>
    /// <param name="inBrowser">Where the declaration stands in the Scope.</param>
    /// <exception cref="ArgumentNullException">The place is null.</exception>
    public GridPointedAt(GridPointedInBrowser<TRow> inBrowser)
    {
        ArgumentNullException.ThrowIfNull(inBrowser);
        InBrowser = inBrowser;
    }

    /// <summary>Where this declaration stands in the Pointing Scope held in the browser (ADR-0430).</summary>
    public GridPointedInBrowser<TRow> InBrowser { get; }
}
