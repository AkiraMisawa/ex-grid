using System.Globalization;
using Xunit;
using static ExPivot.Engine.Tests.Pivot;

namespace ExPivot.Engine.Tests;

/// <summary>
/// A cancelled computation costs the next one nothing (ADR-0153; spine 6). ExPivot cancels the
/// question in flight at every gesture — a scroll that needs a new Window among them — so a live
/// update is cancelled at any of its yields. Whichever yield the cancellation lands at, the next
/// update still reads only the records its batches touched, and the Window it shows equals one
/// computed afresh: the cancellation is observed only where the report's state is whole, and what
/// a cancelled request computed but never published is published by the next.
/// <para>
/// The slices end at every look at the clock, and the clock is looked at after every unit of work,
/// so each yield is a point where a cancellation can land. The first run of each case counts its
/// yields; the sweep then cancels at every one of them, and once before the request starts.
/// </para>
/// </summary>
public class CancelledComputationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed record Trade(long Id, string Desk, string Book, decimal Amount);

    private static PivotFields<Trade> TradeFields() => PivotFields.Of<Trade>()
        .Key("Id", t => t.Id)
        .Text("Desk", t => t.Desk)
        .Text("Book", t => t.Book)
        .Number("Amount", t => t.Amount);

    private static Trade[] Trades(int count) => [.. Enumerable.Range(0, count).Select(i => new Trade(i,
        "Desk" + (i % 7).ToString(CultureInfo.InvariantCulture), "Book" + (i % 13).ToString(CultureInfo.InvariantCulture), i % 100))];

    private static readonly PivotLayout ByDesk = new()
    {
        Rows = [P("Desk"), P("Book")],
        // Max is merged again from every member of a total a change touches: work, and yields, in
        // the cube's update; Sum and Count follow by addition.
        Values = [Sum("Amount"), Value("Amount", PivotAggregation.Count), Value("Amount", PivotAggregation.Max)],
    };

    private static readonly PivotReportSettings Settings = PivotReportSettings.Invariant with
    {
        LabelMetrics = new(9, 7, 3, 14, 6, 4),
    };

    private static readonly PivotReportWindow Window = new(0, 40);

    /// <summary>The report's own slicing: every look at the clock ends a slice, and the clock is
    /// looked at after every unit. It counts the rows the computation reads and its yields, and the
    /// yield <see cref="CancelAt"/> cancels the request armed with <see cref="Armed"/>.</summary>
    private sealed class Pace
    {
        public long Read;
        public int Yields;
        public int CancelAt = -1;
        public CancellationTokenSource? Armed;

        public PivotSlicing Slicing => new()
        {
            Budget = TimeSpan.Zero,
            UnitsPerCheck = 1,
            RowsRead = rows => Read += rows,
            Yield = _ =>
            {
                if (++Yields == CancelAt)
                    Armed?.Cancel();
                return ValueTask.CompletedTask;
            },
        };
    }

    /// <summary>One run: a report read, a batch applied and a request for it cancelled at the
    /// <paramref name="cancelAt"/>-th yield (0: before it starts; −1: never), a second batch, and
    /// the update that shows both. Answers the yields the cancelled request made, the rows that
    /// update read, the Window it shows, and that Window computed afresh.</summary>
    private static async Task<(int Yields, long Read, PivotReportState Shown, PivotReportUpdate Fresh, string[] Copy, string[] FreshCopy)> RunAsync(
        int cancelAt, Func<Trade[], Trade[]> first, Func<Trade[], Trade[]> second, PivotLayout? cancelledLayout = null, int count = 2_000)
    {
        var fields = TradeFields();
        var trades = Trades(count);
        var data = PivotSource.From(trades, fields);
        var pace = new Pace();
        await using var source = PivotReportSource.From(data, slicing: pace.Slicing);
        var client = new PivotReportClient(source);
        Assert.True(await client.ReadAsync(ByDesk, Settings, Window, cancellationToken: Ct));

        Apply(first);
        using (var cancel = new CancellationTokenSource())
        {
            pace.Yields = 0;
            pace.Armed = cancel;
            pace.CancelAt = cancelAt;
            if (cancelAt == 0)
                cancel.Cancel();
            try
            {
                await client.ReadAsync(cancelledLayout ?? ByDesk, Settings, Window, cancellationToken: cancel.Token);
            }
            catch (OperationCanceledException) when (cancel.IsCancellationRequested)
            {
            }
            pace.CancelAt = -1;
            pace.Armed = null;
        }
        var yields = pace.Yields;

        Apply(second);
        pace.Read = 0;
        Assert.True(await client.ReadAsync(ByDesk, Settings, Window, cancellationToken: Ct), client.Refusal?.Message);
        var read = pace.Read;
        var shown = client.Current!;

        await using var afresh = PivotReportSource.From(PivotSource.From(data.Snapshot, fields.Fields));
        var fresh = await afresh.WindowAsync(new("fresh", ByDesk, Settings, Window), Ct);
        var copy = await CopyAllAsync(source, shown.Metadata);
        var freshCopy = await CopyAllAsync(afresh, fresh.Metadata!);
        return (yields, read, shown, fresh, copy, freshCopy);

        void Apply(Func<Trade[], Trade[]> batch)
        {
            var changed = batch(trades);
            var added = changed.Where(t => t.Id >= trades.Length).ToArray();
            data.Apply(fields.Batch(added: added, changed: [.. changed.Where(t => t.Id < trades.Length)]));
            foreach (var trade in changed.Where(t => t.Id < trades.Length))
                trades[trade.Id] = trade;
        }
    }

    // Every cell of the whole report at its version — the label cells and the value cells' shown
    // and raw text — as Copy reads it, beyond the Window.
    private static async Task<string[]> CopyAllAsync(PivotReportSource source, PivotReportMetadata metadata)
    {
        string[] columns = [.. metadata.LabelColumns.Select(c => c.Name), .. metadata.ValueColumns.Select(c => c.Name)];
        var copy = await source.CopyAsync(new(metadata.Version, columns, [new(0, 0, metadata.RowCount - 1, columns.Length - 1)]), Ct);
        Assert.Null(copy.Refusal);
        return [.. copy.Blocks[0].Rows.Select(row => string.Join(" | ", row.Select(cell => cell.Text + "/" + cell.Raw)))];
    }

    private static string[] Texts(IEnumerable<PivotDisplayRow> rows) => [.. rows.Select(row =>
        row.Key + " :: " + string.Join(" | ", row.Labels.Select(label => label.Text)) + " :: "
        + string.Join(" | ", row.Values.Select(value => value?.Text)))];

    private static void SameAsFresh((int Yields, long Read, PivotReportState Shown, PivotReportUpdate Fresh, string[] Copy, string[] FreshCopy) run, string when)
    {
        Assert.Equal(Texts(run.Fresh.Rows!), Texts(run.Shown.Rows));
        Assert.Equal(run.Fresh.Metadata!.RowCount, run.Shown.Metadata.RowCount);
        Assert.Equal(run.Fresh.Metadata.LabelWidths, run.Shown.Metadata.LabelWidths);
        Assert.True(run.FreshCopy.SequenceEqual(run.Copy), $"{when}: the whole report differs from a fresh computation");
    }

    // A value changed: one record each.
    private static Trade[] OneValue(Trade[] trades, long id, decimal amount) => [trades[id] with { Amount = amount }];

    [Fact] // ADR-0153 (spine 6): a value-only update cancelled at any yield leaves the next update incremental and equal to a fresh computation
    public async Task A_cancelled_update_leaves_the_next_incremental_wherever_it_was_cancelled()
    {
        var sweep = await RunAsync(-1, t => OneValue(t, 5, 1_000m), t => OneValue(t, 9, -50m));
        Assert.True(sweep.Read <= 1, $"the uncancelled update read {sweep.Read} rows");
        Assert.True(sweep.Yields >= 3, $"{sweep.Yields} yields: the update was not sliced");
        SameAsFresh(sweep, "uncancelled");
        for (var cancelAt = 0; cancelAt <= sweep.Yields + 1; cancelAt++)
        {
            var run = await RunAsync(cancelAt, t => OneValue(t, 5, 1_000m), t => OneValue(t, 9, -50m));
            // Both batches' records, at most: the one the cancelled request may not have folded,
            // and the one after it. Never the 2,000 a fresh computation reads.
            Assert.True(run.Read <= 2, $"cancelled at yield {cancelAt}: the next update read {run.Read} rows");
            SameAsFresh(run, $"cancelled at yield {cancelAt}");
        }
    }

    // A record moved to an Item no other has: a row appears, labels change, the row sequence moves.
    private static Trade[] NewDesk(Trade[] trades) => [trades[3] with { Desk = "Desk New", Amount = 7m }];

    // A record added under another new Item, and one changed under an old one.
    private static Trade[] NewBook(Trade[] trades) => [new Trade(trades.Length, "Desk2", "Book New", 11m), trades[12] with { Amount = 3m }];

    [Fact] // ADR-0153 (spine 6): a structural update cancelled at any yield — rows appear, labels and the row sequence move — leaves the next incremental, with its labels, widths and row sequence equal to a fresh computation
    public async Task A_cancelled_structural_update_leaves_the_next_incremental_wherever_it_was_cancelled()
    {
        var sweep = await RunAsync(-1, NewDesk, NewBook);
        Assert.True(sweep.Read <= 3, $"the uncancelled update read {sweep.Read} rows");
        SameAsFresh(sweep, "uncancelled");
        for (var cancelAt = 0; cancelAt <= sweep.Yields + 1; cancelAt++)
        {
            var run = await RunAsync(cancelAt, NewDesk, NewBook);
            Assert.True(run.Read <= 3, $"cancelled at yield {cancelAt}: the next update read {run.Read} rows");
            SameAsFresh(run, $"cancelled at yield {cancelAt}");
            Assert.Contains(run.Shown.Rows, row => row.Labels[0].Text == "Desk New");
        }
    }

    [Fact] // ADR-0153 (spine 6): an update cancelled while it lays out a new layout — the batch folded in place, the layout built aside — leaves the next update of either layout incremental and equal to a fresh computation
    public async Task A_cancelled_update_under_a_new_layout_leaves_the_next_incremental()
    {
        var sorted = ByDesk with { Rows = [P("Desk") with { Sort = PivotSort.Descending }, P("Book")] };
        var sweep = await RunAsync(-1, t => OneValue(t, 5, 1_000m), t => OneValue(t, 9, -50m), sorted);
        SameAsFresh(sweep, "uncancelled");
        for (var cancelAt = 0; cancelAt <= sweep.Yields + 1; cancelAt++)
        {
            var run = await RunAsync(cancelAt, t => OneValue(t, 5, 1_000m), t => OneValue(t, 9, -50m), sorted);
            Assert.True(run.Read <= 2, $"cancelled at yield {cancelAt}: the next update read {run.Read} rows");
            SameAsFresh(run, $"cancelled at yield {cancelAt}");
        }
    }

    [Fact] // ADR-0153: the reviewer's measure — 100,000 records, a one-record update reads one row, and after an update cancelled mid-computation the next one-record update still reads one or two, not the 100,000 a fresh pass reads
    public async Task At_100000_records_a_cancelled_update_does_not_cost_a_fresh_pass()
    {
        var uncancelled = await RunAsync(-1, t => OneValue(t, 5, 1_000m), t => OneValue(t, 9, -50m), count: 100_000);
        Assert.True(uncancelled.Read <= 1, $"{uncancelled.Read} rows");
        foreach (var cancelAt in new[] { 0, 1, uncancelled.Yields / 2, uncancelled.Yields })
        {
            var run = await RunAsync(cancelAt, t => OneValue(t, 5, 1_000m), t => OneValue(t, 9, -50m), count: 100_000);
            Assert.True(run.Read <= 2, $"cancelled at yield {cancelAt}: the next update read {run.Read} rows");
            SameAsFresh(run, $"cancelled at yield {cancelAt}");
        }
    }

    [Theory] // ADR-0153/0068 (spine 6): a layout gesture lays out the data the client shows, whether or not the update it superseded was computed — so the change it did not show is marked by the next update, wherever the cancellation landed
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(int.MaxValue)]
    public async Task A_layout_gesture_after_a_discarded_update_shows_the_data_the_client_holds(int cancelAt)
    {
        var fields = TradeFields();
        var trades = Trades(500);
        var data = PivotSource.From(trades, fields);
        var pace = new Pace();
        await using var source = PivotReportSource.From(data, slicing: pace.Slicing);
        var client = new PivotReportClient(source);
        var layout = new PivotLayout { Rows = [P("Desk")], Values = [Sum("Amount")] };
        Assert.True(await client.ReadAsync(layout, Settings, Window, cancellationToken: Ct));
        var shownBefore = Texts(client.Current!.Rows);

        // An update the client discards: cancelled at a yield, or (int.MaxValue) computed and
        // published whole before the client gave it up.
        data.Apply(fields.Batch(changed: [trades[0] with { Amount = 5_000m }]));
        using (var cancel = new CancellationTokenSource())
        {
            pace.Yields = 0;
            pace.Armed = cancel;
            pace.CancelAt = cancelAt;
            if (cancelAt == 0)
                cancel.Cancel();
            var request = new PivotReportRequest("live", layout, Settings, Window, client.Current!.Metadata.Version);
            try
            {
                await source.WindowAsync(request, cancel.Token);
            }
            catch (OperationCanceledException) when (cancel.IsCancellationRequested)
            {
            }
            pace.CancelAt = -1;
            pace.Armed = null;
        }

        // The gesture: a sort, which marks nothing. It shows the data the client holds.
        var sorted = layout with { Rows = [P("Desk") with { Sort = PivotSort.Descending }] };
        Assert.True(await client.ReadAsync(sorted, Settings, Window, cancellationToken: Ct, markChanges: false));
        Assert.Equal(Enumerable.Reverse(shownBefore).Skip(1).Concat(shownBefore[^1..]).Select(Values),
            Texts(client.Current!.Rows).Select(Values));
        Assert.All(client.Current.Rows, row => Assert.All(row.ChangedAt, mark => Assert.Null(mark)));

        // The next update shows the change, and marks the cells it moved.
        Assert.True(await client.ReadAsync(sorted, Settings, Window, cancellationToken: Ct));
        var desk0 = client.Current!.Rows.Single(row => row.Labels[0].Text == "Desk0");
        Assert.NotNull(desk0.ChangedAt[0]);
        Assert.NotNull(client.Current.Rows[^1].ChangedAt[0]);
        Assert.All(client.Current.Rows.Where(row => row.Labels[0].Text is not ("Desk0" or "Grand Total")),
            row => Assert.Null(row.ChangedAt[0]));

        static string Values(string text) => text[(text.LastIndexOf("::", StringComparison.Ordinal) + 2)..];
    }
}
