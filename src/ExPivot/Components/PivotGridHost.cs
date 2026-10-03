using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace ExPivot.Components;

/// <summary>
/// Stands between ExPivot and its grid so that a render the report did not cause does not reach
/// the grid (ADR-0003's rule, one level up): typing in the Field List's search, a drag crossing an
/// Area, a menu opening. ExPivot bumps <see cref="Version"/> whenever anything the grid reads may
/// have changed — a new report, new columns, new parameters from the Consumer — and only then does
/// the grid get its parameters again. Written by hand, because Blazor's own change detection
/// treats a fragment as changed every time.
/// </summary>
internal sealed class PivotGridHost : ComponentBase
{
    private int _rendered = -1;

    /// <summary>Changes whenever what the grid reads may have changed.</summary>
    [Parameter] public int Version { get; set; }

    /// <summary>The grid.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    protected override bool ShouldRender() => Version != _rendered;

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        _rendered = Version;
        builder.AddContent(0, ChildContent);
    }
}
