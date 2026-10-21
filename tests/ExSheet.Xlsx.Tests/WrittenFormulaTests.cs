using System.Globalization;
using ExSheet.Engine;
using Xunit;

namespace ExSheet.Xlsx.Tests;

/// <summary>
/// A Formula written as Excel stores it (ADR-0330 §4): its prefixes put back where Excel writes them and nowhere else, and read
/// back by the reader as the same Formula.
/// </summary>
public class WrittenFormulaTests
{
    private static string Written(string formula)
    {
        var text = WrittenFormula.Write(Entry.Parse(formula, CultureInfo.InvariantCulture)!, null, out var problem);
        Assert.Equal(WrittenFormulaProblem.None, problem);
        return text;
    }

    [Theory] // ADR-0330 §4: _xlfn., _xlfn._xlws., _xlpm. and ANCHORARRAY are put back where Excel stores them, and nowhere else
    [InlineData("=SUM(A1:B2)", "SUM(A1:B2)")]
    [InlineData("=XLOOKUP(A1,B:B,C:C)", "_xlfn.XLOOKUP(A1,B:B,C:C)")]
    [InlineData("=STDEV.S(A1:A9)+VAR.P(A1:A9)", "_xlfn.STDEV.S(A1:A9)+_xlfn.VAR.P(A1:A9)")]
    [InlineData("=FILTER(A1:A9,B1:B9>0)", "_xlfn._xlws.FILTER(A1:A9,B1:B9>0)")]
    [InlineData("=SORT(UNIQUE(A1:A9))", "_xlfn._xlws.SORT(_xlfn.UNIQUE(A1:A9))")]
    [InlineData("=SUM(A1#)", "SUM(_xlfn.ANCHORARRAY(A1))")]
    [InlineData("=COUNT(Sheet2!$B$3#)", "COUNT(_xlfn.ANCHORARRAY(Sheet2!$B$3))")]
    [InlineData("=SUM('My Sheet'!A1#)*2", "SUM(_xlfn.ANCHORARRAY('My Sheet'!A1))*2")]
    [InlineData("=LET(x,1,x+1)", "_xlfn.LET(_xlpm.x,1,_xlpm.x+1)")]
    [InlineData("=LET(rate,0.05,amount,A1,amount*(1+rate))", "_xlfn.LET(_xlpm.rate,0.05,_xlpm.amount,A1,_xlpm.amount*(1+_xlpm.rate))")]
    [InlineData("=LET(x,1,LET(y,x+1,x*y))", "_xlfn.LET(_xlpm.x,1,_xlfn.LET(_xlpm.y,_xlpm.x+1,_xlpm.x*_xlpm.y))")]
    [InlineData("=LET(x,SEQUENCE(3),SUM(x))", "_xlfn.LET(_xlpm.x,_xlfn.SEQUENCE(3),SUM(_xlpm.x))")]
    [InlineData("=\"_xlfn.SEQUENCE(1) and LET(x,1,x)\"&SEQUENCE(1)", "\"_xlfn.SEQUENCE(1) and LET(x,1,x)\"&_xlfn.SEQUENCE(1)")]
    [InlineData("=IFS(A1>1E+5,\"big\",TRUE,\"small\")", "_xlfn.IFS(A1>100000,\"big\",TRUE,\"small\")")]
    [InlineData("=SUM(1:3)+SUM($A:$C)", "SUM(1:3)+SUM($A:$C)")]
    [InlineData("=Sheet2!#REF!+#N/A", "Sheet2!#REF!+#N/A")]
    [InlineData("=LOG10(100)+XOR(TRUE,FALSE)", "LOG10(100)+_xlfn.XOR(TRUE,FALSE)")]
    [InlineData("=CONCAT(\"a\",\"\"\"b\"\"\")", "_xlfn.CONCAT(\"a\",\"\"\"b\"\"\")")]
    public void ADR0330_a_formula_is_written_with_excels_stored_prefixes(string formula, string stored)
    {
        Assert.Equal(stored, Written(formula));
    }

    [Theory] // ADR-0330 §4: a function the engine does not declare is written with its prefix too, so Excel computes it
    [InlineData("=TEXTSPLIT(A1,\",\")", "_xlfn.TEXTSPLIT(A1,\",\")")]
    [InlineData("=LAMBDA(x,y,x+y)(1,2)", null)]
    [InlineData("=MAP(A1:A3,LAMBDA(v,v*2))", "_xlfn.MAP(A1:A3,_xlfn.LAMBDA(_xlpm.v,_xlpm.v*2))")]
    public void ADR0330_an_undeclared_function_is_written_with_its_prefix(string formula, string? stored)
    {
        Entry entry;
        try
        {
            entry = Entry.Parse(formula, CultureInfo.InvariantCulture)!;
        }
        catch (FormulaSyntaxException)
        {
            Assert.Null(stored);
            return;
        }
        Assert.Equal(stored, WrittenFormula.Write(entry, null, out _));
    }

    [Theory] // ADR-0330 §4: what is written reads back as the same Formula
    [InlineData("=XLOOKUP(A1,B:B,C:C,\"none\")")]
    [InlineData("=LET(x,SEQUENCE(3),y,x*2,SUM(x,y))")]
    [InlineData("=SUM(Sheet2!A1#)+SORTBY(A1:A3,B1:B3,-1)")]
    [InlineData("=FILTER(A1:B9,(B1:B9>0)*(A1:A9<>\"\"),\"none\")")]
    [InlineData("=IFERROR(1/0,NA())&TEXTJOIN(\",\",TRUE,A1:A3)")]
    public void ADR0330_a_written_formula_reads_back_as_itself(string formula)
    {
        var stored = Written(formula);

        var read = StoredFormula.Read(stored, out var problem);

        Assert.Equal(StoredFormulaProblem.None, problem);
        Assert.Equal(formula, read);
    }

    [Fact] // ADR-0330 §6: a structured reference has no table to read in the file written, and is not written
    public void ADR0330_a_structured_reference_is_not_written()
    {
        WrittenFormula.Write(Entry.Parse("=SUM(Positions[PV])", CultureInfo.InvariantCulture)!, null, out var problem);

        Assert.Equal(WrittenFormulaProblem.StructuredReference, problem);
    }

    [Fact] // ADR-0330 §4: a name a LET binds is prefixed only where it is bound; read outside the LET it is a name of its own, unprefixed
    public void ADR0330_a_name_is_prefixed_only_where_a_let_binds_it()
    {
        Assert.Equal("_xlfn.LET(_xlpm.x,1,_xlpm.x)+x", Written("=LET(x,1,x)+x"));
    }

    [Fact] // ADR-0330 §4: where the names this writer finds bound are not the ones the engine bound, the Formula is refused rather than written with a prefix in the wrong place
    public void ADR0330_a_formula_whose_names_bind_otherwise_than_the_engine_binds_them_is_refused()
    {
        // A LET's name read in its own value is not yet bound to the engine; this writer binds a name from where it stands.
        var entry = Entry.Parse("=LET(x,x+1,x)", CultureInfo.InvariantCulture)!;
        Assert.Equal(["x"], entry.UnboundNames);

        WrittenFormula.Write(entry, null, out var problem);

        Assert.Equal(WrittenFormulaProblem.Unreadable, problem);
    }

    [Fact] // ADR-0330 §4: every function the engine declares that Excel stores with a prefix is in the writer's list
    public void ADR0330_every_newer_declared_function_is_written_with_its_prefix()
    {
        string[] newer =
        [
            "ACOT", "ACOTH", "AGGREGATE", "CEILING.MATH", "CONCAT", "COT", "COTH", "CSC", "CSCH", "DAYS", "FILTER", "FLOOR.MATH", "IFNA",
            "IFS", "LET", "MAXIFS", "MINIFS", "NUMBERVALUE", "RANK.EQ", "SEC", "SECH", "SEQUENCE", "SORT", "SORTBY", "STDEV.P", "STDEV.S",
            "SWITCH", "TEXTJOIN", "UNIQUE", "VAR.P", "VAR.S", "XLOOKUP", "XMATCH", "XOR",
        ];

        Assert.Equal(newer, DeclaredFunction.All.Select(f => f.Name).Where(WrittenFormula.Newer.Contains).Order(StringComparer.Ordinal));
    }
}
