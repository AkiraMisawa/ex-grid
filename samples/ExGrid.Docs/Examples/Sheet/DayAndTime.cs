namespace ExGrid.Docs.Examples.Sheet;

using System.Globalization;
using ExSheet.Engine;

/// <summary>The day, the time and the quarter's end: TODAY and NOW, as the Sheet Day and the zone give them.</summary>
public static class DayAndTime
{
    /// <summary>The sheet, as a Sheet Document. It holds the Formulas only: no day and no time.</summary>
    public static SheetDocument Document()
    {
        var workbook = new Workbook(CultureInfo.GetCultureInfo("en-US"));
        var sheet = workbook.Sheets[0];
        string[][] rows =
        [
            ["Today", "=TODAY()"],
            ["Now", "=NOW()"],
            ["Quarter end", "=EOMONTH(TODAY(), MOD(3 - MONTH(TODAY()), 3))"],
            ["Days left in the quarter", "=B3-B1"],
        ];
        for (var r = 0; r < rows.Length; r++)
        {
            for (var c = 0; c < rows[r].Length; c++) workbook.Do(sheet.Name, SheetEdit.Enter(new CellAddress(r, c), rows[r][c]));
        }
        workbook.Do(sheet.Name, SheetEdit.SetColumnWidth(CellRange.Parse("A:A"), 24));
        workbook.Do(sheet.Name, SheetEdit.SetColumnWidth(CellRange.Parse("B:B"), 18));
        workbook.Do(sheet.Name, SheetEdit.SetNumberFormat(CellRange.Parse("B1"), NumberFormat.Parse("yyyy-mm-dd")));
        workbook.Do(sheet.Name, SheetEdit.SetNumberFormat(CellRange.Parse("B2"), NumberFormat.Parse("yyyy-mm-dd hh:mm")));
        workbook.Do(sheet.Name, SheetEdit.SetNumberFormat(CellRange.Parse("B3"), NumberFormat.Parse("yyyy-mm-dd")));
        workbook.Do(sheet.Name, SheetEdit.SetNumberFormat(CellRange.Parse("B4"), NumberFormat.Parse("0")));
        return workbook.ToDocument(sheet.Name);
    }
}
