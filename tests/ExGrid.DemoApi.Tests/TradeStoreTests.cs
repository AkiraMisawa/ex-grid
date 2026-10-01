using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ExGrid.DemoApi.Tests;

public sealed class TradeStoreTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact] // ADR-0069: generated at first start and reused after; each run serves a copy of its own
    public async Task ADR0069_the_first_start_generates_and_a_later_start_reuses_the_file_untouched()
    {
        using var directory = new TempDirectory();
        var generated = directory.File(TradeDatabase.FileName(2_000));

        string firstVersion;
        await using (var first = await TestData.ReadyStore(directory.Path, 2_000))
        {
            Assert.True(File.Exists(generated));
            Assert.Equal(TradeStoreState.Ready, first.State);
            Assert.Equal(2_000, first.TradeCount);
            Assert.Equal(0, TestData.Counter(first.Version!));
            Assert.StartsWith(Path.Combine(directory.Path, "runs"), first.WorkingPath);
            await first.ApplyLiveChangesAsync(50, Token);
            firstVersion = first.Version!;
        }
        var bytes = TestData.FileHash(generated);
        var written = File.GetLastWriteTimeUtc(generated);

        await using (var second = await TestData.ReadyStore(directory.Path, 2_000))
        {
            // Another run starts from the generated trades again, under a name of its own.
            Assert.Equal(0, TestData.Counter(second.Version!));
            Assert.NotEqual(TestData.Run(firstVersion), TestData.Run(second.Version!));
            using var connection = TradeDatabase.Open(second.WorkingPath!, SqliteOpenMode.ReadOnly);
            Assert.Equal(TestData.Fingerprint(2_000), TestData.Fingerprint(connection));
        }

        Assert.Equal(bytes, TestData.FileHash(generated));
        Assert.Equal(written, File.GetLastWriteTimeUtc(generated));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(directory.Path, "runs")));
    }

    [Fact] // ADR-0066: every answer carries the Source Version of the data it came from
    public async Task ADR0066_a_read_sees_one_state_and_its_version_while_a_live_change_commits()
    {
        using var directory = new TempDirectory();
        await using var store = await TestData.ReadyStore(directory.Path, 3_000);

        var (readVersion, before, change, during) = await store.ReadAsync(async (read, token) =>
        {
            string before, during;
            using (var command = read.Command(""))
                before = TestData.Fingerprint(command);
            var change = await store.ApplyLiveChangesAsync(25, token);
            using (var command = read.Command(""))
                during = TestData.Fingerprint(command);
            return (read.Version, before, change, during);
        }, Token);

        Assert.Equal(before, during);
        Assert.Equal(0, TestData.Counter(readVersion));
        Assert.Equal(1, TestData.Counter(change.Version));
        var (nowVersion, nowTrades) = await TestData.AllTrades(store);
        Assert.Equal(change.Version, nowVersion);
        Assert.Equal(store.Version, nowVersion);
        using var copy = TradeDatabase.Open(store.WorkingPath!, SqliteOpenMode.ReadOnly);
        Assert.NotEqual(before, TestData.Fingerprint(copy));
        Assert.Equal(3_000, nowTrades.Count);
    }

    [Fact] // ADR-0067: a tick commits with the change counter, and names exactly the trades it touched
    public async Task ADR0067_a_live_tick_moves_the_version_on_by_one_and_names_what_it_touched()
    {
        using var directory = new TempDirectory();
        await using var store = await TestData.ReadyStore(directory.Path, 3_000);
        var (_, before) = await TestData.AllTrades(store);

        var change = await store.ApplyLiveChangesAsync(7, Token);

        var (version, after) = await TestData.AllTrades(store);
        Assert.Equal(change.Version, version);
        Assert.Equal(1, TestData.Counter(version));
        Assert.Equal(change.TradeIds.Order(StringComparer.Ordinal), change.TradeIds);
        Assert.Equal(before.Count, after.Count);

        var moved = 0;
        foreach (var id in change.TradeIds)
        {
            if (before.TryGetValue(id, out var was) && after.TryGetValue(id, out var now))
            {
                Assert.NotEqual(was.Pnl, now.Pnl);
                Assert.Equal(was with { Pnl = now.Pnl, Notional = now.Notional }, now);
                moved++;
            }
        }
        Assert.Equal(7, moved);
        foreach (var (id, trade) in before.Where(pair => !change.TradeIds.Contains(pair.Key)))
            Assert.Equal(trade, after[id]);

        var recorded = await store.ReadAsync(async (read, token) =>
        {
            await using var command = read.Command("SELECT TradeIds, Trades FROM changes WHERE Version = 1");
            await using var reader = await command.ExecuteReaderAsync(token);
            Assert.True(await reader.ReadAsync(token));
            return (JsonSerializer.Deserialize<string[]>(reader.GetString(0)), reader.GetInt64(1));
        }, Token);
        Assert.Equal(change.TradeIds, recorded.Item1);
        Assert.Equal(3_000, recorded.Item2);
    }

    [Fact] // ADR-0067: now and then a trade is cancelled and another booked: the one generation would make next
    public async Task ADR0067_a_booking_adds_the_next_generated_trade_and_cancels_another()
    {
        using var directory = new TempDirectory();
        await using var store = await TestData.ReadyStore(directory.Path, 1_000);
        var (_, before) = await TestData.AllTrades(store);
        var booked = TradeGenerator.TradeId(1_000);

        TradeChange? booking = null;
        for (var tick = 0; tick < 200 && booking is null; tick++)
        {
            var change = await store.ApplyLiveChangesAsync(3, Token);
            if (change.TradeIds.Contains(booked))
                booking = change;
        }

        Assert.NotNull(booking);
        var (_, after) = await TestData.AllTrades(store);
        Assert.Equal(1_000, after.Count);
        Assert.Equal(1_000, store.TradeCount);
        var generated = TradeGenerator.Generate(1_000);
        Assert.Equal(TestData.Line(generated), LineOf(after[booked]));
        var cancelled = Assert.Single(booking.TradeIds, id => before.ContainsKey(id) && !after.ContainsKey(id));
        Assert.Equal(5, booking.TradeIds.Length); // three moved, one cancelled, one booked
        Assert.DoesNotContain(cancelled, after.Keys);
    }

    [Fact] // ADR-0069: tick n's numbers come from n, so the same ticks from the same state make the same trades
    public async Task ADR0069_the_same_ticks_from_the_generated_trades_make_the_same_trades()
    {
        using var one = new TempDirectory();
        using var two = new TempDirectory();
        await using var first = await TestData.ReadyStore(one.Path, 2_000);
        await using var second = await TestData.ReadyStore(two.Path, 2_000);

        for (var tick = 0; tick < 40; tick++)
        {
            var a = await first.ApplyLiveChangesAsync(5, Token);
            var b = await second.ApplyLiveChangesAsync(5, Token);
            Assert.Equal(a.TradeIds, b.TradeIds);
        }

        using var firstCopy = TradeDatabase.Open(first.WorkingPath!, SqliteOpenMode.ReadOnly);
        using var secondCopy = TradeDatabase.Open(second.WorkingPath!, SqliteOpenMode.ReadOnly);
        Assert.Equal(TestData.Fingerprint(firstCopy), TestData.Fingerprint(secondCopy));
        Assert.NotEqual(TestData.Fingerprint(2_000), TestData.Fingerprint(firstCopy));
    }

    [Fact] // ADR-0067: every committed change is told, in the order committed
    public async Task ADR0067_every_committed_change_is_told_in_the_order_committed()
    {
        using var directory = new TempDirectory();
        await using var store = await TestData.ReadyStore(directory.Path, 500);

        var committed = new List<TradeChange>();
        for (var tick = 0; tick < 3; tick++)
            committed.Add(await store.ApplyLiveChangesAsync(2, Token));

        var told = new List<TradeChange>();
        for (var tick = 0; tick < 3; tick++)
            told.Add(await store.Changes.ReadAsync(Token));
        Assert.Equal(committed, told, (a, b) => a.Version == b.Version && a.TradeIds.SequenceEqual(b.TradeIds));
        Assert.Equal([1L, 2L, 3L], told.Select(c => TestData.Counter(c.Version)));
    }

    [Fact] // ADR-0069: a page of trades in TradeId order, with the total, fewer at the end
    public async Task ADR0069_a_page_is_in_TradeId_order_with_the_total_and_fewer_at_the_end()
    {
        using var directory = new TempDirectory();
        await using var store = await TestData.ReadyStore(directory.Path, 1_050);

        var first = await store.ReadPageAsync(0, 10, Token);
        Assert.Equal(store.Version, first.Version);
        Assert.Equal(1_050, first.Total);
        Assert.Equal(Enumerable.Range(0, 10).Select(n => TradeGenerator.TradeId(n)), first.Trades.Select(t => t.TradeId));
        Assert.Equal(TradeGenerator.CommaBook, first.Trades[3].Book);
        Assert.Equal(TradeGenerator.QuoteBook, first.Trades[5].Book);
        Assert.Null(first.Trades[7].Currency);
        Assert.All(first.Trades, t => Assert.Equal(TestData.Line(TradeGenerator.Generate(TradeGenerator.Number(t.TradeId))), LineOf(t)));

        var last = await store.ReadPageAsync(1_000, 100, Token);
        Assert.Equal(50, last.Trades.Count);
        Assert.Equal(TradeGenerator.TradeId(1_049), last.Trades[^1].TradeId);
        Assert.Empty((await store.ReadPageAsync(5_000, 100, Token)).Trades);
    }

    [Fact] // ADR-0069: what a server that has stopped left behind is removed; a running server's is not
    public async Task ADR0069_what_a_stopped_server_left_behind_is_removed_and_a_running_ones_is_kept()
    {
        using var directory = new TempDirectory();
        var runs = Directory.CreateDirectory(Path.Combine(directory.Path, "runs")).FullName;
        // No process has this id: Linux stops far below it, and Windows' are multiples of four.
        var stopped = (int.MaxValue - 1).ToString(CultureInfo.InvariantCulture);
        var running = Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
        string[] left =
        [
            Path.Combine(runs, $"trades-v1-100.{stopped}.0123abcd.sqlite"),
            Path.Combine(runs, $"trades-v1-100.{stopped}.0123abcd.sqlite-wal"),
            directory.File($"trades-v1-100.sqlite.{stopped}.0123abcd.tmp"),
        ];
        var kept = Path.Combine(runs, $"trades-v1-100.{running}.89abcdef.sqlite");
        foreach (var file in left.Append(kept))
            await File.WriteAllTextAsync(file, "left behind", Token);

        await using var store = await TestData.ReadyStore(directory.Path, 100);

        Assert.All(left, file => Assert.False(File.Exists(file), file));
        Assert.True(File.Exists(kept));
    }

    [Fact] // ADR-0069: the data directory is never the repository's, unless EXGRID_DEMO_DATA says so
    public void ADR0069_the_count_and_the_directory_come_from_the_environment_and_a_bad_count_is_refused()
    {
        var defaults = DemoApiOptions.From(new ConfigurationBuilder().Build());
        Assert.Equal(1_000_000, defaults.TradeCount);
        Assert.Equal(Path.Combine(Path.GetTempPath(), "exgrid-demo-api"), defaults.DataDirectory);

        var set = DemoApiOptions.From(Configuration(("EXGRID_DEMO_TRADES", "20,000"), ("EXGRID_DEMO_DATA", "/tmp/elsewhere")));
        Assert.Equal(new DemoApiOptions(20_000, Path.GetFullPath("/tmp/elsewhere")), set);

        foreach (var bad in (string[])["many", "0", "-5", "1.5", "50000001"])
        {
            var refused = Assert.Throws<InvalidOperationException>(() => DemoApiOptions.From(Configuration(("EXGRID_DEMO_TRADES", bad))));
            Assert.Contains($"EXGRID_DEMO_TRADES is \"{bad}\"", refused.Message);
        }
    }

    private static IConfiguration Configuration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    [Fact] // ADR-0069 refined: every other change of a tick falls on the first trades, which a blotter opens on, so it sees changes at any trade count
    public async Task ADR0069_half_of_a_ticks_changes_fall_on_the_busy_trades()
    {
        using var directory = new TempDirectory();
        await using var store = await TestData.ReadyStore(directory.Path, 3_000);

        long busy = 0, all = 0;
        for (var tick = 0; tick < 20; tick++)
        {
            var change = await store.ApplyLiveChangesAsync(20, Token);
            foreach (var id in change.TradeIds)
            {
                all++;
                if (long.Parse(id[1..], CultureInfo.InvariantCulture) - TradeGenerator.FirstKeyNumber < TradeStore.HotTrades)
                    busy++;
            }
        }

        // Uniform picks among 3,000 would put about a sixth there.
        Assert.True(busy * 2 >= all, $"{busy} of {all} changes fell on the first {TradeStore.HotTrades} trades");
    }

    private static string LineOf(Trade t) =>
        TestData.Line(t.TradeId, t.Region, t.Desk, t.Book, t.Product, t.Currency, TradeDatabase.FormatDate(t.TradeDate),
            decimal.ToInt64(t.Notional * 100), decimal.ToInt64(t.Pnl * 100), t.Quantity, t.Confirmed);
}
