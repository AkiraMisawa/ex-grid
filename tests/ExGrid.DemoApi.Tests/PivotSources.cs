using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ExPivot.Engine;
using Xunit;

namespace ExGrid.DemoApi.Tests;

/// <summary>
/// The two sources PV-22 holds to each other (ADR-0066), over the same trades at the same Source
/// Version: the server's SQL source as a page reaches it — <c>PivotSource.Fetch</c> over HTTP, every
/// question and answer a <c>PivotJson</c> document, with the fields and features
/// <c>GET /api/pivot/fields</c> answers — and the reference, <c>PivotSource.From</c> over the trades
/// the server holds, read in <c>TradeId</c> order, its answers sent through <c>PivotJson</c> too.
/// </summary>
internal sealed class PivotSources
{
    /// <summary>Each field read from a trade, as the server's SQL reads it from a row.</summary>
    public static readonly IReadOnlyDictionary<string, Func<Trade, object?>> Accessors = new Dictionary<string, Func<Trade, object?>>(StringComparer.Ordinal)
    {
        ["TradeId"] = t => t.TradeId,
        ["Region"] = t => t.Region,
        ["Desk"] = t => t.Desk,
        ["Book"] = t => t.Book,
        ["Product"] = t => t.Product,
        ["Currency"] = t => t.Currency,
        ["Month"] = t => t.TradeDate.ToString("yyyy-MM", CultureInfo.InvariantCulture),
        ["TradeDate"] = t => t.TradeDate,
        ["Notional"] = t => t.Notional,
        ["Pnl"] = t => t.Pnl,
        ["Quantity"] = t => t.Quantity,
        ["Confirmed"] = t => t.Confirmed,
    };

    private PivotSources(FetchingPivotSource sql, PivotSource reference, string version, IReadOnlyList<Trade> trades, Counter asked)
    {
        Sql = sql;
        Reference = reference;
        Version = version;
        Trades = trades;
        Asked = asked;
    }

    /// <summary>The server's SQL source, through <c>PivotSource.Fetch</c>.</summary>
    public FetchingPivotSource Sql { get; }

    /// <summary><c>PivotSource.From</c> over the same trades.</summary>
    public PivotSource Reference { get; }

    /// <summary>The Source Version the server's data was at when the reference read it.</summary>
    public string Version { get; }

    /// <summary>The trades, in <c>TradeId</c> order.</summary>
    public IReadOnlyList<Trade> Trades { get; }

    /// <summary>How many questions reached the server.</summary>
    public Counter Asked { get; }

    /// <summary>The reference's Source Version: the one its Items and records are asked under.</summary>
    public string ReferenceVersion { get; private set; } = "";

    /// <summary>Both sources over the server's trades as they are now.</summary>
    public static async Task<PivotSources> Over(DemoApiServer server, HttpClient client)
    {
        var token = TestContext.Current.CancellationToken;
        var (fields, features) = await Fields(client);
        var asked = new Counter();
        var sql = PivotSource.Fetch(
            fields,
            features,
            async (query, ct) => PivotJson.ReadAnswer(await Post(client, "/api/pivot/aggregate", PivotJson.Write(query), asked, ct)),
            async (query, ct) => PivotJson.ReadItemPage(await Post(client, "/api/pivot/items", PivotJson.Write(query), asked, ct)),
            async (query, ct) => PivotJson.ReadDetailPage(await Post(client, "/api/pivot/details", PivotJson.Write(query), asked, ct)),
            async ct =>
            {
                asked.Increment();
                return PivotJson.ReadItemCounts(await client.GetStringAsync("/api/pivot/item-counts", ct));
            });

        var (version, byId) = await TestData.AllTrades(server.Store);
        var trades = byId.Values.OrderBy(trade => trade.TradeId, StringComparer.Ordinal).ToArray();
        var referenceFields = fields.Select(field => new PivotField<Trade>(
            field.Name, field.Type, Accessors[field.Name], field.Caption, field.Format)).ToArray();
        var reference = PivotSource.From(trades, referenceFields);
        var sources = new PivotSources(sql, reference, version, trades, asked);
        // The reference's version is its own; an answer carries it.
        sources.ReferenceVersion = (await reference.AggregateAsync(new PivotQuery(), token)).SourceVersion;
        return sources;
    }

    /// <summary>What <c>GET /api/pivot/fields</c> answers, as <c>PivotSource.Fetch</c> takes it.</summary>
    public static async Task<(PivotField[] Fields, PivotSourceFeatures Features)> Fields(HttpClient client)
    {
        var json = await client.GetFromJsonAsync<JsonElement>("/api/pivot/fields", TestContext.Current.CancellationToken);
        var fields = json.GetProperty("fields").EnumerateArray().Select(field => new PivotField(
            field.GetProperty("name").GetString()!,
            Enum.Parse<PivotFieldType>(field.GetProperty("type").GetString()!, ignoreCase: true),
            field.GetProperty("caption").GetString(),
            field.GetProperty("format").GetString())).ToArray();
        var features = json.GetProperty("features");
        return (fields, new PivotSourceFeatures(
            features.GetProperty("aggregations").EnumerateArray().Select(a => Enum.Parse<PivotAggregation>(a.GetString()!, ignoreCase: true)),
            features.GetProperty("canRefresh").GetBoolean(),
            features.GetProperty("countsItems").GetBoolean(),
            features.GetProperty("answersLevels").GetBoolean()));
    }

    /// <summary>The server's answer to a document, as it is sent: a <c>POST</c>.</summary>
    public static async Task<string> Post(HttpClient client, string path, string document, Counter? asked, CancellationToken cancellationToken)
    {
        asked?.Increment();
        using var content = new StringContent(document, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(path, content, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    /// <summary>The reference's answer, sent through <c>PivotJson</c> as the server's is.</summary>
    public async Task<PivotAnswer> ReferenceAnswer(PivotQuery query) =>
        PivotJson.ReadAnswer(PivotJson.Write(await Reference.AggregateAsync(query, TestContext.Current.CancellationToken)));

    /// <summary>The reference's Items, under its own version, sent through <c>PivotJson</c>.</summary>
    public async Task<PivotItemPage> ReferenceItems(PivotItemsQuery query) =>
        PivotJson.ReadItemPage(PivotJson.Write(await Reference.ItemsAsync(
            new PivotItemsQuery(query.Field, query.SourceVersion == Version ? ReferenceVersion : query.SourceVersion, query.Search, query.Max),
            TestContext.Current.CancellationToken)));

    /// <summary>The reference's records behind a cell, under its own version, sent through <c>PivotJson</c>.</summary>
    public async Task<PivotDetailPage> ReferenceDetails(PivotDetailsQuery query) =>
        PivotJson.ReadDetailPage(PivotJson.Write(await Reference.DetailsAsync(
            new PivotDetailsQuery(query.SourceVersion == Version ? ReferenceVersion : query.SourceVersion,
                query.RowItems, query.ColumnItems, query.HiddenItems, query.Start, query.Count),
            TestContext.Current.CancellationToken)));

    // ---- Comparing answers ------------------------------------------------------------------------

    /// <summary>
    /// The same answer: the same refusal, or the same fields with the same parts, the same Items
    /// spelled the same, and every leaf's records and parts equal to the last bit. A source's own
    /// order of leaves and Items is not part of the answer (ADR-0066), so leaves are matched by their
    /// Items. An answer of Level Aggregates is the same kind of answer, its cells matched by their
    /// Items down to their depths, its windows and rest cells by their parents' Items (ADR-0370
    /// section 3).
    /// </summary>
    public static void SameAnswer(PivotAnswer expected, PivotAnswer actual)
    {
        Assert.Equal(expected.Refusal, actual.Refusal);
        if (expected.IsRefused)
            return;
        Assert.Equal(expected.IsLevels, actual.IsLevels);
        Assert.Equal(expected.Rows.Select(axis => axis.Field), actual.Rows.Select(axis => axis.Field));
        Assert.Equal(expected.Columns.Select(axis => axis.Field), actual.Columns.Select(axis => axis.Field));
        Assert.Equal(expected.Values.Select(v => (v.Field, v.Parts)), actual.Values.Select(v => (v.Field, v.Parts)));
        foreach (var (mine, theirs) in expected.Rows.Concat(expected.Columns).Zip(actual.Rows.Concat(actual.Columns)))
            Assert.Equal(mine.Items.Select(Show).Order(StringComparer.Ordinal), theirs.Items.Select(Show).Order(StringComparer.Ordinal));
        Assert.Equal(expected.LeafCount, actual.LeafCount);
        Assert.Equal(Windows(expected), Windows(actual));
        Assert.Equal(Leaves(expected), Leaves(actual));
    }

    /// <summary>
    /// The same answer of windows one row field deep, but for which tied sibling is a margin: the
    /// bundled sources order ties as their leaves first reached them, the SQL source by its order of
    /// Items, and each answers truly (ADR-0370 section 3). So each window's margin on a side is merged
    /// with that side's rest cell, a column group at a time, and those totals are what must agree,
    /// with the windows and every other cell. A margin is told by the order: the first sibling
    /// answered where the window has siblings before it, the last where it has some after it.
    /// </summary>
    public static void SameWindowedAnswer(PivotAnswer expected, PivotAnswer actual, Func<PivotAnswer, int, double> key, bool descending)
    {
        Assert.Equal(expected.Refusal, actual.Refusal);
        if (expected.IsRefused)
            return;
        Assert.Equal(Windows(expected), Windows(actual));
        Assert.Equal(TieProof(expected, key, descending), TieProof(actual, key, descending));
    }

    // Each cell as Leaves writes it, but each margin's and each rest's merged into one line a side and
    // a column group: records, then each field's values, numbers and exact sum.
    private static string[] TieProof(PivotAnswer answer, Func<PivotAnswer, int, double> key, bool descending)
    {
        Assert.Equal(2, answer.Rows.Count);
        var lines = Leaves(answer).ToList();
        var own = Enumerable.Range(0, answer.LeafCount).ToArray();
        var cellLines = own.ToDictionary(cell => cell, cell => Leaves(answer, cell));
        var sides = new SortedDictionary<string, (long Records, long[] Values, long[] Numbers, decimal[] Sums)>(StringComparer.Ordinal);
        foreach (var window in answer.Windows)
        {
            var parent = window.Parent[0];
            var siblings = own.Where(cell => answer.RestAt(cell) is null && answer.Rows[0].ItemAt(cell) == parent
                    && answer.Rows[1].ItemAt(cell) >= 0 && answer.Columns.All(axis => axis.ItemAt(cell) < 0))
                .OrderBy(cell => descending ? -key(answer, cell) : key(answer, cell)).ToArray();
            var margins = new List<(int Item, PivotRestSide Side)>();
            if (window.Before > 0)
                margins.Add((answer.Rows[1].ItemAt(siblings[0]), PivotRestSide.Before));
            if (window.After > 0)
                margins.Add((answer.Rows[1].ItemAt(siblings[^1]), PivotRestSide.After));
            foreach (var cell in own)
            {
                PivotRestSide? side = answer.RestAt(cell) is { } rest && answer.Windows[rest.Window] == window ? rest.Side : null;
                if (side is null && answer.Rows[0].ItemAt(cell) == parent)
                {
                    foreach (var (item, marginSide) in margins)
                    {
                        if (answer.Rows[1].ItemAt(cell) == item)
                            side = marginSide;
                    }
                }
                if (side is null)
                    continue;
                lines.Remove(cellLines[cell]);
                var name = $"{side} {Parent(answer, window)} at " + string.Join(" | ", answer.Columns.Select(axis => axis.ItemAt(cell) is var item and >= 0 ? Show(axis.Items[item]) : "-"));
                (long Records, long[] Values, long[] Numbers, decimal[] Sums) total = sides.TryGetValue(name, out var held)
                    ? held
                    : (0L, new long[answer.Values.Count], new long[answer.Values.Count], new decimal[answer.Values.Count]);
                total.Records += answer.RecordsAt(cell);
                for (var v = 0; v < answer.Values.Count; v++)
                {
                    total.Values[v] += answer.Values[v].ValuesAt(cell);
                    total.Numbers[v] += answer.Values[v].NumbersAt(cell);
                    if ((answer.Values[v].Parts & PivotParts.Sum) != 0)
                        total.Sums[v] += answer.Values[v].SumAt(cell).ExactValue;
                }
                sides[name] = total;
            }
        }
        lines.AddRange(sides.Select(side => $"{side.Key} => records {side.Value.Records}; "
            + string.Join("; ", side.Value.Values.Select((values, v) => $"values {values} numbers {side.Value.Numbers[v]} sum {Normal(side.Value.Sums[v])}"))));
        lines.Sort(StringComparer.Ordinal);
        return [.. lines];
    }

    /// <summary>Each window as a line — its parent's Items and its counts — in ordinal order.</summary>
    public static string[] Windows(PivotAnswer answer)
        => answer.Windows.Select(window => $"under {Parent(answer, window)}: {window.Before} before, {window.After} after")
            .Order(StringComparer.Ordinal).ToArray();

    // [Text:EMEA] — a window's parent's Items.
    private static string Parent(PivotAnswer answer, PivotAnswerWindow window)
        => "[" + string.Join(", ", window.Parent.Select((item, level) => Show(answer.Rows[level].Items[item]))) + "]";

    /// <summary>Each leaf, or cell, as a line — its Items, <c>-</c> below a cell's depth, a rest
    /// cell's side and parent, then its records and parts — in ordinal order.</summary>
    public static string[] Leaves(PivotAnswer answer)
    {
        var lines = new string[answer.LeafCount];
        for (var leaf = 0; leaf < answer.LeafCount; leaf++)
            lines[leaf] = Leaves(answer, leaf);
        Array.Sort(lines, StringComparer.Ordinal);
        return lines;
    }

    // One leaf's, or cell's, line.
    private static string Leaves(PivotAnswer answer, int leaf)
    {
        var axes = answer.Rows.Concat(answer.Columns).ToArray();
        var line = new StringBuilder();
        line.AppendJoin(" | ", axes.Select(axis => axis.ItemAt(leaf) is var item and >= 0 ? Show(axis.Items[item]) : "-"));
        if (answer.RestAt(leaf) is { } rest)
            line.Append(" rest ").Append(rest.Side).Append(" under ").Append(Parent(answer, answer.Windows[rest.Window]));
        line.Append(" => records ").Append(answer.RecordsAt(leaf));
        foreach (var values in answer.Values)
        {
            line.Append("; ").Append(values.Field).Append(": values ").Append(values.ValuesAt(leaf))
                .Append(" numbers ").Append(values.NumbersAt(leaf)).Append(values.NonFiniteAt(leaf) ? " #NUM!" : "");
            if ((values.Parts & PivotParts.Sum) != 0)
                line.Append(" sum ").Append(Show(values.SumAt(leaf)));
            if ((values.Parts & PivotParts.Extremes) != 0)
                line.Append(" min ").Append(Show(values.MinAt(leaf))).Append(" max ").Append(Show(values.MaxAt(leaf)));
        }
        return line.ToString();
    }

    /// <summary>The same page of Items: the same refusal, or the same Items in the same order,
    /// spelled the same, of the same total.</summary>
    public static void SameItems(PivotItemPage expected, PivotItemPage actual)
    {
        Assert.Equal(expected.Refusal, actual.Refusal);
        if (expected.IsRefused)
            return;
        Assert.Equal(expected.Items.Select(Show), actual.Items.Select(Show));
        Assert.Equal(expected.Total, actual.Total);
    }

    /// <summary>The same page of records: the same refusal, or the same fields, start and total,
    /// and every record's values equal, in order.</summary>
    public static void SameDetails(PivotDetailPage expected, PivotDetailPage actual)
    {
        Assert.Equal(expected.Refusal, actual.Refusal);
        if (expected.IsRefused)
            return;
        Assert.Equal(expected.Fields.Select(f => (f.Name, f.Type, f.Caption, f.Format)), actual.Fields.Select(f => (f.Name, f.Type, f.Caption, f.Format)));
        Assert.Equal(expected.Start, actual.Start);
        Assert.Equal(expected.Total, actual.Total);
        Assert.Equal(expected.Records.Select(Show), actual.Records.Select(Show));
    }

    /// <summary><c>Text:EMEA</c>, <c>Number:1234.5</c>, <c>Blank</c>: an Item as it is spelled.</summary>
    public static string Show(PivotItemKey item) => item.ToString();

    /// <summary>An exact number with no trailing zeros, or a double to the last bit.</summary>
    public static string Show(PivotNumber number) => number.IsExact
        ? Normal(number.ExactValue)
        : number.Value.ToString("R", CultureInfo.InvariantCulture) + "d";

    private static string Show(PivotDetailRecord record) => string.Join(" | ", record.Values.Select(value => value switch
    {
        null => "(blank)",
        decimal number => "decimal " + Normal(number),
        double number => "double " + number.ToString("R", CultureInfo.InvariantCulture),
        DateTime date => "date " + date.ToString("O", CultureInfo.InvariantCulture),
        bool flag => "bool " + flag,
        string text => "text " + text,
        _ => "other " + value,
    }));

    private static string Normal(decimal value) => (value / 1.0000000000000000000000000000m).ToString(CultureInfo.InvariantCulture);

    /// <summary>A count the transport increments.</summary>
    public sealed class Counter
    {
        private int _count;

        public int Count => Volatile.Read(ref _count);

        public void Increment() => Interlocked.Increment(ref _count);
    }
}
