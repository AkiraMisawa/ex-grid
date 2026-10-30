using ExPivot.Engine;
using Xunit;

namespace ExPivot.Engine.Tests;

public class ComputationSessionTests
{
    private sealed record Entry(long Id, string Region, string Desk, string Quarter, decimal Amount, double Measure);

    [Theory] // ADR-0153: every form, aggregation and percentage agrees with fresh computation
    [InlineData(PivotReportForm.Compact, false, PivotAxis.Columns)]
    [InlineData(PivotReportForm.Outline, true, PivotAxis.Rows)]
    [InlineData(PivotReportForm.Tabular, false, PivotAxis.Columns)]
    [InlineData(PivotReportForm.Tabular, true, PivotAxis.Rows)]
    public async Task Structural_batches_match_a_fresh_report(PivotReportForm form, bool repeat, PivotAxis axis)
    {
        var fields = PivotFields.Of<Entry>().Key("Id", r => r.Id).Text("Region", r => r.Region)
            .Text("Desk", r => r.Desk).Text("Quarter", r => r.Quarter)
            .Number("Amount", r => r.Amount).Number("Measure", r => r.Measure);
        var records = new List<Entry>
        {
            new(1, "East", "Alpha", "Q1", 10, 1e16), new(2, "East", "Beta", "Q2", 20, 1),
            new(3, "West", "Alpha", "Q1", 30, -1e16), new(4, "West", "Gamma", "Q2", 40, 3),
            new(5, "EAST", "Alpha", "Q1", 2, 0.5), new(6, "East", "Delta", "Q2", 9, -1),
        };
        var source = PivotSource.From(records, fields);
        var compacted = false;
        using var session = new PivotComputationSession(source);
        var layout = new PivotLayout
        {
            Rows = [new("Region") { Sort = new(PivotSortDirection.Descending, 0) }, new("Desk")],
            Columns = [new("Quarter")], Form = form, RepeatItemLabels = repeat, ValuesAxis = axis,
            Values = [.. new[] { "Amount", "Measure" }.SelectMany(field => Enum.GetValues<PivotAggregation>()
                .SelectMany(aggregation => Enum.GetValues<PivotShowValuesAs>().Select(mode =>
                    new PivotValueField(field, aggregation) { ShowValuesAs = mode, NumberFormat = "G17" })))],
        };
        var first = (await session.ComputeAsync(layout, Pivot.EnUs, cancellationToken: TestContext.Current.CancellationToken)).Report!;
        var original = Read(first);
        await Compare();
        // Moving the first record also changes the spelling that remains for East.
        await Change(new(1, "North", "New", "Q3", 7, 2));
        await Change(new(2, "EAST", "Aardvark", "Q1", -25, -2));
        await Change(new(6, "North", "Delta", "Q2", 9, -1));
        Apply(fields.Batch(added: [new Entry(7, "East", "Zero", "Q0", 11, 0.25)]));
        records.Add(new(7, "East", "Zero", "Q0", 11, 0.25));
        await Compare();
        foreach (var id in new long[] { 3, 4, 7, 5 })
        {
            Apply(fields.Batch(removedKeys: [id]));
            records.RemoveAll(record => record.Id == id);
            await Compare();
        }
        // A reproducible sequence exercises complete batches that overlap before the next read.
        var random = new Random(153);
        for (var batch = 0; batch < 24; batch++)
        {
            for (var pending = 0; pending < 3; pending++)
            {
                var id = random.Next(1, 18);
                var prior = records.FindIndex(record => record.Id == id);
                if (prior >= 0 && random.Next(4) == 0)
                {
                    Apply(fields.Batch(removedKeys: [(long)id]));
                    records.RemoveAt(prior);
                }
                else
                {
                    var entry = new Entry(id, new[] { "East", "EAST", "North", "West" }[random.Next(4)],
                        "Desk" + random.Next(5), "Q" + random.Next(4), random.Next(-20, 20), random.Next(-20, 20) / 7d);
                    Apply(prior < 0 ? fields.Batch(added: [entry]) : fields.Batch(changed: [entry]));
                    if (prior >= 0) records.RemoveAt(prior);
                    records.Add(entry);
                }
            }
            await Compare();
        }
        Assert.Equal(original, Read(first));

        void Apply(ExGrid.Data.ChangeBatch batch) => compacted |= source.Apply(batch).Compacted;
        async Task Change(Entry entry)
        {
            Apply(fields.Batch(changed: [entry]));
            records.RemoveAll(record => record.Id == entry.Id);
            records.Add(entry);
            await Compare();
        }
        async Task Compare()
        {
            var actual = await session.ComputeAsync(layout, Pivot.EnUs, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(compacted, actual.IsReset);
            compacted = false;
            var fresh = PivotSource.From(source.Snapshot, fields.Fields);
            var query = PivotQuery.For(layout);
            var answer = await fresh.AggregateAsync(query, TestContext.Current.CancellationToken);
            var expected = PivotEngine.Report(PivotEngine.Cube(query, answer, fresh.Fields), layout, Pivot.EnUs);
            Assert.Equal(expected.Rows.Select(r => r.Key), actual.Report!.Rows.Select(r => r.Key));
            Assert.Equal(expected.Rows.SelectMany(r => r.Labels), actual.Report.Rows.SelectMany(r => r.Labels));
            Assert.Equal(expected.Rows.SelectMany(expected.RowPath).Select(p => p.Item.Value),
                actual.Report.Rows.SelectMany(actual.Report.RowPath).Select(p => p.Item.Value));
            Assert.Equal(Pivot.Headers(expected), Pivot.Headers(actual.Report));
            Assert.Equal(Pivot.Spans(expected), Pivot.Spans(actual.Report));
            Assert.Equal(Read(expected), Read(actual.Report));
        }
        static string[] Read(PivotReport report) => report.Rows.SelectMany(row =>
            Enumerable.Range(0, report.ValueColumns.Count).Select(c =>
            {
                var value = report.ValueAt(row, c);
                return $"{c}: {value?.Text} | {value?.Exact} | {value?.Number:R} | {value?.Error}";
            })).ToArray();
    }

    [Fact] // ADR-0153: adding and removing Items changes only their report portions
    public async Task Items_appear_and_disappear_without_resetting_the_computation()
    {
        var fields = Fields();
        var source = PivotSource.From([new Trade(1, "East", 10), new Trade(2, "West", 20)], fields);
        using var session = new PivotComputationSession(source);
        var first = (await session.ComputeAsync(Layout(), cancellationToken: TestContext.Current.CancellationToken)).Report!;
        source.Apply(fields.Batch(added: [new Trade(3, "North", 30)]));
        var added = await session.ComputeAsync(Layout(), cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(added.IsReset);
        Assert.Equal(["East", "North", "West", "Grand Total"], added.Report!.Rows.Select(r => r.Labels[0].Text));
        Assert.Same(first.Rows[0], added.Report.Rows[0]);
        Assert.Equal("60.00", added.Report.ValueAt(added.Report.Rows[^1], 0)!.Text);
        Assert.Equal(["North"], added.LabelChanges.Select(r => r.Labels[0].Text));
        source.Apply(fields.Batch(removedKeys: [3L]));
        var removed = await session.ComputeAsync(Layout(), cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(removed.IsReset);
        Assert.Equal(first.Rows.Select(r => r.Key), removed.Report!.Rows.Select(r => r.Key));
        Assert.Single(removed.RemovedRows);
        Assert.Equal(4, added.Report.Rows.Count);
    }

    [Fact] // ADR-0153: independent questions fold the same complete batches without rereading the input
    public async Task Independent_reports_share_the_Pivot_Source_but_keep_their_own_computation()
    {
        var fields = Fields();
        var records = Enumerable.Range(0, 1000).Select(i => new Trade(i, "Desk" + i % 10, i)).ToArray();
        var source = PivotSource.From(records, fields);
        using var sums = new PivotComputationSession(source);
        using var counts = new PivotComputationSession(source);
        long sumReads = 0, countReads = 0;
        var sumPace = new PivotSlicing { RowsRead = n => sumReads += n, Budget = TimeSpan.Zero, Yield = async _ => await Task.Yield() };
        var countPace = sumPace with { RowsRead = n => countReads += n };
        var countLayout = Layout() with { Values = [new("Amount", PivotAggregation.Count)] };
        var sumBefore = (await sums.ComputeAsync(Layout(), slicing: sumPace, cancellationToken: TestContext.Current.CancellationToken)).Report!;
        var countBefore = (await counts.ComputeAsync(countLayout, slicing: countPace, cancellationToken: TestContext.Current.CancellationToken)).Report!;
        Assert.Equal(1000, sumReads); Assert.Equal(1000, countReads);
        sumReads = countReads = 0;
        source.Apply(fields.Batch(changed: [records[1] with { Amount = 1001 }]));
        source.Apply(fields.Batch(changed: [records[2] with { Amount = 1002 }]));
        var sumAfter = await sums.ComputeAsync(Layout(), slicing: sumPace, cancellationToken: TestContext.Current.CancellationToken);
        var countAfter = await counts.ComputeAsync(countLayout, slicing: countPace, cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(sumAfter.IsReset); Assert.False(countAfter.IsReset);
        Assert.Equal(2, sumReads); Assert.Equal(2, countReads);
        Assert.Equal(499500m, sumBefore.ValueAt(sumBefore.Rows[^1], 0)!.Exact);
        Assert.Equal(501500m, sumAfter.Report!.ValueAt(sumAfter.Report.Rows[^1], 0)!.Exact);
        Assert.Equal(1000, countAfter.Report!.ValueAt(countAfter.Report.Rows[^1], 0)!.Number);
        Assert.Same(countBefore.Rows, countAfter.Report.Rows);
        sumReads = 0;
        await sums.ComputeAsync(Layout() with { Rows = [new("Desk") { Sort = new(PivotSortDirection.Descending) }] },
            slicing: sumPace, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(0, sumReads);
    }

    [Theory] // ADR-0153: the exact fast path and its rounding/overflow fallback equal the reference
    [InlineData(false)]
    [InlineData(true)]
    public async Task Exact_sum_updates_and_extreme_decimals_keep_reference_arithmetic(bool columns)
    {
        var fields = Fields().Number("Identity", r => r.Id);
        var source = PivotSource.From(Enumerable.Range(0, 100).Select(i => new Trade(i, "Desk" + i % 10, i / 10m)).ToArray(), fields);
        using var session = new PivotComputationSession(source);
        var layout = Layout() with { Columns = columns ? [new("Identity")] : [], Values = [
            ..new[] { PivotAggregation.Sum, PivotAggregation.Average, PivotAggregation.Count }
                .SelectMany(aggregation => Enum.GetValues<PivotShowValuesAs>().Select(mode =>
                    new PivotValueField("Amount", aggregation) { ShowValuesAs = mode, NumberFormat = "G29" }))] };
        var ct = TestContext.Current.CancellationToken;
        await session.ComputeAsync(layout, cancellationToken: ct);
        decimal[] changes = [12.5m, -12.5m, 0.000001m, decimal.MaxValue, -decimal.MaxValue, 0.0000000000000000000000000001m, 7m];
        for (var i = 0; i < 28; i++)
        {
            source.Apply(fields.Batch(changed: [new Trade(i % 7, i % 3 == 0 ? "New desk" : "Desk" + i % 10, changes[i % changes.Length])]));
            var actual = (await session.ComputeAsync(layout, cancellationToken: ct)).Report!;
            var fresh = PivotSource.From(source.Snapshot, fields.Fields);
            var query = PivotQuery.For(layout);
            var expected = PivotEngine.Report(PivotEngine.Cube(query, await fresh.AggregateAsync(query, ct), fresh.Fields), layout);
            Assert.Equal(expected.Rows.Select(row => row.Key), actual.Rows.Select(row => row.Key));
            Assert.Equal(Pivot.Headers(expected), Pivot.Headers(actual));
            for (var row = 0; row < expected.Rows.Count; row++)
                for (var column = 0; column < expected.ValueColumns.Count; column++)
                {
                    var e = expected.ValueAt(expected.Rows[row], column);
                    var v = actual.ValueAt(actual.Rows[row], column);
                    Assert.Equal((e?.Exact, e?.Number, e?.Text, e?.Error), (v?.Exact, v?.Number, v?.Text, v?.Error));
                }
        }
    }

    private sealed record Trade(long Id, string Desk, decimal Amount);
    private static PivotFields<Trade> Fields() => PivotFields.Of<Trade>().Key("Id", r => r.Id).Text("Desk", r => r.Desk).Number("Amount", r => r.Amount);
    private static PivotLayout Layout() => new() { Rows = [new("Desk")], Values = [new("Amount") { NumberFormat = "0.00" }] };

    [Fact] // ADR-0153: a value-only batch changes its report without changing a published version
    public async Task An_incremental_session_keeps_published_reports_and_unchanged_structure()
    {
        var fields = Fields();
        var source = PivotSource.From([new Trade(1, "East", 10), new Trade(2, "West", 20)], fields);
        using var session = new PivotComputationSession(source);
        var first = await session.ComputeAsync(Layout(), cancellationToken: TestContext.Current.CancellationToken);
        source.Apply(fields.Batch(changed: [new Trade(1, "East", 15)]));
        var second = await session.ComputeAsync(Layout(), cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(first.IsRefused);
        Assert.False(second.IsReset);
        Assert.Same(first.Report!.Rows, second.Report!.Rows);
        Assert.Equal("10.00", first.Report.ValueAt(first.Report.Rows[0], 0)!.Text);
        Assert.Equal("15.00", second.Report.ValueAt(second.Report.Rows[0], 0)!.Text);
        Assert.Empty(second.LabelChanges);
    }
}
