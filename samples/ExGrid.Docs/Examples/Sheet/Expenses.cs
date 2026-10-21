namespace ExGrid.Docs.Examples.Sheet;

using System.Globalization;
using ExSheet.Engine;

/// <summary>A quarter's travel and expenses claim, as a Sheet Document: what the Examples open on.</summary>
public static class Expenses
{
    /// <summary>The claim. Entries only: the Values are computed when the Sheet opens.</summary>
    public static SheetDocument Document()
    {
        var workbook = new Workbook(CultureInfo.GetCultureInfo("en-US"));
        var sheet = workbook.Sheets[0];
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
                    workbook.Do(sheet.Name, SheetEdit.Enter(new CellAddress(r, c), rows[r][c]));

        workbook.Do(sheet.Name, SheetEdit.SetColumnWidth(CellRange.Parse("A:A"), 18));
        workbook.Do(sheet.Name, SheetEdit.SetNumberFormat(CellRange.Parse("C2:D7"), NumberFormat.Parse("#,##0.00")));
        workbook.Do(sheet.Name, SheetEdit.SetCellFormat([CellRange.Parse("A1:D1"), CellRange.Parse("A7:D7")], new CellFormatChange { Bold = true }));
        return workbook.ToDocument(sheet.Name);
    }

    /// <summary>The claim and a summary that reads it, as a Workbook Document of two Sheets.</summary>
    public static WorkbookDocument Workbook()
    {
        var workbook = global::ExSheet.Engine.Workbook.Open(Document());
        workbook.Do(workbook.Sheets[0].Name, SheetEdit.Rename("Claim"));
        workbook.Do("Claim", SheetEdit.InsertSheet("Summary"));
        string[][] rows =
        [
            ["Claimed", "=Claim!D7"],
            ["Travel", "=Claim!D2+Claim!D5"],
            ["Approved limit", "6000"],
            ["Over the limit", "=MAX(0, B1-B3)"],
        ];
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
                workbook.Do("Summary", SheetEdit.Enter(new CellAddress(r, c), rows[r][c]));
        workbook.Do("Summary", SheetEdit.SetColumnWidth(CellRange.Parse("A:A"), 18));
        workbook.Do("Summary", SheetEdit.SetNumberFormat(CellRange.Parse("B1:B4"), NumberFormat.Parse("#,##0.00")));
        return workbook.ToDocument();
    }
}
