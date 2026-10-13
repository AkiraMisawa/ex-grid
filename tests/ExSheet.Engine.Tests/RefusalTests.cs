using System.Text.Json;
using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// ADR-0047 (2026-10-12, decided with the user: a refusal is never caught, anywhere). Where ExSheet cannot
/// answer as Excel — the readings of an evaluation part, or no run has settled the answer — it refuses with
/// <c>#VALUE!</c>, and the refusal is the Formula's: <c>IFERROR</c>, <c>ISERROR</c>, <c>ERROR.TYPE</c>,
/// <c>IFNA</c>, <c>COUNT</c>, <c>COUNTA</c> and the aggregates that pass over Error Values do not turn it into
/// an answer, in its own Formula or in any Formula that reads the refused cell, directly, through a range, a
/// <c>LET</c> name, <c>INDIRECT</c>, <c>OFFSET</c>, a Spill Range or a Linked Table, as a Formula that reads a
/// cycle is <c>#CIRC!</c>. A <c>#VALUE!</c> that is Excel's own answer, <c>=1+"a"</c>'s, stays an Error Value
/// any Formula can catch. The corpus cases that ask Excel what it gives over a refused cell are IFERROR-018 to
/// 020, ISERROR-011, ERRORTYPE-022, IFNA-008, COUNT-009, COUNTA-009, AGGREGATE-069, REF-015, LET-113,
/// INDIRECT-100 and SEQUENCE-009.
/// </summary>
public class RefusalTests
{
    /// <summary>The cells every refusal kind below reads.</summary>
    private static Sheet WithData()
    {
        var sheet = NewSheet();
        // A Sheet without a seed waits before it draws (ADR-0180), and a wait comes before a refusal.
        sheet.SetSeed(20261012);
        sheet.Enter("A1", "a");
        sheet.Enter("A2", "b");
        sheet.Enter("D1", "1");
        sheet.Enter("D3", "2");
        foreach (var (address, number) in new[] { ("G1", "30"), ("G2", "10"), ("G3", "20"), ("G4", "50"), ("G5", "40") }) sheet.Enter(address, number);
        foreach (var (address, number) in new[] { ("H1", "1"), ("H2", "2"), ("H3", "3"), ("H4", "4"), ("H5", "5") }) sheet.Enter(address, number);
        sheet.Enter("J1", "100");
        sheet.Enter("J2", "200");
        sheet.Enter("K1", "45000");
        sheet.Enter("K2", "45100");
        return sheet;
    }

    /// <summary>One Formula for each kind of refusal the engine makes, each read with <see cref="WithData"/>.</summary>
    public static TheoryData<string> Refusing =>
    [
        "LEN(A1:A2)",                       // a range where one Value is wanted (ADR-0125)
        "AVERAGE(UNIQUE(D1:D3))",           // a kept blank read where no run settled how (ADR-0125)
        "MOD(-1E-17,1)",                    // MOD's evaluations part (MOD-033)
        "MROUND(6.05,0.1)",                 // a fractional multiple (MROUND-001)
        "CEILING.MATH(0.3,0.1)",            // a quotient integral at 15 digits only
        "RANDBETWEEN(1.1,1.9)",             // a fractional bound (ADR-0180)
        "COUNTIF(A1:A2,\">1,5\")",          // a culture-sensitive criterion
        "UPPER(\"\U00010428\")",            // a character outside the observed casing domain
        "SEARCH(\"\U00010428\",\"a\U00010428\")", // the same, searched
        "TEXT(4.34,\"# ?/?\")",             // a code outside the cell formats' subset (TEXTFN-019)
        "DATEDIF(1,40,\"MD\")",             // DATEDIF's MD
        "EDATE(45000,TRUE)",                // a boolean as EDATE's months (EDATE-023)
        "EDATE(0,0)",                       // EDATE from serial 0 to January 1900 (EDATE-019)
        "WORKDAY(TRUE,1)",                  // a boolean typed as a date (WORKDAY-011)
        "PMT(0.1,2,1000,-1210)",            // PMT's readings magnify their roundings (PMT-049)
        "PV(-1,10,-100)",                   // PV at a rate of -1 (PV-013)
        "XNPV(0,J1:J2,K1:K2)",              // XNPV at a rate of 0 (XNPV-019)
        "XLOOKUP(25,G1:G5,H1:H5,,0,2)",     // a binary search over unsorted data
        "MATCH(25,G1:G5,1)",                // an approximate search over unsorted data
        "INDIRECT(\"A1:A2#\")",             // a # after a range (INDIRECT-092)
        "AGGREGATE(16,6,G1:G5,0.5)",        // a function number the engine does not support
    ];

    [Theory, MemberData(nameof(Refusing))] // ADR-0047 (2026-10-12): each kind of refusal is the Formula's, which nothing in it can catch
    public void ADR0047_every_kind_of_refusal_is_the_formulas(string refusing)
    {
        var sheet = WithData();

        Assert.True(sheet.Evaluate("=" + refusing) is { IsRefused: true }, refusing);
        Assert.True(sheet.Evaluate($"=IFERROR({refusing},0)") is { IsRefused: true }, refusing);
        Assert.True(sheet.Evaluate($"=ISERROR({refusing})") is { IsRefused: true }, refusing);
        Assert.True(sheet.Evaluate($"=ERROR.TYPE({refusing})") is { IsRefused: true }, refusing);
        Assert.True(sheet.Evaluate($"=COUNTA({refusing},1)") is { IsRefused: true }, refusing);
    }

    [Theory, MemberData(nameof(Refusing))] // ADR-0047 (2026-10-12): a refused cell refuses every Formula that reads it, whatever it does with it
    public void ADR0047_a_refused_cell_refuses_the_formula_that_reads_it(string refusing)
    {
        var sheet = WithData();
        sheet.Enter("B1", "=" + refusing);

        Assert.True(sheet.Value("B1") is { IsRefused: true }, refusing);
        Assert.True(sheet.Evaluate("=IFERROR(B1,0)") is { IsRefused: true }, refusing);
        Assert.True(sheet.Evaluate("=ISERROR(B1)") is { IsRefused: true }, refusing);
    }

    /// <summary>What reads B1, refused (<c>MROUND(6.05,0.1)</c>), beside B2 holding 1.</summary>
    public static TheoryData<string> Readers =>
    [
        "=B1",
        "=B1+1",
        "=IFERROR(B1,0)",
        "=IFERROR(B1+1,0)",
        "=ISERROR(B1)",
        "=ISNUMBER(B1)",
        "=ISBLANK(B1)",
        "=ISNA(B1)",
        "=ERROR.TYPE(B1)",
        "=IFNA(B1,0)",
        "=IF(ISERROR(B1),0,1)",
        "=IF(TRUE,1,B1)",
        "=COUNT(B1:B2)",
        "=COUNTA(B1:B2)",
        "=COUNTBLANK(B1:B2)",
        "=SUM(B1:B2)",
        "=SUM(A1:C9)",
        "=AGGREGATE(9,6,B1:B2)",
        "=AGGREGATE(14,6,B1:B2,1)",
        "=SUBTOTAL(9,B1:B2)",
        "=SUMIF(B1:B2,\">0\")",
        "=COUNTIF(B1:B2,\"#VALUE!\")",
        "=ROWS(B1:B2)",
        "=XLOOKUP(1,B2:B3,B1:B2,0)",
        "=LET(x,B1,IFERROR(x,0))",
        "=IFERROR(INDIRECT(\"B1\"),0)",
        "=COUNT(INDIRECT(\"B1:B2\"))",
        "=IFERROR(OFFSET(B2,-1,0),0)",
        "=IFERROR(INDEX(B1:B2,1),0)",
        "=IFERROR(SUM(B1#),0)",
    ];

    [Theory, MemberData(nameof(Readers))] // ADR-0047 (2026-10-12): directly, through a range, a name, INDIRECT, OFFSET, INDEX or a Spill Range
    public void ADR0047_no_formula_catches_a_refused_cell(string formula)
    {
        var sheet = NewSheet();
        sheet.Enter("B1", "=MROUND(6.05,0.1)");
        sheet.Enter("B2", "1");

        Assert.True(sheet.Evaluate(formula) is { IsRefused: true }, formula);
    }

    [Fact] // ADR-0047 (2026-10-12): the refusal is carried along a chain of three cells, and lifted when its cause goes
    public void ADR0047_a_refusal_is_carried_along_a_chain_and_lifted_with_its_cause()
    {
        var sheet = NewSheet();
        sheet.Enter("B1", "=MROUND(6.05,0.1)");
        sheet.Enter("C1", "=IFERROR(B1,0)");
        sheet.Enter("D1", "=IFERROR(C1,1)");
        sheet.Enter("E1", "=COUNTA(D1)");

        foreach (var cell in new[] { "B1", "C1", "D1", "E1" }) Assert.True(sheet.Value(cell) is { IsRefused: true }, cell);
        Assert.Equal("#VALUE!", sheet.Value("E1")!.Value.ToString());

        sheet.Enter("B1", "=MROUND(6,2)");

        Assert.Equal(6, sheet.Number("B1"));
        Assert.Equal(6, sheet.Number("C1"));
        Assert.Equal(6, sheet.Number("D1"));
        Assert.Equal(1, sheet.Number("E1"));
    }

    [Fact] // ADR-0047: a #VALUE! that is Excel's own answer stays an Error Value a Formula can catch, here and in another cell
    public void ADR0047_excels_own_value_error_is_still_caught()
    {
        var sheet = NewSheet();
        sheet.Enter("B1", "=1+\"a\"");

        Assert.False(sheet.Value("B1") is { IsRefused: true });
        Assert.Equal(ErrorValue.Value, sheet.Error("B1"));
        Assert.Equal(0, sheet.Evaluate("=IFERROR(1+\"a\",0)").Number);
        Assert.Equal(0, sheet.Evaluate("=IFERROR(B1,0)").Number);
        Assert.True(sheet.Evaluate("=ISERROR(B1)").Boolean);
        Assert.Equal(3, sheet.Evaluate("=ERROR.TYPE(B1)").Number);
        Assert.Equal(1, sheet.Evaluate("=COUNTA(B1)").Number);
    }

    [Fact] // ADR-0047 (2026-10-12): a Formula that reads a refused cell on demand, through INDIRECT, is refused; one that refused first does not refuse the cell it computes on demand
    public void ADR0047_a_refusal_read_on_demand_is_carried_and_kept_apart()
    {
        var sheet = NewSheet();
        sheet.Enter("C1", "=ISERROR(INDIRECT(\"B1\"))");
        sheet.Enter("C2", "=MROUND(6.05,0.1)+INDIRECT(\"B2\")");
        sheet.Enter("B1", "=MROUND(6.05,0.1)");
        sheet.Enter("B2", "=1+1");

        Assert.True(sheet.Value("C1") is { IsRefused: true });
        Assert.True(sheet.Value("C2") is { IsRefused: true });
        Assert.Equal(2, sheet.Number("B2"));
    }

    [Fact] // ADR-0047, ADR-0049 (2026-10-12): a refusal the Consumer pushes into a Linked Table refuses every Formula that reads its column
    public void ADR0047_a_refusal_in_a_linked_table_refuses_its_readers()
    {
        var sheet = NewSheet();
        sheet.Enter("B1", "=MROUND(6.05,0.1)");
        sheet.DeclareLinkedTable("Positions", ["Id", "PV"], key: "Id");
        sheet.PushLinkedTable("Positions", [[Value.FromText("R-1"), sheet.Value("B1")], [Value.FromText("R-2"), Value.FromNumber(5)]]);
        sheet.Enter("C1", "=COUNTA(Positions[PV])");
        sheet.Enter("C2", "=IFERROR(XLOOKUP(\"R-2\",Positions[Id],Positions[PV]),0)");
        sheet.Enter("C3", "=COUNT(INDIRECT(\"Positions[PV]\"))");
        sheet.Enter("C4", "=COUNTA(Positions[Id])");

        Assert.True(sheet.Value("C1") is { IsRefused: true });
        Assert.True(sheet.Value("C2") is { IsRefused: true });
        Assert.True(sheet.Value("C3") is { IsRefused: true });
        // A column that holds none is read as ever.
        Assert.Equal(2, sheet.Number("C4"));

        sheet.PushLinkedTable("Positions", [[Value.FromText("R-1"), Value.FromNumber(4)], [Value.FromText("R-2"), Value.FromNumber(5)]]);

        Assert.Equal(2, sheet.Number("C1"));
        Assert.Equal(5, sheet.Number("C2"));
        Assert.Equal(2, sheet.Number("C3"));
    }

    [Fact] // ADR-0047, ADR-0049 (2026-10-12): a cycle comes before a refusal, and a wait before a refusal
    public void ADR0047_a_cycle_and_a_wait_come_before_a_refusal()
    {
        var sheet = NewSheet();
        sheet.Enter("B1", "=MROUND(6.05,0.1)");
        sheet.Enter("D1", "=D2");
        sheet.Enter("D2", "=D1");
        sheet.DeclareLinkedTable("Rates", ["Pair", "Mid"]);

        Assert.Equal(ErrorValue.Circ, sheet.Evaluate("=B1+D1").Error);
        Assert.Equal(ErrorValue.GettingData, sheet.Evaluate("=B1+SUM(Rates[Mid])").Error);
    }

    [Fact] // ADR-0047 (2026-10-12): outside the engine a refusal is #VALUE!: shown, written and compared as one
    public void ADR0047_a_refusal_is_value_error_outside_the_engine()
    {
        var sheet = NewSheet();
        sheet.Enter("B1", "=MROUND(6.05,0.1)");
        var refused = sheet.Value("B1")!.Value;

        Assert.Equal(ErrorValue.Value, refused.Error);
        Assert.Equal("#VALUE!", refused.ToString());
        Assert.Equal("#VALUE!", sheet.GetDisplay(CellAddress.Parse("B1")).Text);
        Assert.Equal(Value.FromError(ErrorValue.Value), refused);
        Assert.Equal(Value.FromError(ErrorValue.Value).GetHashCode(), refused.GetHashCode());
    }

    [Fact] // ADR-0047 (2026-10-12): a refused cell's #VALUE! is no constant, which a Formula could catch; Excel's own #VALUE! is one
    public void ADR0047_a_refusal_is_no_constant_entry()
    {
        var sheet = NewSheet();
        sheet.Enter("B1", "=MROUND(6.05,0.1)");

        Assert.Throws<ArgumentException>(() => Entry.FromValue(sheet.Value("B1")!.Value));
        Assert.Equal(ErrorValue.Value, Entry.FromValue(Value.FromError(ErrorValue.Value)).Constant!.Value.Error);
    }

    [Fact] // ADR-0047, ADR-0048 (2026-10-12): a copy writes a refused cell as #VALUE!, and carries its Formula, which a paste refuses again where it still refuses
    public void ADR0047_a_copy_carries_the_refused_formula_and_writes_value_error()
    {
        var sheet = NewSheet();
        sheet.Enter("B1", "=MROUND(6.05,0.1)");
        sheet.Enter("B2", "=IFERROR($B$1,0)");

        var copy = sheet.Copy(CellRange.Parse("B1:B2"));

        Assert.Equal("#VALUE!\r\n#VALUE!\r\n", copy.Text);
        sheet.Do(SheetEdit.Paste(copy.Block!, CellAddress.Parse("D1")));
        Assert.Equal("=MROUND(6.05,0.1)", sheet.GetEntry(CellAddress.Parse("D1"))!.Formula);
        Assert.True(sheet.Value("D1") is { IsRefused: true });
        Assert.True(sheet.Value("D2") is { IsRefused: true });
    }

    [Fact] // ADR-0047 (2026-10-12): the corpus and the engine agree on which #VALUE! is a refusal
    public void ADR0047_every_refusal_in_the_corpus_is_the_formulas_and_every_excel_value_error_is_not()
    {
        // Refused although Excel was seen to give #VALUE! too: the engine cannot tell a pattern PCRE2
        // rejects from one it reads otherwise, so every pattern PortableRegex does not take is refused.
        var refusedWhereExcelGaveValueError = new HashSet<string>(StringComparer.Ordinal) { "XLOOKUP-103", "XLOOKUP-157" };
        var wrong = new List<string>();
        foreach (var area in ExcelCorpus.AreaFiles)
        {
            foreach (var c in ExcelCorpus.Cases(area))
            {
                if (!c.GetProperty("expect").TryGetProperty("value2", out var value2)) continue;
                if (value2.ValueKind != JsonValueKind.String || value2.GetString() != "#VALUE!") continue;
                var id = c.GetProperty("id").GetString()!;
                // The engine's #VALUE! is a refusal where no run has observed Excel's answer, or where it differs from it by decision.
                var refusal = c.GetProperty("source").GetString() == "uncertain" || c.TryGetProperty("engineDiffersByDecision", out _)
                    || refusedWhereExcelGaveValueError.Contains(id);
                if ((ExcelCorpus.CheckedValue(c) is { IsRefused: true }) != refusal) wrong.Add($"{id}: expected {(refusal ? "a refusal" : "an Error Value a Formula can catch")}");
            }
        }

        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }
}
