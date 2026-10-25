using ExGrid.Cells;
using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// Who wears which colour among the References in one text (ADR-0057, DC-46). The shared cases are in
/// <c>tests/cases/cells/</c>; what stays here is what only C# can say: a null, a <c>default</c> value, the
/// one None instance and the palette's length as a constant.
/// </summary>
public class ReferenceColouringTests
{
    private static readonly SelectionRange A1 = new(0, 0, 1, 1);

    [Fact] // ADR-0057 / DC-46, the eighth Windows run: the palette has seven colours, as Excel's
    public void The_palette_has_seven_colours()
        => Assert.Equal(7, ReferenceColour.PaletteLength);

    [Fact] // ADR-0057: no answer, or an empty one, is the one colouring of nothing
    public void No_answer_is_the_one_colouring_of_nothing()
    {
        Assert.Same(ReferenceColouring.None, ReferenceColouring.Of("5", null));
        Assert.Same(ReferenceColouring.None, ReferenceColouring.Of("5", []));
    }

    [Fact] // ADR-0057: a null Reference in the answer is refused by name
    public void A_null_reference_is_refused()
        => Assert.Throws<ArgumentNullException>(() => ReferenceColouring.Of("=A1", [null!]));

    [Fact] // ADR-0057: a null key is refused by name
    public void A_null_key_is_refused()
        => Assert.Throws<ArgumentNullException>(() => new EditorReference(1, 2, (string)null!));

    [Fact] // ADR-0057: the default colour is the palette's first
    public void The_default_is_the_first_colour()
        => Assert.Equal(1, default(ReferenceColour).Place);

    [Fact] // ADR-0057: a colour of cells is found by the range, as the component reads it
    public void A_colour_is_found_by_its_range()
        => Assert.Equal(new ReferenceColour(1), ReferenceColouring.Of("=A1", [new EditorReference(1, 2, A1)]).ColourOf(A1));
}
