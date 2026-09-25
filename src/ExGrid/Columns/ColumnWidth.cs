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

    /// <summary>Computed from content, and never narrowing on its own (ADR-0016). The
    /// zero state.</summary>
    public static ColumnWidth Auto => default;

    /// <summary>A fixed number of pixels, finite and positive — declared, or the user's
    /// intent after a drag or Size to fit (ADR-0016).</summary>
    public static ColumnWidth Fixed(double px)
    {
        if (!double.IsFinite(px) || px <= 0)
            throw new ArgumentOutOfRangeException(nameof(px), px,
                "A Fixed width is a finite, positive number of pixels.");
        return new(px);
    }

    /// <summary>Whether this is an Auto width.</summary>
    public bool IsAuto => _px == 0;

    /// <summary>The Fixed width in pixels. Throws when Auto: an Auto width is resolved
    /// through its <see cref="AutoWidth"/> tracker, never read from here (ADR-0016).</summary>
    public double FixedPx => IsAuto
        ? throw new InvalidOperationException(
            "An Auto width has no fixed pixel value; resolve it through its AutoWidth tracker (ADR-0016).")
        : _px;

    /// <summary><c>Auto</c> or <c>Fixed(120px)</c>. Written by hand: the record's own
    /// would read <see cref="FixedPx"/>, which throws for Auto, so printing a width for a
    /// message or a log would throw instead.</summary>
    public override string ToString()
        => IsAuto ? "Auto" : string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Fixed({_px}px)");
}
