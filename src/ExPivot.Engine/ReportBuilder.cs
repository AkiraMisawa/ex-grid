using System.Globalization;
using System.Text;

namespace ExPivot.Engine;

/// <summary>
/// Reads a Value Field's value where a row node and a column node cross, as aggregated and as
/// shown (ADR-0059). Shared by the report's cells and by an order by value, so the order and
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
/// Lays a cube out under a layout (ADR-0059): the value columns and their Header Group spans
/// from the column tree, the rows from the row tree in the layout's form, each in its Items'
/// order, with subtotals, grand totals, collapsed Items and Σ Values where the layout puts them.
/// </summary>
internal sealed class ReportBuilder
{
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

    public ReportBuilder(PivotCube cube, PivotLayout layout, PivotOptions options)
    {
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

    public PivotReport Build()
    {
        var columns = new List<PivotReportColumn>();
        var spans = new List<PivotHeaderSpan>();
        var rows = new List<PivotReportRow>();
        if (!Empty)
        {
            EmitColumns(columns, spans);
            EmitRows(rows);
        }
        var tiers = ColumnLevels == 0 ? 0 : _valuesOnColumns ? ColumnLevels : ColumnLevels - 1;
        return new PivotReport(_cube, _layout, _options, _reader, LabelColumns(), columns, spans, tiers, rows);
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

    // ---- Columns -------------------------------------------------------------------------

    private int TierOf(int level) => _valuesOnColumns ? ColumnLevels - level : ColumnLevels - 1 - level;

    private void EmitColumns(List<PivotReportColumn> columns, List<PivotHeaderSpan> spans)
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

        foreach (var child in Sorted(root, rows: false))
            EmitColumn(child, columns, spans);

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

    private void EmitColumn(AxisNode node, List<PivotReportColumn> columns, List<PivotHeaderSpan> spans)
    {
        var level = node.Level;
        var innermost = level == ColumnLevels - 1;
        var collapsed = !innermost && _columnCollapse[level].IsCollapsed(node.Item!.Key);
        var label = Label(node, _columnMeta[level]);
        var tier = TierOf(level);
        if (innermost || collapsed)
        {
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
            return;
        }

        var start = columns.Count;
        foreach (var child in Sorted(node, rows: false))
            EmitColumn(child, columns, spans);
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

    private void EmitRows(List<PivotReportRow> rows)
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
                    rows.Add(new PivotReportRow(PivotRowRole.Item, root, vf, carriesValues: true, labels));
                }
            }
            else
            {
                rows.Add(new PivotReportRow(PivotRowRole.GrandTotal, root, -1, carriesValues: true, NewLabels()));
            }
            return;
        }

        foreach (var child in Sorted(root, rows: true))
        {
            if (_layout.Form == PivotReportForm.Tabular)
                EmitTabular(child, rows);
            else
                EmitGrouped(child, rows);
        }

        if (_layout.GrandTotalRow && ValueCount > 0)
        {
            if (_valuesOnRows)
            {
                for (var vf = 0; vf < ValueCount; vf++)
                    rows.Add(new PivotReportRow(PivotRowRole.GrandTotal, root, vf, carriesValues: true, GrandTotalLabels(vf)));
            }
            else
            {
                rows.Add(new PivotReportRow(PivotRowRole.GrandTotal, root, -1, carriesValues: true, GrandTotalLabels(-1)));
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
        return at;
    }

    // The Compact and Outline forms: a group row heads each outer Item's block (ADR-0059).
    private void EmitGrouped(AxisNode node, List<PivotReportRow> rows)
    {
        var level = node.Level;
        var innermost = level == RowLevels - 1;
        var collapsed = !innermost && _rowCollapse[level].IsCollapsed(node.Item!.Key);
        if (innermost || collapsed)
        {
            if (_valuesOnRows)
            {
                rows.Add(new PivotReportRow(PivotRowRole.Group, node, -1, carriesValues: false, ItemLabels(node)));
                for (var vf = 0; vf < ValueCount; vf++)
                    rows.Add(new PivotReportRow(PivotRowRole.Item, node, vf, carriesValues: true, CaptionLabels(node, vf)));
            }
            else
            {
                rows.Add(new PivotReportRow(innermost ? PivotRowRole.Item : PivotRowRole.Group, node, -1,
                    carriesValues: true, ItemLabels(node)));
            }
            return;
        }

        var subtotals = _rowPlacements[level].Subtotals && ValueCount > 0;
        var atTop = subtotals && _layout.SubtotalsAtTop && !_valuesOnRows;
        rows.Add(new PivotReportRow(PivotRowRole.Group, node, -1, carriesValues: atTop, ItemLabels(node)));
        foreach (var child in Sorted(node, rows: true))
            EmitGrouped(child, rows);
        if (subtotals && !atTop)
        {
            if (_valuesOnRows)
            {
                for (var vf = 0; vf < ValueCount; vf++)
                    rows.Add(new PivotReportRow(PivotRowRole.Subtotal, node, vf, carriesValues: true, SubtotalLabels(node, vf)));
            }
            else
            {
                rows.Add(new PivotReportRow(PivotRowRole.Subtotal, node, -1, carriesValues: true, SubtotalLabels(node, -1)));
            }
        }
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
    // block, and its subtotal at the bottom (ADR-0059).
    private void EmitTabular(AxisNode node, List<PivotReportRow> rows)
    {
        var level = node.Level;
        _pending[level] = node;
        var innermost = level == RowLevels - 1;
        var collapsed = !innermost && _rowCollapse[level].IsCollapsed(node.Item!.Key);
        if (innermost || collapsed)
        {
            var role = innermost ? PivotRowRole.Item : PivotRowRole.Group;
            if (_valuesOnRows)
            {
                for (var vf = 0; vf < ValueCount; vf++)
                {
                    var labels = TabularLabels(node, level);
                    labels[_labelColumnCount - 1] = new PivotRowLabel(_values[vf].Caption);
                    rows.Add(new PivotReportRow(role, node, vf, carriesValues: true, labels));
                }
            }
            else
            {
                rows.Add(new PivotReportRow(role, node, -1, carriesValues: true, TabularLabels(node, level)));
            }
            return;
        }

        foreach (var child in Sorted(node, rows: true))
            EmitTabular(child, rows);

        if (_rowPlacements[level].Subtotals && ValueCount > 0)
        {
            var total = PivotWords.Fill(Word(PivotWords.ItemTotal), Label(node, _rowMeta[level]));
            for (var vf = _valuesOnRows ? 0 : -1; vf < (_valuesOnRows ? ValueCount : 0); vf++)
            {
                var labels = TabularLabels(node, level - 1);
                labels[level] = new PivotRowLabel(total);
                if (vf >= 0)
                    labels[_labelColumnCount - 1] = new PivotRowLabel(_values[vf].Caption);
                rows.Add(new PivotReportRow(PivotRowRole.Subtotal, node, vf, carriesValues: true, labels));
            }
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

    private List<AxisNode> Sorted(AxisNode node, bool rows)
    {
        var level = node.Level + 1;
        var placement = rows ? _rowPlacements[level] : _columnPlacements[level];
        var meta = rows ? _rowMeta[level] : _columnMeta[level];
        var children = new List<AxisNode>(node.Children);
        var descending = placement.Sort.Direction == PivotSortDirection.Descending;
        if (placement.Sort.ByValue is { } vf)
        {
            // By a Value Field's value at each Item's total across the other axis, as shown;
            // blank and error values last, ties by label ascending (ADR-0059).
            var other = rows ? _cube.ColumnRoot : _cube.RowRoot;
            var byLabel = new ItemOrder(meta, _labels, _culture, descending: false);
            var keys = new Dictionary<AxisNode, double?>(children.Count);
            foreach (var child in children)
            {
                var value = rows ? _reader.Shown(child, other, vf) : _reader.Shown(other, child, vf);
                keys[child] = value.IsEmpty || value.IsError ? null : value.Number;
            }
            children.Sort((a, b) =>
            {
                var x = keys[a];
                var y = keys[b];
                if (x is null || y is null)
                    return x is null && y is null ? byLabel.Compare(a.Item, b.Item) : x is null ? 1 : -1;
                var compared = x.Value.CompareTo(y.Value);
                if (descending)
                    compared = -compared;
                return compared != 0 ? compared : byLabel.Compare(a.Item, b.Item);
            });
            return children;
        }
        var order = new ItemOrder(meta, _labels, _culture, descending);
        children.Sort((a, b) => order.Compare(a.Item, b.Item));
        return children;
    }

    /// <summary>A field's collapse state, read per Item without searching the layout's list.</summary>
    private sealed class CollapseState(PivotFieldPlacement placement)
    {
        private readonly HashSet<ItemKey> _toggled = placement.ToggledItems.Select(ItemKey.FromPublic).ToHashSet();

        public bool IsCollapsed(ItemKey item) => placement.Collapsed != _toggled.Contains(item);
    }
}

/// <summary>
/// The Value Fields' captions (ADR-0059): a caption of its own, or
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
                throw new InvalidOperationException($"The Value Field of '{values[i].Field}' has an empty caption (ADR-0059).");
            if (!taken.Add(own))
                throw new InvalidOperationException($"Two Value Fields are captioned '{own}' (ADR-0059).");
            if (fieldCaptions.Contains(own))
                throw new InvalidOperationException($"The Value Field of '{values[i].Field}' is captioned '{own}', which is a Pivot Field's caption (ADR-0059).");
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
