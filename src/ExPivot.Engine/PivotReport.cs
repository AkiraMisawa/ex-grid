using System.Globalization;

namespace ExPivot.Engine;

/// <summary>
/// A Pivot Report (ADR-0059/0060): the rows, the label columns, the value columns and the
/// Header Group spans over them, laid out from a <see cref="PivotCube"/> under a
/// <see cref="PivotLayout"/>. Immutable. A value cell is asked of the report
/// (<see cref="ValueAt"/>), which computes it when it is first read and keeps it, so a report of
/// many rows costs its rows, not its cells; a row holds no value and no report (ADR-0161).
/// </summary>
public sealed class PivotReport
{
    private static readonly object NoValue = new();

    private readonly CellReader _reader;

    // The cells read so far, by row, each kept as its value or NoValue for an empty cell; and the
    // rows by what they stand for, made the first time a row is looked up by its key.
    private readonly Dictionary<PivotReportRow, object?[]> _cells = [];
    private Dictionary<PivotRowKey, PivotReportRow>? _rowsByKey;

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
        await slicer.ForAsync(Rows.Count, (from, to) => same = SameRows(other, from, to), weight: 2).ConfigureAwait(false);
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

    /// <summary>
    /// The value in <paramref name="valueColumn"/> of <paramref name="row"/>, or null for an empty
    /// cell (ADR-0161): computed on the first read, and kept by this report. A row is read by what it
    /// stands for in this report's cube, so a row this report shares with the report before it reads
    /// this report's value. A row of a report laid out from another answer is refused by name, never
    /// read as another cell.
    /// </summary>
    /// <param name="row">A row of this report.</param>
    /// <param name="valueColumn">A value column's index in <see cref="ValueColumns"/>.</param>
    public PivotValue? ValueAt(PivotReportRow row, int valueColumn)
    {
        ArgumentNullException.ThrowIfNull(row);
        if ((uint)valueColumn >= (uint)ValueColumns.Count)
            throw new ArgumentOutOfRangeException(nameof(valueColumn), valueColumn, $"The report has {ValueColumns.Count} value columns.");
        if (!_cells.TryGetValue(row, out var cells))
        {
            if (!Holds(row))
                throw new ArgumentException("The row is not of this report's answer: it was laid out from another (ADR-0161).", nameof(row));
            cells = new object?[ValueColumns.Count];
            _cells[row] = cells;
        }
        var cell = cells[valueColumn];
        if (cell is null)
        {
            cell = (object?)Compute(row, valueColumn) ?? NoValue;
            cells[valueColumn] = cell;
        }
        return cell as PivotValue;
    }

    // Whether the row's Items are a node of this report's cube, and its Value Field one of the
    // report's: what reading its cells needs.
    private bool Holds(PivotReportRow row)
    {
        if (row.ValueField >= ValueCaptions.Count)
            return false;
        var node = row.Node;
        while (node.Parent is { } parent)
            node = parent;
        return ReferenceEquals(node, Cube.RowRoot);
    }

    /// <summary>
    /// This report's row that stands for what <paramref name="key"/> names — the same role, Value
    /// Field and Items — or null when it has none (ADR-0161). A row of another report of the layout
    /// finds the row standing for the same thing here. The rows are indexed by their keys the first
    /// time one is looked up.
    /// </summary>
    public PivotReportRow? RowFor(PivotRowKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (_rowsByKey is null)
        {
            var rows = new Dictionary<PivotRowKey, PivotReportRow>(Rows.Count);
            foreach (var row in Rows)
                rows.TryAdd(row.Key, row);
            _rowsByKey = rows;
        }
        return _rowsByKey.TryGetValue(key, out var found) ? found : null;
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
    /// The question for the records behind a cell — Show Details (ADR-0063/0066): the Items of the
    /// row's path and of the column's, the Hidden Items the report was computed under, a range, and
    /// the report's Source Version, under which the records add up to the cell. A label cell is
    /// <paramref name="valueColumn"/> −1, and asks for every record of its row. A row that stands
    /// for no records — a Value Field's row — asks for its Item's.
    /// </summary>
    /// <param name="row">A row of this report, or one that stands for a row of it (<see cref="RowFor"/>).</param>
    /// <param name="valueColumn">A value column's index, or −1 for the row's label cell.</param>
    /// <param name="start">The first record wanted.</param>
    /// <param name="count">How many records are wanted.</param>
    public PivotDetailsQuery DetailsQuery(PivotReportRow row, int valueColumn, int start = 0, int count = int.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(row);
        row = RowFor(row.Key) ?? throw new ArgumentException("The row stands for no row of this report.", nameof(row));
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

    private PivotValue? Compute(PivotReportRow row, int valueColumn)
        => ShownAt(row, valueColumn) is var (value, plan) ? ValueOf(value, plan) : null;

    // A cell's value as shown and the plan that formats it; null for an empty cell.
    private (AggregateValue Value, ValueFieldPlan Plan)? ShownAt(PivotReportRow row, int valueColumn)
    {
        if (!row.CarriesValues)
            return null;
        var vf = ValueFieldAt(row, valueColumn);
        if (vf < 0)
            return null;
        var value = _reader.Shown(row.Node, ValueColumns[valueColumn].Node, vf);
        return value.IsEmpty ? null : (value, _reader.Values[vf]);
    }

    private PivotValue ValueOf(AggregateValue value, ValueFieldPlan plan)
        => PivotValue.From(value, plan.NumberFormat, plan.ShowValuesAs != PivotShowValuesAs.NoCalculation, Culture);

    // ---- What changed since an earlier report (ADR-0067/0161) ----------------------------------------

    /// <summary>
    /// What changed in the painted values since <paramref name="earlier"/>, a report of the same
    /// layout (ADR-0067/0068/0161) — what the Change Highlight marks: every value cell whose painted
    /// text differs from the earlier report's cell that stands for the same row and column, the rows
    /// and the value columns the earlier report has not, whose every cell is new, and the rows and
    /// columns that left. Rows are paired by their keys and columns by their names, so a row a sort by
    /// value moved is compared with itself; a change the number format hides is no change.
    /// </summary>
    public PivotReportChanges ChangesSince(PivotReport earlier)
    {
        ArgumentNullException.ThrowIfNull(earlier);
        return Slicer.Run(ChangesSinceAsync(earlier, Slicer.Unsliced));
    }

    /// <summary>
    /// <see cref="ChangesSince"/> in slices (PV-40): the rows compared a piece at a time, the thread
    /// yielded whenever a slice of <see cref="PivotSlicing.Budget"/> is spent — a redraw laid out
    /// afresh compares every row. Cancelled, it throws at the next yield. A small report is compared
    /// without reading the clock, and the task is complete when it returns.
    /// </summary>
    /// <param name="earlier">The report to compare with.</param>
    /// <param name="slicing">How the work shares the thread; <see cref="PivotSlicing.Default"/> when left out.</param>
    /// <param name="cancellationToken">Stops the work at the next yield.</param>
    public ValueTask<PivotReportChanges> ChangesSinceAsync(PivotReport earlier, PivotSlicing? slicing = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(earlier);
        return ChangesSinceAsync(earlier, Slicer.Of(slicing ?? PivotSlicing.Default, cancellationToken));
    }

    private async ValueTask<PivotReportChanges> ChangesSinceAsync(PivotReport earlier, Slicer slicer)
    {
        // The value columns by name: where they stood, or found among the earlier report's.
        var columns = new int[ValueColumns.Count];
        var newColumns = new List<int>();
        var pairedColumns = new bool[earlier.ValueColumns.Count];
        Dictionary<string, int>? earlierColumns = null;
        for (var c = 0; c < columns.Length; c++)
        {
            var name = ValueColumns[c].Name;
            int k;
            if (c < earlier.ValueColumns.Count && string.Equals(earlier.ValueColumns[c].Name, name, StringComparison.Ordinal))
            {
                k = c;
            }
            else
            {
                earlierColumns ??= earlier.ValueColumns.Select((column, at) => (column.Name, at)).ToDictionary(p => p.Name, p => p.at, StringComparer.Ordinal);
                k = earlierColumns.TryGetValue(name, out var found) ? found : -1;
            }
            columns[c] = k;
            if (k < 0)
                newColumns.Add(c);
            else
                pairedColumns[k] = true;
        }

        // The rows by key: where they stood, as almost every row does, or found among the earlier
        // report's, indexed the first time one is not where it stood.
        var newRows = new List<int>();
        var cells = new List<(int Row, int Column)>();
        Dictionary<PivotRowKey, int>? earlierRows = null;
        bool[]? pairedRows = null;
        await slicer.ForAsync(Rows.Count, (from, to) =>
        {
            for (var i = from; i < to; i++)
            {
                var row = Rows[i];
                int j;
                if (i < earlier.Rows.Count && (ReferenceEquals(earlier.Rows[i], row) || earlier.Rows[i].Key.Equals(row.Key)))
                {
                    j = i;
                }
                else
                {
                    if (earlierRows is null)
                    {
                        earlierRows = new Dictionary<PivotRowKey, int>(earlier.Rows.Count);
                        for (var e = 0; e < earlier.Rows.Count; e++)
                            earlierRows.TryAdd(earlier.Rows[e].Key, e);
                        // Every row before this one stood where it stood.
                        pairedRows = new bool[earlier.Rows.Count];
                        pairedRows.AsSpan(0, Math.Min(i, pairedRows.Length)).Fill(true);
                    }
                    j = earlierRows.TryGetValue(row.Key, out var found) ? found : -1;
                }
                if (j < 0)
                {
                    newRows.Add(i);
                    continue;
                }
                if (pairedRows is not null)
                    pairedRows[j] = true;
                var theirs = earlier.Rows[j];
                for (var c = 0; c < columns.Length; c++)
                {
                    if (columns[c] >= 0 && !SameText(row, c, earlier, theirs, columns[c]))
                        cells.Add((i, c));
                }
            }
        }, weight: 1 + (2 * columns.Length)).ConfigureAwait(false);

        var leftRows = new List<PivotRowKey>();
        for (var j = pairedRows is null ? Rows.Count : 0; j < earlier.Rows.Count; j++)
        {
            if (pairedRows is null || !pairedRows[j])
                leftRows.Add(earlier.Rows[j].Key);
        }
        var leftColumns = new List<string>();
        for (var k = 0; k < pairedColumns.Length; k++)
        {
            if (!pairedColumns[k])
                leftColumns.Add(earlier.ValueColumns[k].Name);
        }
        return new PivotReportChanges([.. newRows], [.. newColumns], [.. cells], [.. leftRows], [.. leftColumns]);
    }

    // Whether a cell paints the same text as a cell of the earlier report. Two values alike to the
    // last bit, formatted alike, paint alike, so only cells whose values differ are formatted.
    private bool SameText(PivotReportRow row, int column, PivotReport earlier, PivotReportRow theirRow, int theirColumn)
    {
        var mine = ShownAt(row, column);
        var theirs = earlier.ShownAt(theirRow, theirColumn);
        if (mine is null && theirs is null)
            return true;
        if (mine is { } a && theirs is { } b && SameValue(a.Value, b.Value) && SameFormat(a.Plan, b.Plan) && Equals(Culture, earlier.Culture))
            return true;
        var text = mine is { } m ? ValueOf(m.Value, m.Plan).Text : "";
        var theirText = theirs is { } t ? earlier.ValueOf(t.Value, t.Plan).Text : "";
        return string.Equals(text, theirText, StringComparison.Ordinal);
    }

    private static bool SameValue(AggregateValue a, AggregateValue b)
        => a.IsEmpty == b.IsEmpty
            && string.Equals(a.Error, b.Error, StringComparison.Ordinal)
            && BitConverter.DoubleToInt64Bits(a.Number) == BitConverter.DoubleToInt64Bits(b.Number)
            && (a.Exact is { } x ? b.Exact is { } y && decimal.GetBits(x).AsSpan().SequenceEqual(decimal.GetBits(y)) : b.Exact is null);

    private static bool SameFormat(ValueFieldPlan a, ValueFieldPlan b)
        => string.Equals(a.NumberFormat, b.NumberFormat, StringComparison.Ordinal)
            && (a.ShowValuesAs == PivotShowValuesAs.NoCalculation) == (b.ShowValuesAs == PivotShowValuesAs.NoCalculation);

    /// <summary>A Value Field's value at a row's total across the columns, as shown — what an
    /// order by that Value Field compares (ADR-0060). Null for an empty or error value.</summary>
    internal double? TotalOf(PivotReportRow row, int vf)
    {
        var value = _reader.Shown(row.Node, Cube.ColumnRoot, vf);
        return value.IsEmpty || value.IsError ? null : value.Number;
    }
}

/// <summary>
/// What changed in a report's painted values since an earlier report of the same layout
/// (<see cref="PivotReport.ChangesSince"/>, ADR-0067/0161): what the Change Highlight marks. Rows and
/// columns are named by their positions in the later report, and those that left by the earlier
/// report's keys and names.
/// </summary>
public sealed class PivotReportChanges
{
    internal PivotReportChanges(int[] newRows, int[] newColumns, (int Row, int Column)[] cells, PivotRowKey[] leftRows, string[] leftColumns)
    {
        NewRows = newRows;
        NewColumns = newColumns;
        Cells = cells;
        LeftRows = leftRows;
        LeftColumns = leftColumns;
    }

    /// <summary>The rows the earlier report has no row for, by index: every cell of each is new.</summary>
    public IReadOnlyList<int> NewRows { get; }

    /// <summary>The value columns the earlier report has no column for, by index: every cell of each
    /// is new.</summary>
    public IReadOnlyList<int> NewColumns { get; }

    /// <summary>The value cells of rows and columns both reports have whose painted text differs, by
    /// row index and value column index, row by row.</summary>
    public IReadOnlyList<(int Row, int Column)> Cells { get; }

    /// <summary>The keys of the earlier report's rows this report has not.</summary>
    public IReadOnlyList<PivotRowKey> LeftRows { get; }

    /// <summary>The names of the earlier report's value columns this report has not.</summary>
    public IReadOnlyList<string> LeftColumns { get; }

    /// <summary>Whether nothing changed: no cell, no row and no column.</summary>
    public bool IsEmpty => NewRows.Count == 0 && NewColumns.Count == 0 && Cells.Count == 0 && LeftRows.Count == 0 && LeftColumns.Count == 0;
}

/// <summary>A resolved Value Field: the cube's accumulation it reads, its Aggregation, its unique
/// caption, how it is shown and in what format.</summary>
internal sealed record ValueFieldPlan(
    int Source, PivotAggregation Aggregation, string Caption, PivotShowValuesAs ShowValuesAs, string? NumberFormat);

/// <summary>
/// One row of a Pivot Report (ADR-0060/0161): what it stands for — its role, its Value Field, its
/// Items, which make its key — and its labels, one per label column. It holds no value and no
/// report: a value cell is asked of a report (<see cref="PivotReport.ValueAt"/>). The row's identity
/// is the grid's change signal (ADR-0003), and a next report shares the rows it did not change.
/// </summary>
public sealed class PivotReportRow
{
    internal PivotReportRow(PivotRowRole role, AxisNode node, int valueField, bool carriesValues, PivotRowLabel[] labels)
    {
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
}

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
