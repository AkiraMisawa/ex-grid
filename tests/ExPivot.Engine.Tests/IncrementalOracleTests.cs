using System.Globalization;
using Xunit;

namespace ExPivot.Engine.Tests;

/// <summary>
/// The incremental computation against a fresh one (ADR-0153; LV-20, LV-26, LV-27). Over recorded
/// seeds, layouts are drawn at random — one or two row fields, none to two column fields, number,
/// date, Boolean and blank Items, subtotals on, off and at the bottom, each form, collapsed and
/// hidden Items, a report filter, grand totals off, Values on rows or columns, an order by label or
/// by a Value Field (a percentage too) — with every Aggregation and Show Values As cycled through
/// their Value Fields; each is then driven through random Change Batches that add, change and
/// remove records, Items appearing and leaving. After every batch the Window a report source
/// shows equals a report computed afresh from the same data, row by row and cell by cell, in
/// values and painted text; a display row whose painted state did not change is the instance shown
/// before, and one that did is a new one; and the Row Sequence Version moves exactly when the rows'
/// order does (ADR-0011).
/// </summary>
public class IncrementalOracleTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed record Row(long Id, string? Region, string Desk, DateTime Day, bool Flag, int Qty, decimal Amount, double Measure);

    private static readonly PivotFields<Row> Fields = PivotFields.Of<Row>()
        .Key("Id", r => r.Id)
        .Text("Region", r => r.Region)
        .Text("Desk", r => r.Desk)
        .Date("Day", r => r.Day, format: "yyyy-MM-dd")
        .Month("Month", of: "Day")
        .Boolean("Flag", r => r.Flag)
        .Number("Qty", r => r.Qty)
        .Number("Amount", r => r.Amount)
        .Number("Measure", r => r.Measure);

    // Fields that can stand in Rows, Columns or Filters: text with a blank, text, a date, a date
    // part, a Boolean and a number.
    private static readonly string[] AxisFields = ["Region", "Desk", "Day", "Month", "Flag", "Qty"];
    private static readonly string[] ValueFields = ["Amount", "Measure", "Qty"];
    private static readonly string?[] Formats = [null, "0.00", "G17", "0%", "#,##0"];

    private static readonly PivotOptions Options = new() { Culture = CultureInfo.GetCultureInfo("en-US") };

    /// <summary>A clock that never moves: no change mark ages out between batches.</summary>
    private sealed class StillClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
    }

    private static Row MakeRow(long id, Random random) => new(
        id,
        random.Next(9) == 0 ? null : new[] { "East", "West", "North", "south" }[random.Next(4)],
        "Desk" + random.Next(6).ToString(CultureInfo.InvariantCulture),
        new DateTime(2026, 1 + random.Next(4), 1 + random.Next(3)),
        random.Next(2) == 0,
        random.Next(-3, 8),
        random.Next(-500, 2000) / 100m,
        random.Next(5) == 0 ? random.NextDouble() * 1e12 : Math.Round(random.NextDouble() * 100 - 30, 3));

    private static PivotLayout DrawLayout(Random random, int ordinal)
    {
        var pool = AxisFields.OrderBy(_ => random.Next()).ToList();
        string Take() { var field = pool[0]; pool.RemoveAt(0); return field; }
        var rows = Enumerable.Range(0, 1 + random.Next(2)).Select(_ => Take()).ToArray();
        var columns = Enumerable.Range(0, random.Next(3)).Select(_ => Take()).ToArray();
        var valueCount = 1 + random.Next(3);
        var values = Enumerable.Range(0, valueCount).Select(v =>
        {
            // Every Aggregation and every Show Values As, cycled across the layouts drawn.
            var aggregation = (PivotAggregation)((ordinal * 3 + v) % Enum.GetValues<PivotAggregation>().Length);
            var show = (PivotShowValuesAs)((ordinal + v) % Enum.GetValues<PivotShowValuesAs>().Length);
            return new PivotValueField(ValueFields[random.Next(ValueFields.Length)], aggregation)
            {
                ShowValuesAs = show,
                NumberFormat = Formats[random.Next(Formats.Length)],
            };
        }).ToArray();
        PivotFieldPlacement Place(string field, bool sortable)
        {
            var placement = new PivotFieldPlacement(field)
            {
                Subtotals = random.Next(4) != 0,
                Collapsed = random.Next(6) == 0,
                Sort = random.Next(5) switch
                {
                    0 => PivotSort.Descending,
                    1 or 2 when sortable => new PivotSort(random.Next(2) == 0 ? PivotSortDirection.Ascending : PivotSortDirection.Descending, random.Next(valueCount)),
                    _ => PivotSort.Ascending,
                },
            };
            if (random.Next(3) == 0)
                placement = placement with { HiddenItems = [ItemOf(field, random)] };
            if (random.Next(3) == 0)
                placement = placement with { ToggledItems = [ItemOf(field, random)] };
            return placement;
        }
        var layout = new PivotLayout
        {
            Rows = [.. rows.Select(field => Place(field, sortable: true))],
            Columns = [.. columns.Select(field => Place(field, sortable: true))],
            Values = values,
            Form = (PivotReportForm)random.Next(3),
            SubtotalsAtTop = random.Next(2) == 0,
            GrandTotalRow = random.Next(4) != 0,
            GrandTotalColumn = random.Next(4) != 0,
            RepeatItemLabels = random.Next(3) == 0,
            ValuesAxis = random.Next(2) == 0 ? PivotAxis.Rows : PivotAxis.Columns,
        };
        if (random.Next(2) == 0)
        {
            var filter = Take();
            layout = layout with
            {
                Filters = [new PivotFieldPlacement(filter) { HiddenItems = random.Next(2) == 0 ? [ItemOf(filter, random)] : [] }],
            };
        }
        return layout;
    }

    // An Item a field may have, to hide or to toggle: sometimes one no record carries yet.
    private static PivotItemKey ItemOf(string field, Random random) => field switch
    {
        "Region" => random.Next(4) == 0 ? PivotItemKey.Blank : PivotItemKey.Text(new[] { "East", "West", "North", "south", "Nowhere" }[random.Next(5)]),
        "Desk" => PivotItemKey.Text("Desk" + random.Next(8).ToString(CultureInfo.InvariantCulture)),
        "Day" => PivotItemKey.Date(new DateTime(2026, 1 + random.Next(4), 1 + random.Next(3))),
        "Month" => PivotItemKey.Number(1 + random.Next(4)),
        "Flag" => PivotItemKey.Boolean(random.Next(2) == 0),
        _ => PivotItemKey.Number(random.Next(-3, 8)),
    };

    public static TheoryData<int> Seeds() => [20261008, 153, 4242, 7, 99991, 31337];

    [Theory] // ADR-0153 (LV-20/LV-26/LV-27): over random layouts and Change Batches, the incremental Window equals a fresh computation, keeps the instance of every row it did not change, and moves the Row Sequence Version exactly when the order moves
    [MemberData(nameof(Seeds))]
    public async Task Incremental_windows_equal_fresh_reports(int seed)
    {
        var random = new Random(seed);
        for (var drawn = 0; drawn < 6; drawn++)
        {
            var ordinal = (seed % 1000) + drawn;
            var layout = DrawLayout(random, ordinal);
            var records = Enumerable.Range(0, 120).Select(i => MakeRow(i, random)).ToDictionary(r => r.Id);
            var nextId = 1_000L;
            var data = PivotSource.From([.. records.Values], Fields);
            await using var source = PivotReportSource.From(data, timeProvider: new StillClock());
            var client = new PivotReportClient(source);
            var settings = PivotReportSettings.From(Options) with { ChangeHighlightDuration = TimeSpan.FromDays(1) };
            var window = new PivotReportWindow(0, 100_000);
            var context = $"seed {seed}, layout {drawn}: {PivotLayoutJson.Write(layout)}";
            Assert.True(await client.ReadAsync(layout, settings, window, cancellationToken: Ct), context + ": " + client.Refusal?.Message);
            await SameAsFreshAsync(client.Current!, data, layout, context + ", first");

            for (var step = 0; step < 12; step++)
            {
                var before = client.Current!;
                var added = new List<Row>();
                var changed = new List<Row>();
                var removed = new List<long>();
                var touched = new HashSet<long>();
                for (var n = 1 + random.Next(4); n > 0; n--)
                {
                    var choice = random.Next(10);
                    if (choice < 2 || records.Count < 20)
                    {
                        var row = MakeRow(nextId++, random);
                        // Now and then an Item no record had.
                        if (random.Next(3) == 0)
                            row = row with { Desk = "New" + random.Next(3).ToString(CultureInfo.InvariantCulture) };
                        added.Add(row);
                        touched.Add(row.Id);
                    }
                    else
                    {
                        var id = records.Keys.ElementAt(random.Next(records.Count));
                        if (!touched.Add(id))
                            continue;
                        if (choice < 4)
                            removed.Add(id);
                        else if (choice < 7)
                        {
                            // Only the measures move: an order by value, a total or a percentage
                            // may change with no Item appearing or leaving.
                            var fresh = MakeRow(id, random);
                            changed.Add(records[id] with { Amount = fresh.Amount * 10, Measure = fresh.Measure });
                        }
                        else
                            changed.Add(MakeRow(id, random) with { Desk = random.Next(2) == 0 ? records[id].Desk : "Desk" + random.Next(8).ToString(CultureInfo.InvariantCulture) });
                    }
                }
                data.Apply(Fields.Batch(added: added, changed: changed, removedKeys: [.. removed]));
                foreach (var row in added.Concat(changed))
                    records[row.Id] = row;
                foreach (var id in removed)
                    records.Remove(id);

                var where = $"{context}, step {step}";
                Assert.True(await client.ReadAsync(layout, settings, window, cancellationToken: Ct), where + ": " + client.Refusal?.Message);
                var after = client.Current!;
                await SameAsFreshAsync(after, data, layout, where);
                SharesUnchangedRows(before, after, where);
            }
        }
    }

    // The Window equals a report computed afresh from the same data: keys and their order, labels,
    // each value's painted text, exact and approximate number and error, the headers and spans.
    private static async Task SameAsFreshAsync(PivotReportState shown, SnapshotPivotSource data, PivotLayout layout, string where)
    {
        var fresh = PivotSource.From(data.Snapshot, Fields.Fields);
        var query = PivotQuery.For(layout);
        var expected = PivotEngine.Report(PivotEngine.Cube(query, await fresh.AggregateAsync(query, Ct), fresh.Fields), layout, Options);
        Assert.True(expected.Rows.Count == shown.Metadata.RowCount, $"{where}: {shown.Metadata.RowCount} rows, a fresh report has {expected.Rows.Count}");
        Assert.True(expected.Rows.Select(row => row.Key).SequenceEqual(shown.Rows.Select(row => row.Key)), $"{where}: the rows' order differs");
        string[] headers = [.. shown.Metadata.LabelColumns.Select(c => c.Header), .. shown.Metadata.ValueColumns.Select(c => c.Header)];
        Assert.Equal(Pivot.Headers(expected), headers);
        Assert.Equal(expected.HeaderSpans, shown.Metadata.HeaderSpans);
        for (var r = 0; r < expected.Rows.Count; r++)
        {
            var row = expected.Rows[r];
            Assert.True(row.Labels.SequenceEqual(shown.Rows[r].Labels), $"{where}: row {r} {row.Key} has other labels");
            for (var c = 0; c < expected.ValueColumns.Count; c++)
            {
                var e = expected.ValueAt(row, c);
                var a = shown.Rows[r].ValueAt(c);
                Assert.True((e?.Text, e?.Exact, e?.Error) == (a?.Text, a?.Exact, a?.Error)
                        && (e is null || e.Number.Equals(a!.Number)),
                    $"{where}: row {r} {row.Key}, column {c}: '{e?.Text}' ({e?.Exact}, {e?.Number:R}) afresh, '{a?.Text}' ({a?.Exact}, {a?.Number:R}) shown");
            }
        }
    }

    // A row whose painted state — labels, shown values under the same value columns, and change
    // marks — did not change is the instance shown before; one that did is new. When the value
    // columns themselves changed, a row painted against them may be new either way. The Row
    // Sequence Version moves exactly when the rows' order does: a Selection is dropped then, and
    // only then (ADR-0011).
    private static void SharesUnchangedRows(PivotReportState before, PivotReportState after, string where)
    {
        var previous = before.Rows.ToDictionary(row => row.Key);
        var sameColumns = before.Metadata.ValueColumns.Select(c => c.Name).SequenceEqual(after.Metadata.ValueColumns.Select(c => c.Name));
        foreach (var row in after.Rows)
        {
            if (!previous.TryGetValue(row.Key, out var old))
                continue;
            if (!sameColumns)
            {
                Assert.True(!ReferenceEquals(old, row) || old.Values.SequenceEqual(row.Values), $"{where}: {row.Key} is the instance shown before, with other values");
                continue;
            }
            var same = old.Labels.SequenceEqual(row.Labels) && old.Values.SequenceEqual(row.Values) && old.ChangedIn.SequenceEqual(row.ChangedIn)
                && old.RowPath.Select(p => (p.Field, p.Item.Kind, p.Item.Value)).SequenceEqual(row.RowPath.Select(p => (p.Field, p.Item.Kind, p.Item.Value)));
            Assert.True(same == ReferenceEquals(old, row),
                $"{where}: {row.Key} {(same ? "did not change, and is a new instance" : "changed, and is the instance shown before")}");
        }
        var reordered = !before.Rows.Select(row => row.Key).SequenceEqual(after.Rows.Select(row => row.Key));
        Assert.True(reordered == (before.Metadata.RowSequenceVersion != after.Metadata.RowSequenceVersion),
            $"{where}: the rows {(reordered ? "moved" : "kept their order")}, and the Row Sequence Version {(reordered ? "did not" : "did")}");
    }
}
