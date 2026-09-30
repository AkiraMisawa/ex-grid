using System.Runtime.InteropServices;

namespace ExPivot.Engine;

/// <summary>
/// What the engine aggregated from one snapshot of Source Records under one set of placed
/// fields (ADR-0059): the Items of every placed field, the tree of row Items and the tree of
/// column Items the included records form, and every cell where they cross. It lays nothing
/// out. Collapse, order, the form, the totals, Show Values As, formats and captions are laid
/// out from it again without a pass over the records (<see cref="PivotEngine.Report"/>);
/// <see cref="Holds"/> says whether a layout can be.
/// </summary>
public sealed class PivotCube
{
    private readonly Dictionary<long, int> _slots;
    private readonly Accumulator[] _accumulators;

    internal PivotCube(
        object records,
        object fields,
        int recordCount,
        int includedRecordCount,
        PivotLayout layout,
        IReadOnlyDictionary<string, FieldMeta> meta,
        AxisNode rowRoot,
        AxisNode columnRoot,
        string[] sources,
        Dictionary<long, int> slots,
        Accumulator[] accumulators,
        IReadOnlyDictionary<string, FieldItems> items)
    {
        RecordsIdentity = records;
        FieldsIdentity = fields;
        RecordCount = recordCount;
        IncludedRecordCount = includedRecordCount;
        Layout = layout;
        Meta = meta;
        RowRoot = rowRoot;
        ColumnRoot = columnRoot;
        Sources = sources;
        _slots = slots;
        _accumulators = accumulators;
        Items = items;
    }

    /// <summary>How many Source Records the snapshot held.</summary>
    public int RecordCount { get; }

    /// <summary>How many of them no Hidden Item left out.</summary>
    public int IncludedRecordCount { get; }

    /// <summary>The layout the cube was aggregated under.</summary>
    public PivotLayout Layout { get; }

    internal object RecordsIdentity { get; }

    internal object FieldsIdentity { get; }

    internal IReadOnlyDictionary<string, FieldMeta> Meta { get; }

    internal AxisNode RowRoot { get; }

    internal AxisNode ColumnRoot { get; }

    /// <summary>The fields accumulated for Values, in the order their accumulators are kept.</summary>
    internal string[] Sources { get; }

    /// <summary>The Items of each placed field, over the whole snapshot.</summary>
    internal IReadOnlyDictionary<string, FieldItems> Items { get; }

    /// <summary>
    /// Whether <paramref name="layout"/> can be laid out from this cube without aggregating
    /// again: the same row and column fields in the same order, the same report filter fields,
    /// the same Hidden Items on each, and every Value Field reading a field this cube
    /// accumulated. Everything else in a layout only lays the cube out (ADR-0059).
    /// </summary>
    public bool Holds(PivotLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (!SameFields(layout.Rows, Layout.Rows, ordered: true)
            || !SameFields(layout.Columns, Layout.Columns, ordered: true)
            || !SameFields(layout.Filters, Layout.Filters, ordered: false))
            return false;
        foreach (var value in layout.Values)
        {
            if (Array.IndexOf(Sources, value.Field) < 0)
                return false;
        }
        return true;
    }

    private static bool SameFields(
        IReadOnlyList<PivotFieldPlacement> one, IReadOnlyList<PivotFieldPlacement> other, bool ordered)
    {
        if (one.Count != other.Count)
            return false;
        for (var i = 0; i < one.Count; i++)
        {
            var mine = one[i];
            var theirs = ordered ? other[i] : other.FirstOrDefault(p => p.Field == mine.Field);
            if (theirs is null || theirs.Field != mine.Field)
                return false;
            if (!ReferenceEquals(mine.HiddenItems, theirs.HiddenItems)
                && !mine.HiddenItems.ToHashSet().SetEquals(theirs.HiddenItems))
                return false;
        }
        return true;
    }

    /// <summary>The accumulation of <paramref name="source"/> where the two nodes cross, or false
    /// when no included record carries both.</summary>
    internal bool TryRead(AxisNode row, AxisNode column, int source, out Accumulator accumulator)
    {
        if (_slots.TryGetValue(SlotKey(row.Id, column.Id), out var slot))
        {
            accumulator = _accumulators[(slot * Sources.Length) + source];
            return true;
        }
        accumulator = default;
        return false;
    }

    internal static long SlotKey(int row, int column) => ((long)row << 32) | (uint)column;
}

/// <summary>One node of an axis tree: the root, or an Item under its parent's.</summary>
internal sealed class AxisNode
{
    public AxisNode(int id, int level, AxisNode? parent, ItemRef? item)
    {
        Id = id;
        Level = level;
        Parent = parent;
        Item = item;
    }

    public int Id { get; }

    /// <summary>−1 for the root; the index of the node's field on its axis otherwise.</summary>
    public int Level { get; }

    public AxisNode? Parent { get; }

    /// <summary>The Item the node stands for; null for the root.</summary>
    public ItemRef? Item { get; }

    public List<AxisNode> Children { get; } = [];

    public Dictionary<ItemKey, AxisNode>? ChildIndex { get; set; }

    public AxisNode Child(ItemRef item, ref int nextId)
    {
        ChildIndex ??= [];
        ref var child = ref CollectionsMarshal.GetValueRefOrAddDefault(ChildIndex, item.Key, out var exists);
        if (!exists)
        {
            child = new AxisNode(nextId++, Level + 1, this, item);
            Children.Add(child);
        }
        return child!;
    }

    /// <summary>This node and every ancestor, root last.</summary>
    public IEnumerable<AxisNode> SelfAndAncestors()
    {
        for (var node = this; node is not null; node = node.Parent)
            yield return node;
    }
}

/// <summary>One Item of one field: its key and the first value that carried it, which labels it.</summary>
internal sealed class ItemRef
{
    private PivotItemKey? _public;

    public ItemRef(ItemKey key, object? firstValue)
    {
        Key = key;
        FirstValue = firstValue;
    }

    public ItemKey Key { get; }

    public object? FirstValue { get; }

    public PivotItemKey PublicKey => _public ??= Key.ToPublic();
}

/// <summary>Every Item one placed field carries in a snapshot, in the order first seen.</summary>
internal sealed class FieldItems
{
    private readonly Dictionary<ItemKey, ItemRef> _map = [];

    public List<ItemRef> InOrderSeen { get; } = [];

    public ItemRef Register(object? value)
    {
        var key = ItemKey.Of(value);
        ref var item = ref CollectionsMarshal.GetValueRefOrAddDefault(_map, key, out var exists);
        if (!exists)
        {
            item = new ItemRef(key, value);
            InOrderSeen.Add(item);
        }
        return item!;
    }
}

/// <summary>What the engine keeps of a declared field besides its accessor.</summary>
internal sealed class FieldMeta
{
    public FieldMeta(PivotFieldInfo info, string? format, IReadOnlyList<object> itemOrder)
    {
        Info = info;
        Format = format;
        var index = new Dictionary<ItemKey, int>();
        for (var i = 0; i < itemOrder.Count; i++)
            index.TryAdd(ItemKey.Of(itemOrder[i]), i);
        DeclaredOrder = index;
    }

    public PivotFieldInfo Info { get; }

    public string? Format { get; }

    public IReadOnlyDictionary<ItemKey, int> DeclaredOrder { get; }
}
