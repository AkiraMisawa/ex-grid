namespace ExGrid.Columns;

/// <summary>
/// The width declaration a column carries: Auto or Fixed, bounded by
/// [<see cref="MinWidthPx"/>, <see cref="MaxWidthPx"/>] (ADR-0016). MaxWidth is what
/// gives <c>####</c> meaning — without an upper bound the column would keep growing and
/// overflow could never occur. A declared Fixed width outside the bounds is refused,
/// not clamped: a declaration that contradicts its own bounds is an error, not an
/// intent. (<c>default(ColumnWidthSpec)</c> has zero bounds and is not a valid spec —
/// the same tolerated struct-default hole as <c>default(SelectionRange)</c>.)
/// </summary>
public readonly record struct ColumnWidthSpec
{
    public const double DefaultMinWidthPx = 40;
    public const double DefaultMaxWidthPx = 400;

    public ColumnWidthSpec(
        ColumnWidth width,
        double minWidthPx = DefaultMinWidthPx,
        double maxWidthPx = DefaultMaxWidthPx)
    {
        if (!double.IsFinite(minWidthPx) || minWidthPx <= 0)
            throw new ArgumentOutOfRangeException(nameof(minWidthPx), minWidthPx,
                "MinWidth is a finite, positive number of pixels.");
        if (!double.IsFinite(maxWidthPx) || maxWidthPx < minWidthPx)
            throw new ArgumentOutOfRangeException(nameof(maxWidthPx), maxWidthPx,
                "MaxWidth is finite and at least MinWidth.");
        if (!width.IsAuto && (width.FixedPx < minWidthPx || width.FixedPx > maxWidthPx))
            throw new ArgumentOutOfRangeException(nameof(width), width.FixedPx,
                $"A declared Fixed width must lie within [{minWidthPx}, {maxWidthPx}] — refused, not clamped (ADR-0016).");

        Width = width;
        MinWidthPx = minWidthPx;
        MaxWidthPx = maxWidthPx;
    }

    public ColumnWidth Width { get; }

    /// <summary>Also the lower bound for dragging — a column cannot be crushed until it
    /// disappears; hiding is an explicit column-menu intent (ADR-0016).</summary>
    public double MinWidthPx { get; }

    public double MaxWidthPx { get; }

    public double Clamp(double px) => Math.Clamp(px, MinWidthPx, MaxWidthPx);

    /// <summary>
    /// "Size to fit": fixes the width at the content of this moment, clamped — an
    /// approximation over the fetched rows (ADR-0016). Takes the widest value's full
    /// required cell width (<see cref="CellTextMetrics.EstimatePx"/> output, padding
    /// included — the same unit <see cref="AutoWidth.Observe"/> takes). Always yields a
    /// Fixed width, and it MAY be narrower than the current one: this is the only way
    /// an Auto column narrows.
    /// </summary>
    public ColumnWidth SizeToFit(double requiredWidthPx)
    {
        if (!double.IsFinite(requiredWidthPx) || requiredWidthPx < 0)
            throw new ArgumentOutOfRangeException(nameof(requiredWidthPx), requiredWidthPx,
                "A required width is a finite, non-negative number of pixels.");
        return ColumnWidth.Fixed(Clamp(requiredWidthPx));
    }

    /// <summary>The one place the Auto-or-Fixed branch lives, so no consumer re-derives
    /// which width applies (ADR-0016).</summary>
    public double ResolveWidthPx(AutoWidth current)
        => Width.IsAuto ? current.CurrentPx : Width.FixedPx;
}
