using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace ExPivot.Engine;

// One axis of a published computation: the initial cube's tree, by node id, under an immutable
// overlay of the nodes and child lists the updates since wrote. A version shares everything an
// update did not write; it reaches no preceding version, and nothing writes the initial tree.
internal sealed class ComputationAxis(AxisNode[] initial, ImmutableDictionary<int, AxisNode> nodes,
    ImmutableDictionary<int, ImmutableList<int>> children)
{
    public AxisNode Node(int id) => nodes.TryGetValue(id, out var node) ? node : initial[id];

    public IReadOnlyList<AxisNode> ChildrenOf(int node)
    {
        if (children.TryGetValue(node, out var ids))
            return new ChildRows(this, ids);
        if (node >= initial.Length)
            return [];
        // A relabelled child is read as the overlay has it.
        var list = initial[node].Children;
        return nodes.IsEmpty ? list : new InitialRows(this, list);
    }

    private sealed class ChildRows(ComputationAxis axis, ImmutableList<int> ids) : IReadOnlyList<AxisNode>
    {
        public int Count => ids.Count;
        public AxisNode this[int index] => axis.Node(ids[index]);
        public IEnumerator<AxisNode> GetEnumerator() { foreach (var id in ids) yield return axis.Node(id); }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class InitialRows(ComputationAxis axis, List<AxisNode> list) : IReadOnlyList<AxisNode>
    {
        public int Count => list.Count;
        public AxisNode this[int index] => axis.Node(list[index].Id);
        public IEnumerator<AxisNode> GetEnumerator() { foreach (var node in list) yield return axis.Node(node.Id); }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// The working index of one axis, private to its computation. It starts as the initial cube's
    /// tree, which it never writes: a node an update makes, relabels or gives other children is
    /// written to the overlay it publishes. What only an update reads — each parent's children by
    /// Item, and the nodes of each Item — is made the first time an update reads it, so the first
    /// report costs what its own cube does.
    /// </summary>
    public sealed class Working
    {
        private readonly AxisNode[] _initial;
        private ImmutableDictionary<int, AxisNode> _nodes = ImmutableDictionary<int, AxisNode>.Empty;
        private ImmutableDictionary<int, ImmutableList<int>> _children = ImmutableDictionary<int, ImmutableList<int>>.Empty;
        // Each parent's children by Item: made the first time an update looks for a child under it.
        private readonly Dictionary<int, Dictionary<ItemKey, int>> _paths = [];
        // How many leaves each node is on the path of, by id; the root is not counted.
        private int[] _users;
        private int _next;
        // The label every node of an Item carries, by level and the pass's Item: an Item's nodes
        // are labelled alike, and relabelled together. And the nodes of each Item, made the first
        // time an Item's label changes.
        private readonly PivotItemKey?[][] _labels;
        private Dictionary<(int Level, ItemKey Item), List<int>>? _byItem;

        public Working(AxisNode[] initial, int levels)
        {
            _initial = initial;
            _next = initial.Length;
            _users = new int[Math.Max(16, initial.Length + (initial.Length / 8))];
            _labels = new PivotItemKey?[levels][];
            for (var level = 0; level < levels; level++)
                _labels[level] = [];
        }

        public HashSet<int> Changed { get; } = [];
        public HashSet<int> Removed { get; } = [];
        public AxisNode Root => Node(0);
        public ComputationAxis Freeze() => new(_initial, _nodes, _children);
        public void Begin() { Changed.Clear(); Removed.Clear(); }

        private AxisNode Node(int id) => _nodes.TryGetValue(id, out var node) ? node : _initial[id];

        /// <summary>A leaf the initial cube holds, at the node the cube made for it: every node on
        /// its path counts it, and each Item on the path is labelled as the cube labelled it.</summary>
        public void Use(AggregationPass pass, int leaf, int start, AxisNode node)
        {
            for (var at = node; at.Parent is not null; at = at.Parent)
            {
                _users[at.Id]++;
                LabelOf(at.Level, pass.Leaves.ItemOfLeaf[start + at.Level][leaf]) ??= at.Item!.PublicKey;
            }
        }

        public AxisNode AddLeaf(AggregationPass pass, int leaf, int start, int count)
        {
            var parent = Root;
            for (var level = 0; level < count; level++)
            {
                var space = pass.Axes[start + level];
                var item = pass.Leaves.ItemOfLeaf[start + level][leaf];
                var paths = PathsUnder(parent.Id);
                var key = space.KeyOf(item);
                if (!paths.TryGetValue(key, out var id))
                {
                    id = _next++;
                    var label = space.PublicKeyOf(item);
                    var made = new AxisNode(id, level, parent, ItemRef.Of(label));
                    _nodes = _nodes.Add(id, made);
                    paths[key] = id;
                    if (id >= _users.Length)
                        Array.Resize(ref _users, Math.Max(id + 1, _users.Length * 2));
                    // The Item's first node sets the label its nodes carry. A node made with a
                    // newer spelling than the others carry came with a batch that touched its
                    // Item, so Relabel brings the others to it.
                    LabelOf(level, item) ??= label;
                    if (_byItem is not null)
                        NodesOf(_byItem, level, key).Add(id);
                }
                if (_users[id]++ == 0)
                {
                    var siblings = ChildIds(parent.Id);
                    if (!siblings.Contains(id))
                        _children = _children.SetItem(parent.Id, siblings.Add(id));
                    Changed.Add(id);
                    Changed.Add(parent.Id);
                }
                parent = Node(id);
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
                _children = _children.SetItem(parent, ChildIds(parent).Remove(node.Id));
                Removed.Add(node.Id);
                Changed.Add(parent);
            }
        }

        /// <summary>Relabels the nodes of each Item a batch touched whose spelling changed: a text
        /// Item is labelled by its first spelling among the records present (ADR-0060).</summary>
        public void Relabel(AggregationPass pass, int start, int count)
        {
            for (var level = 0; level < count; level++)
            {
                var space = pass.Axes[start + level];
                var labels = _labels[level];
                foreach (var item in space.ChangedItems)
                {
                    // An Item no node carries has no label to change.
                    if (item >= labels.Length || labels[item] is not { } label)
                        continue;
                    var key = space.PublicKeyOf(item);
                    if (label.Kind == key.Kind && label.Value == key.Value)
                        continue;
                    labels[item] = key;
                    if (!ByItem().TryGetValue((level, space.KeyOf(item)), out var ids))
                        continue;
                    foreach (var id in ids)
                    {
                        var before = Node(id);
                        if (before.Item!.PublicKey.Kind == key.Kind && before.Item.PublicKey.Value == key.Value)
                            continue;
                        _nodes = _nodes.SetItem(id, new AxisNode(id, before.Level, before.Parent, ItemRef.Of(key)));
                        if (_users[id] > 0)
                            Changed.Add(id);
                    }
                }
            }
        }

        private ref PivotItemKey? LabelOf(int level, int item)
        {
            ref var labels = ref _labels[level];
            if (item >= labels.Length)
                Array.Resize(ref labels, Math.Max(item + 1, Math.Max(16, labels.Length * 2)));
            return ref labels[item];
        }

        private ImmutableList<int> ChildIds(int parent)
        {
            if (_children.TryGetValue(parent, out var ids))
                return ids;
            if (parent >= _initial.Length)
                return ImmutableList<int>.Empty;
            var builder = ImmutableList.CreateBuilder<int>();
            foreach (var child in _initial[parent].Children)
                builder.Add(child.Id);
            return builder.ToImmutable();
        }

        private Dictionary<ItemKey, int> PathsUnder(int parent)
        {
            if (_paths.TryGetValue(parent, out var paths))
                return paths;
            paths = [];
            if (parent < _initial.Length)
            {
                foreach (var child in _initial[parent].Children)
                    paths[child.Item!.Key] = child.Id;
            }
            _paths[parent] = paths;
            return paths;
        }

        // Every node but the root, by its level and Item: the initial cube's and the updates'.
        private Dictionary<(int Level, ItemKey Item), List<int>> ByItem()
        {
            if (_byItem is { } made)
                return made;
            var index = new Dictionary<(int Level, ItemKey Item), List<int>>();
            for (var id = 0; id < _next; id++)
            {
                var node = Node(id);
                if (node.Item is { } item)
                    NodesOf(index, node.Level, item.Key).Add(id);
            }
            return _byItem = index;
        }

        private static List<int> NodesOf(Dictionary<(int Level, ItemKey Item), List<int>> index, int level, ItemKey key)
            => CollectionsMarshal.GetValueRefOrAddDefault(index, (level, key), out _) ??= [];
    }
}
