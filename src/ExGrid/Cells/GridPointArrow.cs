using ExGrid.Selection;

namespace ExGrid.Cells;

/// <summary>
/// An arrow key pressed in an open edit while what Point wrote there was written from outside the grid
/// (<see cref="PointState.WrittenFromOutside"/>; ADR-0058, "The keyboard"). The grid moves nothing for
/// it: the text stands for a cell of another instance, which this grid does not know, and the Consumer
/// that wrote it moves it there. The grid names the key by the meaning ADR-0012 gives its modifiers,
/// and decides nothing about it.
/// </summary>
/// <param name="Direction">The arrow's direction.</param>
/// <param name="Extends">Whether Shift was held: an arrow that extends a range.</param>
/// <param name="ToEdge">Whether the Primary Modifier was held: an arrow that goes to the edge of the
/// data.</param>
public sealed record GridPointArrow(GridDirection Direction, bool Extends = false, bool ToEdge = false);
