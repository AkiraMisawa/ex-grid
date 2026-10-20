using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace ExGrid.DemoPages;

/// <summary>
/// Blazor's <see cref="InputFile"/>, saying when it can hear a choice. InputFile hears a file only
/// once its script is attached to the element, which it asks for after its first render: on the
/// Server host that is a round trip after the input is drawn, and a file chosen in between is lost
/// with nothing said (widened-run ticket 01). <see cref="OnAttached"/> is raised once the script has
/// answered, so a page can keep the input disabled until then.
/// </summary>
public sealed class AttachedInputFile : InputFile
{
    /// <summary>Raised once, when the input's script is attached and a choice is heard.</summary>
    [Parameter]
    public EventCallback OnAttached { get; set; }

    /// <inheritdoc/>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        if (firstRender)
            await OnAttached.InvokeAsync();
    }
}
