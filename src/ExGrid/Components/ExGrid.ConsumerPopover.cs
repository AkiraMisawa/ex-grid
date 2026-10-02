using System.Globalization;
using ExGrid.Chrome;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ExGrid.Components;

// A Consumer's popover (ADR-0050, item 16): the Consumer's own contents in the grid's popover
// frame. The frame is every popover's — placed inside the grid's box and bounded by it, keyed by
// its opening, dismissed by Escape, by a pointer-down elsewhere in the instance and by the box
// shrinking below one row (ADR-0010/0039/0040) — and the core holds no reference to anything the
// contents render: they take DOM focus themselves when their context's count asks. Nothing of it
// stands until the Consumer opens one, so a grid whose Consumer never does is unchanged (DC-1).
public partial class ExGrid<TRow>
{
    // The Consumer's popover while it stands: its contents and its accessible name.
    private ConsumerPopover? _consumerPopover;

    // The requests for the contents' first control (each opening, and Tab wrapping back to it)
    // and for their last (Shift+Tab wrapping back to it, counted from zero at each opening).
    private int _consumerFocusRequest;
    private int _consumerFocusLastRequest;

    // Whether the contents have held the keyboard since this opening. Until they have, a sentinel
    // that takes it was reached from outside the popover, not off its contents' first or last
    // control: on a circuit the contents take the keyboard a round trip after the popover is drawn,
    // and the page has it meanwhile — on nothing, where a command run by a press closed the menu
    // the popover replaced. Shift+Tab there lands on the trailing sentinel, from behind.
    private bool _consumerContentsHeld;

    private sealed record ConsumerPopover(RenderFragment<GridPopoverContext> Content, string Label);

    /// <summary>
    /// Shows a Consumer's own contents in the grid's popover frame (ADR-0050, item 16). The frame
    /// stands under the header band, centred across the grid and inside its box, and is bounded by
    /// the box: contents taller or wider than the room scroll inside it (ADR-0040). It is a popover
    /// by every popover rule: it replaces any popover that stands, the contents take the keyboard
    /// through <see cref="GridPopoverContext.FocusRequest"/> (ADR-0039), Tab and Shift+Tab wrap
    /// inside it, and Escape, a pointer-down elsewhere in the instance and the box shrinking below
    /// one row close it as a Cancel (ADR-0010/0040). However it closes, the keyboard returns to the
    /// root — to its Keyboard Field on a grid that edits (ADR-0080). Calling it again opens new
    /// contents in place of the old.
    /// </summary>
    /// <param name="content">The contents, rendered with the frame's context.</param>
    /// <param name="label">The popover's accessible name, in the Consumer's words: the frame is a
    /// <c>dialog</c>, and the grid holds no sentence of its own (ADR-0036/0039).</param>
    /// <returns>Whether it opened: false while an edit is open, which a popover would otherwise
    /// take the keyboard from with its text uncommitted, and once the grid is disposed.</returns>
    /// <exception cref="ArgumentException"><paramref name="label"/> is empty: a dialog that names
    /// nothing says nothing to assistive technology (ADR-0033).</exception>
    public async Task<bool> OpenPopoverAsync(RenderFragment<GridPopoverContext> content, string label)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        // On the renderer's own context, as every request of the Consumer's is.
        var opened = false;
        await InvokeAsync(() => opened = OpenConsumerPopover(content, label));
        return opened;
    }

    /// <summary>
    /// Closes the Consumer's popover if it stands (ADR-0050, item 16), and the keyboard returns to
    /// the root — to its Keyboard Field on a grid that edits (ADR-0080) — as however else it closes
    /// (ADR-0039). Another popover opened since is not the Consumer's, and stays.
    /// </summary>
    public Task ClosePopoverAsync()
        => InvokeAsync(() =>
        {
            if (_disposed || _consumerPopover is null)
                return;
            ClosePopoverAndReturnFocus();
            _suppressRender = false;
            StateHasChanged();
        });

    /// <summary>
    /// The core's focus function for a Chrome whose frame lies outside the grid (ADR-0010's note
    /// of 2026-09-30, ADR-0071), and a Consumer's for something of its own that held the keyboard
    /// over the grid and goes away — a dialog, a panel, a tab (ADR-0070): the keyboard goes back to
    /// the grid's root, to its Keyboard Field on a grid that edits (ADR-0080), granted only while it
    /// is still this grid's — DOM focus inside the root or on nothing — as every hand-back is
    /// (ADR-0021's note of 2026-09-30). A grid or a control of the page's that the user has moved
    /// to keeps the keyboard (ADR-0018), and so does a field of the grid's own beside the rows, the
    /// Formula Bar or the Name Box, where the user is typing (ADR-0021, widened 2026-09-28). A
    /// Chrome calls it once its frame has closed, so that the next arrow moves the Focus again.
    ///
    /// <para>It moves neither the Focus nor the Selection, and it scrolls nothing: the keyboard
    /// comes back to the cell it left. On Blazor Server the request lands a round trip after the
    /// call, and a click made in that time wins. Before the grid is attached to the page, and after
    /// it is gone, it does nothing. It adds no JavaScript: it is the grid's own hand-back
    /// (ADR-0021).</para>
    /// </summary>
    /// <returns>A task that completes once the request has been made.</returns>
    public Task ReturnKeyboardAsync() => InvokeAsync(() => ReclaimFocusAsync());

    /// <summary>
    /// The keyboard is going to a frame of the Consumer's own, outside the grid (ADR-0050 item 16
    /// and ADR-0039, notes of 2026-10-01): a dialog the Consumer is opening, whose control will
    /// take DOM focus once it is drawn — a round trip or more later on a circuit. Called as the
    /// Consumer opens it: from a command of the grid's menus, a declared key, or its own code.
    ///
    /// <para>Until DOM focus has left the grid's root, the keys typed on the root, its Keyboard Field
    /// (ADR-0080) or the menu the command ran from are held, in order, and then handed to the
    /// element that took focus, as the keydown each would have been; a Tab, which only the browser
    /// can act on, is dropped with every key after it (ADR-0010). The keys are never the grid's: a
    /// digit gated against the root would open an edit behind the frame. If DOM focus does not
    /// leave the root within the hold's fallback, or goes to another grid, the held keys are
    /// dropped. A command's menu still hands the keyboard back to the root — its Keyboard Field on
    /// a grid that edits — as it closes, so that the keys typed meanwhile land where they are held,
    /// not on nothing, where no grid hears them.</para>
    ///
    /// <para>A popover the Consumer opens in the grid's own frame (<see cref="OpenPopoverAsync"/>)
    /// needs no call: the grid hands the keyboard to it itself.</para>
    /// </summary>
    public Task HandKeyboardToFrameAsync()
        => InvokeAsync(async () =>
        {
            if (_disposed)
                return;
            await HandOffAsync("frame");
        });

    /// <summary>Tells the key gate where the keyboard is going (ADR-0039, 2026-10-01): sent before
    /// the key's answer and before the render that closes the menu a command ran from.</summary>
    private async Task HandOffAsync(string to)
    {
        if (_disposed || _scrollHandle is null)
            return;
        try
        {
            await _scrollHandle.InvokeVoidAsync("handOff", to);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or ObjectDisposedException or OperationCanceledException)
        {
        }
    }

    private bool OpenConsumerPopover(RenderFragment<GridPopoverContext> content, string label)
    {
        if (_disposed || _editMode != EditMode.None)
            return false;
        CloseMenus();
        _consumerPopover = new ConsumerPopover(content, label);
        BeginPopoverFocus();
        // The contents are the Consumer's, and focus themselves on their count.
        _popoverFocusPending = PopoverFocus.None;
        _consumerFocusRequest++;
        _consumerFocusLastRequest = 0;
        _consumerContentsHeld = false;
        // The keys typed until the contents hold the keyboard are theirs (ADR-0039, 2026-10-01):
        // told before the render that draws them, and before the answer to a key that opened them.
        _ = HandOffAsync("popover");
        _suppressRender = false;
        StateHasChanged();
        return true;
    }

    /// <summary>The frame's context, for the contents (ADR-0050, item 16).</summary>
    private GridPopoverContext BuildConsumerPopoverContext()
        => new(CloseFromChrome, _consumerFocusRequest, _consumerFocusLastRequest, InnerPopupChangedFromChrome);

    /// <summary>The contents hold the keyboard: the sentinels wrap from here on. Nothing is drawn
    /// for it.</summary>
    private void ConsumerContentsHeld()
    {
        _consumerContentsHeld = true;
        _suppressRender = true;
    }

    /// <summary>
    /// The trailing sentinel takes the keyboard (ADR-0039). Tab off the contents' last control
    /// lands here, and it hands the keyboard back to their first: this handler is the grid's own,
    /// so the render that carries the count follows. Reached before the contents have held the
    /// keyboard, it was entered from behind — Shift+Tab from the page — and the keyboard enters
    /// the contents at their last control, as Shift+Tab would.
    /// </summary>
    private void OnConsumerTrailingSentinel()
    {
        if (_consumerContentsHeld)
            _consumerFocusRequest++;
        else
            _consumerFocusLastRequest++;
    }

    /// <summary>
    /// The leading sentinel takes the keyboard. Shift+Tab off the contents' first control lands
    /// here, and it hands the keyboard on to their last. Reached before the contents have held the
    /// keyboard, it was entered from in front, and the keyboard enters at their first control.
    /// </summary>
    private void OnConsumerLeadingSentinel()
    {
        if (_consumerContentsHeld)
            _consumerFocusLastRequest++;
        else
            _consumerFocusRequest++;
    }

    /// <summary>
    /// Where a Consumer's popover stands (ADR-0040): under the header band, as a column's popover
    /// does, centred across the Viewport and as wide as its contents ask, never wider than the
    /// Viewport — so either edge stays inside the grid's box — and bounded below by the Viewport's
    /// bottom. Nothing is measured: the numbers are the ones the grid is laid out from.
    /// </summary>
    private string ConsumerPopoverStyle()
    {
        var widest = Math.Max(0, _visibleWidthPx);
        var narrowest = Math.Min(PopoverMinWidthPx, widest);
        return string.Create(CultureInfo.InvariantCulture,
            $"left: {widest / 2}px; top: {FormulaBarPx + BandHeightPx}px; transform: translateX(-50%); width: max-content; min-width: {narrowest}px; max-width: {widest}px; max-height: {HeaderPopoverRoomPx}px");
    }
}
