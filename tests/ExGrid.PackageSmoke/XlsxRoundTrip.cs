using System.Globalization;
using ExGrid.Data.Xlsx;

namespace PackageSmoke;

/// <summary>
/// A workbook written and read back through the packed ExGrid.Data.Xlsx, as a Consumer's server and browser take it
/// (ADR-0330 §1, §2): two sheets, the second shown, shared and inline text, a number, a boolean, an error, a shared
/// formula, a dynamic array, a style with a theme colour, and a column width. check.sh runs it through
/// <c>RoundTrip/</c> and fails when what is read back differs; the Blazor application beside it compiles it as a browser
/// takes the package.
/// </summary>
internal static class XlsxRoundTrip
{
    /// <summary>What differs between the workbook written and the one read back; empty when nothing does.</summary>
    public static async Task<IReadOnlyList<string>> RunAsync(CancellationToken token)
    {
        var styles = XlsxStyles.Default with
        {
            NumberFormats = new Dictionary<int, string> { [164] = "#,##0.00" },
            Fills = [XlsxFill.None, XlsxFill.Gray125, XlsxFill.Solid(XlsxColor.FromTheme(4, -0.249977111117893))],
            CellFormats = [new XlsxCellFormat(), new XlsxCellFormat(164, 0, 2, 0, new XlsxAlignment(XlsxHorizontalAlignment.Right))],
        };
        XlsxCell[] first =
        [
            new(0, 0, XlsxCellType.SharedString, null, "Desk"),
            new(0, 1, XlsxCellType.InlineString, null, "Notional "),
            new(1, 0, XlsxCellType.SharedString, null, "EMEA"),
            new(1, 1, XlsxCellType.Number, "1000000.25", StyleIndex: 1),
            new(2, 0, XlsxCellType.Boolean, "1"),
            new(2, 1, XlsxCellType.Number, "2000000.5", Formula: new XlsxFormula("B2*2", XlsxFormulaKind.Shared, "B3:B4", 0), StyleIndex: 1),
            new(3, 0, XlsxCellType.Error, "#N/A", Formula: new XlsxFormula("NA()")),
            new(3, 1, XlsxCellType.Number, "4000001", Formula: new XlsxFormula("", XlsxFormulaKind.Shared, SharedIndex: 0), StyleIndex: 1),
            new(4, 0, XlsxCellType.Number, "1", Formula: new XlsxFormula("_xlfn.SEQUENCE(2)", XlsxFormulaKind.Array, "A5:A6", IsDynamicArray: true)),
            new(5, 0, XlsxCellType.Number, "2"),
        ];
        XlsxCell[] second = [new(0, 0, XlsxCellType.SharedString, null, "EMEA")];

        using var stream = new MemoryStream();
        await using (var writer = XlsxWriter.Create(stream, new XlsxWriteOptions { Styles = styles, ActiveSheet = 1, FullCalculationOnLoad = true }))
        {
            await writer.AddSheet("Trades", [new XlsxColumn(0, 0, 18.5)]).WriteAsync(first, token);
            await writer.AddSheet("Desks").WriteAsync(second, token);
            await writer.CompleteAsync(token);
        }
        stream.Position = 0;

        var differences = new List<string>();
        using var reader = await XlsxReader.OpenAsync(stream, cancellationToken: token);
        if (!reader.Sheets.Select(s => s.Name).SequenceEqual(["Trades", "Desks"]) || reader.ActiveSheet != 1)
            differences.Add($"the sheets were read back as {string.Join(", ", reader.Sheets.Select(s => s.Name))}, {reader.ActiveSheet} shown");
        if (!reader.Styles.CellFormats.SequenceEqual(styles.CellFormats) || !reader.Styles.Fills.SequenceEqual(styles.Fills)
            || reader.Styles.NumberFormats[164] != "#,##0.00")
        {
            differences.Add("the styles were read back otherwise");
        }
        if (reader.Styles.Fills[2].Foreground is not { } fill || reader.Theme.Resolve(fill) is null)
            differences.Add("the fill's theme colour did not resolve");
        Compare(differences, "Trades", first, await reader.ReadSheetAsync(0));
        Compare(differences, "Desks", second, await reader.ReadSheetAsync(1));
        return differences;
    }

    private static void Compare(List<string> differences, string name, XlsxCell[] written, XlsxWorksheet read)
    {
        if (read.Cells.Count != written.Length)
        {
            differences.Add(string.Create(CultureInfo.InvariantCulture, $"{name}: {written.Length} cells were read back as {read.Cells.Count}"));
            return;
        }
        for (var i = 0; i < written.Length; i++)
        {
            // A shared string comes back with its index as its value; the rest exactly as written.
            var expected = written[i].Type == XlsxCellType.SharedString ? written[i] with { Value = read.Cells[i].Value } : written[i];
            if (read.Cells[i] != expected)
                differences.Add($"{name}!{written[i].Reference}: {expected} was read back as {read.Cells[i]}");
        }
    }
}
