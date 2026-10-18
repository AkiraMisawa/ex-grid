using ExGrid.Summarizing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace ExGrid.Components;

// A click the script hears inside the root takes its place in the Input Order (ADR-0240 section 2).
// The grid's own controls whose Blazor handler changes the instance's state, or where the keyboard is,
// say so on their element: data-ex-claims names the events their handler claims — a heading's press
// and click, a ▾ opener's click, a menu item's, a completion candidate's press, the pager's and the
// status line's buttons, a grip's double click, a filter's OK and Cancel and Find's steps. The script's
// capture-phase listeners number such an event as it happens and tell it here (InputTakenAt), just
// before Blazor dispatches it; the handler claims the number as it starts (ClaimedAsync), and is handled
// in its place in the order, one input at a time. While the key gate holds input, the script holds such
// an event too, in its place among the held keys, and tells it and dispatches it in its turn.
//
// One telling waits for its event at a time, as a press on the rows does (PressTakenAt): a number told
// ahead of an event that never came is voided by the next telling, or by a claim of another kind. A
// later number of another channel — a key — never voids it: it waits for it, as it waits for every
// number before it.
public partial class ExGrid<TRow>
{
    // The event the script told of last, and its number, until a handler claims it.
    private (string Kind, int Input)? _claimTold;

    /// <summary>
    /// The next click, press, double click or context menu on one of the grid's own controls that
    /// claim it (<c>data-ex-claims</c>) was numbered <paramref name="input"/> (ADR-0240 section 2). Told
    /// by the grid's listener just before Blazor dispatches the event, at once or at the replay of one
    /// it held, so the handler that hears it next takes that number and is handled in its place in the
    /// Input Order. An earlier telling no handler claimed is void: its event never came.
    ///
    /// <para>Called by the grid's own script module and not for Consumers: it is public
    /// only because JavaScript interop requires it.</para>
    /// </summary>
    /// <param name="kind">The event told of: <c>mousedown</c>, <c>click</c>, <c>dblclick</c> or
    /// <c>contextmenu</c>.</param>
    /// <param name="input">The number the script gave the event, which the root names once it has
    /// been answered (ADR-0220).</param>
    /// <remarks>Not the contract (ADR-0201): the grid's script calls it, so it is public, but it is
    /// hidden from a Consumer's editor and may change in any version.</remarks>
    [JSInvokable, System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public void InputTakenAt(string kind, int input)
    {
        if (_claimTold is { } unheard)
            InputVoid(unheard.Input);
        _claimTold = null;
        if (_disposed || string.IsNullOrEmpty(kind))
        {
            InputVoid(input);
            return;
        }
        _claimTold = (kind, input);
    }

    /// <summary>The number the script told the event of <paramref name="kind"/> by, taken by the
    /// handler that hears it; 0 when none was told, as in a test or for an event nobody numbered. Told
    /// of another kind, that number is no one's, and is void.</summary>
    private int ClaimInput(string kind)
    {
        var told = _claimTold;
        _claimTold = null;
        if (told is not { } claim)
            return 0;
        if (!string.Equals(claim.Kind, kind, StringComparison.Ordinal))
        {
            InputVoid(claim.Input);
            return 0;
        }
        return claim.Input;
    }

    /// <summary>A handler of a control that claims <paramref name="kind"/>, handled in its place in
    /// the Input Order (ADR-0240): once every input numbered before it has been handled, and before
    /// any numbered after it. The number is claimed now, as the event is heard.</summary>
    private Task ClaimedAsync(string kind, Func<Task> handling) => AnsweredAsync(handling, ClaimInput(kind));

    /// <summary><see cref="ClaimedAsync(string, Func{Task})"/> for a handler that waits on nothing.</summary>
    private Task ClaimedAsync(string kind, Action handling) => AnsweredAsync(handling, ClaimInput(kind));

    /// <summary>A click on a control that claims it, handled in its place (<see cref="ClaimedAsync(string, Func{Task})"/>).</summary>
    private Task ClaimedClickAsync(Func<Task> handling) => ClaimedAsync("click", handling);

    /// <summary>A press on a control that claims it, handled in its place (<see cref="ClaimedAsync(string, Action)"/>).</summary>
    private Task ClaimedPressAsync(Action handling) => ClaimedAsync("mousedown", handling);

    // The claimants' handlers the root's markup binds, one delegate each, kept: the same instance every
    // render, so the diff leaves the handler registered (PF-3).
    private Func<MouseEventArgs, Task>? _onHeaderClick;
    private Func<Task>? _onTurnBack;
    private Func<Task>? _onTurnOn;
    private Func<Task>? _onGoToSelection;
    private Func<Task>? _onSelectWholeResult;
    private Func<MouseEventArgs, Task>? _onRowsContextMenu;
    private Func<Task>? _onCancelPanel;
    private Func<Task>? _onFindPrevious;
    private Func<Task>? _onFindNext;
    private Func<SummaryFigures, Task>? _onToggleSummaryFigure;

    private Func<MouseEventArgs, Task> HeaderClickHandler
        => _onHeaderClick ??= e => ClaimedAsync("click", () => OnHeaderClickAsync(e));

    private Func<Task> TurnBackHandler => _onTurnBack ??= () => ClaimedAsync("click", () => TurnPageAsync(-1));

    private Func<Task> TurnOnHandler => _onTurnOn ??= () => ClaimedAsync("click", () => TurnPageAsync(1));

    private Func<Task> GoToSelectionHandler => _onGoToSelection ??= () => ClaimedAsync("click", GoToSelectionAsync);

    private Func<Task> SelectWholeResultHandler
        => _onSelectWholeResult ??= () => ClaimedAsync("click", SelectWholeResultAsync);

    private Func<MouseEventArgs, Task> RowsContextMenuHandler
        => _onRowsContextMenu ??= e => ClaimedAsync("contextmenu", () => OnContextMenuAsync(e));

    private Func<Task> CancelPanelHandler => _onCancelPanel ??= () => ClaimedAsync("click", CancelPanel);

    private Func<Task> FindPreviousHandler => _onFindPrevious ??= () => ClaimedAsync("click", () => FindStepAsync(backward: true));

    private Func<Task> FindNextHandler => _onFindNext ??= () => ClaimedAsync("click", () => FindStepAsync(backward: false));

    private Func<SummaryFigures, Task> ToggleSummaryFigureHandler
        => _onToggleSummaryFigure ??= figure => ClaimedAsync("click", () => ToggleSummaryFigureAsync(figure));
}
