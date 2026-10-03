using System.Text.RegularExpressions;
using Xunit;

namespace ExGrid.DemoApi.Tests;

public sealed partial class TradeGeneratorTests
{
    // What format version 1 generates: the first 20,000 trades, as TestData.Line writes them.
    private const string FormatVersion1Fingerprint = "70504fdcd0b8e4e0312163864898a9709d50b106a6b7435c10743b1eb6ea9e81";

    [Fact] // ADR-0069: a changed generator moves the format version on, so a stale file is never reused
    public void ADR0069_what_the_format_version_generates_is_pinned()
    {
        var fingerprint = TestData.Fingerprint(20_000);
        Assert.True(TradeDatabase.FormatVersion == 1 && fingerprint == FormatVersion1Fingerprint,
            $"The generated trades changed (fingerprint {fingerprint}). Move TradeDatabase.FormatVersion on, so no "
            + "server reuses a file the old generator wrote, and pin the new version's fingerprint here.");
    }

    [Fact] // ADR-0069: the same count gives the same data, because trade n is a function of n alone
    public void ADR0069_trade_n_is_the_same_whatever_is_generated_before_it()
    {
        var forwards = Enumerable.Range(0, 2_000).Select(n => TradeGenerator.Generate(n)).ToArray();
        var backwards = Enumerable.Range(0, 2_000).Reverse().Select(n => TradeGenerator.Generate(n)).Reverse().ToArray();
        Assert.Equal(forwards, backwards);
        Assert.Equal(forwards[1_234], TradeGenerator.Generate(1_234));
    }

    [Fact] // ADR-0069: a Blank currency, a text with a comma and one with a double quote, where documented
    public void ADR0069_the_special_values_are_where_their_documentation_puts_them()
    {
        Assert.Contains(',', TradeGenerator.CommaBook);
        Assert.Contains('"', TradeGenerator.QuoteBook);
        Assert.Equal(TradeGenerator.CommaBook, TradeGenerator.Generate(3).Book);
        Assert.Equal(TradeGenerator.QuoteBook, TradeGenerator.Generate(5).Book);
        Assert.Null(TradeGenerator.Generate(7).Currency);
        Assert.Equal(TradeGenerator.CommaBook, TradeGenerator.Generate(2_003).Book);
        Assert.Equal(TradeGenerator.QuoteBook, TradeGenerator.Generate(2_005).Book);
        Assert.Null(TradeGenerator.Generate(1_007).Currency);

        var trades = Enumerable.Range(0, 20_000).Select(n => TradeGenerator.Generate(n)).ToArray();
        Assert.Equal(20, trades.Count(t => t.Currency is null));
        Assert.Equal(10, trades.Count(t => t.Book == TradeGenerator.CommaBook));
        Assert.Equal(10, trades.Count(t => t.Book == TradeGenerator.QuoteBook));
        Assert.All(trades.Where(t => t.Book == TradeGenerator.CommaBook), t => Assert.Equal(("Americas", "Credit"), (t.Region, t.Desk)));
        Assert.All(trades.Where(t => t.Book == TradeGenerator.QuoteBook), t => Assert.Equal(("EMEA", "Rates"), (t.Region, t.Desk)));
    }

    [Fact] // ADR-0069: DemoPivotData's vocabulary, with more books, drawn as it draws its figures
    public void ADR0069_every_trade_is_one_DemoPivotData_could_have_made_with_more_books()
    {
        string[] regions = ["Americas", "EMEA", "APAC"];
        string[] products = ["Swap", "Bond", "Option", "Future", "Spot"];
        string[] currencies = ["USD", "EUR", "JPY", "GBP", "CHF"];
        var cities = new Dictionary<string, string>
        {
            ["NY"] = "Americas", ["TOR"] = "Americas", ["CHI"] = "Americas", ["SAO"] = "Americas",
            ["LDN"] = "EMEA", ["FRA"] = "EMEA", ["ZRH"] = "EMEA", ["PAR"] = "EMEA",
            ["TKY"] = "APAC", ["HKG"] = "APAC", ["SGP"] = "APAC", ["SYD"] = "APAC",
        };
        var desks = new Dictionary<string, string> { ["RATES"] = "Rates", ["CREDIT"] = "Credit", ["FX"] = "FX", ["EQ"] = "Equities" };
        var last = new DateOnly(2026, 1, 2).AddDays(269);

        var trades = Enumerable.Range(0, 20_000).Select(n => TradeGenerator.Generate(n)).ToArray();
        for (var n = 0; n < trades.Length; n++)
        {
            var t = trades[n];
            Assert.Equal("T" + (10_000_000 + n), t.TradeId);
            Assert.Contains(t.Region, regions);
            Assert.Contains(t.Product, products);
            if (t.Currency is not null)
                Assert.Contains(t.Currency, currencies);
            if (t.Book != TradeGenerator.CommaBook && t.Book != TradeGenerator.QuoteBook)
            {
                var book = BookName().Match(t.Book);
                Assert.True(book.Success, t.Book);
                Assert.Equal(cities[book.Groups["city"].Value], t.Region);
                Assert.Equal(desks[book.Groups["desk"].Value], t.Desk);
            }
            Assert.InRange(t.TradeDate, new DateOnly(2026, 1, 2), last);
            // DemoPivotData: a notional of 10,000 to 4,990,000 in steps of 10,000, and a P&L of
            // (x − 0.45) × notional / 100 with x in [0, 1).
            Assert.Equal(0, t.NotionalCents % 1_000_000);
            Assert.InRange(t.NotionalCents, 1_000_000, 499_000_000);
            Assert.InRange(t.PnlCents, -45 * t.NotionalCents / 10_000, 55 * t.NotionalCents / 10_000);
            Assert.InRange(t.Quantity, 1, 49);
        }

        // All ten of DemoPivotData's books are among them, and the full spread is used.
        Assert.Superset(
            new HashSet<string>(["NY-RATES-01", "NY-CREDIT-02", "TOR-FX-01", "LDN-RATES-01", "LDN-FX-02",
                "FRA-CREDIT-01", "ZRH-EQ-01", "TKY-RATES-01", "HKG-EQ-01", "SGP-FX-01"]),
            trades.Select(t => t.Book).ToHashSet());
        Assert.Equal(194, trades.Select(t => t.Book).Distinct().Count());
        Assert.Equal(TradeGenerator.TradeDays, trades.Select(t => t.TradeDate).Distinct().Count());
        Assert.InRange(trades.Count(t => t.Confirmed), 17_500, 18_500);
    }

    [Fact] // ADR-0069: the Record Keys' text order is their numeric order, for every count the server takes
    public void ADR0069_the_Record_Keys_sort_as_text_in_the_order_they_were_made()
    {
        long[] numbers = [0, 1, 9, 10, 99_999, 999_999, 1_000_000, 9_999_999, DemoApiOptions.MaxTradeCount - 1L, 89_999_999];
        for (var i = 1; i < numbers.Length; i++)
        {
            var before = TradeGenerator.TradeId(numbers[i - 1]);
            var after = TradeGenerator.TradeId(numbers[i]);
            Assert.True(string.CompareOrdinal(before, after) < 0, $"{before} sorts before {after}");
            Assert.Equal(9, after.Length);
        }
        Assert.Equal(1_234_567, TradeGenerator.Number(TradeGenerator.TradeId(1_234_567)));
    }

    [GeneratedRegex("^(?<city>[A-Z]{2,3})-(?<desk>RATES|CREDIT|FX|EQ)-0[1-4]$")]
    private static partial Regex BookName();
}
