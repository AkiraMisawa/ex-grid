using System.Collections.Immutable;

namespace ExPivot.Engine;

internal sealed record ComputationAxis(ImmutableDictionary<int, AxisNode> Nodes,
    ImmutableDictionary<int, ImmutableList<int>> Children)
{
    public IReadOnlyList<AxisNode> ChildrenOf(int node)
        => Children.TryGetValue(node, out var ids) ? new ChildRows(Nodes, ids) : [];

    private sealed class ChildRows(ImmutableDictionary<int, AxisNode> nodes, ImmutableList<int> ids) : IReadOnlyList<AxisNode>
    {
        public int Count => ids.Count;
        public AxisNode this[int index] => nodes[ids[index]];
        public IEnumerator<AxisNode> GetEnumerator() { foreach (var id in ids) yield return nodes[id]; }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public sealed class Working
    {
        private ImmutableDictionary<int, AxisNode> _nodes = ImmutableDictionary<int, AxisNode>.Empty;
        private ImmutableDictionary<int, ImmutableList<int>> _children = ImmutableDictionary<int, ImmutableList<int>>.Empty;
        private readonly Dictionary<(int Parent, ItemKey Key), int> _paths = [];
        private readonly Dictionary<(int Level, int Item), HashSet<int>> _byItem = [];
        private readonly Dictionary<int, int> _users = [];
        private int _next;
        public HashSet<int> Changed { get; } = [];
        public HashSet<int> Removed { get; } = [];
        private Working() { }
        public static async ValueTask<Working> CreateAsync(AxisNode root, Slicer slicer)
        {
            var result = new Working();
            var nodes = result._nodes.ToBuilder();
            var children = result._children.ToBuilder();
            var pending = new Stack<AxisNode>();
            pending.Push(root);
            while (pending.TryPop(out var node))
            {
                nodes.Add(node.Id, node);
                result._next = Math.Max(result._next, node.Id + 1);
                var ids = ImmutableList.CreateBuilder<int>();
                foreach (var child in node.Children)
                {
                    ids.Add(child.Id);
                    result._paths[(node.Id, child.Item!.Key)] = child.Id;
                    pending.Push(child);
                    if (slicer.Done(1)) await slicer.PauseAsync().ConfigureAwait(false);
                }
                children.Add(node.Id, ids.ToImmutable());
                if (slicer.Done(1)) await slicer.PauseAsync().ConfigureAwait(false);
            }
            result._nodes = nodes.ToImmutable();
            result._children = children.ToImmutable();
            return result;
        }
        public AxisNode Root => _nodes[0];
        public ComputationAxis Freeze() => new(_nodes, _children);
        public void Begin() { Changed.Clear(); Removed.Clear(); }

        public AxisNode AddLeaf(AggregationPass pass, int leaf, int start, int count, bool initial = false)
        {
            var parent = Root;
            for (var level = 0; level < count; level++)
            {
                var space = pass.Axes[start + level];
                var item = pass.Leaves.ItemOfLeaf[start + level][leaf];
                var path = (parent.Id, space.KeyOf(item));
                if (!_paths.TryGetValue(path, out var id))
                {
                    id = _next++;
                    var made = new AxisNode(id, level, parent, ItemRef.Of(space.PublicKeyOf(item)));
                    _nodes = _nodes.Add(id, made);
                    _children = _children.Add(id, ImmutableList<int>.Empty);
                    _paths[path] = id;
                }
                if (!_byItem.TryGetValue((start + level, item), out var matching))
                    _byItem[(start + level, item)] = matching = [];
                matching.Add(id);
                var users = _users.GetValueOrDefault(id);
                _users[id] = users + 1;
                if (users == 0 && !initial)
                {
                    if (!_children[parent.Id].Contains(id))
                        _children = _children.SetItem(parent.Id, _children[parent.Id].Add(id));
                    Changed.Add(id);
                    Changed.Add(parent.Id);
                }
                parent = _nodes[id];
            }
            return parent;
        }

        public void RemoveLeaf(AxisNode leaf)
        {
            for (var node = leaf; node.Parent is not null; node = node.Parent)
            {
                if (--_users[node.Id] != 0)
                    continue;
                var parent = node.Parent.Id;
                _children = _children.SetItem(parent, _children[parent].Remove(node.Id));
                Removed.Add(node.Id);
                Changed.Add(parent);
            }
        }

        public void Relabel(AggregationPass pass, int start, int count)
        {
            for (var level = start; level < start + count; level++)
            foreach (var item in pass.Axes[level].ChangedItems)
            {
                if (!_byItem.TryGetValue((level, item), out var ids))
                    continue;
                var key = pass.Axes[level].PublicKeyOf(item);
                foreach (var id in ids)
                {
                    var before = _nodes[id];
                    if (before.Item!.PublicKey.Kind == key.Kind && before.Item.PublicKey.Value == key.Value)
                        continue;
                    _nodes = _nodes.SetItem(id, new AxisNode(id, before.Level, before.Parent, ItemRef.Of(key)));
                    if (_users.GetValueOrDefault(id) > 0)
                        Changed.Add(id);
                }
            }
        }
    }
}
