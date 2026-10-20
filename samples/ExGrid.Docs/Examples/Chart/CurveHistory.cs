using ExChart;
using ExChart.Engine;
using ExGrid.Chrome;
using ExGrid.Components;

namespace ExGrid.Docs.Examples.Chart;

/// <summary>A curve history, dates down and tenors across, for the Examples that chart a grid's selection.</summary>
public static class CurveHistory
{
    /// <summary>One date's zero rates, in percent.</summary>
    public sealed record Row(DateOnly Date, double Y1, double Y2, double Y5, double Y10);

    private static readonly DateOnly Start = new(2026, 10, 1);

    /// <summary>Ten business days of fixed sample rates.</summary>
    public static readonly IReadOnlyList<Row> Rows = Enumerable.Range(0, 10)
        .Select(i => new Row(Start.AddDays(i), 3.00 + i * 0.01, 3.10 + i * 0.012, 3.30 - i * 0.02, 3.60 - i * 0.01))
        .ToList();

    /// <summary>The grid's columns.</summary>
    public static readonly GridColumn<Row>[] Columns =
    [
        new("date", ColumnType.Date, r => r.Date, "Date"),
        new("y1", ColumnType.Number, r => r.Y1, "1Y", format: v => ((double)v).ToString("0.000")),
        new("y2", ColumnType.Number, r => r.Y2, "2Y", format: v => ((double)v).ToString("0.000")),
        new("y5", ColumnType.Number, r => r.Y5, "5Y", format: v => ((double)v).ToString("0.000")),
        new("y10", ColumnType.Number, r => r.Y10, "10Y", format: v => ((double)v).ToString("0.000")),
    ];

    private static readonly Dictionary<string, double> Tenors = new() { ["y1"] = 1, ["y2"] = 2, ["y5"] = 5, ["y10"] = 10 };

    /// <summary>Each row's key: its date.</summary>
    public static readonly Func<Row, object> KeyOf = r => r.Date;

    /// <summary>
    /// What the chart needs to know of the rows: how to find a row again by its key, what each column
    /// is (a level), and where a tenor column stands along a curve. A chart follows the rows it was made
    /// from: a recalculated rate is redrawn, a deleted row becomes a gap with a note.
    /// </summary>
    public static GridChartDeclarations<Row> Declarations() => new()
    {
        RowKey = KeyOf,
        RowOf = key => Rows.FirstOrDefault(r => r.Date == (DateOnly)key),
        KindOf = _ => SeriesKind.Level,
        PositionOf = column => Tenors.TryGetValue(column, out var years) ? years : null,
        PositionTitle = "Tenor (years)",
        RowName = r => r.Date.ToString("yyyy-MM-dd"),
    };
}
