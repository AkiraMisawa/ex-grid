namespace ExGrid.Components;

/// <summary>
/// The inline styles a <see cref="ColumnGeometry"/> implies, each written once per geometry
/// instead of once per cell.
///
/// A width is a property of the column, not of the cell, so composing
/// <c>"width: 120px"</c> inside the render loop meant one string per cell per frame —
/// 242 of them at 22 rows × 11 columns, every frame of a scroll, all identical in groups.
/// The geometry changes only when a width actually moves, so the strings can be cached
/// alongside it and the render path allocates nothing at all.
///
/// It lives in the component layer rather than in <see cref="ColumnGeometry"/> because
/// these are CSS, not arithmetic: the pure layer answers where a column is, and this
/// answers how that is written down for a browser.
/// </summary>
public sealed class ColumnStyles
{
    private readonly string[] _cells;
    private readonly string[] _pinnedCells;
    private readonly string[] _gaps;

    /// <summary>The style strings of <paramref name="geometry"/>'s columns, each written the
    /// first time a render asks for it and kept from then on.</summary>
    /// <remarks>Written on demand rather than all at once: a Sheet has 16,384 columns, of
    /// which a render paints a few dozen. Formatting three strings for every one of them was
    /// most of a geometry's cost under the WebAssembly interpreter, and the Device Pixel's
    /// report, which rebuilds the geometry once at attach (ADR-0090), doubled it: /sheet
    /// arrived 1.2 s later. A string asked for again is the one written the first time, so a
    /// render that repaints the same columns allocates nothing (ADR-0027 P5).</remarks>
    public ColumnStyles(ColumnGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        Geometry = geometry;

        _cells = new string[geometry.Count];
        _pinnedCells = new string[geometry.PinnedCount];
        _gaps = new string[geometry.Count];
    }

    /// <summary>The geometry these strings were written from. A new instance is built
    /// only when a width actually moved, so a row can compare it by reference
    /// (ADR-0003/0016).</summary>
    public ColumnGeometry Geometry { get; }

    /// <summary>The width of an ordinary cell or header cell.</summary>
    public string Cell(int columnIndex)
        => _cells[columnIndex] ??= FormattableString.Invariant($"width: {Geometry.WidthPxOf(columnIndex)}px");

    /// <summary>The width and sticky offset of a Pinned Column's cell.</summary>
    // A sticky cell still occupies its place in the flow, so `left` is its own offset — it
    // sticks exactly where it would otherwise have been, and only once the content has
    // scrolled past it does it stop moving.
    public string PinnedCell(int columnIndex)
        => _pinnedCells[columnIndex] ??= FormattableString.Invariant(
            $"width: {Geometry.WidthPxOf(columnIndex)}px; left: {Geometry.OffsetPxOf(columnIndex)}px");

    /// <summary>The width of the spacer standing in for everything left of the first
    /// painted scrollable column.</summary>
    // The pinned block is already in the flow ahead of it, so its width comes off —
    // measuring from the content's edge instead would push every painted column right by
    // exactly the pinned width.
    public string Gap(int firstScrollableColumn)
        => _gaps[firstScrollableColumn] ??= FormattableString.Invariant(
            $"width: {Math.Max(0, Geometry.OffsetPxOf(firstScrollableColumn) - Geometry.PinnedWidthPx)}px");

    /// <summary>Whether that spacer is worth emitting at all — it is exactly zero when
    /// the first painted column sits against the pinned block, which is where every
    /// unvirtualised grid starts.</summary>
    public bool HasGap(int firstScrollableColumn)
        => Geometry.OffsetPxOf(firstScrollableColumn) > Geometry.PinnedWidthPx;
}
