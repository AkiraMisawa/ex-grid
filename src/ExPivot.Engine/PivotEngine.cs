using System.Globalization;
using System.Runtime.InteropServices;

namespace ExPivot.Engine;

/// <summary>
/// The pivot engine (ADR-0059): Source Records aggregated under a Pivot Layout into a
/// <see cref="PivotCube"/>, and a cube laid out into a <see cref="PivotReport"/>. Pure and
/// synchronous, with no UI: a server computes the same report the screen shows, and a
/// server-side answer is held to this one.
/// </summary>
public static class PivotEngine
{
    /// <summary>Aggregates and lays out in one step — <see cref="Aggregate"/> then
    /// <see cref="Report"/>.</summary>
    public static PivotReport Compute<TRecord>(
        IReadOnlyList<TRecord> records,
        IReadOnlyList<PivotField<TRecord>> fields,
        PivotLayout layout,
        PivotOptions? options = null)
        => Report(Aggregate(records, fields, layout), layout, options);

    /// <summary>
    /// One pass over the records (ADR-0059): every placed field's Items are collected over the
    /// whole snapshot; each record no Hidden Item leaves out is placed in the row and column
    /// trees and its Value Fields' fields are accumulated where they cross; then every total is
    /// merged from the cells below it. Refuses by name a layout that names an undeclared field,
    /// stands a field in two of Filters, Rows and Columns, or orders by a Value Field it does
    /// not have; and a field list that declares a name twice.
    /// </summary>
    public static PivotCube Aggregate<TRecord>(
        IReadOnlyList<TRecord> records,
        IReadOnlyList<PivotField<TRecord>> fields,
        PivotLayout layout)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(layout);
        var declared = Declared(fields);
        Validate(layout, declared);

        var meta = new Dictionary<string, FieldMeta>(StringComparer.Ordinal);
        foreach (var field in fields)
            meta[field.Name] = new FieldMeta(field.Info, field.Format, field.ItemOrder);

        // Every placed field of Filters, Rows and Columns: its accessor, its Items and its
        // Hidden Items. Rows first, then Columns, then Filters, so a record's row path and
        // column path are the first slices of what it carries.
        var placed = layout.Rows.Concat(layout.Columns).Concat(layout.Filters).ToArray();
        var accessors = new Func<TRecord, object?>[placed.Length];
        var items = new FieldItems[placed.Length];
        var hidden = new HashSet<ItemKey>?[placed.Length];
        var itemsByField = new Dictionary<string, FieldItems>(StringComparer.Ordinal);
        for (var i = 0; i < placed.Length; i++)
        {
            accessors[i] = declared[placed[i].Field].Value;
            items[i] = new FieldItems();
            itemsByField[placed[i].Field] = items[i];
            if (placed[i].HiddenItems.Count > 0)
                hidden[i] = placed[i].HiddenItems.Select(ItemKey.FromPublic).ToHashSet();
        }

        var sources = layout.Values.Select(v => v.Field).Distinct(StringComparer.Ordinal).ToArray();
        var sourceAccessors = sources.Select(name => declared[name].Value).ToArray();
        var s = sources.Length;

        var rowCount = layout.Rows.Count;
        var columnCount = layout.Columns.Count;
        var nextRowId = 1;
        var nextColumnId = 1;
        var rowRoot = new AxisNode(0, -1, null, null);
        var columnRoot = new AxisNode(0, -1, null, null);
        var rowNodes = new List<AxisNode> { rowRoot };
        var columnNodes = new List<AxisNode> { columnRoot };

        var slots = new Dictionary<long, int>();
        var accumulators = new Accumulator[Math.Max(16, s * 16)];
        var slotCount = 0;
        var included = 0;
        var refs = new ItemRef[placed.Length];

        for (var r = 0; r < records.Count; r++)
        {
            var record = records[r];
            var excluded = false;
            for (var f = 0; f < placed.Length; f++)
            {
                var item = items[f].Register(accessors[f](record));
                refs[f] = item;
                if (hidden[f] is { } set && set.Contains(item.Key))
                    excluded = true;
            }
            if (excluded)
                continue;
            included++;

            var rowLeaf = rowRoot;
            for (var level = 0; level < rowCount; level++)
            {
                var before = nextRowId;
                rowLeaf = rowLeaf.Child(refs[level], ref nextRowId);
                if (nextRowId != before)
                    rowNodes.Add(rowLeaf);
            }
            var columnLeaf = columnRoot;
            for (var level = 0; level < columnCount; level++)
            {
                var before = nextColumnId;
                columnLeaf = columnLeaf.Child(refs[rowCount + level], ref nextColumnId);
                if (nextColumnId != before)
                    columnNodes.Add(columnLeaf);
            }

            if (s == 0)
                continue;
            var slot = SlotOf(slots, rowLeaf.Id, columnLeaf.Id, ref slotCount, ref accumulators, s);
            for (var v = 0; v < s; v++)
                accumulators[(slot * s) + v].Add(sourceAccessors[v](record));
        }

        // Every total from the cells below it (ADR-0059): each leaf cell is merged into every
        // cell whose row and column are its own or their ancestors. An ancestor cell is never
        // a leaf cell, so nothing is counted twice.
        if (s > 0)
        {
            var leaves = slots.ToArray();
            foreach (var (key, leafSlot) in leaves)
            {
                var rowLeaf = rowNodes[(int)(key >> 32)];
                var columnLeaf = columnNodes[(int)(uint)key];
                for (var row = rowLeaf; row is not null; row = row.Parent)
                {
                    for (var column = columnLeaf; column is not null; column = column.Parent)
                    {
                        if (row == rowLeaf && column == columnLeaf)
                            continue;
                        var target = SlotOf(slots, row.Id, column.Id, ref slotCount, ref accumulators, s);
                        for (var v = 0; v < s; v++)
                            accumulators[(target * s) + v].Merge(in accumulators[(leafSlot * s) + v]);
                    }
                }
            }
        }

        return new PivotCube(records, fields, records.Count, included, layout, meta,
            rowRoot, columnRoot, sources, slots, accumulators, itemsByField);
    }

    private static int SlotOf(
        Dictionary<long, int> slots, int row, int column, ref int slotCount, ref Accumulator[] accumulators, int sources)
    {
        ref var slot = ref CollectionsMarshal.GetValueRefOrAddDefault(slots, PivotCube.SlotKey(row, column), out var exists);
        if (!exists)
        {
            slot = slotCount++;
            var needed = slotCount * sources;
            if (needed > accumulators.Length)
                Array.Resize(ref accumulators, Math.Max(needed, accumulators.Length * 2));
        }
        return slot;
    }

    /// <summary>
    /// Lays a cube out under <paramref name="layout"/> (ADR-0059): the rows, the label columns,
    /// the value columns and their Header Group spans. Cheap — no pass over the records — and
    /// what a collapse, a sort, a form or a Value Field's Aggregation changing costs. Refuses a
    /// layout the cube does not hold (<see cref="PivotCube.Holds"/>).
    /// </summary>
    public static PivotReport Report(PivotCube cube, PivotLayout layout, PivotOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(cube);
        ArgumentNullException.ThrowIfNull(layout);
        if (!cube.Holds(layout))
        {
            throw new InvalidOperationException(
                "The cube was aggregated under other placed fields, Hidden Items or Value Fields than this layout " +
                "has; aggregate again (PivotEngine.Aggregate) before laying it out (ADR-0059).");
        }
        Validate(layout, cube.Meta.ToDictionary(pair => pair.Key, pair => pair.Value.Info, StringComparer.Ordinal));
        return new ReportBuilder(cube, layout, options ?? PivotOptions.Default).Build();
    }

    /// <summary>Whether <paramref name="cube"/> was aggregated from exactly these records and
    /// field declarations and holds <paramref name="layout"/>, so that a new report needs no pass
    /// over the records. The records and the fields are compared by instance: a new list is a
    /// refresh (ADR-0058).</summary>
    public static bool CanReuse<TRecord>(
        PivotCube? cube, IReadOnlyList<TRecord> records, IReadOnlyList<PivotField<TRecord>> fields, PivotLayout layout)
        => cube is not null && ReferenceEquals(cube.RecordsIdentity, records)
            && ReferenceEquals(cube.FieldsIdentity, fields) && cube.Holds(layout);

    /// <summary>
    /// Every Item of a placed field over the whole snapshot, in the field's order — what Filter…
    /// lists (ADR-0060) — each with its label and whether it is hidden now.
    /// </summary>
    public static IReadOnlyList<PivotItemInfo> ItemsOf(
        PivotCube cube, PivotLayout layout, string field, PivotOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(cube);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(field);
        if (!cube.Items.TryGetValue(field, out var items))
            throw new ArgumentException($"'{field}' stands in none of Filters, Rows and Columns of the cube's layout.", nameof(field));
        var placement = layout.PlacementOf(field) is { } at ? layout.PlacementsIn(at.Area)[at.Index] : new PivotFieldPlacement(field);
        var resolved = options ?? PivotOptions.Default;
        var labels = new ItemLabels(resolved);
        var meta = cube.Meta[field];
        var hidden = placement.HiddenItems.Select(ItemKey.FromPublic).ToHashSet();
        var order = new ItemOrder(meta, labels, resolved.Culture,
            placement.Sort.Direction == PivotSortDirection.Descending && placement.Sort.ByValue is null);
        return items.InOrderSeen
            .OrderBy(item => item, order)
            .Select(item => new PivotItemInfo(item.PublicKey, labels.Of(item, meta), hidden.Contains(item.Key)))
            .ToArray();
    }

    /// <summary>
    /// The Source Records behind one cell of a report — Show Details (ADR-0062) — in their order
    /// in the snapshot: those no Hidden Item leaves out, carrying the row's Items and the
    /// column's. A label cell is <paramref name="valueColumn"/> −1, and is every record of its
    /// row. A row that stands for no records — a Value Field's row — is its Item's.
    /// </summary>
    public static IReadOnlyList<TRecord> RecordsBehind<TRecord>(
        IReadOnlyList<TRecord> records,
        IReadOnlyList<PivotField<TRecord>> fields,
        PivotReport report,
        PivotReportRow row,
        int valueColumn)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(row);
        if (!ReferenceEquals(row.Report, report))
            throw new ArgumentException("The row belongs to another report.", nameof(row));
        if (valueColumn < -1 || valueColumn >= report.ValueColumns.Count)
            throw new ArgumentOutOfRangeException(nameof(valueColumn), valueColumn, "Not a value column of the report, nor −1.");

        var declared = Declared(fields);
        var layout = report.Layout;
        // What must match: the row's Items, the column's, and no Hidden Item anywhere.
        var conditions = new List<(Func<TRecord, object?> Value, ItemKey Key)>();
        foreach (var (field, key) in PathOf(row.Node, layout.Rows))
            conditions.Add((declared[field].Value, key));
        if (valueColumn >= 0)
        {
            foreach (var (field, key) in PathOf(report.ValueColumns[valueColumn].Node, layout.Columns))
                conditions.Add((declared[field].Value, key));
        }
        var hidden = layout.Rows.Concat(layout.Columns).Concat(layout.Filters)
            .Where(p => p.HiddenItems.Count > 0)
            .Select(p => (Value: declared[p.Field].Value, Keys: p.HiddenItems.Select(ItemKey.FromPublic).ToHashSet()))
            .ToArray();

        var behind = new List<TRecord>();
        foreach (var record in records)
        {
            var matches = true;
            foreach (var (value, key) in conditions)
            {
                if (!ItemKey.Of(value(record)).Equals(key))
                {
                    matches = false;
                    break;
                }
            }
            if (!matches)
                continue;
            foreach (var (value, keys) in hidden)
            {
                if (keys.Contains(ItemKey.Of(value(record))))
                {
                    matches = false;
                    break;
                }
            }
            if (matches)
                behind.Add(record);
        }
        return behind;
    }

    private static IEnumerable<(string Field, ItemKey Key)> PathOf(AxisNode node, IReadOnlyList<PivotFieldPlacement> placements)
    {
        for (var at = node; at.Item is not null; at = at.Parent!)
            yield return (placements[at.Level].Field, at.Item.Key);
    }

    private static Dictionary<string, PivotField<TRecord>> Declared<TRecord>(IReadOnlyList<PivotField<TRecord>> fields)
    {
        var declared = new Dictionary<string, PivotField<TRecord>>(fields.Count, StringComparer.Ordinal);
        foreach (var field in fields)
        {
            ArgumentNullException.ThrowIfNull(field, nameof(fields));
            if (!declared.TryAdd(field.Name, field))
                throw new ArgumentException($"Two Pivot Fields are named '{field.Name}'; a layout could not say which it means.", nameof(fields));
        }
        return declared;
    }

    private static void Validate<TField>(PivotLayout layout, IReadOnlyDictionary<string, TField> declared)
    {
        var standing = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (area, placements) in new[]
                 {
                     (PivotArea.Filters, layout.Filters), (PivotArea.Columns, layout.Columns), (PivotArea.Rows, layout.Rows),
                 })
        {
            foreach (var placement in placements)
            {
                ArgumentNullException.ThrowIfNull(placement, nameof(layout));
                if (!declared.ContainsKey(placement.Field))
                    throw new InvalidOperationException($"The layout places '{placement.Field}' in {area}, and no Pivot Field of that name is declared (ADR-0059).");
                if (!standing.Add(placement.Field))
                    throw new InvalidOperationException($"The layout places '{placement.Field}' twice among Filters, Rows and Columns; a field stands in one of them at most (ADR-0060).");
                if (placement.Sort.ByValue is { } byValue && (byValue < 0 || byValue >= layout.Values.Count))
                    throw new InvalidOperationException($"'{placement.Field}' is ordered by Value Field {byValue}, and the layout has {layout.Values.Count} (ADR-0059).");
                if (!Enum.IsDefined(placement.Sort.Direction))
                    throw new InvalidOperationException($"'{placement.Field}' has an unknown sort direction ({placement.Sort.Direction}).");
            }
        }
        foreach (var value in layout.Values)
        {
            ArgumentNullException.ThrowIfNull(value, nameof(layout));
            if (!declared.ContainsKey(value.Field))
                throw new InvalidOperationException($"The layout places '{value.Field}' in Values, and no Pivot Field of that name is declared (ADR-0059).");
            if (!Enum.IsDefined(value.Aggregation))
                throw new InvalidOperationException($"The Value Field of '{value.Field}' has an unknown Aggregation ({value.Aggregation}).");
            if (!Enum.IsDefined(value.ShowValuesAs))
                throw new InvalidOperationException($"The Value Field of '{value.Field}' has an unknown Show Values As ({value.ShowValuesAs}).");
            if (value.NumberFormat is { } format && PivotNumberFormat.Check(format) is { } problem)
                throw new InvalidOperationException($"The Value Field of '{value.Field}' has the number format '{format}', which {problem} (ADR-0059).");
        }
        if (!Enum.IsDefined(layout.ValuesAxis))
            throw new InvalidOperationException($"Unknown ValuesAxis ({layout.ValuesAxis}).");
        if (!Enum.IsDefined(layout.Form))
            throw new InvalidOperationException($"Unknown report form ({layout.Form}).");
    }
}

/// <summary>What a report is laid out in: the culture its labels and values are written in and
/// its text is ordered by, and the Consumer's words (ADR-0059).</summary>
public sealed record PivotOptions
{
    /// <summary>The invariant culture's report, in English.</summary>
    public static PivotOptions Default { get; } = new();

    /// <summary>The culture labels and values are formatted in, and text Items ordered by.</summary>
    public CultureInfo Culture { get; init; } = CultureInfo.InvariantCulture;

    /// <summary>The Consumer's word for an id of <see cref="PivotWords"/>, or null to keep the
    /// English.</summary>
    public Func<string, string?>? Label { get; init; }

    internal string Word(string id) => PivotWords.Resolve(id, Label);
}

/// <summary>One Item as Filter… lists it (ADR-0060).</summary>
/// <param name="Key">The Item's key, as a layout writes it.</param>
/// <param name="Label">What it is painted as.</param>
/// <param name="IsHidden">Whether the layout hides it now.</param>
public sealed record PivotItemInfo(PivotItemKey Key, string Label, bool IsHidden);
