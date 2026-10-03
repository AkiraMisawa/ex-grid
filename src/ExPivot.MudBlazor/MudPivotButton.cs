using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ExPivot.MudBlazor;

/// <summary>
/// A <c>MudButton</c> that takes DOM focus when ExPivot asks (ADR-0061): once for each request
/// number it has not acted on, as the built-in views' buttons do. The Field List hands the
/// keyboard back to an entry this way when its menu or panel closes, and a menu's first enabled
/// command takes it when the menu opens — through Blazor's own <c>FocusAsync</c> on the button's
/// element, so this package brings no JavaScript of its own (ADR-0021/0062). Where the keyboard
/// comes from the report's grid — the details dialog's Close — it is taken through
/// <see cref="TakeKeyboard"/>, which the grid grants only while the keyboard is still the report's
/// (ADR-0070's note of 2026-10-02).
/// </summary>
public sealed class MudPivotButton : global::MudBlazor.MudButton
{
    private int _acted;

    /// <summary>Changes when the button should take DOM focus; zero asks nothing. Taking it
    /// scrolls the button into view, as the built-in views' buttons do: a menu opened at the foot
    /// of the pane is shown.</summary>
    [Parameter] public int FocusRequest { get; set; }

    /// <summary>How the button takes DOM focus when asked: called with its element, in place of the
    /// element's own <c>FocusAsync</c> — the report's grid handing the keyboard on (ADR-0070's note
    /// of 2026-10-02). Null, the element's own focus.</summary>
    [Parameter] public Func<ElementReference, Task>? TakeKeyboard { get; set; }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        if (FocusRequest == 0 || FocusRequest == _acted)
            return;
        _acted = FocusRequest;
        try
        {
            await (TakeKeyboard is { } take ? take(_elementReference) : _elementReference.FocusAsync().AsTask());
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or ObjectDisposedException or OperationCanceledException)
        {
            // The circuit or the element went away first; there is nothing left to focus.
        }
    }
}
