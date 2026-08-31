using ExGrid.Selection;

namespace ExGrid.Components;

/// <summary>
/// Where a selected rectangle is painted, written as an inline style (ADR-0008). One
/// element per range, so what this costs depends on how many rectangles there are and
/// never on how many cells they cover — which is the whole reason selection is an
/// overlay and not a class on every selected cell.
///
/// Coordinates are relative to the painted slice rather than to the content, because the
/// overlay lives inside the row Viewport: that element carries the scroll translation and
/// is therefore a stacking context, and being inside it is what lets a rectangle pass
/// <em>under</em> the Pinned Columns instead of over them. The price is that the numbers
/// move when the painted slice moves — a handful of strings per scroll, against the
/// alternative of a translucent band drifting across the pinned block.
///
/// A range spanning the pinned boundary is painted as two rectangles, for the reason the
/// rest of the horizontal axis is asymmetric (ADR-0004): a Pinned Column covers the
/// Viewport's edge rather than occupying content ahead of it, so its part of the
/// selection has to travel with it while the rest pans underneath.
///
/// It lives in the component layer for the reason <see cref="ColumnStyles"/> does: the
/// pure layer answers where a column is, and this answers how that is written down for a
/// browser.
/// </summary>
internal static class SelectionStyles
{
    /// <summary>The part of a range that pans with the content, or null when the range
    /// lies entirely under the Pinned Columns.</summary>
    public static string? Scrollable(
        SelectionRange range, ColumnGeometry columns, double rowHeightPx, int firstPaintedRow)
    {
        var first = Math.Max(range.LeftColumn, columns.PinnedCount);
        if (first > range.RightColumn)
            return null;
        return Rect(
            columns.OffsetPxOf(first),
            columns.OffsetPxOf(range.RightColumn + 1) - columns.OffsetPxOf(first),
            range,
            rowHeightPx,
            firstPaintedRow);
    }

    /// <summary>The part of a range held against the Viewport's left edge, or null when
    /// the range reaches no Pinned Column.</summary>
    public static string? Pinned(
        SelectionRange range, ColumnGeometry columns, double rowHeightPx, int firstPaintedRow)
    {
        var last = Math.Min(range.RightColumn, columns.PinnedCount - 1);
        if (range.LeftColumn > last)
            return null;
        return Rect(
            columns.OffsetPxOf(range.LeftColumn),
            columns.OffsetPxOf(last + 1) - columns.OffsetPxOf(range.LeftColumn),
            range,
            rowHeightPx,
            firstPaintedRow);
    }

    /// <summary>The Focus as a rectangle, so one cell and a block are the same
    /// arithmetic.</summary>
    public static SelectionRange CellRange(CellPosition cell) => new(cell.Row, cell.Column, 1, 1);

    private static string Rect(
        double leftPx, double widthPx, SelectionRange range, double rowHeightPx, int firstPaintedRow)
    {
        // A range reaching far above or below the Viewport is emitted whole and clipped
        // by the scroll container. Trimming it to the painted rows would cost a
        // comparison per range per frame to save nothing: it is one element either way.
        var topPx = (range.TopRow - firstPaintedRow) * rowHeightPx;
        return FormattableString.Invariant(
            $"left: {leftPx}px; top: {topPx}px; width: {widthPx}px; height: {range.RowCount * rowHeightPx}px");
    }
}
