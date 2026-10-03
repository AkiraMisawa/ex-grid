namespace ExGrid.Docs.Examples.Sheet;

using System.Globalization;
using ExSheet.Engine;

/// <summary>
/// A department's year of actuals against its budget, as a finance team lays one out: months
/// across, lines down, subtotals, a variance and number formats. Invented from a fixed seed.
/// </summary>
public static class OperationsBudget
{
    private static readonly string[] Months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    // Columns: A the line; B the year's actual, C its budget, D the variance, E in per cent; F..Q the months.
    private const string Actual = "B", Budget = "C", Variance = "D", Percent = "E", FirstMonth = "F", LastMonth = "Q";

    private static readonly CellColour Navy = CellColour.FromRgb(0x1F3864);
    private static readonly CellColour White = CellColour.FromRgb(0xFFFFFF);
    private static readonly CellColour Section = CellColour.FromRgb(0xD9E1F2);
    private static readonly CellColour Total = CellColour.FromRgb(0xF2F2F2);
    private static readonly CellColour Result = CellColour.FromRgb(0xE2EFDA);
    private static readonly CellColour Muted = CellColour.FromRgb(0x595959);

    private enum Kind { Header, Section, Line, Subtotal, Result, Ratio, Blank }

    private sealed record Row(Kind Kind, string Label, double Monthly = 0, double Budget = 0, string? Formula = null);

    /// <summary>The budget sheet, as a Sheet Document.</summary>
    public static SheetDocument Document()
    {
        // Each Formula is written for column B; {c} stands for the column it is copied to.
        Row[] rows =
        [
            new(Kind.Header, "USD thousands"),
            new(Kind.Section, "Revenue"),
            new(Kind.Line, "Product sales", 412, 5_050),
            new(Kind.Line, "Services", 186, 2_180),
            new(Kind.Line, "Licensing", 64, 720),
            new(Kind.Subtotal, "Total revenue", Formula: "=SUM({c}3:{c}5)"),
            new(Kind.Blank, ""),
            new(Kind.Section, "Cost of sales"),
            new(Kind.Line, "Materials", 151, 1_860),
            new(Kind.Line, "Freight and logistics", 38, 430),
            new(Kind.Subtotal, "Total cost of sales", Formula: "=SUM({c}9:{c}10)"),
            new(Kind.Result, "Gross profit", Formula: "={c}6-{c}11"),
            new(Kind.Ratio, "Gross margin", Formula: "=IFERROR({c}12/{c}6, 0)"),
            new(Kind.Blank, ""),
            new(Kind.Section, "Operating expenses"),
            new(Kind.Line, "Salaries", 168, 2_040),
            new(Kind.Line, "Benefits and payroll tax", 41, 500),
            new(Kind.Line, "Rent and facilities", 32, 384),
            new(Kind.Line, "Software and IT", 24, 270),
            new(Kind.Line, "Travel", 11, 150),
            new(Kind.Line, "Marketing", 29, 380),
            new(Kind.Line, "Professional fees", 14, 150),
            new(Kind.Subtotal, "Total operating expenses", Formula: "=SUM({c}16:{c}22)"),
            new(Kind.Blank, ""),
            new(Kind.Result, "Operating profit", Formula: "={c}12-{c}23"),
            new(Kind.Ratio, "Operating margin", Formula: "=IFERROR({c}25/{c}6, 0)"),
        ];

        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"), "FY2025");
        var random = new Random(2025);
        var typed = new List<KeyValuePair<CellAddress, string>>();
        void Enter(string column, int row, string text) =>
            typed.Add(new(CellAddress.Parse($"{column}{row}"), text));

        for (var i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            var n = i + 1;
            Enter("A", n, row.Label);
            switch (row.Kind)
            {
                case Kind.Header:
                    for (var m = 0; m < 12; m++) Enter(Column(m), n, Months[m]);
                    Enter(Actual, n, "FY actual");
                    Enter(Budget, n, "FY budget");
                    Enter(Variance, n, "Variance");
                    Enter(Percent, n, "Var %");
                    break;
                case Kind.Line:
                    // A month wanders around the line's run rate, with a stronger fourth quarter.
                    for (var m = 0; m < 12; m++)
                    {
                        var season = m >= 9 ? 1.08 : m is 6 or 7 ? 0.95 : 1.0;
                        var value = Math.Round(row.Monthly * season * (0.9 + random.NextDouble() * 0.2));
                        Enter(Column(m), n, value.ToString(CultureInfo.InvariantCulture));
                    }
                    Enter(Actual, n, $"=SUM({FirstMonth}{n}:{LastMonth}{n})");
                    Enter(Budget, n, row.Budget.ToString(CultureInfo.InvariantCulture));
                    break;
                case Kind.Subtotal or Kind.Result or Kind.Ratio:
                    foreach (var column in MonthsAndYear())
                        Enter(column, n, row.Formula!.Replace("{c}", column));
                    break;
            }
            if (row.Kind is Kind.Line or Kind.Subtotal or Kind.Result)
            {
                Enter(Variance, n, $"={Actual}{n}-{Budget}{n}");
                Enter(Percent, n, $"=IFERROR({Variance}{n}/{Budget}{n}, 0)");
            }
        }
        sheet.Enter(typed);

        void Format(string range, CellFormatChange change) =>
            sheet.Do(SheetEdit.SetCellFormat([CellRange.Parse(range)], change));

        sheet.Do(SheetEdit.SetColumnWidth(CellRange.Parse("A:A"), 24));
        sheet.Do(SheetEdit.SetColumnWidth(CellRange.Parse($"{FirstMonth}:{LastMonth}"), 6.5));
        sheet.Do(SheetEdit.SetColumnWidth(CellRange.Parse($"{Actual}:{Variance}"), 9));
        sheet.Do(SheetEdit.SetColumnWidth(CellRange.Parse($"{Percent}:{Percent}"), 7.5));

        var last = rows.Length;
        Format($"B2:{LastMonth}{last}", new CellFormatChange { NumberFormat = NumberFormat.Parse("#,##0;(#,##0)") });
        Format($"{Variance}2:{Variance}{last}", new CellFormatChange { NumberFormat = NumberFormat.Parse("#,##0;[Red](#,##0)") });
        Format($"{Percent}2:{Percent}{last}", new CellFormatChange { NumberFormat = NumberFormat.Parse("0.0%;[Red]-0.0%") });
        Format($"{Actual}1:{Actual}{last}", new CellFormatChange { Bold = true });

        Format($"A1:{LastMonth}1", new CellFormatChange { Bold = true, FontColour = White, Fill = CellFill.Solid(Navy) });
        Format($"B1:{LastMonth}1", new CellFormatChange { Alignment = HorizontalAlignment.Right });
        Format($"{Percent}1:{Percent}{last}", new CellFormatChange { Borders = new BorderChange { Right = new BorderLine(BorderLineStyle.Medium) } });
        for (var i = 0; i < rows.Length; i++)
        {
            var n = i + 1;
            var line = $"A{n}:{LastMonth}{n}";
            switch (rows[i].Kind)
            {
                case Kind.Section:
                    Format(line, new CellFormatChange { Bold = true, Fill = CellFill.Solid(Section) });
                    break;
                case Kind.Subtotal:
                    Format(line, new CellFormatChange { Bold = true, Fill = CellFill.Solid(Total), Borders = new BorderChange { Top = new BorderLine(BorderLineStyle.Thin) } });
                    break;
                case Kind.Result:
                    Format(line, new CellFormatChange
                    {
                        Bold = true,
                        Fill = CellFill.Solid(Result),
                        Borders = new BorderChange { Top = new BorderLine(BorderLineStyle.Thin), Bottom = new BorderLine(BorderLineStyle.Double) },
                    });
                    break;
                case Kind.Ratio:
                    Format(line, new CellFormatChange { Italic = true, FontColour = Muted, NumberFormat = NumberFormat.Parse("0.0%") });
                    break;
            }
        }
        return sheet.ToDocument();
    }

    private static string Column(int month) => CellAddress.ColumnName(5 + month);

    private static IEnumerable<string> MonthsAndYear()
    {
        for (var m = 0; m < 12; m++) yield return Column(m);
        yield return Actual;
        yield return Budget;
    }
}
