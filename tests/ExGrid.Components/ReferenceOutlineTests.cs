using System.Globalization;
using System.Text.RegularExpressions;
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
/// Reference Outlines (ADR-0057; DC-1, DC-46, DC-49's layer 2 half): with the Consumer's References
/// function declared, every range it answers for the open edit's text is outlined once in the
/// selection overlay, in the colour the core gave it; Point's outline takes the colour of the range
/// it points at; a commit or a cancel takes them all away; the colour each key was given is told
/// once per change, and emptied when the edit ends. 50 rows of 20px in a 350 × 200 Viewport under
/// a 20px header; Book (A) and Note (B) edit, Amount (C) does not.
/// </summary>
public partial class ReferenceOutlineTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        new("Note", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
    ];

    /// <summary><c>A1</c>, <c>B2:C3</c> and <c>T[Col]</c> in a Formula: a cell or a range, one
    /// letter and a row number each end, or a table's column, keyed <c>t/col</c>.</summary>
    [GeneratedRegex(@"T\[(?<column>\w+)\]|(?<![A-Za-z0-9\[])(?<c1>[A-Z])(?<r1>\d+)(?::(?<c2>[A-Z])(?<r2>\d+))?")]
    private static partial Regex Token();

    /// <summary>The References in a Formula, in the shape ExSheet answers them: nothing for text
    /// that is not a Formula.</summary>
    private static IReadOnlyList<EditorReference> References(string text)
    {
        if (!text.StartsWith('='))
            return [];
        var references = new List<EditorReference>();
        foreach (Match match in Token().Matches(text))
        {
            if (match.Groups["column"].Success)
            {
                references.Add(new EditorReference(match.Index, match.Length, "t/" + match.Groups["column"].Value.ToLowerInvariant()));
                continue;
            }
            var first = CellOf(match.Groups["c1"].Value, match.Groups["r1"].Value);
            var last = match.Groups["c2"].Success ? CellOf(match.Groups["c2"].Value, match.Groups["r2"].Value) : first;
            references.Add(new EditorReference(match.Index, match.Length, SelectionRange.FromCorners(first, last)));
        }
        return references;
    }

    private static CellPosition CellOf(string letter, string row)
        => new(int.Parse(row, CultureInfo.InvariantCulture) - 1, letter[0] - 'A');

    private static bool PointAt(string text, int caret)
        => text.StartsWith('=') && caret > 0 && caret <= text.Length && "=+-*/(,".Contains(text[caret - 1]);

    private static string ReferenceText(SelectionRange range)
    {
        static string Cell(int row, int column) => FormattableString.Invariant($"{(char)('A' + column)}{row + 1}");
        var topLeft = Cell(range.TopRow, range.LeftColumn);
        return range.RowCount == 1 && range.ColumnCount == 1
            ? topLeft
            : topLeft + ":" + Cell(range.BottomRow, range.RightColumn);
    }

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        bool references = true,
        List<string>? asked = null,
        List<IReadOnlyList<ReferenceKeyColour>>? told = null,
        List<GridEditIntent<TestRow>>? intents = null,
        Action<ComponentParameterCollectionBuilder<ExGrid<TestRow>>>? more = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(50))
              .Add(g => g.TotalCount, 50)
              .Add(g => g.Columns, Columns())
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 200)
              .Add(g => g.ViewportWidth, 350)
              .Add(g => g.PointAt, PointAt)
              .Add(g => g.ReferenceText, ReferenceText)
              .Add(g => g.OnEdit, (GridEditIntent<TestRow> intent) => intents?.Add(intent));
            if (references)
            {
                ps.Add(g => g.ReferencesIn, text =>
                {
                    asked?.Add(text);
                    return References(text);
                });
            }
            if (told is not null)
                ps.Add(g => g.OnReferenceKeyColoursChanged, (IReadOnlyList<ReferenceKeyColour> keys) => told.Add(keys));
            more?.Invoke(ps);
        });

    private static Task ClickAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });

    private static Task PressAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key, string? text = null, int caret = -1, bool fromBar = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(
            key, false, false, false, false, false, fromDescendant: fromBar, editorText: text, editorCaret: caret));

    private static Task TypeAsync(IRenderedComponent<ExGrid<TestRow>> cut, string text)
        => cut.Find(".ex-viewport .ex-editor").InputAsync(new ChangeEventArgs { Value = text });

    /// <summary>Selects A1 and types <c>=</c> onto it, then the rest of <paramref name="formula"/>.</summary>
    private static async Task TypeFormulaAsync(IRenderedComponent<ExGrid<TestRow>> cut, string formula)
    {
        await ClickAsync(cut, 50, 10);
        await PressAsync(cut, "=");
        if (formula.Length > 1)
            await TypeAsync(cut, formula);
    }

    /// <summary>The solid Reference Outlines in the scrollable layer, as class and style.</summary>
    private static List<(string? Class, string? Style)> Outlines(IRenderedComponent<ExGrid<TestRow>> cut, string layer = ".ex-selection")
        => [.. cut.FindAll($"{layer} .ex-reference-outline:not(.ex-point)").Select(e => (e.GetAttribute("class"), e.GetAttribute("style")))];

    private static AngleSharp.Dom.IElement? Point(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.FindAll(".ex-selection .ex-point").SingleOrDefault();

    [Fact] // ADR-0057 / DC-46: =A1+B2:C3 outlines A1 and B2:C3, each once, in the first and second colours
    public async Task Each_range_is_outlined_in_its_colour()
    {
        var cut = RenderGrid();

        await TypeFormulaAsync(cut, "=A1+B2:C3");

        Assert.Equal(
        [
            ("ex-reference-outline ex-reference-1", "left: 0px; top: 0px; width: 100px; height: 20px"),
            ("ex-reference-outline ex-reference-2", "left: 100px; top: 20px; width: 200px; height: 40px"),
        ], Outlines(cut));
        // Paint only, and none in the pinned layer: no column is pinned.
        Assert.Empty(Outlines(cut, ".ex-selection-pinned"));
    }

    [Fact] // ADR-0057 / DC-46, the eighth Windows run: =A1+A1 names A1 twice, in one colour, and each Reference draws its own outline
    public async Task The_same_range_twice_is_outlined_twice_in_one_colour()
    {
        var cut = RenderGrid();

        await TypeFormulaAsync(cut, "=A1+A1");

        Assert.Equal(
        [
            ("ex-reference-outline ex-reference-1", "left: 0px; top: 0px; width: 100px; height: 20px"),
            ("ex-reference-outline ex-reference-1", "left: 0px; top: 0px; width: 100px; height: 20px"),
        ], Outlines(cut));
    }

    [Fact] // ADR-0057 / DC-46 / ADR-0008: one element per range, never one per cell: nine cells are one rectangle
    public async Task A_range_is_one_element_whatever_it_covers()
    {
        var cut = RenderGrid();

        await TypeFormulaAsync(cut, "=A2:C4");

        Assert.Single(cut.FindAll(".ex-reference-outline"));
        Assert.Empty(cut.FindAll(".ex-cell.ex-reference-outline, .ex-cell .ex-reference-outline, .ex-row .ex-reference-outline"));
    }

    [Fact] // ADR-0057 / DC-46 / ADR-0004: a range across the pinned boundary is one element in each layer
    public async Task A_range_across_the_pinned_boundary_is_one_element_per_layer()
    {
        var cut = RenderGrid(more: ps => ps.Add(g => g.PinnedColumnCount, 1));

        await TypeFormulaAsync(cut, "=A1:B2");

        Assert.Equal([("ex-reference-outline ex-reference-1", "left: 0px; top: 0px; width: 100px; height: 40px")], Outlines(cut, ".ex-selection-pinned"));
        Assert.Equal([("ex-reference-outline ex-reference-1", "left: 100px; top: 0px; width: 100px; height: 40px")], Outlines(cut));
    }

    [Fact] // ADR-0057 / DC-46 / ADR-0053: an outline is cut to the painted rows, and the grid never scrolls to show one
    public async Task Outlines_are_cut_to_the_painted_rows_and_never_revealed()
    {
        var cut = RenderGrid();
        await TypeFormulaAsync(cut, "=");
        var scrolls = JSInterop.Invocations.Count(i => i.Identifier == "setScrollOffset");

        await TypeAsync(cut, "=A50+B1:B50");

        var outline = Assert.Single(Outlines(cut));
        Assert.Equal("ex-reference-outline ex-reference-2", outline.Class);
        Assert.StartsWith("left: 100px; top: 0px; width: 100px; height: ", outline.Style);
        Assert.DoesNotContain("height: 1000px", outline.Style);
        Assert.Equal(scrolls, JSInterop.Invocations.Count(i => i.Identifier == "setScrollOffset"));
    }

    [Fact] // ADR-0057: a range off the grid's columns is outlined over the cells the grid has, and one wholly off it not at all
    public async Task A_range_off_the_grid_is_cut_to_it()
    {
        var cut = RenderGrid();

        await TypeFormulaAsync(cut, "=E1+B1:E1");

        Assert.Equal([("ex-reference-outline ex-reference-2", "left: 100px; top: 0px; width: 200px; height: 20px")], Outlines(cut));
    }

    [Fact] // ADR-0057 / DC-46, the eighth Windows run: what Point points at is outlined as any Reference is, in the first colour for = ↓ ↓, and Point's dashes lie over it
    public async Task Points_dashes_lie_over_its_references_outline()
    {
        var cut = RenderGrid();
        await TypeFormulaAsync(cut, "=");

        await PressAsync(cut, "ArrowDown", text: "=", caret: 1);
        await PressAsync(cut, "ArrowDown", text: "=A2", caret: 3);

        var point = Point(cut);
        Assert.NotNull(point);
        Assert.Equal("ex-point ex-point-on-reference", point.GetAttribute("class"));
        Assert.Equal("left: 0px; top: 40px; width: 100px; height: 20px", point.GetAttribute("style"));
        // A3 has its own outline in its colour beneath the dashes, as Excel draws it.
        Assert.Equal([("ex-reference-outline ex-reference-1", "left: 0px; top: 40px; width: 100px; height: 20px")], Outlines(cut));
    }

    [Fact] // ADR-0057 / DC-46: pointed after another Reference, what Point points at takes the second colour, and Point's dashes lie over it
    public async Task Point_after_another_reference_takes_the_next_colour()
    {
        var cut = RenderGrid();
        await TypeFormulaAsync(cut, "=B1+");

        await PressAsync(cut, "ArrowDown", text: "=B1+", caret: 4);

        Assert.Equal("=B1+A2", cut.Find(".ex-viewport .ex-editor").GetAttribute("value"));
        Assert.Equal("ex-point ex-point-on-reference", Point(cut)!.GetAttribute("class"));
        Assert.Equal(
        [
            ("ex-reference-outline ex-reference-1", "left: 100px; top: 0px; width: 100px; height: 20px"),
            ("ex-reference-outline ex-reference-2", "left: 0px; top: 20px; width: 100px; height: 20px"),
        ], Outlines(cut));
    }

    [Fact] // ADR-0057 / DC-46, the eighth Windows run: pointing at cells the text already names shares their colour, and each Reference draws its outline
    public async Task Pointing_at_a_range_already_named_shares_its_colour()
    {
        var cut = RenderGrid();
        await TypeFormulaAsync(cut, "=A2+");

        await PressAsync(cut, "ArrowDown", text: "=A2+", caret: 4);

        Assert.Equal("=A2+A2", cut.Find(".ex-viewport .ex-editor").GetAttribute("value"));
        Assert.Equal("ex-point ex-point-on-reference", Point(cut)!.GetAttribute("class"));
        Assert.Equal(
        [
            ("ex-reference-outline ex-reference-1", "left: 0px; top: 20px; width: 100px; height: 20px"),
            ("ex-reference-outline ex-reference-1", "left: 0px; top: 20px; width: 100px; height: 20px"),
        ], Outlines(cut));
    }

    [Fact] // ADR-0057 / DC-46: Enter commits and takes every outline away
    public async Task A_commit_removes_every_outline()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(intents: intents);
        await TypeFormulaAsync(cut, "=A1+B2:C3");
        Assert.Equal(2, cut.FindAll(".ex-reference-outline").Count);

        await PressAsync(cut, "Enter", text: "=A1+B2:C3", caret: 9);

        Assert.Equal("=A1+B2:C3", Assert.Single(intents).Value);
        Assert.Empty(cut.FindAll(".ex-reference-outline"));
        Assert.Empty(cut.FindAll(".ex-point"));
    }

    [Fact] // ADR-0057 / DC-46: Escape cancels and takes every outline away, Point's included
    public async Task A_cancel_removes_every_outline()
    {
        var cut = RenderGrid();
        await TypeFormulaAsync(cut, "=B1+");
        await PressAsync(cut, "ArrowDown", text: "=B1+", caret: 4);
        Assert.Equal(2, cut.FindAll(".ex-selection .ex-reference-outline").Count);

        await PressAsync(cut, "Escape", text: "=B1+A2", caret: 6);

        Assert.Empty(cut.FindAll(".ex-reference-outline"));
        Assert.Empty(cut.FindAll(".ex-point"));
    }

    [Fact] // ADR-0057 / DC-46: selecting a cell that holds a Formula, without editing it, outlines nothing; F2 does
    public async Task Only_an_open_edit_is_outlined()
    {
        var cut = RenderGrid(more: ps => ps.Add(g => g.EditorTextOf, (_, _) => "=B2"));

        await ClickAsync(cut, 50, 10);
        Assert.Empty(cut.FindAll(".ex-reference-outline"));

        await PressAsync(cut, "F2");
        Assert.Equal([("ex-reference-outline ex-reference-1", "left: 100px; top: 20px; width: 100px; height: 20px")], Outlines(cut));
    }

    [Fact] // ADR-0057 / DC-46: text that is not a Formula is answered with nothing and outlines nothing
    public async Task A_constant_outlines_nothing()
    {
        var cut = RenderGrid();
        await ClickAsync(cut, 50, 10);
        await PressAsync(cut, "A");

        await TypeAsync(cut, "A1+B2");

        Assert.Empty(cut.FindAll(".ex-reference-outline"));
    }

    [Fact] // ADR-0057 / DC-46: typed into the Formula Bar, the outlines follow its text
    public async Task The_formula_bar_is_outlined_as_the_cell_is()
    {
        var cut = RenderGrid(more: ps => ps.Add(g => g.ShowFormulaBar, true));
        await ClickAsync(cut, 50, 10);
        await cut.Find(".ex-formula-bar-text").FocusAsync(new FocusEventArgs());

        await cut.Find(".ex-formula-bar-text").InputAsync(new ChangeEventArgs { Value = "=A1+B2:C3" });

        Assert.Equal(2, Outlines(cut).Count);
        await cut.Find(".ex-formula-bar-text").InputAsync(new ChangeEventArgs { Value = "=A1+B2" });
        Assert.Equal(
        [
            ("ex-reference-outline ex-reference-1", "left: 0px; top: 0px; width: 100px; height: 20px"),
            ("ex-reference-outline ex-reference-2", "left: 100px; top: 20px; width: 100px; height: 20px"),
        ], Outlines(cut));
    }

    [Fact] // ADR-0057 / DC-46: the function is asked on each change of the text and not again for a render that did not change it
    public async Task Asked_once_per_text()
    {
        var asked = new List<string>();
        var cut = RenderGrid(asked: asked);
        await TypeFormulaAsync(cut, "=A1");

        await TypeAsync(cut, "=A1+");
        await cut.InvokeAsync(() => cut.Instance.OnEditorCaretAsync("=A1+", 2));
        cut.Render();

        Assert.Equal(["=", "=A1", "=A1+"], asked);
    }

    [Fact] // ADR-0057 / DC-1: without the function nothing is outlined and Point's outline keeps its own look
    public async Task Without_the_function_nothing_changes()
    {
        var cut = RenderGrid(references: false);
        await TypeFormulaAsync(cut, "=A1+B2:C3+");

        await PressAsync(cut, "ArrowDown", text: "=A1+B2:C3+", caret: 10);

        Assert.Empty(cut.FindAll(".ex-reference-outline"));
        Assert.Empty(cut.FindAll("[class*='ex-reference']"));
        Assert.Equal("ex-point", Point(cut)!.GetAttribute("class"));
    }

    [Fact] // ADR-0057 / DC-49: the colour each key was given is told once per change, and emptied when the edit ends
    public async Task Key_colours_are_told_when_they_change()
    {
        var told = new List<IReadOnlyList<ReferenceKeyColour>>();
        var cut = RenderGrid(told: told);
        await TypeFormulaAsync(cut, "=A1");
        // No key yet: nothing to tell, and nothing was told before.
        Assert.Empty(told);

        await TypeAsync(cut, "=A1+T[PV]");
        Assert.Equal([new ReferenceKeyColour("t/pv", new ReferenceColour(2))], Assert.Single(told));

        // Neither the keys nor their colours changed: nothing is told.
        await TypeAsync(cut, "=A1+T[PV]+");
        await TypeAsync(cut, "=A1+T[PV]+A1");
        Assert.Single(told);

        // A key's colour moves when a range comes to stand before it.
        await TypeAsync(cut, "=B1+A1+T[PV]+A1");
        Assert.Equal(2, told.Count);
        Assert.Equal([new ReferenceKeyColour("t/pv", new ReferenceColour(3))], told[^1]);

        await TypeAsync(cut, "=B1+A1+T[PV]+T[qty]");
        Assert.Equal(
            [new ReferenceKeyColour("t/pv", new ReferenceColour(3)), new ReferenceKeyColour("t/qty", new ReferenceColour(4))],
            told[^1]);

        // The edit ends: an empty set, once.
        await PressAsync(cut, "Escape", text: "=B1+A1+T[PV]+T[qty]", caret: 19);
        Assert.Equal(4, told.Count);
        Assert.Empty(told[^1]);
        cut.Render();
        Assert.Equal(4, told.Count);
    }

    [Fact] // ADR-0057 / DC-49: an edit whose keys went before it ended tells nothing more when it ends
    public async Task An_edit_with_no_keys_left_tells_nothing_at_its_end()
    {
        var told = new List<IReadOnlyList<ReferenceKeyColour>>();
        var intents = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(told: told, intents: intents);
        await TypeFormulaAsync(cut, "=T[PV]");
        await TypeAsync(cut, "=A1");
        Assert.Equal(2, told.Count);
        Assert.Empty(told[^1]);

        await PressAsync(cut, "Enter", text: "=A1", caret: 3);

        Assert.Single(intents);
        Assert.Equal(2, told.Count);
    }

    [Fact] // ADR-0057 / DC-49: the keys are told from a commit as from a cancel
    public async Task A_commit_tells_the_empty_set()
    {
        var told = new List<IReadOnlyList<ReferenceKeyColour>>();
        var cut = RenderGrid(told: told);
        await TypeFormulaAsync(cut, "=SUM(T[PV])");

        await PressAsync(cut, "Enter", text: "=SUM(T[PV])", caret: 11);

        Assert.Equal(2, told.Count);
        Assert.Equal([new ReferenceKeyColour("t/pv", new ReferenceColour(1))], told[0]);
        Assert.Empty(told[1]);
    }

    /// <summary>A Chrome that paints the Cell Editor and keeps its context, to report text as a
    /// substituted control does.</summary>
    private sealed class EditorChrome : IGridChrome
    {
        public CellEditorContext? Context { get; private set; }

        public RenderFragment? FilterPanel(FilterPanelContext context) => null;

        public RenderFragment? ColumnMenu(ColumnMenuContext context) => null;

        public RenderFragment? CellEditor(CellEditorContext context)
        {
            Context = context;
            return builder => builder.AddMarkupContent(0, "<input class='stub-editor' />");
        }

        public RenderFragment? LoadingIndicator(LoadingContext context) => null;
    }

    [Fact] // ADR-0057/0010 / DC-46: text a Chrome's editor reports is outlined as the built-in editor's is
    public async Task A_chrome_editors_text_is_outlined()
    {
        var chrome = new EditorChrome();
        var cut = RenderGrid(more: ps => ps.Add(g => g.Chrome, chrome));
        await ClickAsync(cut, 50, 10);
        await PressAsync(cut, "=");

        await cut.InvokeAsync(() => chrome.Context!.TextChanged("=B2:C3"));

        Assert.Equal([("ex-reference-outline ex-reference-1", "left: 100px; top: 20px; width: 200px; height: 40px")], Outlines(cut));
    }

    [Fact] // ADR-0003 / ADR-0057: outlines are overlay paint, and no row renders for them
    public async Task Outlines_render_no_row()
    {
        var cut = RenderGrid();
        await ClickAsync(cut, 50, 10);
        await PressAsync(cut, "=");
        var before = cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToList();

        await TypeAsync(cut, "=A1+B2:C3");
        await TypeAsync(cut, "=A1+B2:C3+");
        await PressAsync(cut, "ArrowDown", text: "=A1+B2:C3+", caret: 10);
        await PressAsync(cut, "Escape", text: "=A1+B2:C3+A2", caret: 12);

        Assert.Equal(before, cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToList());
    }
}
