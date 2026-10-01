using ExPivot.Engine;
using Xunit;
using static ExPivot.Engine.Tests.Pivot;

namespace ExPivot.Engine.Tests;

/// <summary>The report from Leaf Aggregates (ADR-0059/0065): every total merged from the leaves'
/// parts is the Aggregation of its own records, only the parts asked for travel, and a held answer
/// is laid out again unless an Aggregation needs a part it lacks.</summary>
public class LeafAggregateTests
{
    private sealed record Obs(string? Region, string Desk, string Product, object? Value);

    //  Region   Desk Product  Value
    //  East     A    Apples   1 (int), 2.5 (decimal), 0.25 (double), (blank)
    //  East     B    Pears    "text", TRUE, a date, 4 (long)
    //  East     A    Plums    (blank), (blank)
    //  West     B    Apples   10, 20.25 (decimal), 30 (int)
    //  West     A    Pears    5, NaN
    //  West     B    Plums    2, 2, 2, 2 (decimal)
    //  North    A    Apples   1.25, 2.5, -3.75 (double)
    //  North    B    Pears    7
    //  North    A    Plums    "a", "b"
    //  (blank)  B    Plums    0.10 (decimal)
    //  (blank)  A    Apples   decimal.MaxValue, 1 — an exact sum that overflows
    private static readonly Obs[] Observations =
    [
        new("East", "A", "Apples", 1), new("East", "A", "Apples", 2.5m), new("East", "A", "Apples", 0.25), new("East", "A", "Apples", null),
        new("East", "B", "Pears", "text"), new("East", "B", "Pears", true), new("East", "B", "Pears", new DateTime(2026, 1, 1)), new("East", "B", "Pears", 4L),
        new("East", "A", "Plums", null), new("East", "A", "Plums", null),
        new("West", "B", "Apples", 10m), new("West", "B", "Apples", 20.25m), new("West", "B", "Apples", 30),
        new("West", "A", "Pears", 5), new("West", "A", "Pears", double.NaN),
        new("West", "B", "Plums", 2m), new("West", "B", "Plums", 2m), new("West", "B", "Plums", 2m), new("West", "B", "Plums", 2m),
        new("North", "A", "Apples", 1.25), new("North", "A", "Apples", 2.5), new("North", "A", "Apples", -3.75),
        new("North", "B", "Pears", 7),
        new("North", "A", "Plums", "a"), new("North", "A", "Plums", "b"),
        new(null, "B", "Plums", 0.10m), new(null, "A", "Apples", decimal.MaxValue), new(null, "A", "Apples", 1m),
    ];

    private static readonly PivotField<Obs>[] ObsFields =
    [
        new("Region", PivotFieldType.Text, o => o.Region),
        new("Desk", PivotFieldType.Text, o => o.Desk),
        new("Product", PivotFieldType.Text, o => o.Product),
        new("Value", PivotFieldType.Number, o => o.Value),
    ];

    private static readonly Dictionary<string, Func<Obs, object?>> Read = ObsFields.ToDictionary(f => f.Name, f => f.Value);

    [Theory] // ADR-0059/0065 (PV-4): every cell, subtotal and grand total from the leaves is the Aggregation of its own records
    [InlineData(PivotReportForm.Compact, true)]
    [InlineData(PivotReportForm.Outline, false)]
    [InlineData(PivotReportForm.Tabular, true)]
    public void Every_total_from_the_leaves_is_the_aggregation_of_its_records(PivotReportForm form, bool subtotalsAtTop)
    {
        var layout = new PivotLayout
        {
            Rows = [P("Region"), P("Desk")],
            Columns = [P("Product")],
            Values = [.. Enum.GetValues<PivotAggregation>().Select(a => Value("Value", a))],
            Form = form,
            SubtotalsAtTop = subtotalsAtTop,
        };
        var report = PivotEngine.Compute(Observations, ObsFields, layout, EnUs);
        var cells = 0;

        foreach (var row in report.Rows.Where(r => r.CarriesValues))
        {
            for (var column = 0; column < report.ValueColumns.Count; column++)
            {
                var path = report.RowPath(row).Concat(report.ColumnPath(column)).ToArray();
                var records = Observations.Where(o => path.All(step => PivotItemKey.For(Read[step.Field](o)).Equals(step.Item))).ToArray();
                var aggregation = layout.Values[report.ValueFieldAt(row, column)].Aggregation;
                var expected = Oracle(records.Select(o => o.Value).ToArray(), aggregation);
                var actual = row.ValueAt(column);
                var where = $"{aggregation} at [{string.Join(", ", path.Select(p => p.Item))}]";
                switch (expected)
                {
                    case null:
                        Assert.True(actual is null, where + ": empty, not " + actual);
                        break;
                    case string error:
                        Assert.True(actual?.Error == error, $"{where}: {error}, not {actual}");
                        break;
                    case decimal exact:
                        Assert.True(actual?.Exact == exact, $"{where}: exactly {exact}, not {actual?.Exact?.ToString() ?? actual?.ToString()}");
                        break;
                    case double number:
                        Assert.True(actual is { Exact: null, IsError: false }, $"{where}: a double, not {actual}");
                        Assert.Equal(number, actual!.Number, Math.Max(1, Math.Abs(number)) * 1e-12);
                        break;
                }
                cells++;
            }
        }
        // Leaves, subtotals and grand totals, for each of the eleven Aggregations.
        Assert.True(cells > 11 * 30, $"{cells} cells checked");
    }

    // The table of ADR-0059, over a cell's records, computed from them directly: null for an empty
    // cell, a string for an error value, a decimal for an exact answer, a double otherwise.
    private static object? Oracle(IReadOnlyList<object?> values, PivotAggregation aggregation)
    {
        var present = values.Where(v => v is not null).ToArray();
        if (present.Length == 0)
            return null;
        if (aggregation == PivotAggregation.Count)
            return (decimal)present.Length;
        var numbers = present.Where(v => v is int or long or short or byte or decimal or double or float).ToArray();
        if (aggregation == PivotAggregation.CountNumbers)
            return (decimal)numbers.Length;
        if (numbers.Length == 0)
            return aggregation is PivotAggregation.Sum or PivotAggregation.Max or PivotAggregation.Min or PivotAggregation.Product ? 0m : "#DIV/0!";
        if (numbers.Any(n => n is double d && !double.IsFinite(d)))
            return "#NUM!";
        var exact = numbers.All(n => n is not (double or float));
        var doubles = numbers.Select(Convert.ToDouble).ToArray();
        decimal? exactSum = null;
        if (exact)
        {
            try
            {
                exactSum = numbers.Aggregate(0m, (sum, n) => sum + Convert.ToDecimal(n));
            }
            catch (OverflowException)
            {
                // Exact until it cannot be: then double (ADR-0059).
            }
        }
        var n = doubles.Length;
        var mean = doubles.Average();
        var m2 = doubles.Sum(x => (x - mean) * (x - mean));
        object result = aggregation switch
        {
            PivotAggregation.Sum => exactSum is { } sum ? sum : doubles.Sum(),
            PivotAggregation.Average => exactSum is { } sum ? sum / n : doubles.Sum() / n,
            PivotAggregation.Max => exact ? numbers.Max(Convert.ToDecimal) : doubles.Max(),
            PivotAggregation.Min => exact ? numbers.Min(Convert.ToDecimal) : doubles.Min(),
            PivotAggregation.Product => doubles.Aggregate(1.0, (product, x) => product * x),
            PivotAggregation.Var => n < 2 ? "#DIV/0!" : m2 / (n - 1),
            PivotAggregation.StdDev => n < 2 ? "#DIV/0!" : Math.Sqrt(m2 / (n - 1)),
            PivotAggregation.Varp => m2 / n,
            PivotAggregation.StdDevp => Math.Sqrt(m2 / n),
            _ => throw new ArgumentOutOfRangeException(nameof(aggregation)),
        };
        return result is double d2 && !double.IsFinite(d2) ? "#NUM!" : result;
    }

    [Fact] // ADR-0059/0065: only the parts asked for are accumulated and travel; a part not asked for is refused, not read as zero
    public async Task Only_the_parts_asked_for_travel()
    {
        var source = PivotSource.From(Sales, Fields);
        var answer = await source.AggregateAsync(new PivotQuery(
            rows: [new("Region")],
            values: [new("Amount", PivotParts.Sum), new("Quantity", PivotParts.Counts), new("Online", PivotParts.Extremes | PivotParts.Variance)]),
            TestContext.Current.CancellationToken);

        Assert.Equal([PivotParts.Sum, PivotParts.Counts, PivotParts.Extremes | PivotParts.Variance], answer.Values.Select(v => v.Parts));
        Assert.Equal(PivotNumber.Exact(180m), answer.Values[0].SumAt(0));
        Assert.Throws<InvalidOperationException>(() => answer.Values[0].MaxAt(0));
        Assert.Throws<InvalidOperationException>(() => answer.Values[1].SumAt(0));
        Assert.Throws<InvalidOperationException>(() => answer.Values[2].ProductAt(0));
        Assert.Equal(3, answer.Values[1].ValuesAt(0));

        // A layout asks for each field once, with the parts of all its Value Fields.
        var layout = new PivotLayout
        {
            Values = [Sum("Amount"), Value("Amount", PivotAggregation.Average), Value("Amount", PivotAggregation.Max), Value("Quantity", PivotAggregation.Count)],
        };
        Assert.Equal([new PivotQueryValue("Amount", PivotParts.Sum | PivotParts.Extremes), new PivotQueryValue("Quantity")], PivotQuery.For(layout).Values);
    }

    [Theory] // ADR-0065: each Aggregation reads the parts the table names
    [InlineData(PivotAggregation.Sum, PivotParts.Sum)]
    [InlineData(PivotAggregation.Average, PivotParts.Sum)]
    [InlineData(PivotAggregation.Max, PivotParts.Extremes)]
    [InlineData(PivotAggregation.Min, PivotParts.Extremes)]
    [InlineData(PivotAggregation.Product, PivotParts.Product)]
    [InlineData(PivotAggregation.StdDev, PivotParts.Variance)]
    [InlineData(PivotAggregation.StdDevp, PivotParts.Variance)]
    [InlineData(PivotAggregation.Var, PivotParts.Variance)]
    [InlineData(PivotAggregation.Varp, PivotParts.Variance)]
    [InlineData(PivotAggregation.Count, PivotParts.Counts)]
    [InlineData(PivotAggregation.CountNumbers, PivotParts.Counts)]
    public void Each_aggregation_reads_its_parts(PivotAggregation aggregation, PivotParts parts)
        => Assert.Equal(parts, PivotQuery.PartsOf(aggregation));

    [Fact] // ADR-0059/0065 (PV-26's engine side): a held answer is laid out again unless an Aggregation needs a part it lacks
    public void A_held_answer_asks_again_only_for_missing_parts()
    {
        var layout = new PivotLayout { Rows = [P("Region")], Columns = [P("Online")], Values = [Sum("Amount")] };
        var sum = PivotEngine.Aggregate(Sales, Fields, layout);

        PivotLayout With(PivotAggregation aggregation) => layout with { Values = [Value("Amount", aggregation)] };
        Assert.True(sum.Holds(With(PivotAggregation.Average)));
        Assert.True(sum.Holds(With(PivotAggregation.Count)));
        Assert.True(sum.Holds(With(PivotAggregation.CountNumbers)));
        Assert.False(sum.Holds(With(PivotAggregation.Max)));
        Assert.False(sum.Holds(With(PivotAggregation.Product)));
        Assert.False(sum.Holds(With(PivotAggregation.StdDev)));
        Assert.False(PivotEngine.CanReuse(sum, Sales, Fields, With(PivotAggregation.Min)));
        Assert.Throws<InvalidOperationException>(() => PivotEngine.Report(sum, With(PivotAggregation.Max)));
        Assert.Equal(PivotParts.Sum, sum.Parts["Amount"]);

        var variance = PivotEngine.Aggregate(Sales, Fields, With(PivotAggregation.Var));
        Assert.True(variance.Holds(With(PivotAggregation.StdDev)));
        Assert.True(variance.Holds(With(PivotAggregation.StdDevp)));
        Assert.True(variance.Holds(With(PivotAggregation.Varp)));
        Assert.False(variance.Holds(With(PivotAggregation.Average)));

        var count = PivotEngine.Aggregate(Sales, Fields, With(PivotAggregation.Count));
        Assert.True(count.Holds(With(PivotAggregation.CountNumbers)));
        Assert.False(count.Holds(With(PivotAggregation.Sum)));

        // Sum to Average from the held answer is the report computed afresh.
        Assert.Equal(Lines(Report(With(PivotAggregation.Average))), Lines(PivotEngine.Report(sum, With(PivotAggregation.Average), EnUs)));
    }

    [Fact] // ADR-0065: an answer carrying more parts than asked holds more Aggregations
    public async Task An_answer_with_more_parts_holds_more()
    {
        var layout = new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount")] };
        var query = PivotQuery.For(layout);
        var richer = new PivotQuery(rows: query.Rows, values: [new("Amount", PivotParts.Sum | PivotParts.Extremes)]);
        var answer = await PivotSource.From(Sales, Fields).AggregateAsync(richer, TestContext.Current.CancellationToken);

        var cube = PivotEngine.Cube(query, answer, Fields);

        Assert.True(cube.Holds(layout with { Values = [Value("Amount", PivotAggregation.Max)] }));
        Assert.Equal(Lines(Report(layout with { Values = [Value("Amount", PivotAggregation.Max)] })),
            Lines(PivotEngine.Report(cube, layout with { Values = [Value("Amount", PivotAggregation.Max)] }, EnUs)));
    }

    [Fact] // ADR-0065: a server computing the Leaf Aggregates itself — SQL's GROUP BY — lays out the bundled source's report
    public void A_server_building_its_own_leaves_lays_out_the_same_report()
    {
        var layout = new PivotLayout
        {
            Rows = [P("Region")],
            Columns = [P("Product")],
            Values = [Sum("Amount"), Value("Amount", PivotAggregation.Max), Value("Quantity", PivotAggregation.Average)],
        };
        var query = PivotQuery.For(layout);
        var builder = new PivotAnswerBuilder(query, "sql-1");
        // SELECT Region, Product, COUNT(*), COUNT(Amount), SUM(Amount), MIN(Amount), MAX(Amount),
        //        COUNT(Quantity), SUM(Quantity) ... GROUP BY Region, Product
        foreach (var group in Sales.GroupBy(s => (s.Region, s.Product)))
        {
            var leaf = builder.AddLeaf([PivotItemKey.For(group.Key.Region), PivotItemKey.For(group.Key.Product)], group.Count());
            builder.SetCounts(leaf, 0, group.Count(), group.Count());
            builder.SetSum(leaf, 0, PivotNumber.Exact(group.Sum(s => s.Amount)));
            builder.SetExtremes(leaf, 0, PivotNumber.Exact(group.Min(s => s.Amount)), PivotNumber.Exact(group.Max(s => s.Amount)));
            builder.SetCounts(leaf, 1, group.Count(), group.Count());
            builder.SetSum(leaf, 1, PivotNumber.Exact(group.Sum(s => s.Quantity)));
        }
        var answer = builder.Build();

        var report = PivotEngine.Report(PivotEngine.Cube(query, answer, Fields), layout, EnUs);

        Assert.Equal(Lines(Report(layout)), Lines(report));
        Assert.Equal("sql-1", report.Cube.SourceVersion);
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact] // ADR-0065: a builder refuses what an answer cannot hold, and a cube an answer with two leaves for one cell
    public void What_an_answer_cannot_hold_is_refused()
    {
        var query = new PivotQuery(rows: [new("Region")], values: [new("Amount", PivotParts.Sum)], maxLeaves: 1);
        var builder = new PivotAnswerBuilder(query, "1");
        var east = builder.AddLeaf([PivotItemKey.Text("East")], 2);

        Assert.Throws<ArgumentException>(() => builder.AddLeaf([PivotItemKey.Text("EAST")], 1));
        Assert.Throws<ArgumentException>(() => builder.AddLeaf([PivotItemKey.Text("West"), PivotItemKey.Blank], 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.AddLeaf([PivotItemKey.Text("West")], 0));
        Assert.Throws<InvalidOperationException>(() => builder.SetExtremes(east, 0, PivotNumber.Exact(1), PivotNumber.Exact(2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.SetCounts(east, 0, 3, 3));
        builder.SetCounts(east, 0, 2, 2);
        builder.SetSum(east, 0, PivotNumber.Exact(1));
        builder.AddLeaf([PivotItemKey.Text("West")], 1);
        Assert.Equal(PivotSourceRefusalKind.TooManyLeaves, builder.Build().Refusal!.Kind);

        var twice = PivotJson.ReadAnswer(
            """{"version":1,"type":"answer","sourceVersion":"1","rows":[{"field":"Region","items":[{"kind":"text","value":"East"}],"leaves":[0,0]}],"columns":[],"records":[1,1],"values":[]}""");
        Assert.Contains("two leaves", Assert.Throws<InvalidOperationException>(
            () => PivotEngine.Cube(new PivotQuery(rows: [new("Region")]), twice, Fields)).Message);
    }

    [Fact] // ADR-0063/0065, PV-22: a Decimal is its value, not the scale a source wrote it at — the raw form a copy carries is one, whichever source answered
    public void A_sources_scale_is_not_part_of_the_report()
    {
        var layout = new PivotLayout { Rows = [P("Region"), P("Product")], Values = [Sum("Amount"), Value("Amount", PivotAggregation.Max)] };
        var query = PivotQuery.For(layout);
        // A database's money column: SUM and MAX come back at its two places.
        var builder = new PivotAnswerBuilder(query, "sql-1");
        foreach (var (region, product, amount) in new[] { ("East", "Apples", 0.25m), ("East", "Pears", 0.25m), ("North", "Pears", 75.60m) })
        {
            var leaf = builder.AddLeaf([PivotItemKey.Text(region), PivotItemKey.Text(product)], 1);
            builder.SetCounts(leaf, 0, 1, 1);
            builder.SetSum(leaf, 0, PivotNumber.Exact(amount));
            builder.SetExtremes(leaf, 0, PivotNumber.Exact(amount), PivotNumber.Exact(amount));
        }
        Sale[] records =
        [
            new("East", "Apples", new DateTime(2026, 1, 10), 0.25m, 1, true),
            new("East", "Pears", new DateTime(2026, 1, 12), 0.25m, 1, false),
            new("North", "Pears", new DateTime(2026, 2, 18), 75.6m, 1, false),
        ];

        var raw = Raw(PivotEngine.Report(PivotEngine.Cube(query, builder.Build(), Fields), layout, EnUs));

        Assert.Equal(Raw(Report(records, Fields, layout)), raw);
        // East's 0.25 + 0.25 is 0.50 to decimal addition, and the source's 75.60 is 75.60.
        Assert.Contains("0.5", raw);
        Assert.Contains("75.6", raw);
        Assert.Contains("76.1", raw);
        Assert.DoesNotContain(raw, text => text.Contains('.') && text.EndsWith('0'));
    }

    // Every value cell's raw, locale-free form — what a copy carries — in the report's order.
    private static string[] Raw(PivotReport report) => report.Rows
        .SelectMany(row => Enumerable.Range(0, report.ValueColumns.Count).Select(column => row.ValueAt(column)))
        .OfType<PivotValue>()
        .Select(value => value.ToString(null, System.Globalization.CultureInfo.InvariantCulture))
        .ToArray();
}
