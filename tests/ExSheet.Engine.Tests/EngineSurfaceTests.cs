using System.Globalization;
using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// The facts of the engine's .NET surface that the shared cases of <c>tests/cases/sheet/</c> cannot state: the process
/// culture, a null argument, and calls a sheet case does not make (a name asked about, a Sheet made or renamed directly,
/// two Entries compared). The calls stay here until the cases have a kind that makes them (ADR-0370 section 2).
/// </summary>
public class EngineSurfaceTests
{
    [Theory] // ADR-0047: one spelling whatever culture the process runs under — ',' separates arguments and '.' is the decimal separator
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    [InlineData("ja-JP")]
    [InlineData("en-US")]
    public void The_formula_syntax_is_invariant_under_every_process_culture(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            var sheet = new Sheet(CultureInfo.GetCultureInfo(culture));

            Assert.Equal(3, sheet.Evaluate("=1.5*2").Number);
            Assert.Equal("=1.5*2", sheet.GetEntryText(CellAddress.Parse("ZZ1000")));
            Assert.Throws<FormulaSyntaxException>(() => sheet.Enter("A1", "=1,5*2"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory] // ADR-0046: Excel's rules for a Sheet's name, asked about and refused by every way a Sheet is named
    [InlineData("")]
    [InlineData("a:b")]
    [InlineData("'quoted")]
    [InlineData("History")]
    [InlineData("history")]
    [InlineData("12345678901234567890123456789012")]
    public void An_invalid_name_is_refused(string name)
    {
        Assert.False(Sheet.IsValidName(name, out var reason));
        Assert.NotNull(reason);
        Assert.Throws<ArgumentException>(() => new Sheet(EnUs, name));
        Assert.Throws<ArgumentException>(() => NewSheet().Rename(name));
    }

    [Theory] // ADR-0046: names Excel accepts
    [InlineData("My Sheet")]
    [InlineData("It's")]
    [InlineData("1234567890123456789012345678901")]
    [InlineData("History2")]
    public void A_valid_name_is_accepted(string name)
    {
        Assert.True(Sheet.IsValidName(name, out var reason));
        Assert.Null(reason);
        Assert.Equal(name, new Sheet(EnUs, name).Name);
    }

    [Fact] // ADR-0046: a null where ranges or a change belong is the caller's mistake, refused as one
    public void A_null_cell_format_argument_is_refused()
    {
        var twoPlaces = new CellFormatChange { NumberFormat = NumberFormat.Parse("0.00") };
        Assert.Throws<ArgumentNullException>(() => SheetEdit.SetCellFormat(null!, twoPlaces));
        Assert.Throws<ArgumentNullException>(() => SheetEdit.SetCellFormat([CellRange.Parse("B2")], null!));
    }

    [Fact] // ADR-0047: two Formulas that differ only in whitespace are different Entries
    public void Whitespace_is_part_of_the_entry()
    {
        Assert.NotEqual(Entry.FromFormula("=A1+1"), Entry.FromFormula("= A1+1"));
    }

    [Theory] // ADR-0047: whole columns and rows are written as Excel writes them
    [InlineData("B:D", true, false)]
    [InlineData("3:4", false, true)]
    [InlineData("A:XFD", true, true)]
    [InlineData("A1:B2", false, false)]
    public void Whole_columns_and_rows_parse(string text, bool columns, bool rows)
    {
        var range = CellRange.Parse(text);

        Assert.Equal(columns, range.IsWholeColumns);
        Assert.Equal(rows, range.IsWholeRows);
    }

    [Fact] // ADR-0331: the Sheet shown is one of the Workbook's, and a Sheet of another is refused
    public void Only_a_sheet_of_the_workbook_is_shown()
    {
        var workbook = new Workbook(EnUs, ["Sheet1", "Sheet2"]);

        Assert.Throws<ArgumentException>(() => workbook.Shown = NewSheet());
    }

    [Theory] // ADR-0071, SH-40 (a reading until the eleventh Windows run, case 1): the eight names are Excel's legacy palette
    [InlineData(NumberFormatColour.Black, 0x000000)]
    [InlineData(NumberFormatColour.Blue, 0x0000FF)]
    [InlineData(NumberFormatColour.Cyan, 0x00FFFF)]
    [InlineData(NumberFormatColour.Green, 0x00FF00)]
    [InlineData(NumberFormatColour.Magenta, 0xFF00FF)]
    [InlineData(NumberFormatColour.Red, 0xFF0000)]
    [InlineData(NumberFormatColour.White, 0xFFFFFF)]
    [InlineData(NumberFormatColour.Yellow, 0xFFFF00)]
    public void Each_name_is_painted_in_Excels_legacy_palette(NumberFormatColour colour, int rgb)
    {
        Assert.Equal(rgb, colour.Rgb());
    }

    [Fact] // ADR-0071, ticket 103: d-mmm-yy typed under en-GB, which spells built-in 15 otherwise, is a code of its own
    public void A_local_code_that_is_no_built_in_of_the_culture_is_a_code_of_its_own()
    {
        Assert.True(NumberFormat.TryParseLocal("d-mmm-yy", CultureInfo.GetCultureInfo("en-GB"), out var own, out _));
        Assert.Equal("d\\-mmm\\-yy", own.Code);
    }
}
