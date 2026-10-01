namespace ExSheet.Engine;

/// <summary>
/// What a formatting command sets on the Cell Format of ranges (ADR-0063): every part that is not
/// <see langword="null"/>, each Font emphasis on its own, and every other part left as each cell
/// has it. Its <see cref="Borders"/> are relative to each range it is set on, so several ranges
/// each get their own outline, as in Excel.
/// </summary>
public sealed record CellFormatChange
{
    /// <summary>The Number Format to set; <see cref="Engine.NumberFormat.General"/> sets General.</summary>
    public NumberFormat? NumberFormat { get; init; }

    /// <summary>The horizontal alignment to set; <see cref="HorizontalAlignment.General"/> sets General.</summary>
    public HorizontalAlignment? Alignment { get; init; }

    /// <summary>The Font colour to set; <see cref="CellColour.Automatic"/> sets the Ink.</summary>
    public CellColour? FontColour { get; init; }

    /// <summary>Bold set on, or off.</summary>
    public bool? Bold { get; init; }

    /// <summary>Italic set on, or off.</summary>
    public bool? Italic { get; init; }

    /// <summary>A single underline set on, or off.</summary>
    public bool? Underline { get; init; }

    /// <summary>Strikethrough set on, or off.</summary>
    public bool? Strikethrough { get; init; }

    /// <summary>The Fill to set; <see cref="CellFill.None"/> takes it away.</summary>
    public CellFill? Fill { get; init; }

    /// <summary>The Borders to set, relative to each range the change is set on.</summary>
    public BorderChange? Borders { get; init; }

    /// <summary>Whether the change sets no part at all.</summary>
    public bool IsEmpty =>
        NumberFormat is null && Alignment is null && !SetsFont && Fill is null && !SetsBorders;

    /// <summary>Whether the change sets any part of the Font.</summary>
    internal bool SetsFont => FontColour is not null || Bold is not null || Italic is not null || Underline is not null || Strikethrough is not null;

    /// <summary>Whether the change sets any side.</summary>
    internal bool SetsBorders => Borders is { IsEmpty: false };

    /// <summary>Whether the change sets a part that <paramref name="level"/> records.</summary>
    internal bool Touches(AxisFormat level) =>
        (NumberFormat is not null && level.NumberFormat is not null)
        || (Alignment is not null && level.Alignment is not null)
        || (SetsFont && level.Font is not null)
        || (Fill is not null && level.Fill is not null)
        || (SetsBorders && level.Borders is not null);

    /// <summary>What a cell that showed <paramref name="shown"/> shows once the change is set on it, at <paramref name="place"/> in its range.</summary>
    internal CellFormat ApplyTo(CellFormat shown, PlaceInRange place) => new(
        NumberFormat ?? shown.NumberFormat,
        Alignment ?? shown.Alignment,
        ApplyTo(shown.Font),
        Fill ?? shown.Fill,
        Borders is { } borders ? borders.ApplyTo(shown.Borders, place) : shown.Borders);

    /// <summary><paramref name="font"/> with the emphases and the colour the change sets.</summary>
    internal CellFont ApplyTo(CellFont font) => new(
        FontColour ?? font.Colour,
        Bold ?? font.Bold,
        Italic ?? font.Italic,
        Underline ?? font.Underline,
        Strikethrough ?? font.Strikethrough);
}

/// <summary>
/// The Borders a change sets, relative to each range it is set on (ADR-0063), as Excel's Format
/// Cells sets them: each edge that is not <see langword="null"/> is set — to a line, or to
/// <see cref="BorderLine.None"/> to take the line away — and every other edge is left as each cell
/// has it. <see cref="Outline"/>, <see cref="Inside"/> and <see cref="None"/> are Excel's presets.
/// </summary>
/// <remarks>
/// The line between two cells is one line, as Excel's is (the eleventh Windows run, cases 7 and 13):
/// an outer edge is set on the range's cells and on the cells beside it, which read the same line
/// from the other side. An edge on the Sheet's outer edge has no cell beside it. Whole columns have
/// no top or bottom edge, and whole rows no left or right edge, so an outline over them sets only
/// their sides (case 15); the whole Sheet is its columns.
/// </remarks>
public sealed record BorderChange
{
    /// <summary>The range's top edge: the top side of its first row's cells, and the bottom side of the cells above them.</summary>
    public BorderLine? Top { get; init; }

    /// <summary>The range's bottom edge: the bottom side of its last row's cells, and the top side of the cells below them.</summary>
    public BorderLine? Bottom { get; init; }

    /// <summary>The range's left edge: the left side of its first column's cells, and the right side of the cells to their left.</summary>
    public BorderLine? Left { get; init; }

    /// <summary>The range's right edge: the right side of its last column's cells, and the left side of the cells to their right.</summary>
    public BorderLine? Right { get; init; }

    /// <summary>Every edge between two of the range's rows: the bottom side of the upper cell and the top side of the lower.</summary>
    public BorderLine? InsideHorizontal { get; init; }

    /// <summary>Every edge between two of the range's columns: the right side of the left cell and the left side of the right.</summary>
    public BorderLine? InsideVertical { get; init; }

    /// <summary>Excel's Outline: <paramref name="line"/> on the range's four outer edges.</summary>
    public static BorderChange Outline(BorderLine line) => new() { Top = line, Bottom = line, Left = line, Right = line };

    /// <summary>Excel's Inside: <paramref name="line"/> on every edge inside the range.</summary>
    public static BorderChange Inside(BorderLine line) => new() { InsideHorizontal = line, InsideVertical = line };

    /// <summary>Excel's None: every edge of the range, outer and inner, without a line.</summary>
    public static BorderChange None { get; } = new()
    {
        Top = BorderLine.None,
        Bottom = BorderLine.None,
        Left = BorderLine.None,
        Right = BorderLine.None,
        InsideHorizontal = BorderLine.None,
        InsideVertical = BorderLine.None,
    };

    /// <summary>Whether the change sets no edge.</summary>
    internal bool IsEmpty =>
        Top is null && Bottom is null && Left is null && Right is null && InsideHorizontal is null && InsideVertical is null;

    /// <summary>The four sides a cell at <paramref name="place"/> in its range shows once the change is set, from <paramref name="borders"/>.</summary>
    internal CellBorders ApplyTo(CellBorders borders, PlaceInRange place) => new(
        (place.FirstRow ? Top : InsideHorizontal) ?? borders.Top,
        (place.LastRow ? Bottom : InsideHorizontal) ?? borders.Bottom,
        (place.FirstColumn ? Left : InsideVertical) ?? borders.Left,
        (place.LastColumn ? Right : InsideVertical) ?? borders.Right);

    /// <summary>
    /// The cells beside <paramref name="range"/> across each outer edge the change sets, with the
    /// change that sets their side of it to the same line. An edge on the Sheet's outer edge has
    /// none, which is also why whole columns have none above or below and whole rows none to
    /// either side.
    /// </summary>
    internal IEnumerable<(CellRange Range, BorderChange Borders)> Beside(CellRange range)
    {
        if (Top is { } top && range.First.Row > 0)
        {
            yield return (new CellRange(new CellAddress(range.First.Row - 1, range.First.Column), new CellAddress(range.First.Row - 1, range.Last.Column)), new BorderChange { Bottom = top });
        }
        if (Bottom is { } bottom && range.Last.Row < Sheet.RowCount - 1)
        {
            yield return (new CellRange(new CellAddress(range.Last.Row + 1, range.First.Column), new CellAddress(range.Last.Row + 1, range.Last.Column)), new BorderChange { Top = bottom });
        }
        if (Left is { } left && range.First.Column > 0)
        {
            yield return (new CellRange(new CellAddress(range.First.Row, range.First.Column - 1), new CellAddress(range.Last.Row, range.First.Column - 1)), new BorderChange { Right = left });
        }
        if (Right is { } right && range.Last.Column < Sheet.ColumnCount - 1)
        {
            yield return (new CellRange(new CellAddress(range.First.Row, range.Last.Column + 1), new CellAddress(range.Last.Row, range.Last.Column + 1)), new BorderChange { Left = right });
        }
    }
}

/// <summary>
/// Where a cell lies in a range a change is set on: which of the range's outer edges its sides are.
/// A side on no outer edge is inside the range.
/// </summary>
internal readonly record struct PlaceInRange(bool FirstRow, bool LastRow, bool FirstColumn, bool LastColumn)
{
    /// <summary>A cell set on its own: all four sides are outer edges.</summary>
    public static PlaceInRange Alone { get; } = new(true, true, true, true);

    /// <summary>A cell none of whose sides is an outer edge.</summary>
    public static PlaceInRange Inside { get; } = new(false, false, false, false);

    /// <summary>
    /// Where <paramref name="at"/> lies in <paramref name="range"/>. Whole columns have no top or
    /// bottom edge, and whole rows no left or right edge: an outline over whole columns sets only
    /// their left and right edges (the eleventh Windows run, case 15), and over whole rows only
    /// their top and bottom, a reading by mirror. The whole Sheet is its columns, also a reading by
    /// mirror.
    /// </summary>
    public static PlaceInRange Of(CellRange range, CellAddress at)
    {
        var columns = range.IsWholeColumns;
        var rows = range.IsWholeRows && !columns;
        return new(
            !columns && at.Row == range.First.Row,
            !columns && at.Row == range.Last.Row,
            !rows && at.Column == range.First.Column,
            !rows && at.Column == range.Last.Column);
    }
}
