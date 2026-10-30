namespace ExSheet;

/// <summary>
/// Format Cells as the Script Core describes it to a Chrome (ADR-0071, ADR-0410): every tab's controls, what each shows
/// now, and the refusal OK met, in the Sheet's words. The draft is the Sheet's, in the browser: a Chrome draws its
/// controls from this view and marks them with <see cref="SheetScriptSlotAttributes"/>, and the script reads each as the
/// browser fires it, sets it through the draft, and sends the view again. Each choice's <c>Value</c> is written back as
/// it is, on the control that makes it.
/// </summary>
/// <remarks>
/// The script keeps what a render a round trip behind would show wrongly: the tab shown, what each radio, checkbox and
/// select holds, and the texts of the fields. So the contents draw every tab, never mark which is shown, and draw a text
/// field with no value, a radio or a checkbox with no <c>checked</c>, a select with no option selected and an edge's
/// button with no <c>aria-pressed</c>; the script sets each from the draft. What the view says of them (a choice's
/// <c>Selected</c>, an edge's <c>Pressed</c>) is for what a Chrome shows beside its controls.
/// </remarks>
/// <param name="Title">Format Cells' title, which names its frame.</param>
/// <param name="Labels">The words its controls are labelled with.</param>
/// <param name="Tabs">The tabs, in Excel's order: each <c>Value</c> is a tab's name as the script spells it.</param>
/// <param name="Refusal">Why OK set nothing, while that still stands; null otherwise.</param>
/// <param name="Number">The Number tab.</param>
/// <param name="Alignments">The Alignment tab's horizontal alignments, one selected.</param>
/// <param name="Font">The Font tab.</param>
/// <param name="Border">The Border tab.</param>
/// <param name="Fill">The Fill tab's palette.</param>
public sealed record SheetFormatCellsView(
    string Title,
    SheetFormatCellsLabels Labels,
    IReadOnlyList<SheetFormatCellsChoice> Tabs,
    string? Refusal,
    SheetFormatCellsNumber Number,
    IReadOnlyList<SheetFormatCellsChoice> Alignments,
    SheetFormatCellsFont Font,
    SheetFormatCellsBorder Border,
    SheetFormatCellsPalette Fill);

/// <summary>One choice Format Cells offers (ADR-0071).</summary>
/// <param name="Value">What the control that makes it carries, as <see cref="SheetScriptSlotAttributes.FormatValue"/>.</param>
/// <param name="Label">Its words.</param>
/// <param name="Selected">Whether the draft holds it.</param>
/// <param name="Enabled">Whether it can be made.</param>
/// <param name="Note">Why it cannot, shown beside it; null where nothing is said.</param>
public sealed record SheetFormatCellsChoice(string Value, string Label, bool Selected, bool Enabled, string? Note);

/// <summary>The words Format Cells' controls are labelled with, in the Sheet's language and the Consumer's labels.</summary>
/// <param name="Tabs">The tab list's name.</param>
/// <param name="Category">The Number tab's category list.</param>
/// <param name="DecimalPlaces">The number of decimal places.</param>
/// <param name="ThousandsSeparator">The thousands separator.</param>
/// <param name="NegativeNumbers">How a negative number shows.</param>
/// <param name="Type">The format code typed for Custom.</param>
/// <param name="Horizontal">The horizontal alignment.</param>
/// <param name="FontStyle">The font style.</param>
/// <param name="Underline">The underline.</param>
/// <param name="UnderlineNone">No underline.</param>
/// <param name="UnderlineSingle">A single underline.</param>
/// <param name="Strikethrough">The strikethrough.</param>
/// <param name="NormalFont">Excel's Normal font.</param>
/// <param name="Colour">A colour.</param>
/// <param name="LineStyle">The Border's line style.</param>
/// <param name="Presets">The Border's presets.</param>
/// <param name="BorderEdges">The Border's edges.</param>
/// <param name="Preview">The Border's preview.</param>
/// <param name="MoreColours">More Colours, which takes a hex value.</param>
/// <param name="Ok">OK.</param>
/// <param name="Cancel">Cancel.</param>
public sealed record SheetFormatCellsLabels(
    string Tabs,
    string Category,
    string DecimalPlaces,
    string ThousandsSeparator,
    string NegativeNumbers,
    string Type,
    string Horizontal,
    string FontStyle,
    string Underline,
    string UnderlineNone,
    string UnderlineSingle,
    string Strikethrough,
    string NormalFont,
    string Colour,
    string LineStyle,
    string Presets,
    string BorderEdges,
    string Preview,
    string MoreColours,
    string Ok,
    string Cancel);

/// <summary>The Number tab (ADR-0071): its categories, and the options the category chosen takes.</summary>
/// <param name="Categories">The categories, in Excel's order, the three ExSheet does not read disabled with their
/// reason.</param>
/// <param name="Hint">What General and Text say of themselves; null for any other category.</param>
/// <param name="TakesDecimalPlaces">Whether the category takes a number of decimal places, typed in a field.</param>
/// <param name="ThousandsSeparator">For Number, whether the draft holds a thousands separator; null otherwise.</param>
/// <param name="CurrencySymbol">For Currency, the symbol it shows, in words; null otherwise.</param>
/// <param name="NegativeNumbers">How a negative number shows, each a format code, one selected; none where the category
/// offers no choice.</param>
/// <param name="TakesCode">Whether a format code is typed, as Custom takes it.</param>
/// <param name="CodeRefusal">Why the code typed cannot be read; null while it can.</param>
/// <param name="TypesLegend">The words over <paramref name="Types"/>; null where there are none.</param>
/// <param name="Types">The category's formats, or Custom's codes, each a format code, the draft's selected.</param>
public sealed record SheetFormatCellsNumber(
    IReadOnlyList<SheetFormatCellsChoice> Categories,
    string? Hint,
    bool TakesDecimalPlaces,
    bool? ThousandsSeparator,
    string? CurrencySymbol,
    IReadOnlyList<SheetFormatCellsChoice> NegativeNumbers,
    bool TakesCode,
    string? CodeRefusal,
    string? TypesLegend,
    IReadOnlyList<SheetFormatCellsChoice> Types);

/// <summary>The Font tab (ADR-0071).</summary>
/// <param name="Styles">The styles, as Excel lists them, one selected where the Selection agrees.</param>
/// <param name="Underline">Whether the draft holds an underline.</param>
/// <param name="Strikethrough">Whether the draft holds a strikethrough.</param>
/// <param name="NormalFont">Whether every part of the Font is its default, as Excel's Normal font shows it.</param>
/// <param name="Palette">The Font's colour.</param>
public sealed record SheetFormatCellsFont(
    IReadOnlyList<SheetFormatCellsChoice> Styles,
    bool Underline,
    bool Strikethrough,
    bool NormalFont,
    SheetFormatCellsPalette Palette);

/// <summary>The Border tab (ADR-0071).</summary>
/// <param name="LineStyles">The line styles in Excel's two columns, each from the top.</param>
/// <param name="Presets">The presets, each a button, enabled where the Selection takes it.</param>
/// <param name="Edges">The edges, in Excel's order.</param>
/// <param name="Palette">The line's colour.</param>
public sealed record SheetFormatCellsBorder(
    IReadOnlyList<IReadOnlyList<SheetFormatCellsLineStyle>> LineStyles,
    IReadOnlyList<SheetFormatCellsChoice> Presets,
    IReadOnlyList<SheetFormatCellsEdge> Edges,
    SheetFormatCellsPalette Palette);

/// <summary>A line style of the Border tab, with the stroke its sample is drawn with.</summary>
/// <param name="Value">What its control carries.</param>
/// <param name="Label">Its name.</param>
/// <param name="Selected">Whether the draft holds it.</param>
/// <param name="Width">The stroke's width, as SVG reads it.</param>
/// <param name="Dashes">The stroke's dashes, as SVG reads them; null for a solid line.</param>
public sealed record SheetFormatCellsLineStyle(string Value, string Label, bool Selected, string Width, string? Dashes);

/// <summary>An edge of the Border tab: its button, and its line in the preview.</summary>
/// <param name="Value">What its button carries.</param>
/// <param name="Label">Its name.</param>
/// <param name="Pressed">Its button's <c>aria-pressed</c>: <c>true</c>, <c>false</c>, or <c>mixed</c> where the
/// Selection's cells' sides differ.</param>
/// <param name="Enabled">Whether the Selection has the edge: an inside edge needs two rows or two columns.</param>
/// <param name="InPreview">Whether the preview draws it.</param>
/// <param name="Line">Its line; null where the cells' sides differ, which the preview draws grey and dotted.</param>
public sealed record SheetFormatCellsEdge(string Value, string Label, string Pressed, bool Enabled, bool InPreview, SheetFormatCellsLine? Line);

/// <summary>An edge's line as the preview draws it.</summary>
/// <param name="None">Whether there is no line.</param>
/// <param name="Paint">Its colour, as CSS paints it; <c>currentColor</c> for Automatic.</param>
/// <param name="Width">The stroke's width, as SVG reads it.</param>
/// <param name="Dashes">The stroke's dashes; null for a solid line.</param>
public sealed record SheetFormatCellsLine(bool None, string Paint, string Width, string? Dashes);

/// <summary>A palette of Format Cells (ADR-0071; the eleventh Windows run, case 23): Automatic or No Colour, the theme's
/// sixty colours and the ten standard ones, and More Colours, typed as a hex value.</summary>
/// <param name="Target">Whose colour it sets: <c>font</c>, <c>fill</c> or <c>border</c>.</param>
/// <param name="Legend">The words over it.</param>
/// <param name="First">The first choice's words: Automatic, or No Colour for the Fill.</param>
/// <param name="FirstSelected">Whether the draft holds the first choice.</param>
/// <param name="Rows">The swatches: the theme's six rows of ten, then the standard ten.</param>
public sealed record SheetFormatCellsPalette(
    string Target,
    string Legend,
    string First,
    bool FirstSelected,
    IReadOnlyList<IReadOnlyList<SheetFormatCellsSwatch>> Rows)
{
    /// <summary>What the first choice's control carries, as <see cref="SheetScriptSlotAttributes.FormatValue"/>.</summary>
    public string FirstValue => $"{Target}:none";

    /// <summary>What a swatch's control carries, as <see cref="SheetScriptSlotAttributes.FormatValue"/>.</summary>
    public string ValueOf(SheetFormatCellsSwatch swatch)
    {
        ArgumentNullException.ThrowIfNull(swatch);
        return $"{Target}:{swatch.Index}";
    }
}

/// <summary>A swatch of a palette.</summary>
/// <param name="Index">Its place in the palette, which its control carries.</param>
/// <param name="Name">Excel's name for it.</param>
/// <param name="Paint">Its colour, as CSS paints it: the user's choice of data, written inline, never a Visual Token
/// (ADR-0071).</param>
/// <param name="Selected">Whether the draft holds it.</param>
public sealed record SheetFormatCellsSwatch(int Index, string Name, string Paint, bool Selected);
