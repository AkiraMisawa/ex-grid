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
/// arithmetic, a running total chained down a column, and <c>SUM</c> over whole columns. How it is
/// laid out is <see cref="Case"/>'s. The first three measurements are SH-19's; the fourth is the same
/// Sheet with each row's last Formula reading a whole column, which the decided case does not ask
/// for, and which takes many minutes: run SH-19's alone with
/// <c>-method "ExSheet.Engine.Tests.Measurements.SH19_*"</c>.
/// </summary>
/// <remarks>
/// The engine is synchronous and single-threaded (<see cref="Sheet"/>'s remarks): each piece of work
/// is done on the thread that asks, in one piece, so a time here is how long the change holds the
/// Sheet, and on the Server host the circuit that drives it.
/// </remarks>
public class Measurements
{
    private const int Rows = 10_000;
    private const int Seed = 20261011;

    /// <summary>
    /// How the case is laid out. Rows 1 to 10,000 of columns A to T are the 10,000 by 20:
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
    /// itself. That is 50,020 Formulas and 150,000 constants. In the variant, T is each row's share of
    /// R's whole-column total, <c>=R1/SUM(R:R)</c>, so 10,020 Formulas read a whole column, not 20.
    /// </summary>
    private sealed record Case(string Name, bool ShareOfTotal, int WarmUps, int Runs);

    /// <summary>SH-19's case. Each time is taken seven times, after two runs to warm up.</summary>
    private static readonly Case Decided = new("SH-19's case", ShareOfTotal: false, WarmUps: 2, Runs: 7);

    /// <summary>
    /// The variant. Its runs take tens of seconds each, so each is taken three times with no run to
    /// warm up; the spread of the three says what that costs.
    /// </summary>
    private static readonly Case EveryRowReadsAWholeColumn = new("every row's share of a whole-column SUM", ShareOfTotal: true, WarmUps: 0, Runs: 3);

    [Fact(Explicit = true)] // SH-19 / ADR-0047 / ADR-0048: a full recalculation, every Value computed again from the Entries as a Sheet Document opens
    public void SH19_a_full_recalculation()
    {
        var output = TestContext.Current.TestOutputHelper!;
        output.WriteLine(Machine());
        FullRecalculation(output, Decided, Built(Decided));
    }

    [Fact(Explicit = true)] // SH-19 / ADR-0047: one edit's propagation, from a constant to everything that reads it
    public void SH19_one_edits_propagation()
    {
        var output = TestContext.Current.TestOutputHelper!;
        output.WriteLine(Machine());
        Edits(output, Decided, Built(Decided));
    }

    [Fact(Explicit = true)] // SH-19 / ADR-0046 / ADR-0047: a row inserted near the top, every Reference below it rewritten, and its undo
    public void SH19_a_row_inserted_near_the_top()
    {
        var output = TestContext.Current.TestOutputHelper!;
        output.WriteLine(Machine());
        Insertion(output, Decided, Built(Decided));
    }

    [Fact(Explicit = true)] // ADR-0047: SH-19's three measurements again where 10,000 Formulas each read a whole column (formula-dependencies.md §11)
    public void Every_row_reading_a_whole_column()
    {
        var output = TestContext.Current.TestOutputHelper!;
        output.WriteLine(Machine());
        var built = Stopwatch.StartNew();
        var sheet = Built(EveryRowReadsAWholeColumn);
        output.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{EveryRowReadsAWholeColumn.Name}: built by one SetEntries in {built.Elapsed.TotalMilliseconds:F0} ms"));
        FullRecalculation(output, EveryRowReadsAWholeColumn, sheet);
        Edits(output, EveryRowReadsAWholeColumn, sheet);
        Insertion(output, EveryRowReadsAWholeColumn, sheet);
    }

    /// <summary>The Sheet opened from its Sheet Document: every Value computed from the Entries, as a Consumer's saved Sheet opens (ADR-0048).</summary>
    private static void FullRecalculation(ITestOutputHelper output, Case layout, Sheet sheet)
    {
        var document = sheet.ToDocument();
        output.WriteLine(Describe(layout, document));
        Sheet? opened = null;
        Report(output, $"{layout.Name}: Sheet.Open, every Value computed", Time(() => opened = Sheet.Open(document), layout));
        Check(opened!, layout);
    }

    /// <summary>
    /// A constant typed into a cell, as the Cell Editor commits it (<see cref="Sheet.Do"/> with
    /// <see cref="SheetEdit.Enter(CellAddress, string)"/>, which also records its undo step), 7 and
    /// 11 in turn so that every run changes the Value.
    /// </summary>
    private static void Edits(ITestOutputHelper output, Case layout, Sheet sheet)
    {
        foreach (var (where, address) in new[]
                 {
                     ("A1, which the whole running total reads", new CellAddress(0, 0)),
                     ("A10000, which its own row and the totals read", new CellAddress(Rows - 1, 0)),
                     ("O5000, which only its column's total reads", new CellAddress(4_999, 14)),
                 })
        {
            var next = 0;
            SheetStep? step = null;
            var times = Time(() => step = sheet.Do(SheetEdit.Enter(address, (++next % 2 == 0 ? 7 : 11).ToString(CultureInfo.InvariantCulture))), layout);
            Report(output, $"{layout.Name}: an edit to {where}", times,
                $"{step!.Change.Recalculated.Count:N0} recalculated, {step.Change.ValueChanges.Count:N0} Values changed");
            Check(sheet, layout);
        }
    }

    /// <summary>
    /// A row inserted at row 2, as Ctrl with + inserts one (<see cref="SheetEdit.InsertRows"/>):
    /// every Reference below it is rewritten and the dependency graph rebuilt. Its undo puts the
    /// Sheet back for the next run, and is timed too.
    /// </summary>
    private static void Insertion(ITestOutputHelper output, Case layout, Sheet sheet)
    {
        for (var warm = 0; warm < layout.WarmUps; warm++)
        {
            sheet.Do(SheetEdit.InsertRows(1)).Undo();
        }
        var inserts = new Series();
        var undos = new Series();
        SheetStep? step = null;
        for (var run = 0; run < layout.Runs; run++)
        {
            inserts.Time(() => step = sheet.Do(SheetEdit.InsertRows(1)));
            undos.Time(() => step!.Undo());
        }
        Report(output, $"{layout.Name}: a row inserted at row 2", inserts,
            $"{step!.Change.Recalculated.Count:N0} recalculated, {step.Change.Rows.Count:N0} rows changed");
        Report(output, $"{layout.Name}: that insertion undone", undos);
        Check(sheet, layout);
    }

    private static Sheet Built(Case layout)
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        sheet.SetEntries(Entries(layout));
        return sheet;
    }

    private static List<KeyValuePair<CellAddress, Entry?>> Entries(Case layout)
    {
        var random = new Random(Seed);
        var entries = new List<KeyValuePair<CellAddress, Entry?>>((Rows * 20) + 20);
        void Put(int row, int column, Entry entry) => entries.Add(new(new CellAddress(row, column), entry));
        for (var row = 0; row < Rows; row++)
        {
            var n = (row + 1).ToString(CultureInfo.InvariantCulture);
            Put(row, 0, Entry.FromValue(Value.FromNumber(random.Next(1, 500))));
            Put(row, 1, Entry.FromValue(Value.FromNumber(Math.Round(1 + (random.NextDouble() * 999), 2))));
            for (var column = 2; column < 15; column++)
            {
                Put(row, column, Entry.FromValue(Value.FromNumber(Math.Round(random.NextDouble() * 1000, 2))));
            }
            Put(row, 15, Entry.FromFormula($"=A{n}*B{n}"));
            Put(row, 16, Entry.FromFormula($"=P{n}-C{n}+D{n}*E{n}"));
            Put(row, 17, Entry.FromFormula($"=(Q{n}+F{n})/(1+G{n}/100)"));
            Put(row, 18, Entry.FromFormula(row == 0 ? "=R1" : $"=S{row}+R{n}"));
            Put(row, 19, Entry.FromFormula(layout.ShareOfTotal ? $"=R{n}/SUM(R:R)" : $"=R{n}*H{n}/100-I{n}"));
        }
        for (var column = 0; column < 20; column++)
        {
            var letters = CellAddress.ColumnName(column);
            Put(column, 21, Entry.FromFormula($"=SUM({letters}:{letters})"));
        }
        return entries;
    }

    /// <summary>The Values the case computes, checked against the same arithmetic done here: a time is worth nothing over a wrong answer.</summary>
    private static void Check(Sheet sheet, Case layout)
    {
        double At(int row, int column) => sheet.GetValue(new CellAddress(row, column)) is { Kind: ValueKind.Number } v
            ? v.Number
            : throw new Xunit.Sdk.XunitException($"{new CellAddress(row, column)} is {sheet.GetValue(new CellAddress(row, column))?.ToString() ?? "blank"}, not a number.");
        var running = 0.0;
        for (var row = 0; row < Rows; row++)
        {
            var p = At(row, 0) * At(row, 1);
            var q = p - At(row, 2) + (At(row, 3) * At(row, 4));
            var r = (q + At(row, 5)) / (1 + (At(row, 6) / 100));
            running += r;
            Close(r, At(row, 17), new CellAddress(row, 17));
            Close(running, At(row, 18), new CellAddress(row, 18));
            Close(layout.ShareOfTotal ? r / At(17, 21) : (r * At(row, 7) / 100) - At(row, 8), At(row, 19), new CellAddress(row, 19));
        }
        Close(running, At(17, 21), new CellAddress(17, 21)); // V18, =SUM(R:R)
    }

    // Excel's arithmetic keeps fifteen significant digits, and so may differ from this one's in the last of them.
    private static void Close(double expected, double actual, CellAddress where) =>
        Assert.True(Math.Abs(expected - actual) <= 1e-9 * Math.Max(1, Math.Abs(expected)), $"{where} is {actual}, where {expected} was expected.");

    private static string Describe(Case layout, SheetDocument document)
    {
        var formulas = document.Cells.Count(c => c.Entry is { IsFormula: true });
        var wholeColumns = document.Cells.Count(c => c.Entry?.Formula?.Contains(":", StringComparison.Ordinal) == true);
        var constants = document.Cells.Count(c => c.Entry is { IsFormula: false });
        return string.Create(CultureInfo.InvariantCulture,
            $"{layout.Name}: {Rows:N0} rows by 20 columns, and 20 totals beside them; {formulas:N0} Formulas, {wholeColumns:N0} of them reading a whole column, and {constants:N0} constants (seed {Seed})");
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

    /// <summary>Each of the layout's runs timed, after its runs to warm up.</summary>
    private static Series Time(Action run, Case layout)
    {
        for (var warm = 0; warm < layout.WarmUps; warm++)
        {
            run();
        }
        var series = new Series();
        for (var i = 0; i < layout.Runs; i++)
        {
            series.Time(run);
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
        output.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{what}: median {sorted[sorted.Length / 2]:F1} ms, best {sorted[0]:F1} ms, worst {sorted[^1]:F1} ms (n={sorted.Length}){(note is null ? "" : "; " + note)}; collections by run: {string.Join(' ', series.Collections)}"));
    }
}
