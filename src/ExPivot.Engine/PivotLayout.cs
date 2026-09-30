namespace ExPivot.Engine;

/// <summary>
/// An Item order (ADR-0059): by label, ascending or descending, or by a Value Field's value at
/// each Item's total. <c>(blank)</c> is last either way.
/// </summary>
/// <param name="Direction">Ascending or descending.</param>
/// <param name="ByValue">The index in <see cref="PivotLayout.Values"/> of the Value Field to
/// order by, or null to order by label.</param>
public sealed record PivotSort(PivotSortDirection Direction = PivotSortDirection.Ascending, int? ByValue = null)
{
    /// <summary>By label, A to Z — the default.</summary>
    public static PivotSort Ascending { get; } = new();

    /// <summary>By label, Z to A.</summary>
    public static PivotSort Descending { get; } = new(PivotSortDirection.Descending);
}

/// <summary>
/// A Pivot Field standing in Filters, Rows or Columns, with its settings (ADR-0059/0060): its
/// Item order, its Hidden Items, whether it has subtotals, and which Items are collapsed. The
/// settings travel with the field when it moves between those Areas.
/// </summary>
public sealed record PivotFieldPlacement
{
    private readonly IReadOnlyList<PivotItemKey> _hiddenItems = [];
    private readonly IReadOnlyList<PivotItemKey> _toggledItems = [];
    private readonly PivotSort _sort = PivotSort.Ascending;

    /// <summary>Places the field named <paramref name="field"/> with the default settings.</summary>
    public PivotFieldPlacement(string field)
    {
        ArgumentException.ThrowIfNullOrEmpty(field);
        Field = field;
    }

    /// <summary>The Pivot Field's name.</summary>
    public string Field { get; }

    /// <summary>The Item order; by label, ascending, unless set.</summary>
    public PivotSort Sort
    {
        get => _sort;
        init => _sort = value ?? throw new ArgumentNullException(nameof(Sort));
    }

    /// <summary>The Items the user unticked: every record carrying one is left out of the report,
    /// totals included (ADR-0059).</summary>
    public IReadOnlyList<PivotItemKey> HiddenItems
    {
        get => _hiddenItems;
        init => _hiddenItems = value ?? throw new ArgumentNullException(nameof(HiddenItems));
    }

    /// <summary>Whether an outer Item of this field has a subtotal — Excel's Automatic — or none.
    /// The innermost field of an axis never has one.</summary>
    public bool Subtotals { get; init; } = true;

    /// <summary>The field-wide collapse state, which Expand / Collapse Entire Field sets: true
    /// when every Item is collapsed except the <see cref="ToggledItems"/>.</summary>
    public bool Collapsed { get; init; }

    /// <summary>The Items whose collapse state is the opposite of <see cref="Collapsed"/> —
    /// collapsed ones while the field is expanded, and the other way round.</summary>
    public IReadOnlyList<PivotItemKey> ToggledItems
    {
        get => _toggledItems;
        init => _toggledItems = value ?? throw new ArgumentNullException(nameof(ToggledItems));
    }

    /// <summary>Whether the Item <paramref name="item"/> is collapsed.</summary>
    public bool IsCollapsed(PivotItemKey item) => Collapsed != ToggledItems.Contains(item);
}

/// <summary>
/// A Pivot Field standing in Values (ADR-0059): its Aggregation, its own caption when it has
/// one, how its values are shown, and the number format they are shown in.
/// </summary>
public sealed record PivotValueField
{
    /// <summary>Places the field named <paramref name="field"/> in Values, aggregated by
    /// <paramref name="aggregation"/>.</summary>
    public PivotValueField(string field, PivotAggregation aggregation = PivotAggregation.Sum)
    {
        ArgumentException.ThrowIfNullOrEmpty(field);
        Field = field;
        Aggregation = aggregation;
    }

    /// <summary>The Pivot Field's name.</summary>
    public string Field { get; }

    /// <summary>How the records at a cell are summarised.</summary>
    public PivotAggregation Aggregation { get; init; }

    /// <summary>A caption of the Value Field's own — Excel's Custom Name — or null for
    /// <c>&lt;Aggregation&gt; of &lt;field caption&gt;</c>.</summary>
    public string? Caption { get; init; }

    /// <summary>How the aggregated values are shown.</summary>
    public PivotShowValuesAs ShowValuesAs { get; init; }

    /// <summary>A .NET format string the values are shown in under the report's culture, or null
    /// for General — at most 15 significant digits, or <c>0.00%</c> for a percentage.</summary>
    public string? NumberFormat { get; init; }
}

/// <summary>
/// Which Pivot Fields stand in which Areas, in what order, with each one's settings, and the
/// report's form and totals (ADR-0058). It is ExPivot's View State: it holds no value, it is
/// serialisable (<see cref="PivotLayoutJson"/>), and the Consumer persists it. Immutable; every
/// change is a new instance, which is also the change signal.
/// </summary>
public sealed record PivotLayout
{
    private readonly IReadOnlyList<PivotFieldPlacement> _filters = [];
    private readonly IReadOnlyList<PivotFieldPlacement> _rows = [];
    private readonly IReadOnlyList<PivotFieldPlacement> _columns = [];
    private readonly IReadOnlyList<PivotValueField> _values = [];

    /// <summary>No field anywhere: the report is empty and asks for fields.</summary>
    public static PivotLayout Empty { get; } = new();

    /// <summary>The report filter's fields, in order.</summary>
    public IReadOnlyList<PivotFieldPlacement> Filters
    {
        get => _filters;
        init => _filters = value ?? throw new ArgumentNullException(nameof(Filters));
    }

    /// <summary>The row fields, outermost first.</summary>
    public IReadOnlyList<PivotFieldPlacement> Rows
    {
        get => _rows;
        init => _rows = value ?? throw new ArgumentNullException(nameof(Rows));
    }

    /// <summary>The column fields, outermost first.</summary>
    public IReadOnlyList<PivotFieldPlacement> Columns
    {
        get => _columns;
        init => _columns = value ?? throw new ArgumentNullException(nameof(Columns));
    }

    /// <summary>The Value Fields, in order.</summary>
    public IReadOnlyList<PivotValueField> Values
    {
        get => _values;
        init => _values = value ?? throw new ArgumentNullException(nameof(Values));
    }

    /// <summary>Where Σ Values stands when there are two or more Value Fields: innermost in
    /// Columns (the default, as Excel's) or in Rows (ADR-0059).</summary>
    public PivotAxis ValuesAxis { get; init; } = PivotAxis.Columns;

    /// <summary>How the row labels are set out.</summary>
    public PivotReportForm Form { get; init; } = PivotReportForm.Compact;

    /// <summary>Whether a subtotal is carried on its group row (true, Excel's default) or has
    /// a row of its own at the bottom. The Tabular form always puts it at the bottom.</summary>
    public bool SubtotalsAtTop { get; init; } = true;

    /// <summary>Whether the <c>Grand Total</c> row is painted at the bottom — Excel's "grand
    /// totals for columns".</summary>
    public bool GrandTotalRow { get; init; } = true;

    /// <summary>Whether the <c>Grand Total</c> column is painted at the right — Excel's "grand
    /// totals for rows".</summary>
    public bool GrandTotalColumn { get; init; } = true;

    /// <summary>Whether every row of the Outline and Tabular forms repeats its outer Items'
    /// labels.</summary>
    public bool RepeatItemLabels { get; init; }

    /// <summary>Whether the layout places no field at all — the report then asks for fields.</summary>
    public bool IsEmpty => Filters.Count == 0 && Rows.Count == 0 && Columns.Count == 0 && Values.Count == 0;

    /// <summary>The fields of <paramref name="area"/>; for <see cref="PivotArea.Values"/>, use
    /// <see cref="Values"/>.</summary>
    public IReadOnlyList<PivotFieldPlacement> PlacementsIn(PivotArea area) => area switch
    {
        PivotArea.Filters => Filters,
        PivotArea.Rows => Rows,
        PivotArea.Columns => Columns,
        PivotArea.Values => throw new ArgumentException("Values holds Value Fields; read PivotLayout.Values.", nameof(area)),
        _ => throw new ArgumentOutOfRangeException(nameof(area), area, "Unknown PivotArea."),
    };

    /// <summary>Where the field named <paramref name="field"/> stands among Filters, Rows and
    /// Columns, or null when it stands in none of them.</summary>
    public (PivotArea Area, int Index)? PlacementOf(string field)
    {
        ArgumentNullException.ThrowIfNull(field);
        foreach (var area in new[] { PivotArea.Filters, PivotArea.Columns, PivotArea.Rows })
        {
            var placements = PlacementsIn(area);
            for (var i = 0; i < placements.Count; i++)
            {
                if (placements[i].Field == field)
                    return (area, i);
            }
        }
        return null;
    }

    /// <summary>Whether the field stands anywhere, Values included — a ticked field.</summary>
    public bool Places(string field)
        => PlacementOf(field) is not null || Values.Any(value => value.Field == field);

    /// <summary>
    /// This layout without every field not in <paramref name="fieldNames"/> — for a Saved View
    /// written when the Consumer declared a field it no longer does. ExPivot refuses a layout
    /// naming an undeclared field by name rather than dropping it quietly (ADR-0059); a Consumer
    /// that expects old views calls this first, knowingly.
    /// </summary>
    public PivotLayout KeepingOnly(IEnumerable<string> fieldNames)
    {
        ArgumentNullException.ThrowIfNull(fieldNames);
        var known = fieldNames.ToHashSet(StringComparer.Ordinal);
        var layout = this;
        foreach (var name in Filters.Concat(Rows).Concat(Columns).Select(p => p.Field)
                     .Concat(Values.Select(v => v.Field)).Distinct(StringComparer.Ordinal).ToArray())
        {
            if (!known.Contains(name))
                layout = PivotLayoutEdits.Untick(layout, name);
        }
        return layout;
    }
}
