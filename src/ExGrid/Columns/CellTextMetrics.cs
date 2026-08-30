namespace ExGrid.Columns;

/// <summary>
/// The two numbers the overflow estimate needs, supplied by the theme/layout layer as
/// C# values — geometry truth lives in C# (ADR-0013), and text measurement deliberately
/// makes no round-trip through JavaScript (ADR-0021).
///
/// <see cref="DigitWidthPx"/> is the width of one tabular digit
/// (<c>font-variant-numeric: tabular-nums</c>), and contractually **at least as wide as
/// any glyph the column's formats emit**. A currency symbol wider than a digit would
/// otherwise turn the estimate into an under-estimate — and a clipped number that looks
/// valid is the exact failure <c>####</c> exists to prevent (ADR-0016).
/// </summary>
public readonly record struct CellTextMetrics
{
    public CellTextMetrics(double digitWidthPx, double cellHorizontalPaddingPx)
    {
        if (!double.IsFinite(digitWidthPx) || digitWidthPx <= 0)
            throw new ArgumentOutOfRangeException(nameof(digitWidthPx), digitWidthPx,
                "A digit width is a finite, positive number of pixels.");
        if (!double.IsFinite(cellHorizontalPaddingPx) || cellHorizontalPaddingPx < 0)
            throw new ArgumentOutOfRangeException(nameof(cellHorizontalPaddingPx), cellHorizontalPaddingPx,
                "Cell padding is a finite, non-negative number of pixels.");

        DigitWidthPx = digitWidthPx;
        CellHorizontalPaddingPx = cellHorizontalPaddingPx;
    }

    public double DigitWidthPx { get; }

    /// <summary>Padding on one side; a cell pays it twice.</summary>
    public double CellHorizontalPaddingPx { get; }

    /// <summary>
    /// The full cell width a value requires — every character counted at one
    /// tabular-digit width, plus the padding on both sides. This output is the unit
    /// <see cref="AutoWidth.Observe"/> and <see cref="ColumnWidthSpec.SizeToFit"/>
    /// take, directly comparable to a resolved column width. Separators
    /// (<c>. , - /</c>) are narrower in practice, so the estimate errs toward showing
    /// <c>####</c> one glyph early — the safe direction: an early <c>####</c> costs a
    /// hover, a clipped number costs a misread (ADR-0016).
    /// </summary>
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
