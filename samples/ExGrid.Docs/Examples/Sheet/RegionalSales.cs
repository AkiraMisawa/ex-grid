namespace ExGrid.Docs.Examples.Sheet;

using System.Globalization;
using ExSheet.Engine;

/// <summary>Quarterly sales by region, with a Formula of every supported kind and the answers it refuses to approximate.</summary>
public static class RegionalSales
{
    /// <summary>The sheet, as a Sheet Document.</summary>
    public static SheetDocument Document()
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        string[][] rows =
        [
            ["Region", "Q1", "Q2", "Q3", "Q4", "Total", "Average", "Best quarter", "Status"],
            ["North", "182400", "195300", "201750", "188900", "=SUM(B2:E2)", "=AVERAGE(B2:E2)", "=MAX(B2:E2)", "=IF(F2>=750000, \"On target\", \"Below\")"],
            ["South", "143800", "151200", "139650", "160400", "=SUM(B3:E3)", "=AVERAGE(B3:E3)", "=MAX(B3:E3)", "=IF(F3>=750000, \"On target\", \"Below\")"],
            ["West", "211300", "198750", "224600", "236100", "=SUM(B4:E4)", "=AVERAGE(B4:E4)", "=MAX(B4:E4)", "=IF(F4>=750000, \"On target\", \"Below\")"],
            ["Total", "=SUM(B2:B4)", "=SUM(C2:C4)", "=SUM(D2:D4)", "=SUM(E2:E4)", "=SUM(F2:F4)"],
            [],
            ["Look up a region", "South", "", "Refused, not approximated"],
            ["Its total", "=XLOOKUP(B7, A2:A4, F2:F4, \"Not found\")", "", "Unsupported", "=MEDIAN(B2:E2)"],
            ["Share of all sales", "=ROUND(B8/F5, 3)", "", "Circular", "=E9+1"],
            ["Regions reporting", "=COUNTA(A2:A4)", "", "Divide by zero", "=B2/0"],
            ["", "", "", "Caught", "=IFERROR(E10, 0)"],
        ];
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
                if (rows[r][c].Length > 0)
                    sheet.Enter(new CellAddress(r, c), rows[r][c]);

        sheet.Do(SheetEdit.SetColumnWidth(CellRange.Parse("A:A"), 22));
        sheet.Do(SheetEdit.SetColumnWidth(CellRange.Parse("B:I"), 11));
        sheet.Do(SheetEdit.SetNumberFormat(CellRange.Parse("B2:H5"), NumberFormat.Parse("#,##0")));
        sheet.Do(SheetEdit.SetNumberFormat(CellRange.Parse("B8"), NumberFormat.Parse("#,##0")));
        sheet.Do(SheetEdit.SetNumberFormat(CellRange.Parse("B9"), NumberFormat.Parse("0.0%")));
        sheet.Do(SheetEdit.SetAlignment(CellRange.Parse("B1:H1"), HorizontalAlignment.Right));
        sheet.Do(SheetEdit.SetCellFormat([CellRange.Parse("A1:I1"), CellRange.Parse("A5:F5"), CellRange.Parse("D7")], new CellFormatChange { Bold = true }));
        return sheet.ToDocument();
    }
}
