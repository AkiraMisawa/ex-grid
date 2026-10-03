namespace ExGrid.Docs.Examples.Sheet;

using System.Globalization;
using ExSheet.Engine;

/// <summary>A variance report that reads the ledger's cost centres as a Linked Table.</summary>
public static class CostReport
{
    /// <summary>The report, as a Sheet Document. It records the table's declaration, never its rows.</summary>
    public static SheetDocument Document()
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        sheet.DeclareLinkedTable(CostCentre.Table, CostCentre.Columns, CostCentre.Key);

        string[][] rows =
        [
            ["Cost centre", "Budget", "Actual", "Remaining", "Remaining %"],
            .. new[] { "CC-1100", "CC-2300", "CC-3100" }.Select((code, i) => Line(code, i + 2)),
            [],
            ["All cost centres", "=SUM(CostCentres[Budget])", "=SUM(CostCentres[Actual])", "=B6-C6", "=D6/B6"],
            ["Centres reporting", "=COUNTA(CostCentres[Code])"],
        ];
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
                sheet.Enter(new CellAddress(r, c), rows[r][c]);

        sheet.Do(SheetEdit.SetColumnWidth(CellRange.Parse("A:A"), 18));
        sheet.Do(SheetEdit.SetColumnWidth(CellRange.Parse("B:E"), 12));
        sheet.Do(SheetEdit.SetNumberFormat(CellRange.Parse("B2:D6"), NumberFormat.Parse("#,##0;[Red](#,##0)")));
        sheet.Do(SheetEdit.SetNumberFormat(CellRange.Parse("E2:E6"), NumberFormat.Parse("0.0%;[Red]-0.0%")));
        sheet.Do(SheetEdit.SetCellFormat([CellRange.Parse("A1:E1"), CellRange.Parse("A6:E6")], new CellFormatChange { Bold = true }));
        return sheet.ToDocument();
    }

    // One cost centre's line, read from the table by its code.
    private static string[] Line(string code, int row) =>
    [
        code,
        $"=XLOOKUP(A{row}, CostCentres[Code], CostCentres[Budget])",
        $"=XLOOKUP(A{row}, CostCentres[Code], CostCentres[Actual])",
        $"=B{row}-C{row}",
        $"=D{row}/B{row}",
    ];
}
