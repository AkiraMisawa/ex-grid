namespace ExGrid.Cells;

/// <summary>
/// A colour of the Reference Outline palette (ADR-0057), named by its place in the palette and
/// never by how it looks: the colour at place <c>n</c> is painted from the Visual Token
/// <c>--ex-reference-n</c>, which a Theme sets (ADR-0027/0029). A Consumer never picks one. The
/// core hands them out, and tells the Consumer the one each key was given. The default is the
/// palette's first colour.
/// </summary>
public readonly record struct ReferenceColour
{
    /// <summary>
    /// How many colours the palette has. It is behaviour, not appearance: it decides which
    /// References share a colour, so it lives here and not in the stylesheet (ADR-0057). A Theme
    /// sets the colours, never how many there are. Seven, as the eighth Windows run observed
    /// Excel's range finder cycle.
    /// </summary>
    public const int PaletteLength = 7;

    // The place less one, so that the default is the first colour rather than no colour.
    private readonly int _index;

    /// <summary>The colour at <paramref name="place"/> in the palette.</summary>
    /// <param name="place">1 to <see cref="PaletteLength"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">The place is outside the palette.</exception>
    public ReferenceColour(int place)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(place, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(place, PaletteLength);
        _index = place - 1;
    }

    /// <summary>Its place in the palette, 1 to <see cref="PaletteLength"/>: the <c>n</c> of
    /// <c>--ex-reference-n</c>.</summary>
    public int Place => _index + 1;

    /// <summary>The colour handed to the <paramref name="appearance"/>th distinct thing a text
    /// refers to, counted from 0 in order of first appearance: round the palette, so the eighth
    /// takes the first colour again.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The count is negative.</exception>
    public static ReferenceColour ForAppearance(int appearance)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(appearance);
        return new ReferenceColour((appearance % PaletteLength) + 1);
    }
}
