using ExSheet.Engine;
using Xunit;

namespace ExSheet.Engine.Tests;

/// <summary>
/// F4 cycles the Reference at the caret (ADR-0051, 2026-09-29; SH-28): the engine's rewrite of
/// the text being edited, as Excel does it (the seventeenth Windows run, Part C). A <c>|</c> marks
/// the caret; two of them mark a selection, from the first to the second. The engine answers the
/// span of what it rewrote, for a caret as for a selection: the grid selects it where the key
/// carried a selection or a caret the user moved, and leaves a caret at its end where typing left
/// the caret (ADR-0051), so <see cref="Cycled"/> writes both marks and <see cref="CycledToCaret"/>
/// the caret at the span's end.
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

    /// <summary>The answer with only the caret at the span's end marked, as the grid places it where typing left the caret, and while pointing.</summary>
    private static string? CycledToCaret(string marked)
    {
        var (text, start, end) = Marked(marked);
        var answer = FormulaEntry.CycleReference(text, start, end);
        return answer?.Text.Insert(answer.SelectionEnd, "|");
    }

    [Theory] // ADR-0051 / SH-28: A1 → $A$1 → A$1 → $A1 → A1, the span of the rewritten Reference answered (Part C, case 1)
    [InlineData("=B2|", "=|$B$2|")]
    [InlineData("=$B$2|", "=|B$2|")]
    [InlineData("=B$2|", "=|$B2|")]
    [InlineData("=$B2|", "=|B2|")]
    public void SH28_a_reference_cycles_through_its_four_forms(string marked, string expected)
    {
        Assert.Equal(expected, Cycled(marked));
    }

    [Fact] // ADR-0051 / SH-28: four presses bring the Reference back to where it started, the caret after each in Enter mode (Part C, case 1)
    public void SH28_four_presses_come_back_to_the_relative_form()
    {
        var text = "=B2|";
        var seen = new List<string>();
        for (var press = 0; press < 4; press++)
        {
            text = CycledToCaret(text)!;
            seen.Add(text);
        }

        Assert.Equal(["=$B$2|", "=B$2|", "=$B2|", "=B2|"], seen);
    }

    [Theory] // ADR-0051 / SH-28: the Reference at the caret is the one it is inside or touching, on either side, and its span is answered (Part C, cases 2 to 4: $A$1 selected, 1 to 5)
    [InlineData("=A1|+B2", "=|$A$1|+B2")]
    [InlineData("=|A1+B2", "=|$A$1|+B2")]
    [InlineData("=A|1+B2", "=|$A$1|+B2")]
    [InlineData("=$|A1+B2", "=|A1|+B2")]
    [InlineData("=A1+|B2", "=A1+|$B$2|")]
    [InlineData("=SUM(|A1)", "=SUM(|$A$1|)")]
    [InlineData("=-A1|", "=-|$A$1|")]
    public void SH28_the_reference_the_caret_is_inside_or_touching_cycles(string marked, string expected)
    {
        Assert.Equal(expected, Cycled(marked));
    }

    [Fact] // ADR-0051 / SH-28 / C-diff-4: the two ends of a range are References of their own — =SUM(A1:B2), the caret after B2, F4 four times cycles B2 alone, selected in Edit mode (Part C, case 5: 8-12, 8-11, 8-11, 8-10)
    public void SH28_only_the_end_of_a_range_at_the_caret_cycles()
    {
        var text = "=SUM(A1:B2|)";
        var seen = new List<string>();
        for (var press = 0; press < 4; press++)
        {
            text = Cycled(text)!;
            seen.Add(text);
        }

        Assert.Equal(["=SUM(A1:|$B$2|)", "=SUM(A1:|B$2|)", "=SUM(A1:|$B2|)", "=SUM(A1:|B2|)"], seen);
    }

    [Theory] // ADR-0051 / SH-28 / C-diff-4: A1|:B2 touches A1, A1:|B2 touches B2; each end takes its own next form
    [InlineData("=SUM(|A1:B2)", "=SUM(|$A$1|:B2)")]
    [InlineData("=SUM(A1|:B2)", "=SUM(|$A$1|:B2)")]
    [InlineData("=SUM(A|1:B2)", "=SUM(|$A$1|:B2)")]
    [InlineData("=SUM(A1:|B2)", "=SUM(A1:|$B$2|)")]
    [InlineData("=SUM(A1:B|2)", "=SUM(A1:|$B$2|)")]
    [InlineData("=SUM($A$1:$B$2|)", "=SUM($A$1:|B$2|)")]
    [InlineData("=SUM(|$A$1:$B$2)", "=SUM(|A$1|:$B$2)")]
    public void SH28_the_end_of_a_range_the_caret_touches_cycles(string marked, string expected)
    {
        Assert.Equal(expected, Cycled(marked));
    }

    [Theory] // ADR-0051 / SH-28 / C-diff-5, C-diff-6: the end at the caret takes its own next form and the other end is left (Part C, case 6: =$A1:B2 gives =$A1:$B$2; case 22: =B2:A1 gives =B2:$A$1)
    [InlineData("=$A1:B2|", "=$A1:$B$2|")]
    [InlineData("=B2:A1|", "=B2:$A$1|")]
    [InlineData("=A1:$B$2|", "=A1:B$2|")]
    [InlineData("=$A$1:B2|", "=$A$1:$B$2|")]
    [InlineData("=|$A1:B2", "=A1|:B2")]
    public void SH28_the_other_end_of_a_range_is_left(string marked, string expected)
    {
        Assert.Equal(expected, CycledToCaret(marked));
    }

    [Theory] // ADR-0051 / SH-28: a Sheet qualifier stands before the first end — a caret in it cycles the first end, and a caret at the second end leaves it; the span answered starts after the qualifier (observed, run 18a: =Sheet2!A1+1 with the caret after A1 gave $A$1 selected)
    [InlineData("=Sheet2!A1:B2|", "=Sheet2!A1:|$B$2|")]
    [InlineData("=She|et2!A1:B2", "=Sheet2!|$A$1|:B2")]
    [InlineData("=Sheet2!|A1:B2", "=Sheet2!|$A$1|:B2")]
    public void SH28_a_sheet_qualifier_stands_before_the_first_end(string marked, string expected)
    {
        Assert.Equal(expected, Cycled(marked));
    }

    [Theory] // ADR-0051 / SH-28: a selection cycles every Reference — every end of a range — it covers, overlaps or touches at either end, each to the next form of the first, and covers what was rewritten (Part C, cases 7, 18 to 20)
    [InlineData("=|A1+B2|", "=|$A$1+$B$2|")]
    [InlineData("=A1|+|B1", "=|$A$1+$B$1|")]
    [InlineData("=A1|+2|", "=|$A$1|+2")]
    [InlineData("=|1+|A1", "=1+|$A$1|")]
    [InlineData("=SUM(A1|,|B2)", "=SUM(|$A$1,$B$2|)")]
    [InlineData("=|$A1+B$2|", "=|A1+B2|")]
    [InlineData("=A|1+B|2", "=|$A$1+$B$2|")]
    [InlineData("=SUM(|A1,B2:C3,$D$4|)", "=SUM(|$A$1,$B$2:$C$3,$D$4|)")]
    [InlineData("|=A1*2+B2|", "=|$A$1*2+$B$2|")]
    [InlineData("=|A1+B:B|", "=|$A$1+$B:$B|")]
    [InlineData("=1+|A1|*2", "=1+|$A$1|*2")]
    [InlineData("=SUM(|$A1:B2|)", "=SUM(|A1:B2|)")]
    [InlineData("=SUM(A1|:|B2)", "=SUM(|$A$1:$B$2|)")]
    [InlineData("=SUM(A1:|B2|)", "=SUM(A1:|$B$2|)")]
    [InlineData("=SUM(|A1|:B2)", "=SUM(|$A$1|:B2)")]
    [InlineData("=SUM(A1:B|2)+|C3", "=SUM(A1:|$B$2)+$C$3|")]
    public void SH28_a_selection_cycles_every_reference_it_covers(string marked, string expected)
    {
        Assert.Equal(expected, Cycled(marked));
    }

    [Theory] // ADR-0051 / SH-28: whole columns and whole rows are one Reference of two forms, the whole of it answered (Part C, cases 8 and 9: $A:$A selected, 5 to 10)
    [InlineData("=SUM(A:A|)", "=SUM(|$A:$A|)")]
    [InlineData("=SUM($A:$A|)", "=SUM(|A:A|)")]
    [InlineData("=SUM(A|:A)", "=SUM(|$A:$A|)")]
    [InlineData("=SUM(A:C|)", "=SUM(|$A:$C|)")]
    [InlineData("=SUM($A:C|)", "=SUM(|A:C|)")]
    [InlineData("=SUM(1:1|)", "=SUM(|$1:$1|)")]
    [InlineData("=SUM($1:$1|)", "=SUM(|1:1|)")]
    [InlineData("=SUM(2:5|)", "=SUM(|$2:$5|)")]
    public void SH28_whole_columns_and_rows_have_two_forms(string marked, string expected)
    {
        Assert.Equal(expected, Cycled(marked));
    }

    [Fact] // ADR-0051 / SH-28: a pointed range is asked as a selection over the whole of it, and both ends cycle together, press after press (observed, the readings of 2026-10-12, R1 (vi): =, ↓, Shift+↓, F4 in D10 gave =$D$11:$D$12)
    public void SH28_a_pointed_range_cycles_both_ends_together()
    {
        var text = "=|D11:D12|";
        var seen = new List<string>();
        for (var press = 0; press < 4; press++)
        {
            text = Cycled(text)!;
            seen.Add(text);
        }

        Assert.Equal(["=|$D$11:$D$12|", "=|D$11:D$12|", "=|$D11:$D12|", "=|D11:D12|"], seen);
    }

    [Fact] // ADR-0051 / SH-28 / C-diff-7: =SUM(A:A), the caret after the Reference, F4 three times gives $A:$A, A:A, $A:$A — a cycle of two (Part C, cases 8 and 9)
    public void SH28_a_whole_column_cycles_in_two()
    {
        foreach (var start in new[] { "=SUM(A:A|)", "=SUM(1:1|)" })
        {
            var text = start;
            var seen = new List<string>();
            for (var press = 0; press < 3; press++)
            {
                text = CycledToCaret(text)!;
                seen.Add(text);
            }

            var (absolute, relative) = start.Contains('A', StringComparison.Ordinal) ? ("$A:$A", "A:A") : ("$1:$1", "1:1");
            Assert.Equal([$"=SUM({absolute}|)", $"=SUM({relative}|)", $"=SUM({absolute}|)"], seen);
        }
    }

    [Theory] // ADR-0051 / SH-28: a Sheet qualifier is kept, and only the cell part cycles (Part C, case 10); the span answered starts after the qualifier, at $A$1 (observed, run 18a, answer 4)
    [InlineData("=Sheet2!A1|", "=Sheet2!|$A$1|")]
    [InlineData("=Sheet2!$A$1|", "=Sheet2!|A$1|")]
    [InlineData("=She|et2!A1", "=Sheet2!|$A$1|")]
    [InlineData("='My Sheet'!B2:C3|", "='My Sheet'!B2:|$C$3|")]
    [InlineData("=SUM(Sheet2!A:A|)", "=SUM(Sheet2!|$A:$A|)")]
    public void SH28_a_sheet_qualifier_is_kept(string marked, string expected)
    {
        Assert.Equal(expected, Cycled(marked));
    }

    [Fact] // ADR-0051, run 18a answer 4: with F2 first, =Sheet2!A1+1 and the caret after A1, F4 gave =Sheet2!$A$1+1 with $A$1 selected (8-12) — the span starts at $A$1, not at Sheet2!
    public void ADR0051_the_span_after_F4_starts_after_a_sheet_qualifier()
    {
        Assert.Equal(new ReferenceCycle("=Sheet2!$A$1+1", 8, 12), FormulaEntry.CycleReference("=Sheet2!A1+1", 10, 10));
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

    [Theory] // ADR-0051 / SH-28: a caret touching no Reference changes nothing, nor does a selection covering, overlapping and touching none
    [InlineData("=|")]
    [InlineData("|=A1")]
    [InlineData("=SUM(|)")]
    [InlineData("=A1 |+ B2")]
    [InlineData("=A1%|")]
    [InlineData("=A1 |+| B2")]
    [InlineData("=|1+2|")]
    public void SH28_nothing_at_the_caret_changes_nothing(string marked)
    {
        Assert.Null(Cycled(marked));
    }

    [Fact] // ADR-0051 / SH-28: only the Reference moves; every other character of unfinished text stays where it was
    public void SH28_the_rest_of_unfinished_text_is_kept()
    {
        Assert.Equal("=SUM(  1, $B$2|  ,\"x\"", CycledToCaret("=SUM(  1, B2|  ,\"x\""));
        Assert.Equal("=IF(A1>0,$C$3|", CycledToCaret("=IF(A1>0,C3|"));
    }

    [Fact] // ADR-0051 / SH-28 / C-diff-8: the rewritten Reference's column letters are written in upper case, and the rest of the text — a Sheet qualifier, the other end of a range — as typed, in the order typed (Part C, cases 21 and 22)
    public void SH28_the_rewritten_reference_is_written_in_upper_case()
    {
        Assert.Equal("=$B$2|", CycledToCaret("=b2|"));
        Assert.Equal("=B2:$A$1|", CycledToCaret("=B2:A1|"));
        Assert.Equal("=b2:$A$1|", CycledToCaret("=b2:a1|"));
        Assert.Equal("=sheet2!$A:$A|", CycledToCaret("=sheet2!a:a|"));
        Assert.Equal("=sheet2!$AB$3|+c1", CycledToCaret("=sheet2!ab3|+c1"));
        Assert.Equal("=|$A$1+$B$2|", Cycled("=|a1+b2|"));
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
