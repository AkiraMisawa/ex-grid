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
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        sheet.Enter(CellAddress.Parse("A1"), "2");
        sheet.Enter(CellAddress.Parse("B1"), "3");
        sheet.Enter(CellAddress.Parse("C1"), "=A1+B1");
        return sheet.GetValue(CellAddress.Parse("C1"))!.Value.Number;
    }

    public static void Operate(Sheet sheet)
    {
        var step = sheet.Do(SheetEdit.InsertRows(row: 2));
        step.Undo();

        sheet.DeclareLinkedTable("Positions", ["Id", "PV"]);
        sheet.Enter(CellAddress.Parse("A1"), "=XLOOKUP(\"R-4471\", Positions[Id], Positions[PV])");
        sheet.PushLinkedTable("Positions", [[Value.FromText("R-4471"), Value.FromNumber(250.5)]]);
    }
}
