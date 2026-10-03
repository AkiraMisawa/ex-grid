namespace ExGrid.Docs.Examples.Sheet;

using ExSheet.Engine;

/// <summary>A cost centre from the application's ledger: its budget for the year and what has been booked against it.</summary>
public sealed record CostCentre(string Code, string Department, double Budget, double Actual)
{
    /// <summary>The Linked Table's name, as Formulas read it.</summary>
    public const string Table = "CostCentres";

    /// <summary>The table's columns, in the order every row is pushed.</summary>
    public static readonly string[] Columns = ["Code", "Department", "Budget", "Actual"];

    /// <summary>The column a row is read by: no code appears twice.</summary>
    public const string Key = "Code";

    /// <summary>The cost centre as the table's row: one Value per column, in order.</summary>
    public IReadOnlyList<Value?> ToRow() =>
        [Value.FromText(Code), Value.FromText(Department), Value.FromNumber(Budget), Value.FromNumber(Actual)];

    /// <summary>The ledger as it opens: invented figures, the same on every visit.</summary>
    public static CostCentre[] Sample() =>
    [
        new("CC-1100", "Finance", 1_240_000, 1_198_450),
        new("CC-1200", "Legal", 685_000, 712_300),
        new("CC-2300", "Operations", 3_420_000, 3_286_900),
        new("CC-3100", "Technology", 2_950_000, 3_104_750),
        new("CC-4100", "Marketing", 1_580_000, 1_402_200),
        new("CC-5200", "People", 920_000, 897_600),
    ];
}
