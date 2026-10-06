namespace ExPivot.Engine;

// Private working indexes belong to one computation, never to a published report.
internal sealed class ComputationCube
{
    private readonly Dictionary<int, (AxisNode Row, AxisNode Column, long Records, int First)> _leaves = [];
    private readonly Dictionary<long, SortedSet<(int First, int Leaf)>> _members = new(CellKey.Comparer);
    private readonly ComputationAxis.Working _rows;
    private readonly ComputationAxis.Working _columns;
    private ComputationCells _cells;
    private PivotCube _cube;
    private readonly HashSet<int> _nonAdditive = [];
    public IReadOnlySet<int> ChangedRowNodes => _rows.Changed;
    public IReadOnlySet<int> RemovedRowNodes => _rows.Removed;
    public IReadOnlySet<int> ChangedColumnNodes => _columns.Changed;
    public IReadOnlySet<int> RemovedColumnNodes => _columns.Removed;
    public HashSet<int> ValueRows { get; } = [];
    public HashSet<int> ValueColumns { get; } = [];

    private ComputationCube(PivotCube cube, ComputationCells cells,
        ComputationAxis.Working rows, ComputationAxis.Working columns)
    {
        _cube = cube; _cells = cells; _rows = rows; _columns = columns;
    }

    public static async ValueTask<ComputationCube> CreateAsync(PivotCube cube, AggregationPass pass, Slicer slicer)
    {
        var cells = await cube.ShareCellsAsync(slicer).ConfigureAwait(false);
        var rows = await ComputationAxis.Working.CreateAsync(cube.RowRoot, slicer).ConfigureAwait(false);
        var columns = await ComputationAxis.Working.CreateAsync(cube.ColumnRoot, slicer).ConfigureAwait(false);
        await pass.PrepareChainsAsync(slicer).ConfigureAwait(false);
        var result = new ComputationCube(cube, cells, rows, columns);
        for (var leaf = 0; leaf < pass.Leaves.Count; leaf++)
        {
            if (pass.Leaves.Records[leaf] > 0)
            {
                var added = result.AddLeaf(pass, leaf, initial: true);
                var cell = cells.Cells[CellKey.Of(added.Row.Id, added.Column.Id)];
                var (parts, offset) = cells.At(cell);
                if (!Additive(parts, offset)) result._nonAdditive.Add(leaf);
            }
            if (slicer.Done(1 + pass.Query.Rows.Count + pass.Query.Columns.Count))
                await slicer.PauseAsync().ConfigureAwait(false);
        }
        return result;
    }

    private (AxisNode Row, AxisNode Column, long Records, int First) AddLeaf(AggregationPass pass, int leaf, bool initial = false)
    {
        var row = _rows.AddLeaf(pass, leaf, 0, pass.Query.Rows.Count, initial);
        var column = _columns.AddLeaf(pass, leaf, pass.Query.Rows.Count, pass.Query.Columns.Count, initial);
        var added = (row, column, pass.Leaves.Records[leaf], pass.FirstRecord(leaf));
        _leaves.Add(leaf, added);
        foreach (var key in Totals(row, column))
        {
            if (!_members.TryGetValue(key, out var members))
                _members[key] = members = [];
            members.Add((added.Item4, leaf));
        }
        return added;
    }

    // This sufficient bound makes every decimal prefix exact, in any leaf order: at most
    // Int32.MaxValue leaves, each <= 10^12 and scale <= 6, need fewer than 96 coefficient bits.
    // It is an optimization guard, never a data cap. Larger/scaled/inexact sums and every
    // non-invertible part retain reference-order merging, including decimal rounding/overflow.
    private static bool Additive(PartColumns[] parts, int cell)
    {
        Span<int> bits = stackalloc int[4];
        foreach (var column in parts)
        {
            if ((column.Parts & ~PivotParts.Sum) != 0 || column.Counts[cell].NonFinite) return false;
            if (column.Sums is not { } sums) continue;
            var sum = sums[cell];
            if (sum.Inexact || sum.Exact < -1_000_000_000_000m || sum.Exact > 1_000_000_000_000m) return false;
            decimal.GetBits(sum.Exact, bits);
            if (((bits[3] >> 16) & 255) > 6) return false;
        }
        return true;
    }

    private static IEnumerable<long> Totals(AxisNode row, AxisNode column)
    {
        for (var r = row; r is not null; r = r.Parent)
        for (var c = column; c is not null; c = c.Parent)
            if (r != row || c != column)
                yield return CellKey.Of(r.Id, c.Id);
    }

    public async ValueTask<PivotCube> UpdateAsync(AggregationPass pass, string version, Slicer slicer)
    {
        _rows.Begin(); _columns.Begin(); ValueRows.Clear(); ValueColumns.Clear();
        var changed = pass.ChangedLeaves.Order().ToArray();
        var values = await pass.PartsOfAsync(changed, slicer).ConfigureAwait(false);
        var additive = _nonAdditive.Count == 0;
        for (var at = 0; at < changed.Length; at++)
        {
            if (pass.Leaves.Records[changed[at]] == 0 || Additive(values, at)) _nonAdditive.Remove(changed[at]);
            else _nonAdditive.Add(changed[at]);
        }
        additive &= _nonAdditive.Count == 0;
        var write = new ComputationCells.Writer(_cells);
        var totals = new HashSet<long>(CellKey.Comparer);
        long included = _cube.IncludedRecordCount;
        for (var at = 0; at < changed.Length; at++)
        {
            if (slicer.Done(1 + pass.Query.Rows.Count + pass.Query.Columns.Count))
                await slicer.PauseAsync().ConfigureAwait(false);
            var leaf = changed[at];
            var records = pass.Leaves.Records[leaf];
            var held = _leaves.TryGetValue(leaf, out var old);
            included += records - (held ? old.Records : 0);
            if (!held && records == 0)
                continue;
            var (row, column, _, first) = held ? old : AddLeaf(pass, leaf);
            for (var r = row; r is not null; r = r.Parent) ValueRows.Add(r.Id);
            for (var c = column; c is not null; c = c.Parent) ValueColumns.Add(c.Id);
            var key = CellKey.Of(row.Id, column.Id);
            var affected = Totals(row, column).ToArray();
            totals.UnionWith(affected);
            if (additive)
            {
                var oldParts = held ? _cells.At(_cells.Cells[key]) : default;
                foreach (var total in affected)
                    write.Adjust(write.Cell(total), oldParts.Columns, oldParts.Offset,
                        records > 0 ? values : null, at);
            }
            if (records == 0)
            {
                write.Remove(key);
                _rows.RemoveLeaf(row); _columns.RemoveLeaf(column);
                foreach (var total in Totals(row, column)) _members[total].Remove((first, leaf));
                _leaves.Remove(leaf);
            }
            else
            {
                var nextFirst = pass.FirstRecord(leaf);
                if (first != nextFirst)
                    foreach (var total in Totals(row, column))
                    {
                        _members[total].Remove((first, leaf));
                        _members[total].Add((nextFirst, leaf));
                    }
                _leaves[leaf] = (row, column, records, nextFirst);
                write.Put(write.Cell(key), values, at);
            }
        }
        foreach (var key in totals)
        {
            var members = _members[key];
            if (members.Count == 0)
            {
                write.Remove(key);
                continue;
            }
            var cell = write.Cell(key);
            if (additive) { write.Finish(cell); continue; }
            write.Reset(cell);
            foreach (var (_, leaf) in members)
            {
                var (row, column, _, _) = _leaves[leaf];
                write.Merge(cell, write.Cell(CellKey.Of(row.Id, column.Id)));
                if (slicer.Done(1)) await slicer.PauseAsync().ConfigureAwait(false);
            }
            write.Finish(cell);
        }
        _rows.Relabel(pass, 0, pass.Query.Rows.Count);
        _columns.Relabel(pass, pass.Query.Rows.Count, pass.Query.Columns.Count);
        _cells = write.Freeze();
        return _cube = _cube.WithCells(version, included, _cells, _rows.Freeze(), _columns.Freeze());
    }
}
