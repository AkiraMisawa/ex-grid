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
/// A rectangle is clipped to the painted rows, one row beyond each end (ADR-0053). A
/// whole-column selection is a million rows tall, and a browser clamps a layout length
/// under its Layout Ceiling as it clamps the spacer: painted whole at 150%, its bottom
/// edge stood 22,369,617 px down whatever the range said. Only the visible part was ever
/// needed, and the extra row on each side keeps a clipped edge out of the readable area,
/// so no border is drawn where the range does not end.
///
/// A range spanning the pinned boundary is painted as two rectangles, for the reason the
/// rest of the horizontal axis is asymmetric (ADR-0004): a Pinned Column covers the
/// Viewport's edge rather than occupying content ahead of it, so its part of the
/// selection has to travel with it while the rest pans underneath. A selected range's two
/// rectangles are each the whole range clipped to its own side (<see cref="Range"/>); the
/// bands and outlines that are not a range are cut at the boundary instead.
///
/// It lives in the component layer for the reason <see cref="ColumnStyles"/> does: the
/// pure layer answers where a column is, and this answers how that is written down for a
/// browser.
/// </summary>
internal static class SelectionStyles
{
    /// <summary>
    /// A selected range as one layer paints it (ADR-0008, "Excel's look for the Focus and a
    /// single range"), or null when the layer holds no part of it — and null for a range that
    /// is the Focus's cell alone, which is not tinted at all: the Focus outline marks it.
    ///
    /// A range that spans the pinned boundary is painted <em>whole</em> in both layers, each
    /// clipped to its own side of the boundary. Whatever is drawn inside the range's edge —
    /// the outline of a single range, the forced-colors outline of every range — then stops
    /// at the boundary on each side instead of being drawn along it, so the two parts meet
    /// with no seam; and the scrollable part keeps its box while it slides beneath the
    /// pinned block. Nothing here needs to know how wide an outline is, only that a column
    /// is wider than one.
    ///
    /// A range holding the <paramref name="focus"/> carries a hole where the Focus is, in the
    /// layer the Focus's cell belongs to: the selection's tint never covers the Focus cell, as
    /// Excel's never covers the active cell (the Focus band and the hover band are other
    /// overlays and keep theirs). The hole is
    /// geometry, so it is resolved here with the rectangle and written inline, as the
    /// polygon the stylesheet clips the tint with; the range stays one element whatever its
    /// size. It is left out while the Focus's row is not among the painted rows, where the
    /// clipped rectangle does not reach it.
    /// </summary>
    public static string? Range(
        SelectionRange range, CellPosition focus, ColumnGeometry columns, double rowHeightPx, RowRange painted,
        bool pinnedLayer)
    {
        if (range.CellCount == 1 && range.Contains(focus))
            return null;
        var pinned = columns.PinnedCount;
        var hasPinnedPart = range.LeftColumn < pinned;
        var hasScrollablePart = range.RightColumn >= pinned;
        if ((pinnedLayer ? !hasPinnedPart : !hasScrollablePart) || Clip(range, painted) is not { } rows)
            return null;

        var leftPx = columns.OffsetPxOf(range.LeftColumn);
        var rightPx = columns.OffsetPxOf(range.RightColumn + 1);
        var style = Rect(leftPx, rightPx - leftPx, rows, rowHeightPx, painted.Start);
        if (hasPinnedPart && hasScrollablePart)
        {
            var boundaryPx = columns.OffsetPxOf(pinned);
            style += pinnedLayer
                ? FormattableString.Invariant($"; clip-path: inset(0 {rightPx - boundaryPx}px 0 0)")
                : FormattableString.Invariant($"; clip-path: inset(0 0 0 {boundaryPx - leftPx}px)");
        }
        if (range.Contains(focus) && (focus.Column < pinned) == pinnedLayer
            && focus.Row >= rows.Top && focus.Row < rows.Top + rows.Count)
        {
            var holeLeftPx = columns.OffsetPxOf(focus.Column) - leftPx;
            var holeRightPx = columns.OffsetPxOf(focus.Column + 1) - leftPx;
            var holeTopPx = (focus.Row - rows.Top) * rowHeightPx;
            var holeBottomPx = holeTopPx + rowHeightPx;
            // evenodd: the box, then the Focus's cell inside it, is the box less the cell.
            style += FormattableString.Invariant(
                $"; --ex-range-hole: polygon(evenodd, 0 0, 100% 0, 100% 100%, 0 100%, 0 0, {holeLeftPx}px {holeTopPx}px, {holeRightPx}px {holeTopPx}px, {holeRightPx}px {holeBottomPx}px, {holeLeftPx}px {holeBottomPx}px, {holeLeftPx}px {holeTopPx}px)");
        }
        return style;
    }

    /// <summary>The part of a range that pans with the content, or null when the range
    /// lies entirely under the Pinned Columns.</summary>
    public static string? Scrollable(
        SelectionRange range, ColumnGeometry columns, double rowHeightPx, RowRange painted)
    {
        var first = Math.Max(range.LeftColumn, columns.PinnedCount);
        if (first > range.RightColumn || Clip(range, painted) is not { } rows)
            return null;
        return Rect(
            columns.OffsetPxOf(first),
            columns.OffsetPxOf(range.RightColumn + 1) - columns.OffsetPxOf(first),
            rows,
            rowHeightPx,
            painted.Start);
    }

    /// <summary>The part of a range held against the Viewport's left edge, or null when
    /// the range reaches no Pinned Column.</summary>
    public static string? Pinned(
        SelectionRange range, ColumnGeometry columns, double rowHeightPx, RowRange painted)
    {
        var last = Math.Min(range.RightColumn, columns.PinnedCount - 1);
        if (range.LeftColumn > last || Clip(range, painted) is not { } rows)
            return null;
        return Rect(
            columns.OffsetPxOf(range.LeftColumn),
            columns.OffsetPxOf(last + 1) - columns.OffsetPxOf(range.LeftColumn),
            rows,
            rowHeightPx,
            painted.Start);
    }

    /// <summary>
    /// The fill handle (ADR-0050, item 5): a square of <paramref name="sizePx"/> centred on
    /// the range's bottom-right corner, in the layer that corner belongs to — the pinned one
    /// when the range's last column is pinned, the scrollable one otherwise. Null for the
    /// other layer, so the handle is one element and never two — and null while its corner
    /// is not among the painted rows (ADR-0053), where it could not be seen or grabbed.
    /// </summary>
    public static string? Handle(
        SelectionRange range, ColumnGeometry columns, double rowHeightPx, RowRange painted,
        double sizePx, bool pinnedLayer)
    {
        if ((range.RightColumn < columns.PinnedCount) != pinnedLayer)
            return null;
        if (range.BottomRow < painted.Start - 1 || range.BottomRow > painted.Start + painted.Count)
            return null;
        var (leftPx, topPx) = HandleCornerPx(range, columns, rowHeightPx, painted.Start);
        return FormattableString.Invariant(
            $"left: {leftPx - (sizePx / 2)}px; top: {topPx - (sizePx / 2)}px; width: {sizePx}px; height: {sizePx}px");
    }

    /// <summary>The range's bottom-right corner, in the coordinates its layer paints in:
    /// the painted slice's top, and the content's (or the pinned layer's) left.</summary>
    public static (double LeftPx, double TopPx) HandleCornerPx(
        SelectionRange range, ColumnGeometry columns, double rowHeightPx, int firstPaintedRow)
        => (columns.OffsetPxOf(range.RightColumn + 1), (range.BottomRow + 1 - firstPaintedRow) * rowHeightPx);

    /// <summary>The Focus as a rectangle, so one cell and a block are the same
    /// arithmetic.</summary>
    public static SelectionRange CellRange(CellPosition cell) => new(cell.Row, cell.Column, 1, 1);

    /// <summary>The rows of a range that are painted, one row beyond each end of the
    /// painted slice, or null when it shares none of them (ADR-0053).</summary>
    private static (int Top, int Count)? Clip(SelectionRange range, RowRange painted)
    {
        var top = Math.Max(range.TopRow, painted.Start - 1);
        // Summed as long: a range or a slice at the end of the row space would overflow.
        var bottom = Math.Min((long)range.BottomRow, (long)painted.Start + painted.Count);
        return bottom < top ? null : (top, (int)(bottom - top + 1));
    }

    private static string Rect(
        double leftPx, double widthPx, (int Top, int Count) rows, double rowHeightPx, int firstPaintedRow)
    {
        var topPx = (rows.Top - firstPaintedRow) * rowHeightPx;
        return FormattableString.Invariant(
            $"left: {leftPx}px; top: {topPx}px; width: {widthPx}px; height: {rows.Count * rowHeightPx}px");
    }
}
