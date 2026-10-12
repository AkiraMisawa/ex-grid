// The evaluation Excel uses for MOD: a search over candidate evaluations, each written once, against
// every MOD value Excel was seen to give (every results-*.json under
// tests/ExSheet.Engine.Tests/ExcelOracle/, which hold every observed MOD case of the corpus). Nothing
// here ships.
//
//   nix develop "git+file://$PWD?shallow=1" -c dotnet run spikes/exsheet-financial-evaluation/mod-search.cs
//   ... -- --discriminate    also searches for inputs where the candidates that fit every observed
//                            value part, groups them into classes, and checks the engine's refusal
//
// mod-search.txt holds the last --discriminate run: every candidate with its match count and the cases
// it misses, the predictions for the cases asked next, and the search.
//
// The area pass of 2026-10-12 read MOD(123456789012,0.7) as 0.5000078322611261 (MOD-017), where the
// engine's doubles evaluation, number - divisor*FLOOR(number/divisor), gives 0.5000152587890625.
// Exact arithmetic is BigInteger rationals, rounded to nearest-even at a chosen significand width:
// 53 bits is a double, 64 bits an x87 register. The C# operators and Math.FusedMultiplyAdd are what
// the engine would compute, so the candidates written with them are run as written. "Cancellation" is
// Excel's final addition as the engine has it from observation (Arithmetic.FinalAdd: 0 where the
// operands are closer than 2^-49): taken against the number, |r| <= |n|*2^-49, or against the number
// and the product, each, as FinalAdd takes its two operands.
//
// What it found after the first area pass (2026-10-12, its second round; the first missed the
// multiply-add with a wrong sign as 0):
// 17 cases observed in 35 reads, 12 of them numbers, the same at each read. 33 candidates; 15
// reproduce all 17. Every form of the identity in doubles (A to A9), with or without a 0 for a wrong
// sign or cancellation, misses MOD-017; so do the x87 registers (D to D5, from its 9th digit), decimal
// (E to E3, MOD-009 too) and the multiply-add over a quotient read at 15 digits (C8, MOD-009). The 15
// that fit fall into seven classes, within which no input searched parts them (3,116,066 inputs: a grid
// of 418, 1,915,648 random ones of up to five digits, 1,000,000 a few units in the last place from a
// multiple of the divisor, and 200,000 far smaller than the divisor, of the other sign):
//   1. the exact remainder rounded once: B, B2, B3, C2, C4;
//   2. the same, rounded first to an x87 register (B4, B5): parts from 1 by double rounding only, at the
//      last bit, where the divisor is added to a remainder of the other sign (MOD(-0.087626,6630));
//   3. a fused multiply-add over the double quotient (C, C3): where the double quotient rounds up onto a
//      whole number the exact one is below, a hair of the wrong sign (MOD(1,0.1) is
//      -5.551115123125783E-17), where 1 gives about the divisor (0.09999999999999995);
//   4. the same with a wrong sign taken as 0 (C5): 0 there;
//   5. that, and cancellation (C6, C7): 0 there, and 0 where the remainder is not 0 but within 2^-49 of
//      the number (MOD(1.1,0.1), where 1 to 4 give 2.7755575615628914E-17);
//   6. the exact remainder with cancellation (B6, B7): about the divisor where 3 to 5 give a hair or 0,
//      and 0 where 5 does;
//   7. the exact remainder, 0 where it rounds to the divisor itself (B8): MOD(-1E-20,1) is 0, where
//      every other class gives 1, the divisor.
// The engine answered where all seven gave the same double and refused with #VALUE! where they parted.
// MOD-018 and MOD-019 parted the exact remainder from the doubles' identity, where every class agreed;
// MOD-020 to MOD-032 parted the classes, each pair of them at one case at least.
//
// The second area pass (2026-10-12, Excel 16.0.20430.20146, results-2026-10-12-area-2-com-en-US.json)
// answered MOD-018 to MOD-032, and NO class of the seven gives all 32 cases now (67 reads, 27 numbers):
// MOD-020 to 028 rule out 3 to 5 (Excel keeps the exact remainder, about the divisor), MOD-030 and 031
// rule out 5 and 6 (no cancellation), MOD-029 rules out every class but 2 (6629.9123739999995, the sum
// rounded at 64 bits and then to a double), and MOD-032 rules out every class but 7 (0, the sum stored as
// the divisor taken as 0). So the search was extended (B9 to B16): the divisor added at 64 bits, rounded
// to nearest as an x87 register adds or truncated as a sum of 64-bit significands would be, or truncated
// to a double, and a sum that is the divisor itself taken as 0 in the double stored or in the register.
// 41 candidates; 3 reproduce all 32:
//   B9   the exact remainder, the divisor added in an x87 register (64 bits, to nearest), stored as a
//        double, and 0 where that double is the divisor;
//   B10  the same, 0 where the register holds the divisor (((n%d)+d)%d with every step in the register);
//   B12  the divisor added truncated at 64 bits, stored as a double to nearest, 0 where it is the divisor.
// A register of 54 to 63 bits, rounded to nearest, would give MOD-029 too (its exact sum lies 15/65536 of
// a unit in the last place past the midpoint), and so would truncation at 54 to 65; no hardware or
// natural software holds those widths, and they are not modelled. The three part in two places (3,616,066 inputs searched: the four searches above, and
// 500,000 whose sum with the divisor lies near the midpoint between two doubles):
//   - the sum is the divisor as a double but not in the register, as at MOD(-1E-17,1): B9 and B12 give 0,
//     B10 1. 43,880 inputs, none of up to five digits; MOD-033;
//   - truncating at 64 bits stores another double than rounding there, the sum within one 64-bit unit of
//     the midpoint between two doubles: MOD(-0.084474,2270) is 2269.915526 by B9 and B10, as rounded once,
//     and 2269.9155259999998 by B12; MOD(-0.008511,81200) is 81199.99148900001 by B9 and B10, which round
//     twice, and 81199.991489 by B12, as rounded once. 86,444 inputs, 13 of the 1,915,648 of up to five
//     digits; MOD-034 to MOD-036.
// The engine answers where the three agree, which is the exact remainder wherever no divisor is added
// and the sum rounded once nearly everywhere else, and refuses with #VALUE! where they part:
// --discriminate checks that it refuses exactly there over every input searched (130,316 of 3,616,066,
// 13 of the 1,915,648 of up to five digits, where the seven classes' refusal took 19,633). MOD-037 and
// MOD-038 ask two answers all three share that an evaluation outside them might not: a sum one unit
// below the divisor, and a number of the divisor's sign far smaller than it.

using System.Globalization;
using System.Numerics;
using System.Text.Json;

var root = FindRoot();
var discriminate = args.Contains("--discriminate");

// The arguments of every MOD case, as Excel reads them (2^27 and the like are exact doubles).
var inputs = new Dictionary<string, (double N, double D)>
{
    ["MOD-001"] = (3, 2), ["MOD-002"] = (-3, 2), ["MOD-003"] = (3, -2), ["MOD-004"] = (-3, -2),
    ["MOD-005"] = (3, 0), ["MOD-006"] = (5.5, 2), ["MOD-007"] = (-7.5, 2), ["MOD-008"] = (Math.Pow(2, 27), 1),
    ["MOD-009"] = (0.3, 0.1), ["MOD-010"] = (Math.Pow(2, 40), 3), ["MOD-011"] = (1125899999999, 1),
    ["MOD-012"] = (1125900000000, 1), ["MOD-013"] = (3377699999997, 3), ["MOD-014"] = (3377700000000, 3),
    ["MOD-015"] = (Math.Pow(2, 53), 3), ["MOD-016"] = (1E+16, 7), ["MOD-017"] = (123456789012, 0.7),
    // The first pass's corrections asked these; the second area pass answered them (2026-10-12).
    ["MOD-018"] = (-123456789012, 0.7), ["MOD-019"] = (123456789012, -0.7), ["MOD-020"] = (1, 0.1),
    ["MOD-021"] = (5, 0.1), ["MOD-022"] = (-2.2, 0.1), ["MOD-023"] = (-1, -0.1), ["MOD-024"] = (3.3, 0.01),
    ["MOD-025"] = (0.7, 0.01), ["MOD-026"] = (100000000000, 0.1), ["MOD-027"] = (123456789012, 0.2),
    ["MOD-028"] = (-693380, -93.7), ["MOD-029"] = (-0.087626, 6630), ["MOD-030"] = (1.1, 0.1),
    ["MOD-031"] = (-0.9, 0.1), ["MOD-032"] = (-1E-20, 1),
};

// Every value Excel gave, from every results file; the same case read twice must read the same.
var observed = new SortedDictionary<string, string>();
var reads = 0;
foreach (var file in Directory.GetFiles(Path.Combine(root, "tests/ExSheet.Engine.Tests/ExcelOracle"), "results-*.json").Order())
{
    using var doc = JsonDocument.Parse(File.ReadAllText(file));
    if (!doc.RootElement.TryGetProperty("cases", out var cases)) continue;
    foreach (var c in cases.EnumerateArray())
    {
        var id = c.GetProperty("id").GetString()!;
        if (!id.StartsWith("MOD-", StringComparison.Ordinal)) continue;
        if (!c.TryGetProperty("excel", out var excel) || excel.ValueKind != JsonValueKind.Object) continue;
        var v = excel.GetProperty("value2");
        // Excel's double is its "number" where the oracle wrote one (G17, from the second area pass on);
        // before that, value2 (R). The second pass found the two name the same double in every case.
        var text = excel.TryGetProperty("number", out var number) && number.ValueKind == JsonValueKind.String
            ? R(double.Parse(number.GetString()!, CultureInfo.InvariantCulture))
            : v.ValueKind == JsonValueKind.Number ? R(v.GetDouble()) : v.GetString()!;
        reads++;
        if (observed.TryGetValue(id, out var before) && before != text) throw new InvalidDataException($"{id} read {before} and {text}");
        observed[id] = text;
    }
}

// The limit is the same for every candidate (MOD-011 to MOD-016): the double quotient against 1,125,900,000,000.
const double Limit = 1_125_900_000_000;
// 2^-49, the tolerance of Excel's final addition (Arithmetic.CancelTolerance).
const double Tolerance = 1.0 / 562949953421312.0;
Func<double, double, string> Guarded(Func<double, double, double> remainder) => (n, d) =>
{
    if (d == 0) return "#DIV/0!";
    if (Math.Abs(n / d) >= Limit) return "#NUM!";
    var r = remainder(n, d);
    return R(r == 0 ? 0 : r);
};

var candidates = new List<(string Code, string Name, Func<double, double, string> Mod)>
{
    // Microsoft's identity in doubles; A is the engine before the area pass.
    ("A", "n - d*FLOOR(n/d), doubles", Guarded((n, d) => n - d * Math.Floor(n / d))),
    ("A2", "n - d*TRUNC(n/d), doubles, then +d where the sign is the number's", Guarded((n, d) => SignFix(n - d * Math.Truncate(n / d), d))),
    ("A3", "n - FLOOR(n/d)*d, doubles, the product the other way", Guarded((n, d) => n - Math.Floor(n / d) * d)),
    ("A4", "n - d*FLOOR(n/d) with the quotient read at 15 digits", Guarded((n, d) => n - d * Math.Floor(Fifteen(n / d)))),
    ("A5", "(n/d - FLOOR(n/d))*d, the fraction of the quotient", Guarded((n, d) => (n / d - Math.Floor(n / d)) * d)),
    ("A6", "n - d*FLOOR(n/d), then +d where the sign is the number's", Guarded((n, d) => SignFix(n - d * Math.Floor(n / d), d))),
    ("A7", "n - d*FLOOR(n/d) as a Formula, its last subtraction Excel's final addition", Guarded((n, d) => FinalAdd(n, -(d * Math.Floor(n / d))))),
    ("A8", "n - d*FLOOR(n/d), doubles, 0 where the sign is not the divisor's", Guarded((n, d) => Zero(n - d * Math.Floor(n / d), d))),
    ("A9", "n - d*FLOOR(n/d), doubles, 0 where the sign is not the divisor's, then cancellation", Guarded((n, d) => Cancel(Zero(n - d * Math.Floor(n / d), d), n))),
    // The exact remainder.
    ("B", "exact remainder (C# %, fmod), +d where its sign is the number's, rounded once", Guarded((n, d) => SignFix(n % d, d))),
    ("B2", "n - d*FLOOR(n/d) exactly, the quotient exact, rounded once", Guarded((n, d) => ExactFloorRemainder(n, d, 53))),
    ("B3", "IEEE remainder (round-to-nearest quotient), +d where its sign is not the divisor's", Guarded((n, d) => SignFix(Math.IEEERemainder(n, d), d))),
    ("B4", "exact remainder, +d in an x87 register, then to a double (FPREM, double rounding)", Guarded((n, d) => SignFixX87(n % d, d))),
    ("B5", "n - d*FLOOR(n/d) exactly, rounded to 64 bits and then to 53", Guarded((n, d) => ToDouble(ExactFloorRemainderRational(n, d, 64)))),
    ("B6", "B, then cancellation against the number (|r| <= |n|*2^-49 is 0)", Guarded((n, d) => Cancel(SignFix(n % d, d), n))),
    ("B7", "n - d*FLOOR(n/d) exactly, 0 where n and d*FLOOR(n/d) are closer than 2^-49 of each (FinalAdd's form)", Guarded((n, d) => ExactFinalAdd(n, d))),
    ("B8", "B, then 0 where it rounds to the divisor itself", Guarded((n, d) => SignFix(n % d, d) == d ? 0 : SignFix(n % d, d))),
    // Added after the second area pass (2026-10-12): MOD-029 is B4's double rounding and MOD-032 B8's 0, which no
    // candidate before gave both. The divisor added at 64 bits (an x87 register's, or truncated, as a
    // software sum of 64-bit significands would), or truncated to a double, each with a remainder that
    // reaches the divisor taken as 0: in the register, before it is stored, or in the double stored.
    ("B9", "B4, then 0 where the double is the divisor", Guarded((n, d) => { var r = SignFixX87(n % d, d); return r == d ? 0 : r; })),
    ("B10", "B4, 0 where the x87 register holds the divisor (((n%d)+d)%d, every step in the register)", Guarded((n, d) => SignFixWide(n % d, d, 64, truncate: false, zeroInRegister: true))),
    ("B11", "exact remainder, +d truncated to 64 bits, then rounded to a double", Guarded((n, d) => SignFixWide(n % d, d, 64, truncate: true, zeroInRegister: false))),
    ("B12", "B11, then 0 where the double is the divisor", Guarded((n, d) => { var r = SignFixWide(n % d, d, 64, truncate: true, zeroInRegister: false); return r == d ? 0 : r; })),
    ("B13", "B11, 0 where the 64-bit sum is the divisor", Guarded((n, d) => SignFixWide(n % d, d, 64, truncate: true, zeroInRegister: true))),
    ("B14", "exact remainder, +d truncated to a double (rounded toward zero)", Guarded((n, d) => SignFixWide(n % d, d, 53, truncate: true, zeroInRegister: false))),
    ("B15", "B14, then 0 where the double is the divisor", Guarded((n, d) => { var r = SignFixWide(n % d, d, 53, truncate: true, zeroInRegister: false); return r == d ? 0 : r; })),
    ("B16", "exact remainder, +d at 64 bits toward zero in the register and stored toward zero (an x87 set to chop)", Guarded((n, d) => SignFixWide(n % d, d, 64, truncate: true, zeroInRegister: true, storeTruncated: true))),
    // The quotient in doubles, the multiply-add exact.
    ("C", "FMA(-d, FLOOR(n/d), n)", Guarded((n, d) => Math.FusedMultiplyAdd(-d, Math.Floor(n / d), n))),
    ("C2", "FMA(-d, FLOOR(n/d), n), then +d where the sign is not the divisor's", Guarded((n, d) => SignFix(Math.FusedMultiplyAdd(-d, Math.Floor(n / d), n), d))),
    ("C3", "n - p - e, with p = d*q and e = FMA(d, q, -p) (TwoProduct)", Guarded((n, d) =>
    {
        var q = Math.Floor(n / d);
        var p = d * q;
        var e = Math.FusedMultiplyAdd(d, q, -p);
        return n - p - e;
    })),
    ("C4", "FMA(-d, TRUNC(n/d), n), then +d where the sign is the number's", Guarded((n, d) => SignFix(Math.FusedMultiplyAdd(-d, Math.Truncate(n / d), n), d))),
    ("C5", "FMA(-d, FLOOR(n/d), n), 0 where the sign is not the divisor's", Guarded((n, d) => Zero(Math.FusedMultiplyAdd(-d, Math.Floor(n / d), n), d))),
    ("C6", "FMA(-d, FLOOR(n/d), n), 0 where the sign is not the divisor's, then cancellation", Guarded((n, d) => Cancel(Zero(Math.FusedMultiplyAdd(-d, Math.Floor(n / d), n), d), n))),
    ("C7", "FMA(-d, FLOOR(n/d), n), then cancellation", Guarded((n, d) => Cancel(Math.FusedMultiplyAdd(-d, Math.Floor(n / d), n), n))),
    ("C8", "FMA(-d, FLOOR(n/d), n) with the quotient read at 15 digits", Guarded((n, d) => Math.FusedMultiplyAdd(-d, Math.Floor(Fifteen(n / d)), n))),
    // x87 80-bit intermediates.
    ("D", "n - d*FLOOR(n/d), every step in an x87 register, then to a double", Guarded((n, d) => X87Identity(n, d, quotientX87: true))),
    ("D2", "the quotient in doubles, the product and difference in an x87 register", Guarded((n, d) => X87Identity(n, d, quotientX87: false))),
    ("D3", "D2, 0 where the sign is not the divisor's, then cancellation", Guarded((n, d) => Cancel(Zero(X87Identity(n, d, quotientX87: false), d), n))),
    ("D4", "D, then +d where the sign is not the divisor's, in an x87 register", Guarded((n, d) => X87IdentitySignFixed(n, d))),
    ("D5", "D, then cancellation", Guarded((n, d) => Cancel(X87Identity(n, d, quotientX87: true), n))),
    // Decimal.
    ("E", "decimal: (decimal)n % (decimal)d, +d where the sign is the number's", Guarded((n, d) =>
    {
        try
        {
            var dn = (decimal)n;
            var dd = (decimal)d;
            var r = dn % dd;
            if (r != 0 && (r < 0) != (dd < 0)) r += dd;
            return (double)r;
        }
        catch (OverflowException) { return double.NaN; }
    })),
    ("E2", "decimal, then cancellation", Guarded((n, d) =>
    {
        try
        {
            var dn = (decimal)n;
            var dd = (decimal)d;
            var r = dn % dd;
            if (r != 0 && (r < 0) != (dd < 0)) r += dd;
            return Cancel((double)r, n);
        }
        catch (OverflowException) { return double.NaN; }
    })),
    ("E3", "decimal over the operands read at 15 digits, +d where the sign is the number's", Guarded((n, d) =>
    {
        try
        {
            var dn = (decimal)Fifteen(n);
            var dd = (decimal)Fifteen(d);
            var r = dn % dd;
            if (r != 0 && (r < 0) != (dd < 0)) r += dd;
            return (double)r;
        }
        catch (OverflowException) { return double.NaN; }
    })),
};

Console.WriteLine($"MOD: {observed.Count} cases observed ({reads} reads), {observed.Values.Count(v => !v.StartsWith('#'))} of them numbers; {candidates.Count} candidates.");
Console.WriteLine();
var fits = new List<(string Code, string Name, Func<double, double, string> Mod)>();
foreach (var (code, name, mod) in candidates)
{
    var hits = 0;
    var misses = new List<string>();
    foreach (var (id, excel) in observed)
    {
        var (n, d) = inputs[id];
        var got = mod(n, d);
        if (got == excel) hits++;
        else misses.Add($"{id} gives {got}, Excel {excel}");
    }
    Console.WriteLine($"{hits,2}/{observed.Count}  {code,-3} {name}");
    foreach (var miss in misses) Console.WriteLine($"         {miss}");
    if (misses.Count == 0) fits.Add((code, name, mod));
}

Console.WriteLine();
Console.WriteLine($"Fit every observed value: {fits.Count} of {candidates.Count}: {string.Join(", ", fits.Select(f => f.Code))}.");

// The cases added for the next pass (after the second area pass): those that part the three that fit,
// each pair at one case at least, and two where all three agree, which an evaluation outside them
// (a remainder near the divisor taken as 0, a number far smaller than the divisor taken as 0) would not.
var asked = new (string Id, string Formula, double N, double D)[]
{
    ("MOD-033", "=MOD(-1E-17,1)", -1E-17, 1),
    ("MOD-034", "=MOD(-0.084474,2270)", -0.084474, 2270),
    ("MOD-035", "=MOD(-0.008511,81200)", -0.008511, 81200),
    ("MOD-036", "=MOD(0.071472,-276)", 0.071472, -276),
    ("MOD-037", "=MOD(-1E-16,1)", -1E-16, 1),
    ("MOD-038", "=MOD(1E-20,1)", 1E-20, 1),
};
Console.WriteLine();
Console.WriteLine("Predictions for the new cases, the candidates grouped by the double they give (* fits every observed value), and the engine's answer:");
foreach (var (id, formula, n, d) in asked)
{
    Console.WriteLine($"  {id} {formula}   engine: {EngineMod(n, d)}");
    foreach (var group in candidates.GroupBy(c => c.Mod(n, d)))
    {
        var names = string.Join(", ", group.Select(c => (fits.Any(f => f.Code == c.Code) ? "*" : "") + c.Code));
        Console.WriteLine($"    {group.Key,-26} {names}");
    }
}

if (discriminate)
{
    // Inputs that part the candidates that fit: a grid of everyday numbers, then random searches
    // (seeded, so the run repeats), counted by which candidates part.
    var parted = new SortedDictionary<string, (int Count, string First)>();
    // Everyday inputs (the grid's, and those of up to five digits) where each pair parts, a few of each.
    var everyday = new SortedDictionary<string, List<string>>();
    var collecting = true;
    var engineWrong = new List<string>();
    var engineWrongCount = 0;
    var searched = 0;
    var refused = 0;
    var why = new SortedDictionary<string, int>();
    var totalSearched = 0;
    void Try(double n, double d)
    {
        if (d == 0 || !double.IsFinite(n) || !double.IsFinite(d) || Math.Abs(n / d) >= Limit) return;
        searched++;
        if (EngineWhy(n, d) is { } reason) why[reason] = why.GetValueOrDefault(reason) + 1;
        var answers = fits.Select(f => f.Mod(n, d)).ToArray();
        for (var a = 0; a < fits.Count; a++)
        for (var b = a + 1; b < fits.Count; b++)
        {
            if (answers[a] == answers[b]) continue;
            var key = $"{fits[a].Code}/{fits[b].Code}";
            parted[key] = parted.TryGetValue(key, out var seen)
                ? (seen.Count + 1, seen.First)
                : (1, $"=MOD({R(n)},{R(d)}): {answers[a]} / {answers[b]}");
            if (!collecting) continue;
            if (!everyday.TryGetValue(key, out var list)) everyday[key] = list = [];
            if (list.Count < 12) list.Add($"=MOD({R(n)},{R(d)}): {string.Join(" | ", fits.Select((f, i) => $"{f.Code} {answers[i]}"))}");
        }
        var agreed = answers.Distinct().Count() == 1 ? answers[0] : "#VALUE!";
        var engine = EngineMod(n, d);
        if (engine == "#VALUE!") refused++;
        if (engine == agreed) return;
        if (engineWrongCount++ < 20) engineWrong.Add($"=MOD({R(n)},{R(d)}): engine {engine}, candidates {agreed} ({string.Join(" ", answers.Distinct())})");
    }

    Console.WriteLine();
    Console.WriteLine("Inputs where the candidates that fit every observed value part (the grid):");
    var numbers = new[] { 0.5, 1, 2.2, 5, 10, 100, 0.3, 1.1, 4.2, 7, 1000, 123456.789, 98765432.1, 123456789012, 1E+11, 3.3, 0.7, 1E-300 };
    var divisors = new[] { 0.1, 0.2, 0.3, 0.7, 1.1, 0.01, 0.06, 1.0 / 3, 0.05, 2.2, 0.15, 7 };
    var found = 0;
    foreach (var n0 in numbers)
    foreach (var sign in new[] { 1.0, -1.0 })
    foreach (var d in divisors)
    {
        var n = sign * n0;
        Try(n, d);
        if (Parts(n, d) is not { } line) continue;
        found++;
        Console.WriteLine($"  {line}");
    }
    Console.WriteLine($"  {found} inputs of {numbers.Length * 2 * divisors.Length} part them.");
    Console.WriteLine();
    Console.WriteLine("What the engine refuses in each search, and why:");
    Report("the grid");

    var random = new Random(20261012);
    double Divisor() => (random.Next(4) == 0 ? -1 : 1) * random.Next(1, 1000) * Math.Pow(10, random.Next(-4, 3));
    for (var i = 0; i < 2_000_000; i++)
    {
        // Numbers and divisors of up to five and three digits.
        Try((random.Next(2) == 0 ? 1 : -1) * random.Next(1, 100000) * Math.Pow(10, random.Next(-6, 9)), Divisor());
    }
    Report("2,000,000 numbers of up to five digits");
    collecting = false;
    for (var i = 0; i < 1_000_000; i++)
    {
        // A number a few units in the last place from a multiple of the divisor: the double quotient
        // rounding onto a whole number, a remainder that nearly cancels, a remainder of the other sign.
        var d = Divisor();
        var n = (random.Next(2) == 0 ? 1 : -1) * random.Next(1, 1_000_000) * Math.Abs(d);
        for (var steps = random.Next(-3, 4); steps != 0; steps -= Math.Sign(steps)) n = steps > 0 ? Math.BitIncrement(n) : Math.BitDecrement(n);
        Try(n, d);
    }
    Report("1,000,000 near a multiple of the divisor");
    for (var i = 0; i < 200_000; i++)
    {
        // A number far smaller than the divisor, of the other sign: the remainder plus the divisor rounds to it.
        var d = Divisor();
        Try(-Math.Sign(d) * random.Next(1, 100000) * Math.Pow(10, random.Next(-25, -10)), d);
    }
    Report("200,000 far smaller than the divisor");
    for (var i = 0; i < 500_000; i++)
    {
        // The divisor plus a number of the other sign lying a hair from the midpoint between two doubles,
        // where rounding at 64 bits first, or truncating there, moves the double the sum is stored as: a
        // target t below the divisor, and a number that puts the sum within about half a 64-bit unit of
        // the midpoint above t, nudged a few units in its last place either way.
        var d = Divisor();
        var ad = Math.Abs(d);
        var t = ad * (1 - random.Next(1, 1 << 20) * Math.Pow(2, -random.Next(32, 60)));
        if (!(t < ad)) continue;
        var n = Math.Sign(d) * ((t - ad) + (Math.BitIncrement(t) - t) / 2);
        for (var steps = random.Next(-3, 4); steps != 0; steps -= Math.Sign(steps)) n = steps > 0 ? Math.BitIncrement(n) : Math.BitDecrement(n);
        Try(n, d);
    }
    Report("500,000 whose sum with the divisor lies near a midpoint");

    void Report(string search)
    {
        var total = why.Values.Sum();
        Console.WriteLine($"  {search}: {searched} answered or refused, {total} refused ({string.Join(", ", why.Select(w => $"{w.Key} {w.Value}"))})");
        totalSearched += searched;
        searched = 0;
        why.Clear();
    }

    Console.WriteLine();
    Console.WriteLine($"Every input searched ({totalSearched}): the pairs of fitting candidates that part, how often, and the first input.");
    foreach (var (key, (count, first)) in parted) Console.WriteLine($"  {key,-8} {count,8}  {first}");
    Console.WriteLine();
    Console.WriteLine("Everyday inputs where each pair parts (the grid, and numbers of up to five digits), the first few:");
    foreach (var (key, list) in everyday)
    {
        Console.WriteLine($"  {key}");
        foreach (var line in list) Console.WriteLine($"    {line}");
    }

    // The classes: candidates that never part.
    var classOf = fits.Select((_, i) => i).ToArray();
    int Find(int i) => classOf[i] == i ? i : classOf[i] = Find(classOf[i]);
    for (var a = 0; a < fits.Count; a++)
    for (var b = a + 1; b < fits.Count; b++)
        if (!parted.ContainsKey($"{fits[a].Code}/{fits[b].Code}")) classOf[Find(b)] = Find(a);
    var classes = fits.Select((f, i) => (f, i)).GroupBy(x => Find(x.i)).Select(g => g.Select(x => x.f).ToList()).ToList();
    Console.WriteLine();
    Console.WriteLine($"Classes, the candidates that fit and never part: {classes.Count}.");
    foreach (var members in classes) Console.WriteLine($"  {string.Join(", ", members.Select(m => m.Code))}   ({members[0].Name})");

    // Each pair of classes must part at one asked case at least, so the next pass tells them apart.
    Console.WriteLine();
    Console.WriteLine("Each pair of classes, and the asked cases that part it:");
    var unseparated = 0;
    for (var a = 0; a < classes.Count; a++)
    for (var b = a + 1; b < classes.Count; b++)
    {
        var at = asked.Where(c => classes[a][0].Mod(c.N, c.D) != classes[b][0].Mod(c.N, c.D)).Select(c => c.Id).ToList();
        if (at.Count == 0) unseparated++;
        Console.WriteLine($"  {classes[a][0].Code,-3} / {classes[b][0].Code,-3}  {(at.Count == 0 ? "NONE" : string.Join(", ", at))}");
    }
    Console.WriteLine(unseparated == 0 ? "  Every pair parts at an asked case." : $"  {unseparated} pairs part at no asked case.");

    Console.WriteLine();
    Console.WriteLine($"The engine (Functions.Math.cs, as EngineMod below): refuses {refused} of {totalSearched}; answers otherwise as every candidate that fits does.");
    Console.WriteLine(engineWrongCount == 0
        ? "  It refuses exactly where they part."
        : $"  {engineWrongCount} inputs where it does not, the first:\n    {string.Join("\n    ", engineWrong)}");
}

string? Parts(double n, double d)
{
    var answers = fits.Select(f => f.Mod(n, d)).ToArray();
    var distinct = answers.Distinct().Count();
    if (distinct < 2) return null;
    var groups = answers.Select((a, i) => (a, fits[i].Code)).GroupBy(x => x.a).Select(g => $"{string.Join(",", g.Select(x => x.Code))} {g.Key}");
    return $"=MOD({R(n)},{R(d)})  {distinct} answers: {string.Join(" | ", groups)} (A gives {candidates[0].Mod(n, d)})";
}

// The engine's MOD, as src/ExSheet.Engine/Formulas/Functions.Math.cs writes it (after the second area
// pass): the exact remainder where it has the divisor's sign; where it has the number's, the divisor
// added as B9, B10 and B12 add it, and #VALUE! where they part.
static string EngineMod(double number, double divisor)
{
    if (divisor == 0) return "#DIV/0!";
    if (Math.Abs(number / divisor) >= Limit) return "#NUM!";
    var remainder = number % divisor;
    if (remainder == 0 || (remainder < 0) == (divisor < 0)) return R(remainder == 0 ? 0 : remainder);
    var (afterStore, inRegister, truncated) = EngineTurned(remainder, divisor);
    return afterStore == inRegister && afterStore == truncated ? R(afterStore == 0 ? 0 : afterStore) : "#VALUE!";
}

// Why the engine refuses: which of the three readings part; null where it answers.
static string? EngineWhy(double number, double divisor)
{
    var remainder = number % divisor;
    if (remainder == 0 || (remainder < 0) == (divisor < 0)) return null;
    var (afterStore, inRegister, truncated) = EngineTurned(remainder, divisor);
    if (afterStore != inRegister) return "the divisor itself, as a double but not in the register";
    if (afterStore != truncated) return "truncated at 64 bits, another double";
    return null;
}

// remainder + divisor as the three readings take it, with a positive divisor and then the sign put back.
static (double AfterStore, double InRegister, double Truncated) EngineTurned(double remainder, double divisor)
{
    var sign = divisor < 0 ? -1.0 : 1.0;
    var d = sign * divisor;
    var r = sign * remainder;
    var sum = r + d;
    var error = r - (sum - d);
    var gap = error < 0 ? sum - Math.BitDecrement(sum) : Math.BitIncrement(sum) - sum;
    var steps = error / gap * 2048;
    var nearest = Math.Round(steps, MidpointRounding.ToEven);
    var x87 = Stored(sum, nearest);
    var chopped = Stored(sum, Math.Floor(steps));
    return (sign * (x87 == d ? 0 : x87), sign * (sum == d && nearest == 0 ? 0 : x87), sign * (chopped == d ? 0 : chopped));
}

static double Stored(double sum, double steps) =>
    Math.Abs(steps) < 1024 || (BitConverter.DoubleToInt64Bits(sum) & 1) == 0 ? sum
    : steps > 0 ? Math.BitIncrement(sum) : Math.BitDecrement(sum);

static string R(double x) => x.ToString("R", CultureInfo.InvariantCulture);

static double Fifteen(double x) => double.Parse(x.ToString("E14", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

// +d where a remainder that is not 0 has the sign opposite the divisor's (fmod's carries the number's).
static double SignFix(double r, double d) => r != 0 && (r < 0) != (d < 0) ? r + d : r;

// 0 where a remainder that is not 0 has the sign opposite the divisor's.
static double Zero(double r, double d) => r != 0 && (r < 0) != (d < 0) ? 0 : r;

// Excel's cancellation, taken against the number: a remainder within 2^-49 of it is 0.
static double Cancel(double r, double n) => r != 0 && Math.Abs(r) <= Math.Abs(n) * Tolerance ? 0 : r;

// Arithmetic.FinalAdd: 0 where the operands have opposite signs and are closer than 2^-49 of each.
static double FinalAdd(double a, double b)
{
    var opposite = (a > 0 && b < 0) || (a < 0 && b > 0);
    if (!opposite) return a + b;
    var difference = Math.Abs(a + b);
    return difference < Math.Abs(a) * Tolerance && difference < Math.Abs(b) * Tolerance ? 0 : a + b;
}

static double ExactFinalAdd(double n, double d)
{
    var qn = Q.Of(n);
    var qd = Q.Of(d);
    var product = qd * new Q(Q.Floor(qn / qd), 1);
    var r = qn - product;
    var t = Q.Of(Tolerance);
    return !r.Num.IsZero && Q.Abs(r) < Q.Abs(qn) * t && Q.Abs(r) < Q.Abs(product) * t ? 0 : ToDouble(r);
}

// +d where the remainder's sign is the number's, the sum held at `bits` (rounded to nearest, or
// truncated) and then stored to a double (rounded to nearest, or truncated); a held sum that is the
// divisor itself taken as 0 first where zeroInRegister.
static double SignFixWide(double r, double d, int bits, bool truncate, bool zeroInRegister, bool storeTruncated = false)
{
    if (r == 0 || (r < 0) == (d < 0)) return r;
    var held = truncate ? Truncate(Q.Of(r) + Q.Of(d), bits) : Round(Q.Of(r) + Q.Of(d), bits);
    if (zeroInRegister && (held - Q.Of(d)).Num.IsZero) return 0;
    return storeTruncated ? ToDouble(Truncate(held, 53)) : ToDouble(held);
}

static double SignFixX87(double r, double d)
{
    if (r == 0 || (r < 0) == (d < 0)) return r;
    return ToDouble(Round(Q.Of(r) + Q.Of(d), 64));
}

static double ExactFloorRemainder(double n, double d, int bits) => ToDouble(ExactFloorRemainderRational(n, d, bits));

static Q ExactFloorRemainderRational(double n, double d, int bits)
{
    var qn = Q.Of(n);
    var qd = Q.Of(d);
    var quotient = Q.Floor(qn / qd);
    return Round(qn - qd * new Q(quotient, 1), bits);
}

static Q X87IdentityRational(double n, double d, bool quotientX87)
{
    var qn = Q.Of(n);
    var qd = Q.Of(d);
    var quotient = quotientX87 ? Round(qn / qd, 64) : Q.Of(n / d);
    var floor = new Q(Q.Floor(quotient), 1);
    var product = Round(qd * floor, 64);
    return Round(qn - product, 64);
}

static double X87Identity(double n, double d, bool quotientX87) => ToDouble(X87IdentityRational(n, d, quotientX87));

static double X87IdentitySignFixed(double n, double d)
{
    var r = X87IdentityRational(n, d, quotientX87: true);
    if (!r.Num.IsZero && (r.Num.Sign < 0) != (d < 0)) r = Round(r + Q.Of(d), 64);
    return ToDouble(r);
}

static double ToDouble(Q x)
{
    var r = Round(x, 53);
    if (r.Num.IsZero) return 0;
    // r = m * 2^e with |m| < 2^53, exactly.
    var den = r.Den;
    var e = 0;
    var num = r.Num;
    while (!den.IsOne) { den >>= 1; e--; }
    while (BigInteger.Abs(num) >= (BigInteger.One << 53)) { num >>= 1; e++; } // exact: the rounding left trailing zeros
    return Math.ScaleB((double)num, e);
}

static Q Round(Q x, int bits)
{
    if (x.Num.IsZero) return x;
    var negative = x.Num.Sign < 0;
    var num = BigInteger.Abs(x.Num);
    var den = x.Den;
    // Find e with 2^(bits-1) <= x / 2^e < 2^bits.
    var e = (int)(num.GetBitLength() - den.GetBitLength()) - bits;
    while (true)
    {
        var (sn, sd) = e >= 0 ? (num, den << e) : (num << -e, den);
        if (sn < (sd << (bits - 1))) { e--; continue; }
        if (sn >= (sd << bits)) { e++; continue; }
        var m = BigInteger.DivRem(sn, sd, out var rem);
        var twice = rem * 2;
        if (twice > sd || (twice == sd && !m.IsEven)) m += 1;
        var scaled = e >= 0 ? new Q(m << e, 1) : new Q(m, BigInteger.One << -e);
        return negative ? new Q(-scaled.Num, scaled.Den) : scaled;
    }
}

// Rounded toward zero at a significand of `bits`.
static Q Truncate(Q x, int bits)
{
    if (x.Num.IsZero) return x;
    var negative = x.Num.Sign < 0;
    var num = BigInteger.Abs(x.Num);
    var den = x.Den;
    var e = (int)(num.GetBitLength() - den.GetBitLength()) - bits;
    while (true)
    {
        var (sn, sd) = e >= 0 ? (num, den << e) : (num << -e, den);
        if (sn < (sd << (bits - 1))) { e--; continue; }
        if (sn >= (sd << bits)) { e++; continue; }
        var m = BigInteger.Divide(sn, sd);
        var scaled = e >= 0 ? new Q(m << e, 1) : Q.Make(m, BigInteger.One << -e);
        return negative ? new Q(-scaled.Num, scaled.Den) : scaled;
    }
}

static string FindRoot()
{
    var dir = Directory.GetCurrentDirectory();
    while (!File.Exists(Path.Combine(dir, "ExGrid.slnx"))) dir = Path.GetDirectoryName(dir) ?? throw new InvalidOperationException("Run from the repository.");
    return dir;
}

readonly record struct Q(BigInteger Num, BigInteger Den)
{
    public static Q Of(double x)
    {
        if (x == 0) return new Q(0, 1);
        var bits = BitConverter.DoubleToInt64Bits(x);
        var exponent = (int)((bits >> 52) & 0x7FF);
        var mantissa = bits & 0xFFFFFFFFFFFFFL;
        if (exponent == 0) exponent++; else mantissa |= 1L << 52;
        exponent -= 1075;
        BigInteger m = mantissa;
        if (x < 0) m = -m;
        return exponent >= 0 ? new Q(m << exponent, 1) : Make(m, BigInteger.One << -exponent);
    }

    public static Q Make(BigInteger num, BigInteger den)
    {
        if (den.Sign < 0) { num = -num; den = -den; }
        var g = BigInteger.GreatestCommonDivisor(num, den);
        return g.IsOne || g.IsZero ? new Q(num, den) : new Q(num / g, den / g);
    }

    public static BigInteger Floor(Q x)
    {
        var q = BigInteger.DivRem(x.Num, x.Den, out var r);
        return r.Sign < 0 ? q - 1 : q;
    }

    public static Q Abs(Q x) => new(BigInteger.Abs(x.Num), x.Den);

    public static Q operator +(Q a, Q b) => Make(a.Num * b.Den + b.Num * a.Den, a.Den * b.Den);
    public static Q operator -(Q a, Q b) => Make(a.Num * b.Den - b.Num * a.Den, a.Den * b.Den);
    public static Q operator *(Q a, Q b) => Make(a.Num * b.Num, a.Den * b.Den);
    public static Q operator /(Q a, Q b) => Make(a.Num * b.Den, a.Den * b.Num);

    public static bool operator <(Q a, Q b) => a.Num * b.Den < b.Num * a.Den;
    public static bool operator >(Q a, Q b) => a.Num * b.Den > b.Num * a.Den;
}
