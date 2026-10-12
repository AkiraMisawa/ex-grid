// The second and third searches for the evaluation Excel uses for PMT, PV, FV, NPV and XNPV (2026-10-12, after
// the first area pass, and again after the second, which added PMT's accurate-e families, the financial power,
// NPV's log1p discounts, XNPV's correctly rounded exponential, and the check of every survivor against the Error
// Values Excel answered).
//
//     dotnet run -c Release spikes/exsheet-financial-evaluation/Search.cs             # the table
//     dotnet run -c Release spikes/exsheet-financial-evaluation/Search.cs -- --only PMT,NPV   # some functions
//     dotnet run -c Release spikes/exsheet-financial-evaluation/Search.cs -- --json   # also rewrites search-cs.json
//     dotnet run -c Release spikes/exsheet-financial-evaluation/Search.cs -- --predict "=PMT(0.05/12,36,250000,-5000)" ...
//     dotnet run -c Release spikes/exsheet-financial-evaluation/Search.cs -- --refusals   # the engine against the survivors
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
// The standard library, and ExSheet.Engine for --refusals, which enters each input as a Formula in a Sheet
// through the engine's public surface and compares its answer with the survivors' (2026-10-12, later).

#:property PublishAot=false
#:project ../../src/ExSheet.Engine/ExSheet.Engine.csproj

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
string[]? only = null;
for (var i = 0; i < args.Length - 1; i++) if (args[i] == "--only") only = args[i + 1].Split(',');

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
    ("XNPV", Xnpv.Candidates()),
})
{
    if (only is not null && !only.Contains(name)) continue;
    var clock = System.Diagnostics.Stopwatch.StartNew();
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
            catch (Exception e) when (e is DivideByZeroException or NoAnswer) { got = double.NaN; }
            if (BitConverter.DoubleToInt64Bits(got) == BitConverter.DoubleToInt64Bits(o.Excel)) matches++;
            else off[o.Id] = double.IsFinite(got) ? Ulps(o.Excel, got) : long.MaxValue;
        }
        rows.Add((candidate, matches, off));
    }
    rows.Sort((a, b) => b.Matches != a.Matches ? b.Matches.CompareTo(a.Matches) : a.Off.Values.Sum(v => Math.Min(Math.Abs((double)v), 1e6)).CompareTo(b.Off.Values.Sum(v => Math.Min(Math.Abs((double)v), 1e6))));
    var all = rows.Where(r => r.Matches == cases.Count).ToList();
    survivorsByFunction[name] = all.Select(r => r.Candidate).ToList();
    // The Error Values Excel answered (2026-10-12, later): which each survivor gives.
    var errors = Observations.Errors.Values.Where(e => e.Function == name).OrderBy(e => e.Id, StringComparer.Ordinal).ToList();
    var errorRecord = new Dictionary<string, List<string>>();
    foreach (var (candidate, _, _) in all)
    {
        var missed = errors.Where(e => Refusals.Answer(candidate, e.Input) != e.Error).Select(e => $"{e.Id} {Refusals.Answer(candidate, e.Input)} for {e.Error}").ToList();
        errorRecord[candidate.Short] = missed;
    }
    Console.WriteLine($"\n{name}: {cases.Count} observed doubles at a rate other than 0, {rows.Count} candidates, {all.Count} reproduce all ({clock.Elapsed.TotalSeconds:F0} s)");
    var perCase = cases.ToDictionary(o => o.Id, o => rows.Count(r => !r.Off.ContainsKey(o.Id)));
    Console.WriteLine("  reproduced by: " + string.Join(", ", perCase.Select(p => $"{p.Key} {p.Value}")));
    foreach (var (candidate, matches, off) in rows.Take(14))
        Console.WriteLine($"  {matches}/{cases.Count}  {candidate.Label}" + (off.Count > 0 ? $"   [ulps off: {string.Join(", ", off.Select(p => $"{p.Key} {Signed(p.Value)}"))}]" : "")
            + (errorRecord.TryGetValue(candidate.Short, out var missedErrors) ? (missedErrors.Count == 0 ? $"   [and every observed Error Value, {errors.Count}]" : $"   [misses the Error Values {string.Join(", ", missedErrors)}]") : ""));
    // The observed doubles no candidate of a family gives: the cases that rule the whole family out (2026-10-12, later).
    var families = rows.GroupBy(r => r.Candidate.Family).Select(g => new
    {
        family = g.Key,
        candidates = g.Count(),
        best = g.Max(r => r.Matches),
        reproduceAll = g.Count(r => r.Matches == cases.Count),
        reproducedByNone = cases.Where(o => g.All(r => r.Off.ContainsKey(o.Id))).Select(o => o.Id).ToList(),
    }).OrderByDescending(f => f.best).ToList();
    Console.WriteLine("  by family (best match count, candidates, reproduce all; the observed doubles no candidate of the family gives):");
    foreach (var f in families) Console.WriteLine($"    {f.best}/{cases.Count}  {f.candidates,6}  {f.reproduceAll,4}  {f.family}" + (f.reproducedByNone.Count > 0 ? $"   [none gives {string.Join(", ", f.reproducedByNone)}]" : ""));
    functions[name] = new
    {
        cases = cases.Select(o => o.Id).ToList(),
        candidates = rows.Count,
        matchCounts = rows.GroupBy(r => r.Matches).OrderByDescending(g => g.Key).ToDictionary(g => $"{g.Key}/{cases.Count}", g => g.Count()),
        reproducedBy = perCase,
        byFamily = families,
        reproduceAll = all.Select(r => r.Candidate.Label).ToList(),
        errorValues = errors.Select(e => $"{e.Id} {e.Formula} {e.Error}").ToList(),
        survivorsMissingErrorValues = errorRecord.Where(p => p.Value.Count > 0).ToDictionary(p => p.Key, p => p.Value),
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
    xnpvYears = Xnpv.YearNames,
    xnpvDiscounts = Xnpv.DiscountNames,
    xnpvOrders = Xnpv.OrderNames,
    reading = "Annuity: <form> g<growth> @<precision of the rest> d<how the payment at the start is reached>. NPV: b<base> <discount>@<bits> t@<bits of each term> <order>@<bits of the sum>, or b<base> h<scheme>@<bits>. XNPV: y<years> <discount> t@<bits of each term> <order>@<bits of the sum>.",
};

foreach (var formula in predict) Predict(formula);
if (args.Contains("--discriminate")) Discriminate();
if (args.Contains("--refusals") && !Refusals.Check(survivorsByFunction)) return 1;
if (args.Contains("--discriminate-xnpv")) DiscriminateXnpv(survivorsByFunction["XNPV"]);

if (json)
{
    var path = Path.Combine(root, "spikes/exsheet-financial-evaluation/search-cs.json");
    File.WriteAllText(path, JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    Console.WriteLine($"\nwrote {path}");
}
return 0;

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

// The inputs that tell XNPV's survivors apart (2026-10-12, later; discriminate-xnpv.txt), for the next Windows
// pass: greedily, the input of --refusals' XNPV grid, at a rate above -1 with two values or more, that splits
// the survivors into the most groups, until none splits them further.
void DiscriminateXnpv(List<Candidate> pool)
{
    var grid = Refusals.XnpvGrid().Where(c => c.Input.Rate > -1 && c.Input.Values.Length >= 2).ToList();
    var predictions = grid.ToDictionary(c => c, c => pool.Select(s => Safe(s, c.Input)).ToArray());
    var groups = new List<int[]> { Enumerable.Range(0, pool.Count).ToArray() };
    Console.WriteLine($"\nXNPV: {pool.Count} survivors; the inputs that tell them apart");
    while (true)
    {
        Refusals.Case? pick = null;
        var pickGroups = groups;
        foreach (var (input, values) in predictions)
        {
            var split = groups.SelectMany(g => g.GroupBy(i => BitConverter.DoubleToInt64Bits(values[i])).Select(x => x.ToArray())).ToList();
            if (split.Count > pickGroups.Count) { pick = input; pickGroups = split; }
        }
        if (pick is null) break;
        groups = pickGroups;
        var at = predictions[pick];
        Console.WriteLine($"  {pick.Formula} with {string.Join(" ", pick.Cells.Select(x => $"{x.Address}={x.Typed}"))}  ({at.Distinct().Count()} doubles; {groups.Count} groups so far)");
        foreach (var g in Enumerable.Range(0, pool.Count).GroupBy(i => BitConverter.DoubleToInt64Bits(at[i])).OrderByDescending(g => g.Count()))
            Console.WriteLine($"      {BitConverter.Int64BitsToDouble(g.Key).ToString("R", CultureInfo.InvariantCulture)}: {g.Count()}, e.g. {pool[g.First()].Label}");
        predictions.Remove(pick);
    }
    Console.WriteLine($"  {groups.Count} groups; each holds evaluations that give the same double at every input of the grid:");
    foreach (var g in groups.OrderByDescending(g => g.Length)) Console.WriteLine($"    {g.Length}: {string.Join("; ", g.Take(3).Select(i => pool[i].Short))}{(g.Length > 3 ? "; ..." : "")}");
}

static double Safe(Candidate c, Input input)
{
    try { return c.Run(input); }
    catch (Exception e) when (e is DivideByZeroException or NoAnswer) { return double.NaN; }
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

record Input(double Rate, double Periods, double Third, double Fourth, double Type, double[] Values, double? After, double[]? Dates = null);

/// <summary>An evaluation that answers an Error Value rather than a number, as the ^ operator does for 0^0 (#NUM!) and 0^-1 (#DIV/0!).</summary>
sealed class NoAnswer(string error) : Exception(error)
{
    public string Error { get; } = error;
}

record Observation(string Id, string Formula, string Function, double Excel, Input Input, int Files)
{
    public bool RateIsZero => Input.Rate == 0;
}

/// <summary>An Error Value Excel answered (2026-10-12, the second area pass): a survivor is checked against these too.</summary>
record ErrorObservation(string Id, string Formula, string Function, string Error, Input Input);

static class Observations
{
    private static readonly string[] Areas = ["fv", "pv", "pmt", "npv", "xnpv"];

    /// <summary>Every Error Value Excel answered for a financial call whose arguments are numbers, by case and formula.</summary>
    public static readonly Dictionary<(string, string), ErrorObservation> Errors = [];

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
                if (excel.TryGetProperty("kind", out var errorKind) && errorKind.GetString() == "error" && excel.TryGetProperty("formula", out var errorFormula))
                {
                    var errorId = c.GetProperty("id").GetString()!;
                    var text = errorFormula.GetString()!;
                    if (Regex.IsMatch(text, @"^=(FV|PV|PMT|NPV|XNPV)\(") && !Regex.IsMatch(text, @"\((C2|A6),"))
                        Errors[(errorId, text)] = new ErrorObservation(errorId, text, Regex.Match(text, "^=([A-Z]+)").Groups[1].Value, excel.GetProperty("value2").GetString()!, Parse(text, cells[errorId]));
                    continue;
                }
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
        if (name == "XNPV") return new Input(Scalar(args[0], cells), 0, 0, 0, 0, [.. Range(args[1], cells)], after, [.. Range(args[2], cells)]);
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
        // A NaN or an infinity has no dyadic value; reading its bits as one fed Fixed.Exp a 2^1000 argument,
        // whose series never ends (FV-017's rate of -9 made Goldberg's log1p NaN, 2026-10-12).
        if (!double.IsFinite(d)) throw new NoAnswer("#NUM!");
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
        // PV and FV only (2026-10-12, later): g0's squaring for a whole n, 1 over it for a negative one, and
        // the readings of a fractional n the observed doubles leave, which part where g0 does not reach.
        ("doubles: 1+rate rounded", "squaring (whole n), 1/squaring (negative n), the ^ operator's (other n: the square root at 0.5, else Math.Exp(n*Math.Log(1+r))), the ^ operator's errors at 1+r = 0; e = g-1", (r, n) => Minus1(Pow.AsPowerOperator(1 + r, n))),
        ("doubles: 1+rate rounded", "squaring (whole n), 1/squaring (negative n), Math.Exp(n*Math.Log(1+r)) (positive other n), 1 over Math.Exp(-n*Math.Log(1+r)) (negative other n); e = g-1", (r, n) => Minus1(Pow.OneOver(1 + r, n, asPowerOperator: false))),
        ("doubles: 1+rate rounded", "squaring (whole n), 1/squaring (negative n), the ^ operator's (positive other n), 1 over the ^ operator's to -n (negative other n), the ^ operator's errors at 1+r = 0; e = g-1", (r, n) => Minus1(Pow.OneOver(1 + r, n, asPowerOperator: true))),
        ("doubles: 1+rate rounded", "squaring (whole n), 1/squaring (negative n), e^(n ln(1+r)) with ln and exp correctly rounded (other n, 1+r > 0; else as g0); e = g-1", (r, n) => Minus1(n == Math.Floor(n) || 1 + r <= 0 ? Pow.Squaring(1 + r, n) : Fixed.Exp(X.Mul(X.Of(n), Fixed.Ln(X.Of(1 + r), 53), 53), 53).ToDouble())),
        // The second area pass (2026-10-12, later): g20 with ln and exp correctly rounded, the financial power
        // XNPV-013 asks for (Pow.Financial).
        ("doubles: 1+rate rounded", "the financial power: squaring (whole n), 1 over the power to -n (negative n), the square root at 0.5, else e^(n ln(1+r)) with ln and exp correctly rounded; a negative 1+r, the odd root where 1/n is odd, else #NUM!; 1+r = 0, 0^0 #NUM! and 0 to a negative power #DIV/0!; e = g-1", (r, n) => Minus1(Pow.Financial(1 + r, n, correctlyRounded: true))),
        ("doubles: 1+rate rounded", "the financial power with the platform's ln and exp (Math.Log, Math.Exp); e = g-1", (r, n) => Minus1(Pow.Financial(1 + r, n, correctlyRounded: false))),
    ];

    /// <summary>
    /// PMT's families added by the second area pass (2026-10-12, later), each in doubles: (1+rate)^n - 1 carried
    /// accurately by every pairing of a log1p (correctly rounded, the C library's, Goldberg's from a correctly
    /// rounded or the platform's log, or log(1+r) corrected by (r-((1+r)-1))/(1+r)), the product n*L rounded or
    /// exact, an exp (correctly rounded, the platform's, or 1+e) and an expm1 (correctly rounded, the C library's,
    /// Kahan's from a correctly rounded or the platform's exp and log, or g-1); (1+rate)^n - 1 squared without
    /// forming 1+rate, every way of squaring and of combining; and the period-by-period recurrence e += r(1+e)
    /// in five orders.
    /// </summary>
    private static IEnumerable<(string Family, string Name, Func<double, double, Growth> Raise)> PmtFamilies()
    {
        (string, Func<double, double>)[] logs =
        [
            ("log1p cr", Pow.Log1pCr),
            ("log1p (libm)", Pow.Log1pLibm),
            ("Goldberg's log1p, log cr", x => Pow.Goldberg(x, Pow.LogCr)),
            ("Goldberg's log1p, Math.Log", x => Pow.Goldberg(x, Math.Log)),
            ("log(1+r) cr + (r-((1+r)-1))/(1+r)", x => Pow.Corrected(x, Pow.LogCr)),
            ("Math.Log(1+r) + (r-((1+r)-1))/(1+r)", x => Pow.Corrected(x, Math.Log)),
        ];
        (string, Func<double, double>?)[] exps = [("exp cr", Pow.ExpCr), ("Math.Exp", Math.Exp), ("g = 1+e", null)];
        (string, Func<double, double>?)[] expm1s =
        [
            ("expm1 cr", Pow.Expm1Cr),
            ("expm1 (libm)", Pow.Expm1Libm),
            ("Kahan's expm1, exp and log cr", x => Pow.Kahan(x, Pow.ExpCr, Pow.LogCr)),
            ("Kahan's expm1, Math.Exp and Math.Log", x => Pow.Kahan(x, Math.Exp, Math.Log)),
            ("e = g-1", null),
        ];
        foreach (var (logName, log) in logs)
        foreach (var (expName, exp) in exps)
        foreach (var (expm1Name, expm1) in expm1s)
        {
            if (exp is null && expm1 is null) continue;
            yield return ("accurate e in doubles (second pass)", $"L={logName}, x=n*L rounded, g={expName}, e={expm1Name}", (r, n) =>
            {
                var x = n * log(r);
                if (!double.IsFinite(x)) throw new NoAnswer("#NUM!");
                var e = expm1 is null ? exp!(x) - 1 : expm1(x);
                return new Growth(X.Of(exp is null ? 1 + e : exp(x)), X.Of(e));
            });
        }
        foreach (var (logName, log) in logs)
        foreach (var fromE in new[] { false, true })
        {
            yield return ("accurate e in doubles (second pass)", $"L={logName}, x=n*L exact, g={(fromE ? "1+e" : "exp cr")}, e=expm1 cr", (r, n) =>
            {
                var l = log(r);
                if (!double.IsFinite(l)) throw new NoAnswer("#NUM!");
                var x = X.MulExact(X.Of(n), X.Of(l));
                var e = Fixed.Expm1(x, 53).ToDouble();
                return new Growth(X.Of(fromE ? 1 + e : Fixed.Exp(x, 53).ToDouble()), X.Of(e));
            });
        }
        (string, Func<double, double>)[] squares =
        [
            ("e*(2+e)", e => e * (2 + e)), ("2e+e*e", e => 2 * e + e * e), ("fma(e,e,2e)", e => Math.FusedMultiplyAdd(e, e, 2 * e)), ("(e+e)+e*e", e => (e + e) + e * e),
        ];
        (string, Func<double, double, double>)[] combines =
        [
            ("a+b+a*b", (a, b) => a + b + a * b), ("a+(b+a*b)", (a, b) => a + (b + a * b)), ("fma(a,b,a+b)", (a, b) => Math.FusedMultiplyAdd(a, b, a + b)),
            ("a*(1+b)+b", (a, b) => a * (1 + b) + b), ("a+b*(1+a)", (a, b) => a + b * (1 + a)), ("(a+b)+a*b", (a, b) => (a + b) + a * b),
        ];
        foreach (var (squareName, square) in squares)
        foreach (var (combineName, combine) in combines)
        {
            yield return ("(1+r)^n-1 squared without 1+r (second pass)", $"e squared as {squareName}, combined as {combineName}, from e=r; g=1+e", (r, n) =>
            {
                if (n != Math.Floor(n) || n < 0) throw new NoAnswer("#VALUE!");
                var k = (long)n;
                var result = 0.0;
                var power = r;
                while (k > 0)
                {
                    if ((k & 1) != 0) result = combine(result, power);
                    k >>= 1;
                    if (k > 0) power = square(power);
                }
                return new Growth(X.Of(1 + result), X.Of(result));
            });
        }
        (string, Func<double, double, double>)[] steps =
        [
            ("e+r*(1+e)", (e, r) => e + r * (1 + e)), ("(e+r)+r*e", (e, r) => (e + r) + r * e), ("fma(r,e,e+r)", (e, r) => Math.FusedMultiplyAdd(r, e, e + r)),
            ("e+(r+r*e)", (e, r) => e + (r + r * e)), ("e*r+(e+r)", (e, r) => e * r + (e + r)),
        ];
        foreach (var (stepName, step) in steps)
        {
            yield return ("period by period (second pass)", $"e = {stepName} each period, from 0; g=1+e", (r, n) =>
            {
                if (n != Math.Floor(n) || n < 0 || n > 100000) throw new NoAnswer("#VALUE!");
                var e = 0.0;
                for (var i = 0; i < (long)n; i++) e = step(e, r);
                return new Growth(X.Of(1 + e), X.Of(e));
            });
        }
    }

    /// <summary>The growths PMT is tried with: those after them differ from g0 only at an nper that is not a whole number from 0 (of PMT's observations, PMT-011 and 012 alone), or at a rate of -1; g22, the financial power, and the second pass's families are tried too.</summary>
    private const int PmtGrowths = 18;

    private static Growth Minus1(double g) => double.IsFinite(g) ? new(X.Of(g), X.AddExact(X.Of(g), X.Of(-1))) : throw new NoAnswer("#NUM!");

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
        // The second area pass (2026-10-12, later).
        ("M36 -(fv+pv*g)*r/(e*(1+r*t))", (r, pv, fv, t, g, e) => -(fv + pv * g) * r / (e * (One + r * t))),
        ("M37 -((fv+pv*g)/e)*(r/(1+r*t))", (r, pv, fv, t, g, e) => -((fv + pv * g) / e) * (r / (One + r * t))),
        ("M38 -(fv+pv*g)/(e*((1+r*t)/r))", (r, pv, fv, t, g, e) => -(fv + pv * g) / (e * ((One + r * t) / r))),
        ("M39 -(pv + (pv+fv)/e)*r/(1+r*t)", (r, pv, fv, t, g, e) => -(pv + (pv + fv) / e) * r / (One + r * t)),
        ("M40 -(pv*g+fv)*r/e/(1+r*t)", (r, pv, fv, t, g, e) => -(pv * g + fv) * r / e / (One + r * t)),
        ("M41 -(r*(fv+pv*g))/(e*(1+r*t))", (r, pv, fv, t, g, e) => -(r * (fv + pv * g)) / (e * (One + r * t))),
    ];

    /// <summary>
    /// Whole evaluations of PMT that are not a form over (g, e) (2026-10-12, the second area pass): LibreOffice's
    /// ScGetPMT, which reaches the payment at the start through expm1((n+1) log1p(r)) - r, with correctly rounded
    /// functions or the C library's.
    /// </summary>
    private static IEnumerable<Candidate> PmtSchemes()
    {
        foreach (var (name, log1p, exp, expm1) in new (string, Func<double, double>, Func<double, double>, Func<double, double>)[]
        {
            ("correctly rounded", Pow.Log1pCr, Pow.ExpCr, Pow.Expm1Cr),
            ("the C library's", Pow.Log1pLibm, Math.Exp, Pow.Expm1Libm),
        })
        {
            yield return new Candidate("LibreOffice's ScGetPMT (second pass)", $"LibreOffice: L=log1p(r), -(fv+pv*exp(n*L))*r/expm1(n*L), at the start /(expm1((n+1)*L)-r) | {name}", $"LO {name}", input =>
            {
                var r = input.Rate;
                var n = input.Periods;
                var pv = input.Third;
                var fv = input.Fourth;
                var l = log1p(r);
                var payment = input.Type != 0 ? (fv + pv * exp(n * l)) * r / (expm1((n + 1) * l) - r) : (fv + pv * exp(n * l)) * r / expm1(n * l);
                return -payment;
            });
        }
    }

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

    public static string[] GrowthNames => [.. Growths.Select(g => (g.Family, g.Name)).Concat(PmtFamilies().Select(g => (g.Family, g.Name))).Select((g, i) => $"g{i}: {g.Name} ({g.Family}){(i >= Growths.Length ? ", PMT only" : "")}")];
    public static string[] FormNames => [.. FvForms.Concat(PvForms).Concat(PmtForms).Select(f => f.Name)];
    public static string[] DueNames => [.. Dues.Select((d, i) => $"d{i}: {d.Name}")];

    public static IEnumerable<Candidate> Candidates(string function)
    {
        if (function == "PMT") foreach (var scheme in PmtSchemes()) yield return scheme;
        var forms = function switch { "FV" => FvForms, "PV" => PvForms, _ => PmtForms };
        var dues = function == "PMT" ? Dues : Dues[..1];
        var growths = Growths.Select(g => (g.Family, g.Name, g.Raise, DoublesOnly: false)).ToList();
        if (function == "PMT") growths.AddRange(PmtFamilies().Select(g => (g.Family, g.Name, g.Raise, DoublesOnly: true)));
        foreach (var (gi, (family, growthName, raise, doublesOnly)) in growths.Index())
        {
            if (function == "PMT" && gi >= PmtGrowths && gi < Growths.Length - 2) continue;
            var cache = new Dictionary<(double, double), (Growth? Growth, Exception? Failure)>();
            Growth GrowthOf(double r, double n)
            {
                if (!cache.TryGetValue((r, n), out var known))
                {
                    try { known = (raise(r, n), null); }
                    catch (Exception e) when (e is DivideByZeroException or NoAnswer) { known = (null, e); }
                    cache[(r, n)] = known;
                }
                return known.Growth ?? throw known.Failure!;
            }
            foreach (var p in doublesOnly ? new[] { 53 } : new[] { 53, 64 })
            foreach (var (formName, form) in forms)
            foreach (var (di, (dueName, due)) in dues.Index())
            {
                var label = $"{formName} | g: {growthName} | rest@{p}" + (due is null ? "" : $" | {dueName}");
                var shortLabel = $"{formName.Split(' ')[0]} g{gi} @{p}" + (due is null ? "" : $" d{di}");
                yield return new Candidate($"{family}, rest@{p}", label, shortLabel, input =>
                {
                    var growth = GrowthOf(input.Rate, input.Periods);
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

    /// <summary>
    /// The ^ operator's evaluation (the engine's Arithmetic.Power and Evaluator), with a whole n by squaring as
    /// PV and FV raise it: 0^0 is #NUM! (ARITH-041) and 0 to a negative power #DIV/0! (ARITH-042); a fractional
    /// n of a positive base is the square root at exactly 0.5 (ARITH-133), else Math.Exp(Math.Log(b)*n); of 0,
    /// Math.Pow's 0; of a negative base, an odd root where 1/n is an odd whole number (ARITH-043), else #NUM!.
    /// </summary>
    public static double AsPowerOperator(double b, double n)
    {
        if (b == 0 && n == 0) throw new NoAnswer("#NUM!");
        if (b == 0 && n < 0) throw new NoAnswer("#DIV/0!");
        if (n == Math.Floor(n)) return n >= 0 ? Whole(b, (long)n) : 1 / Whole(b, (long)-n);
        if (b > 0) return n == 0.5 ? Math.Sqrt(b) : Math.Exp(Math.Log(b) * n);
        if (b == 0) return Math.Pow(b, n);
        var root = 1 / n;
        if (double.IsFinite(root) && root == Math.Floor(root) && Math.Abs(root % 2) == 1) return -Math.Exp(Math.Log(-b) * n);
        throw new NoAnswer("#NUM!");
    }

    /// <summary>
    /// A negative n as 1 over the power to -n, as FV-007 is for a whole n: g0's (asPowerOperator false) or the
    /// ^ operator's (true) evaluation of the fractional power.
    /// </summary>
    public static double OneOver(double b, double n, bool asPowerOperator)
    {
        if (n >= 0 || n == Math.Floor(n)) return asPowerOperator ? AsPowerOperator(b, n) : Squaring(b, n);
        return 1 / (asPowerOperator ? AsPowerOperator(b, -n) : Squaring(b, -n));
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

    /// <summary>
    /// The financial power (2026-10-12, the second area pass): FV, PV and XNPV raise 1+rate by squaring to a whole
    /// number, to a negative one as 1 over the power to its absolute value (FV-007, FV-022, PV-009), to 0.5 by the
    /// square root (FV-016) and to any other by e^(n ln b) (FV-006, PV-005), the odd root of a negative base where
    /// 1/n is an odd whole number (FV-017) and #NUM! for any other fraction of one (PV-011), and at a base of 0
    /// 0^0 as #NUM! (FV-018) and 0 to a negative power as #DIV/0! (FV-020, FV-021, PV-012). With
    /// <paramref name="correctlyRounded"/>, ln and exp are correctly rounded (XNPV-013); else the platform's.
    /// </summary>
    public static double Financial(double b, double n, bool correctlyRounded)
    {
        if (b == 0 && n == 0) throw new NoAnswer("#NUM!");
        if (b == 0 && n < 0) throw new NoAnswer("#DIV/0!");
        if (n < 0) return 1 / Financial(b, -n, correctlyRounded);
        if (n == Math.Floor(n)) return n < 9007199254740992 ? Whole(b, (long)n) : throw new NoAnswer("#VALUE!");
        if (b == 0) return 0;
        Func<double, double> exp = correctlyRounded ? ExpCr : Math.Exp;
        Func<double, double> log = correctlyRounded ? LogCr : Math.Log;
        if (b > 0) return n == 0.5 ? Math.Sqrt(b) : exp(n * log(b));
        var root = 1 / n;
        if (double.IsFinite(root) && root == Math.Floor(root) && Math.Abs(root % 2) == 1) return -exp(log(-b) * n);
        throw new NoAnswer("#NUM!");
    }

    public static double LogCr(double x)
    {
        if (!(x > 0 && double.IsFinite(x))) return x == 0 ? double.NegativeInfinity : double.NaN;
        if (!LnCache.TryGetValue(x, out var y)) LnCache[x] = y = Fixed.Ln(X.Of(x), 53).ToDouble();
        return y;
    }
    public static double Log1pLibm(double x) => Libm.Log1p(x);
    public static double Expm1Libm(double x) => Libm.Expm1(x);

    /// <summary>Goldberg's log1p from a log: log(1+x) * x / ((1+x) - 1).</summary>
    public static double Goldberg(double x, Func<double, double> log)
    {
        var u = 1 + x;
        return u == 1 ? x : log(u) * x / (u - 1);
    }

    /// <summary>log1p as log(1+x) corrected by the part of x that 1+x lost: log(u) + (x - (u-1))/u.</summary>
    public static double Corrected(double x, Func<double, double> log)
    {
        var u = 1 + x;
        return log(u) + (x - (u - 1)) / u;
    }

    /// <summary>Kahan's expm1 from an exp and a log: (u-1) * x / log u.</summary>
    public static double Kahan(double x, Func<double, double> exp, Func<double, double> log)
    {
        var u = exp(x);
        if (u == 1) return x;
        var um1 = u - 1;
        return um1 == -1 ? -1 : um1 * x / log(u);
    }

    public static double Log1pCr(double r) => Fixed.Log1p(X.Of(r), 53).ToDouble();
    private static readonly Dictionary<double, double> ExpCache = [];
    private static readonly Dictionary<double, double> LnCache = [];
    public static double ExpCr(double x)
    {
        if (!ExpCache.TryGetValue(x, out var y)) ExpCache[x] = y = !double.IsFinite(x) ? Math.Exp(x) : x > 710 ? double.PositiveInfinity : x < -746 ? 0 : Fixed.Exp(X.Of(x), 53).ToDouble();
        return y;
    }
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

/// <summary>The C library's log1p and expm1 (glibc's here), which .NET does not expose.</summary>
static class Libm
{
    [System.Runtime.InteropServices.DllImport("libm.so.6", EntryPoint = "log1p")] private static extern double CLog1p(double x);
    [System.Runtime.InteropServices.DllImport("libm.so.6", EntryPoint = "expm1")] private static extern double CExpm1(double x);

    public static double Log1p(double x)
    {
        try { return CLog1p(x); }
        catch (DllNotFoundException) { throw new NoAnswer("no libm"); }
    }

    public static double Expm1(double x)
    {
        try { return CExpm1(x); }
        catch (DllNotFoundException) { throw new NoAnswer("no libm"); }
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
        // The second area pass (2026-10-12, later): 1+rate never formed, and the factor squared.
        ("e^(i log1p r), log1p and exp correctly rounded; v/d", (r, b, n) => [.. Enumerable.Range(1, n).Select(i => (true, R.Of(Fixed.Exp(X.Mul(X.Of(i), Fixed.Log1p(X.Of(r), R.P), R.P), R.P))))]),
        ("e^(-i log1p r), log1p and exp correctly rounded; v*d", (r, b, n) => [.. Enumerable.Range(1, n).Select(i => (false, R.Of(Fixed.Exp(X.Mul(X.Of(-i), Fixed.Log1p(X.Of(r), R.P), R.P), R.P))))]),
        ("(1/b)^i by squaring 1/b; v*d", (r, b, n) => [.. Enumerable.Range(1, n).Select(i => (false, R.Of(Pow.SquaringX((R.Of(1.0) / R.Of(b)).V, i, R.P))))]),
        ("Math.Exp(i*Math.Log(b)) with Math.Log(b) once, by b^i = e^(i L); v*(1/d)", (r, b, n) => [.. Enumerable.Range(1, n).Select(i => (false, R.Of(1.0) / R.Of(Math.Exp(i * Math.Log(b.ToDouble())))))]),
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

    public static X X87Power(X b, double i)
    {
        var t = Fixed.MulLog2(X.Of(i), b, 64);
        var k = Math.Round(t.ToDouble(), MidpointRounding.ToEven);
        var m = Fixed.Exp2m1(X.AddExact(t, X.Of(-k)), 64);
        return X.Mul(X.Add(X.One, m, 64), X.Of(Math.ScaleB(1, (int)k)), 64);
    }

    public static readonly string[] Orders =
    [
        "forward", "from the last", "pairwise", "Kahan", "Neumaier", "positive and negative apart", "negative and positive apart",
        "by magnitude", "exact, rounded once", "forward, the last addition as Excel's", "forward, fused: total = fma(v, d, total)",
        "pairwise, halving: (first half) + (second half), the first half the shorter", "by magnitude, the largest first",
    ];

    public static R Sum(string order, List<R> terms, List<(R V, bool Divide, R D)> parts)
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
            case "pairwise, halving: (first half) + (second half), the first half the shorter":
                return Halving(terms);
            case "by magnitude, the largest first":
                return terms.OrderByDescending(x => x.V.Abs().ToDouble()).Aggregate(zero, (t, x) => t + x);
        }
        throw new ArgumentException(order);
    }

    /// <summary>The terms summed by halving: the sum of the first half plus the sum of the second, the first the shorter when they are odd.</summary>
    public static R Halving(List<R> terms) =>
        terms.Count == 1 ? terms[0] : Halving(terms[..(terms.Count / 2)]) + Halving(terms[(terms.Count / 2)..]);

    /// <summary>Excel's last addition (the engine's Arithmetic.FinalAdd): 0 when opposite signs are closer than 2^-49 of each.</summary>
    private static double FinalAdd(double a, double b)
    {
        var opposite = (a > 0 && b < 0) || (a < 0 && b > 0);
        var difference = Math.Abs(a + b);
        return opposite && a != -b && difference < Math.Abs(a) * Math.ScaleB(1, -49) && difference < Math.Abs(b) * Math.ScaleB(1, -49) ? 0 : a + b;
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

// ------------------------------------------------------------------------------------------------
// XNPV (2026-10-12, later): the evaluations search.py tried, and the ones it did not, for Excel's XNPV-001
// and XNPV-004: years from the first date divided by 365 or multiplied by 1/365; the discount b^x by
// e^(x ln b), by squaring or Math.Pow at a whole number of years, or as search.py's; each value divided by
// it or multiplied by its reciprocal; and NPV's orders of summing, with pairwise by halving besides.

static class Xnpv
{
    private static readonly (string Name, Func<double, double> Years)[] Years =
    [
        ("(d-d0)/365", days => days / 365),
        ("(d-d0)*(1/365)", days => days * (1.0 / 365)),
    ];

    private static readonly (string Name, Func<double, double, double> Discount)[] Discounts =
    [
        ("Math.Exp(x*Math.Log(b))", (b, x) => Math.Exp(x * Math.Log(b))),
        ("whole x by squaring, else Math.Exp(x*Math.Log(b))", (b, x) => x == Math.Floor(x) ? Pow.Whole(b, (long)x) : Math.Exp(x * Math.Log(b))),
        ("whole x by Math.Pow, else Math.Exp(x*Math.Log(b))", (b, x) => x == Math.Floor(x) ? Math.Pow(b, x) : Math.Exp(x * Math.Log(b))),
        ("Math.Pow(b, x)", Math.Pow),
        ("b^x correctly rounded", (b, x) => Fixed.Power(X.Of(b), X.Of(x), 53).ToDouble()),
        ("e^(x ln b), ln and exp correctly rounded", (b, x) => Fixed.Exp(X.Mul(X.Of(x), Fixed.Ln(X.Of(b), 53), 53), 53).ToDouble()),
        ("x87 2^(x log2 b): FYL2X, F2XM1, FSCALE", (b, x) => Npv.X87Power(X.Of(b), x).ToDouble()),
        ("1/Math.Exp(-x*Math.Log(b))", (b, x) => 1 / Math.Exp(-x * Math.Log(b))),
        // The second area pass (2026-10-12, later): the financial power with ln and exp correctly rounded, and its kin.
        ("whole x by squaring, else e^(x ln b), ln and exp correctly rounded", (b, x) => x == Math.Floor(x) ? Pow.Whole(b, (long)x) : Pow.ExpCr(x * Pow.LogCr(b))),
        ("whole x by Math.Pow, else e^(x ln b), ln and exp correctly rounded", (b, x) => x == Math.Floor(x) ? Math.Pow(b, x) : Pow.ExpCr(x * Pow.LogCr(b))),
        ("whole x by squaring, else 1/e^(-x ln b), ln and exp correctly rounded", (b, x) => x == Math.Floor(x) ? Pow.Whole(b, (long)x) : 1 / Pow.ExpCr(-x * Pow.LogCr(b))),
        ("whole x by squaring, else b^x correctly rounded", (b, x) => x == Math.Floor(x) ? Pow.Whole(b, (long)x) : Fixed.Power(X.Of(b), X.Of(x), 53).ToDouble()),
        ("whole x by squaring, else the x87's 2^(x log2 b)", (b, x) => x == Math.Floor(x) ? Pow.Whole(b, (long)x) : Npv.X87Power(X.Of(b), x).ToDouble()),
    ];

    // How a value meets its discount d: divided by it, multiplied by 1/d, or multiplied by e^(-x ln b).
    private static readonly string[] Terms = ["v/d", "v*(1/d)", "v*Math.Exp(-x*Math.Log(b))"];

    private static readonly string[] Orders = Npv.Orders;

    public static string[] YearNames => [.. Years.Select((y, i) => $"y{i}: {y.Name}")];
    public static string[] DiscountNames => [.. Discounts.Select((d, i) => $"{i}: {d.Name}")];
    public static string[] OrderNames => [.. Orders.Select((o, i) => $"o{i}: {o}")];

    public static IEnumerable<Candidate> Candidates()
    {
        foreach (var (yi, (yearsName, years)) in Years.Index())
        foreach (var (ki, (discountName, discount)) in Discounts.Index())
        foreach (var (ti, term) in Terms.Index())
        foreach (var termP in new[] { 53, 64 })
        foreach (var sumP in new[] { 53, 64 })
        foreach (var (oi, order) in Orders.Index())
        {
            if (ti == 2 && ki != 0) continue; // e^(-x ln b) is its own discount
            if (order.StartsWith("exact") && sumP == 64) continue;
            if (order.Contains("Excel's") && sumP == 64) continue;
            var label = $"years {yearsName} | discount {discountName} | {term} @{termP} | {order} @{sumP}";
            yield return new Candidate($"XNPV: {discountName}", label, $"y{yi} {ki} {term} t@{termP} o{oi}@{sumP}", input =>
            {
                if (input.Values.Length != input.Dates!.Length) throw new NoAnswer("#NUM!");
                var b = 1 + input.Rate;
                var first = Math.Floor(input.Dates![0]);
                var parts = new List<(R V, bool Divide, R D)>();
                var terms = R.At(termP, () =>
                {
                    var list = new List<R>();
                    for (var i = 0; i < input.Values.Length; i++)
                    {
                        if (Math.Floor(input.Dates[i]) < first) throw new NoAnswer("#NUM!");
                        var x = years(Math.Floor(input.Dates[i]) - first);
                        var v = R.Of(input.Values[i]);
                        var (divide, d) = ti switch
                        {
                            0 => (true, R.Of(discount(b, x))),
                            1 => (false, R.Of(1 / discount(b, x))),
                            _ => (false, R.Of(Math.Exp(-x * Math.Log(b)))),
                        };
                        if (!double.IsFinite(d.ToDouble()) || (divide && d.IsZero)) throw new NoAnswer("#NUM!");
                        parts.Add((v, divide, d));
                        list.Add(divide ? v / d : v * d);
                    }
                    return list;
                });
                return R.At(sumP, () => Npv.Sum(order, terms, parts).ToDouble());
            });
        }
    }

}

// ------------------------------------------------------------------------------------------------
// --refusals (2026-10-12, later): ADR-0047 and ADR-0202 admit an answer only where the evaluations Excel's
// observed doubles leave agree on it. Over a grid of inputs, each survivor of the search above is run, and
// the engine, through its public Sheet, must give their common answer where they agree and #VALUE! where they
// part. Two kinds of exception are listed rather than failed: refusals kept where the survivors agree (an
// argument refused before the area pass and not yet asked), and answers kept where they part (an answer the
// engine gave before this branch, which a corpus case asks Excel).

static class Refusals
{
    private static readonly System.Globalization.CultureInfo EnUs = System.Globalization.CultureInfo.GetCultureInfo("en-US");

    public static bool Check(Dictionary<string, List<Candidate>> survivors)
    {
        var ok = true;
        // The second area pass (2026-10-12, later): PV and FV raise 1+rate by one power, which FV's doubles settle
        // but for its exponential and logarithm, the platform's (g20) or correctly rounded (g22). XNPV-013 settles
        // that: the engine computes g22, and the inputs where g20 parts from it are counted, not failed.
        foreach (var function in new[] { "FV", "PV" })
        {
            var form = function == "FV" ? "F4" : "V5";
            var power = survivors[function].Where(c => c.Short == $"{form} g22 @53").ToList();
            if (power.Count != 1) throw new InvalidOperationException($"{function}'s {form} g22 does not survive");
            ok &= Report($"{function}, the financial power with ln and exp correctly rounded (g22)", power, AnnuityGrid(function), Kept);
            var platform = survivors[function].Where(c => c.Short == $"{form} g23 @53").ToList();
            if (platform.Count == 1) Report($"{function}, g22 beside g23, the same power with the platform's ln and exp (not a failure: XNPV-013 rules the platform's out)", [.. power, .. platform], AnnuityGrid(function), Kept);
            Report($"{function}, every survivor of {function}'s own doubles (not a failure: FV's settle PV's power)", survivors[function], AnnuityGrid(function), Kept);
        }
        // XNPV computes one evaluation: the days over 365, whole years by squaring, else e^(x ln b) correctly
        // rounded, each value divided, summed from the first; it refuses where Excel's last-addition adjustment
        // parts from that sum, the other survivor.
        var xnpv = survivors["XNPV"].Where(c => Regex.IsMatch(c.Short, @"^y0 8 v/d t@53 o(0|9)@53$")).ToList();
        if (xnpv.Count != 2) throw new InvalidOperationException($"{xnpv.Count} of XNPV's two evaluations survive");
        ok &= Report("XNPV, its two evaluations", xnpv, XnpvGrid(), Kept);
        Report("XNPV, every survivor", survivors["XNPV"], XnpvGrid(), Kept);
        ok &= ReportPmt();
        return ok;
    }

    /// <summary>
    /// PMT (2026-10-12, ADR-0047's exception for PMT alone, decided with the user): no evaluation gives every observed
    /// payment, so PMT has no survivors to agree, and the engine gives the exact payment rounded once. Over the annuity
    /// grid the engine must give the payment computed here exactly (the growth exact at a whole nper, else e^(n ln b) in
    /// 256-bit fixed point; the payment at the end rounded once; at the start that double over 1+rate, rounded once),
    /// the financial power's Error Values, and #VALUE! where it refuses until Excel is asked.
    /// </summary>
    private static bool ReportPmt()
    {
        int inputs = 0, answered = 0;
        var refused = new Dictionary<string, int>();
        var failures = new List<string>();
        foreach (var c in AnnuityGrid("PMT"))
        {
            inputs++;
            var (exact, refusal) = ExactPmt(c.Input);
            var engine = Engine(c);
            if (refusal is not null)
            {
                if (engine == "#VALUE!") { refused[refusal] = refused.GetValueOrDefault(refusal) + 1; continue; }
                failures.Add($"{c.Formula}: the engine {engine}, refused there ({refusal})");
                continue;
            }
            if (engine == exact) { answered++; continue; }
            failures.Add($"{c.Formula}: the engine {engine}, the exact payment {exact}");
        }
        Console.WriteLine($"\nPMT, the exact payment rounded once (ADR-0047's exception): {inputs} inputs; the engine gives it, or the financial power's Error Value, at {answered}");
        foreach (var (reason, count) in refused) Console.WriteLine($"  {count} refused: {reason}");
        Console.WriteLine(failures.Count == 0 ? "  the engine gives the exact payment at every other input" : $"  {failures.Count} inputs where the engine does not:");
        foreach (var f in failures.Take(25)) Console.WriteLine($"    {f}");
        return failures.Count == 0;
    }

    /// <summary>PMT's exact payment over the doubles, or the financial power's Error Value, or why the engine refuses the input.</summary>
    private static (string Answer, string? Refusal) ExactPmt(Input input)
    {
        double r = input.Rate, n = input.Periods, pv = input.Third, fv = input.Fourth;
        if (n == 0) return ("#NUM!", null);
        if (r == 0) return (Format(-(pv + fv) / n), null);
        try { Pow.Financial(1 + r, n, correctlyRounded: true); }
        catch (NoAnswer power) { return (power.Error, null); }
        var b = X.AddExact(X.One, X.Of(r));
        if (b.Sign == 0) return ("", "a rate of -1 with nper above 0, where the forms over the growth part between -fv and an Error Value (PMT-034 asks)");
        var m = Math.Abs(n);
        X g;
        if (m == Math.Floor(m)) g = Pow.ExactPower(b, (long)m);
        else if (b.Sign > 0) g = Fixed.Power(b, X.Of(m), 320);
        else g = Fixed.Power(b.Negate(), X.Of(m), 320).Negate();
        if (n < 0) g = X.Div(X.One, g, 400);
        if (!double.IsFinite(g.ToDouble())) return ("", "1+rate raised past a double's range, where the payment is a number and an evaluation in doubles gives #NUM! (PMT-038 asks)");
        var e = X.AddExact(g, X.Of(-1));
        if (e.Sign == 0) return ("", "a growth of exactly 1 (a rate of -2, an even nper), where every form divides by 0 (PMT-035 asks)");
        var owed = X.MulExact(X.Of(r), X.AddExact(X.MulExact(X.Of(pv), g), X.Of(fv))).Negate();
        var end = X.Div(owed, e, 53).ToDouble();
        if (input.Type != 0 && double.IsFinite(end)) end = X.Div(X.Of(end), b, 53).ToDouble();
        return (double.IsFinite(end) ? Format(end) : "#NUM!", null);
    }

    private static string Format(double d) => d == 0 ? "0" : d.ToString("R", CultureInfo.InvariantCulture);

    public record Case(string Formula, Input Input, (string Address, string Typed)[] Cells);

    /// <summary>
    /// Why the engine may differ from the survivors at an input, or null where it may not. <paramref name="unfused"/>
    /// is the first survivor's answer.
    /// </summary>
    private static string? Kept(Case c, bool part, string engine, string unfused)
    {
        var input = c.Input;
        if (c.Formula.StartsWith("=XNPV"))
        {
            if (engine == "#NUM!" && input.Rate < 0) return "#NUM! at a rate below 0, as Excel gave at -0.5, -1, -1.5 and -2 (XNPV-007 to 010, 012, 017), which no evaluation models";
            if (engine == "#VALUE!" && input.Rate == 0) return "refused at a rate of 0, where \"below 0\" and \"0 or below\" part (XNPV-019 asks)";
            return null;
        }
        if (!part && engine == "#VALUE!" && c.Formula.StartsWith("=PV") && input.Rate == -1 && input.Periods > 0)
            return "refused, the survivors dividing by a growth of 0: PV at a rate of -1 with nper above 0, whose Error Value no run has observed (PV-013 asks)";
        return null;
    }

    private static bool Report(string function, List<Candidate> survivors, IEnumerable<Case> grid, Func<Case, bool, string, string, string?> kept)
    {
        int inputs = 0, agreeing = 0, parting = 0;
        var exceptions = new Dictionary<string, int>();
        var failures = new List<string>();
        var reference = survivors.FirstOrDefault(s => s.Short.StartsWith("F4 g0 ") || s.Short.StartsWith("V5 g0 "));
        foreach (var c in grid)
        {
            inputs++;
            var answers = survivors.Select(s => Answer(s, c.Input)).Distinct().ToList();
            var part = answers.Count > 1;
            if (part) parting++; else agreeing++;
            var expected = part ? "#VALUE!" : answers[0];
            var engine = Engine(c);
            if (engine == expected) continue;
            if (kept(c, part, engine, reference is null ? "" : Answer(reference, c.Input)) is { } reason)
            {
                exceptions[reason] = exceptions.GetValueOrDefault(reason) + 1;
                continue;
            }
            failures.Add($"{c.Formula}{(c.Cells.Length > 0 ? " with " + string.Join(" ", c.Cells.Select(x => $"{x.Address}={x.Typed}")) : "")}: the engine {engine}, the survivors {string.Join(" | ", answers)}");
        }
        Console.WriteLine($"\n{function}: {survivors.Count} survivors over {inputs} inputs; they agree at {agreeing} and part at {parting}");
        foreach (var (reason, count) in exceptions) Console.WriteLine($"  {count} {reason}");
        Console.WriteLine(failures.Count == 0 ? "  the engine gives their answer where they agree and #VALUE! where they part, at every other input" : $"  {failures.Count} inputs where the engine does not:");
        foreach (var f in failures.Take(25)) Console.WriteLine($"    {f}");
        return failures.Count == 0;
    }

    /// <summary>A survivor's answer as the engine writes a Value: the double's shortest round-trip form, or an Error Value.</summary>
    public static string Answer(Candidate candidate, Input input)
    {
        try
        {
            var d = candidate.Run(input);
            return !double.IsFinite(d) ? "#NUM!" : d == 0 ? "0" : d.ToString("R", CultureInfo.InvariantCulture);
        }
        catch (NoAnswer e) { return e.Error; }
        catch (DivideByZeroException) { return "#NUM!"; }
    }

    private static string Engine(Case c)
    {
        var sheet = new ExSheet.Engine.Sheet(EnUs);
        foreach (var (address, typed) in c.Cells) sheet.Enter(ExSheet.Engine.CellAddress.Parse(address), typed);
        sheet.Enter(ExSheet.Engine.CellAddress.Parse("ZZ1000"), c.Formula);
        var value = sheet.GetValue(ExSheet.Engine.CellAddress.Parse("ZZ1000"))!.Value;
        return value.Kind == ExSheet.Engine.ValueKind.Number && value.Number == 0 ? "0" : value.ToString();
    }

    /// <summary>Rates and numbers of periods around every reading: whole, negative, 0.5, -0.5, other fractions; a rate of -1 and below.</summary>
    private static IEnumerable<Case> AnnuityGrid(string function)
    {
        string[] rates = ["0.06/12", "0.05/12", "0.03/12", "0.08/12", "0.05", "0.1", "0.42", "0.0365/365", "-0.5", "-1", "-1.5", "-9"];
        string[] periods = ["0", "1", "10", "12", "36", "360", "-1", "-10", "-12", "0.5", "-0.5", "10.5", "-10.5", "10.25", "1/3", "-1/3", "2.5", "36.25", "0.25"];
        string[] thirds = ["0", "-100", "-150", "-200"];
        string[] fourths = ["0", "-500", "1000", "-1000"];
        foreach (var rate in rates)
        foreach (var n in periods)
        foreach (var third in thirds)
        foreach (var fourth in fourths)
        foreach (var type in new[] { "0", "1" })
        {
            var formula = $"={function}({rate},{n},{third},{fourth},{type})";
            yield return new Case(formula, Observations.Parse(formula, []), []);
        }
    }

    /// <summary>The corpus's XNPV inputs, and 4000 drawn: up to seven values, dates whole years apart, days where /365 and *(1/365) part, and any.</summary>
    public static IEnumerable<Case> XnpvGrid()
    {
        var random = new Random(20261012);
        string[] rates = ["0.05", "0.06", "0.07", "0.09", "0.12", "0.1", "0.0525", "-0.5", "1.5", "-1", "-1.5"];
        int[] special = [365, 730, 1095, 1460, 1825, 3, 6, 12, 24, 45, 48, 53, 85, 90, 96, 101, 106];
        string[] values = ["-10000", "2750", "4250", "3250", "3000", "4200", "6800", "-1000", "300", "500", "1000", "2500"];
        var fixedCases = new (string Rate, string[] Values, int[] Days)[]
        {
            ("0.09", ["-10000", "2750", "4250", "3250", "2750"], [0, 60, 303, 411, 456]),
            ("0.12", ["-10000", "2750", "2750", "2750", "4250"], [0, 74, 641, 769, 879]),
            ("0.07", ["-10000", "3000", "4200", "6800"], [0, 365, 730, 1095]),
            ("0.06", ["-10000", "3000", "4200", "6800"], [0, 365, 730, 1095]),
        };
        foreach (var (rate, v, days) in fixedCases) yield return Make(rate, v, days);
        for (var i = 0; i < 4000; i++)
        {
            var count = random.Next(1, 8);
            var days = new int[count];
            for (var j = 1; j < count; j++) days[j] = random.Next(10) < 3 ? special[random.Next(special.Length)] : random.Next(0, 2200);
            yield return Make(rates[random.Next(rates.Length)], [.. Enumerable.Range(0, count).Select(_ => values[random.Next(values.Length)])], days);
        }

        static Case Make(string rate, string[] v, int[] days)
        {
            var cells = new List<(string, string)>();
            for (var j = 0; j < v.Length; j++)
            {
                cells.Add(($"A{j + 1}", v[j]));
                cells.Add(($"B{j + 1}", (39448 + days[j]).ToString(CultureInfo.InvariantCulture)));
            }
            var formula = $"=XNPV({rate},A1:A{v.Length},B1:B{v.Length})";
            var input = new Input(double.Parse(rate, CultureInfo.InvariantCulture), 0, 0, 0, 0,
                [.. v.Select(x => double.Parse(x, CultureInfo.InvariantCulture))], null, [.. days.Select(d => 39448.0 + d)]);
            return new Case(formula, input, [.. cells]);
        }
    }
}
