using ExGrid.Cells;
using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// Who wears which colour among the References in one text (ADR-0057, DC-46): the same cells or
/// the same key share a colour, colours are handed out in order of first appearance, round a
/// palette of seven whose length is behaviour and lives in C#; an answer that would colour the
/// wrong characters is refused by name.
/// </summary>
public class ReferenceColouringTests
{
    private static readonly SelectionRange A1 = new(0, 0, 1, 1);
    private static readonly SelectionRange B2C3 = new(1, 1, 2, 2);

    private static EditorReference Cells(int start, int length, SelectionRange range) => new(start, length, range);

    private static EditorReference Keyed(int start, int length, string key) => new(start, length, key);

    private static int[] Places(ReferenceColouring colouring) => [.. colouring.Colours.Select(c => c.Place)];

    [Fact] // ADR-0057 / DC-46: =A1+B2:C3 gives two colours, in the order the References stand
    public void Two_ranges_take_two_colours_in_order()
    {
        var colouring = ReferenceColouring.Of("=A1+B2:C3", [Cells(1, 2, A1), Cells(4, 5, B2C3)]);

        Assert.Equal([1, 2], Places(colouring));
        Assert.Equal([(A1, new ReferenceColour(1)), (B2C3, new ReferenceColour(2))], colouring.Ranges);
        Assert.Empty(colouring.Keys);
    }

    [Fact] // ADR-0057 / DC-46, the eighth Windows run: =A1+A1 names one range, so it is one colour, but each Reference draws its own outline
    public void The_same_range_twice_is_one_colour_and_two_outlines()
    {
        var colouring = ReferenceColouring.Of("=A1+A1", [Cells(1, 2, A1), Cells(4, 2, A1)]);

        Assert.Equal([1, 1], Places(colouring));
        Assert.Equal([(A1, new ReferenceColour(1))], colouring.Ranges);
        Assert.Equal([(A1, new ReferenceColour(1)), (A1, new ReferenceColour(1))], colouring.Outlines);
    }

    [Fact] // ADR-0057 / DC-46, the eighth Windows run: the outlines follow the text, one per Reference with cells, and a key draws none here
    public void The_outlines_are_one_per_reference_with_cells_in_the_texts_order()
    {
        var colouring = ReferenceColouring.Of("=B2:C3+T[a]+A1", [Cells(12, 2, A1), Keyed(7, 4, "t/a"), Cells(1, 5, B2C3)]);

        Assert.Equal([(B2C3, new ReferenceColour(1)), (A1, new ReferenceColour(3))], colouring.Outlines);
    }

    [Fact] // ADR-0057 (reading): =A1+$A$1 — however written, the same cells share a colour, and a range named again takes no new one
    public void A_range_named_again_does_not_use_up_a_colour()
    {
        var colouring = ReferenceColouring.Of("=A1+$A$1+B2:C3", [Cells(1, 2, A1), Cells(4, 4, A1), Cells(9, 5, B2C3)]);

        Assert.Equal([1, 1, 2], Places(colouring));
    }

    [Fact] // ADR-0057: first appearance is where a Reference stands in the text, whatever order the answer lists them in
    public void First_appearance_is_the_place_in_the_text()
    {
        var colouring = ReferenceColouring.Of("=A1+B2:C3", [Cells(4, 5, B2C3), Cells(1, 2, A1)]);

        Assert.Equal([1, 4], colouring.References.Select(r => r.Start).ToArray());
        Assert.Equal([1, 2], Places(colouring));
        Assert.Equal(new ReferenceColour(1), colouring.ColourOf(A1));
        Assert.Equal(new ReferenceColour(2), colouring.ColourOf(B2C3));
    }

    [Fact] // ADR-0057 / DC-46: a key shares its colour with every Reference carrying it, and counts in the order with the ranges
    public void Keys_share_a_colour_and_count_with_the_ranges()
    {
        const string text = "=A1+SUM(T[PV])+T[PV]+T[Qty]";
        var colouring = ReferenceColouring.Of(text,
        [
            Cells(1, 2, A1), Keyed(8, 5, "t/pv"), Keyed(15, 5, "t/pv"), Keyed(21, 6, "t/qty"),
        ]);

        Assert.Equal([1, 2, 2, 3], Places(colouring));
        Assert.Equal(
            [new ReferenceKeyColour("t/pv", new ReferenceColour(2)), new ReferenceKeyColour("t/qty", new ReferenceColour(3))],
            colouring.Keys);
        Assert.Equal([(A1, new ReferenceColour(1))], colouring.Ranges);
    }

    [Fact] // ADR-0057: keys are compared as written; one spelled differently is another thing
    public void Keys_are_compared_as_written()
    {
        var colouring = ReferenceColouring.Of("=T[a]+T[A]", [Keyed(1, 4, "t/a"), Keyed(6, 4, "t/A")]);

        Assert.Equal([1, 2], Places(colouring));
    }

    [Fact] // ADR-0057 / DC-46, the eighth Windows run: the palette has seven colours, as Excel's, and the eighth thing named takes the first again
    public void The_colours_go_round_a_palette_of_seven()
    {
        Assert.Equal(7, ReferenceColour.PaletteLength);
        var references = Enumerable.Range(0, 10).Select(i => Cells(1 + (3 * i), 2, new SelectionRange(i, 0, 1, 1))).ToArray();
        var text = "=" + string.Concat(Enumerable.Repeat("A1+", 10));

        var colouring = ReferenceColouring.Of(text, references);

        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 1, 2, 3], Places(colouring));
        Assert.Equal(10, colouring.Ranges.Count);
    }

    [Fact] // ADR-0057: no answer, or an empty one, colours nothing
    public void No_answer_colours_nothing()
    {
        Assert.Same(ReferenceColouring.None, ReferenceColouring.Of("5", null));
        Assert.Same(ReferenceColouring.None, ReferenceColouring.Of("5", []));
        Assert.Empty(ReferenceColouring.None.Ranges);
        Assert.Null(ReferenceColouring.None.ColourOf(A1));
    }

    [Theory] // ADR-0057: a span outside the text is refused by name, never coloured over the wrong characters
    [InlineData(2, 2)]
    [InlineData(4, 1)]
    public void A_span_outside_the_text_is_refused(int start, int length)
    {
        var refused = Assert.Throws<ArgumentOutOfRangeException>(() => ReferenceColouring.Of("=A1", [Cells(start, length, A1)]));
        Assert.Contains("ADR-0057", refused.Message);
    }

    [Fact] // ADR-0057: two spans that overlap are refused by name: a character belongs to one Reference at most
    public void Overlapping_spans_are_refused()
    {
        var refused = Assert.Throws<ArgumentException>(() => ReferenceColouring.Of("=B2:C3", [Cells(1, 5, B2C3), Cells(4, 2, A1)]));
        Assert.Contains("overlap", refused.Message);
    }

    [Fact] // ADR-0057: spans that meet without overlapping are two References
    public void Spans_that_meet_are_not_overlapping()
    {
        var colouring = ReferenceColouring.Of("=A1B2", [Cells(1, 2, A1), Cells(3, 2, B2C3)]);

        Assert.Equal([1, 2], Places(colouring));
    }

    [Fact] // ADR-0057: a null Reference in the answer is refused by name
    public void A_null_reference_is_refused()
        => Assert.Throws<ArgumentNullException>(() => ReferenceColouring.Of("=A1", [null!]));

    [Fact] // ADR-0057: a Reference covers at least one character from a place in the text, and a key is not empty
    public void A_reference_is_refused_without_a_span_or_a_key()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Cells(-1, 2, A1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Cells(1, 0, A1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Keyed(1, 0, "t/pv"));
        Assert.Throws<ArgumentException>(() => Keyed(1, 2, ""));
        Assert.Throws<ArgumentNullException>(() => Keyed(1, 2, null!));
    }

    [Fact] // ADR-0057: a Reference is either cells or a key, never both
    public void A_reference_is_cells_or_a_key()
    {
        var cells = Cells(1, 2, A1);
        var keyed = Keyed(1, 2, "t/pv");

        Assert.Equal(A1, cells.Range);
        Assert.Null(cells.Key);
        Assert.Null(keyed.Range);
        Assert.Equal("t/pv", keyed.Key);
    }

    [Theory] // ADR-0057 / UX-1: a colour is a place in the palette, 1 to 7, and nothing else
    [InlineData(0)]
    [InlineData(8)]
    public void A_place_outside_the_palette_is_refused(int place)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new ReferenceColour(place));

    [Fact] // ADR-0057: the default colour is the palette's first, and the count goes round from 0
    public void The_default_is_the_first_colour()
    {
        Assert.Equal(1, default(ReferenceColour).Place);
        Assert.Equal(new ReferenceColour(1), ReferenceColour.ForAppearance(0));
        Assert.Equal(new ReferenceColour(7), ReferenceColour.ForAppearance(6));
        Assert.Equal(new ReferenceColour(1), ReferenceColour.ForAppearance(7));
        Assert.Throws<ArgumentOutOfRangeException>(() => ReferenceColour.ForAppearance(-1));
    }
}
