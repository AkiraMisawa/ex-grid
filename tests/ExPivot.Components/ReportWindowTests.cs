using Bunit;
using ExGrid;
using ExGrid.Components;
using ExPivot.Components.Tests.Support;
using ExPivot.Engine;
using Xunit;
using PivotComponent = ExPivot.Components.ExPivot;

namespace ExPivot.Components.Tests;

public sealed class ReportWindowTests : PivotTestContext
{
    [Fact] // ADR-0151: the component's row extent is metadata; only requested rows reach ExGrid.
    public void ADR0151_A_remote_report_renders_a_window_with_the_complete_extent()
    {
        var requests = new List<PivotReportRequest>();
        var layout = new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount")] };
        var source = PivotReportSource.Fetch(Fields, PivotSourceFeatures.All, PivotReportUpdateMode.Incremental,
            (request, _) =>
            {
                requests.Add(request);
                var metadata = new PivotReportMetadata(new("report-1"), "source-1", "order-1", request.Layout,
                    request.Settings, 1_000_000, [new("label", "Region", "Region")],
                    [new("value", "Sum of Amount", PivotColumnRole.Item, 0, [])], [], 0, ["Sum of Amount"]);
                var rows = Enumerable.Range(request.Window.Start, request.Window.Count).Select(i =>
                    new PivotDisplayRow(new(PivotRowRole.Item, -1, [PivotItemKey.Text($"Region {i}")]),
                        PivotRowRole.Item, -1, true, [new($"Region {i}")], [new(i, i, null, i.ToString())],
                        [new("Region", PivotItemKey.Text($"Region {i}"))])).ToArray();
                return ValueTask.FromResult(PivotReportUpdate.Complete(request, metadata, rows));
            }, items: (query, _) => ValueTask.FromResult(new PivotItemPage(query.SourceVersion, [], 0)));
        SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", true));
        var cut = Render<PivotComponent>(ps => ps.Add(p => p.Source, source).Add(p => p.Layout, layout)
            .Add(p => p.ShowFieldList, false).Add(p => p.ViewportHeight, (ViewportSize)200));

        var grid = cut.FindComponent<ExGrid<PivotDisplayRow>>().Instance;
        Assert.Equal(1_000_000, grid.TotalCount);
        Assert.InRange(grid.Window.Count, 1, 100);
        Assert.Equal("Region 0", grid.Window[0].Labels[0].Text);
        Assert.All(requests, request => Assert.InRange(request.Window.Count, 1, 100));
    }
}
