using System.Globalization;

namespace ExPivot.Engine;

/// <summary>
/// A Pivot Report (ADR-0059/0060): the rows, the label columns, the value columns and the
/// Header Group spans over them, laid out from a <see cref="PivotCube"/> under a
/// <see cref="PivotLayout"/>. Immutable. A value cell is computed when it is first read
/// (<see cref="ValueAt"/>), so a report of many rows costs its rows, not its
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
        IReadOnlyList<PivotReportRow> rows,
        ReportLineage lineage)
    {
        Lineage = lineage;
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
    }

    /// <summary>The cube the report was laid out from.</summary>
    public PivotCube Cube { get; }

    // What this report's rows were made under: the layout, and the reports of it an update made
    // from this one or this one from (ADR-0153). Another report's row is refused, not read.
    internal ReportLineage Lineage { get; }

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

    /// <summary>The Value Fields' captions, in the layout's order, each unique (ADR-0060).</summary>
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
        return ReferenceEquals(other, this) || SameRows(other, 0, Rows.Count);
    }

    /// <summary>
    /// <see cref="HasSameRowsAs"/> in slices (ADR-0066, PV-40): the rows compared a piece at a time,
    /// the thread yielded whenever a slice of <see cref="PivotSlicing.Budget"/> is spent — a refresh
    /// of a large report compares every row. Cancelled, it throws at the next yield. A small report
    /// is compared without reading the clock, and the task is complete when it returns.
    /// </summary>
    /// <param name="other">The report to compare with, or null.</param>
    /// <param name="slicing">How the work shares the thread; <see cref="PivotSlicing.Default"/> when left out.</param>
    /// <param name="cancellationToken">Stops the work at the next yield.</param>
    public ValueTask<bool> HasSameRowsAsAsync(PivotReport? other, PivotSlicing? slicing = null, CancellationToken cancellationToken = default)
    {
        var slicer = Slicer.Of(slicing ?? PivotSlicing.Default, cancellationToken);
        if (other is null || other.Rows.Count != Rows.Count)
            return ValueTask.FromResult(false);
        return ReferenceEquals(other, this) ? ValueTask.FromResult(true) : SameRowsAsync(other, slicer);
    }

    private async ValueTask<bool> SameRowsAsync(PivotReport other, Slicer slicer)
    {
        var same = true;
        await slicer.ForAsync(Rows.Count, (from, to) => same = same && SameRows(other, from, to), weight: 2).ConfigureAwait(false);
        return same;
    }

    // Whether rows [from, to) stand for what the other report's rows there stand for.
    private bool SameRows(PivotReport other, int from, int to)
    {
        for (var i = from; i < to; i++)
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
    /// <exception cref="ArgumentException"><paramref name="row"/> is another report's row.</exception>
    public IReadOnlyList<(string Field, PivotItemKey Item)> RowPath(PivotReportRow row)
    {
        Own(row);
        return Path(row.Node, Layout.Rows, rows: true);
    }

    /// <summary>The Items a value column stands for, outermost first.</summary>
    public IReadOnlyList<(string Field, PivotItemKey Item)> ColumnPath(int valueColumn)
        => Path(ValueColumns[valueColumn].Node, Layout.Columns, rows: false);

    private IReadOnlyList<(string, PivotItemKey)> Path(AxisNode node, IReadOnlyList<PivotFieldPlacement> placements, bool rows)
    {
        var path = new List<(string, PivotItemKey)>();
        for (var at = node; at.Item is not null; at = at.Parent!)
            path.Add((placements[at.Level].Field, Cube.NodeOf(at, rows).Item!.PublicKey));
        path.Reverse();
        return path;
    }

    /// <summary>
    /// The question for the records behind a cell — Show Details (ADR-0063/0066): the Items of the
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
        if (!Rows.Any(held => ReferenceEquals(held, row)))
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
    /// <exception cref="ArgumentException"><paramref name="row"/> is another report's row.</exception>
    public int ValueFieldAt(PivotReportRow row, int valueColumn)
    {
        Own(row);
        if (row.ValueField >= 0)
            return row.ValueField;
        var column = ValueColumns[valueColumn];
        if (column.ValueField >= 0)
            return column.ValueField;
        return _reader.Values.Length == 1 ? 0 : -1;
    }

    /// <summary>
    /// The value of one of this report's rows in a value column, as this immutable report version
    /// shows it, computed when requested. A row this report shares with the versions it was made
    /// from, or that were made from it, under the same layout (ADR-0153), is this report's row too,
    /// and answers this version's value. A row of any other report — another layout, another
    /// computation — is refused: its Value Field and its axis node mean something else here, and
    /// reading them would answer another cell's figure as if it were this one's.
    /// </summary>
    /// <param name="row">A row of this report.</param>
    /// <param name="valueColumn">A value column's index.</param>
    /// <exception cref="ArgumentException"><paramref name="row"/> is another report's row; the
    /// message names its key.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="valueColumn"/> is not a value
    /// column of this report.</exception>
    public PivotValue? ValueAt(PivotReportRow row, int valueColumn)
    {
        Own(row);
        if ((uint)valueColumn >= (uint)ValueColumns.Count)
            throw new ArgumentOutOfRangeException(nameof(valueColumn));
        return Compute(row, valueColumn);
    }

    // A row of this report's lineage, or the refusal that names it. One reference comparison: a
    // report of hundreds of thousands of rows answers each cell without looking its row up.
    private void Own(PivotReportRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (!ReferenceEquals(row.Lineage, Lineage))
            throw new ArgumentException($"The row {row.Key} is not a row of this report: it was laid out by another report, " +
                "whose Value Fields and Items it names. Read a row through the report it came from.", nameof(row));
    }

    internal PivotReport WithCube(PivotCube cube)
        => new(cube, Layout, Options, new CellReader(cube, _reader.Values), LabelColumns, ValueColumns,
            HeaderSpans, HeaderTierCount, Rows, Lineage);

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
    /// order by that Value Field compares (ADR-0060). Null for an empty or error value.</summary>
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
/// One row of a Pivot Report (ADR-0060): what it stands for, its labels — one per label column —
/// and its labels. Unchanged rows are shared by immutable report versions (ADR-0153).
/// Read values through the report version; a structural row owns no report or cell cache.
/// </summary>
public sealed class PivotReportRow
{
    internal PivotReportRow(PivotRowRole role, AxisNode node, int valueField, bool carriesValues, PivotRowLabel[] labels,
        ReportLineage lineage)
    {
        Lineage = lineage;
        Role = role;
        Node = node;
        ValueField = valueField;
        CarriesValues = carriesValues;
        Labels = labels;
        Key = new PivotRowKey(role, valueField, node);
    }

    /// <summary>What the row stands for.</summary>
    public PivotRowRole Role { get; }

    /// <summary>What the row stands for, as a value equal across the reports of one layout
    /// (ADR-0140): its role, its Value Field and its Items. Made with the row; reading it costs
    /// nothing.</summary>
    public PivotRowKey Key { get; }

    /// <summary>The Value Field the row's cells show when the values stand in rows, or −1.</summary>
    public int ValueField { get; }

    /// <summary>The row's labels, one per label column; a label with no text is an empty cell.</summary>
    public IReadOnlyList<PivotRowLabel> Labels { get; }

    /// <summary>Whether the row has value cells — false for a group row whose subtotal is at the
    /// bottom or off, and for an Item's row whose values stand in rows beneath it.</summary>
    public bool CarriesValues { get; }

    internal AxisNode Node { get; }

    // The reports that may read this row: the one it was laid out for and the versions sharing it.
    internal ReportLineage Lineage { get; }
}

/// <summary>An identity, and nothing more: the rows a report builder made, and the reports made
/// of them, share one (<see cref="PivotReport.ValueAt"/>). It holds no report, cube or row, so a
/// row that outlives its report keeps nothing of it alive (ADR-0153).</summary>
internal sealed class ReportLineage;

/// <summary>One label cell of a row (ADR-0060).</summary>
/// <param name="Text">The label; null for an empty cell.</param>
/// <param name="Indent">How many levels the Compact form indents it.</param>
/// <param name="Toggle">The expand / collapse button beside it, for an outer Item.</param>
public sealed record PivotRowLabel(string? Text, int Indent = 0, PivotToggle? Toggle = null)
{
    /// <summary>An empty label cell.</summary>
    public static PivotRowLabel None { get; } = new((string?)null);
}

/// <summary>The expand / collapse button of an outer Item (ADR-0059): which field's Item it
/// toggles, how it is labelled, and whether it is collapsed now.</summary>
/// <param name="Field">The row field's name.</param>
/// <param name="Item">The Item, as a layout writes it.</param>
/// <param name="ItemLabel">The Item's label, for the button's accessible name.</param>
/// <param name="IsCollapsed">Whether it is collapsed.</param>
public sealed record PivotToggle(string Field, PivotItemKey Item, string ItemLabel, bool IsCollapsed);

/// <summary>A label column (ADR-0060).</summary>
/// <param name="Name">Unique among the report's columns, value columns included.</param>
/// <param name="Header">What its header says.</param>
/// <param name="Field">The row field whose Items it shows, or null for the Compact form's one
/// column and the Value Fields' captions.</param>
public sealed record PivotLabelColumn(string Name, string Header, string? Field);

/// <summary>A value column (ADR-0060): what it stands for and its leaf header.</summary>
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

/// <summary>A rectangle above the value columns' headers (ADR-0032/0060).</summary>
/// <param name="Label">Its caption: a column Item, <c>&lt;item&gt; Total</c> or <c>Grand Total</c>.</param>
/// <param name="FirstColumn">The first value column it covers, by index in
/// <see cref="PivotReport.ValueColumns"/>.</param>
/// <param name="ColumnCount">How many value columns it covers.</param>
/// <param name="Tier">Its top tier, 1 directly above the leaf headers.</param>
/// <param name="TierSpan">How many tiers it spans downward.</param>
public sealed record PivotHeaderSpan(string Label, int FirstColumn, int ColumnCount, int Tier, int TierSpan);
