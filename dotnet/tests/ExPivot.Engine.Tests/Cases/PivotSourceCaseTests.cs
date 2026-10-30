using System.Text.Json.Nodes;
using Xunit;

namespace ExPivot.Engine.Tests.Cases;

/// <summary>
/// The Pivot Source cases (<c>tests/cases/pivot</c>; ADR-0370 sections 2 and 5, ADR-0066), kind
/// <c>pivotSource</c>: a source over a data set's records, or over a Snapshot a data set's columns make,
/// with fields declared over its columns or one a column; optionally asked through a fetching source in
/// front of it that offers fewer Aggregations, or answers another question than the one asked. Each step
/// asks it something — its fields, the Leaf Aggregates of a question, a page of a field's Items or of the
/// records behind a cell, a report laid out from its answer — and is answered by what it says, or what it
/// throws. The Script Core's runner is <c>packages/pivot-engine/test/sources.test.ts</c>.
/// </summary>
public class PivotSourceCaseTests
{
    public static TheoryData<string, string> Sources() => PivotTheories.Of("pivotSource");

    [Theory, MemberData(nameof(Sources))] // ADR-0370, ADR-0066: a source answers one question alike on both engines
    public async Task Source(string file, string name)
    {
        var (document, entry) = PivotCases.Find(file, name);
        var answered = await PivotSourceCases.Answer(document, entry, TestContext.Current.CancellationToken);
        if (PivotCases.Writing)
        {
            PivotCases.Write(file, name, "expect", answered);
            return;
        }
        // Compared as JSON, not as text: a number is one however it is spelt (5E-324, 5e-324).
        var expected = PivotCases.Must(entry, "expect");
        Assert.True(JsonNode.DeepEquals(expected, answered), $"expected {expected.ToJsonString()}, answered {answered.ToJsonString()}");
    }
}
