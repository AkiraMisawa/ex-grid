using ExPivot.Engine;
using Microsoft.Data.Sqlite;

namespace ExGrid.DemoApi;

/// <summary>
/// The server's Pivot Source (ADR-0066, ADR-0069): the three questions ExPivot asks — the Leaf
/// Aggregates, a field's Items and the records behind a cell — answered by SQL written by hand over
/// the trades (<see cref="TradePivotSql"/>). <c>/api/pivot</c> serves it as <c>PivotJson</c>, and a
/// page reaches it through <c>PivotSource.Fetch</c>.
/// <list type="bullet">
/// <item><b>Every answer is computed inside one read</b> (<see cref="TradeStore.ReadAsync{T}"/>) and
/// carries that read's Source Version, so it says truly which data it came from.</item>
/// <item><b>Items and records are asked under a version.</b> One that is not the read's is refused
/// with <see cref="PivotSourceRefusalKind.SourceVersionNotHeld"/>: the data has moved on, and records
/// read now would not add up to the cell. The server keeps no older state, only the counter it
/// compares with (ADR-0066).</item>
/// <item><b>It refuses what <c>PivotSource.Fetch</c> refuses</b>, in the same words: a field it does
/// not have, a part only an Aggregation it does not offer reads, and more leaves than the question
/// allows — the last as soon as the reading passes the cap.</item>
/// </list>
/// <c>tests/ExGrid.DemoApi.Tests</c> holds its answers to <c>PivotSource.From</c>'s over the same
/// trades, question for question (PV-22).
/// </summary>
internal sealed class TradePivotSource(TradeStore store) : PivotSource
{
    /// <inheritdoc />
    public override IReadOnlyList<PivotField> Fields => TradePivotFields.Fields;

    /// <inheritdoc />
    public override PivotSourceFeatures Features => TradePivotFields.Features;

    /// <inheritdoc />
    public override async ValueTask<PivotAnswer> AggregateAsync(PivotQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (Refusal(query) is { } refusal)
            return PivotAnswer.Refused(refusal);
        var axis = query.Rows.Concat(query.Columns).Select(placed => TradePivotFields.Find(placed.Field)!).ToArray();
        var values = query.Values.Select(value => TradePivotFields.Find(value.Field)!).ToArray();
        var (sql, parameters) = TradePivotSql.Aggregate(query, axis, values);
        return await store.ReadAsync(async (read, token) =>
        {
            await using var command = read.Command(sql);
            parameters.AddTo(command);
            await using var reader = await command.ExecuteReaderAsync(token);
            var leaves = new Leaves(query, axis, values);
            while (await reader.ReadAsync(token))
            {
                // Refused as soon as the reading passes the cap: the rest is never read.
                if (!leaves.Add(reader))
                    return PivotAnswer.Refused(PivotSourceRefusal.TooManyLeaves(query.MaxLeaves));
            }
            return leaves.Answer(read.Version);
        }, cancellationToken);
    }

    /// <summary>
    /// A field's Items over all the data — not narrowed by any Hidden Item — that match the search,
    /// at most as many as asked, with how many match (ADR-0066). The distinct stored values are read
    /// in SQL; .NET folds them into Items, searches each Item's invariant text ignoring case and
    /// orders them in the source's invariant order, as <c>PivotSource.From</c> does, so a page cut
    /// short holds the same Items.
    /// </summary>
    public override async ValueTask<PivotItemPage> ItemsAsync(PivotItemsQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (TradePivotFields.Find(query.Field) is not { } field)
            return PivotItemPage.Refused(PivotSourceRefusal.UnknownField(query.Field));
        return await store.ReadAsync(async (read, token) =>
        {
            if (query.SourceVersion != read.Version)
                return PivotItemPage.Refused(PivotSourceRefusal.SourceVersionNotHeld(query.SourceVersion));
            await using var command = read.Command(TradePivotSql.Items(field));
            await using var reader = await command.ExecuteReaderAsync(token);
            var seen = new HashSet<PivotItemKey>();
            var matches = new List<ItemOrder.Entry>();
            while (await reader.ReadAsync(token))
            {
                var item = field.ItemAt(reader, 0);
                if (!seen.Add(item))
                    continue;
                if (query.Search is { } search && item.Value?.Contains(search, StringComparison.OrdinalIgnoreCase) != true)
                    continue;
                matches.Add(ItemOrder.Entry.Of(item));
            }
            return new PivotItemPage(read.Version, ItemOrder.First(matches, query.Max), matches.Count);
        }, cancellationToken);
    }

    /// <summary>
    /// One page of the records behind a cell (ADR-0063/0066): those carrying every Item of the
    /// cell's paths and no Hidden Item, in <c>TradeId</c> order — the data's order — as
    /// <c>LIMIT</c> and <c>OFFSET</c>, with how many there are, all read at the version asked.
    /// </summary>
    public override async ValueTask<PivotDetailPage> DetailsAsync(PivotDetailsQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (Refusal(query) is { } refusal)
            return PivotDetailPage.Refused(refusal);
        var parameters = new SqlParameters();
        var where = TradePivotSql.DetailsWhere(query, parameters);
        return await store.ReadAsync(async (read, token) =>
        {
            if (query.SourceVersion != read.Version)
                return PivotDetailPage.Refused(PivotSourceRefusal.SourceVersionNotHeld(query.SourceVersion));
            long total;
            await using (var count = read.Command("SELECT count(*) FROM trades" + where))
            {
                parameters.AddTo(count);
                total = (long)(await count.ExecuteScalarAsync(token))!;
            }

            var records = new List<PivotDetailRecord>();
            if (query.Count > 0 && query.Start < total)
            {
                await using var page = read.Command(
                    $"SELECT {TradePivotSql.DetailColumns} FROM trades{where}\nORDER BY TradeId LIMIT $count OFFSET $start");
                parameters.AddTo(page);
                page.Parameters.AddWithValue("$count", query.Count);
                page.Parameters.AddWithValue("$start", query.Start);
                await using var reader = await page.ExecuteReaderAsync(token);
                var fields = TradePivotFields.All;
                var values = new object?[fields.Count];
                while (await reader.ReadAsync(token))
                {
                    for (var f = 0; f < values.Length; f++)
                        values[f] = fields[f].ValueAt(reader, f);
                    records.Add(new PivotDetailRecord(values));
                }
            }
            return new PivotDetailPage(read.Version, TradePivotFields.Fields, query.Start, total, records);
        }, cancellationToken);
    }

    /// <summary>Excel's Refresh: the next question reads the data as it is then. Raises
    /// <see cref="PivotSource.Changed"/> with the current version.</summary>
    public override ValueTask RefreshAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        OnChanged(new PivotSourceChanged(store.Version));
        return ValueTask.CompletedTask;
    }

    // What every source refuses alike before reading anything (PivotSource.Fetch refuses the same,
    // in the same order and words): a placed field it does not have, then for each field in Values
    // a field it does not have or a part none of its offered Aggregations reads.
    private static PivotSourceRefusal? Refusal(PivotQuery query)
    {
        foreach (var placed in query.Placed)
        {
            if (TradePivotFields.Find(placed.Field) is null)
                return PivotSourceRefusal.UnknownField(placed.Field);
        }
        foreach (var value in query.Values)
        {
            if (TradePivotFields.Find(value.Field) is null)
                return PivotSourceRefusal.UnknownField(value.Field);
            foreach (var part in (PivotParts[])[PivotParts.Sum, PivotParts.Extremes, PivotParts.Product, PivotParts.Variance])
            {
                if ((value.Parts & part) != 0 && NotOffered(part) is { Length: > 0 } readers)
                    return PivotSourceRefusal.AggregationNotOffered(value.Field, readers);
            }
        }
        return null;
    }

    private static PivotSourceRefusal? Refusal(PivotDetailsQuery query)
    {
        foreach (var field in query.RowItems.Select(step => step.Field)
                     .Concat(query.ColumnItems.Select(step => step.Field))
                     .Concat(query.HiddenItems.Select(hidden => hidden.Field)))
        {
            if (TradePivotFields.Find(field) is null)
                return PivotSourceRefusal.UnknownField(field);
        }
        return null;
    }

    // The Aggregations that read a part, when the source offers none of them: the part is not
    // answered. Empty when one of them is offered.
    private static PivotAggregation[] NotOffered(PivotParts part)
    {
        var readers = Enum.GetValues<PivotAggregation>().Where(aggregation => PivotQuery.PartsOf(aggregation) == part).ToArray();
        return readers.Any(TradePivotFields.Features.Offers) ? [] : readers;
    }

    /// <summary>
    /// The leaves of one answer, gathered from the <c>GROUP BY</c>'s groups: groups that are one
    /// combination of Items — two spellings of one text Item — are folded into one leaf, their
    /// parts merged exactly.
    /// <para>
    /// A text Item is labelled by the spelling of the first group read, in the order SQLite returns
    /// the groups: the stored values' order, outermost field first. <c>PivotSource.From</c> labels
    /// it by its first spelling in the data's order, over every record. The two agree wherever an
    /// Item is stored in one spelling, as every generated trade's is (<c>TradePivotSqlTests</c> pins
    /// it). Where one is stored in several, the numbers are the same and the label may be another
    /// of its spellings; choosing the data's first would cost an aggregate more — measured 8% to
    /// 35% on a million trades — or a query more per field.
    /// </para>
    /// </summary>
    private sealed class Leaves(PivotQuery query, TradePivotField[] axis, TradePivotField[] values)
    {
        private readonly Dictionary<PivotItemKey[], int> _index = new(ItemsComparer.Instance);
        private readonly List<Leaf> _leaves = [];

        /// <summary>Folds one group in; false once the leaves pass <see cref="PivotQuery.MaxLeaves"/>.</summary>
        public bool Add(SqliteDataReader reader)
        {
            var items = new PivotItemKey[axis.Length];
            for (var level = 0; level < axis.Length; level++)
                items[level] = axis[level].ItemAt(reader, level);
            if (!_index.TryGetValue(items, out var at))
            {
                if (_leaves.Count == query.MaxLeaves)
                    return false;
                at = _leaves.Count;
                _index.Add(items, at);
                _leaves.Add(new Leaf(items, values.Length));
            }

            var leaf = _leaves[at];
            var column = axis.Length;
            leaf.Records += reader.GetInt64(column++);
            for (var v = 0; v < values.Length; v++)
            {
                ref var part = ref leaf.Values[v];
                var count = reader.GetInt64(column++);
                part.Values += count;
                if (!values[v].IsNumber)
                    continue;
                var parts = query.Values[v].Parts;
                var sumAt = (parts & PivotParts.Sum) != 0 ? column++ : -1;
                var minAt = (parts & PivotParts.Extremes) != 0 ? column : -1;
                if (minAt >= 0)
                    column += 2;
                // A group whose values are all NULL has no numbers, and SQL's sum and extremes of
                // it are NULL: nothing to add.
                if (count == 0)
                    continue;
                var first = part.Numbers == 0;
                part.Numbers += count;
                if (sumAt >= 0)
                    part.Sum += Amount(values[v], reader.GetInt64(sumAt));
                if (minAt >= 0)
                {
                    var min = Amount(values[v], reader.GetInt64(minAt));
                    var max = Amount(values[v], reader.GetInt64(minAt + 1));
                    part.Min = first ? min : Math.Min(part.Min, min);
                    part.Max = first ? max : Math.Max(part.Max, max);
                }
            }
            return true;
        }

        /// <summary>The answer, built leaf by leaf (<see cref="PivotAnswerBuilder"/>).</summary>
        public PivotAnswer Answer(string sourceVersion)
        {
            var builder = new PivotAnswerBuilder(query, sourceVersion);
            foreach (var leaf in _leaves)
            {
                var at = builder.AddLeaf(leaf.Items, leaf.Records);
                for (var v = 0; v < values.Length; v++)
                {
                    var part = leaf.Values[v];
                    builder.SetCounts(at, v, part.Values, part.Numbers);
                    // A field with no numbers sums to 0 and has extremes of 0, as the engine's do.
                    if ((query.Values[v].Parts & PivotParts.Sum) != 0)
                        builder.SetSum(at, v, PivotNumber.Exact(part.Sum));
                    if ((query.Values[v].Parts & PivotParts.Extremes) != 0)
                        builder.SetExtremes(at, v, PivotNumber.Exact(part.Min), PivotNumber.Exact(part.Max));
                }
            }
            return builder.Build();
        }

        // A stored number as the exact amount it is: cents are hundredths, never through a double.
        private static decimal Amount(TradePivotField field, long stored) =>
            field.Kind == TradeValueKind.Cents ? Cents.ToDecimal(stored) : stored;

        private sealed class Leaf(PivotItemKey[] items, int values)
        {
            public PivotItemKey[] Items { get; } = items;

            public long Records { get; set; }

            public ValueParts[] Values { get; } = new ValueParts[values];
        }

        private struct ValueParts
        {
            public long Values;
            public long Numbers;
            public decimal Sum;
            public decimal Min;
            public decimal Max;
        }
    }

    /// <summary>A leaf's Items, equal when every Item is — text ignoring case, as Items are told apart.</summary>
    private sealed class ItemsComparer : IEqualityComparer<PivotItemKey[]>
    {
        public static readonly ItemsComparer Instance = new();

        public bool Equals(PivotItemKey[]? x, PivotItemKey[]? y) =>
            ReferenceEquals(x, y) || (x is not null && y is not null && x.AsSpan().SequenceEqual(y));

        public int GetHashCode(PivotItemKey[] items)
        {
            var hash = new HashCode();
            foreach (var item in items)
                hash.Add(item);
            return hash.ToHashCode();
        }
    }
}

/// <summary>
/// The order a source lists a field's Items in when it knows no culture (<see cref="PivotItemPage"/>):
/// numbers, dates, text, Booleans, <c>#NUM!</c>, then <c>(blank)</c>; numbers and dates by value,
/// <c>FALSE</c> before <c>TRUE</c>, text ordinally ignoring case — total, since two texts equal
/// ignoring case are one Item.
/// </summary>
internal static class ItemOrder
{
    private static readonly Comparer<Entry> Ascending = Comparer<Entry>.Create(Compare);
    private static readonly Comparer<Entry> Descending = Comparer<Entry>.Create((x, y) => Compare(y, x));

    /// <summary>The first <paramref name="max"/> of <paramref name="entries"/> in this order. A page
    /// far shorter than the Items — ten thousand of a million trades — is kept in a heap of its own
    /// size rather than by sorting every Item.</summary>
    public static PivotItemKey[] First(IReadOnlyList<Entry> entries, int max)
    {
        if (max <= 0)
            return [];
        Entry[] first;
        if (entries.Count <= max)
        {
            first = [.. entries];
        }
        else
        {
            // A max-heap of the smallest so far: the root is the largest of them, and goes when a
            // smaller one comes.
            var heap = new PriorityQueue<Entry, Entry>(max, Descending);
            foreach (var entry in entries)
            {
                if (heap.Count < max)
                    heap.Enqueue(entry, entry);
                else if (Compare(entry, heap.Peek()) < 0)
                    heap.DequeueEnqueue(entry, entry);
            }
            first = heap.UnorderedItems.Select(pair => pair.Element).ToArray();
        }
        Array.Sort(first, Ascending);
        return first.Select(entry => entry.Item).ToArray();
    }

    private static int Compare(Entry x, Entry y)
    {
        if (x.Item.Kind != y.Item.Kind)
            return x.Item.Kind.CompareTo(y.Item.Kind);
        return x.Item.Kind switch
        {
            PivotItemKind.Number => x.Number.CompareTo(y.Number),
            PivotItemKind.Date or PivotItemKind.Boolean => x.Ticks.CompareTo(y.Ticks),
            PivotItemKind.Text => string.Compare(x.Item.Value, y.Item.Value, StringComparison.OrdinalIgnoreCase),
            _ => 0,
        };
    }

    /// <summary>An Item with what it is ordered by, read once.</summary>
    internal readonly record struct Entry(PivotItemKey Item, double Number, long Ticks)
    {
        public static Entry Of(PivotItemKey item) => item.Kind switch
        {
            PivotItemKind.Number => new(item, double.Parse(item.Value!, System.Globalization.CultureInfo.InvariantCulture), 0),
            PivotItemKind.Date => new(item, 0, DateTime.ParseExact(item.Value!, "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF", System.Globalization.CultureInfo.InvariantCulture).Ticks),
            PivotItemKind.Boolean => new(item, 0, item.Value == "TRUE" ? 1 : 0),
            _ => new(item, 0, 0),
        };
    }
}
