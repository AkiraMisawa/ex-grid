using ExGrid.Chrome;
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
//
// The keys a popover answers itself are told the same way (ADR-0240 section 4): a key on a menu's
// items or on a filter's value list (keydown), a filter's or Find's form submitted (submit), and Tab
// landing on a popover's sentinel (focus). The built-in handlers claim the number as they start. A
// substituted Chrome's menu hands its key to the core's resolver (ResolveKey), which claims it, and the
// command, the close or the focus the Chrome then asks of the core — in the same handler — is handled
// under that number (ClaimedByKeyOrAsync); asked for nothing, the number is answered once the handler
// has run, in the order's turn. So the keys behind such a key wait on its number, never on a clock.
public partial class ExGrid<TRow>
{
    // The event the script told of last, and its number, until a handler claims it.
    private (string Kind, int Input, int RowCommand)? _claimTold;

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
    /// <param name="kind">The event told of: <c>mousedown</c>, <c>click</c>, <c>dblclick</c>,
    /// <c>contextmenu</c>, <c>keydown</c>, <c>submit</c> or <c>focus</c>.</param>
    /// <param name="input">The number the script gave the event, which the root names once it has
    /// been answered (ADR-0220).</param>
    /// <remarks>Not the contract (ADR-0201): the grid's script calls it, so it is public, but it is
    /// hidden from a Consumer's editor and may change in any version.</remarks>
    [JSInvokable, System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public void InputTakenAt(string kind, int input) => TellInput(kind, input);

    private void TellInput(string kind, int input, int rowCommand = 0)
    {
        var previous = _claimTold;
        _claimTold = null;
        if (previous is { } unheard)
        {
            CancelRowClick(unheard.RowCommand);
            InputVoid(unheard.Input);
        }
        if (_disposed || string.IsNullOrEmpty(kind))
        {
            InputVoid(input);
            return;
        }
        _claimTold = (kind, input, rowCommand);
    }

    /// <summary>The number the script told the event of <paramref name="kind"/> by, taken by the
    /// handler that hears it; 0 when none was told, as in a test or for an event nobody numbered. Told
    /// of another kind, that number is no one's, and is void.</summary>
    private int ClaimInput(string kind, string? alsoKind = null)
    {
        var told = _claimTold;
        _claimTold = null;
        if (told is not { } claim)
            return 0;
        if (!string.Equals(claim.Kind, kind, StringComparison.Ordinal)
            && !string.Equals(claim.Kind, alsoKind, StringComparison.Ordinal))
        {
            CancelRowClick(claim.RowCommand);
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

    /// <summary><see cref="ClaimedAsync(string, Func{Task})"/> for a handler two kinds of event reach: Find's step, by
    /// its button's click or its form's submission.</summary>
    private Task ClaimedAsync(string kind, string alsoKind, Func<Task> handling)
        => AnsweredAsync(handling, ClaimInput(kind, alsoKind));

    // The kinds of event the script tells (data-ex-claims).
    private const string ClaimClick = "click";
    private const string ClaimKeyDown = "keydown";
    private const string ClaimSubmit = "submit";
    private const string ClaimFocus = "focus";

    // A key a substituted Chrome's menu handed to the core's resolver, which claimed its number (ClaimedKey),
    // until the command, close or focus its handler then asks of the core takes it (ClaimedByKeyOrAsync).
    private int _keyClaimed;

    /// <summary>A Chrome's menu key, resolved: the number told for it is claimed now, and taken by the
    /// core's work the Chrome's handler asks for next; asked for nothing, it is answered once that handler
    /// has run (AnswerUntakenKeyAsync), so it is never left owed. The meaning itself is the resolver's.</summary>
    private MenuKey ClaimedKey(Func<MenuKey> resolve)
    {
        var input = ClaimInput(ClaimKeyDown);
        var meaning = resolve();
        if (input > 0)
        {
            if (_keyClaimed > 0)
                InputVoid(_keyClaimed);
            _keyClaimed = input;
            _ = AnswerUntakenKeyAsync(input);
        }
        return meaning;
    }

    /// <summary>The key numbered <paramref name="input"/>, once the handler that resolved it has run: no
    /// command, close or focus took it, so it is answered as handled.</summary>
    private async Task AnswerUntakenKeyAsync(int input)
    {
        await Task.Yield();
        if (_keyClaimed != input)
            return;
        _keyClaimed = 0;
        await AnsweredAsync(() => { }, input);
    }

    /// <summary>Work a substituted Chrome asks of the core — a command run, a close, a focus — under the
    /// number of the key its menu resolved just before (ClaimedKey), or else of the event of
    /// <paramref name="kind"/> told for it (none for a focus).</summary>
    private Task ClaimedByKeyOrAsync(string? kind, Func<Task> handling)
    {
        if (_keyClaimed > 0)
        {
            var key = _keyClaimed;
            _keyClaimed = 0;
            return AnsweredAsync(handling, key);
        }
        return kind is null ? handling() : ClaimedAsync(kind, handling);
    }

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
    private Func<Task>? _onSubmitFind;
    private Func<Task>? _onReturnToCommands;
    private Func<Task>? _onReturnToFind;
    private Func<Task>? _onConsumerLeadingSentinel;
    private Func<Task>? _onConsumerTrailingSentinel;

    // A Consumer's popover contents asked by a sentinel to take the keyboard, and not yet through
    // TakeFocus (FocusOwed, ADR-0240 section 4).
    private bool _consumerFocusOwed;

    private Func<Task> SubmitFindHandler => _onSubmitFind ??= () => ClaimedAsync(ClaimSubmit, SubmitFindAsync);

    private Func<Task> ReturnToCommandsHandler => _onReturnToCommands ??= () => ClaimedAsync(ClaimFocus, ReturnKeyboardToCommandsAsync);

    private Func<Task> ReturnToFindHandler => _onReturnToFind ??= () => ClaimedAsync(ClaimFocus, ReturnKeyboardToFindAsync);

    private Func<Task> ConsumerLeadingSentinelHandler
        => _onConsumerLeadingSentinel ??= () => ClaimedAsync(ClaimFocus, OnConsumerLeadingSentinel);

    private Func<Task> ConsumerTrailingSentinelHandler
        => _onConsumerTrailingSentinel ??= () => ClaimedAsync(ClaimFocus, OnConsumerTrailingSentinel);

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
