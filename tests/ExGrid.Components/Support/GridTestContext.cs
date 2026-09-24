using AngleSharp.Dom;
using Bunit;
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
