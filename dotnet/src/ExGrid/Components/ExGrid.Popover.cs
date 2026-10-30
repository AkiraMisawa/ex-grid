using ExGrid.Chrome;
using ExGrid.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace ExGrid.Components;

// The Consumer's own contents in the grid's popover frame, and the inputs on the Consumer's own markup the grid draws
// (ADR-0441 section 4): the frame is the core's, and the contents are a Razor slot (ADR-0410) moved into it. A control
// there that names an event in data-ex-claims is numbered by the browser as the event happens, and told here just before
// Blazor dispatches it (ClaimTold); the inputs after it wait until the control's handler hands its work to InOrderAsync
// with that kind, which runs it in the number's turn (RunClaimAsync).
public sealed partial class ExGrid<TRow>
{
    // The Consumer's popover drawn, by the number of its opening: the one opening, from the moment the browser mounts
    // its slot inside the call, else the one the browser shows. Its contents are drawn for that opening.
    private ConsumerPopover? _popover;
    // The popover the browser last showed and has not closed: drawn again where a later opening is refused.
    private ConsumerPopover? _popoverShown;
    private int _popoversOpened;
    // The contents standing in the popover's slots, drawn again on their own with each render of the page, whose state
    // they read: the grid's other slots are not drawn again for it.
    private readonly HashSet<PopoverContents> _popoverContents = [];
    // Made once, so the contents' parameters compare equal from render to render.
    private Func<int, RenderFragment>? _drawPopover;
    private Action<PopoverContents, bool>? _popoverContentsStanding;

    private sealed record ConsumerPopover(int Opening, RenderFragment<GridPopoverContext> Content, GridPopoverContext Context);

    // The claims the browser told, by their number, until the handler that hears the event hands its work, or a telling
    // after it voids it. The browser numbers them below zero, apart from the work .NET places.
    private readonly Dictionary<int, TaskCompletionSource<Work?>> _claims = [];
    private (string Kind, int Number)? _claimTold;

    /// <summary>
    /// Shows a Consumer's own contents in the grid's popover frame (ADR-0050 item 16, ADR-0441 section 4). The frame
    /// is every popover's: it stands under the header band, centred across the grid and inside its box, contents taller
    /// or wider than the room scroll inside it (ADR-0040), it replaces any popover that stands, and Escape, a press
    /// elsewhere in the instance and the box shrinking below one row close it as a Cancel. Tab and Shift+Tab wrap inside
    /// it, and however it closes the keyboard returns to the grid (ADR-0039). The inputs made after the opening wait
    /// until the contents stand, and the contents' first control then takes the keyboard where the frame still holds it
    /// (ADR-0410 section 5). Calling it again opens new contents in place of the old.
    ///
    /// <para>A control of the contents' whose handler changes the instance or moves the keyboard — an OK, a choice made,
    /// a key the contents answer — names its event in <c>data-ex-claims</c> and hands its work to
    /// <see cref="GridPopoverContext.InOrder"/>: the inputs made after it wait until that work has run, so a key typed
    /// after a click on OK reaches what the OK left, at any speed (ADR-0240 section 6). A popup the contents open of
    /// their own, a select's list, is reported through <see cref="GridPopoverContext.InnerPopupChanged"/>: while it is
    /// open, Escape is the popup's.</para>
    /// </summary>
    /// <param name="content">The contents, drawn with the frame's context.</param>
    /// <param name="label">The popover's accessible name, in the Consumer's words: the frame is a <c>dialog</c> (ADR-0033).</param>
    /// <returns>Whether it opened: false while an edit is open, whose text a popover would take the keyboard from, and
    /// once the grid is disposed.</returns>
    /// <exception cref="ArgumentException"><paramref name="label"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">The grid is not attached yet.</exception>
    public Task<bool> OpenPopoverAsync(RenderFragment<GridPopoverContext> content, string label)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        var grid = Attached("opens no popover");
        return Dispatched(async () =>
        {
            if (_disposed)
                return false;
            var opening = ++_popoversOpened;
            // Each delegate made once for the opening, so the context compares equal from render to render.
            var context = new GridPopoverContext(() => ClosePopoverOf(grid, opening), 0, 0, open => InnerPopupChanged(grid, opening, open))
            {
                TakeFocus = HandKeyboardToAsync,
                InOrder = InOrderAsync,
            };
            var popover = new ConsumerPopover(opening, content, context);
            _popover = popover;
            bool opened;
            try
            {
                opened = await grid.InvokeAsync<bool>("openPopover", opening, label);
            }
            catch (Exception ex) when (_disposed && ex is JSDisconnectedException or JSException or ObjectDisposedException or OperationCanceledException)
            {
                return false;
            }
            if (opened)
                _popoverShown = popover;
            else if (_popover?.Opening == opening)
                _popover = _popoverShown;
            return opened;
        });
    }

    /// <summary>Closes the Consumer's popover where it stands (ADR-0050 item 16), and the keyboard returns to the grid.
    /// A popover of the grid's own opened since stays.</summary>
    /// <returns>A task that completes once the request has been made.</returns>
    /// <exception cref="InvalidOperationException">The grid is not attached yet.</exception>
    public Task ClosePopoverAsync()
    {
        var grid = Attached("closes no popover");
        return InvokeAsync(async () =>
        {
            if (_disposed || _popover is null)
                return;
            await grid.InvokeVoidAsync("closePopover");
        });
    }

    // The contents' own Close and their popup's report cross as they are made, ahead of whatever the handler sends next.
    private void ClosePopoverOf(IJSObjectReference grid, int opening)
    {
        if (!_disposed && _popover?.Opening == opening)
            _ = Sent(grid.InvokeVoidAsync("closePopoverOf", opening));
    }

    private void InnerPopupChanged(IJSObjectReference grid, int opening, bool open)
    {
        if (!_disposed && _popover?.Opening == opening)
            _ = Sent(grid.InvokeVoidAsync("innerPopupChanged", opening, open));
    }

    /// <summary>The popover of opening <paramref name="opening"/> closed in the browser, however it closed.</summary>
    internal void PopoverClosed(int opening)
    {
        if (_popover?.Opening == opening)
            _popover = null;
        if (_popoverShown?.Opening == opening)
            _popoverShown = null;
    }

    /// <summary>The page rendered: the popover's contents read its state, and are drawn again.</summary>
    private void RedrawPopover()
    {
        foreach (var contents in _popoverContents)
            contents.Redraw();
    }

    /// <summary>The popover's slot for its opening: contents drawn on their own (<see cref="PopoverContents"/>).</summary>
    private RenderFragment PopoverFragment(PopoverSlot slot) => builder =>
    {
        builder.OpenComponent<PopoverContents>(0);
        builder.AddComponentParameter(1, nameof(PopoverContents.Opening), slot.Opening);
        builder.AddComponentParameter(2, nameof(PopoverContents.Draw), _drawPopover ??= DrawPopover);
        builder.AddComponentParameter(3, nameof(PopoverContents.Standing), _popoverContentsStanding ??= PopoverContentsStanding);
        builder.CloseComponent();
    };

    /// <summary>The contents the popover's slot draws for <paramref name="opening"/>: none once another opening stands.</summary>
    private RenderFragment DrawPopover(int opening)
        => _popover is { } standing && standing.Opening == opening ? standing.Content(standing.Context) : static _ => { };

    private void PopoverContentsStanding(PopoverContents contents, bool standing)
    {
        if (standing)
            _popoverContents.Add(contents);
        else
            _popoverContents.Remove(contents);
    }

    /// <summary>
    /// Runs <paramref name="handling"/>, the handler of an input on a control of the Consumer's that the grid draws —
    /// in the Consumer's popover (<see cref="OpenPopoverAsync"/>) or a Template cell — in its place in the Input Order
    /// (ADR-0240 section 6, ADR-0441 section 4): once every input made before it has been answered, and before any made
    /// after it. The control names the event in its <c>data-ex-claims</c> attribute (<c>click</c>, <c>dblclick</c>,
    /// <c>contextmenu</c>, <c>mousedown</c>, <c>keydown</c>, <c>submit</c>, <c>focus</c>, space-separated): the browser
    /// numbers such an event as it happens, and the inputs after it wait until the handler that hears it calls this, with
    /// the event's kind, as it starts, so a handler that declares an event calls this every time it hears it, before
    /// its first <c>await</c>: the number is let go once the event's dispatch is over, whether or not a handler heard it,
    /// and a call made after that takes a place of its own. What the
    /// contents' draft holds — a field typed into, a box ticked — is not an input and needs no call.
    ///
    /// <para>An input whose number was not told — a control that does not declare the event, a call from the
    /// Consumer's own code — runs as <see cref="InOrderAsync(Func{Task})"/> runs it: after every input received so far,
    /// or at once inside work or a handler of the grid's that holds the inputs after it.
    /// <see cref="GridInputKind.KeyboardIn"/> and <see cref="GridInputKind.KeyboardOut"/> are inputs only as a product's
    /// Input Root around the grid tells them, and this grid tells none: their handling does not run.</para>
    /// </summary>
    /// <param name="kind">The event the handler hears.</param>
    /// <param name="handling">The handler's work: the inputs after it wait until it has finished.</param>
    /// <returns>A task that completes once the work has, with its failure where it failed; at once, with nothing done,
    /// once the grid is disposed.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is no kind of input.</exception>
    public Task InOrderAsync(GridInputKind kind, Func<Task> handling)
    {
        ArgumentNullException.ThrowIfNull(handling);
        var claimed = ClaimKindOf(kind);
        if (claimed is null || _disposed)
            return Task.CompletedTask;
        return ClaimedAsync(claimed, handling);
    }

    private async Task ClaimedAsync(string kind, Func<Task> handling)
    {
        // Inside an input under way the work is part of it; a telling standing is another event's, left for its own
        // handler.
        if (InputsHeldByCaller() || TurnCalledFrom() is not null || _claimTold is not { } told)
        {
            await InOrderAsync(handling);
            return;
        }
        if (!string.Equals(told.Kind, kind, StringComparison.Ordinal))
        {
            // Told of another kind, that number is no handler's: its turn ends, and this work takes a place of its own.
            VoidUntakenClaim();
            await InOrderAsync(handling);
            return;
        }
        _claimTold = null;
        if (!_claims.TryGetValue(told.Number, out var claim))
            throw new InvalidOperationException($"Internal: the browser told the claim {told.Number}, which is not held.");
        var work = new Work(_ => handling(), null) { Ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) };
        claim.SetResult(work);
        await work.Ended.Task;
        if (work.Failure is { } failure)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(failure);
    }

    /// <summary>The browser's name of the event <paramref name="kind"/> names; none for a crossing of the keyboard,
    /// which this grid never tells.</summary>
    private static string? ClaimKindOf(GridInputKind kind) => kind switch
    {
        GridInputKind.Click => ClaimKinds.Click,
        GridInputKind.DoubleClick => ClaimKinds.DoubleClick,
        GridInputKind.ContextMenu => ClaimKinds.ContextMenu,
        GridInputKind.Press => ClaimKinds.Press,
        GridInputKind.KeyDown => ClaimKinds.KeyDown,
        GridInputKind.Submit => ClaimKinds.Submit,
        GridInputKind.Focus => ClaimKinds.Focus,
        GridInputKind.KeyboardIn or GridInputKind.KeyboardOut => null,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a kind of input the grid numbers (ADR-0240)."),
    };

    /// <summary>The browser numbered an event of <paramref name="kind"/> on a control of the Consumer's that claims it,
    /// just before Blazor dispatches it: the handler that hears it next takes the number. A telling no handler took is
    /// void, its event never having come.</summary>
    internal void ClaimTold(string kind, int number)
    {
        VoidUntakenClaim();
        _claims[number] = new TaskCompletionSource<Work?>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (_disposed)
            _claims[number].SetResult(null);
        else
            _claimTold = (kind, number);
    }

    /// <summary>The dispatch of the event numbered <paramref name="number"/> is over: no handler took it while it ran, so
    /// none will, stopped on its way or inside Blazor's dispatch, and its turn ends.</summary>
    internal void ClaimPassed(int number)
    {
        if (_claimTold is { } told && told.Number == number)
            VoidUntakenClaim();
    }

    private void VoidUntakenClaim()
    {
        if (_claimTold is not { } told)
            return;
        _claimTold = null;
        // Kept until the browser reaches its turn, which then ends.
        if (_claims.TryGetValue(told.Number, out var claim))
            claim.SetResult(null);
    }

    /// <summary>The browser reached the turn of the claim <paramref name="number"/>: the work its handler handed runs,
    /// and the browser hears <c>landed</c> or <c>failed</c>, or <c>void</c> where no handler handed any, or the grid is
    /// disposed.</summary>
    internal async Task<string> RunClaimAsync(int number)
    {
        if (!_claims.TryGetValue(number, out var claim))
        {
            // Disposing let every claim go, and a turn the browser reached meanwhile runs nothing.
            if (_disposed)
                return "void";
            throw new InvalidOperationException($"Internal: the browser ran the claim {number}, which was never told.");
        }
        var work = await claim.Task;
        _claims.Remove(number);
        if (work is null)
            return "void";
        _works[number] = work;
        try
        {
            return await RunWorkAsync(number);
        }
        finally
        {
            _works.Remove(number);
            work.Ended?.TrySetResult();
        }
    }

    /// <summary>The grid is disposed: no claim is waited for, and the work handed to one not yet run ends with nothing
    /// done.</summary>
    private void VoidClaims()
    {
        _claimTold = null;
        foreach (var claim in _claims.Values)
        {
            if (!claim.TrySetResult(null) && claim.Task.Result is { } handed)
                handed.Ended?.TrySetResult();
        }
        _claims.Clear();
    }
}

/// <summary>The Consumer's popover's slot, as the script mounts it: the opening it is drawn for (ADR-0441 section 4).</summary>
/// <param name="Opening">The opening's number.</param>
internal sealed record PopoverSlot(int Opening)
{
    /// <summary>The slot's name, as the script names it.</summary>
    public const string Name = "consumerPopover";

    /// <summary>The slot the script's data name.</summary>
    public static PopoverSlot Of(string context)
    {
        using var document = System.Text.Json.JsonDocument.Parse(context);
        return new PopoverSlot(document.RootElement.GetProperty("opening").GetInt32());
    }
}

/// <summary>The Consumer's popover's contents in its slot (ADR-0441 section 4): drawn with the grid, and drawn again on
/// their own with each render of the page, whose state they read, so the grid's other slots are not drawn for it.</summary>
internal sealed class PopoverContents : ComponentBase, IDisposable
{
    /// <summary>The opening the slot was mounted for.</summary>
    [Parameter]
    public int Opening { get; set; }

    /// <summary>The contents for an opening, as the popover now stands.</summary>
    [Parameter]
    public Func<int, RenderFragment>? Draw { get; set; }

    /// <summary>Told the contents as they come (<c>true</c>) and go (<c>false</c>).</summary>
    [Parameter]
    public Action<PopoverContents, bool>? Standing { get; set; }

    /// <summary>The page rendered: the contents are drawn again.</summary>
    internal void Redraw() => StateHasChanged();

    /// <inheritdoc />
    protected override void OnInitialized()
        => (Standing ?? throw new InvalidOperationException("Internal: popover contents drawn without the grid's Standing.")).Invoke(this, true);

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
        => builder.AddContent(0, (Draw ?? throw new InvalidOperationException("Internal: popover contents drawn without the grid's Draw.")).Invoke(Opening));

    /// <inheritdoc />
    public void Dispose() => Standing?.Invoke(this, false);
}
