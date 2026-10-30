using ExGrid.Columns;

namespace ExGrid.Components.Tests.Support;

/// <summary>
/// Deliberately mutable: the ADR-0003 contract says an in-place rewrite does not reach
/// the screen, and proving that requires a row that CAN be rewritten in place.
/// </summary>
internal sealed class TestRow
{
    public string Book = "";
    public decimal Amount;
    public DateTime AsOf;
    public bool Active;
}

internal static class TestRows
{
    internal static TestRow[] Window() =>
    [
        new() { Book = "Alpha", Amount = 100.5m, AsOf = new DateTime(2026, 1, 5), Active = true },
        new() { Book = "Beta", Amount = -7m, AsOf = new DateTime(2026, 1, 6), Active = false },
        new() { Book = "Gamma", Amount = 0m, AsOf = new DateTime(2026, 1, 7), Active = true },
    ];

    /// <summary>A result too large to paint, for the virtualisation tests. Book labels
    /// are a constant length so an Auto width settles immediately and slice assertions
    /// are not chasing a growing column.</summary>
    internal static TestRow[] Many(int count)
    {
        var rows = new TestRow[count];
        for (var i = 0; i < count; i++)
        {
            rows[i] = new TestRow
            {
                Book = $"Row {i:D6}",
                Amount = i,
                AsOf = new DateTime(2026, 1, 1).AddDays(i % 365),
                Active = i % 2 == 0,
            };
        }

        return rows;
    }

    internal static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book),
        new("Amount", ColumnType.Number, r => r.Amount),
        new("AsOf", ColumnType.Date, r => r.AsOf),
        new("Active", ColumnType.Boolean, r => r.Active),
    ];

    /// <summary>
    /// More columns than any Viewport can hold, for the horizontal virtualisation tests.
    /// Every cell says which column it came from, so a test can tell exactly which
    /// columns reached the DOM. Fixed widths so the arithmetic under test is not chasing
    /// a growing Auto column.
    /// </summary>
    internal static GridColumn<TestRow>[] Wide(int count, double widthPx = 100)
    {
        var columns = new GridColumn<TestRow>[count];
        for (var i = 0; i < count; i++)
        {
            var index = i;
            columns[i] = new GridColumn<TestRow>(
                ColumnName(index),
                ColumnType.Text,
                r => $"{r.Book}/{index:D2}",
                width: new ColumnWidthSpec(ColumnWidth.Fixed(widthPx)));
        }

        return columns;
    }

    internal static string ColumnName(int index) => $"C{index:D2}";

    /// <summary>Which column a cell painted by <see cref="Wide"/> belongs to — the tail
    /// of its text, read back so header and body can be compared as column sets.</summary>
    internal static string ColumnOf(string cellText) => "C" + cellText[(cellText.IndexOf('/') + 1)..];
}
