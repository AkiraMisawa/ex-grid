using ExPivot.Engine;
using Xunit;

namespace ExPivot.Engine.Tests;

/// <summary>
/// ADR-0153 orders a total's merges by each leaf's first record: the row a fresh pass meets the
/// leaf at. A new question used to chain every stored row to its leaf to read those first records
/// off the chains. That cost a walk over every row and an array of one number a row, though the
/// pass had met each leaf at its first row already. Over a million trades it is what made the
/// branch's questions slower than <c>main</c>'s (PV-21). <c>main</c> chains the rows only when a
/// live update recomputes a leaf.
/// </summary>
public class FirstRecordTests
{
    private sealed record Entry(long Id, string Region, string Desk, decimal Amount, double Measure);

    private static PivotFields<Entry> Fields() => PivotFields.Of<Entry>().Key("Id", e => e.Id)
        .Text("Region", e => e.Region).Text("Desk", e => e.Desk)
        .Number("Amount", e => e.Amount).Number("Measure", e => e.Measure);

    // A leaf by Region and Desk is an id mod 350: ids k, k + 350, k + 700, … share one, and the
    // row of k, the lowest, is its first.
    private static Entry Make(long id, decimal amount) => new(id, "R" + (id % 7), "D" + (id % 50), amount, (double)amount / 7);

    private static PivotValueField Sum(string field) => new(field, PivotAggregation.Sum);

    [Theory] // ADR-0153, PV-21: a new question chains no rows, and knows each leaf's first row
    [InlineData("rows")]
    [InlineData("none")]
    [InlineData("hidden")]
    public async Task A_new_question_chains_no_rows(string shape)
    {
        var fields = Fields();
        // Three chunks of rows, so leaves are made in more than one.
        var source = PivotSource.From([.. Enumerable.Range(1, 10_000).Select(id => Make(id, id % 97 - 48))], fields);
        using var session = new PivotComputationSession(source);
        var layout = shape switch
        {
            "rows" => new PivotLayout { Rows = [new("Region"), new("Desk")], Values = [Sum("Amount"), Sum("Measure")] },
            "none" => new PivotLayout { Values = [Sum("Amount"), Sum("Measure")] },
            _ => new PivotLayout
            {
                Rows = [new PivotFieldPlacement("Region") with { HiddenItems = [PivotItemKey.Text("R1")] }, new("Desk")],
                Values = [Sum("Measure")],
            },
        };
        await session.ComputeAsync(layout, Pivot.EnUs, cancellationToken: TestContext.Current.CancellationToken);
        var pass = session.Pass!;
        Assert.False(pass.HasChains);
        AssertFirstRecords(pass);
        Assert.False(pass.HasChains);
    }

    [Fact] // ADR-0153, ADR-0067: a batch that keeps each leaf's first row, or empties a leaf, chains no rows
    public async Task Batches_that_keep_first_rows_or_empty_leaves_chain_no_rows()
    {
        var fields = Fields();
        var source = PivotSource.From([.. Enumerable.Range(1, 2_000).Select(id => Make(id, id % 97 - 48))], fields);
        using var session = new PivotComputationSession(source);
        // An exact sum is taken out by subtraction, so no batch below recomputes a leaf from its rows.
        var layout = new PivotLayout { Rows = [new("Region"), new("Desk")], Values = [Sum("Amount")] };
        await Ask();

        // Later rows of their leaves change, and leave: every leaf keeps its first row.
        await Apply(fields.Batch(changed: [Make(400, 5), Make(1_401, -7)]));
        await Apply(fields.Batch(removedKeys: [751L, 1_752L]));
        // R3/D3 loses all six of its rows in one batch, and a later batch gives it one again; North
        // brings a leaf no row had.
        await Apply(fields.Batch(removedKeys: [.. Enumerable.Range(0, 6).Select(n => (object)(3L + 350 * n))]));
        await Apply(fields.Batch(added: [Make(2_103, 9), new Entry(2_200, "North", "D1", 4, 0.5)]));
        Assert.False(session.Pass!.HasChains);

        // R1/D1's first row changes while its later rows stay: the next first is found among them,
        // along the chains, which are made for it.
        await Apply(fields.Batch(changed: [Make(1, 3)]));
        Assert.True(session.Pass!.HasChains);
        await Apply(fields.Batch(removedKeys: [351L], changed: [Make(701, 2)]));

        async Task Ask()
        {
            await session.ComputeAsync(layout, Pivot.EnUs, cancellationToken: TestContext.Current.CancellationToken);
            AssertFirstRecords(session.Pass!);
        }
        async Task Apply(ExGrid.Data.ChangeBatch batch)
        {
            Assert.False(source.Apply(batch).Compacted);
            await Ask();
        }
    }

    // Each leaf's first record, read off the rows the pass holds: the lowest numbered row it holds,
    // or none at all for a leaf every row has left.
    private static void AssertFirstRecords(AggregationPass pass)
    {
        var first = new Dictionary<int, int>();
        for (var number = pass.NumberedRows - 1; number >= 0; number--)
        {
            if (pass.LeafOfRow(number) is var leaf and >= 0)
                first[leaf] = number;
        }
        for (var leaf = 0; leaf < pass.Leaves.Count; leaf++)
            Assert.Equal(first.TryGetValue(leaf, out var row) ? row : int.MaxValue, pass.FirstRecord(leaf));
    }
}
