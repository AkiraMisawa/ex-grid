using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using ExSheet.Engine;
using Xunit;

namespace ExSheet.Engine.Tests;

/// <summary>
/// SH-19 on CoreCLR: what a recalculation costs at a large Sheet (ADR-0047: "how fast this is at a
/// large Sheet is measured, not asserted"), measured and never gated — a measurement swings with the
/// machine (AGENTS.md). These run only when asked for, in a Release build, and write what they
/// measured to the test output:
/// <code>
/// dotnet build tests/ExSheet.Engine.Tests -c Release
/// dotnet tests/ExSheet.Engine.Tests/bin/Release/net10.0/ExSheet.Engine.Tests.dll -explicit only -showLiveOutput
/// </code>
/// The case is the one decided with the user on 2026-10-11 (the Definition of Done's SH-19): a
/// synthetic Sheet from a fixed seed, 10,000 rows by 20 columns with about 50,000 Formulas — per-row
/// arithmetic, a running total chained down a column, and <c>SUM</c> over whole columns, its twenty
/// totals standing beside the block. How it is laid out is <see cref="Entries"/>'s.
/// </summary>
/// <remarks>
/// The engine is synchronous and single-threaded (<see cref="Sheet"/>'s remarks): each piece of work
/// is done on the thread that asks, in one piece, so a time here is how long the change holds the
/// Sheet, and on the Server host the circuit that drives it. The Sheet with a whole-column <c>SUM</c>
/// on every row, which SH-19 does not ask for, is measured by <c>spikes/exsheet-sh19-whole-column</c>.
/// </remarks>
public class Measurements
{
    private const string Name = "SH-19's case";
    private const int Rows = 10_000;
    private const int Seed = 20261011;

    // Each time is taken seven times, after two runs to warm up.
    private const int WarmUps = 2;
    private const int Runs = 7;

    [Fact(Explicit = true)] // SH-19 / ADR-0047 / ADR-0048: a full recalculation, every Value computed again from the Entries as a Sheet Document opens
    public void SH19_a_full_recalculation()
    {
        var output = TestContext.Current.TestOutputHelper!;
        output.WriteLine(Machine());
        FullRecalculation(output, Built());
    }

    [Fact(Explicit = true)] // SH-19 / ADR-0047: one edit's propagation, from a constant to everything that reads it
    public void SH19_one_edits_propagation()
    {
        var output = TestContext.Current.TestOutputHelper!;
        output.WriteLine(Machine());
        Edits(output, Built());
    }

    [Fact(Explicit = true)] // SH-19 / ADR-0046 / ADR-0047: a row inserted near the top, every Reference below it rewritten, and its undo
    public void SH19_a_row_inserted_near_the_top()
    {
        var output = TestContext.Current.TestOutputHelper!;
        output.WriteLine(Machine());
        Insertion(output, Built());
    }

    /// <summary>The Sheet opened from its Sheet Document: every Value computed from the Entries, as a Consumer's saved Sheet opens (ADR-0048).</summary>
    private static void FullRecalculation(ITestOutputHelper output, Sheet sheet)
    {
        var document = sheet.ToDocument();
        output.WriteLine(Describe(document));
        Sheet? opened = null;
        Report(output, $"{Name}: Sheet.Open, every Value computed", Time(() => opened = Sheet.Open(document), () => Check(opened!)));
    }

    /// <summary>
    /// A constant typed into a cell, as the Cell Editor commits it (<see cref="Sheet.Do"/> with
    /// <see cref="SheetEdit.Enter(CellAddress, string)"/>, which also records its undo step), 7 and
    /// 11 in turn so that every run changes the Value. Each run recalculates the Formulas it reaches
    /// and no other (ADR-0047), and how many those are is checked with its Values.
    /// </summary>
    private static void Edits(ITestOutputHelper output, Sheet sheet)
    {
        foreach (var (where, address, reached) in new[]
                 {
                     // P1, Q1, R1 and T1, S1 to S10000, and the totals of A and of P to T: 10,010.
                     ("A1, which the whole running total reads", new CellAddress(0, 0), Rows + 10),
                     // P to T of its row, and the totals of A and of P to T: 11.
                     ("A10000, which its own row and the totals read", new CellAddress(Rows - 1, 0), 11),
                     // V15, =SUM(O:O).
                     ("O5000, which only its column's total reads", new CellAddress(4_999, 14), 1),
                 })
        {
            var next = 0;
            SheetStep? step = null;
            var times = Time(() => step = sheet.Do(SheetEdit.Enter(address, (++next % 2 == 0 ? 7 : 11).ToString(CultureInfo.InvariantCulture))), () =>
            {
                Assert.Equal(reached, step!.Change.Recalculated.Count);
                Check(sheet);
            });
            Report(output, $"{Name}: an edit to {where}", times,
                $"{step!.Change.Recalculated.Count:N0} recalculated, {step.Change.ValueChanges.Count:N0} Values changed");
        }
    }

    /// <summary>
    /// A row inserted at row 2, as Ctrl with + inserts one (<see cref="SheetEdit.InsertRows"/>):
    /// every Reference below it is rewritten and the dependency graph rebuilt. Its undo puts the
    /// Sheet back for the next run, and is timed too. Each is checked as it leaves the Sheet, before
    /// the next is done: the inserted Sheet with row 2 blank, every number one row further down than
    /// it was and every Formula reading where its cells went; the undone one as it was before.
    /// </summary>
    private static void Insertion(ITestOutputHelper output, Sheet sheet)
    {
        for (var warm = 0; warm < WarmUps; warm++)
        {
            sheet.Do(SheetEdit.InsertRows(1)).Undo();
        }
        var before = Numbers(sheet);
        // Every Formula on row 2 or below has its References rewritten, and every one that reads a
        // whole column reads cells that moved: all but row 1's, P1 to T1, which read no whole column.
        var reached = (Rows * 5) + 20 - 5;
        var inserts = new Series();
        var undos = new Series();
        SheetStep? step = null;
        for (var run = 0; run < Runs; run++)
        {
            inserts.Time(() => step = sheet.Do(SheetEdit.InsertRows(1)));
            Assert.Equal(reached, step!.Change.Recalculated.Count);
            Check(sheet, insertedAt: 1);
            Assert.Equal(before, Numbers(sheet, insertedAt: 1));
            undos.Time(() => step!.Undo());
            Check(sheet);
            Assert.Equal(before, Numbers(sheet));
        }
        Report(output, $"{Name}: a row inserted at row 2", inserts,
            $"{step!.Change.Recalculated.Count:N0} recalculated, {step.Change.Rows.Count:N0} rows changed");
        Report(output, $"{Name}: that insertion undone", undos);
    }

    private static Sheet Built()
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        sheet.SetEntries(Entries());
        return sheet;
    }

    /// <summary>
    /// The case as it is laid out. Rows 1 to 10,000 of columns A to T are the 10,000 by 20:
    /// <list type="bullet">
    /// <item>A to O hold constants from the seed: a quantity in A, a price in B, and amounts in C to O;</item>
    /// <item>P, Q and R are per-row arithmetic, each reading the one before it: <c>=A1*B1</c>,
    /// <c>=P1-C1+D1*E1</c> and <c>=(Q1+F1)/(1+G1/100)</c>;</item>
    /// <item>S is the running total chained down the column: <c>=R1</c>, then <c>=S1+R2</c> and on, so
    /// that S10000 is reached through 10,000 cells;</item>
    /// <item>T is per-row arithmetic again, <c>=R1*H1/100-I1</c>.</item>
    /// </list>
    /// Beside the block, V1 to V20 total each of its columns over the whole column, <c>=SUM(A:A)</c> to
    /// <c>=SUM(T:T)</c> — beside it, since a whole-column <c>SUM</c> inside its own column would read
    /// itself. That is 50,020 Formulas, 20 of them reading a whole column, and 150,000 constants.
    /// </summary>
    private static List<KeyValuePair<CellAddress, Entry?>> Entries()
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
                Put(row, 15 + i, Entry.FromFormula(formulas[i]));
            }
        }
        for (var column = 0; column < 20; column++)
        {
            Put(column, 21, Entry.FromFormula(Total(column)));
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
            $"=R{n}*H{n}/100-I{n}",
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
    /// The case as the Sheet holds it, checked against the same arithmetic done here: a time is worth
    /// nothing over a wrong answer. Every Formula of P to T and of V reads where its cells are, every
    /// Value of P to T is the arithmetic's, and each of the twenty totals is its column's sum. With a
    /// row inserted at <paramref name="insertedAt"/>, the block's rows below it are one further down,
    /// and that row holds nothing.
    /// </summary>
    private static void Check(Sheet sheet, int? insertedAt = null)
    {
        double At(int row, int column) => Number(sheet, new CellAddress(Place(row, insertedAt), column));
        void Reads(string formula, int row, int column)
        {
            var address = new CellAddress(Place(row, insertedAt), column);
            if (sheet.GetEntry(address)?.Formula != formula)
            {
                Assert.Fail($"{address} holds {sheet.GetEntry(address)?.ToString() ?? "nothing"}, where {formula} was expected.");
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
            var t = (r * At(row, 7) / 100) - At(row, 8);
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
                Assert.True(sheet.GetEntry(address) is null && sheet.GetValue(address) is null, $"{address}, in the row inserted, holds {sheet.GetEntry(address)?.ToString() ?? "nothing"}, shown as {sheet.GetValue(address)?.ToString() ?? "nothing"}.");
            }
        }
    }

    /// <summary>
    /// Every number of the block and of its totals, in the case's own order wherever its rows are now:
    /// what an insertion moves and its undo puts back, compared exactly.
    /// </summary>
    private static double[] Numbers(Sheet sheet, int? insertedAt = null)
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
        : throw new Xunit.Sdk.XunitException($"{address} is {sheet.GetValue(address)?.ToString() ?? "blank"}, not a number.");

    // Excel's arithmetic keeps fifteen significant digits, and so may differ from this one's in the last of them.
    private static void Close(double expected, double actual, CellAddress where) =>
        Assert.True(Math.Abs(expected - actual) <= 1e-9 * Math.Max(1, Math.Abs(expected)), $"{where} is {actual}, where {expected} was expected.");

    private static string Describe(SheetDocument document)
    {
        var formulas = document.Cells.Count(c => c.Entry is { IsFormula: true });
        var wholeColumns = document.Cells.Count(c => c.Entry?.Formula?.Contains(":", StringComparison.Ordinal) == true);
        var constants = document.Cells.Count(c => c.Entry is { IsFormula: false });
        return string.Create(CultureInfo.InvariantCulture,
            $"{Name}: {Rows:N0} rows by 20 columns, and 20 totals beside them; {formulas:N0} Formulas, {wholeColumns:N0} of them reading a whole column, and {constants:N0} constants (seed {Seed})");
    }

    private static string Machine() => string.Create(CultureInfo.InvariantCulture,
        $"{Environment.ProcessorCount} logical CPUs, {RuntimeInformation.FrameworkDescription}, {RuntimeInformation.OSDescription}, server GC {System.Runtime.GCSettings.IsServerGC}, load average {LoadAverage()}");

    // The machine's load as the measurement starts: these numbers are a shared machine's.
    private static string LoadAverage()
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

    private static void Report(ITestOutputHelper output, string what, Series series, string? note = null)
    {
        var sorted = series.Milliseconds.Order().ToArray();
        var median = (sorted[(sorted.Length - 1) / 2] + sorted[sorted.Length / 2]) / 2;
        output.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{what}: median {median:F1} ms, best {sorted[0]:F1} ms, worst {sorted[^1]:F1} ms (n={sorted.Length}){(note is null ? "" : "; " + note)}; collections by run: {string.Join(' ', series.Collections)}"));
    }
}
