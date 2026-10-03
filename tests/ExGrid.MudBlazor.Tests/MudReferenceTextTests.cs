using AngleSharp.Dom;
using Bunit;
using ExGrid.Cells;
using ExGrid.Components;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.MudBlazor.Tests;

/// <summary>
/// The coloured text under this Chrome (ADR-0057; DC-1, DC-47 and DC-48's layer 2 half): the core
/// draws the layer and hands it through the seam's context, and this Chrome's bare inputs place it
/// immediately before themselves, inside the core's boxes — the Cell Editor's and the Formula
/// Bar's. Without a References function there is none.
/// </summary>
public class MudReferenceTextTests : MudTestContext
{
    /// <summary><c>=A1</c> names A1, <c>=B3</c> B3: one Reference, a letter and a digit.</summary>
    private static IReadOnlyList<EditorReference> References(string text)
    {
        var references = new List<EditorReference>();
        for (var i = 1; text.StartsWith('=') && i + 1 < text.Length; i++)
        {
            if (text[i] is >= 'A' and <= 'Z' && char.IsAsciiDigit(text[i + 1]) && !char.IsAsciiLetterOrDigit(text[i - 1]))
                references.Add(new EditorReference(i, 2, new SelectionRange(text[i + 1] - '1', text[i] - 'A', 1, 1)));
        }
        return references;
    }

    private IRenderedComponent<ExGrid<Trade>> RenderGrid(bool references)
        => Render<ExGrid<Trade>>(ps =>
        {
            ps.Add(g => g.Window, Rows(5))
              .Add(g => g.TotalCount, 5)
              .Add(g => g.Columns, Columns())
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 200)
              .Add(g => g.ViewportWidth, 400)
              .Add(g => g.Chrome, MudGridChrome.Default)
              .Add(g => g.ShowFormulaBar, true);
            if (references)
                ps.Add(g => g.ReferencesIn, References);
        });

    private static async Task OpenEditAsync(IRenderedComponent<ExGrid<Trade>> cut)
    {
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 50, OffsetY = 30 });
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("=", false, false, false, false, false));
    }

    private static string Drawn(IElement layer) => global::ReferenceText.ColouredText.Of(layer);

    [Fact] // ADR-0057/0030 / DC-47 / DC-48: this Chrome places the core's layer immediately before each of its inputs, inside the core's boxes
    public async Task The_layer_stands_before_each_input_in_the_cores_box()
    {
        var cut = RenderGrid(references: true);
        await OpenEditAsync(cut);

        await cut.Find("input.mud-ex-editor").InputAsync(new ChangeEventArgs { Value = "=A1+B2" });

        const string drawn = "=<span class=\"ex-reference-1\">A1</span>+<span class=\"ex-reference-2\">B2</span>";
        var cell = cut.Find(".ex-viewport > div.ex-editor > .ex-reference-text");
        Assert.Equal("mud-ex-editor", cell.NextElementSibling!.ClassName);
        Assert.Equal("=A1+B2", cell.GetAttribute("data-ex-text"));
        Assert.Equal(drawn, Drawn(cell));
        var bar = cut.Find(".ex-formula-bar > div.ex-editor.ex-formula-bar-text > .ex-reference-text");
        Assert.Contains("mud-ex-formula-bar-text", bar.NextElementSibling!.ClassName, StringComparison.Ordinal);
        Assert.Equal("=A1+B2", bar.GetAttribute("data-ex-text"));
        Assert.Equal(drawn, Drawn(bar));

        // Typed into the bar, both follow.
        await cut.Find("input.mud-ex-formula-bar-text").FocusAsync(new FocusEventArgs());
        await cut.Find("input.mud-ex-formula-bar-text").InputAsync(new ChangeEventArgs { Value = "=C3" });
        Assert.Equal("=C3", cut.Find(".ex-viewport > div.ex-editor > .ex-reference-text").GetAttribute("data-ex-text"));
        Assert.Equal("=C3", cut.Find(".ex-formula-bar .ex-reference-text").GetAttribute("data-ex-text"));
    }

    [Fact] // ADR-0057 / DC-1: without a References function this Chrome places nothing, and its inputs stand as before
    public async Task Without_the_function_nothing_is_placed()
    {
        var cut = RenderGrid(references: false);
        await OpenEditAsync(cut);

        await cut.Find("input.mud-ex-editor").InputAsync(new ChangeEventArgs { Value = "=A1+B2" });

        Assert.Empty(cut.FindAll(".ex-reference-text"));
        Assert.Single(cut.FindAll(".ex-viewport > div.ex-editor > input.mud-ex-editor"));
        Assert.Single(cut.FindAll(".ex-formula-bar > div.ex-editor.ex-formula-bar-text > input.mud-ex-formula-bar-text"));
    }
}
