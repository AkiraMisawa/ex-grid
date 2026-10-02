using Microsoft.AspNetCore.Components;

namespace ExGrid.Components;

// What a Consumer does with the grid's keyboard (ADR-0070). It gives the keyboard back to the root
// when something of its own that held it over the grid goes away — a dialog, a panel, a tab —
// through ReturnKeyboardAsync, the hand-back a Chrome's frame outside the grid asks for too
// (ADR-0071), which stands beside the Consumer's popover in ExGrid.ConsumerPopover.cs. And it may
// hear the Escape that leaves the grid, in place of the grid's own way out (ADR-0012).
public partial class ExGrid<TRow>
{
    /// <summary>
    /// A Consumer declaration (ADR-0070): raised by an Escape pressed on the root with nothing left
    /// to dismiss, <b>instead of</b> the grid's own way out, which releases Tab (ADR-0012, rewritten
    /// 2026-10-01). The grid keeps the keyboard, its Tab still cycling, and the Consumer decides
    /// what leaving means — closing its dialog, for one — and where the keyboard goes next.
    ///
    /// <para>Only the outermost Escape raises it. ADR-0012's layering is unchanged, and each of
    /// these still peels only its own layer: the Escape that closes an Inner Popup, then the
    /// popover that holds it; the one that cancels an edit; the one that closes a Formula Entry's
    /// list; the one that leaves an Interactive cell; and the one that returns from a control
    /// inside a cell to the root. Each press is one dismissal, and this is the last. It is raised
    /// once per press: a held key's repeats raise it no more until the key is released.</para>
    ///
    /// <para>Without a delegate, the default, nothing changes: Escape with nothing left to dismiss
    /// releases Tab, the root keeping DOM focus, so the next Tab or Shift+Tab leaves the grid
    /// (ADR-0012).</para>
    /// </summary>
    [Parameter] public EventCallback OnLeave { get; set; }

    /// <summary>
    /// The Escape with nothing left to dismiss, pressed on the root (ADR-0012, ADR-0070): the
    /// grid's way out of Enter/Tab cycling. Declared, the Consumer hears it in place of the
    /// release; undeclared, Tab is released (ADR-0012, rewritten 2026-10-01). Once per press: a
    /// held Escape's repeats never reach here (<see cref="OnKeyAsync"/>), and they leave a release
    /// standing in the gate.
    /// </summary>
    private async Task LeaveOrRaiseAsync()
    {
        if (!OnLeave.HasDelegate)
        {
            await ReleaseTabAsync();
            return;
        }
        await OnLeave.InvokeAsync();
    }
}
