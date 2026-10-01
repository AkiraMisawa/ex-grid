using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ExPivot.Engine;
using Xunit;
using static ExGrid.DemoApi.Tests.PivotSources;

namespace ExGrid.DemoApi.Tests;

/// <summary>
/// PV-22: the demo's SQL source answers as <c>PivotSource.From</c> does over the same trades
/// (ADR-0065, ADR-0068) — the Leaf Aggregates, a field's Items and the records behind a cell,
/// question for question, through <c>PivotJson</c> both ways — and refuses what it refuses, in the
/// same words.
/// </summary>
public sealed class PivotSourceTests(PivotApiServer server) : IClassFixture<PivotApiServer>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static PivotQueryField F(string field, params PivotItemKey[] hidden) => new(field, hidden);

    private static PivotQueryValue V(string field, PivotParts parts = PivotParts.Sum | PivotParts.Extremes) => new(field, parts);

    private static PivotItemKey T(string text) => PivotItemKey.Text(text);

    private static PivotItemKey Day(int month, int day) => PivotItemKey.Date(new DateTime(2026, month, day));

    /// <summary>Layouts that between them reach every field, every kind of Item, a Blank, Hidden
    /// Items in each Area — spelled in another case, of another kind, or naming no value at all —
    /// every part the source offers, and the question with no rows or columns.</summary>
    public static TheoryData<string, PivotQuery> Questions => new()
    {
        { "one row field", new(rows: [F("Region")], values: [V("Pnl", PivotParts.Sum)]) },
        {
            "two row fields and a column field",
            new(rows: [F("Region"), F("Desk")], columns: [F("Product")],
                values: [V("Pnl"), V("Notional", PivotParts.Sum), V("Quantity", PivotParts.Counts)])
        },
        { "a column field with a Blank among its Items", new(columns: [F("Currency")], values: [V("Notional")]) },
        {
            "no row or column field: the grand total alone",
            new(values: [V("Pnl"), V("TradeId", PivotParts.Counts), V("Currency", PivotParts.Counts)])
        },
        {
            "Hidden Items in Rows and Columns: a Blank, and Items spelled in another case",
            new(rows: [F("Currency", PivotItemKey.Blank, T("usd"))], columns: [F("Desk", T("fx"))], values: [V("Pnl", PivotParts.Sum)])
        },
        {
            "Filters fields that hide: a Blank, a case variant, and text with a comma and quotes",
            new(rows: [F("Region")],
                filters: [F("Currency", PivotItemKey.Blank, T("Eur")), F("Book", T("ny-credit, RUN-OFF"), T(TradeGenerator.QuoteBook))],
                values: [V("Pnl")])
        },
        { "date Items by Boolean Items", new(rows: [F("TradeDate")], columns: [F("Confirmed")], values: [V("Quantity")]) },
        {
            "the month computed in SQL, with dates hidden in Filters",
            new(rows: [F("Month")], filters: [F("TradeDate", Day(1, 2), Day(2, 3))], values: [V("Pnl", PivotParts.Sum), V("Notional", PivotParts.Extremes)])
        },
        {
            "number Items, hiding a number and a text Item of the number field, which hides nothing",
            new(rows: [F("Quantity", PivotItemKey.Number(12), T("13"))], values: [V("Notional", PivotParts.Extremes)])
        },
        {
            "money as Items, hiding an amount and a number no whole cent is",
            new(rows: [F("Notional", PivotItemKey.Number(10_000), PivotItemKey.Number(0.001))], values: [V("Pnl", PivotParts.Sum)])
        },
        {
            "Values over fields that hold no numbers: counted, summing to 0",
            new(rows: [F("Desk")], values: [V("Region"), V("TradeDate", PivotParts.Counts), V("Confirmed", PivotParts.Sum), V("Month", PivotParts.Extremes)])
        },
        { "every book by every month", new(rows: [F("Book")], columns: [F("Month")], values: [V("Pnl", PivotParts.Sum)]) },
        {
            "a Boolean Item hidden, and a date hidden that has a time of day, which hides nothing",
            new(rows: [F("Confirmed", PivotItemKey.Boolean(false))], columns: [F("Region")],
                filters: [F("TradeDate", PivotItemKey.Date(new DateTime(2026, 1, 2, 13, 45, 0)))], values: [V("Pnl")])
        },
        { "every Item of the row field hidden: no leaves", new(rows: [F("Region", T("americas"), T("EMEA"), T("apac"))], values: [V("Pnl")]) },
        { "a Filters field alone", new(filters: [F("Currency", T("chf"))], values: [V("Quantity")]) },
        { "an Item with a letter outside ASCII, which no trade has", new(rows: [F("Region", T("Émea"))], values: [V("Pnl", PivotParts.Sum)]) },
    };

    [Theory] // ADR-0065, PV-22: the SQL source's Leaf Aggregates are PivotSource.From's, question for question
    [MemberData(nameof(Questions))]
    public async Task ADR0065_PV22_the_SQL_source_answers_as_PivotSource_From(string layout, PivotQuery query)
    {
        using var client = server.Factory.CreateClient();
        var sources = await PivotSources.Over(server, client);

        var answer = await sources.Sql.AggregateAsync(query, Token);

        Assert.False(answer.IsRefused, $"{layout}: {answer.Refusal?.Message}");
        Assert.Equal(sources.Version, answer.SourceVersion);
        SameAnswer(await sources.ReferenceAnswer(query), answer);
    }

    [Fact] // ADR-0065, PV-22: a layout's report computed from the SQL source's parts is the reference's, every offered Aggregation
    public async Task ADR0065_PV22_every_offered_Aggregation_lays_out_the_same_report()
    {
        using var client = server.Factory.CreateClient();
        var sources = await PivotSources.Over(server, client);
        var layout = new PivotLayout
        {
            Rows = [new PivotFieldPlacement("Region"), new PivotFieldPlacement("Desk")],
            Columns = [new PivotFieldPlacement("Product")],
            Filters = [new PivotFieldPlacement("Currency") { HiddenItems = [PivotItemKey.Blank, T("jpy")] }],
            Values =
            [
                new PivotValueField("Pnl", PivotAggregation.Sum),
                new PivotValueField("Pnl", PivotAggregation.Count),
                new PivotValueField("Pnl", PivotAggregation.CountNumbers),
                new PivotValueField("Pnl", PivotAggregation.Average),
                new PivotValueField("Pnl", PivotAggregation.Min),
                new PivotValueField("Pnl", PivotAggregation.Max),
                new PivotValueField("Quantity", PivotAggregation.Average),
                new PivotValueField("Book", PivotAggregation.Count),
                new PivotValueField("Book", PivotAggregation.Average),
            ],
        };
        var query = PivotQuery.For(layout);

        var sql = Report(query, await sources.Sql.AggregateAsync(query, Token), sources.Sql.Fields, layout);
        var reference = Report(query, await sources.ReferenceAnswer(query), sources.Reference.Fields, layout);

        Assert.Equal(Cells(reference), Cells(sql));
        Assert.Contains(Cells(sql), cell => cell.Contains("#DIV/0!", StringComparison.Ordinal)); // the Average of text
    }

    [Fact] // ADR-0062/0065, PV-22: the records behind a report's cells, a page at a time, are the reference's, and add up to the cell
    public async Task ADR0065_PV22_the_records_behind_a_cell_are_the_reference_records_and_add_up_to_it()
    {
        using var client = server.Factory.CreateClient();
        var sources = await PivotSources.Over(server, client);
        var layout = new PivotLayout
        {
            Rows = [new PivotFieldPlacement("Region"), new PivotFieldPlacement("Desk")],
            Columns = [new PivotFieldPlacement("Currency") { HiddenItems = [T("gbp")] }],
            Filters = [new PivotFieldPlacement("Book") { HiddenItems = [T(TradeGenerator.CommaBook.ToUpperInvariant())] }],
            Values = [new PivotValueField("Pnl", PivotAggregation.Sum)],
        };
        var query = PivotQuery.For(layout);
        var report = Report(query, await sources.Sql.AggregateAsync(query, Token), sources.Sql.Fields, layout);

        var cells = 0;
        foreach (var row in report.Rows.Where(row => row.CarriesValues))
        {
            for (var column = 0; column < report.ValueColumns.Count; column += 3)
            {
                if (row.ValueAt(column) is not { } value)
                    continue;
                // The whole cell — one page holds it, under the server's cap on a page — then a
                // page from inside it: the same records, and the same total.
                var whole = report.DetailsQuery(row, column, count: PivotEndpoints.MaxDetailsPage);
                var all = await sources.Sql.DetailsAsync(whole, Token);
                Assert.True(all.Total <= PivotEndpoints.MaxDetailsPage, "a cell this test reads whole fits one page");
                SameDetails(await sources.ReferenceDetails(whole), all);
                Assert.Equal(value.Exact, all.Records.Sum(record => (decimal)record.Values[9]!));
                Assert.All(all.Records, record => Assert.NotEqual("GBP", record.Values[5]));
                var page = report.DetailsQuery(row, column, start: 3, count: 4);
                SameDetails(await sources.ReferenceDetails(page), await sources.Sql.DetailsAsync(page, Token));
                cells++;
            }
        }
        Assert.InRange(cells, 10, 1_000);
    }

    /// <summary>Questions for the records behind a cell that a report does not ask: Items of every
    /// kind on the path, a Blank, an Item no trade can carry, pages past the end and of nothing.</summary>
    public static TheoryData<string, PivotFieldItem[], PivotQueryField[], int, int> DetailQuestions => new()
    {
        { "a Blank on the path", [new("Currency", PivotItemKey.Blank)], [], 0, 50 },
        { "a date and a Boolean on the path", [new("TradeDate", Day(3, 4)), new("Confirmed", PivotItemKey.Boolean(true))], [], 0, 50 },
        { "a number and money on the path", [new("Quantity", PivotItemKey.Number(12)), new("Notional", PivotItemKey.Number(1_250_000))], [], 0, 50 },
        { "text spelled in another case, with Items hidden", [new("Region", T("emea")), new("Desk", T("RATES"))], [F("Product", T("swap")), F("Currency", PivotItemKey.Blank)], 5, 20 },
        { "the month, and text with a comma", [new("Month", T("2026-03")), new("Book", T(TradeGenerator.CommaBook))], [], 0, 50 },
        { "an Item of another kind than its field's", [new("Quantity", T("12"))], [], 0, 50 },
        { "the grand total, a page deep in it", [], [F("Region", T("APAC"))], 9_000, 25 },
        { "a page past the end", [new("Desk", T("FX"))], [], 1_000_000, 25 },
        { "a page of nothing", [new("Desk", T("FX"))], [], 0, 0 },
        { "a trade by its Record Key", [new("TradeId", T("t10000007"))], [], 0, 10 },
    };

    [Theory] // ADR-0065, PV-22: any question for records is answered as the reference answers it
    [MemberData(nameof(DetailQuestions))]
    public async Task ADR0065_PV22_details_are_the_reference_details(string question, PivotFieldItem[] path, PivotQueryField[] hidden, int start, int count)
    {
        using var client = server.Factory.CreateClient();
        var sources = await PivotSources.Over(server, client);
        var query = new PivotDetailsQuery(sources.Version, path, [], hidden, start, count);

        var page = await sources.Sql.DetailsAsync(query, Token);

        Assert.False(page.IsRefused, $"{question}: {page.Refusal?.Message}");
        Assert.Equal(sources.Version, page.SourceVersion);
        SameDetails(await sources.ReferenceDetails(query), page);
    }

    /// <summary>Every field's Items, with and without a search, in pages cut short and whole.</summary>
    public static TheoryData<string, string?, int> ItemQuestions => new()
    {
        { "Region", null, 10_000 },
        { "Desk", null, 10_000 },
        { "Book", null, 10_000 },
        { "Book", "RUN", 10_000 },
        { "Book", "legacy\"", 10_000 },
        { "Product", "o", 2 },
        { "Currency", null, 10_000 },
        { "Currency", "u", 10_000 },
        { "Month", "2026-0", 10_000 },
        { "TradeDate", null, 10_000 },
        { "TradeDate", "-03-", 7 },
        { "Notional", null, 25 },
        { "Notional", "5", 10_000 },
        { "Pnl", "-1", 100 },
        { "Quantity", null, 10_000 },
        { "Confirmed", "RU", 10_000 },
        { "TradeId", null, 10_000 },
        { "TradeId", "t100001", 5 },
        { "TradeId", "no such trade", 10 },
    };

    [Theory] // ADR-0065, PV-22: a field's Items over all the data, searched ignoring case, in the source's invariant order
    [MemberData(nameof(ItemQuestions))]
    public async Task ADR0065_PV22_Items_are_the_reference_Items(string field, string? search, int max)
    {
        using var client = server.Factory.CreateClient();
        var sources = await PivotSources.Over(server, client);
        var query = new PivotItemsQuery(field, sources.Version, search, max);

        var page = await sources.Sql.ItemsAsync(query, Token);

        Assert.False(page.IsRefused, page.Refusal?.Message);
        Assert.Equal(sources.Version, page.SourceVersion);
        SameItems(await sources.ReferenceItems(query), page);
    }

    [Fact] // ADR-0065, PV-22: Items are over all the data: no Hidden Item of a report narrows them
    public async Task ADR0065_Items_are_over_all_the_data_with_the_Blank_last_and_the_total()
    {
        using var client = server.Factory.CreateClient();
        var sources = await PivotSources.Over(server, client);

        var page = await sources.Sql.ItemsAsync(new PivotItemsQuery("Currency", sources.Version), Token);

        Assert.Equal(["Text:CHF", "Text:EUR", "Text:GBP", "Text:JPY", "Text:USD", "Blank"], page.Items.Select(Show));
        Assert.Equal(6, page.Total);
        var trades = await sources.Sql.ItemsAsync(new PivotItemsQuery("TradeId", sources.Version, max: 3), Token);
        Assert.Equal(["Text:T10000000", "Text:T10000001", "Text:T10000002"], trades.Items.Select(Show));
        Assert.Equal(server.TradeCount, trades.Total);
    }

    [Fact] // ADR-0065, PV-22: more leaves than the question allows are refused, as the reference refuses them
    public async Task ADR0065_PV22_more_leaves_than_MaxLeaves_are_refused_by_name()
    {
        using var client = server.Factory.CreateClient();
        var sources = await PivotSources.Over(server, client);

        foreach (var query in new PivotQuery[]
                 {
                     new(rows: [F("TradeId")], values: [V("Pnl")], maxLeaves: 1_000),
                     new(rows: [F("Region")], columns: [F("Desk")], maxLeaves: 11),
                 })
        {
            var refused = await sources.Sql.AggregateAsync(query, Token);
            Assert.Equal(PivotSourceRefusal.TooManyLeaves(query.MaxLeaves), refused.Refusal);
            Assert.Equal((await sources.ReferenceAnswer(query)).Refusal, refused.Refusal);
        }

        // At the cap, answered.
        var atCap = await sources.Sql.AggregateAsync(new PivotQuery(rows: [F("Region")], columns: [F("Desk")], maxLeaves: 12), Token);
        Assert.Equal(12, atCap.LeafCount);
    }

    [Fact] // ADR-0065, PV-22: an unknown field and a part no offered Aggregation reads are refused as PivotSource.Fetch refuses them
    public async Task ADR0065_PV22_an_unknown_field_and_an_unoffered_part_are_refused_in_the_same_words()
    {
        using var client = server.Factory.CreateClient();
        var sources = await PivotSources.Over(server, client);
        PivotQuery[] questions =
        [
            new(rows: [F("Region"), F("Nope")], values: [V("Pnl")]),
            new(filters: [F("Trader", T("x"))]),
            new(rows: [F("Region")], values: [V("Pnl"), V("Margin")]),
            new(rows: [F("Region")], values: [V("Pnl", PivotParts.Product)]),
            new(values: [V("Quantity", PivotParts.Sum | PivotParts.Variance)]),
        ];

        foreach (var query in questions)
        {
            // Asked of the server directly, as a page that skipped PivotSource.Fetch would.
            var direct = PivotJson.ReadAnswer(await Post(client, "/api/pivot/aggregate", PivotJson.Write(query), null, Token));
            var asked = sources.Asked.Count;
            var fetched = await sources.Sql.AggregateAsync(query, Token);
            Assert.True(direct.IsRefused);
            Assert.Equal(fetched.Refusal, direct.Refusal);
            Assert.Equal(asked, sources.Asked.Count); // Fetch refused it without asking
        }

        Assert.Equal(PivotSourceRefusal.UnknownField("Nope"), (await sources.Sql.AggregateAsync(questions[0], Token)).Refusal);
        Assert.Equal((await sources.ReferenceAnswer(questions[0])).Refusal, (await sources.Sql.AggregateAsync(questions[0], Token)).Refusal);
        Assert.Equal(PivotSourceRefusal.AggregationNotOffered("Pnl", [PivotAggregation.Product]),
            (await sources.Sql.AggregateAsync(questions[3], Token)).Refusal);
        Assert.Equal(PivotSourceRefusal.AggregationNotOffered("Quantity",
                [PivotAggregation.StdDev, PivotAggregation.StdDevp, PivotAggregation.Var, PivotAggregation.Varp]),
            (await sources.Sql.AggregateAsync(questions[4], Token)).Refusal);

        foreach (var (path, document) in new[]
                 {
                     ("/api/pivot/items", PivotJson.Write(new PivotItemsQuery("Nope", sources.Version))),
                     ("/api/pivot/details", PivotJson.Write(new PivotDetailsQuery(sources.Version, [new("Region", T("EMEA"))], [], [F("Nope", T("x"))], count: 100))),
                 })
        {
            var refusal = JsonDocument.Parse(await Post(client, path, document, null, Token)).RootElement.GetProperty("refusal");
            Assert.Equal("unknownField", refusal.GetProperty("kind").GetString());
            Assert.Equal("Nope", refusal.GetProperty("field").GetString());
        }
        SameItems(await sources.ReferenceItems(new PivotItemsQuery("Nope", sources.Version)),
            PivotJson.ReadItemPage(await Post(client, "/api/pivot/items", PivotJson.Write(new PivotItemsQuery("Nope", sources.Version)), null, Token)));
    }

    [Fact] // ADR-0068: the features leave out what SQLite cannot answer exactly, and the fields are the pages'
    public async Task ADR0068_the_fields_are_the_pages_and_the_features_leave_out_Product_and_the_variances()
    {
        using var client = server.Factory.CreateClient();
        var (fields, features) = await PivotSources.Fields(client);

        Assert.Equal(
            ["TradeId", "Region", "Desk", "Book", "Product", "Currency", "Month", "TradeDate", "Notional", "Pnl", "Quantity", "Confirmed"],
            fields.Select(f => f.Name));
        Assert.Equal(PivotAggregation.Sum, features.Aggregations[0]);
        Assert.Equal(
            [PivotAggregation.Sum, PivotAggregation.Count, PivotAggregation.Average, PivotAggregation.Max, PivotAggregation.Min, PivotAggregation.CountNumbers],
            features.Aggregations);
        Assert.True(features.CanRefresh);
        var pnl = fields.Single(f => f.Name == "Pnl");
        Assert.Equal(("P&L", PivotFieldType.Number, "#,##0.00"), (pnl.Caption, pnl.Type, pnl.Format));
        var date = fields.Single(f => f.Name == "TradeDate");
        Assert.Equal(("Trade date", PivotFieldType.Date, "yyyy-MM-dd"), (date.Caption, date.Type, date.Format));
        Assert.Equal(PivotFieldType.Boolean, fields.Single(f => f.Name == "Confirmed").Type);
    }

    [Fact] // ADR-0065, PV-23: a live tick moves the version on; Items and records asked under the old one are refused, and the new answers are the new data's
    public async Task ADR0065_PV23_after_a_live_tick_the_old_version_is_refused_and_the_answers_follow_the_data()
    {
        using var client = server.Factory.CreateClient();
        var before = await PivotSources.Over(server, client);
        var query = new PivotQuery(rows: [F("Region")], columns: [F("Product")], values: [V("Pnl"), V("Notional", PivotParts.Sum)]);
        var old = await before.Sql.AggregateAsync(query, Token);
        try
        {
            var change = await server.Store.ApplyLiveChangesAsync(50, Token);

            var after = await PivotSources.Over(server, client);
            Assert.Equal(change.Version, after.Version);
            var answer = await after.Sql.AggregateAsync(query, Token);
            Assert.Equal(change.Version, answer.SourceVersion);
            SameAnswer(await after.ReferenceAnswer(query), answer);
            Assert.NotEqual(Leaves(old), Leaves(answer));

            var stale = PivotSourceRefusal.SourceVersionNotHeld(before.Version);
            Assert.Equal(stale, (await after.Sql.ItemsAsync(new PivotItemsQuery("Region", before.Version), Token)).Refusal);
            Assert.Equal(stale, (await after.Sql.DetailsAsync(new PivotDetailsQuery(before.Version, [new("Region", T("EMEA"))], count: 100), Token)).Refusal);
            // As the reference refuses a version it does not hold, in the same words.
            Assert.Equal((await after.ReferenceItems(new PivotItemsQuery("Region", before.Version))).Refusal, stale);
            Assert.Equal((await after.ReferenceDetails(new PivotDetailsQuery(before.Version))).Refusal, stale);
        }
        finally
        {
            using var reset = await client.PostAsync("/api/reset", null, Token);
            Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        }
    }

    [Theory] // ADR-0065/0068: a document that cannot be read is the caller's mistake: a 400 naming it, never a refusal or a 500
    [InlineData("/api/pivot/aggregate", "not json", "is JSON")]
    [InlineData("/api/pivot/aggregate", "{\"version\":2,\"type\":\"query\"}", "version 2 cannot be read")]
    [InlineData("/api/pivot/aggregate", "{\"version\":1,\"type\":\"itemsQuery\",\"field\":\"Region\",\"sourceVersion\":\"x\",\"max\":5}", "not a 'query'")]
    [InlineData("/api/pivot/aggregate", "{\"version\":1,\"type\":\"query\",\"rows\":[{\"field\":\"Region\"}],\"columns\":[{\"field\":\"Region\"}],\"maxLeaves\":10}", "places 'Region' twice")]
    [InlineData("/api/pivot/items", "{\"version\":1,\"type\":\"itemsQuery\",\"field\":\"Region\"}", "'sourceVersion' is a string")]
    [InlineData("/api/pivot/details", "{\"version\":1,\"type\":\"detailsQuery\",\"sourceVersion\":\"x\",\"start\":-1,\"count\":5}", "cannot be")]
    [InlineData("/api/pivot/details", "{\"version\":1,\"type\":\"detailsQuery\",\"sourceVersion\":\"x\",\"start\":0,\"count\":10001}", "at most 10,000 records")]
    public async Task ADR0068_a_document_that_cannot_be_read_is_answered_400(string path, string document, string detail)
    {
        using var client = server.Factory.CreateClient();
        using var content = new StringContent(document, System.Text.Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(path, content, Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Token);
        Assert.Contains(detail, problem.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    private static PivotReport Report(PivotQuery query, PivotAnswer answer, IReadOnlyList<PivotField> fields, PivotLayout layout) =>
        PivotEngine.Report(PivotEngine.Cube(query, answer, fields), layout, new PivotOptions { Culture = CultureInfo.InvariantCulture });

    // Every row's labels and every value cell's shown text and raw number, in the report's order.
    private static string[] Cells(PivotReport report) => report.Rows
        .Select(row => string.Join(" | ", row.Labels.Select(label => label.Text ?? ""))
            + " => " + string.Join(" | ", Enumerable.Range(0, report.ValueColumns.Count).Select(column =>
                row.ValueAt(column) is { } value ? value.Text + " = " + value.ToString(null, CultureInfo.InvariantCulture) : "")))
        .ToArray();
}
