using Bunit;
using ExGrid.Cells;
using ExGrid.Chrome;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// Point, the fourth editing state (ADR-0051; DC-1, DC-19, DC-20's layer 2 half): with the
/// Consumer's predicate answering true for the text and caret a key carries, the arrows and
/// clicks move a pointing outline painted in the selection overlay, and the Consumer's
/// Reference text is written at the caret; Shift extends; typing ends pointing; F2 toggles
/// with Caret; the Selection and the Focus do not move. 50 rows of 20px in a 350 × 200
/// Viewport under a 20px header; Book (A) and Note (B) edit, Amount (C) does not.
/// </summary>
public class PointModeTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        new("Note", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
    ];

    /// <summary>A Reference can go after <c>=</c>, an operator, <c>(</c> or <c>,</c> in a
    /// Formula — the shape of ExSheet's <c>FormulaEntry.PointAt</c>.</summary>
    private static bool PointAt(string text, int caret)
        => text.StartsWith('=') && caret > 0 && caret <= text.Length && "=+-*/(,".Contains(text[caret - 1]);

    /// <summary><c>A3</c>, <c>B7:C9</c>: the shape of ExSheet's <c>FormulaEntry.ReferenceText</c>.</summary>
    private static string ReferenceText(SelectionRange range)
    {
        static string Cell(int row, int column) => FormattableString.Invariant($"{(char)('A' + column)}{row + 1}");
        var topLeft = Cell(range.TopRow, range.LeftColumn);
        return range.RowCount == 1 && range.ColumnCount == 1
            ? topLeft
            : topLeft + ":" + Cell(range.BottomRow, range.RightColumn);
    }

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        List<GridEditIntent<TestRow>>? intents = null,
        List<GridSelection>? selections = null,
        bool point = true,
        bool formulaBar = false,
        Func<string, int, ValueTask<EditorCompletion?>>? complete = null,
        Func<CellPosition, string?>? nameBoxLabel = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(50))
              .Add(g => g.TotalCount, 50)
              .Add(g => g.Columns, Columns())
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 200)
              .Add(g => g.ViewportWidth, 350)
              .Add(g => g.ShowFormulaBar, formulaBar)
              .Add(g => g.OnEdit, (GridEditIntent<TestRow> intent) => intents?.Add(intent))
              .Add(g => g.SelectionChanged, (GridSelection s) => selections?.Add(s));
            if (point)
            {
                ps.Add(g => g.PointAt, PointAt)
                  .Add(g => g.ReferenceText, ReferenceText);
            }
            if (complete is not null)
                ps.Add(g => g.CompleteEditorText, complete);
            if (nameBoxLabel is not null)
                ps.Add(g => g.NameBoxLabel, nameBoxLabel);
        });

    private static Task ClickAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y, bool shift = false)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y, ShiftKey = shift });

    /// <summary>A key as the capture listener forwards it while editing: with the editor's
    /// text and caret as the browser has them, or none where the test says so.</summary>
    private static Task PressAsync(
        IRenderedComponent<ExGrid<TestRow>> cut, string key, bool shift = false,
        string? text = null, int caret = -1, bool fromBar = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(
            key, false, shift, false, false, false, fromDescendant: fromBar, editorText: text, editorCaret: caret));

    private static Task TypeAsync(IRenderedComponent<ExGrid<TestRow>> cut, string text)
        => cut.Find(".ex-viewport .ex-editor").InputAsync(new ChangeEventArgs { Value = text });

    /// <summary>Selects A1 and types <c>=</c> onto it: Overwrite, with the caret after it.</summary>
    private static async Task StartFormulaAsync(IRenderedComponent<ExGrid<TestRow>> cut)
    {
        await ClickAsync(cut, 50, 10);
        await PressAsync(cut, "=");
    }

    private static string EditorText(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.Find(".ex-viewport .ex-editor").GetAttribute("value") ?? "";

    private static string? PointStyle(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.FindAll(".ex-selection .ex-point").SingleOrDefault()?.GetAttribute("style");

    private List<string> EditingModesTold()
        => [.. JSInterop.Invocations.Where(i => i.Identifier == "setEditing").Select(i => (string)i.Arguments[0]!)];

    [Fact] // ADR-0051/0012 / DC-1: without the predicate, the arrows commit and move as before
    public async Task Without_the_predicate_the_arrows_commit_and_move()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var selections = new List<GridSelection>();
        var cut = RenderGrid(intents, selections, point: false);
        await StartFormulaAsync(cut);

        await PressAsync(cut, "ArrowDown", text: "=", caret: 1);

        Assert.Equal("=", Assert.Single(intents).Value);
        Assert.Equal(new CellPosition(1, 0), selections[^1].Focus);
        Assert.Empty(cut.FindAll(".ex-point"));
    }

    [Fact] // ADR-0051 / DC-19: = ↓ ↓ writes A3, and the outline stands on A3
    public async Task Equals_down_down_writes_a3()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(intents);
        await StartFormulaAsync(cut);

        await PressAsync(cut, "ArrowDown", text: "=", caret: 1);
        Assert.Equal("=A2", EditorText(cut));
        await PressAsync(cut, "ArrowDown", text: "=A2", caret: 3);

        Assert.Equal("=A3", EditorText(cut));
        Assert.Empty(intents);
        // Row 2 of the painted slice, column A: paint only, over the Selection's own layer.
        Assert.Equal("left: 0px; top: 40px; width: 100px; height: 20px", PointStyle(cut));
        // The gate claims Overwrite's keys and the Shift+arrows while an outline stands
        // (ADR-0051's second round).
        Assert.Equal("point", EditingModesTold()[^1]);
    }

    [Fact] // ADR-0051 / DC-19: the Selection and the Focus do not move while pointing
    public async Task The_selection_and_the_focus_stay_put()
    {
        var selections = new List<GridSelection>();
        var cut = RenderGrid(selections: selections);
        await StartFormulaAsync(cut);
        var before = selections.Count;

        await PressAsync(cut, "ArrowDown", text: "=", caret: 1);
        await PressAsync(cut, "ArrowRight", text: "=A2", caret: 3);
        await ClickAsync(cut, 150, 90);

        Assert.Equal(before, selections.Count);
        Assert.Equal(new CellPosition(0, 0), selections[^1].Focus);
        // The editor still stands over the cell being edited.
        Assert.Equal("left: 0px; top: 0px; width: 100px; height: 20px", cut.Find(".ex-viewport .ex-editor").GetAttribute("style"));
        Assert.Equal("=B5", EditorText(cut));
    }

    [Fact] // ADR-0051/0052 / DC-19: Shift extends the outline to a range, from its Focus
    public async Task Shift_extends_to_a_range()
    {
        var cut = RenderGrid();
        await StartFormulaAsync(cut);

        await PressAsync(cut, "ArrowDown", text: "=", caret: 1);
        await PressAsync(cut, "ArrowDown", shift: true, text: "=A2", caret: 3);
        await PressAsync(cut, "ArrowRight", shift: true, text: "=A2:A3", caret: 6);

        Assert.Equal("=A2:B3", EditorText(cut));
        Assert.Equal("left: 0px; top: 20px; width: 200px; height: 40px", PointStyle(cut));
    }

    [Fact] // ADR-0051 / DC-19: Shift and an arrow straight after = extends from the edited cell
    public async Task Shift_first_extends_from_the_edited_cell()
    {
        var cut = RenderGrid();
        await StartFormulaAsync(cut);

        await PressAsync(cut, "ArrowDown", shift: true, text: "=", caret: 1);

        Assert.Equal("=A1:A2", EditorText(cut));
    }

    [Fact] // ADR-0058 (Q53) / SH-36: at a Reference's place with no outline standing, Home starts pointing at the row's first column, as Excel's Home in Enter mode does (Part B of the ninth Windows run, x4)
    public async Task ADR0058_Q53_home_at_a_references_place_points_at_the_rows_first_column()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(intents);
        await ClickAsync(cut, 150, 30);
        await PressAsync(cut, "=");

        await PressAsync(cut, "Home", text: "=", caret: 1);

        Assert.Equal("=A2", EditorText(cut));
        Assert.Equal("left: 0px; top: 20px; width: 100px; height: 20px", PointStyle(cut));
        Assert.Equal("point", EditingModesTold()[^1]);
        Assert.Empty(intents);
    }

    [Fact] // ADR-0058 (Q53) / SH-36: at a Reference's place with no outline standing, End writes nothing and asks for no commit — Excel's turns End Mode on, which the grid does not have (x5)
    public async Task ADR0058_Q53_end_at_a_references_place_writes_nothing_and_commits_nothing()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var selections = new List<GridSelection>();
        var cut = RenderGrid(intents, selections);
        await ClickAsync(cut, 150, 30);
        await PressAsync(cut, "=");
        var selectionsBefore = selections.Count;

        await PressAsync(cut, "End", text: "=", caret: 1);

        Assert.Equal("=", EditorText(cut));
        Assert.Null(PointStyle(cut));
        Assert.Empty(intents);
        Assert.Equal(selectionsBefore, selections.Count);
        Assert.Equal("overwrite", EditingModesTold()[^1]);
    }

    [Fact] // ADR-0051 / ADR-0058 (Q53): with an outline standing, Home and End move it to the row's first and last columns, as before
    public async Task Home_and_end_move_an_outline_that_stands()
    {
        var cut = RenderGrid();
        await ClickAsync(cut, 150, 30);
        await PressAsync(cut, "=");
        await PressAsync(cut, "ArrowDown", text: "=", caret: 1);
        Assert.Equal("=B3", EditorText(cut));

        await PressAsync(cut, "End", text: "=B3", caret: 3);
        Assert.Equal("=C3", EditorText(cut));
        await PressAsync(cut, "Home", text: "=C3", caret: 3);
        Assert.Equal("=A3", EditorText(cut));
    }

    [Fact] // ADR-0012 / ADR-0058 (Q53, the tenth Windows run, case 20): where no Reference can go, Home is Overwrite's — it commits and moves to the row's first column, as before
    public async Task Where_no_reference_can_go_home_commits_and_moves()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var selections = new List<GridSelection>();
        var cut = RenderGrid(intents, selections);
        await ClickAsync(cut, 150, 30);
        await PressAsync(cut, "=");

        await PressAsync(cut, "Home", text: "=1", caret: 2);

        Assert.Equal("=1", Assert.Single(intents).Value);
        Assert.Equal(new CellPosition(1, 0), selections[^1].Focus);
    }

    [Fact] // ADR-0051/0012: typing a constant, the arrows still commit and move
    public async Task A_constant_commits_and_moves()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var selections = new List<GridSelection>();
        var cut = RenderGrid(intents, selections);
        await ClickAsync(cut, 50, 10);
        await PressAsync(cut, "5");

        await PressAsync(cut, "ArrowDown", text: "5", caret: 1);

        Assert.Equal("5", Assert.Single(intents).Value);
        Assert.Equal(new CellPosition(1, 0), selections[^1].Focus);
    }

    [Fact] // ADR-0051 / DC-19: an operator ends pointing; the next arrow points afresh from the edited cell
    public async Task An_operator_ends_pointing()
    {
        var cut = RenderGrid();
        await StartFormulaAsync(cut);
        await PressAsync(cut, "ArrowDown", text: "=", caret: 1);
        await PressAsync(cut, "ArrowDown", text: "=A2", caret: 3);

        await TypeAsync(cut, "=A3+");

        Assert.Null(PointStyle(cut));
        await PressAsync(cut, "ArrowDown", text: "=A3+", caret: 4);
        Assert.Equal("=A3+A2", EditorText(cut));
    }

    [Fact] // ADR-0051 / DC-20: a key is decided from the text it carries, not from what the core last heard
    public async Task The_key_decides_from_the_text_it_carries()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(intents);
        await StartFormulaAsync(cut);

        // The core last heard "=", where a Reference can go; the user has typed on, and the
        // input event is still on the wire. The arrow carries "=5": it must not point.
        await PressAsync(cut, "ArrowDown", text: "=5", caret: 2);

        Assert.Equal("=5", Assert.Single(intents).Value);
        Assert.Empty(cut.FindAll(".ex-point"));
    }

    [Fact] // ADR-0051 / DC-20: and the other way — text the core has not heard yet allows pointing
    public async Task Text_the_core_has_not_heard_can_allow_pointing()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(intents);
        await ClickAsync(cut, 50, 10);
        await PressAsync(cut, "5");

        await PressAsync(cut, "ArrowDown", text: "=5+", caret: 3);

        Assert.Empty(intents);
        Assert.Equal("=5+A2", EditorText(cut));
    }

    [Fact] // ADR-0051 / DC-20: text typed after a Reference the core wrote ends pointing, even before its input event
    public async Task Text_typed_after_a_pointed_reference_ends_pointing()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(intents);
        await StartFormulaAsync(cut);
        await PressAsync(cut, "ArrowDown", text: "=", caret: 1);

        // "=A2" then "5" typed: "=A25" is no place for a Reference, and the arrow commits.
        await PressAsync(cut, "ArrowDown", text: "=A25", caret: 4);

        Assert.Equal("=A25", Assert.Single(intents).Value);
    }

    [Fact] // ADR-0051 / DC-19: F2 switches between moving the caret and pointing
    public async Task F2_toggles_between_caret_and_point()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(intents);
        await StartFormulaAsync(cut);
        await PressAsync(cut, "ArrowDown", text: "=", caret: 1);

        await PressAsync(cut, "F2", text: "=A2", caret: 3); // Point → Caret: the outline goes, the text stays
        Assert.Null(PointStyle(cut));
        Assert.Equal("=A2", EditorText(cut));
        Assert.Equal("caret", EditingModesTold()[^1]);

        await TypeAsync(cut, "=A2*");
        await PressAsync(cut, "F2", text: "=A2*", caret: 4); // Caret → Point where a Reference can go
        Assert.Equal("point", EditingModesTold()[^1]);

        await PressAsync(cut, "ArrowRight", text: "=A2*", caret: 4);
        Assert.Equal("=A2*B1", EditorText(cut));
        Assert.Empty(intents);
    }

    [Fact] // ADR-0051/0010: from Caret, F2 where no Reference can go is Overwrite, as before
    public async Task F2_from_caret_where_no_reference_can_go_is_overwrite()
    {
        var cut = RenderGrid();
        await ClickAsync(cut, 50, 10);
        await PressAsync(cut, "F2");

        await PressAsync(cut, "F2", text: "Row 000001", caret: 10);

        Assert.Equal("overwrite", EditingModesTold()[^1]);
        Assert.Empty(cut.FindAll(".ex-point"));
    }

    [Fact] // ADR-0051/0010: in Caret the arrows are the editor's; one that arrives anyway points nowhere and commits nothing
    public async Task In_caret_an_arrow_neither_points_nor_commits()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(intents);
        await StartFormulaAsync(cut);
        await PressAsync(cut, "F2", text: "=", caret: 1); // Overwrite → Caret

        await PressAsync(cut, "ArrowDown", text: "=", caret: 1);

        Assert.Empty(intents);
        Assert.Equal("=", EditorText(cut));
    }

    [Fact] // ADR-0051 / DC-19: a click writes the clicked cell, Shift+click a range, and the edit stays open
    public async Task A_click_writes_the_clicked_cell()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(intents);
        await StartFormulaAsync(cut);

        await ClickAsync(cut, 150, 70); // B4
        Assert.Equal("=B4", EditorText(cut));
        await ClickAsync(cut, 150, 110, shift: true); // B6
        Assert.Equal("=B4:B6", EditorText(cut));
        Assert.Empty(intents);

        // The drag that started on the press extends the outline.
        await cut.Find(".ex-viewport").MouseMoveAsync(new MouseEventArgs { Buttons = 1, OffsetX = 250, OffsetY = 130 });
        Assert.Equal("=B4:C7", EditorText(cut));
        await cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs { Button = 0, OffsetX = 250, OffsetY = 130 });
        Assert.Equal("=B4:C7", EditorText(cut));
    }

    [Fact] // ADR-0010/0051: where no Reference can go, a click commits and selects as before
    public async Task A_click_where_no_reference_can_go_commits()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var selections = new List<GridSelection>();
        var cut = RenderGrid(intents, selections);
        await ClickAsync(cut, 50, 10);
        await PressAsync(cut, "5");

        await ClickAsync(cut, 150, 70);

        Assert.Equal("5", Assert.Single(intents).Value);
        Assert.Equal(new CellPosition(3, 1), selections[^1].Focus);
    }

    [Fact] // ADR-0051/0007: Enter commits the pointed Formula once and moves from the edited cell
    public async Task Enter_commits_what_was_pointed()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var selections = new List<GridSelection>();
        var cut = RenderGrid(intents, selections);
        await StartFormulaAsync(cut);
        await PressAsync(cut, "ArrowDown", text: "=", caret: 1);
        await PressAsync(cut, "ArrowDown", text: "=A2", caret: 3);

        await PressAsync(cut, "Enter", text: "=A3", caret: 3);

        Assert.Equal("=A3", Assert.Single(intents).Value);
        Assert.Equal(new CellPosition(1, 0), selections[^1].Focus);
        Assert.Empty(cut.FindAll(".ex-point"));
    }

    [Fact] // ADR-0051 / DC-19: pointing works from the Formula Bar, replacing the Reference in the middle of the text
    public async Task Pointing_works_from_the_formula_bar()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(intents, formulaBar: true);
        await ClickAsync(cut, 50, 10);
        await cut.Find(".ex-formula-bar-text").FocusAsync(new FocusEventArgs());
        await cut.Find(".ex-formula-bar-text").InputAsync(new ChangeEventArgs { Value = "=SUM()" });

        // The bar opened Caret; F2 points, with the caret between the parentheses.
        await PressAsync(cut, "F2", text: "=SUM()", caret: 5, fromBar: true);
        await PressAsync(cut, "ArrowDown", text: "=SUM()", caret: 5, fromBar: true);
        Assert.Equal("=SUM(A2)", cut.Find(".ex-formula-bar-text").GetAttribute("value"));
        Assert.Equal("=SUM(A2)", EditorText(cut));

        // The browser put the caret at the end when the value was written; the outline still
        // stands over unchanged text, so the next arrow replaces the Reference it wrote.
        await PressAsync(cut, "ArrowDown", text: "=SUM(A2)", caret: 8, fromBar: true);
        Assert.Equal("=SUM(A3)", cut.Find(".ex-formula-bar-text").GetAttribute("value"));

        // A click from the bar points as well.
        await ClickAsync(cut, 150, 30, shift: true);
        Assert.Equal("=SUM(A2:B3)", cut.Find(".ex-formula-bar-text").GetAttribute("value"));

        await PressAsync(cut, "Enter", text: "=SUM(A2:B3)", caret: 11, fromBar: true);
        Assert.Equal("=SUM(A2:B3)", Assert.Single(intents).Value);
    }

    [Fact] // ADR-0051/0012: the outline walked off screen is revealed; the Focus stays where it was
    public async Task The_outline_is_kept_on_screen()
    {
        var selections = new List<GridSelection>();
        // Read from the Formula Bar: the Cell Editor stands over A1, and once the outline's
        // reveal has walked the view past row 1 the editor's cell is no longer painted, so
        // neither is the editor (ADR-0010); the bar shows the same text (ADR-0051).
        var cut = RenderGrid(selections: selections, formulaBar: true);
        await StartFormulaAsync(cut);
        var text = "=";
        for (var i = 0; i < 12; i++)
        {
            await PressAsync(cut, "ArrowDown", text: text, caret: text.Length);
            text = cut.Find(".ex-formula-bar-text").GetAttribute("value") ?? "";
        }

        Assert.Equal("=A13", text);
        // The outline's row is painted in the render that revealed it (ADR-0012, 2026-09-29).
        Assert.Contains(cut.FindAll("[role=gridcell]"), cell => cell.Id!.EndsWith("r12c0", StringComparison.Ordinal));
        Assert.Contains(JSInterop.Invocations, i => i.Identifier == "setScrollOffset" && (double)i.Arguments[0]! > 0);
        Assert.Equal(new CellPosition(0, 0), selections[^1].Focus);
    }

    [Fact] // ADR-0003: pointing renders no row
    public async Task Pointing_renders_no_row()
    {
        var cut = RenderGrid();
        await StartFormulaAsync(cut);
        var before = cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToList();

        await PressAsync(cut, "ArrowDown", text: "=", caret: 1);
        await PressAsync(cut, "ArrowRight", shift: true, text: "=A2", caret: 3);
        await ClickAsync(cut, 150, 70);

        Assert.Equal(before, cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToList());
    }

    [Fact] // ADR-0051: pointing takes the completion list down, and the list's keys come first while it is open
    public async Task Pointing_and_the_completion_list()
    {
        var cut = RenderGrid(complete: (text, caret) => ValueTask.FromResult<EditorCompletion?>(
            text == "=S" ? new EditorCompletion([new CompletionCandidate("SUM", 1, 1, "SUM(")]) : null));
        await StartFormulaAsync(cut);
        await TypeAsync(cut, "=S");

        await PressAsync(cut, "Tab", text: "=S", caret: 2);
        Assert.Equal("=SUM(", EditorText(cut));

        await PressAsync(cut, "ArrowDown", text: "=SUM(", caret: 5);
        Assert.Equal("=SUM(A2", EditorText(cut));
        Assert.Empty(cut.FindAll(".ex-completion"));
    }

    [Fact] // ADR-0051: a Point predicate without Reference text is refused by name
    public void A_predicate_without_reference_text_is_refused()
    {
        var refused = Assert.Throws<ArgumentException>(() => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(5))
            .Add(g => g.TotalCount, 5)
            .Add(g => g.Columns, Columns())
            .Add(g => g.PointAt, PointAt)));

        Assert.Contains("ReferenceText", refused.Message);
    }

    /// <summary>A Chrome that paints the Cell Editor, recording the mode it was handed.</summary>
    private sealed class ModeChrome : IGridChrome
    {
        public CellEditMode? Mode { get; private set; }

        public RenderFragment? FilterPanel(FilterPanelContext context) => null;

        public RenderFragment? ColumnMenu(ColumnMenuContext context) => null;

        public RenderFragment? CellEditor(CellEditorContext context)
        {
            Mode = context.Mode;
            return builder => builder.AddMarkupContent(0, "<input class='stub-editor' />");
        }

        public RenderFragment? LoadingIndicator(LoadingContext context) => null;
    }

    [Fact] // ADR-0051/0010: a Chrome's editor is told the edit is in Point
    public async Task A_chrome_editor_is_told_point()
    {
        var chrome = new ModeChrome();
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(50))
            .Add(g => g.TotalCount, 50)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 200)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.Chrome, chrome)
            .Add(g => g.PointAt, PointAt)
            .Add(g => g.ReferenceText, ReferenceText));
        await StartFormulaAsync(cut);
        Assert.Equal(CellEditMode.Overwrite, chrome.Mode);

        await PressAsync(cut, "ArrowDown", text: "=", caret: 1);

        Assert.Equal(CellEditMode.Point, chrome.Mode);
    }

    /// <summary>The Name Box's label in the Sheet's words: <c>A1</c>, <c>C7</c>.</summary>
    private static string? CellName(CellPosition cell)
        => FormattableString.Invariant($"{(char)('A' + cell.Column)}{cell.Row + 1}");

    private IRenderedComponent<ExGrid<TestRow>> RenderWithNameBox(List<GridEditIntent<TestRow>>? intents = null)
        => RenderGrid(intents, formulaBar: true, nameBoxLabel: CellName);

    private static string NameBox(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.Find("input.ex-name-box").GetAttribute("value") ?? "";

    [Fact] // ADR-0051/0052 (Excel, behaviours item 9): while pointing by keys the Name Box names the pointed cell, and the outline's Focus once Shift extends it
    public async Task The_name_box_names_the_cell_pointed_by_keys()
    {
        var cut = RenderWithNameBox();
        await StartFormulaAsync(cut);
        Assert.Equal("A1", NameBox(cut));

        await PressAsync(cut, "ArrowDown", text: "=", caret: 1);
        Assert.Equal("A2", NameBox(cut));
        await PressAsync(cut, "ArrowDown", text: "=A2", caret: 3);
        Assert.Equal("A3", NameBox(cut));
        // Shift extends: the outline's Focus is named, as the Selection's is — the end that
        // stays, not the one that moves (ADR-0052).
        await PressAsync(cut, "ArrowRight", shift: true, text: "=A3", caret: 3);
        Assert.Equal("=A3:B3", EditorText(cut));
        Assert.Equal("A3", NameBox(cut));
    }

    [Fact] // ADR-0051 (Excel, behaviours item 9): an operator ends pointing, and the Name Box names the edited cell again
    public async Task Typing_an_operator_returns_the_name_box_to_the_edited_cell()
    {
        var cut = RenderWithNameBox();
        await StartFormulaAsync(cut);
        await PressAsync(cut, "ArrowDown", text: "=", caret: 1);
        Assert.Equal("A2", NameBox(cut));

        await TypeAsync(cut, "=A2+");

        Assert.Equal("A1", NameBox(cut));
    }

    [Fact] // ADR-0051/0052 (Excel, behaviours item 10): a click points, and the Name Box names the clicked cell, which a drag leaves the Focus
    public async Task The_name_box_names_the_cell_pointed_by_a_click()
    {
        var cut = RenderWithNameBox();
        await StartFormulaAsync(cut);

        await ClickAsync(cut, 150, 70); // B4
        Assert.Equal("=B4", EditorText(cut));
        Assert.Equal("B4", NameBox(cut));
        await cut.Find(".ex-viewport").MouseMoveAsync(new MouseEventArgs { Buttons = 1, OffsetX = 250, OffsetY = 130 });
        Assert.Equal("=B4:C7", EditorText(cut));
        // The drag's Focus is where the button went down (ADR-0052).
        Assert.Equal("B4", NameBox(cut));
    }

    [Fact] // ADR-0051: F2 leaves the outline and the Name Box names the edited cell again
    public async Task F2_returns_the_name_box_to_the_edited_cell()
    {
        var cut = RenderWithNameBox();
        await StartFormulaAsync(cut);
        await PressAsync(cut, "ArrowDown", text: "=", caret: 1);

        await PressAsync(cut, "F2", text: "=A2", caret: 3);

        Assert.Equal("A1", NameBox(cut));
    }

    [Fact] // ADR-0051/0012: the pointed Formula committed, the Name Box follows the Focus as before
    public async Task After_the_commit_the_name_box_follows_the_focus()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var cut = RenderWithNameBox(intents);
        await StartFormulaAsync(cut);
        await PressAsync(cut, "ArrowDown", text: "=", caret: 1);
        await PressAsync(cut, "ArrowDown", text: "=A2", caret: 3);

        await PressAsync(cut, "Enter", text: "=A3", caret: 3);

        Assert.Equal("=A3", Assert.Single(intents).Value);
        Assert.Equal("A2", NameBox(cut));
    }

    [Fact] // ADR-0051: Escape cancels the edit, and the Name Box names the Focus, which never moved
    public async Task Escape_returns_the_name_box_to_the_focus()
    {
        var cut = RenderWithNameBox();
        await StartFormulaAsync(cut);
        await PressAsync(cut, "ArrowDown", text: "=", caret: 1);

        await PressAsync(cut, "Escape", text: "=A2", caret: 3);

        Assert.Equal("A1", NameBox(cut));
    }
}
