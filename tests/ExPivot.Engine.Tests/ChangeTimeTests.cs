using Xunit;
using static ExPivot.Engine.Tests.Pivot;

namespace ExPivot.Engine.Tests;

/// <summary>
/// The time a Change Highlight starts is the Consumer's (ADR-0068): a report source marks which
/// cells changed in which Report Version, and the client stamps each change on its own clock when it
/// first adopts a report listing it (<see cref="PivotChangeTimes"/>). A server whose clock is ahead
/// of the browser's, or behind it, changes nothing the user sees.
/// </summary>
public class ChangeTimeTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed record Entry(long Id, string Label, decimal Amount);

    private static readonly PivotLayout Layout = new() { Rows = [P("Label")], Values = [Sum("Amount") with { NumberFormat = "0" }] };

    private static readonly TimeSpan Duration = TimeSpan.FromSeconds(1);

    /// <summary>A clock that moves when the test says.</summary>
    private sealed class SteppedClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public void Advance(TimeSpan by) => _now += by;
        public override DateTimeOffset GetUtcNow() => _now;
    }

    /// <summary>A run against a server whose clock is <paramref name="skew"/> away from the
    /// client's: what the client's stamps say, step by step, as offsets from its own start.</summary>
    private static async Task<List<string>> RunAsync(TimeSpan skew)
    {
        var fields = PivotFields.Of<Entry>().Key("Id", e => e.Id).Text("Label", e => e.Label).Number("Amount", e => e.Amount);
        var entries = Enumerable.Range(0, 6).Select(i => new Entry(i, "L" + i, 10 * i)).ToArray();
        var data = PivotSource.From(entries, fields);
        var start = new DateTimeOffset(2026, 10, 8, 9, 0, 0, TimeSpan.Zero);
        var client = new SteppedClock(start);
        var server = new SteppedClock(start + skew);
        await using var local = PivotReportSource.From(data, timeProvider: server);
        // Over the wire: what the client is handed is what a server's JSON says.
        var remote = PivotReportSource.Fetch(local.Fields, local.Features, local.UpdateMode,
            async (request, ct) => PivotReportJson.Read<PivotReportUpdate>(PivotReportJson.Write(
                await local.WindowAsync(PivotReportJson.Read<PivotReportRequest>(PivotReportJson.Write(request)), ct))));
        var reader = new PivotReportClient(remote);
        var times = new PivotChangeTimes();
        var steps = new List<string>();
        var settings = PivotReportSettings.Invariant with { ChangeHighlightDuration = Duration };

        async Task ReadAsync(string step, int first)
        {
            Assert.True(await reader.ReadAsync(Layout, settings, new(first, 2), cancellationToken: Ct), reader.Refusal?.Message);
            times.Adopt(reader.Current!, client.GetUtcNow(), Duration);
            foreach (var row in reader.Current!.Rows)
            {
                var at = times.ChangedAt(row, 0);
                steps.Add($"{step}: {row.Labels[0].Text} {(at is { } stamped ? (stamped - start).TotalMilliseconds + " ms" : "-")}");
            }
        }
        void Advance(int ms)
        {
            client.Advance(TimeSpan.FromMilliseconds(ms));
            server.Advance(TimeSpan.FromMilliseconds(ms));
        }
        void Change(int id, decimal amount) => data.Apply(fields.Batch(changed: [entries[id] = entries[id] with { Amount = amount }]));

        await ReadAsync("first", 0);
        Advance(3_000);
        Change(0, 1);
        Change(4, 41);
        await ReadAsync("changed", 0);
        Advance(400);
        await ReadAsync("scrolled", 3);
        Advance(400);
        Change(5, 51);
        await ReadAsync("changed again", 4);
        Advance(300);
        await ReadAsync("back", 0);
        Advance(1_000);
        Change(1, 11);
        await ReadAsync("later", 0);
        await ReadAsync("later, scrolled", 4);
        return steps;
    }

    [Theory] // ADR-0068/0153: highlight onset and expiry are the client's: stamped as a change is first adopted, on the client's clock, the same whatever the server's clock says
    [InlineData(-5_000)]
    [InlineData(0)]
    [InlineData(5_000)]
    [InlineData(86_400_000)]
    public async Task Change_times_are_the_clients_whatever_the_servers_clock(int skewMs)
    {
        var steps = await RunAsync(TimeSpan.FromMilliseconds(skewMs));
        var expected = await RunAsync(TimeSpan.Zero);

        Assert.Equal(expected, steps);
        Assert.Equal(
        [
            "first: L0 -", "first: L1 -",
            // Adopted at 3,000 ms on the client's clock: both changes of the version start there.
            "changed: L0 3000 ms", "changed: L1 -",
            // L4 changed in that version, off screen: scrolled to, it shows as of then.
            "scrolled: L3 -", "scrolled: L4 3000 ms",
            "changed again: L4 3000 ms", "changed again: L5 3800 ms",
            // More than a second after it was published, the source no longer marks the first
            // change: its highlight, from 3,000 ms on the client's clock, has ended too.
            "back: L0 -", "back: L1 -",
            "later: L0 -", "later: L1 5100 ms",
            "later, scrolled: L4 -", "later, scrolled: L5 -",
        ], steps);
    }

    [Fact] // ADR-0068: a cell whose change the source lists is stamped once — adopting the same report, or one listing the change again, keeps its first time
    public void A_change_is_stamped_once()
    {
        var mark = new PivotReportVersion("v2");
        var row = new PivotDisplayRow(new(PivotRowRole.Item, -1, [PivotItemKey.Text("A")]), PivotRowRole.Item, -1, true,
            [new("A")], [new(1, 1m, null, "1")], [new("Label", PivotItemKey.Text("A"))], [mark]);
        var metadata = new PivotReportMetadata(new("v3"), "s", "rows", Layout, PivotReportSettings.Invariant, 1,
            [new("label", "Label", "Label")], [new("amount", "Amount", PivotColumnRole.Item, 0, [])], [], 0, ["Amount"])
        { ChangeMarks = [mark] };
        var state = new PivotReportState(metadata, new(0, 1), [row]);
        var times = new PivotChangeTimes();
        var first = new DateTimeOffset(2026, 10, 8, 9, 0, 0, TimeSpan.Zero);

        times.Adopt(state, first, Duration);
        times.Adopt(state, first.AddSeconds(5), Duration);

        Assert.Equal(first, times.ChangedAt(row, 0));
        // Unlisted and its highlight over: forgotten, and with no listing it is never stamped again.
        times.Adopt(state with { Metadata = metadata with { ChangeMarks = [] }, Rows = [] }, first.AddSeconds(6), Duration);
        Assert.Null(times.At(mark));
    }
}
