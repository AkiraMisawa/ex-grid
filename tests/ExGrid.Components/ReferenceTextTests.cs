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
/// editor's text as one run, and on the layer itself the text it was rendered for and which of the
/// grid's highlights covers which characters (ADR-0057, note of 2026-10-01) — immediately before the
/// core's own field, or handed to a Chrome to place before its control. The tests read the colouring
/// back as markup, each Reference a span in its colour (<c>ColouredText</c>). Whether it shows is the listener's to decide in the browser (layer 3). Without the
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

    /// <summary>What a layer colours, written as markup: its text, each Reference a span in its colour
    /// (<c>ColouredText</c>).</summary>
    private static string Drawn(IElement layer) => global::ReferenceText.ColouredText.Of(layer);

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
        Assert.Equal("=<span class=\"ex-reference-1\">B1</span>+<span class=\"ex-reference-2 ex-reference-pointed\">A2</span>", Drawn(layer));
    }

    /// <summary>The <c>setCaret</c> calls the core made: the text, and the selection it placed in it.</summary>
    private List<(string Text, int Start, int End)> SelectionsPlaced()
        => [.. JSInterop.Invocations.Where(i => i.Identifier == "setCaret")
            .Select(i => ((string)i.Arguments[0]!, (int)i.Arguments[1]!, (int)i.Arguments[2]!))];

    [Theory] // ADR-0051 / ADR-0057 (cases 29–31): the Reference Point is writing is shown selected, in both surfaces' layers
    [InlineData("=SUM(", "=SUM(<span class=\"ex-reference-1 ex-reference-pointed\">A2</span>")]
    [InlineData("=1+", "=1+<span class=\"ex-reference-1 ex-reference-pointed\">A2</span>")]
    public async Task The_reference_point_is_writing_is_shown_selected(string typed, string drawn)
    {
        var cut = RenderGrid(more: ps => ps.Add(g => g.ShowFormulaBar, true));
        await TypeFormulaAsync(cut, typed);

        await PressAsync(cut, "ArrowDown", text: typed, caret: typed.Length);

        Assert.Equal(drawn, Drawn(cut.Find(".ex-viewport > .ex-reference-text")));
        // Only the layer of the surface the edit is in shows, which is the listener's to decide:
        // both carry the look.
        Assert.Equal(drawn, Drawn(cut.Find(".ex-formula-bar > .ex-reference-text")));
    }

    /// <summary>Types <paramref name="formula"/> onto A1 and reports the caret at its end, as the
    /// listener does, then writes <paramref name="written"/> as a press outside the grid does
    /// (ADR-0058).</summary>
    private static async Task WriteFromOutsideAsync(IRenderedComponent<ExGrid<TestRow>> cut, string formula, string written)
    {
        await TypeFormulaAsync(cut, formula);
        await cut.InvokeAsync(() => cut.Instance.OnEditorCaretAsync(formula, formula.Length));
        Assert.True(await cut.InvokeAsync(() => cut.Instance.WritePointedTextAsync(written)));
    }

    [Fact] // ADR-0058 / ADR-0057 (2026-09-30) / SH-34: text written from outside is shown selected as a whole, its References in their colours inside it, in both surfaces' layers
    public async Task Text_written_from_outside_is_shown_selected_as_a_whole()
    {
        var cut = RenderGrid(more: ps => ps.Add(g => g.ShowFormulaBar, true));

        await WriteFromOutsideAsync(cut, "=1+", "SUM(A5, B6)");

        const string drawn = "=1+<span class=\"ex-reference-pointed\">SUM(<span class=\"ex-reference-1\">A5</span>, "
            + "<span class=\"ex-reference-2\">B6</span>)</span>";
        Assert.Equal(drawn, Drawn(cut.Find(".ex-viewport > .ex-reference-text")));
        Assert.Equal(drawn, Drawn(cut.Find(".ex-formula-bar > .ex-reference-text")));
    }

    [Theory] // ADR-0058 / ADR-0057 / SH-34: what is written from outside wears the look as one Reference does when it is one, and as a whole when it holds none
    [InlineData("A5", "=1+<span class=\"ex-reference-1 ex-reference-pointed\">A5</span>")]
    [InlineData("PI()", "=1+<span class=\"ex-reference-pointed\">PI()</span>")]
    public async Task Text_written_from_outside_wears_the_look_whatever_it_holds(string written, string drawn)
    {
        var cut = RenderGrid();

        await WriteFromOutsideAsync(cut, "=1+", written);

        Assert.Equal(drawn, Drawn(cut.Find(".ex-viewport > .ex-reference-text")));
    }

    [Fact] // ADR-0058 / ADR-0057 (cases 19, 20x) / SH-34: text written from outside straight after the text's first character is not shown selected
    public async Task Text_written_from_outside_straight_after_the_first_character_is_not_shown_selected()
    {
        var cut = RenderGrid();

        await WriteFromOutsideAsync(cut, "=", "SUM(A5, B6)");

        Assert.Equal(
            "=SUM(<span class=\"ex-reference-1\">A5</span>, <span class=\"ex-reference-2\">B6</span>)",
            Drawn(cut.Find(".ex-viewport > .ex-reference-text")));
    }

    [Fact] // ADR-0058 / ADR-0057: a pointed span that cuts through a Reference wears no look — half a Reference on the grey would say the rest was pointed too
    public async Task A_pointed_span_that_cuts_a_reference_wears_no_look()
    {
        // The Consumer reads 1+A5 as one Reference, which begins before what was written.
        static IReadOnlyList<EditorReference> Across(string text)
            => text == "=1+A5" ? [new EditorReference(1, 4, new SelectionRange(4, 0, 1, 1))] : References(text);
        var cut = RenderGrid(references: false, more: ps => ps.Add(g => g.ReferencesIn, Across));

        await WriteFromOutsideAsync(cut, "=1+", "A5");

        Assert.Equal("=<span class=\"ex-reference-1\">1+A5</span>", Drawn(cut.Find(".ex-viewport > .ex-reference-text")));
    }

    [Fact] // ADR-0051 / ADR-0057 (cases 19, 20x): a Reference straight after the text's first character is not shown selected, however often it is pointed
    public async Task A_reference_pointed_straight_after_the_first_character_is_not_shown_selected()
    {
        var cut = RenderGrid();
        await TypeFormulaAsync(cut, "=");

        await PressAsync(cut, "ArrowDown", text: "=", caret: 1);
        Assert.Equal("=<span class=\"ex-reference-1\">A2</span>", Drawn(cut.Find(".ex-viewport > .ex-reference-text")));
        await PressAsync(cut, "ArrowDown", text: "=A2", caret: 3);

        var layer = cut.Find(".ex-viewport > .ex-reference-text");
        Assert.Equal("=A3", layer.GetAttribute("data-ex-text"));
        Assert.Equal("=<span class=\"ex-reference-1\">A3</span>", Drawn(layer));
        Assert.Single(cut.FindAll(".ex-point"));
    }

    [Fact] // ADR-0051 / ADR-0057 (case 32): the look is not a selection — the caret is placed after the Reference with nothing selected, and a key typed next follows it and ends pointing
    public async Task A_key_typed_after_pointing_follows_the_reference()
    {
        var cut = RenderGrid();
        await TypeFormulaAsync(cut, "=B2+");
        await PressAsync(cut, "ArrowDown", text: "=B2+", caret: 4);
        await PressAsync(cut, "ArrowDown", text: "=B2+A2", caret: 6);
        Assert.Equal(
            "=<span class=\"ex-reference-1\">B2</span>+<span class=\"ex-reference-2 ex-reference-pointed\">A3</span>",
            Drawn(cut.Find(".ex-viewport > .ex-reference-text")));
        // The field's text is never selected: every placement while pointing is a caret, after
        // the Reference.
        Assert.Equal([("=B2+A2", 6, 6), ("=B2+A3", 6, 6)], SelectionsPlaced().Where(p => p.Text.StartsWith("=B2+A", StringComparison.Ordinal)));

        // The browser types at that caret: the digit follows the Reference.
        await TypeAsync(cut, "=B2+A35");

        var layer = cut.Find(".ex-viewport > .ex-reference-text");
        Assert.Equal("=B2+A35", layer.GetAttribute("data-ex-text"));
        Assert.Equal("=<span class=\"ex-reference-1\">B2</span>+<span class=\"ex-reference-2\">A35</span>", Drawn(layer));
        Assert.Empty(cut.FindAll(".ex-point"));
    }

    [Fact] // ADR-0051 / ADR-0057: F2 from Point moves the caret again, the Reference left written and no longer shown selected
    public async Task F2_takes_the_look_with_the_outline()
    {
        var cut = RenderGrid();
        await TypeFormulaAsync(cut, "=SUM(");
        await PressAsync(cut, "ArrowDown", text: "=SUM(", caret: 5);

        await PressAsync(cut, "F2", text: "=SUM(A2", caret: 7);

        Assert.Equal("=SUM(<span class=\"ex-reference-1\">A2</span>", Drawn(cut.Find(".ex-viewport > .ex-reference-text")));
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

    [Fact] // ADR-0057 (note of 2026-10-01) / ADR-0018 / DC-48 / DC-51: the layer's text is one run; its colours are highlights named for the grid, which its own stylesheet paints from the layer's properties
    public async Task The_layer_is_one_run_coloured_by_highlights_of_the_grids_own()
    {
        var first = RenderGrid(more: ps => ps.Add(g => g.ShowFormulaBar, true));
        var second = RenderGrid(more: ps => ps.Add(g => g.ShowFormulaBar, true));
        await TypeFormulaAsync(first, "=A1+B2+A1");
        await TypeFormulaAsync(second, "=B2");

        var prefixes = new List<string>();
        foreach (var cut in new[] { first, second })
        {
            // The grid's id prefix, as its cells carry it.
            var cellId = cut.Find("[id$='-r0c0']").Id!;
            var prefix = cellId[..^"r0c0".Length] + "reference-";
            prefixes.Add(prefix);

            // One run: the line holds the text and no element, on both surfaces.
            foreach (var layer in cut.FindAll(".ex-reference-text"))
            {
                var line = Assert.Single(layer.Children);
                Assert.Empty(line.Children);
                Assert.Equal(layer.GetAttribute("data-ex-text"), line.TextContent);
            }

            // Each stretch names one of this grid's highlights.
            var colours = cut.Find(".ex-viewport > .ex-reference-text").GetAttribute("data-ex-colours")!;
            Assert.All(colours.Split(' '), entry => Assert.StartsWith(prefix, entry.Split(',')[2], StringComparison.Ordinal));

            // The grid's own stylesheet paints its names, and only them: each colour, each pointed
            // shade and the ground, from the property the shipped stylesheet declares on the layer.
            var css = string.Concat(cut.FindAll(".ex-grid > style").Select(style => style.TextContent));
            for (var place = 1; place <= ReferenceColour.PaletteLength; place++)
            {
                var n = place.ToString(CultureInfo.InvariantCulture);
                Assert.Contains($"::highlight({prefix}{n}){{color:var(--ex-reference-text-{n})}}", css, StringComparison.Ordinal);
                Assert.Contains($"::highlight({prefix}{n}-pointed){{color:var(--ex-reference-text-{n}-pointed)}}", css, StringComparison.Ordinal);
            }
            Assert.Contains($"::highlight({prefix}pointed){{background-color:var(--ex-reference-text-pointed)}}", css, StringComparison.Ordinal);
            Assert.Equal((2 * ReferenceColour.PaletteLength) + 1, Regex.Matches(css, @"::highlight\(").Count);
            Assert.DoesNotContain("#", css, StringComparison.Ordinal);
        }
        Assert.NotEqual(prefixes[0], prefixes[1]);
        Assert.Equal($"1,2,{prefixes[0]}1 4,2,{prefixes[0]}2 7,2,{prefixes[0]}1",
            first.Find(".ex-viewport > .ex-reference-text").GetAttribute("data-ex-colours"));
        Assert.Equal($"1,2,{prefixes[1]}1", second.Find(".ex-viewport > .ex-reference-text").GetAttribute("data-ex-colours"));
        // The bar's layer is written for the same text, while it shows the edit.
        Assert.Equal(first.Find(".ex-viewport > .ex-reference-text").GetAttribute("data-ex-colours"),
            first.Find(".ex-formula-bar > .ex-reference-text").GetAttribute("data-ex-colours"));
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
            // And no highlight is painted: the grid writes no stylesheet for them.
            Assert.DoesNotContain("::highlight(", cut.Markup, StringComparison.Ordinal);
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
