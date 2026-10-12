namespace ExGrid.Docs.Examples.Sheet;

using System.Globalization;
using ExSheet.Engine;

/// <summary>Ten rolls of two dice, and a uniform draw beside each, summarised: RAND and RANDBETWEEN over a Sheet Seed.</summary>
public static class DiceRolls
{
    /// <summary>The sheet, as a Sheet Document. It holds the Formulas only: no seed and no draw.</summary>
    public static SheetDocument Document()
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        sheet.Enter(CellAddress.Parse("A1"), "Roll");
        sheet.Enter(CellAddress.Parse("B1"), "Die 1");
        sheet.Enter(CellAddress.Parse("C1"), "Die 2");
        sheet.Enter(CellAddress.Parse("D1"), "Sum");
        sheet.Enter(CellAddress.Parse("E1"), "Uniform");
        for (var roll = 1; roll <= 10; roll++)
        {
            var row = roll + 1;
            sheet.Enter(CellAddress.Parse($"A{row}"), roll.ToString(CultureInfo.InvariantCulture));
            sheet.Enter(CellAddress.Parse($"B{row}"), "=RANDBETWEEN(1,6)");
            sheet.Enter(CellAddress.Parse($"C{row}"), "=RANDBETWEEN(1,6)");
            sheet.Enter(CellAddress.Parse($"D{row}"), $"=B{row}+C{row}");
            sheet.Enter(CellAddress.Parse($"E{row}"), "=RAND()");
        }
        sheet.Enter(CellAddress.Parse("G1"), "Average sum");
        sheet.Enter(CellAddress.Parse("H1"), "=AVERAGE(D2:D11)");
        sheet.Enter(CellAddress.Parse("G2"), "Sevens");
        sheet.Enter(CellAddress.Parse("H2"), "=COUNTIF(D2:D11,7)");
        sheet.Enter(CellAddress.Parse("G3"), "Average uniform");
        sheet.Enter(CellAddress.Parse("H3"), "=AVERAGE(E2:E11)");

        sheet.Do(SheetEdit.SetColumnWidth(CellRange.Parse("G:G"), 16));
        sheet.Do(SheetEdit.SetNumberFormat(CellRange.Parse("E2:E11"), NumberFormat.Parse("0.000")));
        sheet.Do(SheetEdit.SetNumberFormat(CellRange.Parse("H1"), NumberFormat.Parse("0.0")));
        sheet.Do(SheetEdit.SetNumberFormat(CellRange.Parse("H3"), NumberFormat.Parse("0.000")));
        sheet.Do(SheetEdit.SetCellFormat([CellRange.Parse("A1:E1"), CellRange.Parse("G1:G3")], new CellFormatChange { Bold = true }));
        return sheet.ToDocument();
    }
}
