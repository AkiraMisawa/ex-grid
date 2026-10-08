using System.Globalization;
using System.Text;

namespace ExPivot.Engine;

/// <summary>
/// Reads a Value Field's value where a row node and a column node cross, as aggregated and as
/// shown (ADR-0060). Shared by the report's cells and by an order by value, so the order and
/// the cells cannot disagree.
/// </summary>
internal sealed class CellReader(PivotCube cube, ValueFieldPlan[] values)
{
    public ValueFieldPlan[] Values => values;

    public AggregateValue Read(AxisNode row, AxisNode column, int vf)
    {
        var plan = values[vf];
        return cube.Read(row, column, plan.Source, plan.Aggregation);
    }

    public AggregateValue Shown(AxisNode row, AxisNode column, int vf)
    {
        var value = Read(row, column, vf);
        var plan = values[vf];
        if (plan.ShowValuesAs == PivotShowValuesAs.NoCalculation || value.IsEmpty || value.IsError)
            return value;
        var divisor = plan.ShowValuesAs switch
        {
            PivotShowValuesAs.PercentOfGrandTotal => Read(cube.RowRoot, cube.ColumnRoot, vf),
            PivotShowValuesAs.PercentOfColumnTotal => Read(cube.RowRoot, column, vf),
            _ => Read(row, cube.ColumnRoot, vf),
        };
        if (divisor.IsEmpty || divisor.IsError || divisor.Number == 0)
            return AggregateValue.DivideByZero;
        var shown = value.Number / divisor.Number;
        return double.IsFinite(shown) ? AggregateValue.Of(shown) : AggregateValue.NumberError;
    }
}

/// <summary>
/// Lays a cube out under a layout (ADR-0060): the value columns and their Header Group spans
/// from the column tree, the rows from the row tree in the layout's form, each in its Items'
/// order, with subtotals, grand totals, collapsed Items and Σ Values where the layout puts them.
/// <para>
/// Each tree is walked depth first, in its Items' order — each node entered, its children walked,
/// then left — and the walk can stop after any step and go on from there, so that a report is laid
/// out in slices (<see cref="BuildAsync"/>, PV-40) by the very steps that lay it out at once
/// (<see cref="Build"/>). A node's children are ordered a piece at a time too: thousands of
/// siblings are as long a piece of work as thousands of rows.
/// </para>
/// </summary>
internal sealed class ReportBuilder
{
    // What a step of the walk is worth, in the units the slicer counts: a node entered or left,
    // each row it emits, and each column, whose name is written from its Items.
    private const int RowUnits = 4;
    private const int ColumnUnits = 8;

    private readonly PivotCube _cube;
    private readonly PivotLayout _layout;
    private readonly PivotOptions _options;
    private readonly CultureInfo _culture;
    private readonly ItemLabels _labels;
    private readonly CellReader _reader;
    private readonly ValueFieldPlan[] _values;
    private readonly PivotFieldPlacement[] _rowPlacements;
    private readonly PivotFieldPlacement[] _columnPlacements;
    private readonly FieldMeta[] _rowMeta;
    private readonly FieldMeta[] _columnMeta;
    private readonly CollapseState[] _rowCollapse;
    private readonly CollapseState[] _columnCollapse;
    private readonly bool _valuesOnRows;
    private readonly bool _valuesOnColumns;
    private readonly int _labelColumnCount;
    private readonly AxisNode?[] _pending;

    // The rows this builder makes, and the report it hands them to, share one lineage: a report
    // answers the values of its own rows, and refuses another report's (PivotReport.ValueAt). An
    // update of a report continues its lineage, so the rows the versions share stay answerable.
    private readonly ReportLineage _lineage;

    public ReportBuilder(PivotCube cube, PivotLayout layout, PivotOptions options, ReportLineage? lineage = null)
    {
        _lineage = lineage ?? new ReportLineage();
        _cube = cube;
        _layout = layout;
        _options = options;
        _culture = options.Culture;
        _labels = new ItemLabels(options);
        _rowPlacements = [.. layout.Rows];
        _columnPlacements = [.. layout.Columns];
        _rowMeta = _rowPlacements.Select(p => cube.Meta[p.Field]).ToArray();
        _columnMeta = _columnPlacements.Select(p => cube.Meta[p.Field]).ToArray();
        _rowCollapse = _rowPlacements.Select(p => new CollapseState(p)).ToArray();
        _columnCollapse = _columnPlacements.Select(p => new CollapseState(p)).ToArray();

        var captions = ValueCaptions.Resolve(layout.Values, cube.Meta.ToDictionary(p => p.Key, p => p.Value.Info), options);
        _values = layout.Values
            .Select((value, i) => new ValueFieldPlan(
                Array.IndexOf(cube.Sources, value.Field), value.Aggregation, captions[i], value.ShowValuesAs, value.NumberFormat))
            .ToArray();
        _reader = new CellReader(cube, _values);
        _valuesOnRows = _values.Length >= 2 && layout.ValuesAxis == PivotAxis.Rows;
        _valuesOnColumns = _values.Length >= 2 && layout.ValuesAxis == PivotAxis.Columns;
        _labelColumnCount = LabelColumnCount();
        _pending = new AxisNode?[_rowPlacements.Length];
    }

    private int RowLevels => _rowPlacements.Length;

    private int ColumnLevels => _columnPlacements.Length;

    private int ValueCount => _values.Length;

    private bool Empty => _layout.Rows.Count == 0 && _layout.Columns.Count == 0 && _layout.Values.Count == 0;

    private int LabelColumnCount()
    {
        if (Empty)
            return 0;
        if (_layout.Form == PivotReportForm.Compact)
            return RowLevels > 0 || _valuesOnRows ? 1 : 0;
        return RowLevels + (_valuesOnRows ? 1 : 0);
    }

    /// <summary>The report, laid out at once: the steps of <see cref="BuildAsync"/>, never
    /// yielding.</summary>
    public PivotReport Build() => Slicer.Run(BuildAsync(Slicer.Unsliced));

    /// <summary>The report, laid out in slices (PV-40): the value columns and their spans, then the
    /// rows, each tree walked a step at a time, and the rows handed to the report.</summary>
    public async ValueTask<PivotReport> BuildAsync(Slicer slicer)
    {
        var columns = new List<PivotReportColumn>();
        var spans = new List<PivotHeaderSpan>();
        var rows = new List<PivotReportRow>();
        if (!Empty)
        {
            await EmitColumnsAsync(columns, spans, slicer).ConfigureAwait(false);
            await EmitRowsAsync(rows, slicer).ConfigureAwait(false);
        }
        var tiers = ColumnLevels == 0 ? 0 : _valuesOnColumns ? ColumnLevels : ColumnLevels - 1;
        var report = new PivotReport(_cube, _layout, _options, _reader, LabelColumns(), columns, spans, tiers, rows, _lineage);
        return report;
    }

    private PivotReportRow NewRow(PivotRowRole role, AxisNode node, int valueField, bool carriesValues, PivotRowLabel[] labels)
        => new(role, node, valueField, carriesValues, labels, _lineage);

    // The same layout rules used by a complete walk, applied to one affected axis node.
    // Tabular pending labels always form a suffix of the ancestor path: a later sibling
    // starts a new suffix, while a first child inherits its parent's pending ancestors.
    internal (PivotReportRow[] Before, PivotReportRow[] After, bool Descend) RowsOf(AxisNode node, int pendingFrom)
    {
        var before = new List<PivotReportRow>();
        var after = new List<PivotReportRow>();
        if (Empty) return ([], [], false);
        if (node.Item is null)
        {
            if (RowLevels == 0)
            {
                Slicer.Run(EmitRowsAsync(before, Slicer.Unsliced));
                return ([.. before], [], false);
            }
            if (_layout.GrandTotalRow && ValueCount > 0)
                for (var vf = _valuesOnRows ? 0 : -1; vf < (_valuesOnRows ? ValueCount : 0); vf++)
                    after.Add(NewRow(PivotRowRole.GrandTotal, node, vf, true, GrandTotalLabels(vf)));
            return ([], [.. after], true);
        }
        Array.Clear(_pending);
        for (var level = pendingFrom; level < node.Level; level++)
            _pending[level] = AncestorAt(node, level);
        var tabular = _layout.Form == PivotReportForm.Tabular;
        var descend = (tabular ? EnterTabular(node, before) : EnterGrouped(node, before)) >= 0;
        Array.Clear(_pending);
        if (descend)
        {
            if (tabular) LeaveTabular(node, after);
            else LeaveGrouped(node, after);
        }
        return ([.. before], [.. after], descend);
    }

    internal async ValueTask<List<AxisNode>> OrderedChildrenAsync(AxisNode node, Slicer slicer)
    {
        var order = Order(node, rows: true);
        while (true)
        {
            var budget = Slicer.PieceUnits;
            var done = order.Step(ref budget);
            if (slicer.Done(Slicer.PieceUnits - budget)) await slicer.PauseAsync().ConfigureAwait(false);
            if (done) return order.Result!;
        }
    }

    internal async ValueTask<PivotReport> WithRowsAsync(PivotReport previous, IReadOnlyList<PivotReportRow> rows,
        bool columnsChanged, Slicer slicer)
    {
        if (!columnsChanged)
            return new(_cube, _layout, _options, _reader, previous.LabelColumns, previous.ValueColumns,
                previous.HeaderSpans, previous.HeaderTierCount, rows, _lineage);
        var columns = new List<PivotReportColumn>();
        var spans = new List<PivotHeaderSpan>();
        if (!Empty) await EmitColumnsAsync(columns, spans, slicer).ConfigureAwait(false);
        var old = previous.ValueColumns.ToDictionary(c => c.Name);
        for (var i = 0; i < columns.Count; i++)
            if (old.TryGetValue(columns[i].Name, out var held) && held.Header == columns[i].Header)
                columns[i] = held;
        return new(_cube, _layout, _options, _reader, previous.LabelColumns, columns, spans,
            previous.HeaderTierCount, rows, _lineage);
    }

    private string Word(string id) => _options.Word(id);

    private string Label(AxisNode node, FieldMeta meta) => _labels.Of(node.Item!, meta);

    private List<PivotLabelColumn> LabelColumns()
    {
        var columns = new List<PivotLabelColumn>(_labelColumnCount);
        if (_labelColumnCount == 0)
            return columns;
        if (_layout.Form == PivotReportForm.Compact)
        {
            columns.Add(new PivotLabelColumn("row-labels", Word(PivotWords.RowLabels), null));
            return columns;
        }
        for (var i = 0; i < RowLevels; i++)
            columns.Add(new PivotLabelColumn("row:" + _rowPlacements[i].Field, _rowMeta[i].Info.Caption, _rowPlacements[i].Field));
        if (_valuesOnRows)
            columns.Add(new PivotLabelColumn("row:#values", Word(PivotWords.Values), null));
        return columns;
    }

    // ---- The walk ------------------------------------------------------------------------

    /// <summary>
    /// Walks the tree under <paramref name="root"/> depth first, children in their order: each node
    /// entered (<paramref name="enter"/>, which answers −1 when its children are not walked, or a
    /// mark it is left with), its children walked, then left (<paramref name="leave"/>). The root is
    /// entered and left too. It stops after a step whenever the slice is spent, and goes on from
    /// there once the thread was yielded.
    /// </summary>
    private async ValueTask WalkAsync(
        AxisNode root, bool rows, Func<AxisNode, int> enter, Action<AxisNode, int> leave, Func<int> emitted, int unitsEach, Slicer slicer)
    {
        var walk = new Walk(this, root, rows, enter, leave, emitted, unitsEach);
        while (!walk.Run(slicer))
            await slicer.PauseAsync().ConfigureAwait(false);
    }

    private sealed class Walk(
        ReportBuilder builder, AxisNode root, bool rows, Func<AxisNode, int> enter, Action<AxisNode, int> leave, Func<int> emitted, int unitsEach)
    {
        private Frame[] _frames = [new Frame { Node = root }];
        private int _depth = 1;

        /// <summary>Walks on until the tree is done — true — or the slice is spent after a step —
        /// false, to be run again once the thread was yielded.</summary>
        public bool Run(Slicer slicer)
        {
            while (_depth > 0)
            {
                ref var top = ref _frames[_depth - 1];
                var before = emitted();
                var units = 1;
                if (top.Children is not null)
                {
                    if (top.Next < top.Children.Count)
                    {
                        Push(top.Children[top.Next++]);
                        continue;
                    }
                    leave(top.Node, top.Mark);
                    _depth--;
                }
                else if (top.Ordering is { } ordering)
                {
                    var budget = Slicer.PieceUnits;
                    if (ordering.Step(ref budget))
                    {
                        top.Children = ordering.Result;
                        top.Ordering = null;
                    }
                    units = Slicer.PieceUnits - budget;
                }
                else
                {
                    var mark = enter(top.Node);
                    if (mark < 0)
                        _depth--;
                    else
                    {
                        top.Mark = mark;
                        top.Ordering = builder.Order(top.Node, rows);
                    }
                }
                if (slicer.Done(units + ((emitted() - before) * unitsEach)))
                    return false;
            }
            return true;
        }

        private void Push(AxisNode node)
        {
            if (_depth == _frames.Length)
                Array.Resize(ref _frames, _frames.Length * 2);
            _frames[_depth++] = new Frame { Node = node };
        }

        private struct Frame
        {
            public AxisNode Node;
            public int Mark;
            public ChildOrder? Ordering;
            public List<AxisNode>? Children;
            public int Next;
        }
    }

    // ---- Columns -------------------------------------------------------------------------

    private int TierOf(int level) => _valuesOnColumns ? ColumnLevels - level : ColumnLevels - 1 - level;

    private async ValueTask EmitColumnsAsync(List<PivotReportColumn> columns, List<PivotHeaderSpan> spans, Slicer slicer)
    {
        var root = _cube.ColumnRoot;
        if (ColumnLevels == 0)
        {
            if (ValueCount == 0)
                return;
            if (_valuesOnColumns)
            {
                for (var vf = 0; vf < ValueCount; vf++)
                    columns.Add(Column(PivotColumnRole.Item, root, vf, _values[vf].Caption));
            }
            else
            {
                // One value column: the one Value Field's caption, or "Values" when several stand
                // in rows and the column shows each in turn.
                columns.Add(Column(PivotColumnRole.Item, root, -1,
                    ValueCount == 1 ? _values[0].Caption : Word(PivotWords.Values)));
            }
            return;
        }

        await WalkAsync(root, rows: false,
            node => EnterColumn(node, columns, spans),
            (node, start) => LeaveColumn(node, start, columns, spans),
            () => columns.Count + spans.Count, ColumnUnits, slicer).ConfigureAwait(false);

        if (_layout.GrandTotalColumn && ValueCount > 0)
        {
            var label = Word(PivotWords.GrandTotal);
            if (_valuesOnColumns)
            {
                var first = columns.Count;
                for (var vf = 0; vf < ValueCount; vf++)
                    columns.Add(Column(PivotColumnRole.GrandTotal, root, vf, _values[vf].Caption));
                var top = TierOf(0);
                spans.Add(new PivotHeaderSpan(label, first, ValueCount, top, top));
            }
            else
            {
                columns.Add(Column(PivotColumnRole.GrandTotal, root, -1, label));
            }
        }
    }

    // A column Item: an innermost or collapsed one emits its columns, and its children are not
    // walked; any other is left with the first column under it. The root's children are walked.
    private int EnterColumn(AxisNode node, List<PivotReportColumn> columns, List<PivotHeaderSpan> spans)
    {
        if (node.Item is null)
            return 0;
        var level = node.Level;
        var innermost = level == ColumnLevels - 1;
        var collapsed = !innermost && _columnCollapse[level].IsCollapsed(node.Item.Key);
        if (!innermost && !collapsed)
            return columns.Count;
        var label = Label(node, _columnMeta[level]);
        var tier = TierOf(level);
        if (_valuesOnColumns)
        {
            var first = columns.Count;
            for (var vf = 0; vf < ValueCount; vf++)
                columns.Add(Column(PivotColumnRole.Item, node, vf, _values[vf].Caption));
            // The Item over its Value Fields' captions: one tier for an innermost Item, and
            // down to the captions for a collapsed one, which has no levels below it.
            spans.Add(new PivotHeaderSpan(label, first, ValueCount, tier, tier));
        }
        else
        {
            // Its own leaf header; for a collapsed Item it stretches up to its parent's
            // rectangle (ADR-0032).
            columns.Add(Column(PivotColumnRole.Item, node, -1, label));
        }
        return -1;
    }

    // An outer column Item, its children walked: its rectangle over them, then its subtotal.
    private void LeaveColumn(AxisNode node, int start, List<PivotReportColumn> columns, List<PivotHeaderSpan> spans)
    {
        if (node.Item is null)
            return;
        var level = node.Level;
        var label = Label(node, _columnMeta[level]);
        var tier = TierOf(level);
        spans.Add(new PivotHeaderSpan(label, start, columns.Count - start, tier, 1));

        if (_columnPlacements[level].Subtotals && ValueCount > 0)
        {
            var total = PivotWords.Fill(Word(PivotWords.ItemTotal), label);
            if (_valuesOnColumns)
            {
                var first = columns.Count;
                for (var vf = 0; vf < ValueCount; vf++)
                    columns.Add(Column(PivotColumnRole.Subtotal, node, vf, _values[vf].Caption));
                spans.Add(new PivotHeaderSpan(total, first, ValueCount, tier, tier));
            }
            else
            {
                columns.Add(Column(PivotColumnRole.Subtotal, node, -1, total));
            }
        }
    }

    private PivotReportColumn Column(PivotColumnRole role, AxisNode node, int vf, string header)
        => new(ColumnName(role, node, vf), header, role, vf, node);

    // A column's name is its role, its Items and its Value Field's caption — stable across
    // reports for the same column, so a width the user gave it survives a refresh (ADR-0016).
    private string ColumnName(PivotColumnRole role, AxisNode node, int vf)
    {
        var path = new List<string>();
        for (var at = node; at.Item is not null; at = at.Parent!)
        {
            var key = at.Item.PublicKey;
            var text = key.Kind == PivotItemKind.Text ? key.Value!.ToUpperInvariant() : key.Value ?? "";
            path.Add(((int)key.Kind).ToString(CultureInfo.InvariantCulture) + ":" + Uri.EscapeDataString(text));
        }
        path.Reverse();
        var name = new StringBuilder("v:").Append(role switch
        {
            PivotColumnRole.Item => 'i',
            PivotColumnRole.Subtotal => 's',
            _ => 'g',
        });
        foreach (var part in path)
            name.Append('/').Append(part);
        if (vf >= 0)
            name.Append('#').Append(Uri.EscapeDataString(_values[vf].Caption));
        return name.ToString();
    }

    // ---- Rows ----------------------------------------------------------------------------

    private async ValueTask EmitRowsAsync(List<PivotReportRow> rows, Slicer slicer)
    {
        var root = _cube.RowRoot;
        if (RowLevels == 0)
        {
            if (ValueCount == 0)
                return;
            if (_valuesOnRows)
            {
                for (var vf = 0; vf < ValueCount; vf++)
                {
                    var labels = NewLabels();
                    labels[_labelColumnCount - 1] = new PivotRowLabel(_values[vf].Caption);
                    rows.Add(NewRow(PivotRowRole.Item, root, vf, carriesValues: true, labels));
                }
            }
            else
            {
                rows.Add(NewRow(PivotRowRole.GrandTotal, root, -1, carriesValues: true, NewLabels()));
            }
            return;
        }

        if (_layout.Form == PivotReportForm.Tabular)
            await WalkAsync(root, rows: true, node => EnterTabular(node, rows), (node, _) => LeaveTabular(node, rows), () => rows.Count, RowUnits, slicer).ConfigureAwait(false);
        else
            await WalkAsync(root, rows: true, node => EnterGrouped(node, rows), (node, _) => LeaveGrouped(node, rows), () => rows.Count, RowUnits, slicer).ConfigureAwait(false);

        if (_layout.GrandTotalRow && ValueCount > 0)
        {
            if (_valuesOnRows)
            {
                for (var vf = 0; vf < ValueCount; vf++)
                    rows.Add(NewRow(PivotRowRole.GrandTotal, root, vf, carriesValues: true, GrandTotalLabels(vf)));
            }
            else
            {
                rows.Add(NewRow(PivotRowRole.GrandTotal, root, -1, carriesValues: true, GrandTotalLabels(-1)));
            }
        }
    }

    private PivotRowLabel[] NewLabels()
    {
        var labels = new PivotRowLabel[_labelColumnCount];
        Array.Fill(labels, PivotRowLabel.None);
        return labels;
    }

    private PivotRowLabel[] GrandTotalLabels(int vf)
    {
        var labels = NewLabels();
        if (_layout.Form == PivotReportForm.Compact)
        {
            labels[0] = new PivotRowLabel(vf < 0
                ? Word(PivotWords.GrandTotal)
                : PivotWords.Fill(Word(PivotWords.TotalOf), _values[vf].Caption));
            return labels;
        }
        labels[0] = new PivotRowLabel(Word(PivotWords.GrandTotal));
        if (vf >= 0)
            labels[_labelColumnCount - 1] = new PivotRowLabel(_values[vf].Caption);
        return labels;
    }

    private PivotToggle? ToggleOf(AxisNode node)
    {
        var level = node.Level;
        if (level >= RowLevels - 1)
            return null;
        return new PivotToggle(_rowPlacements[level].Field, node.Item!.PublicKey, Label(node, _rowMeta[level]),
            _rowCollapse[level].IsCollapsed(node.Item.Key));
    }

    private AxisNode AncestorAt(AxisNode node, int level)
    {
        var at = node;
        while (at.Level > level)
            at = at.Parent!;
        return _cube.NodeOf(at, rows: true);
    }

    // The Compact and Outline forms: a group row heads each outer Item's block (ADR-0060). An
    // innermost or collapsed Item is its rows, and its children are not walked; an outer one is
    // its group row, then its children, then — when they stand at the bottom — its subtotals.
    private int EnterGrouped(AxisNode node, List<PivotReportRow> rows)
    {
        if (node.Item is null)
            return 0;
        var level = node.Level;
        var innermost = level == RowLevels - 1;
        var collapsed = !innermost && _rowCollapse[level].IsCollapsed(node.Item.Key);
        if (innermost || collapsed)
        {
            if (_valuesOnRows)
            {
                rows.Add(NewRow(PivotRowRole.Group, node, -1, carriesValues: false, ItemLabels(node)));
                for (var vf = 0; vf < ValueCount; vf++)
                    rows.Add(NewRow(PivotRowRole.Item, node, vf, carriesValues: true, CaptionLabels(node, vf)));
            }
            else
            {
                rows.Add(NewRow(innermost ? PivotRowRole.Item : PivotRowRole.Group, node, -1,
                    carriesValues: true, ItemLabels(node)));
            }
            return -1;
        }

        var (_, atTop) = SubtotalsOf(level);
        rows.Add(NewRow(PivotRowRole.Group, node, -1, carriesValues: atTop, ItemLabels(node)));
        return 0;
    }

    private void LeaveGrouped(AxisNode node, List<PivotReportRow> rows)
    {
        if (node.Item is null)
            return;
        var (subtotals, atTop) = SubtotalsOf(node.Level);
        if (!subtotals || atTop)
            return;
        if (_valuesOnRows)
        {
            for (var vf = 0; vf < ValueCount; vf++)
                rows.Add(NewRow(PivotRowRole.Subtotal, node, vf, carriesValues: true, SubtotalLabels(node, vf)));
        }
        else
        {
            rows.Add(NewRow(PivotRowRole.Subtotal, node, -1, carriesValues: true, SubtotalLabels(node, -1)));
        }
    }

    // Whether an outer Item of a level has subtotals, and whether they stand on its group row.
    private (bool Subtotals, bool AtTop) SubtotalsOf(int level)
    {
        var subtotals = _rowPlacements[level].Subtotals && ValueCount > 0;
        return (subtotals, subtotals && _layout.SubtotalsAtTop && !_valuesOnRows);
    }

    // An Item's own row: its label where its form puts it, the button beside an outer one, and
    // its outer Items' labels too when they are repeated.
    private PivotRowLabel[] ItemLabels(AxisNode node)
    {
        var labels = NewLabels();
        var level = node.Level;
        var label = Label(node, _rowMeta[level]);
        if (_layout.Form == PivotReportForm.Compact)
        {
            labels[0] = new PivotRowLabel(label, level, ToggleOf(node));
            return labels;
        }
        labels[level] = new PivotRowLabel(label, 0, ToggleOf(node));
        RepeatOuter(labels, node, level - 1);
        return labels;
    }

    private PivotRowLabel[] CaptionLabels(AxisNode node, int vf)
    {
        var labels = NewLabels();
        if (_layout.Form == PivotReportForm.Compact)
        {
            labels[0] = new PivotRowLabel(_values[vf].Caption, node.Level + 1);
            return labels;
        }
        labels[_labelColumnCount - 1] = new PivotRowLabel(_values[vf].Caption);
        RepeatOuter(labels, node, node.Level);
        return labels;
    }

    private PivotRowLabel[] SubtotalLabels(AxisNode node, int vf)
    {
        var labels = NewLabels();
        var level = node.Level;
        var label = Label(node, _rowMeta[level]);
        if (_layout.Form == PivotReportForm.Compact)
        {
            labels[0] = new PivotRowLabel(vf < 0
                ? PivotWords.Fill(Word(PivotWords.ItemTotal), label)
                : PivotWords.Fill(Word(PivotWords.ItemValue), label, _values[vf].Caption), level);
            return labels;
        }
        labels[level] = new PivotRowLabel(PivotWords.Fill(Word(PivotWords.ItemTotal), label));
        if (vf >= 0)
            labels[_labelColumnCount - 1] = new PivotRowLabel(_values[vf].Caption);
        RepeatOuter(labels, node, level - 1);
        return labels;
    }

    private void RepeatOuter(PivotRowLabel[] labels, AxisNode node, int upToLevel)
    {
        if (!_layout.RepeatItemLabels)
            return;
        for (var level = 0; level <= upToLevel; level++)
        {
            var ancestor = AncestorAt(node, level);
            labels[level] = new PivotRowLabel(Label(ancestor, _rowMeta[level]));
        }
    }

    // The Tabular form: no group rows — an outer Item's label stands on the first row of its
    // block, and its subtotal at the bottom (ADR-0060).
    private int EnterTabular(AxisNode node, List<PivotReportRow> rows)
    {
        if (node.Item is null)
            return 0;
        var level = node.Level;
        _pending[level] = node;
        var innermost = level == RowLevels - 1;
        var collapsed = !innermost && _rowCollapse[level].IsCollapsed(node.Item.Key);
        if (!innermost && !collapsed)
            return 0;
        var role = innermost ? PivotRowRole.Item : PivotRowRole.Group;
        if (_valuesOnRows)
        {
            for (var vf = 0; vf < ValueCount; vf++)
            {
                var labels = TabularLabels(node, level);
                labels[_labelColumnCount - 1] = new PivotRowLabel(_values[vf].Caption);
                rows.Add(NewRow(role, node, vf, carriesValues: true, labels));
            }
        }
        else
        {
            rows.Add(NewRow(role, node, -1, carriesValues: true, TabularLabels(node, level)));
        }
        return -1;
    }

    private void LeaveTabular(AxisNode node, List<PivotReportRow> rows)
    {
        if (node.Item is null)
            return;
        var level = node.Level;
        if (!_rowPlacements[level].Subtotals || ValueCount == 0)
            return;
        var total = PivotWords.Fill(Word(PivotWords.ItemTotal), Label(node, _rowMeta[level]));
        for (var vf = _valuesOnRows ? 0 : -1; vf < (_valuesOnRows ? ValueCount : 0); vf++)
        {
            var labels = TabularLabels(node, level - 1);
            labels[level] = new PivotRowLabel(total);
            if (vf >= 0)
                labels[_labelColumnCount - 1] = new PivotRowLabel(_values[vf].Caption);
            rows.Add(NewRow(PivotRowRole.Subtotal, node, vf, carriesValues: true, labels));
        }
    }

    private PivotRowLabel[] TabularLabels(AxisNode node, int upToLevel)
    {
        var labels = NewLabels();
        for (var level = 0; level <= upToLevel; level++)
        {
            var ancestor = AncestorAt(node, level);
            if (ReferenceEquals(_pending[level], ancestor))
            {
                labels[level] = new PivotRowLabel(Label(ancestor, _rowMeta[level]), 0, ToggleOf(ancestor));
                _pending[level] = null;
            }
            else if (_layout.RepeatItemLabels)
            {
                labels[level] = new PivotRowLabel(Label(ancestor, _rowMeta[level]));
            }
        }
        return labels;
    }

    // ---- Order ---------------------------------------------------------------------------

    /// <summary>
    /// The children of <paramref name="node"/> in their order (ADR-0060), to be made a step at a
    /// time. By a Value Field's value at each Item's total across the other axis, as shown; blank
    /// and error values last, ties by label ascending — the Order Key does not touch a sort by
    /// value. Otherwise by label, under the field's declared order and Order Key.
    /// </summary>
    private ChildOrder Order(AxisNode node, bool rows)
    {
        var level = node.Level + 1;
        var placement = rows ? _rowPlacements[level] : _columnPlacements[level];
        var meta = rows ? _rowMeta[level] : _columnMeta[level];
        var children = _cube.ChildrenOf(node, rows).ToList();
        var descending = placement.Sort.Direction == PivotSortDirection.Descending;
        if (placement.Sort.ByValue is { } vf)
        {
            var other = rows ? _cube.ColumnRoot : _cube.RowRoot;
            var byLabel = new ItemOrder(meta, _labels, _culture, descending: false, byKey: false);
            var keys = new double?[children.Count];
            return new ChildOrder(children,
                i =>
                {
                    var child = children[i];
                    var value = rows ? _reader.Shown(child, other, vf) : _reader.Shown(other, child, vf);
                    keys[i] = value.IsEmpty || value.IsError ? null : value.Number;
                },
                (i, j) =>
                {
                    var x = keys[i];
                    var y = keys[j];
                    if (x is null || y is null)
                        return x is null && y is null ? byLabel.Compare(children[i].Item, children[j].Item) : x is null ? 1 : -1;
                    var compared = x.Value.CompareTo(y.Value);
                    if (descending)
                        compared = -compared;
                    return compared != 0 ? compared : byLabel.Compare(children[i].Item, children[j].Item);
                });
        }
        var order = new ItemOrder(meta, _labels, _culture, descending);
        return new ChildOrder(children, i => order.Prepare(children[i].Item!), (i, j) => order.Compare(children[i].Item, children[j].Item));
    }

    /// <summary>
    /// A node's children ordered a step at a time: each child prepared — its Order Key, or its value
    /// — then a stable merge sort, bottom up, from runs sorted by insertion, every comparison and
    /// every move a unit of work. The order is a total one, so the result is the one any sort
    /// gives.
    /// </summary>
    private sealed class ChildOrder(List<AxisNode> children, Action<int> prepare, Comparison<int> compare)
    {
        private const int Run = 16;

        private readonly int _count = children.Count;
        private int[] _from = [.. Enumerable.Range(0, children.Count)];
        private int[] _to = new int[children.Count];
        private int _prepared;
        private int _runs;
        private int _width = Run;
        private bool _merging;
        private int _lo;
        private int _i;
        private int _j;
        private int _k;
        private int _mid;
        private int _hi;

        /// <summary>The children in their order, once <see cref="Step"/> has answered true.</summary>
        public List<AxisNode>? Result { get; private set; }

        /// <summary>Goes on ordering until it is done — true — or <paramref name="budget"/> units of
        /// work are spent — false; what is left of the budget is left in it.</summary>
        public bool Step(ref int budget)
        {
            while (_prepared < _count)
            {
                if (budget <= 0)
                    return false;
                prepare(_prepared++);
                budget -= 2;
            }
            while (_runs < _count)
            {
                if (budget <= 0)
                    return false;
                var end = Math.Min(_runs + Run, _count);
                budget -= InsertionSort(_runs, end);
                _runs = end;
            }
            while (_width < _count)
            {
                if (!_merging)
                {
                    if (_lo >= _count)
                    {
                        (_from, _to) = (_to, _from);
                        _width *= 2;
                        _lo = 0;
                        continue;
                    }
                    _mid = Math.Min(_lo + _width, _count);
                    _hi = Math.Min(_lo + (2 * _width), _count);
                    _i = _lo;
                    _j = _mid;
                    _k = _lo;
                    _merging = true;
                }
                while (_k < _hi)
                {
                    if (budget <= 0)
                        return false;
                    // The left run's first on a tie: the merge keeps the order runs were in.
                    _to[_k++] = _i < _mid && (_j >= _hi || compare(_from[_i], _from[_j]) <= 0) ? _from[_i++] : _from[_j++];
                    budget--;
                }
                _merging = false;
                _lo += 2 * _width;
            }
            var result = new List<AxisNode>(_count);
            foreach (var index in _from)
                result.Add(children[index]);
            Result = result;
            budget--;
            return true;
        }

        // Sorts [from, to) of the order in place, stably; answers the comparisons made.
        private int InsertionSort(int from, int to)
        {
            var comparisons = 1;
            for (var n = from + 1; n < to; n++)
            {
                var index = _from[n];
                var at = n;
                while (at > from)
                {
                    comparisons++;
                    if (compare(_from[at - 1], index) <= 0)
                        break;
                    _from[at] = _from[at - 1];
                    at--;
                }
                _from[at] = index;
            }
            return comparisons;
        }
    }

    /// <summary>A field's collapse state, read per Item without searching the layout's list.</summary>
    private sealed class CollapseState(PivotFieldPlacement placement)
    {
        private readonly HashSet<ItemKey> _toggled = placement.ToggledItems.Select(ItemKey.FromPublic).ToHashSet();

        public bool IsCollapsed(ItemKey item) => placement.Collapsed != _toggled.Contains(item);
    }
}

/// <summary>
/// The Value Fields' captions (ADR-0060): a caption of its own, or
/// <c>&lt;Aggregation&gt; of &lt;field caption&gt;</c>; a default a second Value Field would
/// repeat takes a number, as Excel's <c>Sum of Amount2</c>.
/// </summary>
public static class ValueCaptions
{
    /// <summary>
    /// Each Value Field's caption, in order. Refuses by name a caption of a Value Field's own that
    /// another Value Field's caption, or a declared Pivot Field's caption, already is — Excel's
    /// "PivotTable field name already exists".
    /// </summary>
    public static IReadOnlyList<string> Resolve(
        IReadOnlyList<PivotValueField> values,
        IReadOnlyDictionary<string, PivotFieldInfo> fields,
        PivotOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(fields);
        var resolved = options ?? PivotOptions.Default;
        var captions = new string[values.Count];
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fieldCaptions = fields.Values.Select(f => f.Caption).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // A caption of its own is fixed; a default takes whatever number keeps it unique.
        for (var i = 0; i < values.Count; i++)
        {
            if (values[i].Caption is not { } own)
                continue;
            if (own.Length == 0)
                throw new InvalidOperationException($"The Value Field of '{values[i].Field}' has an empty caption (ADR-0060).");
            if (!taken.Add(own))
                throw new InvalidOperationException($"Two Value Fields are captioned '{own}' (ADR-0060).");
            if (fieldCaptions.Contains(own))
                throw new InvalidOperationException($"The Value Field of '{values[i].Field}' is captioned '{own}', which is a Pivot Field's caption (ADR-0060).");
            captions[i] = own;
        }
        for (var i = 0; i < values.Count; i++)
        {
            if (captions[i] is not null)
                continue;
            var fieldCaption = fields.TryGetValue(values[i].Field, out var info) ? info.Caption : values[i].Field;
            var basis = PivotWords.Fill(resolved.Word(PivotWords.CaptionOf(values[i].Aggregation)), fieldCaption);
            var caption = basis;
            for (var n = 2; taken.Contains(caption) || fieldCaptions.Contains(caption); n++)
                caption = basis + n.ToString(CultureInfo.InvariantCulture);
            taken.Add(caption);
            captions[i] = caption;
        }
        return captions;
    }
}
