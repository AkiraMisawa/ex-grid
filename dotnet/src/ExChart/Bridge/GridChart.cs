using global::ExChart.Engine;

namespace ExChart;

/// <summary>
/// Whether a selection's series run down its columns or along its rows (ADR-0258). It comes from
/// the grid's declarations, never from the selection's shape.
/// </summary>
public enum ChartOrientation
{
    /// <summary>Each column is a series along an axis column (dates or numbers): a history.</summary>
    SeriesInColumns,

    /// <summary>Each row is a series across columns at declared positions (tenors): a curve.</summary>
    SeriesInRows,
}

/// <summary>A row a series was made from, by its Row Key, with the axis value it had then.</summary>
/// <param name="Key">The row's Row Key (ADR-0140).</param>
/// <param name="X">The row's axis value when the chart was made, which places its gap once the row is gone.</param>
public sealed record KeyedPosition(object Key, double X);

/// <summary>One series of a <see cref="GridChart"/>, in the data's terms (ADR-0251).</summary>
/// <param name="Id">The series' identity in the Chart Definition.</param>
/// <param name="Panel">The panel it was given when it was made: its Header Group, or a panel of its own for a Diff.</param>
public abstract record GridChartSeries(string Id, string Panel);

/// <summary>A column, by its name, at the rows it was made from (series in columns).</summary>
/// <param name="Id">The series' identity: the column's name.</param>
/// <param name="Panel">The panel it was given when it was made.</param>
/// <param name="Column">The column's name.</param>
/// <param name="Rows">The rows it is charted at, by Row Key: its own, as every Excel series has its own ranges (U8).</param>
public sealed record ColumnSeries(string Id, string Panel, string Column, EquatableList<KeyedPosition> Rows) : GridChartSeries(Id, Panel);

/// <summary>A row, by its Row Key, across columns at declared positions (series in rows).</summary>
/// <param name="Id">The series' identity: the row's key and its Header Group.</param>
/// <param name="Panel">The panel it was given when it was made.</param>
/// <param name="RowKey">The row's Row Key (ADR-0140).</param>
/// <param name="Name">The row's name when the chart was made, kept for the legend once the row is gone.</param>
/// <param name="Group">The Header Group the columns stand under, or empty.</param>
/// <param name="Columns">The columns it is charted across, by name, in their positions' order.</param>
public sealed record RowSeries(string Id, string Panel, object RowKey, string Name, string Group, EquatableList<string> Columns) : GridChartSeries(Id, Panel);

/// <summary>
/// A chart made from a grid selection: the set of Row Keys and columns it was made from, never the
/// positions it was painted at (ADR-0258). A recalculated value is redrawn, a row that no longer exists
/// becomes a gap, and a new row is never added.
/// </summary>
/// <param name="Orientation">Whether its series are columns or rows.</param>
/// <param name="AxisColumn">The axis column of a chart of series in columns; null for series in rows, whose axis is the declared positions.</param>
/// <param name="Series">Its series, in the order they were made and added.</param>
public sealed record GridChart(ChartOrientation Orientation, string? AxisColumn, EquatableList<GridChartSeries> Series)
{
    /// <summary>Every Row Key some series is charted at.</summary>
    public IReadOnlySet<object> RowKeys => Series
        .SelectMany(s => s switch { ColumnSeries c => c.Rows.Select(r => r.Key), RowSeries r => [r.RowKey], _ => [] })
        .ToHashSet();
}

/// <summary>A chart the user registered as a target of "Add to chart" (ADR-0258, U7).</summary>
/// <param name="Name">The chart's name, as the menu item says it.</param>
/// <param name="Chart">The chart as it stands.</param>
public sealed record ChartTarget(string Name, GridChart Chart);

/// <summary>Where a click on a chart's point lands in the grid (ADR-0258, M3-Q6).</summary>
/// <param name="RowKey">The point's row, by its Row Key.</param>
/// <param name="Column">The point's column, by its name.</param>
public sealed record GridCell(object RowKey, string Column);
