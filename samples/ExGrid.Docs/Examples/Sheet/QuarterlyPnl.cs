namespace ExGrid.Docs.Examples.Sheet;

using System.Globalization;
using ExSheet.Engine;

/// <summary>A quarterly profit and loss statement, formatted as a finance team formats one.</summary>
public static class QuarterlyPnl
{
    private static readonly CellColour Navy = CellColour.FromRgb(0x1F3864);
    private static readonly CellColour White = CellColour.FromRgb(0xFFFFFF);
    private static readonly CellColour Band = CellColour.FromRgb(0xDDEBF7);

    /// <summary>The statement, as a Sheet Document.</summary>
    public static SheetDocument Document()
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        string[][] rows =
        [
            ["USD thousands", "Q1", "Q2", "Q3", "Q4", "FY"],
            ["Revenue", "4820", "5135", "4970", "5610", "=SUM(B2:E2)"],
            ["Cost of sales", "-2410", "-2520", "-2505", "-2690", "=SUM(B3:E3)"],
            ["Gross profit", "=B2+B3", "=C2+C3", "=D2+D3", "=E2+E3", "=F2+F3"],
            ["Gross margin", "=B4/B2", "=C4/C2", "=D4/D2", "=E4/E2", "=F4/F2"],
            ["Operating expenses", "-1630", "-1710", "-1695", "-1820", "=SUM(B6:E6)"],
            ["Operating profit", "=B4+B6", "=C4+C6", "=D4+D6", "=E4+E6", "=F4+F6"],
            ["Operating margin", "=B7/B2", "=C7/C2", "=D7/D2", "=E7/E2", "=F7/F2"],
        ];
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
                sheet.Enter(new CellAddress(r, c), rows[r][c]);

        void Format(string range, CellFormatChange change) =>
            sheet.Do(SheetEdit.SetCellFormat([CellRange.Parse(range)], change));

        sheet.Do(SheetEdit.SetColumnWidth(CellRange.Parse("A:A"), 22));
        sheet.Do(SheetEdit.SetColumnWidth(CellRange.Parse("B:F"), 10));

        // Thousands with negatives in brackets, and margins as percentages.
        Format("B2:F7", new CellFormatChange { NumberFormat = NumberFormat.Parse("#,##0;(#,##0)") });
        Format("B5:F5", new CellFormatChange { NumberFormat = NumberFormat.Parse("0.0%"), Italic = true });
        Format("B8:F8", new CellFormatChange { NumberFormat = NumberFormat.Parse("0.0%"), Italic = true });
        Format("A5", new CellFormatChange { Italic = true });
        Format("A8", new CellFormatChange { Italic = true });

        // A navy heading row, the totals in bold over a thin line, and the result underlined twice.
        Format("A1:F1", new CellFormatChange { Bold = true, FontColour = White, Fill = CellFill.Solid(Navy) });
        Format("B1:F1", new CellFormatChange { Alignment = HorizontalAlignment.Center });
        Format("A4:F4", new CellFormatChange { Bold = true, Borders = new BorderChange { Top = new BorderLine(BorderLineStyle.Thin) } });
        Format("A7:F7", new CellFormatChange
        {
            Bold = true,
            Fill = CellFill.Solid(Band),
            Borders = new BorderChange { Top = new BorderLine(BorderLineStyle.Thin), Bottom = new BorderLine(BorderLineStyle.Double) },
        });
        Format("F1:F8", new CellFormatChange { Bold = true });
        return sheet.ToDocument();
    }
}
