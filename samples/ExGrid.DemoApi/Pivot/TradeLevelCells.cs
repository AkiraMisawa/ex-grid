using ExPivot.Engine;
using Microsoft.Data.Sqlite;

namespace ExGrid.DemoApi;

/// <summary>
/// The cells of one answer of Level Aggregates (ADR-0370 section 3), folded from the groups
/// <see cref="TradePivotSql.Levels"/> reads: two spellings of one text Item are one cell, their parts
/// merged exactly, as the leaves' are. The cells are refused as soon as they pass the cap, rest cells
/// counted among them.
/// <para>
/// Under a question's window that orders a row field by a value, the groups at the grand total of the
/// columns are read first, every one, because a sibling set is ordered whole (<see cref="Order"/>,
/// <see cref="Plan"/>). Each set ordered by a value with more siblings than the limit is ordered by
/// that value, read from its parts by the engine's own arithmetic (<see cref="TradeParts.Read"/>),
/// non-numbers last and, among ties, in the source's order of Items (<see cref="ItemOrder"/>). The
/// window is cut where the question starts it, no further than the limit from the end, and widened
/// over the siblings tied with its first or its last; the sibling on either side of it is its margin.
/// A set under a sibling an outer window merges into a rest has no window of its own. Then every
/// group is read (<see cref="Add"/>): those of the siblings beyond each margin are merged into that
/// side's rest cells, one a column group, and the groups below them are passed over.
/// </para>
/// <para>
/// The bundled sources order ties by the order their leaves first reached them, which a <c>GROUP BY</c>
/// does not keep. The engine sorts ties on its own and holds no tie across an edge of the window, so
/// either order answers truly; only which of two tied siblings beyond the window is the margin may
/// differ.
/// </para>
/// </summary>
internal sealed class TradeLevelCells
{
    private readonly PivotQuery _query;
    private readonly PivotLevels _levels;
    private readonly TradePivotField[] _rows;
    private readonly TradePivotField[] _columns;
    private readonly TradePivotField[] _values;
    private readonly Dictionary<PivotItemKey?[], Group> _ordering = new(ItemsComparer.Instance);
    private readonly Dictionary<PivotItemKey?[], Group> _cells = new(ItemsComparer.Instance);
    private readonly WindowNode _windows = new();
    private readonly List<Window> _planned = [];
    private int _count;

    /// <summary>Starts the cells of an answer to <paramref name="query"/>, which carries levels.</summary>
    public TradeLevelCells(PivotQuery query, TradePivotField[] rows, TradePivotField[] columns, TradePivotField[] values)
    {
        _query = query;
        _levels = query.Levels ?? throw new InvalidOperationException("Internal: Level Aggregates answer a question with levels.");
        _rows = rows;
        _columns = columns;
        _values = values;
        Windowed = _levels.Window is not null && _levels.Rows.Any(rule => rule.Order is not null);
    }

    /// <summary>Whether a set may be answered as a window: the question has one, and orders a row
    /// field by a value. Its groups at the grand total of the columns are then read first
    /// (<see cref="Order"/>) and its windows planned (<see cref="Plan"/>).</summary>
    public bool Windowed { get; }

    /// <summary>Folds in one group at the grand total of the columns, as
    /// <see cref="TradePivotSql.Levels"/> reads them with <c>columnRootOnly</c>: what orders each set.</summary>
    public void Order(SqliteDataReader reader)
    {
        if (Read(reader, out var key) is not { } records)
            return;
        if (!_ordering.TryGetValue(key, out var group))
            _ordering.Add(key, group = new Group(key, reader.GetInt32(0), 0, _values.Length));
        Fold(group, reader, records);
    }

    /// <summary>Plans the window of each set ordered by a value with more siblings than the limit,
    /// outermost level first, from the groups <see cref="Order"/> read.</summary>
    public void Plan()
    {
        var window = _levels.Window ?? throw new InvalidOperationException("Internal: windows are planned for a question with a window.");
        // Each parent row group's children at the grand total of the columns.
        var children = new Dictionary<PivotItemKey?[], List<Group>>(ItemsComparer.Instance);
        foreach (var group in _ordering.Values)
        {
            if (group.RowDepth == 0)
                continue;
            var parent = Above(group.Key, group.RowDepth - 1);
            if (!children.TryGetValue(parent, out var siblings))
                children.Add(parent, siblings = []);
            siblings.Add(group);
        }
        foreach (var (parent, siblings) in children.OrderBy(pair => pair.Value[0].RowDepth))
        {
            var depth = siblings[0].RowDepth - 1;
            if (_levels.Rows[depth].Order is not { } order || siblings.Count <= window.Limit)
                continue;
            // A set under a sibling an outer window merges into a rest is not answered, and has none.
            if (DroppedAlong(parent, depth) is not null)
                continue;
            var parts = _query.Values[order.Value].Parts;
            var keys = siblings.Select(sibling => sibling.Values[order.Value].Read(order.Aggregation, parts) is { IsNumber: true } read ? read.Number : (double?)null).ToArray();
            var items = siblings.Select(sibling => ItemOrder.Entry.Of(ItemAt(sibling.Key, depth))).ToArray();
            var ordered = Enumerable.Range(0, siblings.Count).ToArray();
            Array.Sort(ordered, (a, b) =>
            {
                var compared = (keys[a], keys[b]) switch
                {
                    (null, null) => 0,
                    (null, _) => 1,
                    (_, null) => -1,
                    ({ } x, { } y) => order.Descending ? y.CompareTo(x) : x.CompareTo(y),
                };
                return compared != 0 ? compared : ItemOrder.Compare(items[a], items[b]);
            });
            bool Tied(int i, int j) => keys[ordered[i]] == keys[ordered[j]];
            var count = ordered.Length;
            var parentItems = Named(parent, depth);
            var start = Math.Min(StartUnder(window, parentItems), count - window.Limit);
            var first = start;
            while (first > 0 && Tied(first - 1, start))
                first--;
            var end = start + window.Limit;
            while (end < count && Tied(end, start + window.Limit - 1))
                end++;
            var made = new Window(parentItems, first, count - end);
            for (var i = 0; i < first - 1; i++)
                made.Dropped.Add(ItemAt(siblings[ordered[i]].Key, depth), PivotRestSide.Before);
            for (var i = end + 1; i < count; i++)
                made.Dropped.Add(ItemAt(siblings[ordered[i]].Key, depth), PivotRestSide.After);
            _planned.Add(made);
            var node = _windows;
            foreach (var item in parentItems)
            {
                node.Children ??= [];
                if (!node.Children.TryGetValue(item, out var child))
                    node.Children.Add(item, child = new WindowNode());
                node = child;
            }
            node.Window = made;
        }
        _ordering.Clear();
    }

    /// <summary>Folds one group in: into its cell, into a rest cell where a window merges its sibling,
    /// or not at all below such a sibling. False once the cells pass <see cref="PivotQuery.MaxLeaves"/>.</summary>
    public bool Add(SqliteDataReader reader)
    {
        if (Read(reader, out var key) is not { } records)
            return true;
        var rowDepth = reader.GetInt32(0);
        Group? cell;
        if (DroppedAlong(key, rowDepth) is { } dropped)
        {
            // Below a sibling merged into a rest, a group is not answered.
            if (rowDepth > dropped.Level + 1)
                return true;
            var rests = dropped.Side == PivotRestSide.Before ? dropped.Window.Before : dropped.Window.After;
            var columnItems = key[_rows.Length..];
            if (!rests.TryGetValue(columnItems, out cell))
            {
                if (_count == _query.MaxLeaves)
                    return false;
                rests.Add(columnItems, cell = new Group(columnItems, 0, reader.GetInt32(1), _values.Length));
                _count++;
            }
        }
        else if (!_cells.TryGetValue(key, out cell))
        {
            if (_count == _query.MaxLeaves)
                return false;
            _cells.Add(key, cell = new Group(key, rowDepth, reader.GetInt32(1), _values.Length));
            _count++;
        }
        Fold(cell, reader, records);
        return true;
    }

    /// <summary>The answer, built cell by cell (<see cref="PivotAnswerBuilder"/>).</summary>
    public PivotAnswer Answer(string sourceVersion)
    {
        var builder = new PivotAnswerBuilder(_query, sourceVersion);
        foreach (var cell in _cells.Values)
        {
            var rowItems = Named(cell.Key, cell.RowDepth);
            var columnItems = Named(cell.Key[_rows.Length..], cell.ColumnDepth);
            SetParts(builder, builder.AddCell(rowItems, columnItems, cell.Records), cell);
        }
        foreach (var made in _planned)
        {
            var at = builder.AddWindow(made.Parent, made.BeforeCount, made.AfterCount);
            foreach (var (side, rests) in (ReadOnlySpan<(PivotRestSide, Dictionary<PivotItemKey?[], Group>)>)[(PivotRestSide.Before, made.Before), (PivotRestSide.After, made.After)])
            {
                foreach (var (columnItems, rest) in rests)
                    SetParts(builder, builder.AddRest(at, side, Named(columnItems, rest.ColumnDepth), rest.Records), rest);
            }
        }
        return builder.Build();
    }

    // A group's key and records, as its row selects them: its depths, then each field's Item or NULL
    // below them, then count(*). Null for the grand total of no trade, which a set with no GROUP BY
    // answers a row for.
    private long? Read(SqliteDataReader reader, out PivotItemKey?[] key)
    {
        var rowDepth = reader.GetInt32(0);
        var columnDepth = reader.GetInt32(1);
        key = new PivotItemKey?[_rows.Length + _columns.Length];
        for (var level = 0; level < rowDepth; level++)
            key[level] = _rows[level].ItemAt(reader, 2 + level);
        for (var level = 0; level < columnDepth; level++)
            key[_rows.Length + level] = _columns[level].ItemAt(reader, 2 + _rows.Length + level);
        var records = reader.GetInt64(2 + _rows.Length + _columns.Length);
        return records == 0 ? null : records;
    }

    // Merges a group's records and parts, read after count(*), into a cell.
    private void Fold(Group cell, SqliteDataReader reader, long records)
    {
        var column = 3 + _rows.Length + _columns.Length;
        cell.Records += records;
        for (var v = 0; v < _values.Length; v++)
            cell.Values[v].Fold(reader, ref column, _values[v], _query.Values[v].Parts);
    }

    // The first level along a row group whose Item a planned window merges into a rest: that window,
    // the level and the side; null where none does.
    private Dropped? DroppedAlong(PivotItemKey?[] key, int rowDepth)
    {
        var node = _windows;
        for (var level = 0; level < rowDepth; level++)
        {
            var item = ItemAt(key, level);
            if (node.Window is { } window && window.Dropped.TryGetValue(item, out var side))
                return new Dropped(window, level, side);
            if (node.Children is not { } children || !children.TryGetValue(item, out var child))
                return null;
            node = child;
        }
        return null;
    }

    // Where the question starts the window under a parent: at its first sibling when no start names it.
    private static int StartUnder(PivotLevelWindow window, PivotItemKey[] parent)
    {
        foreach (var start in window.Starts)
        {
            if (start.Rows.SequenceEqual(parent))
                return start.Start;
        }
        return 0;
    }

    private void SetParts(PivotAnswerBuilder builder, int cell, Group group)
    {
        for (var v = 0; v < _values.Length; v++)
            group.Values[v].SetOn(builder, cell, v, _query.Values[v].Parts);
    }

    // A row group's key with its Items from a level down taken away: the group above it.
    private PivotItemKey?[] Above(PivotItemKey?[] key, int depth)
    {
        var above = new PivotItemKey?[_rows.Length + _columns.Length];
        Array.Copy(key, above, depth);
        return above;
    }

    // The Item a key names at a level above its depth.
    private static PivotItemKey ItemAt(PivotItemKey?[] key, int level)
        => key[level] ?? throw new InvalidOperationException($"Internal: a group named below level {level} names an Item there.");

    // The Items a key names, from its first, down to a depth.
    private static PivotItemKey[] Named(PivotItemKey?[] key, int depth)
    {
        var named = new PivotItemKey[depth];
        for (var level = 0; level < depth; level++)
            named[level] = ItemAt(key, level);
        return named;
    }

    // A row group crossed with a column group: its key names each level's Item, null below its depth
    // on either axis. A rest cell's is its column group's.
    private sealed class Group(PivotItemKey?[] key, int rowDepth, int columnDepth, int values)
    {
        public PivotItemKey?[] Key { get; } = key;

        public int RowDepth { get; } = rowDepth;

        public int ColumnDepth { get; } = columnDepth;

        public long Records { get; set; }

        public TradeParts[] Values { get; } = new TradeParts[values];
    }

    // A window as it is planned: its parent's Items, the siblings before and after the ones shown, the
    // siblings merged into a rest by their Item, and its rest cells by their column groups.
    private sealed class Window(PivotItemKey[] parent, int beforeCount, int afterCount)
    {
        public PivotItemKey[] Parent { get; } = parent;

        public int BeforeCount { get; } = beforeCount;

        public int AfterCount { get; } = afterCount;

        public Dictionary<PivotItemKey, PivotRestSide> Dropped { get; } = [];

        public Dictionary<PivotItemKey?[], Group> Before { get; } = new(ItemsComparer.Instance);

        public Dictionary<PivotItemKey?[], Group> After { get; } = new(ItemsComparer.Instance);
    }

    // The planned windows by their parents' Items, a level at a time, so a group finds the window over
    // each of its row groups by its Items alone.
    private sealed class WindowNode
    {
        public Window? Window { get; set; }

        public Dictionary<PivotItemKey, WindowNode>? Children { get; set; }
    }

    private readonly record struct Dropped(Window Window, int Level, PivotRestSide Side);
}
