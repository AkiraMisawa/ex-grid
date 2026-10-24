namespace ExGrid.Docs.Examples.Sheet;

using System.Globalization;
using ExSheet.Engine;

/// <summary>A quarter of three months and a summary that adds them with 3D References, as a Workbook Document.</summary>
public static class Quarters
{
    /// <summary>Jan, Feb and Mar with the same layout, Year reading across them, and Notes outside the span.</summary>
    public static WorkbookDocument Workbook()
    {
        var workbook = new Workbook(CultureInfo.GetCultureInfo("en-US"), ["Jan", "Feb", "Mar", "Notes", "Year"]);
        (string Month, string Revenue, string Costs)[] months = [("Jan", "48200", "31950"), ("Feb", "51730", "33410"), ("Mar", "56980", "34120")];
        foreach (var (month, revenue, costs) in months)
        {
            Enter(workbook, month, "A1", "Revenue");
            Enter(workbook, month, "B1", revenue);
            Enter(workbook, month, "A2", "Costs");
            Enter(workbook, month, "B2", costs);
            workbook.Do(month, SheetEdit.SetColumnWidth(CellRange.Parse("A:A"), 14));
            workbook.Do(month, SheetEdit.SetNumberFormat(CellRange.Parse("B1:B2"), NumberFormat.Parse("#,##0")));
        }
        Enter(workbook, "Notes", "A1", "Outside the span: drag it between Jan and Mar and Year reads it too.");

        // Each reads B1 or B2 of every Sheet from Jan to Mar, in tab order.
        string[][] rows =
        [
            ["Revenue", "=SUM(Jan:Mar!B1)"],
            ["Costs", "=SUM(Jan:Mar!B2)"],
            ["Margin", "=B1-B2"],
            ["Best month", "=MAX(Jan:Mar!B1)"],
            ["Average costs", "=AVERAGE(Jan:Mar!B2)"],
        ];
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
                workbook.Do("Year", SheetEdit.Enter(new CellAddress(r, c), rows[r][c]));
        workbook.Do("Year", SheetEdit.SetColumnWidth(CellRange.Parse("A:A"), 14));
        workbook.Do("Year", SheetEdit.SetNumberFormat(CellRange.Parse("B1:B5"), NumberFormat.Parse("#,##0")));
        workbook.Shown = workbook.Find("Year")!;
        return workbook.ToDocument();
    }

    private static void Enter(Workbook workbook, string sheet, string address, string typed) =>
        workbook.Do(sheet, SheetEdit.Enter(CellAddress.Parse(address), typed));
}
