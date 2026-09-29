using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
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
/// The coloured text (ADR-0057; DC-1, DC-47 and DC-48's layer 2 half): with the Consumer's
/// References function declared, beneath each editor surface the core renders a layer holding the
/// editor's text, each Reference a span in its colour, and the text it was rendered for on the
/// layer itself — immediately before the core's own field, or handed to a Chrome to place before
/// its control. Whether it shows is the listener's to decide in the browser (layer 3). Without the
/// declaration there is no layer. 50 rows of 20px in a 350 × 200 Viewport; Book (A) and Note (B)
/// edit, Amount (C) does not.
/// </summary>
public partial class ReferenceTextTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        new("Note", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
    ];

    /// <summary><c>A1</c> or <c>B2:C3</c> in a Formula: one letter and a row number each end.</summary>
    [GeneratedRegex(@"(?<![A-Za-z0-9])(?<c1>[A-Z])(?<r1>\d+)(?::(?<c2>[A-Z])(?<r2>\d+))?")]
    private static partial Regex Token();

    /// <summary>The References in a Formula, as ExSheet answers them: nothing for text that is
    /// not a Formula.</summary>
    private static IReadOnlyList<EditorReference> References(string text)
    {
        if (!text.StartsWith('='))
            return [];
        var references = new List<EditorReference>();
        foreach (Match match in Token().Matches(text))
        {
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
        => FormattableString.Invariant($"{(char)('A' + range.LeftColumn)}{range.TopRow + 1}");

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        bool references = true,
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
              .Add(g => g.ReferenceText, ReferenceText);
            if (references)
                ps.Add(g => g.ReferencesIn, References);
            more?.Invoke(ps);
        });

    private static Task ClickAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });

    private static Task PressAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key, string? text = null, int caret = -1)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(
            key, false, false, false, false, false, fromDescendant: false, editorText: text, editorCaret: caret));

    private static Task TypeAsync(IRenderedComponent<ExGrid<TestRow>> cut, string text)
        => cut.Find(".ex-viewport input.ex-editor").InputAsync(new ChangeEventArgs { Value = text });

    /// <summary>Selects A1 and types <c>=</c> onto it, then the rest of <paramref name="formula"/>.</summary>
    private static async Task TypeFormulaAsync(IRenderedComponent<ExGrid<TestRow>> cut, string formula)
    {
        await ClickAsync(cut, 50, 10);
        await PressAsync(cut, "=");
        if (formula.Length > 1)
            await TypeAsync(cut, formula);
    }

    /// <summary>What a layer draws, as markup: its text, each Reference a span in its colour.</summary>
    private static string Drawn(IElement layer)
    {
        var line = Assert.Single(layer.Children);
        Assert.Equal("ex-reference-text-line", line.ClassName);
        return line.InnerHtml;
    }

    [Fact] // ADR-0057 / DC-47: beneath the Cell Editor, immediately before it and in its box, the text with each Reference in its colour
    public async Task The_cell_editors_layer_draws_each_reference_in_its_colour()
    {
        var cut = RenderGrid();

        await TypeFormulaAsync(cut, "=A1+B2:C3");

        var layer = cut.Find(".ex-viewport > .ex-reference-text");
        var editor = cut.Find(".ex-viewport > input.ex-editor");
        Assert.Equal("ex-reference-text ex-reference-text-cell", layer.ClassName);
        Assert.Equal(editor.OuterHtml, layer.NextElementSibling!.OuterHtml);
        // In the editor's own box (DC-48): the same inline geometry, the rest from one rule.
        Assert.Equal(editor.GetAttribute("style"), layer.GetAttribute("style"));
        // The text it was rendered for, where the listener compares it with the field's value.
        Assert.Equal("=A1+B2:C3", layer.GetAttribute("data-ex-text"));
        Assert.Equal("true", layer.GetAttribute("aria-hidden"));
        Assert.Equal("=<span class=\"ex-reference-1\">A1</span>+<span class=\"ex-reference-2\">B2:C3</span>", Drawn(layer));
    }

    [Fact] // ADR-0057 / DC-47: the layer follows each input, a range named twice keeping one colour, and nothing is added to or left out of the text
    public async Task The_layer_follows_each_input()
    {
        var cut = RenderGrid();
        await TypeFormulaAsync(cut, "=A1");
        var layer = cut.Find(".ex-viewport > .ex-reference-text");
        Assert.Equal("=<span class=\"ex-reference-1\">A1</span>", Drawn(layer));

        await TypeAsync(cut, "= A1  +B1+A1 ");

        layer = cut.Find(".ex-viewport > .ex-reference-text");
        Assert.Equal("= A1  +B1+A1 ", layer.GetAttribute("data-ex-text"));
        Assert.Equal("= A1  +B1+A1 ", layer.TextContent);
        Assert.Equal(
            "= <span class=\"ex-reference-1\">A1</span>  +<span class=\"ex-reference-2\">B1</span>+<span class=\"ex-reference-1\">A1</span> ",
            Drawn(layer));
    }

    [Fact] // ADR-0057 / DC-47: text that is not a Formula is drawn as it stands, with no colour
    public async Task A_constant_is_drawn_uncoloured()
    {
        var cut = RenderGrid();
        await ClickAsync(cut, 50, 10);
        await PressAsync(cut, "A");

        await TypeAsync(cut, "A1+B2");

        var layer = cut.Find(".ex-viewport > .ex-reference-text");
        Assert.Equal("A1+B2", layer.GetAttribute("data-ex-text"));
        Assert.Equal("A1+B2", Drawn(layer));
    }

    [Fact] // ADR-0057 / DC-47: a Reference pointed is written into the layer as into the editor
    public async Task A_pointed_reference_is_drawn_in_its_colour()
    {
        var cut = RenderGrid();
        await TypeFormulaAsync(cut, "=B1+");

        await PressAsync(cut, "ArrowDown", text: "=B1+", caret: 4);

        var layer = cut.Find(".ex-viewport > .ex-reference-text");
        Assert.Equal("=B1+A2", layer.GetAttribute("data-ex-text"));
        Assert.Equal("=<span class=\"ex-reference-1\">B1</span>+<span class=\"ex-reference-2\">A2</span>", Drawn(layer));
    }

    [Fact] // ADR-0057 / DC-48: over a pinned cell, the layer rides the editor's sticky anchor, immediately before it
    public async Task Over_a_pinned_cell_the_layer_rides_the_editors_anchor()
    {
        var cut = RenderGrid(more: ps => ps.Add(g => g.PinnedColumnCount, 1));

        await TypeFormulaAsync(cut, "=B2");

        var layer = cut.Find(".ex-editor-pinned > .ex-reference-text");
        var editor = cut.Find(".ex-editor-pinned > input.ex-editor");
        Assert.Equal(editor.OuterHtml, layer.NextElementSibling!.OuterHtml);
        Assert.Equal(editor.GetAttribute("style"), layer.GetAttribute("style"));
        Assert.Equal("=<span class=\"ex-reference-1\">B2</span>", Drawn(layer));
        Assert.Empty(cut.FindAll(".ex-viewport > .ex-reference-text"));
    }

    [Fact] // ADR-0057 / DC-47: a commit and a cancel take the Cell Editor's layer with the editor
    public async Task The_layer_goes_with_the_editor()
    {
        var cut = RenderGrid();
        await TypeFormulaAsync(cut, "=A1");

        await PressAsync(cut, "Escape", text: "=A1", caret: 3);

        Assert.Empty(cut.FindAll(".ex-viewport .ex-reference-text"));
    }

    [Fact] // ADR-0057 / DC-47 / DC-48: the Formula Bar's layer stands beneath its field from the Name Box's edge, empty until an edit opens, then the edit's text
    public async Task The_formula_bars_layer_holds_the_edits_text_while_one_is_open()
    {
        var cut = RenderGrid(more: ps => ps.Add(g => g.ShowFormulaBar, true));
        await ClickAsync(cut, 50, 10);

        var layer = cut.Find(".ex-formula-bar > .ex-reference-text");
        var field = cut.Find(".ex-formula-bar > input.ex-formula-bar-text");
        Assert.Equal("ex-reference-text ex-reference-text-bar", layer.ClassName);
        Assert.Equal(field.OuterHtml, layer.NextElementSibling!.OuterHtml);
        // The field starts where the Name Box ends, in the resolved metrics (ADR-0028).
        var nameBoxWidth = Regex.Match(cut.Find("input.ex-name-box").GetAttribute("style")!, @"width: (?<px>[\d.]+)px").Groups["px"].Value;
        Assert.Equal($"left: {nameBoxWidth}px", layer.GetAttribute("style"));
        // Standing, and empty, with no edit open: an edit opening is a change of its text.
        Assert.Equal("", layer.GetAttribute("data-ex-text"));
        Assert.Equal("", Drawn(layer));

        await PressAsync(cut, "=");
        await TypeAsync(cut, "=A1+B2");
        layer = cut.Find(".ex-formula-bar > .ex-reference-text");
        Assert.Equal("=A1+B2", layer.GetAttribute("data-ex-text"));
        Assert.Equal("=<span class=\"ex-reference-1\">A1</span>+<span class=\"ex-reference-2\">B2</span>", Drawn(layer));

        await PressAsync(cut, "Escape", text: "=A1+B2", caret: 6);
        layer = cut.Find(".ex-formula-bar > .ex-reference-text");
        Assert.Equal("", layer.GetAttribute("data-ex-text"));
        Assert.Equal("", Drawn(layer));
    }

    [Fact] // ADR-0057 / DC-47: typed into the Formula Bar, both surfaces' layers hold the text
    public async Task Typed_into_the_bar_both_layers_hold_the_text()
    {
        var cut = RenderGrid(more: ps => ps.Add(g => g.ShowFormulaBar, true));
        await ClickAsync(cut, 50, 10);
        await cut.Find("input.ex-formula-bar-text").FocusAsync(new FocusEventArgs());

        await cut.Find("input.ex-formula-bar-text").InputAsync(new ChangeEventArgs { Value = "=C3" });

        Assert.Equal("=C3", cut.Find(".ex-formula-bar > .ex-reference-text").GetAttribute("data-ex-text"));
        Assert.Equal("=C3", cut.Find(".ex-viewport > .ex-reference-text").GetAttribute("data-ex-text"));
        Assert.Equal("=<span class=\"ex-reference-1\">C3</span>", Drawn(cut.Find(".ex-viewport > .ex-reference-text")));
    }

    [Fact] // ADR-0057 / DC-1: without the function there is no layer, on either surface, and a Chrome is handed none
    public async Task Without_the_function_there_is_no_layer()
    {
        var chrome = new LayerChrome();
        var builtIn = RenderGrid(references: false, more: ps => ps.Add(g => g.ShowFormulaBar, true));
        var substituted = RenderGrid(references: false, more: ps => ps.Add(g => g.ShowFormulaBar, true).Add(g => g.Chrome, chrome));

        foreach (var cut in new[] { builtIn, substituted })
        {
            await ClickAsync(cut, 50, 10);
            await PressAsync(cut, "=");
            Assert.Empty(cut.FindAll(".ex-reference-text"));
            Assert.Empty(cut.FindAll("[data-ex-text]"));
        }
        Assert.Null(chrome.Editor!.ReferenceText);
        Assert.Null(chrome.Bar!.ReferenceText);
    }

    /// <summary>A Chrome that paints the Cell Editor and the Formula Bar's field, placing the
    /// coloured text immediately before each control as the contract asks, and keeping the
    /// contexts it was handed.</summary>
    private sealed class LayerChrome : IGridChrome
    {
        public CellEditorContext? Editor { get; private set; }

        public FormulaBarTextContext? Bar { get; private set; }

        public RenderFragment? FilterPanel(FilterPanelContext context) => null;

        public RenderFragment? ColumnMenu(ColumnMenuContext context) => null;

        public RenderFragment? LoadingIndicator(LoadingContext context) => null;

        public RenderFragment? CellEditor(CellEditorContext context)
        {
            Editor = context;
            return builder =>
            {
                builder.AddContent(0, context.ReferenceText);
                builder.AddMarkupContent(1, "<input class='stub-editor' />");
            };
        }

        public RenderFragment? FormulaBarText(FormulaBarTextContext context)
        {
            Bar = context;
            return builder =>
            {
                builder.AddContent(0, context.ReferenceText);
                builder.AddMarkupContent(1, "<input class='stub-bar' />");
            };
        }
    }

    [Fact] // ADR-0057/0010 / DC-47 / DC-48: a Chrome is handed the layer and places it before its control, inside the core's box
    public async Task A_chrome_places_the_layer_it_is_handed()
    {
        var chrome = new LayerChrome();
        var cut = RenderGrid(more: ps => ps.Add(g => g.ShowFormulaBar, true).Add(g => g.Chrome, chrome));
        await ClickAsync(cut, 50, 10);
        await PressAsync(cut, "=");

        await cut.InvokeAsync(() => chrome.Editor!.TextChanged("=B2:C3+A1"));

        const string drawn = "=<span class=\"ex-reference-1\">B2:C3</span>+<span class=\"ex-reference-2\">A1</span>";
        var cell = cut.Find(".ex-viewport > div.ex-editor > .ex-reference-text");
        Assert.Equal("ex-reference-text", cell.ClassName);
        Assert.Null(cell.GetAttribute("style"));
        Assert.Equal("stub-editor", cell.NextElementSibling!.ClassName);
        Assert.Equal("=B2:C3+A1", cell.GetAttribute("data-ex-text"));
        Assert.Equal("true", cell.GetAttribute("aria-hidden"));
        Assert.Equal(drawn, Drawn(cell));
        var bar = cut.Find(".ex-formula-bar > div.ex-editor.ex-formula-bar-text > .ex-reference-text");
        Assert.Equal("stub-bar", bar.NextElementSibling!.ClassName);
        Assert.Equal("=B2:C3+A1", bar.GetAttribute("data-ex-text"));
        Assert.Equal(drawn, Drawn(bar));
        // The core renders none of its own beside a Chrome's control.
        Assert.Empty(cut.FindAll(".ex-viewport > .ex-reference-text, .ex-formula-bar > .ex-reference-text"));
    }

    [Fact] // ADR-0003 / ADR-0057: the layer is the editor's paint, and no row renders for it
    public async Task Typing_over_the_layer_renders_no_row()
    {
        var cut = RenderGrid(more: ps => ps.Add(g => g.ShowFormulaBar, true));
        await ClickAsync(cut, 50, 10);
        await PressAsync(cut, "=");
        var before = cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToList();

        await TypeAsync(cut, "=A1");
        await TypeAsync(cut, "=A1+B2:C3");
        await TypeAsync(cut, "=A1+B2:C3+");
        await PressAsync(cut, "ArrowDown", text: "=A1+B2:C3+", caret: 10);
        await PressAsync(cut, "Escape", text: "=A1+B2:C3+A2", caret: 12);

        Assert.Equal(before, cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToList());
    }
}
