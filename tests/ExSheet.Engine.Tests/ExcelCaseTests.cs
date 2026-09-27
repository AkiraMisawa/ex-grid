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

    [Theory, MemberData(nameof(Ids), "typed-constants")] // ADR-0048: constants read under the Sheet's culture
    public void TypedConstants(string id) => Run("typed-constants", id);

    [Theory, MemberData(nameof(Ids), "dates")] // ADR-0047/0048: Excel's 1900 serials
    public void Dates(string id) => Run("dates", id);

    [Theory, MemberData(nameof(Ids), "number-formats")] // ADR-0016/0046/0047: format codes as Excel renders them
    public void NumberFormats(string id) => Run("number-formats", id);

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
        "arithmetic", "average", "copy", "count", "counta", "dates", "errors", "fill", "formula-text", "if", "iferror", "iserror",
        "linked-tables", "max", "min", "number-formats", "references", "round", "sheet-names", "structure", "sum", "typed-constants", "xlookup",
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
