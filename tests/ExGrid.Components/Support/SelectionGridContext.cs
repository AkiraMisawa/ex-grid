using Bunit;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components.Web;

namespace ExGrid.Components.Tests.Support;

/// <summary>
/// The grid the selection suites draw on (ADR-0008/0011/0012): 100 columns of 100px in a
/// 350px Viewport, 20px rows in a 100px Viewport of which the header takes the first 20 —
/// five rows painted, and cell (r, c) pressed at (c × 100 + 50, (r − first painted row) × 20
/// + 10), as the browser would report it.
/// </summary>
public abstract class SelectionGridContext : GridTestContext
{
    private protected const double RowHeightPx = 20;
    private protected const double ViewportHeightPx = 100;
    private protected const double ViewportWidthPx = 350;

    private protected IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        int pinnedColumnCount = 0,
        int windowCount = 200,
        Action<GridSelection>? onSelectionChanged = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(windowCount))
              .Add(g => g.TotalCount, 200)
              .Add(g => g.Columns, TestRows.Wide(100))
              .Add(g => g.RowHeight, RowHeightPx)
              .Add(g => g.ViewportHeight, ViewportHeightPx)
              .Add(g => g.ViewportWidth, ViewportWidthPx)
              .Add(g => g.PinnedColumnCount, pinnedColumnCount);
            if (onSelectionChanged is not null)
                ps.Add(g => g.SelectionChanged, onSelectionChanged);
        });

    /// <summary>A press of the primary button at the Viewport's offsets (x, y).</summary>
    private protected static Task PressAsync(
        IRenderedComponent<ExGrid<TestRow>> cut, double x, double y, bool shift = false, bool ctrl = false)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs
        {
            OffsetX = x,
            OffsetY = y,
            Button = 0,
            Buttons = 1,
            ShiftKey = shift,
            CtrlKey = ctrl,
        });

    /// <summary>A press at the centre of cell (row, column), the rows counted from the top.</summary>
    private protected static Task PressCellAsync(
        IRenderedComponent<ExGrid<TestRow>> cut, int row, int column, bool shift = false, bool ctrl = false)
        => PressAsync(cut, Cell(row, column).X, Cell(row, column).Y, shift, ctrl);

    /// <summary>The centre of cell (row, column) as the browser would report it, with the
    /// row measured from the first painted one.</summary>
    private protected static (double X, double Y) Cell(int row, int column, int firstPaintedRow = 0)
        => ((column * 100) + 50, ((row - firstPaintedRow) * RowHeightPx) + 10);

    /// <summary>A rectangle's box as the overlay writes it inline.</summary>
    private protected static string Rect(double left, double top, double width, double height)
        => FormattableString.Invariant($"left: {left}px; top: {top}px; width: {width}px; height: {height}px");
}
