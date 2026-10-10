using System.Text;
using ExPivot.Engine;
using Xunit;

namespace ExGrid.DemoApi.Tests;

public sealed class ReportWindowTests(PivotApiServer server) : IClassFixture<PivotApiServer>
{
    [Fact] // ADR-0151/0152: the server retains the report and sends only requested display rows
    public async Task Windows_operations_and_expired_state_cross_the_http_boundary()
    {
        using var http = server.Factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        var path = "/api/pivot/reports/" + Guid.NewGuid().ToString("N");
        var layout = new PivotLayout
        {
            Rows = [new("Region"), new("Desk"), new("Month")],
            Values = [new("Pnl") { NumberFormat = "0.00" }],
        };
        var request = new PivotReportRequest("first", layout, PivotReportSettings.Invariant, new(0, 2));
        var first = await Post<PivotReportRequest, PivotReportUpdate>("window", request);
        Assert.Null(first.Refusal);
        Assert.Equal(2, first.Rows!.Count);
        Assert.True(first.Metadata!.RowCount > 20);
        string[] columns = [first.Metadata.ValueColumns[0].Name];
        var selection = new PivotReportCopyQuery(first.Metadata.Version, columns,
            [new(0, 0, first.Metadata.RowCount - 1, 0)]);
        var copy = await Post<PivotReportCopyQuery, PivotReportCopyResult>("copy", selection);
        Assert.Null(copy.Refusal);
        Assert.Equal(first.Metadata.RowCount, copy.Blocks[0].Rows.Count);
        var scrolled = await Post<PivotReportRequest, PivotReportUpdate>("window", request with
            { RequestId = "scroll", Window = new(10, 2), Baseline = first.Metadata.Version });
        Assert.Equal(first.Metadata.Version, scrolled.Metadata!.Version);
        Assert.Equal(2, scrolled.Rows!.Count);
        var items = await Post<PivotReportItemsQuery, PivotReportItemsResult>("items", new(first.Metadata.Version, "Region"));
        Assert.Null(items.Refusal);
        Assert.NotEmpty(items.Items);
        using (var deletion = await http.DeleteAsync(path, ct)) deletion.EnsureSuccessStatusCode();
        var expired = await Post<PivotReportCopyQuery, PivotReportCopyResult>("copy", selection);
        Assert.Equal(PivotReportRefusalKind.ReportVersionNotHeld, expired.Refusal!.Kind);
        var reset = await Post<PivotReportRequest, PivotReportUpdate>("window", request with
            { RequestId = "recover", Baseline = first.Metadata.Version });
        Assert.Null(reset.Refusal);
        Assert.NotEqual(first.Metadata.Version, reset.Metadata!.Version);
        Assert.Equal(2, reset.Rows!.Count);

        async Task<TAnswer> Post<TQuestion, TAnswer>(string operation, TQuestion question)
        {
            using var content = new StringContent(PivotReportJson.Write(question), Encoding.UTF8, "application/json");
            using var response = await http.PostAsync(path + "/" + operation, content, ct);
            response.EnsureSuccessStatusCode();
            return PivotReportJson.Read<TAnswer>(await response.Content.ReadAsStringAsync(ct));
        }
    }
}
