using System.Globalization;
using ExSheet.Engine;

namespace ExSheet;

/// <summary>
/// What Format Cells opens on, and what the user has touched since (ADR-0071, SH-45): the rules
/// every Chrome's Format Cells sets its controls by, so that what a choice means and what OK sets
/// are ExSheet's under every Chrome.
///
/// <para>It opens on the Focus cell's Cell Format. A part that differs across the Selection is
/// shown as Excel shows it (the eleventh Windows run, case 24): the Font style empty
/// (<see cref="FontStyle"/> is <see langword="null"/>), a Fill as No Colour, and an edge as a grey
/// dotted line (<see cref="EdgeLine"/> is <see langword="null"/>). Every other part shows the Focus
/// cell's. A range's outer edges show as they are drawn, a neighbour's line where the cell records
/// none (the fourteenth run, case 13); an edge inside a range shows the cells' own sides. The Border
/// tab's line opens as Thin and Automatic.</para>
///
/// <para><see cref="Change"/> names only the parts the user touched, so OK leaves every other part
/// as each cell has it; with nothing touched it is empty, and OK sets nothing. An edge shown from a
/// neighbour and left alone writes nothing; taken away, it is cleared on both sides, as clearing an
/// edge is (SH-45; a reading, since no run pressed it). A choice that
/// cannot be set — a Custom code ExSheet does not read, More Colours text that is not a colour — is
/// held as <see cref="Refusal"/>, by name, and OK sets nothing until it is put right.</para>
/// </summary>
public sealed class FormatCellsDraft
{
    private readonly CultureInfo _culture;
    private readonly NumberFormatCodes.Recognised _openingNumber;
    private readonly BorderLine?[] _edges;
    private readonly bool[] _edgesTouched = new bool[6];
    private readonly Dictionary<ColourTarget, string> _colourText = [];
    private readonly Dictionary<ColourTarget, string> _colourRefusals = [];

    private bool _numberTouched;
    private bool _alignmentTouched;
    private bool _fontStyleTouched;
    private bool _underlineTouched;
    private bool _strikethroughTouched;
    private bool _fontColourTouched;
    private bool _fillTouched;

    private FormatCellsDraft(CellFormat opening, FormatCellsSpread spread, CultureInfo culture)
    {
        _culture = culture;
        Opening = opening;
        HasInsideHorizontal = spread.InsideHorizontal;
        HasInsideVertical = spread.InsideVertical;
        _openingNumber = NumberFormatCodes.Recognise(opening.NumberFormat, culture);
        RestoreNumber(_openingNumber);
        Alignment = opening.Alignment;
        FontStyle = spread.FontStyleDiffers ? null : StyleOf(opening.Font);
        Underline = opening.Font.Underline;
        Strikethrough = opening.Font.Strikethrough;
        FontColour = opening.Font.Colour;
        Fill = spread.FillDiffers ? CellFill.None : opening.Fill;
        _edges = [.. FormatCellsOffer.Edges.Select(edge => spread.Edges[(int)edge])];
    }

    /// <summary>The Focus cell's Cell Format, which Format Cells opens on.</summary>
    public CellFormat Opening { get; }

    /// <summary>Whether a selected range spans more than one row, so that it has an edge inside it between rows.</summary>
    public bool HasInsideHorizontal { get; }

    /// <summary>Whether a selected range spans more than one column, so that it has an edge inside it between columns.</summary>
    public bool HasInsideVertical { get; }

    // ---- Number ----

    /// <summary>The Number Format category chosen.</summary>
    public NumberFormatCategory Category { get; private set; }

    /// <summary>The decimal places, under Number, Currency, Percentage and Scientific.</summary>
    public int DecimalPlaces { get; private set; }

    /// <summary>Whether Number groups thousands.</summary>
    public bool ThousandsSeparator { get; private set; }

    /// <summary>Which of <see cref="NegativeNumbers"/> is chosen.</summary>
    public int NegativeNumber { get; private set; }

    /// <summary>The Date or Time format chosen, or the Custom code as typed.</summary>
    public string TypeCode { get; private set; } = "";

    /// <summary>The ways a negative number shows under the chosen category, each as its code.</summary>
    public IReadOnlyList<string> NegativeNumbers => Category switch
    {
        NumberFormatCategory.Number =>
            [.. Enumerable.Range(0, NumberFormatCodes.NumberNegativeStyles).Select(n => NumberFormatCodes.Number(DecimalPlaces, ThousandsSeparator, n))],
        NumberFormatCategory.Currency =>
            [.. Enumerable.Range(0, NumberFormatCodes.CurrencyNegativeStyles).Select(n => NumberFormatCodes.Currency(DecimalPlaces, n, _culture))],
        _ => [],
    };

    /// <summary>The formats the chosen category lists: Date's, Time's, or the codes Custom starts from.</summary>
    public IReadOnlyList<string> Types => Category switch
    {
        NumberFormatCategory.Date => NumberFormatCodes.DateTypes,
        NumberFormatCategory.Time => NumberFormatCodes.TimeTypes,
        NumberFormatCategory.Custom => _openingNumber.Category == NumberFormatCategory.Custom && !NumberFormatCodes.CustomTypes.Contains(_openingNumber.Type)
            ? [_openingNumber.Type, .. NumberFormatCodes.CustomTypes]
            : NumberFormatCodes.CustomTypes,
        _ => [],
    };

    /// <summary>The Sheet culture's currency symbol, which Currency writes.</summary>
    public string CurrencySymbol => NumberFormat.CurrencySymbolOf(_culture);

    /// <summary>Whether the chosen category takes decimal places.</summary>
    public bool TakesDecimalPlaces => Category is NumberFormatCategory.Number or NumberFormatCategory.Currency
        or NumberFormatCategory.Percentage or NumberFormatCategory.Scientific;

    /// <summary>The code the Number tab shows now, as it would be set.</summary>
    public string NumberFormatCode => Category switch
    {
        NumberFormatCategory.General => NumberFormat.General.Code,
        NumberFormatCategory.Number => NumberFormatCodes.Number(DecimalPlaces, ThousandsSeparator, NegativeNumber),
        NumberFormatCategory.Currency => NumberFormatCodes.Currency(DecimalPlaces, NegativeNumber, _culture),
        NumberFormatCategory.Percentage => NumberFormatCodes.Percentage(DecimalPlaces),
        NumberFormatCategory.Scientific => NumberFormatCodes.Scientific(DecimalPlaces),
        NumberFormatCategory.Text => "@",
        _ => TypeCode,
    };

    /// <summary>Why the Number tab's code cannot be set, or <see langword="null"/> when it can.</summary>
    public string? NumberFormatRefusal => Read(NumberFormatCode, out var reason) is null ? reason : null;

    /// <summary>Chooses a category, with its options as they opened when it is the one the Focus cell's code is, and the category's defaults otherwise.</summary>
    /// <exception cref="ArgumentException">The category is shown disabled, with the reason.</exception>
    public void SelectCategory(NumberFormatCategory category)
    {
        if (FormatCellsOffer.Categories.First(offer => offer.Category == category).DisabledReason is { } reason)
            throw new ArgumentException(reason, nameof(category));
        _numberTouched = true;
        if (category == _openingNumber.Category)
        {
            RestoreNumber(_openingNumber);
            return;
        }
        // Custom starts from the code shown, as Excel's does.
        var shown = NumberFormatCode;
        Category = category;
        DecimalPlaces = NumberFormatCodes.DefaultPlaces(category, _culture);
        ThousandsSeparator = false;
        NegativeNumber = 0;
        TypeCode = category switch
        {
            NumberFormatCategory.Date => NumberFormatCodes.DateTypes[0],
            NumberFormatCategory.Time => NumberFormatCodes.TimeTypes[0],
            NumberFormatCategory.Custom => shown,
            _ => "",
        };
    }

    /// <summary>Sets the decimal places, 0 to 30.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The places are outside 0 to 30.</exception>
    public void SetDecimalPlaces(int places)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(places);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(places, NumberFormatCodes.MostDecimalPlaces);
        _numberTouched = true;
        DecimalPlaces = places;
    }

    /// <summary>Sets whether Number groups thousands.</summary>
    public void SetThousandsSeparator(bool separator)
    {
        _numberTouched = true;
        ThousandsSeparator = separator;
    }

    /// <summary>Chooses one of <see cref="NegativeNumbers"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">It is not one of them.</exception>
    public void SetNegativeNumber(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, NegativeNumbers.Count);
        _numberTouched = true;
        NegativeNumber = index;
    }

    /// <summary>Chooses a Date or Time format, or types the Custom code.</summary>
    public void SetType(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        _numberTouched = true;
        TypeCode = code;
    }

    // ---- Alignment ----

    /// <summary>The horizontal alignment.</summary>
    public HorizontalAlignment Alignment { get; private set; }

    /// <summary>Sets the horizontal alignment.</summary>
    public void SetAlignment(HorizontalAlignment alignment)
    {
        if (!Enum.IsDefined(alignment)) throw new ArgumentOutOfRangeException(nameof(alignment), alignment, "Not an alignment.");
        _alignmentTouched = true;
        Alignment = alignment;
    }

    // ---- Font ----

    /// <summary>The Font style; <see langword="null"/>, shown empty, while bold or italic differs across the Selection.</summary>
    public FontStyle? FontStyle { get; private set; }

    /// <summary>Whether the text is underlined.</summary>
    public bool Underline { get; private set; }

    /// <summary>Whether the text is struck through.</summary>
    public bool Strikethrough { get; private set; }

    /// <summary>The Font's colour; Automatic is the Ink.</summary>
    public CellColour FontColour { get; private set; }

    /// <summary>Sets the Font style: bold and italic together.</summary>
    public void SetFontStyle(FontStyle style)
    {
        if (!Enum.IsDefined(style)) throw new ArgumentOutOfRangeException(nameof(style), style, "Not a font style.");
        _fontStyleTouched = true;
        FontStyle = style;
    }

    /// <summary>Sets the single underline on or off.</summary>
    public void SetUnderline(bool underline)
    {
        _underlineTouched = true;
        Underline = underline;
    }

    /// <summary>Sets strikethrough on or off.</summary>
    public void SetStrikethrough(bool strikethrough)
    {
        _strikethroughTouched = true;
        Strikethrough = strikethrough;
    }

    /// <summary>Sets the Font's colour: Automatic, or a colour of the palette or More Colours.</summary>
    public void SetFontColour(CellColour colour)
    {
        _fontColourTouched = true;
        FontColour = colour;
        ForgetColourText(ColourTarget.Font);
    }

    // ---- Fill ----

    /// <summary>The Fill; No Colour while the Fill differs across the Selection.</summary>
    public CellFill Fill { get; private set; }

    /// <summary>Sets the Fill: No Colour, or a colour of the palette or More Colours.</summary>
    public void SetFill(CellFill fill)
    {
        _fillTouched = true;
        Fill = fill;
        ForgetColourText(ColourTarget.Fill);
    }

    // ---- Border ----

    /// <summary>The line style an edge or a preset sets; Thin when Format Cells opens.</summary>
    public BorderLineStyle LineStyle { get; private set; } = BorderLineStyle.Thin;

    /// <summary>The line colour an edge or a preset sets; Automatic when Format Cells opens.</summary>
    public CellColour LineColour { get; private set; }

    /// <summary>Chooses the line style.</summary>
    public void SetLineStyle(BorderLineStyle style)
    {
        if (!Enum.IsDefined(style)) throw new ArgumentOutOfRangeException(nameof(style), style, "Not a line style.");
        LineStyle = style;
    }

    /// <summary>Chooses the line colour.</summary>
    public void SetLineColour(CellColour colour)
    {
        LineColour = colour;
        ForgetColourText(ColourTarget.Border);
    }

    /// <summary>The line on an edge; <see langword="null"/>, drawn as a grey dotted line, while it differs across the Selection.</summary>
    public BorderLine? EdgeLine(BorderEdge edge) => _edges[(int)edge];

    /// <summary>Whether an edge can be set: an edge inside the range only where a range has more than one row or column.</summary>
    public bool CanSet(BorderEdge edge) => edge switch
    {
        BorderEdge.InsideHorizontal => HasInsideHorizontal,
        BorderEdge.InsideVertical => HasInsideVertical,
        _ => true,
    };

    /// <summary>Whether a preset is offered: Inside only where a range has an edge inside it.</summary>
    public bool CanApply(BorderPreset preset) => preset != BorderPreset.Inside || HasInsideHorizontal || HasInsideVertical;

    /// <summary>
    /// An edge's button, as Excel's: an edge that shows the chosen line loses it, and any other takes
    /// it. A line style of None takes the line away.
    /// </summary>
    /// <exception cref="InvalidOperationException">The edge cannot be set for this Selection.</exception>
    public void ToggleEdge(BorderEdge edge)
    {
        if (!CanSet(edge)) throw new InvalidOperationException($"The {FormatCellsOffer.NameOf(edge)} edge lies inside a range, and no selected range has one.");
        var pen = Pen;
        SetEdge(edge, _edges[(int)edge] == pen ? BorderLine.None : pen);
    }

    /// <summary>A preset: None takes every line of the range away; Outline and Inside set the chosen line there.</summary>
    /// <exception cref="InvalidOperationException">Inside, and no selected range has an edge inside it.</exception>
    public void ApplyPreset(BorderPreset preset)
    {
        if (!CanApply(preset)) throw new InvalidOperationException("Inside sets the edges inside a range, and no selected range has one.");
        var line = preset == BorderPreset.None ? BorderLine.None : Pen;
        foreach (var edge in FormatCellsOffer.Edges)
        {
            var outer = edge is not (BorderEdge.InsideHorizontal or BorderEdge.InsideVertical);
            var sets = preset switch
            {
                BorderPreset.Outline => outer,
                BorderPreset.Inside => !outer,
                _ => true,
            };
            if (sets && CanSet(edge)) SetEdge(edge, line);
        }
    }

    private BorderLine Pen => new(LineStyle, LineColour);

    private void SetEdge(BorderEdge edge, BorderLine line)
    {
        _edges[(int)edge] = line;
        _edgesTouched[(int)edge] = true;
    }

    // ---- More Colours ----

    /// <summary>The text typed under More Colours for a colour, or empty.</summary>
    public string ColourText(ColourTarget target) => _colourText.GetValueOrDefault(target, "");

    /// <summary>
    /// Reads text typed under More Colours: <c>#</c> and six hex digits, such as <c>#1F4E79</c>, sets
    /// the colour for <paramref name="target"/>. Any other text stands as a <see cref="Refusal"/>
    /// until it is corrected, cleared, or a colour is chosen from the palette.
    /// </summary>
    public void SetColourText(ColourTarget target, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (TryReadHex(text, out var colour))
        {
            switch (target)
            {
                case ColourTarget.Font: SetFontColour(colour); break;
                case ColourTarget.Fill: SetFill(CellFill.Solid(colour)); break;
                default: SetLineColour(colour); break;
            }
            _colourText[target] = text;
            return;
        }
        _colourText[target] = text;
        if (text.Trim().Length == 0) _colourRefusals.Remove(target);
        else _colourRefusals[target] = $"'{text.Trim()}' is not a colour. Type # and six hex digits, such as #1F4E79.";
    }

    /// <summary>A colour written <c>#RRGGBB</c>, or <c>RRGGBB</c>, in either case.</summary>
    public static bool TryReadHex(string text, out CellColour colour)
    {
        ArgumentNullException.ThrowIfNull(text);
        var digits = text.Trim().TrimStart('#');
        if (digits.Length == 6 && digits.All(char.IsAsciiHexDigit)
            && int.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgb))
        {
            colour = CellColour.FromRgb(rgb);
            return true;
        }
        colour = default;
        return false;
    }

    private void ForgetColourText(ColourTarget target)
    {
        _colourText.Remove(target);
        _colourRefusals.Remove(target);
    }

    // ---- OK ----

    /// <summary>
    /// Why OK cannot set what Format Cells shows, by name, or <see langword="null"/> when it can: a
    /// Custom code ExSheet does not read, or More Colours text that is not a colour.
    /// </summary>
    public string? Refusal =>
        (_numberTouched ? NumberFormatRefusal : null)
        ?? _colourRefusals.Values.FirstOrDefault();

    /// <summary>Whether the user has touched anything that OK would set.</summary>
    public bool IsTouched => !Change.IsEmpty;

    /// <summary>The parts the user touched, as one change; empty when nothing was.</summary>
    public CellFormatChange Change
    {
        get
        {
            var (bold, italic) = _fontStyleTouched && FontStyle is { } style ? EmphasisOf(style) : ((bool?)null, (bool?)null);
            var borders = new BorderChange
            {
                Top = TouchedEdge(BorderEdge.Top),
                Bottom = TouchedEdge(BorderEdge.Bottom),
                Left = TouchedEdge(BorderEdge.Left),
                Right = TouchedEdge(BorderEdge.Right),
                InsideHorizontal = TouchedEdge(BorderEdge.InsideHorizontal),
                InsideVertical = TouchedEdge(BorderEdge.InsideVertical),
            };
            return new CellFormatChange
            {
                NumberFormat = _numberTouched ? Read(NumberFormatCode, out _) : null,
                Alignment = _alignmentTouched ? Alignment : null,
                Bold = bold,
                Italic = italic,
                Underline = _underlineTouched ? Underline : null,
                Strikethrough = _strikethroughTouched ? Strikethrough : null,
                FontColour = _fontColourTouched ? FontColour : null,
                Fill = _fillTouched ? Fill : null,
                Borders = _edgesTouched.Any(touched => touched) ? borders : null,
            };
        }
    }

    private BorderLine? TouchedEdge(BorderEdge edge) => _edgesTouched[(int)edge] ? _edges[(int)edge] : null;

    /// <summary>A code as the engine reads it, or null with the reason, refused by name.</summary>
    private static NumberFormat? Read(string code, out string? reason)
    {
        if (code.Trim().Length == 0)
        {
            reason = "Type a number format code under Custom.";
            return null;
        }
        if (NumberFormat.TryParse(code, out var format, out var why))
        {
            reason = null;
            return format;
        }
        reason = $"The number format '{code}' is not one ExSheet reads: {why}";
        return null;
    }

    private void RestoreNumber(NumberFormatCodes.Recognised number)
    {
        Category = number.Category;
        DecimalPlaces = number.Places;
        ThousandsSeparator = number.Separator;
        NegativeNumber = number.Negative;
        TypeCode = number.Category is NumberFormatCategory.Date or NumberFormatCategory.Time or NumberFormatCategory.Custom ? number.Type : "";
    }

    private static FontStyle StyleOf(CellFont font) => (font.Bold, font.Italic) switch
    {
        (true, true) => ExSheet.FontStyle.BoldItalic,
        (true, false) => ExSheet.FontStyle.Bold,
        (false, true) => ExSheet.FontStyle.Italic,
        _ => ExSheet.FontStyle.Regular,
    };

    private static (bool? Bold, bool? Italic) EmphasisOf(FontStyle style) => style switch
    {
        ExSheet.FontStyle.BoldItalic => (true, true),
        ExSheet.FontStyle.Bold => (true, false),
        ExSheet.FontStyle.Italic => (false, true),
        _ => (false, false),
    };

    /// <summary>
    /// Format Cells' draft over <paramref name="ranges"/>, opening on the Focus cell at
    /// <paramref name="focus"/> (ADR-0071): what differs across the ranges is read from what the
    /// Sheet records (<see cref="Sheet.GetCellFormats"/>), so a Selection of whole columns opens as
    /// quickly as one cell.
    /// </summary>
    internal static FormatCellsDraft Open(Sheet sheet, CellAddress focus, IReadOnlyList<CellRange> ranges)
    {
        var opening = sheet.GetCellFormat(focus);
        return new FormatCellsDraft(opening, FormatCellsSpread.Of(sheet, ranges), sheet.Culture);
    }
}

/// <summary>
/// What differs across a Selection, as Format Cells shows it (ADR-0071): whether bold or italic
/// differs, whether the Fill does, and each edge's one line, or <see langword="null"/> where it
/// differs. An outer edge is read as it is drawn, so a neighbour's line shows where the cell records
/// none (the fourteenth Windows run, case 13); an edge inside a range is read from its cells' own
/// sides, so a thick bottom over a plain cell differs (the eleventh run, case 24).
/// </summary>
internal sealed record FormatCellsSpread(bool FontStyleDiffers, bool FillDiffers, BorderLine?[] Edges, bool InsideHorizontal, bool InsideVertical)
{
    public static FormatCellsSpread Of(Sheet sheet, IReadOnlyList<CellRange> ranges)
    {
        var shown = new HashSet<CellFormat>();
        foreach (var range in ranges) shown.UnionWith(sheet.GetCellFormats(range));
        var sides = new HashSet<BorderLine>[6];
        for (var i = 0; i < sides.Length; i++) sides[i] = [];
        foreach (var range in ranges)
        {
            var (first, last) = (range.First, range.Last);
            // An outer edge shows as it is drawn: a neighbour's line where the cell records none
            // (the fourteenth Windows run, case 13).
            var outer = sheet.GetEdgeLines(range);
            sides[(int)BorderEdge.Top].UnionWith(outer.Top);
            sides[(int)BorderEdge.Bottom].UnionWith(outer.Bottom);
            sides[(int)BorderEdge.Left].UnionWith(outer.Left);
            sides[(int)BorderEdge.Right].UnionWith(outer.Right);
            // An edge inside the range compares the cells' own sides (the eleventh run, case 24):
            // the lower side of every row but the last and the upper side of every row but the
            // first; and the same across columns.
            if (range.RowCount > 1)
            {
                Add(BorderEdge.InsideHorizontal, new CellRange(first, new CellAddress(last.Row - 1, last.Column)), b => b.Bottom);
                Add(BorderEdge.InsideHorizontal, new CellRange(new CellAddress(first.Row + 1, first.Column), last), b => b.Top);
            }
            if (range.ColumnCount > 1)
            {
                Add(BorderEdge.InsideVertical, new CellRange(first, new CellAddress(last.Row, last.Column - 1)), b => b.Right);
                Add(BorderEdge.InsideVertical, new CellRange(new CellAddress(first.Row, first.Column + 1), last), b => b.Left);
            }
        }
        return new FormatCellsSpread(
            shown.Select(f => (f.Font.Bold, f.Font.Italic)).Distinct().Count() > 1,
            shown.Select(f => f.Fill).Distinct().Count() > 1,
            [.. FormatCellsOffer.Edges.Select(edge => sides[(int)edge].Count == 1 ? sides[(int)edge].Single() : (sides[(int)edge].Count == 0 ? BorderLine.None : (BorderLine?)null))],
            ranges.Any(range => range.RowCount > 1),
            ranges.Any(range => range.ColumnCount > 1));

        void Add(BorderEdge edge, CellRange part, Func<CellBorders, BorderLine> side)
        {
            foreach (var format in sheet.GetCellFormats(part)) sides[(int)edge].Add(side(format.Borders));
        }
    }
}
