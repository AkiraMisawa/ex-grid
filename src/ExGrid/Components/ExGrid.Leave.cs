using Microsoft.AspNetCore.Components;

namespace ExGrid.Components;

// What a Consumer does with the grid's keyboard (ADR-0069). It gives the keyboard back to the root
// when something of its own that held it over the grid goes away — a dialog, a panel, a tab — and
// it may hear the Escape that leaves the grid, in place of the grid releasing DOM focus.
public partial class ExGrid<TRow>
{
    /// <summary>
    /// A Consumer declaration (ADR-0069): raised by an Escape pressed on the root with nothing left
    /// to dismiss, <b>instead of</b> releasing the grid's DOM focus. The grid keeps the keyboard,
    /// and the Consumer decides what leaving means — closing its dialog, for one — and where the
    /// keyboard goes next. Released first, the keyboard would land on the page's <c>body</c> before
    /// the Consumer could put it anywhere.
    ///
    /// <para>Only the outermost Escape raises it. ADR-0012's layering is unchanged, and each of
    /// these still peels only its own layer: the Escape that closes an Inner Popup, then the
    /// popover that holds it; the one that cancels an edit; the one that closes a Formula Entry's
    /// list; the one that leaves an Interactive cell; and the one that returns from a control
    /// inside a cell to the root. Each press is one dismissal, and this is the last. It is raised
    /// once per press: a held key's repeats raise it no more until the key is released.</para>
    ///
    /// <para>Without a delegate, the default, nothing changes: Escape with nothing left to dismiss
    /// releases the grid's DOM focus (ADR-0012).</para>
    /// </summary>
    [Parameter] public EventCallback OnLeave { get; set; }

    /// <summary>
    /// Gives the grid's root the keyboard back (ADR-0069): what the grid does when one of its own
    /// popovers closes, offered to the Consumer. Call it when something of the Consumer's own that
    /// held the keyboard over the grid goes away — a dialog, a panel, a tab — so that the user's
    /// next key reaches the grid rather than the page.
    ///
    /// <para>The root takes DOM focus only when DOM focus is on nothing (the page's <c>body</c>) or
    /// already inside this grid. A field of the grid's own beside the rows, the Formula Bar or the
    /// Name Box, keeps the keyboard: the user is typing there. Nothing is taken from anywhere else —
    /// not from another control the user chose meanwhile, and not from another grid (ADR-0018). On
    /// Blazor Server the request lands a round trip after the call, and a click made in that time
    /// wins.</para>
    ///
    /// <para>It moves neither the Focus nor the Selection, and it scrolls nothing: the keyboard
    /// comes back to the cell it left. Before the grid is attached to the page, and after it is
    /// gone, it does nothing. It adds no JavaScript: it is the grid's own hand-back (ADR-0021).</para>
    /// </summary>
    /// <returns>A task that completes once the request has been made.</returns>
    public Task ReturnKeyboardAsync()
    {
        // On the renderer's own context, as a placement is: a Consumer calling from outside it
        // must not race a render, nor the attach that sets the handle.
        return InvokeAsync(() => ReclaimFocusAsync(fromField: false));
    }

    /// <summary>
    /// The Escape with nothing left to dismiss, pressed on the root (ADR-0012, ADR-0069): the
    /// grid's way out of Enter/Tab cycling. Declared, the Consumer hears it in place of the grid
    /// releasing DOM focus, once per press; undeclared, the focus is released as before.
    /// </summary>
    /// <param name="repeat">Whether the key is a held key's repeat. A repeat raises nothing, and
    /// releases nothing, where the Consumer listens: the press it repeats has already been heard,
    /// or peeled a layer of its own, and each press is one dismissal.</param>
    private async Task LeaveOrRaiseAsync(bool repeat)
    {
        if (!OnLeave.HasDelegate)
        {
            await LeaveAsync();
            return;
        }
        if (repeat)
            return;
        await OnLeave.InvokeAsync();
    }
}
