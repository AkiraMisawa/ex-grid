using Bunit;
using ExGrid.Components.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// ASY-5: the settle timer that ends a fling (ADR-0004) fires where a real timer does — on a
/// thread-pool thread, not the renderer's — and what it does lands on the renderer's
/// synchronisation context; a fire that arrives after the grid was disposed, as a timer's
/// callback already on its way does, is harmless. The clock is <c>FakeTimeProvider</c>'s, with
/// each timer's callback kept so the test can fire it from a thread of its choosing.
/// </summary>
public class SettleTimerTests : GridTestContext
{
    private const double RowHeightPx = 20;
    private readonly CallbackClock _clock;

    public SettleTimerTests()
    {
        _clock = new CallbackClock(Clock);
        // Registered after the base class's own clock, so this is the one the grid resolves.
        Services.AddSingleton<TimeProvider>(_clock);
    }

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(Action<RowRange> asked)
        => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(10))
            .Add(g => g.TotalCount, 1_000)
            .Add(g => g.Columns, TestRows.Columns())
            .Add(g => g.RowHeight, RowHeightPx)
            .Add(g => g.ViewportHeight, 100)
            .Add(g => g.OnRangeNeeded, asked));

    /// <summary>A hundred rows at once: a fling, which paints Placeholders and arms the settle timer.</summary>
    private async Task<Action> FlingAsync(IRenderedComponent<ExGrid<TestRow>> cut)
    {
        await ScrollToAsync(cut.Find(".ex-scroller"), 100 * RowHeightPx);
        Assert.NotEmpty(cut.FindAll(".ex-placeholder"));
        return _clock.FireOf("OnSettled");
    }

    [Fact] // ADR-0004 / ASY-5: fired from a thread-pool thread, the settle lands on the renderer's context and asks from there
    public async Task ASY5_the_settle_timer_fires_onto_the_renderers_context()
    {
        var askedOnTheRenderersContext = new List<bool>();
        var cut = RenderGrid(_ => askedOnTheRenderersContext.Add(Renderer.Dispatcher.CheckAccess()));
        var settle = await FlingAsync(cut);

        // Where System.Threading.Timer runs its callback: a pool thread, never the renderer's own.
        await Task.Run(() =>
        {
            Assert.False(Renderer.Dispatcher.CheckAccess());
            settle();
        }, Xunit.TestContext.Current.CancellationToken);

        // The range landed on is asked for — by the render the settle caused, on the renderer's
        // context (ADR-0001), so a Consumer's handler never runs on the timer's thread. Its rows
        // are not in the Window, so they stay Placeholders until the Consumer answers (ADR-0004).
        cut.WaitForAssertion(() => Assert.Equal([true], askedOnTheRenderersContext));
        Assert.Equal(5, cut.FindAll(".ex-placeholder").Count);
        Assert.False(Renderer.UnhandledException.IsCompleted);
    }

    [Fact] // ADR-0004 / ADR-0018 / ASY-5: a settle that fires after the grid was disposed does nothing, and throws nothing
    public async Task ASY5_a_settle_firing_after_disposal_is_harmless()
    {
        var asked = new List<RowRange>();
        var cut = RenderGrid(asked.Add);
        var settle = await FlingAsync(cut);

        await DisposeComponentsAsync();
        // Disposing a timer does not recall a callback already on its way (System.Threading.Timer
        // makes no such promise), so the fire still arrives — on a pool thread, after disposal.
        var failure = await Record.ExceptionAsync(() => Task.Run(settle, Xunit.TestContext.Current.CancellationToken));
        await Renderer.Dispatcher.InvokeAsync(() => { }); // whatever the fire queued, run

        Assert.Null(failure);
        Assert.Empty(asked);
        Assert.False(Renderer.UnhandledException.IsCompleted);
    }

    /// <summary><c>FakeTimeProvider</c>, keeping each timer's callback by the method it calls.</summary>
    private sealed class CallbackClock(TimeProvider inner) : TimeProvider
    {
        private readonly Dictionary<string, Action> _fires = [];

        internal Action FireOf(string callback) => _fires[callback];

        public override DateTimeOffset GetUtcNow() => inner.GetUtcNow();

        public override long GetTimestamp() => inner.GetTimestamp();

        public override long TimestampFrequency => inner.TimestampFrequency;

        public override TimeZoneInfo LocalTimeZone => inner.LocalTimeZone;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            _fires[callback.Method.Name] = () => callback(state);
            return inner.CreateTimer(callback, state, dueTime, period);
        }
    }
}
