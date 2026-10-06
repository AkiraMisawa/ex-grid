using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ExGrid.Selection;
using ExGrid.Summarizing;
using Xunit;

namespace ExGrid.DemoApi.Tests;

/// <summary>
/// The Selection Summary in SQL (ADR-0130, SM-8): the demo server's answer is, question for
/// question, what <see cref="GridSummary.Of{TRow}"/> answers over the same trades.
/// </summary>
public sealed class TradeSummaryTests(DemoApiServer server) : IClassFixture<DemoApiServer>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly string[] Columns =
        ["TradeId", "Region", "Desk", "Book", "Product", "Currency", "TradeDate", "Notional", "Pnl", "Quantity", "Confirmed"];

    private static Func<Trade, object?>? ValueOf(string column) => column switch
    {
        "TradeId" => t => t.TradeId,
        "Region" => t => t.Region,
        "Desk" => t => t.Desk,
        "Book" => t => t.Book,
        "Product" => t => t.Product,
        "Currency" => t => t.Currency,
        "TradeDate" => t => t.TradeDate,
        "Notional" => t => t.Notional,
        "Pnl" => t => t.Pnl,
        "Quantity" => t => t.Quantity,
        "Confirmed" => t => t.Confirmed,
        _ => null,
    };

    private async Task<IReadOnlyList<Trade>> AllTradesAsync()
    {
        var trades = new List<Trade>();
        for (long start = 0; start < server.Store.TradeCount; start += 1_000)
            trades.AddRange((await server.Store.ReadPageAsync(start, 1_000, Token)).Trades);
        return trades;
    }

    [Fact] // ADR-0130 / SM-8: random selections, every figure, the SQL answer equals the reference's
    public async Task ADR0130_the_sql_summary_answers_as_the_reference()
    {
        var trades = await AllTradesAsync();
        var random = new Random(130);
        for (var question = 0; question < 40; question++)
        {
            var ranges = new List<SelectionRange>();
            for (var r = random.Next(1, 4); r > 0; r--)
            {
                var top = random.Next(trades.Count);
                var left = random.Next(Columns.Length);
                ranges.Add(new SelectionRange(top, left, random.Next(1, Math.Min(400, trades.Count - top) + 1),
                    random.Next(1, Columns.Length - left + 1)));
            }
            var request = new GridSummaryRequest { Ranges = ranges, Columns = Columns, RowSequenceVersion = 0, Figures = SummaryFigures.All };

            var expected = GridSummary.Of(trades, request, ValueOf);
            var answered = await server.Store.SummarizeAsync(request, Token);

            foreach (var figure in SummaryFigureOrder.Each)
                Assert.Equal(expected[figure], answered[figure]);
        }
    }

    [Fact] // ADR-0130: the endpoint takes the grid's request and answers each figure
    public async Task ADR0130_the_endpoint_answers_the_figures_asked()
    {
        using var client = server.Factory.CreateClient();
        var request = new TradeSummaryRequest([new TradeSummaryRange(0, 7, 10, 1)], Columns, SummaryFigures.Sum | SummaryFigures.Count);
        var trades = (await server.Store.ReadPageAsync(0, 10, Token)).Trades;

        using var response = await client.PostAsJsonAsync("/api/trades/summary", request, Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Token);
        var figures = body.GetProperty("figures");
        Assert.Equal(trades.Sum(t => t.Notional),
            decimal.Parse(figures.GetProperty("Sum").GetProperty("exact").GetString()!, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("10", figures.GetProperty("Count").GetProperty("exact").GetString());
    }
}
