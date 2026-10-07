using System.Globalization;
using ExPivot.Engine;

namespace ExPivot.Engine.Tests;

/// <summary>A sale, the Source Record every test pivots.</summary>
internal sealed record Sale(string? Region, string Product, DateTime Date, decimal Amount, int Quantity, bool Online);

/// <summary>
/// The test data and the ways a test reads a report back: each row as one line — its role, its
/// labels and its values as painted — so that a test states the whole report it expects.
/// </summary>
internal static class Pivot
{
    //  Region  Product  Date        Amount  Quantity  Online
    //  East    Apples   2026-01-10  100     10        TRUE
    //  East    Pears    2026-01-12  50      5         FALSE
    //  East    Apples   2026-02-03  30      3         TRUE
    //  West    Apples   2026-01-20  70      7         FALSE
    //  West    Plums    2026-02-15  20      2         TRUE
    //  North   Pears    2026-02-18  10      1         FALSE
    //  (blank) Plums    2026-03-01  5       1         TRUE
    public static readonly Sale[] Sales =
    [
        new("East", "Apples", new DateTime(2026, 1, 10), 100m, 10, true),
        new("East", "Pears", new DateTime(2026, 1, 12), 50m, 5, false),
        new("East", "Apples", new DateTime(2026, 2, 3), 30m, 3, true),
        new("West", "Apples", new DateTime(2026, 1, 20), 70m, 7, false),
        new("West", "Plums", new DateTime(2026, 2, 15), 20m, 2, true),
        new("North", "Pears", new DateTime(2026, 2, 18), 10m, 1, false),
        new(null, "Plums", new DateTime(2026, 3, 1), 5m, 1, true),
    ];

    public static readonly PivotField<Sale>[] Fields =
    [
        new("Region", PivotFieldType.Text, s => s.Region),
        new("Product", PivotFieldType.Text, s => s.Product),
        new("Date", PivotFieldType.Date, s => s.Date),
        new("Amount", PivotFieldType.Number, s => s.Amount),
        new("Quantity", PivotFieldType.Number, s => s.Quantity),
        new("Online", PivotFieldType.Boolean, s => s.Online),
    ];

    public static readonly PivotOptions EnUs = new() { Culture = CultureInfo.GetCultureInfo("en-US") };

    public static IReadOnlyDictionary<string, PivotFieldInfo> Infos { get; } =
        Fields.ToDictionary(f => f.Name, f => f.Info);

    public static PivotFieldInfo Info(string name) => Infos[name];

    public static PivotReport Report(PivotLayout layout, IReadOnlyList<Sale>? sales = null, PivotOptions? options = null)
        => PivotEngine.Compute(sales ?? Sales, Fields, layout, options ?? EnUs);

    public static PivotReport Report<T>(IReadOnlyList<T> records, IReadOnlyList<PivotField<T>> fields, PivotLayout layout)
        => PivotEngine.Compute(records, fields, layout, EnUs);

    public static PivotFieldPlacement P(string field) => new(field);

    public static PivotValueField Sum(string field) => new(field, PivotAggregation.Sum);

    public static PivotValueField Value(string field, PivotAggregation aggregation) => new(field, aggregation);

    public static PivotLayout RowsBy(params string[] fields) => new()
    {
        Rows = fields.Select(P).ToArray(),
        Values = [Sum("Amount")],
    };

    /// <summary>
    /// One line per row: the role's letter (i Item, g Group, s Subtotal, t Grand total), each
    /// label — indented two spaces a level, <c>[-]</c> or <c>[+]</c> before an Item with a
    /// button — then <c>||</c> and each value as painted, empty for an empty cell.
    /// </summary>
    public static string[] Lines(PivotReport report)
        => report.Rows.Select(row =>
        {
            var role = row.Role switch
            {
                PivotRowRole.Item => "i",
                PivotRowRole.Group => "g",
                PivotRowRole.Subtotal => "s",
                _ => "t",
            };
            var labels = row.Labels.Select(label =>
                new string(' ', label.Indent * 2)
                + (label.Toggle is { } toggle ? toggle.IsCollapsed ? "[+]" : "[-]" : "")
                + (label.Text ?? ""));
            var values = Enumerable.Range(0, report.ValueColumns.Count).Select(i => report.ValueAt(row, i)?.Text ?? "");
            return (role + " " + string.Join(" | ", labels) + " || " + string.Join(" | ", values)).TrimEnd();
        }).ToArray();

    /// <summary>The leaf headers, label columns first.</summary>
    public static string[] Headers(PivotReport report)
        => report.LabelColumns.Select(c => c.Header).Concat(report.ValueColumns.Select(c => c.Header)).ToArray();

    /// <summary>Each Header Group span as <c>Label@first+count t{tier}x{span}</c>, first being the
    /// value column's index.</summary>
    public static string[] Spans(PivotReport report)
        => report.HeaderSpans
            .OrderBy(s => -s.Tier).ThenBy(s => s.FirstColumn)
            .Select(s => $"{s.Label}@{s.FirstColumn}+{s.ColumnCount} t{s.Tier}x{s.TierSpan}")
            .ToArray();

    /// <summary>A row's value in one column as painted, or empty.</summary>
    public static string Cell(PivotReport report, int row, int column) => report.ValueAt(report.Rows[row], column)?.Text ?? "";
}
