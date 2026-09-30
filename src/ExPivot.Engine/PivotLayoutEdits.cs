namespace ExPivot.Engine;

/// <summary>A placed entry of an Area, as the Field List shows it (ADR-0060): a field in
/// Filters, Rows or Columns, a Value Field, or Σ Values, which stands last in its Area.</summary>
/// <param name="Area">The Area it stands in.</param>
/// <param name="Index">Its index in that Area's list; −1 for Σ Values.</param>
public readonly record struct PivotEntry(PivotArea Area, int Index)
{
    /// <summary>Σ Values, standing in the Area its axis names.</summary>
    public static PivotEntry ValuesPseudoField(PivotAxis axis)
        => new(axis == PivotAxis.Rows ? PivotArea.Rows : PivotArea.Columns, -1);

    /// <summary>Whether this is Σ Values.</summary>
    public bool IsValuesPseudoField => Index == -1;
}

/// <summary>Why the Field List refused an edit (ADR-0059/0060).</summary>
public enum PivotRefusal
{
    /// <summary>The edit would hide every Item of a field.</summary>
    HidesEveryItem = 0,

    /// <summary>Another Value Field, or a Pivot Field, already has the caption.</summary>
    CaptionTaken,

    /// <summary>A caption of a Value Field's own may not be empty.</summary>
    CaptionEmpty,

    /// <summary>The number format cannot be used (<see cref="PivotNumberFormat.Check"/>).</summary>
    NumberFormatInvalid,
}

/// <summary>An edit that may be refused: the layout it produced, or the unchanged layout and why
/// not.</summary>
/// <param name="Layout">The new layout, or the one given when refused.</param>
/// <param name="Refusal">Why the edit was refused, or null.</param>
public sealed record PivotEditResult(PivotLayout Layout, PivotRefusal? Refusal = null)
{
    /// <summary>Whether the edit was refused.</summary>
    public bool IsRefused => Refusal is not null;

    /// <summary>The id of the refusal's sentence in <see cref="PivotWords"/>, or null.</summary>
    public string? RefusalWord => Refusal switch
    {
        null => null,
        PivotRefusal.HidesEveryItem => "refused-hides-every-item",
        PivotRefusal.CaptionTaken => "refused-caption-taken",
        PivotRefusal.CaptionEmpty => "refused-caption-empty",
        _ => "refused-number-format",
    };
}

/// <summary>
/// What each Field List gesture means (ADR-0060), as pure functions from a layout to the next.
/// The component, a substituted Chrome and a server apply the same rules: a field stands at most
/// once across Filters, Rows and Columns and its settings travel with it; it may stand in Values
/// any number of times; Σ Values moves between Rows and Columns only and always stands last;
/// and an index is where the entry is inserted in the Area as it stands, the Area's count being
/// its end.
/// </summary>
public static class PivotLayoutEdits
{
    /// <summary>The Aggregation a new Value Field of a field takes: Sum for a field declared
    /// Number, Count for any other (ADR-0059).</summary>
    public static PivotAggregation DefaultAggregation(PivotFieldInfo field)
    {
        ArgumentNullException.ThrowIfNull(field);
        return field.Type == PivotFieldType.Number ? PivotAggregation.Sum : PivotAggregation.Count;
    }

    /// <summary>Ticking a field (ADR-0060): one standing nowhere goes to the end of Values when it
    /// is declared Number, and to the end of Rows otherwise. A field already standing somewhere
    /// is left where it is.</summary>
    public static PivotLayout Tick(PivotLayout layout, PivotFieldInfo field)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(field);
        if (layout.Places(field.Name))
            return layout;
        return field.Type == PivotFieldType.Number
            ? layout with { Values = [.. layout.Values, new PivotValueField(field.Name, DefaultAggregation(field))] }
            : layout with { Rows = [.. layout.Rows, new PivotFieldPlacement(field.Name)] };
    }

    /// <summary>Unticking a field: it leaves every Area it stands in, each of its Value Fields
    /// included (ADR-0060).</summary>
    public static PivotLayout Untick(PivotLayout layout, string field)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(field);
        var result = layout with
        {
            Filters = layout.Filters.Where(p => p.Field != field).ToArray(),
            Rows = layout.Rows.Where(p => p.Field != field).ToArray(),
            Columns = layout.Columns.Where(p => p.Field != field).ToArray(),
        };
        for (var i = result.Values.Count - 1; i >= 0; i--)
        {
            if (result.Values[i].Field == field)
                result = RemoveValue(result, i);
        }
        return Same(layout, result) ? layout : result;
    }

    /// <summary>A field dropped from the list of fields on an Area at <paramref name="index"/>
    /// (ADR-0060): in Values it is a new Value Field, whatever else it stands in; in Filters,
    /// Rows or Columns it is placed there, moving out of whichever of them it stood in, with its
    /// settings.</summary>
    public static PivotLayout Place(PivotLayout layout, PivotFieldInfo field, PivotArea area, int index)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(field);
        if (area == PivotArea.Values)
            return InsertValue(layout, new PivotValueField(field.Name, DefaultAggregation(field)), index);
        CheckArea(area);
        if (layout.PlacementOf(field.Name) is { } at)
            return Move(layout, new PivotEntry(at.Area, at.Index), area, index, field);
        return Insert(layout, area, new PivotFieldPlacement(field.Name), index);
    }

    /// <summary>
    /// A placed entry dropped on an Area at <paramref name="index"/> (ADR-0060). Within its own
    /// Area it is a reorder. A field moving between Filters, Rows and Columns keeps its settings;
    /// one moving to Values leaves its Area and becomes a Value Field; a Value Field moving to
    /// another Area leaves Values and its field is placed there. Σ Values moves between Rows and
    /// Columns only — anywhere else it stays — and always stands last.
    /// </summary>
    /// <param name="layout">The layout as it stands.</param>
    /// <param name="entry">What was dragged.</param>
    /// <param name="area">Where it was dropped.</param>
    /// <param name="index">Where in that Area, before the entry at this index as the Area stands;
    /// its count for the end.</param>
    /// <param name="field">The dragged entry's field — its declared type decides a new Value
    /// Field's Aggregation. Not read for Σ Values.</param>
    public static PivotLayout Move(PivotLayout layout, PivotEntry entry, PivotArea area, int index, PivotFieldInfo? field)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (entry.IsValuesPseudoField)
        {
            return area switch
            {
                PivotArea.Rows => layout.ValuesAxis == PivotAxis.Rows ? layout : layout with { ValuesAxis = PivotAxis.Rows },
                PivotArea.Columns => layout.ValuesAxis == PivotAxis.Columns ? layout : layout with { ValuesAxis = PivotAxis.Columns },
                _ => layout,
            };
        }
        ArgumentNullException.ThrowIfNull(field);

        if (entry.Area == PivotArea.Values)
        {
            CheckIndex(layout.Values.Count, entry.Index);
            var value = layout.Values[entry.Index];
            if (field.Name != value.Field)
                throw new ArgumentException($"The dragged Value Field is '{value.Field}', not '{field.Name}'.", nameof(field));
            if (area == PivotArea.Values)
                return MoveValue(layout, entry.Index, index);
            var withoutValue = RemoveValue(layout, entry.Index);
            if (withoutValue.PlacementOf(value.Field) is { } placed)
                return Move(withoutValue, new PivotEntry(placed.Area, placed.Index), area, index, field);
            return Insert(withoutValue, area, new PivotFieldPlacement(value.Field), index);
        }

        CheckArea(entry.Area);
        var from = layout.PlacementsIn(entry.Area);
        CheckIndex(from.Count, entry.Index);
        var placement = from[entry.Index];
        if (field.Name != placement.Field)
            throw new ArgumentException($"The dragged field is '{placement.Field}', not '{field.Name}'.", nameof(field));
        if (area == PivotArea.Values)
        {
            var without = Replace(layout, entry.Area, from.Where((_, i) => i != entry.Index).ToArray());
            return InsertValue(without, new PivotValueField(placement.Field, DefaultAggregation(field)), index);
        }
        CheckArea(area);
        if (area == entry.Area)
        {
            var target = Math.Clamp(index, 0, from.Count);
            if (target == entry.Index || target == entry.Index + 1)
                return layout;
            var reordered = from.ToList();
            reordered.RemoveAt(entry.Index);
            reordered.Insert(target > entry.Index ? target - 1 : target, placement);
            return Replace(layout, area, reordered);
        }
        var removed = Replace(layout, entry.Area, from.Where((_, i) => i != entry.Index).ToArray());
        return Insert(removed, area, placement, index);
    }

    /// <summary>Removing an entry — Remove Field, or dragging it back to the list of fields
    /// (ADR-0060). Σ Values cannot be removed: it leaves when fewer than two Value Fields
    /// remain.</summary>
    public static PivotLayout Remove(PivotLayout layout, PivotEntry entry)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (entry.IsValuesPseudoField)
            return layout;
        if (entry.Area == PivotArea.Values)
        {
            CheckIndex(layout.Values.Count, entry.Index);
            return RemoveValue(layout, entry.Index);
        }
        CheckArea(entry.Area);
        var from = layout.PlacementsIn(entry.Area);
        CheckIndex(from.Count, entry.Index);
        return Replace(layout, entry.Area, from.Where((_, i) => i != entry.Index).ToArray());
    }

    /// <summary>
    /// A field's Hidden Items, as Filter… applies them (ADR-0059/0060): refused when they would
    /// hide every one of <paramref name="items"/> — the field's Items in the snapshot — because a
    /// report of nothing reads as "no data". A key hidden that no Item carries is kept: it hides
    /// the Item if a later snapshot brings it.
    /// </summary>
    public static PivotEditResult SetHiddenItems(
        PivotLayout layout, string field, IEnumerable<PivotItemKey> hidden, IReadOnlyCollection<PivotItemKey> items)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(hidden);
        ArgumentNullException.ThrowIfNull(items);
        var set = hidden.Distinct().ToArray();
        var hiddenSet = set.ToHashSet();
        if (items.Count > 0 && items.All(hiddenSet.Contains))
            return new PivotEditResult(layout, PivotRefusal.HidesEveryItem);
        return new PivotEditResult(Update(layout, field, p => p with { HiddenItems = set }));
    }

    /// <summary>A field's Item order — Sort A to Z, Z to A, or by a Value Field.</summary>
    public static PivotLayout SetSort(PivotLayout layout, string field, PivotSort sort)
    {
        ArgumentNullException.ThrowIfNull(sort);
        if (sort.ByValue is { } byValue && (byValue < 0 || byValue >= layout.Values.Count))
            throw new ArgumentOutOfRangeException(nameof(sort), byValue, $"The layout has {layout.Values.Count} Value Fields.");
        return Update(layout, field, p => p.Sort == sort ? p : p with { Sort = sort });
    }

    /// <summary>A field's subtotals: Automatic (true) or None.</summary>
    public static PivotLayout SetSubtotals(PivotLayout layout, string field, bool subtotals)
        => Update(layout, field, p => p.Subtotals == subtotals ? p : p with { Subtotals = subtotals });

    /// <summary>One Item collapsed or expanded, wherever it appears (ADR-0059).</summary>
    public static PivotLayout SetCollapsed(PivotLayout layout, string field, PivotItemKey item, bool collapsed)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Update(layout, field, p =>
        {
            if (p.IsCollapsed(item) == collapsed)
                return p;
            var toggled = p.ToggledItems.Contains(item)
                ? p.ToggledItems.Where(key => !key.Equals(item)).ToArray()
                : [.. p.ToggledItems, item];
            return p with { ToggledItems = toggled };
        });
    }

    /// <summary>Expand or Collapse Entire Field: every Item of the field, with no exception.</summary>
    public static PivotLayout SetFieldCollapsed(PivotLayout layout, string field, bool collapsed)
        => Update(layout, field, p => p.Collapsed == collapsed && p.ToggledItems.Count == 0
            ? p
            : p with { Collapsed = collapsed, ToggledItems = [] });

    /// <summary>
    /// A Value Field's settings, as Value Field Settings… applies them (ADR-0059/0060): refused
    /// when its own caption is empty, or is another Value Field's caption or a Pivot Field's,
    /// or when its number format cannot be used.
    /// </summary>
    public static PivotEditResult SetValueField(
        PivotLayout layout, int index, PivotValueField value, IReadOnlyDictionary<string, PivotFieldInfo> fields)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(fields);
        CheckIndex(layout.Values.Count, index);
        if (value.Caption is { } caption)
        {
            if (caption.Trim().Length == 0)
                return new PivotEditResult(layout, PivotRefusal.CaptionEmpty);
            var others = layout.Values.Where((_, i) => i != index).ToArray();
            var otherCaptions = ValueCaptions.Resolve(others, fields);
            if (otherCaptions.Contains(caption, StringComparer.OrdinalIgnoreCase)
                || fields.Values.Any(f => string.Equals(f.Caption, caption, StringComparison.OrdinalIgnoreCase)))
                return new PivotEditResult(layout, PivotRefusal.CaptionTaken);
        }
        if (value.NumberFormat is { } format && PivotNumberFormat.Check(format) is not null)
            return new PivotEditResult(layout, PivotRefusal.NumberFormatInvalid);
        var values = layout.Values.ToArray();
        values[index] = value;
        return new PivotEditResult(layout with { Values = values });
    }

    // ---- The parts ------------------------------------------------------------------------

    private static PivotLayout Insert(PivotLayout layout, PivotArea area, PivotFieldPlacement placement, int index)
    {
        var list = layout.PlacementsIn(area).ToList();
        list.Insert(Math.Clamp(index, 0, list.Count), placement);
        return Replace(layout, area, list);
    }

    private static PivotLayout InsertValue(PivotLayout layout, PivotValueField value, int index)
    {
        var at = Math.Clamp(index, 0, layout.Values.Count);
        var values = layout.Values.ToList();
        values.Insert(at, value);
        // Value Fields at and after the insertion moved one along; an order by one follows it.
        return Remap(layout with { Values = values }, old => old >= at ? old + 1 : old);
    }

    private static PivotLayout MoveValue(PivotLayout layout, int from, int index)
    {
        var target = Math.Clamp(index, 0, layout.Values.Count);
        if (target == from || target == from + 1)
            return layout;
        var values = layout.Values.ToList();
        var moving = values[from];
        values.RemoveAt(from);
        var to = target > from ? target - 1 : target;
        values.Insert(to, moving);
        return Remap(layout with { Values = values }, old =>
        {
            if (old == from)
                return to;
            if (from < to && old > from && old <= to)
                return old - 1;
            if (to < from && old >= to && old < from)
                return old + 1;
            return old;
        });
    }

    private static PivotLayout RemoveValue(PivotLayout layout, int index)
    {
        var values = layout.Values.Where((_, i) => i != index).ToArray();
        // An order by the removed Value Field falls back to its label, in the same direction.
        return Remap(layout with { Values = values }, old => old == index ? null : old > index ? old - 1 : old);
    }

    private static PivotLayout Remap(PivotLayout layout, Func<int, int?> map)
    {
        PivotFieldPlacement[] Mapped(IReadOnlyList<PivotFieldPlacement> placements) => placements
            .Select(p => p.Sort.ByValue is { } old
                ? p with { Sort = new PivotSort(p.Sort.Direction, map(old)) }
                : p)
            .ToArray();
        return layout with { Filters = Mapped(layout.Filters), Rows = Mapped(layout.Rows), Columns = Mapped(layout.Columns) };
    }

    private static PivotLayout Update(PivotLayout layout, string field, Func<PivotFieldPlacement, PivotFieldPlacement> change)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(field);
        if (layout.PlacementOf(field) is not { } at)
            throw new ArgumentException($"'{field}' stands in none of Filters, Rows and Columns.", nameof(field));
        var placements = layout.PlacementsIn(at.Area).ToArray();
        var changed = change(placements[at.Index]);
        if (ReferenceEquals(changed, placements[at.Index]))
            return layout;
        placements[at.Index] = changed;
        return Replace(layout, at.Area, placements);
    }

    private static PivotLayout Replace(PivotLayout layout, PivotArea area, IReadOnlyList<PivotFieldPlacement> placements)
        => area switch
        {
            PivotArea.Filters => layout with { Filters = placements },
            PivotArea.Rows => layout with { Rows = placements },
            PivotArea.Columns => layout with { Columns = placements },
            _ => throw new ArgumentOutOfRangeException(nameof(area), area, "Filters, Rows or Columns."),
        };

    private static bool Same(PivotLayout one, PivotLayout other)
        => one.Filters.SequenceEqual(other.Filters) && one.Rows.SequenceEqual(other.Rows)
            && one.Columns.SequenceEqual(other.Columns) && one.Values.SequenceEqual(other.Values);

    private static void CheckArea(PivotArea area)
    {
        if (area is not (PivotArea.Filters or PivotArea.Rows or PivotArea.Columns))
            throw new ArgumentOutOfRangeException(nameof(area), area, "Filters, Rows or Columns.");
    }

    private static void CheckIndex(int count, int index)
    {
        if (index < 0 || index >= count)
            throw new ArgumentOutOfRangeException(nameof(index), index, $"The Area holds {count} entries.");
    }
}
