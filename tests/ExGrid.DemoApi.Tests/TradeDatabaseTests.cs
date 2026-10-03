using Microsoft.Data.Sqlite;
using Xunit;

namespace ExGrid.DemoApi.Tests;

public sealed class TradeDatabaseTests
{
    [Fact] // ADR-0069: the file is named by the trade count and a format version
    public void ADR0069_the_file_is_named_by_the_format_version_and_the_count()
    {
        Assert.Equal("trades-v1-20000.sqlite", TradeDatabase.FileName(20_000));
        Assert.Equal("trades-v1-1000000.sqlite", TradeDatabase.FileName(1_000_000));
    }

    [Fact] // ADR-0069: a fixed seed — the same count gives byte-identical data
    public void ADR0069_the_same_count_generates_the_same_bytes()
    {
        using var one = new TempDirectory();
        using var two = new TempDirectory();
        var first = one.File(TradeDatabase.FileName(5_000));
        var second = two.File(TradeDatabase.FileName(5_000));
        TradeDatabase.Generate(first, 5_000, null, TestContext.Current.CancellationToken);
        TradeDatabase.Generate(second, 5_000, null, TestContext.Current.CancellationToken);

        Assert.Equal(TestData.FileHash(first), TestData.FileHash(second));
        using var connection = TradeDatabase.Open(first, SqliteOpenMode.ReadOnly);
        Assert.Equal(TestData.Fingerprint(5_000), TestData.Fingerprint(connection));
    }

    [Fact] // ADR-0069: the same count gives the same aggregates, and they are the generator's
    public void ADR0069_the_same_count_gives_the_same_checksum_of_aggregates()
    {
        using var one = new TempDirectory();
        using var two = new TempDirectory();
        var first = one.File(TradeDatabase.FileName(20_000));
        var second = two.File(TradeDatabase.FileName(20_000));
        TradeDatabase.Generate(first, 20_000, null, TestContext.Current.CancellationToken);
        TradeDatabase.Generate(second, 20_000, null, TestContext.Current.CancellationToken);

        var generated = Enumerable.Range(0, 20_000).Select(n => TradeGenerator.Generate(n)).ToArray();
        var expected = string.Join(";", generated
            .GroupBy(t => t.Region)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => $"{g.Key}:{g.Count()}:{g.Sum(t => t.NotionalCents)}:{g.Sum(t => t.PnlCents)}:{g.Count(t => t.Currency is null)}:{g.Sum(t => t.Quantity)}"));

        Assert.Equal(expected, Aggregates(first));
        Assert.Equal(expected, Aggregates(second));
    }

    [Fact] // ADR-0069: money is stored as integer cents, so the database's own SUM is exact
    public void ADR0069_SUM_of_the_stored_cents_is_the_exact_sum_of_the_generated_amounts()
    {
        using var directory = new TempDirectory();
        var path = directory.File(TradeDatabase.FileName(20_000));
        TradeDatabase.Generate(path, 20_000, null, TestContext.Current.CancellationToken);
        var generated = Enumerable.Range(0, 20_000).Select(n => TradeGenerator.Generate(n)).ToArray();

        using var connection = TradeDatabase.Open(path, SqliteOpenMode.ReadOnly);
        Assert.Equal(0L, Scalar(connection, "SELECT count(*) FROM trades WHERE typeof(Pnl) <> 'integer' OR typeof(Notional) <> 'integer'"));
        Assert.Equal(generated.Sum(t => Cents.ToDecimal(t.PnlCents)), Cents.ToDecimal(Scalar(connection, "SELECT SUM(Pnl) FROM trades")));
        Assert.Equal(generated.Sum(t => Cents.ToDecimal(t.NotionalCents)), Cents.ToDecimal(Scalar(connection, "SELECT SUM(Notional) FROM trades")));

        // And per group, as a pivot's GROUP BY would ask.
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Desk, Product, SUM(Pnl) FROM trades GROUP BY Desk, Product";
        using var reader = command.ExecuteReader();
        var groups = 0;
        while (reader.Read())
        {
            var (desk, product) = (reader.GetString(0), reader.GetString(1));
            var sum = generated.Where(t => t.Desk == desk && t.Product == product).Sum(t => Cents.ToDecimal(t.PnlCents));
            Assert.Equal(sum, Cents.ToDecimal(reader.GetInt64(2)));
            groups++;
        }
        Assert.Equal(20, groups);
    }

    [Fact] // ADR-0064/0069: a Blank is kept apart from every value, the empty string included
    public void ADR0069_a_Blank_currency_is_stored_as_NULL()
    {
        using var directory = new TempDirectory();
        var path = directory.File(TradeDatabase.FileName(3_000));
        TradeDatabase.Generate(path, 3_000, null, TestContext.Current.CancellationToken);

        using var connection = TradeDatabase.Open(path, SqliteOpenMode.ReadOnly);
        Assert.Equal(3L, Scalar(connection, "SELECT count(*) FROM trades WHERE Currency IS NULL"));
        Assert.Equal(0L, Scalar(connection, "SELECT count(*) FROM trades WHERE Currency = ''"));
        Assert.Equal(1L, Scalar(connection, "SELECT count(*) FROM trades WHERE TradeId = 'T10000007' AND Currency IS NULL"));
    }

    [Fact] // ADR-0069: generation shows its progress as it goes
    public void ADR0069_generation_reports_its_progress_up_to_the_whole_count()
    {
        using var directory = new TempDirectory();
        var seen = new List<long>();
        TradeDatabase.Generate(directory.File("progress.sqlite"), 25_000, seen.Add, TestContext.Current.CancellationToken);
        Assert.Equal([10_000, 20_000, 25_000], seen);
    }

    [Fact] // ADR-0069: the version 0 state is the generated data
    public void ADR0069_the_generated_file_holds_version_0_and_what_generated_it()
    {
        using var directory = new TempDirectory();
        var path = directory.File(TradeDatabase.FileName(1_500));
        TradeDatabase.Generate(path, 1_500, null, TestContext.Current.CancellationToken);

        using var connection = TradeDatabase.Open(path, SqliteOpenMode.ReadOnly);
        Assert.Equal(1L, Scalar(connection, "SELECT count(*) FROM changes"));
        Assert.Equal(1_500L, Scalar(connection, "SELECT Trades FROM changes WHERE Version = 0"));
        TradeDatabase.Verify(path, 1_500);
    }

    [Fact] // ADR-0069 and principle 1: a file that does not hold what its name promises is refused, not served
    public void ADR0069_a_file_that_does_not_hold_what_its_name_promises_is_refused()
    {
        using var directory = new TempDirectory();
        var path = directory.File(TradeDatabase.FileName(200));
        TradeDatabase.Generate(path, 100, null, TestContext.Current.CancellationToken);
        var refused = Assert.Throws<InvalidOperationException>(() => TradeDatabase.Verify(path, 200));
        Assert.Contains("Trades = 100", refused.Message);
        Assert.Contains("Delete it", refused.Message);

        var garbage = directory.File(TradeDatabase.FileName(300));
        File.WriteAllText(garbage, "not a database");
        var notDatabase = Assert.Throws<InvalidOperationException>(() => TradeDatabase.Verify(garbage, 300));
        Assert.Contains("is not a generated trades database", notDatabase.Message);
    }

    [Fact] // ADR-0069: a generation that is stopped is stopped
    public void ADR0069_generation_stops_when_cancelled()
    {
        using var directory = new TempDirectory();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            TradeDatabase.Generate(directory.File("cancelled.sqlite"), 50_000, null, cancellation.Token));
    }

    private static string Aggregates(string path)
    {
        using var connection = TradeDatabase.Open(path, SqliteOpenMode.ReadOnly);
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Region, count(*), SUM(Notional), SUM(Pnl), count(*) - count(Currency), SUM(Quantity)
            FROM trades GROUP BY Region ORDER BY Region
            """;
        using var reader = command.ExecuteReader();
        var parts = new List<string>();
        while (reader.Read())
            parts.Add($"{reader.GetString(0)}:{reader.GetInt64(1)}:{reader.GetInt64(2)}:{reader.GetInt64(3)}:{reader.GetInt64(4)}:{reader.GetInt64(5)}");
        return string.Join(";", parts);
    }

    private static long Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)command.ExecuteScalar()!;
    }
}
