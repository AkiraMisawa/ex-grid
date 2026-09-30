namespace ExPivot.Engine;

/// <summary>
/// What ExPivot asks a Pivot Source to aggregate (ADR-0065): the row fields and the column
/// fields, in order; the report filter's fields; the Hidden Items of every placed field; for
/// each field in Values, the parts asked for; and the most leaves the answer may have. A
/// serialisable value (<see cref="PivotJson"/>), so it crosses to a server unchanged
/// (ADR-0002), and immutable. Two questions are equal when they ask the same thing.
/// </summary>
public sealed class PivotQuery : IEquatable<PivotQuery>
{
    /// <summary>The default cap on leaves (ADR-0065). Provisional: the Definition of Done's
    /// observational targets record the measurement that settles it.</summary>
    public const int DefaultMaxLeaves = 200_000;

    /// <summary>
    /// Asks one question. Refuses by name a question that could not be answered as asked: a
    /// field placed twice among Filters, Rows and Columns, a field asked for twice in Values,
    /// parts this version does not know, and a cap below one leaf.
    /// </summary>
    /// <param name="rows">The row fields, outermost first, each with its Hidden Items.</param>
    /// <param name="columns">The column fields, outermost first, each with its Hidden Items.</param>
    /// <param name="filters">The report filter's fields, each with its Hidden Items.</param>
    /// <param name="values">Each field in Values once, with the parts its Value Fields read.</param>
    /// <param name="maxLeaves">The most leaves the answer may have; a source that would need more
    /// refuses (ADR-0065).</param>
    public PivotQuery(
        IReadOnlyList<PivotQueryField>? rows = null,
        IReadOnlyList<PivotQueryField>? columns = null,
        IReadOnlyList<PivotQueryField>? filters = null,
        IReadOnlyList<PivotQueryValue>? values = null,
        int maxLeaves = DefaultMaxLeaves)
    {
        Rows = Copy(rows, nameof(rows));
        Columns = Copy(columns, nameof(columns));
        Filters = Copy(filters, nameof(filters));
        Values = Copy(values, nameof(values));
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLeaves, 1);
        MaxLeaves = maxLeaves;

        var placed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in Rows.Concat(Columns).Concat(Filters))
        {
            if (!placed.Add(field.Field))
                throw new ArgumentException($"The question places '{field.Field}' twice among Filters, Rows and Columns; a field stands in one of them at most (ADR-0060).");
        }
        var asked = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in Values)
        {
            if (!asked.Add(value.Field))
                throw new ArgumentException($"The question asks for '{value.Field}' twice in Values; ask once, with the parts of every Value Field of it (ADR-0065).");
        }
    }

    /// <summary>The row fields, outermost first, each with its Hidden Items.</summary>
    public IReadOnlyList<PivotQueryField> Rows { get; }

    /// <summary>The column fields, outermost first, each with its Hidden Items.</summary>
    public IReadOnlyList<PivotQueryField> Columns { get; }

    /// <summary>The report filter's fields, each with its Hidden Items. They make no leaves; a
    /// record carrying one of their Hidden Items is left out.</summary>
    public IReadOnlyList<PivotQueryField> Filters { get; }

    /// <summary>Each field in Values once, with the parts asked for.</summary>
    public IReadOnlyList<PivotQueryValue> Values { get; }

    /// <summary>The most leaves the answer may have.</summary>
    public int MaxLeaves { get; }

    /// <summary>Every placed field — the rows, the columns, then the report filter's.</summary>
    public IEnumerable<PivotQueryField> Placed => Rows.Concat(Columns).Concat(Filters);

    /// <summary>
    /// The question a Pivot Layout asks (ADR-0065): its rows, columns and report filter fields
    /// with their Hidden Items, and each field in Values once, with the parts of all of its Value
    /// Fields' Aggregations (<see cref="PartsOf"/>). Everything else in a layout only lays the
    /// answer out.
    /// </summary>
    public static PivotQuery For(PivotLayout layout, int maxLeaves = DefaultMaxLeaves)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var values = new List<PivotQueryValue>();
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var value in layout.Values)
        {
            ArgumentNullException.ThrowIfNull(value, nameof(layout));
            var parts = PartsOf(value.Aggregation);
            if (index.TryGetValue(value.Field, out var at))
            {
                values[at] = new PivotQueryValue(value.Field, values[at].Parts | parts);
            }
            else
            {
                index[value.Field] = values.Count;
                values.Add(new PivotQueryValue(value.Field, parts));
            }
        }
        return new PivotQuery(Placements(layout.Rows), Placements(layout.Columns), Placements(layout.Filters), values, maxLeaves);
    }

    /// <summary>
    /// The parts an Aggregation reads besides the counts (ADR-0065): Sum and Average the sum;
    /// Max and Min the extremes; Product the product; StdDev, StdDevp, Var and Varp the running
    /// variance; Count and Count Numbers nothing more.
    /// </summary>
    public static PivotParts PartsOf(PivotAggregation aggregation) => aggregation switch
    {
        PivotAggregation.Sum or PivotAggregation.Average => PivotParts.Sum,
        PivotAggregation.Max or PivotAggregation.Min => PivotParts.Extremes,
        PivotAggregation.Product => PivotParts.Product,
        PivotAggregation.StdDev or PivotAggregation.StdDevp or PivotAggregation.Var or PivotAggregation.Varp => PivotParts.Variance,
        PivotAggregation.Count or PivotAggregation.CountNumbers => PivotParts.Counts,
        _ => throw new ArgumentOutOfRangeException(nameof(aggregation), aggregation, "Unknown PivotAggregation."),
    };

    /// <summary>Whether <paramref name="field"/> stands among the question's Filters, Rows and
    /// Columns.</summary>
    public bool Places(string field) => Placed.Any(placed => placed.Field == field);

    /// <inheritdoc />
    public bool Equals(PivotQuery? other)
        => other is not null
            && (ReferenceEquals(this, other)
                || (MaxLeaves == other.MaxLeaves
                    && Rows.SequenceEqual(other.Rows)
                    && Columns.SequenceEqual(other.Columns)
                    && Filters.SequenceEqual(other.Filters)
                    && Values.SequenceEqual(other.Values)));

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as PivotQuery);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(MaxLeaves);
        foreach (var field in Placed)
            hash.Add(field);
        foreach (var value in Values)
            hash.Add(value);
        return hash.ToHashCode();
    }

    /// <summary>The fields and parts, for messages and logs.</summary>
    public override string ToString()
        => $"Rows [{string.Join(", ", Rows)}] Columns [{string.Join(", ", Columns)}] Filters [{string.Join(", ", Filters)}] "
            + $"Values [{string.Join(", ", Values)}] MaxLeaves {MaxLeaves}";

    private static PivotQueryField[] Placements(IReadOnlyList<PivotFieldPlacement> placements)
        => placements.Select(p => new PivotQueryField(p.Field, p.HiddenItems)).ToArray();

    internal static T[] Copy<T>(IReadOnlyList<T>? list, string name) where T : class
    {
        if (list is null)
            return [];
        var copy = list.ToArray();
        foreach (var item in copy)
        {
            if (item is null)
                throw new ArgumentNullException(name, $"'{name}' holds a null entry.");
        }
        return copy;
    }
}

/// <summary>
/// A placed field as a question carries it (ADR-0065): its name and its Hidden Items, written as
/// keys (ADR-0059). Equal when the names are and the Hidden Items are the same set.
/// </summary>
public sealed class PivotQueryField : IEquatable<PivotQueryField>
{
    /// <summary>Carries one placed field.</summary>
    /// <param name="field">The Pivot Field's name.</param>
    /// <param name="hiddenItems">Its Hidden Items; none when left out.</param>
    public PivotQueryField(string field, IReadOnlyList<PivotItemKey>? hiddenItems = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(field);
        Field = field;
        HiddenItems = PivotQuery.Copy(hiddenItems, nameof(hiddenItems));
    }

    /// <summary>The Pivot Field's name.</summary>
    public string Field { get; }

    /// <summary>The Items whose records are left out of the answer, totals included.</summary>
    public IReadOnlyList<PivotItemKey> HiddenItems { get; }

    /// <inheritdoc />
    public bool Equals(PivotQueryField? other)
        => other is not null && Field == other.Field && SameKeys(HiddenItems, other.HiddenItems);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as PivotQueryField);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Field, HiddenItems.Distinct().Count());

    /// <summary><c>Region</c>, or <c>Region (hides Text:West)</c>.</summary>
    public override string ToString()
        => HiddenItems.Count == 0 ? Field : $"{Field} (hides {string.Join(", ", HiddenItems)})";

    internal static bool SameKeys(IReadOnlyList<PivotItemKey> one, IReadOnlyList<PivotItemKey> other)
        => ReferenceEquals(one, other)
            || (one.Count == 0 && other.Count == 0)
            || one.ToHashSet().SetEquals(other);
}

/// <summary>A field in Values as a question carries it (ADR-0065): its name and the parts asked
/// for; the counts always travel.</summary>
public sealed record PivotQueryValue
{
    private const PivotParts Known = PivotParts.Sum | PivotParts.Extremes | PivotParts.Product | PivotParts.Variance;

    /// <summary>Asks for one field's parts.</summary>
    /// <param name="field">The Pivot Field's name.</param>
    /// <param name="parts">The parts asked for besides the counts.</param>
    public PivotQueryValue(string field, PivotParts parts = PivotParts.Counts)
    {
        ArgumentException.ThrowIfNullOrEmpty(field);
        if ((parts & ~Known) != 0)
            throw new ArgumentOutOfRangeException(nameof(parts), parts, $"Parts this version does not know are asked for '{field}'.");
        Field = field;
        Parts = parts;
    }

    /// <summary>The Pivot Field's name.</summary>
    public string Field { get; }

    /// <summary>The parts asked for besides the counts.</summary>
    public PivotParts Parts { get; }

    /// <summary><c>Amount: Sum, Extremes</c>.</summary>
    public override string ToString() => $"{Field}: {Parts}";
}

/// <summary>One Item of one field: a step of a cell's path (ADR-0065).</summary>
/// <param name="Field">The Pivot Field's name.</param>
/// <param name="Item">The Item, written as a key (ADR-0059).</param>
public sealed record PivotFieldItem(string Field, PivotItemKey Item)
{
    /// <summary>The Pivot Field's name.</summary>
    public string Field { get; } = !string.IsNullOrEmpty(Field) ? Field : throw new ArgumentException("A field is named.", nameof(Field));

    /// <summary>The Item.</summary>
    public PivotItemKey Item { get; } = Item ?? throw new ArgumentNullException(nameof(Item));

    /// <summary><c>Region=Text:East</c>.</summary>
    public override string ToString() => $"{Field}={Item}";
}

/// <summary>
/// What ExPivot asks a Pivot Source for a field's Items (ADR-0065): the field's Items over all the
/// data — not narrowed by other fields' Hidden Items — as Filter… and the report filter band list
/// them, under the Source Version the report was computed from. Immutable and serialisable.
/// </summary>
public sealed record PivotItemsQuery
{
    /// <summary>The most Items a page holds unless asked otherwise: what Filter… lists at once.</summary>
    public const int DefaultMax = 10_000;

    /// <summary>Asks for one field's Items.</summary>
    /// <param name="field">The Pivot Field's name.</param>
    /// <param name="sourceVersion">The Source Version the report was computed from; a source that
    /// can no longer answer under it refuses.</param>
    /// <param name="search">Only the Items whose invariant text contains this, ignoring case; null
    /// or empty for every Item.</param>
    /// <param name="max">The most Items the page holds; <see cref="PivotItemPage.Total"/> says how
    /// many there are.</param>
    public PivotItemsQuery(string field, string sourceVersion, string? search = null, int max = DefaultMax)
    {
        ArgumentException.ThrowIfNullOrEmpty(field);
        ArgumentNullException.ThrowIfNull(sourceVersion);
        ArgumentOutOfRangeException.ThrowIfNegative(max);
        Field = field;
        SourceVersion = sourceVersion;
        Search = string.IsNullOrEmpty(search) ? null : search;
        Max = max;
    }

    /// <summary>The Pivot Field's name.</summary>
    public string Field { get; }

    /// <summary>The Source Version the Items are asked under.</summary>
    public string SourceVersion { get; }

    /// <summary>What an Item's invariant text must contain, ignoring case; null for every Item.</summary>
    public string? Search { get; }

    /// <summary>The most Items the page holds.</summary>
    public int Max { get; }
}

/// <summary>
/// What ExPivot asks a Pivot Source for the records behind one cell — Show Details (ADR-0062/0065):
/// the Items along the cell's row path and column path, each shorter than the fields for a
/// subtotal and empty for a grand total; the Hidden Items of every placed field; a range of the
/// records, in the data's order; and the Source Version the report was computed from. Immutable
/// and serialisable.
/// </summary>
public sealed class PivotDetailsQuery : IEquatable<PivotDetailsQuery>
{
    /// <summary>Asks for one page of the records behind a cell.</summary>
    /// <param name="sourceVersion">The Source Version the report was computed from; a source that
    /// can no longer answer under it refuses.</param>
    /// <param name="rowItems">The row path's Items, outermost first.</param>
    /// <param name="columnItems">The column path's Items, outermost first.</param>
    /// <param name="hiddenItems">Every placed field that hides Items, with those Items.</param>
    /// <param name="start">The first record wanted, counted among the records behind the cell.</param>
    /// <param name="count">How many records are wanted from <paramref name="start"/>.</param>
    public PivotDetailsQuery(
        string sourceVersion,
        IReadOnlyList<PivotFieldItem>? rowItems = null,
        IReadOnlyList<PivotFieldItem>? columnItems = null,
        IReadOnlyList<PivotQueryField>? hiddenItems = null,
        int start = 0,
        int count = int.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(sourceVersion);
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        SourceVersion = sourceVersion;
        RowItems = PivotQuery.Copy(rowItems, nameof(rowItems));
        ColumnItems = PivotQuery.Copy(columnItems, nameof(columnItems));
        HiddenItems = PivotQuery.Copy(hiddenItems, nameof(hiddenItems));
        Start = start;
        Count = count;
        var named = new HashSet<string>(StringComparer.Ordinal);
        foreach (var step in RowItems.Concat(ColumnItems))
        {
            if (!named.Add(step.Field))
                throw new ArgumentException($"The cell's path names '{step.Field}' twice.");
        }
        var hiding = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in HiddenItems)
        {
            if (!hiding.Add(field.Field))
                throw new ArgumentException($"The question gives the Hidden Items of '{field.Field}' twice.");
        }
    }

    /// <summary>The Source Version the records are asked under.</summary>
    public string SourceVersion { get; }

    /// <summary>The row path's Items, outermost first.</summary>
    public IReadOnlyList<PivotFieldItem> RowItems { get; }

    /// <summary>The column path's Items, outermost first.</summary>
    public IReadOnlyList<PivotFieldItem> ColumnItems { get; }

    /// <summary>Every placed field that hides Items, with those Items.</summary>
    public IReadOnlyList<PivotQueryField> HiddenItems { get; }

    /// <summary>The first record wanted.</summary>
    public int Start { get; }

    /// <summary>How many records are wanted.</summary>
    public int Count { get; }

    /// <inheritdoc />
    public bool Equals(PivotDetailsQuery? other)
        => other is not null
            && (ReferenceEquals(this, other)
                || (SourceVersion == other.SourceVersion && Start == other.Start && Count == other.Count
                    && RowItems.SequenceEqual(other.RowItems)
                    && ColumnItems.SequenceEqual(other.ColumnItems)
                    && HiddenItems.SequenceEqual(other.HiddenItems)));

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as PivotDetailsQuery);

    /// <inheritdoc />
    public override int GetHashCode()
        => HashCode.Combine(SourceVersion, Start, Count, RowItems.Count, ColumnItems.Count, HiddenItems.Count);
}
