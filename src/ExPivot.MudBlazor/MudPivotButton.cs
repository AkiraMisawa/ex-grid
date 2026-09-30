using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ExPivot.MudBlazor;

/// <summary>
/// A <c>MudButton</c> that takes DOM focus when ExPivot asks (ADR-0060): once for each request
/// number it has not acted on, as the built-in views' buttons do. The Field List hands the
/// keyboard back to an entry this way when its menu or panel closes, and a menu's first enabled
/// command takes it when the menu opens — through Blazor's own <c>FocusAsync</c> on the button's
/// element, so this package brings no JavaScript of its own (ADR-0021/0061).
/// </summary>
public sealed class MudPivotButton : global::MudBlazor.MudButton
{
    private int _acted;

    /// <summary>Changes when the button should take DOM focus; zero asks nothing. Taking it
    /// scrolls the button into view, as the built-in views' buttons do: a menu opened at the foot
    /// of the pane is shown.</summary>
    [Parameter] public int FocusRequest { get; set; }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        if (FocusRequest == 0 || FocusRequest == _acted)
            return;
        _acted = FocusRequest;
        try
        {
            await _elementReference.FocusAsync();
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or ObjectDisposedException or OperationCanceledException)
        {
            // The circuit or the element went away first; there is nothing left to focus.
        }
    }
}
