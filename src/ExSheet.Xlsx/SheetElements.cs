using System.Globalization;
using ExGrid.Data.Xlsx;

namespace ExSheet.Xlsx;

/// <summary>
/// What a worksheet's element the file layer does not read holds that the Open Report says (ADR-0330 §4): what changes how
/// the Sheet looks or prints, or what else it held. Every file Excel writes carries elements in a form that holds nothing
/// of the kind — its margins at their defaults, its phonetic settings, the error marks a user dismissed, its dimension —
/// and those are not listed, so a plain file's report is empty and a listed entry means something was left behind.
/// </summary>
internal static class SheetElements
{
    /// <summary>Excel's default margins, in inches: left, right, top, bottom, header, footer.</summary>
    private static readonly (string Name, double Inches)[] DefaultMargins =
        [("left", 0.7), ("right", 0.7), ("top", 0.75), ("bottom", 0.75), ("header", 0.3), ("footer", 0.3)];

    /// <summary>What <paramref name="element"/> holds that is not carried, each kind with a detail; nothing when it holds none.</summary>
    public static IEnumerable<(NotCarriedKind Kind, string? Detail)> NotCarried(XlsxElement element)
    {
        switch (element.Name)
        {
            // Nothing a Sheet shows or prints: the used range, the sheet's default row (judged below), phonetic settings,
            // dismissed error marks, calculation flags.
            case "dimension" or "phoneticPr" or "ignoredErrors" or "sheetCalcPr":
                break;
            case "sheetFormatPr":
                if (True(element.Attribute("zeroHeight"))) yield return (NotCarriedKind.HiddenRows, null);
                if (True(element.Attribute("customHeight"))) yield return (NotCarriedKind.RowHeights, null);
                // Excel's own base width is 8 characters, which the default 8.43 is; any other default is the Sheet's own.
                if (element.Attribute("defaultColWidth") is not null || element.Attribute("baseColWidth") is { } b && b != "8")
                    yield return (NotCarriedKind.DefaultColumnWidth, null);
                break;
            case "sheetViews":
                if (element.Children.Any(ViewSetsAnything)) yield return (NotCarriedKind.View, null);
                break;
            case "sheetPr":
                if (element.Children.Any(c => c.Name == "tabColor")) yield return (NotCarriedKind.SheetProperties, null);
                if (element.Children.Any(c => c.Name == "pageSetUpPr" && True(c.Attribute("fitToPage")))) yield return (NotCarriedKind.PrintSettings, null);
                break;
            case "pageMargins":
                if (DefaultMargins.Any(m => element.Attribute(m.Name) is { } v && Number(v) is var inches && inches != m.Inches))
                    yield return (NotCarriedKind.PrintSettings, null);
                break;
            case "printOptions":
                if (new[] { "horizontalCentered", "verticalCentered", "headings", "gridLines" }.Any(a => True(element.Attribute(a))))
                    yield return (NotCarriedKind.PrintSettings, null);
                break;
            case "pageSetup":
                if (PageSetupSetsAnything(element)) yield return (NotCarriedKind.PrintSettings, null);
                break;
            case "headerFooter":
                if (element.Children.Any(c => !string.IsNullOrWhiteSpace(c.Text))) yield return (NotCarriedKind.PrintSettings, null);
                break;
            case "rowBreaks" or "colBreaks":
                if (element.Children.Any(c => c.Name == "brk") || element.ChildrenCut) yield return (NotCarriedKind.PrintSettings, null);
                break;
            case "extLst":
                // Each extension by what it holds: Excel 2010's conditional formatting, validation, sparklines, slicers.
                foreach (var held in element.Children.Where(e => e.Name == "ext").SelectMany(e => e.Children))
                {
                    foreach (var kind in NotCarried(held)) yield return kind;
                }
                break;
            case "mergeCells": yield return (NotCarriedKind.MergedCells, null); break;
            case "conditionalFormatting" or "conditionalFormattings": yield return (NotCarriedKind.ConditionalFormatting, null); break;
            case "dataValidations": yield return (NotCarriedKind.DataValidation, null); break;
            case "legacyDrawing": yield return (NotCarriedKind.NotesAndComments, null); break;
            case "hyperlinks": yield return (NotCarriedKind.Hyperlinks, null); break;
            case "drawing" or "picture" or "oleObjects" or "controls" or "legacyDrawingHF" or "sparklineGroups": yield return (NotCarriedKind.PicturesAndCharts, null); break;
            case "sheetProtection" or "protectedRanges": yield return (NotCarriedKind.Protection, null); break;
            case "autoFilter" or "sortState": yield return (NotCarriedKind.AutoFilter, null); break;
            case "tableParts": yield return (NotCarriedKind.ExcelTable, null); break;
            case "slicerList" or "timelineRefs": yield return (NotCarriedKind.Slicers, null); break;
            default: yield return (NotCarriedKind.Other, element.Name); break;
        }
    }

    /// <summary>
    /// Whether a <c>&lt;sheetView&gt;</c> changes how the Sheet looks beyond what is carried: gridlines, headings or zeros
    /// hidden, Formulas shown, right to left, a zoom, a page layout. The selection and the scroll position change nothing;
    /// frozen panes are said on their own (<see cref="NotCarriedKind.FrozenPanes"/>).
    /// </summary>
    private static bool ViewSetsAnything(XlsxElement view) =>
        view.Name == "sheetView"
        && (False(view.Attribute("showGridLines")) || False(view.Attribute("showRowColHeaders")) || False(view.Attribute("showZeros"))
            || True(view.Attribute("showFormulas")) || True(view.Attribute("rightToLeft"))
            || view.Attribute("zoomScale") is { } zoom && zoom != "100"
            || view.Attribute("view") is { } layout && layout != "normal");

    /// <summary>
    /// Whether a <c>&lt;pageSetup&gt;</c> sets anything beyond Excel's defaults (ECMA-376 Part 1, §18.3.1.63). Letter (1) and
    /// A4 (9), the paper Excel writes by the machine's locale, are not counted; any other paper, or a size of its own, is.
    /// The printer's resolution and its settings part are the printer's, and are not counted.
    /// </summary>
    private static bool PageSetupSetsAnything(XlsxElement element) => element.Attributes.Any(a => a.Key switch
    {
        "horizontalDpi" or "verticalDpi" or "id" or "usePrinterDefaults" => false,
        "paperSize" => a.Value is not ("1" or "9"),
        "orientation" => a.Value is not ("default" or "portrait"),
        "scale" => a.Value != "100",
        "fitToWidth" or "fitToHeight" or "firstPageNumber" or "copies" => a.Value != "1",
        "pageOrder" => a.Value != "downThenOver",
        "cellComments" => a.Value != "none",
        "errors" => a.Value != "displayed",
        "blackAndWhite" or "draft" or "useFirstPageNumber" => True(a.Value),
        _ => true,
    });

    private static bool True(string? value) => value is "1" or "true";

    private static bool False(string? value) => value is "0" or "false";

    /// <summary>A margin in inches; one that does not read as a number differs from every default.</summary>
    private static double Number(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : double.NaN;
}
