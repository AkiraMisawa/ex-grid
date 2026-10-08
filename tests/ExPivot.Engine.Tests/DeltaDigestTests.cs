using Xunit;
using static ExPivot.Engine.Tests.Pivot;

namespace ExPivot.Engine.Tests;

/// <summary>
/// A delta is verified, not trusted (ADR-0152): it names the digest of the Window it produces, the
/// client computes the digest of what the delta makes of the Window it holds, and a delta whose
/// result differs — one that left out a subtotal's or a grand total's change, say — or that names no
/// digest is discarded before anything of it becomes current: a complete Window is asked for in its
/// place, and if that cannot be had the last complete report stays, as a Stale Report.
/// </summary>
public class DeltaDigestTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact] // ADR-0152: the digest's hash is the 64-bit FNV-1a, pinned by its published test vectors
    public void The_hash_is_fnv1a_64()
    {
        Assert.Equal(0xcbf29ce484222325UL, PivotReportDigest.Fnv1a([]));
        Assert.Equal(0xaf63dc4c8601ec8cUL, PivotReportDigest.Fnv1a("a"u8));
        Assert.Equal(0x85944171f73967e8UL, PivotReportDigest.Fnv1a("foobar"u8));
    }

    private static PivotReportMetadata FixedMetadata() => new(new("0123456789abcdef0123456789abcdef"), "source:7", "rows-1",
        new PivotLayout { Rows = [P("Region"), P("Product")], Values = [Sum("Amount")] }, PivotReportSettings.Invariant, 120,
        [new("row-labels", "Row Labels", null)], [new("v0", "Sum of Amount", PivotColumnRole.Item, 0, [])], [], 0, ["Sum of Amount"]);

    private static PivotDisplayRow[] FixedRows() =>
    [
        new(new(PivotRowRole.Group, -1, [PivotItemKey.Text("東京")]), PivotRowRole.Group, -1, true,
            [new("東京", 0, new PivotToggle("Region", PivotItemKey.Text("東京"), "東京", false))],
            [new(1234.5, 1234.5m, null, "1,234.50")], [new("Region", PivotItemKey.Text("東京"))]),
        new(new(PivotRowRole.Item, -1, [PivotItemKey.Text("東京"), PivotItemKey.Blank]), PivotRowRole.Item, -1, true,
            [new(null, 1)], [null], [new("Region", PivotItemKey.Text("東京")), new("Product", PivotItemKey.Blank)]),
        new(new(PivotRowRole.GrandTotal, -1, []), PivotRowRole.GrandTotal, -1, true,
            [new("Grand Total")], [new(0, null, "#DIV/0!", "#DIV/0!")], []),
    ];

    [Fact] // ADR-0152: a fixed Window always has the same digest — computed from no process-local hash — so a server and a browser agree on it
    public void A_fixed_window_has_a_pinned_digest()
    {
        var digest = PivotReportDigest.Of(FixedMetadata(), 3, FixedRows());

        // Computed by a separate implementation of the documented serialization as well.
        Assert.Equal("99091a4847daf0d3", digest);
        Assert.Equal(16, digest.Length);
        // Every part counts: the start, the extent, a label, a value's text.
        Assert.NotEqual(digest, PivotReportDigest.Of(FixedMetadata(), 4, FixedRows()));
        Assert.NotEqual(digest, PivotReportDigest.Of(FixedMetadata() with { RowCount = 121 }, 3, FixedRows()));
        var rows = FixedRows();
        rows[2] = new(rows[2].Key, rows[2].Role, -1, true, [new("Grand total")], rows[2].Values, rows[2].RowPath);
        Assert.NotEqual(digest, PivotReportDigest.Of(FixedMetadata(), 3, rows));
        rows = FixedRows();
        rows[0] = new(rows[0].Key, rows[0].Role, -1, true, rows[0].Labels, [new(1234.5, 1234.5m, null, "1,234.5")], rows[0].RowPath);
        Assert.NotEqual(digest, PivotReportDigest.Of(FixedMetadata(), 3, rows));
        // A change mark is not part of it.
        rows = FixedRows();
        rows[0] = new(rows[0].Key, rows[0].Role, -1, true, rows[0].Labels, rows[0].Values, rows[0].RowPath, [new PivotReportVersion("m")]);
        Assert.Equal(digest, PivotReportDigest.Of(FixedMetadata(), 3, rows));
    }

    private sealed record Trade(long Id, string Region, string Desk, decimal Amount);

    private static readonly PivotLayout ByRegionAndDesk = new()
    {
        Rows = [P("Region"), P("Desk")],
        Values = [Sum("Amount"), Sum("Amount") with { ShowValuesAs = PivotShowValuesAs.PercentOfGrandTotal }],
    };

    /// <summary>A server's report, and a relay to it over JSON whose delta can be made wrong, as a
    /// transport that coalesces or rebuilds deltas could make it. Each request it relays, and each
    /// answer it passes on, is kept.</summary>
    private sealed class Relayed : IAsyncDisposable
    {
        private readonly PivotFields<Trade> _fields = PivotFields.Of<Trade>().Key("Id", t => t.Id)
            .Text("Region", t => t.Region).Text("Desk", t => t.Desk).Number("Amount", t => t.Amount);
        private readonly Trade[] _trades =
        [
            new(1, "East", "Credit", 10m), new(2, "East", "Rates", 20m), new(3, "West", "Credit", 30m),
            new(4, "West", "Rates", 40m), new(5, "North", "FX", 50m),
        ];
        private readonly SnapshotPivotSource _data;
        private readonly LocalPivotReportSource _server;

        public Relayed(Func<PivotReportUpdate, PivotReportUpdate>? delta = null, Func<PivotReportRequest, PivotReportUpdate?>? recovery = null)
        {
            _data = PivotSource.From(_trades, _fields);
            _server = PivotReportSource.From(_data);
            Source = PivotReportSource.Fetch(_server.Fields, _server.Features, _server.UpdateMode, async (request, ct) =>
            {
                Asked.Add(request);
                if (request.Baseline is null && Asked.Count > 1 && recovery?.Invoke(request) is { } refused)
                    return refused;
                var update = Wire(await _server.WindowAsync(Wire(request), ct));
                if (update.Rows is null && delta is not null)
                    update = Wire(delta(update));
                Answers.Add(update);
                return update;
            });
        }

        public FetchingPivotReportSource Source { get; }
        public List<PivotReportRequest> Asked { get; } = [];
        public List<PivotReportUpdate> Answers { get; } = [];

        public void Change(long id, decimal amount)
            => _data.Apply(_fields.Batch(changed: [_trades[id - 1] = _trades[id - 1] with { Amount = amount }]));

        /// <summary>The Window a fresh computation of the server's current data shows.</summary>
        public async Task<string[]> FreshAsync(PivotReportWindow window)
        {
            await using var fresh = PivotReportSource.From(PivotSource.From(_data.Snapshot, _fields.Fields));
            return Texts((await fresh.WindowAsync(new("fresh", ByRegionAndDesk, PivotReportSettings.Invariant, window), Ct)).Rows!);
        }

        public ValueTask DisposeAsync() => _server.DisposeAsync();

        private static T Wire<T>(T value) => PivotReportJson.Read<T>(PivotReportJson.Write(value));
    }

    private static string[] Texts(IEnumerable<PivotDisplayRow> rows) => [.. rows.Select(row =>
        string.Join(" | ", row.Labels.Select(label => label.Text)) + " :: " + string.Join(" | ", row.Values.Select(value => value?.Text)))];

    private static readonly PivotReportWindow Window = new(0, 20);

    [Theory] // ADR-0152: a delta that leaves out a subtotal's, a grand total's or a percentage's change is detected by its digest, discarded unseen, and a complete Window shown in its place, equal to a fresh computation
    [InlineData(PivotRowRole.Group)]
    [InlineData(PivotRowRole.GrandTotal)]
    [InlineData(PivotRowRole.Item)]
    public async Task A_delta_missing_a_rows_change_is_detected_and_recovered(PivotRowRole dropped)
    {
        // East / Credit changes: its row, East's subtotal and the grand total change, and every
        // row's percentage of the grand total with them.
        await using var relayed = new Relayed(delta: update => update with
        {
            Changes = [.. update.Changes.Where(change => change.Row.Role != dropped || change.Row.Labels[0].Text is "Rates")],
        });
        var client = new PivotReportClient(relayed.Source);
        Assert.True(await client.ReadAsync(ByRegionAndDesk, PivotReportSettings.Invariant, Window, cancellationToken: Ct));
        var before = client.Current;

        relayed.Change(1, 15m);
        var adopted = await client.ReadAsync(ByRegionAndDesk, PivotReportSettings.Invariant, Window, cancellationToken: Ct);

        Assert.True(adopted, client.Refusal?.Message);
        // The delta, then the complete Window asked for in its place.
        Assert.Equal(3, relayed.Asked.Count);
        Assert.NotNull(relayed.Asked[1].Baseline);
        Assert.Null(relayed.Answers[1].Rows);
        Assert.Null(relayed.Asked[2].Baseline);
        Assert.NotNull(relayed.Answers[2].Rows);
        Assert.NotSame(before, client.Current);
        Assert.Equal(await relayed.FreshAsync(Window), Texts(client.Current!.Rows));
        Assert.Null(client.Refusal);
    }

    [Fact] // ADR-0152: a delta that names no digest is not applied: a complete Window is asked for in its place
    public async Task A_delta_with_no_digest_is_recovered()
    {
        await using var relayed = new Relayed(delta: update => update with { WindowDigest = null });
        var client = new PivotReportClient(relayed.Source);
        Assert.True(await client.ReadAsync(ByRegionAndDesk, PivotReportSettings.Invariant, Window, cancellationToken: Ct));

        relayed.Change(3, 31m);

        Assert.True(await client.ReadAsync(ByRegionAndDesk, PivotReportSettings.Invariant, Window, cancellationToken: Ct), client.Refusal?.Message);
        Assert.Equal(3, relayed.Asked.Count);
        Assert.Null(relayed.Asked[2].Baseline);
        Assert.Equal(await relayed.FreshAsync(Window), Texts(client.Current!.Rows));
    }

    [Fact] // ADR-0152: a complete delta is adopted as it is, with no further request
    public async Task A_correct_delta_is_adopted_with_no_extra_request()
    {
        await using var relayed = new Relayed();
        var client = new PivotReportClient(relayed.Source);
        Assert.True(await client.ReadAsync(ByRegionAndDesk, PivotReportSettings.Invariant, Window, cancellationToken: Ct));
        var before = client.Current!;

        relayed.Change(5, 55m);

        Assert.True(await client.ReadAsync(ByRegionAndDesk, PivotReportSettings.Invariant, Window, cancellationToken: Ct));
        Assert.Equal(2, relayed.Asked.Count);
        Assert.Null(relayed.Answers[1].Rows);
        Assert.NotNull(relayed.Answers[1].WindowDigest);
        Assert.Equal(await relayed.FreshAsync(Window), Texts(client.Current!.Rows));
        Assert.NotSame(before, client.Current);
    }

    [Fact] // ADR-0152/0067: a delta that fails its digest, and a complete Window that cannot be had, leave the last complete report current, as a Stale Report — nothing of the delta shown
    public async Task A_failed_recovery_leaves_a_stale_report()
    {
        await using var relayed = new Relayed(
            delta: update => update with { Changes = [.. update.Changes.Where(change => change.Row.Role != PivotRowRole.GrandTotal)] },
            recovery: request => PivotReportUpdate.Refused(request, new(PivotReportRefusalKind.SourceRefused, "The server is restarting.")));
        var client = new PivotReportClient(relayed.Source);
        Assert.True(await client.ReadAsync(ByRegionAndDesk, PivotReportSettings.Invariant, Window, cancellationToken: Ct));
        var before = client.Current;
        var shown = Texts(before!.Rows);

        relayed.Change(2, 99m);

        Assert.False(await client.ReadAsync(ByRegionAndDesk, PivotReportSettings.Invariant, Window, cancellationToken: Ct));
        Assert.Same(before, client.Current);
        Assert.Equal(shown, Texts(client.Current!.Rows));
        Assert.Equal(PivotReportRefusalKind.StaleReport, client.Refusal!.Kind);
        Assert.Equal(3, relayed.Asked.Count);
    }

    [Fact] // ADR-0152: the digest crosses JSON unchanged, and the Window read back has the digest it names
    public async Task The_digest_survives_a_json_round_trip()
    {
        await using var relayed = new Relayed();
        var client = new PivotReportClient(relayed.Source);
        Assert.True(await client.ReadAsync(ByRegionAndDesk, PivotReportSettings.Invariant, Window, cancellationToken: Ct));
        relayed.Change(4, 41m);
        Assert.True(await client.ReadAsync(ByRegionAndDesk, PivotReportSettings.Invariant, Window, cancellationToken: Ct));

        foreach (var answer in relayed.Answers)
        {
            var read = PivotReportJson.Read<PivotReportUpdate>(PivotReportJson.Write(answer));
            Assert.NotNull(answer.WindowDigest);
            Assert.Equal(answer.WindowDigest, read.WindowDigest);
            if (read.Rows is { } rows)
                Assert.Equal(read.WindowDigest, PivotReportDigest.Of(read.Metadata!, read.Window.Start, rows));
        }
        Assert.Equal(relayed.Answers[1].WindowDigest, PivotReportDigest.Of(client.Current!.Metadata, Window.Start, client.Current.Rows));
    }
}
