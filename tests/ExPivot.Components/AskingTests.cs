using Bunit;
using ExGrid.Components;
using ExGrid.Rows;
using ExPivot.Components.Tests.Support;
using ExPivot.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using PivotComponent = ExPivot.Components.ExPivot;

namespace ExPivot.Components.Tests;

/// <summary>
/// ExPivot asks a Pivot Source, and asking never blocks (ADR-0066): the Field List shows a new layout
/// at once, the report stays as it was under the grid's loading indication, a further change cancels
/// the question in flight, an answer to a superseded question is never painted, a change the answer
/// held lays out asks nothing, a layout over a cap is refused by name, and an Aggregation the source
/// does not answer is never asked for.
/// </summary>
public class AskingTests : PivotTestContext
{
    private static readonly PivotLayout RegionAmount = new() { Rows = [P("Region")], Values = [Sum("Amount")] };

    private static OnDemandSource Holding(PivotSourceFeatures? features = null) => new(Bundled(), features);

    /// <summary>Renders over a source that holds its answers, and answers the first question.</summary>
    private async Task<(IRenderedComponent<PivotComponent> Cut, OnDemandSource Source)> AnsweredAsync(
        PivotLayout layout, Action<ComponentParameterCollectionBuilder<PivotComponent>>? parameters = null, OnDemandSource? source = null)
    {
        source ??= Holding();
        var cut = RenderPivot(layout, parameters, source: source);
        await AnswerAsync(cut, source.Questions[^1]);
        return (cut, source);
    }

    /// <summary>Answers the current question and waits for the report to show it.</summary>
    private static async Task AnswerAsync(IRenderedComponent<PivotComponent> cut, OnDemandSource.Question question)
    {
        await cut.InvokeAsync(question.AnswerAsync);
        cut.WaitForState(() => !cut.Instance.IsLoading);
    }

    /// <summary>Lets whatever the renderer was handed run: an answer's continuation is posted to it,
    /// and a work item queued after it runs after it.</summary>
    private static async Task SettleAsync(IRenderedComponent<PivotComponent> cut)
    {
        await cut.InvokeAsync(() => { });
        await cut.InvokeAsync(() => { });
    }

    // ---- PV-25: asking never blocks -------------------------------------------------------------

    [Fact] // ADR-0066 (PV-25): the first question is out and nothing blocks — the pane shows the layout, the report says it is on its way
    public void The_first_question_does_not_block()
    {
        var source = Holding();

        var cut = RenderPivot(RegionAmount, source: source);

        Assert.Single(source.Questions);
        Assert.True(cut.Instance.IsLoading);
        Assert.Equal(["Region"], AreaEntries(cut, "Rows"));
        Assert.Equal("Loading…", cut.Find(".ex-pivot-empty").TextContent);
        Assert.Empty(cut.FindComponents<ExGrid<PivotDisplayRow>>());
    }

    [Fact] // ADR-0066 (PV-25): while a question is out the Field List shows the new layout, and the report stays as it was under the grid's IsLoading
    public async Task While_a_question_is_out_the_report_stays_as_it_was()
    {
        var told = new List<PivotLayout>();
        var (cut, source) = await AnsweredAsync(RegionAmount, ps => ps.Add(p => p.LayoutChanged, told.Add));
        var before = RowTexts(cut);
        Assert.False(Grid(cut).Instance.IsLoading);

        await TickFieldAsync(cut, "Product", true);

        Assert.Equal(2, source.Questions.Count);
        Assert.Equal(["Region", "Product"], AreaEntries(cut, "Rows"));
        Assert.True(FieldItem(cut, "Product").QuerySelector("input")!.HasAttribute("checked"));
        Assert.Equal(before, RowTexts(cut));
        Assert.True(Grid(cut).Instance.IsLoading);
        Assert.True(cut.Instance.IsLoading);
        Assert.Equal(RegionAmount, cut.Instance.CurrentLayout);
        Assert.Empty(told);

        await AnswerAsync(cut, source.Questions[1]);

        Assert.False(Grid(cut).Instance.IsLoading);
        Assert.Equal("−East | 180", RowTexts(cut)[0]);
        Assert.Equal(["Region", "Product"], cut.Instance.CurrentLayout.Rows.Select(p => p.Field));
        Assert.Single(told);
        Assert.Same(cut.Instance.CurrentLayout, told[0]);
    }

    [Fact] // ADR-0066/0025 (PV-25): a further change cancels the question in flight and asks the next
    public async Task A_further_change_cancels_the_question_in_flight()
    {
        var (cut, source) = await AnsweredAsync(RegionAmount);

        await TickFieldAsync(cut, "Product", true);
        var first = source.Questions[1];
        await TickFieldAsync(cut, "Quantity", true);

        await source.QuestionAsync(2, Xunit.TestContext.Current.CancellationToken);
        Assert.Equal(3, source.Questions.Count);
        Assert.True(first.IsCancelled);
        Assert.False(source.Questions[2].IsCancelled);
        Assert.True(cut.Instance.IsLoading);

        await AnswerAsync(cut, source.Questions[2]);
        Assert.Equal(["Row Labels", "Sum of Amount", "Sum of Quantity"], HeaderTexts(cut));
        Assert.Equal("−East | 180 | 18", RowTexts(cut)[0]);
    }

    [Fact] // ADR-0066/0025 (PV-25): an answer to a superseded question is never painted, even when the source answers it anyway, late
    public async Task An_answer_to_a_superseded_question_is_never_painted()
    {
        var told = new List<PivotLayout>();
        var (cut, source) = await AnsweredAsync(RegionAmount, ps => ps.Add(p => p.LayoutChanged, told.Add));
        source.IgnoresCancellation = true;

        await TickFieldAsync(cut, "Product", true);
        var superseded = source.Questions[1];
        await TickFieldAsync(cut, "Quantity", true);
        var current = await source.QuestionAsync(2, Xunit.TestContext.Current.CancellationToken);

        // The superseded answer arrives first: discarded, and the report goes on waiting.
        Assert.False(superseded.Completion.Task.IsCompleted);
        await cut.InvokeAsync(superseded.AnswerAsync);
        await SettleAsync(cut);
        Assert.True(cut.Instance.IsLoading);
        Assert.Equal(["Row Labels", "Sum of Amount"], HeaderTexts(cut));
        Assert.Equal("East | 180", RowTexts(cut)[0]);

        await AnswerAsync(cut, current);
        Assert.Equal(["Row Labels", "Sum of Amount", "Sum of Quantity"], HeaderTexts(cut));

        // And one arriving after a newer layout was shown — from the answer held — is discarded too.
        await TickFieldAsync(cut, "Online", true);
        var late = source.Questions[3];
        await TickFieldAsync(cut, "Online", false);
        Assert.Equal(4, source.Questions.Count);
        await cut.InvokeAsync(late.AnswerAsync);
        // The held answer's report request finishes asynchronously. Wait for the public
        // layout notification, not a fixed number of renderer work items (ADR-0025).
        cut.WaitForAssertion(() => Assert.Equal(2, told.Count));
        Assert.Equal(["Region", "Product"], cut.Instance.CurrentLayout.Rows.Select(p => p.Field));
        Assert.Equal("−East | 180 | 18", RowTexts(cut)[0]);
        Assert.Equal(2, told.Count);
        Assert.All(told, layout => Assert.Equal(["Region", "Product"], layout.Rows.Select(p => p.Field)));
        Assert.All(told, layout => Assert.Equal(2, layout.Values.Count));
    }

    [Fact] // ADR-0066/0025, ADR-0067 refined (PV-25): a failed question for the user's layout leaves the report as it was and says so on the Pivot Toolbar — not a Stale Report — and the layout goes back
    public async Task A_failure_leaves_the_report_as_it_was_and_says_so()
    {
        var told = new List<PivotLayout>();
        var (cut, source) = await AnsweredAsync(RegionAmount, ps => ps.Add(p => p.LayoutChanged, told.Add));
        var before = RowTexts(cut);

        await TickFieldAsync(cut, "Product", true);
        await cut.InvokeAsync(() => source.Questions[1].Fail(new InvalidOperationException("The server is unreachable.")));
        cut.WaitForState(() => !cut.Instance.IsLoading);

        Assert.Equal(before, RowTexts(cut));
        Assert.False(Grid(cut).Instance.IsLoading);
        Assert.Equal("The source could not answer: The server is unreachable.", cut.Find(".ex-pivot-refusal-notice").TextContent);
        Assert.Equal("alert", cut.Find(".ex-pivot-refusal-notice").GetAttribute("role"));
        Assert.IsType<InvalidOperationException>(cut.Instance.LastError);
        Assert.False(cut.Instance.IsStale);
        Assert.Empty(cut.FindAll(".ex-pivot-stale-notice"));
        Assert.Equal(RegionAmount, cut.Instance.CurrentLayout);
        // The pane goes back to the layout the report shows.
        Assert.Equal(["Region"], AreaEntries(cut, "Rows"));
        Assert.False(FieldItem(cut, "Product").QuerySelector("input")!.HasAttribute("checked"));
        Assert.Empty(told);

        // So the next change starts from the report's layout, and its answer is raised.
        await TickFieldAsync(cut, "Quantity", true);
        await AnswerAsync(cut, source.Questions[2]);
        Assert.Equal(["Amount", "Quantity"], cut.Instance.CurrentLayout.Values.Select(v => v.Field));
        Assert.Equal(["Region"], cut.Instance.CurrentLayout.Rows.Select(p => p.Field));
        Assert.Single(told);
        Assert.Empty(cut.FindAll(".ex-pivot-refusal-notice"));
    }

    [Fact] // ADR-0067 refined (PV-25): before the first report there is no layout to go back to — a failed first question is said on the Pivot Toolbar, and the pane keeps the layout
    public async Task A_failed_first_question_keeps_the_layout()
    {
        var source = Holding();
        var cut = RenderPivot(RegionAmount, source: source);

        await cut.InvokeAsync(() => source.Questions[0].Fail(new InvalidOperationException("The server is unreachable.")));
        cut.WaitForState(() => !cut.Instance.IsLoading);

        Assert.Equal("The source could not answer: The server is unreachable.", cut.Find(".ex-pivot-refusal-notice").TextContent);
        Assert.Equal(["Region"], AreaEntries(cut, "Rows"));
        Assert.Equal(["Sum of Amount"], AreaEntries(cut, "Values"));
        Assert.False(cut.Instance.IsStale);
        Assert.Null(cut.Instance.Report);
    }

    // ---- PV-26: a change that needs no new question asks none ------------------------------------

    [Fact] // ADR-0060/0066 (PV-26): collapse, order, the form, subtotals, grand totals, Show Values As, captions and formats ask nothing
    public async Task A_change_the_held_answer_lays_out_asks_nothing()
    {
        var (cut, source) = await AnsweredAsync(new PivotLayout { Rows = [P("Region"), P("Product")], Values = [Sum("Amount")] });

        await cut.FindAll(".ex-pivot-toggle")[0].ClickAsync(new MouseEventArgs());
        await OpenMenuAsync(cut, "Rows", "Region");
        await RunMenuAsync(cut, "Sort Z to A");
        await cut.Find(".ex-pivot-layout-button").ClickAsync(new MouseEventArgs());
        await RunMenuAsync(cut, "Show in Outline Form");
        await cut.Find(".ex-pivot-layout-button").ClickAsync(new MouseEventArgs());
        await RunMenuAsync(cut, "Show all Subtotals at Bottom of Group");
        await cut.Find(".ex-pivot-layout-button").ClickAsync(new MouseEventArgs());
        await RunMenuAsync(cut, "Off for Rows and Columns");
        await OpenMenuAsync(cut, "Values", "Sum of Amount");
        await RunMenuAsync(cut, "Value Field Settings…");
        await cut.FindAll(".ex-pivot-value-settings select")[1].ChangeAsync(new ChangeEventArgs { Value = "PercentOfGrandTotal" });
        await cut.Find(".ex-pivot-value-settings input[type=text]").InputAsync(new ChangeEventArgs { Value = "Share" });
        await cut.FindAll(".ex-pivot-value-settings input[type=text]")[1].InputAsync(new ChangeEventArgs { Value = "0.0%" });
        await cut.Find(".ex-pivot-ok").ClickAsync(new MouseEventArgs());

        Assert.Single(source.Questions);
        var layout = cut.Instance.CurrentLayout;
        Assert.Equal(PivotReportForm.Outline, layout.Form);
        Assert.False(layout.GrandTotalRow);
        Assert.Equal("Share", layout.Values[0].Caption);
        Assert.Equal(PivotShowValuesAs.PercentOfGrandTotal, layout.Values[0].ShowValuesAs);
        Assert.Equal(PivotSort.Descending, layout.Rows[0].Sort);
        Assert.False(cut.Instance.IsLoading);
    }

    [Fact] // ADR-0060/0066 (PV-26): an Aggregation change asks again only for parts the held answer lacks
    public async Task An_aggregation_change_asks_only_for_missing_parts()
    {
        var (cut, source) = await AnsweredAsync(RegionAmount);

        await ChangeAggregationAsync(cut, "Average");
        Assert.Single(source.Questions);
        Assert.Equal("East | 60", RowTexts(cut)[0]);

        await ChangeAggregationAsync(cut, "Max");
        Assert.Equal(2, source.Questions.Count);
        Assert.Equal(PivotParts.Extremes, source.Questions[1].Query.Values[0].Parts);
        await AnswerAsync(cut, source.Questions[1]);
        Assert.Equal("East | 100", RowTexts(cut)[0]);

        await ChangeAggregationAsync(cut, "Count");
        Assert.Equal(2, source.Questions.Count);
    }

    private static async Task ChangeAggregationAsync(IRenderedComponent<PivotComponent> cut, string aggregation)
    {
        var caption = cut.Instance.Report!.Metadata.ValueCaptions[0];
        await OpenMenuAsync(cut, "Values", caption);
        await RunMenuAsync(cut, "Value Field Settings…");
        await cut.FindAll(".ex-pivot-value-settings select")[0].ChangeAsync(new ChangeEventArgs { Value = aggregation });
        await cut.Find(".ex-pivot-ok").ClickAsync(new MouseEventArgs());
    }

    [Fact] // ADR-0066 refined (PV-26): a Filters field that hides nothing does not travel — placing it asks nothing; hiding one of its Items does
    public async Task A_filters_field_that_hides_nothing_asks_nothing()
    {
        var (cut, source) = await AnsweredAsync(RegionAmount);

        await FieldItem(cut, "Online").DragStartAsync(new DragEventArgs());
        await AreaElement(cut, "Filters").DropAsync(new DragEventArgs());

        Assert.Single(source.Questions);
        Assert.Equal(["Online"], cut.Instance.CurrentLayout.Filters.Select(p => p.Field));
        Assert.Equal("(All)", cut.Find(".ex-pivot-filter-summary").TextContent);

        await cut.Find(".ex-pivot-filter-button").ClickAsync(new MouseEventArgs());
        await cut.FindAll(".ex-pivot-item").Single(i => i.TextContent.Trim() == "FALSE").QuerySelector("input")!
            .ChangeAsync(new ChangeEventArgs { Value = false });
        await cut.Find(".ex-pivot-ok").ClickAsync(new MouseEventArgs());

        Assert.Equal(2, source.Questions.Count);
        Assert.Equal(["Online"], source.Questions[1].Query.Filters.Select(f => f.Field));
        await AnswerAsync(cut, source.Questions[1]);
        Assert.Equal("TRUE", cut.Find(".ex-pivot-filter-summary").TextContent);
        Assert.Equal("East | 130", RowTexts(cut)[0]);
    }

    // ---- PV-29: caps ---------------------------------------------------------------------------------

    [Fact] // ADR-0066 (PV-29): a question carries the leaves' cap, which the Consumer may change
    public async Task A_question_carries_the_leaf_cap()
    {
        var (_, source) = await AnsweredAsync(RegionAmount, ps => ps.Add(p => p.Caps, new PivotCaps { MaxLeaves = 1234 }));

        Assert.Equal(1234, source.Questions[0].Query.MaxLeaves);
        Assert.Equal(PivotQuery.DefaultMaxLeaves, PivotCaps.Default.MaxLeaves);
        Assert.Equal(1_048_576, PivotCaps.Default.MaxRows);
        Assert.Equal(16_384, PivotCaps.Default.MaxColumns);
    }

    [Fact] // ADR-0066 (PV-29): a layout whose source refuses it for its leaves is refused by name, and the layout goes back to the one before
    public async Task A_layout_over_the_leaf_cap_is_refused_by_name()
    {
        var told = new List<PivotLayout>();
        var start = new PivotLayout { Values = [Sum("Amount")] };
        var cut = RenderPivot(start, ps => ps.Add(p => p.Caps, new PivotCaps { MaxLeaves = 3 }).Add(p => p.LayoutChanged, told.Add));

        await TickFieldAsync(cut, "Region", true);

        Assert.Equal("This layout needs more than 3 cells.", cut.Find(".ex-pivot-refusal-notice").TextContent);
        Assert.Empty(AreaEntries(cut, "Rows"));
        Assert.False(FieldItem(cut, "Region").QuerySelector("input")!.HasAttribute("checked"));
        Assert.Equal(["285"], RowTexts(cut));
        Assert.Same(start, cut.Instance.CurrentLayout);
        Assert.Empty(told);
    }

    [Fact] // ADR-0066 (PV-29): a layout whose report would pass the rows' cap is refused by name, whether or not it needed a question
    public async Task A_layout_over_the_row_cap_is_refused_by_name()
    {
        var source = new OnDemandSource(Bundled()) { AnswersAtOnce = true };
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region") with { Collapsed = true }, P("Product")], Values = [Sum("Amount")] },
            ps => ps.Add(p => p.Caps, new PivotCaps { MaxRows = 6 }), source: source);
        Assert.Equal(5, RowTexts(cut).Length);

        // Expanding the field needs no question, and would make 11 rows.
        await OpenMenuAsync(cut, "Rows", "Region");
        await RunMenuAsync(cut, "Expand Entire Field");

        Assert.Single(source.Questions);
        Assert.Equal("This layout needs more than 6 rows.", cut.Find(".ex-pivot-refusal-notice").TextContent);
        Assert.Equal(5, RowTexts(cut).Length);
        Assert.True(cut.Instance.CurrentLayout.Rows[0].Collapsed);
    }

    [Fact] // ADR-0066 (PV-29): a layout whose report would pass the columns' cap is refused by name
    public async Task A_layout_over_the_column_cap_is_refused_by_name()
    {
        var cut = RenderPivot(RegionAmount, ps => ps.Add(p => p.Caps, new PivotCaps { MaxColumns = 4 }));

        await FieldItem(cut, "Product").DragStartAsync(new DragEventArgs());
        await AreaElement(cut, "Columns").DropAsync(new DragEventArgs());

        Assert.Equal("This layout needs more than 4 columns.", cut.Find(".ex-pivot-refusal-notice").TextContent);
        Assert.Empty(AreaEntries(cut, "Columns"));
        Assert.Equal(["Row Labels", "Sum of Amount"], HeaderTexts(cut));
    }

    [Fact] // ADR-0066 (PV-29): the next change clears the refusal
    public async Task The_next_change_clears_the_refusal()
    {
        var cut = RenderPivot(new PivotLayout { Values = [Sum("Amount")] }, ps => ps.Add(p => p.Caps, new PivotCaps { MaxLeaves = 3 }));
        await TickFieldAsync(cut, "Region", true);
        Assert.Single(cut.FindAll(".ex-pivot-refusal-notice"));

        await TickFieldAsync(cut, "Quantity", true);

        Assert.Empty(cut.FindAll(".ex-pivot-refusal-notice"));
        Assert.Equal(["Sum of Amount", "Sum of Quantity"], HeaderTexts(cut));
    }

    // ---- PV-24: what the source answers -------------------------------------------------------------

    private static readonly PivotSourceFeatures SqlLike = new(
        [PivotAggregation.Sum, PivotAggregation.Count, PivotAggregation.Average, PivotAggregation.Max, PivotAggregation.Min,
         PivotAggregation.CountNumbers]);

    [Fact] // ADR-0066 (PV-24): Value Field Settings… offers the Aggregations the source does not answer disabled, with the reason
    public async Task An_aggregation_the_source_does_not_answer_is_offered_disabled_with_the_reason()
    {
        var (cut, _) = await AnsweredAsync(RegionAmount, source: Holding(SqlLike));

        await OpenMenuAsync(cut, "Values", "Sum of Amount");
        await RunMenuAsync(cut, "Value Field Settings…");

        var options = cut.FindAll(".ex-pivot-value-settings select")[0].QuerySelectorAll("option");
        Assert.Equal(["Product", "StdDev", "StdDevp", "Var", "Varp"],
            options.Where(o => o.HasAttribute("disabled")).Select(o => o.TextContent));
        Assert.Equal("The source does not answer Product.", options.Single(o => o.TextContent == "Product").GetAttribute("title"));
        Assert.Contains("The source does not answer Varp.", cut.FindAll(".ex-pivot-not-offered").Select(n => n.TextContent));
    }

    [Fact] // ADR-0066 (PV-24): an Aggregation the source does not answer is never chosen, and never asked for
    public async Task An_aggregation_the_source_does_not_answer_is_never_asked_for()
    {
        var (cut, source) = await AnsweredAsync(RegionAmount, source: Holding(SqlLike));

        await OpenMenuAsync(cut, "Values", "Sum of Amount");
        await RunMenuAsync(cut, "Value Field Settings…");
        await cut.FindAll(".ex-pivot-value-settings select")[0].ChangeAsync(new ChangeEventArgs { Value = "Product" });
        await cut.Find(".ex-pivot-ok").ClickAsync(new MouseEventArgs());

        Assert.Single(source.Questions);
        Assert.Equal(PivotAggregation.Sum, cut.Instance.CurrentLayout.Values[0].Aggregation);
    }

    [Fact] // ADR-0066 (PV-24): a layout the Consumer hands in with an Aggregation the source does not answer is refused by name, and nothing is asked
    public void A_layout_with_an_aggregation_not_offered_is_refused_by_name()
    {
        var source = Holding(SqlLike);

        var cut = RenderPivot(new PivotLayout { Rows = [P("Region")], Values = [new PivotValueField("Amount", PivotAggregation.Product)] }, source: source);

        Assert.Empty(source.Questions);
        Assert.Equal("The source does not answer Product.", cut.Find(".ex-pivot-refusal-notice").TextContent);
    }

    // ---- PV-15: the grid renders only when the report does ------------------------------------

    [Fact] // ADR-0003/0061 (PV-15): a change that is only the pane's — under Defer Layout Update — and the Pivot Toolbar's menu render no grid row and not the grid
    public async Task The_pane_and_the_toolbar_do_not_render_the_grid()
    {
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region"), P("Product")], Values = [Sum("Amount")] });
        var rows = cut.FindComponents<ExGridRow<PivotDisplayRow>>().Sum(r => r.RenderCount);
        var grid = Grid(cut).RenderCount;

        await cut.Find(".ex-pivot-defer input").ChangeAsync(new ChangeEventArgs { Value = true });
        await TickFieldAsync(cut, "Online", true);
        await cut.Find(".ex-pivot-layout-button").ClickAsync(new MouseEventArgs());
        await cut.Find(".ex-pivot-popup").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        await AreaElement(cut, "Rows").QuerySelector(".ex-pivot-entry")!.DragStartAsync(new DragEventArgs());
        await cut.Find(".ex-pivot-sheet").DropAsync(new DragEventArgs());
        await cut.Find("button[aria-label='Hide Field List']").ClickAsync(new MouseEventArgs());
        await cut.Find(".ex-pivot-field-list-toggle").ClickAsync(new MouseEventArgs());

        Assert.Equal(rows, cut.FindComponents<ExGridRow<PivotDisplayRow>>().Sum(r => r.RenderCount));
        Assert.Equal(grid, Grid(cut).RenderCount);
    }
}
