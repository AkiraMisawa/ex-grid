using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Rows;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The Mark Column as it reaches the DOM (ADR-0043): a checkbox per Detail row and one in
/// the header, each gesture reported once, the header painted from the Consumer's counts,
/// and a changed mark repainting only its own row.
/// </summary>
public class RowMarkColumnTests : GridTestContext
{
    private const double RowHeightPx = 20;
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    // Mark 0–100, Book 100–200, Amount 200–300.
    private static readonly GridColumn<TestRow>[] Columns =
    [
        GridColumn<TestRow>.MarkColumn("Mark", width: Fixed100),
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
    ];

    /// <summary>A Consumer's marks, recorded: what it answers is set by the test, and
    /// every intent the grid reports is kept.</summary>
    private sealed class RecordingMarks : IRowMarks<TestRow>
    {
        public HashSet<TestRow> Marked { get; } = new(ReferenceEqualityComparer.Instance);
        public List<RowMarkIntent<TestRow>> Intents { get; } = [];
        public RowMarkCounts? Counts { get; set; }
        public Func<TestRow, RowKind>? RowKind { get; private set; }

        public event Action? Changed;

        public bool IsMarked(TestRow row) => Marked.Contains(row);

        public Task OnMarkIntentAsync(RowMarkIntent<TestRow> intent)
        {
            Intents.Add(intent);
            return Task.CompletedTask;
        }

        public void OnRowKindChanged(Func<TestRow, RowKind>? rowKind) => RowKind = rowKind;

        public void Raise() => Changed?.Invoke();
    }

    private IRenderedComponent<ExGrid<TestRow>> RenderPush(
        RecordingMarks marks,
        TestRow[]? window = null,
        int? total = null,
        Action<ComponentParameterCollectionBuilder<ExGrid<TestRow>>>? more = null)
    {
        window ??= TestRows.Many(50);
        return Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, window)
              .Add(g => g.TotalCount, total ?? window.Length)
              .Add(g => g.Columns, Columns)
              .Add(g => g.Marks, marks)
              .Add(g => g.RowHeight, RowHeightPx)
              .Add(g => g.ViewportHeight, 120)
              .Add(g => g.ViewportWidth, 600);
            more?.Invoke(ps);
        });
    }

    private static Task PressAsync(
        IRenderedComponent<ExGrid<TestRow>> cut, string key, bool ctrl = false, bool shift = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, ctrl, shift, alt: false, meta: false, metaIsPrimary: false));

    private static Task ClickCellAsync(IRenderedComponent<ExGrid<TestRow>> cut, int row, int column)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs
        {
            Button = 0, Buttons = 1, OffsetX = (column * 100) + 50, OffsetY = (row * RowHeightPx) + 10,
        });

    private static IReadOnlyList<AngleSharp.Dom.IElement> RowBoxes(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.FindAll(".ex-row .ex-mark");

    private static AngleSharp.Dom.IElement HeaderBox(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.Find(".ex-header .ex-mark");

    [Fact] // ADR-0043: one checkbox per painted Detail row, reporting the Consumer's answer
    public void Each_painted_row_shows_whether_it_is_marked()
    {
        var window = TestRows.Many(50);
        var marks = new RecordingMarks { Counts = new RowMarkCounts(1, 50, 0) };
        marks.Marked.Add(window[1]);

        var cut = RenderPush(marks, window);

        var boxes = RowBoxes(cut);
        Assert.Equal(cut.FindAll(".ex-row").Count, boxes.Count);
        Assert.Equal("checkbox", boxes[0].GetAttribute("role"));
        Assert.Equal("false", boxes[0].GetAttribute("aria-checked"));
        Assert.Equal("true", boxes[1].GetAttribute("aria-checked"));
    }

    [Fact] // ADR-0043/0024: Group and Total rows carry no checkbox, and the marks are told the roles
    public void Group_and_total_rows_carry_no_checkbox()
    {
        var window = TestRows.Many(50);
        Func<TestRow, RowKind> kinds = r => r.Amount == 1 ? RowKind.Group : r.Amount == 2 ? RowKind.Total : RowKind.Detail;
        var marks = new RecordingMarks { Counts = new RowMarkCounts(0, 48, 0) };

        var cut = RenderPush(marks, window, more: ps => ps.Add(g => g.RowKind, kinds));

        Assert.Empty(cut.FindAll(".ex-row-group .ex-mark"));
        Assert.Empty(cut.FindAll(".ex-row-total .ex-mark"));
        Assert.NotEmpty(RowBoxes(cut));
        Assert.Same(kinds, marks.RowKind);
    }

    [Fact] // ADR-0043: one checkbox is one intent, naming the row by identity — and it does not move the selection
    public async Task A_checkbox_press_reports_one_intent_and_leaves_the_selection_alone()
    {
        var window = TestRows.Many(50);
        var marks = new RecordingMarks { Counts = new RowMarkCounts(0, 50, 0) };
        GridSelection? selection = null;
        var cut = RenderPush(marks, window, more: ps => ps.Add(g => g.SelectionChanged, s => selection = s));

        await RowBoxes(cut)[2].ClickAsync(new MouseEventArgs { Button = 0 });

        var intent = Assert.IsType<RowMarkIntent<TestRow>.OneRow>(Assert.Single(marks.Intents));
        Assert.Same(window[2], intent.Row);
        Assert.True(intent.Marked);
        Assert.Null(selection);
    }

    [Fact] // ADR-0043 (MK-2): a fully marked Window inside a larger, partly marked result is "some"
    public void The_header_is_painted_from_the_counts_not_from_the_window()
    {
        var window = TestRows.Many(50);
        var marks = new RecordingMarks { Counts = new RowMarkCounts(50, 1_000, 0) };
        foreach (var row in window)
            marks.Marked.Add(row);

        var cut = RenderPush(marks, window, total: 1_000);

        Assert.Equal("mixed", HeaderBox(cut).GetAttribute("aria-checked"));
    }

    [Fact] // ADR-0043: the header reads "all" and "none" from the counts too
    public void The_header_shows_all_and_none_from_the_counts()
    {
        var marks = new RecordingMarks { Counts = new RowMarkCounts(50, 50, 0) };
        var cut = RenderPush(marks);
        Assert.Equal("true", HeaderBox(cut).GetAttribute("aria-checked"));

        marks.Counts = new RowMarkCounts(0, 50, 3);
        cut.InvokeAsync(marks.Raise);

        cut.WaitForAssertion(() => Assert.Equal("false", HeaderBox(cut).GetAttribute("aria-checked")));
    }

    [Fact] // ADR-0043: pressing "some" marks all under the current order; pressing "all" unmarks all
    public async Task The_header_press_marks_all_unless_all_are_marked()
    {
        var marks = new RecordingMarks { Counts = new RowMarkCounts(3, 50, 0) };
        var cut = RenderPush(marks, more: ps => ps.Add(g => g.RowSequenceVersion, 7));

        await HeaderBox(cut).ClickAsync(new MouseEventArgs { Button = 0 });
        marks.Counts = new RowMarkCounts(50, 50, 0);
        await cut.InvokeAsync(marks.Raise);
        await HeaderBox(cut).ClickAsync(new MouseEventArgs { Button = 0 });

        Assert.Equal(
            [new RowMarkIntent<TestRow>.AllRows(true, 7), new RowMarkIntent<TestRow>.AllRows(false, 7)],
            marks.Intents);
    }

    [Fact] // ADR-0043/0012: pressing the header's checkbox does not sort
    public async Task The_header_press_does_not_sort()
    {
        var marks = new RecordingMarks { Counts = new RowMarkCounts(0, 50, 0) };
        var sorted = 0;
        var cut = RenderPush(marks, more: ps => ps.Add(g => g.OnSortChanged, (IReadOnlyList<SortSpec> _) => sorted++));

        await HeaderBox(cut).ClickAsync(new MouseEventArgs { Button = 0 });

        Assert.Equal(0, sorted);
    }

    [Fact] // ADR-0043 (MK-1/MK-3): Space in the Mark Column reports every selected row once, by position and order
    public async Task Space_in_the_mark_column_reports_the_selected_rows_once()
    {
        var marks = new RecordingMarks { Counts = new RowMarkCounts(0, 50, 0) };
        var cut = RenderPush(marks, more: ps => ps.Add(g => g.RowSequenceVersion, 4));
        await ClickCellAsync(cut, 1, 0);
        await PressAsync(cut, "ArrowDown", shift: true);
        await PressAsync(cut, "ArrowDown", shift: true);

        await PressAsync(cut, " ");

        var intent = Assert.IsType<RowMarkIntent<TestRow>.Positions>(Assert.Single(marks.Intents));
        Assert.Equal([new RowRange(1, 3)], intent.Ranges);
        Assert.Equal(4, intent.RowSequenceVersion);
    }

    [Fact] // ADR-0043 (MK-3): Ctrl+A then Space over a million rows is one intent, not a million
    public async Task Space_over_a_million_rows_is_one_intent()
    {
        var marks = new RecordingMarks { Counts = new RowMarkCounts(0, 1_000_000, 0) };
        var cut = RenderPush(marks, TestRows.Many(50), total: 1_000_000);
        await ClickCellAsync(cut, 0, 0);
        await PressAsync(cut, "a", ctrl: true);

        await PressAsync(cut, " ");

        var intent = Assert.IsType<RowMarkIntent<TestRow>.Positions>(Assert.Single(marks.Intents));
        Assert.Equal([new RowRange(0, 1_000_000)], intent.Ranges);
    }

    [Fact] // ADR-0043: overlapping ranges in the Mark Column name each row once
    public async Task Overlapping_ranges_name_each_row_once()
    {
        var marks = new RecordingMarks { Counts = new RowMarkCounts(0, 50, 0) };
        var cut = RenderPush(marks);
        await ClickCellAsync(cut, 0, 0);
        await PressAsync(cut, "ArrowDown", shift: true);
        await PressAsync(cut, "ArrowDown", shift: true);
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs
        {
            Button = 0, Buttons = 1, CtrlKey = true, OffsetX = 50, OffsetY = (3 * RowHeightPx) + 10,
        });

        await PressAsync(cut, " ");

        var intent = Assert.IsType<RowMarkIntent<TestRow>.Positions>(Assert.Single(marks.Intents));
        Assert.Equal([new RowRange(0, 4)], intent.Ranges);
    }

    [Fact] // ADR-0043/0020: with the Focus on a data cell, Space keeps its own meaning and marks nothing
    public async Task Space_on_a_data_cell_marks_nothing()
    {
        var marks = new RecordingMarks { Counts = new RowMarkCounts(0, 50, 0) };
        var cut = RenderPush(marks);
        await ClickCellAsync(cut, 0, 0);
        await PressAsync(cut, "ArrowRight", shift: true); // the selection reaches into Book; the Focus stays
        await ClickCellAsync(cut, 0, 1);

        await PressAsync(cut, " ");

        Assert.Empty(marks.Intents);
    }

    [Fact] // ADR-0043: two Mark Columns are refused by name — "all" would not say which
    public void Two_mark_columns_are_refused()
    {
        var marks = new RecordingMarks { Counts = new RowMarkCounts(0, 3, 0) };
        GridColumn<TestRow>[] twice = [.. Columns, GridColumn<TestRow>.MarkColumn("Again")];

        var ex = Assert.Throws<InvalidOperationException>(() => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, twice)
            .Add(g => g.Marks, marks)));

        Assert.Contains("Mark", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Again", ex.Message, StringComparison.Ordinal);
    }

    [Fact] // ADR-0043: a Mark Column with nobody holding the marks is refused by name
    public void A_mark_column_without_marks_is_refused()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, Columns)));

        Assert.Contains(nameof(ExGrid<TestRow>.Marks), ex.Message, StringComparison.Ordinal);
    }

    [Fact] // ADR-0043: a Mark Column over GridSource.Fetch without a mark adapter is refused by name
    public void A_mark_column_over_a_fetching_source_without_an_adapter_is_refused()
    {
        using var source = GridSource.Fetch<TestRow>((_, _) => ValueTask.FromResult(GridPage<TestRow>.Empty));

        var ex = Assert.Throws<InvalidOperationException>(() => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns)));

        Assert.Contains("mark adapter", ex.Message, StringComparison.Ordinal);
    }

    [Fact] // ADR-0043: marks from both the parameter and the Source are refused — one would silently win
    public void Marks_from_both_the_parameter_and_the_source_are_refused()
    {
        var source = GridSource.From(TestRows.Window());

        var ex = Assert.Throws<InvalidOperationException>(() => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns)
            .Add(g => g.Marks, new RecordingMarks())));

        Assert.Contains(nameof(ExGrid<TestRow>.Marks), ex.Message, StringComparison.Ordinal);
    }

    [Fact] // ADR-0043 (MK-7): the count display says how many marks lie outside the current filter
    public void The_count_display_names_the_marks_outside_the_filter()
    {
        var marks = new RecordingMarks { Counts = new RowMarkCounts(2, 50, 70) };

        var cut = RenderPush(marks);

        Assert.Contains("72 marked (70 outside the current filter)", cut.Find(".ex-status").TextContent, StringComparison.Ordinal);
    }

    [Fact] // ADR-0043: with every mark inside the result, the count names no outside clause
    public void The_count_display_is_plain_when_nothing_is_outside()
    {
        var marks = new RecordingMarks { Counts = new RowMarkCounts(5, 50, 0) };

        var cut = RenderPush(marks);

        var status = cut.Find(".ex-status").TextContent;
        Assert.Contains("5 marked", status, StringComparison.Ordinal);
        Assert.DoesNotContain("outside", status, StringComparison.Ordinal);
    }

    [Fact] // ADR-0043/0015: under a pager the header lines up the page, and "Mark all N rows" is offered
    public async Task Under_a_pager_the_header_marks_the_page_and_all_is_offered()
    {
        var window = TestRows.Many(10);
        var marks = new RecordingMarks { Counts = new RowMarkCounts(0, 95, 0) };
        var cut = RenderPush(marks, window, total: 95, more: ps => ps
            .Add(g => g.PageSize, 10)
            .Add(g => g.RowSequenceVersion, 2));

        await HeaderBox(cut).ClickAsync(new MouseEventArgs { Button = 0 });
        var offer = cut.FindAll(".ex-status button").Single(b => b.TextContent == "Mark all 95 rows");
        await offer.ClickAsync(new MouseEventArgs { Button = 0 });

        Assert.Equal(2, marks.Intents.Count);
        var page = Assert.IsType<RowMarkIntent<TestRow>.Positions>(marks.Intents[0]);
        Assert.Equal([new RowRange(0, 10)], page.Ranges);
        Assert.Equal(new RowMarkIntent<TestRow>.AllRows(true, 2), marks.Intents[1]);
    }

    [Fact] // ADR-0043/0015: under a pager the header shows the page, which is wholly in hand
    public void Under_a_pager_the_header_shows_the_page()
    {
        var window = TestRows.Many(10);
        var marks = new RecordingMarks { Counts = new RowMarkCounts(10, 95, 0) };
        foreach (var row in window)
            marks.Marked.Add(row);

        var cut = RenderPush(marks, window, total: 95, more: ps => ps.Add(g => g.PageSize, 10));

        Assert.Equal("true", HeaderBox(cut).GetAttribute("aria-checked"));
    }

    [Fact] // ADR-0043 (MK-4): a changed mark repaints only its own row
    public async Task A_changed_mark_repaints_only_its_row()
    {
        var source = GridSource.From(TestRows.Many(50));
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns)
            .Add(g => g.RowHeight, RowHeightPx)
            .Add(g => g.ViewportHeight, 120)
            .Add(g => g.ViewportWidth, 600));
        var before = cut.FindComponents<ExGridRow<TestRow>>().ToDictionary(r => r.Instance.RowIndex, r => r.RenderCount);

        await RowBoxes(cut)[1].ClickAsync(new MouseEventArgs { Button = 0 });

        cut.WaitForAssertion(() => Assert.Equal("true", RowBoxes(cut)[1].GetAttribute("aria-checked")));
        var after = cut.FindComponents<ExGridRow<TestRow>>().ToDictionary(r => r.Instance.RowIndex, r => r.RenderCount);
        Assert.Equal(before[1] + 1, after[1]);
        Assert.All(after.Where(pair => pair.Key != 1), pair => Assert.Equal(before[pair.Key], pair.Value));
    }

    [Fact] // ADR-0043 (MK-5): a sort drops the selection and keeps the marks on the same rows
    public async Task A_sort_keeps_the_marks_and_drops_the_selection()
    {
        var rows = TestRows.Many(50);
        var source = GridSource.From(rows);
        GridSelection? selection = null;
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns)
            .Add(g => g.RowHeight, RowHeightPx)
            .Add(g => g.ViewportHeight, 120)
            .Add(g => g.ViewportWidth, 600)
            .Add(g => g.SelectionChanged, s => selection = s));
        await RowBoxes(cut)[3].ClickAsync(new MouseEventArgs { Button = 0 });
        await ClickCellAsync(cut, 0, 1);

        // Descending by Amount: row 49 first, so row 3 lands at position 46 — off screen.
        await cut.InvokeAsync(() => source.OnSortChanged([new SortSpec("Amount", SortDirection.Descending)]));

        Assert.True(selection!.IsEmpty);
        Assert.Equal([rows[3]], source.Marks.MarkedRows);
        cut.WaitForAssertion(() => Assert.All(RowBoxes(cut), box => Assert.Equal("false", box.GetAttribute("aria-checked"))));
    }

    [Fact] // ADR-0043 (MK-8): new counts from the Consumer turn the header to "some"
    public async Task New_counts_turn_the_header_to_some()
    {
        var marks = new RecordingMarks { Counts = new RowMarkCounts(50, 50, 0) };
        var cut = RenderPush(marks);
        Assert.Equal("true", HeaderBox(cut).GetAttribute("aria-checked"));

        marks.Counts = new RowMarkCounts(50, 51, 0);
        await cut.InvokeAsync(marks.Raise);

        Assert.Equal("mixed", HeaderBox(cut).GetAttribute("aria-checked"));
    }
}
