using ExPivot.Engine;
using Microsoft.Data.Sqlite;
using Xunit;
using static ExGrid.DemoApi.Tests.PivotSources;

namespace ExGrid.DemoApi.Tests;

/// <summary>
/// The server's Pivot Source over trades whose text the generator never writes — one Item stored in
/// two spellings, ASCII and not — held to <c>PivotSource.From</c> over the same trades (ADR-0060,
/// ADR-0066): the <c>GROUP BY</c>'s groups are folded into one leaf, and a Hidden Item or a cell's
/// Item finds every spelling.
/// </summary>
public sealed class PivotFoldingTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact] // ADR-0060/0066: two spellings of one Item are one leaf, one Item and one cell's records, as the engine reads them
    public async Task ADR0060_two_spellings_of_one_Item_are_one_leaf_as_in_the_engine()
    {
        using var directory = new TempDirectory();
        await using var store = await TestData.ReadyStore(directory.Path, 2_000);
        // The first Credit trade spelled CRÉDIT and the others Crédit; the first EMEA trade EMEA and
        // the others emea. The engine labels an Item by its first spelling in the data's order; the
        // SQL source by the first group it reads, in the stored values' order. Placed outermost,
        // and spelled first where they sort first, the two agree here (TradePivotSource says when
        // they would not: the numbers never differ).
        using (var writer = TradeDatabase.Open(store.WorkingPath!, SqliteOpenMode.ReadWrite))
        {
            TradeDatabase.Execute(writer, null, """
                UPDATE trades SET Desk = CASE WHEN TradeId = (SELECT min(TradeId) FROM trades WHERE Desk = 'Credit') THEN 'CRÉDIT' ELSE 'Crédit' END
                WHERE Desk = 'Credit';
                UPDATE trades SET Region = 'emea'
                WHERE Region = 'EMEA' AND TradeId > (SELECT min(TradeId) FROM trades WHERE Region = 'EMEA');
                """);
        }
        var source = new TradePivotSource(store);
        var (version, byId) = await TestData.AllTrades(store);
        var trades = byId.Values.OrderBy(t => t.TradeId, StringComparer.Ordinal).ToArray();
        var reference = PivotSource.From(trades, TradePivotFields.Fields
            .Select(f => new PivotField<Trade>(f.Name, f.Type, Accessors[f.Name], f.Caption, f.Format)).ToArray());
        var referenceVersion = (await reference.AggregateAsync(new PivotQuery(), Token)).SourceVersion;

        PivotQuery[] questions =
        [
            new(rows: [new("Desk")], values: [new("Pnl", PivotParts.Sum | PivotParts.Extremes)]),
            new(rows: [new("Region")], columns: [new("Product")], values: [new("Pnl", PivotParts.Sum), new("Desk", PivotParts.Counts)]),
            new(rows: [new("Desk", [PivotItemKey.Text("crédit")])], columns: [new("Region", [PivotItemKey.Text("Emea")])], values: [new("Pnl", PivotParts.Sum)]),
            new(rows: [new("Product")], filters: [new("Desk", [PivotItemKey.Text("CRÉDIT")]), new("Region", [PivotItemKey.Text("eMEA")])], values: [new("Quantity", PivotParts.Sum)]),
        ];
        foreach (var query in questions)
            SameAnswer(Json(await reference.AggregateAsync(query, Token)), Json(await source.AggregateAsync(query, Token)));
        var folded = await source.AggregateAsync(questions[0], Token);
        Assert.Equal(["Text:CRÉDIT", "Text:Equities", "Text:FX", "Text:Rates"], folded.Rows[0].Items.Select(Show).Order(StringComparer.Ordinal));
        var hidden = await source.AggregateAsync(questions[2], Token);
        Assert.DoesNotContain(hidden.Rows[0].Items, item => item.Value!.StartsWith("Cr", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(["Text:APAC", "Text:Americas"], hidden.Columns[0].Items.Select(Show).Order(StringComparer.Ordinal));

        foreach (var field in (string[])["Desk", "Region"])
        {
            SameItems(
                await reference.ItemsAsync(new PivotItemsQuery(field, referenceVersion), Token),
                await source.ItemsAsync(new PivotItemsQuery(field, version), Token));
        }

        var cell = new PivotFieldItem[] { new("Desk", PivotItemKey.Text("crÉdit")), new("Region", PivotItemKey.Text("Emea")) };
        var details = await source.DetailsAsync(new PivotDetailsQuery(version, cell, count: 1_000), Token);
        SameDetails(await reference.DetailsAsync(new PivotDetailsQuery(referenceVersion, cell, count: 1_000), Token), details);
        Assert.Contains(details.Records, record => (string)record.Values[2]! == "Crédit" && (string)record.Values[1]! == "emea");
    }

    private static PivotAnswer Json(PivotAnswer answer) => PivotJson.ReadAnswer(PivotJson.Write(answer));
}
