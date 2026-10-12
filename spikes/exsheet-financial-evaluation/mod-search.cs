// The evaluation Excel uses for MOD: a search over candidate evaluations, each written once, against
// every MOD value Excel was seen to give (every results-*.json under
// tests/ExSheet.Engine.Tests/ExcelOracle/, which hold every observed MOD case of the corpus). Nothing
// here ships.
//
//   nix develop "git+file://$PWD?shallow=1" -c dotnet run spikes/exsheet-financial-evaluation/mod-search.cs
//   ... -- --discriminate    also lists inputs where the candidates that fit every observed value part
//
// The area pass of 2026-10-12 read MOD(123456789012,0.7) as 0.5000078322611261 (MOD-017), where the
// engine's doubles evaluation, number - divisor*FLOOR(number/divisor), gives 0.5000152587890625.
// Exact arithmetic is BigInteger rationals, rounded to nearest-even at a chosen significand width:
// 53 bits is a double, 64 bits an x87 register. The C# operators and Math.FusedMultiplyAdd are what
// the engine would compute, so the candidates written with them are run as written.
//
// What it found (2026-10-12): 17 cases observed in 35 reads, 12 of them numbers, the same at each read.
// 18 candidates; 9 reproduce all 17. The doubles' identity (A) and its five variants miss MOD-017 (A4
// MOD-009 too, A5 MOD-009 and MOD-010); x87 registers (D, D2) miss MOD-017 from its 9th digit; decimal
// (E) misses MOD-009 and MOD-017. The 9 that fit fall into three classes, which no other input parts (a grid of 432
// inputs, and 2,000,000 random ones of up to five digits):
//   - the exact remainder rounded once: B, B2, B3, C2 and C4, which the engine now computes (B);
//   - the same, rounded to an x87 register first (B4, B5): it parts from B only by double rounding,
//     8 times in 2,000,000, the first =MOD(-0.087626,6630) (MOD-029);
//   - a fused multiply-add over the double quotient with no correction of the sign (C, C3): where the
//     double quotient rounds up to a whole number the exact one is below, it gives a small number of
//     the wrong sign, 8,857 times in 2,000,000 (MOD-020 to MOD-028).
// MOD-018 and MOD-019 part the exact remainder from the doubles' identity, which every class agrees on.

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
        var text = v.ValueKind == JsonValueKind.Number ? R(v.GetDouble()) : v.GetString()!;
        reads++;
        if (observed.TryGetValue(id, out var before) && before != text) throw new InvalidDataException($"{id} read {before} and {text}");
        observed[id] = text;
    }
}

// The limit is the same for every candidate (MOD-011 to MOD-016): the double quotient against 1,125,900,000,000.
const double Limit = 1_125_900_000_000;
Func<double, double, string> Guarded(Func<double, double, double> remainder) => (n, d) =>
{
    if (d == 0) return "#DIV/0!";
    if (Math.Abs(n / d) >= Limit) return "#NUM!";
    var r = remainder(n, d);
    return R(r == 0 ? 0 : r);
};

var candidates = new List<(string Name, Func<double, double, string> Mod)>
{
    // The engine before this change: Microsoft's identity in doubles.
    ("A  n - d*FLOOR(n/d), doubles", Guarded((n, d) => n - d * Math.Floor(n / d))),
    ("A2 n - d*TRUNC(n/d), doubles, then +d where the sign is the number's", Guarded((n, d) => SignFix(n - d * Math.Truncate(n / d), d))),
    ("A3 n - FLOOR(n/d)*d, doubles, the product the other way", Guarded((n, d) => n - Math.Floor(n / d) * d)),
    ("A4 n - d*FLOOR(n/d) with the quotient read at 15 digits", Guarded((n, d) => n - d * Math.Floor(Fifteen(n / d)))),
    ("A5 (n/d - FLOOR(n/d))*d, the fraction of the quotient", Guarded((n, d) => (n / d - Math.Floor(n / d)) * d)),
    ("A6 n - d*FLOOR(n/d), then +d where the sign is the number's", Guarded((n, d) => SignFix(n - d * Math.Floor(n / d), d))),
    // The exact remainder.
    ("B  exact remainder (C# %, fmod), +d where its sign is the number's, rounded once", Guarded((n, d) => SignFix(n % d, d))),
    ("B2 n - d*FLOOR(n/d) exactly, the quotient exact, rounded once", Guarded((n, d) => ExactFloorRemainder(n, d, 53))),
    ("B3 IEEE remainder (round-to-nearest quotient), +d where its sign is not the divisor's", Guarded((n, d) => SignFix(Math.IEEERemainder(n, d), d))),
    ("B4 exact remainder, +d in an x87 register, then to a double (FPREM, double rounding)", Guarded((n, d) => SignFixX87(n % d, d))),
    ("B5 n - d*FLOOR(n/d) exactly, rounded to 64 bits and then to 53", Guarded((n, d) => ToDouble(ExactFloorRemainderRational(n, d, 64)))),
    // The quotient in doubles, the multiply-add exact.
    ("C  FMA(-d, FLOOR(n/d), n)", Guarded((n, d) => Math.FusedMultiplyAdd(-d, Math.Floor(n / d), n))),
    ("C2 FMA(-d, FLOOR(n/d), n), then +d where the sign is not the divisor's", Guarded((n, d) => SignFix(Math.FusedMultiplyAdd(-d, Math.Floor(n / d), n), d))),
    ("C3 n - p - e, with p = d*q and e = FMA(d, q, -p) (TwoProduct)", Guarded((n, d) =>
    {
        var q = Math.Floor(n / d);
        var p = d * q;
        var e = Math.FusedMultiplyAdd(d, q, -p);
        return n - p - e;
    })),
    ("C4 FMA(-d, TRUNC(n/d), n), then +d where the sign is the number's", Guarded((n, d) => SignFix(Math.FusedMultiplyAdd(-d, Math.Truncate(n / d), n), d))),
    // x87 80-bit intermediates.
    ("D  n - d*FLOOR(n/d), every step in an x87 register, then to a double", Guarded((n, d) => X87Identity(n, d, quotientX87: true))),
    ("D2 the quotient in doubles, the product and difference in an x87 register", Guarded((n, d) => X87Identity(n, d, quotientX87: false))),
    // Decimal.
    ("E  decimal: (decimal)n % (decimal)d, +d where the sign is the number's", Guarded((n, d) =>
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
};

Console.WriteLine($"MOD: {observed.Count} cases observed ({reads} reads), {observed.Values.Count(v => !v.StartsWith('#'))} of them numbers; {candidates.Count} candidates.");
Console.WriteLine();
var fits = new List<(string Name, Func<double, double, string> Mod)>();
foreach (var (name, mod) in candidates)
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
    Console.WriteLine($"{hits,2}/{observed.Count}  {name}");
    foreach (var miss in misses) Console.WriteLine($"         {miss}");
    if (misses.Count == 0) fits.Add((name, mod));
}

Console.WriteLine();
Console.WriteLine($"Fit every observed value: {fits.Count} of {candidates.Count}.");
foreach (var (name, _) in fits) Console.WriteLine($"  {name}");

// The cases added for the next pass: the two the triage named, where the doubles' evaluation and the
// exact remainder part, and those that part the candidates that fit (MOD-018 to MOD-029).
var asked = new (string Id, string Formula, double N, double D)[]
{
    ("MOD-018", "=MOD(-123456789012,0.7)", -123456789012, 0.7),
    ("MOD-019", "=MOD(123456789012,-0.7)", 123456789012, -0.7),
    ("MOD-020", "=MOD(1,0.1)", 1, 0.1),
    ("MOD-021", "=MOD(5,0.1)", 5, 0.1),
    ("MOD-022", "=MOD(-2.2,0.1)", -2.2, 0.1),
    ("MOD-023", "=MOD(-1,-0.1)", -1, -0.1),
    ("MOD-024", "=MOD(3.3,0.01)", 3.3, 0.01),
    ("MOD-025", "=MOD(0.7,0.01)", 0.7, 0.01),
    ("MOD-026", "=MOD(100000000000,0.1)", 100000000000, 0.1),
    ("MOD-027", "=MOD(123456789012,0.2)", 123456789012, 0.2),
    ("MOD-028", "=MOD(-693380,-93.7)", -693380, -93.7),
    ("MOD-029", "=MOD(-0.087626,6630)", -0.087626, 6630),
};
Console.WriteLine();
Console.WriteLine("Predictions for the new cases, the candidates grouped by the double they give (* fits every observed value):");
foreach (var (id, formula, n, d) in asked)
{
    Console.WriteLine($"  {id} {formula}");
    foreach (var group in candidates.GroupBy(c => c.Mod(n, d)))
    {
        var names = string.Join(", ", group.Select(c => (fits.Any(f => f.Name == c.Name) ? "*" : "") + c.Name[..2].Trim()));
        Console.WriteLine($"    {group.Key,-26} {names}");
    }
}

if (discriminate)
{
    // Inputs that part the candidates that fit: a grid of everyday numbers, then a random search over
    // numbers of a few decimal digits (seeded, so the run repeats), counted by which candidates part.
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
        if (Parts(n, d) is not { } line) continue;
        found++;
        Console.WriteLine($"  {line}");
    }
    Console.WriteLine($"  {found} inputs of {numbers.Length * 2 * divisors.Length} part them.");

    Console.WriteLine();
    var random = new Random(20261012);
    var tries = 2_000_000;
    var pairs = new SortedDictionary<string, (int Count, string First)>();
    for (var i = 0; i < tries; i++)
    {
        var n = (random.Next(2) == 0 ? 1 : -1) * random.Next(1, 100000) * Math.Pow(10, random.Next(-6, 9));
        var d = (random.Next(4) == 0 ? -1 : 1) * random.Next(1, 1000) * Math.Pow(10, random.Next(-4, 3));
        if (d == 0 || Math.Abs(n / d) >= Limit) continue;
        var answers = fits.Select(f => f.Mod(n, d)).ToArray();
        for (var a = 0; a < fits.Count; a++)
        for (var b = a + 1; b < fits.Count; b++)
        {
            if (answers[a] == answers[b]) continue;
            var key = $"{fits[a].Name[..2].Trim()} / {fits[b].Name[..2].Trim()}";
            pairs[key] = pairs.TryGetValue(key, out var seen)
                ? (seen.Count + 1, seen.First)
                : (1, $"=MOD({R(n)},{R(d)}): {answers[a]} / {answers[b]}");
        }
    }
    Console.WriteLine($"A random search, {tries} inputs of up to five digits: the pairs of fitting candidates that part, how often, and the first input.");
    foreach (var (key, (count, first)) in pairs) Console.WriteLine($"  {key,-8} {count,7}  {first}");
    var never = new List<string>();
    for (var a = 0; a < fits.Count; a++)
    for (var b = a + 1; b < fits.Count; b++)
    {
        var key = $"{fits[a].Name[..2].Trim()} / {fits[b].Name[..2].Trim()}";
        if (!pairs.ContainsKey(key)) never.Add(key);
    }
    Console.WriteLine($"  Never parted: {string.Join(", ", never)}");
}

string? Parts(double n, double d)
{
    var answers = fits.Select(f => f.Mod(n, d)).ToArray();
    var distinct = answers.Distinct().Count();
    if (distinct < 2) return null;
    var groups = answers.Select((a, i) => (a, fits[i].Name[..2].Trim())).GroupBy(x => x.a).Select(g => $"{string.Join(",", g.Select(x => x.Item2))} {g.Key}");
    return $"=MOD({R(n)},{R(d)})  {distinct} answers: {string.Join(" | ", groups)} (A gives {candidates[0].Mod(n, d)})";
}

static string R(double x) => x.ToString("R", CultureInfo.InvariantCulture);

static double Fifteen(double x) => double.Parse(x.ToString("E14", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

// +d where a remainder that is not 0 has the sign opposite the divisor's (fmod's carries the number's).
static double SignFix(double r, double d) => r != 0 && (r < 0) != (d < 0) ? r + d : r;

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

static double X87Identity(double n, double d, bool quotientX87)
{
    var qn = Q.Of(n);
    var qd = Q.Of(d);
    var quotient = quotientX87 ? Round(qn / qd, 64) : Q.Of(n / d);
    var floor = new Q(Q.Floor(quotient), 1);
    var product = Round(qd * floor, 64);
    return ToDouble(Round(qn - product, 64));
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

    public static Q operator +(Q a, Q b) => Make(a.Num * b.Den + b.Num * a.Den, a.Den * b.Den);
    public static Q operator -(Q a, Q b) => Make(a.Num * b.Den - b.Num * a.Den, a.Den * b.Den);
    public static Q operator *(Q a, Q b) => Make(a.Num * b.Num, a.Den * b.Den);
    public static Q operator /(Q a, Q b) => Make(a.Num * b.Den, a.Den * b.Num);
}
