using ExPivot.Engine;
using Xunit;

namespace ExPivot.Engine.Tests;

public class ReportProtocolTests
{
    [Fact]
    public async Task ADR0152_missing_baseline_recovers_a_complete_window_before_publication()
    {
        var requests = new List<PivotReportRequest>();
        var source = PivotReportSource.Fetch([], PivotSourceFeatures.All, PivotReportUpdateMode.FullRefresh,
            (request, _) =>
            {
                requests.Add(request);
                return ValueTask.FromResult(request.Baseline is not null
                    ? PivotReportUpdate.Refused(request, new(PivotReportRefusalKind.BaselineNotHeld, "The baseline expired."))
                    : PivotReportUpdate.Complete(request, Metadata("v2"), []));
            });
        var client = new PivotReportClient(source);
        Assert.True(await client.ReadAsync(PivotLayout.Empty, PivotReportSettings.Invariant, new(0, 10), cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(await client.ReadAsync(PivotLayout.Empty, PivotReportSettings.Invariant, new(0, 10), cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(3, requests.Count);
        Assert.NotNull(requests[1].Baseline);
        Assert.Null(requests[2].Baseline);
        Assert.Equal("v2", client.Current!.Metadata.Version.Value);
        Assert.Null(client.Refusal);
    }

    [Fact]
    public async Task ADR0151_local_and_serialized_remote_windows_keep_the_full_extent_and_exact_values()
    {
        var data = PivotSource.From(Pivot.Sales, Pivot.Fields);
        await using var local = PivotReportSource.From(data);
        var remote = PivotReportSource.Fetch(data.Fields, data.Features, local.UpdateMode,
            async (query, ct) => PivotReportJson.Read<PivotReportUpdate>(PivotReportJson.Write(
                await local.WindowAsync(PivotReportJson.Read<PivotReportRequest>(PivotReportJson.Write(query)), ct))));
        var client = new PivotReportClient(remote);
        var layout = Pivot.RowsBy("Region");
        Assert.True(await client.ReadAsync(layout, PivotReportSettings.Invariant, new(0, 1),
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(5, client.Current!.Metadata.RowCount);
        Assert.Single(client.Current.Rows);
        Assert.Equal("East", client.Current.Rows[0].Labels[0].Text);
        Assert.Equal(180m, client.Current.Rows[0].ValueAt(0)!.Exact);
    }

    private static PivotReportMetadata Metadata(string version) => new(
        new(version), "source-1", "rows-1", PivotLayout.Empty, PivotReportSettings.Invariant,
        0, [], [], [], 0, []);
}
