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
        InputReached(input);
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

    /// <summary>
    /// Handles an input of a control outside the grid's own markup — a Chrome's, a Consumer's, or a
    /// product's own surface inside the Input Root, such as ExSheet's toolbar — in its place in the Input
    /// Order (ADR-0240 section 6): once every input numbered before it has been handled, and before any
    /// numbered after it. The order is the instance's, so the control's input is taken one at a time
    /// with the grid's own keys, presses and clicks, at machine speed as typed slowly.
    ///
    /// <para>Called from the control's Blazor handler as it starts, on the renderer's context, with the
    /// kind of the event that handler hears. The control declares the events it takes in its
    /// <c>data-ex-claims</c> attribute (<c>click</c>, <c>keydown</c>, …, space-separated): the grid's
    /// listener numbers such an event as it happens and tells the number just before Blazor dispatches
    /// the event, and this call takes it. It hears the grid's own markup — a popover's contents in the
    /// grid's frame included — and, where a product draws the grid, that product's Input Root around it
    /// (the element marked <c>data-ex-input-root</c>, such as ExSheet's box); nothing beyond them. While the grid holds input — behind a key
    /// still being answered — it holds such an event too, and dispatches it in its turn. A handler that
    /// declares an event must call this every time it hears it: a number told and never taken holds every
    /// input behind it until the next one is told.</para>
    ///
    /// <para>An input whose number was not told — a control that does not declare the event, one outside
    /// the Input Root, a call from the Consumer's own code — runs after every input received so far.
    /// Called from inside the handling of an input under way (a command a key runs), it runs at once, as
    /// part of that input.</para>
    ///
    /// <para><see cref="GridInputKind.KeyboardIn"/> and <see cref="GridInputKind.KeyboardOut"/> are the
    /// exception: they are inputs only as the grid tells them, and the grid tells them for one element
    /// alone — a product's Input Root around the grid, while that element itself names <c>focusin</c> and
    /// <c>focusout</c> in its own <c>data-ex-claims</c>, as ExSheet's box does while it is in a Pointing
    /// Scope. The keyboard crossing that element's edge is told, and the handling runs in its place. A move
    /// of DOM focus inside it crosses nothing, so a <c>focusin</c> or <c>focusout</c> handler there hears it
    /// and its handling does not run; and since the grid cannot tell such a move from an event it is not
    /// told of, every other call with these kinds — from an element inside the Input Root that claims
    /// them, from a grid that stands on its own, whose Input Root is its own root — does not run its
    /// handling either, and completes at once. Hear the keyboard arriving at a control of your own in your
    /// own handler, outside the order.</para>
    /// </summary>
    /// <param name="kind">The event the handler hears.</param>
    /// <param name="handling">The handler's work. Its <c>await</c>s are inside the input: the next input
    /// starts once it has finished.</param>
    /// <returns>A task that completes once <paramref name="handling"/> has run, or at once for a
    /// <see cref="GridInputKind.KeyboardIn"/> or <see cref="GridInputKind.KeyboardOut"/> the grid did not
    /// tell; it ends with nothing done once the grid is disposed.</returns>
    public Task InOrderAsync(GridInputKind kind, Func<Task> handling)
    {
        ArgumentNullException.ThrowIfNull(handling);
        if (_disposed)
            return Task.CompletedTask;
        var claimed = ClaimKindOf(kind);
        if (kind is GridInputKind.KeyboardIn or GridInputKind.KeyboardOut)
        {
            // Only a telling of this kind is this handler's: one of another kind waits for its own
            // handler, and is not voided by a move of DOM focus inside the Input Root.
            if (_claimTold is not { } crossing || !string.Equals(crossing.Kind, claimed, StringComparison.Ordinal))
                return Task.CompletedTask;
            _claimTold = null;
            // A crossing the input under way made itself (its focus, heard within its handling) is part
            // of it: handled at once, and its own number is answered as void.
            if (_inputOrder?.InFlow == true)
            {
                InputVoid(crossing.Input);
                return handling();
            }
            return AnsweredAsync(handling, crossing.Input);
        }
        // Inside the handling of an input under way it is part of that input; a telling standing is
        // another event's, left for its own handler.
        if (_inputOrder?.InFlow == true)
            return handling();
        var input = ClaimInput(claimed);
        return input > 0 ? AnsweredAsync(handling, input) : Inputs.AfterReceivedAsync(handling);
    }

    /// <summary>The event name the script tells for <paramref name="kind"/> (data-ex-claims).</summary>
    private static string ClaimKindOf(GridInputKind kind) => kind switch
    {
        GridInputKind.Click => ClaimKinds.Click,
        GridInputKind.DoubleClick => ClaimKinds.DoubleClick,
        GridInputKind.ContextMenu => ClaimKinds.ContextMenu,
        GridInputKind.Press => ClaimKinds.Press,
        GridInputKind.KeyDown => ClaimKinds.KeyDown,
        GridInputKind.Submit => ClaimKinds.Submit,
        GridInputKind.Focus => ClaimKinds.Focus,
        GridInputKind.KeyboardIn => ClaimKinds.KeyboardIn,
        GridInputKind.KeyboardOut => ClaimKinds.KeyboardOut,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a kind of input the grid numbers (ADR-0240)."),
    };

    // InOrderAsync, as the popover frame's contents are handed it (GridPopoverContext.InOrder): one
    // delegate, so the context compares equal from render to render.
    private Func<GridInputKind, Func<Task>, Task>? _inOrder;

    private Func<GridInputKind, Func<Task>, Task> InOrderHandler => _inOrder ??= InOrderAsync;

    // A key a substituted Chrome's menu handed to the core's resolver, which claimed its number (ClaimedKey),
    // until the command, close or focus its handler then asks of the core takes it (ClaimedByKeyOrAsync).
    private int _keyClaimed;

    /// <summary>A Chrome's menu key, resolved: the number told for it is claimed now, and taken by the
    /// core's work the Chrome's handler asks for next; asked for nothing, it is answered once that handler
    /// has run (AnswerUntakenKeyAsync), so it is never left owed. The meaning itself is the resolver's.</summary>
    private MenuKey ClaimedKey(Func<MenuKey> resolve)
    {
        var input = ClaimInput(ClaimKinds.KeyDown);
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
    private Task ClaimedClickAsync(Func<Task> handling) => ClaimedAsync(ClaimKinds.Click, handling);

    /// <summary>A press on a control that claims it, handled in its place (<see cref="ClaimedAsync(string, Action)"/>).</summary>
    private Task ClaimedPressAsync(Action handling) => ClaimedAsync(ClaimKinds.Press, handling);

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

    private Func<Task> SubmitFindHandler => _onSubmitFind ??= () => ClaimedAsync(ClaimKinds.Submit, SubmitFindAsync);

    private Func<Task> ReturnToCommandsHandler => _onReturnToCommands ??= () => ClaimedAsync(ClaimKinds.Focus, ReturnKeyboardToCommandsAsync);

    private Func<Task> ReturnToFindHandler => _onReturnToFind ??= () => ClaimedAsync(ClaimKinds.Focus, ReturnKeyboardToFindAsync);

    private Func<Task> ConsumerLeadingSentinelHandler
        => _onConsumerLeadingSentinel ??= () => ClaimedAsync(ClaimKinds.Focus, OnConsumerLeadingSentinel);

    private Func<Task> ConsumerTrailingSentinelHandler
        => _onConsumerTrailingSentinel ??= () => ClaimedAsync(ClaimKinds.Focus, OnConsumerTrailingSentinel);

    private Func<MouseEventArgs, Task> HeaderClickHandler
        => _onHeaderClick ??= e => ClaimedAsync(ClaimKinds.Click, () => OnHeaderClickAsync(e));

    private Func<Task> TurnBackHandler => _onTurnBack ??= () => ClaimedAsync(ClaimKinds.Click, () => TurnPageAsync(-1));

    private Func<Task> TurnOnHandler => _onTurnOn ??= () => ClaimedAsync(ClaimKinds.Click, () => TurnPageAsync(1));

    private Func<Task> GoToSelectionHandler => _onGoToSelection ??= () => ClaimedAsync(ClaimKinds.Click, GoToSelectionAsync);

    private Func<Task> SelectWholeResultHandler
        => _onSelectWholeResult ??= () => ClaimedAsync(ClaimKinds.Click, SelectWholeResultAsync);

    private Func<MouseEventArgs, Task> RowsContextMenuHandler
        => _onRowsContextMenu ??= e => ClaimedAsync(ClaimKinds.ContextMenu, () => OnContextMenuAsync(e));

    private Func<Task> CancelPanelHandler => _onCancelPanel ??= () => ClaimedAsync(ClaimKinds.Click, CancelPanel);

    private Func<Task> FindPreviousHandler => _onFindPrevious ??= () => ClaimedAsync(ClaimKinds.Click, () => FindStepAsync(backward: true));

    private Func<Task> FindNextHandler => _onFindNext ??= () => ClaimedAsync(ClaimKinds.Click, () => FindStepAsync(backward: false));

    private Func<SummaryFigures, Task> ToggleSummaryFigureHandler
        => _onToggleSummaryFigure ??= figure => ClaimedAsync(ClaimKinds.Click, () => ToggleSummaryFigureAsync(figure));
}

/// <summary>
/// The kinds of event a control claims (<c>data-ex-claims</c>) and the script tells (<c>InputTakenAt</c>),
/// in the browser's names (ADR-0240 section 2): every place the core names one — a handler claiming its
/// number, the markup declaring it — names it from here, so a misspelt kind is a compile error rather
/// than a number told that no handler ever takes.
/// </summary>
internal static class ClaimKinds
{
    /// <summary>A click.</summary>
    public const string Click = "click";

    /// <summary>A double click.</summary>
    public const string DoubleClick = "dblclick";

    /// <summary>A context menu.</summary>
    public const string ContextMenu = "contextmenu";

    /// <summary>A press of the pointer's button.</summary>
    public const string Press = "mousedown";

    /// <summary>A key going down.</summary>
    public const string KeyDown = "keydown";

    /// <summary>A form's submission.</summary>
    public const string Submit = "submit";

    /// <summary>An element taking DOM focus.</summary>
    public const string Focus = "focus";

    /// <summary>The keyboard coming into the Input Root.</summary>
    public const string KeyboardIn = "focusin";

    /// <summary>The keyboard leaving the Input Root.</summary>
    public const string KeyboardOut = "focusout";
}
