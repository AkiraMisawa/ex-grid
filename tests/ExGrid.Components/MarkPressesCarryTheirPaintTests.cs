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
/// checkbox or "Mark all N rows" — for the header's under a pager, the page it named, and for a row's
/// checkbox, the row its cell's id names — and tells them at the release, before Blazor dispatches the
/// click. What the press can still name exactly is honoured: a row's checkbox marks its row by identity
/// wherever the order has moved it, and the header's checkbox under a pager marks the page it was pressed
/// on after the page has turned, as positions under the order it was pressed in. The rest marks nothing
/// and is refused once, through <c>OnMarkRefused</c>: <c>SourceChanged</c> for a press made on what a
/// replaced Source painted, a row's checkbox included; <c>OrderMoved</c> for the header's checkbox or
/// "Mark all N rows" pressed under an order that has moved since. A press told the current paint, or
/// told none, marks as before. Here a test tells the press as the listener does, then clicks.
///
/// <para>Blazor does not deliver an event whose attribute a component since disposed had rendered, so a
/// row's checkbox whose row component a render disposed before its click was heard never hears it. The
/// core answers such a press itself, as the click would have been, after the render that disposed it —
/// or at once, when it is told after — and a press whose click a later press overtook is let go, so that
/// a told press never reaches the next one (principle 6). Where a test delivers the click of a disposed
/// checkbox, it calls the handler that component held, as the renderer would.</para>
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

    /// <summary>The same, with an Action Column after them, Do 300–400.</summary>
    private static readonly GridColumn<TestRow>[] WithAnAction =
    [
        .. Columns,
        GridColumn<TestRow>.ActionColumn("Do", [new GridAction("approve", "Approve")], width: Fixed100),
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

    /// <summary>The same instances, the one at <paramref name="from"/> moved to <paramref name="to"/>.</summary>
    private static TestRow[] Moved(TestRow[] rows, int from, int to)
    {
        var list = rows.ToList();
        var row = list[from];
        list.RemoveAt(from);
        list.Insert(to, row);
        return [.. list];
    }

    /// <summary>The rows with a new instance of the one at <paramref name="at"/>, equal in every value:
    /// the same row, as a live feed hands it over again.</summary>
    private static TestRow[] Renewed(TestRow[] rows, int at)
    {
        var next = (TestRow[])rows.Clone();
        var row = rows[at];
        next[at] = new TestRow { Book = row.Book, Amount = row.Amount, AsOf = row.AsOf, Active = row.Active };
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

    /// <summary>What the listener tells at the release on a mark: the paint, the page and, for a row's
    /// checkbox, the row read at its press.</summary>
    private static Task TellAsync(IRenderedComponent<ExGrid<TestRow>> cut, int paint, int page = -1, int row = -1)
        => cut.InvokeAsync(() => cut.Instance.MarkPressTakenAt(paint, page, row));

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

    /// <summary>
    /// A render that disposes the component painting row 2's checkbox, the order unmoved: without a Row
    /// Key, a new instance of that row, which is a new component (ADR-0140); with one, a scroll that
    /// takes the row out of the painted rows. Answers the rows in force after it.
    /// </summary>
    private async Task<TestRow[]> TakeRowTwosCheckboxAwayAsync(IRenderedComponent<ExGrid<TestRow>> cut, TestRow[] window, bool rowKey)
    {
        if (rowKey)
        {
            await ScrollToAsync(cut.Find(".ex-scroller"), 600);
            Assert.DoesNotContain(cut.FindComponents<ExGridRow<TestRow>>(), r => ReferenceEquals(r.Instance.Row, window[2]));
            return window;
        }
        var renewed = Renewed(window, 2);
        cut.Render(ps => ps.Add(g => g.Window, renewed));
        return renewed;
    }

    // ---- A row's checkbox: refused under another binding, never under another order ----

    [Theory] // ADR-0043 (note of 2026-10-08, decided 2026-10-09) / MK-9, ADR-0142: a press on a row's checkbox made on what a Source since replaced painted — both at version 0 — marks nothing in either source's marks and is refused once as SourceChanged, under a pager and not; with a Row Key the checkbox survives holding the new source's row and hears the click, without one its component is gone and the core answers the press
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
        await TellAsync(cut, pressedOn, row: 2);
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

        await TellAsync(cut, Paint(cut), row: 2);
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
        await TellAsync(cut, pressedOn, row: 0);
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

    // ---- A told press whose click never comes ----

    [Theory] // ADR-0043 / MK-9, ADR-0142: a told press on a row's checkbox whose row component is still rendered waits for its click, and marks once — a render after the click answers nothing more
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_told_row_checkbox_press_whose_row_is_still_rendered_waits_for_its_click(bool rowKey)
    {
        var window = TestRows.Many(50);
        var marks = new RecordingMarks();
        var cut = RenderPushed(marks, window, rowKey: rowKey);

        await TellAsync(cut, Paint(cut), row: 2);
        Assert.Empty(marks.Intents);
        await ClickAsync(RowBoxes(cut)[2]);
        await TakeRowTwosCheckboxAwayAsync(cut, window, rowKey);

        var intent = Assert.IsType<RowMarkIntent<TestRow>.OneRow>(Assert.Single(marks.Intents));
        Assert.Same(window[2], intent.Row);
        Assert.Empty(Refusals);
    }

    [Theory] // ADR-0043 / MK-9, ADR-0142 "no press is lost to Blazor": a press on a row's checkbox whose row component is already gone when the press is told — a new instance of its row without a Row Key, a scroll with one — never hears its click, and is answered at once, as the click would have been: the row at its position under the same order is marked
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_told_row_checkbox_press_whose_component_is_gone_is_answered_at_once_on_the_row_at_its_position(bool rowKey)
    {
        var window = TestRows.Many(50);
        var marks = new RecordingMarks();
        var cut = RenderPushed(marks, window, rowKey: rowKey);
        var pressedOn = Paint(cut);
        var now = await TakeRowTwosCheckboxAwayAsync(cut, window, rowKey);

        await TellAsync(cut, pressedOn, row: 2);

        var intent = Assert.IsType<RowMarkIntent<TestRow>.OneRow>(Assert.Single(marks.Intents));
        Assert.Same(now[2], intent.Row);
        Assert.True(intent.Marked);
        Assert.Empty(Refusals);
    }

    [Theory] // ADR-0043 / MK-9, principle 6 (review of 2026-10-09): a told press on a row's checkbox whose row component a later render disposes before its click is answered after that render, as the click would have been — and never reaches the next press: "Mark all N rows" pressed by key after an order move, told nothing, marks all and is refused for nothing — with a Row Key and without
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_told_row_checkbox_press_whose_click_never_comes_is_answered_and_never_reaches_the_next_press(bool rowKey)
    {
        var window = TestRows.Many(50);
        var marks = new RecordingMarks();
        var cut = RenderPushed(marks, window, pageSize: 40, rowKey: rowKey);
        await TellAsync(cut, Paint(cut), row: 2);

        var now = await TakeRowTwosCheckboxAwayAsync(cut, window, rowKey);

        cut.WaitForAssertion(() => Assert.Single(marks.Intents));
        var answered = Assert.IsType<RowMarkIntent<TestRow>.OneRow>(marks.Intents[0]);
        Assert.Same(now[2], answered.Row);
        Assert.True(answered.Marked);

        Reorder(cut, Swapped(now, 0, 1), version: 1);
        await ClickAsync(MarkAll(cut));

        Assert.Equal(2, marks.Intents.Count);
        Assert.Equal(new RowMarkIntent<TestRow>.AllRows(true, 1), marks.Intents[1]);
        Assert.Empty(Refusals);
    }

    [Theory] // ADR-0043 / MK-9, principle 6: a told press on a row's checkbox told a paint older than every paint the grid keeps a row serial for waits for its click; overtaken by another mark press — "Mark all N rows" pressed by key after an order move, told nothing — it is let go, and never reaches that press, which marks all and is refused for nothing
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_told_row_checkbox_press_overtaken_by_another_mark_press_never_reaches_it(bool rowKey)
    {
        var window = TestRows.Many(50);
        var marks = new RecordingMarks();
        var cut = RenderPushed(marks, window, pageSize: 40, rowKey: rowKey);
        var pressedOn = Paint(cut);
        // Far more paints than the grid keeps serials for, each a new instance of a painted row.
        for (var i = 0; i < 100; i++)
        {
            window = Renewed(window, 1);
            cut.Render(ps => ps.Add(g => g.Window, window));
        }
        await TellAsync(cut, pressedOn, row: 2);
        Assert.Empty(marks.Intents);

        Reorder(cut, Swapped(window, 0, 1), version: 1);
        await ClickAsync(MarkAll(cut));

        Assert.Equal([new RowMarkIntent<TestRow>.AllRows(true, 1)], marks.Intents);
        Assert.Empty(Refusals);
    }

    [Theory] // ADR-0043 / MK-9, principle 6: a told press on a row's checkbox whose component a render disposed is answered before a marking gesture heard after that render — Space in the Mark Column, or "Mark all N rows" by key — even while the grid's after-render pass, which answers it otherwise, has not run yet, as on a circuit until the browser acknowledges the render: the two land in the order they were made
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_lost_row_checkbox_press_is_answered_before_a_marking_gesture_made_after_it(bool markAll)
    {
        var window = TestRows.Many(50);
        var marks = new RecordingMarks();
        var acting = new TaskCompletionSource();
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, window)
            .Add(g => g.TotalCount, window.Length)
            .Add(g => g.Columns, WithAnAction)
            .Add(g => g.Marks, marks)
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 120)
            .Add(g => g.ViewportWidth, 600)
            .Add(g => g.PageSize, 40)
            .Add(g => g.OnAction, (Cells.GridActionEventArgs<TestRow> _) => acting.Task)
            .Add(g => g.OnMarkRefused, (MarkRefusalReason reason) => Refusals.Add(reason)));
        // Rows 0 to 3 selected in the Mark Column, for Space.
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 50, OffsetY = 10 });
        await cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs { Button = 0, OffsetX = 50, OffsetY = 10 });
        for (var i = 0; i < 3; i++)
            await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowDown", false, true, alt: false, meta: false, metaIsPrimary: false));
        var paint = Paint(cut);
        // A press on row 2's checkbox and one on its action, both told, neither clicked.
        await TellAsync(cut, paint, row: 2);
        await cut.InvokeAsync(() => cut.Instance.ActionPressTakenAt(paint, row: 2, column: 3, action: 0));

        // A new instance of row 2 disposes its component. After that render the grid answers the action
        // first, and its handler has not returned: the after-render pass has not reached the mark press.
        var renewed = Renewed(window, 2);
        cut.Render(ps => ps.Add(g => g.Window, renewed));
        Assert.Empty(marks.Intents);

        if (markAll)
            await ClickAsync(MarkAll(cut));
        else
            await cut.InvokeAsync(() => cut.Instance.OnKeyAsync(" ", false, false, alt: false, meta: false, metaIsPrimary: false));

        Assert.Equal(2, marks.Intents.Count);
        var answered = Assert.IsType<RowMarkIntent<TestRow>.OneRow>(marks.Intents[0]);
        Assert.Same(renewed[2], answered.Row);
        Assert.True(answered.Marked);
        if (markAll)
            Assert.Equal(new RowMarkIntent<TestRow>.AllRows(true, 0), marks.Intents[1]);
        else
            Assert.Equal([new RowRange(0, 4)], Assert.IsType<RowMarkIntent<TestRow>.Positions>(marks.Intents[1]).Ranges);

        // The action's handler returns, and the after-render pass finds nothing more to answer.
        await cut.InvokeAsync(acting.SetResult);
        Assert.Equal(2, marks.Intents.Count);
        Assert.Empty(Refusals);
    }

    [Theory] // ADR-0043 / MK-9, ADR-0160: a told press on a row's checkbox whose component is gone, and whose order moved before the core heard it, cannot be paired with its row — the grid keeps no row and no Row Key of the render it was pressed on — so it marks nothing and is refused once as OrderMoved
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_told_row_checkbox_press_whose_component_is_gone_under_a_moved_order_is_refused_as_order_moved(bool rowKey)
    {
        var window = TestRows.Many(50);
        var marks = new RecordingMarks();
        var cut = RenderPushed(marks, window, rowKey: rowKey);
        var pressedOn = Paint(cut);
        // Row 2 moves to row 40, out of the painted rows: its component goes, and position 2 names another row.
        Reorder(cut, Moved(window, 2, 40), version: 1);

        await TellAsync(cut, pressedOn, row: 2);

        Assert.Empty(marks.Intents);
        Assert.Equal([MarkRefusalReason.OrderMoved], Refusals);
    }

    [Theory] // ADR-0043 / MK-9, ADR-0142: a press on a row's checkbox the core answered marks once, even when the component a render disposed still delivers its click
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_answered_row_checkbox_press_marks_once_even_when_its_click_is_delivered(bool rowKey)
    {
        var window = TestRows.Many(50);
        var marks = new RecordingMarks();
        var cut = RenderPushed(marks, window, rowKey: rowKey);
        var held = CheckboxHandlerOf(cut, window[2]);
        await TellAsync(cut, Paint(cut), row: 2);
        await TakeRowTwosCheckboxAwayAsync(cut, window, rowKey);
        cut.WaitForAssertion(() => Assert.Single(marks.Intents));

        await cut.InvokeAsync(() => held(window[2], true));

        Assert.IsType<RowMarkIntent<TestRow>.OneRow>(Assert.Single(marks.Intents));
        Assert.Empty(Refusals);
    }

    // ---- The listener ----

    [Fact] // ADR-0043 / MK-9, ADR-0142, ADR-0021: the listener reads a mark press's paint, the header's page and a row's checkbox's row — from its own cell's id, never a cell of an outer grid — at the mousedown on one of this grid's own marks, not a nested grid's, and tells them at the release on the same mark, as it does for an action; no listener is added
    public void The_script_tells_a_mark_press_its_paint()
    {
        var script = AssetSources.Read("ExGrid", "ex-grid.js");

        Assert.Contains("const mark = target instanceof Element ? target.closest('.ex-mark, .ex-mark-result') : null;", script);
        Assert.Contains("return mark !== null && root !== null && mark.closest('.ex-grid') === root ? mark : null;", script);
        Assert.Contains("const mark = event.button === 0 && !contextPress ? ownMark(event.target) : null;", script);
        Assert.Contains("const markCell = mark !== null ? mark.closest('[role=gridcell]') : null;", script);
        Assert.Contains("const markAt = markCell !== null && root.contains(markCell) ? /r(\\d+)c(\\d+)$/.exec(markCell.id) : null;", script);
        Assert.Contains("markPress = mark !== null ? { mark, paint: paintNow(), page: page === null ? -1 : Number(page), row: markAt ? Number(markAt[1]) : -1 } : null;", script);
        Assert.Contains("if (core && mark !== null && event.button === 0 && ownMark(event.target) === mark.mark) {", script);
        Assert.Contains("core.invokeMethodAsync('MarkPressTakenAt', mark.paint, mark.page, mark.row)", script);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(script, @"addEventListener\('mousedown'"));
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(script, @"addEventListener\('mouseup'"));
    }

    [Fact] // ADR-0043 / MK-9, ADR-0142: what the listener reads is written by the render: the header's checkbox names its page under a pager, "Mark all N rows" is marked as a mark, and a row's checkbox stands in the cell whose id names its row
    public void The_render_writes_what_a_mark_press_reads()
    {
        var window = TestRows.Many(30);
        var cut = RenderPushed(new RecordingMarks(), window, pageSize: 10);

        Assert.Equal("0", HeaderBox(cut).GetAttribute("data-ex-page"));
        Assert.StartsWith("Mark all ", MarkAll(cut).TextContent, StringComparison.Ordinal);
        var cell = RowBoxes(cut)[2].ParentElement!;
        Assert.Equal("gridcell", cell.GetAttribute("role"));
        Assert.EndsWith("r2c0", cell.Id, StringComparison.Ordinal);
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
