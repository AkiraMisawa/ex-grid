using Bunit;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// MEM-3: every <c>DotNetObjectReference</c>, <c>IJSObjectReference</c> and <c>ITimer</c>
/// the grid creates is disposed exactly once — on an ordinary teardown, and when disposal
/// overtakes the module or the handle still on its way, which is where one is easiest to
/// lose. (The grid creates no <c>CancellationTokenSource</c>; the one a fetching source
/// does per fetch is pinned in <c>GridSourceFetchTests</c>, alongside ASY-2.)
/// </summary>
public class ResourceDisposalTests : GridTestContext
{
    private readonly CountingJSRuntime _js = new();
    private readonly CountingTimeProvider _clock;

    public ResourceDisposalTests()
    {
        _clock = new CountingTimeProvider(Clock);
        // Registered after the base class's own, so these are the ones the grid resolves.
        Services.AddSingleton<IJSRuntime>(_js);
        Services.AddSingleton<TimeProvider>(_clock);
    }

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid()
        => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(200))
            .Add(g => g.TotalCount, 200)
            .Add(g => g.Columns, TestRows.Wide(3))
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 120)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.CellMessageOf, (_, _) => "note"));

    private static Task MouseDownAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });

    /// <summary>Created equals disposed, one for one: every module, handle and timer the
    /// grid was handed is disposed exactly once, and every .NET reference it handed out is
    /// released.</summary>
    private void AssertNothingHeld(int modules, int handles)
    {
        Assert.Equal(modules, _js.Modules.Count);
        Assert.Equal(handles, _js.Handles.Count);
        Assert.All(_js.Modules, module => Assert.Equal(1, module.Disposals));
        Assert.All(_js.Handles, handle => Assert.Equal(1, handle.Disposals));
        Assert.All(_clock.Timers, timer => Assert.Equal(1, timer.Disposals));
        Assert.Equal(handles, _js.Selves.Count);
        Assert.All(_js.Selves, self => Assert.Throws<ObjectDisposedException>(
            () => Assert.IsType<DotNetObjectReference<ExGrid<TestRow>>>(self).Value));
    }

    [Fact] // MEM-3 / ADR-0018: a grid gives back every reference and timer it took, once each
    public async Task Every_reference_and_timer_is_given_back_once()
    {
        var cut = RenderGrid();
        // One of each timer the grid keeps: the settle delay after a fling (ADR-0004),
        // the message delay after the Focus moves (ADR-0034), the announcement delay for
        // a selection of more than one cell (ADR-0033), and the auto-scroll's tick while
        // a drag is held at the edge (ADR-0008).
        _js.ScrollOffset = new ScrollOffset(100 * 20, 0); // a hundred rows at once
        await cut.Find(".ex-scroller").ScrollAsync(EventArgs.Empty);
        await MouseDownAsync(cut, 50, 10);
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowDown", false, true, false, false, false));
        await cut.Find(".ex-viewport").MouseMoveAsync(new MouseEventArgs { Buttons = 1, OffsetX = 50, OffsetY = 100 });
        Assert.Equal(
            ["OnAnnounceSettled", "OnEdgeTick", "OnMessageSettled", "OnSettled"],
            _clock.Timers.Select(t => t.Name).Order());

        await DisposeComponentsAsync();

        AssertNothingHeld(modules: 1, handles: 1);
    }

    [Fact] // MEM-3: disposal overtaking the module's import still gives the module back
    public async Task Disposal_before_the_module_lands_still_releases_it()
    {
        _js.ImportGate = new TaskCompletionSource();
        RenderGrid();

        await DisposeComponentsAsync();
        _js.ImportGate.SetResult();
        await Renderer.Dispatcher.InvokeAsync(() => { }); // the grid's continuation, queued behind

        // Never attached, so never handed a handle nor handing out a reference.
        AssertNothingHeld(modules: 1, handles: 0);
    }

    [Fact] // MEM-3 / ADR-0018: disposal overtaking attach gives back the handle it returns, once
    public async Task Disposal_before_the_handle_lands_still_releases_everything_once()
    {
        _js.AttachGate = new TaskCompletionSource();
        RenderGrid();

        await DisposeComponentsAsync();
        _js.AttachGate.SetResult();
        await Renderer.Dispatcher.InvokeAsync(() => { }); // the grid's continuation, queued behind

        AssertNothingHeld(modules: 1, handles: 1);
    }
}
