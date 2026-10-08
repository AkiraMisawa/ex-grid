using System.Text;

namespace ExGrid.Columns;

/// <summary>
/// The numbers the overflow estimate needs, supplied by the theme/layout layer as C#
/// values — geometry truth lives in C# (ADR-0013), and text measurement deliberately
/// makes no round-trip through JavaScript (ADR-0021).
///
/// <para>The estimate charges <b>per character class</b> (ADR-0016): wide, digit, narrow,
/// full-width and other, because one number cannot serve a twelve-digit amount column and a
/// percent column at once. Each width is contractually <b>at least as wide as any glyph
/// of its class the column's formats emit</b>, at every weight the grid itself paints, on
/// every platform the grid supports: overshooting errs toward an early <c>####</c>, the safe
/// direction. <c>system-ui</c> is a different family on each operating system, so one
/// machine's measurement is not enough — DejaVu Sans on Linux paints a bold digit at 9.75px
/// and <c>−</c>, <c>+</c> and <c>#</c> at 11.734px.</para>
///
/// <para><b>The other class</b> (ADR-0016; ticket 83) is every glyph outside the four measured
/// ones: letters, the currency signs wider than a digit, and anything not foreseen. A letter or
/// currency sign the face draws itself is charged its own measured width from a
/// <see cref="GlyphWidthTable"/>; everything else is charged the widest glyph measured, so a glyph
/// nobody listed errs toward <c>####</c>, never toward a cut number. Only a value that can become
/// <c>####</c> is charged either: a Text value or a label is estimated with them at the digit, as
/// before (<see cref="For"/>), because what never hashes is cut visibly.</para>
///
/// <para><b>Bold widths</b> (ADR-0050, item 15; ADR-0071) are the wide, digit, narrow and other
/// classes measured at the bold weight a Consumer's per-cell Font paints, and a bold cell is
/// judged by them: its <c>####</c> decision, and the width its painted text is fitted to. The
/// full-width class is an em in every weight. Metrics built without them derive them from the regular widths (see
/// <see cref="BoldWidthAllowance"/>); the core's own defaults and a Wrapper's supply measured
/// ones.</para>
/// </summary>
public readonly record struct CellTextMetrics
{
    /// <summary>The uniform form: every character charged at one tabular-digit width,
    /// full-width characters and the other class included. What a theme that measured only
    /// its digit supplies; the safe direction holds as long as the digit covers the widest
    /// glyph its formats emit.</summary>
    public CellTextMetrics(double digitWidthPx, double cellHorizontalPaddingPx)
        : this(digitWidthPx, digitWidthPx, digitWidthPx, digitWidthPx, cellHorizontalPaddingPx,
            digitWidthPx * BoldWidthAllowance, digitWidthPx * BoldWidthAllowance, digitWidthPx * BoldWidthAllowance,
            digitWidthPx, digitWidthPx * BoldWidthAllowance)
    {
    }

    /// <summary>The three-class form: the wide, digit and narrow widths, and the padding
    /// on one side. Full-width characters are charged at twice the digit: a tabular digit
    /// is at least half an em in any text face, so twice it covers the em a full-width
    /// glyph occupies. The four-class form states the em exactly. The other class is
    /// charged its allowance over the digit (<see cref="OtherWidthAllowance"/>).</summary>
    public CellTextMetrics(
        double wideWidthPx, double digitWidthPx, double narrowWidthPx, double cellHorizontalPaddingPx)
        : this(wideWidthPx, digitWidthPx, narrowWidthPx, 2 * digitWidthPx, cellHorizontalPaddingPx)
    {
    }

    /// <summary>The four-class form (ADR-0016): the wide, digit, narrow and full-width
    /// widths, and the padding on one side. The full-width class is an em — the font
    /// size — because CJK faces are drawn on the em square. Refuses a narrow width wider
    /// than the digit, or a wide or full-width width narrower than it. The other class is
    /// charged its allowance over the digit (<see cref="OtherWidthAllowance"/>).</summary>
    public CellTextMetrics(
        double wideWidthPx, double digitWidthPx, double narrowWidthPx, double fullWidthPx,
        double cellHorizontalPaddingPx)
        : this(wideWidthPx, digitWidthPx, narrowWidthPx, fullWidthPx, cellHorizontalPaddingPx,
            wideWidthPx * BoldWidthAllowance, digitWidthPx * BoldWidthAllowance, narrowWidthPx * BoldWidthAllowance)
    {
    }

    /// <summary>The four-class form with the bold widths (ADR-0050, item 15): each class measured
    /// again at the bold weight. The full-width class is an em at either weight. The bold widths
    /// are refused by the rules the regular ones are. The other class is charged its allowance
    /// over the digit at each weight (<see cref="OtherWidthAllowance"/>).</summary>
    public CellTextMetrics(
        double wideWidthPx, double digitWidthPx, double narrowWidthPx, double fullWidthPx,
        double cellHorizontalPaddingPx,
        double boldWideWidthPx, double boldDigitWidthPx, double boldNarrowWidthPx)
        : this(wideWidthPx, digitWidthPx, narrowWidthPx, fullWidthPx, cellHorizontalPaddingPx,
            boldWideWidthPx, boldDigitWidthPx, boldNarrowWidthPx,
            digitWidthPx * OtherWidthAllowance, boldDigitWidthPx * OtherWidthAllowance)
    {
    }

    /// <summary>Every class, measured (ADR-0016; ticket 83): the four-class form with the bold
    /// widths, and the other class at the regular and the bold weight — the widest glyph outside
    /// the four classes the column's formats emit, at every weight painted. The other widths are
    /// refused narrower than their digit. A table of single glyphs is added with
    /// <see cref="WithGlyphWidths"/>.</summary>
    public CellTextMetrics(
        double wideWidthPx, double digitWidthPx, double narrowWidthPx, double fullWidthPx,
        double cellHorizontalPaddingPx,
        double boldWideWidthPx, double boldDigitWidthPx, double boldNarrowWidthPx,
        double otherWidthPx, double boldOtherWidthPx)
        : this(wideWidthPx, digitWidthPx, narrowWidthPx, fullWidthPx, cellHorizontalPaddingPx,
            boldWideWidthPx, boldDigitWidthPx, boldNarrowWidthPx, otherWidthPx, boldOtherWidthPx,
            glyphs: null, glyphScale: 1, glyphBold: false)
    {
    }

    private CellTextMetrics(
        double wideWidthPx, double digitWidthPx, double narrowWidthPx, double fullWidthPx,
        double cellHorizontalPaddingPx,
        double boldWideWidthPx, double boldDigitWidthPx, double boldNarrowWidthPx,
        double otherWidthPx, double boldOtherWidthPx,
        GlyphWidthTable? glyphs, double glyphScale, bool glyphBold)
    {
        if (!double.IsFinite(digitWidthPx) || digitWidthPx <= 0)
            throw new ArgumentOutOfRangeException(nameof(digitWidthPx), digitWidthPx,
                "A digit width is a finite, positive number of pixels.");
        if (!double.IsFinite(narrowWidthPx) || narrowWidthPx <= 0 || narrowWidthPx > digitWidthPx)
            throw new ArgumentOutOfRangeException(nameof(narrowWidthPx), narrowWidthPx,
                "The narrow width is a finite, positive number of pixels, no wider than the digit.");
        if (!double.IsFinite(wideWidthPx) || wideWidthPx < digitWidthPx)
            throw new ArgumentOutOfRangeException(nameof(wideWidthPx), wideWidthPx,
                "The wide width is finite and at least the digit's.");
        if (!double.IsFinite(fullWidthPx) || fullWidthPx < digitWidthPx)
            throw new ArgumentOutOfRangeException(nameof(fullWidthPx), fullWidthPx,
                "The full-width width is finite and at least the digit's.");
        if (!double.IsFinite(cellHorizontalPaddingPx) || cellHorizontalPaddingPx < 0)
            throw new ArgumentOutOfRangeException(nameof(cellHorizontalPaddingPx), cellHorizontalPaddingPx,
                "Cell padding is a finite, non-negative number of pixels.");

        if (!double.IsFinite(boldDigitWidthPx) || boldDigitWidthPx <= 0)
            throw new ArgumentOutOfRangeException(nameof(boldDigitWidthPx), boldDigitWidthPx,
                "A bold digit width is a finite, positive number of pixels.");
        if (!double.IsFinite(boldNarrowWidthPx) || boldNarrowWidthPx <= 0 || boldNarrowWidthPx > boldDigitWidthPx)
            throw new ArgumentOutOfRangeException(nameof(boldNarrowWidthPx), boldNarrowWidthPx,
                "The bold narrow width is a finite, positive number of pixels, no wider than the bold digit.");
        if (!double.IsFinite(boldWideWidthPx) || boldWideWidthPx < boldDigitWidthPx)
            throw new ArgumentOutOfRangeException(nameof(boldWideWidthPx), boldWideWidthPx,
                "The bold wide width is finite and at least the bold digit's.");
        if (!double.IsFinite(otherWidthPx) || otherWidthPx < digitWidthPx)
            throw new ArgumentOutOfRangeException(nameof(otherWidthPx), otherWidthPx,
                "The other width is finite and at least the digit's.");
        if (!double.IsFinite(boldOtherWidthPx) || boldOtherWidthPx < boldDigitWidthPx)
            throw new ArgumentOutOfRangeException(nameof(boldOtherWidthPx), boldOtherWidthPx,
                "The bold other width is finite and at least the bold digit's.");

        WideWidthPx = wideWidthPx;
        DigitWidthPx = digitWidthPx;
        NarrowWidthPx = narrowWidthPx;
        FullWidthPx = fullWidthPx;
        CellHorizontalPaddingPx = cellHorizontalPaddingPx;
        BoldWideWidthPx = boldWideWidthPx;
        BoldDigitWidthPx = boldDigitWidthPx;
        BoldNarrowWidthPx = boldNarrowWidthPx;
        OtherWidthPx = otherWidthPx;
        BoldOtherWidthPx = boldOtherWidthPx;
        GlyphWidths = glyphs;
        _glyphScale = glyphScale;
        _glyphBold = glyphBold;
    }

    // What the table's widths are multiplied by (the resolved font size over the size it was
    // measured at), and whether its bold widths are read.
    private readonly double _glyphScale;
    private readonly bool _glyphBold;

    /// <summary>
    /// What metrics built without bold widths charge a bold character, over its regular class
    /// width: 4% more. The regular widths are already the 600 weight the grid's own group and total
    /// rows paint (ADR-0016), and the widest growth measured from there to bold was 3.9% — <c>(</c>
    /// in <c>system-ui</c> on macOS, 5.633px to 5.852px — so the allowance errs toward an early
    /// <c>####</c>, the safe direction. Reasoned from the faces measured, as the full-width
    /// fallback is; a theme that knows its bold widths supplies them.
    /// </summary>
    public const double BoldWidthAllowance = 1.04;

    /// <summary>
    /// What metrics built without the other class charge one of its glyphs, over their digit:
    /// twice it, as the three-class form charges a full-width one. The widest such glyph measured
    /// is 1.59 digits — <c>W</c> and <c>₩</c> in DejaVu Sans Bold, 15.453px against 9.75 (ticket
    /// 83) — so the allowance errs toward an early <c>####</c>, the safe direction. Reasoned from
    /// the faces measured; a theme that knows its other widths supplies them.
    /// </summary>
    public const double OtherWidthAllowance = 2;

    /// <summary>What <c>%</c> costs — and <c>€</c>, <c>−</c>, <c>+</c> and <c>#</c>, each
    /// measured past the digit on some platform: charging them here overshoots where
    /// they are narrower, which is the safe direction. One <c>#</c> of an overflowing
    /// cell is charged at it too, so the run fits the cell it stands in.</summary>
    public double WideWidthPx { get; }

    /// <summary>What a tabular digit costs — and the glyphs measured at a digit's width or
    /// under in every face: the currency signs <c>$ £ ¥ ₹ ₺ ₫</c>, the exponent's <c>E</c>, the
    /// hyphen-minus, the apostrophes and spaces that group digits, and the direction marks
    /// (ticket 83).</summary>
    public double DigitWidthPx { get; }

    /// <summary>What the separators cost — <c>. , ( ) / :</c> and the space.</summary>
    public double NarrowWidthPx { get; }

    /// <summary>What every glyph outside the four measured classes costs (ADR-0016; ticket 83):
    /// letters, the currency signs wider than a digit (<c>₩</c>, <c>₪</c>, <c>₦</c> …) and any
    /// glyph not foreseen. The widest of them, so a glyph nobody listed is charged over its
    /// width.</summary>
    public double OtherWidthPx { get; }

    /// <summary>What a full-width character costs — East Asian Width Wide or Fullwidth:
    /// CJK ideographs, kana, Hangul, fullwidth forms. An em, whatever the family.</summary>
    public double FullWidthPx { get; }

    /// <summary>Padding on one side; a cell pays it twice.</summary>
    public double CellHorizontalPaddingPx { get; }

    /// <summary>The wide class at the bold weight (ADR-0050, item 15).</summary>
    public double BoldWideWidthPx { get; }

    /// <summary>A tabular digit, and the glyphs the digit class holds, at the bold
    /// weight.</summary>
    public double BoldDigitWidthPx { get; }

    /// <summary>The separators at the bold weight.</summary>
    public double BoldNarrowWidthPx { get; }

    /// <summary>The other class at the bold weight (ticket 83).</summary>
    public double BoldOtherWidthPx { get; }

    /// <summary>
    /// Single glyphs charged their own measured width instead of their class's (ticket 83):
    /// letters and currency signs the face draws itself, so a month name or a currency symbol is
    /// charged what it paints. Null charges every glyph by its class. The classes come first: a
    /// glyph a class names is charged the class, whatever the table holds.
    /// </summary>
    public GlyphWidthTable? GlyphWidths { get; }

    /// <summary>
    /// The metrics a bold cell is judged by (ADR-0050, item 15): the bold widths in the regular
    /// widths' place, with the same full-width class — no narrower than the bold digit, which the
    /// uniform form's em would be — and the same padding. Its own bold widths are the same ones, so
    /// it is bold however often it is asked. Hand it to <see cref="OverflowRules.Decide"/> for a bold
    /// cell's <c>####</c> decision.
    /// </summary>
    public CellTextMetrics Bold => new(
        BoldWideWidthPx, BoldDigitWidthPx, BoldNarrowWidthPx, Math.Max(FullWidthPx, BoldDigitWidthPx),
        CellHorizontalPaddingPx, BoldWideWidthPx, BoldDigitWidthPx, BoldNarrowWidthPx,
        BoldOtherWidthPx, BoldOtherWidthPx, GlyphWidths, _glyphScale, glyphBold: true);

    // The generated ToString prints every public property, Bold included, and Bold is metrics of
    // this type whose own Bold is asked in turn: it recursed until the stack overflowed, which a
    // refusal naming these metrics in its message (ExGrid's CellMetrics check) would hit as soon
    // as the message is read — on the Server host, the process. The widths are printed instead.
    private bool PrintMembers(System.Text.StringBuilder builder)
    {
        var c = System.Globalization.CultureInfo.InvariantCulture;
        builder.Append(c, $"WideWidthPx = {WideWidthPx}, DigitWidthPx = {DigitWidthPx}, NarrowWidthPx = {NarrowWidthPx}, ");
        builder.Append(c, $"OtherWidthPx = {OtherWidthPx}, FullWidthPx = {FullWidthPx}, CellHorizontalPaddingPx = {CellHorizontalPaddingPx}, ");
        builder.Append(c, $"BoldWideWidthPx = {BoldWideWidthPx}, BoldDigitWidthPx = {BoldDigitWidthPx}, BoldNarrowWidthPx = {BoldNarrowWidthPx}, ");
        builder.Append(c, $"BoldOtherWidthPx = {BoldOtherWidthPx}, GlyphWidths = {(GlyphWidths is null ? "none" : "a table")}");
        return true;
    }

    /// <summary>These metrics with measured bold widths in place of whatever they carried —
    /// derived, or another theme's (ADR-0050, item 15). The bold other class goes with them: it is
    /// charged its allowance over the new bold digit (<see cref="OtherWidthAllowance"/>).</summary>
    public CellTextMetrics WithBoldWidths(double boldWideWidthPx, double boldDigitWidthPx, double boldNarrowWidthPx)
        => new(WideWidthPx, DigitWidthPx, NarrowWidthPx, FullWidthPx, CellHorizontalPaddingPx,
            boldWideWidthPx, boldDigitWidthPx, boldNarrowWidthPx,
            OtherWidthPx, boldDigitWidthPx * OtherWidthAllowance, GlyphWidths, _glyphScale, _glyphBold);

    /// <summary>
    /// These metrics charging the glyphs <paramref name="table"/> holds their own widths (ticket
    /// 83), scaled from the size the table was measured at to <paramref name="fontSizePx"/>, the
    /// size these metrics are true at. Null takes a table away. The bold widths are read when the
    /// metrics are <see cref="Bold"/>.
    /// </summary>
    public CellTextMetrics WithGlyphWidths(GlyphWidthTable? table, double fontSizePx)
    {
        if (!double.IsFinite(fontSizePx) || fontSizePx <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fontSizePx), fontSizePx,
                "A font size is a finite, positive number of pixels.");
        }
        return new(WideWidthPx, DigitWidthPx, NarrowWidthPx, FullWidthPx, CellHorizontalPaddingPx,
            BoldWideWidthPx, BoldDigitWidthPx, BoldNarrowWidthPx, OtherWidthPx, BoldOtherWidthPx,
            table, table is null ? 1 : fontSizePx / table.MeasuredAtPx, _glyphBold);
    }

    /// <summary>
    /// The metrics a value of <paramref name="type"/> is estimated with (ADR-0016; ticket 83).
    /// A Number or Date becomes <c>####</c> when it does not fit, so it is charged every class,
    /// the other class and the glyph table included: these metrics. A Text or Boolean value never
    /// does — it is cut with a visible ellipsis — so it, and a label, is charged every glyph
    /// outside the measured classes at the digit, as before ticket 83: an Auto width or a header
    /// sized for text does not move. The bold widths follow, so <see cref="Bold"/> composes with
    /// this either way round.
    /// </summary>
    public CellTextMetrics For(ColumnType type) => OverflowRules.HashesWhenOverflowing(type)
        ? this
        : new(WideWidthPx, DigitWidthPx, NarrowWidthPx, FullWidthPx, CellHorizontalPaddingPx,
            BoldWideWidthPx, BoldDigitWidthPx, BoldNarrowWidthPx, DigitWidthPx, BoldDigitWidthPx,
            glyphs: null, glyphScale: 1, _glyphBold);

    /// <summary>Copies the explicit glyph table as resolved widths at this instance's scale and
    /// weight, for a Consumer that sizes labels in another process (ADR-0151). Character classes
    /// still take precedence; an absent table yields an empty immutable dictionary.</summary>
    public IReadOnlyDictionary<int, double> ExportGlyphWidths()
    {
        var widths = new Dictionary<int, double>();
        if (GlyphWidths is { } table)
            foreach (var point in table.CodePoints)
                widths.Add(point, WidthOf(point));
        return new System.Collections.ObjectModel.ReadOnlyDictionary<int, double>(widths);
    }

    /// <summary>One character's charge (ADR-0016): its class's, or its own width where the glyph
    /// table holds it (ticket 83). Anything no class names and no table holds is the other class —
    /// the widest glyph measured, so a glyph nobody listed errs toward <c>####</c>. A surrogate
    /// half is charged as the other class; <see cref="TextWidthPx"/> charges the pair as the one
    /// character it encodes.</summary>
    public double WidthOf(char c) => WidthOf((int)c);

    private double WidthOf(int codePoint) => codePoint switch
    {
        '%' or '€' or '\u2212' or '+' or '#' => WideWidthPx,
        '.' or ',' or '(' or ')' or '/' or ':' or ' ' => NarrowWidthPx,
        _ when IsFullWidth(codePoint) => FullWidthPx,
        _ when IsDigitClass(codePoint) => DigitWidthPx,
        _ when GlyphWidths is { } table && table.TryGetWidthPx(codePoint, _glyphBold, out var px) => px * _glyphScale,
        _ => OtherWidthPx,
    };

    /// <summary>
    /// The digit class: the digits, and the glyphs number formats put among them that were
    /// measured at a digit's width or under in each face the grid's widths come from (ticket 83;
    /// <c>tests/GlyphWidths</c> measures every glyph listed here) — the currency signs drawn on a
    /// digit's advance, the exponent's <c>E</c>, the hyphen-minus, the apostrophes and spaces
    /// cultures group digits with, and the zero-width direction marks a right-to-left culture puts
    /// before a sign. Listing a glyph here is a claim about every face, so a glyph joins only once
    /// it is measured in each.
    /// </summary>
    private static bool IsDigitClass(int c) =>
        c is (>= '0' and <= '9')
            or '$' or '£' or '¥' or '\u20B9' or '\u20BA' or '\u20AB'   // ₹ ₺ ₫
            or 'E' or '-'
            or '\'' or '\u2019' or '\u00A0' or '\u202F'              // de-CH's ’, NBSP, narrow NBSP
            or '\u200E' or '\u200F' or '\u061C';                      // LRM, RLM, ALM

    /// <summary>The width of the text alone, no padding — what an action label's box is
    /// measured from (ADR-0020). O(n) over the text, no layout read (ADR-0021), and no
    /// allocation: the rune enumerator is a struct.</summary>
    public double TextWidthPx(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var sum = 0d;
        foreach (var rune in text.EnumerateRunes())
            sum += WidthOf(rune.Value);
        return sum;
    }

    /// <summary>
    /// East Asian Width Wide or Fullwidth (Unicode Standard Annex #11), by the blocks
    /// that carry them: what a CJK face draws on the em square. Ambiguous-width
    /// characters are not here — they are an em only in a CJK face, and the other class
    /// covers what the text faces draw them at.
    /// </summary>
    private static bool IsFullWidth(int c) =>
        c is (>= 0x1100 and <= 0x115F)       // Hangul Jamo initials
            or (>= 0x2E80 and <= 0x303E)     // CJK radicals, Kangxi, ideographic space and punctuation
            or (>= 0x3041 and <= 0x33FF)     // kana, Bopomofo, Hangul compatibility, enclosed and compatibility CJK
            or (>= 0x3400 and <= 0x4DBF)     // CJK Extension A
            or (>= 0x4E00 and <= 0x9FFF)     // CJK Unified Ideographs
            or (>= 0xA000 and <= 0xA4CF)     // Yi
            or (>= 0xA960 and <= 0xA97F)     // Hangul Jamo Extended-A
            or (>= 0xAC00 and <= 0xD7A3)     // Hangul syllables
            or (>= 0xF900 and <= 0xFAFF)     // CJK Compatibility Ideographs
            or (>= 0xFE10 and <= 0xFE19)     // vertical forms
            or (>= 0xFE30 and <= 0xFE6F)     // CJK compatibility and small forms
            or (>= 0xFF01 and <= 0xFF60)     // fullwidth forms
            or (>= 0xFFE0 and <= 0xFFE6)     // fullwidth signs
            or (>= 0x1F300 and <= 0x1F64F)   // pictographs and emoticons
            or (>= 0x1F900 and <= 0x1F9FF)   // supplemental pictographs
            or (>= 0x20000 and <= 0x3FFFD);  // CJK Extensions B onward

    /// <summary>
    /// The full cell width a value requires — the text charged per character class,
    /// plus the padding on both sides. This output is the unit
    /// <see cref="AutoWidth.Observe"/> and <see cref="ColumnWidthSpec.SizeToFit"/>
    /// take, directly comparable to a resolved column width (ADR-0016).
    /// </summary>
    public double EstimatePx(string text) => TextWidthPx(text) + 2 * CellHorizontalPaddingPx;

    /// <summary>The uniform estimate — every character at the digit width. Kept for
    /// callers that only have a count; the text form above is the accurate one.</summary>
    public double EstimatePx(int characterCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(characterCount);
        return characterCount * DigitWidthPx + 2 * CellHorizontalPaddingPx;
    }

    /// <summary>These metrics with the full-width class charged at least
    /// <paramref name="emPx"/>. The grid knows the em — it emits the font size — so it
    /// lifts whatever a theme or the three-class form supplied (ADR-0016).</summary>
    internal CellTextMetrics WithFullWidthAtLeast(double emPx) => FullWidthPx >= emPx
        ? this
        : new CellTextMetrics(WideWidthPx, DigitWidthPx, NarrowWidthPx, emPx, CellHorizontalPaddingPx,
            BoldWideWidthPx, BoldDigitWidthPx, BoldNarrowWidthPx, OtherWidthPx, BoldOtherWidthPx,
            GlyphWidths, _glyphScale, _glyphBold);

    /// <summary>The width left for content after padding; can be zero or negative in a
    /// crushed column.</summary>
    public double ContentWidthPx(double columnWidthPx)
        => columnWidthPx - 2 * CellHorizontalPaddingPx;
}
