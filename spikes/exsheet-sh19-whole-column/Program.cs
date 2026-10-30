using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using ExSheet.Engine;

// SH-19's three measurements on the variant of its Sheet where every row reads a whole column.
//
//   dotnet run -c Release --project spikes/exsheet-sh19-whole-column [-- <rows>]
//
// <rows> is the block's height, 10,000 by default, which is SH-19's. A smaller one proves the spike
// works in seconds; its times say nothing about SH-19's size, since the variant's cost grows with
// the square of the rows. Every timed run is checked after it, outside the clock, as
// dotnet/tests/ExSheet.Engine.Tests' Measurements checks SH-19's case; a check that fails ends the run with
// exit code 1 and no time for it.

var rows = args.Length > 0 ? int.Parse(args[0], NumberStyles.AllowThousands, CultureInfo.InvariantCulture) : 10_000;
if (rows < 2)
{
    Console.Error.WriteLine("The block needs at least two rows: one inserted at row 2 must have a row below it.");
    return 2;
}
var started = DateTimeOffset.UtcNow;
Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{started:yyyy-MM-ddTHH:mm:ssZ}"));
Console.WriteLine(WholeColumn.Machine());
try
{
    new WholeColumn(rows).Measure();
}
catch (CheckFailed failed)
{
    Console.Error.WriteLine($"CHECK FAILED: {failed.Message}");
    return 1;
}
Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
    $"{DateTimeOffset.UtcNow:yyyy-MM-ddTHH:mm:ssZ}, {(DateTimeOffset.UtcNow - started).TotalMinutes:F1} min in all; load average {WholeColumn.LoadAverage()}"));
return 0;

/// <summary>A time is worth nothing over a wrong answer: what was timed did not hold.</summary>
internal sealed class CheckFailed(string message) : Exception(message);

/// <summary>
/// SH-19's Sheet with T written as each row's share of R's whole-column total. Rows 1 to
/// <see cref="Rows"/> of columns A to T are the block, from seed 20261011 as SH-19's case is:
/// <list type="bullet">
/// <item>A to O hold constants: a quantity in A, a price in B, and amounts in C to O;</item>
/// <item>P, Q and R are per-row arithmetic, each reading the one before it: <c>=A1*B1</c>,
/// <c>=P1-C1+D1*E1</c> and <c>=(Q1+F1)/(1+G1/100)</c>;</item>
/// <item>S is the running total chained down the column: <c>=R1</c>, then <c>=S1+R2</c> and on;</item>
/// <item>T is each row's share of R's total, <c>=R1/SUM(R:R)</c>, where SH-19's case has
/// <c>=R1*H1/100-I1</c>.</item>
/// </list>
/// Beside the block, V1 to V20 total each of its columns over the whole column, <c>=SUM(A:A)</c> to
/// <c>=SUM(T:T)</c>. At 10,000 rows that is 50,020 Formulas, 10,020 of them reading a whole column
/// where SH-19's case has 20, and 150,000 constants.
/// </summary>
internal sealed class WholeColumn(int rows)
{
    private const string Name = "every row's share of a whole-column SUM";
    private const int Seed = 20261011;

    // Each run takes tens of seconds at 10,000 rows, so each time is taken three times with no run
    // to warm up; the spread of the three says what that costs.
    private const int WarmUps = 0;
    private const int Runs = 3;

    private int Rows { get; } = rows;

    public void Measure()
    {
        var built = Stopwatch.StartNew();
        var sheet = Built();
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{Name}: built by one SetEntries in {built.Elapsed.TotalMilliseconds:F0} ms"));
        FullRecalculation(sheet);
        Edits(sheet);
        Insertion(sheet);
    }

    /// <summary>The Sheet opened from its Sheet Document: every Value computed from the Entries, as a Consumer's saved Sheet opens (ADR-0048).</summary>
    private void FullRecalculation(Sheet sheet)
    {
        var document = sheet.ToDocument();
        Console.WriteLine(Describe(document));
        Sheet? opened = null;
        Report($"{Name}: Sheet.Open, every Value computed", Time(() => opened = Sheet.Open(document), () => Check(opened!)));
    }

    /// <summary>
    /// A constant typed into a cell, as the Cell Editor commits it, 7 and 11 in turn so that every
    /// run changes the Value; how many Formulas each run recalculates is checked with its Values.
    /// </summary>
    private void Edits(Sheet sheet)
    {
        var middle = (Rows / 2) - 1;
        foreach (var (where, address, reached) in new[]
                 {
                     // P1, Q1, R1 and T1, S1 to the last S, the totals of A and of P to T, and,
                     // since every T reads R's total, the rest of T: 20,009 at 10,000 rows.
                     ("A1, which the whole running total reads", new CellAddress(0, 0), (2 * Rows) + 9),
                     // P to T of its row, the totals of A and of P to T, and the rest of T: 10,010.
                     (string.Create(CultureInfo.InvariantCulture, $"A{Rows}, which its own row and the totals read"), new CellAddress(Rows - 1, 0), Rows + 10),
                     // V15, =SUM(O:O).
                     (string.Create(CultureInfo.InvariantCulture, $"O{middle + 1}, which only its column's total reads"), new CellAddress(middle, 14), 1),
                 })
        {
            var next = 0;
            SheetStep? step = null;
            var times = Time(() => step = sheet.Do(SheetEdit.Enter(address, (++next % 2 == 0 ? 7 : 11).ToString(CultureInfo.InvariantCulture))), () =>
            {
                Equal(reached, step!.Change.Recalculated.Count, $"Formulas recalculated by an edit to {address}");
                Check(sheet);
            });
            Report($"{Name}: an edit to {where}", times,
                string.Create(CultureInfo.InvariantCulture, $"{step!.Change.Recalculated.Count:N0} recalculated, {step.Change.ValueChanges.Count:N0} Values changed"));
        }
    }

    /// <summary>
    /// A row inserted at row 2, and its undo, each timed and each checked as it leaves the Sheet: the
    /// inserted Sheet with row 2 blank, every number one row further down and every Formula reading
    /// where its cells went; the undone one as it was before.
    /// </summary>
    private void Insertion(Sheet sheet)
    {
        for (var warm = 0; warm < WarmUps; warm++)
        {
            sheet.Do(SheetEdit.InsertRows(1)).Undo();
        }
        var before = Numbers(sheet);
        // Every Formula on row 2 or below has its References rewritten, and every one that reads a
        // whole column reads cells that moved: all but row 1's that read no whole column, P1 to S1,
        // since T1 reads R's total.
        var reached = (Rows * 5) + 20 - 4;
        var inserts = new Series();
        var undos = new Series();
        SheetStep? step = null;
        for (var run = 0; run < Runs; run++)
        {
            inserts.Time(() => step = sheet.Do(SheetEdit.InsertRows(1)));
            Equal(reached, step!.Change.Recalculated.Count, "Formulas recalculated by the insertion");
            Check(sheet, insertedAt: 1);
            SameNumbers(before, Numbers(sheet, insertedAt: 1), "after the insertion");
            undos.Time(() => step!.Undo());
            Check(sheet);
            SameNumbers(before, Numbers(sheet), "after its undo");
        }
        Report($"{Name}: a row inserted at row 2", inserts,
            string.Create(CultureInfo.InvariantCulture, $"{step!.Change.Recalculated.Count:N0} recalculated, {step.Change.Rows.Count:N0} rows changed"));
        Report($"{Name}: that insertion undone", undos);
    }

    private Sheet Built()
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        sheet.SetEntries(Entries());
        return sheet;
    }

    private List<KeyValuePair<CellAddress, Entry?>> Entries()
    {
        var random = new Random(Seed);
        var entries = new List<KeyValuePair<CellAddress, Entry?>>((Rows * 20) + 20);
        void Put(int row, int column, Entry entry) => entries.Add(new(new CellAddress(row, column), entry));
        for (var row = 0; row < Rows; row++)
        {
            Put(row, 0, Entry.FromValue(Value.FromNumber(random.Next(1, 500))));
            Put(row, 1, Entry.FromValue(Value.FromNumber(Math.Round(1 + (random.NextDouble() * 999), 2))));
            for (var column = 2; column < 15; column++)
            {
                Put(row, column, Entry.FromValue(Value.FromNumber(Math.Round(random.NextDouble() * 1000, 2))));
            }
            var formulas = RowFormulas(row, row == 0 ? null : row - 1);
            for (var i = 0; i < formulas.Length; i++)
            {
                Put(row, 15 + i, Entry.Parse(formulas[i], CultureInfo.InvariantCulture)!);
            }
        }
        for (var column = 0; column < 20; column++)
        {
            Put(column, 21, Entry.Parse(Total(column), CultureInfo.InvariantCulture)!);
        }
        return entries;
    }

    /// <summary>
    /// The Formulas of P to T in the block's row now at <paramref name="row"/>, where the block's row
    /// before it is now at <paramref name="above"/> (both counted from 0), or none for the first.
    /// </summary>
    private static string[] RowFormulas(int row, int? above)
    {
        var n = (row + 1).ToString(CultureInfo.InvariantCulture);
        var a = ((above ?? 0) + 1).ToString(CultureInfo.InvariantCulture);
        return
        [
            $"=A{n}*B{n}",
            $"=P{n}-C{n}+D{n}*E{n}",
            $"=(Q{n}+F{n})/(1+G{n}/100)",
            above is null ? $"=R{n}" : $"=S{a}+R{n}",
            $"=R{n}/SUM(R:R)",
        ];
    }

    /// <summary>The total of the block's column <paramref name="column"/>, in V.</summary>
    private static string Total(int column)
    {
        var letters = CellAddress.ColumnName(column);
        return $"=SUM({letters}:{letters})";
    }

    /// <summary>Where the block's row <paramref name="row"/> is now, with a row inserted at <paramref name="insertedAt"/> or none.</summary>
    private static int Place(int row, int? insertedAt) => insertedAt is { } inserted && row >= inserted ? row + 1 : row;

    /// <summary>
    /// The Sheet checked against the same arithmetic done here: every Formula of P to T and of V
    /// reads where its cells are, every Value of P to T is the arithmetic's, and each of the twenty
    /// totals is its column's sum. With a row inserted at <paramref name="insertedAt"/>, the block's
    /// rows below it are one further down, and that row holds nothing.
    /// </summary>
    private void Check(Sheet sheet, int? insertedAt = null)
    {
        double At(int row, int column) => Number(sheet, new CellAddress(Place(row, insertedAt), column));
        void Reads(string formula, int row, int column)
        {
            var address = new CellAddress(Place(row, insertedAt), column);
            if (sheet.GetEntry(address)?.Formula != formula)
            {
                throw new CheckFailed($"{address} holds {sheet.GetEntry(address)?.ToString() ?? "nothing"}, where {formula} was expected.");
            }
        }
        var totals = new double[20];
        var running = 0.0;
        for (var row = 0; row < Rows; row++)
        {
            var formulas = RowFormulas(Place(row, insertedAt), row == 0 ? null : Place(row - 1, insertedAt));
            for (var i = 0; i < formulas.Length; i++)
            {
                Reads(formulas[i], row, 15 + i);
            }
            for (var column = 0; column < 15; column++)
            {
                totals[column] += At(row, column);
            }
            var p = At(row, 0) * At(row, 1);
            var q = p - At(row, 2) + (At(row, 3) * At(row, 4));
            var r = (q + At(row, 5)) / (1 + (At(row, 6) / 100));
            running += r;
            // R's total as the Sheet holds it in V18, which is itself checked against the sum below.
            var t = r / At(17, 21);
            double[] computed = [p, q, r, running, t];
            for (var i = 0; i < computed.Length; i++)
            {
                Close(computed[i], At(row, 15 + i), new CellAddress(Place(row, insertedAt), 15 + i));
                totals[15 + i] += computed[i];
            }
        }
        for (var column = 0; column < 20; column++)
        {
            Reads(Total(column), column, 21);
            Close(totals[column], At(column, 21), new CellAddress(Place(column, insertedAt), 21));
        }
        if (insertedAt is { } inserted)
        {
            for (var column = 0; column <= 21; column++)
            {
                var address = new CellAddress(inserted, column);
                if (sheet.GetEntry(address) is not null || sheet.GetValue(address) is not null)
                {
                    throw new CheckFailed($"{address}, in the row inserted, holds {sheet.GetEntry(address)?.ToString() ?? "nothing"}, shown as {sheet.GetValue(address)?.ToString() ?? "nothing"}.");
                }
            }
        }
    }

    /// <summary>
    /// Every number of the block and of its totals, in the case's own order wherever its rows are now:
    /// what an insertion moves and its undo puts back, compared exactly.
    /// </summary>
    private double[] Numbers(Sheet sheet, int? insertedAt = null)
    {
        var numbers = new double[(Rows * 20) + 20];
        for (var row = 0; row < Rows; row++)
        {
            for (var column = 0; column < 20; column++)
            {
                numbers[(row * 20) + column] = Number(sheet, new CellAddress(Place(row, insertedAt), column));
            }
        }
        for (var column = 0; column < 20; column++)
        {
            numbers[(Rows * 20) + column] = Number(sheet, new CellAddress(Place(column, insertedAt), 21));
        }
        return numbers;
    }

    private static double Number(Sheet sheet, CellAddress address) => sheet.GetValue(address) is { Kind: ValueKind.Number } v
        ? v.Number
        : throw new CheckFailed($"{address} is {sheet.GetValue(address)?.ToString() ?? "blank"}, not a number.");

    // Excel's arithmetic keeps fifteen significant digits, and so may differ from this one's in the last of them.
    private static void Close(double expected, double actual, CellAddress where)
    {
        if (!(Math.Abs(expected - actual) <= 1e-9 * Math.Max(1, Math.Abs(expected))))
        {
            throw new CheckFailed($"{where} is {actual.ToString("R", CultureInfo.InvariantCulture)}, where {expected.ToString("R", CultureInfo.InvariantCulture)} was expected.");
        }
    }

    private static void Equal(int expected, int actual, string what)
    {
        if (expected != actual)
        {
            throw new CheckFailed(string.Create(CultureInfo.InvariantCulture, $"{what}: {actual:N0}, where {expected:N0} were expected."));
        }
    }

    private static void SameNumbers(double[] expected, double[] actual, string when)
    {
        for (var i = 0; i < expected.Length; i++)
        {
            // Compared exactly, with double.Equals, as the test's Assert.Equal compares them.
            if (!expected[i].Equals(actual[i]))
            {
                throw new CheckFailed(string.Create(CultureInfo.InvariantCulture, $"Number {i} of the block and its totals {when} is {actual[i]:R}, where {expected[i]:R} was before."));
            }
        }
    }

    private string Describe(SheetDocument document)
    {
        var formulas = document.Cells.Count(c => c.Entry is { IsFormula: true });
        var wholeColumns = document.Cells.Count(c => c.Entry?.Formula?.Contains(':', StringComparison.Ordinal) == true);
        var constants = document.Cells.Count(c => c.Entry is { IsFormula: false });
        return string.Create(CultureInfo.InvariantCulture,
            $"{Name}: {Rows:N0} rows by 20 columns, and 20 totals beside them; {formulas:N0} Formulas, {wholeColumns:N0} of them reading a whole column, and {constants:N0} constants (seed {Seed})");
    }

    public static string Machine() => string.Create(CultureInfo.InvariantCulture,
        $"{Environment.ProcessorCount} logical CPUs, {RuntimeInformation.FrameworkDescription}, {RuntimeInformation.OSDescription}, server GC {System.Runtime.GCSettings.IsServerGC}, {Configuration} build, load average {LoadAverage()}");

#if DEBUG
    private const string Configuration = "Debug (not a measurement: run with -c Release)";
#else
    private const string Configuration = "Release";
#endif

    // The machine's load as the measurement starts: these numbers are a shared machine's.
    public static string LoadAverage()
    {
        try
        {
            return File.Exists("/proc/loadavg") ? string.Join(' ', File.ReadAllText("/proc/loadavg").Split(' ').Take(3)) : "not known here";
        }
        catch (IOException)
        {
            return "not known here";
        }
    }

    /// <summary>Each run timed, after the runs to warm up, and checked after it, outside the clock.</summary>
    private static Series Time(Action run, Action check)
    {
        for (var warm = 0; warm < WarmUps; warm++)
        {
            run();
        }
        var series = new Series();
        for (var i = 0; i < Runs; i++)
        {
            series.Time(run);
            check();
        }
        return series;
    }

    /// <summary>The runs of one piece of work: the milliseconds of each, and the collections each caused.</summary>
    private sealed class Series
    {
        public List<double> Milliseconds { get; } = [];

        public List<string> Collections { get; } = [];

        // A run starts from a collected heap, so that the garbage of the run before is not charged
        // to it; a collection the run itself causes is charged, and counted.
        public void Time(Action run)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var gen0 = GC.CollectionCount(0);
            var gen2 = GC.CollectionCount(2);
            var clock = Stopwatch.StartNew();
            run();
            Milliseconds.Add(clock.Elapsed.TotalMilliseconds);
            Collections.Add(string.Create(CultureInfo.InvariantCulture, $"g0+{GC.CollectionCount(0) - gen0}/g2+{GC.CollectionCount(2) - gen2}"));
        }
    }

    private static void Report(string what, Series series, string? note = null)
    {
        var sorted = series.Milliseconds.Order().ToArray();
        var median = (sorted[(sorted.Length - 1) / 2] + sorted[sorted.Length / 2]) / 2;
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{what}: median {median:F1} ms, best {sorted[0]:F1} ms, worst {sorted[^1]:F1} ms (n={sorted.Length}){(note is null ? "" : "; " + note)}; collections by run: {string.Join(' ', series.Collections)}"));
    }
}
