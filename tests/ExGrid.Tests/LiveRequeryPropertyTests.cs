using System.Globalization;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// LV-5 (ADR-0141/0023): the incremental requery of <c>GridSource.From</c> with a Row Key equals
/// <see cref="GridQueryEngine.Apply{TRow}"/> over the same rows, Filter and Sorts exactly, position
/// by position and by reference — the stable tie order included — and equals the same changes made
/// one <c>ReplaceRow</c> at a time. Ported from M5's check (<c>spikes/live-update/Requery/
/// PropertyCheck.cs</c>), over the public surface: random bases, random Sorts of 0–3 levels over
/// every column type in either direction, random Filters over every operator each type allows, and
/// random batches of changes, additions and removals, from none to every row — so both the
/// incremental path and the whole requery a large batch falls back to are compared. Several
/// batches are gathered between two publications, so a key changed twice, removed and added again,
/// or added and removed within one interval is folded as the source folds it. The values sit in the
/// corners: Blanks, ties (1, 1.0, 1.00), case pairs, the Turkish i, ß against ss, accents, the empty
/// string, equal DateTimes of different Kinds.
///
/// <para>Also checked after every publication: the Row Sequence Version moved exactly when the
/// sequence of keys did (LV-7). Half the cases run under the Turkish culture, where a
/// culture-aware comparison would answer differently; ordinal comparison must not.</para>
///
/// <para>The seed is recorded in the test, and every failure names it with the case and the
/// batch, so a failure is reproduced by running the test again.</para>
/// </summary>
public class LiveRequeryPropertyTests
{
    private const int Seed = 20261006;
    private const int Cases = 2_500;

    private sealed class Row(int id, string? book, string? desk, decimal? pnl, decimal? notional, DateTime? tradeDate, bool? confirmed)
    {
        public int Id { get; } = id;
        public string? Book { get; } = book;
        public string? Desk { get; } = desk;
        public decimal? Pnl { get; } = pnl;
        public decimal? Notional { get; } = notional;
        public DateTime? TradeDate { get; } = tradeDate;
        public bool? Confirmed { get; } = confirmed;

        public Row With(int field, Random random) => field switch
        {
            0 => new Row(Id, Pick(random, Texts), Desk, Pnl, Notional, TradeDate, Confirmed),
            1 => new Row(Id, Book, Pick(random, Texts), Pnl, Notional, TradeDate, Confirmed),
            2 => new Row(Id, Book, Desk, Pick(random, Numbers), Notional, TradeDate, Confirmed),
            3 => new Row(Id, Book, Desk, Pnl, Pick(random, Numbers), TradeDate, Confirmed),
            4 => new Row(Id, Book, Desk, Pnl, Notional, Pick(random, Dates), Confirmed),
            _ => new Row(Id, Book, Desk, Pnl, Notional, TradeDate, Pick(random, Bools)),
        };

        public Row Copy() => new(Id, Book, Desk, Pnl, Notional, TradeDate, Confirmed);

        public override string ToString() => $"#{Id} {Book}/{Desk} pnl={Pnl} n={Notional} d={TradeDate:O} c={Confirmed}";
    }

    private static readonly string?[] Texts =
        ["a", "A", "b", "B", "ab", "AB", "aB", "ä", "Ä", "ss", "SS", "ß", "i", "I", "ı", "İ", "", " ", "z", "Z", "é", "é", null];

    private static readonly decimal?[] Numbers =
        [null, -1m, 0m, 0.0m, 1m, 1.0m, 1.00m, 2.5m, -2.5m, 1000000m, 0.01m, -0.01m, decimal.MaxValue];

    private static readonly DateTime?[] Dates =
    [
        null,
        new DateTime(2026, 1, 1),
        new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        new DateTime(2026, 1, 2),
        new DateTime(2025, 12, 31, 23, 59, 59),
    ];

    private static readonly bool?[] Bools = [null, true, false];

    private static readonly IReadOnlyList<ColumnInfo<Row>> Columns =
    [
        new("Id", ColumnType.Number, r => r.Id),
        new("Book", ColumnType.Text, r => r.Book),
        new("Desk", ColumnType.Text, r => r.Desk),
        new("Pnl", ColumnType.Number, r => r.Pnl),
        new("Notional", ColumnType.Number, r => r.Notional),
        new("TradeDate", ColumnType.Date, r => r.TradeDate),
        new("Confirmed", ColumnType.Boolean, r => r.Confirmed),
    ];

    private static readonly string[] Queryable = ["Id", "Book", "Desk", "Pnl", "Notional", "TradeDate", "Confirmed"];

    [Fact] // ADR-0141 / LV-5: the incremental requery equals GridQueryEngine.Apply and ReplaceRow one at a time, over random batches
    public void The_incremental_requery_equals_the_reference_over_random_batches()
    {
        var culture = CultureInfo.CurrentCulture;
        try
        {
            var outcome = Run(Seed, Cases);
            Assert.True(outcome.Failures.Count == 0,
                $"Seed {Seed}: {outcome.Failures.Count} failure(s) in {outcome.Publications} publications.\n"
                + string.Join("\n", outcome.Failures.Take(5)));
            // The corners were reached, so the comparison was not vacuous.
            Assert.True(outcome.Coverage.GetValueOrDefault("incremental-sized") > 1_000, Describe(outcome));
            Assert.True(outcome.Coverage.GetValueOrDefault("whole-sized") > 100, Describe(outcome));
            Assert.True(outcome.Coverage.GetValueOrDefault("sequence moved") > 500, Describe(outcome));
            Assert.True(outcome.Coverage.GetValueOrDefault("sequence kept") > 500, Describe(outcome));
            Assert.True(outcome.Coverage.GetValueOrDefault("key removed and added again") > 20, Describe(outcome));
            Assert.True(outcome.ReplaceRowPublications > 200, Describe(outcome));
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    private static string Describe(Outcome outcome)
        => $"seed {Seed}: {outcome.Publications} publications, {outcome.ReplaceRowPublications} against ReplaceRow; "
            + string.Join(", ", outcome.Coverage.OrderBy(c => c.Key, StringComparer.Ordinal).Select(c => $"{c.Key} {c.Value}"));

    private sealed record Outcome(int Publications, int ReplaceRowPublications, List<string> Failures, Dictionary<string, int> Coverage);

    private static Outcome Run(int seed, int cases)
    {
        var random = new Random(seed);
        var failures = new List<string>();
        var coverage = new Dictionary<string, int>(StringComparer.Ordinal);
        void Count(string what) => coverage[what] = coverage.GetValueOrDefault(what) + 1;
        var publications = 0;
        var replaceRowPublications = 0;
        var nextId = 0;

        Row NewRow() => new(nextId++, Pick(random, Texts), Pick(random, Texts), Pick(random, Numbers), Pick(random, Numbers),
            Pick(random, Dates), Pick(random, Bools));

        Row Changed(Row row)
        {
            // Sometimes a new instance with every value the same: a change the row cannot show.
            if (random.Next(10) == 0)
                return row.Copy();
            var changed = row;
            for (var f = random.Next(1, 4); f > 0; f--)
                changed = changed.With(random.Next(6), random);
            return changed == row ? row.Copy() : changed;
        }

        for (var c = 0; c < cases && failures.Count < 10; c++)
        {
            CultureInfo.CurrentCulture = c % 2 == 0 ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo("tr-TR");
            var n = random.Next(10) == 0 ? random.Next(0, 4) : random.Next(1, 300);
            var baseRows = new List<Row>(n);
            for (var i = 0; i < n; i++)
                baseRows.Add(NewRow());
            var sorts = RandomSorts(random);
            var filter = RandomFilter(random);

            var clock = new FakeTimeProvider();
            var source = GridSource.From(baseRows, r => r.Id, clock);
            source.OnColumnsChanged(Columns);
            source.OnFilterChanged(filter);
            source.OnSortChanged(sorts);
            var reference = GridQueryEngine.Apply(baseRows, Columns, filter, sorts);
            if (!SameRows(source.Window, reference, out var why))
            {
                failures.Add($"case {c}: the first result differs: {why}");
                continue;
            }

            // Every publication, as the grid would see it: the version moved exactly when the sequence
            // of keys did, against the publication before it (LV-7).
            var published = source.Window.ToArray();
            var publishedVersion = source.RowSequenceVersion;
            var caseNumber = c;
            source.StateChanged += () =>
            {
                publications++;
                var now = source.Window.ToArray();
                var moved = !SameKeys(published, now);
                Count(moved ? "sequence moved" : "sequence kept");
                if (source.RowSequenceVersion != publishedVersion + (moved ? 1 : 0))
                {
                    failures.Add($"case {caseNumber}: a publication took the version from {publishedVersion} to "
                        + $"{source.RowSequenceVersion}, and the sequence {(moved ? "moved" : "did not move")}");
                }
                published = now;
                publishedVersion = source.RowSequenceVersion;
            };

            // A third of the cases change rows only, and are also replayed on a source without a key,
            // one ReplaceRow at a time.
            var replaceOnly = random.Next(3) == 0;
            InMemoryGridSource<Row>? oneAtATime = null;
            if (replaceOnly)
            {
                oneAtATime = GridSource.From(baseRows);
                oneAtATime.OnColumnsChanged(Columns);
                oneAtATime.OnFilterChanged(filter);
                oneAtATime.OnSortChanged(sorts);
            }

            for (var step = random.Next(1, 7); step > 0 && failures.Count < 10; step--)
            {
                // The interval has passed since the last publication, so the first batch is published
                // at once and the others are gathered into one publication at the interval's end.
                var batches = random.Next(4) == 0 ? random.Next(2, 4) : 1;
                var touched = new HashSet<int>();
                var removedEarlier = new List<int>();
                var replaced = new List<(Row Old, Row New)>();
                for (var b = 0; b < batches; b++)
                {
                    var batch = RandomBatch(random, baseRows, replaceOnly, Changed, NewRow, removedEarlier, Count);
                    // The reference base: changed in place, removed, then added at the end.
                    foreach (var row in batch.Changed)
                    {
                        var at = baseRows.FindIndex(r => r.Id == row.Id);
                        replaced.Add((baseRows[at], row));
                        baseRows[at] = row;
                        touched.Add(row.Id);
                    }
                    foreach (var key in batch.RemovedKeys)
                    {
                        baseRows.RemoveAt(baseRows.FindIndex(r => r.Id == (int)key));
                        removedEarlier.Add((int)key);
                        touched.Add((int)key);
                    }
                    baseRows.AddRange(batch.Added);
                    foreach (var row in batch.Added)
                        touched.Add(row.Id);
                    try
                    {
                        source.Apply(batch);
                    }
                    catch (Exception error)
                    {
                        failures.Add($"case {c} step {step} batch {b}: refused: {error.Message}");
                        break;
                    }
                }
                if (batches > 1)
                    Count("gathered several batches");
                clock.Advance(TimeSpan.FromSeconds(1));

                Count(touched.Count * LiveRequeryShare <= baseRows.Count ? "incremental-sized" : "whole-sized");
                reference = GridQueryEngine.Apply(baseRows, Columns, filter, sorts);
                if (!SameRows(source.Window, reference, out why))
                {
                    failures.Add($"case {c} step {step}: the result differs from GridQueryEngine.Apply: {why}\n"
                        + $"  sorts: {string.Join(", ", sorts.Select(s => $"{s.Column} {s.Direction}"))}; filtered: {filter is not null}; "
                        + $"culture {CultureInfo.CurrentCulture.Name}; {touched.Count} keys touched of {baseRows.Count}");
                    break;
                }
                if (oneAtATime is not null)
                {
                    foreach (var (old, @new) in replaced)
                        oneAtATime.ReplaceRow(old, @new);
                    replaceRowPublications++;
                    if (!SameRows(source.Window, oneAtATime.Window, out why))
                    {
                        failures.Add($"case {c} step {step}: the result differs from ReplaceRow one at a time: {why}");
                        break;
                    }
                }
            }
        }
        return new Outcome(publications, replaceRowPublications, failures, coverage);
    }

    // The source's own share, restated: the test counts which batches were small enough for the
    // incremental path, so that both paths are known to have been compared (LiveRequery.WholeRequeryShare).
    private const int LiveRequeryShare = 6;

    private static GridChangeBatch<Row> RandomBatch(
        Random random, List<Row> baseRows, bool replaceOnly, Func<Row, Row> changed, Func<Row> newRow,
        List<int> removedEarlier, Action<string> count)
    {
        var rows = baseRows.Count;
        // k from none to all of the base, weighted towards few.
        var k = rows == 0 ? 0 : random.Next(4) switch
        {
            0 => 0,
            1 => random.Next(1, Math.Min(rows, 3) + 1),
            2 => random.Next(1, Math.Max(1, rows / 4) + 1),
            _ => random.Next(1, rows + 1),
        };
        var changes = new List<Row>();
        var removed = new List<object>();
        foreach (var at in Enumerable.Range(0, rows).OrderBy(_ => random.Next()).Take(k))
        {
            if (!replaceOnly && random.Next(5) == 0)
                removed.Add(baseRows[at].Id);
            else
                changes.Add(changed(baseRows[at]));
        }
        var added = new List<Row>();
        if (!replaceOnly)
        {
            for (var i = random.Next(4) == 0 ? random.Next(0, 6) : 0; i > 0; i--)
                added.Add(newRow());
            // A key removed by an earlier batch of this gathering, added again: a new row at the end of
            // the base under the old key.
            if (removedEarlier.Count > 0 && random.Next(2) == 0)
            {
                var id = removedEarlier[random.Next(removedEarlier.Count)];
                removedEarlier.Remove(id);
                added.Add(new Row(id, Pick(random, Texts), Pick(random, Texts), Pick(random, Numbers), Pick(random, Numbers),
                    Pick(random, Dates), Pick(random, Bools)));
                count("key removed and added again");
            }
        }
        return new GridChangeBatch<Row>(added, changes, removed);
    }

    private static IReadOnlyList<SortSpec> RandomSorts(Random random)
    {
        var levels = random.Next(4);
        return Queryable.OrderBy(_ => random.Next()).Take(levels)
            .Select(c => new SortSpec(c, random.Next(2) == 0 ? SortDirection.Ascending : SortDirection.Descending))
            .ToArray();
    }

    private static GridFilter? RandomFilter(Random random)
    {
        if (random.Next(4) == 0)
            return null;
        var columns = new Dictionary<string, FilterSpec>(StringComparer.Ordinal);
        foreach (var name in Queryable.OrderBy(_ => random.Next()).Take(random.Next(1, 3)))
        {
            var column = Columns.Single(c => c.Name == name);
            var clauses = new List<FilterClause>();
            for (var i = random.Next(1, 3); i > 0; i--)
                clauses.Add(RandomClause(random, column));
            columns[name] = new FilterSpec(clauses, random.Next(2) == 0 ? FilterCombinator.And : FilterCombinator.Or);
        }
        return new GridFilter(columns);
    }

    private static FilterClause RandomClause(Random random, ColumnInfo<Row> column)
    {
        var allowed = FilterOperators.AllowedFor(column.Type);
        var op = allowed[random.Next(allowed.Count)];
        object? Operand() => column.Name switch
        {
            "Id" => (decimal)random.Next(0, 50),
            "Book" or "Desk" => Pick(random, Texts) ?? "a",
            "Pnl" or "Notional" => Pick(random, Numbers) ?? 0m,
            "TradeDate" => Pick(random, Dates) ?? new DateTime(2026, 1, 1),
            _ => Pick(random, Bools) ?? true,
        };
        return op switch
        {
            FilterOperator.IsBlank or FilterOperator.IsNotBlank => new FilterClause(op),
            FilterOperator.In => new FilterClause(op, Values: Enumerable.Range(0, random.Next(1, 5))
                .Select(_ => random.Next(5) == 0 ? null : Operand()).ToArray()),
            _ => new FilterClause(op, Operand()),
        };
    }

    private static bool SameRows(IReadOnlyList<Row> actual, IReadOnlyList<Row> expected, out string why)
    {
        if (actual.Count != expected.Count)
        {
            why = $"{actual.Count} rows, expected {expected.Count}";
            return false;
        }
        for (var i = 0; i < actual.Count; i++)
        {
            if (!ReferenceEquals(actual[i], expected[i]))
            {
                why = $"position {i}: {actual[i]}, expected {expected[i]}";
                return false;
            }
        }
        why = "";
        return true;
    }

    private static bool SameKeys(IReadOnlyList<Row> a, IReadOnlyList<Row> b)
        => a.Count == b.Count && a.Zip(b).All(pair => pair.First.Id == pair.Second.Id);

    private static T Pick<T>(Random random, T[] values) => values[random.Next(values.Length)];
}
