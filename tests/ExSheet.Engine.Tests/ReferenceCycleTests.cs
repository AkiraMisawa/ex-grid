using ExSheet.Engine;
using Xunit;

namespace ExSheet.Engine.Tests;

/// <summary>
/// F4 cycles the Reference at the caret (ADR-0051, 2026-09-29; SH-28): the engine's rewrite of
/// the text being edited, against ADR-0051's readings. A <c>|</c> marks the caret; two of them
/// mark a selection, from the first to the second.
/// </summary>
public class ReferenceCycleTests
{
    /// <summary>The text without its marks, and the selection they mark: one mark is a caret.</summary>
    private static (string Text, int Start, int End) Marked(string marked)
    {
        var first = marked.IndexOf('|', StringComparison.Ordinal);
        var second = marked.IndexOf('|', first + 1);
        var text = marked.Replace("|", "", StringComparison.Ordinal);
        return second < 0 ? (text, first, first) : (text, first, second - 1);
    }

    /// <summary>The answer written with its marks, as the tests write what they expect; null for no answer.</summary>
    private static string? Cycled(string marked)
    {
        var (text, start, end) = Marked(marked);
        var answer = FormulaEntry.CycleReference(text, start, end);
        if (answer is null) return null;
        return answer.SelectionStart == answer.SelectionEnd
            ? answer.Text.Insert(answer.SelectionStart, "|")
            : answer.Text.Insert(answer.SelectionEnd, "|").Insert(answer.SelectionStart, "|");
    }

    [Theory] // ADR-0051 / SH-28: A1 → $A$1 → A$1 → $A1 → A1, the caret at the end of the rewritten Reference
    [InlineData("=B2|", "=$B$2|")]
    [InlineData("=$B$2|", "=B$2|")]
    [InlineData("=B$2|", "=$B2|")]
    [InlineData("=$B2|", "=B2|")]
    public void SH28_a_reference_cycles_through_its_four_forms(string marked, string expected)
    {
        Assert.Equal(expected, Cycled(marked));
    }

    [Fact] // ADR-0051 / SH-28: four presses bring the Reference back to where it started
    public void SH28_four_presses_come_back_to_the_relative_form()
    {
        var text = "=B2|";
        var seen = new List<string>();
        for (var press = 0; press < 4; press++)
        {
            text = Cycled(text)!;
            seen.Add(text);
        }

        Assert.Equal(["=$B$2|", "=B$2|", "=$B2|", "=B2|"], seen);
    }

    [Theory] // ADR-0051 / SH-28: the Reference at the caret is the one it is inside or touching, on either side
    [InlineData("=A1|+B2", "=$A$1|+B2")]
    [InlineData("=|A1+B2", "=$A$1|+B2")]
    [InlineData("=A|1+B2", "=$A$1|+B2")]
    [InlineData("=$|A1+B2", "=A1|+B2")]
    [InlineData("=A1+|B2", "=A1+$B$2|")]
    [InlineData("=SUM(|A1)", "=SUM($A$1|)")]
    [InlineData("=-A1|", "=-$A$1|")]
    public void SH28_the_reference_the_caret_is_inside_or_touching_cycles(string marked, string expected)
    {
        Assert.Equal(expected, Cycled(marked));
    }

    [Theory] // ADR-0051 / SH-28: a range cycles as one — $A$1:$B$2, A$1:B$2, $A1:$B2, A1:B2
    [InlineData("=SUM(A1:B2|)", "=SUM($A$1:$B$2|)")]
    [InlineData("=SUM($A$1:$B$2|)", "=SUM(A$1:B$2|)")]
    [InlineData("=SUM(A$1:B$2|)", "=SUM($A1:$B2|)")]
    [InlineData("=SUM($A1:$B2|)", "=SUM(A1:B2|)")]
    [InlineData("=SUM(|A1:B2)", "=SUM($A$1:$B$2|)")]
    [InlineData("=SUM(A1:|B2)", "=SUM($A$1:$B$2|)")]
    public void SH28_a_range_cycles_as_one(string marked, string expected)
    {
        Assert.Equal(expected, Cycled(marked));
    }

    [Theory] // ADR-0051 / SH-28: from a range whose ends differ, the next form is the first end's, given to both
    [InlineData("=$A1:B2|", "=A1:B2|")]
    [InlineData("=A1:$B$2|", "=$A$1:$B$2|")]
    [InlineData("=A$1:$B2|", "=$A1:$B2|")]
    [InlineData("=$A$1:B2|", "=A$1:B$2|")]
    public void SH28_a_range_whose_ends_differ_takes_its_first_ends_next_form(string marked, string expected)
    {
        Assert.Equal(expected, Cycled(marked));
    }

    [Theory] // ADR-0051 / SH-28: a selection cycles every Reference it covers, each to the next form of the first, and covers what was rewritten
    [InlineData("=|A1+B2|", "=|$A$1+$B$2|")]
    [InlineData("=|$A1+B$2|", "=|A1+B2|")]
    [InlineData("=A|1+B|2", "=|$A$1+$B$2|")]
    [InlineData("=SUM(|A1,B2:C3,$D$4|)", "=SUM(|$A$1,$B$2:$C$3,$D$4|)")]
    [InlineData("|=A1*2+B2|", "=|$A$1*2+$B$2|")]
    [InlineData("=|A1+B:B|", "=|$A$1+$B:$B|")]
    [InlineData("=1+|A1|*2", "=1+|$A$1|*2")]
    public void SH28_a_selection_cycles_every_reference_it_covers(string marked, string expected)
    {
        Assert.Equal(expected, Cycled(marked));
    }

    [Theory] // ADR-0051 / SH-28: whole columns and whole rows have two forms
    [InlineData("=SUM(A:A|)", "=SUM($A:$A|)")]
    [InlineData("=SUM($A:$A|)", "=SUM(A:A|)")]
    [InlineData("=SUM(A:C|)", "=SUM($A:$C|)")]
    [InlineData("=SUM($A:C|)", "=SUM(A:C|)")]
    [InlineData("=SUM(1:1|)", "=SUM($1:$1|)")]
    [InlineData("=SUM($1:$1|)", "=SUM(1:1|)")]
    [InlineData("=SUM(2:5|)", "=SUM($2:$5|)")]
    public void SH28_whole_columns_and_rows_have_two_forms(string marked, string expected)
    {
        Assert.Equal(expected, Cycled(marked));
    }

    [Theory] // ADR-0051 / SH-28: a Sheet qualifier is kept, and only the cell part cycles
    [InlineData("=Sheet2!A1|", "=Sheet2!$A$1|")]
    [InlineData("=Sheet2!$A$1|", "=Sheet2!A$1|")]
    [InlineData("=She|et2!A1", "=Sheet2!$A$1|")]
    [InlineData("='My Sheet'!B2:C3|", "='My Sheet'!$B$2:$C$3|")]
    [InlineData("=SUM(Sheet2!A:A|)", "=SUM(Sheet2!$A:$A|)")]
    public void SH28_a_sheet_qualifier_is_kept(string marked, string expected)
    {
        Assert.Equal(expected, Cycled(marked));
    }

    [Theory] // ADR-0051 / SH-28: a structured reference, a function name, a number, text in quotes and text that is not a Formula are unchanged
    [InlineData("=SUM(Positions[PV]|)")]
    [InlineData("=SUM(Positions[P|V])")]
    [InlineData("=S|UM(A1)")]
    [InlineData("=SUM|(A1)")]
    [InlineData("=LOG10|(100)")]
    [InlineData("=1+2|")]
    [InlineData("=1|+2")]
    [InlineData("=\"A1\"|")]
    [InlineData("=\"A|1\"")]
    [InlineData("=A1&\"B|2")]
    [InlineData("=#REF!|")]
    [InlineData("=Sheet1!#REF!|")]
    [InlineData("=TRUE|")]
    [InlineData("=ZZZ1|")]
    [InlineData("B2|")]
    [InlineData("|B2")]
    [InlineData("|B2|")]
    [InlineData("")]
    public void SH28_what_is_not_a_reference_is_unchanged(string marked)
    {
        var text = marked.Contains('|', StringComparison.Ordinal) ? marked : marked + "|";
        Assert.Null(Cycled(text));
    }

    [Theory] // ADR-0051 / SH-28: a caret touching no Reference changes nothing, nor does a selection covering none
    [InlineData("=|")]
    [InlineData("|=A1")]
    [InlineData("=SUM(|)")]
    [InlineData("=A1 |+ B2")]
    [InlineData("=A1%|")]
    [InlineData("=A1|+|B2")]
    [InlineData("=|1+2|")]
    public void SH28_nothing_at_the_caret_changes_nothing(string marked)
    {
        Assert.Null(Cycled(marked));
    }

    [Fact] // ADR-0051 / SH-28: only the Reference moves; every other character of unfinished text stays where it was
    public void SH28_the_rest_of_unfinished_text_is_kept()
    {
        Assert.Equal("=SUM(  1, $B$2|  ,\"x\"", Cycled("=SUM(  1, B2|  ,\"x\""));
        Assert.Equal("=IF(A1>0,$C$3|", Cycled("=IF(A1>0,C3|"));
    }

    [Fact] // ADR-0051: only the $ signs are written — the case and the order typed are kept until the Formula is entered (the implementation's reading, asked of Excel in the sixth run)
    public void Only_the_dollar_signs_change()
    {
        Assert.Equal("=$b$2|", Cycled("=b2|"));
        Assert.Equal("=$B$2:$A$1|", Cycled("=B2:A1|"));
        Assert.Equal("=sheet2!$a:$a|", Cycled("=sheet2!a:a|"));
    }

    [Fact] // ADR-0051: a selection outside the text is an argument error, not a guess
    public void A_selection_outside_the_text_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FormulaEntry.CycleReference("=A1", -1, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => FormulaEntry.CycleReference("=A1", 1, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => FormulaEntry.CycleReference("=A1", 3, 2));
        Assert.Throws<ArgumentNullException>(() => FormulaEntry.CycleReference(null!, 0, 0));
    }
}
