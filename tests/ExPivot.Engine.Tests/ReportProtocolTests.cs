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

    private static PivotReportMetadata Metadata(string version) => new(
        new(version), "source-1", "rows-1", PivotLayout.Empty, PivotReportSettings.Invariant,
        0, [], [], [], 0, []);
}
