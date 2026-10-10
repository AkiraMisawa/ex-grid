using System.Collections;

namespace ExPivot.Engine;

// Only the working index is mutable. A published part and its weighted child sequence reach
// immutable rows and parts, never this index or a preceding report version.
//
// The first report of a layout is laid out as one array of rows, as a report walk lays it out:
// its structure is the base, an entry per node saying where its rows lie. An update lays a node
// out again only where the data asks it to; the node is then split out of the part it lies in —
// its own rows, and a part for each child — and the versions share every part an update did not
// touch.
internal sealed class ComputationReport
{
    // A node laid out, by its id: none while Laid is false.
    private struct Working
    {
        public bool Laid;
        public int PendingFrom;
        public int[] Order;
        // Where the node stands among its parent's children, as the parent last ordered them.
        public int Position;
        // Its subtree's rows: its own before its children's, then its own after them, and all of them.
        public int Before;
        public int After;
        public int Rows;
        // The part that is its subtree; null while that lies within an ancestor's first part.
        public Part? Published;
    }

    private abstract class Part : IReadOnlyList<PivotReportRow>
    {
        public abstract int Count { get; }
        public abstract PivotReportRow this[int index] { get; }
        public abstract IEnumerator<PivotReportRow> GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    // A subtree as it was first laid out: its rows in order, in an array of its own.
    private sealed class FirstLayout(PivotReportRow[] rows) : Part
    {
        public PivotReportRow[] Rows { get; } = rows;
        public override int Count => Rows.Length;
        public override PivotReportRow this[int index] => Rows[index];
        public override IEnumerator<PivotReportRow> GetEnumerator() => ((IEnumerable<PivotReportRow>)Rows).GetEnumerator();
    }

    // A subtree an update laid out: its own rows, and its children's parts between them.
    private sealed class Branch(PivotReportRow[] before, Children? children, PivotReportRow[] after) : Part
    {
        public PivotReportRow[] Before { get; } = before;
        public Children? Children { get; } = children;
        public PivotReportRow[] After { get; } = after;
        public override int Count { get; } = checked(before.Length + (children?.Rows ?? 0) + after.Length);
        public override PivotReportRow this[int index]
        {
            get
            {
                if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
                if (index < Before.Length) return Before[index];
                index -= Before.Length;
                var middle = Children?.Rows ?? 0;
                return index < middle ? Children!.RowAt(index) : After[index - middle];
            }
        }
        public override IEnumerator<PivotReportRow> GetEnumerator()
        {
            foreach (var row in Before) yield return row;
            if (Children is not null)
                foreach (var part in Children.All())
                    foreach (var row in part) yield return row;
            foreach (var row in After) yield return row;
        }
    }

    // A balanced sequence of siblings with subtree row counts. Replacing one child's
    // report shares every sibling and copies only its logarithmic search path.
    private sealed class Children(Children? left, Part value, Children? right)
    {
        public Children? Left { get; } = left;
        public Part Value { get; } = value;
        public Children? Right { get; } = right;
        public int Count { get; } = 1 + (left?.Count ?? 0) + (right?.Count ?? 0);
        public int Rows { get; } = checked(value.Count + (left?.Rows ?? 0) + (right?.Rows ?? 0));
        public static Children? Of(IReadOnlyList<Part> values, int start, int count)
        {
            if (count == 0) return null;
            var half = count / 2;
            return new(Of(values, start, half), values[start + half], Of(values, start + half + 1, count - half - 1));
        }
        public Children Set(int index, Part value)
        {
            var before = Left?.Count ?? 0;
            if (index < before) return new(Left!.Set(index, value), Value, Right);
            if (index > before) return new(Left, Value, Right!.Set(index - before - 1, value));
            return ReferenceEquals(Value, value) ? this : new(Left, value, Right);
        }
        public PivotReportRow RowAt(int index)
        {
            var before = Left?.Rows ?? 0;
            if (index < before) return Left!.RowAt(index);
            index -= before;
            return index < Value.Count ? Value[index] : Right!.RowAt(index - Value.Count);
        }
        public IEnumerable<Part> All()
        {
            if (Left is not null) foreach (var item in Left.All()) yield return item;
            yield return Value;
            if (Right is not null) foreach (var item in Right.All()) yield return item;
        }
    }

    // By node id, which the cube hands out densely: an array, not an object and an entry a node.
    private Working[] _nodes = [];
    private readonly Dictionary<int, HashSet<int>> _dirtyChildren = [];
    private readonly HashSet<int> _reorder = [];
    private readonly HashSet<int> _relabel = [];
    private readonly List<PivotReportRow> _labels = [];
    private readonly List<PivotRowKey> _removed = [];
    private PivotCube _cube = null!;
    private ReportBuilder _builder = null!;
    private Slicer _slicer = null!;
    private PivotLayout _layout = null!;
    public IReadOnlyList<PivotReportRow> LabelChanges => _labels;
    public IReadOnlyList<PivotRowKey> RemovedRows => _removed;
    public bool RowSequenceChanged { get; private set; }

    /// <summary>Lays out the first report of a layout, and the structure its updates follow, in
    /// one walk of the row tree: each node's own rows and its children in their order — the rows a
    /// node lays out when an update lays it out again (ADR-0153). <paramref name="builder"/> holds
    /// the cube, layout and options, checked already.</summary>
    public async ValueTask<PivotReport> InitializeAsync(ReportBuilder builder, Slicer slicer)
    {
        Prepare(builder, slicer);
        var rows = new List<PivotReportRow>();
        await LayOutAsync(_cube.RowRoot, 0, 0, rows).ConfigureAwait(false);
        var first = new FirstLayout([.. rows]);
        _nodes[_cube.RowRoot.Id].Published = first;
        RowSequenceChanged = true;
        return await _builder.BuildAroundAsync(first, slicer).ConfigureAwait(false);
    }

    private void Prepare(ReportBuilder builder, Slicer slicer)
    {
        _builder = builder;
        _cube = builder.Cube; _layout = builder.Layout; _slicer = slicer;
        // Room for the nodes updates add, an eighth more, before the array grows.
        if (_nodes.Length < _cube.RowNodes.Length)
            Array.Resize(ref _nodes, _cube.RowNodes.Length + (_cube.RowNodes.Length / 8));
        _dirtyChildren.Clear(); _reorder.Clear(); _relabel.Clear(); _labels.Clear(); _removed.Clear();
        RowSequenceChanged = false;
    }

    // A node's subtree as first laid out, appended to `rows`: its own rows before its children's,
    // its children in their order, then its own rows after them.
    private async ValueTask LayOutAsync(AxisNode node, int position, int pendingFrom, List<PivotReportRow> rows)
    {
        var start = rows.Count;
        var after = _builder.RowsInto(node, pendingFrom, rows, out var descend);
        var before = rows.Count - start;
        int[] order = [];
        if (descend)
        {
            var ordered = await _builder.OrderedChildrenAsync(node, _slicer).ConfigureAwait(false);
            order = new int[ordered.Count];
            for (var i = 0; i < order.Length; i++)
            {
                order[i] = ordered[i].Id;
                await LayOutAsync(ordered[i], i, i == 0 ? pendingFrom : node.Level + 1, rows).ConfigureAwait(false);
                if (_slicer.Done(1)) await _slicer.PauseAsync().ConfigureAwait(false);
            }
        }
        rows.AddRange(after);
        Ensure(node.Id);
        _nodes[node.Id] = new()
        {
            Laid = true, Order = order, Position = position, PendingFrom = pendingFrom,
            Before = before, After = after.Length, Rows = rows.Count - start,
        };
        if (_slicer.Done(1 + before + after.Length)) await _slicer.PauseAsync().ConfigureAwait(false);
    }

    private void Dirty(AxisNode node)
    {
        for (var current = node; current.Parent is { } parent; current = parent)
        {
            if (!_dirtyChildren.TryGetValue(parent.Id, out var children))
                _dirtyChildren[parent.Id] = children = [];
            children.Add(current.Id);
        }
    }

    public async ValueTask<PivotReport> UpdateAsync(PivotReport previous, PivotCube cube,
        ComputationCube computation, Slicer slicer)
    {
        Prepare(new ReportBuilder(cube, previous.Layout, previous.Options, previous.Lineage), slicer);
        foreach (var id in computation.ChangedRowNodes)
        {
            var node = cube.RowNode(id);
            _relabel.Add(id);
            _reorder.Add(id);
            if (node.Parent is { } parent) _reorder.Add(parent.Id);
            Dirty(node);
        }
        // A value sort depends on the shown number. Column/grand percentages may change
        // every sibling order; otherwise only ancestors of affected leaves need an order.
        foreach (var id in computation.ValueRows)
        {
            var node = cube.RowNode(id);
            if (node.Parent is { } parent && _layout.Rows[node.Level].Sort.ByValue is not null)
            { _reorder.Add(parent.Id); Dirty(parent); }
        }
        if (computation.ValueRows.Count > 0 && _layout.Rows.Any(p => p.Sort.ByValue is not null) && _layout.Values.Any(v =>
            v.ShowValuesAs is PivotShowValuesAs.PercentOfGrandTotal or PivotShowValuesAs.PercentOfColumnTotal))
            for (var id = 0; id < _nodes.Length; id++)
            {
                if (!_nodes[id].Laid)
                    continue;
                var node = cube.RowNode(id);
                if (node.Level + 1 < _layout.Rows.Count && _layout.Rows[node.Level + 1].Sort.ByValue is not null)
                { _reorder.Add(id); Dirty(node); }
            }
        var root = await UpdateNodeAsync(cube.RowRoot, 0, 0, false).ConfigureAwait(false);
        var columnsChanged = computation.ChangedColumnNodes.Count > 0 || computation.RemovedColumnNodes.Count > 0
            || (computation.ValueColumns.Count > 0 && _layout.Columns.Any(p => p.Sort.ByValue is not null));
        return await _builder.WithRowsAsync(previous, root, columnsChanged, slicer).ConfigureAwait(false);
    }

    private async ValueTask<Part> UpdateNodeAsync(AxisNode node, int position, int pendingFrom, bool ancestorLabelChanged)
    {
        var exists = Held(node.Id, out var held);
        var boundary = exists && held.PendingFrom != pendingFrom && _layout.Form == PivotReportForm.Tabular;
        var labelsChanged = !exists || boundary || _relabel.Contains(node.Id) || ancestorLabelChanged;
        var needsOrder = !exists || _reorder.Contains(node.Id);
        var hasDirtyChildren = _dirtyChildren.TryGetValue(node.Id, out var dirtyChildren);
        if (exists && !labelsChanged && !needsOrder && !hasDirtyChildren)
        {
            _nodes[node.Id].Position = position;
            return held.Published!;
        }
        // Laid out again: split out of the part it was first laid out in.
        var branch = exists ? Split(node.Id) : null;
        var own = _builder.RowsOf(node, pendingFrom);
        var before = Keep(own.Before, branch?.Before);
        var after = Keep(own.After, branch?.After);
        var forceChildren = (ancestorLabelChanged || _relabel.Contains(node.Id)) && _layout.RepeatItemLabels;
        var firstLabelChanged = (ancestorLabelChanged || _relabel.Contains(node.Id))
            && _layout.Form == PivotReportForm.Tabular && pendingFrom <= node.Level;
        var children = branch?.Children;
        var order = exists ? held.Order : [];
        if (own.Descend)
        {
            var ordered = needsOrder ? await _builder.OrderedChildrenAsync(node, _slicer).ConfigureAwait(false) : null;
            var next = order;
            if (ordered is not null)
            {
                next = new int[ordered.Count];
                for (var i = 0; i < next.Length; i++)
                    next[i] = ordered[i].Id;
            }
            var reordered = !order.SequenceEqual(next);
            if (!exists || reordered)
            {
                RowSequenceChanged = true;
                if (order.Length > 0)
                {
                    var surviving = next.ToHashSet();
                    foreach (var old in order)
                        if (!surviving.Contains(old)) Remove(old);
                }
                var parts = new Part[next.Length];
                for (var i = 0; i < next.Length; i++)
                {
                    // The children as ordered: each the node the cube holds now.
                    parts[i] = await UpdateNodeAsync(ordered![i], i, i == 0 ? pendingFrom : node.Level + 1,
                        forceChildren || (i == 0 && firstLabelChanged)).ConfigureAwait(false);
                    if (_slicer.Done(1)) await _slicer.PauseAsync().ConfigureAwait(false);
                }
                children = Children.Of(parts, 0, parts.Length);
                order = next;
            }
            else
            {
                IEnumerable<int> affected = forceChildren ? order : dirtyChildren ?? Enumerable.Empty<int>();
                if ((boundary || firstLabelChanged) && order.Length > 0) affected = affected.Append(order[0]).Distinct();
                foreach (var id in affected)
                {
                    // A child the order holds: one it no longer holds is not laid out.
                    if (!Held(id, out var child) || child.Position >= order.Length || order[child.Position] != id)
                        continue;
                    var at = child.Position;
                    var made = await UpdateNodeAsync(_cube.RowNode(id), at, at == 0 ? pendingFrom : node.Level + 1,
                        forceChildren || (at == 0 && firstLabelChanged)).ConfigureAwait(false);
                    children = children!.Set(at, made);
                }
            }
        }
        else if (order.Length > 0)
        {
            foreach (var old in order) Remove(old);
            children = null; order = [];
            RowSequenceChanged = true;
        }
        var published = branch is not null && ReferenceEquals(before, branch.Before) && ReferenceEquals(after, branch.After)
            && ReferenceEquals(children, branch.Children) ? branch : new Branch(before, children, after);
        Ensure(node.Id);
        _nodes[node.Id] = new()
        {
            Laid = true, Order = order, Position = position, PendingFrom = pendingFrom,
            Before = before.Length, After = after.Length, Rows = published.Count, Published = published,
        };
        if (_slicer.Done(1 + before.Length + after.Length)) await _slicer.PauseAsync().ConfigureAwait(false);
        return published;
    }

    private bool Held(int id, out Working held)
    {
        held = id < _nodes.Length ? _nodes[id] : default;
        return held.Laid;
    }

    private void Ensure(int id)
    {
        if (id >= _nodes.Length)
            Array.Resize(ref _nodes, Math.Max(id + 1, _nodes.Length * 2));
    }

    // A node first laid out lies in one part with its subtree. Laid out again, it is split: its own
    // rows, and a part for each child holding the child's subtree — copied out, so that a later
    // version holds only the rows it shows.
    private Branch Split(int id)
    {
        var held = _nodes[id];
        if (held.Published is Branch branch)
            return branch;
        var rows = ((FirstLayout)held.Published!).Rows;
        Children? children = null;
        if (held.Order.Length > 0)
        {
            var parts = new Part[held.Order.Length];
            var at = held.Before;
            for (var i = 0; i < parts.Length; i++)
            {
                ref var child = ref _nodes[held.Order[i]];
                parts[i] = child.Published = new FirstLayout(rows.AsSpan(at, child.Rows).ToArray());
                at += child.Rows;
            }
            children = Children.Of(parts, 0, parts.Length);
        }
        var made = new Branch(rows[..held.Before], children, rows[(rows.Length - held.After)..]);
        _nodes[id].Published = made;
        return made;
    }

    private PivotReportRow[] Keep(PivotReportRow[] made, PivotReportRow[]? held)
    {
        if (held is null)
        {
            // A node laid out for the first time: its rows are new.
            _labels.AddRange(made);
            return made;
        }
        var identical = held.Length == made.Length;
        for (var i = 0; i < made.Length; i++)
        {
            var prior = held.FirstOrDefault(row => row.Key.Equals(made[i].Key));
            if (prior is not null && prior.CarriesValues == made[i].CarriesValues && prior.Labels.SequenceEqual(made[i].Labels))
                made[i] = prior;
            else _labels.Add(made[i]);
            identical &= i < held.Length && ReferenceEquals(held[i], made[i]);
        }
        return identical ? held : made;
    }

    private void Remove(int id)
    {
        var held = _nodes[id];
        if (held.Published is FirstLayout first)
        {
            RemoveFirst(id, first.Rows, 0);
            return;
        }
        var branch = (Branch)held.Published!;
        foreach (var row in branch.Before) _removed.Add(row.Key);
        foreach (var row in branch.After) _removed.Add(row.Key);
        foreach (var child in held.Order) Remove(child);
        _nodes[id] = default;
    }

    // A subtree as first laid out, within `rows` from `start`: its own rows, then its children's.
    private void RemoveFirst(int id, PivotReportRow[] rows, int start)
    {
        var held = _nodes[id];
        for (var i = start; i < start + held.Before; i++) _removed.Add(rows[i].Key);
        for (var i = start + held.Rows - held.After; i < start + held.Rows; i++) _removed.Add(rows[i].Key);
        var at = start + held.Before;
        foreach (var child in held.Order)
        {
            var count = _nodes[child].Rows;
            RemoveFirst(child, rows, at);
            at += count;
        }
        _nodes[id] = default;
    }
}
