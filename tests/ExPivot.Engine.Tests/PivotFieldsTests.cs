using ExGrid.Data;
using ExPivot.Engine;
using Xunit;
using static ExPivot.Engine.Tests.Pivot;
using static ExPivot.Engine.Tests.Sources;

namespace ExPivot.Engine.Tests;

/// <summary>
/// Typed field declarations (ADR-0064/0066, Q53): each field declared once, with a typed accessor
/// and its pivot settings, makes both a Snapshot column — read without boxing — and the Pivot Field
/// over it. They are the standard way to hand ExPivot records.
/// </summary>
public class PivotFieldsTests
{
    internal sealed record Trade(
        string Id, string? Region, decimal? Pnl, double? Price, long? Quantity, int? Lots,
        DateTime? Traded, DateOnly? Settles, DateTimeOffset? Booked, bool? Confirmed);

    private static readonly Trade[] Trades =
    [
        new("T1", "East", 10.25m, 99.5, 3, 1, new DateTime(2026, 9, 30), new DateOnly(2026, 10, 2), new DateTimeOffset(2026, 9, 30, 9, 0, 0, TimeSpan.FromHours(9)), true),
        new("T2", "west", -2.5m, null, null, 2, null, null, null, false),
        new("T3", null, null, 101.25, 7, null, new DateTime(2026, 10, 1), new DateOnly(2026, 10, 5), null, null),
    ];

    private static PivotFields<Trade> Declarations() => PivotFields.Of<Trade>()
        .Key("Id", t => t.Id)
        .Text("Region", t => t.Region, caption: "Sales region", itemOrder: ["West"])
        .Number("Pnl", t => t.Pnl, caption: "P&L", format: "#,##0.00")
        .Number("Price", t => t.Price, format: "0.000")
        .Number("Quantity", t => t.Quantity)
        .Number("Lots", t => t.Lots)
        .Date("Traded", t => t.Traded, caption: "Trade date", format: "yyyy-MM-dd")
        .Date("Settles", t => t.Settles)
        .Date("Booked", t => t.Booked)
        .Boolean("Confirmed", t => t.Confirmed)
        .Month("Month", of: "Traded");

    [Fact] // ADR-0064/0066 (Q53): one declaration makes the Pivot Field and the Snapshot column it reads
    public void One_declaration_makes_the_field_and_its_column()
    {
        var fields = Declarations();
        var snapshot = fields.Build(Trades);

        Assert.Equal(
        [
            ("Region", "Sales region", PivotFieldType.Text, null), ("Pnl", "P&L", PivotFieldType.Number, "#,##0.00"),
            ("Price", "Price", PivotFieldType.Number, "0.000"), ("Quantity", "Quantity", PivotFieldType.Number, null),
            ("Lots", "Lots", PivotFieldType.Number, null), ("Traded", "Trade date", PivotFieldType.Date, "yyyy-MM-dd"),
            ("Settles", "Settles", PivotFieldType.Date, null), ("Booked", "Booked", PivotFieldType.Date, null),
            ("Confirmed", "Confirmed", PivotFieldType.Boolean, null), ("Month", "Month", PivotFieldType.Date, null),
        ], fields.Fields.Select(f => (f.Name, f.Caption, f.Type, f.Format)));
        Assert.Equal<object>(["West"], fields.Fields[0].ItemOrder);
        Assert.Equal(
        [
            ("Id", SnapshotKind.Text), ("Region", SnapshotKind.Text), ("Pnl", SnapshotKind.Decimal), ("Price", SnapshotKind.Double),
            ("Quantity", SnapshotKind.Integer), ("Lots", SnapshotKind.Integer), ("Traded", SnapshotKind.Date), ("Settles", SnapshotKind.Date),
            ("Booked", SnapshotKind.Date), ("Confirmed", SnapshotKind.Boolean),
        ], snapshot.Columns.Select(c => (c.Name, c.Kind)));
        Assert.Equal("Sales region", snapshot["Region"].Caption);
        Assert.Equal("Id", snapshot.RecordKey!.Name);
        Assert.True(snapshot.KeepsRecords);
    }

    [Fact] // ADR-0060/0064 (Q53): a Number takes decimal (exact), double, long or int, and its column is of that kind; a date is its clock
    public async Task A_number_and_a_date_are_held_by_their_accessors_type()
    {
        var source = PivotSource.From(Trades, Declarations());
        var answer = await source.AggregateAsync(new PivotQuery(values: [V("Pnl"), V("Price"), V("Quantity"), V("Lots")]), Ct);

        Assert.Equal(PivotNumber.Exact(7.75m), answer.Values[0].SumAt(0));
        Assert.Equal(PivotNumber.Double(200.75), answer.Values[1].SumAt(0));
        Assert.Equal(PivotNumber.Exact(10m), answer.Values[2].SumAt(0));
        Assert.Equal(PivotNumber.Exact(3m), answer.Values[3].SumAt(0));
        // A DateTimeOffset is the clock it shows, its offset dropped (ADR-0064).
        var booked = await source.AggregateAsync(new PivotQuery(rows: [F("Booked")]), Ct);
        SameKeys([PivotItemKey.Date(new DateTime(2026, 9, 30, 9, 0, 0)), PivotItemKey.Blank], booked.Rows[0].Items);
        var settles = await source.AggregateAsync(new PivotQuery(rows: [F("Settles")]), Ct);
        SameKeys([PivotItemKey.Date(new DateTime(2026, 10, 2)), PivotItemKey.Blank, PivotItemKey.Date(new DateTime(2026, 10, 5))], settles.Rows[0].Items);
    }

    [Fact] // ADR-0066 (Q53): the declarations' settings reach the report — captions, formats, the declared order, the date part
    public async Task The_declarations_settings_reach_the_report()
    {
        var source = PivotSource.From(Trades, Declarations());
        var layout = new PivotLayout { Rows = [P("Region")], Columns = [P("Month")], Values = [Sum("Pnl"), Value("Price", PivotAggregation.Max)] };

        var report = await SnapshotSourceTests.ReportOf(source, layout);

        Assert.Equal(["Row Labels", .. Enumerable.Repeat<string[]>(["Sum of P&L", "Max of Price"], 4).SelectMany(pair => pair)], Headers(report));
        Assert.Equal(["Sep", "Oct", "(blank)", "Grand Total"], report.HeaderSpans.Where(s => s.Tier == 1).OrderBy(s => s.FirstColumn).Select(s => s.Label));
        // The declared order puts West first, spelled as its record spells it.
        Assert.Equal("i west ||  |  |  |  | -2.5 |  | -2.5 |", Lines(report)[0]);
        Assert.Equal("i East || 10.25 | 99.5 |  |  |  |  | 10.25 | 99.5", Lines(report)[1]);
    }

    [Fact] // ADR-0064: a name is a field's and its column's, unique among both; a part needs a Date field; a key a Text or Integer one
    public void What_a_declaration_refuses()
    {
        Assert.Throws<ArgumentException>(() => PivotFields.Of<Trade>().Text("Region", t => t.Region).Number("Region", t => t.Pnl));
        Assert.Throws<ArgumentException>(() => PivotFields.Of<Trade>().Key("Id", t => t.Id).Text("Id", t => t.Region));
        Assert.Throws<ArgumentException>(() => PivotFields.Of<Trade>().Date("Traded", t => t.Traded).Month("Traded", of: "Traded"));
        Assert.Throws<ArgumentException>(() => PivotFields.Of<Trade>().Number("Pnl", t => t.Pnl).Year("Year", of: "Pnl"));
        Assert.Throws<ArgumentException>(() => PivotFields.Of<Trade>().Number("Pnl", t => t.Pnl).Key("Pnl"));
        Assert.Throws<ArgumentException>(() => PivotFields.Of<Trade>().Key("Missing"));
        Assert.Throws<ArgumentNullException>(() => PivotFields.Of<Trade>().Text("Region", null!));
        // A refused declaration leaves nothing behind: the name is free again.
        var fields = PivotFields.Of<Trade>();
        Assert.Throws<ArgumentNullException>(() => fields.Text("Region", null!));
        fields.Text("Region", t => t.Region);
        Assert.Equal(["Region"], fields.Fields.Select(f => f.Name));
    }

    [Fact] // ADR-0064 (DA-6): a value that cannot be read fails the build whole, naming the row and the column
    public void A_value_that_cannot_be_read_fails_the_build_by_name()
    {
        var fields = PivotFields.Of<Trade>().Text("Region", t => t.Id == "T2" ? throw new InvalidOperationException("no region") : t.Region);

        var refusal = Assert.Throws<SnapshotException>(() => PivotSource.From(Trades, fields));

        Assert.Equal(2, refusal.Row);
        Assert.Equal("Region", refusal.Column);
    }

    [Fact] // ADR-0064/0066 (PV-27): the records are read in slices, yielding between them and reporting progress; a cancelled read builds nothing
    public async Task The_records_are_read_in_slices()
    {
        var many = Enumerable.Range(0, 5_000).Select(i => Trades[i % 3] with { Id = "T" + i }).ToArray();
        var yields = 0;
        var progress = new List<SnapshotProgress>();
        var slicing = new PivotSlicing { Budget = TimeSpan.Zero, Yield = _ => { yields++; return ValueTask.CompletedTask; } };

        var source = await PivotSource.FromAsync(many, Declarations(), slicing, new Collector(progress), Ct);

        Assert.True(yields > 2, $"{yields} yields");
        Assert.Equal(new SnapshotProgress(5_000, 5_000), progress[^1]);
        Assert.Equal(5_000, source.Snapshot.RowCount);
        SameAnswer(
            await PivotSource.From(many, Declarations()).AggregateAsync(new PivotQuery(rows: [F("Region")], values: [V("Pnl")]), Ct),
            await source.AggregateAsync(new PivotQuery(rows: [F("Region")], values: [V("Pnl")]), Ct),
            sameVersion: false);

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var cancelling = slicing with { Yield = _ => { cancellation.Cancel(); return ValueTask.CompletedTask; } };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PivotSource.FromAsync(many, Declarations(), cancelling, cancellationToken: cancellation.Token).AsTask());
    }

    [Fact] // ADR-0064/0067: the declarations read a Change Batch through the same columns, and the source takes it
    public async Task A_batch_is_read_through_the_declarations()
    {
        var fields = Declarations();
        var source = PivotSource.From(Trades, fields);

        source.Apply(fields.Batch(added: [Trades[0] with { Id = "T4", Pnl = 1m }], changed: [Trades[1] with { Pnl = 2.5m }], removedKeys: ["T3"]));
        var answer = await source.AggregateAsync(new PivotQuery(values: [V("Pnl", PivotParts.Sum)]), Ct);

        Assert.Equal(PivotNumber.Exact(13.75m), answer.Values[0].SumAt(0));
        Assert.Equal(3, answer.RecordsAt(0));
    }

    private sealed class Collector(List<SnapshotProgress> seen) : IProgress<SnapshotProgress>
    {
        public void Report(SnapshotProgress value) => seen.Add(value);
    }

    // ---- The README's example, as it is written there ---------------------------------------------

    private sealed record ReadmeTrade(string Id, string Region, string Desk, string Tenor, DateOnly TradeDate, decimal Pnl, double Price);

    private static class Tenors
    {
        // Months to maturity: "1Y6M" is 18, as "18M" is, and the two stand side by side.
        public static IComparable? Months(string tenor)
        {
            if (tenor is "ON" or "TN")
                return tenor == "ON" ? -2m : -1m;
            decimal months = 0, number = 0;
            foreach (var c in tenor)
            {
                if (char.IsAsciiDigit(c))
                {
                    number = (number * 10) + (c - '0');
                    continue;
                }
                var unit = c switch { 'W' => 0.25m, 'M' => 1m, 'Y' => 12m, _ => 0m };
                if (unit == 0 || number == 0)
                    return null;   // not a tenor: no key, so after the tenors
                months += number * unit;
                number = 0;
            }
            return number == 0 && months > 0 ? months : null;
        }
    }

    [Fact] // ADR-0060/0064/0066: the README's declarations — the standard way — declare, order and answer as it says
    public async Task The_readmes_declarations_answer_as_it_says()
    {
        var fields = PivotFields.Of<ReadmeTrade>()
            .Key("Id", t => t.Id)
            .Text("Region", t => t.Region)
            .Text("Desk", t => t.Desk, itemOrder: ["Rates", "Credit"])
            .Text("Tenor", t => t.Tenor, orderKey: Tenors.Months)
            .Date("TradeDate", t => t.TradeDate, caption: "Trade date")
            .Month("Month", of: "TradeDate")
            .Number("Pnl", t => t.Pnl, caption: "P&L", format: "#,##0.00")
            .Number("Price", t => t.Price);
        ReadmeTrade[] trades =
        [
            new("T-1", "EMEA", "FX", "1Y6M", new DateOnly(2026, 2, 1), 10m, 99.5),
            new("T-2", "EMEA", "Rates", "ON", new DateOnly(2026, 1, 5), -2.5m, 100.25),
            new("T-3", "APAC", "Credit", "18M", new DateOnly(2026, 2, 9), 4m, 101),
            new("T-4", "APAC", "Rates", "1W", new DateOnly(2026, 1, 30), 1m, 98),
            new("T-1042", "APAC", "Rates", "Other", new DateOnly(2026, 3, 1), 7m, 97),
        ];
        var source = PivotSource.From(trades, fields);
        var tenors = new PivotLayout { Rows = [P("Tenor")], Columns = [P("Month")], Values = [Sum("Pnl")] };
        var desks = new PivotLayout { Rows = [P("Desk")], Values = [Sum("Pnl")] };

        var report = await SnapshotSourceTests.ReportOf(source, tenors);
        source.Apply(fields.Batch(added: [trades[0] with { Id = "T-5", Tenor = "TN" }], removedKeys: ["T-1042"]));
        var live = await SnapshotSourceTests.ReportOf(source, tenors);

        Assert.Equal(["Row Labels", "Jan", "Feb", "Mar", "Grand Total"], Headers(report));
        Assert.Equal(["ON", "1W", "18M", "1Y6M", "Other"], report.Rows.Where(r => r.Role == PivotRowRole.Item).Select(r => r.Labels[0].Text!));
        Assert.Equal(["ON", "TN", "1W", "18M", "1Y6M"], live.Rows.Where(r => r.Role == PivotRowRole.Item).Select(r => r.Labels[0].Text!));
        Assert.Equal(["Rates", "Credit", "FX"], (await SnapshotSourceTests.ReportOf(source, desks)).Rows.Where(r => r.Role == PivotRowRole.Item).Select(r => r.Labels[0].Text!));
    }
}
