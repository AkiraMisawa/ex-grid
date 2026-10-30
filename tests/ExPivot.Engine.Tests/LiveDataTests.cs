using System.Globalization;
using ExGrid.Data;
using ExPivot.Engine;
using Xunit;
using static ExPivot.Engine.Tests.Sources;

namespace ExPivot.Engine.Tests;

/// <summary>
/// Live data in the bundled source (ADR-0067, PV-34): a Change Batch makes the next Snapshot, and
/// the answer the source holds is brought up to date from what the batch removed and added — the
/// exact parts by subtraction and addition, every other part recomputed for the leaves it touched
/// — so that after any sequence of batches every leaf equals a fresh aggregation of the Snapshot
/// they made, to the last bit. What both engines answer to a batch — Items that come and leave,
/// spellings, notices, refusals, the versions still answered, the cap, exact sums — is the shared
/// cases' (<c>tests/cases/pivot/source-live.json</c>); these hold how this engine folds.
/// </summary>
public class LiveDataTests
{
    internal sealed record Trade(long Id, string? Desk, string? Book, decimal? Amount, double? Risk, long? Quantity, DateTime? Date, bool? Live);

    internal static PivotFields<Trade> Declarations() => PivotFields.Of<Trade>()
        .Key("Id", t => t.Id)
        .Text("Desk", t => t.Desk)
        .Text("Book", t => t.Book)
        .Number("Amount", t => t.Amount)
        .Number("Risk", t => t.Risk)
        .Number("Quantity", t => t.Quantity)
        .Date("Date", t => t.Date)
        .Month("Month", of: "Date")
        .Boolean("Live", t => t.Live);

    private const PivotParts All = PivotParts.Sum | PivotParts.Extremes | PivotParts.Product | PivotParts.Variance;

    /// <summary>Questions that between them reach every part, every kind of Item on an axis — text
    /// in several spellings, exact and double numbers, dates, a date part, Booleans, #NUM! and
    /// (blank) — Hidden Items in each Area, and a lone grand total.</summary>
    private static PivotQuery[] Questions =>
    [
        new(rows: [F("Desk")], columns: [F("Book")], values: [V("Amount", All), V("Risk", All), V("Quantity", PivotParts.Sum | PivotParts.Extremes), V("Desk", PivotParts.Counts)]),
        new(rows: [F("Desk", PivotItemKey.Text("credit"), PivotItemKey.Blank)], filters: [F("Live", PivotItemKey.Boolean(false))], values: [V("Amount", PivotParts.Sum), V("Risk", PivotParts.Sum)]),
        new(rows: [F("Month")], columns: [F("Live")], values: [V("Amount", PivotParts.Sum | PivotParts.Variance), V("Date", PivotParts.Counts), V("Month", PivotParts.Counts)]),
        new(rows: [F("Book"), F("Desk")], values: [V("Risk", PivotParts.Extremes | PivotParts.Product), V("Amount", PivotParts.Sum), V("Live", PivotParts.Counts)]),
        new(values: [V("Amount", All), V("Risk", All), V("Quantity", All)]),
        new(rows: [F("Date")], columns: [F("Desk", PivotItemKey.Text(""))], values: [V("Quantity", PivotParts.Sum)]),
        new(columns: [F("Amount")], values: [V("Risk", PivotParts.Sum), V("Quantity", PivotParts.Counts)]),
        new(rows: [F("Risk")], filters: [F("Book", PivotItemKey.Text("ny-2"))], values: [V("Amount", PivotParts.Sum | PivotParts.Extremes)]),
    ];

    [Theory] // ADR-0067 (PV-34): after any sequence of batches, every leaf equals a fresh aggregation of the Snapshot they made, to the last bit
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task Every_leaf_of_a_folded_answer_equals_a_fresh_aggregation(int seed)
    {
        var random = new Random(seed);
        var world = new World(random);
        var fields = Declarations();
        var start = fields.Build(world.Initial(300));
        var questions = Questions;
        var read = new long[questions.Length];
        var sources = questions.Select((_, q) => PivotSource.From(start, fields.Fields, new PivotSlicing { RowsRead = rows => read[q] += rows })).ToArray();
        for (var q = 0; q < questions.Length; q++)
            Assert.False((await sources[q].AggregateAsync(questions[q], Ct)).IsRefused);

        var folded = 0;
        for (var b = 0; b < 90; b++)
        {
            var batch = world.Next(fields);
            var compacted = false;
            for (var q = 0; q < questions.Length; q++)
            {
                var change = sources[q].Apply(batch);
                compacted |= change.Compacted;
                read[q] = 0;
                var answer = await sources[q].AggregateAsync(questions[q], Ct);
                // Folded, not read again — unless the batch compacted the Snapshot, which moved
                // its rows, and the question was asked afresh.
                if (!change.Compacted)
                    Assert.Equal(0, read[q]);
                var fresh = await PivotSource.From(sources[q].Snapshot, fields.Fields).AggregateAsync(questions[q], Ct);
                SameLeaves(fresh, answer, $"seed {seed}, batch {b}, question {q}");
            }
            if (!compacted)
                folded++;
        }
        // The batches reached every case the fold has.
        Assert.True(folded > 60, $"{folded} batches folded");
        Assert.True(world.Removed > 100 && world.Changed > 100 && world.Added > 100);
    }

    [Fact] // ADR-0067: a Source Version names the source as well as the Snapshot's version: two sources over one Snapshot answer under two, so a new source is a refresh
    public async Task Two_sources_over_one_snapshot_answer_under_two_versions()
    {
        var fields = Declarations();
        var snapshot = fields.Build([Row(1, "East", 1m)]);

        var one = await PivotSource.From(snapshot, fields.Fields).AggregateAsync(new PivotQuery(), Ct);
        var other = await PivotSource.From(snapshot, fields.Fields).AggregateAsync(new PivotQuery(), Ct);

        Assert.EndsWith(":0", one.SourceVersion);
        Assert.NotEqual(one.SourceVersion, other.SourceVersion);
    }

    [Fact] // ADR-0370 s3 (note of 2026-10-30): a batch applied is raised as a server's Change Batch, from the version before to the one after, ahead of the notice
    public async Task A_batch_applied_is_raised_as_a_servers_change_batch_ahead_of_its_notice()
    {
        var fields = Declarations();
        var source = PivotSource.From(fields.Build([Row(1, "East", 1m), Row(2, "West", 2m)]), fields.Fields);
        var before = source.SourceVersion;
        var heard = new List<object>();
        source.Batched += heard.Add;
        source.Changed += heard.Add;

        source.Apply(fields.Batch(added: [Row(3, "North", 3m)], changed: [Row(1, "East", 5m)], removedKeys: [2L]));

        var batch = Assert.IsType<PivotServerChangeBatch>(heard[0]);
        Assert.Equal(new PivotSourceChanged(source.SourceVersion), heard[1]);
        Assert.Equal(before, batch.From);
        Assert.Equal(source.SourceVersion, batch.To);
        Assert.Equal(fields.Fields.Select(f => f.Name), batch.Fields.Select(f => f.Name));
        var amount = fields.Fields.ToList().FindIndex(f => f.Name == "Amount");
        Assert.Equal([1m, 2m], batch.Removed.Select(r => (decimal)r.Values[amount]!).Order());
        Assert.Equal([3m, 5m], batch.Added.Select(r => (decimal)r.Values[amount]!).Order());
        // What the fetching source reads, as a server writes it.
        Assert.Equal(2, PivotJson.ReadServerChangeBatch(PivotJson.Write(batch)).Added.Count);
        // The records it carries are the ones a cell's records name.
        var details = await source.DetailsAsync(new PivotDetailsQuery(source.SourceVersion), Ct);
        Assert.Contains(details.Records, r => r.Values[amount] is 5m);
    }

    [Fact] // ADR-0067: a batch that compacts the Snapshot moves its rows; the source then answers afresh, as a fresh aggregation does
    public async Task A_compacting_batch_is_answered_afresh()
    {
        var fields = Declarations();
        var records = Enumerable.Range(1, 40).Select(i => Row(i, i % 2 == 0 ? "East" : "West", i)).ToArray();
        long read = 0;
        var source = PivotSource.From(fields.Build(records), fields.Fields, new PivotSlicing { RowsRead = rows => read += rows });
        var query = new PivotQuery(rows: [F("Desk")], values: [V("Amount", PivotParts.Sum | PivotParts.Extremes)]);
        await source.AggregateAsync(query, Ct);

        // Each batch adds a slice; past 64 of them they are merged, and every row has moved.
        SnapshotChange? compaction = null;
        for (var i = 0; compaction is null; i++)
        {
            var change = source.Apply(fields.Batch(changed: [Row(1 + (i % 40), "East", i)]));
            read = 0;
            var answer = await source.AggregateAsync(query, Ct);
            if (change.Compacted)
            {
                compaction = change;
                Assert.True(read > 0);
            }
            else
            {
                Assert.Equal(0, read);
            }
            SameLeaves(await PivotSource.From(source.Snapshot, fields.Fields).AggregateAsync(query, Ct), answer, $"batch {i}");
        }
    }

    [Fact] // ADR-0067: a source over a Snapshot without a Record Key folds a batch that adds, rather than reading its rows again
    public async Task A_snapshot_without_a_key_folds_batches_that_add()
    {
        var columns = new SnapshotBuilder<Trade>().Text("Desk", t => t.Desk).Decimal("Amount", t => t.Amount).Double("Risk", t => t.Risk);
        long read = 0;
        var source = PivotSource.From(columns.Build([Row(1, "East", 1m), Row(2, "West", 2m)]), slicing: new PivotSlicing { RowsRead = rows => read += rows });
        var query = new PivotQuery(rows: [F("Desk")], values: [V("Amount", PivotParts.Sum), V("Risk", All)]);
        await source.AggregateAsync(query, Ct);

        source.Apply(columns.Batch(added: [Row(3, "east", 4.5m) with { Risk = 0.1 }, Row(4, "South", 1m)]));
        read = 0;
        var answer = await source.AggregateAsync(query, Ct);

        Assert.Equal(0, read);
        SameLeaves(await PivotSource.From(source.Snapshot).AggregateAsync(query, Ct), answer, "added");
    }

    [Fact] // ADR-0067: a part that cannot be subtracted is recomputed from the records of the leaves the batch touched, and no others
    public async Task Only_the_touched_leaves_records_are_read_again()
    {
        var fields = Declarations();
        // A hundred desks of ten records each, their risk summed in double.
        var trades = Enumerable.Range(0, 1_000).Select(i => Row(i, "D" + (i % 100).ToString(CultureInfo.InvariantCulture), 1m) with { Risk = i * 0.1 }).ToArray();
        var source = PivotSource.From(trades, fields);
        var query = new PivotQuery(rows: [F("Desk")], values: [V("Risk", PivotParts.Sum), V("Amount", PivotParts.Sum)]);
        await source.AggregateAsync(query, Ct);

        // One record of D7 changed: D7's nine others and the changed one are read, and nothing else.
        source.Apply(fields.Batch(changed: [trades[7] with { Risk = 5 }]));
        var one = source.HeldPass!.RecomputedRows;
        SameLeaves(await PivotSource.From(source.Snapshot, fields.Fields).AggregateAsync(query, Ct), await source.AggregateAsync(query, Ct), "one");
        // Most desks touched at once: one sweep over the column reads the rows of those desks.
        source.Apply(fields.Batch(removedKeys: [.. Enumerable.Range(100, 60).Select(i => (object)(long)i)]));
        var many = source.HeldPass!.RecomputedRows;
        SameLeaves(await PivotSource.From(source.Snapshot, fields.Fields).AggregateAsync(query, Ct), await source.AggregateAsync(query, Ct), "swept");

        Assert.Equal(10, one);
        Assert.Equal(60 * 9, many);
    }

    // ---- The data -------------------------------------------------------------------------------

    internal static Trade Row(long id, string? desk, decimal? amount)
        => new(id, desk, "LDN-1", amount, 1.0, 1, new DateTime(2026, 1, 2), true);

    /// <summary>
    /// Two answers hold the same leaves: the same refusal, or the same Items with the same
    /// spellings, and each leaf — matched by its Items, since a source orders its leaves its own
    /// way — with the same records and every part equal to the last bit: a <c>decimal</c> in the same
    /// form, a <c>double</c> with the same bits.
    /// </summary>
    internal static void SameLeaves(PivotAnswer expected, PivotAnswer actual, string where)
    {
        Assert.True(Equals(expected.Refusal, actual.Refusal), $"{where}: refusal {expected.Refusal} against {actual.Refusal}");
        if (expected.IsRefused)
            return;
        Assert.True(expected.LeafCount == actual.LeafCount, $"{where}: {expected.LeafCount} leaves against {actual.LeafCount}");
        var axes = expected.Rows.Concat(expected.Columns).ToArray();
        var theirAxes = actual.Rows.Concat(actual.Columns).ToArray();
        for (var level = 0; level < axes.Length; level++)
        {
            var mine = axes[level].Items.Select(Spelled).Order(StringComparer.Ordinal);
            var theirs = theirAxes[level].Items.Select(Spelled).Order(StringComparer.Ordinal);
            Assert.True(mine.SequenceEqual(theirs), $"{where}: the Items of {axes[level].Field} are [{string.Join(", ", mine)}] against [{string.Join(", ", theirs)}]");
        }
        var leaves = Enumerable.Range(0, actual.LeafCount).ToDictionary(leaf => PathOf(theirAxes, leaf), StringComparer.Ordinal);
        for (var leaf = 0; leaf < expected.LeafCount; leaf++)
        {
            var path = PathOf(axes, leaf);
            Assert.True(leaves.TryGetValue(path, out var other), $"{where}: no leaf at {path}");
            var at = $"{where}, leaf {path}";
            Assert.True(expected.RecordsAt(leaf) == actual.RecordsAt(other), $"{at}: {expected.RecordsAt(leaf)} records against {actual.RecordsAt(other)}");
            for (var v = 0; v < expected.Values.Count; v++)
            {
                var mine = expected.Values[v];
                var theirs = actual.Values[v];
                var field = $"{at}, {mine.Field}";
                Assert.True(mine.ValuesAt(leaf) == theirs.ValuesAt(other), $"{field}: values");
                Assert.True(mine.NumbersAt(leaf) == theirs.NumbersAt(other), $"{field}: numbers");
                Assert.True(mine.NonFiniteAt(leaf) == theirs.NonFiniteAt(other), $"{field}: non-finite");
                if ((mine.Parts & PivotParts.Sum) != 0)
                    SameNumber(mine.SumAt(leaf), theirs.SumAt(other), field + ": sum");
                if ((mine.Parts & PivotParts.Extremes) != 0)
                {
                    SameNumber(mine.MinAt(leaf), theirs.MinAt(other), field + ": min");
                    SameNumber(mine.MaxAt(leaf), theirs.MaxAt(other), field + ": max");
                }
                if ((mine.Parts & PivotParts.Product) != 0)
                    Assert.True(Bits(mine.ProductAt(leaf)) == Bits(theirs.ProductAt(other)), $"{field}: product {mine.ProductAt(leaf):R} against {theirs.ProductAt(other):R}");
                if ((mine.Parts & PivotParts.Variance) != 0)
                {
                    Assert.True(Bits(mine.MeanAt(leaf)) == Bits(theirs.MeanAt(other)), $"{field}: mean {mine.MeanAt(leaf):R} against {theirs.MeanAt(other):R}");
                    Assert.True(Bits(mine.M2At(leaf)) == Bits(theirs.M2At(other)), $"{field}: M2 {mine.M2At(leaf):R} against {theirs.M2At(other):R}");
                }
            }
        }
    }

    private static void SameNumber(PivotNumber expected, PivotNumber actual, string where)
    {
        var same = expected.IsExact == actual.IsExact
            && (expected.IsExact
                ? decimal.GetBits(expected.ExactValue).SequenceEqual(decimal.GetBits(actual.ExactValue))
                : Bits(expected.Value) == Bits(actual.Value));
        Assert.True(same, $"{where}: {expected} against {actual}");
    }

    private static string Spelled(PivotItemKey key) => key.Kind + ":" + key.Value;

    private static string PathOf(PivotAnswerAxis[] axes, int leaf)
        => string.Join(" | ", axes.Select(axis => Spelled(axis.Items[axis.ItemAt(leaf)])));

    /// <summary>A record set that changes at random: records added, changed and removed by their
    /// key, with text in several spellings, money at new scales and beyond 64 bits, non-finite
    /// doubles, and Items that appear and leave.</summary>
    private sealed class World(Random random)
    {
        private static readonly string?[] Desks = ["Rates", "RATES", "rates", "Credit", "CREDIT", "credit", "FX", "Fx", "", null];
        private static readonly string?[] Books = ["LDN-1", "ldn-1", "NY-2", "TKY-3", null];
        private readonly Dictionary<long, Trade> _held = [];
        private long _nextId = 1;

        public int Added { get; private set; }

        public int Changed { get; private set; }

        public int Removed { get; private set; }

        public Trade[] Initial(int count) => [.. Enumerable.Range(0, count).Select(_ => Hold(New()))];

        public ChangeBatch Next(PivotFields<Trade> fields)
        {
            // Now and then a desk that has not been seen, and now and then every record of one leaves.
            var keys = _held.Keys.ToArray();
            var removed = new HashSet<long>();
            if (random.Next(8) == 0 && keys.Length > 0)
            {
                var desk = _held[keys[random.Next(keys.Length)]].Desk;
                foreach (var key in keys.Where(k => string.Equals(_held[k].Desk, desk, StringComparison.OrdinalIgnoreCase)).Take(40))
                    removed.Add(key);
            }
            for (var i = random.Next(0, 8); i > 0 && keys.Length > 0; i--)
                removed.Add(keys[random.Next(keys.Length)]);
            var changed = new Dictionary<long, Trade>();
            for (var i = random.Next(0, 10); i > 0 && keys.Length > 0; i--)
            {
                var key = keys[random.Next(keys.Length)];
                if (!removed.Contains(key))
                    changed[key] = New() with { Id = key };
            }
            var added = Enumerable.Range(0, random.Next(0, 10)).Select(_ => New()).ToArray();
            foreach (var key in removed)
                _held.Remove(key);
            foreach (var trade in changed.Values.Concat(added))
                Hold(trade);
            Added += added.Length;
            Changed += changed.Count;
            Removed += removed.Count;
            return fields.Batch(added: added, changed: changed.Values, removedKeys: removed.Select(k => (object)k));
        }

        private Trade Hold(Trade trade)
        {
            _held[trade.Id] = trade;
            return trade;
        }

        private Trade New()
        {
            var desk = random.Next(25) == 0 ? "Desk" + random.Next(3).ToString(CultureInfo.InvariantCulture) : Desks[random.Next(Desks.Length)];
            return new Trade(
                _nextId++,
                desk,
                Books[random.Next(Books.Length)],
                Amount(),
                Risk(),
                random.Next(10) == 0 ? null : random.Next(-1000, 1000),
                random.Next(10) == 0 ? null : new DateTime(2026, 1, 1).AddDays(random.Next(0, 400)).AddHours(random.Next(4) == 0 ? random.Next(24) : 0),
                random.Next(10) == 0 ? null : random.Next(2) == 0);
        }

        // Cents mostly; now and then four or six places, which give a batch's slice a scale of its
        // own; rarely a value no 64-bit integer holds at its scale, and rarely one near decimal's
        // edge, whose sums leave decimal for double.
        private decimal? Amount() => random.Next(40) switch
        {
            0 => null,
            1 => Math.Round((decimal)random.NextDouble() * 1000m, 6),
            2 => Math.Round((decimal)random.NextDouble() * 1000m, 4),
            3 => 12345678901.123456789m * (random.Next(2) == 0 ? 1 : -1),
            4 when random.Next(4) == 0 => 30_000_000_000_000_000_000_000_000_000m,
            5 => 0m,
            _ => random.Next(-10_000_000, 10_000_000) / 100m,
        };

        private double? Risk() => random.Next(40) switch
        {
            0 => null,
            1 => double.NaN,
            2 => double.PositiveInfinity,
            3 => double.NegativeInfinity,
            4 => -0.0,
            5 => 1e300 * (random.Next(2) == 0 ? 10 : 1),
            _ => Math.Round((random.NextDouble() - 0.5) * 1000, random.Next(0, 6)),
        };
    }
}
