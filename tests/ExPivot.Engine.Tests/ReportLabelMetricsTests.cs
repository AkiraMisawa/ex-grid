using ExPivot.Engine;
using Xunit;

namespace ExPivot.Engine.Tests;

public sealed class ReportLabelMetricsTests
{
    [Fact] // ADR-0151/0153: remote sizing carries explicit geometry, including a supplementary glyph.
    public void ADR0153_Label_metrics_preserve_class_widths_and_resolved_glyph_overrides()
    {
        var metrics = new PivotReportLabelMetrics(12, 8, 4, 16, 11, 3,
            new Dictionary<int, double> { ['W'] = 14, [0x1D11E] = 13 });
        Assert.Equal(12 + 8 + 4 + 16 + 14 + 13 + 11 + 6,
            metrics.EstimatePx("%1 日W𝄞x"));
    }
}
