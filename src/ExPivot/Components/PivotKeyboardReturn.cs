using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace ExPivot.Components;

/// <summary>
/// Gives the report's grid the keyboard back once what held it over the report has gone (ADR-0070):
/// Show Details' dialog, however it closed, or a details tab whose closing left the report's tab
/// selected. It renders nothing. For each request number it has not acted on, it asks through
/// <see cref="Return"/> after the render that carries the request — the render that took the dialog
/// away, and the report's <c>inert</c> with it, or uncovered the report — so the grid's root can take
/// DOM focus when the request lands. Before that render the root is inert or hidden, and could not.
/// </summary>
internal sealed class PivotKeyboardReturn : ComponentBase
{
    private int _acted;

    /// <summary>The request to give the keyboard back; zero asks nothing.</summary>
    [Parameter] public int Request { get; set; }

    /// <summary>Asks the report's grid for the keyboard back: ExGrid's <c>ReturnKeyboardAsync</c>.
    /// Held in a field by ExPivot, and reads the grid when it is called.</summary>
    [Parameter] public Func<Task>? Return { get; set; }

    // Only a request not yet acted on renders, and so reaches OnAfterRenderAsync: a delegate
    // parameter is "changed" to Blazor on every render of ExPivot (ADR-0003).
    protected override bool ShouldRender() => Request != 0 && Request != _acted;

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Request == 0 || Request == _acted || Return is null)
            return;
        _acted = Request;
        await Return();
    }
}
