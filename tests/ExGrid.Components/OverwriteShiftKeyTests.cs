using Bunit;
using ExGrid.Cells;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// Overwrite is Excel's Enter mode, and the Shift+arrows and Shift+Home mean there what they mean
/// with no edit open (ADR-0010, "Two editing states"; ED-2): the seventeenth Windows run saw Excel
/// commit an entry typed into a cell on Shift+Home and extend the Selection (Part C, case 7), where
/// ExGrid selected the editor's text. Where a Reference can go they point, as Point does with them;
/// elsewhere they commit and extend, a Reject holding the commit as it does for the arrows
/// (ADR-0034). Caret keeps them the editor's, and Shift+End stays the editor's until Excel is asked.
/// The gate's half is read from the shipped script here; real keys are layer 3's. Three editable
/// columns of 100px, 20px rows.
/// </summary>
public class OverwriteShiftKeyTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static GridColumn<TestRow>[] Columns(Func<TestRow, string, EditVerdict>? validate = null) =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true, validate: validate),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100, editable: true, validate: validate),
        new("Desk", ColumnType.Text, r => r.Book, width: Fixed100, editable: true, validate: validate),
    ];

    private static bool PointAt(string text, int caret)
        => text.StartsWith('=') && caret > 0 && caret <= text.Length && "=+-*/(,".Contains(text[caret - 1]);

    private static string ReferenceText(SelectionRange range)
    {
        static string Cell(int row, int column) => FormattableString.Invariant($"{(char)('A' + column)}{row + 1}");
        var first = Cell(range.TopRow, range.LeftColumn);
        return range.RowCount == 1 && range.ColumnCount == 1
            ? first
            : first + ":" + Cell(range.BottomRow, range.RightColumn);
    }

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        List<GridEditIntent<TestRow>> edits,
        Action<GridSelection> selectionChanged,
        Func<TestRow, string, EditVerdict>? validate = null,
        bool point = false)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(50))
              .Add(g => g.TotalCount, 50)
              .Add(g => g.Columns, Columns(validate))
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 200)
              .Add(g => g.ViewportWidth, 350)
              .Add(g => g.OnEdit, edits.Add)
              .Add(g => g.SelectionChanged, selectionChanged);
            if (point)
                ps.Add(g => g.PointAt, PointAt).Add(g => g.ReferenceText, ReferenceText);
        });

    private static Task ClickCellAsync(IRenderedComponent<ExGrid<TestRow>> cut, int row, int column)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs
        {
            Button = 0, Buttons = 1, OffsetX = column * 100 + 50, OffsetY = row * 20 + 10,
        });

    /// <summary>A key as the capture listener forwards it while editing, with the editor's text and caret.</summary>
    private static Task PressAsync(
        IRenderedComponent<ExGrid<TestRow>> cut, string key, bool shift = false, string? text = null, int caret = -1)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(
            key, false, shift, false, false, false, editorText: text, editorCaret: caret, editorSelectionEnd: caret));

    /// <summary>Types <paramref name="typed"/> onto the selected cell: Overwrite, the caret at the end.</summary>
    private static async Task TypeAsync(IRenderedComponent<ExGrid<TestRow>> cut, string typed)
    {
        await PressAsync(cut, typed[..1]);
        if (typed.Length > 1)
            await cut.Find(".ex-viewport .ex-editor").InputAsync(new ChangeEventArgs { Value = typed });
    }

    [Theory] // ADR-0010 / ED-2: in Overwrite a Shift+arrow commits the entry and extends the Selection from the edited cell, as Excel's Enter mode does (the seventeenth Windows run, Part C, case 7)
    [InlineData("ArrowRight", 2, 2)]
    [InlineData("ArrowLeft", 2, 0)]
    [InlineData("ArrowUp", 1, 1)]
    [InlineData("ArrowDown", 3, 1)]
    public async Task ED2_in_overwrite_a_shift_arrow_commits_and_extends(string arrow, int extentRow, int extentColumn)
    {
        var edits = new List<GridEditIntent<TestRow>>();
        GridSelection? selection = null;
        var cut = RenderGrid(edits, s => selection = s);
        await ClickCellAsync(cut, 2, 1);
        await TypeAsync(cut, "75");

        await PressAsync(cut, arrow, shift: true, text: "75", caret: 2);

        var edit = Assert.Single(edits);
        Assert.Equal(("Amount", "75"), (edit.Column, edit.Value));
        Assert.Empty(cut.FindAll(".ex-editor"));
        Assert.Equal(new CellPosition(2, 1), selection!.Focus);
        Assert.Equal(new CellPosition(extentRow, extentColumn), selection.Extent);
    }

    [Fact] // ADR-0010 / ED-2: in Overwrite Shift+Home commits and extends the Selection to the row's first column; a Shift+→ after it moves the Extent back (the seventeenth Windows run, Part C, case 7: B10:D10)
    public async Task ED2_in_overwrite_shift_home_commits_and_extends_to_the_rows_first_column()
    {
        var edits = new List<GridEditIntent<TestRow>>();
        GridSelection? selection = null;
        var cut = RenderGrid(edits, s => selection = s);
        await ClickCellAsync(cut, 2, 2);
        await TypeAsync(cut, "abc");

        await PressAsync(cut, "Home", shift: true, text: "abc", caret: 3);

        Assert.Equal(("Desk", "abc"), (Assert.Single(edits).Column, edits[0].Value));
        Assert.Empty(cut.FindAll(".ex-editor"));
        Assert.Equal(new CellPosition(2, 2), selection!.Focus);
        Assert.Equal(new CellPosition(2, 0), selection.Extent);

        await PressAsync(cut, "ArrowRight", shift: true);

        Assert.Equal(new SelectionRange(2, 1, 1, 2), selection!.FocusRange);
    }

    [Theory] // ADR-0010 / ED-2: at the edge the key extends nothing, and the commit still lands and the editor goes — Shift+Home in the first column, Shift+↑ in the first row
    [InlineData("Home")]
    [InlineData("ArrowUp")]
    public async Task ED2_at_the_edge_the_commit_lands_and_nothing_extends(string key)
    {
        var edits = new List<GridEditIntent<TestRow>>();
        GridSelection? selection = null;
        var cut = RenderGrid(edits, s => selection = s);
        await ClickCellAsync(cut, 0, 0);
        await TypeAsync(cut, "x");

        await PressAsync(cut, key, shift: true, text: "x", caret: 1);

        Assert.Equal(("Book", "x"), (Assert.Single(edits).Column, edits[0].Value));
        Assert.Empty(cut.FindAll(".ex-editor"));
        Assert.Equal(new SelectionRange(0, 0, 1, 1), selection!.FocusRange);
    }

    [Theory] // ADR-0034 / ADR-0010 / ED-2 / ED-15: a Reject holds the commit a Shift+arrow or Shift+Home asks for, and the key keeps no meaning of its own
    [InlineData("ArrowRight")]
    [InlineData("ArrowDown")]
    [InlineData("Home")]
    public async Task ED2_a_reject_holds_a_shift_keys_commit(string key)
    {
        var edits = new List<GridEditIntent<TestRow>>();
        GridSelection? selection = null;
        var cut = RenderGrid(edits, s => selection = s, validate: (_, _) => EditVerdict.Reject("no"));
        await ClickCellAsync(cut, 2, 1);
        var before = selection;
        await TypeAsync(cut, "abc");

        await PressAsync(cut, key, shift: true, text: "abc", caret: 3);

        Assert.Empty(edits);
        Assert.Equal("abc", cut.Find(".ex-viewport .ex-editor").GetAttribute("value"));
        Assert.Equal("true", cut.Find(".ex-viewport .ex-editor").GetAttribute("aria-invalid"));
        Assert.Same(before, selection);
    }

    [Theory] // ADR-0142 / ADR-0011, ADR-0010, ED-2: a commit whose own handler moves the order lands, and the Selection it drops stays dropped: the Shift+arrow or Shift+Home that asked for the commit places no Selection from the first cell on screen, as Enter and the plain arrows place none
    [InlineData("ArrowDown", true)]
    [InlineData("ArrowLeft", true)]
    [InlineData("Home", true)]
    [InlineData("ArrowDown", false)]
    public async Task ED2_a_commit_whose_handler_moves_the_order_extends_no_dropped_selection(string key, bool shift)
    {
        var rows = TestRows.Many(50);
        var source = new TestSource();
        source.Push(rows, totalCount: rows.Length);
        var edits = new List<GridEditIntent<TestRow>>();
        GridSelection? selection = null;
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 200)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.OnEdit, (GridEditIntent<TestRow> intent) =>
            {
                edits.Add(intent);
                // The Consumer's own write moves the order, as a sort on the edited column does: the
                // rows come back in another order under a new Row Sequence Version (ADR-0011).
                source.Push([rows[1], rows[0], .. rows[2..]], totalCount: rows.Length, rowSequenceVersion: source.RowSequenceVersion + 1);
            })
            .Add(g => g.SelectionChanged, s => selection = s));
        await ClickCellAsync(cut, 2, 1);
        await TypeAsync(cut, "75");

        await PressAsync(cut, key, shift: shift, text: "75", caret: 2);

        Assert.Equal(("Amount", "75"), (Assert.Single(edits).Column, edits[0].Value));
        Assert.Empty(cut.FindAll(".ex-editor"));
        Assert.True(cut.Instance.ReadSelection().Selection.IsEmpty);
        Assert.True(selection!.IsEmpty);
    }

    [Theory] // ADR-0010 / ED-2: in Caret, Excel's Edit mode, the Shift+arrows and Shift+Home are the editor's — one that reaches the core while the gate is told the change commits nothing
    [InlineData("ArrowRight")]
    [InlineData("ArrowUp")]
    [InlineData("Home")]
    public async Task ED2_in_caret_a_shift_key_commits_nothing(string key)
    {
        var edits = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(edits, _ => { });
        await ClickCellAsync(cut, 2, 1);
        await TypeAsync(cut, "75");
        await PressAsync(cut, "F2", text: "75", caret: 2);

        await PressAsync(cut, key, shift: true, text: "75", caret: 2);

        Assert.Empty(edits);
        Assert.Equal("75", cut.Find(".ex-viewport .ex-editor").GetAttribute("value"));
    }

    [Fact] // ADR-0010 / ED-2: Shift+End is not Overwrite's until Excel is asked — one that reaches the core commits nothing
    public async Task ED2_shift_end_in_overwrite_commits_nothing()
    {
        var edits = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(edits, _ => { });
        await ClickCellAsync(cut, 2, 1);
        await TypeAsync(cut, "75");

        await PressAsync(cut, "End", shift: true, text: "75", caret: 2);

        Assert.Empty(edits);
        Assert.Equal("75", cut.Find(".ex-viewport .ex-editor").GetAttribute("value"));
    }

    [Fact] // ADR-0010 / ADR-0051 / ED-2: where a Reference can go, Overwrite's Shift+arrow points a range from the edited cell, as Point does with it, and commits nothing
    public async Task ED2_where_a_reference_can_go_a_shift_arrow_points()
    {
        var edits = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(edits, _ => { }, point: true);
        await ClickCellAsync(cut, 2, 1);
        await TypeAsync(cut, "=");

        await PressAsync(cut, "ArrowDown", shift: true, text: "=", caret: 1);

        Assert.Empty(edits);
        Assert.Equal("=B3:B4", cut.Find(".ex-viewport .ex-editor").GetAttribute("value"));
        Assert.NotEmpty(cut.FindAll(".ex-selection .ex-point"));
    }

    [Fact] // ADR-0010 / ADR-0051 / ED-2: where a Reference can go, Overwrite's Shift+Home points from the edited cell to the row's first column (a reading, until Excel is asked)
    public async Task ED2_where_a_reference_can_go_shift_home_points_to_the_rows_first_column()
    {
        var edits = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(edits, _ => { }, point: true);
        await ClickCellAsync(cut, 2, 2);
        await TypeAsync(cut, "=");

        await PressAsync(cut, "Home", shift: true, text: "=", caret: 1);

        Assert.Empty(edits);
        Assert.Equal("=A3:C3", cut.Find(".ex-viewport .ex-editor").GetAttribute("value"));
        Assert.NotEmpty(cut.FindAll(".ex-selection .ex-point"));
    }

    [Fact] // ADR-0051 / ADR-0010, ED-2: Shift+Home is not Point's — the gate leaves it to the editor while pointing, as before, until Excel is asked — and one that reaches the core then neither moves the outline nor commits and extends
    public async Task ED2_while_pointing_shift_home_neither_points_nor_commits()
    {
        var edits = new List<GridEditIntent<TestRow>>();
        GridSelection? selection = null;
        var cut = RenderGrid(edits, s => selection = s, point: true);
        await ClickCellAsync(cut, 2, 2);
        var before = selection;
        await TypeAsync(cut, "=");
        await PressAsync(cut, "ArrowDown", text: "=", caret: 1);
        Assert.Equal("=C4", cut.Find(".ex-viewport .ex-editor").GetAttribute("value"));

        await PressAsync(cut, "Home", shift: true, text: "=C4", caret: 3);

        Assert.Empty(edits);
        Assert.Equal("=C4", cut.Find(".ex-viewport .ex-editor").GetAttribute("value"));
        Assert.NotEmpty(cut.FindAll(".ex-selection .ex-point"));
        Assert.Same(before, selection);
    }

    [Fact] // ADR-0010 / ED-2: the gate's Overwrite set claims the four Shift+arrows and Shift+Home beside the arrows, Home and End — and not Shift+End; its Point set the same, but Shift+Home, which Point leaves to the editor until Excel is asked (ADR-0051)
    public void ED2_the_gate_claims_the_shift_keys_in_overwrite()
    {
        var script = AssetSources.Read("ExGrid", "ex-grid.js");

        Assert.Matches(new System.Text.RegularExpressions.Regex(
            @"const overwriteKeys = new Set\(\[\s*\.\.\.editingKeys, 'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'Home', 'End',\s*"
            + @"'Shift\+ArrowUp', 'Shift\+ArrowDown', 'Shift\+ArrowLeft', 'Shift\+ArrowRight', 'Shift\+Home'\]\);"),
            script);
        Assert.Matches(new System.Text.RegularExpressions.Regex(
            @"const pointKeys = new Set\(\[\s*\.\.\.editingKeys, 'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'Home', 'End',\s*"
            + @"'Shift\+ArrowUp', 'Shift\+ArrowDown', 'Shift\+ArrowLeft', 'Shift\+ArrowRight'\]\);"),
            script);
    }
}
