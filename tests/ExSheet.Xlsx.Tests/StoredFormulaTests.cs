using Xunit;

namespace ExSheet.Xlsx.Tests;

/// <summary>A Formula's text as an .xlsx stores it, read into the text the engine reads (ADR-0330 §4).</summary>
public class StoredFormulaTests
{
    [Theory] // ADR-0330 §4: _xlfn., _xlws. and _xlpm. are taken off outside quotes; quoted text and Sheet names are kept
    [InlineData("_xlfn.XLOOKUP(A1,B:B,C:C)", "=XLOOKUP(A1,B:B,C:C)")]
    [InlineData("_xlfn._xlws.FILTER(A1:A9,A1:A9>0)", "=FILTER(A1:A9,A1:A9>0)")]
    [InlineData("_XLFN.CONCAT(\"_xlfn.\",'_xlfn. Sheet'!A1)", "=CONCAT(\"_xlfn.\",'_xlfn. Sheet'!A1)")]
    [InlineData("_xlfn.LET(_xlpm.rate,0.1,_xlpm.rate*2)", "=LET(rate,0.1,rate*2)")]
    [InlineData("my_xlfn.X(1)", "=my_xlfn.X(1)")]
    [InlineData("SUM(_xlfn.ANCHORARRAY(A1))", "=SUM(A1#)")]
    [InlineData("_xlfn.ANCHORARRAY('My Sheet'!$B$2)*2", "='My Sheet'!$B$2#*2")]
    [InlineData("\"don''t\"&\"[1]\"", "=\"don''t\"&\"[1]\"")]
    public void ADR0330_stored_text_is_read_as_the_engine_reads_it(string stored, string read)
    {
        Assert.Equal(read, StoredFormula.Read(stored, out var problem));
        Assert.Equal(StoredFormulaProblem.None, problem);
    }

    [Theory] // ADR-0330 §4: what the engine cannot reach is told apart before it reads
    [InlineData("[1]Sheet1!A1", "OtherWorkbook")]
    [InlineData("'[2]My Sheet'!A1", "OtherWorkbook")]
    [InlineData("[3]!Rate", "OtherWorkbook")]
    [InlineData("Table1[Amount]", "ExcelTable")]
    [InlineData("SUM(Table1[[#Totals],[Amount]])", "ExcelTable")]
    [InlineData("[@Amount]*2", "ExcelTable")]
    [InlineData("_xlfn.SINGLE(A:A)", "ImplicitIntersection")]
    [InlineData("_xlfn.ANCHORARRAY(A1:B2)", "SpillRange")]
    [InlineData("_xlfn.ANCHORARRAY(OFFSET(A1,1,1))", "SpillRange")]
    public void ADR0330_what_the_engine_cannot_reach_is_told_apart(string stored, string expected)
    {
        StoredFormula.Read(stored, out var problem);

        Assert.Equal(expected, problem.ToString());
    }
}
