using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;

namespace ExSheet.MudBlazor;

/// <summary>
/// Format Cells' tabs: MudBlazor's <see cref="MudTabs"/>, with ARIA's tabs pattern as the built-in
/// Chrome follows it (ADR-0063). MudTabs moves the keyboard between the tabs with the arrow keys
/// and shows a tab only on Enter or Space; here the arrows, Home and End show the tab they reach,
/// so the tabs switch with the arrow keys under either Chrome (SH-45). Excel's Ctrl+Tab and
/// Ctrl+PageDown are the browser's, and a page never receives them.
/// </summary>
internal sealed class MudFormatCellsTabs : MudTabs
{
    private bool _focused;

    /// <summary>Takes the keyboard to the tab shown, once, as soon as the tabs are drawn: Format Cells opens with the keyboard on its tab.</summary>
    [Parameter] public bool FocusOnOpen { get; set; }

    /// <inheritdoc />
    protected override async Task HandleTabKeyDownAsync(KeyboardEventArgs e, MudTabPanel panel)
    {
        var panels = Panels.Where(p => !p.Disabled).ToList();
        // From the tab shown, not the tab the key was pressed on: on a circuit, a second arrow
        // typed with the first is pressed on the tab the keyboard has not yet left, a round trip
        // before the first is answered. The tab shown is where the keyboard is going, as the
        // built-in Chrome counts from it.
        var at = panels.IndexOf(ActivePanel ?? panel);
        int? to = at < 0 ? null : e.Key switch
        {
            "ArrowRight" => (at + 1) % panels.Count,
            "ArrowLeft" => (at + panels.Count - 1) % panels.Count,
            "Home" => 0,
            "End" => panels.Count - 1,
            _ => null,
        };
        if (to is not { } next)
        {
            await base.HandleTabKeyDownAsync(e, panel);
            return;
        }
        var reached = panels[next];
        await ActivatePanelAsync(reached);
        await FocusAsync(reached);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        // The tabs are drawn a render after the panels register, so the first render may have none.
        if (!FocusOnOpen || _focused || ActivePanel is not { } shown || shown.PanelRef.Context is null) return;
        _focused = true;
        await FocusAsync(shown);
    }

    private static async Task FocusAsync(MudTabPanel panel)
    {
        if (panel.PanelRef.Context is null) return;
        try
        {
            await panel.PanelRef.FocusAsync();
        }
        catch (Exception ex) when (ex is Microsoft.JSInterop.JSException or Microsoft.JSInterop.JSDisconnectedException or ObjectDisposedException or OperationCanceledException)
        {
            // The dialog closed, or the circuit did, between the render and the focus.
        }
    }
}
