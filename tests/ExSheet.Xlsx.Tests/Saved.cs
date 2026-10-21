using System.Globalization;
using ExGrid.Data.Xlsx;
using ExSheet.Engine;
using static ExSheet.Xlsx.Tests.Book;

namespace ExSheet.Xlsx.Tests;

/// <summary>
/// A Workbook Document written to a file through the formats' writing half (<see cref="StyleTable"/>,
/// <see cref="SheetFormatPlan"/>), as the Workbook's writer (ticket 04) writes them: every Cell Format at its level and
/// every column width, with the document's constants and Formulas as they are, so that a round trip can be read back.
/// </summary>
internal static class Saved
{
    public static async Task<byte[]> WriteAsync(WorkbookDocument document)
    {
        var styles = new StyleTable(CultureInfo.GetCultureInfo(document.Culture));
        var plans = document.Sheets.Select(s => SheetFormatPlan.For(s, styles)).ToList();
        var shown = document.Sheets.ToList().FindIndex(s => s.Name == document.Shown);
        using var stream = new MemoryStream();
        await using (var writer = XlsxWriter.Create(stream, new XlsxWriteOptions { Styles = styles.Build(), ActiveSheet = shown }))
        {
            for (var i = 0; i < document.Sheets.Count; i++)
            {
                var sheet = document.Sheets[i];
                var plan = plans[i];
                var entries = sheet.Cells.Where(c => c.Entry is not null).ToDictionary(c => c.Address, c => c.Entry!);
                var rows = plan.Rows.ToDictionary(r => r.Row);
                var cells = plan.Cells.Keys.ToLookup(a => a.Row);
                var write = writer.AddSheet(sheet.Name, plan.Columns);
                foreach (var row in cells.Select(g => g.Key).Union(rows.Keys).Order())
                {
                    if (rows.TryGetValue(row, out var formatted)) write.WriteRow(formatted);
                    foreach (var at in cells[row].OrderBy(a => a.Column))
                        write.Write(Cell(at, entries.GetValueOrDefault(at), plan.Cells[at]));
                }
            }
            await writer.CompleteAsync(Token);
        }
        return stream.ToArray();
    }

    private static XlsxCell Cell(CellAddress at, Entry? entry, int style)
    {
        var cell = new XlsxCell(at.Row, at.Column, XlsxCellType.Number, null, StyleIndex: style);
        if (entry is null) return cell;
        if (entry.Formula is { } formula) return cell with { Formula = new XlsxFormula(formula[1..]) };
        var value = entry.Constant!.Value;
        return value.Kind switch
        {
            ValueKind.Number => cell with { Value = value.Number.ToString("R", CultureInfo.InvariantCulture) },
            ValueKind.Text => cell with { Type = XlsxCellType.SharedString, Text = value.Text },
            ValueKind.Boolean => cell with { Type = XlsxCellType.Boolean, Value = value.Boolean ? "1" : "0" },
            _ => cell with { Type = XlsxCellType.Error, Value = value.ToString() },
        };
    }
}
