using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ExGrid.DemoApi.Tests;

/// <summary>A data directory of the test's own, deleted afterwards.</summary>
internal sealed class TempDirectory : IDisposable
{
    public TempDirectory() => Directory.CreateDirectory(Path);

    public string Path { get; } = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(), "exgrid-demo-api-tests", Guid.NewGuid().ToString("N"));

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

/// <summary>What the tests compare trades and files by.</summary>
internal static class TestData
{
    /// <summary>A store over <paramref name="count"/> trades in <paramref name="directory"/>, ready.</summary>
    public static async Task<TradeStore> ReadyStore(string directory, int count)
    {
        var store = new TradeStore(new DemoApiOptions(count, directory), NullLogger<TradeStore>.Instance);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        return store;
    }

    /// <summary>One line per trade, every field in the database's order, in a form that tells a
    /// Blank from every text: what "the same data" means in these tests.</summary>
    public static string Line(string tradeId, string region, string desk, string book, string product, string? currency,
        string tradeDate, long notionalCents, long pnlCents, long quantity, bool confirmed) =>
        string.Join('\u001f',
            tradeId, region, desk, book, product, currency ?? "␀ Blank", tradeDate,
            notionalCents.ToString(CultureInfo.InvariantCulture), pnlCents.ToString(CultureInfo.InvariantCulture),
            quantity.ToString(CultureInfo.InvariantCulture), confirmed ? "1" : "0");

    public static string Line(in GeneratedTrade t) =>
        Line(t.TradeId, t.Region, t.Desk, t.Book, t.Product, t.Currency, TradeDatabase.FormatDate(t.TradeDate),
            t.NotionalCents, t.PnlCents, t.Quantity, t.Confirmed);

    /// <summary>The SHA-256 of the first <paramref name="count"/> generated trades' lines.</summary>
    public static string Fingerprint(int count) =>
        Fingerprint(Enumerable.Range(0, count).Select(n => Line(TradeGenerator.Generate(n))));

    /// <summary>The SHA-256 of every row of <c>trades</c>, in <c>TradeId</c> order, as lines.</summary>
    public static string Fingerprint(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        return Fingerprint(command);
    }

    /// <summary>The same, read through <paramref name="command"/> — one inside a <see cref="TradeRead"/>, say.</summary>
    public static string Fingerprint(SqliteCommand command) => Fingerprint(Rows(command));

    /// <summary>The SHA-256 of a file's bytes.</summary>
    public static string FileHash(string path) => Convert.ToHexStringLower(SHA256.HashData(System.IO.File.ReadAllBytes(path)));

    /// <summary>Every row of <c>trades</c> as a line, in <c>TradeId</c> order.</summary>
    private static IEnumerable<string> Rows(SqliteCommand command)
    {
        command.CommandText = $"SELECT {TradeDatabase.TradeColumns} FROM trades ORDER BY TradeId";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            yield return Line(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5), reader.GetString(6),
                reader.GetInt64(7), reader.GetInt64(8), reader.GetInt64(9), reader.GetInt64(10) != 0);
        }
    }

    /// <summary>Every trade a store holds now, by Record Key, with the version they were read at.</summary>
    public static Task<(string Version, Dictionary<string, Trade> Trades)> AllTrades(TradeStore store) =>
        store.ReadAsync(async (read, token) =>
        {
            await using var command = read.Command($"SELECT {TradeDatabase.TradeColumns} FROM trades ORDER BY TradeId");
            await using var reader = await command.ExecuteReaderAsync(token);
            var trades = new Dictionary<string, Trade>(StringComparer.Ordinal);
            while (await reader.ReadAsync(token))
            {
                var trade = Trade.Read(reader);
                trades.Add(trade.TradeId, trade);
            }
            return (read.Version, trades);
        }, TestContext.Current.CancellationToken);

    /// <summary>The change counter in a Source Version: <c>3f2a9c1e-17</c> is 17.</summary>
    public static long Counter(string version) =>
        long.Parse(version.AsSpan(version.LastIndexOf('-') + 1), NumberStyles.None, CultureInfo.InvariantCulture);

    /// <summary>The run's name in a Source Version: <c>3f2a9c1e-17</c> is <c>3f2a9c1e</c>.</summary>
    public static string Run(string version) => version[..version.LastIndexOf('-')];

    private static string Fingerprint(IEnumerable<string> lines)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var line in lines)
            hash.AppendData(Encoding.UTF8.GetBytes(line + "\u001e"));
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}
