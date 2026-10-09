using System.Runtime.InteropServices;

namespace ExPivot.Engine;

// Private working indexes belong to one computation, never to a published report.
internal sealed class ComputationCube
{
    // Each leaf of the pass, by its number: the nodes it stands at, its records, and its first
    // record — where a fresh pass meets it, which orders the merges into its totals. A leaf with
    // no records has no nodes.
    private struct Leaf
    {
        public AxisNode? Row;
        public AxisNode? Column;
        public long Records;
        public int First;
    }

    private Leaf[] _leaves;
    // Each total's leaves, by first record: what a total that subtraction cannot keep exact is
    // merged again from.
    private readonly Dictionary<long, Members> _members = new(CellKey.Comparer);
    private readonly ComputationAxis.Working _rows;
    private readonly ComputationAxis.Working _columns;
    private ComputationCells _cells;
    private PivotCube _cube;
    // The leaves whose parts subtraction cannot keep exactly, and how many there are.
    private bool[] _nonAdditive;
    private int _nonAdditiveCount;
    public IReadOnlySet<int> ChangedRowNodes => _rows.Changed;
    public IReadOnlySet<int> RemovedRowNodes => _rows.Removed;
    public IReadOnlySet<int> ChangedColumnNodes => _columns.Changed;
    public IReadOnlySet<int> RemovedColumnNodes => _columns.Removed;
    public HashSet<int> ValueRows { get; } = [];
    public HashSet<int> ValueColumns { get; } = [];

    private ComputationCube(PivotCube cube, ComputationCells cells,
        ComputationAxis.Working rows, ComputationAxis.Working columns, int leaves)
    {
        _cube = cube; _cells = cells; _rows = rows; _columns = columns;
        // Room for the leaves updates add, an eighth more, before the array grows.
        _leaves = new Leaf[Math.Max(16, leaves + (leaves / 8))];
        _nonAdditive = new bool[_leaves.Length];
    }

    /// <summary>The computation of a cube the pass's answer made: its cells, trees and leaves
    /// taken as the cube holds them — <paramref name="leaves"/> are the nodes it made for each
    /// leaf of the answer, which are the pass's leaves that have records, in order.</summary>
    public static async ValueTask<ComputationCube> CreateAsync(PivotCube cube, PivotCube.LeafNodes leaves,
        AggregationPass pass, Slicer slicer)
    {
        var rowFields = pass.Query.Rows.Count;
        var columnFields = pass.Query.Columns.Count;
        var cells = cube.ShareCells();
        var rows = new ComputationAxis.Working(cube.RowNodes, rowFields);
        var columns = new ComputationAxis.Working(cube.ColumnNodes, columnFields);
        await pass.PrepareChainsAsync(slicer).ConfigureAwait(false);
        var result = new ComputationCube(cube, cells, rows, columns, pass.Leaves.Count);
        var records = pass.Leaves.Records;
        var answered = 0;
        await slicer.ForAsync(pass.Leaves.Count, (from, to) =>
        {
            for (var leaf = from; leaf < to; leaf++)
            {
                if (records[leaf] == 0)
                    continue;
                if (answered == leaves.Rows.Length)
                    throw new InvalidOperationException("The cube holds fewer leaves than the pass it was made from (ADR-0153).");
                result.Hold(pass, leaf, leaves.Rows[answered], leaves.Columns[answered], records[leaf]);
                answered++;
            }
        }, weight: 1 + rowFields + columnFields).ConfigureAwait(false);
        if (answered != leaves.Rows.Length)
            throw new InvalidOperationException("The cube holds more leaves than the pass it was made from (ADR-0153).");
        foreach (var members in result._members.Values)
            members.Seal();
        return result;
    }

    // A leaf of the initial cube, at the nodes the cube made for it.
    private void Hold(AggregationPass pass, int leaf, AxisNode row, AxisNode column, long records)
    {
        _rows.Use(pass, leaf, 0, row);
        _columns.Use(pass, leaf, pass.Query.Rows.Count, column);
        var first = pass.FirstRecord(leaf);
        _leaves[leaf] = new() { Row = row, Column = column, Records = records, First = first };
        var member = Members.Of(first, leaf);
        for (var r = row; r is not null; r = r.Parent)
        {
            for (var c = column; c is not null; c = c.Parent)
            {
                if (r != row || c != column)
                    MembersOf(CellKey.Of(r.Id, c.Id)).Append(member);
            }
        }
        var (parts, offset) = _cells.At(_cells.CellOf(CellKey.Of(row.Id, column.Id)));
        if (!Additive(parts, offset))
            SetNonAdditive(leaf, true);
    }

    private Members MembersOf(long total)
        => CollectionsMarshal.GetValueRefOrAddDefault(_members, total, out _) ??= new Members();

    private bool Held(int leaf, out Leaf held)
    {
        held = leaf < _leaves.Length ? _leaves[leaf] : default;
        return held.Row is not null;
    }

    private void Keep(int leaf, Leaf held)
    {
        if (leaf >= _leaves.Length)
        {
            var size = Math.Max(leaf + 1, _leaves.Length * 2);
            Array.Resize(ref _leaves, size);
            Array.Resize(ref _nonAdditive, size);
        }
        _leaves[leaf] = held;
    }

    private void SetNonAdditive(int leaf, bool nonAdditive)
    {
        if (leaf >= _nonAdditive.Length)
        {
            if (!nonAdditive)
                return;
            Array.Resize(ref _nonAdditive, Math.Max(leaf + 1, _nonAdditive.Length * 2));
        }
        if (_nonAdditive[leaf] == nonAdditive)
            return;
        _nonAdditive[leaf] = nonAdditive;
        _nonAdditiveCount += nonAdditive ? 1 : -1;
    }

    private (AxisNode Row, AxisNode Column, long Records, int First) AddLeaf(AggregationPass pass, int leaf)
    {
        var row = _rows.AddLeaf(pass, leaf, 0, pass.Query.Rows.Count);
        var column = _columns.AddLeaf(pass, leaf, pass.Query.Rows.Count, pass.Query.Columns.Count);
        var added = (row, column, pass.Leaves.Records[leaf], pass.FirstRecord(leaf));
        Keep(leaf, new() { Row = row, Column = column, Records = added.Item3, First = added.Item4 });
        foreach (var key in Totals(row, column))
            MembersOf(key).Add(Members.Of(added.Item4, leaf));
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
        var additive = _nonAdditiveCount == 0;
        for (var at = 0; at < changed.Length; at++)
            SetNonAdditive(changed[at], pass.Leaves.Records[changed[at]] != 0 && !Additive(values, at));
        additive &= _nonAdditiveCount == 0;
        var write = new ComputationCells.Writer(_cells);
        var totals = new HashSet<long>(CellKey.Comparer);
        long included = _cube.IncludedRecordCount;
        for (var at = 0; at < changed.Length; at++)
        {
            if (slicer.Done(1 + pass.Query.Rows.Count + pass.Query.Columns.Count))
                await slicer.PauseAsync().ConfigureAwait(false);
            var leaf = changed[at];
            var records = pass.Leaves.Records[leaf];
            var held = Held(leaf, out var old);
            included += records - (held ? old.Records : 0);
            if (!held && records == 0)
                continue;
            var (row, column, _, first) = held ? (old.Row!, old.Column!, old.Records, old.First) : AddLeaf(pass, leaf);
            for (var r = row; r is not null; r = r.Parent) ValueRows.Add(r.Id);
            for (var c = column; c is not null; c = c.Parent) ValueColumns.Add(c.Id);
            var key = CellKey.Of(row.Id, column.Id);
            var affected = Totals(row, column).ToArray();
            totals.UnionWith(affected);
            if (additive)
            {
                var oldParts = held ? _cells.At(_cells.CellOf(key)) : default;
                foreach (var total in affected)
                    write.Adjust(write.Cell(total), oldParts.Columns, oldParts.Offset,
                        records > 0 ? values : null, at);
            }
            if (records == 0)
            {
                write.Remove(key);
                _rows.RemoveLeaf(row); _columns.RemoveLeaf(column);
                foreach (var total in affected) _members[total].Remove(Members.Of(first, leaf));
                Keep(leaf, default);
            }
            else
            {
                var nextFirst = pass.FirstRecord(leaf);
                if (first != nextFirst)
                    foreach (var total in affected)
                    {
                        _members[total].Remove(Members.Of(first, leaf));
                        _members[total].Add(Members.Of(nextFirst, leaf));
                    }
                Keep(leaf, new() { Row = row, Column = column, Records = records, First = nextFirst });
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
            foreach (var leaf in members.Leaves())
            {
                var held = _leaves[leaf];
                write.Merge(cell, write.Cell(CellKey.Of(held.Row!.Id, held.Column!.Id)));
                if (slicer.Done(1)) await slicer.PauseAsync().ConfigureAwait(false);
            }
            write.Finish(cell);
        }
        _rows.Relabel(pass, 0, pass.Query.Rows.Count);
        _columns.Relabel(pass, pass.Query.Rows.Count, pass.Query.Columns.Count);
        _cells = write.Freeze();
        return _cube = _cube.WithCells(version, included, _cells, _rows.Freeze(), _columns.Freeze());
    }

    /// <summary>
    /// The leaves of one total, ordered by their first record, then their number: the order a
    /// fresh pass merges them in. A set of (first record, leaf) pairs: the pairs the computation
    /// began with, sorted once, and the few that updates added or took away since — so beginning a
    /// computation costs an array per total, not a node per leaf.
    /// </summary>
    private sealed class Members
    {
        private long[] _initial = [];
        private int _count;
        private HashSet<int>? _gone;     // positions in _initial no longer members
        private SortedSet<long>? _added; // members _initial does not hold

        public static long Of(int first, int leaf) => ((long)first << 32) | (uint)leaf;

        public int Count => _count - (_gone?.Count ?? 0) + (_added?.Count ?? 0);

        /// <summary>A pair the computation begins with; <see cref="Seal"/> orders them.</summary>
        public void Append(long member)
        {
            if (_count == _initial.Length)
                Array.Resize(ref _initial, Math.Max(4, _count * 2));
            _initial[_count++] = member;
        }

        public void Seal()
        {
            if (_count < _initial.Length)
                Array.Resize(ref _initial, _count);
            // The initial leaves come in their order already; a check costs less than a sort.
            for (var i = 1; i < _count; i++)
            {
                if (_initial[i - 1] > _initial[i])
                {
                    Array.Sort(_initial);
                    return;
                }
            }
        }

        public void Add(long member)
        {
            var at = Array.BinarySearch(_initial, 0, _count, member);
            if (at >= 0)
                _gone?.Remove(at);
            else
                (_added ??= []).Add(member);
        }

        public void Remove(long member)
        {
            if (_added?.Remove(member) == true)
                return;
            var at = Array.BinarySearch(_initial, 0, _count, member);
            if (at >= 0)
                (_gone ??= []).Add(at);
        }

        /// <summary>The members' leaves, in order.</summary>
        public IEnumerable<int> Leaves()
        {
            using var added = (_added ?? []).GetEnumerator();
            var more = added.MoveNext();
            for (var at = 0; at < _count; at++)
            {
                if (_gone?.Contains(at) == true)
                    continue;
                var member = _initial[at];
                for (; more && added.Current < member; more = added.MoveNext())
                    yield return (int)added.Current;
                yield return (int)member;
            }
            for (; more; more = added.MoveNext())
                yield return (int)added.Current;
        }
    }
}
