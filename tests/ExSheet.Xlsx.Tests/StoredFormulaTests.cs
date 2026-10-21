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

    [Theory] // ADR-0330 §4: ISO 8601 date and time text is its 1900 serial, read invariantly, an offset converting nothing
    [InlineData("2023-03-15", 45000.0)]
    [InlineData("2023-03-15T12:00:00", 45000.5)]
    [InlineData("2023-03-15T12:00", 45000.5)]
    [InlineData("2023-03-15T12:00:00Z", 45000.5)]
    [InlineData("2023-03-15T12:00:00+09:00", 45000.5)]
    [InlineData("2023-03-15T00:00:00.5", 45000 + 0.5 / 86400)]
    [InlineData("06:00:00", 0.25)]
    [InlineData("1900-02-28", 59.0)]
    [InlineData("1900-03-01", 61.0)]
    public void ADR0330_ISO_dates_are_their_serials(string text, double serial)
    {
        Assert.Equal(serial, IsoDate.Serial(text));
    }

    [Theory] // ADR-0330 §4: text that is not an ISO 8601 date or time, or lies before 1900, is no serial
    [InlineData("")]
    [InlineData("15/03/2023")]
    [InlineData("2023-03-15 12:00:00")]
    [InlineData("2023-02-29")]
    [InlineData("2023-13-01")]
    [InlineData("1899-12-31")]
    [InlineData("24:00:00")]
    [InlineData("12:60")]
    [InlineData("Z")]
    [InlineData("March 15, 2023")]
    public void ADR0330_text_that_is_not_ISO_is_no_serial(string text)
    {
        Assert.Null(IsoDate.Serial(text));
    }
}
