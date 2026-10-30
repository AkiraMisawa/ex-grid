namespace ExSheet.Engine;

/// <summary>
/// A cell's Cell Format as it shows (<c>CONTEXT.md</c>; ADR-0047, ADR-0071): each part from the
/// level that records it, cell over row over column, and the part's default where no level does.
/// </summary>
/// <param name="NumberFormat">The Number Format; <see cref="Engine.NumberFormat.General"/> where no level records one.</param>
/// <param name="Alignment">The horizontal alignment setting; <see cref="HorizontalAlignment.General"/> where no level records one.</param>
/// <param name="Font">The Font; <see cref="CellFont.Default"/> where no level records one.</param>
/// <param name="Fill">The Fill; <see cref="CellFill.None"/> where no level records one.</param>
/// <param name="Borders">The four sides; <see cref="CellBorders.None"/> where no level records them.</param>
public sealed record CellFormat(NumberFormat NumberFormat, HorizontalAlignment Alignment, CellFont Font, CellFill Fill, CellBorders Borders)
{
    /// <summary>What a cell shows where no level records anything: General, General alignment, the default Font, no Fill and no Borders.</summary>
    public static CellFormat Default { get; } = new(NumberFormat.General, HorizontalAlignment.General, CellFont.Default, CellFill.None, CellBorders.None);
}

/// <summary>
/// A cell's Font (<c>CONTEXT.md</c>; ADR-0071): the colour and emphasis of its text. Not its size
/// or typeface: every row has one height, and one digit width decides what fits. The default value
/// is <see cref="Default"/>.
/// </summary>
/// <param name="Colour">The text's colour; <see cref="CellColour.Automatic"/> is the Ink.</param>
/// <param name="Bold">Bold.</param>
/// <param name="Italic">Italic.</param>
/// <param name="Underline">A single underline.</param>
/// <param name="Strikethrough">Strikethrough.</param>
public readonly record struct CellFont(CellColour Colour = default, bool Bold = false, bool Italic = false, bool Underline = false, bool Strikethrough = false)
{
    /// <summary>What a cell's text shows where no level records a Font: Automatic, with no emphasis.</summary>
    public static CellFont Default => default;
}

/// <summary>
/// A cell's Fill (<c>CONTEXT.md</c>; ADR-0071): the one solid colour behind its text, or
/// <see cref="None"/>, where the Paper shows. No patterns and no gradients. The default value is
/// <see cref="None"/>.
/// </summary>
public readonly record struct CellFill
{
    private CellFill(CellColour colour) => Colour = colour;

    /// <summary>No Fill: the Paper shows. Recorded on a cell, it hides a Fill its row or column records.</summary>
    public static CellFill None => default;

    /// <summary>A solid Fill in <paramref name="colour"/>.</summary>
    /// <exception cref="ArgumentException">
    /// The colour is Automatic. A Fill is an RGB value: Excel's Fill offers No Color and colours,
    /// never Automatic, so there is nothing an Automatic Fill would mean.
    /// </exception>
    public static CellFill Solid(CellColour colour) =>
        colour.IsAutomatic ? throw new ArgumentException("A Fill's colour is an RGB value, not Automatic.", nameof(colour)) : new(colour);

    /// <summary>The Fill's colour, always an RGB value; <see langword="null"/> for <see cref="None"/>.</summary>
    public CellColour? Colour { get; }

    /// <summary>Whether this is <see cref="None"/>.</summary>
    public bool IsNone => Colour is null;
}
