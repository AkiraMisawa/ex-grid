using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace ExGrid.Components;

// What a Consumer does with the grid's keyboard (ADR-0070). It gives the keyboard back to the root
// when something of its own that held it over the grid goes away — a dialog, a panel, a tab —
// through ReturnKeyboardAsync, the hand-back a Chrome's frame outside the grid asks for too
// (ADR-0071), which stands beside the Consumer's popover in ExGrid.ConsumerPopover.cs. And it may
// hear the Escape that leaves the grid, which still takes the grid's own way out (ADR-0012).
public partial class ExGrid<TRow>
{
    /// <summary>
    /// A Consumer declaration (ADR-0070): raised by an Escape pressed on the root with nothing left
    /// to dismiss, <b>beside</b> the grid's own way out, which releases Tab (ADR-0012, rewritten
    /// 2026-10-01): the root keeps DOM focus, and the next Tab or Shift+Tab leaves the grid. The
    /// Consumer decides what leaving means — closing its dialog, for one — and may put the keyboard
    /// somewhere first; a Consumer that keeps the grid on screen never keeps the keyboard in it.
    /// (Raised in place of the release until 2026-10-02, when the user decided it stands beside
    /// it.)
    ///
    /// <para>Only the outermost Escape raises it. ADR-0012's layering is unchanged, and each of
    /// these still peels only its own layer: the Escape that closes an Inner Popup, then the
    /// popover that holds it; the one that cancels an edit; the one that closes a Formula Entry's
    /// list; the one that leaves an Interactive cell; and the one that returns from a control
    /// inside a cell to the root. Each press is one dismissal, and this is the last. It is raised
    /// once per press: a held key's repeats raise it no more until the key is released.</para>
    ///
    /// <para>Without a delegate, the default, the release is all: Escape with nothing left to
    /// dismiss releases Tab, the root keeping DOM focus, so the next Tab or Shift+Tab leaves the
    /// grid (ADR-0012).</para>
    /// </summary>
    [Parameter] public EventCallback OnLeave { get; set; }

    /// <summary>
    /// The Escape with nothing left to dismiss, pressed on the root (ADR-0012, ADR-0070): the
    /// grid's way out of Enter/Tab cycling. Tab is released (ADR-0012, rewritten 2026-10-01), and a
    /// Consumer that declared <see cref="OnLeave"/> hears it too (ADR-0070, 2026-10-02). The release
    /// is sent first, so it reaches the gate before anything the Consumer's handler sends — a render
    /// that closes its dialog, the keyboard given to another grid — and before this Escape's answer,
    /// as a circuit delivers in order; the handler does not wait for its round trip. Once per press:
    /// a held Escape's repeats never reach here (<see cref="OnKeyAsync"/>), and they leave a release
    /// standing in the gate.
    /// </summary>
    /// <summary>
    /// The keyboard is leaving this grid for the Consumer's element around it — a dialog the grid stands
    /// in, closing on the Escape that raised <see cref="OnLeave"/> (ADR-0070; decided with the user,
    /// 2026-10-13). Called from the <see cref="OnLeave"/> handler, before its first await.
    ///
    /// <para>On Blazor Server the Consumer's dialog stands a round trip or more after that Escape, and the
    /// keyboard stays in this grid until the Consumer has put it elsewhere: a key typed meanwhile was
    /// this grid's, and moved in a dialog about to go. While a Consumer listens to <see cref="OnLeave"/>,
    /// the keys typed after an Escape on the root wait for its answer; once this is called, they, and the
    /// keys typed on the root until DOM focus leaves it, are passed to the element around the root, in
    /// order, as the keydowns they were. A handler of the Consumer's there holds them and hands them on
    /// where the keyboard lands — to another grid, with
    /// <see cref="ReturnKeyboardAsync(ElementReference, Func{IReadOnlyList{KeyboardEventArgs}})"/> — so
    /// the outcome is the one typed slowly, whatever the round trip. A press on the grid takes the
    /// keyboard back into it, and the keys are its own again.</para>
    ///
    /// <para>It moves no focus and reads no layout: the key gate passes on what it hears (ADR-0021).
    /// Before the grid is attached to the page, and after it is gone, it does nothing.</para>
    /// </summary>
    /// <returns>A task that completes once the request has been made.</returns>
    public Task PassKeysOutAsync()
        => InvokeAsync(async () =>
        {
            if (_disposed || _scrollHandle is null)
                return;
            try
            {
                await _scrollHandle.InvokeVoidAsync("passKeysOut");
            }
            catch (Exception ex) when (ex is JSException or JSDisconnectedException or ObjectDisposedException or OperationCanceledException)
            {
            }
        });

    private async Task LeaveAsync()
    {
        var released = ReleaseTabAsync();
        try
        {
            if (OnLeave.HasDelegate)
                await OnLeave.InvokeAsync();
        }
        finally
        {
            await released;
        }
    }
}
