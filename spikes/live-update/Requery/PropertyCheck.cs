using ExGrid;

namespace Requery;

/// <summary>
/// M5: the incremental result against <see cref="GridQueryEngine.Apply{TRow}"/> over the new base,
/// position by position and by reference, over random bases, batches, Sorts and Filters. Also the
/// sequence-moved answer against the definition written out over every position, and — for
/// batches that only replace — the Window <c>GridSource.From(...).ReplaceRow</c> reaches after the
/// same replacements one at a time.
/// </summary>
public static class PropertyCheck
{
    // Text chosen to fall into OrdinalIgnoreCase's corners: case pairs, letters whose case mapping
    // is culture-sensitive elsewhere (Turkish i), a sharp s against "ss", accents, the empty string
    // (not a Blank) and Blanks.
    private static readonly string?[] Texts =
        ["a", "A", "b", "B", "ab", "AB", "aB", "ä", "Ä", "ss", "SS", "ß", "i", "I", "ı", "İ", "", " ", "z", "Z", "é", "é", null];

    private static readonly decimal?[] Numbers =
        [null, -1m, 0m, 0.0m, 1m, 1.0m, 1.00m, 2.5m, -2.5m, 1000000m, 0.01m, -0.01m, 79228162514264337593543950335m];

    private static readonly DateTime?[] Dates =
    [
        null,
        new DateTime(2026, 1, 1),
        new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),   // the same ticks, another Kind: equal
        new DateTime(2026, 1, 2),
        new DateTime(2025, 12, 31, 23, 59, 59),
    ];

    private static readonly bool?[] Bools = [null, true, false];

    private static readonly string[] Queryable = ["Id", "Book", "Desk", "Pnl", "Notional", "TradeDate", "Confirmed"];

    public sealed record Outcome(int Cases, int Batches, int RowsCompared, int ReplaceRowCases, List<string> Failures, Dictionary<string, int> Coverage);

    public static Outcome Run(int cases, int seed)
    {
        var random = new Random(seed);
        var failures = new List<string>();
        var batches = 0;
        var rowsCompared = 0;
        var replaceRowCases = 0;
        var nextId = 0;
        var coverage = new Dictionary<string, int>(StringComparer.Ordinal);
        void Count(string what) => coverage[what] = coverage.GetValueOrDefault(what) + 1;

        Trade NewTrade() => new(nextId++, Pick(random, Texts), Pick(random, Texts), Pick(random, Numbers), Pick(random, Numbers),
            Pick(random, Dates), Pick(random, Bools));

        Trade Changed(Trade t)
        {
            // A live change touches one to three fields; a sorted or filtered one moves the row.
            var changed = t;
            var fields = random.Next(1, 4);
            for (var f = 0; f < fields; f++)
            {
                changed = random.Next(6) switch
                {
                    0 => new Trade(t.Id, Pick(random, Texts), changed.Desk, changed.Pnl, changed.Notional, changed.TradeDate, changed.Confirmed),
                    1 => new Trade(t.Id, changed.Book, Pick(random, Texts), changed.Pnl, changed.Notional, changed.TradeDate, changed.Confirmed),
                    2 => new Trade(t.Id, changed.Book, changed.Desk, Pick(random, Numbers), changed.Notional, changed.TradeDate, changed.Confirmed),
                    3 => new Trade(t.Id, changed.Book, changed.Desk, changed.Pnl, Pick(random, Numbers), changed.TradeDate, changed.Confirmed),
                    4 => new Trade(t.Id, changed.Book, changed.Desk, changed.Pnl, changed.Notional, Pick(random, Dates), changed.Confirmed),
                    _ => new Trade(t.Id, changed.Book, changed.Desk, changed.Pnl, changed.Notional, changed.TradeDate, Pick(random, Bools)),
                };
            }
            // Sometimes a new instance with every value the same: a change the row cannot show.
            return random.Next(10) == 0 ? new Trade(t.Id, t.Book, t.Desk, t.Pnl, t.Notional, t.TradeDate, t.Confirmed) : changed;
        }

        for (var c = 0; c < cases; c++)
        {
            var n = random.Next(10) == 0 ? random.Next(0, 4) : random.Next(1, 400);
            var baseRows = new List<Trade>(n);
            for (var i = 0; i < n; i++)
                baseRows.Add(NewTrade());
            var sorts = RandomSorts(random);
            var filter = RandomFilter(random);

            IReadOnlyList<Trade> reference;
            try
            {
                reference = GridQueryEngine.Apply(baseRows, TradeColumns.All, filter, sorts);
            }
            catch (InvalidOperationException)
            {
                // A Query the engine refuses (an operator on a type that does not take it cannot
                // arise here, but a malformed one can): nothing to compare.
                continue;
            }
            var incremental = new IncrementalQuery(baseRows, TradeColumns.All, filter, sorts);
            if (!SameRows(incremental.Window, reference, out var why))
            {
                failures.Add($"case {c}: initial result differs: {why}");
                continue;
            }

            var replaceOnly = random.Next(3) == 0;
            var source = replaceOnly ? GridSource.From(baseRows) : null;
            if (source is not null)
            {
                source.OnColumnsChanged(TradeColumns.All);
                source.OnFilterChanged(filter);
                source.OnSortChanged(sorts);
                replaceRowCases++;
            }

            var steps = random.Next(1, 8);
            for (var s = 0; s < steps && failures.Count < 20; s++)
            {
                batches++;
                var previousWindow = incremental.Window.ToArray();
                var batch = RandomBatch(random, baseRows, replaceOnly, Changed, NewTrade);

                // The reference base: replaced in place, removed, then appended.
                foreach (var (old, replacement) in batch.Replaced)
                    baseRows[IndexOf(baseRows, old)] = replacement;
                foreach (var removed in batch.Removed)
                    baseRows.RemoveAt(IndexOf(baseRows, removed));
                baseRows.AddRange(batch.Added);
                reference = GridQueryEngine.Apply(baseRows, TradeColumns.All, filter, sorts);

                var moved = incremental.Apply(batch);
                rowsCompared += reference.Count;
                Count(moved ? "batches: sequence moved" : "batches: sequence kept");
                Count($"batches: {sorts.Count} sort level(s)");
                Count(filter is null ? "batches: no filter" : "batches: filtered");
                Count(batch.Replaced.Count == 0 && batch.Added.Count == 0 && batch.Removed.Count == 0 ? "batches: empty" : batch.Added.Count + batch.Removed.Count > 0 ? "batches: with adds or removes" : "batches: replacements only");
                if (previousWindow.Length != reference.Count)
                    Count("batches: result size changed");
                if (!SameRows(incremental.Window, reference, out why))
                {
                    failures.Add($"case {c} batch {s}: result differs from GridQueryEngine.Apply: {why}\n  sorts: {Describe(sorts)}\n  filter: {Describe(filter)}\n  batch: {batch.Replaced.Count} replaced, {batch.Added.Count} added, {batch.Removed.Count} removed");
                    break;
                }
                var expectedMoved = SequenceMoved(previousWindow, reference, batch);
                if (moved != expectedMoved)
                {
                    failures.Add($"case {c} batch {s}: sequence moved says {moved}, the definition says {expectedMoved}");
                    break;
                }
                if (source is not null)
                {
                    foreach (var (old, replacement) in batch.Replaced)
                        source.ReplaceRow(old, replacement);
                    if (!SameRows(incremental.Window, source.Window, out why))
                    {
                        failures.Add($"case {c} batch {s}: result differs from ReplaceRow one at a time: {why}");
                        break;
                    }
                }
            }
            if (failures.Count >= 20)
                break;
        }
        return new Outcome(cases, batches, rowsCompared, replaceRowCases, failures, coverage);
    }

    private static RowBatch RandomBatch(Random random, List<Trade> baseRows, bool replaceOnly, Func<Trade, Trade> changed, Func<Trade> newTrade)
    {
        var count = baseRows.Count;
        // k from none to all of the base, weighted towards few.
        var k = count == 0 ? 0 : random.Next(4) switch
        {
            0 => 0,
            1 => random.Next(1, Math.Min(count, 3) + 1),
            2 => random.Next(1, Math.Max(1, count / 4) + 1),
            _ => random.Next(1, count + 1),
        };
        var picked = Enumerable.Range(0, count).OrderBy(_ => random.Next()).Take(k).ToArray();
        var replaced = new List<(Trade, Trade)>();
        var removed = new List<Trade>();
        foreach (var at in picked)
        {
            if (!replaceOnly && random.Next(5) == 0)
                removed.Add(baseRows[at]);
            else
                replaced.Add((baseRows[at], changed(baseRows[at])));
        }
        var added = new List<Trade>();
        if (!replaceOnly)
        {
            var adds = random.Next(4) == 0 ? random.Next(0, 6) : 0;
            for (var i = 0; i < adds; i++)
                added.Add(newTrade());
        }
        return new RowBatch(replaced, added, removed);
    }

    private static IReadOnlyList<SortSpec> RandomSorts(Random random)
    {
        var levels = random.Next(4) switch { 0 => 0, 1 => 1, 2 => 2, _ => 3 };
        var columns = Queryable.OrderBy(_ => random.Next()).Take(levels);
        return columns.Select(c => new SortSpec(c, random.Next(2) == 0 ? SortDirection.Ascending : SortDirection.Descending)).ToArray();
    }

    private static GridFilter? RandomFilter(Random random)
    {
        if (random.Next(4) == 0)
            return null;
        var columns = new Dictionary<string, FilterSpec>(StringComparer.Ordinal);
        foreach (var name in Queryable.OrderBy(_ => random.Next()).Take(random.Next(1, 3)))
        {
            var column = TradeColumns.All.Single(c => c.Name == name);
            var clauses = new List<FilterClause>();
            for (var i = random.Next(1, 3); i > 0; i--)
                clauses.Add(RandomClause(random, column));
            columns[name] = new FilterSpec(clauses, random.Next(2) == 0 ? FilterCombinator.And : FilterCombinator.Or);
        }
        return new GridFilter(columns);
    }

    private static FilterClause RandomClause(Random random, ColumnInfo<Trade> column)
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
            FilterOperator.In => new FilterClause(op, Values: Enumerable.Range(0, random.Next(1, 5)).Select(_ => random.Next(5) == 0 ? null : Operand()).ToArray()),
            _ => new FilterClause(op, Operand()),
        };
    }

    /// <summary>The definition the source's ReplaceRow applies, extended to a batch: the same
    /// sequence when every position holds the row it held, or that row's replacement.</summary>
    private static bool SequenceMoved(IReadOnlyList<Trade> previous, IReadOnlyList<Trade> next, RowBatch batch)
    {
        if (previous.Count != next.Count)
            return true;
        var replacedBy = new Dictionary<Trade, Trade>(ReferenceEqualityComparer.Instance);
        foreach (var (old, replacement) in batch.Replaced)
            replacedBy.Add(replacement, old);
        for (var i = 0; i < next.Count; i++)
        {
            if (ReferenceEquals(next[i], previous[i]))
                continue;
            if (replacedBy.TryGetValue(next[i], out var old) && ReferenceEquals(old, previous[i]))
                continue;
            return true;
        }
        return false;
    }

    private static bool SameRows(IReadOnlyList<Trade> actual, IReadOnlyList<Trade> expected, out string why)
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

    private static int IndexOf(List<Trade> rows, Trade row)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            if (ReferenceEquals(rows[i], row))
                return i;
        }
        throw new InvalidOperationException($"{row} is not in the base.");
    }

    private static T Pick<T>(Random random, T[] values) => values[random.Next(values.Length)];

    private static string Describe(IReadOnlyList<SortSpec> sorts) => string.Join(", ", sorts.Select(s => $"{s.Column} {s.Direction}"));

    private static string Describe(GridFilter? filter) => filter is null ? "none"
        : string.Join(" AND ", filter.Columns.Select(c => $"{c.Key}: " + string.Join($" {c.Value.Combinator} ",
            c.Value.Clauses.Select(cl => $"{cl.Operator} {cl.Value}{(cl.Values is null ? "" : "[" + string.Join(",", cl.Values.Select(v => v ?? "null")) + "]")}"))));
}
