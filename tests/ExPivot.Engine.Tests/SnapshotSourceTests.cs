using System.Globalization;
using ExGrid.Data;
using ExPivot.Engine;
using Xunit;
using static ExPivot.Engine.Tests.Pivot;
using static ExPivot.Engine.Tests.Sources;

namespace ExPivot.Engine.Tests;

/// <summary>
/// The bundled source over a Snapshot (ADR-0063/0065): its fields from the columns or declared by
/// column name, Items from each column's kind (ADR-0059, PV-3), every Aggregation read from typed
/// columns with money summed exactly across slices of different scales (PV-4), and the same
/// answers as the records' source over the same data (PV-22).
/// </summary>
public class SnapshotSourceTests
{
    internal sealed record Row(string? Text, decimal? Money, double? Measure, long? Count, DateTime? When, bool? Flag);

    private static SnapshotBuilder<Row> Columns() => new SnapshotBuilder<Row>()
        .Text("Text", r => r.Text, caption: "Label")
        .Decimal("Money", r => r.Money)
        .Double("Measure", r => r.Measure)
        .Integer("Count", r => r.Count)
        .Date("When", r => r.When)
        .Boolean("Flag", r => r.Flag);

    private static Snapshot Rows(params Row[] rows) => Columns().Build(rows);

    /// <summary>The report <paramref name="layout"/> makes from <paramref name="source"/>'s answer.</summary>
    internal static async Task<PivotReport> ReportOf(PivotSource source, PivotLayout layout, PivotOptions? options = null)
    {
        var query = PivotQuery.For(layout);
        var answer = await source.AggregateAsync(query, Ct);
        Assert.False(answer.IsRefused, answer.Refusal?.Message);
        return PivotEngine.Report(PivotEngine.Cube(query, answer, source.Fields), layout, options ?? EnUs);
    }

    private static string[] Labels(PivotReport report)
        => report.Rows.Where(r => r.Role != PivotRowRole.GrandTotal).Select(r => r.Labels[0].Text!).ToArray();

    [Fact] // ADR-0065: with no fields declared, one field per column, captioned as the column is and typed by its kind
    public void Fields_default_from_the_columns()
    {
        var source = PivotSource.From(Rows());

        Assert.Equal(
        [
            ("Text", "Label", PivotFieldType.Text), ("Money", "Money", PivotFieldType.Number), ("Measure", "Measure", PivotFieldType.Number),
            ("Count", "Count", PivotFieldType.Number), ("When", "When", PivotFieldType.Date), ("Flag", "Flag", PivotFieldType.Boolean),
        ], source.Fields.Select(f => (f.Name, f.Caption, f.Type)));
        Assert.All(source.Fields, f => Assert.Null(f.Column));
    }

    [Fact] // ADR-0065: declared fields name their columns, by their own name or another's; settings of their own are kept
    public async Task Declared_fields_name_their_columns()
    {
        PivotField[] fields =
        [
            new("Desk", PivotFieldType.Text, caption: "Trading desk") { Column = "Text" },
            new("Money", PivotFieldType.Number, format: "#,##0.00"),
        ];
        var source = PivotSource.From(Rows(new("A", 1.5m, null, null, null, null), new("a", 2m, null, null, null, null)), fields);

        var report = await ReportOf(source, new PivotLayout { Rows = [P("Desk")], Values = [Sum("Money")] });

        Assert.Same(fields[0], source.Fields[0]);
        Assert.Equal(["i A || 3.5", "t Grand Total || 3.5"], Lines(report));
        Assert.Equal("Sum of Money", Headers(report)[1]);
    }

    [Fact] // ADR-0065: a field naming a column the Snapshot does not have is refused by name, and so is a date part of another kind
    public void An_unknown_column_is_refused_by_name()
    {
        var snapshot = Rows();

        var unknown = Assert.Throws<ArgumentException>(() => PivotSource.From(snapshot, [new PivotField("Region", PivotFieldType.Text)]));
        var other = Assert.Throws<ArgumentException>(() => PivotSource.From(snapshot, [new PivotField("Desk", PivotFieldType.Text) { Column = "Desks" }]));
        var part = Assert.Throws<ArgumentException>(() => PivotSource.From(snapshot, [PivotField.DatePartOf("Month", "Money", PivotDatePart.Month)]));
        var twice = Assert.Throws<ArgumentException>(() => PivotSource.From(snapshot, [new PivotField("Money", PivotFieldType.Number), new PivotField("Money", PivotFieldType.Number)]));

        Assert.Contains("'Region'", unknown.Message);
        Assert.Contains("'Desks'", other.Message);
        Assert.Contains("'Desk'", other.Message);
        Assert.Contains("'Money'", part.Message);
        Assert.Contains("not a Date column", part.Message);
        Assert.Contains("'Money'", twice.Message);
    }

    [Fact] // ADR-0059/0063 (PV-3): text Items ignore case and are labelled by the first spelling; a Blank in every kind is (blank)
    public async Task Text_items_ignore_case_and_blanks_are_blank_in_every_kind()
    {
        var source = PivotSource.From(Rows(
            new("east", 1m, 1, 1, new DateTime(2026, 1, 1), true),
            new("East", 2m, 2, 2, new DateTime(2026, 1, 1), true),
            new(null, null, null, null, null, null),
            new("", 4m, 4, 4, new DateTime(2026, 1, 2), false)));

        foreach (var field in new[] { "Text", "Money", "Measure", "Count", "When", "Flag" })
        {
            var report = await ReportOf(source, new PivotLayout { Rows = [P(field)], Values = [Value("Text", PivotAggregation.Count)] });
            Assert.Equal("(blank)", Labels(report)[^1]);
        }
        var text = await ReportOf(source, new PivotLayout { Rows = [P("Text")], Values = [Sum("Money")] });
        Assert.Equal(["i  || 4", "i east || 3", "i (blank) ||", "t Grand Total || 7"], Lines(text));
    }

    [Fact] // ADR-0059/0063 (PV-3): a number is its value — in every slice, whatever scale it holds it at, and as a Hidden Item's key
    public async Task A_number_is_its_value_in_every_slice()
    {
        var fields = PivotFields.Of<Row>().Number("Money", r => r.Money).Number("Measure", r => r.Measure).Number("Count", r => r.Count).Key("Text", r => r.Text);
        var source = PivotSource.From([new Row("a", 1.5m, 1.5, 1, null, null), new Row("b", 2m, 2, 2, null, null)], fields);
        // A batch's slice holds its values at a scale of its own: four places here.
        source.Apply(fields.Batch(added: [new Row("c", 1.50m, 1.5, 1, null, null), new Row("d", 0.0001m, 0.0001, 3, null, null)]));

        foreach (var field in new[] { "Money", "Measure" })
        {
            var report = await ReportOf(source, new PivotLayout { Rows = [P(field)], Values = [Value(field, PivotAggregation.Count)] });
            Assert.Equal(["i 0.0001 || 1", "i 1.5 || 2", "i 2 || 1", "t Grand Total || 4"], Lines(report));
            var hidden = await ReportOf(source, new PivotLayout { Rows = [P(field) with { HiddenItems = [PivotItemKey.Number(1.5)] }], Values = [Value(field, PivotAggregation.Count)] });
            Assert.Equal(["i 0.0001 || 1", "i 2 || 1", "t Grand Total || 2"], Lines(hidden));
        }
        // An Integer 1, a Decimal 1.0 and a Double 1 are one Item: the same key in every kind.
        var answer = await source.AggregateAsync(new PivotQuery(rows: [F("Count")]), Ct);
        Assert.Contains(PivotItemKey.For(1.0m), answer.Rows[0].Items);
        Assert.Contains(PivotItemKey.For(1.0), answer.Rows[0].Items);
    }

    [Fact] // ADR-0059/0063 (PV-3): a non-finite Double is #NUM!, after the Booleans and before (blank)
    public async Task A_non_finite_double_is_the_error_item()
    {
        var source = PivotSource.From(Rows(
            new("a", 1m, double.NaN, null, null, null),
            new("b", 1m, double.PositiveInfinity, null, null, null),
            new("c", 1m, double.NegativeInfinity, null, null, null),
            new("d", 1m, -0.0, null, null, null),
            new("e", 1m, null, null, null, null)));

        var report = await ReportOf(source, new PivotLayout { Rows = [P("Measure")], Values = [Sum("Money")] });

        Assert.Equal(["i 0 || 1", "i #NUM! || 3", "i (blank) || 1", "t Grand Total || 5"], Lines(report));
        Assert.Equal("#NUM!", Cell(await ReportOf(source, new PivotLayout { Values = [Sum("Measure")] }), 0, 0));
    }

    [Fact] // ADR-0059/0063 (PV-3): a date is its clock value: the Kind of a DateTime is ignored, and a time of day is another Item
    public async Task A_date_is_its_clock_value()
    {
        var source = PivotSource.From(Rows(
            new("a", 1m, null, null, new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc), null),
            new("b", 2m, null, null, new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Local), null),
            new("c", 4m, null, null, new DateTime(2026, 1, 2, 13, 45, 0), null)));

        var report = await ReportOf(source, new PivotLayout { Rows = [P("When")], Values = [Sum("Money")] });

        Assert.Equal(["1/2/2026", new DateTime(2026, 1, 2, 13, 45, 0).ToString("G", EnUs.Culture)], Labels(report));
        Assert.Equal("3", Cell(report, 0, 0));
    }

    [Fact] // ADR-0059/0065 (PV-3): a source lists Items in its own order; the report puts (blank) last in either direction
    public async Task A_text_field_keeps_blank_last_either_way()
    {
        var source = PivotSource.From([1, 2, 3, 4, 5, 6], PivotFields.Of<int>().Text("Kind", i => i switch
        {
            1 => "b",
            2 => "A",
            _ => null,
        }));
        var answer = await source.AggregateAsync(new PivotQuery(rows: [F("Kind")]), Ct);

        SameKeys([PivotItemKey.Text("b"), PivotItemKey.Text("A"), PivotItemKey.Blank], answer.Rows[0].Items);
        var report = await ReportOf(source, new PivotLayout { Rows = [P("Kind") with { Sort = PivotSort.Descending }], Values = [Value("Kind", PivotAggregation.Count)] });
        Assert.Equal(["b", "A", "(blank)"], Labels(report));
    }

    [Fact] // ADR-0059/0063 (PV-4): Integer and Decimal are summed exactly across slices held at different scales, and as decimals where 64 bits cannot hold them
    public async Task Money_is_summed_exactly_across_slices_of_different_scales()
    {
        var fields = PivotFields.Of<Row>().Text("Text", r => r.Text).Number("Money", r => r.Money).Number("Count", r => r.Count).Key("Text");
        Row[] start = [new("a", 0.10m, null, long.MaxValue, null, null), new("b", 0.20m, null, long.MaxValue, null, null)];
        var source = PivotSource.From(start, fields);
        var held = new List<Row>(start);
        // One batch at four places, one at six, one past 64 bits at its scale, one negative.
        Row[][] batches =
        [
            [new("c", 0.0001m, null, 3, null, null)],
            [new("d", 1234.567891m, null, -7, null, null)],
            [new("e", 12345678901.123456789m, null, long.MinValue, null, null)],
            [new("f", -0.3m, null, 0, null, null)],
        ];
        foreach (var batch in batches)
        {
            source.Apply(fields.Batch(added: batch));
            held.AddRange(batch);
        }

        var answer = await source.AggregateAsync(new PivotQuery(values: [V("Money"), V("Count")]), Ct);

        Assert.Equal(PivotNumber.Exact(held.Sum(r => r.Money!.Value)), answer.Values[0].SumAt(0));
        Assert.Equal(PivotNumber.Exact(held.Aggregate(0m, (sum, r) => sum + r.Count!.Value)), answer.Values[1].SumAt(0));
        Assert.Equal(PivotNumber.Exact(-0.3m), answer.Values[0].MinAt(0));
        Assert.Equal(PivotNumber.Exact(12345678901.123456789m), answer.Values[0].MaxAt(0));
        Assert.Equal(PivotNumber.Exact(long.MinValue), answer.Values[1].MinAt(0));
        // 0.1 + 0.2 in decimal is 0.3; held as a value, without the scale it was summed at.
        var tenths = await PivotSource.From(start, fields).AggregateAsync(new PivotQuery(values: [V("Money", PivotParts.Sum)]), Ct);
        Assert.Equal("0.3", tenths.Values[0].SumAt(0).ExactValue.ToString(CultureInfo.InvariantCulture));
        Assert.Equal(PivotNumber.Exact(0.3m), tenths.Values[0].SumAt(0));
    }

    [Fact] // ADR-0059 (PV-4): an exact sum that leaves decimal's range falls back to double, Excel's arithmetic, rather than failing
    public async Task An_exact_sum_past_decimals_range_is_a_double()
    {
        var source = PivotSource.From(Rows(new("a", decimal.MaxValue, null, null, null, null), new("b", decimal.MaxValue, null, null, null, null)));

        var answer = await source.AggregateAsync(new PivotQuery(values: [V("Money", PivotParts.Sum)]), Ct);

        Assert.Equal(PivotNumber.Double(2 * (double)decimal.MaxValue), answer.Values[0].SumAt(0));
    }

    [Theory] // ADR-0059 (PV-4): Text, Date and Boolean in Values are counted and never summed — Sum is 0, as the table says
    [InlineData("Text")]
    [InlineData("When")]
    [InlineData("Flag")]
    public async Task Text_dates_and_booleans_in_values_are_counted_and_not_summed(string field)
    {
        var source = PivotSource.From(Rows(
            new("a", 1m, 1, 1, new DateTime(2026, 1, 1), true),
            new("b", 1m, 1, 1, new DateTime(2026, 1, 2), false),
            new(null, 1m, 1, 1, null, null)));
        var layout = new PivotLayout
        {
            Values = [.. Enum.GetValues<PivotAggregation>().Select(a => Value(field, a))],
        };

        var report = await ReportOf(source, layout);

        Assert.Equal(["t  || 0 | 2 | #DIV/0! | 0 | 0 | 0 | 0 | #DIV/0! | #DIV/0! | #DIV/0! | #DIV/0!"], Lines(report));
    }

    [Fact] // ADR-0059/0065 (PV-4): only the parts asked for are accumulated, from typed columns
    public async Task Only_the_parts_asked_for_are_accumulated()
    {
        var source = PivotSource.From(Rows(new Row("a", 1m, 2, 3, null, null)));

        var answer = await source.AggregateAsync(new PivotQuery(values: [V("Money", PivotParts.Sum), V("Measure", PivotParts.Extremes), V("Count", PivotParts.Counts)]), Ct);

        Assert.Equal([PivotParts.Sum, PivotParts.Extremes, PivotParts.Counts], answer.Values.Select(v => v.Parts));
        Assert.Throws<InvalidOperationException>(() => answer.Values[0].MinAt(0));
        Assert.Throws<InvalidOperationException>(() => answer.Values[1].SumAt(0));
        Assert.Throws<InvalidOperationException>(() => answer.Values[2].SumAt(0));
    }

    [Fact] // ADR-0063/0065 (PV-4): a question over typed columns boxes no value — what it allocates does not grow with the rows
    public async Task A_question_over_columns_boxes_nothing()
    {
        static Row Make(int i) => new("R" + (i % 4).ToString(CultureInfo.InvariantCulture), i / 100m, i * 0.5, i, new DateTime(2026, 1, 1).AddDays(i % 7), i % 2 == 0);
        var small = Rows([.. Enumerable.Range(0, 100_000).Select(Make)]);
        var large = Rows([.. Enumerable.Range(0, 200_000).Select(Make)]);
        var query = new PivotQuery(rows: [F("Text")], columns: [F("When"), F("Flag")], values: [V("Money"), V("Measure"), V("Count")]);
        var slicing = new PivotSlicing { Budget = TimeSpan.FromDays(1) };

        long Allocated(Snapshot snapshot)
        {
            var source = PivotSource.From(snapshot, slicing: slicing);
            var before = GC.GetAllocatedBytesForCurrentThread();
            var answer = source.AggregateAsync(query, Ct);
            Assert.True(answer.IsCompletedSuccessfully, "a question that never yields completes on the calling thread");
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(28, answer.Result.LeafCount);
            return allocated;
        }

        Allocated(small); // warm
        var grown = Allocated(large) - Allocated(small);

        // A box is 24 bytes: a hundred thousand more rows boxed would be 2.4 MB a column.
        Assert.True(grown < 64 * 1024, $"a hundred thousand more rows allocated {grown:N0} bytes more");
    }

    [Fact] // ADR-0065 (PV-22): the typed declarations and the untyped accessors answer alike over the same records
    public async Task Typed_and_untyped_declarations_answer_alike()
    {
        var typed = PivotFields.Of<Sale>()
            .Text("Region", s => s.Region)
            .Text("Product", s => s.Product)
            .Date("Date", s => s.Date)
            .Number("Amount", s => s.Amount)
            .Number("Quantity", s => s.Quantity)
            .Boolean("Online", s => s.Online);
        var fromTyped = PivotSource.From(Sales, typed);
        var fromAccessors = PivotSource.From(Sales, Fields);

        foreach (var query in new[]
                 {
                     new PivotQuery(rows: [F("Region"), F("Product")], columns: [F("Online")], values: [V("Amount"), V("Quantity")]),
                     new PivotQuery(rows: [F("Date")], filters: [F("Region", PivotItemKey.Text("east"))], values: [V("Amount", PivotParts.Sum)]),
                     new PivotQuery(columns: [F("Quantity")], values: [V("Online"), V("Date")]),
                 })
        {
            SameAnswer(await fromAccessors.AggregateAsync(query, Ct), await fromTyped.AggregateAsync(query, Ct), sameVersion: false);
        }
    }

    [Fact] // ADR-0065 (PV-22): a source answering through Fetch from the same Snapshot gives the same Leaf Aggregates, Items and Details, after batches too
    public async Task Fetch_over_a_snapshot_answers_as_from_does()
    {
        var fields = LiveDataTests.Declarations();
        var reference = PivotSource.From(
        [
            LiveDataTests.Row(1, "Rates", 1.25m), LiveDataTests.Row(2, "RATES", -3m) with { Risk = double.NaN },
            LiveDataTests.Row(3, null, null) with { Date = null }, LiveDataTests.Row(4, "Credit", 0.0001m) with { Book = "ny-2", Live = false },
        ], fields);
        var fetched = OverJson(reference);
        PivotQuery[] questions =
        [
            new(rows: [F("Desk")], columns: [F("Month")], values: [V("Amount"), V("Risk"), V("Quantity", PivotParts.Sum)]),
            new(rows: [F("Book", PivotItemKey.Text("LDN-1"))], filters: [F("Live", PivotItemKey.Blank)], values: [V("Desk", PivotParts.Counts), V("Date", PivotParts.Counts)]),
            new(columns: [F("Risk")], values: [V("Amount", PivotParts.Sum | PivotParts.Extremes)]),
        ];

        foreach (var batch in new[]
                 {
                     fields.Batch(),
                     fields.Batch(added: [LiveDataTests.Row(5, "fx", 12.5m)], changed: [LiveDataTests.Row(2, "Rates", 2m)], removedKeys: [3L]),
                 })
        {
            reference.Apply(batch);
            foreach (var query in questions)
            {
                var expected = await reference.AggregateAsync(query, Ct);
                SameAnswer(expected, await fetched.AggregateAsync(query, Ct));
                foreach (var field in query.Placed.Select(f => f.Field))
                    SameItems(await reference.ItemsAsync(new PivotItemsQuery(field, expected.SourceVersion), Ct), await fetched.ItemsAsync(new PivotItemsQuery(field, expected.SourceVersion), Ct));
                var layout = new PivotLayout
                {
                    Rows = [.. query.Rows.Select(f => new PivotFieldPlacement(f.Field) { HiddenItems = f.HiddenItems })],
                    Columns = [.. query.Columns.Select(f => new PivotFieldPlacement(f.Field) { HiddenItems = f.HiddenItems })],
                    Filters = [.. query.Filters.Select(f => new PivotFieldPlacement(f.Field) { HiddenItems = f.HiddenItems })],
                    Values = [.. query.Values.Select(v => Value(v.Field, PivotAggregation.Count))],
                };
                var report = PivotEngine.Report(PivotEngine.Cube(PivotQuery.For(layout), await reference.AggregateAsync(PivotQuery.For(layout), Ct), reference.Fields), layout, EnUs);
                foreach (var row in report.Rows)
                {
                    for (var column = -1; column < report.ValueColumns.Count; column++)
                    {
                        var details = report.DetailsQuery(row, column);
                        SameDetails(await reference.DetailsAsync(details, Ct), await fetched.DetailsAsync(details, Ct));
                    }
                }
            }
        }
    }

    [Fact] // ADR-0062/0065: the records behind a cell carry each field's value by its kind, and the Consumer's own object
    public async Task The_records_behind_a_cell_carry_their_values_and_objects()
    {
        Row[] rows = [new("a", 1.50m, 0.5, 3, new DateTime(2026, 1, 2), true), new("b", null, null, null, null, null)];
        var source = PivotSource.From(Columns().Build(rows));
        var version = (await source.AggregateAsync(new PivotQuery(), Ct)).SourceVersion;

        var page = await source.DetailsAsync(new PivotDetailsQuery(version, rowItems: [new("Text", PivotItemKey.Text("A"))]), Ct);

        var record = Assert.Single(page.Records);
        Assert.Same(rows[0], record.Record);
        Assert.Equal<object?>(["a", 1.5m, 0.5, 3m, new DateTime(2026, 1, 2), true], record.Values);
        Assert.Equal(["Text", "Money", "Measure", "Count", "When", "Flag"], page.Fields.Select(f => f.Name));
    }

    [Fact] // ADR-0065 (PV-27): over a Snapshot, a question works in slices and stops at the next slice when cancelled
    public async Task A_snapshot_question_works_in_slices_and_stops_when_cancelled()
    {
        var snapshot = Rows([.. Enumerable.Range(0, 10).Select(i => new Row("R" + (i % 3).ToString(CultureInfo.InvariantCulture), i, null, null, null, null))]);
        var yields = 0;
        long read = 0;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var source = PivotSource.From(snapshot, slicing: new PivotSlicing
        {
            Budget = TimeSpan.Zero,
            RecordsPerCheck = 3,
            Yield = _ =>
            {
                if (++yields == 2)
                    cancellation.Cancel();
                return ValueTask.CompletedTask;
            },
            RowsRead = rows => read += rows,
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => source.AggregateAsync(new PivotQuery(rows: [F("Text")]), cancellation.Token).AsTask());

        // Two slices of three rows, and not one more.
        Assert.Equal(6, read);
    }
}
