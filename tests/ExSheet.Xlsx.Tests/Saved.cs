using ExSheet.Engine;
using static ExSheet.Xlsx.Tests.Book;

namespace ExSheet.Xlsx.Tests;

/// <summary>Workbooks written to a file by ExSheet.Xlsx's own writer (<see cref="XlsxWorkbookWriter"/>), as an application saves one.</summary>
internal static class Saved
{
    /// <summary>A Workbook Document opened as a Workbook, every Value computed, and written.</summary>
    public static Task<byte[]> WriteAsync(WorkbookDocument document) => WriteAsync(Workbook.Open(document));

    /// <summary>The Workbook as it stands, written.</summary>
    public static async Task<byte[]> WriteAsync(Workbook workbook, XlsxSaveOptions? options = null)
    {
        using var stream = new MemoryStream();
        await XlsxWorkbookWriter.WriteAsync(workbook, stream, options, Token);
        return stream.ToArray();
    }
}
