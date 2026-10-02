using System.Globalization;
using Bunit;
using ExGrid.Cells;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Rows;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The Change Highlight as it reaches the DOM (ADR-0068, DC-64/DC-65): the Consumer says when a
/// cell's shown value changed, the grid paints <c>ex-changed</c> on that value cell while the
/// change is less than the duration ago, and takes the class away itself on one timer —
/// rendering again only the rows whose marks ended. The fake clock is handed in as the grid's
/// <c>Clock</c>, as the ADR says a test does.
/// </summary>
public class ChangeHighlightTests : GridTestContext
{
    private static readonly TimeSpan Second = TimeSpan.FromSeconds(1);

    private static TimeSpan Ms(int milliseconds) => TimeSpan.FromMilliseconds(milliseconds);

    /// <summary>
    /// A Consumer's knowledge of when each cell's shown value changed, keyed by book and column
    /// name, and how often the grid asked. Each <see cref="Lookup"/> is a new delegate — the
    /// change signal — over the same knowledge.
    /// </summary>
    private sealed class ChangeTimes : Dictionary<(string Book, string Column), DateTimeOffset>
    {
        public int Asked { get; private set; }

        public List<string> AskedColumns { get; } = [];

        public CellChangeOf<TestRow> Lookup() => (row, column) =>
        {
            Asked++;
            AskedColumns.Add(column.Name);
            return TryGetValue((row.Book, column.Name), out var at) ? at : null;
        };
    }

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        CellChangeOf<TestRow>? changedAt,
        TimeProvider? clock = null,
        TestRow[]? rows = null,
        GridColumn<TestRow>[]? columns = null,
        Action<ComponentParameterCollectionBuilder<ExGrid<TestRow>>>? more = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, rows ?? TestRows.Window())
              .Add(g => g.Columns, columns ?? TestRows.Columns())
              .Add(g => g.CellChangedAt, changedAt)
              .Add(g => g.Clock, clock ?? Clock);
            more?.Invoke(ps);
        });

    /// <summary>The marked cells, named by their row's book and their column, in order.</summary>
    private static List<(string Book, string Column)> MarkedCells(IRenderedComponent<ExGrid<TestRow>> cut)
        => [.. cut.FindComponents<ExGridRow<TestRow>>()
            .SelectMany(row => row.FindAll(".ex-changed").Select(cell => (
                Book: row.Instance.Row.Book,
                Column: row.Instance.Columns[int.Parse(cell.GetAttribute("aria-colindex")!, CultureInfo.InvariantCulture) - 1].Name)))
            .OrderBy(cell => cell.Book, StringComparer.Ordinal)
            .ThenBy(cell => cell.Column, StringComparer.Ordinal)];

    /// <summary>How many times each row has rendered, by book.</summary>
    private static Dictionary<string, int> RenderCounts(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.FindComponents<ExGridRow<TestRow>>().ToDictionary(row => row.Instance.Row.Book, row => row.RenderCount);

    [Fact] // ADR-0068 / DC-64: exactly the value cells whose change is less than the duration ago wear ex-changed
    public void Exactly_the_cells_whose_change_is_within_the_duration_are_marked()
    {
        var now = Clock.GetUtcNow();
        var times = new ChangeTimes
        {
            [("Alpha", "Book")] = now,
            [("Beta", "Amount")] = now - Ms(999),
            // Its end is now: the end itself is already unmarked.
            [("Gamma", "Active")] = now - Second,
            [("Gamma", "Amount")] = now - TimeSpan.FromMinutes(5),
        };

        var cut = RenderGrid(times.Lookup());

        Assert.Equal([("Alpha", "Book"), ("Beta", "Amount")], MarkedCells(cut));
        // The mark joins the presentation classes; it replaces none of them.
        var amount = Assert.Single(cut.FindAll(".ex-changed.ex-cell-numeric"));
        Assert.Equal("-7", amount.TextContent);
    }

    [Fact] // ADR-0068 / DC-64: the grid takes the mark away itself once the duration has passed, in one step
    public void The_mark_goes_when_the_duration_has_passed()
    {
        var now = Clock.GetUtcNow();
        var cut = RenderGrid(new ChangeTimes { [("Beta", "Amount")] = now }.Lookup());

        Clock.Advance(Second - Ms(1));
        Assert.Equal([("Beta", "Amount")], MarkedCells(cut));

        Clock.Advance(Ms(1));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".ex-changed")));
    }

    [Fact] // ADR-0068 / DC-64: asked of value cells only — an Action, Template or Mark cell paints no value that could change
    public void Only_value_cells_are_asked_and_marked()
    {
        var now = Clock.GetUtcNow();
        var asked = new HashSet<string>(StringComparer.Ordinal);
        CellChangeOf<TestRow> everything = (_, column) =>
        {
            asked.Add(column.Name);
            return now;
        };
        RenderFragment<TemplateCellContext<TestRow>> bar =
            cell => builder => builder.AddMarkupContent(0, $"<span class='bar'>{cell.Row.Book}</span>");
        GridColumn<TestRow>[] columns =
        [
            GridColumn<TestRow>.MarkColumn("Mark"),
            new("Book", ColumnType.Text, r => r.Book),
            GridColumn<TestRow>.ActionColumn("Actions", [new GridAction("open", "Open")]),
            GridColumn<TestRow>.TemplateColumn("Bar", ColumnType.Text, r => r.Book, bar),
            new("Amount", ColumnType.Number, r => r.Amount),
        ];

        var cut = RenderGrid(everything, columns: columns, more: ps => ps
            .Add(g => g.Marks, new NoMarks())
            .Add(g => g.ViewportWidth, 1000));

        Assert.Equal(["Amount", "Book"], asked.Order(StringComparer.Ordinal));
        Assert.Equal(6, cut.FindAll(".ex-changed").Count);
        // Every control-bearing cell is painted, and none of them is marked.
        Assert.Equal(3, cut.FindAll(".ex-row .ex-mark").Count);
        Assert.Equal(3, cut.FindAll(".ex-action").Count);
        Assert.Equal(3, cut.FindAll(".bar").Count);
        Assert.Empty(cut.FindAll(".ex-changed .ex-mark, .ex-changed .ex-action, .ex-changed .bar"));
    }

    [Fact] // ADR-0068 / DC-64: a Pinned Column's value cell is a value cell like any other
    public void A_pinned_value_cell_carries_the_mark()
    {
        var now = Clock.GetUtcNow();
        var cut = RenderGrid(new ChangeTimes { [("Beta", "Book")] = now }.Lookup(),
            more: ps => ps.Add(g => g.PinnedColumnCount, 1));

        var cell = Assert.Single(cut.FindAll(".ex-changed"));
        Assert.Contains("ex-pinned", cell.ClassName, StringComparison.Ordinal);
        Assert.Equal("Beta", cell.TextContent);
    }

    [Fact] // ADR-0068 / DC-64: when a mark ends, only the rows whose marks ended render — no other row does
    public void When_a_mark_ends_only_the_rows_whose_marks_ended_render()
    {
        var now = Clock.GetUtcNow();
        var times = new ChangeTimes
        {
            [("Alpha", "Book")] = now - Ms(500),   // ends at +500 ms
            [("Beta", "AsOf")] = now - Ms(200),    // ends at +800 ms, Beta's earliest
            [("Beta", "Amount")] = now,            // ends at +1 s
        };
        var cut = RenderGrid(times.Lookup());
        Assert.Equal(new Dictionary<string, int> { ["Alpha"] = 1, ["Beta"] = 1, ["Gamma"] = 1 }, RenderCounts(cut));

        Clock.Advance(Ms(500));
        cut.WaitForAssertion(() => Assert.Equal([("Beta", "Amount"), ("Beta", "AsOf")], MarkedCells(cut)));
        Assert.Equal(new Dictionary<string, int> { ["Alpha"] = 2, ["Beta"] = 1, ["Gamma"] = 1 }, RenderCounts(cut));

        // Beta's earlier mark ends; its row renders once and keeps the later one.
        Clock.Advance(Ms(300));
        cut.WaitForAssertion(() => Assert.Equal([("Beta", "Amount")], MarkedCells(cut)));
        Assert.Equal(new Dictionary<string, int> { ["Alpha"] = 2, ["Beta"] = 2, ["Gamma"] = 1 }, RenderCounts(cut));

        Clock.Advance(Ms(200));
        cut.WaitForAssertion(() => Assert.Empty(MarkedCells(cut)));
        Assert.Equal(new Dictionary<string, int> { ["Alpha"] = 2, ["Beta"] = 3, ["Gamma"] = 1 }, RenderCounts(cut));
    }

    [Fact] // ADR-0068 / ADR-0003: the time handed down with every render renders no row whose mark still stands, or that has none
    public void Time_passing_renders_no_row_whose_mark_has_not_ended()
    {
        var now = Clock.GetUtcNow();
        var rows = TestRows.Window();
        var columns = TestRows.Columns();
        var lookup = new ChangeTimes { [("Beta", "Amount")] = now }.Lookup();
        var cut = RenderGrid(lookup, rows: rows, columns: columns);

        Clock.Advance(Ms(500));
        cut.Render(ps => ps.Add(g => g.Window, rows).Add(g => g.Columns, columns).Add(g => g.CellChangedAt, lookup));
        Clock.Advance(Ms(499));
        cut.Render(ps => ps.Add(g => g.Window, rows).Add(g => g.Columns, columns).Add(g => g.CellChangedAt, lookup));

        Assert.Equal([("Beta", "Amount")], MarkedCells(cut));
        Assert.All(cut.FindComponents<ExGridRow<TestRow>>(), row => Assert.Equal(1, row.RenderCount));
    }

    [Fact] // ADR-0068 / ADR-0006: a new delegate is the change signal — the painted rows ask again
    public void A_new_delegate_makes_the_painted_rows_ask_again()
    {
        var now = Clock.GetUtcNow();
        var cut = RenderGrid(new ChangeTimes { [("Beta", "Amount")] = now }.Lookup());

        cut.Render(ps => ps.Add(g => g.CellChangedAt, new ChangeTimes { [("Gamma", "Amount")] = now }.Lookup()));

        Assert.Equal([("Gamma", "Amount")], MarkedCells(cut));
    }

    [Fact] // ADR-0068 / ADR-0006: rewriting what an unchanged delegate answers changes no mark and renders no row
    public void Rewriting_what_an_unchanged_delegate_answers_changes_no_mark()
    {
        var now = Clock.GetUtcNow();
        var times = new ChangeTimes { [("Beta", "Amount")] = now };
        // One delegate for the whole test: the discipline the ADR asks for is to REPLACE it
        // when the times behind it change.
        var lookup = times.Lookup();
        var cut = RenderGrid(lookup);

        times.Clear();
        times[("Gamma", "Amount")] = now;
        cut.Render(ps => ps.Add(g => g.CellChangedAt, lookup));

        Assert.Equal([("Beta", "Amount")], MarkedCells(cut));
        Assert.All(cut.FindComponents<ExGridRow<TestRow>>(), row => Assert.Equal(1, row.RenderCount));
    }

    [Fact] // ADR-0068 / ADR-0003: a row repainted for a reason of its own — a new instance — asks again under an unchanged delegate
    public void A_replaced_row_asks_again_under_an_unchanged_delegate()
    {
        var now = Clock.GetUtcNow();
        var rows = TestRows.Window();
        var times = new ChangeTimes();
        var lookup = times.Lookup();
        var cut = RenderGrid(lookup, rows: rows);
        Assert.Empty(MarkedCells(cut));

        // The live screen's case: a notice says Beta's amount moved, and the Consumer hands over
        // a new Beta with the time it heard it at.
        var beta = new TestRow { Book = "Beta", Amount = -8m, AsOf = rows[1].AsOf, Active = rows[1].Active };
        times[("Beta", "Amount")] = now;
        cut.Render(ps => ps.Add(g => g.Window, [rows[0], beta, rows[2]]).Add(g => g.CellChangedAt, lookup));

        Assert.Equal([("Beta", "Amount")], MarkedCells(cut));
        Assert.Equal("-8", Assert.Single(cut.FindAll(".ex-changed")).TextContent);
        Assert.All(cut.FindComponents<ExGridRow<TestRow>>(), row => Assert.Equal(1, row.RenderCount));
    }

    [Fact] // ADR-0068 / DC-64 / DC-1: without the declaration nothing is asked, no clock is read, no class is painted and no timer exists
    public void Without_the_declaration_nothing_is_read_painted_or_timed()
    {
        var clock = new CountingTimeProvider(Clock);
        var rows = TestRows.Window();
        var columns = TestRows.Columns();
        var cut = RenderGrid(null, clock, rows, columns);

        Clock.Advance(Second);
        cut.Render(ps => ps.Add(g => g.Window, rows).Add(g => g.Columns, columns));

        Assert.Empty(cut.FindAll(".ex-changed"));
        Assert.Empty(clock.Timers);
        Assert.Equal(0, clock.Reads);
        Assert.All(cut.FindComponents<ExGridRow<TestRow>>(), row => Assert.Equal(1, row.RenderCount));
    }

    [Fact] // ADR-0068 / DC-1: taking the declaration away takes every mark away, asks nothing more and lets the timer go
    public void Taking_the_declaration_away_asks_nothing_more_and_keeps_no_timer()
    {
        var now = Clock.GetUtcNow();
        var clock = new CountingTimeProvider(Clock);
        var times = new ChangeTimes { [("Beta", "Amount")] = now };
        var cut = RenderGrid(times.Lookup(), clock);
        var timer = Assert.Single(clock.Timers);
        Assert.Equal(0, timer.Disposals);

        cut.Render(ps => ps.Add(g => g.CellChangedAt, (CellChangeOf<TestRow>?)null));
        var asked = times.Asked;
        // New row instances, so every row renders again: none of them asks.
        cut.Render(ps => ps.Add(g => g.Window, TestRows.Window()));

        Assert.Empty(cut.FindAll(".ex-changed"));
        Assert.Equal(asked, times.Asked);
        Assert.Equal(1, timer.Disposals);
        Assert.Single(clock.Timers);
    }

    [Fact] // ADR-0068 / DC-64: one timer at a time, re-armed for each next end, and none once no mark is painted
    public void One_timer_is_rearmed_for_each_next_end_and_goes_with_the_last_mark()
    {
        var now = Clock.GetUtcNow();
        var clock = new CountingTimeProvider(Clock);
        var times = new ChangeTimes
        {
            [("Alpha", "Book")] = now - Ms(700),   // ends at +300 ms
            [("Beta", "Amount")] = now - Ms(400),  // ends at +600 ms
            [("Gamma", "Active")] = now,           // ends at +1 s
        };
        var cut = RenderGrid(times.Lookup(), clock);

        var timer = Assert.Single(clock.Timers);
        Assert.Equal("OnHighlightsEnded", timer.Name);

        Clock.Advance(Ms(300));
        cut.WaitForAssertion(() => Assert.Equal([("Beta", "Amount"), ("Gamma", "Active")], MarkedCells(cut)));
        Assert.Same(timer, Assert.Single(clock.Timers));
        Assert.Equal(0, timer.Disposals);

        Clock.Advance(Ms(300));
        cut.WaitForAssertion(() => Assert.Equal([("Gamma", "Active")], MarkedCells(cut)));
        Assert.Same(timer, Assert.Single(clock.Timers));
        Assert.Equal(0, timer.Disposals);

        Clock.Advance(Ms(400));
        cut.WaitForAssertion(() => Assert.Empty(MarkedCells(cut)));
        // No mark is painted, so no timer exists.
        Assert.Same(timer, Assert.Single(clock.Timers));
        Assert.Equal(1, timer.Disposals);

        // A new mark arms a new timer; there is never more than one standing.
        cut.Render(ps => ps.Add(g => g.CellChangedAt, new ChangeTimes { [("Alpha", "Amount")] = Clock.GetUtcNow() }.Lookup()));
        Assert.Equal(2, clock.Timers.Count);
        Assert.Single(clock.Timers, t => t.Disposals == 0);
    }

    [Fact] // ADR-0068 / MEM-3: the timer is disposed with the grid, once
    public async Task The_timer_is_disposed_with_the_grid()
    {
        var now = Clock.GetUtcNow();
        var clock = new CountingTimeProvider(Clock);
        RenderGrid(new ChangeTimes { [("Beta", "Amount")] = now }.Lookup(), clock);

        await DisposeComponentsAsync();
        Clock.Advance(Second);

        Assert.Equal(1, Assert.Single(clock.Timers).Disposals);
    }

    [Fact] // ADR-0068 / DC-64: ChangeHighlightDuration is the Consumer's to set
    public void The_duration_is_the_consumers()
    {
        var now = Clock.GetUtcNow();
        var cut = RenderGrid(new ChangeTimes { [("Beta", "Amount")] = now }.Lookup(),
            more: ps => ps.Add(g => g.ChangeHighlightDuration, TimeSpan.FromSeconds(3)));

        Clock.Advance(TimeSpan.FromSeconds(3) - Ms(1));
        Assert.Equal([("Beta", "Amount")], MarkedCells(cut));

        Clock.Advance(Ms(1));
        cut.WaitForAssertion(() => Assert.Empty(MarkedCells(cut)));
    }

    [Fact] // ADR-0068: a new duration is a change of what the painted rows show, so they ask again
    public void A_new_duration_repaints_the_marks()
    {
        var now = Clock.GetUtcNow();
        var lookup = new ChangeTimes { [("Beta", "Amount")] = now }.Lookup();
        var cut = RenderGrid(lookup, more: ps => ps.Add(g => g.ChangeHighlightDuration, TimeSpan.FromSeconds(3)));
        Clock.Advance(TimeSpan.FromSeconds(2));

        cut.Render(ps => ps.Add(g => g.CellChangedAt, lookup).Add(g => g.ChangeHighlightDuration, Second));

        Assert.Empty(MarkedCells(cut));
    }

    [Fact] // ADR-0068: a change time still to come — a server's clock ahead of the grid's — is marked until it and the duration have passed
    public void A_change_time_in_the_future_is_marked_until_it_and_the_duration_have_passed()
    {
        var now = Clock.GetUtcNow();
        var cut = RenderGrid(new ChangeTimes { [("Beta", "Amount")] = now + TimeSpan.FromSeconds(2) }.Lookup());
        Assert.Equal([("Beta", "Amount")], MarkedCells(cut));

        Clock.Advance(TimeSpan.FromSeconds(3) - Ms(1));
        Assert.Equal([("Beta", "Amount")], MarkedCells(cut));

        Clock.Advance(Ms(1));
        cut.WaitForAssertion(() => Assert.Empty(MarkedCells(cut)));
    }

    [Fact] // ADR-0068: a change time far ahead is waited for in steps a timer accepts, and a step that ends no mark renders no row
    public void A_far_future_change_is_waited_for_in_steps_that_render_nothing()
    {
        var now = Clock.GetUtcNow();
        var clock = new CountingTimeProvider(Clock);
        var cut = RenderGrid(new ChangeTimes { [("Beta", "Amount")] = now + TimeSpan.FromDays(10) }.Lookup(), clock);

        Clock.Advance(TimeSpan.FromDays(1));

        Assert.Equal([("Beta", "Amount")], MarkedCells(cut));
        Assert.All(cut.FindComponents<ExGridRow<TestRow>>(), row => Assert.Equal(1, row.RenderCount));
        Assert.Equal(0, Assert.Single(clock.Timers).Disposals);

        Clock.Advance(TimeSpan.FromDays(9) + Second);
        cut.WaitForAssertion(() => Assert.Empty(MarkedCells(cut)));
    }

    [Fact] // ADR-0068: a mark cannot last a negative time — refused by name
    public void A_negative_duration_is_refused()
    {
        var refusal = Assert.Throws<ArgumentOutOfRangeException>(() => RenderGrid(
            new ChangeTimes().Lookup(), more: ps => ps.Add(g => g.ChangeHighlightDuration, TimeSpan.FromSeconds(-1))));

        Assert.Equal("ChangeHighlightDuration", refusal.ParamName);
    }

    [Fact] // ADR-0068: the grid reads the time from the Clock a test hands in, and its timer runs on it
    public void The_marks_follow_the_clock_handed_in()
    {
        var own = new FakeTimeProvider(Clock.GetUtcNow());
        var cut = RenderGrid(new ChangeTimes { [("Beta", "Amount")] = own.GetUtcNow() }.Lookup(), own);

        // The grid's own clock moving ends nothing.
        Clock.Advance(Second);
        Assert.Equal([("Beta", "Amount")], MarkedCells(cut));

        own.Advance(Second);
        cut.WaitForAssertion(() => Assert.Empty(MarkedCells(cut)));
    }

    [Fact] // ADR-0068: with no Clock handed in, the marks run on the grid's own clock — the TimeProvider the host registered
    public void Without_a_clock_the_marks_run_on_the_grids_own()
    {
        var now = Clock.GetUtcNow();
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, TestRows.Columns())
            .Add(g => g.CellChangedAt, new ChangeTimes { [("Beta", "Amount")] = now }.Lookup()));
        Assert.Equal([("Beta", "Amount")], MarkedCells(cut));

        Clock.Advance(Second);

        cut.WaitForAssertion(() => Assert.Empty(MarkedCells(cut)));
    }

    [Fact] // ADR-0068 / DC-65: a mark is keyed by row and column — across scrolls it stays on its cell, leaves with its row and comes back with it
    public async Task A_mark_stays_on_its_row_across_scrolls()
    {
        const double RowHeightPx = 20;
        var now = Clock.GetUtcNow();
        var clock = new CountingTimeProvider(Clock);
        var rows = TestRows.Many(200);
        var cut = RenderGrid(new ChangeTimes { [("Row 000003", "Amount")] = now }.Lookup(), clock, rows,
            more: ps => ps
                .Add(g => g.TotalCount, rows.Length)
                .Add(g => g.RowHeight, RowHeightPx)
                .Add(g => g.ViewportHeight, 120));
        var scroller = cut.Find(".ex-scroller");
        Assert.Equal([("Row 000003", "Amount")], MarkedCells(cut));

        // Two rows down: every painted row now stands where another stood, and the mark stays
        // with its own.
        await ScrollToAsync(scroller, 2 * RowHeightPx);
        Assert.Equal([("Row 000003", "Amount")], MarkedCells(cut));
        Assert.Equal("3", Assert.Single(cut.FindAll(".ex-changed")).TextContent);

        // Out of view: no row paints it, and with no mark painted there is no timer.
        await ScrollToAsync(scroller, 7 * RowHeightPx);
        await ScrollToAsync(scroller, 12 * RowHeightPx);
        Assert.Empty(cut.FindAll(".ex-changed"));
        Assert.All(clock.Timers, timer => Assert.Equal(1, timer.Disposals));

        // Back in view before its end: the row asks again, and the mark is on its cell.
        Clock.Advance(Ms(500));
        await ScrollToAsync(scroller, 7 * RowHeightPx);
        await ScrollToAsync(scroller, 2 * RowHeightPx);
        Assert.Equal([("Row 000003", "Amount")], MarkedCells(cut));

        Clock.Advance(Ms(500));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".ex-changed")));
    }

    [Fact] // ADR-0068 / ADR-0033 / DC-66: a mark is seen, not announced — the live region says nothing as marks come and go
    public void No_live_region_announces_a_mark()
    {
        var now = Clock.GetUtcNow();
        var cut = RenderGrid(new ChangeTimes { [("Beta", "Amount")] = now }.Lookup());
        Assert.Equal("", cut.Find(".ex-announce").TextContent);

        cut.Render(ps => ps.Add(g => g.CellChangedAt, new ChangeTimes { [("Gamma", "Amount")] = now }.Lookup()));
        Assert.Equal("", cut.Find(".ex-announce").TextContent);

        Clock.Advance(Second);
        cut.WaitForAssertion(() => Assert.Empty(MarkedCells(cut)));
        Assert.Equal("", cut.Find(".ex-announce").TextContent);
        // The grid's one live region is the only one there is: a mark brings no other.
        Assert.Single(cut.FindAll("[role=status], [aria-live]"));
    }

    /// <summary>Row Marks nobody holds: enough for a Mark Column to be painted.</summary>
    private sealed class NoMarks : IRowMarks<TestRow>
    {
        public RowMarkCounts? Counts => null;

        public event Action? Changed
        {
            add { }
            remove { }
        }

        public bool IsMarked(TestRow row) => false;

        public Task OnMarkIntentAsync(RowMarkIntent<TestRow> intent) => Task.CompletedTask;

        public void OnRowKindChanged(Func<TestRow, RowKind>? rowKind)
        {
        }
    }
}
