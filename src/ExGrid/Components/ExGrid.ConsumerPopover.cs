using System.Globalization;
using ExGrid.Chrome;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
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
    // and meanwhile it is where the opening found it — on a control of the page's, where the
    // Consumer's own button opened the popover. Shift+Tab from a control after the grid lands on the
    // trailing sentinel, from behind. Opened in a menu's place, the root has it meanwhile, and the
    // key gate holds the keys for the contents, a Shift+Tab among them dropped (ADR-0039, 2026-10-13).
    private bool _consumerContentsHeld;

    private sealed record ConsumerPopover(RenderFragment<GridPopoverContext> Content, string Label, Func<Task>? ClosedByEscape = null);

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
    ///
    /// <para>The keys typed on the grid while it opens — from a command of the grid's menus, which
    /// it replaces, as from the grid's own keys — are held, in order, and handed to the contents
    /// once they hold the keyboard; a Tab or Shift+Tab among them is dropped with the keys behind it,
    /// since only the browser can act on one (ADR-0039, ADR-0010).</para>
    /// </summary>
    /// <param name="content">The contents, rendered with the frame's context.</param>
    /// <param name="label">The popover's accessible name, in the Consumer's words: the frame is a
    /// <c>dialog</c>, and the grid holds no sentence of its own (ADR-0036/0039).</param>
    /// <returns>Whether it opened: false while an edit is open, which a popover would otherwise
    /// take the keyboard from with its text uncommitted, and once the grid is disposed.</returns>
    /// <exception cref="ArgumentException"><paramref name="label"/> is empty: a dialog that names
    /// nothing says nothing to assistive technology (ADR-0033).</exception>
    public Task<bool> OpenPopoverAsync(RenderFragment<GridPopoverContext> content, string label)
        => OpenPopoverCoreAsync(content, label, null);

    /// <summary>
    /// Shows a Consumer's own contents in the grid's popover frame, as
    /// <see cref="OpenPopoverAsync(RenderFragment{GridPopoverContext}, string)"/> does, and tells the
    /// Consumer when Escape closes it (ADR-0050 item 16's note of 2026-10-12): a list that goes back a
    /// level, as ExSheet's toolbar lists go back to the KeyTips that opened them (ADR-0100).
    ///
    /// <para><paramref name="closedByEscape"/> is called on the renderer's context as Escape closes the
    /// popover, before the grid takes the keyboard back to its root. The keys typed after that Escape
    /// are held until the popover is gone and the keyboard has landed, and a hand-off the Consumer tells
    /// before its first await — <see cref="HandKeyboardToFrameAsync()"/>, then
    /// <see cref="HandKeyboardToAsync(ElementReference)"/> — is where they land, in order. Told nothing, they are the
    /// root's. It is not called when the popover closes any other way: a press elsewhere, the box
    /// shrinking, the contents' own <see cref="GridPopoverContext.Close"/>, another popover opened in
    /// its place, or <see cref="ClosePopoverAsync"/>.</para>
    /// </summary>
    /// <param name="content">The contents, rendered with the frame's context.</param>
    /// <param name="label">The popover's accessible name, in the Consumer's words.</param>
    /// <param name="closedByEscape">Called as Escape closes the popover.</param>
    /// <returns>Whether it opened: false while an edit is open, and once the grid is disposed.</returns>
    /// <exception cref="ArgumentException"><paramref name="label"/> is empty.</exception>
    public Task<bool> OpenPopoverAsync(RenderFragment<GridPopoverContext> content, string label, Func<Task> closedByEscape)
    {
        ArgumentNullException.ThrowIfNull(closedByEscape);
        return OpenPopoverCoreAsync(content, label, closedByEscape);
    }

    private async Task<bool> OpenPopoverCoreAsync(RenderFragment<GridPopoverContext> content, string label, Func<Task>? closedByEscape)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        // On the renderer's own context, as every request of the Consumer's is.
        var opened = false;
        await InvokeAsync(() => opened = OpenConsumerPopover(content, label, closedByEscape));
        return opened;
    }

    /// <summary>
    /// Escape closes the Consumer's popover (ADR-0050 item 16's note of 2026-10-12): the Consumer's
    /// own word on it, if it asked for one, is started before the grid takes the keyboard back, so
    /// that a hand-off it tells before its first await reaches the key gate first. Answers what
    /// remains of it, for the core to await once the popover has closed.
    /// </summary>
    private Task? ConsumerPopoverEscaping()
    {
        if (_consumerPopover?.ClosedByEscape is not { } escaped)
            return null;
        try
        {
            return escaped();
        }
        catch (Exception ex)
        {
            return Task.FromException(ex);
        }
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
    /// Chrome calls it once its frame has closed, so that the next arrow moves the Focus again; or,
    /// from a surface joined to the grid's order (<see cref="JoinSurfaceAsync"/>), as it closes, before
    /// it goes: the keyboard there is the instance's, and is granted back from it (ADR-0240 section 6).
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
    /// The core's focus function for a frame of the Consumer's own, outside the grid, that holds the
    /// keyboard and gives it back — a toolbar, a list of its own (ADR-0050 item 16's note of
    /// 2026-10-12, the mirror of <see cref="HandKeyboardToFrameAsync()"/>) — with the keys typed on the
    /// frame on the way. The keyboard goes back to the grid's root, to its Keyboard Field on a grid that
    /// edits (ADR-0080), granted where <see cref="ReturnKeyboardAsync()"/> is granted and also while
    /// DOM focus is still on <paramref name="from"/> or inside it: the frame need not first give the
    /// keyboard up to nothing, where a key typed reaches no grid at all.
    ///
    /// <para>On Blazor Server the request lands a round trip after the call, and the keys typed on the
    /// frame meanwhile reach the Consumer's handlers, not the grid. The Consumer holds them, in order,
    /// from the moment it asks, and <paramref name="typedOnTheWay"/> hands them over: the grid asks for
    /// them once the browser has taken the keyboard back, behind every key the frame heard before that
    /// moment, so the list is whole. They are then the grid's, ahead of any key typed on the root since,
    /// each meaning what it means there (ADR-0010). Declined — the user has put the keyboard elsewhere
    /// meanwhile (ADR-0018) — the grid still asks, so the frame stops holding, and drops them. Before
    /// the grid is attached to the page, and after it is gone, it asks at once and drops them.</para>
    ///
    /// <para>A frame inside the product's Input Root that claims its keys (<c>data-ex-claims</c>), and puts
    /// each in the grid's order through <see cref="InOrderAsync(GridInputKind, Func{Task})"/>, holds none: the grid holds the keys
    /// typed after the one that gave the keyboard back until that one has been answered, and hands them
    /// on where the keyboard landed (ADR-0240 section 6). Its <paramref name="typedOnTheWay"/> then
    /// answers only the keys it held for some other reason.</para>
    ///
    /// <para>It moves neither the Focus nor the Selection, and scrolls nothing. It adds no JavaScript:
    /// it is the grid's own hand-back, whose condition is widened to the frame (ADR-0021).</para>
    /// </summary>
    /// <param name="from">The frame's element that holds the keyboard, or that holds the element that
    /// does.</param>
    /// <param name="typedOnTheWay">Called once, on the renderer's context: returns the keys typed on the
    /// frame since the Consumer asked, in order, and stops holding them.</param>
    /// <returns>A task that completes once the browser has answered the hand-back and the held keys
    /// have been collected. The Consumer can then remove the frame and clear its queue.</returns>
    public Task ReturnKeyboardAsync(ElementReference from, Func<IReadOnlyList<KeyboardEventArgs>> typedOnTheWay)
    {
        ArgumentNullException.ThrowIfNull(typedOnTheWay);
        return InvokeAsync(async () =>
        {
            if (_disposed || _scrollHandle is null)
            {
                typedOnTheWay();
                return;
            }
            var ticket = ++_takeBacks;
            _typedOnTheWay[ticket] = typedOnTheWay;
            try
            {
                // A frame not yet drawn has no element to name: the root's own condition stands.
                await _scrollHandle.InvokeVoidAsync("takeKeyboardBack", string.IsNullOrEmpty(from.Id) ? null : from, ticket);
            }
            catch (Exception ex) when (ex is JSException or JSDisconnectedException or ObjectDisposedException or OperationCanceledException)
            {
                if (_typedOnTheWay.Remove(ticket, out var typed))
                    typed();
            }
        });
    }

    // The frames giving the keyboard back (ReturnKeyboardAsync with a frame), by the ticket the key
    // gate answers with, until it asks for the keys typed on them.
    private readonly Dictionary<int, Func<IReadOnlyList<KeyboardEventArgs>>> _typedOnTheWay = [];
    private int _takeBacks;

    /// <summary>
    /// The key gate has taken the keyboard back from a Consumer's frame, or been declined it
    /// (<see cref="ReturnKeyboardAsync(ElementReference, Func{IReadOnlyList{KeyboardEventArgs}})"/>):
    /// the keys typed on the frame on the way, which it holds and hands on as the root's.
    ///
    /// <para>Called by the grid's own script module and not for Consumers: it is public
    /// only because JavaScript interop requires it.</para>
    /// </summary>
    /// <param name="ticket">The hand-back the gate answers.</param>
    /// <returns>The keys, in order; none for a hand-back already answered.</returns>
    /// <remarks>Not the contract (ADR-0201): the grid's script calls it, so it is public, but it is
    /// hidden from a Consumer's editor and may change in any version.</remarks>
    [JSInvokable, System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public IReadOnlyList<KeyboardEventArgs> KeysTypedOnTheWay(int ticket)
        => _typedOnTheWay.Remove(ticket, out var typed) ? typed() : [];

    /// <summary>
    /// Hands the keyboard this grid holds on to a control of the Consumer's own, outside the grid —
    /// the first control of a frame or a tab the Consumer has opened from it, a dialog's Close for
    /// one (ADR-0070's note of 2026-10-02) — or inside it, a Template cell's control answering
    /// <see cref="Cells.TemplateCellContext{TRow}.FocusRequest"/> (ADR-0204) — in place of that
    /// control's own <c>FocusAsync</c>. It
    /// is granted only while the keyboard is still this grid's: DOM focus inside the root or on
    /// nothing, the condition every hand-back reads (ADR-0021's note of 2026-09-30).
    ///
    /// <para>On Blazor Server the request lands a round trip after the render that drew the
    /// control. A press the user made in that time — on the frame's own content, another grid, a
    /// control of the page's — keeps the keyboard where it put it (ADR-0018), so where the keyboard
    /// ends up does not depend on how long the round trip took. A field of the grid's own beside
    /// the rows, the Formula Bar or the Name Box, keeps it too, as from every hand-back.</para>
    ///
    /// <para>It moves neither the Focus nor the Selection, and renders nothing. The control scrolls
    /// into view, as its own focus would have had it. It reads where DOM focus is and nothing of
    /// the layout: the sixth decision about focus made in script (ADR-0021). Before the grid is
    /// attached to the page, and after it is gone, there is no script of the grid's to read it, and
    /// the control takes the keyboard by its own <c>FocusAsync</c>, as it would without the grid. A
    /// request the page cannot carry out, with the control or the circuit gone, is dropped.</para>
    /// </summary>
    /// <param name="control">The element to hand the keyboard to. Once the grid is attached, a
    /// non-focusable container names its first enabled text field, with that field's value selected
    /// before held keys arrive; without a text field, its first enabled control. A control already
    /// chosen by the user keeps the keyboard (ADR-0039).</param>
    /// <returns>A task that completes once the request has been made.</returns>
    public Task HandKeyboardToAsync(ElementReference control) => HandKeyboardToAsync(control, preventScroll: false);

    /// <summary>
    /// <see cref="HandKeyboardToAsync(ElementReference)"/>, with the control taking the keyboard where it
    /// stands when <paramref name="preventScroll"/> is true: nothing scrolls to bring it into view, as
    /// <c>FocusAsync(preventScroll: true)</c> has it — for a control that is already where the user is
    /// looking, such as the next tab of a tab list the arrow keys move along. The same request otherwise,
    /// granted under the same condition; before the grid is attached, and after it is gone, the control's
    /// own <c>FocusAsync</c> is asked the same way.
    /// </summary>
    /// <param name="control">The element to hand the keyboard to, as for <see cref="HandKeyboardToAsync(ElementReference)"/>.</param>
    /// <param name="preventScroll">Whether the control takes the keyboard without scrolling into view.</param>
    /// <returns>A task that completes once the request has been made.</returns>
    public Task HandKeyboardToAsync(ElementReference control, bool preventScroll)
        => InvokeAsync(async () =>
        {
            try
            {
                if (_disposed || _scrollHandle is null)
                    await control.FocusAsync(preventScroll);
                else
                    await _scrollHandle.InvokeVoidAsync("handKeyboardTo", control, false, preventScroll);
            }
            catch (Exception ex) when (ex is JSException or JSDisconnectedException or ObjectDisposedException or OperationCanceledException)
            {
            }
        });

    /// <summary>
    /// The keyboard is going to a frame of the Consumer's own, outside the grid (ADR-0050 item 16
    /// and ADR-0039, notes of 2026-10-01): a dialog the Consumer is opening, whose control will
    /// take DOM focus once it is drawn — a round trip or more later on a circuit. Called as the
    /// Consumer opens it: from a command of the grid's menus, a declared key, or its own code.
    ///
    /// <para>Until DOM focus has left the grid's root, the keys typed on the root, its Keyboard Field
    /// (ADR-0080) or the menu the command ran from are held, in order, and then handed to the
    /// element that took focus, as the keydown each would have been; a Tab, which only the browser
    /// can act on, is refused aloud with every key after it (ADR-0010, ADR-0240). The keys are never
    /// the grid's: a digit gated against the root would open an edit behind the frame. They wait for
    /// the frame however long it takes, never on a clock (ADR-0240 section 4): the call is a promise
    /// that the keyboard is coming. If DOM focus goes to another grid instead, the held keys are
    /// refused aloud (<see cref="Chrome.KeyRefusalLabelIds.KeyboardDidNotArrive"/>). A command's menu still hands the keyboard back to the root — its Keyboard Field on
    /// a grid that edits — as it closes, so that the keys typed meanwhile land where they are held,
    /// not on nothing, where no grid hears them.</para>
    ///
    /// <para>A popover the Consumer opens in the grid's own frame (<see cref="OpenPopoverAsync(RenderFragment{GridPopoverContext}, string)"/>)
    /// needs no call: the grid hands the keyboard to it itself.</para>
    /// </summary>
    public Task HandKeyboardToFrameAsync()
        => InvokeAsync(async () =>
        {
            if (_disposed)
                return;
            await HandOffAsync("frame");
        });

    /// <summary>
    /// <see cref="HandKeyboardToFrameAsync()"/>, with the keyboard waiting on <paramref name="waitingAt"/>
    /// until the frame's control takes it (ADR-0240 section 6, ticket 06): an element of the frame's own
    /// inside the product's Input Root or a surface joined to it (<see cref="JoinSurfaceAsync"/>), marked
    /// <c>data-ex-claims=""</c>, that takes DOM focus now. A frame needs one when its design system hands
    /// the keyboard back, as it closes, to whatever held it as it opened: a MudDialog's does, and handed
    /// back to the page's button that opened it, the keyboard would leave the Sheet it had just come back
    /// to. Waiting on an element that goes once the frame's control has the keyboard, the dialog hands it
    /// back to nothing.
    ///
    /// <para>The keys typed on the element are held with those typed on the grid, and handed to the frame's
    /// control, in order, when it takes the keyboard through <see cref="HandKeyboardToAsync(ElementReference)"/>,
    /// as they are without one. The element takes the keyboard only while the keyboard is still the
    /// instance's, or still on the control it was on when the hand-off was told — the Consumer's button that
    /// opened the frame. Told while no hand-off to a frame stands, it tells one first.</para>
    /// </summary>
    /// <param name="waitingAt">The frame's own element the keyboard waits on.</param>
    /// <returns>A task that completes once the request has been made.</returns>
    public Task HandKeyboardToFrameAsync(ElementReference waitingAt)
        => InvokeAsync(async () =>
        {
            if (_disposed || _scrollHandle is null)
                return;
            try
            {
                // The script tells the hand-off first when none to a frame stands.
                await _scrollHandle.InvokeVoidAsync("waitForFrameAt", waitingAt);
            }
            catch (Exception ex) when (ex is JSException or JSDisconnectedException or ObjectDisposedException or OperationCanceledException)
            {
            }
        });

    /// <summary>
    /// A surface a design system draws outside the product's Input Root — a <c>MudDialog</c> under its
    /// provider, a <c>MudPopover</c> at the page's body — joins this grid's Input Order while it is open
    /// (ADR-0240 section 6, ticket 06). The surface is <paramref name="element"/>, or, with
    /// <paramref name="around"/>, the nearest element around it that the CSS selector matches: the box the
    /// design system draws around the Chrome's content and gives it no reference to, such as a MudDialog's
    /// <c>[role="dialog"]</c>.
    ///
    /// <para>Joined, the surface is heard as the Input Root is. A click, a press or a form's submission on a
    /// control of it that claims the event (<c>data-ex-claims</c>) is numbered and handled in its place in the
    /// order (<see cref="InOrderAsync(GridInputKind, Func{Task})"/>); so is a key on a control that claims
    /// every key (<c>keydown</c>) or that key alone (<c>keydown:Enter</c>), told as it reaches that control.
    /// An Escape typed in it that no control claims, and none inside keeps for itself — a dropdown closing its
    /// open list keeps its Escape —, is numbered too, and <paramref name="escape"/> is handled in its place:
    /// the surface's own Cancel, wherever in it the keyboard stands, its title and its empty space included.
    /// Any other key goes as it comes: a draft's keys are the draft's (ADR-0240 section 1). While an input is
    /// still being answered, every key typed in the surface is held, and handed on in its turn where that
    /// answer left the keyboard: an arrow typed in a dialog as its Escape closes it reaches the Sheet, as
    /// typed slowly.</para>
    ///
    /// <para>The keyboard on the surface is the instance's: the grid hands it into the surface
    /// (<see cref="HandKeyboardToAsync(ElementReference)"/>) and takes it back to its root from there, so a
    /// surface that closes in an input's turn gives the keyboard back before it goes, and the design system's
    /// own hand-back on closing finds it given. Moving between the surface and the Input Root crosses nothing
    /// (<see cref="GridInputKind.KeyboardIn"/>).</para>
    ///
    /// <para>A surface inside the Input Root is heard there already, one around the grid is not a surface,
    /// and one joined already stays that membership's: none of them joins, and the membership answered does
    /// nothing. Without <paramref name="escape"/>, an Escape typed in the surface goes as it comes. The surface leaves when the
    /// membership is disposed, when the surface leaves the page, or when the grid is disposed. Before the
    /// grid is attached to the page there is nothing to join.</para>
    /// </summary>
    /// <param name="element">An element of the surface, or the surface itself.</param>
    /// <param name="around">A CSS selector naming the surface around <paramref name="element"/>; null for
    /// <paramref name="element"/> itself.</param>
    /// <param name="escape">What an Escape typed in the surface does, in its turn; null for nothing.</param>
    /// <returns>The surface's membership, disposed as it closes, once the keyboard has left it.</returns>
    public async Task<IAsyncDisposable> JoinSurfaceAsync(ElementReference element, string? around = null, Func<Task>? escape = null)
    {
        IAsyncDisposable joined = NoSurface.Instance;
        await InvokeAsync(async () =>
        {
            if (_disposed || _scrollHandle is null)
                return;
            var id = ++_surfaceCount;
            _surfaces[id] = escape;
            try
            {
                // Refused where nothing joins, or where the surface is another membership's already.
                if (await _scrollHandle.InvokeAsync<bool>("joinSurface", element, around, id, escape is not null))
                    joined = new Surface(this, id);
                else
                    _surfaces.Remove(id);
            }
            catch (Exception ex) when (ex is JSException or JSDisconnectedException or ObjectDisposedException or OperationCanceledException)
            {
                _surfaces.Remove(id);
            }
        });
        return joined;
    }

    // The joined surfaces' Escapes, by the number the script knows each by (JoinSurfaceAsync).
    private readonly Dictionary<int, Func<Task>?> _surfaces = [];
    private int _surfaceCount;

    /// <summary>
    /// An Escape typed in the surface joined as <paramref name="surface"/>, numbered <paramref name="input"/>
    /// by the key gate (ADR-0240 section 6, ticket 06): the surface's Escape, handled in its place in the
    /// Input Order; nothing for a surface that has left.
    /// </summary>
    /// <remarks>Not the contract (ADR-0201): the grid's script calls it, so it is public, but it is
    /// hidden from a Consumer's editor and may change in any version.</remarks>
    /// <param name="surface">The number the surface joined by.</param>
    /// <param name="input">The number the script gave the key, which the root names once it has been
    /// answered (ADR-0220).</param>
    /// <param name="fieldText">The browser field's text carried by this input (ADR-0240 section 5).</param>
    /// <returns>A task that completes once the key is answered.</returns>
    [JSInvokable, System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public Task OnSurfaceEscapeAsync(int surface, int input = 0, GridInputText? fieldText = null)
        => AnsweredAsync(() => !_disposed && _surfaces.GetValueOrDefault(surface) is { } escape ? escape() : Task.CompletedTask,
            input, fieldText);

    private Task LeaveSurfaceAsync(int id)
        => InvokeAsync(async () =>
        {
            if (!_surfaces.Remove(id) || _disposed || _scrollHandle is null)
                return;
            try
            {
                // By its number: the surface may have left the page already, where no reference reaches it.
                await _scrollHandle.InvokeVoidAsync("leaveSurface", id);
            }
            catch (Exception ex) when (ex is JSException or JSDisconnectedException or ObjectDisposedException or OperationCanceledException)
            {
            }
        });

    // A joined surface's membership: disposed, the surface leaves.
    private sealed class Surface(ExGrid<TRow> grid, int id) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => new(grid.LeaveSurfaceAsync(id));
    }

    // The membership of a surface that joined nothing.
    private sealed class NoSurface : IAsyncDisposable
    {
        public static readonly NoSurface Instance = new();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

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

    /// <summary>
    /// The key gate heard that the keyboard is going to a Consumer's popover, and numbered that telling
    /// <paramref name="input"/> (ADR-0240 section 4). Answered in its place in the Input Order, after
    /// the opening that told it: the Settled Stamp that answers it then says whether the popover's
    /// contents still owe the keyboard, which the stamp read as the telling arrived did not yet say.
    /// </summary>
    /// <remarks>Not the contract (ADR-0201): the grid's script calls it, so it is public, but it is
    /// hidden from a Consumer's editor and may change in any version.</remarks>
    /// <param name="input">The number the script gave the telling, which the root names once it has
    /// been answered (ADR-0220).</param>
    /// <returns>A task that completes once the telling is answered.</returns>
    /// <param name="fieldText">The browser field's text carried by this input (ADR-0240 section 5).</param>
    [JSInvokable, System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public Task HandOffHeardAsync(int input = 0, GridInputText? fieldText = null) => AnsweredAsync(() => { }, input, fieldText);

    /// <summary>
    /// Opens the Consumer's contents in the frame, in place of any popover that stands.
    ///
    /// <para>In a popover's place — from a command of the Context Menu or a column's menu, ExSheet's
    /// Format Cells… among them, or from a control inside another popover that is still standing —
    /// the core closes that popover itself, and so asks for the root's focus ahead of the
    /// render that swaps it for the contents, as <see cref="ClosePopoverAndReturnFocus"/> does
    /// (ADR-0039, note of 2026-10-13). That render removes the element holding DOM focus, and the
    /// contents take it a round trip later on a circuit. In between, DOM focus was on <c>body</c>,
    /// where the capture-phase listener hears nothing, and whether a key typed there was lost turned
    /// on the round trip. Asked after the key gate is told the keyboard is going to the popover, the
    /// root takes those keys with the hold already standing: they are held, in order, and handed to
    /// the contents once they hold the keyboard; a Tab or Shift+Tab among them is dropped with the
    /// keys behind it (ADR-0010), so one typed in the gap no longer enters the contents at their
    /// last control from behind. Nothing is asked after the render: the contents' own focus has the
    /// last word. Opened over no popover, nothing is replaced, and the keyboard is left where the
    /// opening found it.</para>
    /// </summary>
    private bool OpenConsumerPopover(RenderFragment<GridPopoverContext> content, string label, Func<Task>? closedByEscape = null)
    {
        if (_disposed || _editMode != EditMode.None)
            return false;
        var replaces = AnyPopoverOpen;
        CloseMenus();
        _consumerPopover = new ConsumerPopover(content, label, closedByEscape);
        BeginPopoverFocus();
        // The contents are the Consumer's, and focus themselves on their count: until they have, the
        // keyboard is owed to them, and the keys held for them wait for it (FocusOwed, ADR-0240 section
        // 4). Not owed, a key the hold handed on before their focus landed was refused as a keyboard
        // that did not arrive: Alt, H, F, C, Escape, 1 typed at once lost the Escape and the 1.
        _popoverFocusPending = PopoverFocus.None;
        _chromeFocusOwed = false;
        _consumerFocusOwed = true;
        _consumerFocusRequest++;
        _consumerFocusLastRequest = 0;
        _consumerContentsHeld = false;
        // The keys typed until the contents hold the keyboard are theirs (ADR-0039, 2026-10-01):
        // told before the render that draws them, and before the answer to a key that opened them.
        _ = HandOffAsync("popover");
        // Then the root, while the popover it replaces still holds DOM focus (2026-10-13).
        if (replaces)
            _ = ReclaimFocusAsync();
        _suppressRender = false;
        StateHasChanged();
        return true;
    }

    /// <summary>The frame's context, for the contents (ADR-0050, item 16).</summary>
    private GridPopoverContext BuildConsumerPopoverContext()
        => new(CloseFromChrome, _consumerFocusRequest, _consumerFocusLastRequest, InnerPopupChangedFromChrome)
        {
            TakeFocus = TakeConsumerPopoverFocus,
            InOrder = InOrderHandler,
        };

    /// <summary>The focus function a Consumer's popover is handed (<see cref="GridPopoverContext.TakeFocus"/>):
    /// the control takes DOM focus unless a press inside the popover has overtaken its opening
    /// (ticket 240). Not the editor's question, which a Built-in popover's focus asks: a Consumer's
    /// popover may open from a control of the page's, outside the grid.</summary>
    private Func<ElementReference, bool, Task> TakeConsumerPopoverFocus
        => _takeConsumerPopoverFocus ??= (control, preventScroll) => InvokeAsync(async () =>
        {
            // A popover closed since has nothing to focus: its control would take the keyboard from
            // the root for the moment before the render that removes it, and leave it on the page.
            if (control.Id is null || _disposed || _consumerPopover is null)
            {
                ConsumerFocusTaken();
                return;
            }
            try
            {
                if (_scrollHandle is null)
                    await control.FocusAsync(preventScroll);
                else
                    await _scrollHandle.InvokeVoidAsync("takeFocus", control, preventScroll, true);
            }
            catch (Exception ex) when (ex is JSException or JSDisconnectedException or ObjectDisposedException or OperationCanceledException)
            {
            }
            // No longer owed once the focus has been asked of the script, never before: the stamp that
            // says so travels behind it. Declined (a press elsewhere has overtaken the opening), the
            // contents will not take it.
            ConsumerFocusTaken();
        });

    // The contents' focus is no longer owed; the Settled Stamp says so (ADR-0240 section 4).
    private void ConsumerFocusTaken()
    {
        if (!_consumerFocusOwed)
            return;
        _consumerFocusOwed = false;
        ShowSettledStamp();
    }

    private Func<ElementReference, bool, Task>? _takeConsumerPopoverFocus;

    /// <summary>The contents hold the keyboard: the sentinels wrap from here on, and the keyboard is
    /// no longer owed to them, however they took it — through <c>TakeFocus</c> or their own focus, as the
    /// toolbar's lists take it (ADR-0240 section 4). Nothing is drawn for it but the stamp.</summary>
    private void ConsumerContentsHeld()
    {
        _consumerContentsHeld = true;
        _suppressRender = true;
        ConsumerFocusTaken();
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
        _consumerFocusOwed = true;
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
        _consumerFocusOwed = true;
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
