using System.Globalization;
using Xunit;

namespace ExPivot.Engine.Tests;

/// <summary>
/// What a computation's first report starts from, and what its updates then change (ADR-0153).
/// The first report takes its trees, cells and rows as the cube and the first layout made them;
/// an update writes only what it changes, and lays a node out again only where the data asks it
/// to. Whichever part an update touches — one the first report laid out or one an update did —
/// the report equals a fresh one; a spelling that changes relabels every node of its Item
/// (ADR-0060); and the rows an update says it removed and relabelled are exactly the rows that
/// left and changed, which the report source measures the label columns from.
/// </summary>
public class FirstReportStructureTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed record Sale(long Id, string Region, string Desk, string Book, decimal Amount, double Measure);

    private static readonly PivotFields<Sale> Fields = PivotFields.Of<Sale>()
        .Key("Id", s => s.Id)
        .Text("Region", s => s.Region)
        .Text("Desk", s => s.Desk)
        .Text("Book", s => s.Book)
        .Number("Amount", s => s.Amount)
        .Number("Measure", s => s.Measure);

    [Theory] // ADR-0153 / ADR-0060: a spelling that changes relabels, on either axis, the nodes the first report laid out, the ones an update added and one removed and added again, as a fresh report labels them
    [InlineData(false)]
    [InlineData(true)]
    public async Task ADR0153_a_changed_spelling_relabels_every_node_of_its_item(bool regionOnColumns)
    {
        var records = new List<Sale>
        {
            new(1, "East", "Alpha", "B1", 10, 1), new(2, "east", "Beta", "B1", 20, 2),
            new(3, "West", "Alpha", "B2", 30, 3), new(6, "West", "Beta", "B2", 5, 0.5),
        };
        var source = PivotSource.From(records, Fields);
        using var session = new PivotComputationSession(source);
        PivotValueField[] values = [new("Amount", PivotAggregation.Sum), new("Measure", PivotAggregation.Max)];
        var layout = regionOnColumns
            ? new PivotLayout { Rows = [new("Desk")], Columns = [new("Region")], Values = values }
            : new PivotLayout { Rows = [new("Region"), new("Desk")], Values = values };
        await CompareAsync("first");

        // A node under East an update adds, labelled as East is spelled now.
        await ApplyAsync("an Item added under East", added: [new(4, "EAST", "Gamma", "B3", 7, 7)]);
        // East's first spelling leaves with its last record: every East node is spelled "east",
        // and East / Alpha leaves the report.
        await ApplyAsync("the first spelling gone", removed: [1]);
        await ApplyAsync("the second spelling gone", removed: [2]);
        // East / Alpha comes back, spelled as East is spelled again.
        await ApplyAsync("a removed node added again", added: [new(5, "east", "Alpha", "B1", 3, 3)]);
        await ApplyAsync("a spelling that labels nothing yet", added: [new(7, "WEST", "Delta", "B4", 1, 1)]);
        await ApplyAsync("West's first spelling gone", removed: [3, 6]);

        async Task ApplyAsync(string what, Sale[]? added = null, long[]? removed = null)
        {
            source.Apply(Fields.Batch(added: added, removedKeys: removed?.Cast<object>().ToArray()));
            records.RemoveAll(record => removed?.Contains(record.Id) == true);
            records.AddRange(added ?? []);
            await CompareAsync(what);
        }

        async Task CompareAsync(string what)
        {
            var result = await session.ComputeAsync(layout, Pivot.EnUs, cancellationToken: Ct);
            await SameAsFreshAsync(result.Report!, source, layout, what);
        }
    }

    public static TheoryData<int, PivotReportForm, bool> Runs() => new()
    {
        { 20261009, PivotReportForm.Compact, false },
        { 153, PivotReportForm.Tabular, true },
        { 7, PivotReportForm.Outline, false },
        { 4242, PivotReportForm.Tabular, false },
    };

    [Theory] // ADR-0153: over random structural batches, whichever parts an update splits, its removed rows are exactly the rows that left, its label changes every row that came or whose labels changed, and the report equals a fresh one
    [MemberData(nameof(Runs))]
    public async Task ADR0153_an_updates_removed_and_relabelled_rows_are_exactly_the_rows_that_left_and_changed(
        int seed, PivotReportForm form, bool repeatLabels)
    {
        var random = new Random(seed);
        string[] regions = ["East", "EAST", "east", "West", "North", "south"];
        var nextId = 0L;
        Sale Make(long id) => new(id, regions[random.Next(regions.Length)],
            "Desk" + random.Next(6).ToString(CultureInfo.InvariantCulture),
            "Book" + random.Next(8).ToString(CultureInfo.InvariantCulture),
            random.Next(-50, 200), random.NextDouble() * 100);
        var records = Enumerable.Range(0, 300).Select(_ => Make(nextId++)).ToDictionary(r => r.Id);
        var source = PivotSource.From([.. records.Values], Fields);
        using var session = new PivotComputationSession(source);
        var layout = new PivotLayout
        {
            Rows = [new("Region"), new("Desk"), new("Book")],
            Values =[new("Amount", PivotAggregation.Sum), new("Measure", PivotAggregation.Min)],
            Form = form,
            RepeatItemLabels = repeatLabels,
        };
        var previous = (await session.ComputeAsync(layout, Pivot.EnUs, cancellationToken: Ct)).Report!;
        for (var step = 0; step < 24; step++)
        {
            var added = new List<Sale>();
            var changed = new List<Sale>();
            var removed = new List<object>();
            if (step % 4 == 3)
            {
                // A whole Desk of a Region leaves: a subtree the first report laid out, or one an
                // update laid out since.
                var desk = records.Values.ElementAt(random.Next(records.Count));
                foreach (var record in records.Values.Where(r => string.Equals(r.Region, desk.Region, StringComparison.OrdinalIgnoreCase) && r.Desk == desk.Desk))
                    removed.Add(record.Id);
            }
            for (var n = 1 + random.Next(5); n > 0; n--)
            {
                var choice = random.Next(10);
                if (choice < 3)
                    added.Add(Make(nextId++));
                else
                {
                    var id = records.Keys.ElementAt(random.Next(records.Count));
                    if (removed.Contains(id) || changed.Any(r => r.Id == id))
                        continue;
                    if (choice < 5)
                        removed.Add(id);
                    else
                        changed.Add(Make(id));
                }
            }
            source.Apply(Fields.Batch(added: added, changed: changed, removedKeys: removed));
            foreach (var id in removed)
                records.Remove((long)id);
            foreach (var record in added.Concat(changed))
                records[record.Id] = record;

            var where = $"seed {seed}, step {step}";
            var result = await session.ComputeAsync(layout, Pivot.EnUs, cancellationToken: Ct);
            var report = result.Report!;
            await SameAsFreshAsync(report, source, layout, where);
            if (!result.IsReset)
            {
                var before = previous.Rows.ToDictionary(row => row.Key);
                var after = report.Rows.ToDictionary(row => row.Key);
                Assert.Equal(result.RemovedRows.Count, result.RemovedRows.Distinct().Count());
                Assert.True(before.Keys.Where(key => !after.ContainsKey(key)).ToHashSet().SetEquals(result.RemovedRows),
                    $"{where}: the rows removed are not the rows that left");
                var relabelled = result.LabelChanges.Select(row => row.Key).ToHashSet();
                foreach (var row in report.Rows)
                {
                    if (!before.TryGetValue(row.Key, out var old) || !old.Labels.SequenceEqual(row.Labels))
                        Assert.True(relabelled.Contains(row.Key), $"{where}: {row.Key} came or changed its labels, and is not among the label changes");
                }
                foreach (var row in result.LabelChanges)
                    Assert.True(after.TryGetValue(row.Key, out var shown) && ReferenceEquals(shown, row), $"{where}: {row.Key} is a label change the report does not show");
            }
            previous = report;
        }
    }

    // The report equals one computed afresh from the same data: the rows' keys in order, their
    // labels, the Items each row and value column stands for as spelled, the headers and their
    // spans, and every value.
    private static async Task SameAsFreshAsync(PivotReport actual, SnapshotPivotSource source, PivotLayout layout, string where)
    {
        var fresh = PivotSource.From(source.Snapshot, Fields.Fields);
        var query = PivotQuery.For(layout);
        var expected = PivotEngine.Report(PivotEngine.Cube(query, await fresh.AggregateAsync(query, Ct), fresh.Fields), layout, Pivot.EnUs);
        Assert.True(expected.Rows.Select(row => row.Key).SequenceEqual(actual.Rows.Select(row => row.Key)), $"{where}: the rows differ");
        Assert.True(expected.Rows.SelectMany(row => row.Labels).SequenceEqual(actual.Rows.SelectMany(row => row.Labels)), $"{where}: the labels differ");
        Assert.Equal(Spelled(expected.Rows.SelectMany(expected.RowPath)), Spelled(actual.Rows.SelectMany(actual.RowPath)));
        Assert.Equal(Spelled(Enumerable.Range(0, expected.ValueColumns.Count).SelectMany(expected.ColumnPath)),
            Spelled(Enumerable.Range(0, actual.ValueColumns.Count).SelectMany(actual.ColumnPath)));
        Assert.Equal(Pivot.Headers(expected), Pivot.Headers(actual));
        Assert.Equal(Pivot.Spans(expected), Pivot.Spans(actual));
        for (var r = 0; r < expected.Rows.Count; r++)
        {
            for (var c = 0; c < expected.ValueColumns.Count; c++)
            {
                var e = expected.ValueAt(expected.Rows[r], c);
                var a = actual.ValueAt(actual.Rows[r], c);
                Assert.True((e?.Text, e?.Exact, e?.Error) == (a?.Text, a?.Exact, a?.Error) && (e is null || e.Number.Equals(a!.Number)),
                    $"{where}: row {r} {expected.Rows[r].Key}, column {c}: '{e?.Text}' afresh, '{a?.Text}' shown");
            }
        }
    }

    private static string[] Spelled(IEnumerable<(string Field, PivotItemKey Item)> path)
        => [.. path.Select(p => p.Field + "=" + p.Item.Kind + ":" + p.Item.Value)];
}
