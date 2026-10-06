using System.Globalization;
using Bunit;
using ExGrid.Cells;
using ExGrid.Clipboard;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// ADR-0154: the user's write survives value changes, while target identity, validation and
/// deterministic Action delivery remain. Tests drive the public component seam.
///
/// 20px rows in a 120px Viewport (five rows painted), 350px wide: Book 0–100 and Amount 100–200,
/// both editable, and an Action Column 200–300 where a test asks for one.
/// </summary>
public class WriteRefusalTests : GridTestContext
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

    /// <summary>What the grid raised, in order of kind.</summary>
    private sealed class Heard
    {
        public List<GridEditIntent<TestRow>> Edits { get; } = [];

        /// <summary>What the Consumer does with an Edit Intent beyond hearing it, if anything.</summary>
        public Action<GridEditIntent<TestRow>>? OnEdit { get; set; }

        /// <summary>What the Consumer does with an Action beyond hearing it, if anything.</summary>
        public Action<GridActionEventArgs<TestRow>>? OnAction { get; set; }
        public List<GridPasteIntent> Pastes { get; } = [];
        public List<PasteRefusalReason> PasteRefusals { get; } = [];
        public List<GridActionEventArgs<TestRow>> Actions { get; } = [];
        public List<GridActionRefusal> ActionRefusals { get; } = [];
        public List<GridFillIntent> Fills { get; } = [];
        public List<GridClearIntent> Clears { get; } = [];
    }

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        TestRow[] rows, Heard heard, GridColumn<TestRow>[]? columns = null,
        Action<ComponentParameterCollectionBuilder<ExGrid<TestRow>>>? extra = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, rows)
              .Add(g => g.TotalCount, rows.Length)
              .Add(g => g.Columns, columns ?? Columns())
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 120)
              .Add(g => g.ViewportWidth, 350)
              .Add(g => g.OnEdit, (GridEditIntent<TestRow> i) =>
              {
                  heard.Edits.Add(i);
                  heard.OnEdit?.Invoke(i);
              })
              .Add(g => g.OnPaste, (GridPasteIntent i) =>
              {
                  heard.Pastes.Add(i);
              })
              .Add(g => g.OnPasteRefused, (PasteRefusalReason r) => heard.PasteRefusals.Add(r))
              .Add(g => g.OnAction, (GridActionEventArgs<TestRow> a) =>
              {
                  heard.Actions.Add(a);
                  heard.OnAction?.Invoke(a);
              })
              .Add(g => g.OnActionRefused, (GridActionRefusal r) => heard.ActionRefusals.Add(r))
              .Add(g => g.OnFill, (GridFillIntent i) => heard.Fills.Add(i))
              .Add(g => g.OnClear, (GridClearIntent i) => heard.Clears.Add(i));
            extra?.Invoke(ps);
        });

    /// <summary>The rows with the one at <paramref name="index"/> replaced by a new instance, as a
    /// live source hands over a changed row (ADR-0003: a new instance is the change signal).</summary>
    private static TestRow[] Changed(TestRow[] rows, int index, string? book = null, decimal? amount = null)
    {
        var next = (TestRow[])rows.Clone();
        var old = rows[index];
        next[index] = new TestRow { Book = book ?? old.Book, Amount = amount ?? old.Amount, AsOf = old.AsOf, Active = old.Active };
        return next;
    }

    private static void Push(IRenderedComponent<ExGrid<TestRow>> cut, TestRow[] rows)
        => cut.Render(ps => ps.Add(g => g.Window, rows));

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

    private static Task TypeAsync(IRenderedComponent<ExGrid<TestRow>> cut, string text)
        => cut.Find(".ex-editor").InputAsync(new ChangeEventArgs { Value = text });

    [Fact] // ADR-0154: keep the existing target, validation and operation rules.
    public async Task ADR0154_a_commit_over_a_changed_cell_uses_the_current_row()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");
        await TypeAsync(cut, "5x");
        var changed = Changed(rows, 0, book: "Moved upstream", amount: 200m);
        Push(cut, changed);
        await KeyAsync(cut, "Enter");
        var edit = Assert.Single(heard.Edits);
        Assert.Same(changed[0], edit.Row);
        Assert.Equal("5x", edit.Value);
        Assert.Empty(cut.FindAll(".ex-editor"));
    }

    [Fact] // ADR-0154: keep the existing target, validation and operation rules.
    public async Task ADR0154_a_delayed_paste_overwrites_changed_text()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var text = "Before";
        PaintedTextOf<TestRow> paintedText = (_, column, _, _) => column.Name == "Book" ? text : null;
        var cut = RenderGrid(rows, heard, extra: ps => ps.Add(g => g.PaintedText, paintedText));
        await ClickAsync(cut, 50, 10);
        var pressedOn = Paint(cut);
        text = "After";
        Push(cut, Changed(rows, 0, book: "After"));

        await cut.InvokeAsync(() => cut.Instance.OnPasteAsync("x", null, pressedOn));

        Assert.Equal("x", Assert.Single(heard.Pastes).Values[0][0]);
        Assert.Empty(heard.PasteRefusals);
    }

    [Fact] // ADR-0154: keep the existing target, validation and operation rules.
    public async Task ADR0154_a_lost_action_click_fires_with_the_current_target()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, WithAction());
        var pressedOn = Paint(cut);
        Push(cut, Changed(rows, 0, amount: 777m));

        await cut.InvokeAsync(() => cut.Instance.ActionPressTakenAt(pressedOn, row: 0, column: 2, action: 0));

        Assert.Empty(heard.ActionRefusals);
        var action = Assert.Single(heard.Actions);
        Assert.Equal(777m, action.Row.Amount);
        Assert.Equal("approve", action.ActionName);
    }

    [Fact] // ADR-0154: keep the existing target, validation and operation rules.
    public async Task ADR0154_A_change_to_another_cell_of_the_row_refuses_nothing()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");
        var changed = Changed(rows, 0, amount: 999_999m);
        Push(cut, changed);

        await KeyAsync(cut, "Enter");

        var intent = Assert.Single(heard.Edits);
        Assert.Same(changed[0], intent.Row);
    }

    [Fact] // ADR-0154: keep the existing target, validation and operation rules.
    public async Task ADR0154_The_edit_verdict_judges_the_row_as_it_is_at_the_commit()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var judged = new List<TestRow>();
        GridColumn<TestRow>[] columns =
        [
            new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true,
                validate: (row, _) =>
                {
                    judged.Add(row);
                    return row.Amount > 1000 ? EditVerdict.Reject("too large to rename") : EditVerdict.Accept;
                }),
            new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100, editable: true),
        ];
        var cut = RenderGrid(rows, heard, columns);
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");
        var changed = Changed(rows, 0, amount: 5000m);
        Push(cut, changed);

        await KeyAsync(cut, "Enter");

        Assert.Same(changed[0], Assert.Single(judged));
        Assert.Empty(heard.Edits);
        Assert.Single(cut.FindAll("input.ex-editor"));
    }

    [Fact] // ADR-0154: keep the existing target, validation and operation rules.
    public async Task ADR0154_An_action_press_on_an_unchanged_row_fires_once()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, WithAction());
        var pressedOn = Paint(cut);
        Push(cut, Changed(rows, 1, book: "Another row moved"));

        await cut.InvokeAsync(() => cut.Instance.ActionPressTakenAt(pressedOn));
        await cut.FindAll(".ex-action")[0].ClickAsync(new MouseEventArgs());

        var action = Assert.Single(heard.Actions);
        Assert.Same(rows[0], action.Row);
        Assert.Empty(heard.ActionRefusals);
    }

    [Fact] // ADR-0154: keep the existing target, validation and operation rules.
    public async Task ADR0154_Space_on_an_action_whose_row_is_not_painted_fires()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, WithAction());
        await ClickAsync(cut, 250, 10);
        await ScrollToAsync(cut.Find(".ex-scroller"), 30 * 20);
        Assert.NotEqual(0, Attribute(cut, "data-ex-first-row"));
        var pressedOn = Paint(cut);
        var changed = Changed(rows, 0, book: "Moved upstream");
        Push(cut, changed);

        await KeyAsync(cut, " ", paint: pressedOn);

        Assert.Same(changed[0], Assert.Single(heard.Actions).Row);
        Assert.Empty(heard.ActionRefusals);
    }

    [Fact] // ADR-0154: keep the existing target, validation and operation rules.
    public async Task ADR0154_A_shape_refusal_still_says_its_own_reason()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await ClickAsync(cut, 50, 50, shift: true);
        var pressedOn = Paint(cut);
        Push(cut, Changed(rows, 1, book: "Moved upstream"));

        // A 2×1 block into three rows: not a whole multiple.
        await cut.InvokeAsync(() => cut.Instance.OnPasteAsync("a\r\nb\r\n", null, pressedOn));

        Assert.Equal([PasteRefusalReason.ShapeMismatch], heard.PasteRefusals);
    }

    [Fact] // ADR-0154: keep the existing target, validation and operation rules.
    public async Task ADR0154_A_fill_drag_over_an_unchanged_target_raises_its_intent()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, extra: ps => ps.Add(g => g.ShowFillHandle, true));
        await ClickAsync(cut, 50, 10);
        await ClickAsync(cut, 50, 30, shift: true);
        await DownAsync(cut, 100, 40);
        await cut.Find(".ex-viewport").MouseMoveAsync(new MouseEventArgs { Buttons = 1, OffsetX = 50, OffsetY = 70 });
        var releasedOn = Paint(cut);
        Push(cut, Changed(rows, 3, amount: 1m));

        await cut.InvokeAsync(() => cut.Instance.PressTakenAt("mouseup", 50, 70, Attribute(cut, "data-ex-first-row"), 0,
            Attribute(cut, "data-ex-sequence"), Attribute(cut, "data-ex-layout"), releasedOn));
        await UpAsync(cut, 50, 70);

        Assert.Single(heard.Fills);
        Assert.Empty(heard.PasteRefusals);
    }

    [Fact] // ADR-0154: keep the existing target, validation and operation rules.
    public async Task ADR0154_One_enter_up_two_enter_typed_at_once_commits_both()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        var taken = Paint(cut);

        await KeyAsync(cut, "1", paint: taken);
        await KeyAsync(cut, "Enter", paint: taken);
        // The Consumer writes the 1: a new instance, painted by a render none of the keys after the
        // Enter were taken on.
        Push(cut, Changed(rows, 0, book: "1"));
        Assert.Equal(taken, Paint(cut));
        await KeyAsync(cut, "ArrowUp", paint: taken);
        await KeyAsync(cut, "2", paint: taken);
        await KeyAsync(cut, "Enter", paint: taken);

        Assert.Equal(["1", "2"], heard.Edits.Select(e => e.Value));
    }

    [Fact] // ADR-0154: keep the existing target, validation and operation rules.
    public async Task ADR0154_Five_enter_up_paste_typed_at_once_pastes()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        var taken = Paint(cut);

        await KeyAsync(cut, "5", paint: taken);
        await KeyAsync(cut, "Enter", paint: taken);
        Push(cut, Changed(rows, 0, book: "5"));
        await KeyAsync(cut, "ArrowUp", paint: taken);
        await cut.InvokeAsync(() => cut.Instance.OnPasteAsync("x", "<table><tr><td>x</td></tr></table>", taken));

        Assert.Empty(heard.PasteRefusals);
        var paste = Assert.Single(heard.Pastes);
        Assert.Equal(new CellPosition(0, 0), new CellPosition(paste.Plan.Targets[0].TopRow, paste.Plan.Targets[0].LeftColumn));
    }

    [Fact] // ADR-0154: keep the existing target, validation and operation rules.
    public async Task ADR0154_A_commit_inside_a_gather_interval_lands_with_the_newest_row()
    {
        var rows = TestRows.Many(50);
        var source = new GatheringSource(rows);
        var heard = new Heard();
        var cut = RenderSourceGrid(source, heard);
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");
        var gathered = Changed(rows, 0, amount: 999_999m);
        source.Gather(gathered);

        await KeyAsync(cut, "Enter");

        Assert.Equal(1, source.Asked);
        var intent = Assert.Single(heard.Edits);
        Assert.Same(gathered[0], intent.Row);
        Assert.Equal("5", intent.Value);
        // The newest version is painted, not only judged.
        Assert.Contains(cut.FindAll(".ex-row")[0].QuerySelectorAll("[role=gridcell]"), c => c.TextContent.Replace(",", "") == "999999");
    }

    [Fact] // ADR-0154: keep the existing target, validation and operation rules.
    public async Task ADR0154_A_gathered_change_that_moves_the_order_discards_the_edit()
    {
        var rows = TestRows.Many(50);
        var source = new GatheringSource(rows);
        var heard = new Heard();
        var discarded = new List<EditDiscardReason>();
        var cut = RenderSourceGrid(source, heard,
            extra: ps => ps.Add(g => g.OnEditDiscarded, (EditDiscardReason r) => discarded.Add(r)));
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");
        source.Gather([.. Enumerable.Reverse(rows)], version: 1);

        await KeyAsync(cut, "Enter");

        Assert.Empty(heard.Edits);
        Assert.Empty(cut.FindAll(".ex-editor"));
        Assert.Equal([EditDiscardReason.OrderChanged], discarded);
    }

    [Fact] // ADR-0154: keep the existing target, validation and operation rules.
    public async Task ADR0154_An_action_press_fires_with_the_gathered_row()
    {
        var rows = TestRows.Many(50);
        var source = new GatheringSource(rows);
        var heard = new Heard();
        var cut = RenderSourceGrid(source, heard, WithAction());
        var pressedOn = Paint(cut);
        // A change to a field no column paints: nothing the user saw moved.
        var gathered = (TestRow[])rows.Clone();
        gathered[0] = new TestRow { Book = rows[0].Book, Amount = rows[0].Amount, AsOf = new DateTime(2030, 1, 1), Active = rows[0].Active };
        source.Gather(gathered);

        await cut.InvokeAsync(() => cut.Instance.ActionPressTakenAt(pressedOn));
        await cut.FindAll(".ex-action")[0].ClickAsync(new MouseEventArgs());

        Assert.Empty(heard.ActionRefusals);
        Assert.Same(gathered[0], Assert.Single(heard.Actions).Row);
    }

    [Fact] // ADR-0154: keep the existing target, validation and operation rules.
    public async Task ADR0154_An_action_on_GridSource_From_writes_back_after_a_gathered_change()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var source = GridSource.From(rows, r => r.Book, Clock);
        var thrown = new List<Exception>();
        heard.OnAction = action =>
        {
            try
            {
                var row = action.Row;
                source.ReplaceRow(row, new TestRow { Book = row.Book, Amount = 0m, AsOf = row.AsOf, Active = row.Active });
            }
            catch (Exception error)
            {
                thrown.Add(error);
            }
        };
        var cut = RenderSourceGrid(source, heard, WithAction());
        source.Apply(new(changed: [Changed(rows, 5, amount: 55m)[5]]));
        var pressedOn = Paint(cut);
        var later = new DateTime(2030, 1, 1);
        source.Apply(new(changed: [new TestRow { Book = rows[0].Book, Amount = rows[0].Amount, AsOf = later, Active = rows[0].Active }]));
        Assert.NotEqual(later, source.Window[0].AsOf);

        await cut.InvokeAsync(() => cut.Instance.ActionPressTakenAt(pressedOn));
        await cut.FindAll(".ex-action")[0].ClickAsync(new MouseEventArgs());

        Assert.Empty(heard.ActionRefusals);
        Assert.Empty(thrown);
        Assert.Equal(later, Assert.Single(heard.Actions).Row.AsOf);
        Assert.Equal(0m, source.Window[0].Amount);
        Assert.Equal(later, source.Window[0].AsOf);
    }

    [Fact] // ADR-0154: keep the existing target, validation and operation rules.
    public async Task ADR0154_Space_on_an_action_whose_order_the_gathered_change_moved_is_refused()
    {
        var rows = TestRows.Many(50);
        var source = new GatheringSource(rows);
        var heard = new Heard();
        var cut = RenderSourceGrid(source, heard, WithAction());
        await ClickAsync(cut, 250, 10);
        var pressedOn = Paint(cut);
        // Every row a new instance, in another order.
        source.Gather([.. rows.Select(r => new TestRow { Book = r.Book, Amount = r.Amount, AsOf = r.AsOf, Active = r.Active }).Reverse()], version: 1);

        await KeyAsync(cut, " ", paint: pressedOn);

        Assert.Equal(1, source.Asked);
        Assert.Empty(heard.Actions);
        Assert.Equal(ActionRefusalReason.RenderNoLongerKept, Assert.Single(heard.ActionRefusals).Reason);
    }

    [Fact] // ADR-0154: keep the existing target, validation and operation rules.
    public async Task ADR0154_With_a_row_key_a_press_whose_row_moved_is_paired_by_key_and_fires()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, WithAction(), extra: ps => ps.Add(g => g.RowKey, ByBook));
        var pressedOn = Paint(cut);
        var moved = Moved(rows, 0, 2);
        cut.Render(ps => ps.Add(g => g.Window, moved).Add(g => g.RowSequenceVersion, 1));

        await cut.InvokeAsync(() => cut.Instance.ActionPressTakenAt(pressedOn));
        // The row's kept component, now painted third.
        await cut.FindAll(".ex-action")[2].ClickAsync(new MouseEventArgs());

        Assert.Empty(heard.ActionRefusals);
        Assert.Same(moved[2], Assert.Single(heard.Actions).Row);
    }

    [Fact] // ADR-0154: keep the existing target, validation and operation rules.
    public async Task ADR0154_Without_a_row_key_a_press_whose_row_moved_is_refused_as_no_longer_kept()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, WithAction());
        var pressedOn = Paint(cut);
        cut.Render(ps => ps.Add(g => g.Window, Moved(rows, 0, 2)).Add(g => g.RowSequenceVersion, 1));

        await cut.InvokeAsync(() => cut.Instance.ActionPressTakenAt(pressedOn));
        await cut.FindAll(".ex-action")[2].ClickAsync(new MouseEventArgs());

        Assert.Empty(heard.Actions);
        Assert.Equal(ActionRefusalReason.RenderNoLongerKept, Assert.Single(heard.ActionRefusals).Reason);
    }

    [Fact] // ADR-0154: keep the existing target, validation and operation rules.
    public async Task ADR0154_With_a_row_key_a_press_whose_row_left_the_window_is_refused()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, WithAction(), extra: ps => ps.Add(g => g.RowKey, ByBook));
        var pressedOn = Paint(cut);
        // The handler of row 0's button as it was painted, before the row leaves.
        var olderHandler = cut.FindComponents<ExGridRow<TestRow>>().Single(r => ReferenceEquals(r.Instance.Row, rows[0])).Instance.OnAction!;
        cut.Render(ps => ps.Add(g => g.Window, rows[1..]).Add(g => g.TotalCount, rows.Length - 1).Add(g => g.RowSequenceVersion, 1));

        await cut.InvokeAsync(() => cut.Instance.ActionPressTakenAt(pressedOn));
        await cut.InvokeAsync(() => olderHandler(new GridActionEventArgs<TestRow>(rows[0], "Do", "approve")));

        Assert.Empty(heard.Actions);
        Assert.Equal(ActionRefusalReason.RenderNoLongerKept, Assert.Single(heard.ActionRefusals).Reason);
    }

    [Fact] // ADR-0140/0154: equivalent Row Key delegates remain the same declaration.
    public async Task ADR0154_a_source_returning_equivalent_key_delegates_preserves_the_action_target()
    {
        var rows = TestRows.Many(50);
        var source = new GatheringSource(rows, freshKey: true);
        Assert.NotSame(source.RowKey, source.RowKey);
        Assert.Equal(source.RowKey, source.RowKey);
        var heard = new Heard();
        var cut = RenderSourceGrid(source, heard, WithAction());
        var taken = Paint(cut);
        await cut.InvokeAsync(() => cut.Instance.ActionPressTakenAt(taken, 0, 2, 0));
        var current = Changed(rows, 0, amount: 777m);
        await cut.InvokeAsync(() => { source.Gather(current); source.PublishGathered(); });
        await cut.FindAll(".ex-action")[0].ClickAsync(new MouseEventArgs());
        Assert.Empty(heard.ActionRefusals);
        Assert.Same(current[0], Assert.Single(heard.Actions).Row);
    }

    private sealed class GatheringSource(TestRow[] rows, bool freshKey = false) : IGridSource<TestRow>
    {
        private IReadOnlyList<TestRow>? _gathered;
        private int? _gatheredVersion;

        public IReadOnlyList<TestRow> Window { get; private set; } = rows;

        public Func<TestRow, object>? RowKey => freshKey ? new Func<TestRow, object>(GetKey) : null;

        private object GetKey(TestRow row) => row.Book;

        public int WindowStart => 0;

        public int? TotalCount => Window.Count;

        public bool IsLoading => false;

        public int RowSequenceVersion { get; private set; }

        public IReadOnlyList<SortSpec> Sorts => [];

        public GridFilter? Filter => null;

        public int Asked { get; private set; }

        public event Action? StateChanged;

        /// <summary>Holds <paramref name="window"/> as gathered and not yet published.</summary>
        public void Gather(IReadOnlyList<TestRow> window, int? version = null)
        {
            _gathered = window;
            _gatheredVersion = version;
        }

        public void PublishGathered()
        {
            Asked++;
            if (_gathered is not { } next)
                return;
            _gathered = null;
            Window = next;
            RowSequenceVersion = _gatheredVersion ?? RowSequenceVersion;
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

    private IRenderedComponent<ExGrid<TestRow>> RenderSourceGrid(
        IGridSource<TestRow> source, Heard heard, GridColumn<TestRow>[]? columns = null,
        Action<ComponentParameterCollectionBuilder<ExGrid<TestRow>>>? extra = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Source, source)
              .Add(g => g.Columns, columns ?? Columns())
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 120)
              .Add(g => g.ViewportWidth, 350)
              .Add(g => g.OnEdit, (GridEditIntent<TestRow> i) =>
              {
                  heard.Edits.Add(i);
                  heard.OnEdit?.Invoke(i);
              })
              .Add(g => g.OnPaste, (GridPasteIntent i) =>
              {
                  heard.Pastes.Add(i);
              })
              .Add(g => g.OnPasteRefused, (PasteRefusalReason r) => heard.PasteRefusals.Add(r))
              .Add(g => g.OnAction, (GridActionEventArgs<TestRow> a) =>
              {
                  heard.Actions.Add(a);
                  heard.OnAction?.Invoke(a);
              })
              .Add(g => g.OnActionRefused, (GridActionRefusal r) => heard.ActionRefusals.Add(r))
              .Add(g => g.OnFill, (GridFillIntent i) => heard.Fills.Add(i))
              .Add(g => g.OnClear, (GridClearIntent i) => heard.Clears.Add(i));
            extra?.Invoke(ps);
        });

    private static readonly Func<TestRow, object> ByBook = static row => row.Book;

    /// <summary>The rows with the one at <paramref name="from"/> moved to <paramref name="to"/>, as
    /// a new instance with <paramref name="amount"/> or its own values.</summary>
    private static TestRow[] Moved(TestRow[] rows, int from, int to, decimal? amount = null)
    {
        var list = rows.ToList();
        var old = list[from];
        list.RemoveAt(from);
        list.Insert(to, new TestRow { Book = old.Book, Amount = amount ?? old.Amount, AsOf = old.AsOf, Active = old.Active });
        return [.. list];
    }

    [Theory] // ADR-0154: every range-write family accepts changed source and target values.
    [InlineData("paste")]
    [InlineData("Delete")]
    [InlineData("d")]
    [InlineData("r")]
    [InlineData("Enter")]
    public async Task ADR0154_range_writes_accept_value_changes(string gesture)
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await ClickAsync(cut, 150, 30, shift: true);
        var taken = Paint(cut);
        if (gesture == "Enter") await KeyAsync(cut, "9", paint: taken);
        var current = Changed(Changed(rows, 0, book: "New source", amount: 700m), 1, book: "New target", amount: 800m);
        Push(cut, current);
        if (gesture == "paste") await cut.InvokeAsync(() => cut.Instance.OnPasteAsync("9", "<table><tr><td>9</td></tr></table>", taken));
        else await KeyAsync(cut, gesture, ctrl: gesture is "d" or "r" or "Enter", paint: taken);
        Assert.Empty(heard.PasteRefusals);
        if (gesture == "Delete") Assert.Single(heard.Clears);
        else
        {
            var intent = Assert.Single(heard.Pastes);
            Assert.Equal(0, intent.RowSequenceVersion);
            if (gesture == "d") Assert.Equal("New source", intent.Values[0][0]);
            else if (gesture == "r") Assert.Equal("New target", intent.Values[1][0]);
            else Assert.Equal("9", intent.Values[0][0]);
        }
    }

    [Fact] // ADR-0154: a handle sends ranges; it does not freeze source values.
    public async Task ADR0154_a_fill_drag_accepts_changed_source_and_target()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, extra: ps => ps.Add(g => g.ShowFillHandle, true));
        await ClickAsync(cut, 50, 10);
        await ClickAsync(cut, 50, 30, shift: true);
        await DownAsync(cut, 100, 40);
        await cut.Find(".ex-viewport").MouseMoveAsync(new MouseEventArgs { Buttons = 1, OffsetX = 50, OffsetY = 70 });
        Push(cut, Changed(Changed(rows, 0, book: "Source"), 3, book: "Target"));
        await UpAsync(cut, 50, 70);
        var fill = Assert.Single(heard.Fills);
        Assert.Equal(new SelectionRange(0, 0, 2, 1), fill.Source);
        Assert.Equal(new SelectionRange(2, 0, 2, 1), fill.Target);
        Assert.Empty(heard.PasteRefusals);
    }

    [Theory] // ADR-0154: values may change before an opening key/composition arrives.
    [InlineData("5")]
    [InlineData("F2")]
    [InlineData("composition")]
    public async Task ADR0154_delayed_editor_opening_uses_current_values(string gesture)
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        var taken = Paint(cut);
        var changed = Changed(rows, 0, book: "New value");
        Push(cut, changed);
        if (gesture == "composition") await cut.InvokeAsync(() => cut.Instance.OnKeyFieldTextAsync("かな", taken));
        else await KeyAsync(cut, gesture, paint: taken);
        await KeyAsync(cut, "Enter", paint: taken);
        var edit = Assert.Single(heard.Edits);
        Assert.Same(changed[0], edit.Row);
        Assert.Equal(gesture == "F2" ? "New value" : gesture == "composition" ? "かな" : "5", edit.Value);
    }

    [Theory] // ADR-0154: neither rows nor columns may silently become a delayed operation's target.
    [InlineData(false, "paste")]
    [InlineData(true, "paste")]
    [InlineData(false, "Delete")]
    [InlineData(true, "Delete")]
    [InlineData(false, "5")]
    [InlineData(true, "5")]
    public async Task ADR0154_an_old_address_never_writes_a_new_selection(bool columns, string gesture)
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        var taken = Paint(cut);
        if (columns) cut.Render(ps => ps.Add(g => g.Columns, Columns().Reverse().ToArray()));
        else cut.Render(ps => ps.Add(g => g.Window, rows.Reverse().ToArray()).Add(g => g.RowSequenceVersion, 1));
        await ClickAsync(cut, 50, 10); // A fresh selection must not rescue the old operation.
        if (gesture == "paste") await cut.InvokeAsync(() => cut.Instance.OnPasteAsync("x", null, taken));
        else await KeyAsync(cut, gesture, paint: taken);
        Assert.Empty(heard.Edits);
        Assert.Empty(heard.Pastes);
        Assert.Empty(heard.Clears);
        Assert.Empty(cut.FindAll(".ex-editor"));
        if (gesture != "5") Assert.Equal([PasteRefusalReason.RenderNoLongerKept], heard.PasteRefusals);
    }

    [Theory] // ADR-0154: an asynchronous source acquisition belongs to its original two-axis address.
    [InlineData(false)]
    [InlineData(true)]
    public async Task ADR0154_a_fill_source_answer_after_reorder_is_refused(bool columns)
    {
        var rows = TestRows.Many(50);
        var held = new TaskCompletionSource<IReadOnlyList<TestRow>>();
        var asked = new TaskCompletionSource();
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, extra: ps => ps.Add(g => g.OnCopyRowsNeeded, (RowRange _, CancellationToken _) => { asked.SetResult(); return held.Task; }));
        cut.Render(ps => ps.Add(g => g.Window, [rows[1]]).Add(g => g.WindowStart, 1));
        await cut.InvokeAsync(() => cut.Instance.PlaceSelectionAsync(new SelectionRange(1, 0, 1, 1), new CellPosition(1, 0), 0));
        var fill = KeyAsync(cut, "d", ctrl: true);
        await asked.Task;
        if (columns) cut.Render(ps => ps.Add(g => g.Columns, Columns().Reverse().ToArray()));
        else cut.Render(ps => ps.Add(g => g.RowSequenceVersion, 1));
        held.SetResult([rows[0]]);
        await fill;
        Assert.Empty(heard.Pastes);
        Assert.Equal([PasteRefusalReason.SourceUnavailable], heard.PasteRefusals);
    }

    [Fact] // ADR-0154 / ADR-0035: an awaited source never overrides a revoked Editable declaration.
    public async Task ADR0154_a_fill_refuses_a_target_that_became_read_only_while_acquiring_source()
    {
        var rows = TestRows.Many(50);
        var held = new TaskCompletionSource<IReadOnlyList<TestRow>>();
        var asked = new TaskCompletionSource();
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, extra: ps => ps.Add(g => g.OnCopyRowsNeeded,
            (RowRange _, CancellationToken _) => { asked.SetResult(); return held.Task; }));
        cut.Render(ps => ps.Add(g => g.Window, [rows[1]]).Add(g => g.WindowStart, 1));
        await cut.InvokeAsync(() => cut.Instance.PlaceSelectionAsync(new SelectionRange(1, 0, 1, 1), new CellPosition(1, 0), 0));
        var fill = KeyAsync(cut, "d", ctrl: true);
        await asked.Task;
        cut.Render(ps => ps.Add(g => g.Columns, [
            new GridColumn<TestRow>("Book", ColumnType.Text, r => r.Book, width: Fixed100), Columns()[1]]));
        held.SetResult([rows[0]]);
        await fill;
        Assert.Empty(heard.Pastes);
        Assert.Equal([PasteRefusalReason.TargetNotEditable], heard.PasteRefusals);
    }

    [Fact] // ADR-0154: gathered changes become the current row before the one edited field is replaced.
    public async Task ADR0154_gathered_changes_to_the_edited_and_other_cells_are_published_before_commit()
    {
        var rows = TestRows.Many(50);
        var source = GridSource.From(rows, r => r.Book, Clock);
        var heard = new Heard();
        heard.OnEdit = edit => source.ReplaceRow(edit.Row, new TestRow { Book = edit.Row.Book,
            Amount = decimal.Parse(edit.Value, CultureInfo.InvariantCulture), AsOf = edit.Row.AsOf, Active = edit.Row.Active });
        var cut = RenderSourceGrid(source, heard);
        source.Apply(new(changed: [Changed(rows, 5, amount: 55m)[5]]));
        await ClickAsync(cut, 150, 10);
        await KeyAsync(cut, "5");
        var later = new DateTime(2030, 1, 1);
        source.Apply(new(changed: [new TestRow { Book = rows[0].Book, Amount = 777m, AsOf = later }]));
        await KeyAsync(cut, "Enter");
        Assert.Equal(777m, Assert.Single(heard.Edits).Row.Amount);
        Assert.Equal(5m, source.Window[0].Amount);
        Assert.Equal(later, source.Window[0].AsOf);
    }

    [Theory] // ADR-0154: expiring Action metadata never refuses a range solely for value updates.
    [InlineData(64, false)]
    [InlineData(200, false)]
    [InlineData(64, true)]
    [InlineData(200, true)]
    public async Task ADR0154_many_value_updates_keep_the_same_address(int count, bool actions)
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, actions ? WithAction() : Columns());
        await ClickAsync(cut, 50, 10);
        var taken = Paint(cut);
        for (var i = 0; i < count; i++) { rows = Changed(rows, 0, book: i.ToString()); Push(cut, rows); }
        if (!actions) Assert.Equal(taken, Paint(cut));
        await cut.InvokeAsync(() => cut.Instance.OnPasteAsync("User", null, taken));
        Assert.Single(heard.Pastes);
        Assert.Empty(heard.PasteRefusals);
    }

    [Theory] // ADR-0154: Action dispatch survives a value change, movement by key, and a lost click.
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ADR0154_actions_resolve_the_current_row_and_deliver_once(bool keyed, bool beforeReplacement)
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, WithAction(), ps => { if (keyed) ps.Add(g => g.RowKey, ByBook); });
        var paint = Paint(cut);
        var oldHandler = cut.FindComponents<ExGridRow<TestRow>>().First().Instance.OnAction!;
        if (beforeReplacement) await cut.InvokeAsync(() => cut.Instance.ActionPressTakenAt(paint, 0, 2, 0));
        var current = keyed ? Moved(rows, 0, 2, 777m) : Changed(rows, 0, amount: 777m);
        cut.Render(ps => ps.Add(g => g.Window, current).Add(g => g.RowSequenceVersion, keyed ? 1 : 0));
        if (!beforeReplacement) await cut.InvokeAsync(() => cut.Instance.ActionPressTakenAt(paint, 0, 2, 0));
        // Replacing or moving the original button can lose its native click. Dispatch must
        // already have happened, even with a Row Key that kept the component alive.
        Assert.Single(heard.Actions);
        // Even a late callback for a core-answered click must not repeat the command.
        await cut.InvokeAsync(() => oldHandler(new(rows[0], "Do", "approve")));
        Assert.Empty(heard.ActionRefusals);
        Assert.Same(current[keyed ? 2 : 0], Assert.Single(heard.Actions).Row);
    }

    [Theory] // ADR-0154: original command names, never a replacement command at the same index.
    [InlineData(false)]
    [InlineData(true)]
    public async Task ADR0154_an_action_keeps_its_original_command_or_refuses(bool removed)
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, WithAction());
        var taken = Paint(cut);
        await cut.InvokeAsync(() => cut.Instance.ActionPressTakenAt(taken, 0, 2, 0));
        cut.Render(ps => ps.Add(g => g.Columns, [.. Columns(), GridColumn<TestRow>.ActionColumn("Do",
            removed ? [new("cancel", "Cancel")] : [new("cancel", "Cancel"), new("approve", "Approve")], width: Fixed100)]));
        // A changed declaration may replace the original event attribute even when its
        // command still exists elsewhere. Completion must not depend on a surviving click.
        if (removed) Assert.Single(heard.ActionRefusals);
        else Assert.Single(heard.Actions);
        await cut.FindAll(".ex-action")[0].ClickAsync(new MouseEventArgs());
        if (removed)
        {
            Assert.Empty(heard.Actions);
            Assert.Equal("approve", Assert.Single(heard.ActionRefusals).ActionName);
        }
        else Assert.Equal("approve", Assert.Single(heard.Actions).ActionName);
    }

    [Theory] // ADR-0154: Space names the command in its original declaration, not today's index.
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ADR0154_a_delayed_space_preserves_its_original_command(bool interactive, bool removed)
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var initial = interactive
            ? new GridAction[] { new("inspect", "Inspect"), new("approve", "Approve") }
            : [new("approve", "Approve")];
        var cut = RenderGrid(rows, heard, [.. Columns(), GridColumn<TestRow>.ActionColumn("Do", initial, width: Fixed100)]);
        await ClickAsync(cut, 250, 10);
        if (interactive)
        {
            await KeyAsync(cut, " ");
            await KeyAsync(cut, "ArrowRight");
        }
        var taken = Paint(cut);
        cut.Render(ps => ps.Add(g => g.Columns, [.. Columns(), GridColumn<TestRow>.ActionColumn("Do",
            removed ? [new("cancel", "Cancel")] : [new("approve", "Approve"), new("cancel", "Cancel")], width: Fixed100)]));
        await KeyAsync(cut, " ", paint: taken);
        if (removed)
        {
            Assert.Empty(heard.Actions);
            Assert.Equal("approve", Assert.Single(heard.ActionRefusals).ActionName);
        }
        else
        {
            Assert.Empty(heard.ActionRefusals);
            Assert.Equal("approve", Assert.Single(heard.Actions).ActionName);
        }
    }

    [Theory] // ADR-0154: asynchronous clipboard reads keep the selection from the paste event.
    [InlineData(false)]
    [InlineData(true)]
    public async Task ADR0154_a_streamed_paste_keeps_its_original_selection(bool html)
    {
        var heard = new Heard();
        var cut = RenderGrid(TestRows.Many(50), heard);
        await ClickAsync(cut, 50, 10);
        var stream = new HeldPasteStream(html ? "<table><tr><td>User</td></tr></table>" : "User");
        var paste = cut.InvokeAsync(() => cut.Instance.OnPasteStreamsAsync(html ? null : stream, html ? stream : null, Paint(cut)));
        await stream.Opened.Task;
        await ClickAsync(cut, 150, 30);
        stream.Release();
        await paste;
        Assert.Empty(heard.PasteRefusals);
        Assert.Equal([new SelectionRange(0, 0, 1, 1)], Assert.Single(heard.Pastes).Plan.Targets);
        Assert.True(stream.Disposed);
    }

    [Theory] // ADR-0154: a newer selection cannot rescue a paste's obsolete row/column order.
    [InlineData("rows")]
    [InlineData("columns")]
    [InlineData("gathered")]
    public async Task ADR0154_a_streamed_paste_refuses_its_original_order_after_it_changes(string change)
    {
        var rows = TestRows.Many(50);
        var source = new GatheringSource(rows);
        var heard = new Heard();
        var cut = change == "gathered" ? RenderSourceGrid(source, heard) : RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        var stream = new HeldPasteStream("User");
        var paste = cut.InvokeAsync(() => cut.Instance.OnPasteStreamsAsync(stream, null));
        await stream.Opened.Task;
        if (change == "columns") cut.Render(ps => ps.Add(g => g.Columns, Columns().Reverse().ToArray()));
        else if (change == "rows") cut.Render(ps => ps.Add(g => g.Window, rows.Reverse().ToArray()).Add(g => g.RowSequenceVersion, 1));
        else source.Gather(rows.Reverse().ToArray(), version: 1);
        await ClickAsync(cut, 150, 30);
        stream.Release();
        await paste;
        Assert.Empty(heard.Pastes);
        Assert.Equal([PasteRefusalReason.RenderNoLongerKept], heard.PasteRefusals);
        Assert.True(stream.Disposed);
    }

    [Fact] // ADR-0154: source-local sequence numbers do not identify a different source's rows.
    public async Task ADR0154_a_streamed_paste_never_rebinds_to_a_different_source_at_the_same_version()
    {
        var rows = TestRows.Many(50);
        var original = new GatheringSource(rows, freshKey: true);
        var replacement = new GatheringSource(Changed(rows, 0, book: "Another source's row"), freshKey: true);
        Assert.Equal(original.RowSequenceVersion, replacement.RowSequenceVersion);
        var heard = new Heard();
        var cut = RenderSourceGrid(original, heard);
        await ClickAsync(cut, 50, 10);
        var stream = new HeldPasteStream("User");
        var paste = cut.InvokeAsync(() => cut.Instance.OnPasteStreamsAsync(stream, null));
        await stream.Opened.Task;
        cut.Render(ps => ps.Add(g => g.Source, replacement));
        stream.Release();
        await paste;
        Assert.Empty(heard.Pastes);
        Assert.Equal([PasteRefusalReason.RenderNoLongerKept], heard.PasteRefusals);
        Assert.True(stream.Disposed);
    }

    private sealed class HeldPasteStream(string text) : IJSStreamReference
    {
        private readonly byte[] _bytes = System.Text.Encoding.UTF8.GetBytes(text);
        private readonly TaskCompletionSource<Stream> _stream = new();
        public TaskCompletionSource Opened { get; } = new();
        public bool Disposed { get; private set; }
        public long Length => _bytes.Length;
        public void Release() => _stream.SetResult(new MemoryStream(_bytes, writable: false));
        public ValueTask<Stream> OpenReadStreamAsync(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
        {
            Opened.SetResult();
            return new(_stream.Task);
        }
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new NotSupportedException();
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => throw new NotSupportedException();
    }

    [Fact] // ADR-0154: a removed keyed target is refused once without retaining its old payload.
    public async Task ADR0154_a_removed_action_row_reports_its_original_address()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, WithAction(), ps => ps.Add(g => g.RowKey, ByBook));
        var paint = Paint(cut);
        cut.Render(ps => ps.Add(g => g.Window, rows[1..]).Add(g => g.TotalCount, 49).Add(g => g.RowSequenceVersion, 1));
        await cut.InvokeAsync(() => cut.Instance.ActionPressTakenAt(paint, 0, 2, 0));
        Assert.Empty(heard.Actions);
        var refusal = Assert.Single(heard.ActionRefusals);
        Assert.Equal(rows[0].Book, refusal.RowKey);
        Assert.Equal(0, refusal.RowIndex);
        Assert.Equal(0, refusal.RowSequenceVersion);
        Assert.Equal("Do", refusal.ColumnName);
        Assert.Equal("approve", refusal.ActionName);
    }
}
