using Bunit;
using ExGrid.Clipboard;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The clipboard wiring (ADR-0005/0014): what the copy event's synchronous question is
/// answered with, which route a selection takes, and the one Edit Intent a paste
/// raises. The real clipboard is layer 3's; what is pinned here is everything up to
/// its edge.
/// </summary>
public class ClipboardWiringTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    // Both editable: the shape rules are what this file pins, and a paste into a column
    // that never opted in is refused before them (ADR-0035). "Locked" is that column.
    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100, editable: true),
        new("Locked", ColumnType.Text, r => r.Book, width: Fixed100),
    ];

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        Action<Bunit.ComponentParameterCollectionBuilder<ExGrid<TestRow>>>? extra = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Window())
              .Add(g => g.Columns, Columns())
              .Add(g => g.RowHeight, 20)
              .Add(g => g.ViewportHeight, 100)
              .Add(g => g.ViewportWidth, 350);
            extra?.Invoke(ps);
        });

    private static Task ClickCellAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });

    private static Task PressAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key, bool ctrl = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, ctrl, false, false, false, false));

    [Fact] // ADR-0005 / CP-4: both formats in one answer — display in the text, raw in the html
    public async Task A_selection_in_the_window_answers_both_formats_synchronously()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 50, 10);                       // Book, row 0
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowRight", false, true, false, false, false)); // extend to Amount

        var payload = await cut.InvokeAsync(() => cut.Instance.BuildCopyPayload());

        Assert.Equal("data", payload.Kind);
        Assert.Equal("Alpha\t100.5\r\n", payload.Text);
        Assert.Equal("<table data-ex-grid=\"invariant\"><tr><td>Alpha</td><td>100.5</td></tr></table>", payload.Html);
    }

    [Fact] // ADR-0005 / CP-15: an empty selection refuses with its own reason; the clipboard is untouched
    public async Task An_empty_selection_refuses_by_name()
    {
        CopyRefusalReason? refused = null;
        var cut = RenderGrid(ps => ps.Add(g => g.OnCopyRefused, (CopyRefusalReason r) => refused = r));

        var payload = await cut.InvokeAsync(() => cut.Instance.BuildCopyPayload());

        Assert.Equal("none", payload.Kind);
        Assert.Equal(CopyRefusalReason.EmptySelection, refused);
    }

    [Fact] // ADR-0005 / CP-23: a write the browser rejects is a Refusal, raised once — never a silence
    public async Task A_write_the_browser_rejects_is_refused_by_name()
    {
        var refusals = new List<CopyRefusalReason>();
        var cut = RenderGrid(ps => ps.Add(g => g.OnCopyRefused, (CopyRefusalReason r) => refusals.Add(r)));

        await cut.InvokeAsync(() => cut.Instance.OnCopyWriteRejectedAsync());

        Assert.Equal([CopyRefusalReason.ClipboardUnavailable], refusals);
    }

    [Fact] // ADR-0005 / CP-2: refusal is strictly past the cap
    public async Task The_cap_refuses_strictly_past_it()
    {
        CopyRefusalReason? refused = null;
        var cut = RenderGrid(ps => ps
            .Add(g => g.CopyCellCap, 2L)
            .Add(g => g.OnCopyRefused, (CopyRefusalReason r) => refused = r));
        await ClickCellAsync(cut, 50, 10);
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowRight", false, true, false, false, false));

        // Exactly at the cap: two cells copy.
        var atCap = await cut.InvokeAsync(() => cut.Instance.BuildCopyPayload());
        Assert.Equal("data", atCap.Kind);
        Assert.Null(refused);

        // One more cell is past it: nothing copies.
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowDown", false, true, false, false, false));
        var pastCap = await cut.InvokeAsync(() => cut.Instance.BuildCopyPayload());
        Assert.Equal("none", pastCap.Kind);
        Assert.Equal(CopyRefusalReason.TooLarge, refused);
    }

    [Fact] // ADR-0005 / CP-7: beyond the Window the synchronous answer is "ask again asynchronously"
    public async Task A_selection_beyond_the_window_takes_the_async_route()
    {
        var source = new TestSource();
        source.Push(TestRows.Window(), windowStart: 0, totalCount: 100);
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20)
            .Add(g => g.ViewportHeight, 100)
            .Add(g => g.ViewportWidth, 350));
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, " ", ctrl: true);                  // whole column, rows 0..99

        var payload = await cut.InvokeAsync(() => cut.Instance.BuildCopyPayload());
        Assert.Equal("async", payload.Kind);

        // The async route asks the source for the rows it does not hold, emits the
        // whole block, and stores nothing.
        source.CopyRows = range =>
        {
            var rows = new TestRow[range.Count];
            for (var i = 0; i < range.Count; i++)
                rows[i] = new TestRow { Book = $"B{range.Start + i}", Amount = range.Start + i };
            return rows;
        };
        var assembled = await cut.InvokeAsync(() => cut.Instance.BuildCopyPayloadAsync());

        Assert.NotNull(assembled);
        Assert.Equal("data", assembled!.Kind);
        Assert.Equal([new RowRange(0, 100)], source.CopyRequested);
        var lines = assembled.Text!.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(100, lines.Length);
        Assert.Equal("B0", lines[0]);
        Assert.Equal("B99", lines[99]);
    }

    [Fact] // ADR-0005: beyond the Window with nobody to ask, refuse by name — never the fraction in hand
    public async Task Beyond_the_window_with_no_provider_refuses()
    {
        CopyRefusalReason? refused = null;
        var cut = RenderGrid(ps => ps
            .Add(g => g.TotalCount, 100)
            .Add(g => g.OnCopyRefused, (CopyRefusalReason r) => refused = r));
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, " ", ctrl: true);

        var payload = await cut.InvokeAsync(() => cut.Instance.BuildCopyPayload());

        Assert.Equal("none", payload.Kind);
        Assert.Equal(CopyRefusalReason.RowsUnavailable, refused);
    }

    [Fact] // ADR-0005: a short answer aborts the copy — a partial block is never emitted
    public async Task A_short_answer_aborts_the_async_copy()
    {
        CopyRefusalReason? refused = null;
        var cut = RenderGrid(ps => ps
            .Add(g => g.TotalCount, 100)
            .Add(g => g.OnCopyRowsNeeded, (RowRange range, CancellationToken _) =>
                Task.FromResult<IReadOnlyList<TestRow>>([new TestRow { Book = "only one" }]))
            .Add(g => g.OnCopyRefused, (CopyRefusalReason r) => refused = r));
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, " ", ctrl: true);

        var assembled = await cut.InvokeAsync(() => cut.Instance.BuildCopyPayloadAsync());

        Assert.Null(assembled);
        Assert.Equal(CopyRefusalReason.RowsUnavailable, refused);
    }

    [Fact] // ADR-0014 / PST-1: a paste is one Edit Intent carrying every cell
    public async Task A_paste_raises_one_intent()
    {
        var intents = new List<GridPasteIntent>();
        var cut = RenderGrid(ps => ps.Add(g => g.OnPaste, (GridPasteIntent i) => intents.Add(i)));
        await ClickCellAsync(cut, 50, 10);
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowDown", false, true, false, false, false));

        await cut.InvokeAsync(() => cut.Instance.OnPasteAsync("x\r\ny\r\n", null));

        var intent = Assert.Single(intents);
        Assert.Equal(2, intent.CellCount);
        Assert.Equal("x", intent.ValueFor(new CellPosition(0, 0)));
        Assert.Equal("y", intent.ValueFor(new CellPosition(1, 0)));
    }

    [Fact] // ADR-0005 / CP-21: a paste arrives as streams and means exactly what the strings meant
    public async Task A_paste_arrives_as_streams()
    {
        var intents = new List<GridPasteIntent>();
        var cut = RenderGrid(ps => ps.Add(g => g.OnPaste, (GridPasteIntent i) => intents.Add(i)));
        await ClickCellAsync(cut, 50, 10);
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowDown", false, true, false, false, false));
        // Excel's HTML flavour wins over the text, which is the display format (ADR-0005).
        var text = FakeJSStream.Of("1\r\n2\r\n");
        var html = FakeJSStream.Of("<table><tr><td x:num=\"1.25\">1</td></tr><tr><td x:num=\"2.5\">2</td></tr></table>");

        await cut.InvokeAsync(() => cut.Instance.OnPasteStreamsAsync(text, html));

        var intent = Assert.Single(intents);
        Assert.Equal("1.25", intent.ValueFor(new CellPosition(0, 0)));
        Assert.Equal("2.5", intent.ValueFor(new CellPosition(1, 0)));
        Assert.True(text.Disposed && html.Disposed);
    }

    [Fact] // ADR-0005 / CP-22: past the byte cap the paste is refused whole, and nothing is read
    public async Task A_paste_past_the_byte_cap_is_refused_unread()
    {
        var intents = new List<GridPasteIntent>();
        PasteRefusalReason? refused = null;
        var cut = RenderGrid(ps => ps
            .Add(g => g.PasteByteCap, 100L)
            .Add(g => g.OnPaste, (GridPasteIntent i) => intents.Add(i))
            .Add(g => g.OnPasteRefused, (PasteRefusalReason r) => refused = r));
        await ClickCellAsync(cut, 50, 10);
        // The text alone would fit. Only the HTML carries the raw value, so falling back
        // to the text would paste the rounded display format — never done (ADR-0005).
        var text = FakeJSStream.OfLength(40);
        var html = FakeJSStream.OfLength(61);

        await cut.InvokeAsync(() => cut.Instance.OnPasteStreamsAsync(text, html));

        Assert.Equal(PasteRefusalReason.TooLarge, refused);
        Assert.Empty(intents);
        Assert.False(text.Opened);
        Assert.False(html.Opened);
        Assert.True(text.Disposed && html.Disposed);
    }

    [Fact] // ADR-0005 / CP-22: exactly on the cap is not past it
    public async Task A_paste_exactly_on_the_byte_cap_is_read()
    {
        var intents = new List<GridPasteIntent>();
        var cut = RenderGrid(ps => ps
            .Add(g => g.PasteByteCap, 4L)
            .Add(g => g.OnPaste, (GridPasteIntent i) => intents.Add(i)));
        await ClickCellAsync(cut, 50, 10);

        await cut.InvokeAsync(() => cut.Instance.OnPasteStreamsAsync(FakeJSStream.Of("fill"), null));

        Assert.Equal("fill", Assert.Single(intents).ValueFor(new CellPosition(0, 0)));
    }

    [Fact] // ADR-0005: the default ceiling is 16 MB across both flavours
    public void The_default_paste_byte_cap_is_sixteen_megabytes()
    {
        var cut = RenderGrid();

        Assert.Equal(16L * 1024 * 1024, cut.Instance.PasteByteCap);
    }

    [Fact] // ADR-0014 / PST-2: rows not in the Window are included in the target — intended
    public async Task A_paste_target_covers_rows_outside_the_window()
    {
        var intents = new List<GridPasteIntent>();
        var cut = RenderGrid(ps => ps
            .Add(g => g.TotalCount, 100)
            .Add(g => g.OnPaste, (GridPasteIntent i) => intents.Add(i)));
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, " ", ctrl: true);                  // whole column, 100 rows

        await cut.InvokeAsync(() => cut.Instance.OnPasteAsync("fill", null));

        var intent = Assert.Single(intents);
        Assert.Equal(100, intent.CellCount);
        Assert.Equal([new SelectionRange(0, 0, 100, 1)], intent.Plan.Targets);
        Assert.Equal("fill", intent.ValueFor(new CellPosition(99, 0)));
    }

    [Fact] // ADR-0014 / CP-13: the intent's cells are the selection's — never outside it
    public async Task A_paste_never_writes_outside_the_selection()
    {
        var intents = new List<GridPasteIntent>();
        PasteRefusalReason? refused = null;
        var cut = RenderGrid(ps => ps
            .Add(g => g.OnPaste, (GridPasteIntent i) => intents.Add(i))
            .Add(g => g.OnPasteRefused, (PasteRefusalReason r) => refused = r));
        await ClickCellAsync(cut, 50, 10);                       // a single cell

        // A 3×2 block onto one cell is Excel's spill — refused here (ADR-0014).
        await cut.InvokeAsync(() => cut.Instance.OnPasteAsync("a\tb\r\nc\td\r\ne\tf\r\n", null));

        Assert.Empty(intents);
        Assert.Equal(PasteRefusalReason.SingleCellTarget, refused);
    }

    [Fact] // ADR-0014: nothing tabular consults no rule and raises nothing
    public async Task An_empty_clipboard_does_nothing()
    {
        var intents = new List<GridPasteIntent>();
        PasteRefusalReason? refused = null;
        var cut = RenderGrid(ps => ps
            .Add(g => g.OnPaste, (GridPasteIntent i) => intents.Add(i))
            .Add(g => g.OnPasteRefused, (PasteRefusalReason r) => refused = r));
        await ClickCellAsync(cut, 50, 10);

        await cut.InvokeAsync(() => cut.Instance.OnPasteAsync("", ""));

        Assert.Empty(intents);
        Assert.Null(refused);
    }

    [Fact] // ADR-0005: the html flavour's precision wins over the text's display rounding
    public async Task A_paste_prefers_the_html_flavours_precision()
    {
        var intents = new List<GridPasteIntent>();
        var cut = RenderGrid(ps => ps.Add(g => g.OnPaste, (GridPasteIntent i) => intents.Add(i)));
        await ClickCellAsync(cut, 50, 10);

        await cut.InvokeAsync(() => cut.Instance.OnPasteAsync(
            "1,234.57", "<table><tr><td x:num=\"1234.56789\">1,234.57</td></tr></table>"));

        Assert.Equal("1234.56789", Assert.Single(intents).ValueFor(new CellPosition(0, 0)));
    }

    [Fact] // ADR-0050 item 10 / DC-33: the paste intent says where each field came from, through the tiling
    public async Task A_paste_intent_marks_each_field_invariant_or_shown()
    {
        var intents = new List<GridPasteIntent>();
        var cut = RenderGrid(ps => ps.Add(g => g.OnPaste, (GridPasteIntent i) => intents.Add(i)));
        await ClickCellAsync(cut, 50, 10);
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowRight", false, true, false, false, false));
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowDown", false, true, false, false, false));
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowDown", false, true, false, false, false));

        // One row of two, tiled down three rows: an x:num and a shown text.
        await cut.InvokeAsync(() => cut.Instance.OnPasteAsync(
            "1,234.50\t1,5\r\n", "<table><tr><td x:num=\"1234.5\">1,234.50</td><td x:num>1,5</td></tr></table>"));

        var intent = Assert.Single(intents);
        Assert.Equal([[PasteFieldOrigin.Invariant, PasteFieldOrigin.ShownText]], intent.Origins);
        Assert.Equal(PasteFieldOrigin.Invariant, intent.OriginFor(new CellPosition(2, 0)));
        Assert.Equal(PasteFieldOrigin.ShownText, intent.OriginFor(new CellPosition(2, 1)));
    }

    [Fact] // ADR-0050 item 10 / DC-33: a copy from this grid pastes back as invariant
    public async Task The_grids_own_copy_pastes_back_as_invariant()
    {
        var intents = new List<GridPasteIntent>();
        var cut = RenderGrid(ps => ps.Add(g => g.OnPaste, (GridPasteIntent i) => intents.Add(i)));
        await ClickCellAsync(cut, 50, 10);
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowRight", false, true, false, false, false));
        var payload = await cut.InvokeAsync(() => cut.Instance.BuildCopyPayload());

        await cut.InvokeAsync(() => cut.Instance.OnPasteAsync(payload.Text, payload.Html));

        Assert.Equal([[PasteFieldOrigin.Invariant, PasteFieldOrigin.Invariant]], Assert.Single(intents).Origins);
    }

    [Fact] // ADR-0035 / CP-16: a target covering a non-editable column refuses, and raises no intent
    public async Task A_paste_covering_a_non_editable_column_is_refused_and_raises_no_intent()
    {
        var intents = new List<GridPasteIntent>();
        PasteRefusalReason? refused = null;
        var cut = RenderGrid(ps => ps
            .Add(g => g.OnPaste, (GridPasteIntent i) => intents.Add(i))
            .Add(g => g.OnPasteRefused, (PasteRefusalReason r) => refused = r));
        await ClickCellAsync(cut, 150, 10);                      // Amount, editable
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowRight", false, true, false, false, false)); // into Locked

        await cut.InvokeAsync(() => cut.Instance.OnPasteAsync("a\tb\r\n", null));

        Assert.Empty(intents);
        Assert.Equal(PasteRefusalReason.TargetNotEditable, refused);
    }

    [Fact] // ADR-0035: the declaration is reported before the shape rules — a reselection would not help
    public async Task An_editability_refusal_outranks_the_shape_refusal()
    {
        PasteRefusalReason? refused = null;
        var cut = RenderGrid(ps => ps.Add(g => g.OnPasteRefused, (PasteRefusalReason r) => refused = r));
        await ClickCellAsync(cut, 250, 10);                      // Locked, a single cell

        // On an editable column this is Excel's spill, refused as SingleCellTarget.
        await cut.InvokeAsync(() => cut.Instance.OnPasteAsync("a\tb\r\nc\td\r\n", null));

        Assert.Equal(PasteRefusalReason.TargetNotEditable, refused);
    }

    [Fact] // ADR-0035: a selection wholly inside the editable columns is unaffected
    public async Task A_paste_inside_the_editable_columns_still_goes_through()
    {
        var intents = new List<GridPasteIntent>();
        var cut = RenderGrid(ps => ps.Add(g => g.OnPaste, (GridPasteIntent i) => intents.Add(i)));
        await ClickCellAsync(cut, 50, 10);
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowRight", false, true, false, false, false));

        await cut.InvokeAsync(() => cut.Instance.OnPasteAsync("a\tb\r\n", null));

        Assert.Equal(2, Assert.Single(intents).CellCount);
    }

    [Fact] // ADR-0050 item 3 / DC-8: declared, a block onto one cell is one intent, and the block becomes the Selection
    public async Task A_spilled_paste_raises_one_intent_and_selects_the_block()
    {
        var intents = new List<GridPasteIntent>();
        GridSelection? selection = null;
        var cut = RenderGrid(ps => ps
            .Add(g => g.PasteMaySpill, true)
            .Add(g => g.OnPaste, (GridPasteIntent i) => intents.Add(i))
            .Add(g => g.SelectionChanged, (GridSelection s) => selection = s));
        await ClickCellAsync(cut, 50, 10);                       // Book, row 0 — a single cell

        await cut.InvokeAsync(() => cut.Instance.OnPasteAsync("a\tb\r\nc\td\r\n", null));

        var intent = Assert.Single(intents);
        Assert.Equal([new SelectionRange(0, 0, 2, 2)], intent.Plan.Targets);
        Assert.Equal(4, intent.CellCount);
        Assert.Equal("d", intent.ValueFor(new CellPosition(1, 1)));
        Assert.Equal([new SelectionRange(0, 0, 2, 2)], selection!.Ranges);
        Assert.Equal(new CellPosition(0, 0), selection.Anchor);
        Assert.Equal(new CellPosition(0, 0), selection.Focus);
        Assert.Equal(4, selection.CellCount);                    // the count on display is the count written
    }

    [Fact] // ADR-0050 item 3 / DC-10: a spill past the grid's last row is refused by name, and nothing moves
    public async Task A_spill_past_the_last_row_is_refused_by_name()
    {
        var intents = new List<GridPasteIntent>();
        PasteRefusalReason? refused = null;
        GridSelection? selection = null;
        var cut = RenderGrid(ps => ps
            .Add(g => g.PasteMaySpill, true)
            .Add(g => g.OnPaste, (GridPasteIntent i) => intents.Add(i))
            .Add(g => g.OnPasteRefused, (PasteRefusalReason r) => refused = r)
            .Add(g => g.SelectionChanged, (GridSelection s) => selection = s));
        await ClickCellAsync(cut, 50, 50);                       // Book, row 2 — the last row

        await cut.InvokeAsync(() => cut.Instance.OnPasteAsync("a\r\nb\r\n", null));

        Assert.Empty(intents);
        Assert.Equal(PasteRefusalReason.SpillPastExtent, refused);
        Assert.Equal([new SelectionRange(2, 0, 1, 1)], selection!.Ranges);
    }

    [Fact] // ADR-0050 item 3 / ADR-0035 / DC-10: Editable is judged on the spilled block before anything is raised
    public async Task A_spill_covering_a_non_editable_column_is_refused()
    {
        var intents = new List<GridPasteIntent>();
        PasteRefusalReason? refused = null;
        var cut = RenderGrid(ps => ps
            .Add(g => g.PasteMaySpill, true)
            .Add(g => g.OnPaste, (GridPasteIntent i) => intents.Add(i))
            .Add(g => g.OnPasteRefused, (PasteRefusalReason r) => refused = r));
        await ClickCellAsync(cut, 150, 10);                      // Amount, editable; the block reaches Locked

        await cut.InvokeAsync(() => cut.Instance.OnPasteAsync("a\tb\r\n", null));

        Assert.Empty(intents);
        Assert.Equal(PasteRefusalReason.TargetNotEditable, refused);
    }

    [Fact] // ADR-0050 item 3 / ADR-0011: a spill whose handling moved the order places no stale block
    public async Task A_spill_whose_intent_moved_the_order_does_not_place_the_block()
    {
        GridSelection? selection = null;
        IRenderedComponent<ExGrid<TestRow>>? cut = null;
        cut = RenderGrid(ps => ps
            .Add(g => g.PasteMaySpill, true)
            .Add(g => g.OnPaste, (GridPasteIntent _) =>
                cut!.Render(p => p.Add(g => g.RowSequenceVersion, 1)))
            .Add(g => g.SelectionChanged, (GridSelection s) => selection = s));
        await ClickCellAsync(cut, 50, 10);

        await cut.InvokeAsync(() => cut.Instance.OnPasteAsync("a\tb\r\nc\td\r\n", null));

        Assert.NotEqual([new SelectionRange(0, 0, 2, 2)], selection?.Ranges ?? []);
    }

    [Fact] // ADR-0050 item 9 / DC-32: declared, the keyboard's synchronous copy asks the answer with the range and writes what it returns
    public async Task A_copy_answer_is_asked_on_the_synchronous_route_and_written_as_given()
    {
        var requests = new List<GridCopyRequest>();
        var cut = RenderGrid(ps => ps.Add(g => g.CopyAnswer, (GridCopyRequest r) =>
        {
            requests.Add(r);
            return GridCopyAnswer.Write("mine\r\n", "<table><tr><td>mine</td></tr></table>");
        }));
        await ClickCellAsync(cut, 50, 10);
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowRight", false, true, false, false, false));

        var payload = await cut.InvokeAsync(() => cut.Instance.BuildCopyPayload());

        Assert.Equal("data", payload.Kind);
        Assert.Equal("mine\r\n", payload.Text);
        Assert.Equal("<table><tr><td>mine</td></tr></table>", payload.Html);
        var request = Assert.Single(requests);
        Assert.Equal([new SelectionRange(0, 0, 1, 2)], request.Plan.Segments);
        Assert.False(request.WithHeaders);
    }

    [Fact] // ADR-0050 item 9 / DC-32: the menu's asynchronous route asks it too, with the header request, and gathers no rows
    public async Task A_copy_answer_is_asked_on_the_asynchronous_route()
    {
        var requests = new List<GridCopyRequest>();
        var gathered = 0;
        var cut = RenderGrid(ps => ps
            .Add(g => g.TotalCount, 100)
            .Add(g => g.OnCopyRowsNeeded, (RowRange _, CancellationToken _) =>
            {
                gathered++;
                return Task.FromResult<IReadOnlyList<TestRow>>([]);
            })
            .Add(g => g.CopyAnswer, (GridCopyRequest r) =>
            {
                requests.Add(r);
                return GridCopyAnswer.Write("t", "h");
            }));
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, " ", ctrl: true);                  // whole column, rows 0..99, beyond the Window

        var synchronous = await cut.InvokeAsync(() => cut.Instance.BuildCopyPayload());
        var assembled = await cut.InvokeAsync(() => cut.Instance.BuildCopyPayloadAsync(withHeaders: true));

        // Answered in process on both routes: the synchronous one no longer has to defer.
        Assert.Equal("data", synchronous.Kind);
        Assert.NotNull(assembled);
        Assert.Equal("data", assembled!.Kind);
        Assert.Equal(("t", "h"), (assembled.Text, assembled.Html));
        Assert.Equal(0, gathered);
        Assert.Equal(2, requests.Count);
        Assert.Equal([new SelectionRange(0, 0, 100, 1)], requests[1].Plan.Segments);
        Assert.True(requests[1].WithHeaders);
    }

    [Fact] // ADR-0050 item 9 / DC-32: a refusal leaves the clipboard untouched, is raised by name, and says the Consumer's sentence
    public async Task A_copy_answer_refusal_is_raised_and_announced_on_both_routes()
    {
        var refusals = new List<CopyRefusalReason>();
        var cut = RenderGrid(ps => ps
            .Add(g => g.OnCopyRefused, (CopyRefusalReason r) => refusals.Add(r))
            .Add(g => g.CopyAnswer, (GridCopyRequest _) => GridCopyAnswer.Refuse("The copy reaches cells still getting data.")));
        await ClickCellAsync(cut, 50, 10);

        var synchronous = await cut.InvokeAsync(() => cut.Instance.BuildCopyPayload());
        var assembled = await cut.InvokeAsync(() => cut.Instance.BuildCopyPayloadAsync());

        Assert.Equal("none", synchronous.Kind);
        Assert.Null(assembled);
        cut.WaitForAssertion(() =>
            Assert.Equal([CopyRefusalReason.RefusedByConsumer, CopyRefusalReason.RefusedByConsumer], refusals));
        cut.WaitForAssertion(() =>
            Assert.Equal("The copy reaches cells still getting data.", cut.Find(".ex-announce").TextContent));
    }

    [Fact] // ADR-0050 item 9 / ADR-0005: the grid's own refusals come first — an empty selection never asks the Consumer
    public async Task A_copy_the_rules_refuse_never_asks_the_answer()
    {
        var asked = 0;
        CopyRefusalReason? refused = null;
        var cut = RenderGrid(ps => ps
            .Add(g => g.OnCopyRefused, (CopyRefusalReason r) => refused = r)
            .Add(g => g.CopyAnswer, (GridCopyRequest _) =>
            {
                asked++;
                return GridCopyAnswer.Write("t", "h");
            }));

        var payload = await cut.InvokeAsync(() => cut.Instance.BuildCopyPayload());

        Assert.Equal("none", payload.Kind);
        cut.WaitForAssertion(() => Assert.Equal(CopyRefusalReason.EmptySelection, refused));
        Assert.Equal(0, asked);
    }

    [Fact] // ADR-0050 item 9: a refusal without a sentence is not an answer
    public void A_copy_refusal_needs_a_sentence()
    {
        Assert.ThrowsAny<ArgumentException>(() => GridCopyAnswer.Refuse(" "));
    }
}
