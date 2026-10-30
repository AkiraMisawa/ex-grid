using ExPivot.Engine;
using Xunit;
using static ExPivot.Engine.Tests.Pivot;
using static ExPivot.Engine.Tests.Sources;

namespace ExPivot.Engine.Tests;

/// <summary>
/// Date parts (ADR-0060, Q3; PV-32) as the C# surface declares them: through <see cref="PivotFields"/>
/// in one line, over a Snapshot, and as a <see cref="PivotField{TRecord}"/> of the records' own. How a
/// part is labelled, ordered, worded and hidden is held on both engines by the shared cases
/// (<c>tests/cases/pivot/dates.json</c>).
/// </summary>
public class DatePartTests
{
    private sealed record Trade(DateTime? TradeDate, decimal Amount);

    private static readonly Trade[] Trades =
    [
        new(new DateTime(2026, 9, 30), 1m),
        new(new DateTime(2026, 1, 15), 2m),
        new(new DateTime(2025, 4, 1), 4m),
        new(new DateTime(2026, 12, 31, 23, 59, 0), 8m),
        new(null, 16m),
        new(new DateTime(2026, 8, 2), 32m),
        new(new DateTime(2025, 9, 1), 64m),
    ];

    private static PivotFields<Trade> Fields() => PivotFields.Of<Trade>()
        .Date("TradeDate", t => t.TradeDate, caption: "Trade date")
        .Year("Year", of: "TradeDate")
        .Quarter("Quarter", of: "TradeDate")
        .Month("Month", of: "TradeDate", caption: "Month of trade")
        .Number("Amount", t => t.Amount);

    private static async Task<string[]> Lines(PivotLayout layout, PivotOptions? options = null)
        => Pivot.Lines(await SnapshotSourceTests.ReportOf(PivotSource.From(Trades, Fields()), layout, options));

    private static PivotLayout By(params PivotFieldPlacement[] rows) => new() { Rows = rows, Values = [Sum("Amount")] };

    [Fact] // ADR-0060 (PV-32): a date part's words answer only their own ids, and a month is from 1 to 12
    public void The_date_words_know_only_their_ids()
    {
        Assert.Null(PivotDateWords.EnglishFor("row-labels"));
        Assert.Throws<ArgumentOutOfRangeException>(() => PivotDateWords.Month(13));
    }

    [Fact] // ADR-0060/0066 (PV-32): a part is a field like any other — its own caption, a Date field's defaults, counted in Values, and the records behind it
    public async Task A_part_is_a_field_like_any_other()
    {
        var fields = Fields();
        var source = PivotSource.From(Trades, fields);
        var layout = new PivotLayout { Rows = [P("Month")], Values = [Value("Month", PivotAggregation.Count)] };
        var report = await SnapshotSourceTests.ReportOf(source, layout);
        var september = report.Rows.Single(r => r.Labels[0].Text == "Sep");
        var details = await source.DetailsAsync(report.DetailsQuery(september, 0), Ct);

        Assert.Equal(("Month", "Month of trade", PivotFieldType.Date, "TradeDate", PivotDatePart.Month),
            (fields.Fields[3].Name, fields.Fields[3].Caption, fields.Fields[3].Type, fields.Fields[3].Column, fields.Fields[3].DatePart));
        Assert.Equal("Year", fields.Fields[1].Caption);
        Assert.Equal(["Row Labels", "Count of Month of trade"], Headers(report));
        Assert.Equal("t Grand Total || 6", Pivot.Lines(report)[^1]);
        Assert.Equal([1m, 64m], details.Records.Select(r => ((Trade)r.Record!).Amount));
        // A record carries a part as its number, as a detail record carries every integer.
        Assert.Equal<object?>(9m, details.Records[0].Values[3]);
    }

    [Fact] // ADR-0060 (PV-32): a part over the records' own accessors answers as the declared one does
    public async Task A_part_over_untyped_accessors_answers_alike()
    {
        PivotField<Trade>[] untyped =
        [
            new("TradeDate", PivotFieldType.Date, t => t.TradeDate),
            new("Month", PivotFieldType.Date, t => t.TradeDate) { DatePart = PivotDatePart.Month },
            new("Amount", PivotFieldType.Number, t => t.Amount),
        ];
        var layout = By(P("Month"));

        Assert.Equal(await Lines(layout), Pivot.Lines(PivotEngine.Compute(Trades, untyped, layout, EnUs)));
        var refused = Assert.Throws<ExGrid.Data.SnapshotException>(() => PivotEngine.Compute(
            [new Trade(new DateTime(2026, 1, 1), 1m)],
            [new PivotField<Trade>("Month", PivotFieldType.Date, t => "September") { DatePart = PivotDatePart.Month }],
            layout with { Values = [] }));
        Assert.Contains("'Month'", refused.Message);
    }

    [Fact] // ADR-0060 (PV-32): a part is declared from a Date field, and only a Date field
    public void A_part_is_of_a_date_field()
    {
        Assert.Throws<ArgumentException>(() => PivotFields.Of<Trade>().Number("Amount", t => t.Amount).Month("Month", of: "Amount"));
        Assert.Throws<ArgumentException>(() => PivotFields.Of<Trade>().Month("Month", of: "TradeDate"));
        Assert.Throws<ArgumentException>(() => new PivotField("Month", PivotFieldType.Text) { DatePart = PivotDatePart.Month });
    }
}
