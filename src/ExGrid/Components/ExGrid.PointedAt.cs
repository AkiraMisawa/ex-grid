using ExGrid.Cells;
using ExGrid.Columns;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace ExGrid.Components;

// Pointed at from outside (ADR-0058): while a Consumer declares it, a press on the rows or the column
// headers does not act. Its default is suppressed, so DOM focus stays where it was; the Selection and
// the Focus stay put; no sort, menu, reorder, resize or Heading drag runs; and the press is handed to
// the Consumer as what it landed on. The grid also draws the dashes and the column outlines the
// declaration asks for. What a press means is the Consumer's: the grid knows no Formula.
public partial class ExGrid<TRow>
{
    /// <summary>
    /// A Consumer declaration (ADR-0058): that this grid is pointed at from outside — a Formula edited
    /// elsewhere on the page points at its cells — and what it draws meanwhile. While
    /// <see cref="GridPointedAt{TRow}.IsPointedAt"/> holds, a primary press on the rows or the column
    /// headers moves neither DOM focus nor the Selection and the Focus, runs no sort, column menu,
    /// reorder, resize or Heading drag, and is handed to the declaration as the pressed cell, the pressed
    /// column header, or a shape the Consumer will refuse; the root wears <c>ex-pointed-at</c>, and the
    /// pointer over the rows and headers is <c>cell</c>. Whether or not it holds, the declaration's
    /// dashes and column outlines are drawn in the selection overlay. The grid follows the declaration's
    /// changes as it is told of them.
    ///
    /// <para>A grid with an open edit of its own is never declared pointed at (ADR-0058, ADR-0018
    /// section 7), and the grid assumes so. Null — the default — changes nothing (DC-1, DC-52).</para>
    /// </summary>
    [Parameter] public GridPointedAt<TRow>? PointedAt { get; set; }

    // The declaration whose changes the grid is listening to, and the handler it listens with — a
    // field, so the same delegate that was added is the one removed.
    private GridPointedAt<TRow>? _boundPointedAt;
    private Action? _onPointedAtChanged;

    // The two meanings a press on the rows can have, and the two a press on the header can have, each
    // held in a field: the one bound is what the browser dispatches to, and a delegate rebuilt per
    // render would be a new handler for the diff every time (PF-3). Which one a press reaches is
    // decided by the render the browser showed when it was made — the same render that did, or did
    // not, suppress its default.
    private Func<MouseEventArgs, Task>? _onRowsPress;
    private Func<MouseEventArgs, Task>? _onPointedRowsPress;
    private Func<MouseEventArgs, Task>? _onHeaderPress;
    private Func<MouseEventArgs, Task>? _onPointedHeaderPress;

    // Whether the last press on the rows or the header was handed over: the click, the double click
    // and the context menu that follow it are the same gesture, and keep no meaning of their own.
    private bool _pressHandedOver;

    // A press handed over while its button is still down: where it was, and whether on a header. A
    // move onto another cell (column) hands the drag over once, and the drag ends there.
    private PointedDrag? _pointedDrag;

    /// <summary>A press handed over whose button is still down.</summary>
    private readonly record struct PointedDrag(CellPosition From, bool FromHeader);

    /// <summary>Whether the grid is pointed at now (ADR-0058).</summary>
    private bool PointedAtNow => PointedAt is { IsPointedAt: true };

    /// <summary>What a press on the rows reaches: handed over while the grid is pointed at, answered
    /// as the grid's own otherwise.</summary>
    private Func<MouseEventArgs, Task> RowsPressHandler
        => PointedAtNow ? _onPointedRowsPress ??= OnPointedRowsPress : _onRowsPress ??= OnMouseDown;

    /// <summary>What a press on the header reaches, as <see cref="RowsPressHandler"/> for the rows.</summary>
    private Func<MouseEventArgs, Task> HeaderPressHandler
        => PointedAtNow ? _onPointedHeaderPress ??= OnPointedHeaderPress : _onHeaderPress ??= OnHeaderMouseDown;

    /// <summary>Whether a press on the rows suppresses its default, so DOM focus stays where it is:
    /// while an edit that may point is open (ADR-0051), and while the grid is pointed at from outside
    /// (ADR-0058).</summary>
    private bool RowsPressKeepsFocus => PressKeepsTheEditor || PointedAtNow;

    /// <summary>Listens to the declaration passed, and stops listening to one no longer passed.</summary>
    private void BindPointedAt()
    {
        if (ReferenceEquals(_boundPointedAt, PointedAt))
            return;
        if (_boundPointedAt is not null)
            _boundPointedAt.Changed -= _onPointedAtChanged;
        _boundPointedAt = PointedAt;
        if (_boundPointedAt is not null)
            _boundPointedAt.Changed += _onPointedAtChanged ??= OnPointedAtChanged;
    }

    /// <summary>Stops listening, as the grid is disposed.</summary>
    private void UnbindPointedAt()
    {
        if (_boundPointedAt is not null)
            _boundPointedAt.Changed -= _onPointedAtChanged;
        _boundPointedAt = null;
    }

    /// <summary>The declaration changed: the grid repaints from it — its root class, what a press
    /// reaches, its dashes and its column outlines.</summary>
    private void OnPointedAtChanged()
    {
        if (_disposed)
            return;
        _ = InvokeAsync(() =>
        {
            if (_disposed)
                return;
            try
            {
                ResolveOutlinedColumns();
            }
            catch (Exception ex)
            {
                _ = DispatchExceptionAsync(ex);
                return;
            }
            // Not a UI event: an armed suppression would swallow this render.
            _suppressRender = false;
            StateHasChanged();
        });
    }

    private Task OnPointedRowsPress(MouseEventArgs e) => _pressAnswer = HandOverRowsPressAsync(e);

    private Task OnPointedHeaderPress(MouseEventArgs e) => _pressAnswer = HandOverHeaderPressAsync(e);

    /// <summary>
    /// A press on the rows while the grid is pointed at (ADR-0058): handed over as the cell it landed
    /// on, or as more than one cell with Shift or on a Row Heading. The Selection and the Focus do not
    /// move, and no edit opens or commits. A press on a cell is followed while its button is down, so
    /// that a drag onto another cell can be handed over too.
    /// </summary>
    private async Task HandOverRowsPressAsync(MouseEventArgs e)
    {
        _pressHandedOver = true;
        var dragging = _dragging;
        SetDragging(false);
        // Nothing painted changes for a press handed over, unless a drag ended or begins.
        _suppressRender = !dragging;
        if (e.Button != 0 || PointedAt is not { } pointedAt || !double.IsFinite(e.OffsetX) || !double.IsFinite(e.OffsetY))
            return;
        var geometry = _columnStyles.Geometry;
        GridPointedPress<TRow> press;
        if (ShowsRowHeadings && geometry.IsInLead(e.OffsetX, _scrollLeftPx))
        {
            if (CellUnder(e) is null)
                return;
            press = new GridPointedPress<TRow>(GridPointedPressKind.SeveralCells);
        }
        // Past the last column the rows are dead space, not the last column again: ColumnAt clamps
        // for a drag, which is the wrong answer for a press that landed on nothing.
        else if (e.OffsetX < 0 || e.OffsetX >= geometry.TotalWidthPx || CellUnder(e) is not { } cell)
        {
            return;
        }
        else if (e.ShiftKey)
        {
            press = new GridPointedPress<TRow>(GridPointedPressKind.SeveralCells);
        }
        else
        {
            press = new GridPointedPress<TRow>(GridPointedPressKind.Cell, WindowRowAt(cell.Row), Columns[cell.Column].Name);
            _pointedDrag = new PointedDrag(cell, FromHeader: false);
            SetDragging(true);
        }
        await pointedAt.OnPress(press);
    }

    /// <summary>
    /// A press on the header while the grid is pointed at (ADR-0058): handed over as the column whose
    /// header it landed on, as several columns with Shift, as a Header Group's rectangle, or as more
    /// than one cell on the corner where the Headings meet. Nothing is sorted, selected, grabbed or
    /// opened. A press on a column's header is followed while its button is down, so that a drag onto
    /// another column can be handed over too.
    /// </summary>
    private async Task HandOverHeaderPressAsync(MouseEventArgs e)
    {
        _pressHandedOver = true;
        var dragging = _dragging;
        SetDragging(false);
        _suppressRender = !dragging;
        if (e.Button != 0 || PointedAt is not { } pointedAt || !double.IsFinite(e.OffsetX) || !double.IsFinite(e.OffsetY))
            return;
        var geometry = _columnStyles.Geometry;
        GridPointedPress<TRow> press;
        if (ShowsRowHeadings && geometry.IsInLead(e.OffsetX, _scrollLeftPx))
        {
            press = new GridPointedPress<TRow>(GridPointedPressKind.SeveralCells);
        }
        else if (e.OffsetX < 0 || e.OffsetX >= geometry.TotalWidthPx || geometry.ColumnAt(e.OffsetX, _scrollLeftPx) is not { } column)
        {
            return;
        }
        else if (IsAboveLeaf(e, column, out var group))
        {
            // An empty tier above a leaf that does not stretch there is no header at all.
            if (group is null)
                return;
            press = new GridPointedPress<TRow>(GridPointedPressKind.HeaderGroup);
        }
        else if (e.ShiftKey)
        {
            press = new GridPointedPress<TRow>(GridPointedPressKind.SeveralColumns);
        }
        else
        {
            press = new GridPointedPress<TRow>(GridPointedPressKind.ColumnHeader, Column: Columns[column].Name);
            RememberHeadingPress(e, e.OffsetY - BandHeightPx);
            _pointedDrag = new PointedDrag(new CellPosition(0, column), FromHeader: true);
            SetDragging(true);
            AttachHeaderDrag();
        }
        await pointedAt.OnPress(press);
    }

    /// <summary>
    /// A move while a press handed over is held, over the rows or the header. From a cell, reaching
    /// another cell makes it a drag across cells; from a header, reaching another column makes it a
    /// drag across columns. Either is handed over once, and the drag ends there: the grid follows it
    /// no further, and nothing it passes over is selected.
    /// </summary>
    /// <returns>Whether the drag was handed over.</returns>
    private bool MovePointedDrag(MouseEventArgs e)
    {
        if (_pointedDrag is not { } drag)
            return false;
        bool moved;
        if (drag.FromHeader)
        {
            // Placed by its client delta from the press, whatever element the event fired on, as a
            // Heading drag is.
            var (x, _) = HeadingPointer(e);
            moved = double.IsFinite(x)
                && _columnStyles.Geometry.ColumnAt(x + _scrollLeftPx, _scrollLeftPx) is { } column
                && column != drag.From.Column;
        }
        else
        {
            moved = double.IsFinite(e.OffsetX) && double.IsFinite(e.OffsetY)
                && CellUnder(e) is { } cell && cell != drag.From;
        }
        if (!moved)
        {
            _suppressRender = true;
            return false;
        }
        SetDragging(false);
        _ = HandOverDragAsync(new GridPointedPress<TRow>(
            drag.FromHeader ? GridPointedPressKind.SeveralColumns : GridPointedPressKind.SeveralCells));
        return true;
    }

    /// <summary>A drag handed over from a move: nothing awaits it, so a failure in the Consumer's
    /// answer is reported through the renderer rather than left unobserved.</summary>
    private async Task HandOverDragAsync(GridPointedPress<TRow> press)
    {
        if (PointedAt is not { } pointedAt)
            return;
        try
        {
            await pointedAt.OnPress(press);
        }
        catch (Exception ex)
        {
            await DispatchExceptionAsync(ex);
        }
    }

    /// <summary>The row instance at a position in the whole result, or null while it has not arrived
    /// (a Placeholder).</summary>
    private TRow? WindowRowAt(int row)
    {
        var slice = row - _windowStart;
        return slice >= 0 && slice < _window.Count ? _window[slice] : null;
    }

    /// <summary>
    /// Whether a press on the header at <paramref name="column"/> lands in the tiers above the column's
    /// own header, where Header Groups stand (ADR-0032), and the group it lands on there — null over an
    /// empty tier above a leaf that does not stretch there.
    /// </summary>
    private bool IsAboveLeaf(MouseEventArgs e, int column, out ResolvedHeaderGroup? group)
    {
        group = null;
        if (_headerLayout.TierCount == 0)
            return false;
        var leafTopPx = BandHeightPx - (_headerLayout.LeafTierSpanOf(column) * _metrics.HeaderHeightPx);
        if (e.OffsetY >= leafTopPx)
            return false;
        var tier = _headerLayout.TierCount - (int)Math.Floor(e.OffsetY / _metrics.HeaderHeightPx);
        foreach (var candidate in _headerLayout.Groups)
        {
            if (column >= candidate.FirstColumn && column < candidate.FirstColumn + candidate.MemberCount
                && tier >= candidate.BottomTier && tier <= candidate.TopTier)
            {
                group = candidate;
                break;
            }
        }
        return true;
    }

    /// <summary>
    /// The dashes the declaration asks for (ADR-0058), as the range they are drawn over: the named
    /// column's body across all its rows, or its cell in the first painted row the declaration names.
    /// Null for none — no dashes asked, a column the grid does not show, or a row that is not painted:
    /// the grid never scrolls to show one.
    /// </summary>
    private SelectionRange? PointDashesRange(RowRange painted)
    {
        if (PointedAt?.Dashes is not { } dashes)
            return null;
        var extent = Extent;
        if (extent.RowCount <= 0)
            return null;
        var column = -1;
        for (var c = 0; c < Columns.Count; c++)
        {
            if (string.Equals(Columns[c].Name, dashes.Column, StringComparison.Ordinal))
            {
                column = c;
                break;
            }
        }
        if (column < 0)
            return null;
        if (dashes.Row is not { } isRow)
            return new SelectionRange(0, column, extent.RowCount, 1);
        for (var row = painted.Start; row < painted.Start + painted.Count; row++)
        {
            if (WindowRowAt(row) is { } data && isRow(data))
                return new SelectionRange(row, column, 1, 1);
        }
        return null;
    }
}
