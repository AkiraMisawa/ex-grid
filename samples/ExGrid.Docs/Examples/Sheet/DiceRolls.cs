namespace ExGrid.Docs.Examples.Sheet;

using System.Globalization;
using ExSheet.Engine;

/// <summary>Ten rolls of two dice, and a uniform draw beside each, summarised: RAND and RANDBETWEEN over a Sheet Seed.</summary>
public static class DiceRolls
{
    /// <summary>The sheet, as a Sheet Document. It holds the Formulas only: no seed and no draw.</summary>
    public static SheetDocument Document()
    {
        var workbook = new Workbook(CultureInfo.GetCultureInfo("en-US"));
        var sheet = workbook.Sheets[0];
        workbook.Do(sheet.Name, SheetEdit.Enter(CellAddress.Parse("A1"), "Roll"));
        workbook.Do(sheet.Name, SheetEdit.Enter(CellAddress.Parse("B1"), "Die 1"));
        workbook.Do(sheet.Name, SheetEdit.Enter(CellAddress.Parse("C1"), "Die 2"));
        workbook.Do(sheet.Name, SheetEdit.Enter(CellAddress.Parse("D1"), "Sum"));
        workbook.Do(sheet.Name, SheetEdit.Enter(CellAddress.Parse("E1"), "Uniform"));
        for (var roll = 1; roll <= 10; roll++)
        {
            var row = roll + 1;
            workbook.Do(sheet.Name, SheetEdit.Enter(CellAddress.Parse($"A{row}"), roll.ToString(CultureInfo.InvariantCulture)));
            workbook.Do(sheet.Name, SheetEdit.Enter(CellAddress.Parse($"B{row}"), "=RANDBETWEEN(1,6)"));
            workbook.Do(sheet.Name, SheetEdit.Enter(CellAddress.Parse($"C{row}"), "=RANDBETWEEN(1,6)"));
            workbook.Do(sheet.Name, SheetEdit.Enter(CellAddress.Parse($"D{row}"), $"=B{row}+C{row}"));
            workbook.Do(sheet.Name, SheetEdit.Enter(CellAddress.Parse($"E{row}"), "=RAND()"));
        }
        workbook.Do(sheet.Name, SheetEdit.Enter(CellAddress.Parse("G1"), "Average sum"));
        workbook.Do(sheet.Name, SheetEdit.Enter(CellAddress.Parse("H1"), "=AVERAGE(D2:D11)"));
        workbook.Do(sheet.Name, SheetEdit.Enter(CellAddress.Parse("G2"), "Sevens"));
        workbook.Do(sheet.Name, SheetEdit.Enter(CellAddress.Parse("H2"), "=COUNTIF(D2:D11,7)"));
        workbook.Do(sheet.Name, SheetEdit.Enter(CellAddress.Parse("G3"), "Average uniform"));
        workbook.Do(sheet.Name, SheetEdit.Enter(CellAddress.Parse("H3"), "=AVERAGE(E2:E11)"));

        workbook.Do(sheet.Name, SheetEdit.SetColumnWidth(CellRange.Parse("G:G"), 16));
        workbook.Do(sheet.Name, SheetEdit.SetNumberFormat(CellRange.Parse("E2:E11"), NumberFormat.Parse("0.000")));
        workbook.Do(sheet.Name, SheetEdit.SetNumberFormat(CellRange.Parse("H1"), NumberFormat.Parse("0.0")));
        workbook.Do(sheet.Name, SheetEdit.SetNumberFormat(CellRange.Parse("H3"), NumberFormat.Parse("0.000")));
        workbook.Do(sheet.Name, SheetEdit.SetCellFormat([CellRange.Parse("A1:E1"), CellRange.Parse("G1:G3")], new CellFormatChange { Bold = true }));
        return workbook.ToDocument(sheet.Name);
    }
}
