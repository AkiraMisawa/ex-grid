using Bunit;
using ExGrid;
using ExGrid.Components;
using ExGrid.Selection;
using ExPivot.Components.Tests.Support;
using ExPivot.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using PivotComponent = ExPivot.Components.ExPivot;

namespace ExPivot.Components.Tests;

/// <summary>
/// Find in ExPivot (ADR-0332 §1): Ctrl+F over the report as shown. Every cell the report's grid lays
/// out — row labels, values and totals — is matched on its displayed text (the column labels stand in
/// the grid's headers, which Find cannot land on, and are not searched); a collapsed group is
/// not searched inside, and nothing is expanded to show a match. The rows outside the Window are read
/// from the report source by the Report Version on screen. The painted matches are marked by the
/// reference rule over the same text (ADR-0332 §4).
/// </summary>
public sealed class FindTests : PivotTestContext
{
    private static readonly PivotLayout RegionAmount = new() { Rows = [P("Region")], Values = [Sum("Amount")] };

    // Compact form, every group expanded:
    //   −East | 180, Apples | 130, Pears | 50, −North | 10, Pears | 10, −West | 90, Apples | 70,
    //   Plums | 20, −(blank) | 5, Plums | 5, Grand Total | 285
    private static readonly PivotLayout RegionProduct = new() { Rows = [P("Region"), P("Product")], Values = [Sum("Amount")] };

    private static Task PressAsync(IRenderedComponent<PivotComponent> cut, string key, bool ctrl = false, bool fromDescendant = false)
    {
        var grid = Grid(cut);
        return grid.InvokeAsync(() => grid.Instance.OnKeyAsync(key, ctrl, false, false, false, false, fromDescendant));
    }

    private static Task TypeAsync(IRenderedComponent<PivotComponent> cut, string text)
        => cut.InvokeAsync(() => Grid(cut).Find("input.ex-find-field").InputAsync(new ChangeEventArgs { Value = text }));

    private static Task NextAsync(IRenderedComponent<PivotComponent> cut)
        => cut.InvokeAsync(() => Grid(cut).Find("button.ex-find-next").ClickAsync(new MouseEventArgs()));

    private static Task PreviousAsync(IRenderedComponent<PivotComponent> cut)
        => cut.InvokeAsync(() => Grid(cut).Find("button.ex-find-previous").ClickAsync(new MouseEventArgs()));

    private static CellPosition Focus(IRenderedComponent<PivotComponent> cut)
        => Grid(cut).Instance.ReadSelection().Selection.Focus;

    /// <summary>The Focus, or null while nothing is selected.</summary>
    private static CellPosition? FocusOrNone(IRenderedComponent<PivotComponent> cut)
        => Grid(cut).Instance.ReadSelection().Selection is { IsEmpty: false } selection ? selection.Focus : null;

    private static string Outcome(IRenderedComponent<PivotComponent> cut)
        => Grid(cut).Find(".ex-find-outcome").TextContent.Trim();

    /// <summary>Each painted row's first cell's text, with the indexes of its marked cells, "*" after
    /// the current match.</summary>
    private static Dictionary<int, string> Marks(IRenderedComponent<PivotComponent> cut)
        => cut.InvokeAsync(() =>
        {
            var marks = new Dictionary<int, string>();
            var rows = Grid(cut).FindAll(".ex-viewport .ex-row");
            for (var r = 0; r < rows.Count; r++)
            {
                var cells = rows[r].QuerySelectorAll("[role=gridcell]");
                var marked = new List<string>();
                for (var i = 0; i < cells.Length; i++)
                {
                    if (cells[i].ClassList.Contains("ex-match-mark"))
                        marked.Add(cells[i].ClassList.Contains("ex-match-mark-current") ? $"{i}*" : $"{i}");
                }
                if (marked.Count > 0)
                    marks[r] = string.Join(",", marked);
            }
            return marks;
        }).GetAwaiter().GetResult();

    [Fact] // ADR-0332 §1: Ctrl+F opens the report grid's find panel, and a step finds a row label, a value and a total by the text shown
    public async Task ADR0332_ctrl_f_finds_a_label_a_value_and_a_total_as_shown()
    {
        var cut = RenderPivot(RegionAmount);
        Assert.Equal(["East | 180", "North | 10", "West | 90", "(blank) | 5", "Grand Total | 285"], RowTexts(cut));

        await PressAsync(cut, "f", ctrl: true);
        Assert.Single(Grid(cut).FindAll(".ex-popover-find"));

        await TypeAsync(cut, "west");
        await NextAsync(cut);
        Assert.Equal(new CellPosition(2, 0), Focus(cut));

        await TypeAsync(cut, "90");
        await NextAsync(cut);
        Assert.Equal(new CellPosition(2, 1), Focus(cut));

        await TypeAsync(cut, "Grand");
        await NextAsync(cut);
        Assert.Equal(new CellPosition(4, 0), Focus(cut));

        await TypeAsync(cut, "285");
        await NextAsync(cut);
        Assert.Equal(new CellPosition(4, 1), Focus(cut));

        // The (blank) Item's label is what is shown, and is found as shown.
        await TypeAsync(cut, "(blank)");
        await NextAsync(cut);
        Assert.Equal(new CellPosition(3, 0), Focus(cut));
    }

    [Fact] // ADR-0332 §1: a value is matched on its displayed text — the Value Field's number format applied — never on the number behind it
    public async Task ADR0332_a_value_is_matched_as_formatted()
    {
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount") with { NumberFormat = "#,##0.00" }] });
        Assert.Equal("East | 180.00", RowTexts(cut)[0]);
        await PressAsync(cut, "f", ctrl: true);

        await TypeAsync(cut, "180.00");
        await NextAsync(cut);
        Assert.Equal(new CellPosition(0, 1), Focus(cut));

        // Whole cell: "180" is not the text shown, so nothing matches, and the panel says so.
        await cut.InvokeAsync(() => Grid(cut).Find("input.ex-find-whole-cell").ChangeAsync(new ChangeEventArgs { Value = true }));
        await TypeAsync(cut, "180");
        await NextAsync(cut);
        Assert.Equal("No match", Outcome(cut));
        Assert.Equal(new CellPosition(0, 1), Focus(cut));
    }

    [Fact] // ADR-0332 §1 and ADR-0055: steps run by rows from the cell after the Focus and wrap, Next and Previous alike
    public async Task ADR0332_find_walks_by_rows_across_the_columns_in_order_and_wraps()
    {
        var cut = RenderPivot(RegionProduct);
        await PressAsync(cut, "f", ctrl: true);
        await TypeAsync(cut, "pears");

        await NextAsync(cut);
        Assert.Equal(new CellPosition(2, 0), Focus(cut));
        await NextAsync(cut);
        Assert.Equal(new CellPosition(4, 0), Focus(cut));
        // Past the last, it wraps to the first.
        await NextAsync(cut);
        Assert.Equal(new CellPosition(2, 0), Focus(cut));
        // Previous goes back, wrapping too.
        await PreviousAsync(cut);
        Assert.Equal(new CellPosition(4, 0), Focus(cut));
        await PreviousAsync(cut);
        Assert.Equal(new CellPosition(2, 0), Focus(cut));
    }

    [Fact] // ADR-0332 §1: a collapsed group is not searched inside, and nothing is expanded to show a match — the report keeps its shape
    public async Task ADR0332_a_collapsed_group_is_not_searched_and_nothing_expands()
    {
        PivotLayout? told = null;
        var collapsed = RegionProduct with { Rows = [P("Region") with { ToggledItems = [PivotItemKey.Text("East")] }, P("Product")] };
        var cut = RenderPivot(collapsed, ps => ps.Add(p => p.LayoutChanged, layout => told = layout));
        var shape = RowTexts(cut);
        Assert.StartsWith("+East | 180", shape[0], StringComparison.Ordinal);
        Assert.Equal("Pears | 10", shape[2]);
        var version = Grid(cut).Instance.RowSequenceVersion;
        await PressAsync(cut, "f", ctrl: true);

        // East's Pears lie inside the collapsed group: only North's is found, and found again.
        await TypeAsync(cut, "pears");
        await NextAsync(cut);
        Assert.Equal(new CellPosition(2, 0), Focus(cut));
        await NextAsync(cut);
        Assert.Equal(new CellPosition(2, 0), Focus(cut));

        // East's Apples (130) are not laid out, so their total is not found either.
        await TypeAsync(cut, "130");
        await NextAsync(cut);
        Assert.Equal("No match", Outcome(cut));

        Assert.Null(told);
        Assert.Equal(shape, RowTexts(cut));
        Assert.Equal(version, Grid(cut).Instance.RowSequenceVersion);
        Assert.True(cut.Instance.CurrentLayout.Rows[0].IsCollapsed(PivotItemKey.Text("East")));
    }

    [Fact] // ADR-0332 §1: the rows beyond the Window are searched too, read from the report source, and the Focus is taken to the match
    public async Task ADR0332_rows_beyond_the_window_are_searched()
    {
        var records = Enumerable.Range(0, 600).Select(i => new Sale($"R{i:0000}", "Apples", i, 1, true)).ToArray();
        var cut = RenderPivot(RegionAmount, records: records);
        // Six hundred records are summed off the renderer: the report is read once it has landed.
        cut.WaitForState(() => !cut.Instance.IsLoading);
        Assert.DoesNotContain(RowTexts(cut), text => text.StartsWith("R0450", StringComparison.Ordinal));
        await PressAsync(cut, "f", ctrl: true);

        await TypeAsync(cut, "r0450");
        await NextAsync(cut);

        Assert.Equal(new CellPosition(450, 0), Focus(cut));
        // And backward from there, past the start, to the Grand Total's label's neighbour: "R0599".
        await TypeAsync(cut, "R059");
        await PreviousAsync(cut);
        Assert.Equal(new CellPosition(599, 0), Focus(cut));
    }

    [Fact] // ADR-0332 §1 and ADR-0055: with more than one cell selected, a step searches within the selection
    public async Task ADR0332_a_selection_is_the_scope()
    {
        var cut = RenderPivot(RegionProduct);
        var grid = Grid(cut).Instance;
        // North's and West's rows: rows 3 to 7, both columns.
        await cut.InvokeAsync(() => grid.PlaceSelectionAsync(new SelectionRange(3, 0, 5, 2), new CellPosition(3, 0), grid.RowSequenceVersion));
        await PressAsync(cut, "f", ctrl: true);

        await TypeAsync(cut, "apples");
        await NextAsync(cut);
        Assert.Equal(new CellPosition(6, 0), Focus(cut));
        // The only one inside: East's Apples, at row 1, is outside the selection.
        await NextAsync(cut);
        Assert.Equal(new CellPosition(6, 0), Focus(cut));
    }

    [Fact] // ADR-0332 §4: ExPivot passes the reference rule, so every painted match is marked as it is typed, and the current one apart
    public async Task ADR0332_the_matches_in_view_are_marked()
    {
        var cut = RenderPivot(RegionProduct);
        await PressAsync(cut, "f", ctrl: true);

        await TypeAsync(cut, "pears");
        Assert.Equal(new Dictionary<int, string> { [2] = "0", [4] = "0" }, Marks(cut));

        await NextAsync(cut);
        Assert.Equal(new Dictionary<int, string> { [2] = "0*", [4] = "0" }, Marks(cut));

        // A value and a label alike: "5" is in Pears' 50, (blank)'s 5, Plums' 5 and the Grand Total 285.
        await TypeAsync(cut, "5");
        var marks = Marks(cut);
        Assert.Equal("1", marks[2]);
        Assert.Equal("1", marks[8]);
        Assert.Equal("1", marks[9]);
        Assert.Equal("1", marks[10]);

        // The marks go when the panel closes.
        await PressAsync(cut, "Escape", fromDescendant: true);
        Assert.Empty(Marks(cut));
    }

    [Fact] // ADR-0332 §1 and ADR-0151: over a server's report, Find reads the cells through its copy transport, by the Report Version on screen, in blocks, and stops at the match
    public async Task ADR0332_a_servers_report_is_searched_through_its_copy_transport()
    {
        var records = Enumerable.Range(0, 12_000).Select(i => new Sale($"R{i:00000}", "Apples", i, 1, true)).ToArray();
        await using var server = PivotReportSource.From(Bundled(records));
        static T Wire<T>(T value) => PivotReportJson.Read<T>(PivotReportJson.Write(value));
        var copies = new List<PivotReportCopyQuery>();
        var remote = PivotReportSource.Fetch(server.Fields, server.Features, server.UpdateMode,
            async (request, ct) => Wire(await server.WindowAsync(Wire(request), ct)),
            items: server.RawItemsAsync, reportItems: async (query, ct) => Wire(await server.ItemsAsync(Wire(query), ct)),
            copy: async (query, ct) =>
            {
                copies.Add(query);
                return Wire(await server.CopyAsync(Wire(query), ct));
            },
            summary: server.SummaryAsync, details: server.DetailsAsync);
        var cut = RenderPivot(RegionAmount, reportSource: remote);
        cut.WaitForAssertion(() => Assert.Equal("R00000 | 0", RowTexts(cut)[0]));
        await PressAsync(cut, "f", ctrl: true);

        await TypeAsync(cut, "R11999");
        await NextAsync(cut);

        cut.WaitForAssertion(() => Assert.Equal(new CellPosition(11999, 0), Focus(cut)));
        Assert.True(copies.Count > 1, "the report is read in blocks, not whole");
        Assert.All(copies, query => Assert.Equal(cut.Instance.Report!.Metadata.Version, query.Version));
        Assert.All(copies, query => Assert.Equal(Grid(cut).Instance.Columns.Select(c => c.Name), query.Columns));

        // A match near the Focus is answered from the first block read, and nothing further is asked.
        copies.Clear();
        await TypeAsync(cut, "R11990");
        await PreviousAsync(cut);
        cut.WaitForAssertion(() => Assert.Equal(new CellPosition(11990, 0), Focus(cut)));
        Assert.Single(copies);
    }

    // ---- Ctrl+H, and a step that cannot go on (ADR-0332 §1/§2, ADR-0025) ---------------------------

    /// <summary>What the Pivot Toolbar says of the user's last gesture, or null.</summary>
    private static string? Notice(IRenderedComponent<PivotComponent> cut)
        => cut.FindAll(".ex-pivot-refusal-notice") is { Count: 1 } notices ? notices[0].TextContent.Trim() : null;

    [Fact] // ADR-0332 §2: a report is not written, so Ctrl+H opens nothing, and ExPivot says so in Excel's words
    public async Task ADR0332_ctrl_h_on_the_report_is_refused_and_said()
    {
        var cut = RenderPivot(RegionAmount);

        await PressAsync(cut, "h", ctrl: true);

        Assert.Empty(Grid(cut).FindAll(".ex-popover-find"));
        cut.WaitForAssertion(() => Assert.Equal("Cannot change this part of a PivotTable report.", Notice(cut)));
        Assert.Equal("Cannot change this part of a PivotTable report.", PivotWords.EnglishFor("replace-unavailable"));
        Assert.Equal("alert", cut.Find(".ex-pivot-refusal-notice").GetAttribute("role"));
    }

    [Fact] // ADR-0332 §2 and ADR-0060: the refusal of Ctrl+H is Excel's Japanese edition's under PivotWords.Japanese
    public async Task ADR0332_ctrl_h_is_refused_in_the_japanese_words()
    {
        var cut = RenderPivot(RegionAmount, ps => ps.Add(p => p.Label, PivotWords.Japanese));

        await PressAsync(cut, "h", ctrl: true);

        cut.WaitForAssertion(() => Assert.Equal("ピボットテーブル レポートのこの部分は変更できません。", Notice(cut)));
    }

    /// <summary>A report computed by a "server" over <see cref="LiveSource"/>, whose Copy — what Find
    /// reads the report through — the test answers, holds or replaces; redrawn at once on a change.</summary>
    private (IRenderedComponent<PivotComponent> Cut, LiveSource Live, HeldCopies Copies) RenderOverServer(IReadOnlyList<Sale>? records = null)
    {
        var live = new LiveSource(records);
        var server = PivotReportSource.From(live);
        var copies = new HeldCopies(server);
        var remote = PivotReportSource.Fetch(server.Fields, server.Features, server.UpdateMode, server.WindowAsync,
            items: server.RawItemsAsync, reportItems: server.ItemsAsync, copy: copies.CopyAsync,
            summary: server.SummaryAsync, details: server.DetailsAsync);
        live.Changed += change => remote.NotifyChanged(change.SourceVersion);
        var cut = RenderPivot(RegionAmount, ps => ps.Add(p => p.RedrawInterval, TimeSpan.Zero), reportSource: remote);
        cut.WaitForAssertion(() => Assert.NotEmpty(RowTexts(cut)));
        return (cut, live, copies);
    }

    /// <summary>The sales with East's first sale at <paramref name="amount"/>: East's total is that
    /// plus 80.</summary>
    private static Sale[] EastAt(decimal amount) => [Sales[0] with { Amount = amount }, .. Sales[1..]];

    private static PivotReportCopyResult NotHeld(PivotReportCopyQuery query)
        => new(query.Version, [], new PivotReportRefusal(PivotReportRefusalKind.ReportVersionNotHeld, "Not held."));

    /// <summary>Moves the data on while a step is out, and waits for the report on screen to show it.</summary>
    private static async Task MoveOnAsync(IRenderedComponent<PivotComponent> cut, LiveSource live, decimal eastAmount)
    {
        var before = cut.Instance.Report!.Metadata.Version;
        await cut.InvokeAsync(() => live.Publish(EastAt(eastAmount)));
        cut.WaitForAssertion(() => Assert.Equal($"East | {eastAmount + 80}", RowTexts(cut)[0]));
        Assert.NotEqual(before, cut.Instance.Report!.Metadata.Version);
    }

    [Fact] // ADR-0332 §1 and ADR-0025: a source that refuses to be read is said on the Pivot Toolbar, and the step is given up — nothing moves, and no "No match" is claimed
    public async Task ADR0332_a_copy_refusal_is_said_and_the_step_is_given_up()
    {
        var (cut, _, copies) = RenderOverServer();
        copies.Answer = query => new(query.Version, [], new PivotReportRefusal(PivotReportRefusalKind.SourceRefused, "The server is busy."));
        await PressAsync(cut, "f", ctrl: true);
        var focus = FocusOrNone(cut);

        await TypeAsync(cut, "west");
        await NextAsync(cut);

        cut.WaitForAssertion(() => Assert.Equal("The source refused to answer: The server is busy.", Notice(cut)));
        Assert.Equal("", Outcome(cut));
        Assert.Equal(focus, FocusOrNone(cut));
        Assert.Single(Grid(cut).FindAll(".ex-popover-find"));
    }

    [Fact] // ADR-0332 §1 and ADR-0025: a source that no longer holds the report on screen is said, and the step is given up
    public async Task ADR0332_a_report_on_screen_the_source_no_longer_holds_is_said()
    {
        var (cut, _, copies) = RenderOverServer();
        copies.Answer = NotHeld;
        await PressAsync(cut, "f", ctrl: true);
        var focus = FocusOrNone(cut);

        await TypeAsync(cut, "west");
        await NextAsync(cut);

        cut.WaitForAssertion(() => Assert.Equal("The source no longer holds the report shown: refresh, then find again.", Notice(cut)));
        Assert.Equal("", Outcome(cut));
        Assert.Equal(focus, FocusOrNone(cut));
    }

    [Theory] // ADR-0332 §1 and ADR-0011: a live update under the same order while a step reads — the old version's cells answered, or no longer held — is searched again over the report now shown, never answered from the old one
    [InlineData(false)]
    [InlineData(true)]
    public async Task ADR0332_a_step_under_a_live_update_searches_the_report_now_shown(bool notHeld)
    {
        var (cut, live, copies) = RenderOverServer();
        await PressAsync(cut, "f", ctrl: true);
        await TypeAsync(cut, "999");
        var order = Grid(cut).Instance.RowSequenceVersion;

        copies.Hold = true;
        var step = NextAsync(cut);
        var (query, reply) = await copies.NextHeldAsync();
        copies.Hold = false;
        // East becomes 999 while the step reads the report before it, where nothing shows 999.
        await MoveOnAsync(cut, live, 919);
        Assert.Equal(order, Grid(cut).Instance.RowSequenceVersion);
        reply.SetResult(notHeld ? NotHeld(query) : await copies.ServerAsync(query));
        await step;

        cut.WaitForAssertion(() => Assert.Equal(new CellPosition(0, 1), Focus(cut)));
        // The first read was of the version replaced; the step ends reading the version now shown.
        Assert.NotEqual(cut.Instance.Report!.Metadata.Version, copies.Asked[0].Version);
        Assert.Equal(cut.Instance.Report!.Metadata.Version, copies.Asked[^1].Version);
        Assert.Null(Notice(cut));
    }

    [Fact] // ADR-0332 §1 and ADR-0025: a report that keeps changing under a step is searched again a bounded number of times, then said, and the step given up
    public async Task ADR0332_a_report_that_keeps_changing_is_said_after_three_searches()
    {
        var (cut, live, copies) = RenderOverServer();
        await PressAsync(cut, "f", ctrl: true);
        await TypeAsync(cut, "no such text");
        var focus = FocusOrNone(cut);

        copies.Hold = true;
        var step = NextAsync(cut);
        foreach (var amount in new[] { 300m, 400m, 500m })
        {
            var (query, reply) = await copies.NextHeldAsync();
            await MoveOnAsync(cut, live, amount);
            reply.SetResult(NotHeld(query));
        }
        await step;

        cut.WaitForAssertion(() => Assert.Equal("The report kept changing while it was searched: find again.", Notice(cut)));
        Assert.Equal(3, copies.Asked.Count);
        Assert.Equal("", Outcome(cut));
        Assert.Equal(focus, FocusOrNone(cut));
    }

    [Fact] // ADR-0332 §1 and ADR-0055: a long walk is given up between its reads once the panel closes — nothing further is read, and nothing moves
    public async Task ADR0332_closing_the_panel_stops_a_long_walk()
    {
        var records = Enumerable.Range(0, 12_000).Select(i => new Sale($"R{i:00000}", "Apples", i, 1, true)).ToArray();
        var (cut, _, copies) = RenderOverServer(records);
        await PressAsync(cut, "f", ctrl: true);
        await TypeAsync(cut, "R11999");
        var focus = FocusOrNone(cut);

        copies.Hold = true;
        var step = NextAsync(cut);
        var (query, reply) = await copies.NextHeldAsync();
        copies.Hold = false;
        await PressAsync(cut, "Escape", fromDescendant: true);
        Assert.Empty(Grid(cut).FindAll(".ex-popover-find"));
        reply.SetResult(await copies.ServerAsync(query));
        await step;

        Assert.Single(copies.Asked);
        Assert.Equal(focus, FocusOrNone(cut));
        Assert.Null(Notice(cut));
    }

    /// <summary>Copy, as a server's transport: answered by the server, by <see cref="Answer"/>, or
    /// held for the test while <see cref="Hold"/>.</summary>
    private sealed class HeldCopies(PivotReportSource server)
    {
        private readonly System.Threading.Channels.Channel<(PivotReportCopyQuery, TaskCompletionSource<PivotReportCopyResult>)> _held =
            System.Threading.Channels.Channel.CreateUnbounded<(PivotReportCopyQuery, TaskCompletionSource<PivotReportCopyResult>)>();

        public bool Hold { get; set; }

        public Func<PivotReportCopyQuery, PivotReportCopyResult>? Answer { get; set; }

        public List<PivotReportCopyQuery> Asked { get; } = [];

        public async ValueTask<PivotReportCopyResult> CopyAsync(PivotReportCopyQuery query, CancellationToken cancellationToken)
        {
            lock (Asked) Asked.Add(query);
            if (Hold)
            {
                var reply = new TaskCompletionSource<PivotReportCopyResult>(TaskCreationOptions.RunContinuationsAsynchronously);
                _held.Writer.TryWrite((query, reply));
                return await reply.Task;
            }
            return Answer?.Invoke(query) ?? await server.CopyAsync(query, cancellationToken);
        }

        public async Task<(PivotReportCopyQuery Query, TaskCompletionSource<PivotReportCopyResult> Reply)> NextHeldAsync()
            => await _held.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        public Task<PivotReportCopyResult> ServerAsync(PivotReportCopyQuery query) => server.CopyAsync(query).AsTask();
    }
}
