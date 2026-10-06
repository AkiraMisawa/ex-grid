using System.Globalization;
using BoundaryBench;
using ExPivot.Engine;

// A small untimed semantic oracle using the shipped computation API.
var source = new Source(5952, 2976);
var report = new Report(source.Leaves());
var records = Enumerable.Range(0, 5952).Select(i => new Input(
    $"Group {(i % 2976) / 124:D3}", $"Item {i % 2976:D6}", (10000 + i % 19001) / 100m)).ToArray();
PivotField<Input>[] fields = [new("Group", PivotFieldType.Text, r => r.Group),
    new("Item", PivotFieldType.Text, r => r.Item), new("Value", PivotFieldType.Number, r => r.Value)];
var version = 0;
Check("initial");
foreach (var (batch, visible) in new[] { (1, true), (1, false), (1000, true), (1000, false) })
{
    for (var i = 0; i < batch; i++)
    {
        var id = visible && i == 0 ? 0 : 124 + i;
        records[id] = records[id] with { Value = records[id].Value + 0.01m + (version % 11) / 100m };
    }
    report.Apply(source.Apply(batch, visible));
    version++;
    Check($"update {batch} {visible}");
}
report.ToggleExpand(); Check("collapse");
report.ToggleExpand(); Check("expand");
report.Sort(); Check("sort by Sum descending");
Console.WriteLine("All 8 phases match the shipped PivotEngine.Compute in every row label and exact decimal value.");

void Check(string phase)
{
    var layout = new PivotLayout
    {
        Rows = [new("Group") { ToggledItems = report.Collapsed ? [PivotItemKey.Text("Group 000")] : [] },
            new("Item") { Sort = report.Sorted ? new PivotSort(PivotSortDirection.Descending, 0) : PivotSort.Ascending }],
        Values = [new("Value", PivotAggregation.Sum)],
    };
    var oracle = PivotEngine.Compute(records, fields, layout, new PivotOptions { Culture = CultureInfo.InvariantCulture });
    var rows = report.Rows().ToArray();
    if (rows.Length != oracle.Rows.Count) throw new InvalidOperationException($"Row count differs: {phase}.");
    for (var i = 0; i < rows.Length; i++)
    {
        var expected = oracle.Rows[i];
        if (rows[i].Label != expected.Labels.Single().Text || rows[i].Value != expected.ValueAt(0)?.Exact)
            throw new InvalidOperationException($"Row {i} differs in {phase}: {rows[i]} vs {expected.Labels.Single().Text} {expected.ValueAt(0)?.Exact}.");
    }
    Console.WriteLine($"PASS {phase}: {rows.Length} rows, {report.FullHash()}");
}
sealed record Input(string Group, string Item, decimal Value);
