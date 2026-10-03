using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// Spilled arrays (ADR-0125): what the case corpus cannot state — the layout of a spill, its
/// obstacles, what reads it, and how it moves with the Sheet's structure.
/// </summary>
public class SpillTests
{
    private static Sheet WithColumn(params double[] numbers)
    {
        var sheet = NewSheet();
        for (var i = 0; i < numbers.Length; i++) sheet.Enter($"A{i + 1}", numbers[i].ToString(EnUs));
        return sheet;
    }

    private static CellAddress At(string address) => CellAddress.Parse(address);

    [Fact] // ADR-0125: a Reference to several cells spills from its Anchor, and the cells it covers hold no Entry
    public void A_range_spills_from_its_anchor()
    {
        var sheet = WithColumn(1, 2, 3);
        sheet.Enter("C1", "=A1:A3*10");

        Assert.Equal(10, sheet.Number("C1"));
        Assert.Equal(20, sheet.Number("C2"));
        Assert.Equal(30, sheet.Number("C3"));
        Assert.Null(sheet.SpilledFrom(At("C1")));
        Assert.Equal(At("C1"), sheet.SpilledFrom(At("C3")));
        Assert.Null(sheet.GetEntry(At("C2")));
    }

    [Fact] // ADR-0125: a spill is not recorded in a Sheet Document, and reopening lays it out again
    public void A_spill_is_not_recorded_and_reopens()
    {
        var sheet = WithColumn(1, 2);
        sheet.Enter("C1", "=A1:A2");

        var document = sheet.ToDocument();
        var reopened = Sheet.Open(SheetDocument.FromJson(document.ToJson()));

        Assert.DoesNotContain("C2", document.ToJson());
        Assert.Equal(2, reopened.Number("C2"));
        Assert.Equal(At("C1"), reopened.SpilledFrom(At("C2")));
    }

    [Fact] // ADR-0125: a blank in the array shows as 0, as Excel's spill of a blank cell does
    public void A_blank_spills_as_zero()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "1");
        sheet.Enter("A3", "3");
        sheet.Enter("C1", "=A1:A3");

        Assert.Equal(0, sheet.Number("C2"));
    }

    [Fact] // ADR-0125: an Entry in the way is #SPILL!, nothing is overwritten, and clearing it lets the Formula spill
    public void An_entry_in_the_way_is_spill_until_cleared()
    {
        var sheet = WithColumn(1, 2, 3);
        sheet.Enter("C3", "x");
        sheet.Enter("C1", "=A1:A3");

        Assert.Equal(ErrorValue.Spill, sheet.Error("C1"));
        Assert.Null(sheet.Value("C2"));
        Assert.Equal("x", sheet.Value("C3")!.Value.Text);

        var cleared = sheet.Enter("C3", "");

        Assert.Equal(1, sheet.Number("C1"));
        Assert.Equal(3, sheet.Number("C3"));
        Assert.Contains(At("C1"), cleared.ValueChanges);
        Assert.Contains(At("C2"), cleared.ValueChanges);
    }

    [Fact] // ADR-0125: typing into a spilled cell enters an Entry there, and the Anchor becomes #SPILL!
    public void Typing_into_a_spilled_cell_makes_the_anchor_spill()
    {
        var sheet = WithColumn(1, 2, 3);
        sheet.Enter("C1", "=A1:A3");

        sheet.Enter("C2", "7");

        Assert.Equal(ErrorValue.Spill, sheet.Error("C1"));
        Assert.Equal(7, sheet.Number("C2"));
        Assert.Null(sheet.Value("C3"));
        Assert.Null(sheet.SpilledFrom(At("C3")));
    }

    [Fact] // ADR-0125: a spill past the Sheet's edge is #SPILL!
    public void A_spill_past_the_edge_is_spill()
    {
        var sheet = WithColumn(1, 2);
        sheet.Enter("ZZ1000", "=A:A");

        Assert.Equal(ErrorValue.Spill, sheet.Error("ZZ1000"));
    }

    [Fact] // ADR-0125: of two spills that would overlap, the Anchor first in address order spills, whichever was entered first
    public void Overlapping_spills_are_decided_by_address()
    {
        foreach (var order in new[] { new[] { "D1", "C2" }, ["C2", "D1"] })
        {
            var sheet = WithColumn(1, 2, 3);
            sheet.Enter("B1", "4");
            foreach (var anchor in order)
            {
                // D1 spills D1:D3 down; C2 would spill C2:D2 across, and D2 is D1's. D1 comes first.
                sheet.Enter(anchor, anchor == "D1" ? "=A1:A3" : "=A1:B1");
            }

            Assert.Equal(2, sheet.Number("D2"));
            Assert.Equal(At("D1"), sheet.SpilledFrom(At("D2")));
            Assert.Equal(ErrorValue.Spill, sheet.Error("C2"));
        }
    }

    [Fact] // ADR-0125: a Formula that reads a spilled cell is recalculated when the spill changes
    public void A_reader_of_a_spilled_cell_follows_the_spill()
    {
        var sheet = WithColumn(1, 2);
        sheet.Enter("C1", "=A1:A2*2");
        sheet.Enter("E1", "=C2+1");
        Assert.Equal(5, sheet.Number("E1"));

        sheet.Enter("A2", "10");
        Assert.Equal(21, sheet.Number("E1"));

        sheet.Enter("C2", "x");
        Assert.Equal(ErrorValue.Value, sheet.Error("E1"));

        sheet.Enter("C2", "");
        Assert.Equal(21, sheet.Number("E1"));
    }

    [Fact] // ADR-0125: a spill that shrinks releases the cells it no longer covers
    public void A_shrinking_spill_releases_its_cells()
    {
        var sheet = WithColumn(1, 2, 3);
        sheet.Enter("C1", "=A1:A3");
        Assert.Equal(3, sheet.Number("C3"));

        sheet.Enter("C1", "=A1:A2");

        Assert.Null(sheet.Value("C3"));
        Assert.Null(sheet.SpilledFrom(At("C3")));
    }

    [Fact] // ADR-0125: clearing the Anchor clears its spill
    public void Clearing_the_anchor_clears_its_spill()
    {
        var sheet = WithColumn(1, 2);
        sheet.Enter("C1", "=A1:A2");

        sheet.Enter("C1", "");

        Assert.Null(sheet.Value("C2"));
        Assert.Null(sheet.SpilledFrom(At("C2")));
    }

    [Fact] // ADR-0125: a function that reduces an array takes it whole
    public void A_reducing_function_takes_an_array()
    {
        var sheet = WithColumn(1, 2, 3);

        Assert.Equal(12, sheet.Evaluate("=SUM(A1:A3*2)").Number);
        Assert.Equal(2, sheet.Evaluate("=SUM((A1:A3>1)*1)").Number);
    }

    [Fact] // ADR-0125: a spill moves with its Anchor when rows are inserted above it
    public void A_spill_moves_with_its_anchor()
    {
        var sheet = WithColumn(1, 2);
        sheet.Enter("C1", "=A1:A2");

        sheet.InsertRows(0);

        Assert.Equal(1, sheet.Number("C2"));
        Assert.Equal(2, sheet.Number("C3"));
        Assert.Null(sheet.Value("C1"));
        Assert.Equal(At("C2"), sheet.SpilledFrom(At("C3")));
    }

    [Fact] // ADR-0125: a row inserted inside the spill grows the range it reads, and the spill with it
    public void A_row_inserted_inside_a_spill_grows_it()
    {
        var sheet = WithColumn(1, 2);
        sheet.Enter("C1", "=A1:A2");

        sheet.InsertRows(1);

        Assert.Equal(1, sheet.Number("C1"));
        Assert.Equal(0, sheet.Number("C2"));
        Assert.Equal(2, sheet.Number("C3"));
    }

    [Fact] // ADR-0125 / ADR-0049: a Linked Table's column spills, and follows its rows
    public void A_linked_tables_column_spills_and_follows_its_rows()
    {
        var sheet = NewSheet();
        sheet.DeclareLinkedTable("T", ["V"]);
        sheet.Enter("A1", "=T[V]");
        Assert.Equal(ErrorValue.GettingData, sheet.Error("A1"));

        sheet.PushLinkedTable("T", [[Value.FromNumber(1)], [Value.FromNumber(2)], [Value.FromNumber(3)]]);
        Assert.Equal(3, sheet.Number("A3"));

        sheet.PushLinkedTable("T", [[Value.FromNumber(1)]]);
        Assert.Equal(1, sheet.Number("A1"));
        Assert.Null(sheet.Value("A2"));
    }

    [Fact] // ADR-0125: #SPILL! propagates as any Error Value does
    public void Spill_propagates()
    {
        var sheet = WithColumn(1, 2);
        sheet.Enter("C2", "x");
        sheet.Enter("C1", "=A1:A2");
        sheet.Enter("E1", "=C1+1");

        Assert.Equal(ErrorValue.Spill, sheet.Error("E1"));
    }

    [Fact] // ADR-0125: A1# reads the Spill Range of the Formula in A1, and follows it as it grows
    public void A_spill_reference_reads_the_spill_range()
    {
        var sheet = WithColumn(1, 2, 3);
        sheet.Enter("C1", "=A1:A2*2");
        sheet.Enter("E1", "=SUM(C1#)");
        sheet.Enter("F1", "=ROWS(C1#)");
        Assert.Equal(6, sheet.Number("E1"));
        Assert.Equal(2, sheet.Number("F1"));

        sheet.Enter("C1", "=A1:A3*2");

        Assert.Equal(12, sheet.Number("E1"));
        Assert.Equal(3, sheet.Number("F1"));
    }

    [Fact] // ADR-0125: A1# spills itself, as a Reference to several cells does
    public void A_spill_reference_spills()
    {
        var sheet = WithColumn(1, 2);
        sheet.Enter("C1", "=A1:A2");
        sheet.Enter("E1", "=C1#+10");

        Assert.Equal(11, sheet.Number("E1"));
        Assert.Equal(12, sheet.Number("E2"));
    }

    [Fact] // ADR-0125: A1# is #REF! when A1 does not spill: no Formula, one Value, or #SPILL!
    public void A_spill_reference_to_no_spill_is_ref()
    {
        var sheet = WithColumn(1, 2);
        sheet.Enter("E1", "=SUM(C1#)");
        Assert.Equal(ErrorValue.Ref, sheet.Error("E1"));

        sheet.Enter("C1", "=A1");
        Assert.Equal(ErrorValue.Ref, sheet.Error("E1"));

        sheet.Enter("C1", "=A1:A2");
        Assert.Equal(3, sheet.Number("E1"));

        sheet.Enter("C2", "x");
        Assert.Equal(ErrorValue.Ref, sheet.Error("E1"));

        sheet.Enter("C2", "");
        Assert.Equal(3, sheet.Number("E1"));
    }

    [Fact] // ADR-0125: A1# is written as typed, moves with A1, and is #REF! when A1 is deleted
    public void A_spill_reference_moves_with_its_anchor()
    {
        var sheet = WithColumn(1, 2);
        sheet.Enter("C2", "=A1:A2");
        sheet.Enter("E1", "=sum(c2#)");
        Assert.Equal("=SUM(C2#)", sheet.GetEntryText(At("E1")));

        sheet.InsertRows(0);
        Assert.Equal("=SUM(C3#)", sheet.GetEntryText(At("E2")));
        Assert.Equal(3, sheet.Number("E2"));

        sheet.DeleteRows(2);
        Assert.Equal("=SUM(#REF!)", sheet.GetEntryText(At("E2")));
    }

    [Theory] // ADR-0125: # follows one cell only, and the @ operator is refused on entry
    [InlineData("=A1:A2#")]
    [InlineData("=A:A#")]
    [InlineData("=@A1:A2")]
    [InlineData("=SUM(@A1:A2)")]
    public void Other_spill_syntax_is_refused(string formula)
    {
        Assert.Throws<FormulaSyntaxException>(() => Entry.FromFormula(formula));
    }

    [Theory] // ADR-0125: an array larger than the engine holds is refused with #NUM!, never cut short
    [InlineData("=SUM(1001:2025*1)")]
    [InlineData("=SUM(A:Q*1)")]
    [InlineData("=SEQUENCE(1048576,17)")]
    [InlineData("=SUM(A1:A1048576+B1:XFD1)")]
    public void An_array_too_large_to_hold_is_num(string formula)
    {
        var sheet = WithColumn(1, 2);

        Assert.Equal(ErrorValue.Num, sheet.Evaluate(formula).Error);
    }
}
