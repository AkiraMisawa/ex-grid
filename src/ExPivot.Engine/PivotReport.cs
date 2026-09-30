using System.Globalization;

namespace ExPivot.Engine;

/// <summary>
/// A Pivot Report (ADR-0058/0059): the rows, the label columns, the value columns and the
/// Header Group spans over them, laid out from a <see cref="PivotCube"/> under a
/// <see cref="PivotLayout"/>. Immutable. A value cell is computed when it is first read
/// (<see cref="PivotReportRow.ValueAt"/>), so a report of many rows costs its rows, not its
/// cells.
/// </summary>
public sealed class PivotReport
{
    private readonly CellReader _reader;

    internal PivotReport(
        PivotCube cube,
        PivotLayout layout,
        PivotOptions options,
        CellReader reader,
        IReadOnlyList<PivotLabelColumn> labelColumns,
        IReadOnlyList<PivotReportColumn> valueColumns,
        IReadOnlyList<PivotHeaderSpan> headerSpans,
        int headerTierCount,
        IReadOnlyList<PivotReportRow> rows)
    {
        Cube = cube;
        Layout = layout;
        Options = options;
        _reader = reader;
        LabelColumns = labelColumns;
        ValueColumns = valueColumns;
        HeaderSpans = headerSpans;
        HeaderTierCount = headerTierCount;
        Rows = rows;
        ValueCaptions = reader.Values.Select(v => v.Caption).ToArray();
        foreach (var row in rows)
            row.Attach(this);
    }

    /// <summary>The cube the report was laid out from.</summary>
    public PivotCube Cube { get; }

    /// <summary>The layout the report was laid out under.</summary>
    public PivotLayout Layout { get; }

    /// <summary>The culture and words it was laid out in.</summary>
    public PivotOptions Options { get; }

    /// <summary>The culture its labels and values are written in.</summary>
    public CultureInfo Culture => Options.Culture;

    /// <summary>Whether the layout places no row field, column field or Value Field — the report
    /// has nothing to show and asks for fields.</summary>
    public bool IsEmpty => Layout.Rows.Count == 0 && Layout.Columns.Count == 0 && Layout.Values.Count == 0;

    /// <summary>The label columns, leftmost first: one in the Compact form, one per row field
    /// otherwise, and one for the Value Fields' captions when they stand in rows. None when no
    /// row labels are shown.</summary>
    public IReadOnlyList<PivotLabelColumn> LabelColumns { get; }

    /// <summary>The value columns, left to right, after the label columns.</summary>
    public IReadOnlyList<PivotReportColumn> ValueColumns { get; }

    /// <summary>The rectangles above the value columns' headers — the column Items — each over
    /// a run of <see cref="ValueColumns"/> (ADR-0032).</summary>
    public IReadOnlyList<PivotHeaderSpan> HeaderSpans { get; }

    /// <summary>How many tiers stand above the leaf headers.</summary>
    public int HeaderTierCount { get; }

    /// <summary>The rows, top to bottom.</summary>
    public IReadOnlyList<PivotReportRow> Rows { get; }

    /// <summary>The Value Fields' captions, in the layout's order, each unique (ADR-0059).</summary>
    public IReadOnlyList<string> ValueCaptions { get; }

    /// <summary>
    /// Whether <paramref name="other"/> has the same sequence of rows — the same role, Value
    /// Field and Items at every position — so that a selection made on one names the same rows
    /// on the other (ADR-0011). A refresh that changes only values keeps it; an expand, a sort,
    /// a hidden Item or a new Item does not.
    /// </summary>
    public bool HasSameRowsAs(PivotReport? other)
    {
        if (other is null || other.Rows.Count != Rows.Count)
            return false;
        if (ReferenceEquals(other, this))
            return true;
        for (var i = 0; i < Rows.Count; i++)
        {
            var mine = Rows[i];
            var theirs = other.Rows[i];
            if (mine.Role != theirs.Role || mine.ValueField != theirs.ValueField || !SamePath(mine.Node, theirs.Node))
                return false;
        }
        return true;
    }

    private static bool SamePath(AxisNode one, AxisNode other)
    {
        while (true)
        {
            if (one.Level != other.Level)
                return false;
            if (one.Item is null)
                return other.Item is null;
            if (other.Item is null || !one.Item.Key.Equals(other.Item.Key))
                return false;
            one = one.Parent!;
            other = other.Parent!;
        }
    }

    /// <summary>The Items a row stands for, outermost first: each row field's name and Item.</summary>
    public IReadOnlyList<(string Field, PivotItemKey Item)> RowPath(PivotReportRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return Path(row.Node, Layout.Rows);
    }

    /// <summary>The Items a value column stands for, outermost first.</summary>
    public IReadOnlyList<(string Field, PivotItemKey Item)> ColumnPath(int valueColumn)
        => Path(ValueColumns[valueColumn].Node, Layout.Columns);

    private static IReadOnlyList<(string, PivotItemKey)> Path(AxisNode node, IReadOnlyList<PivotFieldPlacement> placements)
    {
        var path = new List<(string, PivotItemKey)>();
        for (var at = node; at.Item is not null; at = at.Parent!)
            path.Add((placements[at.Level].Field, at.Item.PublicKey));
        path.Reverse();
        return path;
    }

    /// <summary>
    /// The question for the records behind a cell — Show Details (ADR-0062/0065): the Items of the
    /// row's path and of the column's, the Hidden Items the report was computed under, a range, and
    /// the report's Source Version, under which the records add up to the cell. A label cell is
    /// <paramref name="valueColumn"/> −1, and asks for every record of its row. A row that stands
    /// for no records — a Value Field's row — asks for its Item's.
    /// </summary>
    /// <param name="row">A row of this report.</param>
    /// <param name="valueColumn">A value column's index, or −1 for the row's label cell.</param>
    /// <param name="start">The first record wanted.</param>
    /// <param name="count">How many records are wanted.</param>
    public PivotDetailsQuery DetailsQuery(PivotReportRow row, int valueColumn, int start = 0, int count = int.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (!ReferenceEquals(row.Report, this))
            throw new ArgumentException("The row belongs to another report.", nameof(row));
        if (valueColumn < -1 || valueColumn >= ValueColumns.Count)
            throw new ArgumentOutOfRangeException(nameof(valueColumn), valueColumn, "Not a value column of the report, nor −1.");
        var rowItems = RowPath(row).Select(step => new PivotFieldItem(step.Field, step.Item)).ToArray();
        var columnItems = valueColumn < 0
            ? []
            : ColumnPath(valueColumn).Select(step => new PivotFieldItem(step.Field, step.Item)).ToArray();
        var hidden = Cube.Query.Placed.Where(field => field.HiddenItems.Count > 0).ToArray();
        return new PivotDetailsQuery(Cube.SourceVersion, rowItems, columnItems, hidden, start, count);
    }

    /// <summary>The Value Field a cell of <paramref name="row"/> in <paramref name="valueColumn"/>
    /// shows, or −1 where no Value Field is placed.</summary>
    public int ValueFieldAt(PivotReportRow row, int valueColumn)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.ValueField >= 0)
            return row.ValueField;
        var column = ValueColumns[valueColumn];
        if (column.ValueField >= 0)
            return column.ValueField;
        return _reader.Values.Length == 1 ? 0 : -1;
    }

    internal PivotValue? Compute(PivotReportRow row, int valueColumn)
    {
        if (!row.CarriesValues)
            return null;
        var vf = ValueFieldAt(row, valueColumn);
        if (vf < 0)
            return null;
        var value = _reader.Shown(row.Node, ValueColumns[valueColumn].Node, vf);
        if (value.IsEmpty)
            return null;
        var plan = _reader.Values[vf];
        return PivotValue.From(value, plan.NumberFormat, plan.ShowValuesAs != PivotShowValuesAs.NoCalculation, Culture);
    }

    /// <summary>A Value Field's value at a row's total across the columns, as shown — what an
    /// order by that Value Field compares (ADR-0059). Null for an empty or error value.</summary>
    internal double? TotalOf(PivotReportRow row, int vf)
    {
        var value = _reader.Shown(row.Node, Cube.ColumnRoot, vf);
        return value.IsEmpty || value.IsError ? null : value.Number;
    }
}

/// <summary>A resolved Value Field: the cube's accumulation it reads, its Aggregation, its unique
/// caption, how it is shown and in what format.</summary>
internal sealed record ValueFieldPlan(
    int Source, PivotAggregation Aggregation, string Caption, PivotShowValuesAs ShowValuesAs, string? NumberFormat);

/// <summary>
/// One row of a Pivot Report (ADR-0059): what it stands for, its labels — one per label column —
/// and its value cells, computed when first read. The report is the row's owner; the row's
/// identity is the grid's change signal (ADR-0003), and a new report is new rows.
/// </summary>
public sealed class PivotReportRow
{
    private static readonly object NoValue = new();
    private object?[]? _cells;
    private PivotReport? _report;

    internal PivotReportRow(PivotRowRole role, AxisNode node, int valueField, bool carriesValues, PivotRowLabel[] labels)
    {
        Role = role;
        Node = node;
        ValueField = valueField;
        CarriesValues = carriesValues;
        Labels = labels;
    }

    /// <summary>What the row stands for.</summary>
    public PivotRowRole Role { get; }

    /// <summary>The Value Field the row's cells show when the values stand in rows, or −1.</summary>
    public int ValueField { get; }

    /// <summary>The row's labels, one per label column; a label with no text is an empty cell.</summary>
    public IReadOnlyList<PivotRowLabel> Labels { get; }

    /// <summary>Whether the row has value cells — false for a group row whose subtotal is at the
    /// bottom or off, and for an Item's row whose values stand in rows beneath it.</summary>
    public bool CarriesValues { get; }

    /// <summary>The report the row belongs to.</summary>
    public PivotReport Report => _report ?? throw new InvalidOperationException("The row has not been attached to its report.");

    internal AxisNode Node { get; }

    internal void Attach(PivotReport report) => _report = report;

    /// <summary>The value in <paramref name="valueColumn"/>, or null for an empty cell. Computed on
    /// the first read and kept.</summary>
    public PivotValue? ValueAt(int valueColumn)
    {
        var report = Report;
        _cells ??= new object?[report.ValueColumns.Count];
        var cell = _cells[valueColumn];
        if (cell is null)
        {
            cell = (object?)report.Compute(this, valueColumn) ?? NoValue;
            _cells[valueColumn] = cell;
        }
        return cell as PivotValue;
    }
}

/// <summary>One label cell of a row (ADR-0059).</summary>
/// <param name="Text">The label; null for an empty cell.</param>
/// <param name="Indent">How many levels the Compact form indents it.</param>
/// <param name="Toggle">The expand / collapse button beside it, for an outer Item.</param>
public sealed record PivotRowLabel(string? Text, int Indent = 0, PivotToggle? Toggle = null)
{
    /// <summary>An empty label cell.</summary>
    public static PivotRowLabel None { get; } = new((string?)null);
}

/// <summary>The expand / collapse button of an outer Item (ADR-0058): which field's Item it
/// toggles, how it is labelled, and whether it is collapsed now.</summary>
/// <param name="Field">The row field's name.</param>
/// <param name="Item">The Item, as a layout writes it.</param>
/// <param name="ItemLabel">The Item's label, for the button's accessible name.</param>
/// <param name="IsCollapsed">Whether it is collapsed.</param>
public sealed record PivotToggle(string Field, PivotItemKey Item, string ItemLabel, bool IsCollapsed);

/// <summary>A label column (ADR-0059).</summary>
/// <param name="Name">Unique among the report's columns, value columns included.</param>
/// <param name="Header">What its header says.</param>
/// <param name="Field">The row field whose Items it shows, or null for the Compact form's one
/// column and the Value Fields' captions.</param>
public sealed record PivotLabelColumn(string Name, string Header, string? Field);

/// <summary>A value column (ADR-0059): what it stands for and its leaf header.</summary>
public sealed class PivotReportColumn
{
    internal PivotReportColumn(string name, string header, PivotColumnRole role, int valueField, AxisNode node)
    {
        Name = name;
        Header = header;
        Role = role;
        ValueField = valueField;
        Node = node;
    }

    /// <summary>Unique among the report's columns and stable for the same Items, Value Field and
    /// role across reports — so a width the user gave it survives a refresh.</summary>
    public string Name { get; }

    /// <summary>The leaf header.</summary>
    public string Header { get; }

    /// <summary>What the column stands for.</summary>
    public PivotColumnRole Role { get; }

    /// <summary>The Value Field the column's cells show when the values stand in columns, or −1.</summary>
    public int ValueField { get; }

    internal AxisNode Node { get; }
}

/// <summary>A rectangle above the value columns' headers (ADR-0032/0059).</summary>
/// <param name="Label">Its caption: a column Item, <c>&lt;item&gt; Total</c> or <c>Grand Total</c>.</param>
/// <param name="FirstColumn">The first value column it covers, by index in
/// <see cref="PivotReport.ValueColumns"/>.</param>
/// <param name="ColumnCount">How many value columns it covers.</param>
/// <param name="Tier">Its top tier, 1 directly above the leaf headers.</param>
/// <param name="TierSpan">How many tiers it spans downward.</param>
public sealed record PivotHeaderSpan(string Label, int FirstColumn, int ColumnCount, int Tier, int TierSpan);
