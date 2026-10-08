using System.Collections;

namespace ExPivot.Engine;

// Only the working index is mutable. A published Branch and its weighted child sequence
// reach immutable rows/branches, never this index or a preceding report version.
internal sealed class ComputationReport
{
    private sealed class Working(int id, int pendingFrom)
    {
        public int Id { get; } = id;
        public int PendingFrom { get; set; } = pendingFrom;
        public int[] Order { get; set; } = [];
        public Dictionary<int, int> Positions { get; set; } = [];
        public required Branch Published { get; set; }
    }

    private sealed class Branch(PivotReportRow[] before, Children? children, PivotReportRow[] after) : IReadOnlyList<PivotReportRow>
    {
        public PivotReportRow[] Before { get; } = before;
        public Children? Children { get; } = children;
        public PivotReportRow[] After { get; } = after;
        public int Count { get; } = checked(before.Length + (children?.Rows ?? 0) + after.Length);
        public PivotReportRow this[int index]
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
        public IEnumerator<PivotReportRow> GetEnumerator()
        {
            foreach (var row in Before) yield return row;
            if (Children is not null)
                foreach (var branch in Children.All())
                    foreach (var row in branch) yield return row;
            foreach (var row in After) yield return row;
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    // A balanced sequence of siblings with subtree row counts. Replacing one child's
    // report shares every sibling and copies only its logarithmic search path.
    private sealed class Children(Children? left, Branch value, Children? right)
    {
        public Children? Left { get; } = left;
        public Branch Value { get; } = value;
        public Children? Right { get; } = right;
        public int Count { get; } = 1 + (left?.Count ?? 0) + (right?.Count ?? 0);
        public int Rows { get; } = checked(value.Count + (left?.Rows ?? 0) + (right?.Rows ?? 0));
        public static Children? Of(IReadOnlyList<Branch> values, int start, int count)
        {
            if (count == 0) return null;
            var half = count / 2;
            return new(Of(values, start, half), values[start + half], Of(values, start + half + 1, count - half - 1));
        }
        public Children Set(int index, Branch value)
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
        public IEnumerable<Branch> All()
        {
            if (Left is not null) foreach (var item in Left.All()) yield return item;
            yield return Value;
            if (Right is not null) foreach (var item in Right.All()) yield return item;
        }
    }

    private readonly Dictionary<int, Working> _nodes = [];
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

    public async ValueTask<PivotReport> InitializeAsync(PivotReport report, Slicer slicer)
    {
        Prepare(report.Cube, report.Layout, report.Options, report.Lineage, slicer);
        // Seed the initial structure with the already laid-out rows, preserving identity.
        var original = new Dictionary<PivotRowKey, PivotReportRow>();
        foreach (var row in report.Rows)
        {
            original.Add(row.Key, row);
            if (slicer.Done(1)) await slicer.PauseAsync().ConfigureAwait(false);
        }
        var rows = await UpdateNodeAsync(report.Cube.RowRoot, 0, false, original).ConfigureAwait(false);
        _labels.Clear();
        return await _builder.WithRowsAsync(report, rows, false, slicer).ConfigureAwait(false);
    }

    private void Prepare(PivotCube cube, PivotLayout layout, PivotOptions options, ReportLineage lineage, Slicer slicer)
    {
        _cube = cube; _layout = layout; _slicer = slicer;
        _builder = new(cube, layout, options, lineage);
        _dirtyChildren.Clear(); _reorder.Clear(); _relabel.Clear(); _labels.Clear(); _removed.Clear();
        RowSequenceChanged = false;
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
        Prepare(cube, previous.Layout, previous.Options, previous.Lineage, slicer);
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
            foreach (var id in _nodes.Keys)
            {
                var node = cube.RowNode(id);
                if (node.Level + 1 < _layout.Rows.Count && _layout.Rows[node.Level + 1].Sort.ByValue is not null)
                { _reorder.Add(id); Dirty(node); }
            }
        var root = await UpdateNodeAsync(cube.RowRoot, 0, false).ConfigureAwait(false);
        var columnsChanged = computation.ChangedColumnNodes.Count > 0 || computation.RemovedColumnNodes.Count > 0
            || (computation.ValueColumns.Count > 0 && _layout.Columns.Any(p => p.Sort.ByValue is not null));
        return await _builder.WithRowsAsync(previous, root, columnsChanged, slicer).ConfigureAwait(false);
    }

    private async ValueTask<Branch> UpdateNodeAsync(AxisNode node, int pendingFrom, bool ancestorLabelChanged,
        Dictionary<PivotRowKey, PivotReportRow>? original = null)
    {
        var exists = _nodes.TryGetValue(node.Id, out var held);
        var boundary = exists && held!.PendingFrom != pendingFrom && _layout.Form == PivotReportForm.Tabular;
        var labelsChanged = !exists || boundary || _relabel.Contains(node.Id) || ancestorLabelChanged;
        var needsOrder = !exists || _reorder.Contains(node.Id);
        var hasDirtyChildren = _dirtyChildren.TryGetValue(node.Id, out var dirtyChildren);
        if (exists && !labelsChanged && !needsOrder && !hasDirtyChildren) return held!.Published;
        var own = _builder.RowsOf(node, pendingFrom);
        var before = Keep(own.Before, held?.Published.Before, original);
        var after = Keep(own.After, held?.Published.After, original);
        var forceChildren = (ancestorLabelChanged || _relabel.Contains(node.Id)) && _layout.RepeatItemLabels;
        var firstLabelChanged = (ancestorLabelChanged || _relabel.Contains(node.Id))
            && _layout.Form == PivotReportForm.Tabular && pendingFrom <= node.Level;
        var children = held?.Published.Children;
        var order = held?.Order ?? [];
        if (own.Descend)
        {
            var ordered = needsOrder ? await _builder.OrderedChildrenAsync(node, _slicer).ConfigureAwait(false) : null;
            var next = ordered?.Select(child => child.Id).ToArray() ?? order;
            var reordered = !order.SequenceEqual(next);
            if (!exists || reordered)
            {
                RowSequenceChanged = true;
                var surviving = next.ToHashSet();
                foreach (var old in order)
                    if (!surviving.Contains(old)) Remove(old);
                var parts = new Branch[next.Length];
                for (var i = 0; i < next.Length; i++)
                {
                    parts[i] = await UpdateNodeAsync(_cube.RowNode(next[i]), i == 0 ? pendingFrom : node.Level + 1,
                        forceChildren || (i == 0 && firstLabelChanged), original).ConfigureAwait(false);
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
                    if (!held!.Positions.TryGetValue(id, out var position)) continue;
                    var child = await UpdateNodeAsync(_cube.RowNode(id), position == 0 ? pendingFrom : node.Level + 1,
                        forceChildren || (position == 0 && firstLabelChanged), original).ConfigureAwait(false);
                    children = children!.Set(position, child);
                }
            }
        }
        else if (order.Length > 0)
        {
            foreach (var old in order) Remove(old);
            children = null; order = [];
            RowSequenceChanged = true;
        }
        var published = exists && ReferenceEquals(before, held!.Published.Before) && ReferenceEquals(after, held.Published.After)
            && ReferenceEquals(children, held.Published.Children) ? held.Published : new Branch(before, children, after);
        if (!exists)
            _nodes[node.Id] = held = new(node.Id, pendingFrom) { Published = published };
        if (!ReferenceEquals(order, held!.Order))
        {
            held.Order = order;
            held.Positions = order.Select((id, index) => (id, index)).ToDictionary(p => p.id, p => p.index);
        }
        held.PendingFrom = pendingFrom;
        held.Published = published;
        if (_slicer.Done(1 + before.Length + after.Length)) await _slicer.PauseAsync().ConfigureAwait(false);
        return published;
    }

    private PivotReportRow[] Keep(PivotReportRow[] made, PivotReportRow[]? held,
        Dictionary<PivotRowKey, PivotReportRow>? original)
    {
        var identical = held is not null && held.Length == made.Length;
        for (var i = 0; i < made.Length; i++)
        {
            var prior = original?.GetValueOrDefault(made[i].Key)
                ?? held?.FirstOrDefault(row => row.Key.Equals(made[i].Key));
            if (prior is not null && prior.CarriesValues == made[i].CarriesValues && prior.Labels.SequenceEqual(made[i].Labels))
                made[i] = prior;
            else _labels.Add(made[i]);
            identical &= held is not null && i < held.Length && ReferenceEquals(held[i], made[i]);
        }
        return identical ? held! : made;
    }

    private void Remove(int id)
    {
        var held = _nodes[id];
        foreach (var row in held.Published.Before) _removed.Add(row.Key);
        foreach (var row in held.Published.After) _removed.Add(row.Key);
        foreach (var child in held.Order) Remove(child);
        _nodes.Remove(id);
    }
}
