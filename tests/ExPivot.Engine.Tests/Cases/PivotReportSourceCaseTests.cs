using System.Text.Json.Nodes;
using Xunit;

namespace ExPivot.Engine.Tests.Cases;

/// <summary>
/// The report source cases (<c>tests/cases/pivot</c>; ADR-0370 sections 2, 4 and 5, ADR-0151 to ADR-0153),
/// kind <c>pivotReportSource</c>: a local report source over a case's Pivot Source — a data set's records,
/// or a Snapshot of its columns that Change Batches are applied to — with the Order Keys the case registers by
/// a policy's name, on a clock the case moves by hand. Each step asks a Window, applies a batch, moves the
/// clock, or asks a Copy, a Selection Summary, a field's Items or the records behind a cell under a Report
/// Version, and is answered by what the source says, or what it throws. Versions are written v1, s1 and r1 in
/// the order the source first named them. The Script Core's runner is
/// <c>src/npm/pivot-engine/test/report-sources.test.ts</c>.
/// </summary>
public class PivotReportSourceCaseTests
{
    public static TheoryData<string, string> ReportSources() => PivotTheories.Of("pivotReportSource");

    [Theory, MemberData(nameof(ReportSources))] // ADR-0370, ADR-0151, ADR-0152, ADR-0153: a report source answers alike on both engines
    public async Task Report_source(string file, string name)
    {
        var (document, entry) = PivotCases.Find(file, name);
        var answered = await PivotReportSourceCases.Answer(document, entry, TestContext.Current.CancellationToken);
        if (PivotCases.Writing)
        {
            PivotCases.Write(file, name, "expect", answered);
            return;
        }
        var expected = PivotCases.Must(entry, "expect");
        Assert.True(JsonNode.DeepEquals(expected, answered), $"expected {expected.ToJsonString()}, answered {answered.ToJsonString()}");
    }
}
