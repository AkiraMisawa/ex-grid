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
    /// two cells for one group, or a cell whose parent on either axis is not in the answer; a window
    /// the question does not ask for, or one that is not where it was asked; a rest cell without its
    /// window's parent, or a side it says has no siblings beyond its margin. Its shape — each cell
    /// named down to its depth — the answer checked as it was made.
    /// </summary>
    public static string? Mismatch(PivotLevels levels, PivotAnswer answer)
    {
        var rows = answer.RowAxes;
        var columns = answer.ColumnAxes;
        var rowExpanded = Expanded(levels.Rows, rows);
        var columnExpanded = Expanded(levels.Columns, columns);
        var restOf = answer.Rests;
        var cells = new HashSet<CellPath>(answer.LeafCount);
        var rests = new HashSet<(CellPath, int)>();
        var paths = new CellPath[answer.LeafCount];
        for (var cell = 0; cell < answer.LeafCount; cell++)
        {
            if (!Shown(rows, rowExpanded, cell, Depth(rows, cell)) || !Shown(columns, columnExpanded, cell, Depth(columns, cell)))
                return $"its cell {Describe(answer, cell)} is a group the question's levels do not show";
            paths[cell] = CellPath.Of(rows, columns, cell);
            if (restOf is not null && restOf[cell] >= 0)
            {
                if (!rests.Add((paths[cell], restOf[cell])))
                    return $"it has two rest cells for one side of a window at {Describe(answer, cell)}";
                continue;
            }
            if (!cells.Add(paths[cell]))
                return $"it has two cells for {Describe(answer, cell)}";
        }
        for (var cell = 0; cell < answer.LeafCount; cell++)
        {
            var path = paths[cell];
            var rowDepth = Depth(rows, cell);
            var columnDepth = Depth(columns, cell);
            if (restOf is not null && restOf[cell] >= 0)
            {
                if (!cells.Contains(path))
                    return $"its rest cell {Describe(answer, cell)} is answered without its parent's cell";
                continue;
            }
            if (rowDepth > 0 && !cells.Contains(path.Above(rowDepth - 1)))
                return $"its cell {Describe(answer, cell)} is shown without its parent row group";
            if (columnDepth > 0 && !cells.Contains(path.Above(rows.Length + columnDepth - 1)))
                return $"its cell {Describe(answer, cell)} is shown without its parent column group";
        }
        return WindowMismatch(levels, answer, rowExpanded, paths);
    }

    // Each set ordered by a value under a question's window: answered whole up to the limit, and past
    // it as a window where it was asked, its rest cells on a side only where that side has siblings
    // beyond its margin.
    private static string? WindowMismatch(PivotLevels levels, PivotAnswer answer, bool[][] rowExpanded, CellPath[] paths)
    {
        var rows = answer.RowAxes;
        var windows = answer.WindowArray;
        var restOf = answer.Rests;
        if (levels.Window is not { } asked)
            return windows.Length == 0 ? null : "it answers a window, and the question asks for none";
        var rowCount = rows.Length;
        // The children each parent row group has at the grand total of the columns, by the parent's path.
        var children = new Dictionary<string, int>(StringComparer.Ordinal);
        var parents = new HashSet<string>(StringComparer.Ordinal);
        var order = new List<string>();
        for (var cell = 0; cell < paths.Length; cell++)
        {
            if (restOf is not null && restOf[cell] >= 0)
                continue;
            if (!paths[cell].AtColumnRoot(rowCount))
                continue;
            var depth = Depth(rows, cell);
            parents.Add(paths[cell].RowKey(depth));
            if (depth == 0)
                continue;
            var parent = paths[cell].RowKey(depth - 1);
            if (children.TryGetValue(parent, out var count))
            {
                children[parent] = count + 1;
            }
            else
            {
                children[parent] = 1;
                order.Add(parent);
            }
        }
        var windowed = new HashSet<string>(StringComparer.Ordinal);
        for (var w = 0; w < windows.Length; w++)
        {
            var window = windows[w];
            var key = string.Join(",", window.Parent);
            var depth = window.Parent.Count;
            if (!windowed.Add(key))
                return $"it has two windows under {DescribeParent(rows, window.Parent)}";
            if (!parents.Contains(key))
                return $"its window {w} is under {DescribeParent(rows, window.Parent)}, a group it has no cell for";
            if (depth > 0 && !rowExpanded[depth - 1][window.Parent[depth - 1]])
                return $"its window {w} is under {DescribeParent(rows, window.Parent)}, whose children the question's levels do not show";
            if (levels.Rows[depth].Order is null)
                return $"its window {w} is of the Items of '{rows[depth].Field}', which the question orders by label";
            var answered = children.GetValueOrDefault(key);
            var margins = (window.Before > 0 ? 1 : 0) + (window.After > 0 ? 1 : 0);
            var shown = answered - margins;
            if (shown < 1)
                return $"its window {w} under {DescribeParent(rows, window.Parent)} shows no sibling";
            var total = window.Before + shown + window.After;
            if (total <= asked.Limit)
                return $"its window {w} under {DescribeParent(rows, window.Parent)} is of {total} siblings, which the limit of {asked.Limit} answers whole";
            var start = Math.Min(asked.StartUnder(ParentItems(rows, window.Parent)), total - asked.Limit);
            if (window.Before > start || window.Before + shown < start + asked.Limit)
                return $"its window {w} under {DescribeParent(rows, window.Parent)} shows siblings {window.Before} to {window.Before + shown - 1}, and the question asks for {start} to {start + asked.Limit - 1}";
        }
        var restSides = new HashSet<int>();
        for (var cell = 0; cell < paths.Length; cell++)
        {
            if (restOf is not null && restOf[cell] >= 0 && paths[cell].AtColumnRoot(rowCount))
                restSides.Add(restOf[cell]);
        }
        for (var w = 0; w < windows.Length; w++)
        {
            foreach (var (side, count, code) in new[] { ("before", windows[w].Before, 2 * w), ("after", windows[w].After, 2 * w + 1) })
            {
                if (count >= 2 != restSides.Contains(code))
                {
                    return count >= 2
                        ? $"its window {w} has {count} siblings {side} it and no rest cell for those beyond its margin"
                        : $"its window {w} has {count} siblings {side} it and a rest cell beyond its margin";
                }
            }
        }
        // A set ordered by a value that came whole holds no more siblings than the limit.
        foreach (var parent in order)
        {
            var count = children[parent];
            if (windowed.Contains(parent) || count <= asked.Limit)
                continue;
            var named = parent.Length == 0 ? [] : parent.Split(',').Select(int.Parse).ToArray();
            if (levels.Rows[named.Length].Order is null)
                continue;
            return $"it answers the {count} siblings under {DescribeParent(rows, named)} whole, past the window's limit of {asked.Limit}";
        }
        return null;
    }

    private static PivotItemKey[] ParentItems(PivotAnswerAxis[] rows, IReadOnlyList<int> parent)
        => parent.Select((item, level) => rows[level].ItemArray[item]).ToArray();

    private static string DescribeParent(PivotAnswerAxis[] rows, IReadOnlyList<int> parent)
        => "[" + string.Join(", ", parent.Select((item, level) => $"{rows[level].Field}={rows[level].ItemArray[item]}")) + "]";

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
    /// <para>Under a question's window, the cells are merged once to order each sibling set past the
    /// limit by its value — among ties, by the order the leaves first reached them — and merged again
    /// with the siblings beyond each margin merged into rest cells instead, a leaf at a time in the
    /// leaves' order, so a rest's parts are the merge of its leaves as a cell's are.</para>
    /// </summary>
    public static async ValueTask<PivotAnswer> FromLeavesAsync(PivotQuery query, PivotAnswer leaves, Slicer slicer)
    {
        var levels = query.Levels ?? throw new InvalidOperationException("Internal: a level answer answers a question with levels.");
        var window = levels.Window;
        if (window is null || !levels.Rows.Any(rule => rule.Order is not null))
        {
            var whole = await MergeAsync(query, levels, leaves, null, columnRootOnly: false, slicer).ConfigureAwait(false);
            return whole is null ? PivotAnswer.Refused(PivotSourceRefusal.TooManyLeaves(query.MaxLeaves)) : AnswerOf(leaves, whole, []);
        }
        // The row groups at the grand total of the columns, every one, are what orders each set.
        var rowGroups = await MergeAsync(query, levels, leaves, null, columnRootOnly: true, slicer).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Internal: an uncapped merge is never refused.");
        var planned = PlanWindows(levels, window, leaves, rowGroups);
        var cut = await MergeAsync(query, levels, leaves, planned.Count == 0 ? null : WindowTree.Of(planned), columnRootOnly: false, slicer).ConfigureAwait(false);
        return cut is null ? PivotAnswer.Refused(PivotSourceRefusal.TooManyLeaves(query.MaxLeaves)) : AnswerOf(leaves, cut, planned);
    }

    // Where a dropped sibling's leaves go: its window, by its provisional place, and the side.
    private readonly record struct Dropped(int Window, bool After);

    // A window as the first merge plans it: its parent's path in the leaves' Items, its counts, and
    // its dropped siblings by their Item.
    private sealed record PlannedWindow(int[] Parent, int Before, int After, Dictionary<int, Dropped> DroppedItems);

    // The cells merged from the leaves: each one's path in the leaves' Items, its rest code, its
    // records and parts.
    private sealed record Merged(List<CellPath> Paths, List<int> Rests, long[] Records, PartColumns[] Values)
    {
        public int Count => Paths.Count;
    }

    // The planned windows by their parents' Items, level by level, so a leaf finds the window over
    // each of its row groups by its Items alone.
    private sealed class WindowTree
    {
        public PlannedWindow? Window { get; private set; }

        public Dictionary<int, WindowTree>? Children { get; private set; }

        public static WindowTree Of(List<PlannedWindow> planned)
        {
            var root = new WindowTree();
            foreach (var window in planned)
            {
                var node = root;
                foreach (var item in window.Parent)
                {
                    node.Children ??= [];
                    if (!node.Children.TryGetValue(item, out var child))
                        node.Children[item] = child = new WindowTree();
                    node = child;
                }
                node.Window = window;
            }
            return root;
        }
    }

    // Merges every leaf into the cells shown along it; a leaf under a dropped sibling of a window is
    // merged into that window's rest cell instead, once a column group. Null past the cap; with
    // columnRootOnly, only the cells at the grand total of the columns, and never capped, for
    // ordering each set before its window is cut.
    private static async ValueTask<Merged?> MergeAsync(
        PivotQuery query, PivotLevels levels, PivotAnswer leaves, WindowTree? windows, bool columnRootOnly, Slicer slicer)
    {
        var capped = !columnRootOnly;
        var rows = leaves.RowAxes;
        var columns = leaves.ColumnAxes;
        var rowExpanded = Expanded(levels.Rows, rows);
        var columnExpanded = Expanded(levels.Columns, columns);
        var answered = leaves.ValueColumns;
        var values = new PartColumns[answered.Length];
        for (var v = 0; v < values.Length; v++)
        {
            values[v] = new PartColumns(answered[v].Parts, 16);
            if (answered[v].Columns.Absolutes is not null)
                values[v].TrackAbsolutes();
        }
        var index = new Dictionary<(CellPath, int), int>();
        var paths = new List<CellPath>();
        var rests = new List<int>();
        var records = new long[16];
        var tooMany = false;
        await slicer.ForAsync(leaves.LeafCount, (from, to) =>
        {
            for (var leaf = from; leaf < to && !tooMany; leaf++)
            {
                var rowDeepest = DeepestShown(rows, rowExpanded, leaf);
                var columnDeepest = columnRootOnly ? 0 : DeepestShown(columns, columnExpanded, leaf);
                // The first level whose sibling along the leaf a window drops, and where its leaves go.
                var droppedAt = rowDeepest;
                Dropped? dropped = null;
                var node = windows;
                for (var level = 0; level < rowDeepest && node is not null; level++)
                {
                    var item = rows[level].ItemOfLeaf[leaf];
                    if (node.Window is { } held && held.DroppedItems.TryGetValue(item, out var drop))
                    {
                        droppedAt = level;
                        dropped = drop;
                        break;
                    }
                    node = node.Children is { } children && children.TryGetValue(item, out var child) ? child : null;
                }
                for (var rowDepth = rowDeepest; rowDepth >= 0 && !tooMany; rowDepth--)
                {
                    if (dropped is not null && rowDepth > droppedAt + 1)
                        continue;
                    var rest = dropped is { } d && rowDepth == droppedAt + 1 ? 2 * d.Window + (d.After ? 1 : 0) : -1;
                    for (var columnDepth = columnDeepest; columnDepth >= 0; columnDepth--)
                    {
                        var path = CellPath.Along(rows, columns, leaf, rest < 0 ? rowDepth : droppedAt, columnDepth);
                        if (!index.TryGetValue((path, rest), out var cell))
                        {
                            cell = paths.Count;
                            if (capped && cell == query.MaxLeaves)
                            {
                                tooMany = true;
                                break;
                            }
                            index.Add((path, rest), cell);
                            paths.Add(path);
                            rests.Add(rest);
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
            return null;
        var count = paths.Count;
        foreach (var parts in values)
        {
            await slicer.ForAsync(count, parts.Finish).ConfigureAwait(false);
            await slicer.ForAsync(count, parts.Canonicalize).ConfigureAwait(false);
        }
        return new Merged(paths, rests, records, values);
    }

    // The windows of the sibling sets past the limit, outermost level first: a set under a sibling an
    // outer window drops is not answered, and has none.
    private static List<PlannedWindow> PlanWindows(PivotLevels levels, PivotLevelWindow window, PivotAnswer leaves, Merged merged)
    {
        var rows = leaves.RowAxes;
        var rowCount = rows.Length;
        // Each parent's children at the grand total of the columns, in the order the leaves first
        // reached them.
        var children = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        var parentPaths = new List<int[]>();
        for (var cell = 0; cell < merged.Count; cell++)
        {
            var path = merged.Paths[cell];
            if (!path.AtColumnRoot(rowCount))
                continue;
            var depth = path.RowDepth(rowCount);
            if (depth == 0)
                continue;
            var key = path.RowKey(depth - 1);
            if (!children.TryGetValue(key, out var siblings))
            {
                children[key] = siblings = [];
                parentPaths.Add(Enumerable.Range(0, depth - 1).Select(path.ItemAt).ToArray());
            }
            siblings.Add(cell);
        }
        var planned = new List<PlannedWindow>();
        var droppedUnder = new Dictionary<string, PlannedWindow>(StringComparer.Ordinal);
        foreach (var parent in parentPaths.OrderBy(p => p.Length))
        {
            var depth = parent.Length;
            if (levels.Rows[depth].Order is not { } order)
                continue;
            // A set under a dropped sibling is merged into a rest: it has no window.
            var under = false;
            for (var level = 0; level < depth && !under; level++)
                under = droppedUnder.TryGetValue(string.Join(",", parent.Take(level)), out var above) && above.DroppedItems.ContainsKey(parent[level]);
            if (under)
                continue;
            var siblings = children[string.Join(",", parent)];
            if (siblings.Count <= window.Limit)
                continue;
            var values = merged.Values[order.Value];
            var keys = new Dictionary<int, double?>();
            foreach (var cell in siblings)
            {
                var value = values.Read(cell, order.Aggregation);
                keys[cell] = value.IsEmpty || value.Error is not null ? null : value.Number;
            }
            var ordered = siblings.OrderBy(cell => cell, Comparer<int>.Create((a, b) =>
            {
                var x = keys[a];
                var y = keys[b];
                if (x is null || y is null)
                    return x is null ? (y is null ? 0 : 1) : -1;
                var compared = x < y ? -1 : x > y ? 1 : 0;
                return order.Descending ? -compared : compared;
            })).ToArray();
            bool Tied(int i, int j) => keys[ordered[i]] == keys[ordered[j]];
            var count = ordered.Length;
            var start = Math.Min(window.StartUnder(parent.Select((item, level) => rows[level].ItemArray[item]).ToArray()), count - window.Limit);
            var first = start;
            while (first > 0 && Tied(first - 1, start))
                first--;
            var end = start + window.Limit;
            while (end < count && Tied(end, start + window.Limit - 1))
                end++;
            var dropped = new Dictionary<int, Dropped>();
            var provisional = planned.Count;
            for (var i = 0; i < first - 1; i++)
                dropped[merged.Paths[ordered[i]].ItemAt(depth)] = new Dropped(provisional, false);
            for (var i = end + 1; i < count; i++)
                dropped[merged.Paths[ordered[i]].ItemAt(depth)] = new Dropped(provisional, true);
            var made = new PlannedWindow(parent, first, count - end, dropped);
            planned.Add(made);
            droppedUnder[string.Join(",", parent)] = made;
        }
        return planned;
    }

    // The answer of merged cells, its Items renumbered to those some cell names, in the leaves'
    // answer's order; its windows in the order of their parents' cells at the grand total of the
    // columns.
    private static PivotAnswer AnswerOf(PivotAnswer leaves, Merged merged, List<PlannedWindow> planned)
    {
        var rows = leaves.RowAxes;
        var columns = leaves.ColumnAxes;
        var levelCount = rows.Length + columns.Length;
        var count = merged.Count;
        var axes = new PivotAnswerAxis[levelCount];
        var renumberings = new int[levelCount][];
        for (var level = 0; level < levelCount; level++)
        {
            var axis = level < rows.Length ? rows[level] : columns[level - rows.Length];
            // The Items some cell names, in the leaves' answer's order.
            var renumbered = renumberings[level] = new int[axis.ItemArray.Length];
            Array.Fill(renumbered, -1);
            foreach (var path in merged.Paths)
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
                var item = merged.Paths[cell].ItemAt(level);
                itemOfCell[cell] = item < 0 ? -1 : renumbered[item];
            }
            axes[level] = new PivotAnswerAxis(axis.Field, [.. items], itemOfCell, count, levels: true);
        }
        var answerValues = leaves.ValueColumns.Select((v, i) => new PivotAnswerValues(v.Field, merged.Values[i], count)).ToArray();
        if (planned.Count == 0)
            return new PivotAnswer(leaves.SourceVersion, axes[..rows.Length], axes[rows.Length..], count, merged.Records, answerValues, levels: true);
        // Each window's parent's cell at the grand total, which places it.
        var cellOf = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var cell = 0; cell < count; cell++)
        {
            var path = merged.Paths[cell];
            if (merged.Rests[cell] >= 0 || !path.AtColumnRoot(rows.Length))
                continue;
            cellOf[path.RowKey(path.RowDepth(rows.Length))] = cell;
        }
        var placeOf = planned.Select(w => cellOf[string.Join(",", w.Parent)]).ToArray();
        var order = Enumerable.Range(0, planned.Count).OrderBy(w => placeOf[w]).ToArray();
        var finalOf = new int[planned.Count];
        for (var place = 0; place < order.Length; place++)
            finalOf[order[place]] = place;
        var windows = order.Select(w =>
        {
            var made = planned[w];
            var parentItems = made.Parent.Select((item, level) => renumberings[level][item]).ToArray();
            return new PivotAnswerWindow(parentItems, made.Before, made.After);
        }).ToArray();
        var rests = merged.Rests.Select(code => code < 0 ? -1 : 2 * finalOf[code / 2] + code % 2).ToArray();
        return new PivotAnswer(leaves.SourceVersion, axes[..rows.Length], axes[rows.Length..], count, merged.Records, answerValues, levels: true, windows, rests);
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

        // Whether every column Item is −1: the cell is at the grand total of the columns.
        public bool AtColumnRoot(int rowCount)
        {
            for (var level = rowCount; level < _items.Length; level++)
            {
                if (_items[level] >= 0)
                    return false;
            }
            return true;
        }

        // How deep the cell is named along the rows.
        public int RowDepth(int rowCount)
        {
            var depth = 0;
            while (depth < rowCount && _items[depth] >= 0)
                depth++;
            return depth;
        }

        // The row Items down to a depth, as a key.
        public string RowKey(int depth) => string.Join(",", _items.Take(depth));

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
