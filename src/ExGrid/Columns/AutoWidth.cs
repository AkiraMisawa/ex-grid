namespace ExGrid.Columns;

/// <summary>
/// The current computed width of an Auto column. It only ever grows (ADR-0016): a width
/// that moved back down as screens of short values scrolled past would make the columns
/// judder, so not narrowing is what makes it settle. Grow-only is structural — the only
/// transition is <see cref="Observe"/>, which takes the maximum internally, never a
/// caller's discipline. Narrowing happens only through the spec's explicit size-to-fit.
///
/// The spec lives inside the tracker: its bounds are part of what the tracker is, and a
/// changed column declaration correctly means a fresh tracker re-measured from the next
/// Window.
/// </summary>
public readonly record struct AutoWidth
{
    private readonly ColumnWidthSpec _spec;

    /// <summary>A fresh tracker for a column declared with <paramref name="spec"/>. Before
    /// anything is observed the column stands at the spec's MinWidth (ADR-0016).</summary>
    public AutoWidth(ColumnWidthSpec spec)
    {
        _spec = spec;
        // The ADR's "initially: computed from the first Window" is the first
        // observations growing this floor — before anything is observed the column
        // stands at MinWidth.
        CurrentPx = spec.MinWidthPx;
    }

    /// <summary>The width the column stands at now: MinWidth until something is observed,
    /// then the widest observation clamped to the spec's bounds. It never decreases.</summary>
    public double CurrentPx { get; private init; }

    /// <summary>
    /// Folds in the full cell width one value requires — the output of
    /// <see cref="CellTextMetrics.EstimatePx(string)"/>, padding included, in the same unit as
    /// the resolved column width. NOT the inner content width of
    /// <see cref="CellTextMetrics.ContentWidthPx"/>: feeding that here would produce a
    /// column exactly 2×padding too narrow, hashing the very value it auto-sized to.
    /// Clamped to the spec's bounds; monotone, commutative and order-independent, so
    /// the same observations settle at the same width in any order and no oscillation
    /// is possible.
    /// </summary>
    public AutoWidth Observe(double requiredWidthPx)
    {
        if (!double.IsFinite(requiredWidthPx) || requiredWidthPx < 0)
            throw new ArgumentOutOfRangeException(nameof(requiredWidthPx), requiredWidthPx,
                "A required width is a finite, non-negative number of pixels.");
        return this with { CurrentPx = Math.Max(CurrentPx, _spec.Clamp(requiredWidthPx)) };
    }
}
