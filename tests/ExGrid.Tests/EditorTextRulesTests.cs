using ExGrid.Cells;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// What formula entry's own operations do to the editor's text (ADR-0051): a candidate accepted,
/// a span replaced, and the hint split for painting.
/// </summary>
public class EditorTextRulesTests
{
    [Fact] // ADR-0051: accepting a candidate replaces its span, and the caret lands after it
    public void Accepting_a_candidate_replaces_its_span()
    {
        var (text, caret) = EditorTextRules.Accept("=SU+1", new CompletionCandidate("SUM", 1, 2, "SUM("));

        Assert.Equal("=SUM(+1", text);
        Assert.Equal(5, caret);
    }

    [Fact] // ADR-0051: a zero-length span inserts
    public void A_zero_length_span_inserts()
    {
        var (text, caret) = EditorTextRules.Replace("=SUM()", 5, 0, "A3");

        Assert.Equal("=SUM(A3)", text);
        Assert.Equal(7, caret);
    }

    [Theory] // ADR-0051: a span outside the text is refused by name, never clamped onto the wrong characters
    [InlineData(-1, 0)]
    [InlineData(4, 0)]
    [InlineData(2, 2)]
    [InlineData(0, -1)]
    public void A_span_outside_the_text_is_refused(int start, int length)
    {
        var refused = Assert.Throws<ArgumentOutOfRangeException>(() => EditorTextRules.Replace("=SU", start, length, "X"));
        Assert.Contains("ADR-0051", refused.Message);
    }

    [Fact] // ADR-0051: the hint splits around the argument the caret is in
    public void The_hint_splits_around_its_emphasis()
    {
        var hint = new EditorHint("SUM(number1, [number2], …)", 4, 7);

        Assert.Equal(("SUM(", "number1", ", [number2], …)"), hint.Parts());
    }

    [Theory] // ADR-0051: an emphasis outside the hint sets off nothing rather than a guess
    [InlineData(0, 0)]
    [InlineData(-1, 2)]
    [InlineData(3, 10)]
    public void An_emphasis_outside_the_hint_sets_off_nothing(int start, int length)
        => Assert.Equal(("SUM(", "", ""), new EditorHint("SUM(", start, length).Parts());

    [Fact] // ADR-0051: an answer with neither candidates nor hint is empty
    public void An_answer_with_nothing_in_it_is_empty()
    {
        Assert.True(EditorCompletion.None.IsEmpty);
        Assert.False(new EditorCompletion([], new EditorHint("SUM(")).IsEmpty);
        Assert.False(new EditorCompletion([new CompletionCandidate("SUM", 1, 2, "SUM(")]).IsEmpty);
    }
}
