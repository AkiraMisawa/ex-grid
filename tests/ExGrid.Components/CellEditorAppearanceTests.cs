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
/// The Cell Editor keeps the edited cell's Fill and Font (ADR-0050 item 15; ticket 88): its field,
/// the box a Chrome's control stands in, and the coloured text beneath them (ADR-0057) wear the
/// cell's own Font and Fill classes, whose editor rules set the editor's tokens. The box, its
/// geometry and its outline stay the core's, the Formula Bar keeps its own look, and no row
/// renders. 50 rows of 20px; Book (A) and Note (B) edit, Amount (C) does not.
/// </summary>
public class CellEditorAppearanceTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));
    private static readonly RgbColour Red = RgbColour.FromRgb(0xFF0000);
    private static readonly RgbColour Blue = RgbColour.FromRgb(0x0000FF);
    private static readonly RgbColour Yellow = RgbColour.FromRgb(0xFFFF00);

    private static readonly CellAppearance Looked = new() { FontColour = Red, Bold = true, Italic = true, Underline = true, Strikethrough = true, Fill = Yellow };

    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        new("Note", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
    ];

    /// <summary>The first row's Book (A1) looks like <paramref name="a1"/>; every other cell has none.</summary>
    private static CellAppearanceOf<TestRow> A1Is(CellAppearance a1)
        => (row, column) => row.Book == "Row 000000" && column.Name == "Book" ? a1 : default;

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        CellAppearanceOf<TestRow>? appearance,
        Action<ComponentParameterCollectionBuilder<ExGrid<TestRow>>>? more = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(50))
              .Add(g => g.TotalCount, 50)
              .Add(g => g.Columns, Columns())
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 200)
              .Add(g => g.ViewportWidth, 350)
              .Add(g => g.CellAppearance, appearance);
            more?.Invoke(ps);
        });

    private static Task ClickAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });

    private static Task PressAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, false, false, false, false, false));

    /// <summary>Selects A1 and types onto it, which opens the Cell Editor there.</summary>
    private static async Task EditA1Async(IRenderedComponent<ExGrid<TestRow>> cut)
    {
        await ClickAsync(cut, 50, 10);
        await PressAsync(cut, "x");
    }

    private static IElement Editor(IRenderedComponent<ExGrid<TestRow>> cut) => cut.Find(".ex-viewport .ex-editor");

    private static string Css(IRenderedComponent<ExGrid<TestRow>> cut) => cut.Find(".ex-grid > style").TextContent;

    [Fact] // ADR-0050 item 15 (ticket 88): the Cell Editor wears the edited cell's Font and Fill, and its rules set the editor's tokens
    public async Task The_editor_takes_the_edited_cells_font_and_fill()
    {
        var cut = RenderGrid(A1Is(Looked));

        await EditA1Async(cut);

        var editor = Editor(cut);
        Assert.Equal("INPUT", editor.TagName);
        Assert.Equal("ex-editor ex-font-ff0000bius ex-fill-ffff00", editor.ClassName);
        // The Font as the editor's text and the Fill as its ground, through the editor's own tokens:
        // a field that turns see-through over the coloured text (ADR-0057) stays so.
        Assert.Contains(
            ".ex-viewport .ex-editor.ex-font-ff0000bius,.ex-viewport .ex-editor.ex-font-ff0000bius :is(input,textarea,.ex-reference-text),.ex-viewport .ex-reference-text-cell.ex-font-ff0000bius"
            + "{--ex-editor-color:#ff0000;font-weight:700;font-style:italic;text-decoration-line:underline line-through;}",
            Css(cut));
        Assert.Contains(".ex-viewport .ex-editor.ex-fill-ffff00,.ex-viewport .ex-reference-text-cell.ex-fill-ffff00{--ex-editor-background:#ffff00}", Css(cut));
    }

    [Fact] // ADR-0050 item 15 (ticket 88): no geometry moves — the editor's box is where it was without the look
    public async Task The_editors_box_does_not_move()
    {
        var plain = RenderGrid(appearance: null);
        var looked = RenderGrid(A1Is(Looked));

        await EditA1Async(plain);
        await EditA1Async(looked);

        Assert.Equal(Editor(plain).GetAttribute("style"), Editor(looked).GetAttribute("style"));
        Assert.Equal(
            plain.FindAll(".ex-row").Select(r => r.GetAttribute("style")),
            looked.FindAll(".ex-row").Select(r => r.GetAttribute("style")));
    }

    [Fact] // DC-1 / ADR-0050 item 15: without either declaration, and over a cell that has no look, the editor is as before
    public async Task Without_a_look_the_editor_is_as_before()
    {
        var undeclared = RenderGrid(appearance: null);
        var plainCell = RenderGrid(A1Is(Looked));

        await EditA1Async(undeclared);
        await ClickAsync(plainCell, 50, 30);   // A2, which has none
        await PressAsync(plainCell, "x");

        Assert.Equal("ex-editor", Editor(undeclared).ClassName);
        Assert.Empty(undeclared.FindAll(".ex-grid > style"));
        Assert.Equal("ex-editor", Editor(plainCell).ClassName);
    }

    [Fact] // ADR-0050 item 15 / ADR-0071 (ticket 88): the editor's own declaration outranks the cell's, so a colour that paints the formatted Value is not the editor's
    public async Task The_editor_appearance_outranks_the_cells()
    {
        var cut = RenderGrid(A1Is(new CellAppearance { FontColour = Red, Fill = Yellow }),
            ps => ps.Add(g => g.EditorAppearance, A1Is(new CellAppearance { FontColour = Blue, Fill = Yellow })));

        await EditA1Async(cut);

        Assert.Equal("ex-editor ex-font-0000ff ex-fill-ffff00", Editor(cut).ClassName);
        // The cell beneath keeps its own.
        Assert.Contains("ex-font-ff0000", cut.FindAll(".ex-viewport [role=row]")[0].QuerySelectorAll(".ex-cell")[0].ClassList);
    }

    [Fact] // ADR-0050 item 15 (ticket 88): the editor's declaration alone is enough, and paints no cell
    public async Task The_editor_appearance_alone_paints_the_editor_only()
    {
        var cut = RenderGrid(appearance: null, ps => ps.Add(g => g.EditorAppearance, A1Is(Looked)));

        await EditA1Async(cut);

        Assert.Equal("ex-editor ex-font-ff0000bius ex-fill-ffff00", Editor(cut).ClassName);
        Assert.Contains("--ex-editor-background:#ffff00", Css(cut));
        Assert.Empty(cut.FindAll(".ex-cell[class*='ex-font-'], .ex-cell[class*='ex-fill-']"));
    }

    [Fact] // ADR-0050 item 15 / ADR-0004: over a Pinned Column the editor on its sticky anchor wears the look too
    public async Task A_pinned_editor_takes_the_look()
    {
        var cut = RenderGrid(A1Is(Looked), ps => ps.Add(g => g.PinnedColumnCount, 1));

        await EditA1Async(cut);

        Assert.Equal("ex-editor ex-font-ff0000bius ex-fill-ffff00", cut.Find(".ex-editor-pinned > input.ex-editor").ClassName);
    }

    [Fact] // ADR-0057 / ADR-0050 item 15 (ticket 88): the coloured text beneath the field wears the look, so it is read on the Fill in the Font
    public async Task The_coloured_text_takes_the_look()
    {
        var cut = RenderGrid(A1Is(Looked), ps => ps
            .Add(g => g.ReferencesIn, text => text.StartsWith('=') ? [new EditorReference(1, 2, SelectionRange.FromCorners(new(1, 1), new(1, 1)))] : [])
            .Add(g => g.ShowFormulaBar, true));

        await EditA1Async(cut);
        await cut.Find(".ex-viewport input.ex-editor").InputAsync(new ChangeEventArgs { Value = "=B2" });

        var layer = cut.Find(".ex-viewport > .ex-reference-text");
        Assert.Equal("ex-reference-text ex-reference-text-cell ex-font-ff0000bius ex-fill-ffff00", layer.ClassName);
        Assert.Equal("ex-editor ex-font-ff0000bius ex-fill-ffff00", layer.NextElementSibling!.ClassName);
        // The Reference keeps its own colour's class over the look.
        Assert.Equal("=<span class=\"ex-reference-1\">B2</span>", global::ReferenceText.ColouredText.Of(layer));
        // The Formula Bar frames the Paper: its field and its layer keep their own look.
        Assert.DoesNotContain(cut.FindAll(".ex-formula-bar *"), e => e.ClassName?.Contains("ex-font-") == true || e.ClassName?.Contains("ex-fill-") == true);
    }

    [Fact] // ADR-0010 / ADR-0050 item 15 (ticket 88): a Chrome's control stands in the core's box, which wears the look
    public async Task A_chromes_box_takes_the_look()
    {
        var cut = RenderGrid(A1Is(Looked), ps => ps.Add(g => g.Chrome, new BareChrome()));

        await EditA1Async(cut);

        var box = cut.Find(".ex-viewport div.ex-editor");
        Assert.Equal("ex-editor ex-font-ff0000bius ex-fill-ffff00", box.ClassName);
        Assert.Equal("stub-editor", Assert.Single(box.Children).ClassName);
    }

    [Fact] // ED-1 / P1 / P4 (ADR-0027): the look renders no row, and nothing reaches JavaScript for it
    public async Task The_look_renders_no_row_and_reaches_no_javascript()
    {
        var plain = RenderGrid(appearance: null);
        var looked = RenderGrid(A1Is(Looked));
        await ClickAsync(plain, 50, 10);
        await ClickAsync(looked, 50, 10);
        var renders = looked.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToList();

        var before = JSInterop.Invocations.Count;
        await PressAsync(plain, "x");
        var without = JSInterop.Invocations.Count - before;
        before = JSInterop.Invocations.Count;
        await PressAsync(looked, "x");
        var with = JSInterop.Invocations.Count - before;

        Assert.Equal(renders, looked.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToList());
        Assert.Equal(without, with);
    }

    /// <summary>A Chrome whose Cell Editor is a bare control, as a Wrapper's is.</summary>
    private sealed class BareChrome : IGridChrome
    {
        public RenderFragment? FilterPanel(FilterPanelContext context) => null;

        public RenderFragment? ColumnMenu(ColumnMenuContext context) => null;

        public RenderFragment? LoadingIndicator(LoadingContext context) => null;

        public RenderFragment? CellEditor(CellEditorContext context)
            => builder => builder.AddMarkupContent(0, "<input class=\"stub-editor\" />");
    }
}
