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
/// Excel's PivotTable, drawn by ExGrid as that grid's Consumer (ADR-0058): the report filter band,
/// one ExGrid over the Pivot Report, and the Field List beside it (ADR-0060). ExPivot holds the
/// Pivot Layout and computes the report with ExPivot.Engine; ExGrid paints, selects, navigates,
/// copies and reports.
/// </summary>
/// <typeparam name="TRecord">The Consumer's Source Record type.</typeparam>
// This part is the report half: the one ExGrid, its columns, its label cells, its Context Menu
// and its double click (ADR-0058/0062).
public partial class ExPivot<TRecord>
{
    /// <summary>The widest a label column is sized to by its labels; a wider label is cut with an
    /// ellipsis, as text is (ADR-0016). A user's drag may make it wider.</summary>
    private const double MaxLabelWidthPx = 480;

    private const double MinLabelWidthPx = 60;

    // Row Kind from the row's role (ADR-0024/0058); one instance for every ExPivot.
    private static readonly Func<PivotReportRow, RowKind> RowKindOf = static row => row.Role switch
    {
        PivotRowRole.Item => RowKind.Detail,
        PivotRowRole.Group => RowKind.Group,
        _ => RowKind.Total,
    };

    // An error value is centred, as Excel centres it (ADR-0059); everything else follows the kind.
    private static readonly Func<PivotReportRow, GridColumn<PivotReportRow>, CellAlign> AlignOf =
        static (row, column) => column.Value(row) is PivotValue { IsError: true } ? CellAlign.Center : CellAlign.Auto;

    // A value cell paints the text the engine formatted; its raw form is the number (ADR-0005/0059).
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

    private PivotCube? _cube;
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

    /// <summary>Creates the component; the report is computed when its parameters arrive.</summary>
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

    private void Recompute()
    {
        var options = new PivotOptions { Culture = _culture, Label = Label };
        if (!PivotEngine.CanReuse(_cube, Records, Fields, _layout))
            _cube = PivotEngine.Aggregate(Records, Fields, _layout);
        var report = PivotEngine.Report(_cube!, _layout, options);
        // The order a selection is written in (ADR-0011): kept when only values moved.
        if (_report is null || !report.HasSameRowsAs(_report))
            _rowSequenceVersion++;
        _report = report;
        _items.Clear();
        BuildColumns();
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
            var width = _userWidths.TryGetValue(label.Name, out var user) ? user : LabelWidth(report, i);
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

    /// <summary>A label column's width, from its labels (ADR-0058): each label's text as the grid
    /// estimates it, plus its indent and its button, and the header's own need; bounded so one
    /// long label does not push the values off screen.</summary>
    private double LabelWidth(PivotReport report, int column)
    {
        var metrics = _metrics.CellMetrics;
        var indent = IndentPx;
        var widest = _metrics.HeaderRequiredPx(report.LabelColumns[column].Header, menuButton: false, sortable: false);
        foreach (var row in report.Rows)
        {
            var label = row.Labels[column];
            if (label.Text is null && label.Toggle is null)
                continue;
            var px = metrics.EstimatePx(label.Text ?? "") + (label.Indent * indent) + (label.Toggle is null ? 0 : indent);
            if (px > widest)
                widest = px;
        }
        return Math.Clamp(Math.Ceiling(widest), MinLabelWidthPx, MaxLabelWidthPx);
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
    /// One label cell (ADR-0058): the indent, the expand / collapse button of an outer Item, and the
    /// label. The button is plain markup with <c>ex-interactive</c> and <c>tabindex="-1"</c>; only in
    /// the one cell Space asks to enter is it a component, which takes DOM focus itself (ADR-0037).
    /// A double click on it stops there: its offsets are the button's (ADR-0062).
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
        if (RowHeight is { } rowHeight)
            builder.AddComponentParameter(16, nameof(ExGrid<PivotReportRow>.RowHeight), rowHeight);
        if (Density is { } density)
            builder.AddComponentParameter(17, nameof(ExGrid<PivotReportRow>.Density), density);
        if (CellMetrics is { } metrics)
            builder.AddComponentParameter(18, nameof(ExGrid<PivotReportRow>.CellMetrics), metrics);
        if (GridChrome is { } chrome)
            builder.AddComponentParameter(19, nameof(ExGrid<PivotReportRow>.Chrome), chrome);
        if (SelectionChanged.HasDelegate)
            builder.AddComponentParameter(20, nameof(ExGrid<PivotReportRow>.SelectionChanged), SelectionChanged);
        builder.AddComponentReferenceCapture(21, grid => _grid = (ExGrid<PivotReportRow>)grid);
        builder.CloseComponent();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // The toggled Item keeps the Focus, as it does in Excel: placed once the report it was
        // toggled into has reached the grid, under that report's order (ADR-0050 item 4, ADR-0011).
        if (_focusAfterToggle is { } pending && _grid is { } grid && _report is { } report)
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
        return ApplyAsync(PivotLayoutEdits.SetCollapsed(_layout, toggle.Field, toggle.Item, !toggle.IsCollapsed));
    }

    private async Task OnWidthChangedAsync(ColumnWidthChange change)
    {
        // A width the user dragged is theirs until the column leaves the report (ADR-0016).
        _userWidths[change.Column] = change.WidthPx;
        BuildColumns();
        StateHasChanged();
        await Task.CompletedTask;
    }

    // ---- The double click and Show Details (ADR-0062) ------------------------------------------

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

    private async Task ShowDetailsAsync(PivotReportRow row, int valueColumn)
    {
        if (!OnShowDetails.HasDelegate || _report is not { } report || !ReferenceEquals(row.Report, report))
            return;
        if (row.ValueAt(valueColumn) is null)
            return;
        var records = PivotEngine.RecordsBehind(Records, Fields, report, row, valueColumn);
        var details = new PivotDetails<TRecord>(
            records,
            Items(report.RowPath(row)),
            Items(report.ColumnPath(valueColumn)),
            report.ValueFieldAt(row, valueColumn) is var vf and >= 0 ? report.ValueCaptions[vf] : null);
        await OnShowDetails.InvokeAsync(details);
    }

    private IReadOnlyList<PivotDetailItem> Items(IReadOnlyList<(string Field, PivotItemKey Item)> path)
        => path.Select(step => new PivotDetailItem(CaptionOf(step.Field), ItemLabel(step.Field, step.Item))).ToArray();

    private string CaptionOf(string field) => Fields.FirstOrDefault(f => f.Name == field)?.Caption ?? field;

    private string ItemLabel(string field, PivotItemKey item)
        => ItemsOf(field).FirstOrDefault(i => i.Key.Equals(item))?.Label ?? item.Value ?? Word(PivotWords.Blank);

    // ---- The Context Menu (ADR-0036/0058) --------------------------------------------------------

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
        var row = context.Row;
        var labelColumn = report.LabelColumns.ToList().FindIndex(c => c.Name == context.Column);
        var valueColumn = labelColumn >= 0 ? -1 : report.ValueColumns.ToList().FindIndex(c => c.Name == context.Column);
        var commands = new List<GridCommand>();

        if (CollapsibleItemOf(report, row) is { } item)
        {
            var placement = _layout.Rows.First(p => p.Field == item.Field);
            var collapsed = placement.IsCollapsed(item.Item);
            commands.Add(new GridCommand(PivotCommandIds.Expand, collapsed,
                () => ToggleFromMenuAsync(item.Field, item.Item, collapse: false)));
            commands.Add(new GridCommand(PivotCommandIds.Collapse, !collapsed,
                () => ToggleFromMenuAsync(item.Field, item.Item, collapse: true)));
            commands.Add(new GridCommand(PivotCommandIds.ExpandField, placement.Collapsed || placement.ToggledItems.Count > 0,
                () => ApplyAsync(PivotLayoutEdits.SetFieldCollapsed(_layout, item.Field, false))));
            commands.Add(new GridCommand(PivotCommandIds.CollapseField, !placement.Collapsed || placement.ToggledItems.Count > 0,
                () => ApplyAsync(PivotLayoutEdits.SetFieldCollapsed(_layout, item.Field, true))));
        }

        var field = labelColumn >= 0 ? FieldOfLabel(report, row, labelColumn) : InnermostFieldOf(report, row);
        if (labelColumn >= 0 && field is not null)
        {
            var sort = _layout.Rows.First(p => p.Field == field).Sort;
            commands.Add(new GridCommand(PivotCommandIds.SortAscending, sort != PivotSort.Ascending,
                () => ApplyAsync(PivotLayoutEdits.SetSort(_layout, field, PivotSort.Ascending))));
            commands.Add(new GridCommand(PivotCommandIds.SortDescending, sort != PivotSort.Descending,
                () => ApplyAsync(PivotLayoutEdits.SetSort(_layout, field, PivotSort.Descending))));
        }
        if (valueColumn >= 0)
        {
            var vf = report.ValueFieldAt(row, valueColumn);
            if (field is not null && vf >= 0)
            {
                var sort = _layout.Rows.First(p => p.Field == field).Sort;
                var smallest = new PivotSort(PivotSortDirection.Ascending, vf);
                var largest = new PivotSort(PivotSortDirection.Descending, vf);
                commands.Add(new GridCommand(PivotCommandIds.SortSmallestToLargest, sort != smallest,
                    () => ApplyAsync(PivotLayoutEdits.SetSort(_layout, field, smallest))));
                commands.Add(new GridCommand(PivotCommandIds.SortLargestToSmallest, sort != largest,
                    () => ApplyAsync(PivotLayoutEdits.SetSort(_layout, field, largest))));
            }
            if (OnShowDetails.HasDelegate)
            {
                commands.Add(new GridCommand(PivotCommandIds.ShowDetails, row.ValueAt(valueColumn) is not null,
                    () => ShowDetailsAsync(row, valueColumn)));
            }
            if (vf >= 0)
            {
                commands.Add(new GridCommand(PivotCommandIds.ValueFieldSettings, true,
                    () => OpenFromReportAsync(new PivotEntry(PivotArea.Values, vf), Surface.ValueFieldSettings)));
            }
        }
        if (field is not null)
        {
            var at = _layout.PlacementOf(field)!.Value;
            commands.Add(new GridCommand(PivotCommandIds.RemoveNamedField + ":" + CaptionOf(field), true,
                () => ApplyAsync(PivotLayoutEdits.Remove(_layout, new PivotEntry(at.Area, at.Index)))));
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
        return ApplyAsync(PivotLayoutEdits.SetCollapsed(_layout, field, item, collapse));
    }

    private Task SetFieldListShownAsync(bool shown)
    {
        _fieldListShown = shown;
        if (!shown)
            CloseOpenQuietly();
        StateHasChanged();
        return Task.CompletedTask;
    }

    /// <summary>The Item a row's Expand and Collapse act on: its own when it is an outer Item, its
    /// parent's when it is an innermost one, as Excel's Collapse on a detail row collapses the
    /// group it is in. None on a grand total row.</summary>
    private (string Field, PivotItemKey Item)? CollapsibleItemOf(PivotReport report, PivotReportRow row)
    {
        var path = report.RowPath(row);
        for (var level = path.Count - 1; level >= 0; level--)
        {
            if (level < _layout.Rows.Count - 1)
                return path[level];
        }
        return null;
    }

    private string? InnermostFieldOf(PivotReport report, PivotReportRow row)
        => report.RowPath(row) is { Count: > 0 } path ? path[^1].Field : null;

    private string? FieldOfLabel(PivotReport report, PivotReportRow row, int labelColumn)
        => report.LabelColumns[labelColumn].Field ?? (row.ValueField < 0 ? InnermostFieldOf(report, row) : null);
}
