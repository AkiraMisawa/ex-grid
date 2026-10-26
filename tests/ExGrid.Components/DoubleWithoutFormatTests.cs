using System.Globalization;
using Bunit;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// ADR-0006's note of 2026-10-26, as the grid shows it: without a <c>Format</c>, a <see cref="double"/>
/// shows Excel's General, at most 15 significant digits, while its entry — what the editor opens on and
/// the Formula Bar shows — keeps the value whole, so an edit committed unchanged writes back the same number.
/// </summary>
public class DoubleWithoutFormatTests : GridTestContext
{
    private const double Sum = 0.1 + 0.2;

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid()
        => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.ShowFormulaBar, true)
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, new GridColumn<TestRow>[]
            {
                new("Sum", ColumnType.Number, _ => Sum, editable: true),
                new("Zero", ColumnType.Number, _ => -0.0, editable: true),
            })
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 120)
            .Add(g => g.ViewportWidth, 600));

    [Fact] // ADR-0006 (2026-10-26): 0.1 + 0.2 shows 0.3, and a negative zero shows 0
    public void The_cells_show_fifteen_significant_digits()
    {
        var cut = RenderGrid();

        var cells = cut.FindAll(".ex-row").First().QuerySelectorAll(".ex-cell").Select(c => c.TextContent).ToArray();

        Assert.Equal(["0.3", "0"], cells);
    }

    [Fact] // ADR-0006 (2026-10-26) / ADR-0051: the editor and the Formula Bar hold the whole number, alike
    public async Task The_editor_and_the_formula_bar_hold_the_whole_number()
    {
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            var cut = RenderGrid();
            await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 20, OffsetY = 10 });
            var shown = new List<string?> { cut.Find(".ex-formula-bar-text").GetAttribute("value") };
            await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("F2", false, false, false, false, false));
            shown.Add(cut.Find(".ex-editor").GetAttribute("value"));
            await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("Escape", false, false, false, false, false));
            await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowRight", false, false, false, false, false));
            shown.Add(cut.Find(".ex-formula-bar-text").GetAttribute("value"));

            // The bar over 0.1 + 0.2, its editor, and the bar over a negative zero.
            Assert.Equal(["0.30000000000000004", "0.30000000000000004", "0"], shown);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }
}
