using ExGrid.Cells;
using ExGrid.Columns;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace ExGrid.Components;

// Pointed at from outside (ADR-0058): while a Consumer declares it, a press on the rows or the column
// headers does not act. Its default is suppressed, so DOM focus stays where it was; the Selection and
// the Focus stay put; no sort, menu, reorder, resize or Heading drag runs; and the press is handed to
// the Consumer as what it landed on. Where the declaration names the root of the grid that points,
// the press keeps its place among that grid's keys: it is handed over once the keys typed there
// before it have been handed on, and that grid holds the keys typed after it until it has been
// answered (ADR-0058, "On a circuit"). The grid also draws the dashes and the column outlines the
// declaration asks for, and answers it where one step from a cell or a column lands and scrolls a
// cell into view, or a column across only, for arrow keys pressed elsewhere (ADR-0058, "The
// keyboard"; DC-55). What a press means is the Consumer's: the grid knows no Formula.
public partial class ExGrid<TRow> : IPointedAtGrid<TRow>
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
    /// <para>A grid with an open edit of its own is not pointed at, whatever the declaration says
    /// (ADR-0058, ADR-0018 section 7): a press on it goes to its edit. Null — the default — changes
    /// nothing (DC-1, DC-52).</para>
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

    // A column to reveal across only, in the Focus's place (StageReveal): the one arrow keys pressed
    // elsewhere reached from another column (ADR-0058, 2026-10-01; DC-55). Set alongside _revealFocus,
    // and cleared with it. Of a cell and a column asked for before the render, the later is revealed.
    private int? _revealColumnAcross;

    // Whether the last press on the rows or the header was handed over: the click, the double click
    // and the context menu that follow it are the same gesture, and keep no meaning of their own.
    private bool _pressHandedOver;

    // A press handed over while its button is still down: where it was, and whether on a header. A
    // move onto another cell (column) hands the drag over once, and the drag ends there.
    private PointedDrag? _pointedDrag;

    /// <summary>A press handed over whose button is still down.</summary>
    private readonly record struct PointedDrag(CellPosition From, bool FromHeader);

    // A press handed on keeps its place among the keys of the grid that points (ADR-0058, "On a
    // circuit"; ADR-0021's note of 2026-09-30). The script tells that grid's root of each primary
    // press it passes on while this grid is pointed at, and tells this core first, in the same
    // browser task and ahead of the press itself: the press, numbered, and whether it is in turn
    // already — nothing held before it there — and later, if not, that it now is. So the press a
    // pointed handler hears next is the one announced last. The announced press no press has
    // claimed yet; the presses told of and not yet in turn, by their number; and the hand-over of
    // the last press on a pointed handler, which a drag from it is handed over behind.
    private HandedOnPress? _pressAnnounced;
    private readonly Dictionary<int, HandedOnPress> _pressesOutOfTurn = [];
    private Task _pointedPressHandOver = Task.CompletedTask;

    /// <summary>A press the script handed on to the grid that points: whether its turn has come
    /// there, and whether this core has answered it.</summary>
    private sealed class HandedOnPress(int number)
    {
        public int Number { get; } = number;

        public TaskCompletionSource InTurn { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Answered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>
    /// The id of this grid's root element, unique on the page. A Consumer that points from this grid
    /// at another one names this root in that grid's declaration
    /// (<see cref="GridPointedAt{TRow}.PointingRootId"/>), so that a press there keeps its place
    /// among the keys typed here (ADR-0058, "On a circuit").
    /// </summary>
    public string RootId => _idPrefix + "root";

    /// <summary>The root the render names while the grid is pointed at (ADR-0058): the one the
    /// script tells of each press handed over. Null otherwise, and the attribute is not written.</summary>
    private string? PointedFrom => PointedAtNow ? PointedAt!.PointingRootId : null;

    /// <summary>Whether the grid is pointed at now (ADR-0058): declared so, and holding no open edit
    /// of its own, which a press goes to instead.</summary>
    private bool PointedAtNow => PointedAt is { IsPointedAt: true } && _editMode == EditMode.None;

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

    /// <summary>Listens to the declaration passed, and answers for it (DC-55); stops listening to one
    /// no longer passed.</summary>
    private void BindPointedAt()
    {
        if (ReferenceEquals(_boundPointedAt, PointedAt))
            return;
        UnbindPointedAt();
        _boundPointedAt = PointedAt;
        if (_boundPointedAt is not null)
        {
            _boundPointedAt.Changed += _onPointedAtChanged ??= OnPointedAtChanged;
            _boundPointedAt.Grid = this;
        }
    }

    /// <summary>Stops listening, as the grid is disposed or given another declaration, and stops
    /// answering for the declaration.</summary>
    private void UnbindPointedAt()
    {
        if (_boundPointedAt is not null)
        {
            _boundPointedAt.Changed -= _onPointedAtChanged;
            if (ReferenceEquals(_boundPointedAt.Grid, this))
                _boundPointedAt.Grid = null;
        }
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

    private Task OnPointedRowsPress(MouseEventArgs e)
    {
        e = AsTaken(e, "mousedown");
        var handedOn = ClaimHandedOnPress(e);
        _pointedPressHandOver = HandOverRowsPressAsync(e, handedOn);
        return _pressAnswer = AnswerHandedOnAsync(_pointedPressHandOver, handedOn);
    }

    private Task OnPointedHeaderPress(MouseEventArgs e)
    {
        var handedOn = ClaimHandedOnPress(e);
        _pointedPressHandOver = HandOverHeaderPressAsync(e, handedOn);
        return _pressAnswer = AnswerHandedOnAsync(_pointedPressHandOver, handedOn);
    }

    /// <summary>
    /// A press the grid's script has handed on to the root of the grid that points (ADR-0058, "On a
    /// circuit"; ADR-0021's note of 2026-09-30), told ahead of the press itself: the next press on the
    /// rows or the header is handed over only once it is in turn there — once that grid has handed on
    /// the keys typed before it — and the task completes once it has been answered, which is how long
    /// that grid holds the keys typed after it. A press is answered once the task
    /// <see cref="GridPointedAt{TRow}.OnPress"/> returned for it has completed, or at once if it
    /// handed nothing over (a press past the last column, say).
    ///
    /// <para>Called by the grid's own script module and not for Consumers: it is public
    /// only because JavaScript interop requires it.</para>
    /// </summary>
    /// <param name="press">The script's number for the press, which <see cref="PressInTurn"/>
    /// names.</param>
    /// <param name="inTurn">Whether the press is in turn already: the grid that points held no keys
    /// before it.</param>
    /// <returns>Completes once the press has been answered.</returns>
    [JSInvokable]
    public Task PressHandedOnAsync(int press, bool inTurn)
    {
        if (_disposed)
            return Task.CompletedTask;
        // Told of and never heard (no press should be): answered now, so that grid's keys go on.
        ReleaseHandedOnPress(_pressAnnounced);
        var handedOn = new HandedOnPress(press);
        if (inTurn)
            handedOn.InTurn.TrySetResult();
        else
            _pressesOutOfTurn[press] = handedOn;
        _pressAnnounced = handedOn;
        return handedOn.Answered.Task;
    }

    /// <summary>
    /// A press the grid's script handed on is in turn now (ADR-0058, "On a circuit"): the grid that
    /// points has handed on the keys typed before it, and the press may be handed over.
    ///
    /// <para>Called by the grid's own script module and not for Consumers: it is public
    /// only because JavaScript interop requires it.</para>
    /// </summary>
    /// <param name="press">The number <see cref="PressHandedOnAsync"/> was told.</param>
    [JSInvokable]
    public void PressInTurn(int press)
    {
        if (_pressesOutOfTurn.Remove(press, out var handedOn))
            handedOn.InTurn.TrySetResult();
    }

    /// <summary>The press announced last, taken by the primary press a pointed handler hears; null
    /// when none was announced — the press is then handed over at once.</summary>
    private HandedOnPress? ClaimHandedOnPress(MouseEventArgs e)
    {
        if (e.Button != 0)
            return null;
        var handedOn = _pressAnnounced;
        _pressAnnounced = null;
        return handedOn;
    }

    /// <summary>A press handed on is answered once its hand-over has run, however it ended, and once
    /// the render it asked for has gone out.</summary>
    private static async Task AnswerHandedOnAsync(Task handOver, HandedOnPress? handedOn)
    {
        try
        {
            await handOver;
            // A press in turn at once is handed over inside Blazor's dispatch of it, and the render of
            // what its Consumer wrote goes out only as that dispatch returns: answered from inside it,
            // the answer reached the browser first, and the key held behind the press was typed into
            // the text the write had not yet reached (`*` lost on the Server host at 150 ms). Yielding
            // leaves the dispatch, so the answer follows the render.
            if (handedOn is not null)
                await Task.Yield();
        }
        finally
        {
            handedOn?.Answered.TrySetResult();
        }
    }

    /// <summary>Answers a press handed on without waiting for its turn any longer.</summary>
    private void ReleaseHandedOnPress(HandedOnPress? handedOn)
    {
        if (handedOn is null)
            return;
        _pressesOutOfTurn.Remove(handedOn.Number);
        handedOn.InTurn.TrySetResult();
        handedOn.Answered.TrySetResult();
    }

    /// <summary>As the grid is disposed: every press handed on and still waiting is let go, so the
    /// grid that points holds no keys for it.</summary>
    private void ReleaseHandedOnPresses()
    {
        ReleaseHandedOnPress(_pressAnnounced);
        _pressAnnounced = null;
        foreach (var handedOn in _pressesOutOfTurn.Values.ToArray())
            handedOn.InTurn.TrySetResult();
        _pressesOutOfTurn.Clear();
    }

    /// <summary>Waits, before a press is handed over, until it is in turn among the keys of the
    /// grid that points; false when the grid was disposed meanwhile.</summary>
    private async Task<bool> InTurnAsync(HandedOnPress? handedOn)
    {
        if (handedOn is null)
            return true;
        await handedOn.InTurn.Task;
        return !_disposed;
    }

    /// <summary>
    /// A press on the rows while the grid is pointed at (ADR-0058): handed over as the cell it landed
    /// on, or as more than one cell with Shift or on a Row Heading. The Selection and the Focus do not
    /// move, and no edit opens or commits. A press on a cell is followed while its button is down, so
    /// that a drag onto another cell can be handed over too. A press handed on to the grid that points
    /// is handed over once it is in turn there.
    /// </summary>
    private async Task HandOverRowsPressAsync(MouseEventArgs e, HandedOnPress? handedOn)
    {
        _pressHandedOver = true;
        var dragging = _dragging;
        SetDragging(false);
        // Nothing painted changes for a press handed over, unless a drag ended or begins.
        _suppressRender = !dragging;
        if (e.Button != 0 || PointedAt is not { } pointedAt || !double.IsFinite(e.OffsetX) || !double.IsFinite(e.OffsetY))
            return;
        var geometry = ColumnsOf(e);
        GridPointedPress<TRow> press;
        if (ShowsRowHeadings && geometry.IsInLead(e.OffsetX, ScrollLeftOf(e)))
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
        if (await InTurnAsync(handedOn))
            await pointedAt.OnPress(press);
    }

    /// <summary>
    /// A press on the header while the grid is pointed at (ADR-0058): handed over as the column whose
    /// header it landed on, as several columns with Shift, as a Header Group's rectangle, or as more
    /// than one cell on the corner where the Headings meet. Nothing is sorted, selected, grabbed or
    /// opened. A press on a column's header is followed while its button is down, so that a drag onto
    /// another column can be handed over too. A press handed on to the grid that points is handed over
    /// once it is in turn there.
    /// </summary>
    private async Task HandOverHeaderPressAsync(MouseEventArgs e, HandedOnPress? handedOn)
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
        if (await InTurnAsync(handedOn))
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
            drag.FromHeader ? GridPointedPressKind.SeveralColumns : GridPointedPressKind.SeveralCells, Dragged: true));
        return true;
    }

    /// <summary>A drag handed over from a move: nothing awaits it, so a failure in the Consumer's
    /// answer is reported through the renderer rather than left unobserved. It is handed over after
    /// the press it began with, which may still be waiting for its turn among the keys of the grid
    /// that points: the Consumer takes back what that press wrote.</summary>
    private async Task HandOverDragAsync(GridPointedPress<TRow> press)
    {
        if (PointedAt is not { } pointedAt)
            return;
        try
        {
            // The press's own failure is reported on its own path.
            await _pointedPressHandOver.ContinueWith(static _ => { }, CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            if (_disposed)
                return;
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

    /// <inheritdoc />
    async Task<GridPointedStep<TRow>> IPointedAtGrid<TRow>.StepAsync(
        Func<TRow, bool> isRow, string column, GridDirection direction, Func<string, bool> isColumn)
    {
        // On the renderer's own context: the request comes from another instance's key, and must not
        // race this grid's render.
        var step = new GridPointedStep<TRow>(GridPointedStepKind.NotHeld);
        await InvokeAsync(() => step = PointedStep(isRow, column, direction, isColumn));
        return step;
    }

    /// <inheritdoc />
    async Task<GridPointedStep<TRow>> IPointedAtGrid<TRow>.StepFromColumnAsync(string column, GridDirection direction, Func<string, bool> isColumn)
    {
        var step = new GridPointedStep<TRow>(GridPointedStepKind.NotHeld);
        await InvokeAsync(() => step = PointedStepFromColumn(column, direction, isColumn));
        return step;
    }

    /// <inheritdoc />
    async Task<bool> IPointedAtGrid<TRow>.RevealAsync(Func<TRow, bool> isRow, string column)
    {
        var revealed = false;
        await InvokeAsync(() => revealed = RevealPointedCell(isRow, column));
        return revealed;
    }

    /// <inheritdoc />
    async Task<bool> IPointedAtGrid<TRow>.RevealColumnAsync(string column)
    {
        var revealed = false;
        await InvokeAsync(() => revealed = RevealPointedColumn(column));
        return revealed;
    }

    /// <summary>
    /// The cell one step from the named one (ADR-0058, "The keyboard"; DC-55): a row up or down in
    /// the current order, in the same column, or the nearest column the Consumer names left or right,
    /// in the same row. The Selection, the Focus and the scroll do not move.
    /// </summary>
    private GridPointedStep<TRow> PointedStep(Func<TRow, bool> isRow, string column, GridDirection direction, Func<string, bool> isColumn)
    {
        if (_disposed || !PointedAtNow || ColumnNamed(column) is not { } from || PointedRowOf(isRow) is not { } row)
            return new GridPointedStep<TRow>(GridPointedStepKind.NotHeld);
        if (direction is GridDirection.Up or GridDirection.Down)
        {
            var next = direction == GridDirection.Up ? row - 1 : row + 1;
            if (next < 0 || next >= TotalRows)
                return new GridPointedStep<TRow>(GridPointedStepKind.Edge);
            return WindowRowAt(next) is { } data
                ? new GridPointedStep<TRow>(GridPointedStepKind.Cell, data, column)
                : new GridPointedStep<TRow>(GridPointedStepKind.RowNotArrived, Column: column);
        }
        return NearestColumn(from, direction, isColumn) is { } reached
            ? new GridPointedStep<TRow>(GridPointedStepKind.Cell, WindowRowAt(row), reached)
            : new GridPointedStep<TRow>(GridPointedStepKind.Edge);
    }

    /// <summary>
    /// What one step from a whole column reaches (ADR-0058, "The keyboard", as Part B of the ninth
    /// Windows run settled it; DC-55): down, the column's first row in the current order; left or
    /// right, the nearest column the Consumer names, as a column; up, nothing. The Selection, the Focus
    /// and the scroll do not move.
    /// </summary>
    private GridPointedStep<TRow> PointedStepFromColumn(string column, GridDirection direction, Func<string, bool> isColumn)
    {
        if (_disposed || !PointedAtNow || ColumnNamed(column) is not { } from)
            return new GridPointedStep<TRow>(GridPointedStepKind.NotHeld);
        switch (direction)
        {
            case GridDirection.Up:
                return new GridPointedStep<TRow>(GridPointedStepKind.Edge);
            case GridDirection.Down:
                if (TotalRows == 0)
                    return new GridPointedStep<TRow>(GridPointedStepKind.Edge);
                return WindowRowAt(0) is { } first
                    ? new GridPointedStep<TRow>(GridPointedStepKind.Cell, first, column)
                    : new GridPointedStep<TRow>(GridPointedStepKind.RowNotArrived, Column: column);
            default:
                return NearestColumn(from, direction, isColumn) is { } reached
                    ? new GridPointedStep<TRow>(GridPointedStepKind.Column, Column: reached)
                    : new GridPointedStep<TRow>(GridPointedStepKind.Edge);
        }
    }

    /// <summary>The name of the nearest column left or right of <paramref name="from"/> that
    /// <paramref name="isColumn"/> answers true for, passing over the others; null with none that
    /// way.</summary>
    private string? NearestColumn(int from, GridDirection direction, Func<string, bool> isColumn)
    {
        var step = direction == GridDirection.Left ? -1 : 1;
        for (var c = from + step; c >= 0 && c < Columns.Count; c += step)
        {
            if (isColumn(Columns[c].Name))
                return Columns[c].Name;
        }
        return null;
    }

    /// <summary>
    /// Scrolls the named cell into view, as Point's outline is kept in view (ADR-0051, ADR-0058;
    /// DC-55): the page turns where one is paged, and the reveal runs at the top of the render, as
    /// every reveal does. The Selection and the Focus do not move.
    /// </summary>
    /// <returns>Whether the grid holds the cell.</returns>
    private bool RevealPointedCell(Func<TRow, bool> isRow, string column)
    {
        if (_disposed || ColumnNamed(column) is not { } c || PointedRowOf(isRow) is not { } row)
            return false;
        var cell = new CellPosition(row, c);
        _revealTarget = new ExtentReveal(cell, new SelectionRange(row, c, 1, 1));
        _revealColumnAcross = null;
        if (PageSize is { } pageSize && row / pageSize != _pageIndex)
        {
            _pageIndex = row / pageSize;
            PrepareRender();
        }
        _revealFocus = true;
        // Not a UI event of this grid's: an armed suppression would swallow this render.
        _suppressRender = false;
        StateHasChanged();
        return true;
    }

    /// <summary>
    /// Scrolls the named column into view across only (ADR-0058, 2026-10-01; DC-55): its body is what
    /// is dashed, and no row of it is the one pointed at, so the rows stay where they are. The reveal
    /// runs at the top of the render, as every reveal does. The Selection and the Focus do not move.
    /// </summary>
    /// <returns>Whether the grid shows the column.</returns>
    private bool RevealPointedColumn(string column)
    {
        if (_disposed || ColumnNamed(column) is not { } c)
            return false;
        _revealColumnAcross = c;
        _revealTarget = null;
        _revealFocus = true;
        // Not a UI event of this grid's: an armed suppression would swallow this render.
        _suppressRender = false;
        StateHasChanged();
        return true;
    }

    /// <summary>The position, in the whole result, of the row <paramref name="isRow"/> names: the
    /// painted rows are asked first, since the cell pointed at was pressed or revealed there, and
    /// then the rest of the Window. Null when the grid holds no such row.</summary>
    private int? PointedRowOf(Func<TRow, bool> isRow)
    {
        if (_visible is { } painted)
        {
            for (var row = painted.Start; row < painted.Start + painted.Count; row++)
            {
                if (WindowRowAt(row) is { } data && isRow(data))
                    return row;
            }
        }
        for (var slice = 0; slice < _window.Count; slice++)
        {
            if (isRow(_window[slice]))
                return _windowStart + slice;
        }
        return null;
    }

    /// <summary>The index of the column of that name, or null for a name the grid does not show.</summary>
    private int? ColumnNamed(string column)
    {
        for (var c = 0; c < Columns.Count; c++)
        {
            if (string.Equals(Columns[c].Name, column, StringComparison.Ordinal))
                return c;
        }
        return null;
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
        if (ColumnNamed(dashes.Column) is not { } column)
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
