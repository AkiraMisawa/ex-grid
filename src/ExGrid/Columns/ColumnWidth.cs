namespace ExGrid.Columns;

/// <summary>
/// A column's declared width: Auto (computed from content) or a Fixed number of pixels
/// (ADR-0016). The zero state IS Auto, so an unassigned width means the sensible
/// default rather than an invalid value.
/// </summary>
public readonly record struct ColumnWidth
{
    private readonly double _px; // 0 is Auto; Fixed is always positive

    private ColumnWidth(double px) => _px = px;

    public static ColumnWidth Auto => default;

    public static ColumnWidth Fixed(double px)
    {
        if (!double.IsFinite(px) || px <= 0)
            throw new ArgumentOutOfRangeException(nameof(px), px,
                "A Fixed width is a finite, positive number of pixels.");
        return new(px);
    }

    public bool IsAuto => _px == 0;

    public double FixedPx => IsAuto
        ? throw new InvalidOperationException(
            "An Auto width has no fixed pixel value; resolve it through its AutoWidth tracker (ADR-0016).")
        : _px;
}
