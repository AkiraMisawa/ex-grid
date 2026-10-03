namespace ExGrid.Docs.Examples.Snapshot;

/// <summary>An invented ledger account, with a value of every kind a Snapshot holds. A null is a
/// Blank.</summary>
public sealed record Account(
    long Id,
    string Code,
    string? Desk,
    decimal? Balance,
    double? Rate,
    DateOnly? Opened,
    bool? Active);

public static class Accounts
{
    public static Account[] Sample() =>
    [
        new(1, "004417", "Rates", 1_250_000.00m, 0.0425, new DateOnly(2019, 3, 14), true),
        new(2, "004418", "Rates", -86_400.50m, 0.0410, new DateOnly(2019, 3, 14), true),
        new(3, "010032", "Credit", 7_340_215.37m, 0.0612, new DateOnly(2021, 11, 2), true),
        new(4, "010033", "", 0.00m, 0.0, new DateOnly(2022, 1, 5), false),
        new(5, "020101", "FX", null, null, new DateOnly(2023, 6, 30), true),
        new(6, "020102", null, 15_000_000.00m, 0.0398, null, null),
        new(7, "030550", "Equities", 942_118.04m, double.NaN, new DateOnly(2024, 9, 1), true),
        new(8, "030551", "Equities", 12.34m, 0.0, new DateOnly(2025, 2, 28), false),
    ];
}
