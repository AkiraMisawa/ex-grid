namespace ExGrid.Docs.Examples.Sheet;

using System.Globalization;
using ExSheet.Engine;

/// <summary>A quarter's travel and expenses claim, as a Sheet Document: what the Examples open on.</summary>
public static class Expenses
{
    /// <summary>The claim. Entries only: the Values are computed when the Sheet opens.</summary>
    public static SheetDocument Document()
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        string[][] rows =
        [
            ["Item", "Qty", "Unit cost", "Amount"],
            ["Flights LHR-JFK", "2", "1240", "=B2*C2"],
            ["Hotel, nights", "6", "289.5", "=B3*C3"],
            ["Client dinner", "1", "412.8", "=B4*C4"],
            ["Taxis", "9", "38.25", "=B5*C5"],
            ["Conference pass", "1", "1650", "=B6*C6"],
            ["Total", "", "", "=SUM(D2:D6)"],
        ];
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
                if (rows[r][c].Length > 0)
                    sheet.Enter(new CellAddress(r, c), rows[r][c]);

        sheet.Do(SheetEdit.SetColumnWidth(CellRange.Parse("A:A"), 18));
        sheet.Do(SheetEdit.SetNumberFormat(CellRange.Parse("C2:D7"), NumberFormat.Parse("#,##0.00")));
        sheet.Do(SheetEdit.SetCellFormat([CellRange.Parse("A1:D1"), CellRange.Parse("A7:D7")], new CellFormatChange { Bold = true }));
        return sheet.ToDocument();
    }
}
