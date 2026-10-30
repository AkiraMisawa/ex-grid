using System.Text.Json;
using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// ADR-0016, ADR-0030: Blazor's ExGrid over the Script Core hands the browser the widths a Consumer or a Wrapper gives
/// it, as numbers the core takes whole: <c>CellMetrics</c>, a glyph table charged as .NET charges it, and a Wrapper's
/// cascaded <see cref="GridPresentationDefaults"/>, whose hover band and Row Stripes stand where the Consumer wrote none.
/// What the core does with them is its own, tested there.
/// </summary>
public class ScriptGridCellMetricsTests : BunitContext
{
    private readonly BunitJSModuleInterop _module;

    public ScriptGridCellMetricsTests()
    {
        _module = JSInterop.SetupModule(invocation => invocation.Identifier == "import");
        _module.Setup<string?>("stamp").SetResult(global::ExGrid.Components.ScriptCoreAsset.Stamp);
        var grid = _module.SetupModule("attach", _ => true);
        grid.SetupVoid("update", _ => true).SetVoidResult();
        grid.SetupVoid("state", _ => true).SetVoidResult();
    }

    private static readonly GridColumn<TestRow>[] Columns = [new("Amount", ColumnType.Number, r => r.Amount)];

    private JsonElement Attached()
    {
        using var document = JsonDocument.Parse(Assert.IsType<string>(Assert.Single(_module.Invocations["attach"]).Arguments[2]));
        return document.RootElement.GetProperty("options").Clone();
    }

    [Fact] // ADR-0016: every class crosses, and a glyph is charged in the browser what .NET charges it at the metrics' size
    public void ADR0016_cell_metrics_cross_whole_with_each_glyph_charged_as_dotnet_charges_it()
    {
        var metrics = new CellTextMetrics(9, 7, 3, 13, 8, 10, 8, 4, 12, 13)
            .WithGlyphWidths(new GlyphWidthTable(16, [("W", 14, 15)]), 13);
        Render<global::ExGrid.Components.ExGrid<TestRow>>(ps => ps
            .Add(g => g.Rows, TestRows.Window())
            .Add(g => g.Columns, Columns)
            .Add(g => g.CellMetrics, metrics));

        var sent = Attached().GetProperty("cellMetrics");
        Assert.Equal(7, sent.GetProperty("digitWidthPx").GetDouble());
        Assert.Equal(13, sent.GetProperty("boldOtherWidthPx").GetDouble());
        Assert.Equal(1, sent.GetProperty("fontSizePx").GetDouble());
        var glyphs = sent.GetProperty("glyphWidths");
        Assert.Equal(1, glyphs.GetProperty("measuredAtPx").GetDouble());
        var w = Assert.Single(glyphs.GetProperty("glyphs").EnumerateArray()).EnumerateArray().ToArray();
        Assert.Equal("W", w[0].GetString());
        // `W` measured at 16 px, charged at 13: the numbers packages/blazor/test/wire-metrics.test.ts reads.
        Assert.Equal(11.375, w[1].GetDouble());
        Assert.Equal(12.1875, w[2].GetDouble());
        Assert.Equal(metrics.TextWidthPx("W"), w[1].GetDouble());
        Assert.Equal(metrics.Bold.TextWidthPx("W"), w[2].GetDouble());
    }

    [Fact] // ADR-0030: a Wrapper's widths and words cross, and its Row Stripes stand where the Consumer wrote none
    public void ADR0030_a_wrappers_defaults_cross_and_its_words_stand_where_the_consumer_wrote_none()
    {
        var defaults = new GridPresentationDefaults(10.4, 8.0, 4.95, 14, GridDensity.Standard, stripeRows: true);
        Render<CascadingValue<GridPresentationDefaults>>(ps => ps
            .Add(c => c.Value, defaults)
            .AddChildContent<global::ExGrid.Components.ExGrid<TestRow>>(grid => grid
                .Add(g => g.Rows, TestRows.Window())
                .Add(g => g.Columns, Columns)
                .Add(g => g.HighlightHoverRow, false)));

        var options = Attached();
        var sent = options.GetProperty("presentationDefaults");
        Assert.Equal(8.0, sent.GetProperty("digitWidthPx").GetDouble());
        Assert.Equal(14, sent.GetProperty("fontSizePx").GetDouble());
        Assert.Equal("standard", sent.GetProperty("density").GetString());
        Assert.True(sent.GetProperty("stripeRows").GetBoolean());
        Assert.False(sent.TryGetProperty("highlightHoverRow", out _));
        // Not written, the Wrapper's stripes stand; written, the Consumer's hover band does.
        Assert.False(options.TryGetProperty("stripeRows", out _));
        Assert.False(options.GetProperty("highlightHoverRow").GetBoolean());
        Assert.False(options.TryGetProperty("cellMetrics", out _));
    }

    [Fact] // ADR-0016: metrics nobody measured are refused by name, as the C# core refuses them
    public void ADR0016_default_cell_metrics_are_refused()
    {
        var refused = Assert.Throws<ArgumentOutOfRangeException>(() => Render<global::ExGrid.Components.ExGrid<TestRow>>(ps => ps
            .Add(g => g.Rows, TestRows.Window())
            .Add(g => g.Columns, Columns)
            .Add(g => g.CellMetrics, default(CellTextMetrics))));

        Assert.Equal("CellMetrics", refused.ParamName);
    }
}
