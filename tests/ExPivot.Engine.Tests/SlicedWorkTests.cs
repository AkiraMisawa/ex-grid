using System.Globalization;
using ExGrid.Data;
using ExPivot.Engine;
using Xunit;
using static ExPivot.Engine.Tests.Pivot;
using static ExPivot.Engine.Tests.Sources;

namespace ExPivot.Engine.Tests;

/// <summary>
/// The work after a question's pass, sliced as the pass is (ADR-0066, settled 2026-10-01; PV-40):
/// the bundled source assembling its answer, the cube made from an answer, and the report laid out
/// from a cube each yield the thread whenever a slice is spent, stop at a yield when cancelled, and
/// give exactly what the synchronous forms give. A batch applied while the answer held is assembled
/// waits for it, so no answer is half a batch (ADR-0067).
/// </summary>
public class SlicedWorkTests
{
    private static readonly Lazy<Measurements.Trade[]> Trades = new(() => Measurements.Make(20_000));
    private static readonly PivotFields<Measurements.Trade> TradeFields = Measurements.Fields();
    private static readonly Lazy<Snapshot> TradeSnapshot = new(() => TradeFields.Build(Trades.Value));

    /// <summary>A large answer: 270 trade dates by 62 books by four products, which 20,000 trades fill
    /// to some 17,000 leaves, with exact and double sums, extremes and variances.</summary>
    private static PivotLayout LargeLayout => new()
    {
        Rows = [P("TradeDate"), P("Book")],
        Columns = [P("Product")],
        Values = [Sum("Notional"), Value("Pnl", PivotAggregation.StdDev), Value("Price", PivotAggregation.Max), Value("Price", PivotAggregation.Sum)],
    };

    /// <summary>Slices that end at every look at the clock, and a look after every unit: the most
    /// pieces the work can be cut into. The yields are counted, and the work goes on at once.</summary>
    private sealed class EveryPiece
    {
        public int Yields;

        public PivotSlicing Slicing => new()
        {
            Budget = TimeSpan.Zero,
            UnitsPerCheck = 1,
            Yield = _ =>
            {
                Yields++;
                return ValueTask.CompletedTask;
            },
        };
    }

    /// <summary>Slices whose yields really leave the thread, so the work goes on wherever the runtime
    /// puts it.</summary>
    private static readonly PivotSlicing Leaving = new()
    {
        Budget = TimeSpan.Zero,
        UnitsPerCheck = 64,
        Yield = async _ => await Task.Yield(),
    };

    private static readonly PivotSlicing Never = new() { Budget = TimeSpan.FromDays(1) };

    private static (PivotQuery Query, PivotAnswer Answer, PivotSource Source) Large()
    {
        var source = PivotSource.From(TradeSnapshot.Value, TradeFields.Fields, Never);
        var query = PivotQuery.For(LargeLayout, int.MaxValue);
        var answer = source.AggregateAsync(query, Ct);
        Assert.True(answer.IsCompletedSuccessfully);
        return (query, answer.Result, source);
    }

    // ---- The sliced forms give what the synchronous forms give ------------------------------------

    /// <summary>The answers the cube tests read: every kind of value and Item over the deals —
    /// blanks, text in two spellings, non-finite doubles, a decimal sum past 128 bits — the sales,
    /// with no row field, no column field, no Value Field and no leaf at all, and the large answer.</summary>
    private static async Task<List<(string Name, PivotQuery Query, PivotAnswer Answer, IReadOnlyList<PivotField> Fields)>> Answers()
    {
        var answers = new List<(string, PivotQuery, PivotAnswer, IReadOnlyList<PivotField>)>();
        var deals = PivotSource.From(Deals, DealFields);
        foreach (var (query, q) in Questions.Select((query, q) => (query, q)))
            answers.Add(($"deals {q}", query, await deals.AggregateAsync(query, Ct), deals.Fields));
        var sales = PivotSource.From(Sales, Fields);
        foreach (var (layout, l) in Layouts().Select((layout, l) => (layout, l)))
        {
            var query = PivotQuery.For(layout);
            answers.Add(($"sales {l}", query, await sales.AggregateAsync(query, Ct), sales.Fields));
        }
        var nothing = PivotQuery.For(new PivotLayout
        {
            Rows = [P("Region") with { HiddenItems = [PivotItemKey.Text("East"), PivotItemKey.Text("West"), PivotItemKey.Text("North"), PivotItemKey.Blank] }],
            Values = [Sum("Amount")],
        });
        answers.Add(("no leaf", nothing, await sales.AggregateAsync(nothing, Ct), sales.Fields));
        var (largeQuery, largeAnswer, large) = Large();
        answers.Add(("large", largeQuery, largeAnswer, large.Fields));
        return answers;
    }

    [Fact] // ADR-0066 (PV-40): the cube made in slices is the cube made at once, cell for cell and part for part
    public async Task The_sliced_cube_is_the_cube()
    {
        foreach (var (name, query, answer, fields) in await Answers())
        {
            Assert.False(answer.IsRefused, name);
            var expected = PivotEngine.Cube(query, answer, fields);
            var everyPiece = new EveryPiece();

            SameCube(expected, await PivotEngine.CubeAsync(query, answer, fields, everyPiece.Slicing, Ct), name);
            SameCube(expected, await PivotEngine.CubeAsync(query, answer, fields, Leaving, Ct), name);
            if (answer.LeafCount > 1)
                Assert.True(everyPiece.Yields > 0, $"{name}: {answer.LeafCount} leaves made in one piece");
        }
    }

    [Fact] // ADR-0066 (PV-40): an answer the sliced cube refuses is refused by name, as the synchronous one refuses it
    public async Task The_sliced_cube_refuses_what_the_cube_refuses()
    {
        var sales = PivotSource.From(Sales, Fields);
        var query = PivotQuery.For(RowsBy("Region"));
        var answer = await sales.AggregateAsync(query, Ct);
        var other = PivotQuery.For(RowsBy("Product"));
        var refused = PivotAnswer.Refused(PivotSourceRefusal.TooManyLeaves(3));

        foreach (var (asked, given) in new[] { (other, answer), (query, refused) })
        {
            var expected = Assert.Throws<InvalidOperationException>(() => PivotEngine.Cube(asked, given, sales.Fields));
            var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => PivotEngine.CubeAsync(asked, given, sales.Fields, new EveryPiece().Slicing, Ct).AsTask());
            Assert.Equal(expected.Message, actual.Message);
        }
    }

    /// <summary>Layouts over the sales that reach every way a report is laid out: the three forms,
    /// subtotals at the top, at the bottom and off, grand totals off, collapsed and toggled Items,
    /// sorts by label and by value, several Value Fields in rows and in columns, Show Values As,
    /// formats, repeated labels, only columns, only values, and nothing.</summary>
    private static IEnumerable<PivotLayout> Layouts()
    {
        var two = new PivotLayout
        {
            Rows = [P("Region"), P("Product")],
            Columns = [P("Online")],
            Values = [Sum("Amount"), Value("Quantity", PivotAggregation.Average) with { NumberFormat = "N2" }],
        };
        yield return two;
        yield return two with { Form = PivotReportForm.Outline, SubtotalsAtTop = false };
        yield return two with { Form = PivotReportForm.Tabular, RepeatItemLabels = true };
        yield return two with { ValuesAxis = PivotAxis.Rows };
        yield return two with { ValuesAxis = PivotAxis.Rows, Form = PivotReportForm.Tabular };
        yield return two with { ValuesAxis = PivotAxis.Rows, Form = PivotReportForm.Outline, RepeatItemLabels = true };
        yield return two with { GrandTotalRow = false, GrandTotalColumn = false };
        yield return two with { Rows = [P("Region") with { Subtotals = false, Sort = PivotSort.Descending }, P("Product")] };
        yield return two with { Rows = [P("Region") with { Collapsed = true, ToggledItems = [PivotItemKey.Text("East")] }, P("Product")] };
        yield return two with { Rows = [P("Region") with { Sort = new PivotSort(PivotSortDirection.Descending, ByValue: 0) }, P("Product") with { Sort = new PivotSort(ByValue: 1) }] };
        yield return new PivotLayout
        {
            Rows = [P("Product")],
            Columns = [P("Region") with { Collapsed = false }, P("Online")],
            Values = [Value("Amount", PivotAggregation.Sum) with { ShowValuesAs = PivotShowValuesAs.PercentOfColumnTotal }],
        };
        yield return new PivotLayout
        {
            Columns = [P("Region"), P("Product") with { Sort = PivotSort.Descending }],
            Values = [Sum("Amount"), Value("Amount", PivotAggregation.Max)],
            ValuesAxis = PivotAxis.Columns,
        };
        yield return new PivotLayout
        {
            Columns = [P("Region") with { Collapsed = true }, P("Product")],
            Values = [Sum("Amount"), Sum("Quantity")],
        };
        yield return new PivotLayout { Values = [Sum("Amount"), Value("Quantity", PivotAggregation.Count)], ValuesAxis = PivotAxis.Rows };
        yield return new PivotLayout { Rows = [P("Date"), P("Region")] };
        yield return new PivotLayout();
    }

    [Fact] // ADR-0060/0066 (PV-40): the report laid out in slices is the report laid out at once, in every form
    public async Task The_sliced_report_is_the_report()
    {
        var sales = PivotSource.From(Sales, Fields);
        foreach (var layout in Layouts())
        {
            var query = PivotQuery.For(layout);
            var cube = PivotEngine.Cube(query, await sales.AggregateAsync(query, Ct), sales.Fields);
            var expected = PivotEngine.Report(cube, layout, EnUs);
            var everyPiece = new EveryPiece();

            SameReport(expected, await PivotEngine.ReportAsync(cube, layout, EnUs, everyPiece.Slicing, Ct));
            SameReport(expected, await PivotEngine.ReportAsync(cube, layout, EnUs, Leaving, Ct));
            // A layout with a row or a column field walks a tree, a step at a time.
            if (layout.Rows.Count + layout.Columns.Count > 0)
                Assert.True(everyPiece.Yields > 0, $"{expected.Rows.Count} rows laid out in one piece");
        }

        var (largeQuery, largeAnswer, large) = Large();
        var largeCube = PivotEngine.Cube(largeQuery, largeAnswer, large.Fields);
        foreach (var layout in new[]
                 {
                     LargeLayout,
                     LargeLayout with { Form = PivotReportForm.Tabular, Rows = [P("TradeDate") with { Sort = PivotSort.Descending }, P("Book") with { Sort = new PivotSort(PivotSortDirection.Descending, ByValue: 0) }] },
                     LargeLayout with { Rows = [P("TradeDate") with { Collapsed = true }, P("Book")], ValuesAxis = PivotAxis.Rows },
                 })
        {
            var expected = PivotEngine.Report(largeCube, layout, EnUs);
            SameReport(expected, await PivotEngine.ReportAsync(largeCube, layout, EnUs, new EveryPiece().Slicing, Ct));
            SameReport(expected, await PivotEngine.ReportAsync(largeCube, layout, EnUs, Leaving, Ct));
        }
    }

    [Fact] // ADR-0060 (PV-31/PV-40): Items ordered by an Order Key, and one that throws, lay out in slices as at once
    public async Task Order_keys_are_read_as_at_once()
    {
        var calls = 0;
        var keyed = PivotFields.Of<Sale>()
            .Text("Region", s => s.Region, orderKey: region => { calls++; return region is "West" ? null : (IComparable)(-region.Length); })
            .Text("Product", s => s.Product)
            .Number("Amount", s => s.Amount);
        var throwing = PivotFields.Of<Sale>()
            .Text("Region", s => s.Region, orderKey: region => region == "North" ? throw new FormatException("no") : region)
            .Number("Amount", s => s.Amount);
        var layout = new PivotLayout { Rows = [P("Region"), P("Product")], Values = [Sum("Amount")] };
        var query = PivotQuery.For(layout);

        var source = PivotSource.From(Sales, keyed);
        var cube = PivotEngine.Cube(query, await source.AggregateAsync(query, Ct), source.Fields);
        var expected = PivotEngine.Report(cube, layout, EnUs);
        var sliced = await PivotEngine.ReportAsync(PivotEngine.Cube(query, await source.AggregateAsync(query, Ct), source.Fields), layout, EnUs, new EveryPiece().Slicing, Ct);
        SameReport(expected, sliced);
        // Once per Item and per cube: three Regions in each of the two cubes; a Blank has no value
        // to key.
        Assert.Equal(6, calls);

        var failing = PivotSource.From(Sales, throwing);
        var failingQuery = PivotQuery.For(layout with { Rows = [P("Region")] });
        var failingCube = PivotEngine.Cube(failingQuery, await failing.AggregateAsync(failingQuery, Ct), failing.Fields);
        var message = Assert.Throws<InvalidOperationException>(() => PivotEngine.Report(failingCube, layout with { Rows = [P("Region")] }, EnUs)).Message;
        var sliceMessage = (await Assert.ThrowsAsync<InvalidOperationException>(() => PivotEngine.ReportAsync(failingCube, layout with { Rows = [P("Region")] }, EnUs, new EveryPiece().Slicing, Ct).AsTask())).Message;
        Assert.Equal(message, sliceMessage);
    }

    [Fact] // ADR-0060/0066 (PV-40): a node with thousands of children is ordered in slices, by label, by key and by value, as at once
    public async Task Many_siblings_are_ordered_in_slices_as_at_once()
    {
        // Every trade's own notional and P&L: a few thousand Items under the root, text, numbers
        // and a sort by value.
        var layouts = new[]
        {
            new PivotLayout { Rows = [P("Pnl")], Values = [Sum("Notional")] },
            new PivotLayout { Rows = [P("Pnl") with { Sort = PivotSort.Descending }], Values = [Sum("Notional")] },
            new PivotLayout { Rows = [P("Notional") with { Sort = new PivotSort(PivotSortDirection.Descending, ByValue: 0) }], Values = [Sum("Price")] },
            new PivotLayout { Rows = [P("Book"), P("Notional") with { Sort = new PivotSort(ByValue: 0) }], Columns = [P("Region")], Values = [Value("Pnl", PivotAggregation.Min)] },
        };
        var source = PivotSource.From(TradeSnapshot.Value, TradeFields.Fields, Never);
        foreach (var layout in layouts)
        {
            var query = PivotQuery.For(layout, int.MaxValue);
            var cube = PivotEngine.Cube(query, await source.AggregateAsync(query, Ct), source.Fields);
            var expected = PivotEngine.Report(cube, layout, EnUs);
            Assert.True(Math.Max(cube.RowRoot.Children.Count, cube.RowRoot.Children.Max(c => c.Children.Count)) > 200);

            SameReport(expected, await PivotEngine.ReportAsync(cube, layout, EnUs, new EveryPiece().Slicing, Ct));
        }
    }

    [Fact] // ADR-0059/0011 (PV-40): rows compared in slices compare as at once
    public async Task Rows_compared_in_slices_compare_as_at_once()
    {
        var layout = new PivotLayout { Rows = [P("Region"), P("Product")], Values = [Sum("Amount")] };
        var report = Report(layout);
        var refreshed = Report(layout, Sales.Select(s => s with { Amount = s.Amount * 2, Region = s.Region?.ToUpperInvariant() }).ToArray());
        var collapsed = Report(layout with { Rows = [P("Region") with { Collapsed = true }, P("Product")] });
        var fewer = Report(layout, Sales[..6]);
        var renamed = Report(layout, Sales.Select(s => s with { Product = s.Product == "Plums" ? "Prunes" : s.Product }).ToArray());

        foreach (var other in new[] { refreshed, collapsed, fewer, renamed, report, null })
        {
            var everyPiece = new EveryPiece();
            Assert.Equal(report.HasSameRowsAs(other), await report.HasSameRowsAsAsync(other, everyPiece.Slicing, Ct));
        }
        var (query, answer, source) = Large();
        var large = PivotEngine.Report(PivotEngine.Cube(query, answer, source.Fields), LargeLayout, EnUs);
        var again = PivotEngine.Report(PivotEngine.Cube(query, answer, source.Fields), LargeLayout, EnUs);
        var yielded = new EveryPiece();
        Assert.True(await large.HasSameRowsAsAsync(again, yielded.Slicing, Ct));
        Assert.True(yielded.Yields > 10);
    }

    // ---- They yield, as the pass does ---------------------------------------------------------

    [Fact] // ADR-0066 (PV-40): a large answer's cube and report yield whenever a slice is spent; a small one never reads the clock
    public async Task A_large_answer_is_made_and_laid_out_in_slices()
    {
        var (query, answer, source) = Large();
        var yields = 0;
        var zero = new PivotSlicing
        {
            Budget = TimeSpan.Zero,
            Yield = _ =>
            {
                yields++;
                return ValueTask.CompletedTask;
            },
        };

        var cube = await PivotEngine.CubeAsync(query, answer, source.Fields, zero, Ct);
        var cubeYields = yields;
        yields = 0;
        await PivotEngine.ReportAsync(cube, LargeLayout, EnUs, zero, Ct);

        // At the default, the clock is read about every thousand units of work, and with no budget
        // every look ends a slice.
        Assert.True(cubeYields > answer.LeafCount / 1024, $"{answer.LeafCount:N0} leaves, {cubeYields} yields");
        Assert.True(yields > 4, $"{yields} yields");

        // A budget never spent never yields, and the work is complete when it returns.
        var unspent = new PivotSlicing { Budget = TimeSpan.FromDays(1), Yield = _ => throw new InvalidOperationException("yielded") };
        Assert.True(PivotEngine.CubeAsync(query, answer, source.Fields, unspent, Ct).IsCompletedSuccessfully);
        Assert.True(PivotEngine.ReportAsync(cube, LargeLayout, EnUs, unspent, Ct).IsCompletedSuccessfully);

        // A small answer's work never reaches a look at the clock: no budget, and still no yield.
        var clock = new CountingClock();
        var small = PivotSource.From(Sales, Fields);
        var smallQuery = PivotQuery.For(RowsBy("Region", "Product"));
        var smallAnswer = await small.AggregateAsync(smallQuery, Ct);
        var tight = zero with { TimeProvider = clock };
        yields = 0;
        var smallCube = await PivotEngine.CubeAsync(smallQuery, smallAnswer, small.Fields, tight, Ct);
        await PivotEngine.ReportAsync(smallCube, RowsBy("Region", "Product"), EnUs, tight, Ct);
        Assert.Equal(0, yields);
        // Each begins its slice, and reads the clock no more.
        Assert.Equal(2, clock.Reads);
    }

    [Fact] // ADR-0066 (PV-40): a slice of the work after a pass ends when its budget is spent, by the clock it is given
    public async Task A_slice_ends_when_its_budget_is_spent()
    {
        var (query, answer, source) = Large();
        var cube = PivotEngine.Cube(query, answer, source.Fields);
        var report = PivotEngine.Report(cube, LargeLayout, EnUs);
        var again = PivotEngine.Report(cube, LargeLayout, EnUs);

        foreach (var work in new Func<PivotSlicing, Task>[]
                 {
                     slicing => PivotEngine.CubeAsync(query, answer, source.Fields, slicing, Ct).AsTask(),
                     slicing => PivotEngine.ReportAsync(cube, LargeLayout, EnUs, slicing, Ct).AsTask(),
                     slicing => report.HasSameRowsAsAsync(again, slicing, Ct).AsTask(),
                 })
        {
            var clock = new SteppingClock(TimeSpan.FromMilliseconds(10));
            var readsAtYield = new List<int> { 0 };
            var slicing = new PivotSlicing
            {
                // A look at the clock after every piece of work, each 10 ms on: a slice of 25 ms
                // ends at its third look.
                TimeProvider = clock,
                Budget = TimeSpan.FromMilliseconds(25),
                UnitsPerCheck = 1,
                Yield = _ =>
                {
                    readsAtYield.Add(clock.Reads);
                    return ValueTask.CompletedTask;
                },
            };

            await work(slicing);

            // Every slice reads the clock as it begins and three times more, and yields.
            var slices = readsAtYield.Zip(readsAtYield.Skip(1), (before, after) => after - before).ToArray();
            Assert.True(slices.Length > 3, $"{slices.Length} slices");
            Assert.All(slices, reads => Assert.Equal(4, reads));
        }
    }

    [Fact] // ADR-0066 (PV-40): cancelled, the work after a pass stops at the next yield and does nothing more
    public async Task Cancelled_work_stops_at_the_next_yield()
    {
        var (query, answer, source) = Large();
        var cube = PivotEngine.Cube(query, answer, source.Fields);
        var report = PivotEngine.Report(cube, LargeLayout, EnUs);
        var again = PivotEngine.Report(cube, LargeLayout, EnUs);
        foreach (var work in new Func<PivotSlicing, CancellationToken, Task>[]
                 {
                     (slicing, token) => PivotEngine.CubeAsync(query, answer, source.Fields, slicing, token).AsTask(),
                     (slicing, token) => PivotEngine.ReportAsync(cube, LargeLayout, EnUs, slicing, token).AsTask(),
                     (slicing, token) => report.HasSameRowsAsAsync(again, slicing, token).AsTask(),
                 })
        {
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);
            var clock = new CountingClock();
            var yields = 0;
            var readsAtCancel = -1;
            var slicing = new PivotSlicing
            {
                Budget = TimeSpan.Zero,
                TimeProvider = clock,
                // Cancelled while the thread is yielded after the second slice.
                Yield = _ =>
                {
                    if (++yields == 2)
                    {
                        cancellation.Cancel();
                        readsAtCancel = clock.Reads;
                    }
                    return ValueTask.CompletedTask;
                },
            };

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => work(slicing, cancellation.Token));

            Assert.Equal(2, yields);
            // Nothing after the yield: not even the next slice's look at the clock.
            Assert.Equal(readsAtCancel, clock.Reads);
        }
    }

    [Fact] // ADR-0066 (PV-40): work asked for under a token already cancelled does nothing
    public async Task Work_cancelled_before_it_starts_does_nothing()
    {
        var (query, answer, source) = Large();
        var cube = PivotEngine.Cube(query, answer, source.Fields);
        var cancelled = new CancellationToken(canceled: true);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PivotEngine.CubeAsync(query, answer, source.Fields, null, cancelled).AsTask());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PivotEngine.ReportAsync(cube, LargeLayout, EnUs, null, cancelled).AsTask());
    }

    // ---- The bundled source assembles its answer in slices --------------------------------------

    [Fact] // ADR-0066 (PV-40): after its last slice of rows, the bundled source assembles a large answer in slices too, and it is the answer
    public async Task The_bundled_source_assembles_its_answer_in_slices()
    {
        var query = PivotQuery.For(LargeLayout, int.MaxValue);
        var rows = (long)TradeSnapshot.Value.Rows.Count;
        long read = 0;
        var yields = 0;
        var yieldsAfterThePass = 0;
        var slicing = new PivotSlicing
        {
            Budget = TimeSpan.Zero,
            RowsRead = n => read += n,
            Yield = _ =>
            {
                yields++;
                if (read == rows)
                    yieldsAfterThePass++;
                return ValueTask.CompletedTask;
            },
        };
        var expected = await PivotSource.From(TradeSnapshot.Value, TradeFields.Fields, Never).AggregateAsync(query, Ct);

        // Over a Snapshot, and over records read through untyped accessors.
        var untyped = new PivotField<Measurements.Trade>[]
        {
            new("TradeDate", PivotFieldType.Date, t => t.TradeDate), new("Book", PivotFieldType.Text, t => t.Book),
            new("Product", PivotFieldType.Text, t => t.Product), new("Notional", PivotFieldType.Number, t => t.Notional),
            new("Pnl", PivotFieldType.Number, t => t.Pnl), new("Price", PivotFieldType.Number, t => t.Price),
        };
        var untypedExpected = await PivotSource.From(Trades.Value, untyped, Never).AggregateAsync(query, Ct);
        foreach (var (source, reference) in new (PivotSource, PivotAnswer)[]
                 {
                     (PivotSource.From(TradeSnapshot.Value, TradeFields.Fields, slicing), expected),
                     (PivotSource.From(Trades.Value, untyped, slicing), untypedExpected),
                 })
        {
            // The untyped source reads its records into a Snapshot on its first question.
            await source.AggregateAsync(new PivotQuery(), Ct);
            read = 0;
            yields = 0;
            yieldsAfterThePass = 0;

            var answer = await source.AggregateAsync(query, Ct);

            Assert.Equal(rows, read);
            Assert.True(yields > rows / 1024, $"{yields} yields");
            Assert.True(yieldsAfterThePass > reference.LeafCount / 1024, $"{reference.LeafCount:N0} leaves assembled with {yieldsAfterThePass} yields");
            SameAnswer(reference, answer, sameVersion: false);
        }
    }

    [Fact] // ADR-0066 (PV-40): a question cancelled while its answer is assembled stops at the next yield
    public async Task A_question_cancelled_while_its_answer_is_assembled_stops_there()
    {
        var query = PivotQuery.For(LargeLayout, int.MaxValue);
        var rows = (long)TradeSnapshot.Value.Rows.Count;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        long read = 0;
        var clock = new CountingClock();
        var readsAtCancel = -1;
        var assemblyYields = 0;
        var slicing = new PivotSlicing
        {
            Budget = TimeSpan.Zero,
            TimeProvider = clock,
            RowsRead = n => read += n,
            Yield = _ =>
            {
                // The second yield after the last row: the answer is half assembled.
                if (read == rows && ++assemblyYields == 2)
                {
                    cancellation.Cancel();
                    readsAtCancel = clock.Reads;
                }
                return ValueTask.CompletedTask;
            },
        };
        var source = PivotSource.From(TradeSnapshot.Value, TradeFields.Fields, slicing);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => source.AggregateAsync(query, cancellation.Token).AsTask());

        Assert.Equal(readsAtCancel, clock.Reads);
        // Nothing half made is held: the same question asked again is read again, and answered.
        read = 0;
        var answer = await source.AggregateAsync(query, Ct);
        Assert.Equal(rows, read);
        SameAnswer(await PivotSource.From(TradeSnapshot.Value, TradeFields.Fields, Never).AggregateAsync(query, Ct), answer, sameVersion: false);
    }

    [Fact] // ADR-0067 (PV-34/PV-40): the answer held is assembled in slices, and a batch applied meanwhile waits for it — no answer is half a batch
    public async Task A_batch_applied_while_the_held_answer_is_assembled_waits_for_it()
    {
        var trades = Trades.Value;
        var query = PivotQuery.For(LargeLayout, int.MaxValue);
        SnapshotPivotSource? source = null;
        ChangeBatch? pending = null;
        long read = 0;
        var yields = 0;
        var slicing = new PivotSlicing
        {
            Budget = TimeSpan.Zero,
            RowsRead = n => read += n,
            Yield = _ =>
            {
                yields++;
                // At the first yield of the held answer's assembly, a batch arrives.
                if (pending is { } batch)
                {
                    pending = null;
                    source!.Apply(batch);
                }
                return ValueTask.CompletedTask;
            },
        };
        source = PivotSource.From(TradeSnapshot.Value, TradeFields.Fields, slicing);
        await source.AggregateAsync(query, Ct);

        // A first batch is folded into the answer held, which is then assembled in slices.
        var first = TradeFields.Batch(changed: [.. trades[..300].Select(t => t with { Notional = t.Notional + 10_000m, Price = t.Price + 1 })]);
        source.Apply(first);
        var afterFirst = source.Snapshot;
        var second = TradeFields.Batch(
            changed: [.. trades[100..400].Select(t => t with { Pnl = -t.Pnl, Price = t.Price * 2 })],
            removedKeys: [.. trades[500..510].Select(t => (object)t.Id)],
            added: [.. Measurements.Make(50, seed: 7).Select((t, i) => t with { Id = 1_000_000 + i })]);
        pending = second;
        read = 0;
        yields = 0;

        var folded = await source.AggregateAsync(query, Ct);

        // Assembled in slices from the answer held, the Snapshot's rows not read again. The second
        // batch arrived at its first yield, and is not in it: it was folded in once the assembly was
        // over, which read the rows the batch brought, and no others.
        Assert.Null(pending);
        Assert.Equal(second.Added!.RowCount + second.Changed!.RowCount, read);
        Assert.True(yields > folded.LeafCount / 1024, $"{yields} yields");
        Assert.NotSame(afterFirst, source.Snapshot);
        var expected = await PivotSource.From(afterFirst, TradeFields.Fields, Never).AggregateAsync(query, Ct);
        // A folded answer keeps its leaves where they were, so the leaves are compared by their Items.
        LiveDataTests.SameLeaves(expected, folded, "the answer held, assembled while a batch arrived");

        // The next question brings the second batch, from the answer held.
        read = 0;
        var next = await source.AggregateAsync(query, Ct);
        Assert.Equal(0, read);
        LiveDataTests.SameLeaves(await PivotSource.From(source.Snapshot, TradeFields.Fields, Never).AggregateAsync(query, Ct), next, "the next answer");
        Assert.NotEqual(folded.SourceVersion, next.SourceVersion);
    }

    // ---- What the tests compare and count ----------------------------------------------------

    /// <summary>The same cube: the same question, version and records, the same trees — every node
    /// numbered alike, under the same parent, for the same Item — and every cell's parts, to the
    /// last bit.</summary>
    private static void SameCube(PivotCube expected, PivotCube actual, string name)
    {
        Assert.Equal(expected.Query, actual.Query);
        Assert.Equal(expected.SourceVersion, actual.SourceVersion);
        Assert.Equal(expected.IncludedRecordCount, actual.IncludedRecordCount);
        Assert.Equal(expected.Sources, actual.Sources);
        Assert.Equal(expected.Parts.OrderBy(p => p.Key), actual.Parts.OrderBy(p => p.Key));
        SameTree(expected.RowRoot, actual.RowRoot, name);
        SameTree(expected.ColumnRoot, actual.ColumnRoot, name);
        var keys = expected.CellKeys.Order().ToArray();
        Assert.Equal(keys, actual.CellKeys.Order().ToArray());
        for (var source = 0; source < expected.Sources.Length; source++)
        {
            var mine = expected.ValuesOf(source);
            var theirs = actual.ValuesOf(source);
            Assert.Equal(mine.Parts, theirs.Parts);
            foreach (var key in keys)
            {
                var (i, j) = (expected.CellOf(key), actual.CellOf(key));
                Assert.Equal(i, j);
                Assert.Equal(mine.Counts[i], theirs.Counts[j]);
                if (mine.Sums is { } sums)
                    Assert.Equal(PartBits(sums[i]), PartBits(theirs.Sums![j]));
                if (mine.Extremes is { } extremes)
                    Assert.Equal(PartBits(extremes[i]), PartBits(theirs.Extremes![j]));
                if (mine.Products is { } products)
                    Assert.Equal(Bits(products[i]), Bits(theirs.Products![j]));
                if (mine.Variances is { } variances)
                    Assert.Equal((Bits(variances[i].Mean), Bits(variances[i].M2)), (Bits(theirs.Variances![j].Mean), Bits(theirs.Variances![j].M2)));
            }
        }
    }

    private static void SameTree(AxisNode expected, AxisNode actual, string name)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Level, actual.Level);
        Assert.Equal(expected.Item?.PublicKey.Kind, actual.Item?.PublicKey.Kind);
        Assert.Equal(expected.Item?.PublicKey.Value, actual.Item?.PublicKey.Value);
        Assert.Equal(expected.Item?.FirstValue, actual.Item?.FirstValue);
        Assert.True(expected.Children.Count == actual.Children.Count, $"{name}: node {expected.Id}");
        for (var c = 0; c < expected.Children.Count; c++)
            SameTree(expected.Children[c], actual.Children[c], name);
    }

    private static string PartBits(ExGrid.Data.AggregateSum sum)
        => string.Join(",", decimal.GetBits(sum.Exact)) + $"|{Bits(sum.Double)}|{Bits(sum.Compensation)}|{sum.Inexact}";

    private static string PartBits(ExGrid.Data.AggregateExtremes extremes)
        => string.Join(",", decimal.GetBits(extremes.ExactMin)) + "|" + string.Join(",", decimal.GetBits(extremes.ExactMax))
            + $"|{Bits(extremes.Min)}|{Bits(extremes.Max)}|{extremes.Inexact}";

    /// <summary>The same report: its columns, its header spans and tiers, its captions, and every
    /// row's role, labels, toggles and painted values, in order.</summary>
    private static void SameReport(PivotReport expected, PivotReport actual)
    {
        Assert.Equal(expected.LabelColumns, actual.LabelColumns);
        Assert.Equal(
            expected.ValueColumns.Select(c => (c.Name, c.Header, c.Role, c.ValueField)),
            actual.ValueColumns.Select(c => (c.Name, c.Header, c.Role, c.ValueField)));
        Assert.Equal(expected.HeaderSpans, actual.HeaderSpans);
        Assert.Equal(expected.HeaderTierCount, actual.HeaderTierCount);
        Assert.Equal(expected.ValueCaptions, actual.ValueCaptions);
        Assert.Equal(expected.Rows.Count, actual.Rows.Count);
        Assert.Equal(Lines(expected), Lines(actual));
        for (var i = 0; i < expected.Rows.Count; i++)
        {
            Assert.Equal(expected.Rows[i].Labels, actual.Rows[i].Labels);
            Assert.Equal(expected.RowPath(expected.Rows[i]), actual.RowPath(actual.Rows[i]));
        }
        Assert.True(expected.HasSameRowsAs(actual));
    }

    /// <summary>A clock that counts how often it is read.</summary>
    private sealed class CountingClock : TimeProvider
    {
        public int Reads { get; private set; }

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp()
        {
            Reads++;
            return 0;
        }
    }

    /// <summary>A clock that moves on by <paramref name="step"/> every time it is read, and counts
    /// the reads.</summary>
    private sealed class SteppingClock(TimeSpan step) : TimeProvider
    {
        private long _now;

        public int Reads { get; private set; }

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp()
        {
            Reads++;
            return _now += step.Ticks;
        }
    }
}
