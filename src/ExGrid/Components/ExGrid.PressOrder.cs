using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace ExGrid.Components;

// A press on the rows keeps its place among held keys (ADR-0021/0010, ED-22). While keys are
// held, or a change of editing mode is being answered, the listener holds a primary press on
// the rows, and its release, and replays them in order behind the keys typed before them. The
// keys typed after a press are handed on only once the core has answered it — here: the press
// may commit an open edit and wait on the Consumer hearing it, and the keys after it must be
// gated against the mode it leaves, never the one it found. A press into the Formula Bar's text
// is answered the same way: its focus opens an edit, or moves one from the cell into the bar in
// Caret (ADR-0051, ED-29), and the listener holds the keys typed into the bar until that is
// answered — by then the bar shows the text the cell was typed to (ADR-0021, 2026-10-01).
public partial class ExGrid<TRow>
{
    // The press or release on the rows, or the focus a press into the Formula Bar gave it, that
    // the core heard last, while the core is answering it.
    private Task _pressAnswer = Task.CompletedTask;

    private Task OnMouseDown(MouseEventArgs e) => _pressAnswer = AnswerPressAsync(AsTaken(e, "mousedown"));

    private Task OnMouseUp(MouseEventArgs e) => _pressAnswer = AnswerReleaseAsync(AsTaken(e, "mouseup"));

    private Task OnFormulaBarPressedAsync() => _pressAnswer = OnFormulaBarFocusAsync();

    /// <summary>
    /// A press into the Formula Bar, answered in its turn among the held keys (ADR-0051,
    /// ADR-0021/0010): completes once the edit its focus opens has been told to the key gate.
    /// The listener asks after the focus has been dispatched, when every press and key held
    /// before it has been answered. Its focus reached the core at once, ahead of them, and a
    /// press on the rows held before it may since have ended the edit that focus joined; when
    /// <paramref name="barHoldsFocus"/> says the bar still holds DOM focus and no edit stands,
    /// the press is answered again from here, as the focus would have been, on the cell the
    /// Focus is on now.
    ///
    /// <para>Called by the grid's own script module and not for Consumers: it is public
    /// only because JavaScript interop requires it.</para>
    /// </summary>
    /// <param name="barHoldsFocus">Whether the Formula Bar's field, or a Chrome's control inside
    /// its box, holds DOM focus as the press's turn comes.</param>
    [JSInvokable]
    public Task BarPressAnsweredAsync(bool barHoldsFocus)
    {
        if (_disposed)
            return Task.CompletedTask;
        if (barHoldsFocus && _editMode == EditMode.None)
            _pressAnswer = FromChromeAsync(OnFormulaBarFocusAsync);
        return PressAnsweredAsync();
    }

    /// <summary>
    /// Completes once the press or release on the rows the core heard last has been answered
    /// in full (ADR-0021/0010): the commit it made, the Consumer hearing it, and the editing
    /// mode the key gate was told. The grid's listener asks it after replaying a held press,
    /// before it hands on the keys held behind it. The listener replays the press as an event
    /// and asks this straight after, so the press is always heard first. A press that failed
    /// has been reported on its own path, and is answered all the same.
    ///
    /// <para>Called by the grid's own script module and not for Consumers: it is public
    /// only because JavaScript interop requires it.</para>
    /// </summary>
    [JSInvokable]
    public Task PressAnsweredAsync()
        => _disposed
            ? Task.CompletedTask
            : _pressAnswer.ContinueWith(static _ => { }, CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

    // What the script said the next press or release on the rows was taken against (ED-31;
    // ADR-0021, note of 2026-10-02), until that event is heard. It is told just before Blazor
    // dispatches the event, so the event the core hears next is the one it describes.
    private TakenAt? _taken;

    // The layouts the rows were painted under lately, the newest last: the column geometry, the
    // columns' names in order, the row height, and the binding — which Source the rows were painted
    // from (_binding). A press names the one it was painted under (data-ex-layout), and is resolved
    // against it. A press is answered a few round trips after it is made at most, so a short memory
    // is enough; one older than it lands on no cell.
    private const int PaintLayoutsKept = 16;
    private readonly List<PaintLayout> _paintLayouts = [];
    private int _paintLayoutId;

    private sealed record PaintLayout(int Id, ColumnGeometry Columns, string[] ColumnNames, double RowHeightPx, int Binding);

    /// <summary>Keeps the layout the next render paints the rows under, if it is a new one
    /// (ED-31). The columns' names are kept as one array while they are unchanged, so two layouts
    /// share an index space exactly when they share that array. A replaced Source is a new layout:
    /// its rows are another source's (ADR-0142).</summary>
    private void NotePaintLayout()
    {
        var geometry = _columnStyles.Geometry;
        var rowHeight = _metrics.RowHeightPx;
        var last = _paintLayouts.Count > 0 ? _paintLayouts[^1] : null;
        var names = last is not null && SameNames(last.ColumnNames) ? last.ColumnNames : [.. Columns.Select(c => c.Name)];
        if (last is not null && ReferenceEquals(last.Columns, geometry)
            && ReferenceEquals(last.ColumnNames, names) && last.RowHeightPx == rowHeight && last.Binding == _binding)
        {
            return;
        }
        _paintLayouts.Add(new PaintLayout(++_paintLayoutId, geometry, names, rowHeight, _binding));
        if (_paintLayouts.Count > PaintLayoutsKept)
            _paintLayouts.RemoveAt(0);
    }

    private bool SameNames(string[] names)
    {
        if (names.Length != Columns.Count)
            return false;
        for (var c = 0; c < names.Length; c++)
        {
            if (!string.Equals(names[c], Columns[c].Name, StringComparison.Ordinal))
                return false;
        }
        return true;
    }

    /// <summary>
    /// What a press or release on the rows was taken against (ED-31; ADR-0021, note of
    /// 2026-10-02), told by the grid's listener just before Blazor dispatches the event: at once,
    /// or at the replay of a press it held. The core resolves the event against this, not against
    /// the slice, scroll and layout it holds when the event arrives, so a press held behind keys
    /// that moved the view lands where it was made. One taken under another row order, under
    /// columns since renamed, reordered, added or removed, on rows that are no longer there, or on
    /// the rows of a Source since replaced by another instance (ADR-0142), lands on no cell.
    ///
    /// <para>Called by the grid's own script module and not for Consumers: it is public
    /// only because JavaScript interop requires it.</para>
    /// </summary>
    /// <param name="kind">The event told of: <c>mousedown</c> or <c>mouseup</c>.</param>
    /// <param name="offsetX">The x the browser gave the event, from the Viewport's left edge.</param>
    /// <param name="offsetY">The y the browser gave the event, from the Viewport's top edge.</param>
    /// <param name="firstRow">The first row the Viewport painted then (<c>data-ex-first-row</c>);
    /// negative when it painted none.</param>
    /// <param name="scrollLeftPx">The scroller's horizontal offset then.</param>
    /// <param name="rowSequence">The Row Sequence Version the Viewport was painted under
    /// (<c>data-ex-sequence</c>).</param>
    /// <param name="layout">The layout the Viewport was painted under (<c>data-ex-layout</c>).</param>
    [JSInvokable]
    public void PressTakenAt(string kind, double offsetX, double offsetY, int firstRow,
        double scrollLeftPx, int rowSequence, int layout)
    {
        // It crosses the JS boundary: a NaN would travel silently into the offsets.
        _taken = !_disposed && double.IsFinite(offsetX) && double.IsFinite(offsetY) && double.IsFinite(scrollLeftPx)
            ? new TakenAt(kind, offsetX, offsetY, firstRow, scrollLeftPx, rowSequence, layout)
            : null;
    }

    private sealed record TakenAt(string Kind, double OffsetX, double OffsetY, int FirstRow,
        double ScrollLeftPx, int RowSequence, int Layout);

    /// <summary>A press or release as it was taken (ED-31): the offsets it was given, and the first
    /// row, horizontal scroll and layout they were measured against. <see cref="Layout"/> is null
    /// when what it was taken on is no longer held: the press lands on no cell.</summary>
    private sealed class TakenMouseEventArgs : MouseEventArgs
    {
        public PaintLayout? Layout { get; init; }

        public int FirstRow { get; init; }

        public double ScrollLeftPx { get; init; }
    }

    /// <summary>The event as it was taken, if the script told of it (ED-31); the event as it came
    /// otherwise. What was told is for this event alone: told of another kind, it is no one's.</summary>
    private MouseEventArgs AsTaken(MouseEventArgs e, string kind)
    {
        var told = _taken;
        _taken = null;
        if (told is null || told.Kind != kind)
            return e;
        var layout = _paintLayouts.FindLast(l => l.Id == told.Layout);
        var held = layout is not null && ReferenceEquals(layout.ColumnNames, _paintLayouts[^1].ColumnNames)
            && layout.Binding == _binding && told.RowSequence == _sequenceVersion
            && told.FirstRow >= _pageStartRow && told.FirstRow < _pageStartRow + _geometry.TotalRowCount;
        return new TakenMouseEventArgs
        {
            Type = e.Type, Detail = e.Detail, Button = e.Button, Buttons = e.Buttons,
            ScreenX = e.ScreenX, ScreenY = e.ScreenY, ClientX = e.ClientX, ClientY = e.ClientY,
            PageX = e.PageX, PageY = e.PageY, MovementX = e.MovementX, MovementY = e.MovementY,
            CtrlKey = e.CtrlKey, ShiftKey = e.ShiftKey, AltKey = e.AltKey, MetaKey = e.MetaKey,
            OffsetX = told.OffsetX, OffsetY = told.OffsetY,
            Layout = held ? layout : null,
            FirstRow = told.FirstRow,
            ScrollLeftPx = told.ScrollLeftPx,
        };
    }

    /// <summary>The first row the event's offsets were measured against: the one painted when it was
    /// taken, the slice held now for an event nobody told of, null for one taken on what is no
    /// longer held.</summary>
    private static int? FirstRowOf(MouseEventArgs e, RowRange visible)
        => e is TakenMouseEventArgs taken ? (taken.Layout is null ? null : taken.FirstRow) : visible.Start;

    /// <summary>The horizontal scroll the event's x was measured against, which places the pinned
    /// band (ED-31).</summary>
    private double ScrollLeftOf(MouseEventArgs e)
        => e is TakenMouseEventArgs taken ? taken.ScrollLeftPx : _scrollLeftPx;

    /// <summary>The columns the event's x was measured against (ED-31).</summary>
    private ColumnGeometry ColumnsOf(MouseEventArgs e)
        => e is TakenMouseEventArgs { Layout: { } layout } ? layout.Columns : _columnStyles.Geometry;

    /// <summary>The row height the event's y was measured against (ED-31).</summary>
    private double RowHeightOf(MouseEventArgs e)
        => e is TakenMouseEventArgs { Layout: { } layout } ? layout.RowHeightPx : _geometry.RowHeightPx;
}
