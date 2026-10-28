using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace ExSheet.Script;

/// <summary>
/// Where the Script ExSheet's <c>ToolbarContent</c> is rendered, its rows and items declaring themselves to
/// <see cref="ScriptToolbar"/> (ADR-0100): rendered again only when the Consumer hands another <c>ToolbarContent</c>, so
/// the Sheet rendering again for what they declared does not have them declare it again.
/// </summary>
internal sealed class ScriptToolbarHost : ComponentBase
{
    private RenderFragment? _drawn;

    /// <summary>The toolbar the rows and items declare themselves to.</summary>
    [Parameter, EditorRequired] public ScriptToolbar? Toolbar { get; set; }

    /// <summary>The Consumer's <c>ToolbarContent</c>.</summary>
    [Parameter, EditorRequired] public RenderFragment? Content { get; set; }

    /// <inheritdoc />
    protected override bool ShouldRender() => !ReferenceEquals(Content, _drawn);

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var toolbar = Toolbar ?? throw new InvalidOperationException("Internal: a toolbar host with no toolbar.");
        _drawn = Content;
        builder.OpenComponent<CascadingValue<ScriptToolbar>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<ScriptToolbar>.Value), toolbar);
        builder.AddComponentParameter(2, nameof(CascadingValue<ScriptToolbar>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<ScriptToolbar>.ChildContent), (RenderFragment)(inner =>
        {
            inner.OpenComponent<CascadingValue<ToolbarOrder>>(0);
            inner.AddComponentParameter(1, nameof(CascadingValue<ToolbarOrder>.Value), toolbar.Root);
            inner.AddComponentParameter(2, nameof(CascadingValue<ToolbarOrder>.IsFixed), true);
            inner.AddComponentParameter(3, nameof(CascadingValue<ToolbarOrder>.ChildContent), Content);
            inner.CloseComponent();
        }));
        builder.CloseComponent();
    }
}
