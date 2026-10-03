using System.Text.Json;
using Xunit;

namespace ExSheet.Engine.Tests;

/// <summary>
/// ADR-0047: every admitted function gives Excel's answer, and its tests are written from Excel's
/// observed behaviour. The cases live in one corpus, <c>ExcelCases/*.json</c>, which these theories
/// run against the engine and <c>ExcelOracle/oracle.ps1</c> runs against a real Excel on Windows.
/// A case's <c>source</c> says where its expected answer came from: <c>documented</c> (Microsoft's
/// documentation), <c>observed</c> (a real Excel, by the oracle) or <c>uncertain</c> (ticket 19:
/// the engine's current answer, to be asked). A case the engine answers differently from Excel by
/// decision names the ADR in <c>engineDiffersByDecision</c>; its <c>expect</c> is still the engine's
/// answer, and <c>excelExpect</c>, where given, is Excel's.
/// </summary>
public class ExcelCaseTests
{
    public static TheoryData<string> Ids(string area) => [.. ExcelCorpus.Cases(area).Select(c => c.GetProperty("id").GetString()!)];

    private static void Run(string area, string id)
    {
        var c = ExcelCorpus.Case(area, id);
        var differences = ExcelCorpus.Run(c);
        Assert.True(differences.Count == 0, $"{id} ({c.GetProperty("description").GetString()}):\n  {string.Join("\n  ", differences)}");
    }

    [Theory, MemberData(nameof(Ids), "arithmetic")] // ADR-0047: operators, coercion and doubles as Excel's
    public void Arithmetic(string id) => Run("arithmetic", id);

    [Theory, MemberData(nameof(Ids), "errors")] // ADR-0047: Error Values propagate; a cycle is #CIRC!
    public void Errors(string id) => Run("errors", id);

    [Theory, MemberData(nameof(Ids), "references")] // ADR-0046/0047: what a Reference or a name reads
    public void References(string id) => Run("references", id);

    [Theory, MemberData(nameof(Ids), "formula-text")] // ADR-0047: the stored Formula is Excel's spelling, whitespace kept
    public void FormulaText(string id) => Run("formula-text", id);

    [Theory, MemberData(nameof(Ids), "sheet-names")] // ADR-0046: Sheet qualifiers and renaming
    public void SheetNames(string id) => Run("sheet-names", id);

    [Theory, MemberData(nameof(Ids), "sum")] // ADR-0047
    public void Sum(string id) => Run("sum", id);

    [Theory, MemberData(nameof(Ids), "average")] // ADR-0047
    public void Average(string id) => Run("average", id);

    [Theory, MemberData(nameof(Ids), "min")] // ADR-0047
    public void Min(string id) => Run("min", id);

    [Theory, MemberData(nameof(Ids), "max")] // ADR-0047
    public void Max(string id) => Run("max", id);

    [Theory, MemberData(nameof(Ids), "count")] // ADR-0047
    public void Count(string id) => Run("count", id);

    [Theory, MemberData(nameof(Ids), "counta")] // ADR-0047
    public void CountA(string id) => Run("counta", id);

    [Theory, MemberData(nameof(Ids), "if")] // ADR-0047
    public void If(string id) => Run("if", id);

    [Theory, MemberData(nameof(Ids), "round")] // ADR-0047
    public void Round(string id) => Run("round", id);

    [Theory, MemberData(nameof(Ids), "iferror")] // ADR-0047
    public void IfError(string id) => Run("iferror", id);

    [Theory, MemberData(nameof(Ids), "iserror")] // ADR-0047
    public void IsError(string id) => Run("iserror", id);

    [Theory, MemberData(nameof(Ids), "xlookup")] // ADR-0047/0049
    public void XLookup(string id) => Run("xlookup", id);

    [Theory, MemberData(nameof(Ids), "and")] // ADR-0047, docs/specs/exsheet-functions ticket 01
    public void And(string id) => Run("and", id);

    [Theory, MemberData(nameof(Ids), "or")] // ADR-0047, docs/specs/exsheet-functions ticket 01
    public void Or(string id) => Run("or", id);

    [Theory, MemberData(nameof(Ids), "not")] // ADR-0047, docs/specs/exsheet-functions ticket 01
    public void Not(string id) => Run("not", id);

    [Theory, MemberData(nameof(Ids), "ifna")] // ADR-0047, docs/specs/exsheet-functions ticket 01
    public void IfNa(string id) => Run("ifna", id);

    [Theory, MemberData(nameof(Ids), "isblank")] // ADR-0047, docs/specs/exsheet-functions ticket 01
    public void IsBlank(string id) => Run("isblank", id);

    [Theory, MemberData(nameof(Ids), "isnumber")] // ADR-0047, docs/specs/exsheet-functions ticket 01
    public void IsNumber(string id) => Run("isnumber", id);

    [Theory, MemberData(nameof(Ids), "roundup")] // ADR-0047, docs/specs/exsheet-functions ticket 01
    public void RoundUp(string id) => Run("roundup", id);

    [Theory, MemberData(nameof(Ids), "rounddown")] // ADR-0047, docs/specs/exsheet-functions ticket 01
    public void RoundDown(string id) => Run("rounddown", id);

    [Theory, MemberData(nameof(Ids), "abs")] // ADR-0047, docs/specs/exsheet-functions ticket 01
    public void Abs(string id) => Run("abs", id);

    [Theory, MemberData(nameof(Ids), "int")] // ADR-0047, docs/specs/exsheet-functions ticket 01
    public void Int(string id) => Run("int", id);

    [Theory, MemberData(nameof(Ids), "mod")] // ADR-0047, docs/specs/exsheet-functions ticket 01
    public void Mod(string id) => Run("mod", id);

    [Theory, MemberData(nameof(Ids), "date")] // ADR-0047, docs/specs/exsheet-functions ticket 01
    public void Date(string id) => Run("date", id);

    [Theory, MemberData(nameof(Ids), "year")] // ADR-0047, docs/specs/exsheet-functions ticket 01
    public void Year(string id) => Run("year", id);

    [Theory, MemberData(nameof(Ids), "month")] // ADR-0047, docs/specs/exsheet-functions ticket 01
    public void Month(string id) => Run("month", id);

    [Theory, MemberData(nameof(Ids), "day")] // ADR-0047, docs/specs/exsheet-functions ticket 01
    public void Day(string id) => Run("day", id);

    [Theory, MemberData(nameof(Ids), "eomonth")] // ADR-0047, docs/specs/exsheet-functions ticket 01
    public void EoMonth(string id) => Run("eomonth", id);

    [Theory, MemberData(nameof(Ids), "edate")] // ADR-0047, docs/specs/exsheet-functions ticket 01
    public void EDate(string id) => Run("edate", id);

    [Theory, MemberData(nameof(Ids), "left")] // ADR-0047, docs/specs/exsheet-functions ticket 01
    public void Left(string id) => Run("left", id);

    [Theory, MemberData(nameof(Ids), "right")] // ADR-0047, docs/specs/exsheet-functions ticket 01
    public void Right(string id) => Run("right", id);

    [Theory, MemberData(nameof(Ids), "mid")] // ADR-0047, docs/specs/exsheet-functions ticket 01
    public void Mid(string id) => Run("mid", id);

    [Theory, MemberData(nameof(Ids), "len")] // ADR-0047, docs/specs/exsheet-functions ticket 01
    public void Len(string id) => Run("len", id);

    [Theory, MemberData(nameof(Ids), "trim")] // ADR-0047, docs/specs/exsheet-functions ticket 01
    public void Trim(string id) => Run("trim", id);

    [Theory, MemberData(nameof(Ids), "concat")] // ADR-0047, docs/specs/exsheet-functions ticket 01
    public void Concat(string id) => Run("concat", id);

    [Theory, MemberData(nameof(Ids), "index")] // ADR-0047, docs/specs/exsheet-functions ticket 01
    public void Index(string id) => Run("index", id);

    [Theory, MemberData(nameof(Ids), "text")] // ADR-0120: TEXT reads its code in the invariant spelling, as a cell format shows it
    public void Text(string id) => Run("text", id);

    [Theory, MemberData(nameof(Ids), "product")] // ADR-0047, docs/specs/exsheet-functions ticket 04
    public void Product(string id) => Run("product", id);

    [Theory, MemberData(nameof(Ids), "median")] // ADR-0047, docs/specs/exsheet-functions ticket 04
    public void Median(string id) => Run("median", id);

    [Theory, MemberData(nameof(Ids), "large")] // ADR-0047, docs/specs/exsheet-functions ticket 04
    public void Large(string id) => Run("large", id);

    [Theory, MemberData(nameof(Ids), "small")] // ADR-0047, docs/specs/exsheet-functions ticket 04
    public void Small(string id) => Run("small", id);

    [Theory, MemberData(nameof(Ids), "istext")] // ADR-0047, docs/specs/exsheet-functions ticket 04
    public void IsText(string id) => Run("istext", id);

    [Theory, MemberData(nameof(Ids), "isna")] // ADR-0047, docs/specs/exsheet-functions ticket 04
    public void IsNa(string id) => Run("isna", id);

    [Theory, MemberData(nameof(Ids), "na")] // ADR-0047, docs/specs/exsheet-functions ticket 04
    public void Na(string id) => Run("na", id);

    [Theory, MemberData(nameof(Ids), "ifs")] // ADR-0047, docs/specs/exsheet-functions ticket 04
    public void Ifs(string id) => Run("ifs", id);

    [Theory, MemberData(nameof(Ids), "switch")] // ADR-0047, docs/specs/exsheet-functions ticket 04
    public void Switch(string id) => Run("switch", id);

    [Theory, MemberData(nameof(Ids), "xmatch")] // ADR-0047, docs/specs/exsheet-functions ticket 04
    public void XMatch(string id) => Run("xmatch", id);

    [Theory, MemberData(nameof(Ids), "choose")] // ADR-0047, docs/specs/exsheet-functions ticket 04
    public void Choose(string id) => Run("choose", id);

    [Theory, MemberData(nameof(Ids), "row")] // ADR-0047, docs/specs/exsheet-functions ticket 04
    public void Row(string id) => Run("row", id);

    [Theory, MemberData(nameof(Ids), "column")] // ADR-0047, docs/specs/exsheet-functions ticket 04
    public void Column(string id) => Run("column", id);

    [Theory, MemberData(nameof(Ids), "trunc")] // ADR-0047, docs/specs/exsheet-functions ticket 04
    public void Trunc(string id) => Run("trunc", id);

    [Theory, MemberData(nameof(Ids), "power")] // ADR-0047, docs/specs/exsheet-functions ticket 04
    public void Power(string id) => Run("power", id);

    [Theory, MemberData(nameof(Ids), "sqrt")] // ADR-0047, docs/specs/exsheet-functions ticket 04
    public void Sqrt(string id) => Run("sqrt", id);

    [Theory, MemberData(nameof(Ids), "weekday")] // ADR-0047, docs/specs/exsheet-functions ticket 04
    public void Weekday(string id) => Run("weekday", id);

    [Theory, MemberData(nameof(Ids), "days")] // ADR-0047, docs/specs/exsheet-functions ticket 04
    public void Days(string id) => Run("days", id);

    [Theory, MemberData(nameof(Ids), "networkdays")] // ADR-0047, docs/specs/exsheet-functions ticket 04
    public void NetworkDays(string id) => Run("networkdays", id);

    [Theory, MemberData(nameof(Ids), "workday")] // ADR-0047, docs/specs/exsheet-functions ticket 04
    public void Workday(string id) => Run("workday", id);

    [Theory, MemberData(nameof(Ids), "textjoin")] // ADR-0047, docs/specs/exsheet-functions ticket 04
    public void TextJoin(string id) => Run("textjoin", id);

    [Theory, MemberData(nameof(Ids), "concatenate")] // ADR-0047, docs/specs/exsheet-functions ticket 04
    public void Concatenate(string id) => Run("concatenate", id);

    [Theory, MemberData(nameof(Ids), "substitute")] // ADR-0047, docs/specs/exsheet-functions ticket 04
    public void Substitute(string id) => Run("substitute", id);

    [Theory, MemberData(nameof(Ids), "replace")] // ADR-0047, docs/specs/exsheet-functions ticket 04
    public void Replace(string id) => Run("replace", id);

    [Theory, MemberData(nameof(Ids), "find")] // ADR-0047, docs/specs/exsheet-functions ticket 04
    public void Find(string id) => Run("find", id);

    [Theory, MemberData(nameof(Ids), "pmt")] // ADR-0047, docs/specs/exsheet-functions ticket 04
    public void Pmt(string id) => Run("pmt", id);

    [Theory, MemberData(nameof(Ids), "pv")] // ADR-0047, docs/specs/exsheet-functions ticket 04
    public void Pv(string id) => Run("pv", id);

    [Theory, MemberData(nameof(Ids), "fv")] // ADR-0047, docs/specs/exsheet-functions ticket 04
    public void Fv(string id) => Run("fv", id);

    [Theory, MemberData(nameof(Ids), "npv")] // ADR-0047, docs/specs/exsheet-functions ticket 04
    public void Npv(string id) => Run("npv", id);

    [Theory, MemberData(nameof(Ids), "rank-eq")] // ADR-0047, docs/specs/exsheet-functions ticket 05
    public void RankEq(string id) => Run("rank-eq", id);

    [Theory, MemberData(nameof(Ids), "stdev-s")] // ADR-0047, docs/specs/exsheet-functions ticket 05
    public void StdevS(string id) => Run("stdev-s", id);

    [Theory, MemberData(nameof(Ids), "stdev-p")] // ADR-0047, docs/specs/exsheet-functions ticket 05
    public void StdevP(string id) => Run("stdev-p", id);

    [Theory, MemberData(nameof(Ids), "var-s")] // ADR-0047, docs/specs/exsheet-functions ticket 05
    public void VarS(string id) => Run("var-s", id);

    [Theory, MemberData(nameof(Ids), "var-p")] // ADR-0047, docs/specs/exsheet-functions ticket 05
    public void VarP(string id) => Run("var-p", id);

    [Theory, MemberData(nameof(Ids), "xor")] // ADR-0047, docs/specs/exsheet-functions ticket 05
    public void Xor(string id) => Run("xor", id);

    [Theory, MemberData(nameof(Ids), "rows")] // ADR-0047, docs/specs/exsheet-functions ticket 05
    public void Rows(string id) => Run("rows", id);

    [Theory, MemberData(nameof(Ids), "columns")] // ADR-0047, docs/specs/exsheet-functions ticket 05
    public void Columns(string id) => Run("columns", id);

    [Theory, MemberData(nameof(Ids), "sign")] // ADR-0047, docs/specs/exsheet-functions ticket 05
    public void Sign(string id) => Run("sign", id);

    [Theory, MemberData(nameof(Ids), "exp")] // ADR-0047, docs/specs/exsheet-functions ticket 05
    public void Exp(string id) => Run("exp", id);

    [Theory, MemberData(nameof(Ids), "ln")] // ADR-0047, docs/specs/exsheet-functions ticket 05
    public void Ln(string id) => Run("ln", id);

    [Theory, MemberData(nameof(Ids), "log10")] // ADR-0047, docs/specs/exsheet-functions ticket 05
    public void Log10(string id) => Run("log10", id);

    [Theory, MemberData(nameof(Ids), "pi")] // ADR-0047, docs/specs/exsheet-functions ticket 05
    public void Pi(string id) => Run("pi", id);

    [Theory, MemberData(nameof(Ids), "time")] // ADR-0047, docs/specs/exsheet-functions ticket 05
    public void Time(string id) => Run("time", id);

    [Theory, MemberData(nameof(Ids), "hour")] // ADR-0047, docs/specs/exsheet-functions ticket 05
    public void Hour(string id) => Run("hour", id);

    [Theory, MemberData(nameof(Ids), "minute")] // ADR-0047, docs/specs/exsheet-functions ticket 05
    public void Minute(string id) => Run("minute", id);

    [Theory, MemberData(nameof(Ids), "second")] // ADR-0047, docs/specs/exsheet-functions ticket 05
    public void Second(string id) => Run("second", id);

    [Theory, MemberData(nameof(Ids), "rept")] // ADR-0047, docs/specs/exsheet-functions ticket 05
    public void Rept(string id) => Run("rept", id);

    [Theory, MemberData(nameof(Ids), "exact")] // ADR-0047, docs/specs/exsheet-functions ticket 05
    public void Exact(string id) => Run("exact", id);

    [Theory, MemberData(nameof(Ids), "numbervalue")] // ADR-0047, docs/specs/exsheet-functions ticket 05
    public void NumberValue(string id) => Run("numbervalue", id);

    [Theory, MemberData(nameof(Ids), "xnpv")] // ADR-0047, docs/specs/exsheet-functions ticket 05
    public void XNpv(string id) => Run("xnpv", id);

    [Theory, MemberData(nameof(Ids), "typed-constants")] // ADR-0048: constants read under the Sheet's culture
    public void TypedConstants(string id) => Run("typed-constants", id);

    [Theory, MemberData(nameof(Ids), "dates")] // ADR-0047/0048: Excel's 1900 serials
    public void Dates(string id) => Run("dates", id);

    [Theory, MemberData(nameof(Ids), "number-formats")] // ADR-0016/0046/0047: format codes as Excel renders them
    public void NumberFormats(string id) => Run("number-formats", id);

    [Theory, MemberData(nameof(Ids), "general-width")] // ADR-0047, SH-20: General fits its column; a typed number widens a default one
    public void GeneralWidth(string id) => Run("general-width", id);

    [Theory, MemberData(nameof(Ids), "format-levels")] // ADR-0047, SH-21: formats at cell, row and column level, cell over row over column
    public void FormatLevels(string id) => Run("format-levels", id);

    [Theory, MemberData(nameof(Ids), "column-widths")] // ADR-0046, SH-22: widths recorded per column, moved by insertion and deletion
    public void ColumnWidths(string id) => Run("column-widths", id);

    [Theory, MemberData(nameof(Ids), "formula-formats")] // ADR-0047: a Formula entered into a General cell takes a format as Excel's does
    public void FormulaFormats(string id) => Run("formula-formats", id);

    [Theory, MemberData(nameof(Ids), "structure")] // ADR-0046/0047: insertion and deletion rewrite References
    public void Structure(string id) => Run("structure", id);

    [Theory, MemberData(nameof(Ids), "fill")] // ADR-0050: fill as Excel's, or refused
    public void Fill(string id) => Run("fill", id);

    [Theory, MemberData(nameof(Ids), "copy")] // ADR-0048: copy and paste shift relative References
    public void Copy(string id) => Run("copy", id);

    [Theory, MemberData(nameof(Ids), "linked-tables")] // ADR-0049: Linked Tables read by structured reference and by key
    public void LinkedTables(string id) => Run("linked-tables", id);

    private static readonly string[] Read =
    [
        "abs", "and", "arithmetic", "average", "choose", "column", "column-widths", "columns", "concat", "concatenate", "copy", "count", "counta", "date", "dates", "day", "days", "edate", "eomonth", "errors", "exact", "exp", "fill", "find", "format-levels", "formula-formats", "formula-text", "fv", "general-width", "hour", "if", "iferror", "ifna", "ifs", "index", "int", "isblank", "iserror", "isna", "isnumber", "istext", "large", "left", "len", "linked-tables", "ln", "log10", "max", "median", "mid", "min", "minute", "mod", "month", "na", "networkdays", "not", "npv", "number-formats", "numbervalue", "or", "pi", "pmt", "power", "product", "pv", "rank-eq", "references", "replace", "rept", "right", "round", "rounddown", "roundup", "row", "rows", "second", "sheet-names", "sign", "small", "sqrt", "stdev-p", "stdev-s", "structure", "substitute", "sum", "switch", "text", "textjoin", "time", "trim", "trunc", "typed-constants", "var-p", "var-s", "weekday", "workday", "xlookup", "xmatch", "xnpv", "xor", "year",
    ];

    [Fact] // ADR-0047: a corpus file no theory reads would be a set of cases that silently never runs
    public void Every_corpus_file_is_read_by_a_theory()
    {
        Assert.Equal(Read, ExcelCorpus.AreaFiles);
    }

    [Fact] // ADR-0047: ids are stable and unique, every case says where its answer came from, and every difference names its decision
    public void Every_case_is_well_formed()
    {
        var all = Read.SelectMany(ExcelCorpus.Cases).ToList();

        Assert.Equal(all.Count, all.Select(c => c.GetProperty("id").GetString()).Distinct(StringComparer.Ordinal).Count());
        Assert.All(all, c =>
        {
            var id = c.GetProperty("id").GetString()!;
            Assert.Matches("^[A-Z0-9]+-[0-9]{3}$", id);
            Assert.Contains(c.GetProperty("source").GetString(), new[] { "documented", "observed", "uncertain" });
            Assert.False(string.IsNullOrWhiteSpace(c.GetProperty("description").GetString()), id);
            Assert.True(c.GetProperty("expect").EnumerateObject().Any(), id);
            if (c.TryGetProperty("engineDiffersByDecision", out var why)) Assert.StartsWith("ADR-", why.GetString(), StringComparison.Ordinal);
            if (c.TryGetProperty("excelExpect", out _)) Assert.True(c.TryGetProperty("engineDiffersByDecision", out _), id);
        });
    }

    [Fact] // ADR-0047: every row of ticket 19 is in the corpus, as uncertain until Excel answers it
    public void Every_row_of_ticket_19_is_a_case()
    {
        var rows = Read.SelectMany(ExcelCorpus.Cases)
            .Where(c => c.TryGetProperty("ticket19Row", out _))
            .ToList();

        Assert.Equal(Enumerable.Range(1, 35), rows.Select(c => c.GetProperty("ticket19Row").GetInt32()).Distinct().Order());
        Assert.All(rows, c => Assert.NotEqual("documented", c.GetProperty("source").GetString()));
    }
}
