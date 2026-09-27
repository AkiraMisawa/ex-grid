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

    [Fact] // ADR-0048/0014: a block pastes only over a whole multiple of itself (what the repeats write is in ExcelCases/copy.json)
    public void A_paste_over_other_than_a_multiple_is_refused_as_an_argument()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "1");
        sheet.Enter("A2", "2");

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

    [Fact] // ADR-0048 (SH-14): inward, a pasted date takes a date format (what each field becomes is in ExcelCases/copy.json)
    public void Pasted_dates_take_a_date_format()
    {
        var sheet = NewSheet();

        sheet.Do(SheetEdit.PasteText([["=A1+1", "1,234"], ["9/27/2026", "12%"]], At("B1")));

        Assert.True(sheet.GetFormat(At("B2")).IsDate);
    }

    [Fact] // ADR-0048 (observed in Excel): pasted text that cannot be read as a Formula is text; typed, it is still refused
    public void An_unreadable_pasted_formula_is_text()
    {
        var sheet = NewSheet();

        var step = sheet.Do(SheetEdit.PasteText([new(At("A1"), "5"), new(At("B1"), "=1+")]));

        Assert.Equal(5, sheet.Number("A1"));
        Assert.Equal(Value.FromText("=1+"), sheet.GetValue(At("B1")));
        Assert.False(sheet.GetEntry(At("B1"))!.IsFormula);
        step.Undo();
        Assert.Null(sheet.GetEntry(At("B1")));
        Assert.Throws<FormulaSyntaxException>(() => sheet.Do(SheetEdit.Enter(At("B1"), "=1+")));
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
