using Bunit;
using ExGrid;
using ExGrid.Components;
using ExPivot.Components.Tests.Support;
using ExPivot.Engine;
using Microsoft.AspNetCore.Components;
using Xunit;
using PivotComponent = ExPivot.Components.ExPivot;

namespace ExPivot.Components.Tests;

/// <summary>
/// A remote report's Window, read ahead (ADR-0182, LV-34), and asked for before the rows on screen
/// run out (ADR-0183, FN-3a). ExPivot asks its report source for the range its grid needs widened by
/// the source's read-ahead either side, clamped into the report; a scroll within the Window held asks
/// nothing, and one past it asks once; a question whose Window covers the rows on screen stays in
/// flight; and the grid's Range Request Margin is the smaller of 10 and the read-ahead. Counted in
/// the questions the report source is asked, never in time. The report is 999 Regions and the Grand
/// Total: 1,000 rows, asked for over JSON as a server's would be.
/// </summary>
public sealed class ReportReadAheadTests : PivotTestContext
{
    /// <summary>A keyed deal: each in a Region of its own.</summary>
    public sealed record Deal(long Id, string Region, decimal Amount);

    private static readonly PivotFields<Deal> DealFields = PivotFields.Of<Deal>()
        .Key("Id", d => d.Id)
        .Text("Region", d => d.Region)
        .Number("Amount", d => d.Amount);

    // 999 Regions and the Grand Total: a report of 1,000 rows. Row i is Region i.
    private static readonly PivotLayout ByRegion = new() { Rows = [P("Region")], Values = [Sum("Amount")] };

    private const double RowHeightPx = 20;

    private static readonly PivotSlicing Whole = new() { Budget = TimeSpan.FromDays(1) };

    private static readonly Deal[] Deals = [.. Enumerable.Range(0, 999).Select(i => new Deal(i, $"Region {i:D4}", i))];

    private static T Wire<T>(T value) => PivotReportJson.Read<T>(PivotReportJson.Write(value));

    /// <summary>A server's report over the deals, its data, the report source that asks it over JSON
    /// — recording every Window it is asked for, and every answer — and the pivot showing it.</summary>
    private sealed record Remote(
        IRenderedComponent<PivotComponent> Cut, SnapshotPivotSource Data, LocalPivotReportSource Server,
        FetchingPivotReportSource Source, List<PivotReportWindow> Asked, List<PivotReportUpdate> Answers) : IAsyncDisposable
    {
        public int Count { get { lock (Asked) return Asked.Count; } }

        public PivotReportWindow Last { get { lock (Asked) return Asked[^1]; } }

        public ValueTask DisposeAsync() => Server.DisposeAsync();
    }

    /// <param name="readAheadRows">What the report source is given; null leaves Fetch's default.</param>
    /// <param name="answerWhen">Awaited before each Window question is answered: a test holds one.</param>
    /// <param name="answer">Rewrites the server's answer, as a faulty relay would.</param>
    private Remote RenderRemote(int? readAheadRows = null, Func<PivotReportRequest, Task>? answerWhen = null,
        Func<PivotReportUpdate, PivotReportUpdate>? answer = null)
    {
        var data = PivotSource.From(Deals, DealFields, Whole);
        var server = PivotReportSource.From(data, timeProvider: Clock, slicing: Whole);
        var asked = new List<PivotReportWindow>();
        var answers = new List<PivotReportUpdate>();
        async ValueTask<PivotReportUpdate> WindowAsync(PivotReportRequest request, CancellationToken ct)
        {
            lock (asked)
                asked.Add(request.Window);
            if (answerWhen is not null)
                await answerWhen(request).WaitAsync(ct);
            var update = Wire(await server.WindowAsync(Wire(request), ct));
            update = answer?.Invoke(update) ?? update;
            lock (asked)
                answers.Add(update);
            return update;
        }
        var source = readAheadRows is { } readAhead
            ? PivotReportSource.Fetch(server.Fields, server.Features, server.UpdateMode, WindowAsync,
                items: server.RawItemsAsync, reportItems: async (query, ct) => Wire(await server.ItemsAsync(Wire(query), ct)),
                readAheadRows: readAhead)
            : PivotReportSource.Fetch(server.Fields, server.Features, server.UpdateMode, WindowAsync,
                items: server.RawItemsAsync, reportItems: async (query, ct) => Wire(await server.ItemsAsync(Wire(query), ct)));
        data.Changed += change => source.NotifyChanged(change.SourceVersion);
        var cut = RenderPivot(ByRegion, ps => ps
            .Add(p => p.RowHeight, RowHeightPx)
            .Add(p => p.ShowFieldList, false)
            .Add(p => p.Slicing, Whole), reportSource: source);
        cut.WaitForAssertion(() => Assert.Equal(1000, Grid(cut).Instance.TotalCount));
        return new(cut, data, server, source, asked, answers);
    }

    /// <summary>How many rows the grid shows: the rows on screen it asks for.</summary>
    private static int OnScreen(IRenderedComponent<PivotComponent> cut) => RowTexts(cut).Length;

    /// <summary>Scrolls to <paramref name="row"/> at the top and waits until the grid paints it there,
    /// from a row it holds.</summary>
    private async Task ScrollToRowAsync(IRenderedComponent<PivotComponent> cut, int row)
    {
        await ScrollToRowOrPlaceholderAsync(cut, row);
        cut.WaitForAssertion(() => Assert.StartsWith($"Region {row:D4} |", RowTexts(cut)[0], StringComparison.Ordinal));
    }

    /// <summary>Scrolls to <paramref name="row"/> at the top and waits until the grid has settled
    /// there and painted it, held or a Placeholder: the Range Request it raises, if any, is out.</summary>
    private async Task ScrollToRowOrPlaceholderAsync(IRenderedComponent<PivotComponent> cut, int row)
    {
        await ScrollReportAsync(cut, row * RowHeightPx);
        cut.WaitForAssertion(() => Assert.Equal((row + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
            Grid(cut).FindAll(".ex-viewport .ex-row")[0].GetAttribute("aria-rowindex")));
    }

    private void NoExceptionReachedTheRenderer()
        => Assert.False(Renderer.UnhandledException.IsCompleted, "an exception reached the renderer");

    [Fact] // ADR-0182 (LV-34): a report source asked through Fetch reads sixty rows ahead unless told; a local one none
    public async Task LV34_A_remote_source_reads_sixty_rows_ahead_by_default_and_a_local_one_none()
    {
        var data = PivotSource.From(Deals, DealFields, Whole);
        await using var local = PivotReportSource.From(data, timeProvider: Clock, slicing: Whole);
        static ValueTask<PivotReportUpdate> Never(PivotReportRequest request, CancellationToken ct) => throw new NotSupportedException();

        Assert.Equal(0, local.ReadAheadRows);
        Assert.Equal(60, PivotReportSource.Fetch(local.Fields, local.Features, local.UpdateMode, Never).ReadAheadRows);
        Assert.Equal(120, PivotReportSource.Fetch(local.Fields, local.Features, local.UpdateMode, Never, readAheadRows: 120).ReadAheadRows);
        var refusal = Assert.Throws<ArgumentOutOfRangeException>(
            () => PivotReportSource.Fetch(local.Fields, local.Features, local.UpdateMode, Never, readAheadRows: -1));
        Assert.Equal("readAheadRows", refusal.ParamName);
    }

    [Theory] // ADR-0182 (LV-34): the rows on screen, widened by the read-ahead either side and clamped into the report
    [InlineData(200, 15, 60, 16823, 140, 135)]
    // At the report's start, and at its end.
    [InlineData(10, 15, 60, 16823, 0, 85)]
    [InlineData(16808, 15, 60, 16823, 16748, 75)]
    // Nothing read ahead: the rows on screen.
    [InlineData(200, 15, 0, 16823, 200, 15)]
    // Rows past the end of a report that shrank under them: never short of them, and the answer's
    // row count clamps the Window (ADR-0151).
    [InlineData(500, 15, 60, 503, 440, 75)]
    public void LV34_The_Window_asked_is_the_rows_on_screen_widened_and_clamped_into_the_report(
        int start, int count, int readAhead, int rows, int windowStart, int windowCount)
        => Assert.Equal(new PivotReportWindow(windowStart, windowCount),
            PivotComponent.Widened(new RowRange(start, count), readAhead, rows));

    [Theory] // ADR-0183 (LV-34, FN-3a): ExPivot sets its grid's Range Request Margin to the smaller of 10 and its source's read-ahead
    [InlineData(0, 0)]
    [InlineData(5, 5)]
    [InlineData(60, 10)]
    [InlineData(120, 10)]
    public async Task LV34_The_grids_margin_is_the_smaller_of_10_and_the_read_ahead(int readAhead, int margin)
    {
        await using var remote = RenderRemote(readAhead);

        Assert.Equal(margin, Grid(remote.Cut).Instance.RangeRequestMargin);
    }

    [Fact] // ADR-0183 (LV-34): a local report reads nothing ahead, and its grid asks only once the rows on screen run out
    public void LV34_A_local_report_sets_no_margin()
    {
        var cut = RenderPivot(ByRegion, ps => ps.Add(p => p.RowHeight, RowHeightPx).Add(p => p.ShowFieldList, false)
            .Add(p => p.Slicing, Whole), source: PivotSource.From(Deals, DealFields, Whole));
        cut.WaitForAssertion(() => Assert.Equal(1000, Grid(cut).Instance.TotalCount));

        Assert.Equal(0, Grid(cut).Instance.RangeRequestMargin);
    }

    [Fact] // ADR-0182 (LV-34): the first Window is as it was; a scroll past it asks once, for the rows on screen and sixty either side
    public async Task LV34_A_scroll_past_the_Window_asks_once_for_it_widened_sixty_rows_either_side()
    {
        await using var remote = RenderRemote();
        // The first Window, as before: 64 rows from the top.
        Assert.Equal([new PivotReportWindow(0, 64)], remote.Asked);

        await ScrollToRowAsync(remote.Cut, 500);

        var onScreen = OnScreen(remote.Cut);
        Assert.Equal(2, remote.Count);
        Assert.Equal(new PivotReportWindow(440, onScreen + 120), remote.Last);
        remote.Cut.WaitForAssertion(() => Assert.Equal(440, Grid(remote.Cut).Instance.WindowStart));
        Assert.Equal(onScreen + 120, Grid(remote.Cut).Instance.Window.Count);
        NoExceptionReachedTheRenderer();
    }

    [Fact] // ADR-0182/0183 (LV-34, FN-3a): a scroll within the Window held asks nothing; ten rows before it runs out, it asks once, with every row on screen still held
    public async Task LV34_A_scroll_within_the_Window_asks_nothing_and_ten_rows_before_its_end_asks_once()
    {
        TaskCompletionSource? hold = null;
        await using var remote = RenderRemote(answerWhen: _ => hold?.Task ?? Task.CompletedTask);
        await ScrollToRowAsync(remote.Cut, 500);
        remote.Cut.WaitForAssertion(() => Assert.Equal(440, Grid(remote.Cut).Instance.WindowStart));
        var onScreen = OnScreen(remote.Cut);
        var before = remote.Count;

        // Down to rows 550 on: ten rows below them is the Window's last row, 559 + the rows on
        // screen. Nothing is asked, up the Window or down it.
        foreach (var row in new[] { 520, 540, 550, 470, 450 })
            await ScrollToRowAsync(remote.Cut, row);
        Assert.Equal(before, remote.Count);

        // From row 551, the tenth row below the rows on screen is not held, though every row on
        // screen is: the next Window is asked for, once, and while it is out the rows on screen are
        // painted from the Window held — none is a Placeholder.
        await ScrollToRowAsync(remote.Cut, 550);
        hold = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await ScrollToRowAsync(remote.Cut, 551);
        remote.Cut.WaitForAssertion(() => Assert.Equal(before + 1, remote.Count));
        Assert.Equal(new PivotReportWindow(491, onScreen + 120), remote.Last);
        Assert.Empty(Grid(remote.Cut).FindAll(".ex-viewport .ex-placeholder"));
        Assert.Equal(onScreen, RowTexts(remote.Cut).Count(text => text.StartsWith("Region ", StringComparison.Ordinal)));

        hold.SetResult();
        remote.Cut.WaitForAssertion(() => Assert.Equal(491, Grid(remote.Cut).Instance.WindowStart));
        Assert.Equal(before + 1, remote.Count);
        NoExceptionReachedTheRenderer();
    }

    [Fact] // ADR-0182/0183 (LV-34): a question in flight whose Window covers the rows on screen stays in flight, though the margin is not in it; the next is asked once it lands
    public async Task LV34_A_question_in_flight_that_covers_the_rows_on_screen_stays_in_flight()
    {
        TaskCompletionSource? hold = null;
        await using var remote = RenderRemote(answerWhen: request => hold is { } held && request.Window.Start == 491 ? held.Task : Task.CompletedTask);
        await ScrollToRowAsync(remote.Cut, 500);
        remote.Cut.WaitForAssertion(() => Assert.Equal(440, Grid(remote.Cut).Instance.WindowStart));
        var onScreen = OnScreen(remote.Cut);

        // Ten rows before the Window's end: rows 491 on are asked for, and the answer held.
        hold = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var before = remote.Count;
        await ScrollToRowAsync(remote.Cut, 550);
        await ScrollToRowAsync(remote.Cut, 551);
        remote.Cut.WaitForAssertion(() => Assert.Equal(before + 1, remote.Count));
        Assert.Equal(new PivotReportWindow(491, onScreen + 120), remote.Last);

        // On to rows the question out covers, though the Window held does not, nor the question the
        // ten rows below them: it stays in flight, and nothing else is asked. Its rows past the Window
        // held wait as Placeholders, as they would for any question out (ADR-0001).
        foreach (var row in new[] { 570, 590, 611 })
            await ScrollToRowOrPlaceholderAsync(remote.Cut, row);
        Assert.Equal(before + 1, remote.Count);

        // Once it lands, the grid asks again, ten rows short of the new Window's end: one question.
        hold.SetResult();
        remote.Cut.WaitForAssertion(() => Assert.Equal(before + 2, remote.Count));
        Assert.Equal(new PivotReportWindow(551, onScreen + 120), remote.Last);
        remote.Cut.WaitForAssertion(() => Assert.Equal(551, Grid(remote.Cut).Instance.WindowStart));
        Assert.Equal(before + 2, remote.Count);
        NoExceptionReachedTheRenderer();
    }

    [Fact] // ADR-0182 (LV-34): a question whose Window does not cover the rows on screen is superseded by the next
    public async Task LV34_A_question_in_flight_that_does_not_cover_the_rows_on_screen_is_superseded()
    {
        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var remote = RenderRemote(answerWhen: request => request.Window.Start == 440 ? hold.Task : Task.CompletedTask);
        await ScrollToRowOrPlaceholderAsync(remote.Cut, 500);
        var onScreen = OnScreen(remote.Cut);
        remote.Cut.WaitForAssertion(() => Assert.Equal(new PivotReportWindow(440, onScreen + 120), remote.Last));
        var before = remote.Count;

        // Far past the Window asked for: it covers none of the rows on screen, so another question
        // replaces it, and its answer is what is shown.
        await ScrollToRowAsync(remote.Cut, 800);
        Assert.Equal(before + 1, remote.Count);
        Assert.Equal(new PivotReportWindow(740, onScreen + 120), remote.Last);
        Assert.Equal(740, Grid(remote.Cut).Instance.WindowStart);
        hold.SetResult();
        NoExceptionReachedTheRenderer();
    }

    [Fact] // ADR-0182 (LV-34): the Window asked is clamped into the report at both ends
    public async Task LV34_The_Window_read_ahead_is_clamped_into_the_report()
    {
        await using var remote = RenderRemote();
        await ScrollToRowAsync(remote.Cut, 500);
        var onScreen = OnScreen(remote.Cut);

        // The report's last rows on screen: sixty rows above them, none below.
        var last = 1000 - onScreen;
        await ScrollToRowOrPlaceholderAsync(remote.Cut, last);
        remote.Cut.WaitForAssertion(() => Assert.Equal(new PivotReportWindow(last - 60, onScreen + 60), remote.Last));
        remote.Cut.WaitForAssertion(() => Assert.StartsWith("Grand Total", RowTexts(remote.Cut)[^1], StringComparison.Ordinal));

        // Back at the top: none above, sixty below.
        await ScrollToRowAsync(remote.Cut, 0);
        Assert.Equal(new PivotReportWindow(0, onScreen + 60), remote.Last);
        NoExceptionReachedTheRenderer();
    }

    [Fact] // ADR-0182/0151 (LV-34): a report that shrinks under a Window read ahead is shown at its last rows, the Window clamped into it at its height
    public async Task LV34_A_Window_read_ahead_is_clamped_into_a_report_that_shrank()
    {
        await using var remote = RenderRemote();
        await ScrollToRowAsync(remote.Cut, 500);
        remote.Cut.WaitForAssertion(() => Assert.Equal(440, Grid(remote.Cut).Instance.WindowStart));
        var height = Grid(remote.Cut).Instance.Window.Count;

        // Every deal from the 300th on goes: 301 rows, which end before the Window starts.
        Clock.Advance(TimeSpan.FromSeconds(1));
        await remote.Cut.InvokeAsync(() => remote.Data.Apply(DealFields.Batch(
            removedKeys: Deals.Where(d => d.Id >= 300).Select(d => (object)d.Id))));

        remote.Cut.WaitForAssertion(() => Assert.Equal(301, Grid(remote.Cut).Instance.TotalCount));
        Assert.Equal(new PivotReportWindow(301 - height, height), remote.Last);
        Assert.Equal(301 - height, Grid(remote.Cut).Instance.WindowStart);
        remote.Cut.WaitForAssertion(() => Assert.StartsWith("Grand Total", RowTexts(remote.Cut)[^1], StringComparison.Ordinal));
        Assert.False(remote.Cut.Instance.IsStale);
        NoExceptionReachedTheRenderer();
    }

    [Fact] // ADR-0182 (LV-34): read ahead 0 asks for exactly the rows on screen, as ExPivot did before
    public async Task LV34_A_source_that_reads_nothing_ahead_is_asked_for_exactly_the_rows_on_screen()
    {
        await using var remote = RenderRemote(readAheadRows: 0);
        await ScrollToRowAsync(remote.Cut, 500);

        Assert.Equal(new PivotReportWindow(500, OnScreen(remote.Cut)), remote.Last);
        Assert.Equal(0, Grid(remote.Cut).Instance.RangeRequestMargin);
    }

    [Fact] // ADR-0182 (LV-34): an answer that does not reach the rows the grid needs is refused by name, and the report stays
    public async Task LV34_An_answer_short_of_the_rows_the_grid_needs_is_refused_by_name()
    {
        // A relay that caps its answers at 20 rows from the start of the Window asked: short of the
        // rows on screen, which start 60 rows in.
        await using var remote = RenderRemote(answer: update => update.Rows is { Count: > 20 } rows && update.Window.Start > 0
            ? update with { Window = update.Window with { Count = 20 }, Rows = [.. rows.Take(20)] }
            : update);
        var shown = Grid(remote.Cut).Instance.Window;

        await ScrollToRowOrPlaceholderAsync(remote.Cut, 500);

        remote.Cut.WaitForAssertion(() => Assert.True(remote.Cut.Instance.IsStale));
        var message = remote.Cut.Instance.LastError?.Message ?? "";
        Assert.Contains("rows 440", message, StringComparison.Ordinal);
        Assert.Contains("ADR-0182", message, StringComparison.Ordinal);
        // Nothing of the answer reached the grid: it holds the Window it had.
        Assert.Same(shown, Grid(remote.Cut).Instance.Window);
        Assert.Equal(0, Grid(remote.Cut).Instance.WindowStart);
        NoExceptionReachedTheRenderer();
    }

    [Fact] // ADR-0182/0152 (LV-34): Window Changes and their Window Digest cover the whole Window read ahead — a row changed out of sight is shown when scrolled to, asking nothing
    public async Task LV34_Window_Changes_cover_the_rows_read_ahead()
    {
        await using var remote = RenderRemote();
        await ScrollToRowAsync(remote.Cut, 500);
        remote.Cut.WaitForAssertion(() => Assert.Equal(440, Grid(remote.Cut).Instance.WindowStart));
        var asked = remote.Count;

        // Region 0450 is ten rows into the Window, out of sight above the rows on screen.
        Clock.Advance(TimeSpan.FromSeconds(1));
        await remote.Cut.InvokeAsync(() => remote.Data.Apply(DealFields.Batch(changed: [Deals[450] with { Amount = 4500m }])));

        remote.Cut.WaitForAssertion(() => Assert.Equal(asked + 1, remote.Count));
        remote.Cut.WaitForAssertion(() => Assert.Contains(remote.Answers, a => a.Rows is null && a.Changes.Count > 0));
        PivotReportUpdate changes;
        lock (remote.Asked)
            changes = remote.Answers[^1];
        // Window Changes against the Window held, read ahead, naming its digest: the row changed is
        // ten rows in, and the server compared every row of the Window to find it.
        Assert.Null(changes.Rows);
        Assert.Equal(Grid(remote.Cut).Instance.Window.Count, changes.Window.Count);
        Assert.Equal(440, changes.Window.Start);
        Assert.NotNull(changes.WindowDigest);
        Assert.Equal([10], changes.Changes.Select(change => change.Offset));
        Assert.False(remote.Cut.Instance.IsStale);

        // Scrolled up to it: painted from the Window held, nothing asked.
        await ScrollToRowAsync(remote.Cut, 450);
        Assert.Equal("Region 0450 | 4500", RowTexts(remote.Cut)[0]);
        Assert.Equal(asked + 1, remote.Count);
        NoExceptionReachedTheRenderer();
    }
}
