namespace ExGrid.Components;

/// <summary>
/// The inline styles a <see cref="ColumnGeometry"/> implies, built once per geometry
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

    /// <summary>Writes every column's style strings from <paramref name="geometry"/>,
    /// once.</summary>
    public ColumnStyles(ColumnGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        Geometry = geometry;

        _cells = new string[geometry.Count];
        _pinnedCells = new string[geometry.PinnedCount];
        _gaps = new string[geometry.Count];
        for (var i = 0; i < geometry.Count; i++)
        {
            var width = geometry.WidthPxOf(i);
            _cells[i] = FormattableString.Invariant($"width: {width}px");
            if (i < geometry.PinnedCount)
            {
                // A sticky cell still occupies its place in the flow, so `left` is its
                // own offset — it sticks exactly where it would otherwise have been, and
                // only once the content has scrolled past it does it stop moving.
                _pinnedCells[i] = FormattableString.Invariant(
                    $"width: {width}px; left: {geometry.OffsetPxOf(i)}px");
            }
            // What stands in for the columns left out to the left of the first painted
            // one. The pinned block is already in the flow ahead of it, so its width
            // comes off — measuring from the content's edge instead would push every
            // painted column right by exactly the pinned width.
            _gaps[i] = FormattableString.Invariant(
                $"width: {Math.Max(0, geometry.OffsetPxOf(i) - geometry.PinnedWidthPx)}px");
        }
    }

    /// <summary>The geometry these strings were written from. A new instance is built
    /// only when a width actually moved, so a row can compare it by reference
    /// (ADR-0003/0016).</summary>
    public ColumnGeometry Geometry { get; }

    /// <summary>The width of an ordinary cell or header cell.</summary>
    public string Cell(int columnIndex) => _cells[columnIndex];

    /// <summary>The width and sticky offset of a Pinned Column's cell.</summary>
    public string PinnedCell(int columnIndex) => _pinnedCells[columnIndex];

    /// <summary>The width of the spacer standing in for everything left of the first
    /// painted scrollable column.</summary>
    public string Gap(int firstScrollableColumn) => _gaps[firstScrollableColumn];

    /// <summary>Whether that spacer is worth emitting at all — it is exactly zero when
    /// the first painted column sits against the pinned block, which is where every
    /// unvirtualised grid starts.</summary>
    public bool HasGap(int firstScrollableColumn)
        => Geometry.OffsetPxOf(firstScrollableColumn) > Geometry.PinnedWidthPx;
}
