using ExGrid;
using ExGrid.Columns;

namespace ExposureChart.Charting;

/// <summary>Whether one panel draws everything on one value axis or splits its series.</summary>
public enum ChartLayout
{
    /// <summary>One panel per Header Group; a Change series reads against a right-hand axis.</summary>
    PanelPerGroup,

    /// <summary>Every series on one panel and one axis, as Excel's default chart would draw them.</summary>
    OneAxis,
}

/// <summary>A point; a missing value is a gap, never a zero.</summary>
public readonly record struct ChartPoint(DateOnly X, decimal? Y);

/// <param name="Slot">The series' colour, by its leaf header: every "After" is one colour in every panel.</param>
/// <param name="Group">The Header Group the column stands under, or empty.</param>
/// <param name="Text">The column's own format, so a value reads as its cell does.</param>
public sealed record ChartSeries(
    string Column, string Name, string Group, string Leaf, int Slot, int GroupIndex,
    bool Secondary, IReadOnlyList<ChartPoint> Points, Func<decimal, string> Text);

public sealed record ChartPanel(string Title, IReadOnlyList<ChartSeries> Series);

/// <summary>What is drawn, resolved against the data as it is now.</summary>
/// <param name="Missing">Row keys of the definition the data no longer holds.</param>
public sealed record ChartModel(
    string XHeader,
    IReadOnlyList<DateOnly> Dates,
    IReadOnlyList<ChartPanel> Panels,
    IReadOnlyList<string> Leaves,
    int Missing);

public static class ChartModelBuilder
{
    /// <param name="rows">The Consumer's data: every row it holds, in any order.</param>
    /// <param name="isChange">Which columns hold a change rather than a level — the Consumer's
    /// declaration, not a guess from the header's wording.</param>
    public static ChartModel Build<TRow>(
        ChartDefinition definition,
        IReadOnlyList<GridColumn<TRow>> columns,
        IReadOnlyList<HeaderGroup> groups,
        IReadOnlyList<TRow> rows,
        Func<string, bool> isChange,
        ChartLayout layout)
    {
        var byName = columns.ToDictionary(c => c.Name);
        var x = byName[definition.XColumn];

        // A date axis plots by value, so the grid's row order plays no part.
        var charted = rows
            .Select(r => (Row: r, X: x.Value(r) is DateOnly d ? d : (DateOnly?)null))
            .Where(p => p.X is { } d && definition.RowKeys.Contains(d))
            .OrderBy(p => p.X)
            .ToList();
        var dates = charted.Select(p => p.X!.Value).ToList();
        var missing = definition.RowKeys.Count - dates.Distinct().Count();

        var tierOne = groups.Where(g => g.Tier == 1).ToList();
        string GroupOf(string column) => tierOne.FirstOrDefault(g => g.Columns.Contains(column))?.Label ?? "";

        var leaves = new List<string>();
        var groupNames = new List<string>();
        var series = new List<ChartSeries>();
        foreach (var name in definition.SeriesColumns)
        {
            var column = byName[name];
            var group = GroupOf(name);
            if (!leaves.Contains(column.Header)) leaves.Add(column.Header);
            if (!groupNames.Contains(group)) groupNames.Add(group);

            // Every tier's label above the leaf, top-down, then the leaf: "EPE(0) · Before".
            var path = groups.Where(g => g.Columns.Contains(name)).OrderByDescending(g => g.Tier)
                .Select(g => g.Label).Append(column.Header);
            var points = charted.Select(p => new ChartPoint(p.X!.Value, ToDecimal(column.Value(p.Row)))).ToList();
            var format = column.Format;
            series.Add(new ChartSeries(
                name, string.Join(" · ", path), group, column.Header,
                leaves.IndexOf(column.Header), groupNames.IndexOf(group),
                layout == ChartLayout.PanelPerGroup && isChange(name),
                points,
                format is null ? v => v.ToString("N2") : v => format(v)));
        }

        var panels = layout == ChartLayout.OneAxis
            ? [new ChartPanel("", series)]
            : series.GroupBy(s => s.Group).Select(g => new ChartPanel(g.Key, g.ToList())).ToList();
        return new ChartModel(x.Header, dates, panels, leaves, missing);
    }

    private static decimal? ToDecimal(object? value) => value switch
    {
        null => null,
        decimal d => d,
        double d when double.IsFinite(d) => (decimal)d,
        float f when float.IsFinite(f) => (decimal)f,
        int i => i,
        long l => l,
        _ => null,
    };
}
