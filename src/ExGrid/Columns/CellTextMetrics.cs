namespace ExGrid.Columns;

/// <summary>
/// The numbers the overflow estimate needs, supplied by the theme/layout layer as C#
/// values — geometry truth lives in C# (ADR-0013), and text measurement deliberately
/// makes no round-trip through JavaScript (ADR-0021).
///
/// <para>The estimate charges <b>per character class</b> (ADR-0016): three widths —
/// wide, digit, narrow — because one number cannot serve a twelve-digit amount column
/// and a percent column at once. Measured in Chrome, <c>system-ui</c> at the
/// stylesheet's 14px, <c>tabular-nums</c>, at the 600 weight the group and total rows
/// paint: a digit is 9.058px, <c>%</c> is 13.836px — 54% past the digit — and the
/// separators run 4.40–5.63px. Each width is contractually <b>at least as wide as any
/// glyph of its class the column's formats emit</b>, at the boldest weight the grid
/// itself paints: overshooting errs toward an early <c>####</c>, the safe direction.</para>
/// </summary>
public readonly record struct CellTextMetrics
{
    /// <summary>The uniform form: every character charged at one tabular-digit width.
    /// What a theme that measured only its digit supplies; the safe direction holds as
    /// long as the digit covers the widest glyph its formats emit.</summary>
    public CellTextMetrics(double digitWidthPx, double cellHorizontalPaddingPx)
        : this(digitWidthPx, digitWidthPx, digitWidthPx, cellHorizontalPaddingPx)
    {
    }

    /// <summary>The per-class form (ADR-0016): the wide, digit and narrow widths, and the
    /// padding on one side. Refuses a narrow width wider than the digit, or a wide width
    /// narrower than it.</summary>
    public CellTextMetrics(
        double wideWidthPx, double digitWidthPx, double narrowWidthPx, double cellHorizontalPaddingPx)
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
        if (!double.IsFinite(cellHorizontalPaddingPx) || cellHorizontalPaddingPx < 0)
            throw new ArgumentOutOfRangeException(nameof(cellHorizontalPaddingPx), cellHorizontalPaddingPx,
                "Cell padding is a finite, non-negative number of pixels.");

        WideWidthPx = wideWidthPx;
        DigitWidthPx = digitWidthPx;
        NarrowWidthPx = narrowWidthPx;
        CellHorizontalPaddingPx = cellHorizontalPaddingPx;
    }

    /// <summary>What <c>%</c> costs — and <c>€</c>, whose measured 9.331px runs past
    /// the digit: charging it here overshoots, which is the safe direction.</summary>
    public double WideWidthPx { get; }

    /// <summary>What a tabular digit costs — and every character not classed wide or
    /// narrow. One <c>#</c> of an overflowing cell is charged at it too.</summary>
    public double DigitWidthPx { get; }

    /// <summary>What the separators cost — <c>. , ( ) / :</c> and the space.</summary>
    public double NarrowWidthPx { get; }

    /// <summary>Padding on one side; a cell pays it twice.</summary>
    public double CellHorizontalPaddingPx { get; }

    /// <summary>One character's charge, by class (ADR-0016). Anything unclassified is
    /// a digit — the middle of the three, and what the tabular-digit contract makes
    /// safe for every glyph a format emits.</summary>
    public double WidthOf(char c) => c switch
    {
        '%' or '€' => WideWidthPx,
        '.' or ',' or '(' or ')' or '/' or ':' or ' ' => NarrowWidthPx,
        _ => DigitWidthPx,
    };

    /// <summary>The width of the text alone, no padding — what an action label's box is
    /// measured from (ADR-0020). O(n) over the text, no layout read (ADR-0021).</summary>
    public double TextWidthPx(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var sum = 0d;
        foreach (var c in text)
            sum += WidthOf(c);
        return sum;
    }

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

    /// <summary>The width left for content after padding; can be zero or negative in a
    /// crushed column.</summary>
    public double ContentWidthPx(double columnWidthPx)
        => columnWidthPx - 2 * CellHorizontalPaddingPx;
}
