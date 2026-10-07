using Bunit;
using ExGrid.Clipboard;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The Copied Range (ADR-0170): a copy's rectangles outlined once its write lands, and kept only
/// while the clipboard still holds them. The script's half — telling a landing, and a change of the
/// clipboard by anyone else, through clipboardchange — is layer 3's; what is pinned here is what
/// the grid does when told, and every end the grid sees for itself.
/// </summary>
public class CopiedRangeTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100, editable: true),
        new("Active", ColumnType.Text, r => r.Active ? "yes" : "no", width: Fixed100),
    ];

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        Action<ComponentParameterCollectionBuilder<ExGrid<TestRow>>>? extra = null, TestRow[]? window = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, window ?? TestRows.Window())
              .Add(g => g.Columns, Columns())
              .Add(g => g.RowHeight, 20)
              .Add(g => g.ViewportHeight, 100)
              .Add(g => g.ViewportWidth, 350);
            extra?.Invoke(ps);
        });

    private static Task ClickCellAsync(IRenderedComponent<ExGrid<TestRow>> cut, int row, int column, bool ctrl = false)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs
        {
            Button = 0, Buttons = 1, OffsetX = (column * 100) + 50, OffsetY = (row * 20) + 10, CtrlKey = ctrl,
        });

    private static Task PressAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key, bool ctrl = false, bool shift = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, ctrl, shift, false, false, false));

    /// <summary>A copy of the Selection by the <c>copy</c> event's route, landed as the script
    /// reports it once the event has set the data.</summary>
    private static async Task<ClipboardPayload> CopyAndLandAsync(IRenderedComponent<ExGrid<TestRow>> cut)
    {
        var payload = await cut.InvokeAsync(() => cut.Instance.BuildCopyPayload());
        Assert.Equal("data", payload.Kind);
        await cut.InvokeAsync(() => cut.Instance.OnCopyLandedAsync(payload.Landing));
        return payload;
    }

    private static IReadOnlyList<string> Outlines(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.FindAll(".ex-copied-range").Select(e => e.GetAttribute("style") ?? "").ToList();

    [Fact] // ADR-0170 / CP-26: the outline is drawn once the write lands, never when the payload is only built
    public async Task A_copy_is_outlined_once_its_write_lands()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 0, 0);
        await PressAsync(cut, "ArrowRight", shift: true);

        var payload = await cut.InvokeAsync(() => cut.Instance.BuildCopyPayload());

        Assert.True(payload.Landing > 0);
        Assert.Empty(Outlines(cut));

        await cut.InvokeAsync(() => cut.Instance.OnCopyLandedAsync(payload.Landing));

        var outline = Assert.Single(Outlines(cut));
        Assert.Contains("width: 200px", outline);
    }

    [Fact] // ADR-0170 / CP-26: an earlier build landing after a later one was built outlines nothing — the later one lands or fails on its own
    public async Task Only_the_latest_built_copy_is_outlined_when_it_lands()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 0, 0);
        var first = await cut.InvokeAsync(() => cut.Instance.BuildCopyPayload());
        await PressAsync(cut, "ArrowRight", shift: true);
        var second = await cut.InvokeAsync(() => cut.Instance.BuildCopyPayload());

        await cut.InvokeAsync(() => cut.Instance.OnCopyLandedAsync(first.Landing));
        Assert.Empty(Outlines(cut));

        await cut.InvokeAsync(() => cut.Instance.OnCopyLandedAsync(second.Landing));
        Assert.Contains("width: 200px", Assert.Single(Outlines(cut)));
    }

    [Fact] // ADR-0152 / ADR-0170 / CP-26: an asynchronous Consumer answer carries the same landing contract as a synchronous one
    public async Task An_asynchronous_consumer_copy_is_outlined_only_when_it_lands()
    {
        var answer = new TaskCompletionSource<GridCopyAnswer>();
        var cut = RenderGrid(ps => ps.Add(g => g.CopyAnswerAsync,
            (GridCopyRequest _, CancellationToken __) => answer.Task));
        await ClickCellAsync(cut, 0, 0);

        var copying = cut.InvokeAsync(() => cut.Instance.BuildCopyPayloadAsync());
        Assert.False(copying.IsCompleted);
        Assert.Empty(Outlines(cut));
        await cut.InvokeAsync(() => answer.SetResult(GridCopyAnswer.Write("Alpha", "<table><tr><td>Alpha</td></tr></table>")));
        var payload = Assert.IsType<ClipboardPayload>(await copying);

        Assert.True(payload.Landing > 0);
        Assert.Empty(Outlines(cut));
        await cut.InvokeAsync(() => cut.Instance.OnCopyLandedAsync(payload.Landing));
        Assert.Single(Outlines(cut));
    }

    [Theory] // ADR-0152 / ADR-0170 / CP-29 / CP-30: a delayed answer cannot outline changed coordinates or text as if they were copied
    [InlineData("order")]
    [InlineData("columns")]
    [InlineData("text")]
    public async Task An_asynchronous_copy_keeps_the_coordinates_and_text_it_was_asked_for(string change)
    {
        var answer = new TaskCompletionSource<GridCopyAnswer>();
        var rows = TestRows.Window();
        var cut = RenderGrid(window: rows, extra: ps => ps.Add(g => g.CopyAnswerAsync,
            (GridCopyRequest _, CancellationToken __) => answer.Task));
        await ClickCellAsync(cut, 0, 0);
        var copying = cut.InvokeAsync(() => cut.Instance.BuildCopyPayloadAsync());
        Assert.False(copying.IsCompleted);

        if (change == "order")
            cut.Render(ps => ps.Add(g => g.RowSequenceVersion, 1));
        else if (change == "columns")
            cut.Render(ps => ps.Add(g => g.Columns, Columns().Reverse().ToArray()));
        else
            cut.Render(ps => ps.Add(g => g.Window, new TestRow[] { new() { Book = "Changed" }, rows[1], rows[2] }));
        await cut.InvokeAsync(() => answer.SetResult(GridCopyAnswer.Write("Alpha", "<table><tr><td>Alpha</td></tr></table>")));
        var payload = Assert.IsType<ClipboardPayload>(await copying);

        Assert.Equal("Alpha", payload.Text);
        await cut.InvokeAsync(() => cut.Instance.OnCopyLandedAsync(payload.Landing));
        Assert.Empty(Outlines(cut));
    }

    [Fact] // ADR-0170 / CP-26: a refused copy lands nothing, carries nothing to outline, and leaves the outline the clipboard still matches
    public async Task A_refused_copy_leaves_the_outline_it_found()
    {
        var cut = RenderGrid(ps => ps.Add(g => g.CopyCellCap, 1).Add(g => g.OnCopyRefused, (CopyRefusalReason _) => { }));
        await ClickCellAsync(cut, 0, 0);
        await CopyAndLandAsync(cut);
        var before = Assert.Single(Outlines(cut));

        await PressAsync(cut, "ArrowRight", shift: true);
        var refused = await cut.InvokeAsync(() => cut.Instance.BuildCopyPayload());

        Assert.Equal("none", refused.Kind);
        Assert.Equal(0, refused.Landing);
        Assert.Equal(before, Assert.Single(Outlines(cut)));
    }

    [Fact] // ADR-0170 / CP-27: the clipboard changed by anyone but this grid's own copy — the outline goes
    public async Task A_change_of_the_clipboard_drops_the_outline()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 1, 1);
        await CopyAndLandAsync(cut);
        Assert.Single(Outlines(cut));

        await cut.InvokeAsync(() => cut.Instance.OnClipboardChangedAsync());

        Assert.Empty(Outlines(cut));
    }

    [Fact] // ADR-0170 / ADR-0012 / ADR-0070 / CP-28: Escape peels the outline as a layer of its own; only the next one releases Tab and raises OnLeave
    public async Task Escape_drops_the_outline_and_only_the_next_escape_leaves()
    {
        var left = 0;
        var cut = RenderGrid(ps => ps.Add(g => g.OnLeave, () => left++));
        await ClickCellAsync(cut, 0, 0);
        await CopyAndLandAsync(cut);

        await PressAsync(cut, "Escape");

        Assert.Empty(Outlines(cut));
        Assert.Equal(0, left);
        Assert.Equal(0, Js.TabReleases);

        await PressAsync(cut, "Escape");

        Assert.Equal(1, left);
        Assert.Equal(1, Js.TabReleases);
    }

    [Fact] // ADR-0170 / CP-29: an edit opening ends the outline, as an edit ends Excel's copy mode
    public async Task An_edit_opening_drops_the_outline()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 0, 0);
        await CopyAndLandAsync(cut);

        await PressAsync(cut, "F2");

        Assert.Single(cut.FindAll(".ex-editor"));
        Assert.Empty(Outlines(cut));
    }

    [Fact] // ADR-0170 / ADR-0011 / CP-29: a new Row Sequence Version drops the outline, as it drops the Selection
    public async Task A_new_row_sequence_drops_the_outline()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 0, 0);
        await CopyAndLandAsync(cut);

        cut.Render(ps => ps.Add(g => g.RowSequenceVersion, 1));

        Assert.Empty(Outlines(cut));
    }

    [Fact] // ADR-0170 / ADR-0011 / CP-29: a change of the visible columns drops the outline, as it drops the Selection
    public async Task A_change_of_the_columns_drops_the_outline()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 0, 0);
        await CopyAndLandAsync(cut);

        cut.Render(ps => ps.Add(g => g.Columns, Columns().Reverse().ToArray()));

        Assert.Empty(Outlines(cut));
    }

    [Fact] // ADR-0170 / ADR-0142 / CP-30: a copied cell that comes to read other text drops the outline; a new instance that reads the same, or a change outside the copied cells, keeps it
    public async Task A_copied_cell_that_reads_otherwise_drops_the_outline()
    {
        var rows = TestRows.Window();
        var cut = RenderGrid(window: rows);
        await ClickCellAsync(cut, 0, 0);
        await PressAsync(cut, "ArrowRight", shift: true);      // Book and Amount of the first row
        await CopyAndLandAsync(cut);

        // The first row again, another instance with the same Book and Amount but another Active,
        // which was not copied; and the second row changed, which was not copied either.
        TestRow[] same =
        [
            new() { Book = rows[0].Book, Amount = rows[0].Amount, Active = !rows[0].Active },
            new() { Book = "Changed", Amount = 1m },
            rows[2],
        ];
        cut.Render(ps => ps.Add(g => g.Window, same));
        Assert.Single(Outlines(cut));

        TestRow[] moved = [new() { Book = rows[0].Book, Amount = rows[0].Amount + 1 }, same[1], same[2]];
        cut.Render(ps => ps.Add(g => g.Window, moved));

        Assert.Empty(Outlines(cut));
    }

    [Fact] // ADR-0170 / CP-31: a paste into the grid leaves the outline — the clipboard still holds the copy
    public async Task A_paste_leaves_the_outline()
    {
        var intents = new List<GridPasteIntent>();
        var cut = RenderGrid(ps => ps.Add(g => g.OnPaste, (GridPasteIntent i) => intents.Add(i)));
        await ClickCellAsync(cut, 0, 0);
        await CopyAndLandAsync(cut);
        await ClickCellAsync(cut, 2, 0);

        await cut.InvokeAsync(() => cut.Instance.OnPasteAsync("Alpha", null));

        Assert.Single(intents);
        Assert.Single(Outlines(cut));
    }

    [Fact] // ADR-0170 / CP-26: each rectangle of a copied multi-range selection is outlined
    public async Task Each_rectangle_of_a_copied_multi_range_selection_is_outlined()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 0, 0);
        await ClickCellAsync(cut, 2, 0, ctrl: true);

        await CopyAndLandAsync(cut);

        Assert.Equal(2, Outlines(cut).Count);
    }

    [Fact] // ADR-0170 / ADR-0005 / CP-26 / CP-30: a copy gathered beyond the Window is outlined once it lands, and fingerprinted from the rows it gathered
    public async Task A_copy_beyond_the_window_is_outlined_from_the_rows_it_gathered()
    {
        var all = TestRows.Many(100);
        var cut = RenderGrid(window: all[..3], extra: ps => ps
            .Add(g => g.TotalCount, 100)
            .Add(g => g.OnCopyRowsNeeded, (RowRange range, CancellationToken _) =>
                Task.FromResult<IReadOnlyList<TestRow>>(all.Skip(range.Start).Take(range.Count).ToArray())));
        await ClickCellAsync(cut, 0, 0);
        await PressAsync(cut, " ", ctrl: true);                 // the whole column, beyond the Window

        var deferred = await cut.InvokeAsync(() => cut.Instance.BuildCopyPayload());
        var payload = await cut.InvokeAsync(() => cut.Instance.BuildCopyPayloadAsync());

        Assert.Equal("async", deferred.Kind);
        Assert.Equal(0, deferred.Landing);
        Assert.NotNull(payload);
        await cut.InvokeAsync(() => cut.Instance.OnCopyLandedAsync(payload!.Landing));
        Assert.Single(Outlines(cut));

        TestRow[] moved = [all[0], new() { Book = "Changed" }, all[2]];
        cut.Render(ps => ps.Add(g => g.Window, moved));

        Assert.Empty(Outlines(cut));
    }
}
