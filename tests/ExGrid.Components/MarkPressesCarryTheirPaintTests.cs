using System.Globalization;
using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Rows;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// A press on a mark carries the render it was made on, as a press on an action does, and is judged
/// against it (ADR-0043's note of 2026-10-08, decided with the user on 2026-10-09; MK-9; ADR-0142). The
/// grid's listener reads the paint the Viewport named at the mousedown on a row's checkbox, the header's
/// checkbox or "Mark all N rows" — and, for the header's under a pager, the page it named — and tells them
/// at the release, before Blazor dispatches the click. What the press can still name exactly is honoured: a row's checkbox marks its row by identity
/// wherever the order has moved it, and the header's checkbox under a pager marks the page it was pressed
/// on after the page has turned, as positions under the order it was pressed in. The rest marks nothing
/// and is refused once, through <c>OnMarkRefused</c>: <c>SourceChanged</c> for a press made on what a
/// replaced Source painted, a row's checkbox included; <c>OrderMoved</c> for the header's checkbox or
/// "Mark all N rows" pressed under an order that has moved since. A press told the current paint, or
/// told none, marks as before. Here a test tells the press as the listener does, then clicks.
///
/// <para>Where a test delivers the click of a checkbox whose row component a render disposed, it calls
/// the handler that component held, as the renderer would.</para>
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

    /// <summary>Every refusal the grid raised through <c>OnMarkRefused</c>, in order.</summary>
    private List<MarkRefusalReason> Refusals { get; } = [];

    /// <summary>A pushed Window, its marks held by <paramref name="marks"/>, keyed by its Book where
    /// <paramref name="rowKey"/> says so.</summary>
    private IRenderedComponent<ExGrid<TestRow>> RenderPushed(
        RecordingMarks marks, TestRow[] window, int? pageSize = null, bool rowKey = false)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, window)
              .Add(g => g.TotalCount, window.Length)
              .Add(g => g.Columns, Columns)
              .Add(g => g.Marks, marks)
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 120)
              .Add(g => g.ViewportWidth, 600)
              .Add(g => g.OnMarkRefused, (MarkRefusalReason reason) => Refusals.Add(reason));
            if (pageSize is { } size)
                ps.Add(g => g.PageSize, size);
            if (rowKey)
                ps.Add(g => g.RowKey, ByBook);
        });

    /// <summary>A bound Source that keeps its own marks.</summary>
    private IRenderedComponent<ExGrid<TestRow>> RenderBound(IGridSource<TestRow> source, int? pageSize = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Source, source)
              .Add(g => g.Columns, Columns)
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 120)
              .Add(g => g.ViewportWidth, 600)
              .Add(g => g.OnMarkRefused, (MarkRefusalReason reason) => Refusals.Add(reason));
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

    /// <summary>The handler the row component painting <paramref name="row"/> holds for its checkbox: what
    /// a click on it would still reach, were the renderer to deliver it once a render disposed that
    /// component.</summary>
    private static Func<TestRow, bool, Task> CheckboxHandlerOf(IRenderedComponent<ExGrid<TestRow>> cut, TestRow row)
        => cut.FindComponents<ExGridRow<TestRow>>().Single(r => ReferenceEquals(r.Instance.Row, row)).Instance.OnMark!;

    // ---- A row's checkbox: refused under another binding, never under another order ----

    [Theory] // ADR-0043 (note of 2026-10-08, decided 2026-10-09) / MK-9, ADR-0142: a press on a row's checkbox made on what a Source since replaced painted — both at version 0 — marks nothing in either source's marks and is refused once as SourceChanged, under a pager and not; with a Row Key the checkbox survives holding the new source's row and hears the click, without one its component is gone and the click its handler held is delivered
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task A_row_checkbox_pressed_on_the_replaced_sources_paint_marks_nothing_and_is_refused_as_source_changed(bool rowKey, bool pager)
    {
        var rows = TestRows.Many(50);
        var old = new MarkingSource(rows, rowKey);
        var replacement = new MarkingSource(Copies(rows), rowKey);
        var cut = RenderBound(old, pager ? 20 : null);
        var pressedOn = Paint(cut);
        var held = CheckboxHandlerOf(cut, rows[2]);

        cut.Render(ps => ps.Add(g => g.Source, replacement));
        await TellAsync(cut, pressedOn);
        if (rowKey)
            await ClickAsync(RowBoxes(cut)[2]);
        else
            await cut.InvokeAsync(() => held(rows[2], true));

        Assert.Empty(old.Heard.Intents);
        Assert.Empty(replacement.Heard.Intents);
        Assert.Equal([MarkRefusalReason.SourceChanged], Refusals);
    }

    [Theory] // ADR-0043 / MK-9, ADR-0142: a press on a row's checkbox told the new Source's paint marks its row, as before, and is refused for nothing — under a pager and not, with a Row Key and without
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task A_row_checkbox_pressed_on_the_new_sources_paint_marks_its_row(bool rowKey, bool pager)
    {
        var rows = TestRows.Many(50);
        var old = new MarkingSource(rows, rowKey);
        var replacement = new MarkingSource(Copies(rows), rowKey);
        var cut = RenderBound(old, pager ? 20 : null);
        cut.Render(ps => ps.Add(g => g.Source, replacement));

        await TellAsync(cut, Paint(cut));
        await ClickAsync(RowBoxes(cut)[2]);

        Assert.Empty(old.Heard.Intents);
        var intent = Assert.IsType<RowMarkIntent<TestRow>.OneRow>(Assert.Single(replacement.Heard.Intents));
        Assert.Same(replacement.Window[2], intent.Row);
        Assert.Empty(Refusals);
    }

    [Theory] // ADR-0043 / MK-9, ADR-0142: a row's checkbox names its row by identity, which an order move leaves it: pressed under an order since moved, it marks the row it was pressed on, wherever the order took it, and is refused for nothing — under a pager and not, with a Row Key and without
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task A_row_checkbox_pressed_under_a_moved_order_marks_its_row(bool rowKey, bool pager)
    {
        var window = TestRows.Many(50);
        var marks = new RecordingMarks();
        var cut = RenderPushed(marks, window, pager ? 20 : null, rowKey);
        var pressedOn = Paint(cut);

        // Rows 0 and 2 change places: the checkbox pressed at row 0 stands at row 2, holding its row.
        Reorder(cut, Swapped(window, 0, 2), version: 1);
        await TellAsync(cut, pressedOn);
        await ClickAsync(RowBoxes(cut)[2]);

        var intent = Assert.IsType<RowMarkIntent<TestRow>.OneRow>(Assert.Single(marks.Intents));
        Assert.Same(window[0], intent.Row);
        Assert.True(intent.Marked);
        Assert.Empty(Refusals);
    }

    // ---- The header's checkbox and "Mark all N rows": by position ----

    [Theory] // ADR-0043 / MK-9, ADR-0015: under a pager the header's checkbox names the page it was pressed on; heard after the page was turned, under the same order and Source, it lines up that page — as positions under the order it was pressed in — and is refused for nothing; one told the page shown now lines that page up, as before
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_header_checkbox_pressed_on_a_page_turned_since_marks_the_page_it_was_pressed_on(bool rowKey)
    {
        var window = TestRows.Many(30);
        var marks = new RecordingMarks();
        var cut = RenderPushed(marks, window, pageSize: 10, rowKey: rowKey);
        var pressedOn = Paint(cut);
        var page = PageOf(cut);
        Assert.Equal(0, page);

        await ClickAsync(cut.FindAll(".ex-pager button")[1]);
        Assert.Equal(10, PageOf(cut));
        await TellAsync(cut, pressedOn, page);
        await ClickAsync(HeaderBox(cut));

        var pressed = Assert.IsType<RowMarkIntent<TestRow>.Positions>(Assert.Single(marks.Intents));
        Assert.Equal([new RowRange(0, 10)], pressed.Ranges);
        Assert.Equal(0, pressed.RowSequenceVersion);
        Assert.Empty(Refusals);

        await TellAsync(cut, Paint(cut), PageOf(cut));
        await ClickAsync(HeaderBox(cut));
        var shown = Assert.IsType<RowMarkIntent<TestRow>.Positions>(marks.Intents[1]);
        Assert.Equal([new RowRange(10, 10)], shown.Ranges);
        Assert.Empty(Refusals);
    }

    [Theory] // ADR-0043 / MK-9: through GridSource.From, the header's checkbox pressed on page 1 and heard after the page was turned to page 2 marks page 1's rows, which the source resolves by position under the same order though they are no longer on screen, and none of page 2's — with a Row Key and without
    [InlineData(false)]
    [InlineData(true)]
    public async Task Through_GridSource_From_a_press_on_a_page_turned_since_marks_that_pages_rows(bool rowKey)
    {
        var rows = TestRows.Many(30);
        var source = rowKey ? GridSource.From(rows, ByBook) : GridSource.From(rows);
        var cut = RenderBound(source, pageSize: 10);
        var pressedOn = Paint(cut);
        var page = PageOf(cut);

        await ClickAsync(cut.FindAll(".ex-pager button")[1]);
        await TellAsync(cut, pressedOn, page);
        await ClickAsync(HeaderBox(cut));

        Assert.Equal(rows[..10].Select(r => r.Book), source.Marks.MarkedRows.Select(r => r.Book));
        Assert.Empty(Refusals);
    }

    [Theory] // ADR-0043 / MK-9, ADR-0011: the header's checkbox — under a pager, naming its page, and without one, naming the result — pressed under an order that has moved since marks nothing and is refused once as OrderMoved; on the current paint it marks as before, refused for nothing — with a Row Key and without
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task The_header_checkbox_pressed_under_a_moved_order_marks_nothing_and_is_refused_as_order_moved(bool rowKey, bool pager, bool moved)
    {
        var window = TestRows.Many(30);
        var marks = new RecordingMarks();
        var cut = RenderPushed(marks, window, pager ? 10 : null, rowKey);
        var pressedOn = Paint(cut);
        var page = PageOf(cut);
        Assert.Equal(pager ? 0 : -1, page);

        if (moved)
            Reorder(cut, Swapped(window, 0, 1), version: 1);
        await TellAsync(cut, pressedOn, page);
        await ClickAsync(HeaderBox(cut));

        if (moved)
        {
            Assert.Empty(marks.Intents);
            Assert.Equal([MarkRefusalReason.OrderMoved], Refusals);
            return;
        }
        Assert.Empty(Refusals);
        if (!pager)
        {
            Assert.Equal([new RowMarkIntent<TestRow>.AllRows(true, 0)], marks.Intents);
            return;
        }
        var intent = Assert.IsType<RowMarkIntent<TestRow>.Positions>(Assert.Single(marks.Intents));
        Assert.Equal([new RowRange(0, 10)], intent.Ranges);
    }

    [Theory] // ADR-0043 / MK-9, ADR-0011: a page turned since and an order moved since: the press named the page's rows under an order that no longer stands, so it marks nothing and is refused once as OrderMoved
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_header_checkbox_on_a_page_turned_since_under_a_moved_order_is_refused_as_order_moved(bool rowKey)
    {
        var window = TestRows.Many(30);
        var marks = new RecordingMarks();
        var cut = RenderPushed(marks, window, pageSize: 10, rowKey: rowKey);
        var pressedOn = Paint(cut);
        var page = PageOf(cut);

        await ClickAsync(cut.FindAll(".ex-pager button")[1]);
        Reorder(cut, Swapped(window, 0, 1), version: 1);
        await TellAsync(cut, pressedOn, page);
        await ClickAsync(HeaderBox(cut));

        Assert.Empty(marks.Intents);
        Assert.Equal([MarkRefusalReason.OrderMoved], Refusals);
    }

    [Theory] // ADR-0043 / MK-9, ADR-0142: the header's checkbox — under a pager and not — pressed on what a Source since replaced painted, both at version 0, marks nothing in either source's marks and is refused once as SourceChanged — with a Row Key and without
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task The_header_checkbox_pressed_on_the_replaced_sources_paint_marks_nothing_and_is_refused_as_source_changed(bool rowKey, bool pager)
    {
        var rows = TestRows.Many(30);
        var old = new MarkingSource(rows, rowKey);
        var replacement = new MarkingSource(Copies(rows), rowKey);
        var cut = RenderBound(old, pager ? 10 : null);
        var pressedOn = Paint(cut);
        var page = PageOf(cut);

        cut.Render(ps => ps.Add(g => g.Source, replacement));
        await TellAsync(cut, pressedOn, page);
        await ClickAsync(HeaderBox(cut));

        Assert.Empty(old.Heard.Intents);
        Assert.Empty(replacement.Heard.Intents);
        Assert.Equal([MarkRefusalReason.SourceChanged], Refusals);
    }

    [Theory] // ADR-0043 / MK-9, ADR-0015: "Mark all N rows" names the result as it stood at the press: pressed under an order that has moved since, it marks nothing and is refused once as OrderMoved; on the current paint it marks all as before, refused for nothing — with a Row Key and without
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Mark_all_rows_pressed_under_a_moved_order_marks_nothing_and_is_refused_as_order_moved(bool rowKey, bool moved)
    {
        var window = TestRows.Many(30);
        var marks = new RecordingMarks();
        var cut = RenderPushed(marks, window, pageSize: 10, rowKey: rowKey);
        var pressedOn = Paint(cut);

        if (moved)
            Reorder(cut, Swapped(window, 0, 1), version: 1);
        await TellAsync(cut, pressedOn);
        await ClickAsync(MarkAll(cut));

        Assert.Equal(moved ? [] : [new RowMarkIntent<TestRow>.AllRows(true, 0)], marks.Intents);
        Assert.Equal(moved ? [MarkRefusalReason.OrderMoved] : [], Refusals);
    }

    [Theory] // ADR-0043 / MK-9, ADR-0142: "Mark all N rows" pressed on what a Source since replaced painted, both at version 0, marks nothing in either source's marks and is refused once as SourceChanged — with a Row Key and without
    [InlineData(false)]
    [InlineData(true)]
    public async Task Mark_all_rows_pressed_on_the_replaced_sources_paint_marks_nothing_and_is_refused_as_source_changed(bool rowKey)
    {
        var rows = TestRows.Many(30);
        var old = new MarkingSource(rows, rowKey);
        var replacement = new MarkingSource(Copies(rows), rowKey);
        var cut = RenderBound(old, pageSize: 10);
        var pressedOn = Paint(cut);

        cut.Render(ps => ps.Add(g => g.Source, replacement));
        await TellAsync(cut, pressedOn);
        await ClickAsync(MarkAll(cut));

        Assert.Empty(old.Heard.Intents);
        Assert.Empty(replacement.Heard.Intents);
        Assert.Equal([MarkRefusalReason.SourceChanged], Refusals);
    }

    [Theory] // ADR-0043 / MK-9, ADR-0142: a mark press nobody told of — made by key or by script, with no mousedown before it — is taken as aimed at the newest paint, and marks, refused for nothing
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_untold_mark_press_marks_as_before(bool rowKey)
    {
        var window = TestRows.Many(50);
        var marks = new RecordingMarks();
        var cut = RenderPushed(marks, window, rowKey: rowKey);
        Reorder(cut, Swapped(window, 0, 1), version: 1);

        await ClickAsync(HeaderBox(cut));

        Assert.Equal([new RowMarkIntent<TestRow>.AllRows(true, 1)], marks.Intents);
        Assert.Empty(Refusals);
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
