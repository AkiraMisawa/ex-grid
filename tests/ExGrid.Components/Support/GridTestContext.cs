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
    /// Reads <paramref name="read"/> on the renderer's dispatcher once it has nothing in hand: in the
    /// first of its turns that runs at once, on the test's own thread. An answer the test gives — a
    /// browser's (<see cref="HeldJSAnswer{TResult}"/>), a Consumer's task — posts the grid's
    /// continuation to the dispatcher as it is given, and whatever that continuation posts in turn is
    /// handed over before it ends; so the reading sees all of it taken in, however long the process
    /// takes to get round to it, and anything it threw to the renderer fails the test, as it failed
    /// bUnit's wait.
    /// </summary>
    /// <remarks>bUnit's <c>WaitForAssertion</c> says as much only within its timeout, and a loaded
    /// process can hold a posted continuation for longer: a whole suite at 32 threads on four cores
    /// held one past ten seconds, and the wait failed having checked nothing ("Check count: 0",
    /// 2026-10-13). A reading taken straight after the answer, on the test's own thread, is worse:
    /// it may come before the continuation, beside it, or after it, and a test asserting that an
    /// answer changed nothing passes either way.</remarks>
    internal async Task<T> ReadWhenIdleAsync<T>(Func<T> read)
    {
        var value = await InIdleTurnAsync(read);
        ThrowWhatTheRendererCaught();
        return value;
    }

    /// <summary>Runs <paramref name="assertion"/> on the renderer's dispatcher once it has nothing in
    /// hand, as <see cref="ReadWhenIdleAsync{T}"/> reads: once, and it fails the test if it throws or
    /// if the work before it threw to the renderer. What bUnit's <c>WaitForAssertion</c> was asked to
    /// wait for, without the clock.</summary>
    internal Task AssertWhenIdleAsync(Action assertion)
        => ReadWhenIdleAsync(() =>
        {
            assertion();
            return true;
        });

    /// <summary>Returns once the renderer's dispatcher has nothing in hand, as
    /// <see cref="ReadWhenIdleAsync{T}"/> reads: every task the work before it completes on the
    /// dispatcher is complete.</summary>
    internal Task WhenIdleAsync() => ReadWhenIdleAsync(static () => true);

    // Set on the test's thread for the length of a call into the dispatcher. A turn that finds it ran
    // inside the call, on that thread, which the dispatcher does only when it has nothing in hand.
    [ThreadStatic]
    private static object? t_turnCalled;

    // How many turns a dispatcher that never runs out of work is given before the test says so: a
    // count, not a time, so it fails the same way however loaded the process is.
    private const int TurnsBeforeGivingUp = 10_000;

    private async Task<T> InIdleTurnAsync<T>(Func<T> read)
    {
        if (Renderer.Dispatcher.CheckAccess())
            throw new InvalidOperationException("Called on the renderer's dispatcher, where every turn runs at once: there is no telling from here whether it is idle.");
        for (var turn = 0; turn < TurnsBeforeGivingUp; turn++)
        {
            var called = new object();
            Task<(bool Idle, T Value)> taken;
            t_turnCalled = called;
            try
            {
                taken = Renderer.Dispatcher.InvokeAsync(() => ReferenceEquals(t_turnCalled, called) ? (true, read()) : (false, default(T)!));
            }
            finally
            {
                t_turnCalled = null;
            }
            var (idle, value) = await taken;
            if (idle)
                return value;
        }
        throw new InvalidOperationException(FormattableString.Invariant(
            $"The renderer's dispatcher still had work in hand after {TurnsBeforeGivingUp} turns: something keeps handing it more."));
    }

    /// <summary>An exception the renderer caught from the grid's own work — a continuation that threw,
    /// a failure dispatched to the host's error UI — rethrown here, where the test can fail on it.
    /// bUnit's wait listened for it; a turn of the dispatcher has to ask.</summary>
    private void ThrowWhatTheRendererCaught()
    {
        if (Renderer.UnhandledException is { IsCompletedSuccessfully: true } caught)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(caught.Result).Throw();
    }

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
