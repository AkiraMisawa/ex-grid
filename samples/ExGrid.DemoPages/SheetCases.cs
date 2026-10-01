using System.Globalization;
using global::ExSheet.Engine;

namespace ExGrid.DemoPages;

/// <summary>
/// The Sheets <c>/sheet?case=</c> opens in place of the page's own: the set-ups of the eleventh
/// Windows run's Part A (<c>docs/specs/exsheet/verify-on-windows-11.md</c>), so that its Part C can
/// put the DemoHost beside Excel's screenshots of the same cells, and the browser suite reads them
/// too. A case's keys are not pressed here: Part C presses them, as Part A did. <c>paper</c> is not a
/// run's case: it gathers recorded colours and emphases on the Paper, the ones a row or a column
/// records among them.
/// </summary>
public static class SheetCases
{
    private static readonly CellColour Red = CellColour.FromRgb(0xFF0000);
    private static readonly CellColour Blue = CellColour.FromRgb(0x0000FF);
    private static readonly CellFill Yellow = CellFill.Solid(CellColour.FromRgb(0xFFFF00));
    private static readonly CellFill LightBlue = CellFill.Solid(CellColour.FromRgb(0x00B0F0));

    /// <summary>The names <see cref="Document"/> knows, in the order the run numbers them.</summary>
    public static IReadOnlyList<string> Names { get; } = ["1", "2", "3b", "3c", "4", "5", "6", "16", "17", "18", "paper"];

    /// <summary>
    /// Case <paramref name="name"/>'s Sheet, in <paramref name="culture"/>; null when no case is
    /// asked for.
    /// </summary>
    /// <exception cref="ArgumentException">The name is not one of <see cref="Names"/>: refused by
    /// name rather than opened as the page's own Sheet, which a comparison would then be made
    /// against.</exception>
    public static SheetDocument? Document(string? name, CultureInfo culture)
    {
        if (name is null) return null;
        var sheet = new Sheet(culture);
        switch (name)
        {
            case "1":
                // Rows 1–8: -5 and 5 in each of the eight named colours; [White]'s row filled black.
                string[] names = ["Black", "Blue", "Cyan", "Green", "Magenta", "Red", "White", "Yellow"];
                for (var row = 0; row < names.Length; row++)
                {
                    Enter(sheet, $"A{row + 1}", "-5");
                    Enter(sheet, $"B{row + 1}", "5");
                    Format(sheet, new CellFormatChange { NumberFormat = NumberFormat.Parse($"[{names[row]}]0") }, $"A{row + 1}:B{row + 1}");
                }
                Format(sheet, new CellFormatChange { Fill = CellFill.Solid(CellColour.FromRgb(0x000000)) }, "A7:B7");
                break;
            case "2":
                // The Number Format's colour wins over the Font's.
                Enter(sheet, "A1", "-5");
                Enter(sheet, "B1", "5");
                Format(sheet, new CellFormatChange { NumberFormat = NumberFormat.Parse("0;[Red]-0"), FontColour = Blue }, "A1:B1");
                break;
            case "3b":
                // No section shows them, so no colour. The case's A4, 5 in 0;[Red]@, is left out:
                // the engine refuses that code, which Excel takes.
                Enter(sheet, "A1", "abc");
                Enter(sheet, "A2", "TRUE");
                Enter(sheet, "A3", "=1/0");
                Format(sheet, new CellFormatChange { NumberFormat = NumberFormat.Parse("[Red]0") }, "A1:A3");
                break;
            case "3c":
                // A #### keeps its section's colour: column A at the width Excel showed it at.
                Enter(sheet, "A1", "-123456789");
                Format(sheet, new CellFormatChange { NumberFormat = NumberFormat.Parse("0;[Red]-0") }, "A1");
                sheet.Do(SheetEdit.SetColumnWidth(CellRange.WholeColumns(0, 0), 9.5));
                break;
            case "4":
                Format(sheet, new CellFormatChange { Fill = Yellow }, "B2");
                break;
            case "5":
                Format(sheet, new CellFormatChange { Fill = CellFill.Solid(CellColour.FromRgb(0xFFFFFF)) }, "B2");
                break;
            case "6":
                Format(sheet, new CellFormatChange { Fill = Yellow }, "B2:C2");
                break;
            case "16":
                Enter(sheet, "A1", "abc");
                Enter(sheet, "A2", "def");
                break;
            case "17":
                Enter(sheet, "A1", "abc");
                Enter(sheet, "A2", "def");
                Format(sheet, new CellFormatChange { Bold = true }, "A1");
                break;
            case "18":
                Enter(sheet, "A1", "1234.5");
                break;
            case "paper":
                Enter(sheet, "A1", "red");
                Enter(sheet, "B1", "-5");
                Enter(sheet, "C1", "bold");
                Enter(sheet, "D1", "italic");
                Enter(sheet, "A2", "underline");
                Enter(sheet, "B2", "strike");
                Enter(sheet, "C2", "automatic");
                Format(sheet, new CellFormatChange { FontColour = Red }, "A1");
                Format(sheet, new CellFormatChange { NumberFormat = NumberFormat.Parse("0;[Red]-0"), FontColour = Blue }, "B1");
                Format(sheet, new CellFormatChange { Bold = true }, "C1");
                Format(sheet, new CellFormatChange { Italic = true }, "D1");
                Format(sheet, new CellFormatChange { Underline = true }, "A2");
                Format(sheet, new CellFormatChange { Strikethrough = true }, "B2");
                Format(sheet, new CellFormatChange { Fill = Yellow }, "C3");
                // A row and a column record a Fill, so cells that hold nothing are painted.
                Format(sheet, new CellFormatChange { Fill = LightBlue }, "5:5");
                Format(sheet, new CellFormatChange { Fill = Yellow }, "F:F");
                break;
            default:
                throw new ArgumentException(
                    $"Unknown case '{name}': /sheet?case= opens one of {string.Join(", ", Names)}.", nameof(name));
        }
        return sheet.ToDocument();
    }

    private static void Enter(Sheet sheet, string address, string typed) => sheet.Enter(CellAddress.Parse(address), typed);

    private static void Format(Sheet sheet, CellFormatChange change, string range) =>
        sheet.Do(SheetEdit.SetCellFormat([CellRange.Parse(range)], change));
}
