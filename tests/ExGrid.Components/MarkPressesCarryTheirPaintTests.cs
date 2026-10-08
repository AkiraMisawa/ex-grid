using System.Globalization;
using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Rows;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// A press on a mark carries the render it was made on, as a press on an action does (ADR-0142, LV-32;
/// ADR-0043; decided 2026-10-08). The grid's listener reads the paint the Viewport named at the
/// mousedown on a row's checkbox, the header's checkbox or "Mark all N rows" — and, for the header's
/// under a pager, the page it named — and tells them at the release, before Blazor dispatches the
/// click. A press made on what a Source since replaced painted is dropped; so is one that names rows
/// by position — the header's page or result, "Mark all N rows" — under an order that has moved since,
/// or on a page turned since. Dropped, it marks nothing and raises no Row Mark intent, silently: there
/// is no refusal channel for marks. A row's checkbox names its row by identity, which an order move
/// leaves it. A press told the current paint marks as before. Here a test tells the press as the
/// listener does, then clicks.
///
/// Mark 0–100, Book 100–200, Amount 200–300; 20px rows in a 120px Viewport.
/// </summary>
public class MarkPressesCarryTheirPaintTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static readonly Func<TestRow, object> ByBook = static row => row.Book;

    private static readonly GridColumn<TestRow>[] Columns =
    [
        GridColumn<TestRow>.MarkColumn("Mark", width: Fixed100),
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
    ];

    /// <summary>A pushed Window, its marks held by <paramref name="marks"/>.</summary>
    private IRenderedComponent<ExGrid<TestRow>> RenderPushed(RecordingMarks marks, TestRow[] window, int? pageSize = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, window)
              .Add(g => g.TotalCount, window.Length)
              .Add(g => g.Columns, Columns)
              .Add(g => g.Marks, marks)
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 120)
              .Add(g => g.ViewportWidth, 600);
            if (pageSize is { } size)
                ps.Add(g => g.PageSize, size);
        });

    /// <summary>A bound Source that keeps its own marks.</summary>
    private IRenderedComponent<ExGrid<TestRow>> RenderBound(MarkingSource source, int? pageSize = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Source, source)
              .Add(g => g.Columns, Columns)
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 120)
              .Add(g => g.ViewportWidth, 600);
            if (pageSize is { } size)
                ps.Add(g => g.PageSize, size);
        });

    /// <summary>The rows in another order, under the next Row Sequence Version (ADR-0011).</summary>
    private static void Reorder(IRenderedComponent<ExGrid<TestRow>> cut, TestRow[] reordered, int version)
        => cut.Render(ps => ps.Add(g => g.Window, reordered).Add(g => g.RowSequenceVersion, version));

    private static TestRow[] Swapped(TestRow[] rows, int a, int b)
    {
        var next = (TestRow[])rows.Clone();
        (next[a], next[b]) = (next[b], next[a]);
        return next;
    }

    /// <summary>Another source's rows: new instances with the same values, and so the same keys.</summary>
    private static TestRow[] Copies(TestRow[] rows)
        => [.. rows.Select(r => new TestRow { Book = r.Book, Amount = r.Amount, AsOf = r.AsOf, Active = r.Active })];

    private static int Paint(IRenderedComponent<ExGrid<TestRow>> cut)
        => int.Parse(cut.Find(".ex-viewport").GetAttribute("data-ex-paint")!, CultureInfo.InvariantCulture);

    /// <summary>The page the header's checkbox names, as the listener reads it at the press, or −1.</summary>
    private static int PageOf(IRenderedComponent<ExGrid<TestRow>> cut)
        => HeaderBox(cut).GetAttribute("data-ex-page") is { } page ? int.Parse(page, CultureInfo.InvariantCulture) : -1;

    /// <summary>What the listener tells at the release on a mark: the paint and the page read at its press.</summary>
    private static Task TellAsync(IRenderedComponent<ExGrid<TestRow>> cut, int paint, int page = -1)
        => cut.InvokeAsync(() => cut.Instance.MarkPressTakenAt(paint, page));

    private static IReadOnlyList<AngleSharp.Dom.IElement> RowBoxes(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.FindAll(".ex-row .ex-mark");

    private static AngleSharp.Dom.IElement HeaderBox(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.Find(".ex-header .ex-mark");

    private static AngleSharp.Dom.IElement MarkAll(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.Find(".ex-status .ex-mark-result");

    private static Task ClickAsync(AngleSharp.Dom.IElement element)
        => element.ClickAsync(new MouseEventArgs { Button = 0 });

    // ---- A row's checkbox: dropped under another binding, never under another order ----

    [Theory] // ADR-0142 / LV-32, ADR-0043: a press on a row's checkbox made on what a Source since replaced painted — both at version 0 — marks nothing in either source's marks; with a Row Key the checkbox survives holding the new source's row, without one another row's stands in its place
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_row_checkbox_pressed_on_the_replaced_sources_paint_marks_nothing(bool rowKey)
    {
        var rows = TestRows.Many(50);
        var old = new MarkingSource(rows, rowKey);
        var replacement = new MarkingSource(Copies(rows), rowKey);
        var cut = RenderBound(old);
        var pressedOn = Paint(cut);

        cut.Render(ps => ps.Add(g => g.Source, replacement));
        await TellAsync(cut, pressedOn);
        await ClickAsync(RowBoxes(cut)[2]);

        Assert.Empty(old.Heard.Intents);
        Assert.Empty(replacement.Heard.Intents);
    }

    [Theory] // ADR-0142 / LV-32, ADR-0043: a press on a row's checkbox told the new Source's paint marks its row, as before
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_row_checkbox_pressed_on_the_new_sources_paint_marks_its_row(bool rowKey)
    {
        var rows = TestRows.Many(50);
        var old = new MarkingSource(rows, rowKey);
        var replacement = new MarkingSource(Copies(rows), rowKey);
        var cut = RenderBound(old);
        cut.Render(ps => ps.Add(g => g.Source, replacement));

        await TellAsync(cut, Paint(cut));
        await ClickAsync(RowBoxes(cut)[2]);

        Assert.Empty(old.Heard.Intents);
        var intent = Assert.IsType<RowMarkIntent<TestRow>.OneRow>(Assert.Single(replacement.Heard.Intents));
        Assert.Same(replacement.Window[2], intent.Row);
    }

    [Fact] // ADR-0142 / LV-32, ADR-0043: a row's checkbox names its row by identity, which an order move leaves it: pressed under an order since moved, it marks the row it was pressed on
    public async Task A_row_checkbox_pressed_under_a_moved_order_marks_its_row()
    {
        var window = TestRows.Many(50);
        var marks = new RecordingMarks();
        var cut = RenderPushed(marks, window);
        var pressedOn = Paint(cut);
        var reordered = Swapped(window, 0, 2);

        Reorder(cut, reordered, version: 1);
        await TellAsync(cut, pressedOn);
        await ClickAsync(RowBoxes(cut)[2]);

        var intent = Assert.IsType<RowMarkIntent<TestRow>.OneRow>(Assert.Single(marks.Intents));
        Assert.Same(reordered[2], intent.Row);
    }

    // ---- The header's checkbox and "Mark all N rows": dropped under another order or binding ----

    [Theory] // ADR-0142 / LV-32, ADR-0043/0015: under a pager the header's checkbox names the page's rows; pressed under an order that has moved since, it marks nothing, and pressed on the current paint it lines up the page as before
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_header_checkbox_under_a_pager_pressed_under_a_moved_order_marks_nothing(bool moved)
    {
        var window = TestRows.Many(30);
        var marks = new RecordingMarks();
        var cut = RenderPushed(marks, window, pageSize: 10);
        var pressedOn = Paint(cut);
        var page = PageOf(cut);
        Assert.Equal(0, page);

        if (moved)
            Reorder(cut, Swapped(window, 0, 1), version: 1);
        await TellAsync(cut, pressedOn, page);
        await ClickAsync(HeaderBox(cut));

        if (moved)
        {
            Assert.Empty(marks.Intents);
            return;
        }
        var intent = Assert.IsType<RowMarkIntent<TestRow>.Positions>(Assert.Single(marks.Intents));
        Assert.Equal([new RowRange(0, 10)], intent.Ranges);
    }

    [Fact] // ADR-0142 / LV-32, ADR-0043/0015: the header's checkbox under a pager, pressed on what a Source since replaced painted — both at version 0 — marks nothing in either source's marks
    public async Task The_header_checkbox_under_a_pager_pressed_on_the_replaced_sources_paint_marks_nothing()
    {
        var rows = TestRows.Many(30);
        var old = new MarkingSource(rows, keyed: false);
        var replacement = new MarkingSource(Copies(rows), keyed: false);
        var cut = RenderBound(old, pageSize: 10);
        var pressedOn = Paint(cut);
        var page = PageOf(cut);

        cut.Render(ps => ps.Add(g => g.Source, replacement));
        await TellAsync(cut, pressedOn, page);
        await ClickAsync(HeaderBox(cut));

        Assert.Empty(old.Heard.Intents);
        Assert.Empty(replacement.Heard.Intents);
    }

    [Fact] // ADR-0142 / LV-32, ADR-0015: the header's checkbox names the page it was pressed on: a press whose page was turned since marks nothing; one told the page shown now lines that page up
    public async Task The_header_checkbox_pressed_on_a_page_turned_since_marks_nothing()
    {
        var window = TestRows.Many(30);
        var marks = new RecordingMarks();
        var cut = RenderPushed(marks, window, pageSize: 10);
        var pressedOn = Paint(cut);
        var page = PageOf(cut);

        await ClickAsync(cut.FindAll(".ex-pager button")[1]);
        Assert.Equal(10, PageOf(cut));
        await TellAsync(cut, pressedOn, page);
        await ClickAsync(HeaderBox(cut));
        Assert.Empty(marks.Intents);

        await TellAsync(cut, Paint(cut), PageOf(cut));
        await ClickAsync(HeaderBox(cut));
        var intent = Assert.IsType<RowMarkIntent<TestRow>.Positions>(Assert.Single(marks.Intents));
        Assert.Equal([new RowRange(10, 10)], intent.Ranges);
    }

    [Theory] // ADR-0142 / LV-32, ADR-0043: without a pager the header's checkbox names the whole result as it stood at the press: pressed under an order that has moved since, it marks nothing; on the current paint it marks all as before
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_header_checkbox_pressed_under_a_moved_order_marks_nothing(bool moved)
    {
        var window = TestRows.Many(50);
        var marks = new RecordingMarks();
        var cut = RenderPushed(marks, window);
        var pressedOn = Paint(cut);
        Assert.Equal(-1, PageOf(cut));

        if (moved)
            Reorder(cut, Swapped(window, 0, 1), version: 1);
        await TellAsync(cut, pressedOn);
        await ClickAsync(HeaderBox(cut));

        Assert.Equal(moved ? [] : [new RowMarkIntent<TestRow>.AllRows(true, 0)], marks.Intents);
    }

    [Theory] // ADR-0142 / LV-32, ADR-0043/0015: "Mark all N rows" names the result as it stood at the press: pressed under an order that has moved since, it marks nothing; on the current paint it marks all as before
    [InlineData(false)]
    [InlineData(true)]
    public async Task Mark_all_rows_pressed_under_a_moved_order_marks_nothing(bool moved)
    {
        var window = TestRows.Many(30);
        var marks = new RecordingMarks();
        var cut = RenderPushed(marks, window, pageSize: 10);
        var pressedOn = Paint(cut);

        if (moved)
            Reorder(cut, Swapped(window, 0, 1), version: 1);
        await TellAsync(cut, pressedOn);
        await ClickAsync(MarkAll(cut));

        Assert.Equal(moved ? [] : [new RowMarkIntent<TestRow>.AllRows(true, 0)], marks.Intents);
    }

    [Fact] // ADR-0142 / LV-32, ADR-0043: a mark press nobody told of — made by script, with no mousedown before it — is taken as aimed at the newest paint, and marks
    public async Task An_untold_mark_press_marks_as_before()
    {
        var window = TestRows.Many(50);
        var marks = new RecordingMarks();
        var cut = RenderPushed(marks, window);
        Reorder(cut, Swapped(window, 0, 1), version: 1);

        await ClickAsync(HeaderBox(cut));

        Assert.Equal([new RowMarkIntent<TestRow>.AllRows(true, 1)], marks.Intents);
    }

    // ---- The listener ----

    [Fact] // ADR-0142 / LV-32, ADR-0021: the listener reads a mark press's paint, and the header's page, at the mousedown on one of this grid's own marks — not a nested grid's — and tells them at the release on the same mark, as it does for an action; no listener is added
    public void The_script_tells_a_mark_press_its_paint()
    {
        var script = AssetSources.Read("ExGrid", "ex-grid.js");

        Assert.Contains("const mark = target instanceof Element ? target.closest('.ex-mark, .ex-mark-result') : null;", script);
        Assert.Contains("return mark !== null && root !== null && mark.closest('.ex-grid') === root ? mark : null;", script);
        Assert.Contains("const mark = event.button === 0 && !contextPress ? ownMark(event.target) : null;", script);
        Assert.Contains("markPress = mark !== null ? { mark, paint: paintNow(), page: page === null ? -1 : Number(page) } : null;", script);
        Assert.Contains("if (core && mark !== null && event.button === 0 && ownMark(event.target) === mark.mark) {", script);
        Assert.Contains("core.invokeMethodAsync('MarkPressTakenAt', mark.paint, mark.page)", script);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(script, @"addEventListener\('mousedown'"));
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(script, @"addEventListener\('mouseup'"));
    }

    [Fact] // ADR-0142 / LV-32: what the listener reads is written by the render: the header's checkbox names its page under a pager, and "Mark all N rows" is marked as a mark
    public void The_render_writes_what_a_mark_press_reads()
    {
        var window = TestRows.Many(30);
        var cut = RenderPushed(new RecordingMarks(), window, pageSize: 10);

        Assert.Equal("0", HeaderBox(cut).GetAttribute("data-ex-page"));
        Assert.StartsWith("Mark all ", MarkAll(cut).TextContent, StringComparison.Ordinal);
        Assert.Null(RenderPushed(new RecordingMarks(), window).Find(".ex-header .ex-mark").GetAttribute("data-ex-page"));
    }

    /// <summary>A Consumer's marks, recorded: every intent the grid reports is kept.</summary>
    private sealed class RecordingMarks : IRowMarks<TestRow>
    {
        public List<RowMarkIntent<TestRow>> Intents { get; } = [];

        public RowMarkCounts? Counts => new RowMarkCounts(0, 50, 0);

        public event Action? Changed { add { } remove { } }

        public bool IsMarked(TestRow row) => false;

        public Task OnMarkIntentAsync(RowMarkIntent<TestRow> intent)
        {
            Intents.Add(intent);
            return Task.CompletedTask;
        }

        public void OnRowKindChanged(Func<TestRow, RowKind>? rowKind)
        {
        }
    }

    /// <summary>A fresh source at Row Sequence Version 0 that keeps its own marks, with a Row Key
    /// (its Book) or without one.</summary>
    private sealed class MarkingSource(IReadOnlyList<TestRow> rows, bool keyed) : IGridSource<TestRow>
    {
        public IReadOnlyList<TestRow> Window { get; } = rows;

        public int WindowStart => 0;

        public int? TotalCount => Window.Count;

        public bool IsLoading => false;

        public int RowSequenceVersion => 0;

        public IReadOnlyList<SortSpec> Sorts => [];

        public GridFilter? Filter => null;

        public event Action? StateChanged { add { } remove { } }

        public RecordingMarks Heard { get; } = new();

        public IRowMarks<TestRow>? Marks => Heard;

        public Func<TestRow, object>? RowKey => keyed ? ByBook : null;

        public void OnSortChanged(IReadOnlyList<SortSpec> sorts)
        {
        }

        public void OnFilterChanged(GridFilter? filter)
        {
        }

        public void OnColumnsChanged(IReadOnlyList<ColumnInfo<TestRow>> columns)
        {
        }

        public Task OnRangeNeededAsync(RowRange range) => Task.CompletedTask;

        public Task<IReadOnlyList<TestRow>> GetRowsAsync(RowRange range, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<TestRow>>([.. Window.Skip(range.Start).Take(range.Count)]);

        public Task<Chrome.DistinctValues> GetDistinctValuesAsync(string column, CancellationToken cancellationToken)
            => Task.FromResult(Chrome.DistinctValues.Of([]));
    }
}
