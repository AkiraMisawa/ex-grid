using System.Globalization;
using Bunit;
using ExGrid.Cells;
using ExGrid.Clipboard;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// A <c>Source</c> replaced by another instance is a new binding (ADR-0011, ADR-0142): whatever the two
/// sources' Row Sequence Versions are — two fresh <c>GridSource.From</c> both start at 0 — the positions
/// the old one painted name the new one's rows. The Selection is dropped, an open edit is discarded and
/// said, and every gesture told a paint of the old source is refused as <c>SourceChanged</c>, rather
/// than landing on the new source's row at the same place. Here a test tells a gesture an earlier
/// paint, as the browser does (<c>data-ex-paint</c>); what the browser reads is layer 3's.
///
/// 20px rows in a 120px Viewport (five rows painted), 350px wide: Book 0–100 and Amount 100–200, both
/// editable, and an Action Column 200–300 where a test asks for one.
/// </summary>
public class SourceReplacedTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static readonly Func<TestRow, object> ByBook = static row => row.Book;

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

    /// <summary>What the grid raised.</summary>
    private sealed class Heard
    {
        public List<GridEditIntent<TestRow>> Edits { get; } = [];
        public List<GridCommitRefusal> CommitRefusals { get; } = [];
        public List<GridPasteIntent> Pastes { get; } = [];
        public List<PasteRefusalReason> PasteRefusals { get; } = [];
        public List<GridActionEventArgs<TestRow>> Actions { get; } = [];
        public List<GridActionRefusal<TestRow>> ActionRefusals { get; } = [];
        public List<GridFillIntent> Fills { get; } = [];
        public List<GridClearIntent> Clears { get; } = [];
        public List<GridOverwriteNotice> Notices { get; } = [];
        public List<EditDiscardReason> Discards { get; } = [];
    }

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        IGridSource<TestRow> source, Heard heard, GridColumn<TestRow>[]? columns = null,
        Action<ComponentParameterCollectionBuilder<ExGrid<TestRow>>>? extra = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Source, source)
              .Add(g => g.Columns, columns ?? Columns())
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 120)
              .Add(g => g.ViewportWidth, 350)
              .Add(g => g.OnEdit, (GridEditIntent<TestRow> i) => heard.Edits.Add(i))
              .Add(g => g.OnCommitRefused, (GridCommitRefusal r) => heard.CommitRefusals.Add(r))
              .Add(g => g.OnOverwriteNotice, (GridOverwriteNotice n) => heard.Notices.Add(n))
              .Add(g => g.OnEditDiscarded, (EditDiscardReason r) => heard.Discards.Add(r))
              .Add(g => g.OnPaste, (GridPasteIntent i) => heard.Pastes.Add(i))
              .Add(g => g.OnPasteRefused, (PasteRefusalReason r) => heard.PasteRefusals.Add(r))
              .Add(g => g.OnAction, (GridActionEventArgs<TestRow> a) => heard.Actions.Add(a))
              .Add(g => g.OnActionRefused, (GridActionRefusal<TestRow> r) => heard.ActionRefusals.Add(r))
              .Add(g => g.OnFill, (GridFillIntent i) => heard.Fills.Add(i))
              .Add(g => g.OnClear, (GridClearIntent i) => heard.Clears.Add(i));
            extra?.Invoke(ps);
        });

    /// <summary>New instances of <paramref name="rows"/>, with the same values: another source's rows.</summary>
    private static TestRow[] Copies(TestRow[] rows)
        => [.. rows.Select(r => new TestRow { Book = r.Book, Amount = r.Amount, AsOf = r.AsOf, Active = r.Active })];

    /// <summary>The Consumer hands the grid another Source instance.</summary>
    private static void Replace(IRenderedComponent<ExGrid<TestRow>> cut, IGridSource<TestRow> source)
        => cut.Render(ps => ps.Add(g => g.Source, source));

    private static int Attribute(IRenderedComponent<ExGrid<TestRow>> cut, string name)
        => int.Parse(cut.Find(".ex-viewport").GetAttribute(name)!, CultureInfo.InvariantCulture);

    /// <summary>The render the Viewport says it was painted by.</summary>
    private static int Paint(IRenderedComponent<ExGrid<TestRow>> cut) => Attribute(cut, "data-ex-paint");

    private static Task KeyAsync(
        IRenderedComponent<ExGrid<TestRow>> cut, string key, bool ctrl = false, bool shift = false, int paint = -1)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, ctrl, shift, false, false, false, paint: paint));

    private static Task DownAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y, bool shift = false)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y, ShiftKey = shift });

    private static Task UpAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs { Button = 0, OffsetX = x, OffsetY = y });

    private static async Task ClickAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y, bool shift = false)
    {
        await DownAsync(cut, x, y, shift);
        await UpAsync(cut, x, y);
    }

    private static Task PasteAsync(IRenderedComponent<ExGrid<TestRow>> cut, int paint)
        => cut.InvokeAsync(() => cut.Instance.OnPasteAsync("x", "<table><tr><td>x</td></tr></table>", paint));

    private static bool EditorOpen(IRenderedComponent<ExGrid<TestRow>> cut) => cut.FindAll("input.ex-editor").Count > 0;

    // ---- The premise, and the Selection ----

    [Fact] // ADR-0142 / ADR-0011: two fresh sources both report Row Sequence Version 0, so the version alone cannot tell a replaced Source
    public void Two_fresh_sources_both_start_at_version_zero()
    {
        var rows = TestRows.Many(50);

        Assert.Equal(0, GridSource.From(rows).RowSequenceVersion);
        Assert.Equal(0, GridSource.From(Copies(rows)).RowSequenceVersion);
        Assert.Equal(0, GridSource.From(rows, ByBook, Clock).RowSequenceVersion);
    }

    [Fact] // ADR-0011 / ADR-0142: a replaced Source drops the Selection, as an order move does, though both sources are at version 0
    public async Task A_replaced_source_drops_the_selection_whatever_the_versions_are()
    {
        var rows = TestRows.Many(50);
        var cut = RenderGrid(GridSource.From(rows), new Heard());
        await ClickAsync(cut, 50, 30);
        Assert.False(cut.Instance.ReadSelection().Selection.IsEmpty);

        Replace(cut, GridSource.From(Copies(rows)));

        Assert.True(cut.Instance.ReadSelection().Selection.IsEmpty);
        Assert.Equal(0, cut.Instance.ReadSelection().RowSequenceVersion);
    }

    [Fact] // ADR-0142 / ADR-0011: the same Source handed again is no replacement, and keeps the Selection
    public async Task The_same_source_handed_again_keeps_the_selection()
    {
        var rows = TestRows.Many(50);
        var source = GridSource.From(rows);
        var cut = RenderGrid(source, new Heard());
        await ClickAsync(cut, 50, 30);

        Replace(cut, source);

        Assert.False(cut.Instance.ReadSelection().Selection.IsEmpty);
    }

    [Fact] // ADR-0142 / ED-31: a press on the rows taken on what the replaced Source painted lands on no cell
    public async Task A_press_taken_on_the_replaced_sources_rows_lands_on_no_cell()
    {
        var rows = TestRows.Many(50);
        var cut = RenderGrid(GridSource.From(rows), new Heard());
        var sequence = Attribute(cut, "data-ex-sequence");
        var layout = Attribute(cut, "data-ex-layout");
        Replace(cut, GridSource.From(Copies(rows)));

        await cut.InvokeAsync(() => cut.Instance.PressTakenAt("mousedown", 50, 30, 0, 0, sequence, layout));
        await DownAsync(cut, 50, 30);

        Assert.True(cut.Instance.ReadSelection().Selection.IsEmpty);
    }

    // ---- Writes aimed at what the replaced Source painted ----

    [Fact] // ADR-0142 / ADR-0011: a paste taken on the replaced Source's paint is refused as SourceChanged, and writes nothing into the new source — both sources at version 0
    public async Task A_paste_aimed_at_the_replaced_source_is_refused_as_source_changed()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var old = GridSource.From(rows);
        var cut = RenderGrid(old, heard);
        await ClickAsync(cut, 50, 30);
        var pressedOn = Paint(cut);
        var replacement = GridSource.From(Copies(rows));
        Assert.Equal(old.RowSequenceVersion, replacement.RowSequenceVersion);
        Replace(cut, replacement);

        await PasteAsync(cut, pressedOn);

        Assert.Empty(heard.Pastes);
        Assert.Equal([PasteRefusalReason.SourceChanged], heard.PasteRefusals);
        Assert.True(cut.Instance.ReadSelection().Selection.IsEmpty);
    }

    [Theory] // ADR-0142 / ADR-0011, ADR-0012: Delete, Ctrl+D and Ctrl+R taken on the replaced Source's paint are refused as SourceChanged, and not taken as a first key on the empty Selection the replacement left
    [InlineData("Delete", false)]
    [InlineData("d", true)]
    [InlineData("r", true)]
    public async Task A_write_key_aimed_at_the_replaced_source_is_refused_as_source_changed(string key, bool ctrl)
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(GridSource.From(rows), heard);
        await ClickAsync(cut, 50, 30);
        await ClickAsync(cut, 150, 50, shift: true);
        var pressedOn = Paint(cut);
        Replace(cut, GridSource.From(Copies(rows)));

        await KeyAsync(cut, key, ctrl: ctrl, paint: pressedOn);

        Assert.Empty(heard.Pastes);
        Assert.Empty(heard.Clears);
        Assert.Equal([PasteRefusalReason.SourceChanged], heard.PasteRefusals);
        Assert.True(cut.Instance.ReadSelection().Selection.IsEmpty);
    }

    [Fact] // ADR-0142: once the user selects again under the new Source, a write taken on its paint lands
    public async Task A_write_taken_on_the_new_sources_paint_lands()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(GridSource.From(rows), heard);
        Replace(cut, GridSource.From(Copies(rows)));
        await ClickAsync(cut, 50, 30);

        await KeyAsync(cut, "Delete", paint: Paint(cut));

        Assert.Single(heard.Clears);
        Assert.Empty(heard.PasteRefusals);
    }

    [Fact] // ADR-0142 / ADR-0035: Ctrl+D whose source row was being read from the Source when it was replaced is refused as SourceChanged, never filled from either source
    public async Task A_fill_key_whose_rows_were_read_from_the_replaced_source_is_refused()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        // Row 0 is outside the Window, so Ctrl+D on row 1 asks the source for it.
        var old = new PendingRowsSource(rows[1..], windowStart: 1, total: rows.Length);
        var cut = RenderGrid(old, heard);
        await ClickAsync(cut, 50, 30);
        var pressedOn = Paint(cut);

        var fill = KeyAsync(cut, "d", ctrl: true, paint: pressedOn);
        Assert.NotNull(old.Asked);
        Replace(cut, GridSource.From(Copies(rows)));
        await cut.InvokeAsync(() => old.Asked!.SetResult([rows[0]]));
        await fill;

        Assert.Empty(heard.Pastes);
        Assert.Equal([PasteRefusalReason.SourceChanged], heard.PasteRefusals);
    }

    [Fact] // ADR-0142 / ADR-0050 item 5: a fill-handle drag released after the Source was replaced raises no intent, and is refused as SourceChanged
    public async Task A_fill_drag_released_after_the_source_was_replaced_is_refused_as_source_changed()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(GridSource.From(rows), heard, extra: ps => ps.Add(g => g.ShowFillHandle, true));
        // Book rows 0 and 1, and the handle at their corner dragged down to row 3.
        await ClickAsync(cut, 50, 10);
        await ClickAsync(cut, 50, 30, shift: true);
        await DownAsync(cut, 100, 40);
        await cut.Find(".ex-viewport").MouseMoveAsync(new MouseEventArgs { Buttons = 1, OffsetX = 50, OffsetY = 70 });
        var sequence = Attribute(cut, "data-ex-sequence");
        var layout = Attribute(cut, "data-ex-layout");
        Replace(cut, GridSource.From(Copies(rows)));

        await cut.InvokeAsync(() => cut.Instance.PressTakenAt("mouseup", 50, 70, 0, 0, sequence, layout));
        await UpAsync(cut, 50, 70);

        Assert.Empty(heard.Fills);
        Assert.Equal([PasteRefusalReason.SourceChanged], heard.PasteRefusals);
    }

    // ---- An open edit ----

    [Theory] // ADR-0142 / ADR-0011: an open Cell Editor is discarded when the Source is replaced, said as SourceChanged, and raises no Edit Intent — with a Row Key it does not follow its key into the new source
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_open_editor_is_discarded_when_the_source_is_replaced(bool rowKey)
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rowKey ? GridSource.From(rows, ByBook, Clock) : GridSource.From(rows), heard);
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");
        Assert.True(EditorOpen(cut));

        Replace(cut, rowKey ? GridSource.From(Copies(rows), ByBook, Clock) : GridSource.From(Copies(rows)));

        Assert.Equal([EditDiscardReason.SourceChanged], heard.Discards);
        Assert.False(EditorOpen(cut));
        // Nothing is left to commit: Enter writes nothing into the new source.
        await KeyAsync(cut, "Enter", paint: Paint(cut));
        Assert.Empty(heard.Edits);
        Assert.Empty(heard.Notices);
        Assert.Empty(heard.CommitRefusals);
    }

    [Fact] // ADR-0142 / ADR-0051: an open Formula Bar edit is discarded the same way
    public async Task An_open_formula_bar_edit_is_discarded_when_the_source_is_replaced()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(GridSource.From(rows), heard, extra: ps => ps.Add(g => g.ShowFormulaBar, true));
        await ClickAsync(cut, 50, 10);
        await cut.Find(".ex-formula-bar-text").FocusAsync(new FocusEventArgs());
        await cut.Find(".ex-formula-bar-text").InputAsync(new ChangeEventArgs { Value = "Typed in the bar" });

        Replace(cut, GridSource.From(Copies(rows)));

        Assert.Equal([EditDiscardReason.SourceChanged], heard.Discards);
        await KeyAsync(cut, "Enter", paint: Paint(cut));
        Assert.Empty(heard.Edits);
    }

    [Fact] // ADR-0142 / ADR-0007: a Ctrl+Enter fill typed before the Source was replaced fills nothing: its editor went with the source, said as SourceChanged
    public async Task A_ctrl_enter_fill_across_a_replaced_source_writes_nothing()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(GridSource.From(rows), heard);
        await ClickAsync(cut, 50, 10);
        await ClickAsync(cut, 50, 50, shift: true);
        await KeyAsync(cut, "5");
        var typedOn = Paint(cut);

        Replace(cut, GridSource.From(Copies(rows)));
        await KeyAsync(cut, "Enter", ctrl: true, paint: typedOn);

        Assert.Equal([EditDiscardReason.SourceChanged], heard.Discards);
        Assert.Empty(heard.Pastes);
        Assert.Empty(heard.Edits);
    }

    // ---- Keys and IME text aimed with the Selection the replacement dropped ----

    [Theory] // ADR-0142 / ADR-0012: a printable key taken on the replaced Source's paint is not dropped: as after an order move, the first key places the Focus on the first painted cell and opens the editor there holding it, so the keys after it commit the whole of what was typed, never its tail
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_typed_key_aimed_with_the_dropped_selection_behaves_as_after_an_order_move(bool sourceReplaced)
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var source = new SwappableSource(rows);
        var cut = RenderGrid(source, heard);
        await ClickAsync(cut, 50, 50);
        var typedOn = Paint(cut);
        if (sourceReplaced)
            Replace(cut, GridSource.From(Copies(rows)));
        else
            await cut.InvokeAsync(() => source.Reorder([rows[1], rows[0], .. rows[2..]]));
        Assert.True(cut.Instance.ReadSelection().Selection.IsEmpty);

        await KeyAsync(cut, "5", paint: typedOn);
        await cut.Find(".ex-editor").InputAsync(new ChangeEventArgs { Value = "500" });
        await KeyAsync(cut, "Enter", paint: Paint(cut));

        var edit = Assert.Single(heard.Edits);
        Assert.Equal("500", edit.Value);
        Assert.Equal(sourceReplaced ? "Row 000000" : "Row 000001", edit.Row.Book);
        Assert.Empty(heard.PasteRefusals);
    }

    [Fact] // ADR-0142 / ADR-0080, ADR-0012: IME text ending after the Source was replaced opens the editor as a typed key does, on the first painted cell, holding the whole text
    public async Task Ime_text_after_the_source_was_replaced_opens_the_editor_holding_it()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(GridSource.From(rows), heard);
        await ClickAsync(cut, 50, 50);
        Replace(cut, GridSource.From(Copies(rows)));

        await cut.InvokeAsync(() => cut.Instance.OnKeyFieldTextAsync("かな"));
        await KeyAsync(cut, "Enter", paint: Paint(cut));

        var edit = Assert.Single(heard.Edits);
        Assert.Equal("かな", edit.Value);
        // On the new source's first painted row, as a first key places it: not the row the
        // Selection the replacement dropped stood on (row 2).
        Assert.Equal("Row 000000", edit.Row.Book);
    }

    // ---- An Action press ----

    [Theory] // ADR-0142 / LV-12: a pointer press on an Action taken on the replaced Source's paint is refused as SourceChanged naming no row, though the button's component survived holding the new source's row — with a Row Key, and without one over the same instances
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_pointer_press_on_an_action_aimed_at_the_replaced_source_is_refused(bool rowKey)
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rowKey ? GridSource.From(rows, ByBook, Clock) : GridSource.From(rows), heard, WithAction());
        var pressedOn = Paint(cut);
        await cut.InvokeAsync(() => cut.Instance.ActionPressTakenAt(pressedOn, row: 0, column: 2, action: 0));

        // With a Row Key the new source's row comes under the same key; without one the new source
        // holds the very same instances. Either way the row component, and its button, survive.
        Replace(cut, rowKey ? GridSource.From(Copies(rows), ByBook, Clock) : GridSource.From(rows));
        Assert.Empty(heard.ActionRefusals);
        await cut.FindAll(".ex-action")[0].ClickAsync(new MouseEventArgs());

        Assert.Empty(heard.Actions);
        var refusal = Assert.Single(heard.ActionRefusals);
        Assert.Equal(ActionRefusalReason.SourceChanged, refusal.Reason);
        Assert.Equal("approve", refusal.ActionName);
        Assert.Null(refusal.Row);
    }

    [Fact] // ADR-0142 / LV-12, "No press is lost to Blazor": a told press whose row component the replacement disposed is answered by the core, refused as SourceChanged
    public async Task A_told_press_whose_component_the_replacement_disposed_is_refused()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(GridSource.From(rows), heard, WithAction());
        var pressedOn = Paint(cut);
        await cut.InvokeAsync(() => cut.Instance.ActionPressTakenAt(pressedOn, row: 0, column: 2, action: 0));

        Replace(cut, GridSource.From(Copies(rows)));

        cut.WaitForAssertion(() => Assert.Equal(ActionRefusalReason.SourceChanged, Assert.Single(heard.ActionRefusals).Reason));
        Assert.Empty(heard.Actions);
    }

    [Theory] // ADR-0142 / LV-12, ADR-0037: Space taken on the replaced Source's paint is refused as SourceChanged naming no row, with a Row Key or without, and not taken as a first key
    [InlineData(false)]
    [InlineData(true)]
    public async Task Space_aimed_at_the_replaced_source_is_refused(bool rowKey)
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rowKey ? GridSource.From(rows, ByBook, Clock) : GridSource.From(rows), heard, WithAction());
        await ClickAsync(cut, 250, 10);
        var pressedOn = Paint(cut);
        Replace(cut, rowKey ? GridSource.From(Copies(rows), ByBook, Clock) : GridSource.From(rows));

        await KeyAsync(cut, " ", paint: pressedOn);

        Assert.Empty(heard.Actions);
        var refusal = Assert.Single(heard.ActionRefusals);
        Assert.Equal(ActionRefusalReason.SourceChanged, refusal.Reason);
        Assert.Null(refusal.Row);
        Assert.True(cut.Instance.ReadSelection().Selection.IsEmpty);
    }

    [Fact] // ADR-0142 / LV-12: a press taken on the new Source's paint acts on its row
    public async Task A_press_taken_on_the_new_sources_paint_acts()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(GridSource.From(rows), heard, WithAction());
        var copies = Copies(rows);
        Replace(cut, GridSource.From(copies));

        await cut.InvokeAsync(() => cut.Instance.ActionPressTakenAt(Paint(cut), row: 0, column: 2, action: 0));
        await cut.FindAll(".ex-action")[0].ClickAsync(new MouseEventArgs());

        Assert.Empty(heard.ActionRefusals);
        Assert.Same(copies[0], Assert.Single(heard.Actions).Row);
    }

    /// <summary>A source whose Window starts past row 0, and which answers a request for rows outside
    /// it only when the test completes <see cref="Asked"/>: a server still answering.</summary>
    private sealed class PendingRowsSource(IReadOnlyList<TestRow> window, int windowStart, int total) : IGridSource<TestRow>
    {
        public IReadOnlyList<TestRow> Window => window;

        public int WindowStart => windowStart;

        public int? TotalCount => total;

        public bool IsLoading => false;

        public int RowSequenceVersion => 0;

        public IReadOnlyList<SortSpec> Sorts => [];

        public GridFilter? Filter => null;

        /// <summary>The request for rows outside the Window, once asked.</summary>
        public TaskCompletionSource<IReadOnlyList<TestRow>>? Asked { get; private set; }

        public event Action? StateChanged
        {
            add { }
            remove { }
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
            => (Asked = new TaskCompletionSource<IReadOnlyList<TestRow>>()).Task;

        public Task<Chrome.DistinctValues> GetDistinctValuesAsync(string column, CancellationToken cancellationToken)
            => Task.FromResult(Chrome.DistinctValues.Of([]));
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
