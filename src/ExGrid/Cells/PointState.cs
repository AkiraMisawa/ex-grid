namespace ExGrid.Cells;

/// <summary>
/// Where an open edit stands with respect to Point (ADR-0051), as the grid tells its Consumer
/// (ADR-0058): whether a Reference can be written at the edit's caret now, and whether what Point
/// wrote there was handed to the grid from outside it. A Consumer that points from other instances —
/// ExSheet's Pointing Scope — declares them pointed at while the edit is in Point. The grid knows no
/// Formula: whether a Reference can go at the caret is the Consumer's own <c>PointAt</c>.
/// </summary>
public enum PointState
{
    /// <summary>No edit is open, or the Consumer's <c>PointAt</c> says no Reference can go at its
    /// caret and nothing Point wrote stands before it — after <c>)</c>, say, or while the caret is
    /// not known.</summary>
    None,

    /// <summary>The edit is in Point: <c>PointAt</c> says a Reference can go at the caret, or a
    /// Reference Point wrote over this grid's own cells stands there over unchanged text.</summary>
    InPoint,

    /// <summary>The edit is in Point, and what stands at the caret, over unchanged text, is text
    /// written through <c>WritePointedTextAsync</c>: a press outside the grid pointed. A further press,
    /// on this grid or outside it, replaces it, as Point replaces what it wrote.</summary>
    WrittenFromOutside,
}
