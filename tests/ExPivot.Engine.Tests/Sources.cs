using ExPivot.Engine;
using Xunit;

namespace ExPivot.Engine.Tests;

/// <summary>A deal, the Source Record the Pivot Source tests ask about: every kind of value the
/// engine reads, blanks, a value of another kind than its field's, non-finite doubles, and a
/// decimal sum that overflows.</summary>
internal sealed record Deal(string? Desk, string? Book, DateTime? Date, object? Amount, double? Risk, bool? Live, object? Tag);

internal enum Rating { Low, High }

/// <summary>The data, the questions and the comparisons the Pivot Source tests share
/// (ADR-0065).</summary>
internal static class Sources
{
    //  Desk     Book    Date              Amount                  Risk      Live   Tag
    //  Rates    LDN-1   2026-01-02        100.50                  1.5       TRUE   High
    //  RATES    LDN-1   2026-01-02 13:45  200                     0.1       FALSE  Low
    //  Credit   NY-2    2026-02-03        7 (int)                 NaN       TRUE   (blank)
    //  Credit   (blank) (blank)           "7" (text)              -0        (bl.)  a Guid
    //  (blank)  NY-2    2026-02-03        (blank)                 Infinity  TRUE   12
    //  ""       TKY-3   2026-03-04        decimal.MaxValue        5e-324    FALSE  High
    //  FX       TKY-3   2026-03-04        decimal.MaxValue        2.5       TRUE   Low
    //  FX       LDN-1   2026-01-02        12345678901234567890L   1e300     FALSE  TRUE
    //  Rates    NY-2    2026-02-03        0.1 (double)            3         TRUE   2026-01-01 (DateOnly)
    public static readonly Deal[] Deals =
    [
        new("Rates", "LDN-1", new DateTime(2026, 1, 2), 100.50m, 1.5, true, Rating.High),
        new("RATES", "LDN-1", new DateTime(2026, 1, 2, 13, 45, 0), 200m, 0.1, false, Rating.Low),
        new("Credit", "NY-2", new DateTime(2026, 2, 3), 7, double.NaN, true, null),
        new("Credit", null, null, "7", -0.0, null, Guid.Parse("7d3f7a52-1b49-4a3e-9d57-2b8c3c0e6f11")),
        new(null, "NY-2", new DateTime(2026, 2, 3), null, double.PositiveInfinity, true, 12),
        new("", "TKY-3", new DateTime(2026, 3, 4), decimal.MaxValue, 5e-324, false, Rating.High),
        new("FX", "TKY-3", new DateTime(2026, 3, 4), decimal.MaxValue, 2.5, true, Rating.Low),
        new("FX", "LDN-1", new DateTime(2026, 1, 2), 12345678901234567890L, 1e300, false, true),
        new("Rates", "NY-2", new DateTime(2026, 2, 3), 0.1, 3.0, true, new DateOnly(2026, 1, 1)),
    ];

    public static readonly PivotField<Deal>[] DealFields =
    [
        new("Desk", PivotFieldType.Text, d => d.Desk),
        new("Book", PivotFieldType.Text, d => d.Book, caption: "Trading book"),
        new("Date", PivotFieldType.Date, d => d.Date, format: "yyyy-MM-dd"),
        new("Amount", PivotFieldType.Number, d => d.Amount, format: "#,##0.00"),
        new("Risk", PivotFieldType.Number, d => d.Risk),
        new("Live", PivotFieldType.Boolean, d => d.Live),
        new("Tag", PivotFieldType.Text, d => d.Tag),
    ];

    public const PivotParts AllParts = PivotParts.Sum | PivotParts.Extremes | PivotParts.Product | PivotParts.Variance;

    /// <summary>The running test's token, so a cancelled run stops the source too.</summary>
    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static PivotQueryField F(string field, params PivotItemKey[] hidden) => new(field, hidden);

    public static PivotQueryValue V(string field, PivotParts parts = AllParts) => new(field, parts);

    /// <summary>Questions over <see cref="Deals"/> that between them reach every kind of Item,
    /// every part, Hidden Items in each Area, and the empty question.</summary>
    public static PivotQuery[] Questions =>
    [
        new(rows: [F("Desk")], values: [V("Amount"), V("Risk")]),
        new(rows: [F("Desk"), F("Book")], columns: [F("Live")], values: [V("Amount", PivotParts.Sum), V("Tag", PivotParts.Counts)]),
        new(rows: [F("Book")], columns: [F("Date")], filters: [F("Live", PivotItemKey.Boolean(false))], values: [V("Risk", PivotParts.Sum | PivotParts.Extremes)]),
        new(columns: [F("Tag")], values: [V("Amount")]),
        new(values: [V("Amount", PivotParts.Sum)]),
        new(rows: [F("Desk", PivotItemKey.Text("rates"), PivotItemKey.Blank)], values: [V("Amount", PivotParts.Sum), V("Risk", PivotParts.Variance)]),
        new(rows: [F("Tag")], columns: [F("Desk", PivotItemKey.Text(""))], values: [V("Risk", PivotParts.Product)]),
        new(),
        new(rows: [F("Date")], filters: [F("Book", PivotItemKey.Text("NY-2"))]),
    ];

    /// <summary>A Fetch source whose transport is JSON both ways, into <paramref name="server"/>
    /// (ADR-0065): every question and every answer crosses as a document.</summary>
    public static FetchingPivotSource OverJson(PivotSource server, IReadOnlyList<PivotField>? fields = null)
        => PivotSource.Fetch(
            fields ?? server.Fields,
            server.Features,
            async (query, ct) => PivotJson.ReadAnswer(PivotJson.Write(await server.AggregateAsync(PivotJson.ReadQuery(PivotJson.Write(query)), ct))),
            async (query, ct) => PivotJson.ReadItemPage(PivotJson.Write(await server.ItemsAsync(PivotJson.ReadItemsQuery(PivotJson.Write(query)), ct))),
            async (query, ct) => PivotJson.ReadDetailPage(PivotJson.Write(await server.DetailsAsync(PivotJson.ReadDetailsQuery(PivotJson.Write(query)), ct))));

    /// <summary>Two answers are the same answer: the same refusal, or the same Source Version, the
    /// same Items with the same first spellings, and every leaf's Items, records and parts equal to
    /// the last bit.</summary>
    public static void SameAnswer(PivotAnswer expected, PivotAnswer actual, bool sameVersion = true)
    {
        Assert.Equal(expected.Refusal, actual.Refusal);
        if (expected.IsRefused)
            return;
        if (sameVersion)
            Assert.Equal(expected.SourceVersion, actual.SourceVersion);
        Assert.Equal(expected.LeafCount, actual.LeafCount);
        SameAxes(expected.Rows, actual.Rows, expected.LeafCount);
        SameAxes(expected.Columns, actual.Columns, expected.LeafCount);
        for (var leaf = 0; leaf < expected.LeafCount; leaf++)
            Assert.Equal(expected.RecordsAt(leaf), actual.RecordsAt(leaf));
        Assert.Equal(expected.Values.Count, actual.Values.Count);
        for (var v = 0; v < expected.Values.Count; v++)
        {
            var mine = expected.Values[v];
            var theirs = actual.Values[v];
            Assert.Equal(mine.Field, theirs.Field);
            Assert.Equal(mine.Parts, theirs.Parts);
            for (var leaf = 0; leaf < expected.LeafCount; leaf++)
            {
                Assert.Equal(mine.ValuesAt(leaf), theirs.ValuesAt(leaf));
                Assert.Equal(mine.NumbersAt(leaf), theirs.NumbersAt(leaf));
                Assert.Equal(mine.NonFiniteAt(leaf), theirs.NonFiniteAt(leaf));
                if ((mine.Parts & PivotParts.Sum) != 0)
                    Assert.Equal(mine.SumAt(leaf), theirs.SumAt(leaf));
                if ((mine.Parts & PivotParts.Extremes) != 0)
                {
                    Assert.Equal(mine.MinAt(leaf), theirs.MinAt(leaf));
                    Assert.Equal(mine.MaxAt(leaf), theirs.MaxAt(leaf));
                }
                if ((mine.Parts & PivotParts.Product) != 0)
                    Assert.Equal(Bits(mine.ProductAt(leaf)), Bits(theirs.ProductAt(leaf)));
                if ((mine.Parts & PivotParts.Variance) != 0)
                {
                    Assert.Equal(Bits(mine.MeanAt(leaf)), Bits(theirs.MeanAt(leaf)));
                    Assert.Equal(Bits(mine.M2At(leaf)), Bits(theirs.M2At(leaf)));
                }
            }
        }
    }

    private static void SameAxes(IReadOnlyList<PivotAnswerAxis> expected, IReadOnlyList<PivotAnswerAxis> actual, int leaves)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var level = 0; level < expected.Count; level++)
        {
            Assert.Equal(expected[level].Field, actual[level].Field);
            SameKeys(expected[level].Items, actual[level].Items);
            for (var leaf = 0; leaf < leaves; leaf++)
                Assert.Equal(expected[level].ItemAt(leaf), actual[level].ItemAt(leaf));
        }
    }

    /// <summary>The same keys in the same order, text spelled the same — key equality alone
    /// ignores case, and the first spelling labels an Item.</summary>
    public static void SameKeys(IReadOnlyList<PivotItemKey> expected, IReadOnlyList<PivotItemKey> actual)
        => Assert.Equal(expected.Select(k => (k.Kind, k.Value)), actual.Select(k => (k.Kind, k.Value)));

    public static void SameItems(PivotItemPage expected, PivotItemPage actual)
    {
        Assert.Equal(expected.Refusal, actual.Refusal);
        if (expected.IsRefused)
            return;
        Assert.Equal(expected.SourceVersion, actual.SourceVersion);
        Assert.Equal(expected.Total, actual.Total);
        SameKeys(expected.Items, actual.Items);
    }

    /// <summary>The same records behind a cell: the values of each, by field. The Consumer's own
    /// objects stay in the process, so they are not compared.</summary>
    public static void SameDetails(PivotDetailPage expected, PivotDetailPage actual)
    {
        Assert.Equal(expected.Refusal, actual.Refusal);
        if (expected.IsRefused)
            return;
        Assert.Equal(expected.SourceVersion, actual.SourceVersion);
        Assert.Equal(expected.Start, actual.Start);
        Assert.Equal(expected.Total, actual.Total);
        Assert.Equal(expected.Fields.Select(f => (f.Name, f.Type, f.Caption, f.Format)), actual.Fields.Select(f => (f.Name, f.Type, f.Caption, f.Format)));
        Assert.Equal(expected.Records.Count, actual.Records.Count);
        for (var r = 0; r < expected.Records.Count; r++)
        {
            var mine = expected.Records[r].Values;
            var theirs = actual.Records[r].Values;
            Assert.Equal(mine.Count, theirs.Count);
            for (var f = 0; f < mine.Count; f++)
                SameValue(mine[f], theirs[f]);
        }
    }

    /// <summary>One value, of the same type and equal — a double to the last bit.</summary>
    public static void SameValue(object? expected, object? actual)
    {
        Assert.Equal(expected?.GetType(), actual?.GetType());
        if (expected is double d)
            Assert.Equal(Bits(d), Bits((double)actual!));
        else
            Assert.Equal(expected, actual);
    }

    public static long Bits(double value) => BitConverter.DoubleToInt64Bits(value);
}
