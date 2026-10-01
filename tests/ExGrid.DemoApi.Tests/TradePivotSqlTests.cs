using ExPivot.Engine;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ExGrid.DemoApi.Tests;

/// <summary>
/// The conditions the server's Pivot Source writes (ADR-0066, ADR-0069), run against a small
/// table in memory that holds what the generated trades never do — spellings of one Item in other
/// cases, letters outside ASCII — so that each rule is seen to match the engine's, not only to agree
/// with it on the demo's data.
/// </summary>
public sealed class TradePivotSqlTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public TradePivotSqlTests()
    {
        _connection.Open();
        TradeDatabase.AddItemCollation(_connection);
        using var command = _connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE trades (TradeId TEXT NOT NULL PRIMARY KEY, Region TEXT, Currency TEXT, TradeDate TEXT NOT NULL,
                                 Notional INTEGER NOT NULL, Quantity INTEGER NOT NULL, Confirmed INTEGER NOT NULL) STRICT, WITHOUT ROWID;
            INSERT INTO trades VALUES
                ('T1', 'EMEA', 'USD', '2026-01-02', 1000000, 12, 1),
                ('T2', 'emea', 'usd', '2026-01-02', 1000001, 13, 0),
                ('T3', 'Émea', NULL,  '2026-01-03', 123456,  12, 1),
                ('T4', 'ÉMEA', 'EUR', '2026-01-04', -1,      14, 0),
                ('T5', 'APAC', NULL,  '2026-01-04', 0,       12, 1);
            """;
        command.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();

    private static TradePivotField Field(string name) => TradePivotFields.Find(name)!;

    // The trades a condition leaves, in TradeId order.
    private string[] Where(string? condition, SqlParameters parameters)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT TradeId FROM trades" + (condition is null ? "" : " WHERE " + condition) + " ORDER BY TradeId";
        parameters.AddTo(command);
        using var reader = command.ExecuteReader();
        var ids = new List<string>();
        while (reader.Read())
            ids.Add(reader.GetString(0));
        return [.. ids];
    }

    private string[] Keeping(string field, params PivotItemKey[] hidden)
    {
        var parameters = new SqlParameters();
        return Where(TradePivotSql.Keeping(Field(field), hidden, parameters), parameters);
    }

    private string[] Carrying(string field, PivotItemKey item)
    {
        var parameters = new SqlParameters();
        return Where(TradePivotSql.Carrying(Field(field), item, parameters), parameters);
    }

    [Fact] // ADR-0060/0066: hiding a currency keeps every Blank — NOT IN alone would drop them, and the totals would be quietly short
    public void ADR0066_hiding_an_Item_keeps_the_Blanks_and_hiding_the_Blank_drops_only_them()
    {
        Assert.Equal(["T3", "T4", "T5"], Keeping("Currency", PivotItemKey.Text("usd")));
        Assert.Equal(["T1", "T2", "T4"], Keeping("Currency", PivotItemKey.Blank));
        Assert.Equal(["T4"], Keeping("Currency", PivotItemKey.Blank, PivotItemKey.Text("USD")));
        Assert.Equal(["T3", "T5"], Carrying("Currency", PivotItemKey.Blank));

        // What the trap looks like: the Blanks are gone with nothing said.
        Assert.Equal(["T4"], Where("Currency COLLATE NOCASE NOT IN ('usd')", new SqlParameters()));
    }

    [Fact] // ADR-0060: text is told apart ignoring case, as the engine tells it — every letter, not only ASCII's
    public void ADR0060_text_is_compared_as_the_engine_compares_it_ignoring_case()
    {
        // An ASCII Item: NOCASE, which is the engine's comparison for it.
        Assert.Equal(["T1", "T2"], Carrying("Region", PivotItemKey.Text("Emea")));
        Assert.Contains("COLLATE NOCASE", TradePivotSql.Carrying(Field("Region"), PivotItemKey.Text("Emea"), new SqlParameters()));
        // An Item with a letter outside ASCII: the engine's own comparison, which folds É and é.
        Assert.Equal(["T3", "T4"], Carrying("Region", PivotItemKey.Text("éMEA")));
        Assert.Equal(["T1", "T2", "T5"], Keeping("Region", PivotItemKey.Text("éMEA")));
        Assert.Contains("COLLATE " + TradeDatabase.ItemCollation, TradePivotSql.Keeping(Field("Region"), [PivotItemKey.Text("éMEA")], new SqlParameters()));
        // NOCASE alone folds the ASCII letters and not É: it would have found nothing.
        Assert.Empty(Where("Region = 'éMEA' COLLATE NOCASE", new SqlParameters()));
        // And no letter outside ASCII is equal to an ASCII one ignoring case, which is why NOCASE
        // is the engine's comparison wherever the Item, or the stored text, is ASCII.
        for (var c = 0x80; c <= 0xFFFF; c++)
        {
            if (char.IsSurrogate((char)c))
                continue;
            for (var a = 0x20; a < 0x7F; a++)
                Assert.False(string.Equals(((char)c).ToString(), ((char)a).ToString(), StringComparison.OrdinalIgnoreCase), $"U+{c:X4}");
        }
    }

    [Fact] // ADR-0060: an Item no stored value of the field can be matches no trade, and hiding it hides nothing
    public void ADR0060_an_Item_no_stored_value_can_be_matches_nothing()
    {
        Assert.Empty(Carrying("Quantity", PivotItemKey.Text("12")));
        Assert.Empty(Carrying("Notional", PivotItemKey.Number(0.001)));
        Assert.Empty(Carrying("TradeDate", PivotItemKey.Date(new DateTime(2026, 1, 2, 9, 30, 0))));
        Assert.Empty(Carrying("Region", PivotItemKey.Number(1)));
        Assert.Empty(Carrying("Confirmed", PivotItemKey.Error));
        Assert.Null(TradePivotSql.Keeping(Field("Quantity"), [PivotItemKey.Text("12"), PivotItemKey.Error], new SqlParameters()));
        Assert.Equal("0", TradePivotSql.Carrying(Field("Quantity"), PivotItemKey.Text("12"), new SqlParameters()));
    }

    [Fact] // ADR-0060: numbers, dates and Booleans are matched by the value the engine reads
    public void ADR0060_numbers_dates_and_Booleans_are_matched_by_value()
    {
        Assert.Equal(["T1", "T3", "T5"], Carrying("Quantity", PivotItemKey.Number(12)));
        Assert.Equal(["T1"], Carrying("Notional", PivotItemKey.Number(10_000)));
        Assert.Equal(["T2"], Carrying("Notional", PivotItemKey.Number(10_000.01)));
        Assert.Equal(["T3"], Carrying("Notional", PivotItemKey.Number(1_234.56)));
        Assert.Equal(["T4"], Carrying("Notional", PivotItemKey.Number(-0.01)));
        Assert.Equal(["T5"], Carrying("Notional", PivotItemKey.Number(0)));
        Assert.Equal(["T1", "T2"], Carrying("TradeDate", PivotItemKey.Date(new DateTime(2026, 1, 2))));
        Assert.Equal(["T1", "T2", "T3", "T4", "T5"], Carrying("Month", PivotItemKey.Text("2026-01")));
        Assert.Empty(Carrying("Month", PivotItemKey.Text("2026-02")));
        Assert.Equal(["T2", "T4"], Carrying("Confirmed", PivotItemKey.Boolean(false)));
        Assert.Equal(["T2", "T4"], Keeping("Quantity", PivotItemKey.Number(12)));
    }

    [Theory] // ADR-0060: an amount of whole cents is the number Item (double)decimal reads, and nothing else is
    [InlineData(1_234.56, 123_456L)]
    [InlineData(-0.01, -1L)]
    [InlineData(10_000, 1_000_000L)]
    [InlineData(4_990_000, 499_000_000L)]
    [InlineData(0, 0L)]
    [InlineData(0.30000000000000004, null)]
    [InlineData(0.001, null)]
    [InlineData(1e-320, null)]
    [InlineData(9e13, null)]
    [InlineData(double.NaN, null)]
    public void ADR0060_a_number_Item_is_the_cents_whose_amount_it_is(double number, long? cents)
    {
        Assert.Equal(cents, TradeValues.CentsOf(number));
        if (cents is { } whole)
            Assert.Equal(number, (double)Cents.ToDecimal(whole));
    }

    [Fact] // ADR-0069: the trades' text is ASCII, each Item spelled one way — where NOCASE and grouping by the stored value are the engine's
    public void ADR0069_the_generated_text_is_ASCII_with_one_spelling_per_Item()
    {
        var trades = Enumerable.Range(0, 20_000).Select(n => TradeGenerator.Generate(n)).ToArray();
        foreach (var values in new Func<GeneratedTrade, string?>[] { t => t.TradeId, t => t.Region, t => t.Desk, t => t.Book, t => t.Product, t => t.Currency })
        {
            var distinct = trades.Select(values).OfType<string>().Distinct(StringComparer.Ordinal).ToArray();
            Assert.All(distinct, text => Assert.True(System.Text.Ascii.IsValid(text), text));
            Assert.Equal(distinct.Length, distinct.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }
    }
}
