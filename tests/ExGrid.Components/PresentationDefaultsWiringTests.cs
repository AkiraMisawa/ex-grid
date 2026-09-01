using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// A Wrapper's cascaded presentation defaults reaching the grid (ADR-0030): the
/// glyph widths of its font where no CellMetrics were written, its Density where
/// none was, and an explicit parameter still winning (ADR-0028). Observed through the
/// one place the widths matter — whether a twelve-digit amount fits or hashes
/// (ADR-0016) — rather than through a private field.
/// </summary>
public class PresentationDefaultsWiringTests : GridTestContext
{
    private static readonly GridPresentationDefaults Roboto = new(10.4, 8.0, 4.95, 14);

    // Twelve digits at Roboto's 8.0px plus 2 × 8px padding is 112px exactly, which
    // fits; at the system default's 9.058px it is 124.7px, which hashes.
    private static readonly ColumnWidthSpec Width112 = new(ColumnWidth.Fixed(112));

    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Amount", ColumnType.Number, r => r.Amount, width: Width112),
    ];

    private static TestRow[] Rows() => [new() { Amount = 123456789012m }];

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        GridPresentationDefaults? cascaded,
        Action<Bunit.ComponentParameterCollectionBuilder<ExGrid<TestRow>>>? extra = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, Rows())
              .Add(g => g.TotalCount, 1)
              .Add(g => g.Columns, Columns())
              .Add(g => g.ViewportHeight, 120)
              .Add(g => g.ViewportWidth, 350);
            if (cascaded is not null)
                ps.AddCascadingValue(cascaded);
            extra?.Invoke(ps);
        });

    private static string AmountCell(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.Find(".ex-row .ex-cell").TextContent;

    [Fact] // ADR-0030: the Wrapper's widths are what the overflow estimate reads
    public void The_cascaded_widths_decide_the_fit()
    {
        Assert.StartsWith("#", AmountCell(RenderGrid(cascaded: null)));
        Assert.Equal("123456789012", AmountCell(RenderGrid(Roboto)));
    }

    [Fact] // ADR-0028: an explicit CellMetrics beats the cascade
    public void Explicit_metrics_beat_the_cascade()
    {
        var cut = RenderGrid(Roboto, ps => ps.Add(g => g.CellMetrics, new CellTextMetrics(20, 20, 10, 8)));

        Assert.StartsWith("#", AmountCell(cut));
    }

    [Fact] // ADR-0030: the cascaded Density stands where none was written
    public void The_cascaded_density_is_the_default()
    {
        var cut = RenderGrid(new GridPresentationDefaults(10.4, 8.0, 4.95, 14, GridDensity.Standard));

        Assert.Contains("--ex-row-height: 32px", cut.Find(".ex-grid").GetAttribute("style"));
    }

    [Fact] // ADR-0028: an explicit Density beats the cascaded one, per value
    public void Explicit_density_beats_the_cascade()
    {
        var cut = RenderGrid(
            new GridPresentationDefaults(10.4, 8.0, 4.95, 14, GridDensity.Standard),
            ps => ps.Add(g => g.Density, GridDensity.Excel));

        var style = cut.Find(".ex-grid").GetAttribute("style")!;
        Assert.Contains("--ex-row-height: 20px", style);
        // And the widths follow Excel's 12px, scaled from the 14px measurement.
        Assert.Contains("--ex-font-size: 12px", style);
    }

    [Fact] // ADR-0030: a cascade that names no Density leaves the grid's own default
    public void A_cascade_without_density_leaves_compact()
    {
        var cut = RenderGrid(Roboto);

        Assert.Contains("--ex-row-height: 28px", cut.Find(".ex-grid").GetAttribute("style"));
    }
}
