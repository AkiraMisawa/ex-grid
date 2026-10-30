using System.Globalization;
using System.IO.Compression;
using System.Text;
using ExGrid.Data.Xlsx;
using ExSheet.Engine;
using Xunit;

namespace ExSheet.Xlsx.Tests;

/// <summary>Workbooks written in the test with ExGrid.Data.Xlsx's writer, edited part by part where the writer writes no such thing, and read.</summary>
internal static class Book
{
    public static CancellationToken Token => TestContext.Current.CancellationToken;

    public static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

    /// <summary>One worksheet to write.</summary>
    public sealed record Page(string Name, XlsxCell[] Cells, XlsxColumn[]? Columns = null, XlsxSheetVisibility Visibility = XlsxSheetVisibility.Visible, XlsxRow[]? Rows = null);

    public static XlsxCell Number(string at, string value, int style = 0) => Cell(at, XlsxCellType.Number, value) with { StyleIndex = style };

    public static XlsxCell Text(string at, string text) => Cell(at, XlsxCellType.SharedString, null) with { Text = text };

    public static XlsxCell Inline(string at, string text) => Cell(at, XlsxCellType.InlineString, null) with { Text = text };

    public static XlsxCell Boolean(string at, bool value) => Cell(at, XlsxCellType.Boolean, value ? "1" : "0");

    public static XlsxCell Error(string at, string text) => Cell(at, XlsxCellType.Error, text);

    /// <summary>A cell with a Formula as the file stores it, and a cached Value that reading must never take.</summary>
    public static XlsxCell Formula(string at, string text, string cached = "999") =>
        Cell(at, XlsxCellType.Number, cached) with { Formula = new XlsxFormula(text) };

    public static XlsxCell Cell(string at, XlsxCellType type, string? value)
    {
        Assert.True(XlsxCellReference.TryParse(at, out var row, out var column));
        return new XlsxCell(row, column, type, value);
    }

    /// <summary>A workbook of <paramref name="pages"/>, written by the file layer's writer, each page's cells in the sheet's order.</summary>
    public static async Task<byte[]> WriteAsync(XlsxWriteOptions? options, params Page[] pages)
    {
        using var stream = new MemoryStream();
        await using (var writer = XlsxWriter.Create(stream, options))
        {
            foreach (var page in pages)
            {
                var sheet = writer.AddSheet(page.Name, page.Columns, page.Visibility);
                var rows = (page.Rows ?? []).ToDictionary(r => r.Row);
                var cells = page.Cells.ToLookup(c => c.Row);
                // Each formatted row is begun before its cells, rows and cells in the sheet's order.
                foreach (var row in cells.Select(g => g.Key).Union(rows.Keys).Order())
                {
                    if (rows.TryGetValue(row, out var formatted)) sheet.WriteRow(formatted);
                    foreach (var cell in cells[row].OrderBy(c => c.Column)) sheet.Write(cell);
                }
            }
            await writer.CompleteAsync(Token);
        }
        return stream.ToArray();
    }

    public static Task<byte[]> WriteAsync(params Page[] pages) => WriteAsync(null, pages);

    /// <summary>The file with one part's text passed through <paramref name="edit"/>, which must change it.</summary>
    public static byte[] Edit(byte[] file, string part, Func<string, string> edit)
    {
        var parts = Parts(file);
        var before = parts[part];
        var after = edit(before);
        Assert.NotEqual(before, after);
        parts[part] = after;
        return Zip(parts);
    }

    /// <summary>The file with a part added, as another program would leave one.</summary>
    public static byte[] Add(byte[] file, string part, string content)
    {
        var parts = Parts(file);
        parts.Add(part, content);
        return Zip(parts);
    }

    /// <summary>The file with a theme part of <paramref name="schemeColors"/> (dark 1, light 1, dark 2, light 2, six accents, two hyperlinks, as <c>RRGGBB</c>), related from the workbook as Excel relates its own.</summary>
    public static byte[] WithTheme(byte[] file, params string[] schemeColors)
    {
        Assert.Equal(12, schemeColors.Length);
        string[] names = ["dk1", "lt1", "dk2", "lt2", "accent1", "accent2", "accent3", "accent4", "accent5", "accent6", "hlink", "folHlink"];
        var scheme = string.Concat(names.Zip(schemeColors, (n, c) => $"""<a:{n}><a:srgbClr val="{c}"/></a:{n}>"""));
        var theme = $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <a:theme xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" name="Custom"><a:themeElements><a:clrScheme name="Custom">{scheme}</a:clrScheme></a:themeElements></a:theme>
            """;
        var related = Edit(file, "xl/_rels/workbook.xml.rels", rels => rels.Replace("</Relationships>",
            """<Relationship Id="rIdTheme" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/theme" Target="theme/theme1.xml"/></Relationships>""",
            StringComparison.Ordinal));
        return Add(related, "xl/theme/theme1.xml", theme);
    }

    /// <summary>The worksheet's XML with <paramref name="xml"/> written after its cells.</summary>
    public static Func<string, string> AfterCells(string xml) => sheet => sheet.Replace("</sheetData>", "</sheetData>" + xml, StringComparison.Ordinal);

    public static async Task<XlsxOpenResult> ReadAsync(byte[] file, CultureInfo? culture = null, XlsxReadOptions? options = null)
        => await XlsxWorkbookReader.ReadAsync(new MemoryStream(file), culture ?? EnUs, options, Token);

    public static async Task<XlsxOpenException> RefusedAsync(byte[] file)
        => await Assert.ThrowsAsync<XlsxOpenException>(async () => await ReadAsync(file));

    /// <summary>The document's Sheet named <paramref name="name"/>.</summary>
    public static SheetDocument Sheet(this WorkbookDocument document, string name) => document.Sheets.Single(s => s.Name == name);

    /// <summary>The Entry the Sheet records at <paramref name="at"/>, or null.</summary>
    public static Entry? EntryAt(this SheetDocument sheet, string at) => sheet.Cells.SingleOrDefault(c => c.Address == CellAddress.Parse(at))?.Entry;

    private static Dictionary<string, string> Parts(byte[] file)
    {
        using var zip = new ZipArchive(new MemoryStream(file), ZipArchiveMode.Read);
        var parts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in zip.Entries)
        {
            using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
            parts[entry.FullName] = reader.ReadToEnd();
        }
        return parts;
    }

    private static byte[] Zip(Dictionary<string, string> parts)
    {
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in parts)
            {
                using var stream = zip.CreateEntry(name).Open();
                stream.Write(Encoding.UTF8.GetBytes(content));
            }
        }
        return memory.ToArray();
    }
}
