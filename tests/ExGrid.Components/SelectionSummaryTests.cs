using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Data;
using ExGrid.Selection;
using ExGrid.Summarizing;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The Selection Summary (ADR-0130, SM-1..SM-5, SM-9, SM-10, SM-14, SM-15): the grid asks and shows only the
/// answer to the current question; the Consumer sums. 20px rows in a 120px Viewport, three 100px
/// columns: Book, Amount, AsOf. Row i's Amount is i.
/// </summary>
public class SelectionSummaryTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static GridColumn<TestRow>[] Columns(Func<object, string>? amountFormat = null) =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100, format: amountFormat, editable: true),
        new("AsOf", ColumnType.Date, r => r.AsOf, width: Fixed100),
    ];

    private sealed class Heard
    {
        public List<GridSummaryRequest> Requests { get; } = [];
        public List<CancellationToken> Tokens { get; } = [];
        public List<SelectionSummary> Changes { get; } = [];
        public List<SummaryFigures> Chosen { get; } = [];
    }

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        Heard heard,
        Func<GridSummaryRequest, CancellationToken, Task<GridSummaryResult>>? onSummarize,
        TestRow[]? rows = null,
        Func<object, string>? amountFormat = null,
        bool menu = false)
    {
        rows ??= TestRows.Many(500);
        return Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, rows)
              .Add(g => g.TotalCount, rows.Length)
              .Add(g => g.Columns, Columns(amountFormat))
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 120)
              .Add(g => g.ViewportWidth, 350)
              .Add(g => g.OnSelectionSummaryChanged, s => heard.Changes.Add(s));
            if (menu)
                ps.Add(g => g.SummaryFiguresChanged, f => heard.Chosen.Add(f));
            ps.Add(g => g.OnClear, _ => { });
            if (onSummarize is not null)
            {
                ps.Add(g => g.OnSummarize, (request, token) =>
                {
                    heard.Requests.Add(request);
                    heard.Tokens.Add(token);
                    return onSummarize(request, token);
                });
            }
        });
    }

    private static Task PressAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key, bool shift = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, false, shift, false, false, false));

    private static Task ClickCellAsync<TRow>(IRenderedComponent<ExGrid<TRow>> cut, double x, double y, bool shift = false)
        where TRow : class
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y, ShiftKey = shift });

    // The reference, over the rendered rows: what InMemoryGridSource would answer.
    private static Func<GridSummaryRequest, CancellationToken, Task<GridSummaryResult>> Reference(TestRow[] rows)
        => (request, _) => Task.FromResult(GridSummary.Of(rows, request, name => name switch
        {
            "Book" => r => r.Book,
            "Amount" => r => r.Amount,
            "AsOf" => r => r.AsOf,
            _ => null,
        }));

    private static string SummaryText(IRenderedComponent<ExGrid<TestRow>> cut) => cut.Find(".ex-summary").TextContent;

    [Fact] // ADR-0130 / SM-1: the question carries the ranges, the version, the visible columns and the figures
    public async Task ADR0130_a_request_carries_the_ranges_the_version_the_columns_and_the_figures()
    {
        var heard = new Heard();
        var rows = TestRows.Many(500);
        var cut = RenderGrid(heard, Reference(rows), rows);
        cut.Render(ps => ps.Add(g => g.RowSequenceVersion, 7));

        await ClickCellAsync(cut, 150, 10); // (0, 1)
        await PressAsync(cut, "ArrowDown", shift: true);

        var request = Assert.Single(heard.Requests);
        Assert.Equal([new SelectionRange(0, 1, 2, 1)], request.Ranges);
        Assert.Equal(7, request.RowSequenceVersion);
        Assert.Equal(["Book", "Amount", "AsOf"], request.Columns);
        Assert.Equal(SummaryFigures.Default, request.Figures);
        Assert.Equal(new CellPosition(0, 1), request.Focus);
    }

    [Fact] // ADR-0130 / SM-9: the default figures, in Excel's order, the Focus's column's format applied
    public async Task ADR0130_the_status_line_shows_average_count_and_sum()
    {
        var heard = new Heard();
        var rows = TestRows.Many(500);
        var cut = RenderGrid(heard, Reference(rows), rows,
            amountFormat: v => ((decimal)v).ToString("N2", System.Globalization.CultureInfo.InvariantCulture));

        await ClickCellAsync(cut, 150, 30); // (1, 1): Amount 1
        await PressAsync(cut, "ArrowDown", shift: true);
        await PressAsync(cut, "ArrowDown", shift: true); // 1, 2, 3

        cut.WaitForAssertion(() => Assert.Equal("Average: 2.00Count: 3Sum: 6.00", SummaryText(cut)));
        var figures = cut.FindAll(".ex-summary-figure").Select(e => e.TextContent).ToArray();
        Assert.Equal(["Average: 2.00", "Count: 3", "Sum: 6.00"], figures);
    }

    [Fact] // ADR-0130 / SM-2: a change of selection clears the figures at once; the late answer is discarded
    public async Task ADR0130_a_new_selection_never_shows_the_previous_answer()
    {
        var heard = new Heard();
        var gates = new List<TaskCompletionSource<GridSummaryResult>>();
        var cut = RenderGrid(heard, (request, token) =>
        {
            var gate = new TaskCompletionSource<GridSummaryResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            gates.Add(gate);
            return gate.Task;
        });

        await ClickCellAsync(cut, 150, 10);
        await PressAsync(cut, "ArrowDown", shift: true);
        Assert.Equal("Calculating…", SummaryText(cut));

        // The first answer arrives and stands.
        await cut.InvokeAsync(() => gates[0].SetResult(Answer(sum: 1m)));
        cut.WaitForAssertion(() => Assert.Contains("Sum: 1", SummaryText(cut)));

        // The selection moves: the figures go in the render that shows the move.
        await PressAsync(cut, "ArrowDown", shift: true);
        Assert.Equal("Calculating…", SummaryText(cut));

        // It moves again while the second question is out: that one is cancelled, and its answer,
        // arriving late, is shown nowhere.
        await PressAsync(cut, "ArrowDown", shift: true);
        Assert.True(heard.Tokens[1].IsCancellationRequested);
        await cut.InvokeAsync(() => gates[1].SetResult(Answer(sum: 999m)));
        Assert.Equal("Calculating…", SummaryText(cut));
        Assert.DoesNotContain(heard.Changes, c => c.Result?[SummaryFigures.Sum]?.Exact == 999m);

        await cut.InvokeAsync(() => gates[2].SetResult(Answer(sum: 3m)));
        cut.WaitForAssertion(() => Assert.Contains("Sum: 3", SummaryText(cut)));
    }

    [Fact] // ADR-0130 / SM-2: an answer under a stale Row Sequence Version shows nothing
    public async Task ADR0130_an_answer_read_under_another_order_is_dropped()
    {
        var heard = new Heard();
        var gate = new TaskCompletionSource<GridSummaryResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cut = RenderGrid(heard, (_, _) => gate.Task);
        await ClickCellAsync(cut, 150, 10);
        await PressAsync(cut, "ArrowDown", shift: true);

        // The order moves while the question is out: the selection is dropped (ADR-0011).
        cut.Render(ps => ps.Add(g => g.RowSequenceVersion, 1));
        await cut.InvokeAsync(() => gate.SetResult(Answer(sum: 1m)));

        Assert.Empty(cut.FindAll(".ex-summary-figure"));
        Assert.Equal(SelectionSummaryStatus.None, heard.Changes[^1].Status);
    }

    [Fact] // ADR-0130 / SM-3: a row changing under the standing selection asks again; a scroll does not
    public async Task ADR0130_rows_changing_under_the_selection_ask_again()
    {
        var heard = new Heard();
        var rows = TestRows.Many(500);
        var cut = RenderGrid(heard, Reference(rows), rows);
        await ClickCellAsync(cut, 150, 10);
        await PressAsync(cut, "ArrowDown", shift: true);
        Assert.Single(heard.Requests);

        // The same rows handed over again, in a new list: nothing moved.
        cut.Render(ps => ps.Add(g => g.Window, rows.ToArray()));
        Assert.Single(heard.Requests);

        // A row replaced: the values may have moved.
        var changed = rows.ToArray();
        changed[1] = new TestRow { Book = "Row 000001", Amount = 100m };
        cut.Render(ps => ps.Add(g => g.Window, changed));
        Assert.Equal(2, heard.Requests.Count);
    }

    // ---- SM-15: the walk of a new Window (ADR-0130, 2026-10-07) ----------------------------------

    /// <summary>Counts the comparisons the grid makes of one row with another.</summary>
    private sealed class Comparisons
    {
        public int Count { get; set; }
    }

    /// <summary>A row with value equality that counts each comparison: the grid learns whether the
    /// rows moved by the row type's equality (ADR-0130).</summary>
    private sealed class CountedRow(int id, decimal amount, Comparisons comparisons) : IEquatable<CountedRow>
    {
        public int Id { get; } = id;

        public decimal Amount { get; } = amount;

        public CountedRow WithAmount(decimal value) => new(Id, value, comparisons);

        public bool Equals(CountedRow? other)
        {
            comparisons.Count++;
            return other is not null && other.Id == Id && other.Amount == Amount;
        }

        public override bool Equals(object? obj) => Equals(obj as CountedRow);

        public override int GetHashCode() => HashCode.Combine(Id, Amount);
    }

    private static CountedRow[] CountedRows(int count, Comparisons comparisons)
    {
        var rows = new CountedRow[count];
        for (var i = 0; i < count; i++)
            rows[i] = new CountedRow(i, i, comparisons);
        return rows;
    }

    private IRenderedComponent<ExGrid<CountedRow>> RenderCounted(CountedRow[] rows, List<GridSummaryRequest> requests)
        => Render<ExGrid<CountedRow>>(ps => ps
            .Add(g => g.Window, rows)
            .Add(g => g.TotalCount, rows.Length)
            .Add(g => g.Columns, new GridColumn<CountedRow>[]
            {
                new("Id", ColumnType.Number, r => r.Id, width: Fixed100),
                new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
            })
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 120)
            .Add(g => g.ViewportWidth, 250)
            .Add(g => g.OnSummarize, (request, _) =>
            {
                requests.Add(request);
                return Task.FromResult(Answer(sum: 0m));
            }));

    [Fact] // ADR-0130 / SM-15: with no figure standing or asked for, a new Window is not walked
    public async Task ADR0130_with_no_figure_a_new_window_is_not_walked()
    {
        var comparisons = new Comparisons();
        var requests = new List<GridSummaryRequest>();
        var rows = CountedRows(500, comparisons);
        var cut = RenderCounted(rows, requests);
        await ClickCellAsync(cut, 150, 10); // one cell: nothing to sum
        comparisons.Count = 0;

        var changed = rows.ToArray();
        changed[^1] = changed[^1].WithAmount(-1m);
        cut.Render(ps => ps.Add(g => g.Window, changed));

        Assert.Equal(0, comparisons.Count);
        Assert.Empty(requests);
    }

    [Fact] // ADR-0130 / SM-15: a row changing outside the Selection moves no figure, and only the selected positions are compared
    public async Task ADR0130_a_change_outside_the_selection_moves_no_figure()
    {
        var comparisons = new Comparisons();
        var requests = new List<GridSummaryRequest>();
        var rows = CountedRows(500, comparisons);
        var cut = RenderCounted(rows, requests);
        await ClickCellAsync(cut, 150, 30);              // (1, 1)
        await ClickCellAsync(cut, 150, 50, shift: true); // to (2, 1): rows 1 and 2
        Assert.Single(requests);
        comparisons.Count = 0;

        var changed = rows.ToArray();
        changed[4] = changed[4].WithAmount(-1m);
        changed[0] = changed[0].WithAmount(-1m);
        cut.Render(ps => ps.Add(g => g.Window, changed));

        Assert.Single(requests);
        Assert.Equal(2, comparisons.Count);
    }

    [Fact] // ADR-0130 / SM-15: a row changing inside the Selection still asks again
    public async Task ADR0130_a_change_inside_the_selection_asks_again()
    {
        var comparisons = new Comparisons();
        var requests = new List<GridSummaryRequest>();
        var rows = CountedRows(500, comparisons);
        var cut = RenderCounted(rows, requests);
        await ClickCellAsync(cut, 150, 30);
        await ClickCellAsync(cut, 150, 50, shift: true); // rows 1 and 2

        var changed = rows.ToArray();
        changed[2] = changed[2].WithAmount(-1m);
        cut.Render(ps => ps.Add(g => g.Window, changed));

        Assert.Equal(2, requests.Count);
        Assert.Equal(requests[0].Ranges, requests[1].Ranges);
    }

    [Fact] // ADR-0130 / SM-15: a changed row count under a standing figure asks again, even a row added after the Selection
    public async Task ADR0130_a_changed_row_count_asks_again()
    {
        var comparisons = new Comparisons();
        var requests = new List<GridSummaryRequest>();
        var rows = CountedRows(500, comparisons);
        var cut = RenderCounted(rows, requests);
        await ClickCellAsync(cut, 150, 30);
        await ClickCellAsync(cut, 150, 50, shift: true); // rows 1 and 2

        CountedRow[] added = [.. rows, new CountedRow(500, 500m, comparisons)];
        cut.Render(ps => ps.Add(g => g.Window, added).Add(g => g.TotalCount, added.Length));

        Assert.Equal(2, requests.Count);
    }

    [Fact] // ADR-0130 / SM-15: a row count that no longer holds a selected row asks again
    public async Task ADR0130_a_row_count_cutting_into_the_selection_asks_again()
    {
        var comparisons = new Comparisons();
        var requests = new List<GridSummaryRequest>();
        var rows = CountedRows(5, comparisons);
        var cut = RenderCounted(rows, requests);
        await ClickCellAsync(cut, 150, 50);
        await ClickCellAsync(cut, 150, 90, shift: true); // rows 2 to 4

        var fewer = rows[..4];
        cut.Render(ps => ps.Add(g => g.Window, fewer).Add(g => g.TotalCount, fewer.Length));

        Assert.Equal(2, requests.Count);
    }

    [Fact] // ADR-0130 / SM-15: a row count that moves while the Selection is outside both Windows asks again — a row added above may have shifted it
    public async Task ADR0130_a_row_count_moving_under_a_selection_the_window_left_asks_again()
    {
        var comparisons = new Comparisons();
        var requests = new List<GridSummaryRequest>();
        var rows = CountedRows(500, comparisons);
        var cut = RenderCounted(rows, requests);
        await ClickCellAsync(cut, 150, 30);
        await ClickCellAsync(cut, 150, 50, shift: true); // rows 1 and 2
        cut.Render(ps => ps.Add(g => g.Window, rows[100..150]).Add(g => g.WindowStart, 100));
        Assert.Single(requests);

        // A row added before row 1 would shift both selected rows, and neither Window holds them to
        // show whether one was.
        cut.Render(ps => ps.Add(g => g.Window, rows[100..150]).Add(g => g.TotalCount, 501));

        Assert.Equal(2, requests.Count);
    }

    [Fact] // ADR-0130 / SM-15: the same list handed over at another start holds other rows at the selected positions, and asks again
    public async Task ADR0130_the_same_list_at_another_start_asks_again()
    {
        var comparisons = new Comparisons();
        var requests = new List<GridSummaryRequest>();
        var rows = CountedRows(500, comparisons);
        var cut = RenderCounted(rows, requests);
        await ClickCellAsync(cut, 150, 30);
        await ClickCellAsync(cut, 150, 50, shift: true); // rows 1 and 2
        var slice = rows[..50];
        cut.Render(ps => ps.Add(g => g.Window, slice));

        cut.Render(ps => ps.Add(g => g.WindowStart, 1));

        Assert.Equal(2, requests.Count);
    }

    [Fact] // ADR-0130 / SM-15 / ADR-0011: an order that moved drops the Selection and its figures, so the new Window is not walked
    public async Task ADR0130_a_window_in_a_new_order_is_not_walked()
    {
        var comparisons = new Comparisons();
        var requests = new List<GridSummaryRequest>();
        var rows = CountedRows(500, comparisons);
        var cut = RenderCounted(rows, requests);
        await ClickCellAsync(cut, 150, 30);
        await ClickCellAsync(cut, 150, 50, shift: true); // rows 1 and 2
        comparisons.Count = 0;

        cut.Render(ps => ps.Add(g => g.Window, rows.Reverse().ToArray()).Add(g => g.RowSequenceVersion, 1));

        Assert.Equal(0, comparisons.Count);
        Assert.Single(requests);
    }

    [Fact] // ADR-0130 / SM-4: a decline shows its reason, never a figure
    public async Task ADR0130_a_decline_shows_its_reason()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard, (_, _) => Task.FromResult(GridSummaryResult.Declined("Too many rows")));
        await ClickCellAsync(cut, 150, 10);
        await PressAsync(cut, "ArrowDown", shift: true);

        cut.WaitForAssertion(() => Assert.Equal("Too many rows", SummaryText(cut)));
        Assert.Empty(cut.FindAll(".ex-summary-figure"));
        Assert.Equal(SelectionSummaryStatus.Declined, heard.Changes[^1].Status);
    }

    [Fact] // ADR-0130 / SM-5: with nobody to answer, no strip, figure or mark is shown
    public async Task ADR0130_with_nobody_to_answer_nothing_is_shown()
    {
        var cut = RenderGrid(new Heard(), onSummarize: null);
        await ClickCellAsync(cut, 150, 10);
        await PressAsync(cut, "ArrowDown", shift: true);

        Assert.False(cut.Instance.CanSummarize);
        Assert.Empty(cut.FindAll(".ex-summary"));
        Assert.Empty(cut.FindAll(".ex-status"));
    }

    [Fact] // ADR-0130 / SM-14: switched off with nobody listening, the grid shows no strip and asks nothing
    public async Task ADR0130_switched_off_nothing_is_shown_or_asked()
    {
        var requests = 0;
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(50))
            .Add(g => g.TotalCount, 50)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 120)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.ShowSelectionSummary, false)
            .Add(g => g.OnSummarize, (_, _) => { requests++; return Task.FromResult(Answer(sum: 0m)); }));
        await ClickCellAsync(cut, 150, 10);
        await PressAsync(cut, "ArrowDown", shift: true);

        Assert.True(cut.Instance.CanSummarize);
        Assert.Equal(0, requests);
        Assert.Empty(cut.FindAll(".ex-summary"));
        Assert.Empty(cut.FindAll(".ex-status"));

        // Switched back on, the standing selection is asked about.
        cut.Render(ps => ps.Add(g => g.ShowSelectionSummary, true));
        Assert.Equal(1, requests);
    }

    [Fact] // ADR-0130 / SM-14: switched off, a Consumer that listens is still told, for a status bar of its own
    public async Task ADR0130_switched_off_a_listener_is_still_told()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard, (_, _) => Task.FromResult(Answer(sum: 5m)));
        cut.Render(ps => ps.Add(g => g.ShowSelectionSummary, false));
        await ClickCellAsync(cut, 150, 10);
        await PressAsync(cut, "ArrowDown", shift: true);

        Assert.Empty(cut.FindAll(".ex-status"));
        cut.WaitForAssertion(() => Assert.Equal(5m, heard.Changes[^1].Result?[SummaryFigures.Sum]?.Exact));
    }

    [Fact] // ADR-0130 / SM-5: OnSummarize beside a bound Source is refused by name
    public void ADR0130_on_summarize_beside_a_source_is_refused()
    {
        var source = GridSource.From(TestRows.Window());
        var ex = Assert.Throws<InvalidOperationException>(() => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns())
            .Add(g => g.OnSummarize, (_, _) => Task.FromResult(Answer(sum: 0m)))));

        Assert.Contains("OnSummarize", ex.Message);
    }

    [Fact] // ADR-0130 / SM-6: a bound in-memory Source answers by itself
    public async Task ADR0130_a_bound_source_answers()
    {
        var source = GridSource.From(TestRows.Many(50));
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 120)
            .Add(g => g.ViewportWidth, 350));
        await ClickCellAsync(cut, 150, 10);
        await PressAsync(cut, "ArrowDown", shift: true);
        await PressAsync(cut, "ArrowDown", shift: true); // 0, 1, 2

        Assert.True(cut.Instance.CanSummarize);
        cut.WaitForAssertion(() => Assert.Equal("Average: 1Count: 3Sum: 3", SummaryText(cut)));
    }

    [Fact] // ADR-0130: a single cell asks nothing
    public async Task ADR0130_one_cell_asks_nothing()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard, (_, _) => Task.FromResult(Answer(sum: 0m)));
        await ClickCellAsync(cut, 150, 10);

        Assert.Empty(heard.Requests);
        Assert.Equal("", SummaryText(cut));
    }

    [Fact] // ADR-0130: an answer that is not the answer to the question is the Consumer's defect, named
    public async Task ADR0130_an_answer_with_other_figures_is_refused_by_name()
    {
        var cut = RenderGrid(new Heard(), (_, _) => Task.FromResult(GridSummaryResult.Answered(
            new Dictionary<SummaryFigures, AggregateResult> { [SummaryFigures.Sum] = AggregateResult.Of(1m) })));
        await ClickCellAsync(cut, 150, 10);

        await PressAsync(cut, "ArrowDown", shift: true);

        var raised = await Renderer.UnhandledException.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
        var ex = Assert.IsType<InvalidOperationException>(raised is AggregateException aggregate ? aggregate.InnerException : raised);
        Assert.Contains("ADR-0130", ex.Message);
    }

    [Fact] // ADR-0130 / SM-9: the right-click menu reports a change of figures; the grid holds none
    public async Task ADR0130_the_figures_menu_reports_the_choice()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard, (_, _) => Task.FromResult(Answer(sum: 0m)), menu: true);

        await cut.Find(".ex-summary").TriggerEventAsync("oncontextmenu", new MouseEventArgs { Button = 2 });
        var items = cut.FindAll(".ex-summary-menu-item");
        Assert.Equal(6, items.Count);
        Assert.Equal(["true", "true", "false", "false", "false", "true"], items.Select(i => i.GetAttribute("aria-checked")));

        await items[4].ClickAsync(new MouseEventArgs()); // Max
        Assert.Equal([SummaryFigures.Default | SummaryFigures.Max], heard.Chosen);
        // Held by the Consumer: the grid still shows what its parameter says.
        Assert.Equal("false", cut.FindAll(".ex-summary-menu-item")[4].GetAttribute("aria-checked"));
    }

    [Fact] // ADR-0130 / SM-9: without anybody to hold the choice, no menu is offered
    public async Task ADR0130_no_menu_without_a_binding()
    {
        var cut = RenderGrid(new Heard(), (_, _) => Task.FromResult(Answer(sum: 0m)));

        await Assert.ThrowsAsync<MissingEventHandlerException>(
            () => cut.Find(".ex-summary").TriggerEventAsync("oncontextmenu", new MouseEventArgs { Button = 2 }));

        Assert.Empty(cut.FindAll(".ex-summary-menu"));
    }

    [Fact] // ADR-0130 / SM-10: the figures are not written to the live region
    public async Task ADR0130_the_figures_are_not_announced()
    {
        var heard = new Heard();
        var rows = TestRows.Many(500);
        var cut = RenderGrid(heard, Reference(rows), rows);
        await ClickCellAsync(cut, 150, 30);
        await PressAsync(cut, "ArrowDown", shift: true);
        cut.WaitForAssertion(() => Assert.Contains("Sum", SummaryText(cut)));

        Assert.DoesNotContain("Sum", cut.Find(".ex-announce").TextContent);
        Assert.Null(cut.Find(".ex-summary").GetAttribute("role"));
        Assert.Null(cut.Find(".ex-summary").GetAttribute("aria-live"));
    }

    [Fact] // ADR-0130 / SM-3: an edit the grid hands over asks again, under the selection that stands
    public async Task ADR0130_an_edit_handed_over_asks_again()
    {
        var heard = new Heard();
        var rows = TestRows.Many(500);
        var cut = RenderGrid(heard, Reference(rows), rows);
        await ClickCellAsync(cut, 150, 10);
        await PressAsync(cut, "ArrowDown", shift: true);
        Assert.Single(heard.Requests);

        await PressAsync(cut, "Delete");

        Assert.Equal(2, heard.Requests.Count);
        Assert.Equal(heard.Requests[0].Ranges, heard.Requests[1].Ranges);
    }

    [Fact] // ADR-0130: one cell Ctrl+clicked twice is one cell, and asks nothing
    public async Task ADR0130_the_same_cell_twice_is_one_cell()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard, (_, _) => Task.FromResult(Answer(sum: 0m)));
        await ClickCellAsync(cut, 150, 10);

        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 150, OffsetY = 10, CtrlKey = true });

        Assert.Empty(heard.Requests);
    }

    [Fact] // ADR-0130 / ADR-0060: a figure that is not finite says #NUM!, never nothing
    public async Task ADR0130_a_figure_that_is_not_finite_says_so()
    {
        var cut = RenderGrid(new Heard(), (_, _) => Task.FromResult(GridSummaryResult.Answered(
            new Dictionary<SummaryFigures, AggregateResult>
            {
                [SummaryFigures.Average] = AggregateResult.Of(AggregateError.NotANumber),
                [SummaryFigures.Count] = AggregateResult.Of(2m),
                [SummaryFigures.Sum] = AggregateResult.Of(AggregateError.NotANumber),
            })));
        await ClickCellAsync(cut, 150, 10);
        await PressAsync(cut, "ArrowDown", shift: true);

        cut.WaitForAssertion(() => Assert.Equal("Average: #NUM!Count: 2Sum: #NUM!", SummaryText(cut)));
    }

    [Fact] // ADR-0130: a figure's text from the answerer is shown as it is; a figure without one is the grid's
    public async Task ADR0130_an_answerers_text_is_shown()
    {
        var cut = RenderGrid(new Heard(), (request, _) => Task.FromResult(GridSummaryResult.Answered(
            new Dictionary<SummaryFigures, AggregateResult>
            {
                [SummaryFigures.Average] = AggregateResult.Of(1.5m),
                [SummaryFigures.Count] = AggregateResult.Of(2m),
                [SummaryFigures.Sum] = AggregateResult.Of(3m),
            },
            new Dictionary<SummaryFigures, string> { [SummaryFigures.Sum] = "¥3" })));
        await ClickCellAsync(cut, 150, 10);
        await PressAsync(cut, "ArrowDown", shift: true);

        cut.WaitForAssertion(() => Assert.Equal("Average: 1.5Count: 2Sum: ¥3", SummaryText(cut)));
    }

    private static GridSummaryResult Answer(decimal sum) => GridSummaryResult.Answered(
        new Dictionary<SummaryFigures, AggregateResult>
        {
            [SummaryFigures.Average] = AggregateResult.Of(sum),
            [SummaryFigures.Count] = AggregateResult.Of(2m),
            [SummaryFigures.Sum] = AggregateResult.Of(sum),
        });
}
