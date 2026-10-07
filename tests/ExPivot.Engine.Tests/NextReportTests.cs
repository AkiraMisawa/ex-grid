using System.Globalization;
using ExGrid.Data;
using ExPivot.Engine;
using Xunit;
using static ExPivot.Engine.Tests.Sources;

namespace ExPivot.Engine.Tests;

/// <summary>
/// A live redraw makes the next report from the last (ADR-0161, PV-44). Over random Change Batches
/// and layouts — subtotals on and off, each form, Value Fields on rows and on columns, each
/// Aggregation and Show Values As — the next cube and report made from the ones on screen equal a
/// report built afresh, row by row, key by key and cell by cell, in values and painted text, whether
/// the answer names its changed leaves or the engine compares them. A row whose painted text changed
/// is a new instance; a row whose painted text did not is the same instance, an untouched path's
/// above all; and a redraw ADR-0161 lists as one that starts afresh shares no row. The cube on screen
/// is left exactly as it was.
/// <para>The seeds are recorded in the cases: each runs six layouts, fifteen batches each, over four
/// hundred trades, and every failure names its seed, layout and batch.</para>
/// </summary>
public class NextReportTests
{
    internal sealed record Trade(long Id, string Region, string Desk, string Product, decimal? Amount, double? Risk, long? Quantity, DateTime Date, bool Live);

    private static readonly PivotFields<Trade> Declared = PivotFields.Of<Trade>()
        .Key("Id", t => t.Id)
        .Text("Region", t => t.Region)
        .Text("Desk", t => t.Desk)
        .Text("Product", t => t.Product)
        .Number("Amount", t => t.Amount)
        .Number("Risk", t => t.Risk)
        .Number("Quantity", t => t.Quantity)
        .Date("Date", t => t.Date)
        .Month("Month", of: "Date")
        .Boolean("Live", t => t.Live);

    private static readonly PivotOptions EnUs = new() { Culture = CultureInfo.GetCultureInfo("en-US") };

    private const int Layouts = 6;
    private const int Batches = 15;

    /// <summary>What a seed's redraws were, so that a run that shared nothing, or started nothing
    /// afresh, cannot pass unnoticed.</summary>
    private sealed class Tally
    {
        public int Incremental;
        public int Afresh;
        public int SharedRows;
        public int NewRows;
    }

    [Theory] // ADR-0161/0060 (PV-44): the next report made from the last equals one built afresh, and shares exactly the rows whose painted text did not change
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public async Task The_next_report_equals_one_built_afresh(int seed)
    {
        var random = new Random(seed);
        var world = new World(random);
        var fields = Declared.Fields;
        var source = PivotSource.From(world.Initial(400), Declared);
        var named = new Tally();
        var compared = new Tally();
        var forms = new HashSet<PivotReportForm>();
        PivotCube? cube = null, cubeB = null;
        PivotReport? report = null, reportB = null;
        for (var k = 0; k < Layouts; k++)
        {
            var layout = RandomLayout(random, seed, k);
            forms.Add(layout.Form);
            var query = PivotQuery.For(layout);
            var answer = await source.AggregateAsync(cube is null ? query : query.WithChangedSince(cube.SourceVersion), Ct);
            Assert.False(answer.IsRefused, $"seed {seed}, layout {k}: {answer.Refusal?.Message}");
            if (cube is null)
            {
                cube = cubeB = PivotEngine.Cube(query, answer, fields);
                report = reportB = PivotEngine.Report(cube, layout, EnUs);
            }
            else
            {
                // A new layout is no data: made from the report before it, it shares no row.
                var laidOut = PivotEngine.Report(PivotEngine.Cube(query, answer, fields), layout, EnUs);
                (cube, report) = Step(cube, report!, query, answer, layout, laidOut, false, $"seed {seed}, layout {k}, a new layout, as named", named);
                (cubeB, reportB) = Step(cubeB!, reportB!, query, answer.WithChangedLeaves(null), layout, laidOut, false, $"seed {seed}, layout {k}, a new layout, compared", compared);
            }
            for (var b = 0; b < Batches; b++)
            {
                var (batch, kind) = world.Next(Declared);
                var change = source.Apply(batch);
                var asked = query.WithChangedSince(cube.SourceVersion);
                var next = await source.AggregateAsync(asked, Ct);
                var where = $"seed {seed}, layout {k}, batch {b} ({kind}{(change.Compacted ? ", compacted" : "")})";
                Assert.False(next.IsRefused, $"{where}: {next.Refusal?.Message}");
                var fresh = PivotEngine.Report(PivotEngine.Cube(asked, next, fields), layout, EnUs);
                var sameLeaves = SameStructure(answer, next);
                var changed = sameLeaves ? ChangedLeaves(answer, next) : -1;

                // As the source answered, naming the leaves its fold touched.
                var namedIncremental = !StartsAfresh(layout) && sameLeaves
                    && (next.ChangedLeaves is { } says && says.Since == cube.SourceVersion
                        ? says.SameLeaves && !PivotCube.TooManyChanged(says.Leaves.Count, next.LeafCount)
                        : !PivotCube.TooManyChanged(changed, next.LeafCount));
                (cube, report) = Step(cube, report!, asked, next, layout, fresh, namedIncremental, $"{where}, as named", named);

                // As a source that says nothing answers: the engine compares the leaves itself.
                var comparedIncremental = !StartsAfresh(layout) && sameLeaves && !PivotCube.TooManyChanged(changed, next.LeafCount);
                (cubeB, reportB) = Step(cubeB!, reportB!, asked, next.WithChangedLeaves(null), layout, fresh, comparedIncremental, $"{where}, compared", compared);

                answer = next;
            }
        }

        // Every form ran, and the redraws both shared rows and started afresh, both ways.
        Assert.Equal(3, forms.Count);
        foreach (var (tally, way) in new[] { (named, "named"), (compared, "compared") })
        {
            Assert.True(tally.Incremental > 20, $"seed {seed}, {way}: {tally.Incremental} redraws from the last");
            Assert.True(tally.Afresh > 3, $"seed {seed}, {way}: {tally.Afresh} redraws afresh");
            Assert.True(tally.SharedRows > 20 && tally.NewRows > 0, $"seed {seed}, {way}: {tally.SharedRows} rows shared, {tally.NewRows} made anew");
        }
    }

    [Theory] // ADR-0161 (PV-44): new words or a new culture are no data — the next report, made from the last over newer data, shares no row, and equals one laid out afresh
    [InlineData("words")]
    [InlineData("culture")]
    public async Task New_words_or_a_new_culture_share_no_row(string change)
    {
        var trades = new World(new Random(11)).Initial(200);
        var source = PivotSource.From(trades, Declared);
        var layout = new PivotLayout { Rows = [new PivotFieldPlacement("Region"), new PivotFieldPlacement("Desk")], Values = [new PivotValueField("Amount")] };
        var query = PivotQuery.For(layout);
        var cube = PivotEngine.Cube(query, await source.AggregateAsync(query, Ct), Declared.Fields);
        var report = PivotEngine.Report(cube, layout, EnUs);
        source.Apply(Declared.Batch(changed: [trades[0] with { Amount = 1m }]));
        var asked = query.WithChangedSince(cube.SourceVersion);
        var answer = await source.AggregateAsync(asked, Ct);
        var options = change == "words" ? EnUs with { Label = PivotWords.Japanese } : new PivotOptions { Culture = CultureInfo.GetCultureInfo("de-DE") };

        var nextCube = PivotEngine.NextCube(cube, asked, answer, Declared.Fields);
        var next = PivotEngine.NextReport(report, nextCube, layout, options);

        Assert.Equal(cube.Id, nextCube.MadeFrom);
        Assert.False(next.WasMadeFrom(report));
        Assert.DoesNotContain(next.Rows, row => report.Rows.Contains(row));
        SameReport(PivotEngine.Report(PivotEngine.Cube(asked, answer, Declared.Fields), layout, options), next, change);
    }

    [Fact] // ADR-0161: the values that changed over many redraws, once they pass an eighth of the cells, are copied once into a cube's own — and every cube still equals one built afresh
    public async Task Values_changed_over_many_redraws_are_folded_into_a_cube_of_its_own()
    {
        var random = new Random(20261007);
        var trades = new List<Trade>();
        for (var region = 0; region < 128; region++)
        {
            for (var desk = 0; desk < 64; desk++)
                trades.Add(new Trade(trades.Count + 1, "R" + region.ToString("000", CultureInfo.InvariantCulture), "D" + desk.ToString("00", CultureInfo.InvariantCulture), "Bond", 1m, 1.0, 1, new DateTime(2026, 1, 1), true));
        }
        var source = PivotSource.From(trades.ToArray(), Declared);
        var layout = new PivotLayout { Rows = [new PivotFieldPlacement("Region"), new PivotFieldPlacement("Desk")], Values = [new PivotValueField("Amount"), new PivotValueField("Risk", PivotAggregation.Max)] };
        var query = PivotQuery.For(layout);
        var cube = PivotEngine.Cube(query, await source.AggregateAsync(query, Ct), Declared.Fields);
        var report = PivotEngine.Report(cube, layout, EnUs);
        var apart = new List<int>();
        // Fewer batches than make the Snapshot compact, which would start the cube afresh.
        for (var redraw = 0; redraw < 50; redraw++)
        {
            var amended = Enumerable.Range(0, 300).Select(_ => trades[random.Next(trades.Count)]).DistinctBy(t => t.Id)
                .Select(t => t with { Amount = random.Next(1, 1000) / 100m, Risk = random.NextDouble() }).ToArray();
            foreach (var trade in amended)
                trades[(int)trade.Id - 1] = trade;
            source.Apply(Declared.Batch(changed: amended));
            var asked = query.WithChangedSince(cube.SourceVersion);
            var answer = await source.AggregateAsync(asked, Ct);
            cube = PivotEngine.NextCube(cube, asked, answer, Declared.Fields);
            report = PivotEngine.NextReport(report, cube, layout, EnUs);
            apart.Add(cube.ChangedApart);
            if (redraw % 10 == 9)
                SameReport(PivotEngine.Report(PivotEngine.Cube(asked, answer, Declared.Fields), layout, EnUs), report, $"redraw {redraw}");
        }

        // Held apart, growing; past an eighth of the cells, folded in once, and growing again.
        Assert.True(apart.Max() > 2048, $"at most {apart.Max()} cells held apart");
        var folded = Enumerable.Range(1, apart.Count - 1).Where(i => apart[i] < apart[i - 1]).ToArray();
        Assert.NotEmpty(folded);
        Assert.All(folded, i => Assert.Equal(0, apart[i]));
        Assert.True(apart[^1] > 0);
    }

    [Fact] // ADR-0161 (PV-40): the next cube and report are made in slices, yielding as a cube and report built afresh do, with the same result as at once
    public async Task The_next_cube_and_report_are_made_in_slices()
    {
        var trades = Enumerable.Range(0, 3_000).Select(i => new Trade(i + 1, "R" + i.ToString("0000", CultureInfo.InvariantCulture), "Rates", "Bond", i, i, i, new DateTime(2026, 1, 1), true)).ToArray();
        var source = PivotSource.From(trades, Declared);
        var layout = new PivotLayout { Rows = [new PivotFieldPlacement("Region")], Values = [new PivotValueField("Amount")] };
        var query = PivotQuery.For(layout);
        var cube = PivotEngine.Cube(query, await source.AggregateAsync(query, Ct), Declared.Fields);
        var report = PivotEngine.Report(cube, layout, EnUs);
        source.Apply(Declared.Batch(changed: [trades[10] with { Amount = -1m }]));
        var asked = query.WithChangedSince(cube.SourceVersion);
        var answer = await source.AggregateAsync(asked, Ct);
        var yields = 0;
        var slicing = new PivotSlicing { Budget = TimeSpan.Zero, UnitsPerCheck = 64, Yield = _ => { yields++; return ValueTask.CompletedTask; } };

        var nextCube = await PivotEngine.NextCubeAsync(cube, asked, answer, Declared.Fields, slicing, Ct);
        var cubeYields = yields;
        var next = await PivotEngine.NextReportAsync(report, nextCube, layout, EnUs, slicing, Ct);

        Assert.True(cubeYields > 0, "the next cube never yielded");
        Assert.True(yields > cubeYields, "the next report never yielded");
        Assert.True(next.WasMadeFrom(report));
        SameReport(PivotEngine.NextReport(report, PivotEngine.NextCube(cube, asked, answer, Declared.Fields), layout, EnUs), next, "sliced");
        Assert.Equal(report.Rows.Count - 2, next.Rows.Count(row => report.RowFor(row.Key) is { } before && ReferenceEquals(before, row)));
    }

    private static (PivotCube, PivotReport) Step(
        PivotCube previousCube, PivotReport previous, PivotQuery query, PivotAnswer answer, PivotLayout layout, PivotReport fresh,
        bool incremental, string where, Tally tally)
    {
        var before = Lines(previous);
        var cube = PivotEngine.NextCube(previousCube, query, answer, Declared.Fields);
        var next = PivotEngine.NextReport(previous, cube, layout, EnUs);

        SameReport(fresh, next, where);
        // Nothing is rewritten in place: the cube on screen lays out what it laid out.
        Assert.True(before.SequenceEqual(Lines(PivotEngine.Report(previousCube, previous.Layout, previous.Options))), $"{where}: the cube before changed");
        Assert.True(before.SequenceEqual(Lines(previous)), $"{where}: the report before changed");

        var earlier = previous.Rows.ToHashSet(ReferenceEqualityComparer.Instance);
        if (!incremental)
        {
            tally.Afresh++;
            Assert.True(next.Rows.All(row => !earlier.Contains(row)), $"{where}: a redraw that starts afresh shared a row");
            return (cube, next);
        }
        tally.Incremental++;
        Assert.Equal(previous.Rows.Count, next.Rows.Count);
        for (var i = 0; i < next.Rows.Count; i++)
        {
            var row = next.Rows[i];
            if (Painted(next, row) == Painted(previous, previous.Rows[i]))
            {
                Assert.True(ReferenceEquals(previous.Rows[i], row), $"{where}: row {i} ({row.Key}) painted the same text and was made anew");
                tally.SharedRows++;
            }
            else
            {
                Assert.True(!earlier.Contains(row), $"{where}: row {i} ({row.Key}) painted new text and was shared");
                tally.NewRows++;
            }
        }
        return (cube, next);
    }

    /// <summary>A layout ADR-0161 lays out afresh whatever the data did: an order that follows
    /// values, or a Show Values As that reads other rows.</summary>
    private static bool StartsAfresh(PivotLayout layout)
        => layout.Rows.Concat(layout.Columns).Any(p => p.Sort.ByValue is not null)
            || layout.Values.Any(v => v.ShowValuesAs != PivotShowValuesAs.NoCalculation);

    // ---- The oracle -------------------------------------------------------------------------------

    /// <summary>Whether two answers hold the same leaves in the same order, under the same Items
    /// spelled the same.</summary>
    private static bool SameStructure(PivotAnswer one, PivotAnswer other)
    {
        if (one.LeafCount != other.LeafCount)
            return false;
        var axes = one.Rows.Concat(one.Columns).ToArray();
        var theirs = other.Rows.Concat(other.Columns).ToArray();
        for (var level = 0; level < axes.Length; level++)
        {
            var mine = axes[level];
            var their = theirs[level];
            if (!mine.Items.Select(Spelled).SequenceEqual(their.Items.Select(Spelled), StringComparer.Ordinal))
                return false;
            for (var leaf = 0; leaf < one.LeafCount; leaf++)
            {
                if (mine.ItemAt(leaf) != their.ItemAt(leaf))
                    return false;
            }
        }
        return true;
    }

    private static string Spelled(PivotItemKey key) => key.Kind + ":" + key.Value;

    /// <summary>How many leaves of two answers of the same leaves hold other parts: a decimal of
    /// another value, a double of other bits.</summary>
    private static int ChangedLeaves(PivotAnswer one, PivotAnswer other)
    {
        var changed = 0;
        for (var leaf = 0; leaf < one.LeafCount; leaf++)
        {
            for (var v = 0; v < one.Values.Count; v++)
            {
                if (!SameParts(one.Values[v], other.Values[v], leaf))
                {
                    changed++;
                    break;
                }
            }
        }
        return changed;
    }

    private static bool SameParts(PivotAnswerValues mine, PivotAnswerValues theirs, int leaf)
    {
        if (mine.ValuesAt(leaf) != theirs.ValuesAt(leaf) || mine.NumbersAt(leaf) != theirs.NumbersAt(leaf) || mine.NonFiniteAt(leaf) != theirs.NonFiniteAt(leaf))
            return false;
        if ((mine.Parts & PivotParts.Sum) != 0 && !Same(mine.SumAt(leaf), theirs.SumAt(leaf)))
            return false;
        if ((mine.Parts & PivotParts.Extremes) != 0 && (!Same(mine.MinAt(leaf), theirs.MinAt(leaf)) || !Same(mine.MaxAt(leaf), theirs.MaxAt(leaf))))
            return false;
        if ((mine.Parts & PivotParts.Product) != 0 && Bits(mine.ProductAt(leaf)) != Bits(theirs.ProductAt(leaf)))
            return false;
        return (mine.Parts & PivotParts.Variance) == 0
            || (Bits(mine.MeanAt(leaf)) == Bits(theirs.MeanAt(leaf)) && Bits(mine.M2At(leaf)) == Bits(theirs.M2At(leaf)));
    }

    private static bool Same(PivotNumber one, PivotNumber other)
        => one.IsExact == other.IsExact && (one.IsExact ? one.ExactValue == other.ExactValue : Bits(one.Value) == Bits(other.Value));

    // ---- Reading reports --------------------------------------------------------------------------

    /// <summary>A row as painted: its labels and each value cell's text.</summary>
    private static string Painted(PivotReport report, PivotReportRow row)
        => string.Join(" | ", row.Labels.Select(label => label.Text ?? ""))
            + " || " + string.Join(" | ", Enumerable.Range(0, report.ValueColumns.Count).Select(column => report.ValueAt(row, column)?.Text ?? ""));

    private static string[] Lines(PivotReport report) => report.Rows.Select(row => Painted(report, row)).ToArray();

    /// <summary>Two reports alike: the same columns, spans and captions; row by row the same key,
    /// role, labels and cells — each value the same number to the last bit, the same exact decimal
    /// in the same form, the same error and the same text.</summary>
    private static void SameReport(PivotReport expected, PivotReport actual, string where)
    {
        Assert.True(expected.LabelColumns.SequenceEqual(actual.LabelColumns), $"{where}: label columns");
        Assert.True(expected.HeaderSpans.SequenceEqual(actual.HeaderSpans), $"{where}: header spans");
        Assert.True(expected.ValueCaptions.SequenceEqual(actual.ValueCaptions), $"{where}: value captions");
        Assert.True(expected.HeaderTierCount == actual.HeaderTierCount, $"{where}: header tiers");
        Assert.True(expected.Cube.IncludedRecordCount == actual.Cube.IncludedRecordCount, $"{where}: included records");
        Assert.True(expected.Cube.SourceVersion == actual.Cube.SourceVersion, $"{where}: source version");
        Assert.True(expected.ValueColumns.Count == actual.ValueColumns.Count, $"{where}: {expected.ValueColumns.Count} value columns against {actual.ValueColumns.Count}");
        for (var j = 0; j < expected.ValueColumns.Count; j++)
        {
            var (e, a) = (expected.ValueColumns[j], actual.ValueColumns[j]);
            Assert.True(e.Name == a.Name && e.Header == a.Header && e.Role == a.Role && e.ValueField == a.ValueField, $"{where}: value column {j}");
        }
        Assert.True(expected.Rows.Count == actual.Rows.Count, $"{where}: {expected.Rows.Count} rows against {actual.Rows.Count}");
        for (var i = 0; i < expected.Rows.Count; i++)
        {
            var (e, a) = (expected.Rows[i], actual.Rows[i]);
            var at = $"{where}, row {i} ({e.Key})";
            Assert.True(e.Key.Equals(a.Key) && e.Role == a.Role && e.ValueField == a.ValueField && e.CarriesValues == a.CarriesValues, $"{at}: what it stands for");
            Assert.True(e.Labels.SequenceEqual(a.Labels), $"{at}: labels");
            for (var j = 0; j < expected.ValueColumns.Count; j++)
            {
                var (ev, av) = (expected.ValueAt(e, j), actual.ValueAt(a, j));
                var same = ev is null
                    ? av is null
                    : av is not null && ev.Text == av.Text && ev.Error == av.Error && Bits(ev.Number) == Bits(av.Number)
                        && (ev.Exact is { } x ? av.Exact is { } y && decimal.GetBits(x).SequenceEqual(decimal.GetBits(y)) : av.Exact is null);
                Assert.True(same, $"{at}, column {j}: {ev?.Text ?? "(empty)"} against {av?.Text ?? "(empty)"}");
            }
        }
    }

    // ---- The data and the layouts -----------------------------------------------------------------

    private static readonly PivotAggregation[] Aggregations = Enum.GetValues<PivotAggregation>();

    /// <summary>
    /// A layout of the seed's <paramref name="k"/>th turn: up to two row fields and a column field,
    /// one to three Value Fields — each Aggregation in turn across the seeds, now and then a Show
    /// Values As or a number format that hides a change — each form in turn, the values on columns
    /// and on rows in turn, subtotals on and off, at the top and at the bottom, an Item collapsed or
    /// hidden now and then, and now and then an order by value.
    /// </summary>
    private static PivotLayout RandomLayout(Random random, int seed, int k)
    {
        string[] axes = ["Region", "Desk", "Product", "Month", "Live"];
        var order = axes.OrderBy(_ => random.Next()).ToArray();
        var rowCount = random.Next(4) switch { 0 => 1, 3 => 0, _ => 2 };
        var columnCount = rowCount == 0 || random.Next(3) == 0 ? 1 : 0;
        var valueCount = k % 2 == 0 ? random.Next(1, 4) : random.Next(2, 4);
        var values = new List<PivotValueField>();
        string[] numbers = ["Amount", "Risk", "Quantity"];
        for (var v = 0; v < valueCount; v++)
        {
            var aggregation = Aggregations[((seed * 7) + (k * 3) + v) % Aggregations.Length];
            var counting = aggregation is PivotAggregation.Count or PivotAggregation.CountNumbers && random.Next(3) == 0;
            var field = counting ? (random.Next(2) == 0 ? "Product" : "Date") : numbers[random.Next(numbers.Length)];
            values.Add(new PivotValueField(field, aggregation)
            {
                ShowValuesAs = random.Next(6) == 0 ? (PivotShowValuesAs)random.Next(1, 4) : PivotShowValuesAs.NoCalculation,
                NumberFormat = random.Next(5) switch { 0 => "0", 1 => "#,##0.00", _ => null },
            });
        }
        PivotFieldPlacement Place(string field, bool outer) => new(field)
        {
            Subtotals = random.Next(4) != 0,
            Collapsed = outer && random.Next(10) == 0,
            ToggledItems = outer && random.Next(6) == 0 ? [PivotItemKey.Text("East")] : [],
            HiddenItems = random.Next(8) == 0 ? [field == "Live" ? PivotItemKey.Boolean(false) : PivotItemKey.Text("North")] : [],
            Sort = random.Next(10) switch
            {
                0 => new PivotSort(random.Next(2) == 0 ? PivotSortDirection.Ascending : PivotSortDirection.Descending, random.Next(valueCount)),
                1 or 2 => PivotSort.Descending,
                _ => PivotSort.Ascending,
            },
        };
        var rows = order.Take(rowCount).Select((field, at) => Place(field, at < rowCount - 1)).ToArray();
        var columns = order.Skip(rowCount).Take(columnCount).Select(field => Place(field, false)).ToArray();
        var filters = random.Next(4) == 0 ? new[] { new PivotFieldPlacement(order[^1]) { HiddenItems = [order[^1] == "Live" ? PivotItemKey.Boolean(true) : PivotItemKey.Text("West")] } } : [];
        return new PivotLayout
        {
            Rows = rows,
            Columns = columns,
            Filters = filters,
            Values = values,
            Form = (PivotReportForm)((seed + k) % 3),
            ValuesAxis = k % 2 == 0 ? PivotAxis.Columns : PivotAxis.Rows,
            SubtotalsAtTop = random.Next(2) == 0,
            GrandTotalRow = random.Next(5) != 0,
            GrandTotalColumn = random.Next(5) != 0,
            RepeatItemLabels = random.Next(3) == 0,
        };
    }

    /// <summary>
    /// Trades that change at random, as a live feed changes them: mostly a few amended in place,
    /// some amended to what they were, some moved to another region, desk or product, some added and
    /// some removed, and now and then a third of them at once. Money at new scales, doubles that are
    /// not finite, and a region spelled anew now and then.
    /// </summary>
    private sealed class World(Random random)
    {
        private static readonly string[] Regions = ["East", "West", "North", "South", "Central"];
        private static readonly string[] Desks = ["Rates", "Credit", "FX", "Equity"];
        private static readonly string[] Products = ["Bond", "Swap", "Option"];
        private readonly Dictionary<long, Trade> _held = [];
        private long _nextId = 1;

        public Trade[] Initial(int count) => [.. Enumerable.Range(0, count).Select(_ => Hold(New()))];

        public (ChangeBatch Batch, string Kind) Next(PivotFields<Trade> fields)
        {
            var keys = _held.Keys.ToArray();
            Trade Pick() => _held[keys[random.Next(keys.Length)]];
            var roll = random.Next(100);
            var changed = new Dictionary<long, Trade>();
            var added = new List<Trade>();
            var removed = new HashSet<long>();
            string kind;
            if (roll < 55)
            {
                kind = "amended";
                for (var i = random.Next(1, 5); i > 0; i--)
                {
                    var trade = Pick();
                    changed[trade.Id] = trade with { Amount = Amount(), Risk = Risk(), Quantity = Quantity() };
                }
            }
            else if (roll < 62)
            {
                kind = "amended to the same";
                var trade = Pick();
                changed[trade.Id] = trade;
            }
            else if (roll < 72)
            {
                kind = "moved";
                var trade = Pick();
                changed[trade.Id] = random.Next(3) switch
                {
                    0 => trade with { Region = Regions[random.Next(Regions.Length)] },
                    1 => trade with { Desk = Desks[random.Next(Desks.Length)] },
                    _ => trade with { Product = Products[random.Next(Products.Length)] },
                };
            }
            else if (roll < 82)
            {
                kind = "added";
                for (var i = random.Next(1, 4); i > 0; i--)
                    added.Add(New());
            }
            else if (roll < 93)
            {
                kind = "removed";
                for (var i = random.Next(1, 4); i > 0; i--)
                    removed.Add(Pick().Id);
            }
            else
            {
                kind = "many amended";
                foreach (var key in keys.Where(_ => random.Next(3) == 0))
                    changed[key] = _held[key] with { Amount = Amount(), Risk = Risk() };
            }
            foreach (var key in removed)
            {
                changed.Remove(key);
                _held.Remove(key);
            }
            foreach (var trade in changed.Values.Concat(added))
                Hold(trade);
            return (fields.Batch(added: added, changed: changed.Values, removedKeys: removed.Select(key => (object)key)), kind);
        }

        private Trade Hold(Trade trade)
        {
            _held[trade.Id] = trade;
            return trade;
        }

        private Trade New()
        {
            var region = Regions[random.Next(Regions.Length)];
            if (random.Next(30) == 0)
                region = region.ToUpperInvariant();
            return new Trade(
                _nextId++,
                region,
                Desks[random.Next(Desks.Length)],
                Products[random.Next(Products.Length)],
                Amount(),
                Risk(),
                Quantity(),
                new DateTime(2026, 1, 1).AddDays(random.Next(0, 120)),
                random.Next(2) == 0);
        }

        // Cents mostly; now and then four or six places, a value far past cents, or none.
        private decimal? Amount() => random.Next(30) switch
        {
            0 => null,
            1 => Math.Round((decimal)random.NextDouble() * 1000m, 6),
            2 => Math.Round((decimal)random.NextDouble() * 1000m, 4),
            3 => 12345678901.123456789m * (random.Next(2) == 0 ? 1 : -1),
            4 => 0m,
            _ => random.Next(-1_000_000, 1_000_000) / 100m,
        };

        private double? Risk() => random.Next(40) switch
        {
            0 => null,
            1 => double.NaN,
            2 => double.PositiveInfinity,
            3 => -0.0,
            _ => Math.Round((random.NextDouble() - 0.5) * 1000, random.Next(0, 6)),
        };

        private long? Quantity() => random.Next(15) == 0 ? null : random.Next(-1000, 1000);
    }
}
