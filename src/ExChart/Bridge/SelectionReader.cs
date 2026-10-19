using global::ExChart.Engine;
using global::ExGrid;
using global::ExGrid.Columns;
using global::ExGrid.Selection;

namespace ExChart;

/// <summary>What the bridge reads of a grid: its columns, groups and order, and the rows at positions in it.</summary>
internal sealed record GridState<TRow>(
    IReadOnlyList<GridColumn<TRow>> Columns,
    IReadOnlyList<HeaderGroup> Groups,
    int RowSequenceVersion,
    IReadOnlyList<TRow> Window,
    int WindowStart,
    Func<RowRange, Task<IReadOnlyList<TRow>>>? Fetch);

/// <summary>
/// Turns a grid selection into a <see cref="GridChart"/>, and a <see cref="GridChart"/> into a Chart
/// Definition (ADR-0251, ADR-0258). What it cannot make sense of it refuses by name, rather than
/// drawing something plausible (spine rule 1).
/// </summary>
internal static class SelectionReader
{
    internal const string Stale = "The rows were reordered after the selection was made. Select again.";

    private static bool IsNumber<TRow>(GridColumn<TRow> c) => c.Type == ColumnType.Number && !c.IsMarkColumn && c.Actions.Count == 0;

    private static bool IsAxis<TRow>(GridColumn<TRow> c, GridChartDeclarations<TRow> d) =>
        c.Type == ColumnType.Date || (IsNumber(c) && d.AxisColumns.Contains(c.Name));

    private static double? PositionOf<TRow>(GridColumn<TRow> c, GridChartDeclarations<TRow> d) =>
        IsNumber(c) ? d.PositionOf?.Invoke(c.Name) : null;

    private static string Headers<TRow>(IEnumerable<GridColumn<TRow>> columns) => string.Join(", ", columns.Select(c => c.Header));

    /// <summary>The orientations the selection's columns declare, before any orientation the Consumer fixed.</summary>
    internal static IReadOnlyList<ChartOrientation> Declared<TRow>(GridState<TRow> g, IReadOnlyList<SelectionRange> ranges, GridChartDeclarations<TRow> d)
    {
        var covered = Covered(g, Whole(g, ranges));
        var found = new List<ChartOrientation>(2);
        if (covered.Any(c => IsAxis(c, d))) found.Add(ChartOrientation.SeriesInColumns);
        if (covered.Any(c => PositionOf(c, d) is not null)) found.Add(ChartOrientation.SeriesInRows);
        return found;
    }

    // One cell is Excel's "chart the table around it": every column of the Window (ADR-0251).
    private static bool IsOneCell(IReadOnlyList<SelectionRange> ranges) => ranges is [{ RowCount: 1, ColumnCount: 1 }];

    private static IReadOnlyList<SelectionRange> Whole<TRow>(GridState<TRow> g, IReadOnlyList<SelectionRange> ranges) =>
        IsOneCell(ranges) && g.Window.Count > 0 && g.Columns.Count > 0
            ? [new SelectionRange(g.WindowStart, 0, g.Window.Count, g.Columns.Count)]
            : ranges;

    private static List<GridColumn<TRow>> Covered<TRow>(GridState<TRow> g, IReadOnlyList<SelectionRange> ranges) =>
        ranges.SelectMany(r => Enumerable.Range(r.LeftColumn, r.ColumnCount)).Where(i => i < g.Columns.Count)
            .Distinct().Order().Select(i => g.Columns[i]).ToList();

    private static string GroupOf(IReadOnlyList<HeaderGroup> groups, string column) =>
        groups.FirstOrDefault(x => x.Tier == 1 && x.Columns.Contains(column))?.Label ?? "";

    /// <summary>
    /// Reads <paramref name="ranges"/>, taken under <paramref name="version"/>, as <paramref name="orientation"/>;
    /// into <paramref name="target"/> when the series are added to a chart already drawn (U7).
    /// </summary>
    internal static async Task<(GridChart? Chart, string? Refusal)> ReadAsync<TRow>(
        GridState<TRow> g, IReadOnlyList<SelectionRange> ranges, int version, GridChartDeclarations<TRow> d,
        ChartOrientation? orientation, ChartTarget? target)
    {
        if (ranges.Count == 0) return (null, "Nothing is selected.");
        // The gesture carries the order it was taken against; another order now means its positions
        // name other rows (spine rule 6).
        if (version != g.RowSequenceVersion) return (null, Stale);
        if (IsOneCell(ranges) && g.Window.Count == 0) return (null, "The grid shows no rows to chart.");

        var oneCell = IsOneCell(ranges);
        ranges = Whole(g, ranges);
        var covered = Covered(g, ranges);
        var declared = Declared(g, ranges, d);

        if (target is { } t)
        {
            // An added series is read the target's way; a selection that declares only the other way
            // is a curve into a history, or a history into a curve, and is refused by name (ADR-0258).
            if (declared.Count > 0 && !declared.Contains(t.Chart.Orientation))
                return (null, t.Chart.Orientation == ChartOrientation.SeriesInColumns
                    ? $"The selection charts curves across declared positions, but '{t.Name}' is drawn along {AxisHeader(g, t.Chart)}."
                    : $"The selection charts along {Headers(covered.Where(c => IsAxis(c, d)))}, but '{t.Name}' is drawn across declared positions.");
            orientation = t.Chart.Orientation;
        }
        else
        {
            orientation ??= d.Orientation ?? (declared.Count == 1 ? declared[0] : null);
            if (orientation is null)
                return (null, declared.Count == 0
                    ? "The selection has no axis column (dates or numbers) and no columns with declared positions, so it has no horizontal axis."
                    : "The selection can be charted with series in columns or in rows; choose one.");
        }

        var positions = ranges.SelectMany(r => Enumerable.Range(r.TopRow, r.RowCount)).Distinct().Order().ToList();
        var rows = await RowsAsync(g, positions);
        if (rows is null) return (null, "The grid could not hand over every selected row; select fewer rows.");

        var made = orientation == ChartOrientation.SeriesInColumns
            ? Columns(g, ranges, covered, rows, d, oneCell, target)
            : Rows(g, ranges, covered, rows, d, oneCell, target);
        if (made.Refusal is not null) return made;

        var chart = made.Chart!;
        if (target is { } into)
            chart = into.Chart with { Series = into.Chart.Series.Concat(chart.Series).ToEquatableList() };
        try
        {
            chart = OwnPanelsForDiffs(g, chart, d, target?.Chart.Series.Count ?? 0);
            _ = Draw(g.Columns, g.Groups, chart, d);
        }
        catch (ChartRefusedException e)
        {
            return (null, e.Message);
        }
        return (chart, null);
    }

    private static string AxisHeader<TRow>(GridState<TRow> g, GridChart chart) =>
        g.Columns.FirstOrDefault(c => c.Name == chart.AxisColumn)?.Header ?? chart.AxisColumn ?? "";

    private static async Task<Dictionary<int, TRow>?> RowsAsync<TRow>(GridState<TRow> g, List<int> positions)
    {
        var rows = new Dictionary<int, TRow>(positions.Count);
        var end = g.WindowStart + g.Window.Count;
        foreach (var run in Runs(positions))
        {
            if (run.Start >= g.WindowStart && run.Start + run.Count <= end)
            {
                for (var i = 0; i < run.Count; i++) rows[run.Start + i] = g.Window[run.Start + i - g.WindowStart];
                continue;
            }
            if (g.Fetch is null) return null;
            var fetched = await g.Fetch(run);
            if (fetched.Count < run.Count) return null;
            for (var i = 0; i < run.Count; i++) rows[run.Start + i] = fetched[i];
        }
        return rows;
    }

    private static IEnumerable<RowRange> Runs(List<int> positions)
    {
        for (var i = 0; i < positions.Count;)
        {
            var j = i;
            while (j + 1 < positions.Count && positions[j + 1] == positions[j] + 1) j++;
            yield return new RowRange(positions[i], j - i + 1);
            i = j + 1;
        }
    }

    private static (GridChart? Chart, string? Refusal) Columns<TRow>(
        GridState<TRow> g, IReadOnlyList<SelectionRange> ranges, List<GridColumn<TRow>> covered, Dictionary<int, TRow> rows,
        GridChartDeclarations<TRow> d, bool oneCell, ChartTarget? target)
    {
        var axes = covered.Where(c => IsAxis(c, d)).ToList();
        GridColumn<TRow> axis;
        if (target is { } t)
        {
            axis = g.Columns.FirstOrDefault(c => c.Name == t.Chart.AxisColumn)!;
            if (axis is null) return (null, $"'{t.Name}' is drawn along {t.Chart.AxisColumn}, which is no longer in the grid.");
            if (axes.Any(c => c.Name != axis.Name))
                return (null, $"The selection charts along {Headers(axes.Where(c => c.Name != axis.Name))}, but '{t.Name}' is drawn along {axis.Header}.");
        }
        else
        {
            if (axes.Count == 0) return (null, "The selection has no axis column (dates or numbers) to put along the horizontal axis.");
            if (axes.Count > 1) return (null, $"The selection has {axes.Count} axis columns ({Headers(axes)}); choose one for the horizontal axis.");
            axis = axes[0];
        }

        var others = covered.Where(c => c.Name != axis.Name).ToList();
        var notNumbers = others.Where(c => !IsNumber(c)).ToList();
        if (notNumbers.Count > 0 && !oneCell)
            return (null, $"{Headers(notNumbers)} cannot be plotted: not a number column.");
        var figures = others.Where(IsNumber).ToList();
        if (figures.Count == 0) return (null, $"The selection has only {axis.Header}; select the figures to plot beside it.");

        var series = new List<GridChartSeries>();
        foreach (var column in figures)
        {
            if (target?.Chart.Series.Any(s => s is ColumnSeries c && c.Column == column.Name) == true)
                return (null, $"{column.Header} is already in '{target.Name}'.");
            var index = IndexOf(g.Columns, column);
            var keyed = new List<KeyedPosition>();
            var seen = new HashSet<object>();
            foreach (var position in ranges.Where(r => index >= r.LeftColumn && index <= r.RightColumn)
                         .SelectMany(r => Enumerable.Range(r.TopRow, r.RowCount)).Distinct().Order())
            {
                var row = rows[position];
                var key = d.RowKey(row);
                if (!seen.Add(key)) continue;
                if (AxisX(axis.Value(row)) is not { } x)
                    return (null, $"A selected row has no {axis.Header}, so it has no place along the axis.");
                keyed.Add(new KeyedPosition(key, x));
            }
            series.Add(new ColumnSeries(column.Name, GroupOf(g.Groups, column.Name), column.Name, keyed.ToEquatableList()));
        }
        return (new GridChart(ChartOrientation.SeriesInColumns, axis.Name, series.ToEquatableList()), null);
    }

    private static (GridChart? Chart, string? Refusal) Rows<TRow>(
        GridState<TRow> g, IReadOnlyList<SelectionRange> ranges, List<GridColumn<TRow>> covered, Dictionary<int, TRow> rows,
        GridChartDeclarations<TRow> d, bool oneCell, ChartTarget? target)
    {
        // A number column with no position was chosen and cannot be drawn on this axis: refused by
        // name, not dropped (ADR-0251). A text or date column names the row, and is not charted.
        var unpositioned = covered.Where(c => IsNumber(c) && PositionOf(c, d) is null).ToList();
        if (unpositioned.Count > 0 && !oneCell)
            return (null, $"{Headers(unpositioned)} has no declared position on the {Title(d)} axis, so it cannot be drawn here.");

        var all = g.Columns.Where(c => PositionOf(c, d) is not null).ToList();
        var chosen = covered.Where(c => PositionOf(c, d) is not null).ToList();
        if (chosen.Count == 0) return (null, $"The selection covers no column with a position on the {Title(d)} axis.");
        // A curve needs two points: a selection over one positioned column stands for its rows' whole curves.
        var whole = chosen.Count <= 1;
        var groups = all.Select(c => GroupOf(g.Groups, c.Name)).Distinct().ToList();

        var series = new List<GridChartSeries>();
        var seen = new HashSet<object>();
        foreach (var position in ranges.SelectMany(r => Enumerable.Range(r.TopRow, r.RowCount)).Distinct().Order())
        {
            var row = rows[position];
            var key = d.RowKey(row);
            if (!seen.Add(key)) continue;
            var name = d.RowName?.Invoke(row) ?? key.ToString() ?? "";
            foreach (var group in groups)
            {
                var inGroup = (whole ? all : chosen).Where(c => GroupOf(g.Groups, c.Name) == group).ToList();
                if (inGroup.Count == 0) continue;
                if (inGroup.Count == 1 && !whole) inGroup = all.Where(c => GroupOf(g.Groups, c.Name) == group).ToList();
                if (target?.Chart.Series.Any(s => s is RowSeries r && Equals(r.RowKey, key) && r.Group == group) == true)
                    return (null, $"{Label(name, group)} is already in '{target.Name}'.");
                var kinds = inGroup.Select(c => d.KindOf?.Invoke(c.Name) ?? SeriesKind.Undeclared).Distinct().ToList();
                if (kinds.Count > 1)
                    return (null, $"{(group.Length == 0 ? "The positioned columns" : group)} mix Series Kinds ({Headers(inGroup)}), so they are not one curve.");
                var ordered = inGroup.OrderBy(c => PositionOf(c, d)).Select(c => c.Name).ToEquatableList();
                var id = group.Length == 0 ? $"{key}" : $"{key} · {group}";
                series.Add(new RowSeries(id, "", key, name, group, ordered));
            }
        }
        return (new GridChart(ChartOrientation.SeriesInRows, null, series.ToEquatableList()), null);
    }

    private static int IndexOf<TRow>(IReadOnlyList<GridColumn<TRow>> columns, GridColumn<TRow> column)
    {
        for (var i = 0; i < columns.Count; i++) if (ReferenceEquals(columns[i], column)) return i;
        return -1;
    }

    private static string Title<TRow>(GridChartDeclarations<TRow> d) => d.PositionTitle.Length == 0 ? "positions'" : d.PositionTitle;

    private static string Label(string name, string group) => group.Length == 0 ? name : $"{name} · {group}";

    // A series with negative values beside positive ones (a Diff) goes to a panel of its own by
    // default, because aligning zeros for it costs the other series half the panel (ADR-0251).
    // Decided once, when the series is made, so a recalculation never moves it between panels.
    private static GridChart OwnPanelsForDiffs<TRow>(GridState<TRow> g, GridChart chart, GridChartDeclarations<TRow> d, int kept)
    {
        var definition = Draw(g.Columns, g.Groups, chart, d, validate: false);
        bool Mixed(int i)
        {
            var values = definition.Series[i].Points.Where(p => p.Y is not null).Select(p => p.Y!.Value.ToDouble()).ToList();
            return values.Any(v => v < 0) && values.Any(v => v > 0);
        }
        var series = chart.Series.ToList();
        for (var i = kept; i < series.Count; i++)
        {
            var panel = series[i].Panel;
            if (!Mixed(i) || !series.Where((s, j) => j != i && s.Panel == panel).Any()) continue;
            series[i] = series[i] with { Panel = definition.Series[i].Name };
        }
        return chart with { Series = series.ToEquatableList() };
    }

    /// <summary>
    /// The Chart Definition <paramref name="chart"/> stands for now: each value read by Row Key from the
    /// Consumer's data (ADR-0258), a row that no longer exists a gap, and a Note that says how many.
    /// </summary>
    internal static ChartDefinition Draw<TRow>(
        IReadOnlyList<GridColumn<TRow>> columns, IReadOnlyList<HeaderGroup> groups, GridChart chart, GridChartDeclarations<TRow> d, bool validate = true)
    {
        var byName = columns.ToDictionary(c => c.Name);
        var gone = new HashSet<object>();
        var missing = new List<string>();
        TRow? RowOf(object key)
        {
            var row = d.RowOf(key);
            if (row is null) gone.Add(key);
            return row;
        }

        ChartAxis axis;
        var series = new List<ChartSeries>(chart.Series.Count);
        if (chart.Orientation == ChartOrientation.SeriesInColumns)
        {
            if (!byName.TryGetValue(chart.AxisColumn ?? "", out var x))
                throw new ChartRefusedException($"The chart is drawn along {chart.AxisColumn}, which is no longer in the grid.");
            axis = x.Type == ColumnType.Date ? ChartAxis.Dates(x.Header) : ChartAxis.Numbers(x.Header);
            foreach (var s in chart.Series.OfType<ColumnSeries>())
            {
                byName.TryGetValue(s.Column, out var column);
                if (column is null) missing.Add(s.Column);
                var points = s.Rows.Select(r =>
                {
                    var row = RowOf(r.Key);
                    if (row is null) return new ChartPoint(r.X, null);
                    return new ChartPoint(AxisX(x.Value(row)) ?? r.X, column is null ? null : ValueOf(column.Value(row)));
                }).OrderBy(p => p.X).ToEquatableList();
                var path = groups.Where(gr => gr.Columns.Contains(s.Column)).OrderByDescending(gr => gr.Tier).Select(gr => gr.Label);
                var header = column?.Header ?? s.Column;
                var group = GroupOf(groups, s.Column);
                series.Add(new ChartSeries
                {
                    Id = s.Id,
                    Name = string.Join(" · ", path.Append(header)),
                    Kind = d.KindOf?.Invoke(s.Column) ?? SeriesKind.Undeclared,
                    Panel = s.Panel,
                    Keys = new AppearanceKeys(header, group.Length == 0 ? null : group),
                    Points = points,
                    Segments = EquatableList<Segment>.Empty,
                    Format = column?.Format,
                });
            }
        }
        else
        {
            var used = chart.Series.OfType<RowSeries>().SelectMany(s => s.Columns).Distinct()
                .Select(n => byName.GetValueOrDefault(n)).OfType<GridColumn<TRow>>()
                .Select(c => (Position: d.PositionOf?.Invoke(c.Name) ?? 0, c.Header)).Distinct().OrderBy(p => p.Position);
            axis = ChartAxis.Numbers(d.PositionTitle) with { Scale = d.PositionScale, Named = used.ToEquatableList() };
            foreach (var s in chart.Series.OfType<RowSeries>())
            {
                var row = RowOf(s.RowKey);
                var cols = s.Columns.Select(n => byName.GetValueOrDefault(n)).ToList();
                foreach (var (n, c) in s.Columns.Zip(cols)) if (c is null) missing.Add(n);
                var points = cols.OfType<GridColumn<TRow>>()
                    .Select(c => new ChartPoint(d.PositionOf?.Invoke(c.Name) ?? 0, row is null ? null : ValueOf(c.Value(row))))
                    .OrderBy(p => p.X).ToEquatableList();
                var name = row is null ? s.Name : d.RowName?.Invoke(row) ?? s.Name;
                var first = cols.OfType<GridColumn<TRow>>().FirstOrDefault();
                series.Add(new ChartSeries
                {
                    Id = s.Id,
                    Name = Label(name, s.Group),
                    Kind = first is null ? SeriesKind.Undeclared : d.KindOf?.Invoke(first.Name) ?? SeriesKind.Undeclared,
                    Panel = s.Panel,
                    Keys = new AppearanceKeys(name, s.Group.Length == 0 ? null : s.Group),
                    Points = points,
                    Segments = EquatableList<Segment>.Empty,
                    Format = first?.Format,
                });
            }
        }

        var notes = new List<string>();
        // A row that no longer exists is a gap, never zero, and the chart says so (ADR-0258, M3-Q3).
        if (gone.Count == 1) notes.Add("1 of the selected rows no longer exists.");
        else if (gone.Count > 1) notes.Add($"{gone.Count} of the selected rows no longer exist.");
        foreach (var name in missing.Distinct()) notes.Add($"{name} is no longer in the grid, so it is drawn without values.");

        if (!validate)
        {
            // Read before any series has its Series Kind checked, to see its values' signs.
            series = series.Select(s => s.Kind == SeriesKind.Undeclared ? s with { Kind = SeriesKind.Level } : s).ToList();
        }
        return new ChartDefinition(axis, series.ToEquatableList(), notes.ToEquatableList());
    }

    /// <summary>A cell's value as a chart value, in the type it came in (ADR-0251); null for an empty or non-numeric value.</summary>
    internal static ChartValue? ValueOf(object? value) => value switch
    {
        decimal m => m,
        double v when double.IsFinite(v) => v,
        float f when float.IsFinite(f) => (double)f,
        int i => (decimal)i,
        long l => (decimal)l,
        short s => (decimal)s,
        _ => null,
    };

    /// <summary>An axis cell's value as a position: a date's day number, or a number.</summary>
    internal static double? AxisX(object? value) => value switch
    {
        DateOnly date => date.DayNumber,
        DateTime time => DateOnly.FromDateTime(time).DayNumber,
        DateTimeOffset offset => DateOnly.FromDateTime(offset.Date).DayNumber,
        _ => ValueOf(value)?.ToDouble(),
    };
}
