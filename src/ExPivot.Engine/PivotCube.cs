using System.Runtime.InteropServices;

namespace ExPivot.Engine;

/// <summary>
/// What the engine holds of a Pivot Source's answer to one question (ADR-0059/0065): the tree of
/// row Items and the tree of column Items its leaves form, and every cell where they cross — each
/// leaf as the source answered it, and every subtotal and grand total merged from the leaves'
/// parts, never from the totals below it. It lays nothing out. Collapse, order, the form, the
/// totals, Show Values As, formats and captions are laid out from it again without asking the
/// source (<see cref="PivotEngine.Report"/>); <see cref="Holds"/> says whether a layout can be.
/// </summary>
public sealed class PivotCube
{
    private readonly Dictionary<long, int> _cells;
    private readonly PartColumns[] _values;

    private PivotCube(
        PivotQuery query,
        string sourceVersion,
        long includedRecordCount,
        IReadOnlyDictionary<string, FieldMeta> meta,
        AxisNode rowRoot,
        AxisNode columnRoot,
        string[] sources,
        Dictionary<long, int> cells,
        PartColumns[] values)
    {
        Query = query;
        SourceVersion = sourceVersion;
        IncludedRecordCount = includedRecordCount;
        Meta = meta;
        RowRoot = rowRoot;
        ColumnRoot = columnRoot;
        Sources = sources;
        _cells = cells;
        _values = values;
    }

    /// <summary>The question the answer was given to.</summary>
    public PivotQuery Query { get; }

    /// <summary>The Source Version of the answer: a field's Items and a cell's records are asked
    /// for under it.</summary>
    public string SourceVersion { get; }

    /// <summary>How many records no Hidden Item left out — the grand total's records.</summary>
    public long IncludedRecordCount { get; }

    /// <summary>The fields in Values, each with the parts the answer carries — what an Aggregation
    /// can be read from without asking again.</summary>
    public IReadOnlyDictionary<string, PivotParts> Parts
        => Sources.Select((name, v) => (name, v)).ToDictionary(p => p.name, p => _values[p.v].Parts, StringComparer.Ordinal);

    /// <summary>The records a cube built by <see cref="PivotEngine.Aggregate{TRecord}"/> was built
    /// from, compared by instance; null otherwise.</summary>
    internal object? RecordsIdentity { get; private init; }

    /// <summary>The field declarations, likewise.</summary>
    internal object? FieldsIdentity { get; private init; }

    /// <summary>Every Item of a field over all the records, for a cube built from records in the
    /// process; null for one built from a source's answer, whose Items are asked of the source.</summary>
    internal Func<string, IReadOnlyList<ItemKey>>? AllItems { get; private init; }

    internal IReadOnlyDictionary<string, FieldMeta> Meta { get; }

    internal AxisNode RowRoot { get; }

    internal AxisNode ColumnRoot { get; }

    /// <summary>The fields in Values, in the order their parts are kept.</summary>
    internal string[] Sources { get; }

    /// <summary>Every cell's key, for the hash's own test.</summary>
    internal IEnumerable<long> CellKeys => _cells.Keys;

    /// <summary>
    /// The cube of an answer (ADR-0065). Refuses by name an answer to another question — other row
    /// or column fields, other fields in Values, fewer parts than asked, more leaves than allowed —
    /// and an answer with two leaves for one cell.
    /// </summary>
    internal static PivotCube Build(
        PivotQuery query,
        PivotAnswer answer,
        IReadOnlyDictionary<string, FieldMeta> meta,
        object? records = null,
        object? fields = null,
        Func<string, IReadOnlyList<ItemKey>>? allItems = null)
    {
        if (answer.Mismatch(query) is { } mismatch)
            throw new InvalidOperationException($"The answer does not answer the question: {mismatch} (ADR-0065).");
        foreach (var field in query.Placed.Select(f => f.Field).Concat(query.Values.Select(v => v.Field)))
        {
            if (!meta.ContainsKey(field))
                throw new InvalidOperationException($"The question names '{field}', and no Pivot Field of that name is declared (ADR-0059).");
        }

        var leafCount = answer.LeafCount;
        var rowRoot = new AxisNode(0, -1, null, null);
        var columnRoot = new AxisNode(0, -1, null, null);
        var rowNodes = new List<AxisNode> { rowRoot };
        var columnNodes = new List<AxisNode> { columnRoot };
        var rowLeaves = Tree(answer.RowAxes, rowRoot, rowNodes, leafCount);
        var columnLeaves = Tree(answer.ColumnAxes, columnRoot, columnNodes, leafCount);

        var sources = answer.ValueColumns.Select(v => v.Field).ToArray();
        var values = answer.ValueColumns.Select(v =>
        {
            var columns = new PartColumns(v.Parts, Math.Max(16, leafCount * 2));
            columns.CopyFrom(v.Columns, leafCount);
            return columns;
        }).ToArray();

        // The leaves are the first cells, as the source answered them.
        var cells = new Dictionary<long, int>(Math.Max(16, leafCount * 2), CellKey.Comparer);
        long included = 0;
        for (var leaf = 0; leaf < leafCount; leaf++)
        {
            if (!cells.TryAdd(CellKey.Of(rowLeaves[leaf].Id, columnLeaves[leaf].Id), leaf))
                throw new InvalidOperationException($"The answer has two leaves for one cell (leaf {leaf}); a source answers each combination of Items once (ADR-0065).");
            included += answer.Records[leaf];
        }

        // Every total from its leaves (ADR-0059): each leaf is merged into every cell whose row
        // and column are its own or their ancestors. An ancestor's cell is never a leaf's, so
        // nothing is counted twice, and the parts combine exactly. With no field in Values there
        // is nothing to read at a cell, and no total is made.
        var cellCount = leafCount;
        for (var leaf = 0; values.Length > 0 && leaf < leafCount; leaf++)
        {
            var rowLeaf = rowLeaves[leaf];
            var columnLeaf = columnLeaves[leaf];
            for (var row = rowLeaf; row is not null; row = row.Parent)
            {
                for (var column = columnLeaf; column is not null; column = column.Parent)
                {
                    if (row == rowLeaf && column == columnLeaf)
                        continue;
                    ref var cell = ref CollectionsMarshal.GetValueRefOrAddDefault(cells, CellKey.Of(row.Id, column.Id), out var exists);
                    if (!exists)
                    {
                        cell = cellCount++;
                        foreach (var columns in values)
                            columns.EnsureCapacity(cellCount);
                    }
                    var target = cell;
                    foreach (var columns in values)
                        columns.Merge(target, columns, leaf);
                }
            }
        }

        // One form for each exact value (ADR-0063): a source may write a Decimal at any scale — a
        // database's money comes back as 75.60 — and decimal addition keeps the larger scale, so
        // 0.25 + 0.25 is 0.50. The report, and the raw form a copy carries, is then the same
        // whichever source answered (PV-22).
        foreach (var columns in values)
            columns.Canonicalize(cellCount);

        return new PivotCube(query, answer.SourceVersion, included, meta, rowRoot, columnRoot, sources, cells, values)
        {
            RecordsIdentity = records,
            FieldsIdentity = fields,
            AllItems = allItems,
        };
    }

    // One axis's tree from the leaves' Items, level by level; the node each leaf ends at.
    private static AxisNode[] Tree(PivotAnswerAxis[] axes, AxisNode root, List<AxisNode> nodes, int leafCount)
    {
        var refs = axes.Select(axis => axis.ItemArray.Select(ItemRef.Of).ToArray()).ToArray();
        var leaves = new AxisNode[leafCount];
        for (var leaf = 0; leaf < leafCount; leaf++)
        {
            var node = root;
            for (var level = 0; level < axes.Length; level++)
            {
                var item = axes[level].ItemOfLeaf[leaf];
                node = node.Child(item, refs[level][item], nodes);
            }
            leaves[leaf] = node;
        }
        return leaves;
    }

    /// <summary>
    /// Whether <paramref name="layout"/> can be laid out from this cube without asking again
    /// (ADR-0059/0065): the same row and column fields in the same order, the same report filter
    /// fields that hide Items, the same Hidden Items on each, and every Value Field reading a field
    /// in Values here whose parts its Aggregation reads — Sum and Average read one part, so a
    /// change between them asks nothing; Max reads another, so a change from Sum to Max asks
    /// again. A field in Filters that hides nothing changes no leaf, so placing it, or moving it
    /// while it hides nothing, needs no new answer (ADR-0065, refined). Everything else in a
    /// layout only lays the cube out.
    /// </summary>
    public bool Holds(PivotLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (!SameFields(layout.Rows, Query.Rows, ordered: true)
            || !SameFields(layout.Columns, Query.Columns, ordered: true)
            || !SameFields(PivotQuery.Hiding(layout.Filters), Query.Filters.Where(f => f.HiddenItems.Count > 0).ToArray(), ordered: false))
            return false;
        foreach (var value in layout.Values)
        {
            var source = Array.IndexOf(Sources, value.Field);
            if (source < 0 || !Enum.IsDefined(value.Aggregation) || !_values[source].Answers(value.Aggregation))
                return false;
        }
        return true;
    }

    private static bool SameFields(IReadOnlyList<PivotFieldPlacement> placements, IReadOnlyList<PivotQueryField> held, bool ordered)
    {
        if (placements.Count != held.Count)
            return false;
        for (var i = 0; i < placements.Count; i++)
        {
            var mine = placements[i];
            var theirs = ordered ? held[i] : held.FirstOrDefault(f => f.Field == mine.Field);
            if (theirs is null || theirs.Field != mine.Field || !PivotQueryField.SameKeys(mine.HiddenItems, theirs.HiddenItems))
                return false;
        }
        return true;
    }

    /// <summary>An Aggregation of the field in Values <paramref name="source"/> where two nodes
    /// cross; empty where no included record carries both.</summary>
    internal AggregateValue Read(AxisNode row, AxisNode column, int source, PivotAggregation aggregation)
        => _cells.TryGetValue(CellKey.Of(row.Id, column.Id), out var cell)
            ? _values[source].Read(cell, aggregation)
            : AggregateValue.Empty;
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

    // The children by their Item's index among the next level's Items.
    private Dictionary<int, AxisNode>? _childIndex;

    /// <summary>The child for the next level's Item <paramref name="index"/>, made — and numbered
    /// after <paramref name="nodes"/> — when it is new.</summary>
    public AxisNode Child(int index, ItemRef item, List<AxisNode> nodes)
    {
        _childIndex ??= [];
        ref var child = ref CollectionsMarshal.GetValueRefOrAddDefault(_childIndex, index, out var exists);
        if (!exists)
        {
            child = new AxisNode(nodes.Count, Level + 1, this, item);
            nodes.Add(child);
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

/// <summary>One Item of one field: its key, and the value that labels it — a text Item's first
/// spelling, a number, a date or a Boolean.</summary>
internal sealed class ItemRef
{
    public ItemRef(ItemKey key, object? firstValue, PivotItemKey? publicKey = null)
    {
        Key = key;
        FirstValue = firstValue;
        PublicKey = publicKey ?? key.ToPublic();
    }

    public ItemKey Key { get; }

    public object? FirstValue { get; }

    public PivotItemKey PublicKey { get; }

    /// <summary>Whether <see cref="OrderKey"/> has been computed: a field's Order Key is called once
    /// per Item (ADR-0059).</summary>
    public bool HasOrderKey { get; private set; }

    /// <summary>What the field's Order Key gave the Item, or null for no key.</summary>
    public IComparable? OrderKey { get; private set; }

    public void SetOrderKey(IComparable? key)
    {
        OrderKey = key;
        HasOrderKey = true;
    }

    /// <summary>The Item an answer names: its key, and the value its key stands for.</summary>
    public static ItemRef Of(PivotItemKey key) => Of(ItemKey.FromPublic(key), key);

    /// <summary>The Item a key stands for, labelled by the value it stands for.</summary>
    public static ItemRef Of(ItemKey key, PivotItemKey? publicKey = null) => new(key, key.Kind switch
    {
        PivotItemKind.Text => key.Text,
        PivotItemKind.Number => key.Number,
        PivotItemKind.Date => new DateTime(key.Ticks),
        PivotItemKind.Boolean => key.Ticks == 1,
        _ => null,
    }, publicKey);
}

/// <summary>What the engine keeps of a declared field: how the Field List shows it, and how its
/// Items are labelled and ordered.</summary>
internal sealed class FieldMeta
{
    public FieldMeta(PivotField field)
    {
        Info = field.Info;
        Format = field.Format;
        DatePart = field.DatePart;
        OrderKey = field.OrderKey;
        var index = new Dictionary<ItemKey, int>();
        for (var i = 0; i < field.ItemOrder.Count; i++)
            index.TryAdd(ItemKey.Of(field.ItemOrder[i]), i);
        DeclaredOrder = index;
    }

    public PivotFieldInfo Info { get; }

    public string? Format { get; }

    public IReadOnlyDictionary<ItemKey, int> DeclaredOrder { get; }

    /// <summary>The part of a Date column the field is, or null (ADR-0059).</summary>
    public PivotDatePart? DatePart { get; }

    /// <summary>The field's Order Key, or null (ADR-0059).</summary>
    public Func<object, IComparable?>? OrderKey { get; }

    /// <summary>The metadata of every declared field, by name, refusing a name declared twice.</summary>
    public static IReadOnlyDictionary<string, FieldMeta> Of(IReadOnlyList<PivotField> fields)
        => PivotSource.Declared(fields).ToDictionary(pair => pair.Key, pair => new FieldMeta(pair.Value), StringComparer.Ordinal);
}
