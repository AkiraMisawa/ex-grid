using Bunit;
using ExGrid.Components.Tests.Support;
using ExGrid.Data;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// A grid built from a source's shape (ADR-0342) is an ordinary grid: its columns paint their
/// format codes' text, and the virtualisation invariants hold for it as for any column (ADR-0027).
/// </summary>
public class ShapedGridRenderTests : GridTestContext
{
    private static ShapedGrid Grid(int rows)
    {
        var builder = new SnapshotColumnsBuilder();
        var desk = builder.Text("desk");
        var notional = builder.Decimal("notional");
        for (var i = 0; i < rows; i++)
        {
            desk.Append(i % 2 == 0 ? "Rates" : "FX");
            notional.Append(1000.5m * (i + 1));
        }
        var snapshot = builder.Build();
        var spec = GridViewSpec.Read(
            """{"data": "trades", "columns": [{"column": "desk", "width": 80}, {"column": "notional", "format": "#,##0.00", "width": 140}]}""",
            _ => DataShape.Of(snapshot));
        return ShapedGrid.Of(snapshot, spec);
    }

    [Fact] // ADR-0342 / ADR-0027 P1: the DOM does not grow with the row count
    public void A_shaped_grid_paints_its_formatted_text_and_only_its_window()
    {
        var grid = Grid(10_000);

        var cut = Render<ExGrid<ShapedRow>>(ps => ps
            .Add(g => g.Source, grid.Source)
            .Add(g => g.Columns, grid.Columns)
            .Add(g => g.PinnedColumnCount, grid.PinnedColumnCount)
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 120)
            .Add(g => g.ViewportWidth, 350));

        var rows = cut.FindAll(".ex-row");
        Assert.InRange(rows.Count, 1, 40);
        var first = rows[0].QuerySelectorAll(".ex-cell").Select(c => c.TextContent.Trim()).ToArray();
        Assert.Equal(["Rates", "1,000.50"], first);
    }
}
