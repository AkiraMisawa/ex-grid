using System.Globalization;
using ExGrid;
using ExGrid.Cells;
using ExGrid.Chrome;
using ExGrid.Columns;
using ExGrid.Components;
using ExGrid.Rows;
using ExGrid.Selection;
using ExPivot.Chrome;
using ExPivot.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace ExPivot.Components;

/// <summary>
/// Excel's PivotTable, drawn by ExGrid as that grid's Consumer (ADR-0059): the Pivot Toolbar above
/// the report, one ExGrid over the Pivot Report, Show Details' tabs at its foot, and the Field List
/// beside it (ADR-0061). ExPivot holds the Pivot Layout, asks its Pivot Source for the Leaf
/// Aggregates and lays the report out with ExPivot.Engine (ADR-0066); ExGrid paints, selects,
/// navigates, copies and reports.
/// </summary>
// This part is the report half: the one ExGrid, its columns, its label cells, its Context Menu
// and its double click (ADR-0059/0063).
public partial class ExPivot
{
    /// <summary>The widest a label column is sized to by its labels; a wider label is cut with an
    /// ellipsis, as text is (ADR-0016). A user's drag may make it wider.</summary>
    private const double MaxLabelWidthPx = 480;

    private const double MinLabelWidthPx = 60;

    // Row Kind from the row's role (ADR-0024/0059); one instance for every ExPivot.
    private static readonly Func<PivotReportRow, RowKind> RowKindOf = static row => row.Role switch
    {
        PivotRowRole.Item => RowKind.Detail,
        PivotRowRole.Group => RowKind.Group,
        _ => RowKind.Total,
    };

    // An error value is centred, as Excel centres it (ADR-0060); everything else follows the kind.
    private static readonly Func<PivotReportRow, GridColumn<PivotReportRow>, CellAlign> AlignOf =
        static (row, column) => column.Value(row) is PivotValue { IsError: true } ? CellAlign.Center : CellAlign.Auto;

    // A value cell paints the text the engine formatted; its raw form is the number (ADR-0005/0060).
    private static readonly Func<object, string> TextOfValue = static value => ((PivotValue)value).Text;

    private readonly Dictionary<string, double> _userWidths = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GridColumn<PivotReportRow>> _columnCache = new(StringComparer.Ordinal);
    private readonly List<RenderFragment<TemplateCellContext<PivotReportRow>>> _labelTemplates = [];
    private readonly List<Func<PivotReportRow, object?>> _labelValues = [];
    private readonly List<Func<PivotReportRow, object?>> _valueAccessors = [];
    private readonly List<string> _indentStyles = [];
    private readonly EventCallback<ColumnWidthChange> _widthChanged;
    private readonly Func<ContextMenuContext<PivotReportRow>, IEnumerable<GridCommand>> _contextCommands;
    private readonly Func<string, string?> _commandLabel;
    private readonly EventCallback<CellPosition> _doubleClick;
    private readonly RenderFragment _gridFragment;

    private PivotReport? _report;
    private int _rowSequenceVersion;
    private IReadOnlyList<GridColumn<PivotReportRow>> _columns = [];
    private IReadOnlyList<HeaderGroup>? _headerGroups;
    private int _pinned;
    private GridMetrics _metrics = GridMetrics.Resolve(GridDensity.Compact);
    private ExGrid<PivotReportRow>? _grid;
    // The Item whose row should hold the Focus once the report it was toggled into has reached the
    // grid, and the label column its button is in.
    private (PivotToggle Toggle, int Column)? _focusAfterToggle;

    /// <summary>Creates the component; the report is asked for when its parameters arrive.</summary>
    public ExPivot()
    {
        // Held in fields: a delegate's identity reaches the grid, and one made per render would
        // look like a change every time (ADR-0003). No receiver: each re-renders what it changes.
        _widthChanged = new EventCallback<ColumnWidthChange>(null, (Func<ColumnWidthChange, Task>)OnWidthChangedAsync);
        _contextCommands = ContextCommandsFor;
        _commandLabel = CommandLabelFor;
        _doubleClick = new EventCallback<CellPosition>(null, (Func<CellPosition, Task>)OnCellDoubleClickAsync);
        _gridFragment = RenderGrid;
        _escape = new EventCallback(null, (Func<Task>)OnEscapeAsync);
        _closeDialog = new EventCallback(null, (Action)CloseDialog);
        _idPrefix = "ex-pivot-" + Guid.NewGuid().ToString("N")[..12];
    }

    // The Pivot Chrome's grid Chrome, asked once per Pivot Chrome and held: one made per render
    // would be a new parameter every time, and the grid would re-render with each (ADR-0003).
    // The words it is handed read Label when asked, so a new Label needs no new Chrome.
    private IPivotChrome? _gridChromeOf;
    private IGridChrome? _pivotGridChrome;

    private IGridChrome? GridChrome
    {
        get
        {
            if (Chrome is { } chrome)
                return chrome;
            if (!ReferenceEquals(_gridChromeOf, PivotChrome))
            {
                _gridChromeOf = PivotChrome;
                _pivotGridChrome = PivotChrome?.GridChrome(_commandLabel);
            }
            return _pivotGridChrome;
        }
    }

    private double IndentPx => Math.Round(_metrics.CellMetrics.FullWidthPx);

    private string RootStyle => string.Create(CultureInfo.InvariantCulture,
        $"--ex-pivot-field-list-width: {FieldListWidth}px; --ex-pivot-indent: {IndentPx}px{(ViewportHeight.IsStretch ? "; height: 100%; flex: 1 1 auto; min-height: 0" : "")}");

    private string EmptyStyle => ViewportHeight.IsStretch
        ? "flex: 1 1 auto; min-height: 0"
        : string.Create(CultureInfo.InvariantCulture, $"height: {ViewportHeight.Px}px");

    // The box the report and a details tab's records share: under a Stretch height it is the
    // flex item the grid stretches in (ADR-0028).
    private string? SheetStyle => ViewportHeight.IsStretch ? "display: flex; flex-direction: column; flex: 1 1 auto; min-height: 0" : null;

    // What the box says while there is no report to show: that one is on its way, or how to start.
    private string EmptyText => _loading && _layout.Rows.Count + _layout.Columns.Count + _layout.Values.Count > 0
        ? Word("loading")
        : Word("empty-report");

    // Bumped whenever anything the grid reads may have changed (PivotGridHost).
    private int _gridVersion;

    private RenderFragment Grid() => builder =>
    {
        builder.OpenComponent<PivotGridHost>(0);
        builder.AddComponentParameter(1, nameof(PivotGridHost.Version), _gridVersion);
        builder.AddComponentParameter(2, nameof(PivotGridHost.ChildContent), _gridFragment);
        builder.CloseComponent();
    };

    /// <summary>Metrics resolved as the grid resolves them (ADR-0028/0030): the explicit
    /// parameter, then a Wrapper's cascaded value, then the preset. True when they moved.</summary>
    private bool ResolveGeometry()
    {
        var metrics = GridMetrics.Resolve(
            Density ?? Presentation?.Density ?? GridDensity.Compact, RowHeight, null, CellMetrics, Presentation);
        if (metrics == _metrics)
            return false;
        _metrics = metrics;
        _indentStyles.Clear();
        return true;
    }

    private void BuildColumns()
    {
        _gridVersion++;
        if (_report is not { } report)
            return;
        var columns = new List<GridColumn<PivotReportRow>>(report.LabelColumns.Count + report.ValueColumns.Count);
        for (var i = 0; i < report.LabelColumns.Count; i++)
        {
            var label = report.LabelColumns[i];
            var width = _userWidths.TryGetValue(label.Name, out var user) ? user : LabelWidthsOf(report)[i];
            var index = i;
            columns.Add(Cached(Key(label.Name, label.Header, i, width), () => GridColumn<PivotReportRow>.TemplateColumn(
                label.Name, ColumnType.Text, LabelValue(index), LabelTemplate(index), label.Header, FixedWidth(width))));
        }
        for (var j = 0; j < report.ValueColumns.Count; j++)
        {
            var value = report.ValueColumns[j];
            double? width = _userWidths.TryGetValue(value.Name, out var user) ? user : null;
            var index = j;
            columns.Add(Cached(Key(value.Name, value.Header, j, width ?? 0), () => new GridColumn<PivotReportRow>(
                value.Name, ColumnType.Number, ValueAccessor(index), value.Header,
                width is { } px ? FixedWidth(px) : null, format: TextOfValue)));
        }
        if (!SameColumns(columns, _columns))
            _columns = columns;
        if (_columnCache.Count > (4 * columns.Count) + 64)
        {
            var current = columns.ToHashSet();
            foreach (var stale in _columnCache.Where(pair => !current.Contains(pair.Value)).Select(pair => pair.Key).ToArray())
                _columnCache.Remove(stale);
        }

        // The column Items as Header Groups over the value columns, never the pinned labels
        // (ADR-0032): a rectangle cannot straddle the pinned boundary.
        var names = report.ValueColumns.Select(c => c.Name).ToArray();
        var groups = report.HeaderSpans
            .Select(span => new HeaderGroup(span.Label, names[span.FirstColumn..(span.FirstColumn + span.ColumnCount)], span.Tier, span.TierSpan))
            .ToArray();
        if (!SameGroups(groups, _headerGroups))
            _headerGroups = groups.Length == 0 ? null : groups;
        _pinned = report.LabelColumns.Count;
    }

    private static string Key(string name, string header, int index, double width)
        => string.Create(CultureInfo.InvariantCulture, $"{name}\u001F{header}\u001F{index}\u001F{width}");

    private GridColumn<PivotReportRow> Cached(string key, Func<GridColumn<PivotReportRow>> create)
    {
        if (!_columnCache.TryGetValue(key, out var column))
        {
            column = create();
            _columnCache[key] = column;
        }
        return column;
    }

    private static ColumnWidthSpec FixedWidth(double px)
        => new(ColumnWidth.Fixed(px), minWidthPx: Math.Min(40, px), maxWidthPx: Math.Max(ColumnWidthSpec.DefaultMaxWidthPx, px));

    private static bool SameColumns(List<GridColumn<PivotReportRow>> columns, IReadOnlyList<GridColumn<PivotReportRow>> previous)
    {
        if (columns.Count != previous.Count)
            return false;
        for (var i = 0; i < columns.Count; i++)
        {
            if (!ReferenceEquals(columns[i], previous[i]))
                return false;
        }
        return true;
    }

    private static bool SameGroups(HeaderGroup[] groups, IReadOnlyList<HeaderGroup>? previous)
    {
        if (previous is null)
            return groups.Length == 0;
        if (groups.Length != previous.Count)
            return false;
        for (var i = 0; i < groups.Length; i++)
        {
            var one = groups[i];
            var other = previous[i];
            if (one.Label != other.Label || one.Tier != other.Tier || one.TierSpan != other.TierSpan
                || !one.Columns.SequenceEqual(other.Columns, StringComparer.Ordinal))
                return false;
        }
        return true;
    }

    // The label columns' widths from their labels, for the report and the metrics they were sized
    // under: measured as the report was laid out, in slices (PV-40), and again only when the
    // metrics moved since — never on a width the user drags.
    private PivotReport? _labelWidthsOf;
    private GridMetrics? _labelWidthsMetrics;
    private double[] _labelWidths = [];

    /// <summary>How many rows' labels are measured between two looks at the clock.</summary>
    private const int LabelRowsPerLook = 1024;

    private void KeepLabelWidths(PivotReport report, GridMetrics metrics, double[] widths)
    {
        _labelWidthsOf = report;
        _labelWidthsMetrics = metrics;
        _labelWidths = widths;
    }

    /// <summary>The label columns' widths of <paramref name="report"/> under the metrics now: those
    /// kept, or measured now when there are none for them.</summary>
    private double[] LabelWidthsOf(PivotReport report)
    {
        if (!ReferenceEquals(_labelWidthsOf, report) || _labelWidthsMetrics != _metrics)
        {
            var widths = HeaderWidths(report, _metrics);
            MeasureLabels(report, _metrics, 0, report.Rows.Count, widths);
            KeepLabelWidths(report, _metrics, Bounded(widths));
        }
        return _labelWidths;
    }

    /// <summary><see cref="LabelWidthsOf"/>'s widths, measured a piece of rows at a time, yielding
    /// whenever the work's slice is spent (PV-40).</summary>
    private async Task<double[]> LabelWidthsAsync(PivotReport report, GridMetrics metrics, Pace pace)
    {
        var widths = HeaderWidths(report, metrics);
        var rows = report.Rows.Count;
        for (var from = 0; from < rows; from += LabelRowsPerLook)
        {
            if (from > 0 && pace.Spent)
                await pace.YieldAsync();
            MeasureLabels(report, metrics, from, Math.Min(rows, from + LabelRowsPerLook), widths);
        }
        return Bounded(widths);
    }

    /// <summary>What each label column's header needs, which its labels can only widen.</summary>
    private static double[] HeaderWidths(PivotReport report, GridMetrics metrics)
    {
        var widths = new double[report.LabelColumns.Count];
        for (var column = 0; column < widths.Length; column++)
            widths[column] = metrics.HeaderRequiredPx(report.LabelColumns[column].Header, menuButton: false, sortable: false);
        return widths;
    }

    /// <summary>A label column's width, from its labels (ADR-0059): each label's text as the grid
    /// estimates it, plus its indent and its button — over rows [<paramref name="from"/>,
    /// <paramref name="to"/>), widening <paramref name="widths"/>.</summary>
    private static void MeasureLabels(PivotReport report, GridMetrics metrics, int from, int to, double[] widths)
    {
        var text = metrics.CellMetrics;
        var indent = Math.Round(text.FullWidthPx);
        for (var i = from; i < to; i++)
        {
            var labels = report.Rows[i].Labels;
            for (var column = 0; column < widths.Length; column++)
            {
                var label = labels[column];
                if (label.Text is null && label.Toggle is null)
                    continue;
                var px = text.EstimatePx(label.Text ?? "") + (label.Indent * indent) + (label.Toggle is null ? 0 : indent);
                if (px > widths[column])
                    widths[column] = px;
            }
        }
    }

    /// <summary>The widths, bounded so one long label does not push the values off screen.</summary>
    private static double[] Bounded(double[] widths)
    {
        for (var column = 0; column < widths.Length; column++)
            widths[column] = Math.Clamp(Math.Ceiling(widths[column]), MinLabelWidthPx, MaxLabelWidthPx);
        return widths;
    }

    private Func<PivotReportRow, object?> LabelValue(int column)
    {
        while (_labelValues.Count <= column)
        {
            var index = _labelValues.Count;
            _labelValues.Add(row => row.Labels[index].Text);
        }
        return _labelValues[column];
    }

    private Func<PivotReportRow, object?> ValueAccessor(int column)
    {
        while (_valueAccessors.Count <= column)
        {
            var index = _valueAccessors.Count;
            _valueAccessors.Add(row => row.ValueAt(index));
        }
        return _valueAccessors[column];
    }

    private RenderFragment<TemplateCellContext<PivotReportRow>> LabelTemplate(int column)
    {
        while (_labelTemplates.Count <= column)
        {
            var index = _labelTemplates.Count;
            _labelTemplates.Add(context => builder => RenderLabel(builder, context, index));
        }
        return _labelTemplates[column];
    }

    private string IndentStyle(int indent)
    {
        while (_indentStyles.Count <= indent)
            _indentStyles.Add(string.Create(CultureInfo.InvariantCulture, $"padding-left: {_indentStyles.Count * IndentPx}px"));
        return _indentStyles[indent];
    }

    private string ToggleName(PivotToggle toggle)
        => PivotWords.Fill(Word(toggle.IsCollapsed ? "expand-item" : "collapse-item"), toggle.ItemLabel);

    /// <summary>
    /// One label cell (ADR-0059): the indent, the expand / collapse button of an outer Item, and the
    /// label. The button is plain markup with <c>ex-interactive</c> and <c>tabindex="-1"</c>; only in
    /// the one cell Space asks to enter is it a component, which takes DOM focus itself (ADR-0037).
    /// A double click on it stops there: its offsets are the button's (ADR-0063).
    /// </summary>
    private void RenderLabel(RenderTreeBuilder builder, TemplateCellContext<PivotReportRow> context, int column)
    {
        var label = context.Row.Labels[column];
        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "class", "ex-pivot-label");
        if (label.Indent > 0)
            builder.AddAttribute(2, "style", IndentStyle(label.Indent));
        if (label.Toggle is { } toggle)
        {
            var onToggle = EventCallback.Factory.Create<MouseEventArgs>(this, () => ToggleAsync(toggle, column));
            if (context.FocusRequest != 0)
            {
                builder.OpenComponent<PivotToggleButton>(3);
                builder.AddComponentParameter(4, nameof(PivotToggleButton.Collapsed), toggle.IsCollapsed);
                builder.AddComponentParameter(5, nameof(PivotToggleButton.Name), ToggleName(toggle));
                builder.AddComponentParameter(6, nameof(PivotToggleButton.OnToggle), onToggle);
                builder.AddComponentParameter(7, nameof(PivotToggleButton.FocusRequest), context.FocusRequest);
                builder.CloseComponent();
            }
            else
            {
                builder.OpenElement(8, "button");
                builder.AddAttribute(9, "type", "button");
                builder.AddAttribute(10, "class", PivotToggleButton.ClassFor(toggle.IsCollapsed));
                builder.AddAttribute(11, "tabindex", "-1");
                builder.AddAttribute(12, "aria-expanded", toggle.IsCollapsed ? "false" : "true");
                builder.AddAttribute(13, "aria-label", ToggleName(toggle));
                builder.AddAttribute(14, "onclick", onToggle);
                builder.AddEventStopPropagationAttribute(15, "ondblclick", true);
                builder.AddContent(16, PivotToggleButton.GlyphFor(toggle.IsCollapsed));
                builder.CloseElement();
            }
        }
        if (label.Text is { } text)
        {
            builder.OpenElement(17, "span");
            builder.AddAttribute(18, "class", "ex-pivot-label-text");
            builder.AddContent(19, text);
            builder.CloseElement();
        }
        builder.CloseElement();
    }

    private void RenderGrid(RenderTreeBuilder builder)
    {
        var report = _report!;
        builder.OpenComponent<ExGrid<PivotReportRow>>(0);
        builder.AddComponentParameter(1, nameof(ExGrid<PivotReportRow>.Window), report.Rows);
        builder.AddComponentParameter(2, nameof(ExGrid<PivotReportRow>.Columns), _columns);
        builder.AddComponentParameter(3, nameof(ExGrid<PivotReportRow>.HeaderGroups), _headerGroups);
        builder.AddComponentParameter(4, nameof(ExGrid<PivotReportRow>.PinnedColumnCount), _pinned);
        builder.AddComponentParameter(5, nameof(ExGrid<PivotReportRow>.RowSequenceVersion), _rowSequenceVersion);
        builder.AddComponentParameter(6, nameof(ExGrid<PivotReportRow>.RowKind), RowKindOf);
        builder.AddComponentParameter(7, nameof(ExGrid<PivotReportRow>.CellAlign), AlignOf);
        builder.AddComponentParameter(8, nameof(ExGrid<PivotReportRow>.HeaderClickSelects), true);
        builder.AddComponentParameter(9, nameof(ExGrid<PivotReportRow>.HideColumnMenu), true);
        builder.AddComponentParameter(10, nameof(ExGrid<PivotReportRow>.OnColumnWidthChanged), _widthChanged);
        builder.AddComponentParameter(11, nameof(ExGrid<PivotReportRow>.ContextCommands), _contextCommands);
        builder.AddComponentParameter(12, nameof(ExGrid<PivotReportRow>.CommandLabel), _commandLabel);
        builder.AddComponentParameter(13, nameof(ExGrid<PivotReportRow>.ViewportHeight), ViewportHeight);
        builder.AddComponentParameter(14, nameof(ExGrid<PivotReportRow>.ViewportWidth), ViewportWidth);
        builder.AddComponentParameter(15, nameof(ExGrid<PivotReportRow>.OnCellDoubleClick), _doubleClick);
        // While a question is out, the report stays as it was under the grid's own indication
        // (ADR-0010/0066).
        builder.AddComponentParameter(16, nameof(ExGrid<PivotReportRow>.IsLoading), _loading);
        if (RowHeight is { } rowHeight)
            builder.AddComponentParameter(17, nameof(ExGrid<PivotReportRow>.RowHeight), rowHeight);
        if (Density is { } density)
            builder.AddComponentParameter(18, nameof(ExGrid<PivotReportRow>.Density), density);
        if (CellMetrics is { } metrics)
            builder.AddComponentParameter(19, nameof(ExGrid<PivotReportRow>.CellMetrics), metrics);
        if (GridChrome is { } chrome)
            builder.AddComponentParameter(20, nameof(ExGrid<PivotReportRow>.Chrome), chrome);
        if (SelectionChanged.HasDelegate)
            builder.AddComponentParameter(21, nameof(ExGrid<PivotReportRow>.SelectionChanged), SelectionChanged);
        // The Change Highlight (ADR-0067/0068): ExPivot says when a cell's painted value changed
        // with the data, through a delegate that is new for each data version and null while
        // nothing can be marked; the grid marks the cell for the duration, on ExPivot's clock.
        builder.AddComponentParameter(22, nameof(ExGrid<PivotReportRow>.CellChangedAt), _cellChangedAt);
        builder.AddComponentParameter(23, nameof(ExGrid<PivotReportRow>.ChangeHighlightDuration), ChangeHighlightDuration);
        builder.AddComponentParameter(24, nameof(ExGrid<PivotReportRow>.Clock), _time);
        builder.AddComponentReferenceCapture(25, grid => _grid = (ExGrid<PivotReportRow>)grid);
        builder.CloseComponent();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // The toggled Item keeps the Focus, as it does in Excel: placed once the report it was
        // toggled into has reached the grid, under that report's order (ADR-0050 item 4, ADR-0011).
        if (_focusAfterToggle is { } pending && _grid is { } grid && _report is { } report
            && ReferenceEquals(report.Layout, _layout))
        {
            _focusAfterToggle = null;
            var (toggle, column) = pending;
            for (var i = 0; i < report.Rows.Count; i++)
            {
                if (report.Rows[i].Labels[column].Toggle is { } at && at.Field == toggle.Field && at.Item.Equals(toggle.Item))
                {
                    var cell = new CellPosition(i, column);
                    await grid.PlaceSelectionAsync(new SelectionRange(i, column, 1, 1), cell, _rowSequenceVersion);
                    break;
                }
            }
        }
    }

    private Task ToggleAsync(PivotToggle toggle, int column)
    {
        _focusAfterToggle = (toggle, column);
        return ReportEditAsync(layout => PivotLayoutEdits.SetCollapsed(layout, toggle.Field, toggle.Item, !toggle.IsCollapsed));
    }

    private async Task OnWidthChangedAsync(ColumnWidthChange change)
    {
        // A width the user dragged is theirs until the column leaves the report (ADR-0016).
        _userWidths[change.Column] = change.WidthPx;
        BuildColumns();
        StateHasChanged();
        await Task.CompletedTask;
    }

    // ---- The double click and Show Details (ADR-0063) ------------------------------------------

    private async Task OnCellDoubleClickAsync(CellPosition cell)
    {
        if (_report is not { } report || cell.Row < 0 || cell.Row >= report.Rows.Count)
            return;
        var row = report.Rows[cell.Row];
        var labels = report.LabelColumns.Count;
        if (cell.Column < labels)
        {
            // A double click on an outer Item's label expands or collapses it, as in Excel.
            if (row.Labels[cell.Column].Toggle is { } toggle)
                await ToggleAsync(toggle, cell.Column);
            return;
        }
        await ShowDetailsAsync(row, cell.Column - labels);
    }

    private IReadOnlyList<PivotDetailItem> Items(PivotReport report, IReadOnlyList<(string Field, PivotItemKey Item)> path)
        => path.Select(step => new PivotDetailItem(CaptionOf(step.Field), LabelOf(report, step.Field, step.Item))).ToArray();

    private string CaptionOf(string field) => FieldOf(field)?.Caption ?? field;

    private PivotField? FieldOf(string field)
    {
        foreach (var declared in Source.Fields)
        {
            if (declared.Name == field)
                return declared;
        }
        return null;
    }

    /// <summary>An Item's label, by the engine's rule for its field (ADR-0060).</summary>
    private string LabelOf(PivotReport report, string field, PivotItemKey item)
    {
        if (FieldOf(field) is not { } declared)
            return item.Value ?? Word(PivotWords.Blank);
        var page = new PivotItemPage(report.Cube.SourceVersion, [item], 1);
        return PivotEngine.ItemsOf(page, report.Layout, declared, _options)[0].Label;
    }

    // ---- The Context Menu (ADR-0036/0059) --------------------------------------------------------

    private string? CommandLabelFor(string id)
    {
        if (Label?.Invoke(id) is { } own)
            return own;
        return PivotCommandIds.CaptionOfRemove(id) is { } caption
            ? PivotWords.Fill(Word(PivotCommandIds.RemoveNamedField), caption)
            : PivotWords.EnglishFor(id);
    }

    private IEnumerable<GridCommand> ContextCommandsFor(ContextMenuContext<PivotReportRow> context)
    {
        if (_report is not { } report || !ReferenceEquals(context.Row.Report, report))
            return [];
        var layout = report.Layout;
        var row = context.Row;
        var labelColumn = report.LabelColumns.ToList().FindIndex(c => c.Name == context.Column);
        var valueColumn = labelColumn >= 0 ? -1 : report.ValueColumns.ToList().FindIndex(c => c.Name == context.Column);
        var commands = new List<GridCommand>();

        if (CollapsibleItemOf(report, row) is { } item)
        {
            var placement = layout.Rows.First(p => p.Field == item.Field);
            var collapsed = placement.IsCollapsed(item.Item);
            commands.Add(new GridCommand(PivotCommandIds.Expand, collapsed,
                () => ToggleFromMenuAsync(item.Field, item.Item, collapse: false)));
            commands.Add(new GridCommand(PivotCommandIds.Collapse, !collapsed,
                () => ToggleFromMenuAsync(item.Field, item.Item, collapse: true)));
            commands.Add(new GridCommand(PivotCommandIds.ExpandField, placement.Collapsed || placement.ToggledItems.Count > 0,
                () => ReportEditAsync(l => PivotLayoutEdits.SetFieldCollapsed(l, item.Field, false))));
            commands.Add(new GridCommand(PivotCommandIds.CollapseField, !placement.Collapsed || placement.ToggledItems.Count > 0,
                () => ReportEditAsync(l => PivotLayoutEdits.SetFieldCollapsed(l, item.Field, true))));
        }

        var field = labelColumn >= 0 ? FieldOfLabel(report, row, labelColumn) : InnermostFieldOf(report, row);
        if (labelColumn >= 0 && field is not null)
        {
            var sort = layout.Rows.First(p => p.Field == field).Sort;
            commands.Add(new GridCommand(PivotCommandIds.SortAscending, sort != PivotSort.Ascending,
                () => ReportEditAsync(l => PivotLayoutEdits.SetSort(l, field, PivotSort.Ascending))));
            commands.Add(new GridCommand(PivotCommandIds.SortDescending, sort != PivotSort.Descending,
                () => ReportEditAsync(l => PivotLayoutEdits.SetSort(l, field, PivotSort.Descending))));
        }
        if (valueColumn >= 0)
        {
            var vf = report.ValueFieldAt(row, valueColumn);
            if (field is not null && vf >= 0)
            {
                var sort = layout.Rows.First(p => p.Field == field).Sort;
                var smallest = new PivotSort(PivotSortDirection.Ascending, vf);
                var largest = new PivotSort(PivotSortDirection.Descending, vf);
                commands.Add(new GridCommand(PivotCommandIds.SortSmallestToLargest, sort != smallest,
                    () => ReportEditAsync(l => PivotLayoutEdits.SetSort(l, field, smallest))));
                commands.Add(new GridCommand(PivotCommandIds.SortLargestToSmallest, sort != largest,
                    () => ReportEditAsync(l => PivotLayoutEdits.SetSort(l, field, largest))));
            }
            // Show Details is always offered: the tab, the dialog or the Consumer takes the
            // records (ADR-0059). An empty cell has none to show.
            commands.Add(new GridCommand(PivotCommandIds.ShowDetails, row.ValueAt(valueColumn) is not null,
                () => ShowDetailsAsync(row, valueColumn)));
            if (vf >= 0)
            {
                // The panel opens in the pane, under the Value Field's entry: offered while the
                // pane shows that Value Field where the report does.
                var shownInPane = vf < PaneLayout.Values.Count && Equals(PaneLayout.Values[vf], layout.Values[vf]);
                commands.Add(new GridCommand(PivotCommandIds.ValueFieldSettings, shownInPane,
                    () => OpenFromReportAsync(new PivotEntry(PivotArea.Values, vf), Surface.ValueFieldSettings)));
            }
        }
        if (field is not null)
        {
            commands.Add(new GridCommand(PivotCommandIds.RemoveNamedField + ":" + CaptionOf(field), true,
                () => ReportEditAsync(l => l.PlacementOf(field) is { } at
                    ? PivotLayoutEdits.Remove(l, new PivotEntry(at.Area, at.Index))
                    : throw new ArgumentException($"'{field}' no longer stands in the layout."))));
        }
        commands.Add(_fieldListShown
            ? new GridCommand(PivotCommandIds.HideFieldList, true, () => SetFieldListShownAsync(false))
            : new GridCommand(PivotCommandIds.ShowFieldList, true, () => SetFieldListShownAsync(true)));
        return commands;
    }

    private Task ToggleFromMenuAsync(string field, PivotItemKey item, bool collapse)
    {
        var column = _report?.LabelColumns.Count == 1 ? 0 : Math.Max(0, _layout.Rows.ToList().FindIndex(p => p.Field == field));
        _focusAfterToggle = (new PivotToggle(field, item, "", !collapse), column);
        return ReportEditAsync(layout => PivotLayoutEdits.SetCollapsed(layout, field, item, collapse));
    }

    /// <summary>Shows or hides the Field List — its heading, the Pivot Toolbar, or the Context Menu —
    /// and tells a Consumer that binds it (ADR-0061). The user's choice holds until the Consumer's
    /// <see cref="ShowFieldList"/> itself changes: a Consumer that binds it hands the choice back,
    /// and one that does not keeps passing the value it always passed.</summary>
    private async Task SetFieldListShownAsync(bool shown)
    {
        if (_fieldListShown == shown)
            return;
        _fieldListShown = shown;
        if (!shown)
            EndDrag();
        if (!shown && _open is { OnToolbar: false })
            CloseOpenQuietly();
        StateHasChanged();
        await ShowFieldListChanged.InvokeAsync(shown);
    }

    /// <summary>The Item a row's Expand and Collapse act on: its own when it is an outer Item, its
    /// parent's when it is an innermost one, as Excel's Collapse on a detail row collapses the
    /// group it is in. None on a grand total row.</summary>
    private static (string Field, PivotItemKey Item)? CollapsibleItemOf(PivotReport report, PivotReportRow row)
    {
        var path = report.RowPath(row);
        for (var level = path.Count - 1; level >= 0; level--)
        {
            if (level < report.Layout.Rows.Count - 1)
                return path[level];
        }
        return null;
    }

    private static string? InnermostFieldOf(PivotReport report, PivotReportRow row)
        => report.RowPath(row) is { Count: > 0 } path ? path[^1].Field : null;

    private static string? FieldOfLabel(PivotReport report, PivotReportRow row, int labelColumn)
        => report.LabelColumns[labelColumn].Field ?? (row.ValueField < 0 ? InnermostFieldOf(report, row) : null);
}
