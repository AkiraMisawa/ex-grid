using ExPivot.Engine;
using Xunit;

namespace ExPivot.Engine.Tests;

/// <summary>
/// ADR-0153 merges a total that subtraction cannot keep exact again from its leaves, in their first
/// records' order, and keeps each total's leaves in a list for it. A new question used to make
/// those lists, for every total, though only an update reads them. With several fields in Rows and
/// Columns that is several lists a leaf, and over a million trades it was most of what the
/// branch's questions still cost beyond <c>main</c>'s (PV-21).
/// </summary>
public class MemberListsTests
{
    private sealed record Entry(long Id, string Region, string Desk, string Book, decimal Amount, double Measure);

    [Fact] // ADR-0153, PV-21: a new question lists no total's leaves; the first update lists them
    public async Task A_new_question_lists_no_totals_leaves_and_the_first_update_does()
    {
        var fields = PivotFields.Of<Entry>().Key("Id", e => e.Id)
            .Text("Region", e => e.Region).Text("Desk", e => e.Desk).Text("Book", e => e.Book)
            .Number("Amount", e => e.Amount).Number("Measure", e => e.Measure);
        static Entry Make(long id, double measure) => new(id, "R" + (id % 3), "D" + (id % 4), "B" + (id % 5), id % 11, measure);
        var source = PivotSource.From([.. Enumerable.Range(1, 600).Select(id => Make(id, id / 7d))], fields);
        using var session = new PivotComputationSession(source);
        // Two fields a side: each leaf stands under eight totals. A double's sum is merged again
        // from the leaves whenever it is touched.
        var layout = new PivotLayout
        {
            Rows = [new("Region"), new("Desk")],
            Columns = [new("Book")],
            Values = [new PivotValueField("Measure", PivotAggregation.Sum) { NumberFormat = "G17" }],
        };
        await session.ComputeAsync(layout, Pivot.EnUs, cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(session.Computation!.HasMembers);

        source.Apply(fields.Batch(changed: [Make(5, 1e16), Make(9, -1e16)]));
        var updated = (await session.ComputeAsync(layout, Pivot.EnUs, cancellationToken: TestContext.Current.CancellationToken)).Report!;
        Assert.True(session.Computation!.HasMembers);
        var fresh = PivotSource.From(source.Snapshot, fields.Fields);
        var query = PivotQuery.For(layout);
        var expected = PivotEngine.Report(PivotEngine.Cube(query, await fresh.AggregateAsync(query, TestContext.Current.CancellationToken), fresh.Fields), layout, Pivot.EnUs);
        Assert.Equal(Read(expected), Read(updated));

        static string[] Read(PivotReport report) => [.. report.Rows.SelectMany(row =>
            Enumerable.Range(0, report.ValueColumns.Count).Select(c => $"{c}: {report.ValueAt(row, c)?.Number:R}"))];
    }
}
