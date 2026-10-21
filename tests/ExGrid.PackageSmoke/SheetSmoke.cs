using System.Globalization;
using ExSheet.Engine;

namespace PackageSmoke;

/// <summary>
/// ExSheet.Engine's README example, compiled against the packed package as a Consumer takes it
/// (ADR-0042). It is not run: that the package restores and its API compiles is what this checks;
/// what the engine computes is the engine's own suite's.
/// </summary>
internal static class SheetSmoke
{
    public static double Compute()
    {
        var workbook = new Workbook(CultureInfo.GetCultureInfo("en-US"));
        var sheet = workbook.Sheets[0];
        workbook.Do("Sheet1", SheetEdit.Enter(CellAddress.Parse("A1"), "2"));
        workbook.Do("Sheet1", SheetEdit.Enter(CellAddress.Parse("B1"), "3"));
        workbook.Do("Sheet1", SheetEdit.Enter(CellAddress.Parse("C1"), "=A1+B1"));
        return sheet.GetValue(CellAddress.Parse("C1"))!.Value.Number;
    }

    public static void Operate(Workbook workbook)
    {
        var step = workbook.Do("Sheet1", SheetEdit.InsertRows(row: 2));
        step.Undo();

        workbook.DeclareLinkedTable("Positions", ["Id", "PV"]);
        workbook.Do("Sheet1", SheetEdit.Enter(CellAddress.Parse("A1"), "=XLOOKUP(\"R-4471\", Positions[Id], Positions[PV])"));
        workbook.PushLinkedTable("Positions", [[Value.FromText("R-4471"), Value.FromNumber(250.5)]]);
        workbook.SetSeed(7);
    }
}
