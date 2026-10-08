using Bunit;
using ExGrid.Components;
using ExGrid.Rows;
using ExGrid.Selection;
using ExPivot.Components.Tests.Support;
using ExPivot.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using PivotComponent = ExPivot.Components.ExPivot;

namespace ExPivot.Components.Tests;

/// <summary>
/// Live data in the component (ADR-0067): ExPivot listens to its source's <c>Changed</c> and asks
/// again for the whole answer — the bundled source's and a server's alike — gathering the changes
/// and redrawing at most once per interval, from the newest version, never half a batch. A change
/// of values keeps the Row Sequence Version, the Selection and an open menu or panel; an Item that
/// appears or leaves drops the Selection (ADR-0011). A change of data never cancels a question out,
/// and a user's layout always wins over a refresh. The clock is the test's.
/// </summary>
public class LiveDataTests : PivotTestContext
{
    private static readonly PivotLayout RegionAmount = new() { Rows = [P("Region")], Values = [Sum("Amount")] };

    private static readonly PivotLayout RegionProduct = new() { Rows = [P("Region"), P("Product")], Values = [Sum("Amount")] };

    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(250);

    /// <summary>The sales with East's first Apples sold for <paramref name="amount"/> instead of 100.</summary>
    private static Sale[] EastApples(decimal amount) => [Sales[0] with { Amount = amount }, .. Sales[1..]];

    private static readonly Sale South = new("South", "Apples", 1m, 1, true);

    private static Task PublishAsync(IRenderedComponent<PivotComponent> cut, LiveSource source, IReadOnlyList<Sale> records)
        => cut.InvokeAsync(() => source.Publish(records));

    // ---- PV-35: gathered, and redrawn at most every 250 ms --------------------------------------

    [Fact] // ADR-0067 (PV-35): changes are gathered and the report redrawn at most every 250 ms, from the newest version
    public async Task Changes_are_gathered_and_redrawn_at_most_every_250_ms()
    {
        var source = new LiveSource();
        var cut = RenderPivot(RegionAmount, source: source);
        Assert.Single(source.Questions);
        Assert.Equal(PivotComponent.DefaultRedrawInterval, cut.Instance.RedrawInterval);

        // The first change after a quiet spell is asked for at once.
        await PublishAsync(cut, source, EastApples(101));
        Assert.Equal(2, source.Questions.Count);
        Assert.Equal("East | 181", RowTexts(cut)[0]);

        // Within 250 ms of it, the next ones are gathered.
        await PublishAsync(cut, source, EastApples(102));
        await PublishAsync(cut, source, EastApples(103));
        Clock.Advance(Interval - TimeSpan.FromMilliseconds(1));
        Assert.Equal(2, source.Questions.Count);
        Assert.Equal("East | 181", RowTexts(cut)[0]);

        // And asked for once, from the newest, when the interval has passed.
        Clock.Advance(TimeSpan.FromMilliseconds(1));
        await source.QuestionAsync(2, Xunit.TestContext.Current.CancellationToken);
        Assert.Equal(3, source.Questions.Count);
        cut.WaitForAssertion(() => Assert.Equal("East | 183", RowTexts(cut)[0]));
        Assert.Equal(source.Questions[0].Query, source.Questions[2].Query);

        Clock.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(3, source.Questions.Count);
    }

    [Fact] // ADR-0067 (PV-35): the Consumer sets the interval, and 0 redraws on every change
    public async Task A_zero_interval_redraws_on_every_change()
    {
        var source = new LiveSource();
        var cut = RenderPivot(RegionAmount, ps => ps.Add(p => p.RedrawInterval, TimeSpan.Zero), source: source);

        foreach (var amount in new[] { 101m, 102m, 103m })
        {
            await PublishAsync(cut, source, EastApples(amount));
            Assert.Equal($"East | {amount + 80}", RowTexts(cut)[0]);
        }

        Assert.Equal(4, source.Questions.Count);
    }

    [Fact] // ADR-0067 (PV-35): the Consumer's interval is honoured
    public async Task The_consumers_interval_is_honoured()
    {
        var source = new LiveSource();
        var cut = RenderPivot(RegionAmount, ps => ps.Add(p => p.RedrawInterval, TimeSpan.FromSeconds(2)), source: source);
        await PublishAsync(cut, source, EastApples(101));
        await PublishAsync(cut, source, EastApples(102));

        Clock.Advance(TimeSpan.FromMilliseconds(1999));
        Assert.Equal(2, source.Questions.Count);
        Clock.Advance(TimeSpan.FromMilliseconds(1));

        cut.WaitForAssertion(() => Assert.Equal("East | 182", RowTexts(cut)[0]));
        Assert.Equal(3, source.Questions.Count);
    }

    [Fact] // ADR-0067 (PV-35): a negative interval is refused by name
    public void A_negative_interval_is_refused()
    {
        var refusal = Assert.Throws<ArgumentOutOfRangeException>(
            () => RenderPivot(RegionAmount, ps => ps.Add(p => p.RedrawInterval, TimeSpan.FromMilliseconds(-1))));

        Assert.Equal(nameof(PivotComponent.RedrawInterval), refusal.ParamName);
    }

    [Fact] // ADR-0067/0066 (PV-35): a question for newer data in flight is not cancelled by further changes; they are asked for, from the newest, once it lands
    public async Task Changes_during_a_question_are_asked_for_after_it_lands()
    {
        var source = new LiveSource();
        var cut = RenderPivot(RegionAmount, source: source);
        source.AnswersAtOnce = false;

        await PublishAsync(cut, source, EastApples(101));
        var first = source.Questions[1];
        await PublishAsync(cut, source, EastApples(102));
        await PublishAsync(cut, source, EastApples(103));

        Assert.Equal(2, source.Questions.Count);
        Assert.False(first.IsCancelled);
        // Asked quietly: the report on screen is the newest there is until the answer lands.
        Assert.False(cut.Instance.IsLoading);
        Assert.False(Grid(cut).Instance.IsLoading);

        await cut.InvokeAsync(first.AnswerAsync);
        cut.WaitForAssertion(() => Assert.Equal("East | 181", RowTexts(cut)[0]));
        Assert.Equal(2, source.Questions.Count);

        Clock.Advance(Interval);
        await source.QuestionAsync(2, Xunit.TestContext.Current.CancellationToken);
        Assert.Equal(3, source.Questions.Count);
        await cut.InvokeAsync(source.Questions[2].AnswerAsync);
        cut.WaitForAssertion(() => Assert.Equal("East | 183", RowTexts(cut)[0]));
    }

    [Fact] // ADR-0067 (PV-35): never half a batch — every cell of a redraw comes from one answer
    public async Task A_redraw_is_one_answer()
    {
        var source = new LiveSource();
        var cut = RenderPivot(RegionAmount, source: source);

        // A batch that moves a sale from East to West: before it, or after it, never between.
        Sale[] moved = [Sales[0] with { Region = "West" }, .. Sales[1..]];
        await PublishAsync(cut, source, moved);

        Assert.Equal(["East | 80", "North | 10", "West | 190", "(blank) | 5", "Grand Total | 285"], RowTexts(cut));
        Assert.Single(source.AnsweredVersions.Skip(1).Distinct());
    }

    // ---- PV-35: what a change of values keeps, and what an Item appearing drops ------------------

    [Fact] // ADR-0067/0011 (PV-35, PV-13): a change of values alone keeps the Row Sequence Version, and so the Selection
    public async Task A_change_of_values_keeps_the_selection()
    {
        var selections = new List<GridSelection>();
        var source = new LiveSource();
        var cut = RenderPivot(RegionAmount, ps => ps.Add(p => p.SelectionChanged, selections.Add), source: source);
        var version = Grid(cut).Instance.RowSequenceVersion;
        await cut.InvokeAsync(() => Grid(cut).Instance.PlaceSelectionAsync(new SelectionRange(0, 1, 3, 1), new CellPosition(0, 1), version));
        cut.WaitForAssertion(() => Assert.False(Assert.Single(selections).IsEmpty));

        await PublishAsync(cut, source, EastApples(101));

        Assert.Equal("East | 181", RowTexts(cut)[0]);
        Assert.Equal(version, Grid(cut).Instance.RowSequenceVersion);
        Assert.Single(selections);
        Assert.NotEmpty(cut.FindAll(".ex-selection .ex-range"));
    }

    [Theory] // ADR-0067/0011 (PV-35): an Item that appears or leaves changes the rows, and the Selection is dropped
    [InlineData("appears")]
    [InlineData("leaves")]
    public async Task An_item_that_appears_or_leaves_drops_the_selection(string how)
    {
        var selections = new List<GridSelection>();
        var source = new LiveSource();
        var cut = RenderPivot(RegionAmount, ps => ps.Add(p => p.SelectionChanged, selections.Add), source: source);
        var version = Grid(cut).Instance.RowSequenceVersion;
        await cut.InvokeAsync(() => Grid(cut).Instance.PlaceSelectionAsync(new SelectionRange(0, 1, 1, 1), new CellPosition(0, 1), version));
        cut.WaitForAssertion(() => Assert.Single(selections));

        await PublishAsync(cut, source, how == "appears" ? [.. Sales, South] : Sales.Where(s => s.Region != "North").ToArray());

        Assert.NotEqual(version, Grid(cut).Instance.RowSequenceVersion);
        cut.WaitForAssertion(() => Assert.True(selections[^1].IsEmpty));
        Assert.Empty(cut.FindAll(".ex-selection .ex-range"));
    }

    [Fact] // ADR-0067 (PV-35): an open panel stays open across a change of values, its draft kept
    public async Task An_open_panel_stays_open_across_a_change_of_values()
    {
        var source = new LiveSource();
        var cut = RenderPivot(RegionAmount, source: source);
        await OpenMenuAsync(cut, "Values", "Sum of Amount");
        await RunMenuAsync(cut, "Value Field Settings…");
        await cut.Find(".ex-pivot-value-settings input[type=text]").InputAsync(new ChangeEventArgs { Value = "Takings" });

        await PublishAsync(cut, source, EastApples(101));

        Assert.Equal("East | 181", RowTexts(cut)[0]);
        Assert.Single(cut.FindAll(".ex-pivot-value-settings"));
        Assert.Equal("Takings", cut.Find(".ex-pivot-value-settings input[type=text]").GetAttribute("value"));
        await cut.Find(".ex-pivot-ok").ClickAsync(new MouseEventArgs());
        Assert.Equal("Takings", cut.Instance.CurrentLayout.Values[0].Caption);
        Assert.Equal(["Row Labels", "Takings"], HeaderTexts(cut));
    }

    [Fact] // ADR-0067 (PV-35): an open menu stays open across a change of values — a placed field's and the Layout menu
    public async Task An_open_menu_stays_open_across_a_change_of_values()
    {
        var source = new LiveSource();
        var cut = RenderPivot(RegionProduct, source: source);
        await OpenMenuAsync(cut, "Rows", "Region");

        await PublishAsync(cut, source, EastApples(101));

        Assert.Equal("−East | 181", RowTexts(cut)[0]);
        Assert.Single(cut.FindAll(".ex-pivot-field-list .ex-pivot-popup"));
        Assert.Contains("Move to Column Labels", cut.FindAll(".ex-pivot-menu-item").Select(MenuLabel));

        await cut.Find(".ex-pivot-layout-button").ClickAsync(new MouseEventArgs());
        Clock.Advance(Interval);
        await PublishAsync(cut, source, EastApples(102));

        Assert.Equal("−East | 182", RowTexts(cut)[0]);
        Assert.Single(cut.FindAll(".ex-pivot-toolbar .ex-pivot-popup"));
        Assert.Equal("true", cut.Find(".ex-pivot-layout-button").GetAttribute("aria-expanded"));
    }

    [Fact] // ADR-0067/0066 (PV-35, PV-23): an open Filter… stays open when an Item appears, and lists it under the new version
    public async Task An_open_filter_lists_an_item_that_appears()
    {
        var source = new LiveSource();
        var cut = RenderPivot(RegionAmount, source: source);
        await OpenMenuAsync(cut, "Rows", "Region");
        await RunMenuAsync(cut, "Filter…");
        cut.WaitForAssertion(() => Assert.Equal(5, cut.FindAll(".ex-pivot-item").Count));

        await PublishAsync(cut, source, [.. Sales, South]);

        cut.WaitForAssertion(() => Assert.Equal(["(Select All)", "East", "North", "South", "West", "(blank)"],
            cut.FindAll(".ex-pivot-item").Select(i => i.TextContent.Trim())));
        Assert.Single(cut.FindAll(".ex-pivot-popup"));
    }

    // ---- ADR-0066 refined: the Items in view while a new version's are on their way -------------

    private static readonly PivotLayout WestHidden = new()
    {
        Filters = [P("Region") with { HiddenItems = [PivotItemKey.Text("West")] }],
        Rows = [P("Product")],
        Values = [Sum("Amount")],
    };

    private static string BandSummary(IRenderedComponent<PivotComponent> cut) => cut.Find(".ex-pivot-filter-summary").TextContent;

    private static string[] ListedItems(IRenderedComponent<PivotComponent> cut)
        => cut.FindAll(".ex-pivot-item-filter .ex-pivot-item").Select(i => i.TextContent.Trim()).ToArray();

    /// <summary>Renders over a live source whose Items answer on demand, the first listing answered.</summary>
    private async Task<(IRenderedComponent<PivotComponent> Cut, LiveSource Source)> ListedAsync(PivotLayout layout)
    {
        var source = new LiveSource { HoldsItems = true };
        var cut = RenderPivot(layout, source: source);
        // A first listing has nothing to show yet, and says so.
        Assert.Equal("Loading…", BandSummary(cut));
        await cut.InvokeAsync(Assert.Single(source.ItemQuestions).AnswerAsync);
        cut.WaitForAssertion(() => Assert.Equal("(Multiple Items)", BandSummary(cut)));
        return (cut, source);
    }

    [Fact] // ADR-0066 refined (PV-23, PV-35): a new Source Version keeps the band's summary in view while its Items are on their way, and asks for them under the new version
    public async Task A_new_version_keeps_the_bands_summary_while_its_items_are_on_their_way()
    {
        var (cut, source) = await ListedAsync(WestHidden);
        var first = cut.Instance.Report!.Metadata.SourceVersion;

        await PublishAsync(cut, source, [.. Sales, South]);

        Assert.NotEqual(first, cut.Instance.Report!.Metadata.SourceVersion);
        var asked = source.ItemQuestions[^1];
        Assert.Equal(2, source.ItemQuestions.Count);
        Assert.Equal(cut.Instance.Report!.Metadata.SourceVersion, asked.Query.SourceVersion);
        Assert.False(asked.Completion.Task.IsCompleted);
        Assert.Equal("(Multiple Items)", BandSummary(cut));

        await cut.InvokeAsync(asked.AnswerAsync);
        Assert.Equal("(Multiple Items)", BandSummary(cut));
    }

    [Fact] // ADR-0066 refined (PV-23): Filter… lists the earlier version's Items while the new version's are on their way — marked busy, not blanked, OK enabled — and the new version's replace them when they land
    public async Task Filter_lists_the_earlier_versions_items_while_the_new_ones_are_on_their_way()
    {
        var (cut, source) = await ListedAsync(WestHidden);
        await cut.Find(".ex-pivot-filter-button").ClickAsync(new MouseEventArgs());
        Assert.Equal(["(Select All)", "East", "North", "West", "(blank)"], ListedItems(cut));
        Assert.Null(cut.Find(".ex-pivot-item-list").GetAttribute("aria-busy"));

        await PublishAsync(cut, source, [.. Sales, South]);

        Assert.Equal(2, source.ItemQuestions.Count);
        Assert.Equal(["(Select All)", "East", "North", "West", "(blank)"], ListedItems(cut));
        Assert.Equal("true", cut.Find(".ex-pivot-item-list").GetAttribute("aria-busy"));
        Assert.Empty(cut.FindAll(".ex-pivot-item-filter .ex-pivot-loading"));
        Assert.False(cut.Find(".ex-pivot-ok").HasAttribute("disabled"));
        Assert.Single(cut.FindAll(".ex-pivot-popup"));

        await cut.InvokeAsync(source.ItemQuestions[1].AnswerAsync);

        cut.WaitForAssertion(() => Assert.Equal(["(Select All)", "East", "North", "South", "West", "(blank)"], ListedItems(cut)));
        Assert.Null(cut.Find(".ex-pivot-item-list").GetAttribute("aria-busy"));
    }

    [Fact] // ADR-0066 refined (PV-23): ticking and applying against the earlier version's Items is safe — Hidden Items are keys, which name the same Items under any version
    public async Task Applying_against_the_earlier_versions_items_hides_them_by_key()
    {
        var (cut, source) = await ListedAsync(WestHidden);
        await PublishAsync(cut, source, [.. Sales, South]);
        await cut.Find(".ex-pivot-filter-button").ClickAsync(new MouseEventArgs());
        Assert.Equal("true", cut.Find(".ex-pivot-item-list").GetAttribute("aria-busy"));

        await cut.FindAll(".ex-pivot-item").Single(i => i.TextContent.Trim() == "East").QuerySelector("input")!
            .ChangeAsync(new ChangeEventArgs { Value = false });
        await cut.Find(".ex-pivot-ok").ClickAsync(new MouseEventArgs());

        Assert.Equal(
            new HashSet<PivotItemKey> { PivotItemKey.Text("West"), PivotItemKey.Text("East") },
            cut.Instance.CurrentLayout.Filters[0].HiddenItems.ToHashSet());
        // North's Pears, the blank region's Plums, and South's Apples: the newest data, East and
        // West left out.
        Assert.Equal(["Apples | 1", "Pears | 10", "Plums | 5", "Grand Total | 16"], RowTexts(cut));
    }

    private static readonly Sale Central = South with { Region = "Central" };

    private static readonly Sale Delta = South with { Region = "Delta" };

    [Fact] // ADR-0066 refined (PV-23): Items listed under a version the report has since moved past, landing late, stay in view when they are the newest listed
    public async Task A_late_listing_that_is_the_newest_stays_in_view()
    {
        var (cut, source) = await ListedAsync(WestHidden);
        await cut.Find(".ex-pivot-filter-button").ClickAsync(new MouseEventArgs());

        // The second version's Items are still on their way when the third version's report lands.
        await PublishAsync(cut, source, [.. Sales, South]);
        var second = source.ItemQuestions[1];
        Clock.Advance(Interval);
        await PublishAsync(cut, source, [.. Sales, South, Central]);
        var third = source.ItemQuestions[2];

        await cut.InvokeAsync(second.AnswerAsync);

        cut.WaitForAssertion(() => Assert.Equal(["(Select All)", "East", "North", "South", "West", "(blank)"], ListedItems(cut)));
        Assert.Equal("true", cut.Find(".ex-pivot-item-list").GetAttribute("aria-busy"));

        await cut.InvokeAsync(third.AnswerAsync);
        cut.WaitForAssertion(() => Assert.Equal(["(Select All)", "Central", "East", "North", "South", "West", "(blank)"], ListedItems(cut)));
        Assert.Null(cut.Find(".ex-pivot-item-list").GetAttribute("aria-busy"));
    }

    [Fact] // ADR-0066 refined (PV-23): a listing older than the Items in view, landing late, never takes their place
    public async Task A_late_listing_older_than_the_one_in_view_is_set_aside()
    {
        var (cut, source) = await ListedAsync(WestHidden);
        await cut.Find(".ex-pivot-filter-button").ClickAsync(new MouseEventArgs());
        await PublishAsync(cut, source, [.. Sales, South]);
        var second = source.ItemQuestions[1];
        Clock.Advance(Interval);
        await PublishAsync(cut, source, [.. Sales, South, Central]);
        await cut.InvokeAsync(source.ItemQuestions[2].AnswerAsync);
        cut.WaitForAssertion(() => Assert.Equal(["(Select All)", "Central", "East", "North", "South", "West", "(blank)"], ListedItems(cut)));

        // A fourth version: the third's Items stay in view while its own are on their way, and the
        // second's, landing meanwhile, are older than them.
        Clock.Advance(Interval);
        await PublishAsync(cut, source, [.. Sales, South, Central, Delta]);
        var fourth = source.ItemQuestions[3];
        await cut.InvokeAsync(second.AnswerAsync);

        Assert.Equal(["(Select All)", "Central", "East", "North", "South", "West", "(blank)"], ListedItems(cut));
        Assert.Equal("true", cut.Find(".ex-pivot-item-list").GetAttribute("aria-busy"));

        await cut.InvokeAsync(fourth.AnswerAsync);
        cut.WaitForAssertion(() => Assert.Equal(["(Select All)", "Central", "Delta", "East", "North", "South", "West", "(blank)"], ListedItems(cut)));
    }

    [Fact] // ADR-0066 refined (PV-23): a new source lists from nothing — its first listing shows loading
    public async Task A_new_source_lists_its_items_from_nothing()
    {
        var (cut, _) = await ListedAsync(WestHidden);
        var next = new LiveSource { HoldsItems = true };

        cut.Render(ps => ps.Add(p => p.DataSource, next));

        Assert.Equal("Loading…", BandSummary(cut));
        await cut.InvokeAsync(Assert.Single(next.ItemQuestions).AnswerAsync);
        cut.WaitForAssertion(() => Assert.Equal("(Multiple Items)", BandSummary(cut)));
    }

    // ---- A change of data, and a question already out ------------------------------------------

    [Fact] // ADR-0067/0066: a change of data does not cancel the user's layout question; it is asked for once that lands, and only it marks cells
    public async Task A_change_does_not_cancel_a_users_layout_question()
    {
        var told = new List<PivotLayout>();
        var source = new LiveSource();
        var cut = RenderPivot(RegionAmount, ps => ps.Add(p => p.LayoutChanged, told.Add), source: source);
        source.AnswersAtOnce = false;

        await TickFieldAsync(cut, "Quantity", true);
        var layoutQuestion = source.Questions[1];
        await PublishAsync(cut, source, EastApples(101));

        Assert.Equal(2, source.Questions.Count);
        Assert.False(layoutQuestion.IsCancelled);
        Assert.True(cut.Instance.IsLoading);

        // Answered from the data it was asked under.
        await cut.InvokeAsync(layoutQuestion.AnswerAsync);
        cut.WaitForAssertion(() => Assert.Equal("East | 180 | 18", RowTexts(cut)[0]));
        Assert.Single(told);
        Assert.Empty(ChangeHighlightTests.MarkedTexts(cut));

        // The change is asked for after it: the whole answer, for the user's layout.
        await source.QuestionAsync(2, Xunit.TestContext.Current.CancellationToken);
        Assert.Equal(3, source.Questions.Count);
        Assert.Equal(layoutQuestion.Query, source.Questions[2].Query);
        await cut.InvokeAsync(source.Questions[2].AnswerAsync);
        cut.WaitForAssertion(() => Assert.Equal("East | 181 | 18", RowTexts(cut)[0]));
        Assert.Single(told);
        Assert.Equal(["181", "286"], ChangeHighlightTests.MarkedTexts(cut));
    }

    [Fact] // ADR-0067/0066: a user's layout change wins over a question for newer data — it is cancelled, its late answer never painted, and the user's question brings the change
    public async Task A_users_layout_change_supersedes_a_question_for_newer_data()
    {
        var told = new List<PivotLayout>();
        var source = new LiveSource();
        var cut = RenderPivot(RegionAmount, ps => ps.Add(p => p.LayoutChanged, told.Add), source: source);
        source.AnswersAtOnce = false;
        await PublishAsync(cut, source, EastApples(101));
        var live = source.Questions[1];

        await TickFieldAsync(cut, "Quantity", true);

        Assert.True(live.IsCancelled);
        await source.QuestionAsync(2, Xunit.TestContext.Current.CancellationToken);
        Assert.Equal(3, source.Questions.Count);
        await cut.InvokeAsync(source.Questions[2].AnswerAsync);
        cut.WaitForAssertion(() => Assert.Equal("East | 181 | 18", RowTexts(cut)[0]));
        Assert.Single(told);
        // A new layout marks nothing, even with new data in the same redraw.
        Assert.Empty(ChangeHighlightTests.MarkedTexts(cut));

        await cut.InvokeAsync(live.AnswerAsync);
        Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal("East | 181 | 18", RowTexts(cut)[0]);
        Assert.Equal(["Row Labels", "Sum of Amount", "Sum of Quantity"], HeaderTexts(cut));
        Assert.Equal(3, source.Questions.Count);
    }

    [Fact] // ADR-0067/0060: a gesture laid out from the answer held supersedes a question for newer data, and the change is asked for again
    public async Task A_gesture_from_the_held_answer_gathers_the_change_again()
    {
        var source = new LiveSource();
        var cut = RenderPivot(RegionProduct, source: source);
        source.AnswersAtOnce = false;
        await PublishAsync(cut, source, EastApples(101));
        var live = source.Questions[1];

        await cut.FindAll(".ex-pivot-toggle")[0].ClickAsync(new MouseEventArgs());

        Assert.True(live.IsCancelled);
        cut.WaitForAssertion(() => Assert.Equal("+East | 180", RowTexts(cut)[0]));
        await source.QuestionAsync(2, Xunit.TestContext.Current.CancellationToken);
        Assert.Equal(3, source.Questions.Count);
        await cut.InvokeAsync(source.Questions[2].AnswerAsync);
        cut.WaitForAssertion(() => Assert.Equal("+East | 181", RowTexts(cut)[0]));
        Assert.Equal(["181", "286"], ChangeHighlightTests.MarkedTexts(cut));
    }

    [Fact] // ADR-0067: a source handed over is no longer listened to, and nothing is heard after dispose
    public async Task Nothing_is_heard_from_a_source_replaced_or_after_dispose()
    {
        var first = new LiveSource();
        var cut = RenderPivot(RegionAmount, source: first);
        var second = new LiveSource();
        cut.Render(ps => ps.Add(p => p.DataSource, second));
        Assert.Single(second.Questions);

        await cut.InvokeAsync(() => first.Publish(EastApples(101)));
        Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Single(first.Questions);
        Assert.Equal("East | 180", RowTexts(cut)[0]);

        await DisposeComponentsAsync();
        second.Publish(EastApples(102));
        Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Single(second.Questions);
    }

    [Fact] // ADR-0067: a notice raised on another thread is marshalled to the renderer's
    public async Task A_notice_from_another_thread_is_marshalled()
    {
        var source = new LiveSource();
        var cut = RenderPivot(RegionAmount, source: source);

        await Task.Run(() => source.Publish(EastApples(101)), Xunit.TestContext.Current.CancellationToken);

        cut.WaitForAssertion(() => Assert.Equal("East | 181", RowTexts(cut)[0]));
    }

    [Fact] // ADR-0066/0067: a change under a layout whose Aggregation the source no longer answers is refused by name, once, and nothing is asked
    public async Task A_change_the_source_can_no_longer_answer_is_refused_once()
    {
        var source = new LiveSource();
        var cut = RenderPivot(RegionAmount, source: source);
        source.Offer(PivotAggregation.Count);

        await PublishAsync(cut, source, EastApples(101));
        Clock.Advance(TimeSpan.FromSeconds(1));

        Assert.Single(source.Questions);
        Assert.Equal("The source does not answer Sum.", cut.Find(".ex-pivot-refusal-notice").TextContent);
        Assert.Equal("East | 180", RowTexts(cut)[0]);
    }

    // ---- PV-38: a server's source, and Refresh -------------------------------------------------

    /// <summary>A server's source through <c>PivotSource.Fetch</c>, answering from the data the
    /// test holds now, and counting the questions it was asked.</summary>
    private sealed class Server
    {
        public PivotSource Data { get; set; } = Bundled();

        public List<PivotQuery> Asked { get; } = [];

        public FetchingPivotSource Source { get; }

        public Server(bool canRefresh = true)
        {
            Source = PivotSource.Fetch(Fields, new PivotSourceFeatures(Enum.GetValues<PivotAggregation>(), canRefresh),
                (query, ct) =>
                {
                    Asked.Add(query);
                    return Data.AggregateAsync(query, ct);
                },
                (query, ct) => Data.ItemsAsync(query, ct),
                (query, ct) => Data.DetailsAsync(query, ct));
        }
    }

    [Fact] // ADR-0066/0067 (PV-38): when the Consumer says a server's data changed, ExPivot asks again for the whole answer
    public async Task A_servers_notice_asks_again_for_the_whole_answer()
    {
        var server = new Server();
        var cut = RenderPivot(RegionProduct, source: server.Source);
        Assert.Single(server.Asked);

        server.Data = Bundled(EastApples(101));
        await cut.InvokeAsync(() => server.Source.NotifyChanged());

        Assert.Equal(2, server.Asked.Count);
        Assert.Equal(server.Asked[0], server.Asked[1]);
        Assert.Equal("−East | 181", RowTexts(cut)[0]);
        Assert.Equal("Apples | 131", RowTexts(cut)[1]);
        Assert.Equal(["181", "131", "286"], ChangeHighlightTests.MarkedTexts(cut));
    }

    [Fact] // ADR-0066/0067 (PV-38): Refresh asks a server's source again for the whole answer — once, though the source says its data moved on
    public async Task Refresh_asks_a_servers_source_again_once()
    {
        var server = new Server();
        var cut = RenderPivot(RegionAmount, source: server.Source);

        server.Data = Bundled(EastApples(101));
        await cut.Find(".ex-pivot-refresh-button").ClickAsync(new MouseEventArgs());

        Assert.Equal(2, server.Asked.Count);
        Assert.Equal(server.Asked[0], server.Asked[1]);
        Assert.Equal("East | 181", RowTexts(cut)[0]);
        Assert.Equal(["181", "286"], ChangeHighlightTests.MarkedTexts(cut));
        // The source's own notice of the refresh is answered by Refresh's question, not asked again.
        Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(2, server.Asked.Count);

        server.Data = Bundled(EastApples(102));
        await cut.Instance.RefreshAsync();
        Assert.Equal(3, server.Asked.Count);
        Assert.Equal("East | 182", RowTexts(cut)[0]);

        Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(3, server.Asked.Count);
    }

    [Theory] // ADR-0067 (PV-38): Refresh is a person's request — asked at once, whatever the interval, under the loading indication, and once
    [InlineData(false)]
    [InlineData(true)]
    public async Task Refresh_is_asked_at_once(bool withinTheInterval)
    {
        var source = new LiveSource(canRefresh: true);
        var cut = RenderPivot(RegionAmount, source: source);
        if (withinTheInterval)
            await PublishAsync(cut, source, EastApples(101));
        var asked = source.Questions.Count;
        source.AnswersAtOnce = false;

        await cut.Find(".ex-pivot-refresh-button").ClickAsync(new MouseEventArgs());

        Assert.Equal(1, source.Refreshes);
        Assert.Equal(asked + 1, source.Questions.Count);
        Assert.True(cut.Instance.IsLoading);
        Assert.True(Grid(cut).Instance.IsLoading);
        await cut.InvokeAsync(source.Questions[^1].AnswerAsync);
        cut.WaitForState(() => !cut.Instance.IsLoading);
        Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(asked + 1, source.Questions.Count);
    }

    [Fact] // ADR-0061/0066: a Refresh that supersedes the user's layout question asks for that layout, and still raises it once shown
    public async Task A_refresh_over_a_users_question_still_raises_the_layout()
    {
        var told = new List<PivotLayout>();
        var source = new LiveSource(canRefresh: true);
        var cut = RenderPivot(RegionAmount, ps => ps.Add(p => p.LayoutChanged, told.Add), source: source);
        source.AnswersAtOnce = false;
        await TickFieldAsync(cut, "Quantity", true);
        var layoutQuestion = source.Questions[1];

        await cut.Find(".ex-pivot-refresh-button").ClickAsync(new MouseEventArgs());

        Assert.True(layoutQuestion.IsCancelled);
        await source.QuestionAsync(2, Xunit.TestContext.Current.CancellationToken);
        Assert.Equal(layoutQuestion.Query, source.Questions[2].Query);
        await cut.InvokeAsync(source.Questions[2].AnswerAsync);
        cut.WaitForAssertion(() => Assert.Equal("East | 180 | 18", RowTexts(cut)[0]));
        var raised = Assert.Single(told);
        Assert.Same(cut.Instance.CurrentLayout, raised);
        Assert.Equal(["Amount", "Quantity"], raised.Values.Select(v => v.Field));
    }

    [Fact] // ADR-0066/0067 (PV-38): a notice naming the Source Version already on screen asks nothing; one naming another asks
    public async Task A_notice_of_the_version_on_screen_asks_nothing()
    {
        var server = new Server();
        var cut = RenderPivot(RegionAmount, source: server.Source);
        var version = cut.Instance.Report!.Metadata.SourceVersion;

        await cut.InvokeAsync(() => server.Source.NotifyChanged(version));
        Assert.Single(server.Asked);

        server.Data = Bundled(EastApples(101));
        await cut.InvokeAsync(() => server.Source.NotifyChanged("next"));
        Assert.Equal(2, server.Asked.Count);
        Assert.Equal("East | 181", RowTexts(cut)[0]);
    }

    // ---- PV-15: what a change of data renders --------------------------------------------------

    [Fact] // ADR-0003/0067 (PV-15): a change of data the grid cannot be shown renders no grid row and not the grid; the pane's interactions still render none
    public async Task A_stale_change_renders_no_grid_row()
    {
        var source = new LiveSource();
        var cut = RenderPivot(RegionProduct, source: source);
        var rows = cut.FindComponents<ExGridRow<PivotDisplayRow>>().Sum(r => r.RenderCount);
        var grid = Grid(cut).RenderCount;

        source.Fails = new InvalidOperationException("The server is unreachable.");
        await PublishAsync(cut, source, EastApples(101));
        await cut.Find(".ex-pivot-search").InputAsync(new ChangeEventArgs { Value = "Reg" });

        Assert.Single(cut.FindAll(".ex-pivot-stale-notice"));
        Assert.Equal(rows, cut.FindComponents<ExGridRow<PivotDisplayRow>>().Sum(r => r.RenderCount));
        Assert.Equal(grid, Grid(cut).RenderCount);
    }
}
