using System.Runtime.InteropServices;

namespace ExPivot.Engine;

/// <summary>
/// What the engine holds of a Pivot Source's answer to one question (ADR-0060/0066): the tree of
/// row Items and the tree of column Items its leaves form, and every cell where they cross — each
/// leaf as the source answered it, and every subtotal and grand total merged from the leaves'
/// parts, never from the totals below it. It lays nothing out. Collapse, order, the form, the
/// totals, Show Values As, formats and captions are laid out from it again without asking the
/// source (<see cref="PivotEngine.Report"/>); <see cref="Holds"/> says whether a layout can be.
/// <para>A live redraw makes the next cube from the last (ADR-0161): it shares the axis trees and
/// the cells, takes its own copy of the values that change, and leaves the cube it was made from
/// exactly as it was.</para>
/// </summary>
public sealed class PivotCube
{
    private static long s_made;

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

    /// <summary>The cell a key names, for layer 1, which holds the sliced cube to the synchronous
    /// one cell by cell.</summary>
    internal int CellOf(long key) => _cells[key];

    /// <summary>The parts of the field in Values <paramref name="source"/> at every cell of a cube
    /// built afresh, for layer 1.</summary>
    internal PartColumns ValuesOf(int source) => _changedAt is null ? _values[source] : throw new InvalidOperationException("The cube was made from another: read its cells.");

    /// <summary>A number of its own, which a cube made from this one names (<see cref="MadeFrom"/>).</summary>
    internal long Id { get; } = Interlocked.Increment(ref s_made);

    /// <summary>The <see cref="Id"/> of the cube this one was made from, its cells on the changed
    /// leaves' paths computed again (ADR-0161); 0 for a cube built afresh.</summary>
    internal long MadeFrom { get; private init; }

    /// <summary>For a cube made from another: by node id, the row nodes on a changed leaf's path —
    /// the rows whose cells may differ from the other's.</summary>
    internal bool[]? AffectedRows { get; private init; }

    /// <summary>For a cube made from another: by node id, the column nodes on a changed leaf's path.</summary>
    internal bool[]? AffectedColumns { get; private init; }

    /// <summary>How many cells' values this cube holds apart from the values it shares, for layer 1.</summary>
    internal int ChangedApart => _changedAt?.Count ?? 0;

    // The leaves' nodes and the answer's axes, which a cube made from this one shares, and compares
    // the next answer's leaves with.
    private Structure? _structure;

    // A cube made from another holds the values that changed apart, by cell, over the other's.
    private Dictionary<int, int>? _changedAt;
    private PartColumns[]? _changed;

    private sealed class Structure(AxisNode[] rowLeaves, AxisNode[] columnLeaves, PivotAnswerAxis[] rowAxes, PivotAnswerAxis[] columnAxes, int rowNodes, int columnNodes)
    {
        public AxisNode[] RowLeaves { get; } = rowLeaves;
        public AxisNode[] ColumnLeaves { get; } = columnLeaves;
        public PivotAnswerAxis[] RowAxes { get; } = rowAxes;
        public PivotAnswerAxis[] ColumnAxes { get; } = columnAxes;
        public int RowNodes { get; } = rowNodes;
        public int ColumnNodes { get; } = columnNodes;
        public int LeafCount => RowLeaves.Length;
    }

    /// <summary>
    /// The cube of an answer (ADR-0066). Refuses by name an answer to another question — other row
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
        => Slicer.Run(BuildAsync(query, answer, meta, Slicer.Unsliced, records, fields, allItems));

    /// <summary>
    /// <see cref="Build"/> in slices (PV-40): the same steps — the trees, the leaves' cells, every
    /// total merged, every exact value written in one form — each over its leaves or cells a piece
    /// at a time, yielding whenever the slice is spent. The answer is only read.
    /// </summary>
    internal static async ValueTask<PivotCube> BuildAsync(
        PivotQuery query,
        PivotAnswer answer,
        IReadOnlyDictionary<string, FieldMeta> meta,
        Slicer slicer,
        object? records = null,
        object? fields = null,
        Func<string, IReadOnlyList<ItemKey>>? allItems = null)
    {
        if (answer.Mismatch(query) is { } mismatch)
            throw new InvalidOperationException($"The answer does not answer the question: {mismatch} (ADR-0066).");
        foreach (var field in query.Placed.Select(f => f.Field).Concat(query.Values.Select(v => v.Field)))
        {
            if (!meta.ContainsKey(field))
                throw new InvalidOperationException($"The question names '{field}', and no Pivot Field of that name is declared (ADR-0060).");
        }

        var leafCount = answer.LeafCount;
        var rowRoot = new AxisNode(0, -1, null, null);
        var columnRoot = new AxisNode(0, -1, null, null);
        var rowNodes = new List<AxisNode> { rowRoot };
        var columnNodes = new List<AxisNode> { columnRoot };
        var rowLeaves = await TreeAsync(answer.RowAxes, rowRoot, rowNodes, leafCount, slicer).ConfigureAwait(false);
        var columnLeaves = await TreeAsync(answer.ColumnAxes, columnRoot, columnNodes, leafCount, slicer).ConfigureAwait(false);

        var sources = answer.ValueColumns.Select(v => v.Field).ToArray();
        var values = new PartColumns[answer.ValueColumns.Length];
        for (var v = 0; v < values.Length; v++)
        {
            var answered = answer.ValueColumns[v].Columns;
            var columns = values[v] = new PartColumns(answer.ValueColumns[v].Parts, Math.Max(16, leafCount * 2));
            // A copy of memory: a piece of a few thousand cells is a moment's work.
            await slicer.ForAsync(leafCount, (from, to) => columns.CopyRange(answered, from, to), weight: 1).ConfigureAwait(false);
        }

        // The leaves are the first cells, as the source answered them.
        var cells = new Dictionary<long, int>(Math.Max(16, leafCount * 2), CellKey.Comparer);
        long included = 0;
        await slicer.ForAsync(leafCount, (from, to) =>
        {
            for (var leaf = from; leaf < to; leaf++)
            {
                if (!cells.TryAdd(CellKey.Of(rowLeaves[leaf].Id, columnLeaves[leaf].Id), leaf))
                    throw new InvalidOperationException($"The answer has two leaves for one cell (leaf {leaf}); a source answers each combination of Items once (ADR-0066).");
                included += answer.Records[leaf];
            }
        }, weight: 2).ConfigureAwait(false);

        // Every total from its leaves (ADR-0060): each leaf is merged into every cell whose row
        // and column are its own or their ancestors. An ancestor's cell is never a leaf's, so
        // nothing is counted twice, and the parts combine exactly. With no field in Values there
        // is nothing to read at a cell, and no total is made.
        var cellCount = leafCount;
        if (values.Length > 0)
        {
            // What one leaf costs: a cell looked up, and every field merged into it, for each pair
            // of its row and column or their ancestors.
            var pairs = ((answer.RowAxes.Length + 1) * (answer.ColumnAxes.Length + 1)) - 1;
            await slicer.ForAsync(leafCount, (from, to) =>
            {
                for (var leaf = from; leaf < to; leaf++)
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
            }, weight: Math.Max(1, pairs * (1 + values.Length))).ConfigureAwait(false);
        }

        // One form for each exact value (ADR-0064): a source may write a Decimal at any scale — a
        // database's money comes back as 75.60 — and decimal addition keeps the larger scale, so
        // 0.25 + 0.25 is 0.50. The report, and the raw form a copy carries, is then the same
        // whichever source answered (PV-22).
        foreach (var columns in values)
            await slicer.ForAsync(cellCount, columns.Canonicalize).ConfigureAwait(false);

        return new PivotCube(query, answer.SourceVersion, included, meta, rowRoot, columnRoot, sources, cells, values)
        {
            RecordsIdentity = records,
            FieldsIdentity = fields,
            AllItems = allItems,
            _structure = new Structure(rowLeaves, columnLeaves, answer.RowAxes, answer.ColumnAxes, rowNodes.Count, columnNodes.Count),
        };
    }

    /// <summary>
    /// Whether so many of an answer's leaves changed that the next cube is built afresh rather than
    /// from the last (ADR-0161: the engine's own threshold): more than a quarter of them, and more
    /// than sixteen.
    /// </summary>
    internal static bool TooManyChanged(int changed, int leafCount) => changed > Math.Max(16, leafCount / 4);

    /// <summary><see cref="PivotEngine.NextCube"/>'s work: from <paramref name="previous"/> when the
    /// question and the fields are its, and its answer's leaves are the answer's with a few of them
    /// changed; afresh otherwise.</summary>
    internal static async ValueTask<PivotCube> NextAsync(PivotCube previous, PivotQuery query, PivotAnswer answer, IReadOnlyList<PivotField> fields, Slicer slicer)
    {
        var changed = SameFields(previous.FieldsIdentity, fields) && previous.Query.Equals(query) && answer.Mismatch(query) is null
            ? await previous.ChangedLeavesAsync(answer, slicer).ConfigureAwait(false)
            : null;
        if (changed is null || TooManyChanged(changed.Count, answer.LeafCount))
            return await BuildAsync(query, answer, FieldMeta.Of(fields), slicer, fields: fields).ConfigureAwait(false);
        return await previous.NextFromAsync(query, answer, changed, slicer).ConfigureAwait(false);
    }

    // The same field declarations, one by one: the Items are labelled and ordered as they were.
    private static bool SameFields(object? held, IReadOnlyList<PivotField> fields)
    {
        if (ReferenceEquals(held, fields))
            return true;
        if (held is not IReadOnlyList<PivotField> mine || mine.Count != fields.Count)
            return false;
        for (var i = 0; i < mine.Count; i++)
        {
            if (!ReferenceEquals(mine[i], fields[i]))
                return false;
        }
        return true;
    }

    /// <summary>
    /// The leaves of <paramref name="answer"/> that changed since this cube's answer, or null when
    /// they are not the same leaves — another count, other Items or Items spelled anew, a leaf under
    /// other Items — or the answer says they were made afresh. The answer's own word is taken when it
    /// names this cube's Source Version; otherwise each leaf's parts are compared with this cube's
    /// cell for it: a decimal of another value, or a double of other bits, is a change.
    /// </summary>
    private async ValueTask<IReadOnlyList<int>?> ChangedLeavesAsync(PivotAnswer answer, Slicer slicer)
    {
        if (_structure is not { } shape || answer.LeafCount != shape.LeafCount
            || !SameAxes(shape.RowAxes, answer.RowAxes, shape.LeafCount) || !SameAxes(shape.ColumnAxes, answer.ColumnAxes, shape.LeafCount)
            || !SameParts(answer.ValueColumns))
            return null;
        if (answer.ChangedLeaves is { } says && string.Equals(says.Since, SourceVersion, StringComparison.Ordinal))
            return says.SameLeaves ? says.Leaves : null;
        var changed = new List<int>();
        var answered = answer.ValueColumns;
        await slicer.ForAsync(shape.LeafCount, (from, to) =>
        {
            for (var leaf = from; leaf < to; leaf++)
            {
                for (var v = 0; v < answered.Length; v++)
                {
                    var (columns, at) = CellParts(leaf, v);
                    if (!columns.SameAs(at, answered[v].Columns, leaf))
                    {
                        changed.Add(leaf);
                        break;
                    }
                }
            }
        }, weight: 1 + answered.Length).ConfigureAwait(false);
        return changed;
    }

    // The same parts of each field in Values as this cube keeps: a source may answer more than was
    // asked, and what is merged and compared is what both carry.
    private bool SameParts(PivotAnswerValues[] answered)
    {
        for (var v = 0; v < answered.Length; v++)
        {
            if (answered[v].Parts != _values[v].Parts)
                return false;
        }
        return true;
    }

    // Two answers' axes alike: the same Items spelled the same, and each leaf under the same Item.
    private static bool SameAxes(PivotAnswerAxis[] mine, PivotAnswerAxis[] theirs, int leafCount)
    {
        if (mine.Length != theirs.Length)
            return false;
        for (var level = 0; level < mine.Length; level++)
        {
            var (items, their) = (mine[level].ItemArray, theirs[level].ItemArray);
            if (!string.Equals(mine[level].Field, theirs[level].Field, StringComparison.Ordinal) || items.Length != their.Length)
                return false;
            for (var i = 0; i < items.Length; i++)
            {
                if (!PivotItemKey.SameSpelling(items[i], their[i]))
                    return false;
            }
            if (!mine[level].ItemOfLeaf.AsSpan(0, leafCount).SequenceEqual(theirs[level].ItemOfLeaf.AsSpan(0, leafCount)))
                return false;
        }
        return true;
    }

    // Where a cell's parts of the field in Values `source` are kept: among the values that changed,
    // or the values shared.
    private (PartColumns Columns, int At) CellParts(int cell, int source)
        => _changedAt is not null && _changedAt.TryGetValue(cell, out var slot) ? (_changed![source], slot) : (_values[source], cell);

    /// <summary>
    /// The next cube from this one (ADR-0161): the axis trees and the cells shared, and the cells on
    /// the changed leaves' paths computed again — each changed leaf's cell as the source answered it,
    /// and every total above it on both axes from every leaf beneath that total, in the leaves'
    /// order, as a cube built afresh merges them, then each exact value written in one form. Those
    /// values are the next cube's own; this cube's are only read.
    /// </summary>
    private async ValueTask<PivotCube> NextFromAsync(PivotQuery query, PivotAnswer answer, IReadOnlyList<int> changed, Slicer slicer)
    {
        var shape = _structure!;
        var answered = answer.ValueColumns;
        var rowAffected = new bool[shape.RowNodes];
        var columnAffected = new bool[shape.ColumnNodes];
        foreach (var leaf in changed)
        {
            for (var node = shape.RowLeaves[leaf]; node is not null && !rowAffected[node.Id]; node = node.Parent)
                rowAffected[node.Id] = true;
            for (var node = shape.ColumnLeaves[leaf]; node is not null && !columnAffected[node.Id]; node = node.Parent)
                columnAffected[node.Id] = true;
        }

        // The cells computed again, each with a slot of its own: a changed leaf's, and every total on
        // its paths. With no field in Values a cube holds no total, and nothing is computed.
        var slots = new Dictionary<int, int>();
        var totals = new Dictionary<long, int>(CellKey.Comparer);
        if (_values.Length > 0)
        {
            foreach (var leaf in changed)
            {
                slots.TryAdd(leaf, slots.Count);
                var (rowLeaf, columnLeaf) = (shape.RowLeaves[leaf], shape.ColumnLeaves[leaf]);
                for (var row = rowLeaf; row is not null; row = row.Parent)
                {
                    for (var column = columnLeaf; column is not null; column = column.Parent)
                    {
                        var key = CellKey.Of(row.Id, column.Id);
                        if ((row == rowLeaf && column == columnLeaf) || totals.ContainsKey(key))
                            continue;
                        var slot = slots.Count;
                        slots.Add(_cells[key], slot);
                        totals.Add(key, slot);
                    }
                }
            }
        }
        var work = new PartColumns[_values.Length];
        for (var v = 0; v < work.Length; v++)
            work[v] = new PartColumns(_values[v].Parts, Math.Max(1, slots.Count));

        long included = 0;
        var pairs = ((answer.RowAxes.Length + 1) * (answer.ColumnAxes.Length + 1)) - 1;
        await slicer.ForAsync(shape.LeafCount, (from, to) =>
        {
            for (var leaf = from; leaf < to; leaf++)
            {
                included += answer.Records[leaf];
                if (totals.Count == 0)
                    continue;
                // The totals the leaf is beneath that are computed again: its row's and column's
                // ancestors from the nearest on a changed path up, crossed.
                var (rowLeaf, columnLeaf) = (shape.RowLeaves[leaf], shape.ColumnLeaves[leaf]);
                var nearestRow = rowLeaf;
                while (!rowAffected[nearestRow.Id])
                    nearestRow = nearestRow.Parent!;
                var nearestColumn = columnLeaf;
                while (!columnAffected[nearestColumn.Id])
                    nearestColumn = nearestColumn.Parent!;
                for (var row = nearestRow; row is not null; row = row.Parent)
                {
                    for (var column = nearestColumn; column is not null; column = column.Parent)
                    {
                        if ((row == rowLeaf && column == columnLeaf) || !totals.TryGetValue(CellKey.Of(row.Id, column.Id), out var slot))
                            continue;
                        for (var v = 0; v < work.Length; v++)
                            work[v].Merge(slot, answered[v].Columns, leaf);
                    }
                }
            }
        }, weight: totals.Count == 0 ? 1 : Math.Max(1, pairs * (1 + work.Length))).ConfigureAwait(false);
        foreach (var leaf in changed)
        {
            if (!slots.TryGetValue(leaf, out var slot))
                continue;
            for (var v = 0; v < work.Length; v++)
                work[v].CopyCell(slot, answered[v].Columns, leaf);
        }
        foreach (var columns in work)
            columns.Canonicalize(0, slots.Count);

        var (values, changedAt, changedValues) = Overlay(slots, work);
        return new PivotCube(query, answer.SourceVersion, included, Meta, RowRoot, ColumnRoot, Sources, _cells, values)
        {
            FieldsIdentity = FieldsIdentity,
            _structure = shape,
            _changedAt = changedAt,
            _changed = changedValues,
            MadeFrom = Id,
            AffectedRows = rowAffected,
            AffectedColumns = columnAffected,
        };
    }

    /// <summary>
    /// The next cube's values: this cube's shared, with the values that changed — this cube's own
    /// that were not computed again, and the ones that were — held apart by cell. When those grow
    /// past an eighth of the cells, every cell is copied once into values of the next cube's own,
    /// which the cubes made from it share in turn.
    /// </summary>
    private (PartColumns[] Values, Dictionary<int, int>? ChangedAt, PartColumns[]? Changed) Overlay(Dictionary<int, int> slots, PartColumns[] work)
    {
        if (slots.Count == 0)
            return (_values, _changedAt, _changed);
        var carried = _changedAt is null ? 0 : _changedAt.Count(pair => !slots.ContainsKey(pair.Key));
        var count = carried + slots.Count;
        var cellCount = _cells.Count;
        if (count > Math.Max(4096, cellCount / 8))
        {
            var flat = new PartColumns[_values.Length];
            for (var v = 0; v < flat.Length; v++)
            {
                flat[v] = new PartColumns(_values[v].Parts, Math.Max(16, cellCount));
                flat[v].CopyFrom(_values[v], cellCount);
                if (_changedAt is not null)
                {
                    foreach (var (cell, slot) in _changedAt)
                        flat[v].CopyCell(cell, _changed![v], slot);
                }
                foreach (var (cell, slot) in slots)
                    flat[v].CopyCell(cell, work[v], slot);
            }
            return (flat, null, null);
        }
        var changedAt = new Dictionary<int, int>(count);
        var changed = new PartColumns[_values.Length];
        for (var v = 0; v < changed.Length; v++)
            changed[v] = new PartColumns(_values[v].Parts, count);
        if (_changedAt is not null)
        {
            foreach (var (cell, slot) in _changedAt)
            {
                if (slots.ContainsKey(cell))
                    continue;
                var at = changedAt.Count;
                changedAt.Add(cell, at);
                for (var v = 0; v < changed.Length; v++)
                    changed[v].CopyCell(at, _changed![v], slot);
            }
        }
        foreach (var (cell, slot) in slots)
        {
            var at = changedAt.Count;
            changedAt.Add(cell, at);
            for (var v = 0; v < changed.Length; v++)
                changed[v].CopyCell(at, work[v], slot);
        }
        return (_values, changedAt, changed);
    }

    // One axis's tree from the leaves' Items, level by level, a piece of leaves at a time; the node
    // each leaf ends at.
    private static async ValueTask<AxisNode[]> TreeAsync(PivotAnswerAxis[] axes, AxisNode root, List<AxisNode> nodes, int leafCount, Slicer slicer)
    {
        var refs = new ItemRef[axes.Length][];
        for (var level = 0; level < axes.Length; level++)
        {
            var items = axes[level].ItemArray;
            var levelRefs = refs[level] = new ItemRef[items.Length];
            await slicer.ForAsync(items.Length, (from, to) =>
            {
                for (var i = from; i < to; i++)
                    levelRefs[i] = ItemRef.Of(items[i]);
            }, weight: 2).ConfigureAwait(false);
        }
        var leaves = new AxisNode[leafCount];
        await slicer.ForAsync(leafCount, (from, to) =>
        {
            for (var leaf = from; leaf < to; leaf++)
            {
                var node = root;
                for (var level = 0; level < axes.Length; level++)
                {
                    var item = axes[level].ItemOfLeaf[leaf];
                    node = node.Child(item, refs[level][item], nodes);
                }
                leaves[leaf] = node;
            }
        }, weight: Math.Max(1, axes.Length)).ConfigureAwait(false);
        return leaves;
    }

    /// <summary>
    /// Whether <paramref name="layout"/> can be laid out from this cube without asking again
    /// (ADR-0060/0066): the same row and column fields in the same order, the same report filter
    /// fields that hide Items, the same Hidden Items on each, and every Value Field reading a field
    /// in Values here whose parts its Aggregation reads — Sum and Average read one part, so a
    /// change between them asks nothing; Max reads another, so a change from Sum to Max asks
    /// again. A field in Filters that hides nothing changes no leaf, so placing it, or moving it
    /// while it hides nothing, needs no new answer (ADR-0066, refined). Everything else in a
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
    {
        if (!_cells.TryGetValue(CellKey.Of(row.Id, column.Id), out var cell))
            return AggregateValue.Empty;
        var (columns, at) = CellParts(cell, source);
        return columns.Read(at, aggregation);
    }
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
        PathHash = item is null ? 0 : HashCode.Combine(parent!.PathHash, item.KeyHash);
    }

    /// <summary>The hash of this node's Items and its ancestors', as a report row's key compares
    /// them (<see cref="PivotRowKey"/>): made with the node from its parent's and its Item's, so a
    /// row's key hashes nothing and allocates nothing for its path. 0 for the root.</summary>
    public int PathHash { get; }

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
        KeyHash = PublicKey.GetHashCode();
    }

    /// <summary>The hash of <see cref="PublicKey"/>, computed once per Item: every node under the
    /// Item combines it into its <see cref="AxisNode.PathHash"/>.</summary>
    public int KeyHash { get; }

    public ItemKey Key { get; }

    public object? FirstValue { get; }

    public PivotItemKey PublicKey { get; }

    /// <summary>Whether <see cref="OrderKey"/> has been computed: a field's Order Key is called once
    /// per Item (ADR-0060).</summary>
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

    /// <summary>The part of a Date column the field is, or null (ADR-0060).</summary>
    public PivotDatePart? DatePart { get; }

    /// <summary>The field's Order Key, or null (ADR-0060).</summary>
    public Func<object, IComparable?>? OrderKey { get; }

    /// <summary>The metadata of every declared field, by name, refusing a name declared twice.</summary>
    public static IReadOnlyDictionary<string, FieldMeta> Of(IReadOnlyList<PivotField> fields)
        => PivotSource.Declared(fields).ToDictionary(pair => pair.Key, pair => new FieldMeta(pair.Value), StringComparer.Ordinal);
}
