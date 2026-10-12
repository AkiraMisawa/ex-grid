// The second search for the evaluation Excel uses for PMT, PV, FV and NPV (2026-10-12, after the area pass).
//
//     dotnet run spikes/exsheet-financial-evaluation/Search.cs             # the table
//     dotnet run spikes/exsheet-financial-evaluation/Search.cs -- --json   # also rewrites search-cs.json
//     dotnet run spikes/exsheet-financial-evaluation/Search.cs -- --predict "=PMT(0.05/12,36,250000,-5000)" ...
//
// Acceptance experiment only (ADR-0047, ADR-0202): nothing here ships. search.py, the first search, tried
// some 13,000 evaluations against the seventeenth run's doubles. This one runs under .NET, because the
// engine's Math.Pow, Math.Exp and Math.Log are what would compute, and adds the families search.py did not
// try: (1+rate)^n - 1 carried as expm1(n log1p(rate)), by the C library's functions or correctly rounded or
// by Kahan's and Goldberg's tricks; the x87's FYL2XP1 and F2XM1; any nper (fractional, negative); fused
// multiply-adds; for NPV every way of discounting and summing at 53 and 64 bits, an x87 register stored to
// a double at each step, and Excel's final-addition adjustment. Each candidate is run over EVERY Excel
// double the oracle's results files hold for the function, the agreeing cases too, and counted: a match is
// Excel's double to the bit. There are no case-specific answers.
//
// Arithmetic: native doubles where a candidate is in doubles (so Math.* is the platform's), and an exact
// dyadic number rounded to nearest-even at 64 bits after every operation where a candidate models an x87
// register. Correctly rounded logarithms and exponentials are computed in 256-bit fixed point.
//
// Standard library only.

#:property PublishAot=false

using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

var root = FindRoot();
var observations = Observations.Load(root);
var json = args.Contains("--json");
var predict = new List<string>();
for (var i = 0; i < args.Length - 1; i++) if (args[i] == "--predict") predict.Add(args[i + 1]);

Console.WriteLine($"{observations.Count} Excel doubles ({RuntimeLabel()})");
foreach (var o in observations) Console.WriteLine($"  {o.Id,-8} {o.Formula,-42} {o.Excel.ToString("R", CultureInfo.InvariantCulture)}  ({o.Files} results files)");

var output = new Dictionary<string, object>
{
    ["runtime"] = RuntimeLabel(),
    ["observations"] = observations.Select(o => new { o.Id, o.Formula, excel = o.Excel }).ToList(),
};
var functions = new Dictionary<string, object>();
var survivorsByFunction = new Dictionary<string, List<Candidate>>();
foreach (var (name, candidates) in new (string, IEnumerable<Candidate>)[]
{
    ("FV", Annuity.Candidates("FV")),
    ("PV", Annuity.Candidates("PV")),
    ("PMT", Annuity.Candidates("PMT")),
    ("NPV", Npv.Candidates()),
})
{
    var cases = observations.Where(o => o.Function == name && !o.RateIsZero).ToList();
    var rows = new List<(Candidate Candidate, int Matches, Dictionary<string, long> Off)>();
    foreach (var candidate in candidates)
    {
        var off = new Dictionary<string, long>();
        var matches = 0;
        foreach (var o in cases)
        {
            double got;
            try { got = candidate.Run(o.Input); }
            catch (DivideByZeroException) { got = double.NaN; }
            if (BitConverter.DoubleToInt64Bits(got) == BitConverter.DoubleToInt64Bits(o.Excel)) matches++;
            else off[o.Id] = double.IsFinite(got) ? Ulps(o.Excel, got) : long.MaxValue;
        }
        rows.Add((candidate, matches, off));
    }
    rows.Sort((a, b) => b.Matches != a.Matches ? b.Matches.CompareTo(a.Matches) : a.Off.Values.Sum(v => Math.Min(Math.Abs((double)v), 1e6)).CompareTo(b.Off.Values.Sum(v => Math.Min(Math.Abs((double)v), 1e6))));
    var all = rows.Where(r => r.Matches == cases.Count).ToList();
    survivorsByFunction[name] = all.Select(r => r.Candidate).ToList();
    Console.WriteLine($"\n{name}: {cases.Count} observed doubles at a rate other than 0, {rows.Count} candidates, {all.Count} reproduce all");
    var perCase = cases.ToDictionary(o => o.Id, o => rows.Count(r => !r.Off.ContainsKey(o.Id)));
    Console.WriteLine("  reproduced by: " + string.Join(", ", perCase.Select(p => $"{p.Key} {p.Value}")));
    foreach (var (candidate, matches, off) in rows.Take(14))
        Console.WriteLine($"  {matches}/{cases.Count}  {candidate.Label}" + (off.Count > 0 ? $"   [ulps off: {string.Join(", ", off.Select(p => $"{p.Key} {Signed(p.Value)}"))}]" : ""));
    var families = rows.GroupBy(r => r.Candidate.Family).Select(g => new { family = g.Key, candidates = g.Count(), best = g.Max(r => r.Matches), reproduceAll = g.Count(r => r.Matches == cases.Count) }).OrderByDescending(f => f.best).ToList();
    Console.WriteLine("  by family (candidates, best match count, reproduce all):");
    foreach (var f in families) Console.WriteLine($"    {f.best}/{cases.Count}  {f.candidates,6}  {f.reproduceAll,4}  {f.family}");
    functions[name] = new
    {
        cases = cases.Select(o => o.Id).ToList(),
        candidates = rows.Count,
        matchCounts = rows.GroupBy(r => r.Matches).OrderByDescending(g => g.Key).ToDictionary(g => $"{g.Key}/{cases.Count}", g => g.Count()),
        reproducedBy = perCase,
        byFamily = families,
        reproduceAll = all.Select(r => r.Candidate.Label).ToList(),
        best = rows.Where(r => r.Matches < cases.Count).Take(40).Select(r => new { candidate = r.Candidate.Label, matches = r.Matches, ulpsOff = r.Off }).ToList(),
        every = rows.Select(r => $"{r.Matches}/{cases.Count} {r.Candidate.Short}").ToList(),
    };
}
output["functions"] = functions;
output["legend"] = new
{
    annuityGrowths = Annuity.GrowthNames,
    annuityForms = Annuity.FormNames,
    pmtDues = Annuity.DueNames,
    npvBases = Npv.BaseNames,
    npvDiscounts = Npv.DiscountNames,
    npvSchemes = Npv.SchemeNames,
    npvOrders = Npv.OrderNames,
    reading = "Annuity: <form> g<growth> @<precision of the rest> d<how the payment at the start is reached>. NPV: b<base> <discount>@<bits> t@<bits of each term> <order>@<bits of the sum>, or b<base> h<scheme>@<bits>.",
};

foreach (var formula in predict) Predict(formula);
if (args.Contains("--discriminate")) Discriminate();

if (json)
{
    var path = Path.Combine(root, "spikes/exsheet-financial-evaluation/search-cs.json");
    File.WriteAllText(path, JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    Console.WriteLine($"\nwrote {path}");
}
return;

// The doubles each function's best candidates give for a formula: the candidates that reproduce every
// observed double, else the ten with the most matches.
void Predict(string formula)
{
    var input = Observations.Parse(formula, new Dictionary<string, string>());
    var function = Regex.Match(formula, "^=([A-Z]+)").Groups[1].Value;
    IEnumerable<Candidate> pool = function switch
    {
        "NPV" => Npv.Candidates(),
        _ => Annuity.Candidates(function),
    };
    var cases = observations.Where(o => o.Function == function && !o.RateIsZero).ToList();
    var scored = pool.Select(c => (c, matches: cases.Count(o => Same(Safe(c, o.Input), o.Excel)))).OrderByDescending(x => x.matches).ToList();
    var floor = scored[0].matches == cases.Count ? cases.Count : scored[0].matches - 1;
    var top = scored.Where(x => x.matches >= floor).ToList();
    Console.WriteLine($"\n{formula}: the {top.Count} candidates with {floor} or more of {cases.Count} observed doubles");
    foreach (var group in top.GroupBy(x => Safe(x.c, input)).OrderBy(g => g.Key))
        Console.WriteLine($"  {group.Key.ToString("R", CultureInfo.InvariantCulture)}: {group.Count()}, e.g. {group.First().c.Label} ({group.First().matches}/{cases.Count})");
}

// Inputs that tell the candidates apart, for the next Windows pass. The pool is the candidates with
// the most matches and those one short of it (two for NPV, whose three distinct observations leave
// few), with the four best of every family. Greedily: the input that splits the pool into the most
// groups; once no input splits it further, the input with the most distinct doubles at a rate and
// periods not yet chosen. 14 inputs.
void Discriminate()
{
    var rates = new[] { "0.03/12", "0.045/12", "0.0525/12", "0.065/12", "0.07/12", "0.09/12", "0.025/4", "0.12/12", "0.15", "0.0365/365", "0.06/52", "0.001", "0.00001" };
    var periods = new[] { "12", "24", "36", "60", "120", "240", "360" };
    var pmtGrid = (from r in rates from n in periods from pv in new[] { "10000", "250000", "1000" } from fv in new[] { "0", "-5000", "20000" } from t in new[] { "0", "1" }
                   select t == "0" ? $"=PMT({r},{n},{pv},{fv})" : $"=PMT({r},{n},{pv},{fv},{t})").ToList();
    var lists = new[]
    {
        "-10000,3000,4200,6800", "-25000,6000,8000,9000,7500,4000", "8000,9200,10000,12000,14500", "-40000,8000,9200,10000,12000,14500",
        "1000,2000,3000", "-1500,500,500,500,500", "-120000,39000,30000,21000,37000,46000", "-5000,1200,1400,1600,1800,2000",
        "250,-100,300,-50", "-10000,2000,2000,2000,2000,2000,2000,2000", "100,200,300,400,500,600,700,800,900,1000", "-7500,3000,3000,3000",
    };
    var npvGrid = (from r in new[] { "0.05", "0.07", "0.08", "0.1", "0.12", "0.15", "0.045", "0.09", "0.06/12" } from v in lists select $"=NPV({r},{v})").ToList();
    foreach (var (function, grid, all) in new[] { ("PMT", pmtGrid, Annuity.Candidates("PMT").ToList()), ("NPV", npvGrid, Npv.Candidates().ToList()) })
    {
        var cases = observations.Where(o => o.Function == function && !o.RateIsZero).ToList();
        var scored = all.Select(c => (c, matches: cases.Count(o => Same(Safe(c, o.Input), o.Excel)))).ToList();
        var most = scored.Max(x => x.matches);
        var floor = most - (function == "NPV" ? 2 : 1);
        var best = scored.Where(x => x.matches >= floor)
            .Concat(scored.GroupBy(x => x.c.Family).SelectMany(g => g.OrderByDescending(x => x.matches).Take(4)))
            .DistinctBy(x => x.c.Label).ToList();
        var engine = function == "PMT" ? all.Single(c => c.Label == "M1 -(r*(pv*g+fv))/((1+r*t)*e) | g: (1+r)^n and (1+r)^n-1 exact (any n: e^(n ln(1+r)) to 2^-250) | rest@64 | at the start: end / (1+r) exact, rounded once") : null;
        var predictions = grid.ToDictionary(f => f, f =>
        {
            var input = Observations.Parse(f, []);
            return best.Select(x => Safe(x.c, input)).ToArray();
        });
        var groups = new List<int[]> { Enumerable.Range(0, best.Count).ToArray() };
        var used = new HashSet<string>();
        Console.WriteLine($"\n{function}: a pool of {best.Count} candidates ({floor} or more of {cases.Count} observed doubles, and each family's four best); the inputs that tell them apart");
        for (var chosen = 0; chosen < 14 && predictions.Count > 0; chosen++)
        {
            string? pick = null;
            var pickGroups = groups;
            foreach (var (formula, values) in predictions)
            {
                var split = groups.SelectMany(g => g.GroupBy(i => BitConverter.DoubleToInt64Bits(values[i])).Select(x => x.ToArray())).ToList();
                if (split.Count > pickGroups.Count) { pick = formula; pickGroups = split; }
            }
            if (pick is null)
            {
                var wantStart = chosen % 2 == 1; // alternate payments at the end and at the start
                pick = predictions.Where(p => !used.Contains(Key(p.Key)) && (function != "PMT" || p.Key.EndsWith(",1)") == wantStart))
                    .OrderByDescending(p => p.Value.Where(double.IsFinite).Select(BitConverter.DoubleToInt64Bits).Distinct().Count())
                    .ThenBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key).FirstOrDefault();
                if (pick is null) break;
            }
            else groups = pickGroups;
            used.Add(Key(pick));
            var at = predictions[pick];
            var engineSays = engine is null ? "" : $"; the engine {Safe(engine, Observations.Parse(pick, [])).ToString("R", CultureInfo.InvariantCulture)}";
            Console.WriteLine($"  {pick}  ({at.Where(double.IsFinite).Distinct().Count()} doubles; {groups.Count} groups so far{engineSays})");
            foreach (var g in Enumerable.Range(0, best.Count).GroupBy(i => BitConverter.DoubleToInt64Bits(at[i])).OrderByDescending(g => g.Count()))
                Console.WriteLine($"      {BitConverter.Int64BitsToDouble(g.Key).ToString("R", CultureInfo.InvariantCulture)}: {g.Count()}, e.g. {best[g.First()].c.Label} ({best[g.First()].matches}/{cases.Count})");
            predictions.Remove(pick);
        }
    }

    // A rate (PMT), or a rate and its values' count (NPV): one input of each.
    static string Key(string formula)
    {
        var parts = Regex.Match(formula, @"^=([A-Z]+)\((.*)\)$").Groups[2].Value.Split(',');
        return formula.StartsWith("=PMT") ? parts[0] : $"{parts[0]}|{parts.Length}";
    }
}

static double Safe(Candidate c, Input input)
{
    try { return c.Run(input); }
    catch (DivideByZeroException) { return double.NaN; }
}

static bool Same(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);

static long Ulps(double excel, double got) => Ordered(got) - Ordered(excel);

static long Ordered(double x)
{
    var bits = BitConverter.DoubleToInt64Bits(x);
    return bits < 0 ? long.MinValue - bits : bits;
}

static string Signed(long v) => v == long.MaxValue ? "not finite" : v > 0 ? $"+{v}" : $"{v}";

static string RuntimeLabel() => $"{System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}, {System.Runtime.InteropServices.RuntimeInformation.OSDescription}";

static string FindRoot()
{
    var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ExGrid.slnx"))) dir = dir.Parent;
    return dir?.FullName ?? throw new InvalidOperationException("Run from inside the repository.");
}

// ------------------------------------------------------------------------------------------------
// The observations

record Input(double Rate, double Periods, double Third, double Fourth, double Type, double[] Values, double? After);

record Observation(string Id, string Formula, string Function, double Excel, Input Input, int Files)
{
    public bool RateIsZero => Input.Rate == 0;
}

static class Observations
{
    private static readonly string[] Areas = ["fv", "pv", "pmt", "npv"];

    public static List<Observation> Load(string root)
    {
        var cells = new Dictionary<string, Dictionary<string, string>>();
        var fixtures = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "tests/ExSheet.Engine.Tests/ExcelCases/fixtures.json"))).RootElement.GetProperty("fixtures");
        foreach (var area in Areas)
        {
            var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, $"tests/ExSheet.Engine.Tests/ExcelCases/{area}.json")));
            foreach (var c in doc.RootElement.GetProperty("cases").EnumerateArray())
            {
                var map = new Dictionary<string, string>();
                if (c.TryGetProperty("fixture", out var f)) foreach (var p in fixtures.GetProperty(f.GetString()!).GetProperty("cells").EnumerateObject()) map[p.Name] = p.Value.GetString()!;
                if (c.TryGetProperty("cells", out var cs)) foreach (var p in cs.EnumerateObject()) map[p.Name] = p.Value.GetString()!;
                cells[c.GetProperty("id").GetString()!] = map;
            }
        }
        var seen = new Dictionary<(string, string), Observation>();
        foreach (var path in Directory.GetFiles(Path.Combine(root, "tests/ExSheet.Engine.Tests/ExcelOracle"), "results-*.json").Order(StringComparer.Ordinal))
        {
            var doc = JsonDocument.Parse(File.ReadAllText(path).TrimStart('﻿'));
            if (!doc.RootElement.TryGetProperty("cases", out var list)) continue;
            foreach (var c in list.EnumerateArray())
            {
                if (!c.TryGetProperty("area", out var area) || !Areas.Contains(area.GetString())) continue;
                if (!c.TryGetProperty("excel", out var excel) || excel.ValueKind != JsonValueKind.Object) continue;
                if (!excel.TryGetProperty("kind", out var kind) || kind.GetString() != "number") continue;
                var id = c.GetProperty("id").GetString()!;
                var formula = excel.GetProperty("formula").GetString()!;
                var raw = excel.GetProperty("number");
                var number = raw.ValueKind == JsonValueKind.String ? double.Parse(raw.GetString()!, CultureInfo.InvariantCulture) : raw.GetDouble();
                if (seen.TryGetValue((id, formula), out var known))
                {
                    if (!Same(known.Excel, number)) throw new InvalidDataException($"{id}: two runs disagree");
                    seen[(id, formula)] = known with { Files = known.Files + 1 };
                    continue;
                }
                seen[(id, formula)] = new Observation(id, formula, Regex.Match(formula, "^=([A-Z]+)").Groups[1].Value, number, Parse(formula, cells[id]), 1);
            }
        }
        return [.. seen.Values.OrderBy(o => o.Id, StringComparer.Ordinal)];

        static bool Same(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);
    }

    /// <summary>A financial call as the corpus writes one: literals, Excel's arithmetic on them in doubles, a cell or a range.</summary>
    public static Input Parse(string formula, Dictionary<string, string> cells)
    {
        var m = Regex.Match(formula, @"^=([A-Z]+)\((.*)\)(?:\+([A-Z]+\d+))?$");
        var name = m.Groups[1].Value;
        var args = m.Groups[2].Value.Split(',');
        double? after = m.Groups[3].Success ? Number(cells[m.Groups[3].Value]) : null;
        if (name == "NPV")
        {
            var values = new List<double>();
            foreach (var a in args.Skip(1))
            {
                if (a.Contains(':')) values.AddRange(Range(a, cells));
                else if (Regex.IsMatch(a, @"^[A-Z]+\d+$")) values.AddRange(Range($"{a}:{a}", cells));
                else values.Add(Arithmetic(a));
            }
            return new Input(Scalar(args[0], cells), 0, 0, 0, 0, [.. values], after);
        }
        var v = args.Select(a => Scalar(a, cells)).Concat(Enumerable.Repeat(0.0, 5)).ToArray();
        return new Input(v[0], v[1], v[2], v[3], v[4], [], after);
    }

    private static double Scalar(string text, Dictionary<string, string> cells) =>
        Regex.IsMatch(text, @"^[A-Z]+\d+$") ? Number(cells[text]) : Arithmetic(text);

    private static double Number(string raw) => double.Parse(raw, CultureInfo.InvariantCulture);

    /// <summary>The few argument expressions the corpus writes (0.06/12, 8%/12, 12*20), in doubles.</summary>
    private static double Arithmetic(string text)
    {
        text = text.Trim();
        if (text.Length == 0) return 0;
        var m = Regex.Match(text, @"^(-?[\d.]+)(%?)(?:([*/])(-?[\d.]+))?$");
        if (!m.Success) throw new InvalidDataException(text);
        var a = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        if (m.Groups[2].Value == "%") a /= 100;
        if (m.Groups[3].Value == "/") a /= double.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture);
        if (m.Groups[3].Value == "*") a *= double.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture);
        return a;
    }

    /// <summary>The numbers SUM takes from a range: text, Booleans and blanks passed over.</summary>
    private static IEnumerable<double> Range(string reference, Dictionary<string, string> cells)
    {
        var m = Regex.Match(reference, @"^([A-Z]+)(\d+):\1(\d+)$");
        for (var row = int.Parse(m.Groups[2].Value); row <= int.Parse(m.Groups[3].Value); row++)
        {
            if (!cells.TryGetValue($"{m.Groups[1].Value}{row}", out var raw) || raw.StartsWith('\'') || raw.StartsWith('=')) continue;
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) yield return number;
        }
    }
}

// ------------------------------------------------------------------------------------------------
// Exact arithmetic: a dyadic number M * 2^E, rounded to nearest-even at P bits after each operation

readonly record struct X(BigInteger M, int E)
{
    public static readonly X Zero = new(0, 0);
    public static readonly X One = new(1, 0);

    public static X Of(double d)
    {
        if (d == 0) return Zero;
        var bits = BitConverter.DoubleToInt64Bits(d);
        var exponent = (int)((bits >> 52) & 0x7FF);
        var mantissa = bits & 0xFFFFFFFFFFFFFL;
        if (exponent == 0) exponent = 1; else mantissa |= 1L << 52;
        BigInteger m = mantissa;
        return new X(d < 0 ? -m : m, exponent - 1075);
    }

    public double ToDouble()
    {
        var r = Round(this, 53);
        return r.M.IsZero ? 0 : Math.ScaleB((double)r.M, r.E);
    }

    public int Sign => M.Sign;
    public X Negate() => new(-M, E);
    public X Abs() => new(BigInteger.Abs(M), E);

    public static X Round(X x, int p)
    {
        if (x.M.IsZero) return Zero;
        var a = BigInteger.Abs(x.M);
        var length = (int)a.GetBitLength();
        if (length <= p) return x;
        var shift = length - p;
        var keep = a >> shift;
        var rest = a - (keep << shift);
        var half = BigInteger.One << (shift - 1);
        if (rest > half || (rest == half && !keep.IsEven)) keep += 1;
        return new X(x.M.Sign < 0 ? -keep : keep, x.E + shift);
    }

    public static X Add(X a, X b, int p) => Round(AddExact(a, b), p);

    public static X AddExact(X a, X b)
    {
        if (a.M.IsZero) return b;
        if (b.M.IsZero) return a;
        var e = Math.Min(a.E, b.E);
        return new X((a.M << (a.E - e)) + (b.M << (b.E - e)), e);
    }

    public static X Mul(X a, X b, int p) => Round(new X(a.M * b.M, a.E + b.E), p);

    public static X MulExact(X a, X b) => new(a.M * b.M, a.E + b.E);

    public static X Div(X a, X b, int p)
    {
        if (b.M.IsZero) throw new DivideByZeroException();
        if (a.M.IsZero) return Zero;
        var na = BigInteger.Abs(a.M);
        var nb = BigInteger.Abs(b.M);
        var shift = Math.Max(0, p + 3 + (int)nb.GetBitLength() - (int)na.GetBitLength());
        var q = BigInteger.DivRem(na << shift, nb, out var rem);
        q = (q << 1) | (rem.IsZero ? BigInteger.Zero : BigInteger.One); // the sticky bit decides a tie
        var sign = a.M.Sign * b.M.Sign;
        return Round(new X(sign < 0 ? -q : q, a.E - b.E - shift - 1), p);
    }

    /// <summary>a/b correctly rounded at p bits, a and b exact rationals given as dyadics.</summary>
    public static X DivExact(X a, X b, int p) => Div(a, b, p);

    public static int Compare(X a, X b) => AddExact(a, b.Negate()).M.Sign;
}

/// <summary>256-bit fixed point: correctly rounded ln, exp, log2 and 2^x of dyadic arguments.</summary>
static class Fixed
{
    private const int S = 256;
    private static readonly BigInteger OneF = BigInteger.One << S;
    private static readonly BigInteger Ln2F = Atanh2(BigInteger.One, 3);

    /// <summary>x in fixed point, truncated toward zero.</summary>
    private static BigInteger Of(X x) => x.E + S >= 0 ? x.M << (x.E + S) : BigInteger.Divide(x.M, BigInteger.One << -(x.E + S));

    private static BigInteger Mul(BigInteger a, BigInteger b) => BigInteger.Divide(a * b, OneF);

    /// <summary>2 atanh(n/d) = ln((d+n)/(d-n)), 0 &lt;= n/d &lt;= 1/3, to 2^-250.</summary>
    private static BigInteger Atanh2(BigInteger n, BigInteger d)
    {
        var z = BigInteger.Divide(n << S, d);
        var z2 = Mul(z, z);
        var term = z;
        var sum = BigInteger.Zero;
        for (var k = 1; !term.IsZero; k += 2)
        {
            sum += term / k;
            term = Mul(term, z2);
        }
        return 2 * sum;
    }

    /// <summary>ln x for x &gt; 0.</summary>
    public static BigInteger Ln(X x)
    {
        if (x.Sign <= 0) throw new DivideByZeroException();
        var length = (int)x.M.GetBitLength();
        var k = x.E + length - 1;            // x = y * 2^k, y in [1, 2)
        var top = BigInteger.One << (length - 1);
        return k * Ln2F + Atanh2(x.M - top, x.M + top);
    }

    /// <summary>e^f for f in fixed point.</summary>
    public static BigInteger Exp(BigInteger f)
    {
        var k = (int)Math.Round((double)f / (double)Ln2F);
        var s = f - k * Ln2F;
        var term = OneF;
        var sum = BigInteger.Zero;
        for (var j = 1; !term.IsZero; j++)
        {
            sum += term;
            term = BigInteger.Divide(term * s, OneF * j);
        }
        return k >= 0 ? sum << k : sum >> -k;
    }

    public static X RoundFixed(BigInteger f, int p) => X.Round(new X(f, -S), p);

    public static X Ln(X x, int p) => RoundFixed(Ln(x), p);
    public static X Exp(X x, int p) => RoundFixed(Exp(Of(x)), p);
    public static X Expm1(X x, int p) => RoundFixed(Exp(Of(x)) - OneF, p);

    /// <summary>e^(n ln b) for an exact b and n, rounded once.</summary>
    public static X Power(X b, X n, int p) => RoundFixed(Exp(Mul(Of(n), Ln(b))), p);

    /// <summary>ln(1 + r), 1 + r exact.</summary>
    public static X Log1p(X r, int p) => RoundFixed(Ln(X.AddExact(X.One, r)), p);

    /// <summary>n * log2(b), rounded once: the x87's FYL2X, and with b = 1 + r exact its FYL2XP1.</summary>
    public static X MulLog2(X n, X b, int p) => RoundFixed(BigInteger.Divide(Of(n) * Ln(b), Ln2F), p);

    /// <summary>2^t - 1, rounded once: the x87's F2XM1.</summary>
    public static X Exp2m1(X t, int p) => RoundFixed(Exp(Mul(Of(t), Ln2F)) - OneF, p);
}

// ------------------------------------------------------------------------------------------------
// Arithmetic contexts: doubles (native), or an x87 register (64 bits, exact then rounded)

/// <summary>
/// A number computed at the precision every operation rounds to, <see cref="P"/>: 53 bits is a double,
/// 64 an x87 register, and anything wider is near exact. The search is single-threaded, so the
/// precision is ambient, as in search.py.
/// </summary>
readonly struct R(X v)
{
    public readonly X V = v;

    public static int P = 53;

    public static R Of(double d) => new(X.Round(X.Of(d), P));
    public static R Of(X x) => new(X.Round(x, P));
    public double ToDouble() => V.ToDouble();
    public R To(int p) => new(X.Round(V, p));
    public bool IsZero => V.M.IsZero;

    public static R operator +(R a, R b) => new(X.Add(a.V, b.V, P));
    public static R operator -(R a, R b) => new(X.Add(a.V, b.V.Negate(), P));
    public static R operator -(R a) => new(a.V.Negate());
    public static R operator *(R a, R b) => new(X.Mul(a.V, b.V, P));
    public static R operator /(R a, R b) => new(X.Div(a.V, b.V, P));

    /// <summary>a*b + c rounded once (a fused multiply-add).</summary>
    public static R Fma(R a, R b, R c) => new(X.Round(X.AddExact(X.MulExact(a.V, b.V), c.V), P));

    /// <summary>Evaluates <paramref name="f"/> with every operation at <paramref name="p"/> bits.</summary>
    public static T At<T>(int p, Func<T> f)
    {
        var saved = P;
        P = p;
        try { return f(); }
        finally { P = saved; }
    }
}

/// <summary>A candidate: its family, its full label, a short one for the record (the legend spells it out), and its evaluation.</summary>
record Candidate(string Family, string Label, string Short, Func<Input, double> Run);

// ------------------------------------------------------------------------------------------------
// PMT, PV and FV

/// <summary>(1+rate)^n and (1+rate)^n - 1, as exact values; the form rounds them to its precision.</summary>
record Growth(X G, X E);

static class Annuity
{
    // Ways of raising 1+rate to nper. Each takes rate and nper; a method that cannot raise to an nper throws.
    private static readonly (string Family, string Name, Func<double, double, Growth> Raise)[] Growths =
    [
        ("doubles: 1+rate rounded", "squaring (whole n), 1/squaring (negative n), Math.Exp(n*Math.Log(1+r)) (other n); e = g-1", (r, n) => Minus1(Pow.Squaring(1 + r, n))),
        ("doubles: 1+rate rounded", "squaring (whole n), squaring 1/(1+r) (negative n), Math.Pow (other n); e = g-1", (r, n) => Minus1(Pow.SquaringReciprocal(1 + r, n))),
        ("doubles: 1+rate rounded", "squaring (whole n), Math.Pow (negative and other n); e = g-1", (r, n) => Minus1(n >= 0 && n == Math.Floor(n) ? Pow.Whole(1 + r, (long)n) : Math.Pow(1 + r, n))),
        ("doubles: 1+rate rounded", "Math.Pow(1+r, n); e = g-1", (r, n) => Minus1(Math.Pow(1 + r, n))),
        ("doubles: 1+rate rounded", "Math.Exp(n*Math.Log(1+r)); e = g-1", (r, n) => Minus1(Math.Exp(n * Math.Log(1 + r)))),
        ("doubles: 1+rate rounded", "(1+r)^n correctly rounded; e = g-1", (r, n) => Minus1(Fixed.Power(X.Of(1 + r), X.Of(n), 53).ToDouble())),
        ("accurate e in doubles", "L=log1p(r) cr, x=n*L, g=exp(x) cr, e=expm1(x) cr", (r, n) => Accurate(r, n, Pow.Log1pCr, Pow.ExpCr, Pow.Expm1Cr)),
        ("accurate e in doubles", "L=log1p(r) cr, x=n*L, e=expm1(x) cr, g=1+e", (r, n) => Accurate(r, n, Pow.Log1pCr, null, Pow.Expm1Cr)),
        ("accurate e in doubles", "L=Goldberg's log1p (Math.Log), e=Kahan's expm1 (Math.Exp, Math.Log), g=Math.Exp(x)", (r, n) => Accurate(r, n, Pow.Log1pGoldberg, Math.Exp, Pow.Expm1Kahan)),
        ("accurate e in doubles", "L=Goldberg's log1p, e=Kahan's expm1, g=1+e", (r, n) => Accurate(r, n, Pow.Log1pGoldberg, null, Pow.Expm1Kahan)),
        ("accurate e in doubles", "L=log1p(r) cr, e=Kahan's expm1, g=Math.Exp(x)", (r, n) => Accurate(r, n, Pow.Log1pCr, Math.Exp, Pow.Expm1Kahan)),
        ("accurate e in doubles", "L=Goldberg's log1p, e=expm1(x) cr, g=exp(x) cr", (r, n) => Accurate(r, n, Pow.Log1pGoldberg, Pow.ExpCr, Pow.Expm1Cr)),
        ("accurate e in doubles", "g as PV and FV raise it, e=expm1(n*log1p(r)) cr", (r, n) => new Growth(X.Of(Pow.Squaring(1 + r, n)), X.Of(Pow.Expm1Cr(n * Pow.Log1pCr(r))))),
        ("x87: FYL2XP1, F2XM1", "t=n*log2(1+r) (FYL2XP1); |t|<1: e=F2XM1(t), g=1+e; else g=2^round(t)*(1+F2XM1(t-round(t))), e=g-1", (r, n) => X87(r, n, split: false, eFromParts: false)),
        ("x87: FYL2XP1, F2XM1", "FYL2XP1, always split by FRNDINT, e=g-1", (r, n) => X87(r, n, split: true, eFromParts: false)),
        ("x87: FYL2XP1, F2XM1", "FYL2XP1, split, e=m*2^i+(2^i-1)", (r, n) => X87(r, n, split: true, eFromParts: true)),
        ("x87: 1+rate in a register", "squaring of 1+r at 64 bits (whole n), 1 over it (negative n); e=g-1", (r, n) => Register(r, n)),
        ("exact", "(1+r)^n and (1+r)^n-1 exact (any n: e^(n ln(1+r)) to 2^-250)", (r, n) => Exact(r, n)),
    ];

    private static Growth Minus1(double g) => new(X.Of(g), X.AddExact(X.Of(g), X.Of(-1)));

    private static Growth Accurate(double r, double n, Func<double, double> log1p, Func<double, double>? exp, Func<double, double> expm1)
    {
        var x = n * log1p(r);
        var e = expm1(x);
        return new Growth(X.Of(exp is null ? 1 + e : exp(x)), X.Of(e));
    }

    private static Growth X87(double r, double n, bool split, bool eFromParts)
    {
        var t = Fixed.MulLog2(X.Of(n), X.AddExact(X.One, X.Of(r)), 64);
        if (!split && X.Compare(t.Abs(), X.One) < 0)
        {
            var m = Fixed.Exp2m1(t, 64);
            return new Growth(X.Add(X.One, m, 64), m);
        }
        var i = Math.Round(t.ToDouble(), MidpointRounding.ToEven);
        var f = X.AddExact(t, X.Of(-i));
        var mf = Fixed.Exp2m1(f, 64);
        var scale = X.Of(Math.ScaleB(1, (int)i));
        var g = X.Mul(X.Add(X.One, mf, 64), scale, 64);
        var e = eFromParts ? X.Add(X.Mul(mf, scale, 64), X.Of(Math.ScaleB(1, (int)i) - 1), 64) : X.Add(g, X.Of(-1), 64);
        return new Growth(g, e);
    }

    private static Growth Register(double r, double n)
    {
        if (n != Math.Floor(n)) throw new DivideByZeroException();
        var g = Pow.SquaringX(X.Add(X.One, X.Of(r), 64), n, 64);
        return new Growth(g, X.Add(g, X.Of(-1), 64));
    }

    private static Growth Exact(double r, double n)
    {
        var b = X.AddExact(X.One, X.Of(r));
        if (n == Math.Floor(n) && n >= 0 && n < 100000)
        {
            var exact = Pow.ExactPower(b, (long)n);
            return new Growth(exact, X.AddExact(exact, X.Of(-1)));
        }
        var g = Fixed.Power(b, X.Of(n), 200);
        return new Growth(g, X.Add(g, X.Of(-1), 200));
    }

    // Forms: each order of operations Microsoft's identity allows (search.py's, and the groupings it did
    // not try). R's operations round to the ambient precision.
    private delegate R Form(R r, R third, R fourth, R t, R g, R e);

    private static R One => R.Of(1.0);

    private static readonly (string Name, Form Form)[] FvForms =
    [
        ("F1 -(pv*g + pmt*(1+r*t)*((g-1)/r))", (r, pmt, pv, t, g, e) => -(pv * g + pmt * (One + r * t) * (e / r))),
        ("F2 -(pv*g + pmt*(1+r*t)*(g-1)/r)", (r, pmt, pv, t, g, e) => -(pv * g + pmt * (One + r * t) * e / r)),
        ("F3 -(pv*g + (pmt*(1+r*t)/r)*(g-1))", (r, pmt, pv, t, g, e) => -(pv * g + (pmt * (One + r * t) / r) * e)),
        ("F4 -(pv*g + pmt*((1+r*t)*((g-1)/r)))", (r, pmt, pv, t, g, e) => -(pv * g + pmt * ((One + r * t) * (e / r)))),
        ("F5 -((pv + pmt*(1+r*t)/r)*g - pmt*(1+r*t)/r)", (r, pmt, pv, t, g, e) => -((pv + pmt * (One + r * t) / r) * g - pmt * (One + r * t) / r)),
        ("F6 -(pmt*(1+r*t)*((g-1)/r) + pv*g)", (r, pmt, pv, t, g, e) => -(pmt * (One + r * t) * (e / r) + pv * g)),
        ("F7 -(pv*g + ((g-1)/r)*(1+r*t)*pmt)", (r, pmt, pv, t, g, e) => -(pv * g + (e / r) * (One + r * t) * pmt)),
        ("F8 -(pv*g + pmt*(((g-1)/r)*(1+r*t)))", (r, pmt, pv, t, g, e) => -(pv * g + pmt * ((e / r) * (One + r * t)))),
        ("F9 -fma(pv, g, pmt*((1+r*t)*((g-1)/r)))", (r, pmt, pv, t, g, e) => -R.Fma(pv, g, pmt * ((One + r * t) * (e / r)))),
        ("F10 -fma(pmt, (1+r*t)*((g-1)/r), pv*g)", (r, pmt, pv, t, g, e) => -R.Fma(pmt, (One + r * t) * (e / r), pv * g)),
    ];

    private static readonly (string Name, Form Form)[] PvForms =
    [
        ("V1 -(fv + pmt*(1+r*t)*((g-1)/r))/g", (r, pmt, fv, t, g, e) => -(fv + pmt * (One + r * t) * (e / r)) / g),
        ("V2 -(fv + pmt*(1+r*t)*(g-1)/r)/g", (r, pmt, fv, t, g, e) => -(fv + pmt * (One + r * t) * e / r) / g),
        ("V3 -(fv/g + pmt*(1+r*t)*((1-1/g)/r))", (r, pmt, fv, t, g, e) => -(fv / g + pmt * (One + r * t) * ((One - One / g) / r))),
        ("V4 -(fv + (pmt*(1+r*t)/r)*(g-1))/g", (r, pmt, fv, t, g, e) => -(fv + (pmt * (One + r * t) / r) * e) / g),
        ("V5 -(fv + pmt*((1+r*t)*((g-1)/r)))/g", (r, pmt, fv, t, g, e) => -(fv + pmt * ((One + r * t) * (e / r))) / g),
        ("V6 -(pmt*(1+r*t)*((g-1)/r) + fv)/g", (r, pmt, fv, t, g, e) => -(pmt * (One + r * t) * (e / r) + fv) / g),
        ("V7 -(fv + pmt*(1+r*t)*((g-1)/r))*(1/g)", (r, pmt, fv, t, g, e) => -(fv + pmt * (One + r * t) * (e / r)) * (One / g)),
        ("V8 -(fv + pmt*((1+r*t)*((g-1)/r)))*(1/g)", (r, pmt, fv, t, g, e) => -(fv + pmt * ((One + r * t) * (e / r))) * (One / g)),
        ("V9 -(fv + pmt*(((g-1)/r)*(1+r*t)))/g", (r, pmt, fv, t, g, e) => -(fv + pmt * ((e / r) * (One + r * t))) / g),
        ("V10 -fma(pmt, (1+r*t)*((g-1)/r), fv)/g", (r, pmt, fv, t, g, e) => -R.Fma(pmt, (One + r * t) * (e / r), fv) / g),
    ];

    private static readonly (string Name, Form Form)[] PmtForms =
    [
        ("M1 -(r*(pv*g+fv))/((1+r*t)*e)", (r, pv, fv, t, g, e) => -(r * (pv * g + fv)) / ((One + r * t) * e)),
        ("M2 -(pv*g+fv)/((1+r*t)*(e/r))", (r, pv, fv, t, g, e) => -(pv * g + fv) / ((One + r * t) * (e / r))),
        ("M3 -(pv*g+fv)*r/((1+r*t)*e)", (r, pv, fv, t, g, e) => -(pv * g + fv) * r / ((One + r * t) * e)),
        ("M4 -((pv*g+fv)/(e/r))/(1+r*t)", (r, pv, fv, t, g, e) => -((pv * g + fv) / (e / r)) / (One + r * t)),
        ("M5 -(pv*g+fv)/e*r/(1+r*t)", (r, pv, fv, t, g, e) => -(pv * g + fv) / e * r / (One + r * t)),
        ("M6 -(pv+fv/g)*r/((1+r*t)*(1-1/g))", (r, pv, fv, t, g, e) => -(pv + fv / g) * r / ((One + r * t) * (One - One / g))),
        ("M7 -(fv+pv*g)/((1+r*t)*((g-1)/r))", (r, pv, fv, t, g, e) => -(fv + pv * g) / ((One + r * t) * (e / r))),
        ("M8 -(pv*g+fv)/((1+r*t)*e/r)", (r, pv, fv, t, g, e) => -(pv * g + fv) / ((One + r * t) * e / r)),
        ("M9 -(pv*r*g + fv*r)/((1+r*t)*e)", (r, pv, fv, t, g, e) => -(pv * r * g + fv * r) / ((One + r * t) * e)),
        ("M10 -(pv*r/(1-1/g) + fv*r/e)/(1+r*t)", (r, pv, fv, t, g, e) => -(pv * r / (One - One / g) + fv * r / e) / (One + r * t)),
        ("M11 -(pv*g+fv)/(((1+r*t)*e)/r)", (r, pv, fv, t, g, e) => -(pv * g + fv) / (((One + r * t) * e) / r)),
        ("M12 -(pv*g+fv)/(e*(1+r*t)/r)", (r, pv, fv, t, g, e) => -(pv * g + fv) / (e * (One + r * t) / r)),
        ("M13 -(pv*g+fv)/(e/r*(1+r*t))", (r, pv, fv, t, g, e) => -(pv * g + fv) / (e / r * (One + r * t))),
        ("M15 -((pv*g+fv)*r/e)/(1+r*t)", (r, pv, fv, t, g, e) => -((pv * g + fv) * r / e) / (One + r * t)),
        ("M16 -(pv*g/e + fv/e)*r/(1+r*t)", (r, pv, fv, t, g, e) => -(pv * g / e + fv / e) * r / (One + r * t)),
        ("M17 -(pv*(r*g/e) + fv*(r/e))/(1+r*t)", (r, pv, fv, t, g, e) => -(pv * (r * g / e) + fv * (r / e)) / (One + r * t)),
        ("M18 -(pv*(r+r/e) + fv*(r/e))/(1+r*t)", (r, pv, fv, t, g, e) => -(pv * (r + r / e) + fv * (r / e)) / (One + r * t)),
        ("M19 -(pv*r + (pv+fv)*r/e)/(1+r*t)", (r, pv, fv, t, g, e) => -(pv * r + (pv + fv) * r / e) / (One + r * t)),
        ("M20 -(pv*r + (pv+fv)/(e/r))/(1+r*t)", (r, pv, fv, t, g, e) => -(pv * r + (pv + fv) / (e / r)) / (One + r * t)),
        ("M21 -(pv+fv/g)*r/((1+r*t)*(e/g))", (r, pv, fv, t, g, e) => -(pv + fv / g) * r / ((One + r * t) * (e / g))),
        ("M22 -(pv*g+fv)*(r/e)/(1+r*t)", (r, pv, fv, t, g, e) => -(pv * g + fv) * (r / e) / (One + r * t)),
        ("M23 -(r/e)*(pv*g+fv)/(1+r*t)", (r, pv, fv, t, g, e) => -(r / e) * (pv * g + fv) / (One + r * t)),
        ("M24 -(pv*(r/e+r) + fv*(r/e))/(1+r*t)", (r, pv, fv, t, g, e) => -(pv * (r / e + r) + fv * (r / e)) / (One + r * t)),
        ("M25 -(pv*g+fv)/(e/r)/(1+r*t)", (r, pv, fv, t, g, e) => -(pv * g + fv) / (e / r) / (One + r * t)),
        ("M26 -(r*fma(pv,g,fv))/((1+r*t)*e)", (r, pv, fv, t, g, e) => -(r * R.Fma(pv, g, fv)) / ((One + r * t) * e)),
        ("M27 -fma(pv,g,fv)/((1+r*t)*(e/r))", (r, pv, fv, t, g, e) => -R.Fma(pv, g, fv) / ((One + r * t) * (e / r))),
        ("M28 -fma(pv,g,fv)*r/((1+r*t)*e)", (r, pv, fv, t, g, e) => -R.Fma(pv, g, fv) * r / ((One + r * t) * e)),
        ("M29 -fma(pv,g,fv)/e*r/(1+r*t)", (r, pv, fv, t, g, e) => -R.Fma(pv, g, fv) / e * r / (One + r * t)),
        ("M30 -fma(pv+fv, r/e, pv*r)/(1+r*t)", (r, pv, fv, t, g, e) => -R.Fma(pv + fv, r / e, pv * r) / (One + r * t)),
        ("M31 -fma(pv, r, (pv+fv)*r/e)/(1+r*t)", (r, pv, fv, t, g, e) => -R.Fma(pv, r, (pv + fv) * r / e) / (One + r * t)),
        ("M32 -fma(pv*r, g, fv*r)/((1+r*t)*e)", (r, pv, fv, t, g, e) => -R.Fma(pv * r, g, fv * r) / ((One + r * t) * e)),
        ("M33 -(pv+fv*v)*r/((1+r*t)*(1-v)), v=1/g", (r, pv, fv, t, g, e) => { var v = One / g; return -(pv + fv * v) * r / ((One + r * t) * (One - v)); }),
        ("M34 -(pv+fv*v)/((1+r*t)*((1-v)/r)), v=1/g", (r, pv, fv, t, g, e) => { var v = One / g; return -(pv + fv * v) / ((One + r * t) * ((One - v) / r)); }),
        ("M35 -(pv+fv*v)*r/((1+r*t)*(e*v)), v=1/g", (r, pv, fv, t, g, e) => { var v = One / g; return -(pv + fv * v) * r / ((One + r * t) * (e * v)); }),
    ];

    // How a payment at the start is reached: inside the form, or from the payment at the end stored to a double.
    private static readonly (string Name, Func<double, double, double>? Due)[] Dues =
    [
        ("inline", null),
        ("at the start: the payment at the end, a double, / (1+r) in doubles", (end, r) => end / (1 + r)),
        ("at the start: end * (1/(1+r)) in doubles", (end, r) => end * (1 / (1 + r))),
        ("at the start: end - end*r/(1+r) in doubles", (end, r) => end - end * r / (1 + r)),
        ("at the start: end / (1+r) at 64 bits", (end, r) => R.At(64, () => (R.Of(end) / (R.Of(1.0) + R.Of(r))).ToDouble())),
        ("at the start: end / (1+r) exact, rounded once", (end, r) => X.Div(X.Of(end), X.AddExact(X.One, X.Of(r)), 53).ToDouble()),
    ];

    public static string[] GrowthNames => [.. Growths.Select((g, i) => $"g{i}: {g.Name} ({g.Family})")];
    public static string[] FormNames => [.. FvForms.Concat(PvForms).Concat(PmtForms).Select(f => f.Name)];
    public static string[] DueNames => [.. Dues.Select((d, i) => $"d{i}: {d.Name}")];

    public static IEnumerable<Candidate> Candidates(string function)
    {
        var forms = function switch { "FV" => FvForms, "PV" => PvForms, _ => PmtForms };
        var dues = function == "PMT" ? Dues : Dues[..1];
        foreach (var (gi, (family, growthName, raise)) in Growths.Index())
        {
            var cache = new Dictionary<(double, double), Growth?>();
            Growth? GrowthOf(double r, double n)
            {
                if (cache.TryGetValue((r, n), out var known)) return known;
                Growth? found;
                try { found = raise(r, n); }
                catch (DivideByZeroException) { found = null; }
                return cache[(r, n)] = found;
            }
            foreach (var p in new[] { 53, 64 })
            foreach (var (formName, form) in forms)
            foreach (var (di, (dueName, due)) in dues.Index())
            {
                var label = $"{formName} | g: {growthName} | rest@{p}" + (due is null ? "" : $" | {dueName}");
                var shortLabel = $"{formName.Split(' ')[0]} g{gi} @{p}" + (due is null ? "" : $" d{di}");
                yield return new Candidate($"{family}, rest@{p}", label, shortLabel, input =>
                {
                    var growth = GrowthOf(input.Rate, input.Periods) ?? throw new DivideByZeroException();
                    var atStart = input.Type != 0;
                    double Evaluate(double t) => R.At(p, () => form(R.Of(input.Rate), R.Of(input.Third), R.Of(input.Fourth), R.Of(t), R.Of(growth.G), R.Of(growth.E)).ToDouble());
                    if (due is null || !atStart) return Evaluate(atStart ? 1 : 0);
                    return due(Evaluate(0), input.Rate);
                });
            }
        }
    }
}

static class Pow
{
    /// <summary>The PV/FV model: squaring from the lowest bit for a whole n, 1 over it for a negative one, e^(n ln b) otherwise.</summary>
    public static double Squaring(double b, double n)
    {
        if (n != Math.Floor(n)) return Math.Exp(n * Math.Log(b));
        return n >= 0 ? Whole(b, (long)n) : 1 / Whole(b, (long)-n);
    }

    public static double SquaringReciprocal(double b, double n)
    {
        if (n != Math.Floor(n)) return Math.Pow(b, n);
        return n >= 0 ? Whole(b, (long)n) : Whole(1 / b, (long)-n);
    }

    public static double Whole(double b, long k)
    {
        var result = 1.0;
        var square = b;
        while (k > 0)
        {
            if ((k & 1) != 0) result *= square;
            k >>= 1;
            if (k > 0) square *= square;
        }
        return result;
    }

    public static X SquaringX(X b, double n, int p)
    {
        var k = (long)Math.Abs(n);
        var result = X.One;
        var square = b;
        while (k > 0)
        {
            if ((k & 1) != 0) result = X.Mul(result, square, p);
            k >>= 1;
            if (k > 0) square = X.Mul(square, square, p);
        }
        return n >= 0 ? result : X.Div(X.One, result, p);
    }

    public static X ExactPower(X b, long k)
    {
        var exact = X.One;
        for (var i = 0; i < k; i++) exact = X.MulExact(exact, b);
        return exact;
    }

    public static double Log1pCr(double r) => Fixed.Log1p(X.Of(r), 53).ToDouble();
    public static double ExpCr(double x) => Fixed.Exp(X.Of(x), 53).ToDouble();
    public static double Expm1Cr(double x) => Fixed.Expm1(X.Of(x), 53).ToDouble();

    /// <summary>Goldberg's log1p from the platform's log: log(1+x) * x / ((1+x) - 1).</summary>
    public static double Log1pGoldberg(double x)
    {
        var u = 1 + x;
        return u == 1 ? x : Math.Log(u) * x / (u - 1);
    }

    /// <summary>Kahan's expm1 from the platform's exp and log: (u-1) * x / log u.</summary>
    public static double Expm1Kahan(double x)
    {
        var u = Math.Exp(x);
        if (u == 1) return x;
        var um1 = u - 1;
        return um1 == -1 ? -1 : um1 * x / Math.Log(u);
    }
}

// ------------------------------------------------------------------------------------------------
// NPV

static class Npv
{
    // How 1+rate is formed.
    private static readonly (string Name, Func<double, X> Base)[] Bases =
    [
        ("1+r a double", r => X.Of(1 + r)),
        ("1+r at 64 bits", r => X.Add(X.One, X.Of(r), 64)),
        ("1+r exact", r => X.AddExact(X.One, X.Of(r))),
    ];

    // Each value's discount at the ambient precision: Divide (v / d) or multiply (v * d).
    private delegate List<(bool Divide, R D)> Discount(double rate, X b, int count);

    private static readonly (string Name, Discount Discount)[] Discounts =
    [
        ("running product d *= b; v/d", (r, b, n) => Running(R.Of(b), n, (d, bb) => d * bb, divide: true, reciprocal: false)),
        ("running product d *= b; v*(1/d)", (r, b, n) => Running(R.Of(b), n, (d, bb) => d * bb, divide: false, reciprocal: true)),
        ("running quotient d /= b; v*d", (r, b, n) => Running(R.Of(b), n, (d, bb) => d / bb, divide: false, reciprocal: false)),
        ("running factor f = 1/b, d *= f; v*d", (r, b, n) => Running(R.Of(1.0) / R.Of(b), n, (d, f) => d * f, divide: false, reciprocal: false)),
        ("running factor f = 1/b, d *= f; v/(1/d)", (r, b, n) => Running(R.Of(1.0) / R.Of(b), n, (d, f) => d * f, divide: true, reciprocal: true)),
        ("b^i by squaring; v/b^i", (r, b, n) => [.. Enumerable.Range(1, n).Select(i => (true, R.Of(Pow.SquaringX(R.Of(b).V, i, R.P))))]),
        ("b^i by squaring; v*(1/b^i)", (r, b, n) => [.. Enumerable.Range(1, n).Select(i => (false, R.Of(1.0) / R.Of(Pow.SquaringX(R.Of(b).V, i, R.P))))]),
        ("b^i correctly rounded; v/b^i", (r, b, n) => [.. Enumerable.Range(1, n).Select(i => (true, R.Of(Pow.ExactPower(R.Of(b).V, i))))]),
        ("b^-i correctly rounded; v*b^-i", (r, b, n) => [.. Enumerable.Range(1, n).Select(i => (false, R.Of(X.Div(X.One, Pow.ExactPower(R.Of(b).V, i), R.P))))]),
        ("Math.Pow(b, i); v/d", (r, b, n) => [.. Enumerable.Range(1, n).Select(i => (true, R.Of(Math.Pow(b.ToDouble(), i))))]),
        ("Math.Pow(b, -i); v*d", (r, b, n) => [.. Enumerable.Range(1, n).Select(i => (false, R.Of(Math.Pow(b.ToDouble(), -i))))]),
        ("Math.Exp(i*Math.Log(b)); v/d", (r, b, n) => [.. Enumerable.Range(1, n).Select(i => (true, R.Of(Math.Exp(i * Math.Log(b.ToDouble())))))]),
        ("Math.Exp(-i*Math.Log(b)); v*d", (r, b, n) => [.. Enumerable.Range(1, n).Select(i => (false, R.Of(Math.Exp(-i * Math.Log(b.ToDouble())))))]),
        ("e^(i ln b), ln and exp correctly rounded; v/d", (r, b, n) => [.. Enumerable.Range(1, n).Select(i => (true, R.Of(Fixed.Exp(X.Mul(X.Of(i), Fixed.Ln(b, R.P), R.P), R.P))))]),
        ("e^(-i ln b), ln and exp correctly rounded; v*d", (r, b, n) => [.. Enumerable.Range(1, n).Select(i => (false, R.Of(Fixed.Exp(X.Mul(X.Of(-i), Fixed.Ln(b, R.P), R.P), R.P))))]),
        ("x87 2^(i log2 b): FYL2X, F2XM1, FSCALE; v/d", (r, b, n) => [.. Enumerable.Range(1, n).Select(i => (true, R.Of(X87Power(b, i))))]),
    ];

    private static List<(bool, R)> Running(R b, int n, Func<R, R, R> step, bool divide, bool reciprocal)
    {
        var d = R.Of(1.0);
        var list = new List<(bool, R)>();
        for (var i = 0; i < n; i++)
        {
            d = step(d, b);
            list.Add(reciprocal ? (divide, R.Of(1.0) / d) : (divide, d));
        }
        return list;
    }

    private static X X87Power(X b, int i)
    {
        var t = Fixed.MulLog2(X.Of(i), b, 64);
        var k = Math.Round(t.ToDouble(), MidpointRounding.ToEven);
        var m = Fixed.Exp2m1(X.AddExact(t, X.Of(-k)), 64);
        return X.Mul(X.Add(X.One, m, 64), X.Of(Math.ScaleB(1, (int)k)), 64);
    }

    private static readonly string[] Orders =
    [
        "forward", "from the last", "pairwise", "Kahan", "Neumaier", "positive and negative apart", "negative and positive apart",
        "by magnitude", "exact, rounded once", "forward, the last addition as Excel's", "forward, fused: total = fma(v, d, total)",
    ];

    private static R Sum(string order, List<R> terms, List<(R V, bool Divide, R D)> parts)
    {
        var zero = R.Of(0.0);
        switch (order)
        {
            case "forward":
                return terms.Aggregate(zero, (t, x) => t + x);
            case "from the last":
                return Enumerable.Reverse(terms).Aggregate(zero, (t, x) => t + x);
            case "pairwise":
            {
                var xs = terms.ToList();
                while (xs.Count > 1)
                {
                    var ys = new List<R>();
                    for (var i = 0; i + 1 < xs.Count; i += 2) ys.Add(xs[i] + xs[i + 1]);
                    if (xs.Count % 2 == 1) ys.Add(xs[^1]);
                    xs = ys;
                }
                return xs[0];
            }
            case "Kahan":
            {
                R t = zero, c = zero;
                foreach (var x in terms)
                {
                    var y = x - c;
                    var tt = t + y;
                    c = (tt - t) - y;
                    t = tt;
                }
                return t;
            }
            case "Neumaier":
            {
                R t = zero, c = zero;
                foreach (var x in terms)
                {
                    var tt = t + x;
                    c = c + (X.Compare(t.V.Abs(), x.V.Abs()) >= 0 ? (t - tt) + x : (x - tt) + t);
                    t = tt;
                }
                return t + c;
            }
            case "positive and negative apart":
            case "negative and positive apart":
            {
                var pos = terms.Where(x => x.V.Sign >= 0).Aggregate(zero, (t, x) => t + x);
                var neg = terms.Where(x => x.V.Sign < 0).Aggregate(zero, (t, x) => t + x);
                return order.StartsWith("positive") ? pos + neg : neg + pos;
            }
            case "by magnitude":
                return terms.OrderBy(x => x.V.Abs().ToDouble()).Aggregate(zero, (t, x) => t + x);
            case "exact, rounded once":
                return R.Of(terms.Aggregate(X.Zero, (t, x) => X.AddExact(t, x.V)));
            case "forward, the last addition as Excel's":
            {
                var t = zero;
                for (var i = 0; i < terms.Count; i++) t = i == terms.Count - 1 ? R.Of(FinalAdd(t.ToDouble(), terms[i].ToDouble())) : t + terms[i];
                return t;
            }
            case "forward, fused: total = fma(v, d, total)":
                return parts.Aggregate(zero, (t, x) => x.Divide ? t + x.V / x.D : R.Fma(x.V, x.D, t));
        }
        throw new ArgumentException(order);
    }

    /// <summary>Excel's last addition (the engine's Arithmetic.FinalAdd): 0 when opposite signs are closer than 2^-49 of each.</summary>
    private static double FinalAdd(double a, double b)
    {
        var opposite = (a > 0 && b < 0) || (a < 0 && b > 0);
        return opposite && Math.Abs(a + b) <= Math.ScaleB(Math.Max(Math.Abs(a), Math.Abs(b)), -49) ? 0 : a + b;
    }

    // Whole schemes that do not discount each value on its own: Horner's rule, from either end.
    private static readonly (string Name, Func<X, double[], R> Scheme)[] Schemes =
    [
        ("Horner from the last: t = (t + v)/b", (b, v) => Enumerable.Reverse(v).Aggregate(R.Of(0.0), (t, x) => (t + R.Of(x)) / R.Of(b))),
        ("Horner from the last: t = (t + v)*(1/b)", (b, v) => { var f = R.Of(1.0) / R.Of(b); return Enumerable.Reverse(v).Aggregate(R.Of(0.0), (t, x) => (t + R.Of(x)) * f); }),
        ("Horner from the last: t = t/b + v, then /b", (b, v) => Enumerable.Reverse(v).Aggregate(R.Of(0.0), (t, x) => t / R.Of(b) + R.Of(x)) / R.Of(b)),
        ("Horner from the first: t = t*b + v, then / b^n (running product)", (b, v) =>
        {
            var t = v.Aggregate(R.Of(0.0), (acc, x) => acc * R.Of(b) + R.Of(x));
            var d = v.Aggregate(R.Of(1.0), (acc, _) => acc * R.Of(b));
            return t / d;
        }),
        ("Horner from the first: t = fma(t, b, v), then / b^n (running product)", (b, v) =>
        {
            var t = v.Aggregate(R.Of(0.0), (acc, x) => R.Fma(acc, R.Of(b), R.Of(x)));
            var d = v.Aggregate(R.Of(1.0), (acc, _) => acc * R.Of(b));
            return t / d;
        }),
        ("Horner from the last, fused: t = fma(t, 1/b, v), then *(1/b)", (b, v) =>
        {
            var f = R.Of(1.0) / R.Of(b);
            return Enumerable.Reverse(v).Aggregate(R.Of(0.0), (t, x) => R.Fma(t, f, R.Of(x))) * f;
        }),
    ];

    public static string[] BaseNames => [.. Bases.Select((b, i) => $"b{i}: {b.Name}")];
    public static string[] DiscountNames => [.. Discounts.Select((d, i) => $"{i}: {d.Name}")];
    public static string[] SchemeNames => [.. Schemes.Select((h, i) => $"h{i}: {h.Name}")];
    public static string[] OrderNames => [.. Orders.Select((o, i) => $"o{i}: {o}")];

    public static IEnumerable<Candidate> Candidates()
    {
        foreach (var (bi, (baseName, makeBase)) in Bases.Index())
        foreach (var (hi, (schemeName, scheme)) in Schemes.Index())
        foreach (var p in new[] { 53, 64 })
        {
            yield return new Candidate("Horner's rule", $"{baseName} | {schemeName} @{p}", $"b{bi} h{hi}@{p}", input =>
            {
                var total = R.At(p, () => scheme(makeBase(input.Rate), input.Values).ToDouble());
                return input.After is { } after ? total + after : total;
            });
        }
        foreach (var (bi, (baseName, makeBase)) in Bases.Index())
        foreach (var (ki, (discountName, discount)) in Discounts.Index())
        foreach (var discountP in new[] { 53, 64 })
        foreach (var termP in new[] { 53, 64 })
        foreach (var sumP in new[] { 53, 64 })
        foreach (var (oi, order) in Orders.Index())
        {
            if (order.StartsWith("exact") && sumP == 64) continue;
            if (order.Contains("Excel's") && sumP == 64) continue;
            var label = $"{baseName} | {discountName} @{discountP} | terms @{termP} | {order} @{sumP}";
            yield return new Candidate(discountName, label, $"b{bi} {ki}@{discountP} t@{termP} o{oi}@{sumP}", input =>
            {
                var b = makeBase(input.Rate);
                var discounts = R.At(discountP, () => discount(input.Rate, b, input.Values.Length));
                var parts = R.At(termP, () => input.Values.Select((v, i) => (R.Of(v), discounts[i].Divide, discounts[i].D)).ToList());
                var terms = R.At(termP, () => parts.Select(x => x.Divide ? x.Item1 / x.D : x.Item1 * x.D).ToList());
                var total = R.At(sumP, () => Sum(order, terms, parts).ToDouble());
                return input.After is { } after ? total + after : total;
            });
        }
    }
}
