using System.Globalization;
using Bunit;
using ExPivot.Chrome;
using ExPivot.Components.Tests.Support;
using ExPivot.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using PivotComponent = ExPivot.Components.ExPivot;

namespace ExPivot.Components.Tests;

/// <summary>
/// The Stale Report (ADR-0067, PV-37): when the newest data cannot be shown — it breaks a cap, or
/// the source refuses or fails — the report stays on the last version it could compute, and a
/// notice under the Pivot Toolbar says what happened and as of when, with Retry, which asks again.
/// The notice goes when an answer is laid out. A refused layout the user asked for is not a Stale
/// Report: the layout goes back, and the Pivot Toolbar says so. The clock is the test's.
/// </summary>
public class StaleReportTests : PivotTestContext
{
    private static readonly PivotLayout RegionAmount = new() { Rows = [P("Region")], Values = [Sum("Amount")] };

    private static readonly PivotLayout RegionProduct = new() { Rows = [P("Region"), P("Product")], Values = [Sum("Amount")] };

    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    private static readonly Sale South = new("South", "Apples", 1m, 1, true);

    private static Sale[] EastApples(decimal amount) => [Sales[0] with { Amount = amount }, .. Sales[1..]];

    private static Task PublishAsync(IRenderedComponent<PivotComponent> cut, LiveSource source, IReadOnlyList<Sale> records)
        => cut.InvokeAsync(() => source.Publish(records));

    public StaleReportTests() => Clock.SetUtcNow(new DateTimeOffset(2026, 10, 1, 14, 32, 5, TimeSpan.Zero));

    /// <summary>The time a notice writes for an instant, as the report's culture writes it.</summary>
    private string TimeOf(DateTimeOffset at) => TimeZoneInfo.ConvertTime(at, Clock.LocalTimeZone).ToString("T", English);

    private static string Notice(IRenderedComponent<PivotComponent> cut) => cut.Find(".ex-pivot-stale .ex-pivot-stale-message").TextContent;

    [Fact] // ADR-0067/0066 (PV-37): newer data over the leaves' cap leaves the report as it was, and the notice says so, as of the time of the data shown, with Retry
    public async Task Newer_data_over_the_leaf_cap_leaves_a_stale_report()
    {
        var source = new LiveSource();
        var told = new List<PivotLayout>();
        var cut = RenderPivot(RegionAmount, ps => ps.Add(p => p.Caps, new PivotCaps { MaxLeaves = 4 }).Add(p => p.LayoutChanged, told.Add), source: source);
        var shownAt = Clock.GetUtcNow();
        var before = RowTexts(cut);
        var layout = cut.Instance.CurrentLayout;
        Assert.Empty(cut.FindAll(".ex-pivot-stale-notice"));
        Assert.Equal("status", cut.Find(".ex-pivot-stale").GetAttribute("role"));
        Assert.False(cut.Instance.IsStale);
        Clock.Advance(TimeSpan.FromSeconds(7));

        await PublishAsync(cut, source, [.. Sales, South]);

        Assert.Equal(before, RowTexts(cut));
        Assert.True(cut.Instance.IsStale);
        Assert.Equal($"Showing the data as of {TimeOf(shownAt)}: the newest data needs more than 4 cells.", Notice(cut));
        Assert.Equal("Retry", cut.Find(".ex-pivot-stale .ex-pivot-retry").TextContent);
        Assert.False(cut.Find(".ex-pivot-retry").HasAttribute("disabled"));
        // The report's layout stays; it was not refused, and nothing is said about it.
        Assert.Same(layout, cut.Instance.CurrentLayout);
        Assert.Equal(["Region"], AreaEntries(cut, "Rows"));
        Assert.Empty(cut.FindAll(".ex-pivot-refusal-notice"));
        Assert.Empty(told);
    }

    [Fact] // ADR-0067 (PV-37): newer data over the rows' cap leaves a stale report; a layout that fits lays the newest answer out, and the notice goes
    public async Task Newer_data_over_the_row_cap_leaves_a_stale_report_until_a_layout_fits()
    {
        var source = new LiveSource();
        var cut = RenderPivot(RegionProduct, ps => ps.Add(p => p.Caps, new PivotCaps { MaxRows = 11 }), source: source);
        var shownAt = Clock.GetUtcNow();
        Assert.Equal(11, RowTexts(cut).Length);
        Clock.Advance(TimeSpan.FromMinutes(1));

        await PublishAsync(cut, source, [.. Sales, South]);

        Assert.Equal(11, RowTexts(cut).Length);
        Assert.Equal($"Showing the data as of {TimeOf(shownAt)}: the newest data needs more than 11 rows.", Notice(cut));

        // Collapsing East lays the answer held — the newest — out in 11 rows.
        await cut.FindAll(".ex-pivot-toggle")[0].ClickAsync(new MouseEventArgs());

        Assert.Contains("−South | 1", RowTexts(cut));
        Assert.Equal(11, RowTexts(cut).Length);
        Assert.Empty(cut.FindAll(".ex-pivot-stale-notice"));
        Assert.False(cut.Instance.IsStale);
    }

    [Fact] // ADR-0067/0025 (PV-37): a source that fails leaves the report as it was, stale, saying so; the failure is kept
    public async Task A_failing_source_leaves_a_stale_report()
    {
        var source = new LiveSource();
        var cut = RenderPivot(RegionAmount, source: source);
        var shownAt = Clock.GetUtcNow();
        var before = RowTexts(cut);
        Clock.Advance(TimeSpan.FromSeconds(3));
        source.Fails = new InvalidOperationException("The server is unreachable.");

        await PublishAsync(cut, source, EastApples(101));

        Assert.Equal(before, RowTexts(cut));
        Assert.Equal($"Showing the data as of {TimeOf(shownAt)}: the source could not answer: The server is unreachable.", Notice(cut));
        Assert.IsType<InvalidOperationException>(cut.Instance.LastError);
        Assert.Empty(cut.FindAll(".ex-pivot-refusal-notice"));
    }

    [Fact] // ADR-0067 (PV-37): a source that refuses newer data leaves a stale report saying why
    public async Task A_refusing_source_leaves_a_stale_report()
    {
        var source = new LiveSource();
        var cut = RenderPivot(RegionAmount, source: source);
        var shownAt = Clock.GetUtcNow();
        source.Refuses = new PivotSourceRefusal(PivotSourceRefusalKind.SourceVersionNotHeld, "The data is being reloaded.");

        await PublishAsync(cut, source, EastApples(101));

        Assert.Equal("East | 180", RowTexts(cut)[0]);
        Assert.Equal($"Showing the data as of {TimeOf(shownAt)}: the source refused to answer: The data is being reloaded.", Notice(cut));
    }

    [Fact] // ADR-0067 (PV-37): Retry asks again — under the loading indication, not twice at once — and the notice goes when the answer is laid out, which marks what changed
    public async Task Retry_asks_again_and_the_notice_goes_on_success()
    {
        var source = new LiveSource();
        var cut = RenderPivot(RegionAmount, source: source);
        source.Fails = new InvalidOperationException("The server is unreachable.");
        await PublishAsync(cut, source, EastApples(101));
        Assert.Single(cut.FindAll(".ex-pivot-stale-notice"));
        var asked = source.Questions.Count;
        source.Fails = null;
        source.AnswersAtOnce = false;

        await cut.Find(".ex-pivot-retry").ClickAsync(new MouseEventArgs());

        Assert.Equal(asked + 1, source.Questions.Count);
        Assert.Equal(source.Questions[0].Query, source.Questions[^1].Query);
        Assert.True(cut.Instance.IsLoading);
        Assert.True(cut.Find(".ex-pivot-retry").HasAttribute("disabled"));
        Assert.Single(cut.FindAll(".ex-pivot-stale-notice"));

        await cut.InvokeAsync(source.Questions[^1].AnswerAsync);

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".ex-pivot-stale-notice")));
        Assert.False(cut.Instance.IsStale);
        Assert.Null(cut.Instance.LastError);
        Assert.Equal("East | 181", RowTexts(cut)[0]);
        Assert.Equal(["181", "286"], ChangeHighlightTests.MarkedTexts(cut));
    }

    [Fact] // ADR-0067 (PV-37): a Retry that fails again leaves the report stale, as of the same time
    public async Task A_failed_retry_stays_stale()
    {
        var source = new LiveSource();
        var cut = RenderPivot(RegionAmount, source: source);
        var shownAt = Clock.GetUtcNow();
        source.Fails = new InvalidOperationException("The server is unreachable.");
        await PublishAsync(cut, source, EastApples(101));
        Clock.Advance(TimeSpan.FromSeconds(30));
        source.Fails = new InvalidOperationException("The server is still unreachable.");

        await cut.Find(".ex-pivot-retry").ClickAsync(new MouseEventArgs());

        Assert.Equal($"Showing the data as of {TimeOf(shownAt)}: the source could not answer: The server is still unreachable.", Notice(cut));
        Assert.False(cut.Instance.IsLoading);
        Assert.Equal("East | 180", RowTexts(cut)[0]);
    }

    [Fact] // ADR-0067 (PV-37): the next change of data that can be shown takes the notice away
    public async Task The_next_change_that_can_be_shown_takes_the_notice_away()
    {
        var source = new LiveSource();
        var cut = RenderPivot(RegionAmount, source: source);
        source.Fails = new InvalidOperationException("The server is unreachable.");
        await PublishAsync(cut, source, EastApples(101));
        Assert.True(cut.Instance.IsStale);
        source.Fails = null;
        Clock.Advance(TimeSpan.FromMilliseconds(250));

        await PublishAsync(cut, source, EastApples(102));

        Assert.False(cut.Instance.IsStale);
        Assert.Empty(cut.FindAll(".ex-pivot-stale-notice"));
        Assert.Equal("East | 182", RowTexts(cut)[0]);
    }

    [Fact] // ADR-0067 (PV-37): a gesture laid out from the answer held brings no newer data, so the report stays stale, as of the same time
    public async Task A_gesture_from_the_held_answer_leaves_the_report_stale()
    {
        var source = new LiveSource();
        var cut = RenderPivot(RegionProduct, source: source);
        var shownAt = Clock.GetUtcNow();
        Clock.Advance(TimeSpan.FromSeconds(5));
        source.Fails = new InvalidOperationException("The server is unreachable.");
        await PublishAsync(cut, source, EastApples(101));
        var asked = source.Questions.Count;

        await cut.FindAll(".ex-pivot-toggle")[0].ClickAsync(new MouseEventArgs());

        Assert.Equal("+East | 180", RowTexts(cut)[0]);
        Assert.Equal(asked, source.Questions.Count);
        Assert.True(cut.Instance.IsStale);
        Assert.Equal($"Showing the data as of {TimeOf(shownAt)}: the source could not answer: The server is unreachable.", Notice(cut));
    }

    [Fact] // ADR-0067 (PV-37): the as-of time carries its date when the data shown is not today's
    public async Task The_time_carries_its_date_when_not_today()
    {
        var source = new LiveSource();
        var cut = RenderPivot(RegionAmount, source: source);
        var shownAt = Clock.GetUtcNow();
        Clock.Advance(TimeSpan.FromDays(1));
        source.Fails = new InvalidOperationException("The server is unreachable.");

        await PublishAsync(cut, source, EastApples(101));

        var asOf = TimeZoneInfo.ConvertTime(shownAt, Clock.LocalTimeZone).ToString("G", English);
        Assert.StartsWith($"Showing the data as of {asOf}: ", Notice(cut));
    }

    [Fact] // ADR-0066/0067 (PV-37, PV-29): a layout the user asked for and a cap refused is not a Stale Report — the layout goes back, and the Pivot Toolbar says so
    public async Task A_refused_layout_is_not_a_stale_report()
    {
        var source = new LiveSource();
        var cut = RenderPivot(RegionAmount, ps => ps.Add(p => p.Caps, new PivotCaps { MaxLeaves = 4 }), source: source);

        await FieldItem(cut, "Product").DragStartAsync(new DragEventArgs());
        await AreaElement(cut, "Columns").DropAsync(new DragEventArgs());

        Assert.Equal("This layout needs more than 4 cells.", cut.Find(".ex-pivot-refusal-notice").TextContent);
        Assert.Equal("alert", cut.Find(".ex-pivot-refusal-notice").GetAttribute("role"));
        Assert.Empty(AreaEntries(cut, "Columns"));
        Assert.Empty(cut.FindAll(".ex-pivot-stale-notice"));
        Assert.False(cut.Instance.IsStale);
    }

    [Fact] // ADR-0067/0061 (PV-37): a refusal of the user's layout and a Stale Report stand in one place, each saying its own thing, and a change of data leaves the refusal standing
    public async Task A_refusal_and_a_stale_report_stand_together()
    {
        var source = new LiveSource();
        var cut = RenderPivot(RegionAmount, ps => ps.Add(p => p.Caps, new PivotCaps { MaxLeaves = 4 }), source: source);
        await FieldItem(cut, "Product").DragStartAsync(new DragEventArgs());
        await AreaElement(cut, "Columns").DropAsync(new DragEventArgs());

        await PublishAsync(cut, source, [.. Sales, South]);

        Assert.Equal("This layout needs more than 4 cells.", cut.Find(".ex-pivot-refusal-notice").TextContent);
        Assert.EndsWith(": the newest data needs more than 4 cells.", Notice(cut));
        var report = cut.Find(".ex-pivot-report");
        var order = report.Children.Select(c => c.ClassName).ToArray();
        Assert.True(Array.IndexOf(order, "ex-pivot-refusal-notice") < Array.IndexOf(order, "ex-pivot-stale"));
        Assert.True(Array.IndexOf(order, "ex-pivot-stale") < Array.IndexOf(order, "ex-pivot-sheet"));
    }

    [Fact] // ADR-0061/0067 (PV-9, PV-37): the notice is the Chrome's to draw, inside ExPivot's live region — handed the sentence, the reason, the time and Retry — and its Retry asks as the built-in's does
    public async Task The_notice_through_a_substituted_chrome()
    {
        var chrome = new StaleChrome();
        var source = new LiveSource();
        var cut = RenderPivot(RegionAmount, ps => ps.Add(p => p.PivotChrome, chrome), source: source);
        var shownAt = Clock.GetUtcNow();
        Assert.Null(chrome.Stale);
        source.Fails = new InvalidOperationException("The server is unreachable.");

        await PublishAsync(cut, source, EastApples(101));

        var context = chrome.Stale!;
        Assert.Equal($"Showing the data as of {TimeOf(shownAt)}: the source could not answer: The server is unreachable.", context.Message);
        Assert.Equal("the source could not answer: The server is unreachable.", context.Reason);
        Assert.Equal(shownAt, context.AsOf);
        Assert.Equal(TimeOf(shownAt), context.AsOfText);
        Assert.Equal(PivotCommandIds.Retry, context.Retry.Id);
        Assert.Equal("Retry", context.Retry.Label);
        Assert.True(context.Retry.Enabled);
        Assert.Single(cut.FindAll(".ex-pivot-stale[role=status] .stub-stale"));
        Assert.Empty(cut.FindAll(".ex-pivot-stale-notice"));

        source.Fails = null;
        var asked = source.Questions.Count;
        await cut.InvokeAsync(context.Retry.Invoke);

        Assert.Equal(asked + 1, source.Questions.Count);
        Assert.Empty(cut.FindAll(".stub-stale"));
        Assert.Equal("East | 181", RowTexts(cut)[0]);
    }

    [Fact] // ADR-0060/0067 (PV-37): the notice's words are ExPivot's, by id, which the Consumer's Label replaces
    public async Task The_notices_words_are_replaced_by_id()
    {
        var source = new LiveSource();
        var words = new Dictionary<string, string>
        {
            ["stale-report"] = "As of {0} — {1}",
            ["stale-source-failed"] = "no answer ({0})",
            [PivotCommandIds.Retry] = "Try again",
        };
        var cut = RenderPivot(RegionAmount, ps => ps.Add(p => p.Label, id => words.GetValueOrDefault(id)), source: source);
        var shownAt = Clock.GetUtcNow();
        source.Fails = new InvalidOperationException("down");

        await PublishAsync(cut, source, EastApples(101));

        Assert.Equal($"As of {TimeOf(shownAt)} — no answer (down)", Notice(cut));
        Assert.Equal("Try again", cut.Find(".ex-pivot-retry").TextContent);
    }

    // ---- PV-47: a redraw that runs out of memory ----------------------------------------------

    /// <summary>A keyed sale, for Change Batches.</summary>
    public sealed record Trade(long Id, string Region, decimal Amount);

    [Fact] // ADR-0067's note of 2026-10-07 (PV-47): a live redraw that runs out of memory while the report is made leaves the report on screen, stale, saying memory ran out; nothing reaches the renderer, and the next change asks again
    public async Task A_redraw_out_of_memory_while_the_report_is_made_leaves_it_stale()
    {
        var starved = false;
        // The engine asks a field's Order Key of each new Item as it lays the report out.
        var fields = PivotFields.Of<Trade>()
            .Key("Id", t => t.Id)
            .Text("Region", t => t.Region, orderKey: region => starved ? throw new OutOfMemoryException() : region)
            .Number("Amount", t => t.Amount);
        var source = PivotSource.From([new Trade(1, "East", 100m), new Trade(2, "West", 70m)], fields);
        var cut = RenderPivot(RegionAmount, source: source);
        var shownAt = Clock.GetUtcNow();
        var report = cut.Instance.Report;
        Clock.Advance(TimeSpan.FromSeconds(3));
        starved = true;

        await cut.InvokeAsync(() => source.Apply(fields.Batch(added: [new Trade(3, "North", 10m)])));

        Assert.Same(report, cut.Instance.Report);
        Assert.Equal(["East | 100", "West | 70", "Grand Total | 170"], RowTexts(cut));
        Assert.True(cut.Instance.IsStale);
        Assert.Equal($"Showing the data as of {TimeOf(shownAt)}: memory ran out while the newest data was laid out.", Notice(cut));
        Assert.Null(cut.Instance.LastError);
        Assert.False(cut.Instance.IsLoading);
        Assert.False(Renderer.UnhandledException.IsCompleted);

        starved = false;
        Clock.Advance(PivotComponent.DefaultRedrawInterval);
        await cut.InvokeAsync(() => source.Apply(fields.Batch(added: [new Trade(4, "South", 1m)])));

        cut.WaitForAssertion(() => Assert.False(cut.Instance.IsStale));
        Assert.Equal(["East | 100", "North | 10", "South | 1", "West | 70", "Grand Total | 181"], RowTexts(cut));
        Assert.False(Renderer.UnhandledException.IsCompleted);
    }

    [Fact] // ADR-0067's note of 2026-10-07 (PV-47): a live redraw that runs out of memory while its batch is folded into the cube leaves the report stale in the same way, and the next change computes afresh
    public async Task A_redraw_out_of_memory_while_the_cube_is_made_leaves_it_stale()
    {
        var starved = false;
        // ExPivot's slices: every look at the clock ends one, and a yield while starved runs out of
        // memory — the first is in the cube a batch of three thousand changed records updates.
        var slicing = new PivotSlicing
        {
            Budget = TimeSpan.Zero,
            Yield = _ => starved ? throw new OutOfMemoryException() : ValueTask.CompletedTask,
        };
        var fields = PivotFields.Of<Trade>()
            .Key("Id", t => t.Id)
            .Text("Region", t => t.Region)
            .Number("Amount", t => t.Amount);
        var trades = Enumerable.Range(0, 3_000).Select(i => new Trade(i, "R" + i.ToString("0000", CultureInfo.InvariantCulture), 1m)).ToArray();
        var source = PivotSource.From(trades, fields, new PivotSlicing { Budget = TimeSpan.FromDays(1) });
        var cut = RenderPivot(RegionAmount, ps => ps.Add(p => p.Slicing, slicing), source: source);
        var shownAt = Clock.GetUtcNow();
        var report = cut.Instance.Report;
        Assert.Equal("R0000 | 1", RowTexts(cut)[0]);
        starved = true;

        await cut.InvokeAsync(() => source.Apply(fields.Batch(changed: [.. trades.Select(t => t with { Amount = 2m })])));

        Assert.Same(report, cut.Instance.Report);
        Assert.Equal("R0000 | 1", RowTexts(cut)[0]);
        Assert.Equal($"Showing the data as of {TimeOf(shownAt)}: memory ran out while the newest data was laid out.", Notice(cut));
        Assert.Null(cut.Instance.LastError);
        Assert.False(Renderer.UnhandledException.IsCompleted);

        starved = false;
        Clock.Advance(PivotComponent.DefaultRedrawInterval);
        await cut.InvokeAsync(() => source.Apply(fields.Batch(changed: [trades[1] with { Amount = 3m }])));

        cut.WaitForAssertion(() => Assert.False(cut.Instance.IsStale));
        Assert.Equal(["R0000 | 2", "R0001 | 3"], RowTexts(cut)[..2]);
        Assert.False(Renderer.UnhandledException.IsCompleted);
    }

    [Fact] // ADR-0067's note of 2026-10-07 (PV-47): the reason's words are ExPivot's, by id, in Excel's Japanese edition too
    public async Task The_out_of_memory_reason_is_worded_by_id()
    {
        var starved = false;
        var fields = PivotFields.Of<Trade>()
            .Key("Id", t => t.Id)
            .Text("Region", t => t.Region, orderKey: region => starved ? throw new OutOfMemoryException() : region)
            .Number("Amount", t => t.Amount);
        var source = PivotSource.From([new Trade(1, "East", 100m)], fields);
        var cut = RenderPivot(RegionAmount, ps => ps.Add(p => p.Label, PivotWords.Japanese), source: source);
        starved = true;

        await cut.InvokeAsync(() => source.Apply(fields.Batch(added: [new Trade(2, "West", 1m)])));

        var word = PivotWords.Japanese("stale-out-of-memory");
        Assert.NotNull(word);
        Assert.EndsWith(": " + word, Notice(cut));
    }

    /// <summary>A keyed sale with a desk, for a layout that adds a field.</summary>
    public sealed record DeskTrade(long Id, string Region, string Desk, decimal Amount);

    [Fact] // ADR-0067/0066: a layout the user asks for that runs out of memory is not a Stale Report: it is refused, the layout goes back to the one the report shows, and the Pivot Toolbar says memory ran out
    public async Task A_layout_out_of_memory_is_refused_and_goes_back()
    {
        var starved = false;
        // Desk's Items are ordered — their Order Key asked — as the first report with Desk is laid out.
        var fields = PivotFields.Of<DeskTrade>()
            .Key("Id", t => t.Id)
            .Text("Region", t => t.Region)
            .Text("Desk", t => t.Desk, orderKey: desk => starved ? throw new OutOfMemoryException() : desk)
            .Number("Amount", t => t.Amount);
        var source = PivotSource.From([new DeskTrade(1, "East", "Rates", 100m), new DeskTrade(2, "West", "FX", 70m)], fields);
        var told = new List<PivotLayout>();
        var cut = RenderPivot(RegionAmount, ps => ps.Add(p => p.LayoutChanged, told.Add), source: source);
        var layout = cut.Instance.CurrentLayout;
        starved = true;

        await TickFieldAsync(cut, "Desk", true);

        cut.WaitForAssertion(() => Assert.Equal("Memory ran out while this layout was laid out.", cut.Find(".ex-pivot-refusal-notice").TextContent.Trim()));
        Assert.False(cut.Instance.IsStale);
        Assert.Same(layout, cut.Instance.CurrentLayout);
        Assert.Equal(["Region"], AreaEntries(cut, "Rows"));
        Assert.Equal(["East | 100", "West | 70", "Grand Total | 170"], RowTexts(cut));
        Assert.Empty(told);
        Assert.Null(cut.Instance.LastError);
        Assert.False(Renderer.UnhandledException.IsCompleted);

        // With memory again, the same gesture lays the layout out.
        starved = false;
        await TickFieldAsync(cut, "Desk", true);
        cut.WaitForAssertion(() => Assert.Equal(["Region", "Desk"], AreaEntries(cut, "Rows")));
        Assert.Single(told);
    }

    /// <summary>A Chrome that draws a stub for the Stale Report's notice and keeps the context it
    /// was last handed.</summary>
    private sealed class StaleChrome : IPivotChrome
    {
        public PivotStaleReportContext? Stale { get; private set; }

        RenderFragment? IPivotChrome.StaleReport(PivotStaleReportContext context)
        {
            Stale = context;
            return builder => builder.AddMarkupContent(0, "<div class='stub-stale'></div>");
        }
    }
}
