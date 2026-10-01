using ExPivot.Engine;
using Xunit;
using static ExPivot.Engine.Tests.Pivot;
using static ExPivot.Engine.Tests.Sources;

namespace ExPivot.Engine.Tests;

/// <summary>
/// Date parts (ADR-0060, Q3; PV-32): a field declared as the year, quarter or month of a Date
/// column, in one line. Each part is labelled as Excel labels it, in the report's words — <c>2026</c>,
/// <c>Qtr3</c>, <c>Sep</c> — and ordered by the calendar, never alphabetically; the part of a Blank is
/// a Blank.
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

    [Fact] // ADR-0060 (PV-32): a year is labelled by its number, ordered by the calendar; the part of a Blank is a Blank
    public async Task Years_are_labelled_and_ordered_by_the_calendar()
        => Assert.Equal(["i 2025 || 68", "i 2026 || 43", "i (blank) || 16", "t Grand Total || 127"], await Lines(By(P("Year"))));

    [Fact] // ADR-0060 (PV-32): a quarter is Qtr1 to Qtr4, by the calendar
    public async Task Quarters_are_labelled_as_excel_labels_them()
        => Assert.Equal(["i Qtr1 || 2", "i Qtr2 || 4", "i Qtr3 || 97", "i Qtr4 || 8", "i (blank) || 16", "t Grand Total || 127"], await Lines(By(P("Quarter"))));

    [Fact] // ADR-0060 (PV-32): a month is Jan to Dec, by the calendar and never alphabetically; descending reverses it with (blank) last
    public async Task Months_are_ordered_by_the_calendar_never_alphabetically()
    {
        Assert.Equal(
            ["i Jan || 2", "i Apr || 4", "i Aug || 32", "i Sep || 65", "i Dec || 8", "i (blank) || 16", "t Grand Total || 127"],
            await Lines(By(P("Month"))));
        Assert.Equal(
            ["i Dec || 8", "i Sep || 65", "i Aug || 32", "i Apr || 4", "i Jan || 2", "i (blank) || 16", "t Grand Total || 127"],
            await Lines(By(P("Month") with { Sort = PivotSort.Descending })));
    }

    [Fact] // ADR-0060 (PV-32): the parts nest as Excel's grouping does, and a part reads the date's clock, ignoring its time of day
    public async Task The_parts_nest_by_year_quarter_and_month()
    {
        var lines = await Lines(By(P("Year"), P("Quarter"), P("Month")));

        Assert.Equal(
        [
            "g [-]2025 || 68", "g   [-]Qtr2 || 4", "i     Apr || 4", "g   [-]Qtr3 || 64", "i     Sep || 64",
            "g [-]2026 || 43", "g   [-]Qtr1 || 2", "i     Jan || 2", "g   [-]Qtr3 || 33", "i     Aug || 32", "i     Sep || 1",
            "g   [-]Qtr4 || 8", "i     Dec || 8",
            "g [-](blank) || 16", "g   [-](blank) || 16", "i     (blank) || 16",
            "t Grand Total || 127",
        ], lines);
    }

    [Fact] // ADR-0060 (PV-32): the parts are painted in the report's words, which the Consumer replaces by id
    public async Task The_parts_are_painted_in_the_reports_words()
    {
        var japanese = new PivotOptions
        {
            Culture = EnUs.Culture,
            Label = id => id switch
            {
                PivotDateWords.Year => "{0}年",
                PivotDateWords.Quarter => "第{0}四半期",
                _ when id == PivotDateWords.Month(9) => "9月",
                _ => null,
            },
        };

        Assert.Equal(["i 2025年 || 68", "i 2026年 || 43", "i (blank) || 16", "t Grand Total || 127"], await Lines(By(P("Year")), japanese));
        Assert.Equal("i 第3四半期 || 97", (await Lines(By(P("Quarter")), japanese))[2]);
        Assert.Equal(["i Jan || 2", "i Apr || 4", "i Aug || 32", "i 9月 || 65"], (await Lines(By(P("Month")), japanese))[..4]);
    }

    [Fact] // ADR-0060 (PV-32, PV-33): the bundled Japanese words a date part as the Japanese edition's date grouping does
    public async Task The_bundled_japanese_words_the_parts()
    {
        var japanese = new PivotOptions { Culture = EnUs.Culture, Label = PivotWords.Japanese };

        Assert.Equal(["i 2025年 || 68", "i 2026年 || 43", "i (空白) || 16", "t 総計 || 127"], await Lines(By(P("Year")), japanese));
        Assert.Equal("i 第3四半期 || 97", (await Lines(By(P("Quarter")), japanese))[2]);
        Assert.Equal(["i 1月 || 2", "i 4月 || 4", "i 8月 || 32", "i 9月 || 65"], (await Lines(By(P("Month")), japanese))[..4]);
        Assert.All(PivotDateWords.Ids, id => Assert.NotNull(PivotWords.JapaneseFor(id)));
    }

    [Fact] // ADR-0060 (PV-32): the word ids, each with its English
    public void The_words_have_ids_with_their_english()
    {
        Assert.Equal(
            ["date-year", "date-quarter", "date-month-1", "date-month-2", "date-month-3", "date-month-4", "date-month-5", "date-month-6",
             "date-month-7", "date-month-8", "date-month-9", "date-month-10", "date-month-11", "date-month-12"],
            PivotDateWords.Ids);
        Assert.Equal(["{0}", "Qtr{0}", "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"], PivotDateWords.Ids.Select(PivotDateWords.EnglishFor));
        Assert.Null(PivotDateWords.EnglishFor("row-labels"));
        Assert.Throws<ArgumentOutOfRangeException>(() => PivotDateWords.Month(13));
    }

    [Fact] // ADR-0060 (PV-32): a part's Item is its number, so a Hidden Item reads the same in any culture
    public async Task A_parts_hidden_item_is_its_number()
    {
        var lines = await Lines(By(P("Month") with { HiddenItems = [PivotItemKey.Number(9), PivotItemKey.Blank] }));
        var answer = await PivotSource.From(Trades, Fields()).AggregateAsync(new PivotQuery(rows: [F("Quarter")]), Ct);

        Assert.Equal(["i Jan || 2", "i Apr || 4", "i Aug || 32", "i Dec || 8", "t Grand Total || 46"], lines);
        SameKeys([PivotItemKey.Number(3), PivotItemKey.Number(1), PivotItemKey.Number(2), PivotItemKey.Number(4), PivotItemKey.Blank], answer.Rows[0].Items);
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
