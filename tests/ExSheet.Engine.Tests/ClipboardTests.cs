using System.Globalization;
using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>Copy and paste of Entries, Values outward, text inward (ticket 14): SH-14's engine half.</summary>
public class ClipboardTests
{
    private static CellAddress At(string address) => CellAddress.Parse(address);

    private static string? Formula(Sheet sheet, string address) => sheet.GetEntry(At(address))?.Formula;

    private static SheetBlock CopyBlock(Sheet sheet, string range) => sheet.Copy(CellRange.Parse(range)).Block!;

    [Fact] // ADR-0048 (SH-14): copying =A1 from B1 to B2 pastes =A2
    public void A_relative_reference_shifts_by_the_distance_pasted()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "1");
        sheet.Enter("A2", "2");
        sheet.Enter("B1", "=A1");

        sheet.Do(SheetEdit.Paste(CopyBlock(sheet, "B1"), At("B2")));

        Assert.Equal("=A2", Formula(sheet, "B2"));
        Assert.Equal(2, sheet.Number("B2"));
    }

    [Fact] // ADR-0048 (SH-14): absolute parts stay, relative parts move, across and down
    public void Absolute_parts_stay_where_they_are()
    {
        var sheet = NewSheet();
        sheet.Enter("C3", "=$A$1+A$1+$A1+A1");

        sheet.Do(SheetEdit.Paste(CopyBlock(sheet, "C3"), At("E6")));

        Assert.Equal("=$A$1+C$1+$A4+C4", Formula(sheet, "E6"));
    }

    [Theory] // ADR-0048: a Reference shifted off the Sheet is #REF!, as in Excel
    [InlineData("B2", "=A1", "B1", "=#REF!")]
    [InlineData("B2", "=A1", "A2", "=#REF!")]
    [InlineData("B3", "=SUM(A1:A2)", "B1", "=SUM(#REF!)")]
    [InlineData("B3", "=SUM(A1:A2)+1", "B2", "=SUM(#REF!)+1")]
    [InlineData("B3", "=$A$1+A1", "B1", "=$A$1+#REF!")]
    [InlineData("A1", "=B1048576", "A2", "=#REF!")]
    public void A_reference_shifted_off_the_sheet_is_ref_error(string from, string formula, string to, string expected)
    {
        var sheet = NewSheet();
        sheet.Enter(from, formula);

        sheet.Do(SheetEdit.Paste(CopyBlock(sheet, from), At(to)));

        Assert.Equal(expected, Formula(sheet, to));
    }

    [Theory] // ADR-0047/0048: a range is written from its top-left whichever of its corners moved
    [InlineData("B6", "=SUM(A$5:A6)", "B1", "=SUM(A1:A$5)")]
    [InlineData("C1", "=SUM($A1:B1)", "A1", "=SUM(#REF!)")]
    [InlineData("D1", "=SUM($B1:C1)", "A1", "=SUM(#REF!)")]
    [InlineData("D1", "=SUM($C1:C1)", "B1", "=SUM(A1:$C1)")]
    public void A_range_is_written_from_its_top_left(string from, string formula, string to, string expected)
    {
        var sheet = NewSheet();
        sheet.Enter(from, formula);

        sheet.Do(SheetEdit.Paste(CopyBlock(sheet, from), At(to)));

        Assert.Equal(expected, Formula(sheet, to));
    }

    [Fact] // ADR-0048: whole columns move across and whole rows move down, and not the other way
    public void Whole_columns_and_rows_shift_along_their_own_axis()
    {
        var sheet = NewSheet();
        sheet.Enter("B2", "=SUM(A:A)+SUM(1:1)");

        sheet.Do(SheetEdit.Paste(CopyBlock(sheet, "B2"), At("D7")));

        Assert.Equal("=SUM(C:C)+SUM(6:6)", Formula(sheet, "D7"));
    }

    [Fact] // ADR-0048: a paste writes formats and alignment, and a blank cell of the block clears what it lands on
    public void A_paste_writes_the_whole_cell_and_blanks_clear()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "1.5");
        sheet.SetFormat(At("A1"), NumberFormat.Parse("0.00"));
        sheet.SetAlignment(At("A2"), HorizontalAlignment.Center);
        sheet.Enter("C1", "old");
        sheet.Enter("C2", "old");
        sheet.SetFormat(At("C2"), NumberFormat.Parse("0%"));

        sheet.Do(SheetEdit.Paste(CopyBlock(sheet, "A1:A2"), At("C1")));

        Assert.Equal("1.50", sheet.GetDisplay(At("C1")).Text);
        Assert.Null(sheet.GetEntry(At("C2")));
        Assert.True(sheet.GetFormat(At("C2")).IsGeneral);
        Assert.Equal(HorizontalAlignment.Center, sheet.GetAlignment(At("C2")));
    }

    [Fact] // ADR-0048: a block holds the Entries as they were at the copy, whatever changes after
    public void A_block_is_a_snapshot()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "1");
        var block = CopyBlock(sheet, "A1");

        sheet.Enter("A1", "2");
        sheet.Do(SheetEdit.Paste(block, At("B1")));

        Assert.Equal(1, sheet.Number("B1"));
        Assert.Equal(1, block.EntryAt(0, 0)!.Constant!.Value.Number);
    }

    [Fact] // ADR-0048/0014: a block pasted over a whole multiple of itself repeats, each copy shifted
    public void A_paste_over_a_multiple_repeats_the_block()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "1");
        sheet.Enter("A2", "2");
        sheet.Enter("A3", "3");
        sheet.Enter("A4", "4");
        sheet.Enter("B1", "=A1*10");

        sheet.Do(SheetEdit.Paste(CopyBlock(sheet, "B1"), CellRange.Parse("B2:B4")));

        Assert.Equal<string>(["=A2*10", "=A3*10", "=A4*10"], [Formula(sheet, "B2")!, Formula(sheet, "B3")!, Formula(sheet, "B4")!]);
        Assert.Throws<ArgumentException>(() => SheetEdit.Paste(CopyBlock(sheet, "A1:A2"), CellRange.Parse("C1:C3")));
    }

    [Fact] // ADR-0050: a block that would run past the Sheet's edge is refused by name, and nothing changes
    public void A_paste_past_the_edge_is_refused()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "1");
        sheet.Enter("A2", "2");
        var edit = SheetEdit.Paste(CopyBlock(sheet, "A1:A2"), At("C1048576"));

        Assert.Equal(SheetRefusalReason.BlockWouldLeaveSheet, sheet.Check(edit)!.Reason);
        var refused = Assert.Throws<SheetRefusedException>(() => sheet.Do(edit));
        Assert.Equal(SheetRefusalReason.BlockWouldLeaveSheet, refused.Refusal.Reason);
        Assert.Null(sheet.GetEntry(At("C1048576")));
    }

    [Fact] // ADR-0048 (SH-13): a paste is one step, and its undo restores every cell it wrote over
    public void A_paste_is_one_step()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "1");
        sheet.Enter("A2", "=A1+1");
        sheet.Enter("C2", "keep me");
        sheet.SetFormat(At("C1"), NumberFormat.Parse("0%"));
        var before = sheet.ToDocument().ToJson();

        var step = sheet.Do(SheetEdit.Paste(CopyBlock(sheet, "A1:A2"), At("C1")));
        Assert.Equal("=C1+1", Formula(sheet, "C2"));
        step.Undo();

        Assert.Equal(before, sheet.ToDocument().ToJson());
        Assert.Equal("keep me", sheet.Value("C2")!.Value.Text);
    }

    [Fact] // ADR-0048/0005 (SH-14): outward, a copy carries Values: as shown in the text flavour, unformatted in the HTML one
    public void A_copy_carries_values_outward()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "1234.5");
        sheet.SetFormat(At("A1"), NumberFormat.Parse("#,##0.00"));
        sheet.Enter("B1", "=A1*2");
        sheet.Enter("A2", "a\tb");
        sheet.Enter("B2", "=1/0");
        sheet.Enter("A3", "TRUE");
        sheet.Enter("B3", "say \"hi\"");

        var copy = sheet.Copy(CellRange.Parse("A1:C3"));

        Assert.False(copy.IsRefused);
        Assert.Equal("1,234.50\t2469\t\r\n\"a\tb\"\t#DIV/0!\t\r\nTRUE\t\"say \"\"hi\"\"\"\t\r\n", copy.Text);
        Assert.Equal("<table><tr><td>1234.5</td><td>2469</td><td></td></tr><tr><td>a\tb</td><td>#DIV/0!</td><td></td></tr><tr><td>TRUE</td><td>say \"hi\"</td><td></td></tr></table>", copy.Html);
    }

    [Fact] // ADR-0016/0048: a Value its format cannot show goes out as the Value, never as ####
    public void A_value_that_cannot_be_shown_goes_out_as_itself()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "-1");
        sheet.SetFormat(At("A1"), NumberFormat.Parse("yyyy-mm-dd"));

        var copy = sheet.Copy(CellRange.Parse("A1"));

        Assert.Equal("-1\r\n", copy.Text);
    }

    [Fact] // ADR-0048 (SH-14): inward, =A1+1 becomes a Formula and 1,234 a number under en-US
    public void Pasted_text_is_read_as_typed()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "41");

        sheet.Do(SheetEdit.PasteText([["=A1+1", "1,234"], ["9/27/2026", "12%"]], At("B1")));

        Assert.Equal("=A1+1", Formula(sheet, "B1"));
        Assert.Equal(42, sheet.Number("B1"));
        Assert.Equal(1234, sheet.Number("C1"));
        Assert.Equal(46292, sheet.Number("B2"));
        Assert.True(sheet.GetFormat(At("B2")).IsDate);
        Assert.Equal(0.12, sheet.Number("C2"));
    }

    [Fact] // ADR-0048 (SH-11): inward text is read under the Sheet's culture, not the program's
    public void Pasted_text_follows_the_sheets_culture()
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("de-DE"));

        sheet.Do(SheetEdit.PasteText([["1.234,5", "=SUM(1.5,2)"]], At("A1")));

        Assert.Equal(1234.5, sheet.Number("A1"));
        Assert.Equal(3.5, sheet.Number("B1"));
    }

    [Fact] // ADR-0048: an empty field clears its cell; a ragged block is refused as an argument
    public void Empty_fields_clear_and_ragged_blocks_are_refused()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "old");

        sheet.Do(SheetEdit.PasteText([[""]], At("A1")));

        Assert.Null(sheet.GetEntry(At("A1")));
        Assert.Throws<ArgumentException>(() => SheetEdit.PasteText([["a", "b"], ["c"]], At("A1")));
    }

    [Fact] // ADR-0050: pasted text that would run past the edge is refused by name
    public void Pasted_text_past_the_edge_is_refused()
    {
        var sheet = NewSheet();

        var edit = SheetEdit.PasteText([["1", "2"]], At("XFD1"));

        Assert.Equal(SheetRefusalReason.BlockWouldLeaveSheet, sheet.Check(edit)!.Reason);
        Assert.Throws<SheetRefusedException>(() => sheet.Do(edit));
    }
}
