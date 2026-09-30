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
/// An edit in the Formula Bar never enters Overwrite (ADR-0051, note of 2026-09-30; ED-29's
/// layer 2 half): typing that ends Point there returns to Caret, an edit a press moves from the
/// cell into the bar goes into Caret, and one that moves back into the cell keeps the mode it
/// has. So Home, End, ← and → in the bar never commit the Formula or move the Focus. In the
/// cell nothing changes (ADR-0012). Real keys are layer 3's. 50 rows of 20px in a 350 × 200
/// Viewport; Book (A) and Note (B) edit, Amount (C) does not. The edits are on B1, where Home
/// in Overwrite would move the Focus to A1.
/// </summary>
public class FormulaBarModeTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static readonly CellPosition B1 = new(0, 1);

    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        new("Note", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
    ];

    /// <summary>The shape of ExSheet's <c>FormulaEntry.PointAt</c>.</summary>
    private static bool PointAt(string text, int caret)
        => text.StartsWith('=') && caret > 0 && caret <= text.Length && "=+-*/(,".Contains(text[caret - 1]);

    private static string ReferenceText(SelectionRange range)
        => FormattableString.Invariant($"{(char)('A' + range.LeftColumn)}{range.TopRow + 1}");

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        List<GridEditIntent<TestRow>> intents,
        List<GridSelection> selections,
        bool point = true,
        Func<string, int, int, EditorRewrite?>? cycle = null,
        IGridChrome? chrome = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(50))
              .Add(g => g.TotalCount, 50)
              .Add(g => g.Columns, Columns())
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 200)
              .Add(g => g.ViewportWidth, 350)
              .Add(g => g.ShowFormulaBar, true)
              .Add(g => g.OnEdit, (GridEditIntent<TestRow> intent) => intents.Add(intent))
              .Add(g => g.SelectionChanged, (GridSelection s) => selections.Add(s));
            if (point)
            {
                ps.Add(g => g.PointAt, PointAt)
                  .Add(g => g.ReferenceText, ReferenceText);
            }
            if (cycle is not null)
                ps.Add(g => g.CycleReference, cycle);
            if (chrome is not null)
                ps.Add(g => g.Chrome, chrome);
        });

    private static Task ClickAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });

    /// <summary>A key as the capture listener forwards it while editing, with the editor's text
    /// and caret as the browser has them.</summary>
    private static Task PressAsync(
        IRenderedComponent<ExGrid<TestRow>> cut, string key, string? text = null, int caret = -1, bool fromBar = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(
            key, false, false, false, false, false, fromDescendant: fromBar, editorText: text, editorCaret: caret));

    private static AngleSharp.Dom.IElement Bar(IRenderedComponent<ExGrid<TestRow>> cut) => cut.Find(".ex-formula-bar-text");

    private static Task PressIntoBarAsync(IRenderedComponent<ExGrid<TestRow>> cut) => Bar(cut).FocusAsync(new FocusEventArgs());

    /// <summary>Typing in the bar: its input, and the caret the listener reports with it.</summary>
    private static async Task TypeInBarAsync(IRenderedComponent<ExGrid<TestRow>> cut, string text)
    {
        await Bar(cut).InputAsync(new ChangeEventArgs { Value = text });
        await cut.InvokeAsync(() => cut.Instance.OnEditorCaretAsync(text, text.Length));
    }

    private static string BarText(IRenderedComponent<ExGrid<TestRow>> cut) => Bar(cut).GetAttribute("value") ?? "";

    private static string CellEditorText(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.Find(".ex-viewport .ex-editor").GetAttribute("value") ?? "";

    private List<string> EditingModesTold()
        => [.. JSInterop.Invocations.Where(i => i.Identifier == "setEditing").Select(i => (string)i.Arguments[0]!)];

    /// <summary>The edit on B1 is still open, nothing was committed, and the Focus has not moved
    /// from B1.</summary>
    private static void AssertTheEditStandsOnB1(
        IRenderedComponent<ExGrid<TestRow>> cut, List<GridEditIntent<TestRow>> intents, List<GridSelection> selections)
    {
        Assert.Empty(intents);
        Assert.Equal(B1, selections[^1].Focus);
        Assert.Single(cut.FindAll(".ex-viewport .ex-editor"));
    }

    [Fact] // ADR-0051 (2026-09-30) / ED-29: in the Formula Bar, typing that ends Point returns to Caret, and Home then commits nothing
    public async Task ED29_typing_that_ends_point_in_the_bar_returns_to_caret()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var selections = new List<GridSelection>();
        var cut = RenderGrid(intents, selections);
        await ClickAsync(cut, 150, 10);
        await PressIntoBarAsync(cut);
        await TypeInBarAsync(cut, "=");
        await ClickAsync(cut, 50, 70); // A4: a press on a cell points from the bar
        Assert.Equal("=A4", BarText(cut));
        Assert.Equal("point", EditingModesTold()[^1]);

        await TypeInBarAsync(cut, "=A4+");

        Assert.Equal("caret", EditingModesTold()[^1]);
        Assert.Empty(cut.FindAll(".ex-selection .ex-point"));
        // A Home forwarded all the same — by a gate not yet told — moves nothing.
        await PressAsync(cut, "Home", "=A4+", 4, fromBar: true);
        AssertTheEditStandsOnB1(cut, intents, selections);
        Assert.Equal("=A4+", BarText(cut));
    }

    [Fact] // ADR-0051 (2026-09-30) / ED-29: an edit a press moves from the cell into the bar goes into Caret, where Home, End, ← and → commit nothing
    public async Task ED29_an_edit_moved_from_overwrite_into_the_bar_goes_into_caret()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var selections = new List<GridSelection>();
        var cut = RenderGrid(intents, selections);
        await ClickAsync(cut, 150, 10);
        await PressAsync(cut, "=");
        Assert.Equal("overwrite", EditingModesTold()[^1]);

        await PressIntoBarAsync(cut);

        Assert.Equal("caret", EditingModesTold()[^1]);
        // Keys typed straight after the press, gated as Overwrite's before the gate was told.
        foreach (var key in new[] { "Home", "End", "ArrowLeft", "ArrowRight" })
            await PressAsync(cut, key, "=", 1, fromBar: true);
        AssertTheEditStandsOnB1(cut, intents, selections);
        Assert.Equal("=", BarText(cut));
    }

    [Fact] // ADR-0051 (2026-09-30, third round) / ED-29: a press into the bar while pointing ends pointing, in Caret, and the Reference stays written
    public async Task ED29_a_press_into_the_bar_while_pointing_ends_pointing_in_caret()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var selections = new List<GridSelection>();
        var cut = RenderGrid(intents, selections);
        await ClickAsync(cut, 150, 10);
        await PressAsync(cut, "=");
        await PressAsync(cut, "ArrowDown", "=", 1);
        Assert.Equal("=B2", CellEditorText(cut));
        Assert.Single(cut.FindAll(".ex-selection .ex-point"));

        await PressIntoBarAsync(cut);

        Assert.Equal("caret", EditingModesTold()[^1]);
        Assert.Empty(cut.FindAll(".ex-selection .ex-point"));
        Assert.Equal("=B2", BarText(cut));
        Assert.Equal("=B2", CellEditorText(cut));
        await PressAsync(cut, "ArrowDown", "=B2", 3, fromBar: true);
        AssertTheEditStandsOnB1(cut, intents, selections);
        Assert.Equal("=B2", BarText(cut));
    }

    [Fact] // ADR-0051/0018 / ED-29 / ED-26: the keyboard handed back to the bar an edit is pointing in keeps Point — only an edit moving in from the cell goes into Caret
    public async Task ED29_the_keyboard_handed_back_to_the_bar_keeps_point_there()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var selections = new List<GridSelection>();
        var cut = RenderGrid(intents, selections);
        await ClickAsync(cut, 150, 10);
        await PressIntoBarAsync(cut);
        await TypeInBarAsync(cut, "=");
        // The keyboard was elsewhere (another grid), and a press on a cell pointed: the core
        // hands the keyboard back to the bar, whose focus the core hears.
        await ClickAsync(cut, 50, 70); // A4
        Assert.Equal("point", EditingModesTold()[^1]);

        await PressIntoBarAsync(cut);

        Assert.Equal("point", EditingModesTold()[^1]);
        Assert.Single(cut.FindAll(".ex-selection .ex-point"));
        await ClickAsync(cut, 50, 90); // A5: the outline still stands, and moves
        Assert.Equal("=A5", BarText(cut));
        AssertTheEditStandsOnB1(cut, intents, selections);
    }

    [Fact] // ADR-0051 (2026-09-30) / ED-29: an edit that moves back into the cell keeps the mode it has — Caret, whose arrows commit nothing
    public async Task ED29_an_edit_moved_back_into_the_cell_keeps_caret()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var selections = new List<GridSelection>();
        var cut = RenderGrid(intents, selections);
        await ClickAsync(cut, 150, 10);
        await PressAsync(cut, "5");
        await PressIntoBarAsync(cut);
        Assert.Equal("caret", EditingModesTold()[^1]);

        await cut.Find(".ex-viewport .ex-editor").InputAsync(new ChangeEventArgs { Value = "56" });

        Assert.Equal("caret", EditingModesTold()[^1]);
        await PressAsync(cut, "ArrowDown", "56", 2);
        AssertTheEditStandsOnB1(cut, intents, selections);
    }

    [Fact] // ADR-0051/0012 / ED-29: in the cell nothing changes — typing onto it opens Overwrite, typing that ends Point returns to Overwrite, and the arrow commits and moves
    public async Task ED29_in_the_cell_typing_that_ends_point_still_returns_to_overwrite()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var selections = new List<GridSelection>();
        var cut = RenderGrid(intents, selections);
        await ClickAsync(cut, 150, 10);
        await PressAsync(cut, "=");
        Assert.Equal("overwrite", EditingModesTold()[^1]);
        await PressAsync(cut, "ArrowDown", "=", 1);
        Assert.Equal("point", EditingModesTold()[^1]);

        await cut.Find(".ex-viewport .ex-editor").InputAsync(new ChangeEventArgs { Value = "=B25" });

        Assert.Equal("overwrite", EditingModesTold()[^1]);
        await PressAsync(cut, "ArrowDown", "=B25", 4);
        Assert.Equal("=B25", Assert.Single(intents).Value);
        Assert.Equal(new CellPosition(1, 1), selections[^1].Focus);
    }

    [Fact] // ADR-0051 (2026-09-30) / ED-29: where no formula entry is declared, the edit a press moves into the bar goes into Caret too, and a Home the gate took as Overwrite's commits nothing
    public async Task ED29_without_formula_entry_the_bar_is_never_in_overwrite_either()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var selections = new List<GridSelection>();
        var cut = RenderGrid(intents, selections, point: false);
        await ClickAsync(cut, 150, 10);
        await PressAsync(cut, "5");

        await PressIntoBarAsync(cut);

        Assert.Equal("caret", EditingModesTold()[^1]);
        await PressAsync(cut, "Home", fromBar: true);
        AssertTheEditStandsOnB1(cut, intents, selections);
        Assert.Equal("5", BarText(cut));
    }

    [Fact] // ADR-0051 (2026-09-29, 2026-09-30) / ED-29: in the bar, an F4 rewrite that ends pointing returns to Caret, as typing does
    public async Task ED29_an_F4_rewrite_that_ends_point_in_the_bar_returns_to_caret()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var selections = new List<GridSelection>();
        // Rewrites more than the outline's Reference: the text before it changes too.
        var cut = RenderGrid(intents, selections,
            cycle: (text, _, _) => new EditorRewrite(text.Replace('5', '6'), text.Length, text.Length));
        await ClickAsync(cut, 150, 10);
        await PressIntoBarAsync(cut);
        await TypeInBarAsync(cut, "=5+");
        await ClickAsync(cut, 50, 70); // A4
        Assert.Equal("=5+A4", BarText(cut));
        Assert.Equal("point", EditingModesTold()[^1]);

        await PressAsync(cut, "F4", "=5+A4", 5, fromBar: true);

        Assert.Equal("=6+A4", BarText(cut));
        Assert.Equal("caret", EditingModesTold()[^1]);
        Assert.Empty(cut.FindAll(".ex-selection .ex-point"));
        await PressAsync(cut, "Home", "=6+A4", 5, fromBar: true);
        AssertTheEditStandsOnB1(cut, intents, selections);
    }

    /// <summary>A Chrome that paints the Cell Editor and the bar's text field, recording what it
    /// was handed.</summary>
    private sealed class EditorChrome : IGridChrome
    {
        public CellEditorContext? EditorHanded { get; private set; }

        public FormulaBarTextContext? BarHanded { get; private set; }

        public RenderFragment? FilterPanel(FilterPanelContext context) => null;

        public RenderFragment? ColumnMenu(ColumnMenuContext context) => null;

        public RenderFragment? CellEditor(CellEditorContext context)
        {
            EditorHanded = context;
            return builder => builder.AddMarkupContent(0, "<span class='stub-editor'></span>");
        }

        public RenderFragment? LoadingIndicator(LoadingContext context) => null;

        public RenderFragment? FormulaBarText(FormulaBarTextContext context)
        {
            BarHanded = context;
            return builder => builder.AddMarkupContent(0, "<span class='stub-bar'></span>");
        }
    }

    [Fact] // ADR-0051 (2026-09-30)/0030 / ED-29: under a Chrome, a press into its bar moves the edit into Caret, and its Cell Editor is told so
    public async Task ED29_a_press_into_a_chromes_bar_moves_the_edit_into_caret()
    {
        var chrome = new EditorChrome();
        var intents = new List<GridEditIntent<TestRow>>();
        var selections = new List<GridSelection>();
        var cut = RenderGrid(intents, selections, chrome: chrome);
        await ClickAsync(cut, 150, 10);
        await PressAsync(cut, "5");
        Assert.Equal(CellEditMode.Overwrite, chrome.EditorHanded!.Mode);

        await cut.InvokeAsync(() => chrome.BarHanded!.Focused());

        Assert.Equal(CellEditMode.Caret, chrome.EditorHanded!.Mode);
        Assert.Equal("caret", EditingModesTold()[^1]);
        await PressAsync(cut, "Home", "5", 1, fromBar: true);
        Assert.Empty(intents);
        Assert.Equal(B1, selections[^1].Focus);
    }
}
