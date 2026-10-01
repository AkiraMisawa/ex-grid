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
    /// overlays and keep theirs). The hole is geometry, so it is resolved here with the
    /// rectangle and written inline, as the polygon the stylesheet clips the tint with; the
    /// range stays one element whatever its size. It is left out while the Focus's row is not
    /// among the painted rows, where the clipped rectangle does not reach it.
    ///
    /// <para>The outline of a single range lies where Excel's does, outside the range on every
    /// side but one that something above the selection layer would cover
    /// (<see cref="OutlineSides"/>); so each layer's clip lets a row's height past the range's
    /// edges on every side but the boundary, which is more than any outline is wide.</para>
    /// </summary>
    public static string? Range(
        SelectionRange range, CellPosition focus, ColumnGeometry columns, double rowHeightPx, RowRange painted,
        bool pinnedLayer, OutlineCover cover)
    {
        if (range.CellCount == 1 && range.Contains(focus))
            return null;
        if (Whole(range, columns, rowHeightPx, painted, pinnedLayer, outsidePx: rowHeightPx) is not { } style
            || Clip(range, painted) is not { } rows || Side(range, columns, pinnedLayer) is not { } side)
            return null;
        style += OutlineSides(range.TopRow, side.First, columns, pinnedLayer, cover);

        var leftPx = columns.OffsetPxOf(range.LeftColumn);
        if (range.Contains(focus) && Side(CellRange(focus), columns, pinnedLayer) is not null
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

    /// <summary>
    /// A rectangle painted whole in one layer and clipped to that layer's side of the pinned
    /// boundary, or null when the layer holds no part of it: how a selected range is painted
    /// (<see cref="Range"/>, which adds the Focus's hole), and how a Reference Outline is
    /// (ADR-0057), so that whatever is drawn inside the edge stops at the boundary on each side
    /// with no seam. The clip lets what is drawn past the rectangle's edges show for
    /// <paramref name="outsidePx"/> on every side but the boundary: zero for what is drawn inside
    /// the edge.
    /// </summary>
    public static string? Whole(
        SelectionRange range, ColumnGeometry columns, double rowHeightPx, RowRange painted, bool pinnedLayer,
        double outsidePx = 0)
    {
        if (Side(range, columns, pinnedLayer) is null || Clip(range, painted) is not { } rows)
            return null;

        var leftPx = columns.OffsetPxOf(range.LeftColumn);
        var rightPx = columns.OffsetPxOf(range.RightColumn + 1);
        var style = Rect(leftPx, rightPx - leftPx, rows, rowHeightPx, painted.Start);
        if (Side(range, columns, !pinnedLayer) is not null)
        {
            var boundaryPx = columns.OffsetPxOf(columns.PinnedCount);
            var o = outsidePx > 0 ? FormattableString.Invariant($"{-outsidePx}px") : "0";
            style += pinnedLayer
                ? FormattableString.Invariant($"; clip-path: inset({o} {rightPx - boundaryPx}px {o} {o})")
                : FormattableString.Invariant($"; clip-path: inset({o} {o} {o} {boundaryPx - leftPx}px)");
        }
        return style;
    }

    /// <summary>
    /// The Focus as one layer paints it: its cell, cut to the layer's side, with the sides of
    /// its outline that stay inside the cell (<see cref="OutlineSides"/>); null when the layer
    /// holds no part of it.
    /// </summary>
    public static string? Focus(
        CellPosition focus, ColumnGeometry columns, double rowHeightPx, RowRange painted, bool pinnedLayer, OutlineCover cover)
    {
        var cell = CellRange(focus);
        if (Part(cell, columns, rowHeightPx, painted, pinnedLayer) is not { } style)
            return null;
        return style + OutlineSides(focus.Row, focus.Column, columns, pinnedLayer, cover);
    }

    /// <summary>
    /// What lies above the selection layer at its edges at the moment (ADR-0008, 2026-10-01):
    /// <paramref name="FirstOpenRow"/> is the first row whose top edge lies below the top of the
    /// readable area, so every row before it has its top under the header (or above the
    /// Viewport); <paramref name="ScrollLeftPx"/> is the horizontal scroll offset, which takes a
    /// scrollable column's left edge under the Row Headings or the pinned block.
    /// </summary>
    internal readonly record struct OutlineCover(int FirstOpenRow, double ScrollLeftPx);

    // The sides of the Selection's outline that stay inside the range, as the stylesheet reads
    // them (ex-grid.css): written once, and none when every side lies outside, as Excel's does.
    private const string InsideTop = "; --ex-outline-in-t: 1";
    private const string InsideLeft = "; --ex-outline-in-l: 1";
    private const string InsideTopAndLeft = "; --ex-outline-in-t: 1; --ex-outline-in-l: 1";

    // A pixel's hundredth: a row's top edge or a column's left edge this close to what covers it
    // is flush with it.
    private const double FlushPx = 0.01;

    /// <summary>
    /// The sides of the Selection's outline that stay inside the range (ADR-0008, 2026-10-01).
    /// Excel draws it on the gridline and one pixel outside the range, so it covers a Border on
    /// every outer edge, and so does the stylesheet, except on a side whose outer pixels
    /// something above the selection layer would cover: a top edge at or above the readable
    /// area's top, under the header; a left edge at or left of the Row Headings' edge, or of the
    /// pinned block's for a scrollable column. There the side stays inside, as it was, so its
    /// width stays the others' (UX-18). The bottom and right sides lie on the range's own last
    /// pixels and one past, where nothing covers them.
    /// </summary>
    private static string OutlineSides(int topRow, int firstColumn, ColumnGeometry columns, bool pinnedLayer, OutlineCover cover)
    {
        var top = topRow < cover.FirstOpenRow;
        var left = pinnedLayer
            ? columns.OffsetPxOf(firstColumn) <= columns.LeadWidthPx + FlushPx
            : columns.OffsetPxOf(firstColumn) - cover.ScrollLeftPx <= columns.PinnedWidthPx + FlushPx;
        return (top, left) switch
        {
            (true, true) => InsideTopAndLeft,
            (true, false) => InsideTop,
            (false, true) => InsideLeft,
            _ => "",
        };
    }

    /// <summary>The part of a range that pans with the content, or null when the range
    /// lies entirely under the Pinned Columns.</summary>
    public static string? Scrollable(
        SelectionRange range, ColumnGeometry columns, double rowHeightPx, RowRange painted)
        => Part(range, columns, rowHeightPx, painted, pinnedLayer: false);

    /// <summary>The part of a range held against the Viewport's left edge, or null when
    /// the range reaches no Pinned Column.</summary>
    public static string? Pinned(
        SelectionRange range, ColumnGeometry columns, double rowHeightPx, RowRange painted)
        => Part(range, columns, rowHeightPx, painted, pinnedLayer: true);

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
        if (Side(CellRange(new CellPosition(range.BottomRow, range.RightColumn)), columns, pinnedLayer) is null)
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

    /// <summary>A rectangle's part on one side of the pinned boundary, cut at it: what the
    /// bands, the Focus and the other outlines paint in each layer.</summary>
    private static string? Part(
        SelectionRange range, ColumnGeometry columns, double rowHeightPx, RowRange painted, bool pinnedLayer)
    {
        if (Side(range, columns, pinnedLayer) is not { } side || Clip(range, painted) is not { } rows)
            return null;
        return Rect(
            columns.OffsetPxOf(side.First),
            columns.OffsetPxOf(side.Last + 1) - columns.OffsetPxOf(side.First),
            rows,
            rowHeightPx,
            painted.Start);
    }

    /// <summary>The columns of a rectangle on one side of the pinned boundary — the Pinned
    /// Columns' side or the scrollable one — or null when it has none there. The one place
    /// the split is decided, for a range painted whole and clipped as for a part cut at the
    /// boundary.</summary>
    private static (int First, int Last)? Side(SelectionRange range, ColumnGeometry columns, bool pinnedLayer)
    {
        var first = pinnedLayer ? range.LeftColumn : Math.Max(range.LeftColumn, columns.PinnedCount);
        var last = pinnedLayer ? Math.Min(range.RightColumn, columns.PinnedCount - 1) : range.RightColumn;
        return first > last ? null : (first, last);
    }

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
