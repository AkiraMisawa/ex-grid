using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// ERR-5 at layer 2 (ADR-0025): a Source that throws does not take the grid down. The failure
/// reaches the Consumer through the Source's <c>FetchFailed</c>, and the grid keeps painting the
/// last good Window. A <c>GridSource.Fetch</c> over a server that answers until it is told to
/// fail; 20px rows in a 200px Viewport.
/// </summary>
public class SourceFailureTests : GridTestContext
{
    private sealed class Server
    {
        public TestRow[] Rows { get; } = TestRows.Many(1000);

        public bool Failing { get; set; }

        public int Asked { get; private set; }

        public ValueTask<GridPage<TestRow>> Fetch(GridQuery query, CancellationToken cancellation)
        {
            Asked++;
            if (Failing)
                return ValueTask.FromException<GridPage<TestRow>>(new TimeoutException("the server did not answer"));
            var start = Math.Min(query.Range.Start, Rows.Length);
            var count = Math.Min(query.Range.Count, Rows.Length - start);
            return ValueTask.FromResult(new GridPage<TestRow>(Rows[start..(start + count)], start, Rows.Length));
        }
    }

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(IGridSource<TestRow> source)
        => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, TestRows.Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 200));

    private static string[] PaintedBooks(IRenderedComponent<ExGrid<TestRow>> cut)
        => [.. cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.Instance.Row.Book)];

    [Fact] // ERR-5, ADR-0025: a re-read that throws reaches the Consumer, and the rows on screen stay
    public async Task ERR5_a_failed_re_read_reaches_the_consumer_and_the_last_window_stays_painted()
    {
        var server = new Server();
        var source = GridSource.Fetch<TestRow>(server.Fetch, readAheadRows: 20, rowKey: r => r.Book);
        var reported = new List<Exception>();
        source.FetchFailed += reported.Add;
        var cut = RenderGrid(source);
        cut.WaitForAssertion(() => Assert.Contains("Row 000000", PaintedBooks(cut)));
        var painted = PaintedBooks(cut);
        var window = source.Window;

        server.Failing = true;
        var asked = server.Asked;
        await cut.InvokeAsync(() => source.NotifyChanged());
        cut.WaitForAssertion(() => Assert.Single(reported));

        Assert.True(server.Asked > asked);
        Assert.IsType<TimeoutException>(reported[0]);
        Assert.Same(reported[0], source.LastError);
        Assert.Same(window, source.Window);
        Assert.False(source.IsLoading);
        // The grid kept painting what it had.
        Assert.Equal(painted, PaintedBooks(cut));
    }

    [Fact] // ERR-5, ADR-0025: a Range Request the grid raised that throws reaches the Consumer; scrolled back, the Window is painted
    public async Task ERR5_a_failed_range_request_reaches_the_consumer_and_the_grid_keeps_painting()
    {
        var server = new Server();
        var source = GridSource.Fetch<TestRow>(server.Fetch, readAheadRows: 20);
        var reported = new List<Exception>();
        source.FetchFailed += reported.Add;
        var cut = RenderGrid(source);
        cut.WaitForAssertion(() => Assert.Contains("Row 000000", PaintedBooks(cut)));
        var window = source.Window;

        server.Failing = true;
        await ScrollToAsync(cut.Find(".ex-scroller"), 10_000);
        await cut.InvokeAsync(() => Clock.Advance(TimeSpan.FromMilliseconds(500)));
        cut.WaitForAssertion(() => Assert.NotEmpty(reported));

        Assert.IsType<TimeoutException>(reported[^1]);
        Assert.Same(window, source.Window);

        // Back where the Window is, its rows are painted: the grid is still standing.
        await ScrollToAsync(cut.Find(".ex-scroller"), 0);
        await cut.InvokeAsync(() => Clock.Advance(TimeSpan.FromMilliseconds(500)));
        Assert.Contains("Row 000000", PaintedBooks(cut));
    }
}
