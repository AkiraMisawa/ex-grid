using System.Globalization;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components.Web;

namespace ExGrid.Components;

// A drag across Headings (ADR-0050, item 1, and ADR-0012, both as settled on 2026-09-29). A press
// on a Column Heading or a Row Heading selects whole columns or rows as a click does, and each move
// puts the Extent on the column or row under the pointer — over the Headings or over the cells,
// only the pointer's column (row) counts. It is its own drag mode: a cell drag's moves extend by
// cell and would collapse the whole columns. On a plain ExGrid the header's click sorts, so a press
// there selects nothing until the pointer reaches another column; released where it was pressed,
// it is a click.
public partial class ExGrid<TRow>
{
    /// <summary>Which Headings a drag runs across.</summary>
    private enum HeadingAxis
    {
        None,
        Columns,
        Rows,
    }

    /// <summary>A press on a plain grid's header, until the release makes it a click or the pointer
    /// reaches another column and makes it a drag (ADR-0012, 2026-09-29): the column pressed and the
    /// modifiers the press was made with.</summary>
    private readonly record struct HeaderPress(int Column, bool Extends, bool Toggles);

    private HeadingAxis _headingDrag;
    private HeaderPress? _headerPress;

    // Where the press was, relative to the Viewport's readable box — across from its left edge,
    // down from the top of the rows — and the client position it was made at. Every later event of
    // the gesture is placed by its client delta from the press, whatever element it fired on: a
    // resize grip or a menu button in the header takes the pointer for its own gesture, and an
    // event over it reports offsets measured from itself. The resize and the reorder read ClientX
    // deltas for the same reason; nothing is measured (ADR-0021).
    private double _headingPressX;
    private double _headingPressY;
    private double _headingPressClientX;
    private double _headingPressClientY;

    // The header's own move and release handlers, splatted onto the header only while a Heading
    // drag or a header press runs — for the reason the Viewport's move handler is (ADR-0008): a
    // pointer merely crossing the header would otherwise raise an event per frame.
    private readonly Dictionary<string, object> _headerDragHandler = new();
    private Action<MouseEventArgs>? _onHeaderDragMove;
    private Action<MouseEventArgs>? _onHeaderDragUp;

    /// <summary>
    /// The leaf column a press or a click on the header names, or null: the corner where the
    /// Headings meet names none, nor does the dead space past the last column (ColumnAt clamps
    /// there, for a drag), nor a Header Group's rectangle, which is not the leaf under it
    /// (ADR-0032).
    /// </summary>
    private int? HeaderColumnAt(MouseEventArgs e)
    {
        var geometry = _columnStyles.Geometry;
        if (!double.IsFinite(e.OffsetX) || !double.IsFinite(e.OffsetY)
            || geometry.IsInLead(e.OffsetX, _scrollLeftPx)
            || e.OffsetX < 0 || e.OffsetX >= geometry.TotalWidthPx
            || geometry.ColumnAt(e.OffsetX, _scrollLeftPx) is not { } column)
        {
            return null;
        }
        if (_headerLayout.TierCount > 0
            && e.OffsetY < BandHeightPx - (_headerLayout.LeafTierSpanOf(column) * _metrics.HeaderHeightPx))
        {
            return null;
        }
        return column;
    }

    /// <summary>
    /// A press on the header, where no reorder is wired (ADR-0050, item 1; ADR-0012). Where the
    /// header selects, the press is the click: it selects at once and the drag begins, and the
    /// click that follows the release has nothing left to do. Where it sorts, the press is
    /// remembered, and the moves decide.
    /// </summary>
    private void PressHeader(MouseEventArgs e)
    {
        if (e.Button != 0 || HeaderColumnAt(e) is not { } column)
            return;
        // Pressed on the header itself: its grips, buttons and checkbox keep their presses.
        RememberHeadingPress(e, e.OffsetY - BandHeightPx);
        var extends = e.ShiftKey;
        var toggles = Toggles(e);
        if (HeaderClickSelects)
        {
            _gestureConsumedClick = true;
            BeginHeadingDrag(HeadingAxis.Columns, column, extends, toggles);
            return;
        }
        _headerPress = new HeaderPress(column, extends, toggles);
        _headingDrag = HeadingAxis.Columns;
        SetDragging(true);
        AttachHeaderDrag();
    }

    /// <summary>Whether a press on a Heading adds or takes out (ADR-0050, item 1): Ctrl, or Meta
    /// where Meta is Command, as on cells; Shift outranks it (ADR-0012).</summary>
    private bool Toggles(MouseEventArgs e) => !e.ShiftKey && (e.CtrlKey || (e.MetaKey && _metaIsPrimary));

    /// <summary>
    /// What a press on a Heading selects (ADR-0050, item 1; ADR-0052): the whole column or row with
    /// the Focus on the first one on screen, or with Shift whole columns (rows) from the Focus's to
    /// this one, the Focus staying — and then the drag, whose moves move the Extent. With Ctrl the
    /// column (row) is added as a new range, which the drag then grows; or, wholly selected, it is
    /// taken out, and no drag begins, as none begins from a cell taken out (ADR-0012).
    /// </summary>
    private void BeginHeadingDrag(HeadingAxis axis, int index, bool extends, bool toggles)
    {
        var extent = Extent;
        if (extent.RowCount <= 0 || extent.ColumnCount <= 0)
            return;
        var current = _selection.Selection;
        var columns = axis == HeadingAxis.Columns;
        if (toggles && (columns ? current.CoversColumn(index, extent) : current.CoversRow(index, extent)))
        {
            Apply(columns
                ? current.ToggleColumn(index, extent, FirstVisibleRow)
                : current.ToggleRow(index, extent, FirstVisibleColumn));
            SetDragging(false);
            return;
        }
        Apply(columns
            ? extends ? current.ExtendToColumn(index, extent, FirstVisibleRow)
                : toggles ? current.ToggleColumn(index, extent, FirstVisibleRow)
                : current.SelectColumn(index, extent, FirstVisibleRow)
            : extends ? current.ExtendToRow(index, extent, FirstVisibleColumn)
                : toggles ? current.ToggleRow(index, extent, FirstVisibleColumn)
                : current.SelectRow(index, extent, FirstVisibleColumn));
        _headingDrag = axis;
        _headerPress = null;
        SetDragging(true);
        AttachHeaderDrag();
    }

    /// <summary>Remembers where a Heading press was, the Viewport's readable box being what stays
    /// put while the content scrolls beneath it: across from the offsets the press carries, and
    /// down by <paramref name="viewportY"/>, which the caller reads from the element it was on.</summary>
    private void RememberHeadingPress(MouseEventArgs e, double viewportY)
    {
        _headingPressX = e.OffsetX - _scrollLeftPx;
        _headingPressY = viewportY;
        _headingPressClientX = e.ClientX;
        _headingPressClientY = e.ClientY;
    }

    /// <summary>Where the pointer of a Heading gesture is now, relative to the Viewport's readable
    /// box, from the event's client delta from the press.</summary>
    private (double X, double Y) HeadingPointer(MouseEventArgs e)
        => (_headingPressX + (e.ClientX - _headingPressClientX), _headingPressY + (e.ClientY - _headingPressClientY));

    /// <summary>The row the Focus lands on when a Column Heading is pressed: the first on screen,
    /// so the Viewport does not move for a press on the header (KB-9).</summary>
    private int FirstVisibleRow => _visible?.Start ?? 0;

    /// <summary>The column the Focus lands on when a Row Heading is pressed: the first on
    /// screen, where the Viewport already is (ADR-0050).</summary>
    private int FirstVisibleColumn => FirstVisibleCell()?.Column ?? 0;

    private void AttachHeaderDrag()
    {
        _headerDragHandler["onmousemove"] = _onHeaderDragMove ??= OnHeaderDragMove;
        _headerDragHandler["onmouseup"] = _onHeaderDragUp ??= OnHeaderDragUp;
    }

    /// <summary>Whatever ends a drag ends a Heading drag and a header press with it.</summary>
    private void ForgetHeadingDrag()
    {
        _headingDrag = HeadingAxis.None;
        _headerPress = null;
        _headerDragHandler.Clear();
    }

    /// <summary>
    /// A move over the header during a Heading gesture. The Column Headings are what it is over, so
    /// the pointer's column counts; a Row Heading drag learns nothing from the header band. The
    /// button's state ends the gesture, as it ends a cell drag (ADR-0008).
    /// </summary>
    private void OnHeaderDragMove(MouseEventArgs e)
    {
        // A press handed over from a grid pointed at (ADR-0058) grows into no Heading drag.
        if (_pointedDrag is not null)
        {
            if ((e.Buttons & 1) == 0)
            {
                _suppressRender = true;
                SetDragging(false);
                return;
            }
            MovePointedDrag(e);
            return;
        }
        if (!_dragging || _headingDrag == HeadingAxis.None)
        {
            _suppressRender = true;
            return;
        }
        if ((e.Buttons & 1) == 0)
        {
            _suppressRender = true;
            SetDragging(false);
            return;
        }
        if (_headingDrag != HeadingAxis.Columns)
        {
            _suppressRender = true;
            return;
        }
        MoveHeadingDrag(e);
    }

    /// <summary>
    /// The release over the header. Where the pointer came to counts as a move — a fast release on
    /// another header reaches another column without a move between — and then the gesture ends.
    /// A press released on its own column, having crossed no other, is left to its click.
    /// </summary>
    private void OnHeaderDragUp(MouseEventArgs e)
    {
        if (_pointedDrag is not null)
            MovePointedDrag(e);
        else if (_headingDrag == HeadingAxis.Columns)
            MoveHeadingDrag(e);
        _suppressRender = true;
        SetDragging(false);
    }

    /// <summary>
    /// The pointer moved during a Heading gesture, over the header or over the cells: the Extent
    /// goes to its column (row), whatever row (column) it is over (ADR-0050, item 1). A plain
    /// header's press becomes a drag when the pointer reaches another column; from then on the
    /// press never sorts, even released back over the column it started on (ADR-0012).
    /// </summary>
    private void MoveHeadingDrag(MouseEventArgs e)
    {
        var extent = Extent;
        var (x, y) = HeadingPointer(e);
        if (!double.IsFinite(x) || !double.IsFinite(y))
        {
            _suppressRender = true;
            return;
        }
        if (_headingDrag == HeadingAxis.Columns)
        {
            if (_columnStyles.Geometry.ColumnAt(x + _scrollLeftPx, _scrollLeftPx) is not { } column)
            {
                _suppressRender = true;
                return;
            }
            if (_headerPress is { } press)
            {
                if (column == press.Column)
                {
                    _suppressRender = true;
                    return;
                }
                _gestureConsumedClick = true;
                BeginHeadingDrag(HeadingAxis.Columns, press.Column, press.Extends, press.Toggles);
                if (_headingDrag == HeadingAxis.None)
                    return;
            }
            Apply(_selection.Selection.ExtendToColumn(column, extent, FirstVisibleRow));
        }
        else
        {
            // Read through the mapping, as the edge band's tick reads it (ADR-0053); RowAt clamps
            // inside the page, so a drag never turns it (ADR-0015).
            if (_geometry.RowAt(_geometry.ContentOffsetAt(_scrollTopPx) + y) is not { } row)
            {
                _suppressRender = true;
                return;
            }
            Apply(_selection.Selection.ExtendToRow(_pageStartRow + row, extent, FirstVisibleColumn));
        }
        UpdateEdgeBand(x, y);
    }

    /// <summary>
    /// An edge-band tick during a Heading drag (ADR-0008): the scroll has moved the cells under the
    /// unmoved pointer, and the Extent follows along the Heading's axis only.
    /// </summary>
    private bool ExtendHeadingDragTo(CellPosition under)
        => _headingDrag == HeadingAxis.Columns
            ? Apply(_selection.Selection.ExtendToColumn(under.Column, Extent, FirstVisibleRow))
            : Apply(_selection.Selection.ExtendToRow(under.Row, Extent, FirstVisibleColumn));

    /// <summary>
    /// The Size Tip's text (ADR-0052, 2026-09-29): the Consumer's size label for the range, while a
    /// Heading drag covers more than one column or row. Null otherwise — over one, without a label,
    /// or with a null answer — and then nothing is painted and the Name Box names the Focus.
    /// </summary>
    private string? SizeTipText()
    {
        if (!_dragging || _headingDrag == HeadingAxis.None || _headerPress is not null
            || NameBoxSizeLabel is not { } label || _selection.Selection.IsEmpty)
        {
            return null;
        }
        var range = _selection.Selection.FocusRange;
        var covered = _headingDrag == HeadingAxis.Columns ? range.ColumnCount : range.RowCount;
        return covered > 1 ? label(range) : null;
    }

    /// <summary>
    /// Where the Size Tip stands (ADR-0052, ADR-0040): at the Heading the Extent is on — beneath the
    /// Extent's Column Heading, or beside its Row Heading — inside the grid's box. Its width is the
    /// Cell Metrics' estimate of its text, written inline, so the clamp and the laid-out box are one
    /// number and nothing is measured (ADR-0027/0021).
    /// </summary>
    private string SizeTipStyle(string text)
    {
        var geometry = _columnStyles.Geometry;
        var extent = _selection.Selection.Extent;
        var width = Math.Min(_metrics.CellMetrics.For(ColumnType.Text).EstimatePx(text), Math.Max(0, _visibleWidthPx));
        double left;
        double top;
        if (_headingDrag == HeadingAxis.Columns)
        {
            left = extent.Column < geometry.PinnedCount
                ? geometry.OffsetPxOf(extent.Column)
                : geometry.OffsetPxOf(extent.Column) - _scrollLeftPx;
            top = BandHeightPx;
        }
        else
        {
            left = geometry.LeadWidthPx;
            top = BandHeightPx + _geometry.ViewportTopPxOf(extent.Row - _pageStartRow, _scrollTopPx);
            top = Math.Clamp(top, BandHeightPx, Math.Max(BandHeightPx, _visibleHeightPx - _metrics.RowHeightPx));
        }
        left = Math.Clamp(left, 0, Math.Max(0, _visibleWidthPx - width));
        return string.Create(CultureInfo.InvariantCulture,
            $"left: {left}px; top: {FormulaBarPx + top}px; width: {width}px");
    }
}
