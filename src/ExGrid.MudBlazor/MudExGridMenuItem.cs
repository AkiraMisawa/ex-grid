using Microsoft.AspNetCore.Components;

namespace ExGrid.MudBlazor;

/// <summary>
/// A <c>MudButton</c> that can take focus without scrolling. MudBlazor's own
/// <c>FocusAsync</c> always scrolls the button into view, and the menu's opening focus must
/// not: on a Server circuit it lands a round trip after the menu is shown, when the user may
/// already have scrolled a menu taller than its grid (ADR-0039/0040).
/// </summary>
internal sealed class MudExGridMenuItem : global::MudBlazor.MudButton
{
    /// <summary>Focuses the button through Blazor's own <c>FocusAsync</c> on its element.</summary>
    public ValueTask FocusAsync(bool preventScroll) => _elementReference.FocusAsync(preventScroll);
}
