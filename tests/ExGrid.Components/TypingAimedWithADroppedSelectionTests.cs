using System.Globalization;
using Bunit;
using ExGrid.Cells;
using ExGrid.Clipboard;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// Typing aimed with a Selection that an order move or a replaced Source has dropped since (ADR-0142,
/// ADR-0011, ADR-0012; decided with the user 2026-10-08). A key carries the paint it was typed against
/// (<c>data-ex-paint</c>), and a composition the paint at its start. Told a paint under which the
/// Selection it was aimed with has since been dropped, a key that would open an edit — a character,
/// F2, Backspace, a composition's text — opens nothing and writes nothing anywhere: it is thrown away
/// and said through <c>OnEditDiscarded</c>, as <c>OrderMoved</c> or <c>SourceChanged</c>, once for all
/// the keys typed against that Selection. No key of the run is taken as the first key on the empty
/// Selection the drop left; a key typed after the user saw the new state, told a newer paint, keeps
/// the first-key rule.
///
/// 20px rows in a 120px Viewport, 350px wide: Book 0–100 and Amount 100–200, both editable, and an
/// Action Column 200–300 where a test asks for one.
/// </summary>
public class TypingAimedWithADroppedSelectionTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100, editable: true),
    ];

    private static GridColumn<TestRow>[] WithAction() =>
    [
        .. Columns(),
        GridColumn<TestRow>.ActionColumn("Do", [new GridAction("approve", "Approve")], width: Fixed100),
    ];

    private sealed class Heard
    {
        public List<GridEditIntent<TestRow>> Edits { get; } = [];
        public List<GridPasteIntent> Pastes { get; } = [];
        public List<GridClearIntent> Clears { get; } = [];
        public List<PasteRefusalReason> PasteRefusals { get; } = [];
        public List<EditDiscardReason> Discards { get; } = [];
        public List<bool> EditingChanges { get; } = [];
    }

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(IGridSource<TestRow> source, Heard heard, GridColumn<TestRow>[]? columns = null)
        => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, columns ?? Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 120)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.OnEdit, (GridEditIntent<TestRow> i) => heard.Edits.Add(i))
            .Add(g => g.OnPaste, (GridPasteIntent i) => heard.Pastes.Add(i))
            .Add(g => g.OnClear, (GridClearIntent i) => heard.Clears.Add(i))
            .Add(g => g.OnPasteRefused, (PasteRefusalReason r) => heard.PasteRefusals.Add(r))
            .Add(g => g.OnEditDiscarded, (EditDiscardReason r) => heard.Discards.Add(r))
            .Add(g => g.OnEditingChanged, (bool open) => heard.EditingChanges.Add(open)));

    private static TestRow[] Copies(TestRow[] rows)
        => [.. rows.Select(r => new TestRow { Book = r.Book, Amount = r.Amount, AsOf = r.AsOf, Active = r.Active })];

    private static int Paint(IRenderedComponent<ExGrid<TestRow>> cut)
        => int.Parse(cut.Find(".ex-viewport").GetAttribute("data-ex-paint")!, CultureInfo.InvariantCulture);

    private static Task KeyAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key, int paint, bool ctrl = false, bool shift = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, ctrl, shift, false, false, false, paint: paint));

    private static async Task ClickAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
    {
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });
        await cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs { Button = 0, OffsetX = x, OffsetY = y });
    }

    private static bool EditorOpen(IRenderedComponent<ExGrid<TestRow>> cut) => cut.FindAll("input.ex-editor").Count > 0;

    /// <summary>Drops the Selection the way the test asks: the order moves under a new Row Sequence
    /// Version, or the Source is replaced by another instance at the same version 0.</summary>
    private static async Task DropAsync(IRenderedComponent<ExGrid<TestRow>> cut, SwappableSource source, TestRow[] rows, bool sourceReplaced)
    {
        if (sourceReplaced)
            cut.Render(ps => ps.Add(g => g.Source, GridSource.From(Copies(rows))));
        else
            await cut.InvokeAsync(() => source.Reorder([rows[1], rows[0], .. rows[2..]]));
        Assert.True(cut.Instance.ReadSelection().Selection.IsEmpty);
    }

    private static EditDiscardReason ReasonFor(bool sourceReplaced)
        => sourceReplaced ? EditDiscardReason.SourceChanged : EditDiscardReason.OrderMoved;

    [Theory] // ADR-0142 / ADR-0011, ADR-0012 (2026-10-08): `5` `0` `0` Enter typed against a Selection the rows moving or a replaced Source dropped: nothing opens, nothing is written anywhere, the Selection stays empty, and the loss is said once
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_run_of_typing_aimed_with_a_dropped_selection_writes_nothing_and_is_said_once(bool sourceReplaced)
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var source = new SwappableSource(rows);
        var cut = RenderGrid(source, heard);
        await ClickAsync(cut, 50, 50);
        var typedOn = Paint(cut);
        await DropAsync(cut, source, rows, sourceReplaced);

        foreach (var key in new[] { "5", "0", "0", "Enter" })
            await KeyAsync(cut, key, typedOn);

        Assert.Equal([ReasonFor(sourceReplaced)], heard.Discards);
        Assert.Empty(heard.Edits);
        Assert.Empty(heard.Pastes);
        Assert.Empty(heard.Clears);
        Assert.Empty(heard.EditingChanges);
        Assert.False(EditorOpen(cut));
        Assert.True(cut.Instance.ReadSelection().Selection.IsEmpty);
    }

    [Theory] // ADR-0142 / ADR-0080, ADR-0011 (2026-10-08): a composition started against a Selection the rows moving or a replaced Source dropped opens nothing, writes nothing, and is said
    [InlineData(false)]
    [InlineData(true)]
    public async Task Ime_text_composed_against_a_dropped_selection_opens_nothing_and_is_said(bool sourceReplaced)
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var source = new SwappableSource(rows);
        var cut = RenderGrid(source, heard);
        await ClickAsync(cut, 50, 50);
        var composedOn = Paint(cut);
        await DropAsync(cut, source, rows, sourceReplaced);

        var opened = await cut.InvokeAsync(() => cut.Instance.OnKeyFieldTextAsync("かな", composedOn));
        await KeyAsync(cut, "Enter", composedOn);

        Assert.False(opened);
        Assert.Equal([ReasonFor(sourceReplaced)], heard.Discards);
        Assert.Empty(heard.Edits);
        Assert.False(EditorOpen(cut));
        Assert.True(cut.Instance.ReadSelection().Selection.IsEmpty);
    }

    [Theory] // ADR-0142 / ADR-0012 (2026-10-08): typing told the paint that shows the dropped Selection's state — typed after the user saw it — keeps the first-key rule: the editor opens on the first painted cell holding it, and commits there
    [InlineData(false)]
    [InlineData(true)]
    public async Task Typing_told_the_new_paint_opens_an_edit_by_the_first_key_rule(bool sourceReplaced)
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var source = new SwappableSource(rows);
        var cut = RenderGrid(source, heard);
        await ClickAsync(cut, 50, 50);
        await DropAsync(cut, source, rows, sourceReplaced);
        var seen = Paint(cut);

        await KeyAsync(cut, "5", seen);
        await cut.Find(".ex-editor").InputAsync(new ChangeEventArgs { Value = "500" });
        await KeyAsync(cut, "Enter", seen);

        Assert.Empty(heard.Discards);
        var edit = Assert.Single(heard.Edits);
        Assert.Equal("500", edit.Value);
        // The first painted row of the new state.
        Assert.Equal(sourceReplaced ? "Row 000000" : "Row 000001", edit.Row.Book);
    }

    [Fact] // ADR-0142 (2026-10-08): IME text composed on the new paint keeps the first-key rule too
    public async Task Ime_text_composed_on_the_new_paint_opens_an_edit_by_the_first_key_rule()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var source = new SwappableSource(rows);
        var cut = RenderGrid(source, heard);
        await ClickAsync(cut, 50, 50);
        await DropAsync(cut, source, rows, sourceReplaced: true);

        var opened = await cut.InvokeAsync(() => cut.Instance.OnKeyFieldTextAsync("かな", Paint(cut)));
        await KeyAsync(cut, "Enter", Paint(cut));

        Assert.True(opened);
        Assert.Empty(heard.Discards);
        Assert.Equal("かな", Assert.Single(heard.Edits).Value);
    }

    [Fact] // ADR-0142 (2026-10-08): the keys of the run are said once; a key typed after the user saw the new state opens its own edit, by the first-key rule
    public async Task The_run_is_said_once_and_a_key_on_the_new_paint_starts_afresh()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var source = new SwappableSource(rows);
        var cut = RenderGrid(source, heard);
        await ClickAsync(cut, 50, 50);
        var typedOn = Paint(cut);
        await DropAsync(cut, source, rows, sourceReplaced: false);

        await KeyAsync(cut, "5", typedOn);
        await KeyAsync(cut, "1", typedOn);
        await KeyAsync(cut, "F2", typedOn);
        await KeyAsync(cut, "Backspace", typedOn);
        Assert.Equal([EditDiscardReason.OrderMoved], heard.Discards);
        Assert.False(EditorOpen(cut));

        await KeyAsync(cut, "7", Paint(cut));
        await KeyAsync(cut, "Enter", Paint(cut));

        Assert.Equal([EditDiscardReason.OrderMoved], heard.Discards);
        Assert.Equal("7", Assert.Single(heard.Edits).Value);
    }

    [Theory] // ADR-0142 / ADR-0012 (2026-10-08): F2 and Backspace typed against a dropped Selection open nothing, and are said, as a character is
    [InlineData("F2")]
    [InlineData("Backspace")]
    public async Task F2_and_backspace_aimed_with_a_dropped_selection_open_nothing(string key)
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var source = new SwappableSource(rows);
        var cut = RenderGrid(source, heard);
        await ClickAsync(cut, 50, 50);
        var typedOn = Paint(cut);
        await DropAsync(cut, source, rows, sourceReplaced: false);

        await KeyAsync(cut, key, typedOn);

        Assert.Equal([EditDiscardReason.OrderMoved], heard.Discards);
        Assert.False(EditorOpen(cut));
        Assert.True(cut.Instance.ReadSelection().Selection.IsEmpty);
    }

    [Theory] // ADR-0142 / ADR-0012 (2026-10-08): a key that moves or selects, aimed with a dropped Selection, moves nothing and is not taken as a first key — the Focus it named is gone — and says nothing, for nothing was typed
    [InlineData("ArrowDown", false)]
    [InlineData("Enter", false)]
    [InlineData("Tab", false)]
    [InlineData("a", true)]
    public async Task A_key_that_moves_aimed_with_a_dropped_selection_places_no_focus(string key, bool ctrl)
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var source = new SwappableSource(rows);
        var cut = RenderGrid(source, heard);
        await ClickAsync(cut, 50, 50);
        var typedOn = Paint(cut);
        await DropAsync(cut, source, rows, sourceReplaced: true);

        await KeyAsync(cut, key, typedOn, ctrl: ctrl);

        Assert.True(cut.Instance.ReadSelection().Selection.IsEmpty);
        Assert.Empty(heard.Discards);
    }

    [Fact] // ADR-0142 / ADR-0012: a key aimed under a moved order with nothing selected was aimed with no Selection, and keeps the first-key rule
    public async Task A_key_aimed_with_nothing_selected_keeps_the_first_key_rule_across_an_order_move()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var source = new SwappableSource(rows);
        var cut = RenderGrid(source, heard);
        var typedOn = Paint(cut);
        await cut.InvokeAsync(() => source.Reorder([rows[1], rows[0], .. rows[2..]]));

        await KeyAsync(cut, "5", typedOn);

        Assert.Empty(heard.Discards);
        Assert.True(EditorOpen(cut));
    }

    [Fact] // ADR-0142 (2026-10-08): typing aimed with a dropped Selection whose Focus was on a cell that does not edit would have opened nothing: it is dropped, and nothing is said
    public async Task Typing_aimed_at_a_cell_that_does_not_edit_is_dropped_unsaid()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var source = new SwappableSource(rows);
        var cut = RenderGrid(source, heard, WithAction());
        await ClickAsync(cut, 250, 10);
        var typedOn = Paint(cut);
        await DropAsync(cut, source, rows, sourceReplaced: false);

        await KeyAsync(cut, "5", typedOn);

        Assert.Empty(heard.Discards);
        Assert.False(EditorOpen(cut));
        Assert.True(cut.Instance.ReadSelection().Selection.IsEmpty);
    }

    [Fact] // ADR-0142 (2026-10-08): an edit open as the Source is replaced is discarded and said once; the keys typed after it against the old paint are that same typing, and say nothing more
    public async Task Typing_after_an_edit_the_replacement_discarded_is_not_said_again()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var source = new SwappableSource(rows);
        var cut = RenderGrid(source, heard);
        await ClickAsync(cut, 50, 50);
        var typedOn = Paint(cut);
        await KeyAsync(cut, "5", typedOn);
        Assert.True(EditorOpen(cut));

        cut.Render(ps => ps.Add(g => g.Source, GridSource.From(Copies(rows))));
        await KeyAsync(cut, "0", typedOn);
        await KeyAsync(cut, "Enter", typedOn);

        Assert.Equal([EditDiscardReason.SourceChanged], heard.Discards);
        Assert.Empty(heard.Edits);
        Assert.False(EditorOpen(cut));
        Assert.True(cut.Instance.ReadSelection().Selection.IsEmpty);
    }

    [Fact] // ADR-0142 / ADR-0080, ADR-0021: a composition carries the paint the Viewport named as it started, read in compositionstart, and hands it over with its text
    public void The_script_carries_a_compositions_paint()
    {
        var script = AssetSources.Read("ExGrid", "ex-grid.js");

        Assert.Contains("keyFieldComposing = true;\n        keyFieldPaint = paintNow();", script);
        Assert.Contains("held.push({ text, paint: keyFieldPaint });", script);
        Assert.Contains("'OnKeyFieldTextAsync', k.text, k.paint)", script);
    }

    /// <summary>A source whose order the test moves, under a new Row Sequence Version.</summary>
    private sealed class SwappableSource(IReadOnlyList<TestRow> rows) : IGridSource<TestRow>
    {
        public IReadOnlyList<TestRow> Window { get; private set; } = rows;

        public int WindowStart => 0;

        public int? TotalCount => Window.Count;

        public bool IsLoading => false;

        public int RowSequenceVersion { get; private set; }

        public IReadOnlyList<SortSpec> Sorts => [];

        public GridFilter? Filter => null;

        public event Action? StateChanged;

        /// <summary>The rows in another order: the version moves.</summary>
        public void Reorder(IReadOnlyList<TestRow> reordered)
        {
            Window = reordered;
            RowSequenceVersion++;
            StateChanged?.Invoke();
        }

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
