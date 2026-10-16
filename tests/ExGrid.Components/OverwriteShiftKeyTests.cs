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
/// Overwrite is Excel's Enter mode, and the Shift+arrows, Shift+Home and the Primary Modifier's
/// arrows mean there what they mean with no edit open (ADR-0010, "Two editing states"; ED-2). Excel
/// was seen to commit an entry typed into a cell and extend the Selection on Shift+Home (the
/// seventeenth Windows run, Part C, case 7) and on Shift+→, Shift+↓ and Shift+←, and to commit and
/// move on Ctrl+→ and commit and extend on Ctrl+Shift+→ (the readings of 2026-10-12, R1 (vii)),
/// where ExGrid selected the editor's text or moved its caret; the other directions follow by
/// generalisation. Where a Reference can go the Shift keys point, as Point does with them (=, then
/// Shift+Home, observed with a Ctrl+C before it in R1's reading after its first step, and as it was
/// typed by run 18a), and the Primary Modifier's arrows point at the edge (run 18a: =, Ctrl+↓ and
/// =, Ctrl+Shift+↓); elsewhere they commit, a Reject holding
/// the commit as it does for the arrows (ADR-0034). Caret keeps them the editor's. Shift+End stays
/// the editor's: Excel's turns on an End Mode the grid does not have (R1 (vii); ADR-0058, Q53), and
/// what it does here is not decided. The gate's half is read from the shipped script here; real
/// keys are layer 3's. Three editable columns of 100px, 20px rows.
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
        IRenderedComponent<ExGrid<TestRow>> cut, string key, bool shift = false, string? text = null, int caret = -1, bool ctrl = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(
            key, ctrl, shift, false, false, false, editorText: text, editorCaret: caret, editorSelectionEnd: caret));

    /// <summary>Types <paramref name="typed"/> onto the selected cell: Overwrite, the caret at the end.</summary>
    private static async Task TypeAsync(IRenderedComponent<ExGrid<TestRow>> cut, string typed)
    {
        await PressAsync(cut, typed[..1]);
        if (typed.Length > 1)
            await cut.Find(".ex-viewport .ex-editor").InputAsync(new ChangeEventArgs { Value = typed });
    }

    [Theory] // ADR-0010 / ED-2: in Overwrite a Shift+arrow commits the entry and extends the Selection from the edited cell, as Excel's Enter mode does (observed, R1 (vii): Shift+→ D10:E10, Shift+↓ D10:D11, Shift+← C10:D10; Shift+↑ by generalisation)
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

    [Theory] // ADR-0142 / ADR-0011, ADR-0010, ED-2: a commit whose own handler moves the order lands, and the Selection it drops stays dropped: the Shift+arrow, Shift+Home or Primary Modifier's arrow that asked for the commit places no Selection from the first cell on screen, as Enter and the plain arrows place none
    [InlineData("ArrowDown", true, false)]
    [InlineData("ArrowLeft", true, false)]
    [InlineData("Home", true, false)]
    [InlineData("ArrowDown", false, false)]
    [InlineData("ArrowRight", false, true)]
    [InlineData("ArrowDown", true, true)]
    public async Task ED2_a_commit_whose_handler_moves_the_order_extends_no_dropped_selection(string key, bool shift, bool ctrl)
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

        await PressAsync(cut, key, shift: shift, ctrl: ctrl, text: "75", caret: 2);

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

    [Fact] // ADR-0010 / ED-2: Shift+End is not Overwrite's — Excel's turns on an End Mode the grid does not have (R1 (vii)), and what it does here is not decided — so one that reaches the core commits nothing
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

    [Fact] // ADR-0010 / ADR-0051 / ED-2: where a Reference can go, Overwrite's Shift+Home points from the edited cell to the row's first column (observed with a Ctrl+C before it, R1's reading after its first step: = in D10, a Ctrl+C that copied nothing, then Shift+Home pointed, and a Ctrl+C copied A10:D10)
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

    [Fact] // ADR-0051 / ADR-0010, ED-2: while pointing, Shift+Home extends the outline from its fixed end to the row's first column, and pointing goes on — nothing commits (observed, R1 (viii): =, ↓, Shift+Home in D10 gave =A11:D11, in Point)
    public async Task ED2_while_pointing_shift_home_extends_the_outline_to_the_rows_first_column()
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
        Assert.Equal("=A4:C4", cut.Find(".ex-viewport .ex-editor").GetAttribute("value"));
        Assert.NotEmpty(cut.FindAll(".ex-selection .ex-point"));
        Assert.Same(before, selection);

        // Pointing goes on over what Shift+Home wrote: ↓ moves the outline on from its fixed end.
        await PressAsync(cut, "ArrowDown", text: "=A4:C4", caret: 6);
        Assert.Equal("=C5", cut.Find(".ex-viewport .ex-editor").GetAttribute("value"));
    }

    [Theory] // ADR-0010 / ED-2: in Overwrite the Primary Modifier's arrows commit the entry and then move or extend to the edge as with no edit open (observed, R1 (vii): Ctrl+→ moved to XFD10, Ctrl+Shift+→ selected D10:XFD10; the other directions by generalisation)
    [InlineData("ArrowRight", false, 2, 2, 2, 2)]
    [InlineData("ArrowDown", false, 49, 1, 49, 1)]
    [InlineData("ArrowLeft", false, 2, 0, 2, 0)]
    [InlineData("ArrowUp", false, 0, 1, 0, 1)]
    [InlineData("ArrowRight", true, 2, 1, 2, 2)]
    [InlineData("ArrowDown", true, 2, 1, 49, 1)]
    public async Task ED2_in_overwrite_a_primary_modifier_arrow_commits_and_goes_to_the_edge(
        string arrow, bool shift, int focusRow, int focusColumn, int extentRow, int extentColumn)
    {
        var edits = new List<GridEditIntent<TestRow>>();
        GridSelection? selection = null;
        var cut = RenderGrid(edits, s => selection = s);
        await ClickCellAsync(cut, 2, 1);
        await TypeAsync(cut, "75");

        await PressAsync(cut, arrow, shift: shift, ctrl: true, text: "75", caret: 2);

        var edit = Assert.Single(edits);
        Assert.Equal(("Amount", "75"), (edit.Column, edit.Value));
        Assert.Empty(cut.FindAll(".ex-editor"));
        Assert.Equal(new CellPosition(focusRow, focusColumn), selection!.Focus);
        Assert.Equal(new CellPosition(extentRow, extentColumn), selection.Extent);
    }

    [Theory] // ADR-0034 / ADR-0010 / ED-2 / ED-15: a Reject holds the commit a Primary Modifier's arrow asks for, and the key keeps no meaning of its own
    [InlineData(false)]
    [InlineData(true)]
    public async Task ED2_a_reject_holds_a_primary_modifier_arrows_commit(bool shift)
    {
        var edits = new List<GridEditIntent<TestRow>>();
        GridSelection? selection = null;
        var cut = RenderGrid(edits, s => selection = s, validate: (_, _) => EditVerdict.Reject("no"));
        await ClickCellAsync(cut, 2, 1);
        var before = selection;
        await TypeAsync(cut, "abc");

        await PressAsync(cut, "ArrowRight", shift: shift, ctrl: true, text: "abc", caret: 3);

        Assert.Empty(edits);
        Assert.Equal("abc", cut.Find(".ex-viewport .ex-editor").GetAttribute("value"));
        Assert.Equal("true", cut.Find(".ex-viewport .ex-editor").GetAttribute("aria-invalid"));
        Assert.Same(before, selection);
    }

    [Theory] // ADR-0010 / ED-2: in Caret, Excel's Edit mode, the Primary Modifier's arrows are the editor's — one that reaches the core while the gate is told the change commits nothing
    [InlineData(false)]
    [InlineData(true)]
    public async Task ED2_in_caret_a_primary_modifier_arrow_commits_nothing(bool shift)
    {
        var edits = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(edits, _ => { });
        await ClickCellAsync(cut, 2, 1);
        await TypeAsync(cut, "75");
        await PressAsync(cut, "F2", text: "75", caret: 2);

        await PressAsync(cut, "ArrowRight", shift: shift, ctrl: true, text: "75", caret: 2);

        Assert.Empty(edits);
        Assert.Equal("75", cut.Find(".ex-viewport .ex-editor").GetAttribute("value"));
    }

    [Theory] // ADR-0051 / ADR-0010 / ED-2: where a Reference can go, Overwrite's Primary Modifier's arrows point at the edge — with Shift from the edited cell — and nothing commits (observed in Excel, run 18a: =, Ctrl+↓ in D10 gave =D1048576 in Point, and =, Ctrl+Shift+↓ gave =D10:D1048576; the other directions by generalisation). No DataEdge is declared here, so the edge is the grid's
    [InlineData("ArrowDown", false, "=B50")]
    [InlineData("ArrowDown", true, "=B3:B50")]
    [InlineData("ArrowLeft", false, "=A3")]
    [InlineData("ArrowUp", true, "=B1:B3")]
    [InlineData("ArrowRight", true, "=B3:C3")]
    public async Task ADR0051_where_a_reference_can_go_a_primary_modifier_arrow_points_at_the_edge(
        string arrow, bool shift, string written)
    {
        var edits = new List<GridEditIntent<TestRow>>();
        GridSelection? selection = null;
        var cut = RenderGrid(edits, s => selection = s, point: true);
        await ClickCellAsync(cut, 2, 1);
        var before = selection;
        await TypeAsync(cut, "=");

        await PressAsync(cut, arrow, shift: shift, ctrl: true, text: "=", caret: 1);

        Assert.Empty(edits);
        Assert.Equal(written, cut.Find(".ex-viewport .ex-editor").GetAttribute("value"));
        Assert.NotEmpty(cut.FindAll(".ex-selection .ex-point"));
        Assert.Same(before, selection);
    }

    [Fact] // ADR-0051 / ED-2: while an outline stands, the Primary Modifier's arrows move or extend it to the edge, and pointing goes on (by generalisation from run 18a's =, Ctrl+↓)
    public async Task ADR0051_while_pointing_a_primary_modifier_arrow_moves_the_outline_to_the_edge()
    {
        var edits = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(edits, _ => { }, point: true);
        await ClickCellAsync(cut, 2, 1);
        await TypeAsync(cut, "=");
        await PressAsync(cut, "ArrowDown", text: "=", caret: 1);
        Assert.Equal("=B4", cut.Find(".ex-viewport .ex-editor").GetAttribute("value"));

        await PressAsync(cut, "ArrowDown", ctrl: true, text: "=B4", caret: 3);
        Assert.Equal("=B50", cut.Find(".ex-viewport .ex-editor").GetAttribute("value"));

        await PressAsync(cut, "ArrowUp", shift: true, ctrl: true, text: "=B50", caret: 4);
        Assert.Equal("=B1:B50", cut.Find(".ex-viewport .ex-editor").GetAttribute("value"));
        Assert.Empty(edits);
        Assert.NotEmpty(cut.FindAll(".ex-selection .ex-point"));
    }

    [Fact] // ADR-0010 / ADR-0051 / ED-2: the gate's Point set claims the four Shift+arrows and Shift+Home beside the arrows, Home and End (R1 (viii)), and the Primary Modifier's arrows with Shift or without (run 18a); its Overwrite set is the same keys (R1 (vii), run 18a) — and neither claims Shift+End
    public void ED2_the_gate_claims_the_shift_keys_in_overwrite()
    {
        var script = AssetSources.Read("ExGrid", "ex-grid.js");

        Assert.Matches(new System.Text.RegularExpressions.Regex(
            @"const pointKeys = new Set\(\[\s*\.\.\.editingKeys, 'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'Home', 'End',\s*"
            + @"'Shift\+ArrowUp', 'Shift\+ArrowDown', 'Shift\+ArrowLeft', 'Shift\+ArrowRight', 'Shift\+Home',\s*"
            + @"'Control\+ArrowUp', 'Control\+ArrowDown', 'Control\+ArrowLeft', 'Control\+ArrowRight',\s*"
            + @"'Control\+Shift\+ArrowUp', 'Control\+Shift\+ArrowDown', 'Control\+Shift\+ArrowLeft', 'Control\+Shift\+ArrowRight'\]\);"),
            script);
        Assert.Matches(new System.Text.RegularExpressions.Regex(@"const overwriteKeys = new Set\(pointKeys\);"), script);
        Assert.DoesNotContain("'Shift+End'", script.Split("const pointKeys")[1].Split("const completionKeys")[0], StringComparison.Ordinal);
    }
}
