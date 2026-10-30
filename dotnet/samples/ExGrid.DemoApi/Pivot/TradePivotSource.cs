using ExGrid.Data;
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
/// <item><b>Past the cap, a question with levels is answered with the groups it shows</b> (ADR-0370
/// section 3), in the same read: <see cref="TradePivotSql.Levels"/> groups them, and
/// <see cref="TradeLevelCells"/> folds them into cells and cuts the windows of the sets ordered by a
/// value. A question whose leaves fit is answered with them, levels or not.</item>
/// </list>
/// <c>dotnet/tests/ExGrid.DemoApi.Tests</c> holds its answers to <c>PivotSource.From</c>'s over the same
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
        var rows = query.Rows.Select(placed => TradePivotSql.Field(placed.Field)).ToArray();
        var columns = query.Columns.Select(placed => TradePivotSql.Field(placed.Field)).ToArray();
        var axis = rows.Concat(columns).ToArray();
        var values = query.Values.Select(value => TradePivotSql.Field(value.Field)).ToArray();
        var (sql, parameters) = TradePivotSql.Aggregate(query, axis, values);
        return await store.ReadAsync(async (read, token) =>
        {
            await using (var command = read.Command(sql))
            {
                parameters.AddTo(command);
                await using var reader = await command.ExecuteReaderAsync(token);
                var leaves = new Leaves(query, axis, values);
                var fits = true;
                while (fits && await reader.ReadAsync(token))
                {
                    // Refused as soon as the reading passes the cap: the rest is never read.
                    fits = leaves.Add(reader);
                }
                if (fits)
                    return leaves.Answer(read.Version);
            }
            if (query.Levels is null)
                return PivotAnswer.Refused(PivotSourceRefusal.TooManyLeaves(query.MaxLeaves));
            // Past the cap, a question with levels is answered with the groups it shows, in the same
            // read, so the answer is one Source Version (ADR-0370 section 3).
            var cells = new TradeLevelCells(query, rows, columns, values);
            if (cells.Windowed)
            {
                // Every row group at the grand total of the columns, which orders each set.
                var (orderSql, orderParameters) = TradePivotSql.Levels(query, rows, columns, values, columnRootOnly: true);
                await using var orderCommand = read.Command(orderSql);
                orderParameters.AddTo(orderCommand);
                await using var ordering = await orderCommand.ExecuteReaderAsync(token);
                while (await ordering.ReadAsync(token))
                    cells.Order(ordering);
                cells.Plan();
            }
            var (levelSql, levelParameters) = TradePivotSql.Levels(query, rows, columns, values);
            await using var levelCommand = read.Command(levelSql);
            levelParameters.AddTo(levelCommand);
            await using var groups = await levelCommand.ExecuteReaderAsync(token);
            while (await groups.ReadAsync(token))
            {
                // Refused as soon as the cells pass the cap: the rest is never read.
                if (!cells.Add(groups))
                    return PivotAnswer.Refused(PivotSourceRefusal.TooManyLeaves(query.MaxLeaves));
            }
            return cells.Answer(read.Version);
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
            var matches = new List<ItemOrder.Entry>();
            foreach (var item in await ItemsOf(read, field, token))
            {
                if (query.Search is { } search && item.Value?.Contains(search, StringComparison.OrdinalIgnoreCase) != true)
                    continue;
                matches.Add(ItemOrder.Entry.Of(item));
            }
            return new PivotItemPage(read.Version, ItemOrder.First(matches, query.Max), matches.Count);
        }, cancellationToken);
    }

    /// <summary>
    /// Each field's count of Items (ADR-0370 section 3), all read at one version: the length of the
    /// field's whole page of Items, read as <see cref="ItemsAsync"/> reads them. It costs a
    /// <c>SELECT DISTINCT</c> a field each time it is asked, and ExPivot asks once a version, so the
    /// Field List can show the counts and mark a drop past the leaf cap before it is asked.
    /// </summary>
    public override async ValueTask<PivotItemCounts> ItemCountsAsync(CancellationToken cancellationToken = default)
        => await store.ReadAsync(async (read, token) =>
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var field in TradePivotFields.All)
                counts.Add(field.Name, (await ItemsOf(read, field, token)).Count);
            return new PivotItemCounts(read.Version, counts);
        }, cancellationToken);

    // A field's distinct stored values, folded into its Items in the order SQL lists them.
    private static async Task<List<PivotItemKey>> ItemsOf(TradeRead read, TradePivotField field, CancellationToken token)
    {
        await using var command = read.Command(TradePivotSql.Items(field));
        await using var reader = await command.ExecuteReaderAsync(token);
        var seen = new HashSet<PivotItemKey>();
        var items = new List<PivotItemKey>();
        while (await reader.ReadAsync(token))
        {
            var item = field.ItemAt(reader, 0);
            if (seen.Add(item))
                items.Add(item);
        }
        return items;
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
                leaf.Values[v].Fold(reader, ref column, values[v], query.Values[v].Parts);
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
                    leaf.Values[v].SetOn(builder, at, v, query.Values[v].Parts);
            }
            return builder.Build();
        }

        private sealed class Leaf(PivotItemKey[] items, int values)
        {
            public PivotItemKey[] Items { get; } = items;

            public long Records { get; set; }

            public TradeParts[] Values { get; } = new TradeParts[values];
        }
    }
}

/// <summary>
/// One field in Values' parts over a group of trades (ADR-0066): its values that are not Blank, its
/// numbers, and their exact sum and extremes. A group's are read from the columns SQL answers for
/// it, and two groups' merge exactly — counts and sums add, extremes compare — so a leaf or a cell
/// folded from several groups holds the parts of all their trades.
/// </summary>
internal struct TradeParts
{
    public long Values;
    public long Numbers;
    public decimal Sum;
    public decimal Min;
    public decimal Max;

    /// <summary>Folds in one group's columns of a field, read from <paramref name="column"/> on, as
    /// <c>TradePivotSql</c> selects them: its count, then the parts asked for of a number field.</summary>
    public void Fold(SqliteDataReader reader, ref int column, TradePivotField field, PivotParts parts)
    {
        var count = reader.GetInt64(column++);
        if (!field.IsNumber)
        {
            Values += count;
            return;
        }
        var sumAt = (parts & PivotParts.Sum) != 0 ? column++ : -1;
        var minAt = (parts & PivotParts.Extremes) != 0 ? column : -1;
        if (minAt >= 0)
            column += 2;
        // A group whose values are all NULL has no numbers, and SQL's sum and extremes of it are
        // NULL: nothing to add.
        if (count == 0)
            return;
        Merge(new TradeParts
        {
            Values = count,
            Numbers = count,
            Sum = sumAt >= 0 ? Amount(field, reader.GetInt64(sumAt)) : 0,
            Min = minAt >= 0 ? Amount(field, reader.GetInt64(minAt)) : 0,
            Max = minAt >= 0 ? Amount(field, reader.GetInt64(minAt + 1)) : 0,
        });
    }

    /// <summary>Merges another group's parts of the same field in.</summary>
    public void Merge(in TradeParts other)
    {
        Values += other.Values;
        if (other.Numbers == 0)
            return;
        var first = Numbers == 0;
        Numbers += other.Numbers;
        Sum += other.Sum;
        Min = first ? other.Min : Math.Min(Min, other.Min);
        Max = first ? other.Max : Math.Max(Max, other.Max);
    }

    /// <summary>Sets the parts asked for at a leaf or cell of an answer. A field with no numbers sums
    /// to 0 and has extremes of 0, as the engine's do.</summary>
    public readonly void SetOn(PivotAnswerBuilder builder, int at, int value, PivotParts parts)
    {
        builder.SetCounts(at, value, Values, Numbers);
        if ((parts & PivotParts.Sum) != 0)
            builder.SetSum(at, value, PivotNumber.Exact(Sum));
        if ((parts & PivotParts.Extremes) != 0)
            builder.SetExtremes(at, value, PivotNumber.Exact(Min), PivotNumber.Exact(Max));
    }

    /// <summary>An Aggregation read from these parts by the engine's own arithmetic (ADR-0060,
    /// ADR-0130): what a report shows for the group, and what orders its siblings by a value. A part
    /// not among <paramref name="parts"/> is not given, so an Aggregation that reads it throws.</summary>
    public readonly AggregateResult Read(PivotAggregation aggregation, PivotParts parts) => AggregateArithmetic.Read(
        aggregation switch
        {
            PivotAggregation.Sum => Aggregation.Sum,
            PivotAggregation.Count => Aggregation.Count,
            PivotAggregation.Average => Aggregation.Average,
            PivotAggregation.Max => Aggregation.Max,
            PivotAggregation.Min => Aggregation.Min,
            PivotAggregation.CountNumbers => Aggregation.CountNumbers,
            _ => throw new InvalidOperationException($"Internal: {aggregation} is not offered, and a question reading it is refused first."),
        },
        new AggregateCounts { Values = Values, Numbers = Numbers },
        (parts & PivotParts.Sum) != 0 ? new AggregateSum { Exact = Sum } : null,
        (parts & PivotParts.Extremes) != 0 ? new AggregateExtremes { ExactMin = Min, ExactMax = Max } : null);

    // A stored number as the exact amount it is: cents are hundredths, never through a double.
    private static decimal Amount(TradePivotField field, long stored) =>
        field.Kind == TradeValueKind.Cents ? Cents.ToDecimal(stored) : stored;
}

/// <summary>Items, equal when every Item is — text ignoring case, as Items are told apart. A null
/// stands for a level a cell is not named at.</summary>
internal sealed class ItemsComparer : IEqualityComparer<PivotItemKey?[]>
{
    public static readonly ItemsComparer Instance = new();

    public bool Equals(PivotItemKey?[]? x, PivotItemKey?[]? y) =>
        ReferenceEquals(x, y) || (x is not null && y is not null && x.AsSpan().SequenceEqual(y));

    public int GetHashCode(PivotItemKey?[] items)
    {
        var hash = new HashCode();
        foreach (var item in items)
            hash.Add(item);
        return hash.ToHashCode();
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

    /// <summary>Two Items in this order.</summary>
    internal static int Compare(Entry x, Entry y)
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
