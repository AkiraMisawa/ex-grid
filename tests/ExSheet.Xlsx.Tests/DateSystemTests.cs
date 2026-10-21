using ExGrid.Data.Xlsx;
using ExSheet.Engine;
using Xunit;
using static ExSheet.Xlsx.Tests.Book;

namespace ExSheet.Xlsx.Tests;

/// <summary>A workbook on the 1904 date system has its date serials moved to 1900's, which changes no date (ADR-0330 §4).</summary>
public class DateSystemTests
{
    /// <summary>Cell formats: 0 General, 1 built-in date (14), 2 built-in time (21), 3 a custom date, 4 a duration, 5 a custom time, 6 a number, 7 <c>[h]:mm</c>, 8 <c>[mm]:ss</c>.</summary>
    private static readonly XlsxStyles Styles = XlsxStyles.Default with
    {
        NumberFormats = new Dictionary<int, string> { [164] = "dd/mm/yyyy hh:mm", [165] = "[h]:mm:ss", [166] = "mm:ss.0", [167] = "0.00E+00", [168] = "[h]:mm", [169] = "[mm]:ss" },
        CellFormats = [new(), new(14), new(21), new(164), new(165), new(166), new(167), new(168), new(169)],
    };

    private static async Task<SheetDocument> ReadAsync(bool date1904, params XlsxCell[] cells)
    {
        var file = await WriteAsync(new XlsxWriteOptions { Styles = Styles, Date1904 = date1904 }, new Page("Sheet1", cells));
        return (await Book.ReadAsync(file)).Document.Sheet("Sheet1");
    }

    [Fact] // ADR-0330 §4: a date's serial on the 1904 system is moved by 1,462 days, to the same day on 1900's
    public async Task ADR0330_1904_date_serials_are_moved_to_1900s()
    {
        // 43,538 on the 1904 system is 15 March 2023, which is 45,000 on 1900's.
        var sheet = await ReadAsync(true, Number("A1", "43538", style: 1), Number("A2", "43538.5", style: 3), Number("A3", "0", style: 1));

        Assert.Equal(Value.FromNumber(45000), sheet.EntryAt("A1")!.Constant);
        Assert.Equal(Value.FromNumber(45000.5), sheet.EntryAt("A2")!.Constant);
        Assert.Equal(Value.FromNumber(1462), sheet.EntryAt("A3")!.Constant);
    }

    [Fact] // ADR-0330 §4: a number that is not a date, a time of day and a duration are the same number on both systems
    public async Task ADR0330_1904_numbers_times_and_durations_are_not_moved()
    {
        var sheet = await ReadAsync(true,
            Number("A1", "43538"), Number("A2", "0.5", style: 2), Number("A3", "1.25", style: 4), Number("A4", "0.001", style: 5), Number("A5", "12345", style: 6));

        Assert.Equal(Value.FromNumber(43538), sheet.EntryAt("A1")!.Constant);
        Assert.Equal(Value.FromNumber(0.5), sheet.EntryAt("A2")!.Constant);
        Assert.Equal(Value.FromNumber(1.25), sheet.EntryAt("A3")!.Constant);
        Assert.Equal(Value.FromNumber(0.001), sheet.EntryAt("A4")!.Constant);
        Assert.Equal(Value.FromNumber(12345), sheet.EntryAt("A5")!.Constant);
    }

    [Fact] // ADR-0330 §4: an elapsed time in brackets is a duration, so the m beside it is minutes and the number is not moved
    public async Task ADR0330_1904_elapsed_durations_are_not_moved()
    {
        var sheet = await ReadAsync(true, Number("A1", "1.5", style: 7), Number("A2", "0.25", style: 8));

        Assert.Equal(Value.FromNumber(1.5), sheet.EntryAt("A1")!.Constant);
        Assert.Equal(Value.FromNumber(0.25), sheet.EntryAt("A2")!.Constant);
    }

    [Fact] // ADR-0330 §4: on the 1900 system nothing is moved
    public async Task ADR0330_1900_serials_are_kept()
    {
        var sheet = await ReadAsync(false, Number("A1", "45000", style: 1));

        Assert.Equal(Value.FromNumber(45000), sheet.EntryAt("A1")!.Constant);
    }

    [Fact] // ADR-0330 §4: a Formula computes its date on 1900's, whatever system the file was on
    public async Task ADR0330_1904_a_Formula_is_carried_as_it_is()
    {
        var file = await WriteAsync(new XlsxWriteOptions { Styles = Styles, Date1904 = true },
            new Page("Sheet1", [Formula("A1", "DATE(2023,3,15)", cached: "43538") with { StyleIndex = 1 }]));

        var document = (await Book.ReadAsync(file)).Document;

        Assert.Equal("=DATE(2023,3,15)", document.Sheet("Sheet1").EntryAt("A1")!.Formula);
        Assert.Equal(Value.FromNumber(45000), Workbook.Open(document).Sheets[0].GetValue(CellAddress.Parse("A1")));
    }

    [Theory] // ADR-0330 §4: which Number Formats show a date, and so move on the 1904 system
    [InlineData("yyyy-mm-dd", true)]
    [InlineData("mmm", true)]
    [InlineData("[$-F800]dddd, mmmm dd, yyyy", true)]
    [InlineData("m/d/yy h:mm AM/PM", true)]
    [InlineData("ge.m.d", true)]
    [InlineData("h:mm", false)]
    [InlineData("mm:ss", false)]
    [InlineData("[h]:mm:ss", false)]
    [InlineData("[h]:mm", false)]
    [InlineData("[mm]:ss", false)]
    [InlineData("[m]", false)]
    [InlineData("[hh]", false)]
    [InlineData("[Red][h]:mm", false)]
    [InlineData("[Red]mmm", true)]
    [InlineData("h:mm AM/PM", false)]
    [InlineData("0.00E+00", false)]
    [InlineData("General", false)]
    [InlineData("#,##0.00 \"days\"", false)]
    [InlineData("[Red]#,##0;[Blue]-#,##0", false)]
    [InlineData("0 \\d", false)]
    [InlineData("0;yyyy", false)]
    public void ADR0330_which_formats_show_a_date(string code, bool date)
    {
        Assert.Equal(date, DateFormats.CodeShowsDate(code));
    }

    [Theory] // ADR-0330 §4: the built-in formats that show a date, and those that show only a time
    [InlineData(14, true)]
    [InlineData(17, true)]
    [InlineData(22, true)]
    [InlineData(18, false)]
    [InlineData(21, false)]
    [InlineData(46, false)]
    [InlineData(0, false)]
    [InlineData(4, false)]
    public void ADR0330_which_built_in_formats_show_a_date(int id, bool date)
    {
        Assert.Equal(date, DateFormats.ShowsDate(id, null));
    }
}
