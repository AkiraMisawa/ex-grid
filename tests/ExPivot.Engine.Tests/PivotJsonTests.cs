using System.Text.Json;
using ExPivot.Engine;
using Xunit;
using static ExPivot.Engine.Tests.Sources;

namespace ExPivot.Engine.Tests;

/// <summary>Every Pivot Source question and answer round-trips through <see cref="PivotJson"/>,
/// and a document of a version the reader does not know is refused (ADR-0065, PV-16).</summary>
public class PivotJsonTests
{
    private static readonly PivotSource Server = PivotSource.From(Deals, DealFields);

    [Fact] // ADR-0065: a question round-trips, Hidden Items of every kind and first spellings kept
    public void A_query_round_trips()
    {
        var query = new PivotQuery(
            rows: [F("Desk", PivotItemKey.Text("Rates"), PivotItemKey.Text("ÉCHANGE \"x\""), PivotItemKey.Blank), F("Book")],
            columns: [F("Date", PivotItemKey.Date(new DateTime(2026, 1, 2, 13, 45, 0, 500)))],
            filters: [F("Live", PivotItemKey.Boolean(false)), F("Risk", PivotItemKey.Number(1234.5), PivotItemKey.Error)],
            values: [V("Amount", PivotParts.Sum | PivotParts.Extremes), V("Tag", PivotParts.Counts), V("Risk", PivotParts.Product | PivotParts.Variance)],
            maxLeaves: 12_345);

        var json = PivotJson.Write(query);
        var read = PivotJson.ReadQuery(json);

        Assert.Equal(query, read);
        Assert.Equal(json, PivotJson.Write(read));
        SameKeys(query.Rows[0].HiddenItems, read.Rows[0].HiddenItems);
        Assert.Equal(12_345, read.MaxLeaves);
    }

    [Fact] // ADR-0065: the Leaf Aggregates round-trip — every part, exact decimals, non-finite doubles
    public async Task An_answer_round_trips_to_the_last_bit()
    {
        foreach (var query in Questions)
        {
            var answer = await Server.AggregateAsync(query, Ct);
            var json = PivotJson.Write(answer);
            var read = PivotJson.ReadAnswer(json);

            SameAnswer(answer, read);
            Assert.Equal(json, PivotJson.Write(read));
        }
    }

    [Fact] // ADR-0065/0059: an exact sum crosses as its digits, a double as its shortest text, and a non-finite one by name
    public void Numbers_cross_exactly()
    {
        var query = new PivotQuery(rows: [F("Desk")], values: [V("Amount")]);
        var builder = new PivotAnswerBuilder(query, "v7");
        var exact = builder.AddLeaf([PivotItemKey.Text("Rates")], 3);
        builder.SetCounts(exact, 0, 3, 3);
        builder.SetSum(exact, 0, PivotNumber.Exact(79228162514264337593543950335m));
        builder.SetExtremes(exact, 0, PivotNumber.Exact(-1.000000000000000000000000001m), PivotNumber.Exact(0.10m));
        builder.SetProduct(exact, 0, double.PositiveInfinity);
        builder.SetVariance(exact, 0, double.NaN, double.NegativeInfinity);
        var inexact = builder.AddLeaf([PivotItemKey.Text("FX")], 2);
        builder.SetCounts(inexact, 0, 2, 2, nonFinite: true);
        builder.SetSum(inexact, 0, PivotNumber.Double(0.30000000000000004));
        builder.SetExtremes(inexact, 0, PivotNumber.Double(-0.0), PivotNumber.Double(double.NaN));
        builder.SetProduct(inexact, 0, 5e-324);
        builder.SetVariance(inexact, 0, 1e300, -0.0);
        var answer = builder.Build();

        var json = PivotJson.Write(answer);
        var read = PivotJson.ReadAnswer(json);

        SameAnswer(answer, read);
        var values = read.Values[0];
        Assert.Equal(79228162514264337593543950335m, values.SumAt(0).ExactValue);
        Assert.Equal(-1.000000000000000000000000001m, values.MinAt(0).ExactValue);
        Assert.Equal(2, values.MaxAt(0).ExactValue.Scale);
        Assert.True(double.IsPositiveInfinity(values.ProductAt(0)));
        Assert.True(double.IsNaN(values.MeanAt(0)));
        Assert.Equal(PivotNumber.Double(0.30000000000000004), values.SumAt(1));
        Assert.True(double.IsNegative(values.MinAt(1).Value));
        Assert.True(values.NonFiniteAt(1));
        Assert.Contains("79228162514264337593543950335", json);
        Assert.Contains("\"0.30000000000000004\"", json);
        Assert.Contains("\"Infinity\"", json);
        Assert.Contains("\"NaN\"", json);
    }

    [Fact] // ADR-0065: the leaves travel column by column — an array per level and per part, never an object per leaf
    public async Task The_leaves_travel_column_by_column()
    {
        var records = Enumerable.Range(0, 900).Select(i => new Deal("D" + (i % 30), "B" + (i / 30), null, i, i * 0.5, i % 2 == 0, null)).ToArray();
        var server = PivotSource.From(records, DealFields);
        var answer = await server.AggregateAsync(new PivotQuery(rows: [F("Desk"), F("Book")], values: [V("Amount"), V("Risk")]), Ct);

        var json = PivotJson.Write(answer);
        using var document = JsonDocument.Parse(json);

        Assert.Equal(900, answer.LeafCount);
        Assert.Equal(900, document.RootElement.GetProperty("records").GetArrayLength());
        Assert.Equal(900, document.RootElement.GetProperty("rows")[1].GetProperty("leaves").GetArrayLength());
        Assert.Equal(900, document.RootElement.GetProperty("values")[0].GetProperty("sum").GetArrayLength());
        // Objects: the document, two axes with their 60 Items, two fields in Values.
        Assert.Equal(1 + 2 + 60 + 2, json.Count(c => c == '{'));
    }

    [Theory] // ADR-0065: every refusal round-trips, with its kind, its sentence, its field and its cap
    [MemberData(nameof(Refusals))]
    public void A_refusal_round_trips(PivotSourceRefusal refusal)
    {
        Assert.Equal(refusal, PivotJson.ReadAnswer(PivotJson.Write(PivotAnswer.Refused(refusal))).Refusal);
        Assert.Equal(refusal, PivotJson.ReadItemPage(PivotJson.Write(PivotItemPage.Refused(refusal))).Refusal);
        Assert.Equal(refusal, PivotJson.ReadDetailPage(PivotJson.Write(PivotDetailPage.Refused(refusal))).Refusal);
    }

    public static TheoryData<PivotSourceRefusal> Refusals =>
    [
        PivotSourceRefusal.TooManyLeaves(200_000),
        PivotSourceRefusal.UnknownField("Desk"),
        PivotSourceRefusal.AggregationNotOffered("Amount", [PivotAggregation.Product]),
        PivotSourceRefusal.SourceVersionNotHeld("41"),
    ];

    [Fact] // ADR-0065: a question for Items and its page round-trip
    public async Task Items_round_trip()
    {
        var version = (await Server.AggregateAsync(new PivotQuery(), Ct)).SourceVersion;
        foreach (var query in new[]
                 {
                     new PivotItemsQuery("Desk", version),
                     new PivotItemsQuery("Tag", version, search: "o", max: 2),
                     new PivotItemsQuery("Date", version, max: 0),
                     new PivotItemsQuery("Risk", "not-this-one"),
                 })
        {
            var read = PivotJson.ReadItemsQuery(PivotJson.Write(query));
            Assert.Equal(query, read);

            var page = await Server.ItemsAsync(query, Ct);
            SameItems(page, PivotJson.ReadItemPage(PivotJson.Write(page)));
        }
    }

    [Fact] // ADR-0065: a question for the records behind a cell and its page round-trip, each value by its field's type
    public async Task Details_round_trip()
    {
        var version = (await Server.AggregateAsync(new PivotQuery(), Ct)).SourceVersion;
        var query = new PivotDetailsQuery(
            version,
            rowItems: [new("Book", PivotItemKey.Text("LDN-1"))],
            columnItems: [new("Date", PivotItemKey.Date(new DateTime(2026, 1, 2)))],
            hiddenItems: [F("Desk", PivotItemKey.Text("FX")), F("Live", PivotItemKey.Blank)],
            start: 0,
            count: 10);
        var read = PivotJson.ReadDetailsQuery(PivotJson.Write(query));
        Assert.Equal(query, read);

        var everything = await Server.DetailsAsync(new PivotDetailsQuery(version), Ct);
        Assert.Equal(Deals.Length, everything.Records.Count);
        var json = PivotJson.Write(everything);
        SameDetails(everything, PivotJson.ReadDetailPage(json));
        Assert.Equal(json, PivotJson.Write(PivotJson.ReadDetailPage(json)));

        // By its field's type: text, an exact number, a double as its text, a date, a Boolean; a
        // value of another type — "7" in Amount, a Boolean in Tag — tagged with its own.
        Assert.Contains("[\"Rates\",\"LDN-1\",\"2026-01-02T00:00:00\",100.50,\"1.5\",true,\"High\"]", json);
        Assert.Contains("{\"text\":\"7\"}", json);
        Assert.Contains("{\"boolean\":true}", json);
        Assert.Contains("{\"date\":\"2026-01-01T00:00:00\"}", json);
        Assert.Contains("\"NaN\"", json);
    }

    [Fact] // ADR-0065/0066: a source's notice that its data moved on round-trips, with or without its version
    public void A_change_round_trips()
    {
        Assert.Equal(new PivotSourceChanged("42"), PivotJson.ReadSourceChanged(PivotJson.Write(new PivotSourceChanged("42"))));
        Assert.Equal(new PivotSourceChanged(), PivotJson.ReadSourceChanged(PivotJson.Write(new PivotSourceChanged())));
    }

    [Fact] // ADR-0065: a document of a version the reader does not know is refused, whatever it is
    public async Task An_unknown_version_is_refused()
    {
        var answer = await Server.AggregateAsync(Questions[0], Ct);
        var documents = new (string Json, Action<string> Read)[]
        {
            (PivotJson.Write(Questions[0]), json => PivotJson.ReadQuery(json)),
            (PivotJson.Write(answer), json => PivotJson.ReadAnswer(json)),
            (PivotJson.Write(new PivotItemsQuery("Desk", "1")), json => PivotJson.ReadItemsQuery(json)),
            (PivotJson.Write(PivotItemPage.Refused(PivotSourceRefusal.UnknownField("X"))), json => PivotJson.ReadItemPage(json)),
            (PivotJson.Write(new PivotDetailsQuery("1")), json => PivotJson.ReadDetailsQuery(json)),
            (PivotJson.Write(PivotDetailPage.Refused(PivotSourceRefusal.UnknownField("X"))), json => PivotJson.ReadDetailPage(json)),
            (PivotJson.Write(new PivotSourceChanged("1")), json => PivotJson.ReadSourceChanged(json)),
        };
        foreach (var (json, read) in documents)
        {
            Assert.StartsWith("{\"version\":1,", json);
            var refused = Assert.Throws<NotSupportedException>(() => read(json.Replace("\"version\":1", "\"version\":2")));
            Assert.Contains("version 2", refused.Message);
            Assert.Throws<FormatException>(() => read(json.Replace("\"version\":1,", "")));
        }
    }

    [Fact] // ADR-0065: a document of another type, or one that cannot be, is refused by name rather than half-read
    public async Task A_document_that_cannot_be_is_refused()
    {
        var answer = await Server.AggregateAsync(Questions[0], Ct);
        var json = PivotJson.Write(answer);

        Assert.Contains("'answer'", Assert.Throws<FormatException>(() => PivotJson.ReadQuery(json)).Message);
        Assert.Throws<FormatException>(() => PivotJson.ReadAnswer("[1]"));
        Assert.Throws<FormatException>(() => PivotJson.ReadAnswer("{\"version\":1,"));
        // A leaf array one short, an Item index out of range, a double where an exact sum stands.
        Assert.Throws<FormatException>(() => PivotJson.ReadAnswer(json.Replace("\"records\":[", "\"records\":[1,")));
        var tampered = JsonDocument.Parse(json).RootElement.GetProperty("rows")[0].GetProperty("leaves").GetRawText();
        Assert.Throws<FormatException>(() => PivotJson.ReadAnswer(json.Replace(tampered, tampered.Replace("[0", "[99"))));
        Assert.Throws<FormatException>(() => PivotJson.ReadQuery("{\"version\":1,\"type\":\"query\",\"values\":[{\"field\":\"A\",\"parts\":[\"median\"]}]}"));
        Assert.Throws<FormatException>(() => PivotJson.ReadQuery("{\"version\":1,\"type\":\"query\",\"rows\":[{\"field\":\"A\"}],\"columns\":[{\"field\":\"A\"}]}"));
        Assert.Throws<FormatException>(() => PivotJson.ReadItemPage("{\"version\":1,\"type\":\"itemPage\",\"sourceVersion\":\"1\",\"items\":[{\"kind\":\"number\",\"value\":\"12,5\"}],\"total\":1}"));
    }
}
