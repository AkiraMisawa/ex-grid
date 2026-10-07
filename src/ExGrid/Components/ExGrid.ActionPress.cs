using ExGrid.Cells;
using ExGrid.Clipboard;
using ExGrid.Columns;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ExGrid.Components;

// An Action press acts on the row it was pressed on, as that row is now (ADR-0142, LV-12, LV-20): with
// a Row Key, the row under the pressed row's key while the Window holds it; without one, the pressed
// instance, or — for a press whose button a render disposed — the row at the position the browser told,
// while the order it was taken under holds. Otherwise it is refused: RowGone, or, without a Row Key,
// OrderMoved. No press is lost to Blazor, and none fires twice.
public partial class ExGrid<TRow>
{
    /// <summary>
    /// An Action press refused (ADR-0142, LV-12, LV-20): its row is gone
    /// (<see cref="ActionRefusalReason.RowGone"/>), or, without a Row Key, it named its row by a
    /// position under an order that has moved since (<see cref="ActionRefusalReason.OrderMoved"/>).
    /// A press never is refused because its row's values changed: it acts on the row it was pressed
    /// on, as that row is now. With a Row Key, that is the row under the pressed row's key, wherever
    /// the Window holds it, Space included. Without one, a press whose button a render has since
    /// disposed acts on the row at the position it was told, while the order it was taken under
    /// holds. Nothing is raised through <see cref="OnAction"/>. The refusal names the row where the
    /// grid still holds it, and none where it does not (ADR-0160). A Template cell's own controls are
    /// the Consumer's (ADR-0037). The grid holds no string for it; Chrome words it into its refusal
    /// live region (A11Y-16).
    /// </summary>
    [Parameter] public EventCallback<GridActionRefusal<TRow>> OnActionRefused { get; set; }

    /// <summary>A press on an action of this grid's own rows, told by the grid's listener, until its
    /// click is heard or the core answers it: the paint it was taken against, and — where the
    /// listener read them — its row's position, the serial of the row component that painted its
    /// button, and the column and action it stood for. <see cref="Pressed"/> is the row at that
    /// position when the press was told under the order it was taken under, else null.</summary>
    private sealed record ActionPress(int Paint, int Row, int Serial, TRow? Pressed, string? Column, string? Action);

    // The press being told, until its click is heard or the core answers it. Its row is one of the
    // three holdings that outlive a Window (ADR-0160): bounded to one, and dropped when the press is
    // heard or answered. The others are the Window last measured for Auto widths (_measuredWindow)
    // and the Row Key of the row under an open editor (_editKey).
    private ActionPress? _actionPress;

    // Whether the core answered a told press whose click had not come. A click such a press's
    // disposed component still delivers is that press's, and acts on nothing more: it fires once
    // (LV-12), whether or not the click comes.
    private bool _actionPressAnswered;

    /// <summary>
    /// What the next press on an action of this grid's own rows was taken against (ADR-0142,
    /// LV-12): the paint the Viewport named at its mousedown, and the row, column and action the
    /// button stood for in it. Told by the grid's listener at the release on the same button, just
    /// before Blazor dispatches the click, so the click the core hears next is the one it
    /// describes. Reading what the render wrote is not a measurement, and nothing per cell crosses
    /// (ADR-0021, notes of 2026-10-05 to 2026-10-07).
    ///
    /// <para>Blazor does not deliver an event whose attribute a component since disposed had
    /// rendered. Without a Row Key a row whose instance a render replaced while the press was on its
    /// way has its component disposed (ADR-0140), and the click on its button would then be lost
    /// without a word. So a press whose row component is no longer rendered is answered here, and
    /// one whose component a later render disposes before its click arrives is answered after that
    /// render: it acts on the row at the told position while the order it was taken under holds,
    /// and is refused otherwise (ADR-0142).</para>
    ///
    /// <para>Called by the grid's own script module and not for Consumers: it is public
    /// only because JavaScript interop requires it.</para>
    /// </summary>
    /// <param name="paint">The paint the Viewport named at the press (<c>data-ex-paint</c>).</param>
    /// <param name="row">The pressed cell's row, as its id names it, or −1.</param>
    /// <param name="column">The pressed cell's column, as its id names it, or −1.</param>
    /// <param name="action">Which of the cell's actions was pressed, or −1.</param>
    [JSInvokable]
    public async Task ActionPressTakenAt(int paint, int row = -1, int column = -1, int action = -1)
    {
        if (_disposed)
            return;
        _actionPressAnswered = false;
        var painted = _paintRows.FindLast(p => p.Id == paint);
        var at = painted is null ? -1 : row - painted.FirstRow;
        if (painted is null || at < 0 || at >= painted.Serials.Length || painted.Serials[at] == 0
            || column < 0 || column >= painted.Columns.Count || action < 0 || action >= painted.Columns[column].Actions.Count)
        {
            _actionPress = new ActionPress(paint, -1, 0, null, null, null);
            return;
        }
        _actionPress = new ActionPress(paint, row, painted.Serials[at], AimedUnderAnotherOrder(paint) ? null : RowInHand(row),
            painted.Columns[column].Name, painted.Columns[column].Actions[action].Name);
        await AnswerActionPressWithNoClickAsync();
    }

    /// <summary>
    /// Answers a told press whose click Blazor will not deliver, because the row component that
    /// rendered its button is no longer rendered (ADR-0142, "No press is lost to Blazor"). Raised as
    /// the click would have been, on the row it was pressed on, or refused. A press whose component
    /// is still rendered waits for its click. Decided by the order the core hears things in, never
    /// by time.
    /// </summary>
    private async Task AnswerActionPressWithNoClickAsync()
    {
        if (_actionPress is not { Serial: > 0 } press || _disposed || _rowSerials.ContainsValue(press.Serial))
            return;
        _actionPress = null;
        _actionPressAnswered = true;
        await ActAsync(press.Pressed, press.Column!, press.Action!, press.Paint, press.Row, byPosition: false);
    }

    /// <summary>An action pressed by pointer, through its row's button (ADR-0020): the click of the
    /// press the grid's listener told of, if it did (ADR-0142, LV-12).</summary>
    private Task RaiseActionAsync(GridActionEventArgs<TRow> args)
    {
        var press = _actionPress;
        var answered = _actionPressAnswered;
        _actionPress = null;
        _actionPressAnswered = false;
        // The click of a press the core has answered already, which its component, disposed since,
        // still delivered: the press was acted on once.
        if (press is null && answered && !RendersRowComponentOf(args.Row))
            return Task.CompletedTask;
        return ActAsync(args.Row, args.ColumnName, args.ActionName, press?.Paint ?? PaintNotTold,
            press is { Row: >= 0 } told ? told.Row : null, byPosition: false);
    }

    /// <summary>
    /// Raises an Action press, or its refusal (ADR-0020, ADR-0142): on the row it was pressed on, as
    /// that row is in the newest version the bound source holds (D5, LV-16) — the button held the row
    /// it was painted with, and a handler that writes the row back through <c>GridSource.From</c>'s
    /// <c>ReplaceRow</c> must not be refused for a stale version.
    /// </summary>
    /// <param name="pressed">The row the pressed button held, or the one the core resolved for it;
    /// null when it resolved none.</param>
    /// <param name="column">The Action Column's name.</param>
    /// <param name="action">The action's name.</param>
    /// <param name="told">The paint the press was taken against (ADR-0142).</param>
    /// <param name="at">The position the press named, under the order of <paramref name="told"/>;
    /// null where it named none.</param>
    /// <param name="byPosition">Whether the press names its row by the position alone — Space on the
    /// Focus — rather than by the row its button held.</param>
    private async Task ActAsync(TRow? pressed, string column, string action, int told, int? at, bool byPosition)
    {
        // Pointed at from outside, a press on the rows is handed over instead of acting (ADR-0058):
        // the stylesheet lets it through to the rows, and this is the guard behind it.
        if (_disposed || PointedAtNow)
            return;
        // A press on any action ends Interactive (ADR-0037), by pointer as by Space — and
        // the pointer's press arrives through the row's handler, which renders the row but
        // not this root.
        if (EndInteractive())
        {
            _suppressRender = false;
            StateHasChanged();
        }
        // A position names its row only under the order it was taken against (ADR-0011). Without a
        // Row Key, Space names its row by nothing else, so under another order it is refused; with
        // one it acts by the key of the row it stands on, and a press whose button held its row still
        // has the row to go by.
        var orderMoved = at is not null && AimedUnderAnotherOrder(told);
        if (orderMoved && byPosition && _rowKey is null)
        {
            await RefuseActionAsync(pressed, column, action, ActionRefusalReason.OrderMoved);
            return;
        }
        if (orderMoved)
            at = null;
        at ??= pressed is { } held ? PositionInHand(held) : null;
        var version = _sequenceVersion;
        if (!await TakeInGatheredAsync())
            return;
        if (version != _sequenceVersion)
        {
            orderMoved = at is not null;
            at = null;
        }
        // And nothing else: no focus is moved (ADR-0037, amended). A press never focused the button,
        // so the keyboard is still where it was — or in whatever the handler opened, which taking it
        // back to the root would rob.
        if (PressedRowNow(pressed, at, orderMoved, out var reason) is not { } row)
        {
            await RefuseActionAsync(pressed, column, action, reason);
            return;
        }
        if (OnAction.HasDelegate)
            await OnAction.InvokeAsync(new GridActionEventArgs<TRow>(row, column, action));
    }

    /// <summary>
    /// The row a press acts on now (ADR-0142, LV-12): with a Row Key, the row under the pressed row's
    /// key, wherever the Window holds it (ADR-0140); without one, the pressed instance while the rows
    /// painted now hold it, else the row at <paramref name="at"/>, the position the press named while
    /// it still names that row. Null when there is none, and <paramref name="reason"/> says why.
    /// </summary>
    private TRow? PressedRowNow(TRow? pressed, int? at, bool orderMoved, out ActionRefusalReason reason)
    {
        reason = ActionRefusalReason.RowGone;
        if (_rowKey is { } rowKey)
        {
            if (pressed is null)
                return at is { } told ? RowInHand(told) : null;
            var key = rowKey(pressed);
            if (at is { } p && RowInHand(p) is { } there && Equals(rowKey(there), key))
                return there;
            return PositionInHandByKey(rowKey, key) is { } found ? RowInHand(found) : null;
        }
        if (pressed is not null && PositionInHand(pressed) is not null)
            return pressed;
        if (at is { } position && RowInHand(position) is { } newest)
            return newest;
        if (orderMoved)
            reason = ActionRefusalReason.OrderMoved;
        return null;
    }

    /// <summary>Raises the refusal of a press, naming <paramref name="pressed"/> only while the
    /// Window holds it (ADR-0160).</summary>
    private async Task RefuseActionAsync(TRow? pressed, string column, string action, ActionRefusalReason reason)
    {
        if (!OnActionRefused.HasDelegate)
            return;
        var held = pressed is not null && (_rowKey is { } rowKey
            ? PositionInHandByKey(rowKey, rowKey(pressed)) is not null
            : WindowHolds(pressed));
        await OnActionRefused.InvokeAsync(new GridActionRefusal<TRow>(held ? pressed : null, column, action, reason));
    }

    /// <summary>Whether the Window holds <paramref name="row"/>, by reference (ADR-0003).</summary>
    private bool WindowHolds(TRow row)
    {
        foreach (var held in _window)
        {
            if (ReferenceEquals(held, row))
                return true;
        }
        return false;
    }
}
