using ExGrid.Data;

namespace ExPivot.Engine;

/// <summary>
/// Level Aggregates (ADR-0370 section 3): which groups a question's <see cref="PivotLevels"/> show,
/// the check of a level answer against them, and the bundled sources' level answer, merged from the
/// leaves.
/// <para>
/// A group named by Items <c>x₀ … x₍d−1₎</c> is shown when <c>x₀ … x₍d−2₎</c> are all expanded: the
/// outermost field's groups and the grand total always are. Along a leaf the shown groups are those
/// down to a depth, the first Item that is collapsed being the last one named, so a cell is a shown
/// row group crossed with a shown column group. The bundled sources merge each leaf, in the leaves'
/// order, into every shown cell along it, as the cube merges a leaf into its totals; the cells are
/// numbered as the leaves first reach them, row groups deepest first and, within one, column groups
/// deepest first. The same arithmetic in the same order is what makes the TypeScript engine's answer
/// the same, bit for bit.
/// </para>
/// </summary>
internal static class PivotLevelAnswers
{
    /// <summary>
    /// Why a level answer does not answer the levels asked, or null: a cell the rules do not show,
    /// two cells for one group, or a cell whose parent on either axis is not in the answer. Its
    /// shape — each cell named down to its depth — the answer checked as it was made.
    /// </summary>
    public static string? Mismatch(PivotLevels levels, PivotAnswer answer)
    {
        var rows = answer.RowAxes;
        var columns = answer.ColumnAxes;
        var rowExpanded = Expanded(levels.Rows, rows);
        var columnExpanded = Expanded(levels.Columns, columns);
        var cells = new HashSet<CellPath>(answer.LeafCount);
        var paths = new CellPath[answer.LeafCount];
        for (var cell = 0; cell < answer.LeafCount; cell++)
        {
            if (!Shown(rows, rowExpanded, cell, Depth(rows, cell)) || !Shown(columns, columnExpanded, cell, Depth(columns, cell)))
                return $"its cell {Describe(answer, cell)} is a group the question's levels do not show";
            paths[cell] = CellPath.Of(rows, columns, cell);
            if (!cells.Add(paths[cell]))
                return $"it has two cells for {Describe(answer, cell)}";
        }
        for (var cell = 0; cell < answer.LeafCount; cell++)
        {
            var path = paths[cell];
            var rowDepth = Depth(rows, cell);
            var columnDepth = Depth(columns, cell);
            if (rowDepth > 0 && !cells.Contains(path.Above(rowDepth - 1)))
                return $"its cell {Describe(answer, cell)} is shown without its parent row group";
            if (columnDepth > 0 && !cells.Contains(path.Above(rows.Length + columnDepth - 1)))
                return $"its cell {Describe(answer, cell)} is shown without its parent column group";
        }
        return null;
    }

    /// <summary>
    /// The bundled sources' level answer (ADR-0370 section 3), for a question with levels whose leaves
    /// passed its cap: a pass over every leaf there is, each sum with its magnitudes beside it, merged
    /// into the cells shown. Never kept: a Change Batch is not folded into it.
    /// </summary>
    public static async ValueTask<PivotAnswer> AnswerAsync(
        Snapshot snapshot,
        IReadOnlyDictionary<string, FieldBinding> bindings,
        PivotQuery query,
        Action<long>? rowsRead,
        string sourceVersion,
        Slicer slicer)
    {
        var pass = new AggregationPass(snapshot, bindings, query, keepRows: false, rowsRead, levels: true);
        await slicer.PassAsync(pass.RowCount, pass.Step).ConfigureAwait(false);
        await pass.CompleteAsync(slicer).ConfigureAwait(false);
        var leaves = await pass.AnswerAsync(sourceVersion, slicer).ConfigureAwait(false);
        return await FromLeavesAsync(query, leaves, slicer).ConfigureAwait(false);
    }

    /// <summary>
    /// The level answer to <paramref name="query"/> merged from <paramref name="leaves"/>, every leaf
    /// there is, each sum with its magnitudes beside it; or, past the question's cap on cells, the
    /// refusal for it. In slices, as the cube is made (PV-40).
    /// </summary>
    public static async ValueTask<PivotAnswer> FromLeavesAsync(PivotQuery query, PivotAnswer leaves, Slicer slicer)
    {
        var levels = query.Levels ?? throw new InvalidOperationException("Internal: a level answer answers a question with levels.");
        var rows = leaves.RowAxes;
        var columns = leaves.ColumnAxes;
        var rowExpanded = Expanded(levels.Rows, rows);
        var columnExpanded = Expanded(levels.Columns, columns);
        var levelCount = rows.Length + columns.Length;
        var answered = leaves.ValueColumns;
        var values = new PartColumns[answered.Length];
        for (var v = 0; v < values.Length; v++)
        {
            values[v] = new PartColumns(answered[v].Parts, 16);
            if (answered[v].Columns.Absolutes is not null)
                values[v].TrackAbsolutes();
        }
        var index = new Dictionary<CellPath, int>();
        var paths = new List<CellPath>();
        var records = new long[16];
        var tooMany = false;
        await slicer.ForAsync(leaves.LeafCount, (from, to) =>
        {
            for (var leaf = from; leaf < to && !tooMany; leaf++)
            {
                var rowDeepest = DeepestShown(rows, rowExpanded, leaf);
                var columnDeepest = DeepestShown(columns, columnExpanded, leaf);
                for (var rowDepth = rowDeepest; rowDepth >= 0 && !tooMany; rowDepth--)
                {
                    for (var columnDepth = columnDeepest; columnDepth >= 0; columnDepth--)
                    {
                        var path = CellPath.Along(rows, columns, leaf, rowDepth, columnDepth);
                        if (!index.TryGetValue(path, out var cell))
                        {
                            cell = paths.Count;
                            if (cell == query.MaxLeaves)
                            {
                                tooMany = true;
                                break;
                            }
                            index.Add(path, cell);
                            paths.Add(path);
                            if (cell == records.Length)
                                Array.Resize(ref records, cell * 2);
                            foreach (var columnsOfValue in values)
                                columnsOfValue.EnsureCapacity(cell + 1);
                        }
                        records[cell] += leaves.Records[leaf];
                        for (var v = 0; v < values.Length; v++)
                            values[v].Merge(cell, answered[v].Columns, leaf);
                    }
                }
            }
        }, weight: Math.Max(1, (rows.Length + 1) * (columns.Length + 1) * (1 + values.Length))).ConfigureAwait(false);
        if (tooMany)
            return PivotAnswer.Refused(PivotSourceRefusal.TooManyLeaves(query.MaxLeaves));

        var count = paths.Count;
        foreach (var parts in values)
        {
            await slicer.ForAsync(count, parts.Finish).ConfigureAwait(false);
            await slicer.ForAsync(count, parts.Canonicalize).ConfigureAwait(false);
        }
        var axes = new PivotAnswerAxis[levelCount];
        for (var level = 0; level < levelCount; level++)
        {
            var axis = level < rows.Length ? rows[level] : columns[level - rows.Length];
            // The Items some cell names, in the leaves' answer's order.
            var renumbered = new int[axis.ItemArray.Length];
            Array.Fill(renumbered, -1);
            foreach (var path in paths)
            {
                var item = path.ItemAt(level);
                if (item >= 0)
                    renumbered[item] = 0;
            }
            var items = new List<PivotItemKey>();
            for (var item = 0; item < renumbered.Length; item++)
            {
                if (renumbered[item] < 0)
                    continue;
                renumbered[item] = items.Count;
                items.Add(axis.ItemArray[item]);
            }
            var itemOfCell = new int[count];
            for (var cell = 0; cell < count; cell++)
            {
                var item = paths[cell].ItemAt(level);
                itemOfCell[cell] = item < 0 ? -1 : renumbered[item];
            }
            axes[level] = new PivotAnswerAxis(axis.Field, [.. items], itemOfCell, count, levels: true);
        }
        var answerValues = answered.Select((v, i) => new PivotAnswerValues(v.Field, values[i], count)).ToArray();
        return new PivotAnswer(leaves.SourceVersion, axes[..rows.Length], axes[rows.Length..], count, records, answerValues, levels: true);
    }

    // Each level's Items: whether the groups below each are shown.
    private static bool[][] Expanded(IReadOnlyList<PivotLevelRule> rules, PivotAnswerAxis[] axes)
    {
        var expanded = new bool[axes.Length][];
        for (var level = 0; level < axes.Length; level++)
        {
            var items = axes[level].ItemArray;
            var rule = rules[level];
            var shown = expanded[level] = new bool[items.Length];
            for (var item = 0; item < items.Length; item++)
                shown[item] = rule.IsExpanded(items[item]);
        }
        return expanded;
    }

    // How deep a cell is named along one axis.
    private static int Depth(PivotAnswerAxis[] axes, int cell)
    {
        var depth = 0;
        while (depth < axes.Length && axes[depth].ItemOfLeaf[cell] >= 0)
            depth++;
        return depth;
    }

    // Whether the group a cell names down to depth along one axis is shown: every Item above its
    // own is expanded.
    private static bool Shown(PivotAnswerAxis[] axes, bool[][] expanded, int cell, int depth)
    {
        for (var level = 0; level < depth - 1; level++)
        {
            if (!expanded[level][axes[level].ItemOfLeaf[cell]])
                return false;
        }
        return true;
    }

    // The deepest group shown along a leaf on one axis.
    private static int DeepestShown(PivotAnswerAxis[] axes, bool[][] expanded, int leaf)
    {
        if (axes.Length == 0)
            return 0;
        var depth = 1;
        while (depth < axes.Length && expanded[depth - 1][axes[depth - 1].ItemOfLeaf[leaf]])
            depth++;
        return depth;
    }

    // [Region=Text:East] × [] — a cell's Items, for a refusal.
    private static string Describe(PivotAnswer answer, int cell)
    {
        static string Path(PivotAnswerAxis[] axes, int cell)
            => "[" + string.Join(", ", axes.TakeWhile(a => a.ItemOfLeaf[cell] >= 0).Select(a => $"{a.Field}={a.ItemArray[a.ItemOfLeaf[cell]]}")) + "]";
        return $"{Path(answer.RowAxes, cell)} × {Path(answer.ColumnAxes, cell)}";
    }

    // A cell's Item index at every level, −1 below its depth on either axis.
    private readonly struct CellPath : IEquatable<CellPath>
    {
        private readonly int[] _items;

        private CellPath(int[] items) => _items = items;

        public static CellPath Of(PivotAnswerAxis[] rows, PivotAnswerAxis[] columns, int cell)
            => Along(rows, columns, cell, rows.Length, columns.Length);

        public static CellPath Along(PivotAnswerAxis[] rows, PivotAnswerAxis[] columns, int cell, int rowDepth, int columnDepth)
        {
            var items = new int[rows.Length + columns.Length];
            for (var level = 0; level < rows.Length; level++)
                items[level] = level < rowDepth ? rows[level].ItemOfLeaf[cell] : -1;
            for (var level = 0; level < columns.Length; level++)
                items[rows.Length + level] = level < columnDepth ? columns[level].ItemOfLeaf[cell] : -1;
            return new CellPath(items);
        }

        public int ItemAt(int level) => _items[level];

        // The path with the Item at one level taken away: the group above along that axis.
        public CellPath Above(int level)
        {
            var items = (int[])_items.Clone();
            items[level] = -1;
            return new CellPath(items);
        }

        public bool Equals(CellPath other) => _items.AsSpan().SequenceEqual(other._items);

        public override bool Equals(object? obj) => obj is CellPath other && Equals(other);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var item in _items)
                hash.Add(item);
            return hash.ToHashCode();
        }
    }
}
