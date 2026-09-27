using Bunit;
using ExGrid.Cells;
using ExGrid.Clipboard;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// Excel's editing keys, from the component's entry point down (ADR-0007/0035/0046):
/// undo and redo forwarded, Backspace, Delete's Clear Intent, and the fill keys. Whether
/// the browser's keydown is taken is the gate's, and layer 3's; which keys the gate is
/// told to take is asserted here. 20px rows, Book (editable) and Amount (editable) then
/// Note (not editable), each 100px.
/// </summary>
public class ExcelKeyTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static GridColumn<TestRow>[] Columns(bool editable, Func<TestRow, string, EditVerdict> validate) =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: editable, validate: validate),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100, editable: editable, validate: validate,
            format: v => ((decimal)v).ToString("N0", System.Globalization.CultureInfo.InvariantCulture)),
        new("Note", ColumnType.Date, r => r.AsOf, width: Fixed100),
    ];

    private sealed class Heard
    {
        public int Undos;
        public int Redos;
        public List<GridPasteIntent> Pastes { get; } = [];
        public List<GridClearIntent> Clears { get; } = [];
        public List<PasteRefusalReason> Refusals { get; } = [];
        public List<GridEditIntent<TestRow>> Edits { get; } = [];
        public int Validations;
    }

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        Heard heard,
        bool undo = true,
        bool redo = true,
        bool editable = true,
        TestRow[]? rows = null,
        int? totalCount = null,
        Func<RowRange, CancellationToken, Task<IReadOnlyList<TestRow>>>? copyRows = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            var columns = Columns(editable, (_, _) => { heard.Validations++; return EditVerdict.Accept; });
            ps.Add(g => g.Window, rows ?? TestRows.Many(50))
              .Add(g => g.TotalCount, totalCount ?? (rows?.Length ?? 50))
              .Add(g => g.Columns, columns)
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 120)
              .Add(g => g.ViewportWidth, 350)
              .Add(g => g.OnPaste, p => heard.Pastes.Add(p))
              .Add(g => g.OnClear, c => heard.Clears.Add(c))
              .Add(g => g.OnPasteRefused, r => heard.Refusals.Add(r))
              .Add(g => g.OnEdit, e => heard.Edits.Add(e));
            if (undo)
                ps.Add(g => g.OnUndo, () => heard.Undos++);
            if (redo)
                ps.Add(g => g.OnRedo, () => heard.Redos++);
            if (copyRows is not null)
                ps.Add(g => g.OnCopyRowsNeeded, copyRows);
        });

    private static Task PressAsync(
        IRenderedComponent<ExGrid<TestRow>> cut, string key, bool ctrl = false, bool shift = false,
        bool meta = false, bool metaIsPrimary = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, ctrl, shift, false, meta, metaIsPrimary));

    private static Task ClickCellAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y, bool shift = false)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y, ShiftKey = shift });

    private static Task TypeAsync(IRenderedComponent<ExGrid<TestRow>> cut, string text)
        => cut.Find(".ex-editor").InputAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = text });

    // ---- Undo and redo (ADR-0007, KB-37) ----

    [Fact] // ADR-0007 / KB-37: Ctrl+Z raises OnUndo, and nothing about the grid changes
    public async Task Ctrl_z_is_forwarded_as_undo()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard);
        await ClickCellAsync(cut, 150, 30); // (1, 1)
        var before = cut.Markup;

        await PressAsync(cut, "z", ctrl: true);

        Assert.Equal(1, heard.Undos);
        Assert.Equal(0, heard.Redos);
        Assert.Equal(before, cut.Markup);
    }

    [Theory] // ADR-0007 / KB-37: redo answers both Excel spellings, Command folding in on a Mac
    [InlineData("y", false, false)]
    [InlineData("Z", true, false)]
    [InlineData("Z", true, true)]
    public async Task Ctrl_y_and_ctrl_shift_z_are_forwarded_as_redo(string key, bool shift, bool command)
    {
        var heard = new Heard();
        var cut = RenderGrid(heard);

        await PressAsync(cut, key, ctrl: !command, shift: shift, meta: command, metaIsPrimary: command);

        Assert.Equal(1, heard.Redos);
        Assert.Equal(0, heard.Undos);
    }

    [Fact] // ADR-0007 / KB-37: a key is taken only for someone listening
    public void The_gate_is_told_to_take_undo_only_with_a_listener()
    {
        var heard = new Heard();
        RenderGrid(heard, undo: true, redo: false);

        Assert.Contains("Control+z", Js.TakenAtAttach);
        Assert.DoesNotContain("Control+y", Js.TakenAtAttach);
        Assert.DoesNotContain("Control+Shift+Z", Js.TakenAtAttach);
    }

    [Fact] // ADR-0007: while editing, Ctrl+Z is the editor's own and reaches no Consumer
    public async Task Ctrl_z_while_editing_is_not_forwarded()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard);
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "5");

        await PressAsync(cut, "z", ctrl: true);

        Assert.Equal(0, heard.Undos);
        Assert.Single(cut.FindAll("input.ex-editor"));
    }

    // ---- Backspace (ADR-0035, ED-23) ----

    [Fact] // ADR-0035 / ED-23: Backspace opens an empty Overwrite editor, and writes nothing yet
    public async Task Backspace_opens_an_empty_editor()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard);
        await ClickCellAsync(cut, 50, 10);

        await PressAsync(cut, "Backspace");

        Assert.Equal("", cut.Find(".ex-editor").GetAttribute("value"));
        await PressAsync(cut, "Escape");
        Assert.Empty(heard.Edits);
        Assert.Empty(cut.FindAll(".ex-editor"));
    }

    [Fact] // ADR-0035 / ED-23: committed, the empty text is an ordinary edit of that one cell
    public async Task Backspace_then_enter_commits_the_empty_text()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard);
        await ClickCellAsync(cut, 50, 30); // (1, 0)

        await PressAsync(cut, "Backspace");
        await PressAsync(cut, "Enter");

        var edit = Assert.Single(heard.Edits);
        Assert.Equal("Book", edit.Column);
        Assert.Equal("", edit.Value);
    }

    [Fact] // ADR-0035 / ED-23: on a cell that does not edit, Backspace opens nothing
    public async Task Backspace_on_a_non_editable_cell_opens_nothing()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard);
        await ClickCellAsync(cut, 250, 10); // Note

        await PressAsync(cut, "Backspace");

        Assert.Empty(cut.FindAll(".ex-editor"));
    }

    // ---- Delete (ADR-0046, ED-24) ----

    [Fact] // ADR-0046 / ED-24: one Clear Intent over the whole selection, carrying no value
    public async Task Delete_raises_one_clear_intent_over_the_selection()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard);
        await ClickCellAsync(cut, 50, 10);
        await ClickCellAsync(cut, 150, 50, shift: true); // (0,0)–(2,1)

        await PressAsync(cut, "Delete");

        var clear = Assert.Single(heard.Clears);
        Assert.Equal([new SelectionRange(0, 0, 3, 2)], clear.Targets);
        Assert.Equal(6, clear.CellCount);
        Assert.Empty(heard.Pastes);
        Assert.Empty(heard.Refusals);
        Assert.Equal(0, heard.Validations);
    }

    [Fact] // ADR-0046 / ED-24: a selection covering a non-editable column is refused whole
    public async Task Delete_over_a_non_editable_column_is_refused()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard);
        await ClickCellAsync(cut, 150, 10);
        await ClickCellAsync(cut, 250, 30, shift: true); // Amount..Note

        await PressAsync(cut, "Delete");

        Assert.Empty(heard.Clears);
        Assert.Equal([PasteRefusalReason.TargetNotEditable], heard.Refusals);
    }

    [Fact] // ADR-0046 / ADR-0012: with no Focus, Delete only places one
    public async Task Delete_with_no_focus_only_places_it()
    {
        var heard = new Heard();
        GridSelection? selection = null;
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(50))
            .Add(g => g.TotalCount, 50)
            .Add(g => g.Columns, Columns(true, (_, _) => EditVerdict.Accept))
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 120)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.OnClear, c => heard.Clears.Add(c))
            .Add(g => g.SelectionChanged, s => selection = s));

        await PressAsync(cut, "Delete");

        Assert.Empty(heard.Clears);
        Assert.Equal(new CellPosition(0, 0), selection!.Focus);
    }

    [Fact] // ADR-0046 / ED-25: the gate is told to take the writing keys only on a grid that edits
    public void The_writing_keys_are_taken_only_on_a_grid_that_edits()
    {
        RenderGrid(new Heard(), editable: false);

        Assert.DoesNotContain("Delete", Js.TakenAtAttach);
        Assert.DoesNotContain("Backspace", Js.TakenAtAttach);
        Assert.DoesNotContain("Control+d", Js.TakenAtAttach);
        Assert.DoesNotContain("Control+r", Js.TakenAtAttach);
    }

    // ---- Ctrl+D and Ctrl+R (ADR-0035, CP-24/CP-25) ----

    [Fact] // ADR-0035 / CP-24: Ctrl+D writes the top row's raw values down the range, as one intent
    public async Task Ctrl_d_fills_down_with_the_raw_values()
    {
        var heard = new Heard();
        var rows = TestRows.Many(50);
        rows[2].Amount = 1234.5m; // shown as "1,235" by the column's format
        var cut = RenderGrid(heard, rows: rows);
        await ClickCellAsync(cut, 50, 50);  // (2, 0)
        await ClickCellAsync(cut, 150, 90, shift: true); // (4, 1)

        await PressAsync(cut, "d", ctrl: true);

        var paste = Assert.Single(heard.Pastes);
        Assert.Equal([new SelectionRange(3, 0, 2, 2)], paste.Plan.Targets);
        Assert.Equal("Row 000002", paste.ValueFor(new(4, 0)));
        Assert.Equal("1234.5", paste.ValueFor(new(3, 1)));
        Assert.Empty(heard.Refusals);
        Assert.Equal(0, heard.Validations);
    }

    [Fact] // ADR-0035 / CP-24: Ctrl+R on one column fills from the column to its left
    public async Task Ctrl_r_on_one_column_fills_from_the_left()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard);
        await ClickCellAsync(cut, 150, 10);  // (0, 1)
        await ClickCellAsync(cut, 150, 50, shift: true); // (2, 1)

        await PressAsync(cut, "r", ctrl: true);

        var paste = Assert.Single(heard.Pastes);
        Assert.Equal([new SelectionRange(0, 1, 3, 1)], paste.Plan.Targets);
        Assert.Equal("Row 000001", paste.ValueFor(new(1, 1)));
    }

    [Fact] // ADR-0035 / CP-25: a refusal is reported and no intent raised
    public async Task Ctrl_d_on_the_first_row_is_refused()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard);
        await ClickCellAsync(cut, 50, 10);

        await PressAsync(cut, "d", ctrl: true);

        Assert.Empty(heard.Pastes);
        Assert.Equal([PasteRefusalReason.NothingToFillFrom], heard.Refusals);
    }

    [Fact] // ADR-0035 / CP-25: source rows outside the Window are asked for, and a short answer refuses
    public async Task A_source_outside_the_window_is_asked_for_and_refused_when_short()
    {
        var heard = new Heard();
        var asked = new List<RowRange>();
        var cut = RenderGrid(heard, rows: TestRows.Many(50), totalCount: 500,
            copyRows: (range, _) => { asked.Add(range); return Task.FromResult<IReadOnlyList<TestRow>>([]); });
        await ClickCellAsync(cut, 50, 10);
        await ClickCellAsync(cut, 150, 10, shift: true);
        await PressAsync(cut, "ArrowDown", ctrl: true, shift: true); // Book..Amount, every row

        await PressAsync(cut, "r", ctrl: true);

        Assert.Equal([new RowRange(0, 500)], asked);
        Assert.Empty(heard.Pastes);
        Assert.Equal([PasteRefusalReason.SourceUnavailable], heard.Refusals);
    }
}
