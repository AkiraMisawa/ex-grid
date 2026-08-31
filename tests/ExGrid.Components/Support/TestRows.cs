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

    internal static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book),
        new("Amount", ColumnType.Number, r => r.Amount),
        new("AsOf", ColumnType.Date, r => r.AsOf),
        new("Active", ColumnType.Boolean, r => r.Active),
    ];
}
