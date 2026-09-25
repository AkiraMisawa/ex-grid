using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace ExGrid.Components.Tests.Support;

/// <summary>
/// Every grid test needs the same two seams: the JavaScript module the component
/// imports, and control over the clock the settle delay runs on (ADR-0004) — real
/// waiting would make the suite slow and flaky.
/// </summary>
public abstract class GridTestContext : BunitContext
{
    protected GridTestContext()
    {
        Services.AddSingleton<TimeProvider>(Clock);
        Js = GridJSInterop.Setup(this);
    }

    internal FakeTimeProvider Clock { get; } = new();

    // What every grid test means unless it says otherwise: a grid that has connected and
    // hears events. A Prerendered grid is its own case (ADR-0033, A11Y-20). Set at the
    // first render rather than here: telling bUnit the renderer's info builds the
    // renderer, which closes the service collection a test may still be adding to.
    private bool _rendererInfoSet;

    private void EnsureRendererInfo()
    {
        if (!_rendererInfoSet)
            SetRendererInfo(new RendererInfo("Server", isInteractive: true));
    }

    public new void SetRendererInfo(RendererInfo rendererInfo)
    {
        _rendererInfoSet = true;
        base.SetRendererInfo(rendererInfo);
    }

    public new IRenderedComponent<TComponent> Render<TComponent>(
        Action<ComponentParameterCollectionBuilder<TComponent>>? parameterBuilder = null)
        where TComponent : IComponent
    {
        EnsureRendererInfo();
        return base.Render(parameterBuilder);
    }

    public new IRenderedComponent<Bunit.Rendering.ContainerFragment> Render(RenderFragment renderFragment)
    {
        EnsureRendererInfo();
        return base.Render(renderFragment);
    }

    internal GridJSInterop Js { get; }

    /// <summary>
    /// Moves the browser's scroll position and raises the scroll event the component
    /// listens for. The event is Blazor's own @onscroll — attaching listeners in
    /// JavaScript is not on the allowlist; only reading the offsets is (ADR-0021).
    /// Both axes move together because a real one does: a trackpad gesture is diagonal.
    /// </summary>
    internal async Task ScrollToAsync(IElement scroller, double top, double left = 0)
    {
        Js.SetScrollOffset(top, left);
        await scroller.ScrollAsync(EventArgs.Empty);
    }
}
