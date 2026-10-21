using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Runtime.InteropServices;
using ExGrid.Data.Xlsx;

// Writes workbooks of increasing size — ten columns a row: four numbers, three texts, two Formulas and a date, most
// styled — and reads each back with the limits lifted, timing both and recording the file's size, its worksheet's
// uncompressed XML, its shared strings, what the read allocated and what it held at its middle. The numbers set the
// default limits (ADR-0330 §9; ticket 01 of docs/specs/exsheet-xlsx). Run in Release:
//   dotnet run -c Release --project tests/ExGrid.Data.Xlsx.Tests/Measure -- <directory> [cells …]
// With MEASURE_UNIQUE=1 every text is its own: the worst case for the shared strings, the one part held whole.

// The read is measured in a process of its own, so that nothing the write left behind is counted:
//   Measure read <file> <cells> prints the seconds it took, the cells read, the sheet's XML bytes, the bytes it allocated,
//   the bytes it held at its middle above what the process held before it opened the file, and the process's peak
//   working set.
if (args is ["read", var readPath, var readCells])
{
    Settle();
    var before = GC.GetTotalMemory(forceFullCollection: true);
    var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
    var timer = Stopwatch.StartNew();
    var (count, bytes, middle) = await ReadBook(readPath, long.Parse(readCells, CultureInfo.InvariantCulture), timer);
    timer.Stop();
    var allocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
    Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
        $"{timer.Elapsed.TotalSeconds} {count} {bytes} {allocated} {middle - before} {Process.GetCurrentProcess().PeakWorkingSet64}"));
    return 0;
}

if (args.Length < 1)
{
    Console.Error.WriteLine("usage: Measure <directory> [cells …]");
    return 2;
}
var directory = args[0];
Directory.CreateDirectory(directory);
long[] sizes = args.Length > 1 ? [.. args.Skip(1).Select(a => long.Parse(a, CultureInfo.InvariantCulture))] : [100_000, 1_000_000, 5_000_000, 10_000_000];
var unique = Environment.GetEnvironmentVariable("MEASURE_UNIQUE") == "1";
Console.WriteLine($"{Environment.ProcessorCount} logical CPUs, {RuntimeInformation.FrameworkDescription}, {RuntimeInformation.OSDescription}");
Console.WriteLine(unique ? "every text unique" : "texts from a pool of 1,000");
Console.WriteLine("cells | file MiB | sheet XML MiB | XML bytes/cell | shared strings MiB | write s | read s | cells/s read | allocated MiB | held mid-read MiB | peak working set MiB");

foreach (var cells in sizes)
{
    var rows = (int)(cells / 10);
    var path = Path.Combine(directory, $"measure-{cells}.xlsx");

    var write = Stopwatch.StartNew();
    await WriteBook(path, rows, unique);
    write.Stop();

    var self = typeof(Program).Assembly.Location;
    var child = Process.Start(new ProcessStartInfo(Environment.ProcessPath!, [self, "read", path, cells.ToString(CultureInfo.InvariantCulture)])
    {
        RedirectStandardOutput = true,
    })!;
    var line = (await child.StandardOutput.ReadToEndAsync()).Trim().Split(' ');
    await child.WaitForExitAsync();
    var seconds = double.Parse(line[0], CultureInfo.InvariantCulture);
    var read = long.Parse(line[1], CultureInfo.InvariantCulture);
    var sheetBytes = long.Parse(line[2], CultureInfo.InvariantCulture);
    var allocated = long.Parse(line[3], CultureInfo.InvariantCulture);
    var held = long.Parse(line[4], CultureInfo.InvariantCulture);
    var peak = long.Parse(line[5], CultureInfo.InvariantCulture);
    if (read != (long)rows * 10)
        throw new InvalidOperationException($"Read {read} cells of {rows * 10L}.");
    long sharedBytes;
    using (var zip = new ZipArchive(File.OpenRead(path)))
        sharedBytes = zip.GetEntry("xl/sharedStrings.xml")?.Length ?? 0;
    var fileBytes = new FileInfo(path).Length;
    Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
        $"{cells:N0} | {Mib(fileBytes)} | {Mib(sheetBytes)} | {sheetBytes / (double)cells:F1} | {Mib(sharedBytes)} | {write.Elapsed.TotalSeconds:F2} | {seconds:F2} | {read / seconds:N0} | {Mib(allocated)} | {Mib(held)} | {Mib(peak)}"));
    File.Delete(path);
}
return 0;

static void Settle()
{
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
}

static async Task<(long Read, long SheetBytes, long Middle)> ReadBook(string path, long cells, Stopwatch timer)
{
    long read = 0;
    long middle = 0;
    var options = new XlsxReadOptions { MaxCells = long.MaxValue, MaxUncompressedBytes = long.MaxValue };
    await using var file = File.OpenRead(path);
    using var reader = await XlsxReader.OpenAsync(file, options);
    using var sheet = reader.OpenSheet(0);
    while (await sheet.ReadAsync())
    {
        read += sheet.Cells.Count;
        if (middle == 0 && read >= cells / 2)
        {
            timer.Stop();
            middle = GC.GetTotalMemory(forceFullCollection: true);
            timer.Start();
        }
    }
    return (read, reader.SheetBytes, middle);
}

static async Task WriteBook(string path, int rows, bool unique)
{
    var styles = XlsxStyles.Default with
    {
        NumberFormats = new Dictionary<int, string> { [164] = "#,##0.00" },
        CellFormats = [new XlsxCellFormat(), new XlsxCellFormat(164), new XlsxCellFormat(14)],
    };
    string[] pool = [.. Enumerable.Range(0, 1000).Select(i => $"Desk {i:D4} – {(char)('A' + i % 26)}")];
    string Text(int r, int k) => unique ? $"Trade {r:D8}-{k} EMEA" : pool[(r * (k * 6 + 1)) % pool.Length];

    await using var file = File.Create(path);
    await using var book = XlsxWriter.Create(file, new XlsxWriteOptions { Styles = styles });
    var sheet = book.AddSheet("Data");
    for (var r = 0; r < rows; r++)
    {
        var row = (r + 1).ToString(CultureInfo.InvariantCulture);
        sheet.Write(new XlsxCell(r, 0, XlsxCellType.Number, row));
        sheet.Write(new XlsxCell(r, 1, XlsxCellType.Number, (r * 1.37).ToString("R", CultureInfo.InvariantCulture), StyleIndex: 1));
        sheet.Write(new XlsxCell(r, 2, XlsxCellType.Number, (r % 977 - 400).ToString(CultureInfo.InvariantCulture)));
        sheet.Write(new XlsxCell(r, 3, XlsxCellType.Number, (r * 0.001).ToString("R", CultureInfo.InvariantCulture), StyleIndex: 1));
        sheet.Write(new XlsxCell(r, 4, XlsxCellType.SharedString, null, Text(r, 0)));
        sheet.Write(new XlsxCell(r, 5, XlsxCellType.SharedString, null, Text(r, 1)));
        sheet.Write(new XlsxCell(r, 6, XlsxCellType.SharedString, null, Text(r, 2)));
        sheet.Write(new XlsxCell(r, 7, XlsxCellType.Number, (r * 2.37 + 1).ToString("R", CultureInfo.InvariantCulture), Formula: new XlsxFormula($"A{row}+B{row}"), StyleIndex: 1));
        sheet.Write(new XlsxCell(r, 8, XlsxCellType.Number, "1", Formula: new XlsxFormula($"IF(C{row}>0,1,0)")));
        sheet.Write(new XlsxCell(r, 9, XlsxCellType.Number, (45000 + r % 3650).ToString(CultureInfo.InvariantCulture), StyleIndex: 2));
        if (r % 4096 == 0)
            await sheet.FlushAsync();
    }
    await book.CompleteAsync();
}

static string Mib(long bytes) => (bytes / 1048576.0).ToString("F1", CultureInfo.InvariantCulture);
