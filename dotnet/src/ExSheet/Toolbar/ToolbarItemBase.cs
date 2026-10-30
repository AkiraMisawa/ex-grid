using Microsoft.AspNetCore.Components;

namespace ExSheet;

/// <summary>
/// A Toolbar Item (ADR-0100): one element of a Toolbar Row, which declares what it is and leaves what it does and how
/// it looks to the Sheet. It stands inside an ExSheet's <c>ToolbarContent</c> and declares itself there, in markup
/// order; the Script Core draws it and answers its presses, and the Chrome dresses it. It draws nothing itself. The
/// items are ExSheet's own, a <see cref="ToolbarSeparator"/> and the Consumer's <see cref="ToolbarButton"/>: the Script
/// Core has no other.
/// </summary>
public abstract class ToolbarItemBase : ComponentBase, IDisposable
{
    private protected ToolbarItemBase() { }

    /// <summary>The Toolbar Row this item stands in; null for an item of a toolbar that declares no rows.</summary>
    [CascadingParameter] public ToolbarRow? Row { get; set; }

    /// <summary>The scope this item takes its place on the toolbar from. Internal, as its type is
    /// (ADR-0201): Blazor supplies a cascading parameter that is not public, on a Consumer's item too.</summary>
    [CascadingParameter] internal ToolbarOrder? Order { get; set; }

    /// <summary>Where the item stands on the toolbar, in markup order.</summary>
    internal ToolbarOrder Place { get; private set; } = new();

    /// <summary>The ExSheet's toolbar, where the item declares itself. Internal, as its type is (ADR-0201).</summary>
    [CascadingParameter] internal global::ExSheet.Components.ScriptToolbar? Script { get; set; }

    /// <summary>
    /// The item's KeyTip letters (ADR-0100): a <see cref="ToolbarButton"/>'s only, which carries one when it is declared
    /// here and none otherwise. ExSheet's own items carry Excel's, and refuse another.
    /// </summary>
    [Parameter] public string? KeyTip { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized() => Place = Order?.Next() ?? Place;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        var script = Script
            ?? throw new InvalidOperationException($"{GetType().Name} is a Toolbar Item: it stands inside an ExSheet's ToolbarContent (ADR-0100).");
        script.Join(this);
    }

    /// <inheritdoc />
    protected override bool ShouldRender() => false;

    /// <inheritdoc />
    public void Dispose()
    {
        Script?.Leave(this);
        GC.SuppressFinalize(this);
    }
}
