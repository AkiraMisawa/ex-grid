using ExGrid.Selection;

namespace ExGrid.Components;

/// <summary>
/// What a reader of the grid looks at: the Selection, with the Row Sequence Version its positions are
/// written in, and where the rows and columns are scrolled to. A Consumer that shows several sets of
/// rows in one grid, one at a time — ExSheet's Sheets behind their tabs (ADR-0331) — keeps each set's
/// view with <see cref="ExGrid{TRow}.ReadView"/> and hands it back with
/// <see cref="ExGrid{TRow}.ShowViewWith(GridView)"/> when that set is shown again; over the Script Core,
/// <see cref="Script.ExGrid{TRow}.ReadViewAsync"/> and <see cref="Script.ExGrid{TRow}.ShowViewWith(GridView)"/>
/// (ADR-0441).
/// </summary>
/// <param name="RowSequenceVersion">The Row Sequence Version the Selection's positions are written in.</param>
/// <param name="Selection">The Selection.</param>
/// <param name="ScrollTopPx">The scroller's vertical offset, in pixels.</param>
/// <param name="ScrollLeftPx">The scroller's horizontal offset, in pixels.</param>
public readonly record struct GridView(int RowSequenceVersion, GridSelection Selection, double ScrollTopPx, double ScrollLeftPx)
{
    /// <summary>The grid and the set of rows the view was read of (<see cref="ExGrid{TRow}.ReadView"/>),
    /// carried through <c>with</c>, so a view handed back to that grid brings the same set again; none for a
    /// view that grid did not read, which brings a set of its own. Over the Script Core the set is the number
    /// the browser keeps the view it read under (ADR-0441). Not part of the view's equality: two
    /// views of the same Selection and offsets are the same view, whoever read them.</summary>
    internal RowSetStamp? RowSet { get; init; }

    /// <summary>Whether <paramref name="other"/> has the same Row Sequence Version, Selection and offsets.</summary>
    /// <param name="other">The view to compare with.</param>
    /// <returns>Whether the two are the same view.</returns>
    public bool Equals(GridView other)
        => RowSequenceVersion == other.RowSequenceVersion && Equals(Selection, other.Selection)
           && ScrollTopPx.Equals(other.ScrollTopPx) && ScrollLeftPx.Equals(other.ScrollLeftPx);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(RowSequenceVersion, Selection, ScrollTopPx, ScrollLeftPx);
}

/// <summary>Which grid read a view, and of which of its sets of rows (<see cref="GridView.RowSet"/>).</summary>
internal sealed record RowSetStamp(object Grid, int Set)
{
    /// <summary>Over the Script Core, the view as the browser read it, which keeps it as it was read: one handed back
    /// with another Selection or other offsets is refused, not shown as read (ADR-0441).</summary>
    internal GridView? Read { get; init; }
}
