using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

public class LegacyLookupTests
{
    [Theory]
    [InlineData("vlookup")]
    [InlineData("hlookup")]
    [InlineData("match")]
    public void ADR0047_Observed_Windows_lookup_cases_match_Excel_or_recorded_refusal(string area)
    {
        var differences = ExcelCorpus.Cases(area).SelectMany(c => ExcelCorpus.Run(c)
            .Select(d => $"{c.GetProperty("id").GetString()}: {d}"));

        Assert.Empty(differences);
    }

    [Theory] // Windows VLOOKUP/HLOOKUP/MATCH-001/002: exact takes the first duplicate, ascending approximate the last.
    [InlineData("=VLOOKUP(2,A1:B4,2,FALSE)", 20)]
    [InlineData("=VLOOKUP(2,A1:B4,2,TRUE)", 30)]
    [InlineData("=HLOOKUP(2,TRANSPOSE(A1:B4),2,FALSE)", 20)]
    [InlineData("=HLOOKUP(2,TRANSPOSE(A1:B4),2,TRUE)", 30)]
    [InlineData("=MATCH(2,A1:A4,0)", 2)]
    [InlineData("=MATCH(2,A1:A4,1)", 3)]
    public void ADR0047_Legacy_lookup_duplicate_choice_is_not_XLookups(string formula, double expected)
    {
        var sheet = Ascending();

        Assert.Equal(expected, sheet.Evaluate(formula).Number);
    }

    [Theory]
    [InlineData("=VLOOKUP(\"é\",A1:B2,2,FALSE)")]
    [InlineData("=HLOOKUP(\"é\",TRANSPOSE(A1:B2),2,FALSE)")]
    [InlineData("=MATCH(\"é\",A1:A2,0)")]
    public void ADR0047_Exact_lookup_refuses_unobserved_Unicode_matching(string formula)
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "é");
        sheet.Enter("B1", "10");

        Assert.Equal(ErrorValue.Value, sheet.Evaluate(formula).Error);
    }

    [Theory]
    [InlineData("=VLOOKUP(C1,A1:B2,2,FALSE)")]
    [InlineData("=HLOOKUP(C1,TRANSPOSE(A1:B2),2,FALSE)")]
    [InlineData("=MATCH(C1,A1:A2,0)")]
    public void ADR0047_Lookup_refuses_text_keys_beyond_the_legacy_255_character_limit(string formula)
    {
        var sheet = NewSheet();
        sheet.Enter("A1", new string('x', 256));
        sheet.Enter("C1", new string('x', 256));
        sheet.Enter("B1", "10");

        Assert.Equal(ErrorValue.Value, sheet.Evaluate(formula).Error);
    }

    [Theory]
    [InlineData("=VLOOKUP(2,A1:B2,2,FALSE)")]
    [InlineData("=HLOOKUP(2,TRANSPOSE(A1:B2),2,FALSE)")]
    [InlineData("=MATCH(2,A1:A2,0)")]
    public void ADR0047_Exact_lookup_refuses_an_unobserved_match_after_an_error(string formula)
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "=NA()");
        sheet.Enter("A2", "2");
        sheet.Enter("B2", "20");

        Assert.Equal(ErrorValue.Value, sheet.Evaluate(formula).Error);
    }

    [Fact]
    public void ADR0047_Descending_Match_refuses_an_unobserved_nearest_duplicate_choice()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "3");
        sheet.Enter("A2", "2");
        sheet.Enter("A3", "2");
        sheet.Enter("A4", "1");

        Assert.Equal(ErrorValue.Value, sheet.Evaluate("=MATCH(1.5,A1:A4,-1)").Error);
    }

    [Fact]
    public void ADR0047_Changing_a_lookup_key_or_target_recalculates_its_dependents()
    {
        var sheet = Ascending();
        sheet.Enter("D1", "=VLOOKUP(2,A1:B4,2,FALSE)");
        sheet.Enter("D2", "=D1*2");
        Assert.Equal(20, sheet.Number("D1"));

        sheet.Enter("B2", "25");
        Assert.Equal(25, sheet.Number("D1"));
        Assert.Equal(50, sheet.Number("D2"));

        sheet.Enter("A2", "9");
        Assert.Equal(30, sheet.Number("D1"));
        Assert.Equal(60, sheet.Number("D2"));
    }

    [Fact]
    public void ADR0047_Lookup_reference_rewrites_on_structure_edits()
    {
        var sheet = Ascending();
        sheet.Enter("D1", "=VLOOKUP(2,A1:B4,2,FALSE)");

        sheet.InsertRows(1, 1);

        Assert.Equal("=VLOOKUP(2,A1:B5,2,FALSE)", sheet.GetEntry(CellAddress.Parse("D1"))!.Formula);
        Assert.Equal(20, sheet.Number("D1"));
    }

    [Theory]
    [InlineData("=VLOOKUP(3,A1:B4,2,FALSE)=0")]
    [InlineData("=NOT(ISBLANK(VLOOKUP(3,A1:B4,2,FALSE)))")]
    public void ADR0047_A_blank_lookup_target_is_a_number_even_inside_another_function(string formula)
    {
        Assert.True(Ascending().Evaluate(formula).Boolean);
    }

    [Theory]
    [InlineData("=VLOOKUP(1,Keys[Code],1,FALSE)")]
    [InlineData("=HLOOKUP(1,Keys[Code],1,FALSE)")]
    [InlineData("=MATCH(1,Keys[Code],0)")]
    public void ADR0049_Lookup_does_not_answer_for_data_that_has_not_arrived(string formula)
    {
        var sheet = NewSheet();
        sheet.DeclareLinkedTable("Keys", ["Code"]);

        Assert.Equal(ErrorValue.GettingData, sheet.Evaluate(formula).Error);
    }

    [Theory]
    [InlineData("=VLOOKUP(1,Keys[Code],1,FALSE)", 1)]
    [InlineData("=HLOOKUP(1,Keys[Code],2,FALSE)", 2)]
    [InlineData("=MATCH(2,Keys[Code],0)", 2)]
    public void ADR0049_Lookup_reads_Linked_Table_columns(string formula, double expected)
    {
        var sheet = NewSheet();
        sheet.DeclareLinkedTable("Keys", ["Code"]);
        sheet.PushLinkedTable("Keys", [[Value.FromNumber(1)], [Value.FromNumber(2)]]);

        Assert.Equal(expected, sheet.Evaluate(formula).Number);
    }

    [Theory]
    [InlineData("=VLOOKUP(1,A1:B2,2,FALSE)")]
    [InlineData("=HLOOKUP(1,A1:B2,2,FALSE)")]
    [InlineData("=MATCH(1,A1:A2,0)")]
    public void ADR0047_Lookup_does_not_hide_a_cycle_in_its_input(string formula)
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "1");
        sheet.Enter("A2", "=D1");
        sheet.Enter("D1", formula);

        Assert.Equal(ErrorValue.Circ, sheet.Error("D1"));
    }

    [Theory]
    [InlineData("=VLOOKUP(A1:A2,A1:B4,2,FALSE)")]
    [InlineData("=HLOOKUP(A1:A2,TRANSPOSE(A1:B4),2,FALSE)")]
    [InlineData("=MATCH(A1:A2,A1:A4,0)")]
    public void ADR0125_Lookup_does_not_silently_intersect_an_array_key(string formula)
    {
        Assert.Equal(ErrorValue.Value, Ascending().Evaluate(formula).Error);
    }

    private static Sheet Ascending()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "1");
        sheet.Enter("A2", "2");
        sheet.Enter("A3", "2");
        sheet.Enter("A4", "3");
        sheet.Enter("B1", "10");
        sheet.Enter("B2", "20");
        sheet.Enter("B3", "30");
        return sheet;
    }
}
