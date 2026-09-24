namespace ExGrid.Selection;

/// <summary>
/// The selection: a list of rectangles in position space plus one Anchor (the fixed end
/// of range extension) and one Focus (the moving end, where keyboard operations start)
/// (ADR-0011 / 0012). Immutable — every gesture is a pure transition returning a new
/// value, and each takes the current <see cref="GridExtent"/> because the model never
/// holds data-dependent bounds.
///
/// Which range the Anchor and Focus belong to is <em>stored</em>, never re-derived from
/// geometry: ranges can overlap, and a Ctrl+click toggle-off leaves both standing
/// detached on the deselected cell, outside every range. Inferring membership from
/// coordinates picks an arbitrary range in exactly those states.
///
/// Dropping the selection is the holder's act: when the Row Sequence Version (or the
/// visible-column set) changes, the holder assigns <see cref="Empty"/> (ADR-0011). A
/// transition applied to a state that no longer fits the extent throws — that is a
/// missed drop, and positions would quietly point at different cells.
///
/// Mouse drag is not a distinct transition: the holder maps mousedown to
/// <see cref="Click"/> and mousemove to <see cref="ExtendTo"/>. Do not invent a third
/// path in the binding layer.
/// </summary>
public sealed record GridSelection
{
    /// <summary>Nothing selected: no range, and no Anchor or Focus. What a holder assigns
    /// when the Row Sequence Version or the visible-column set changes (ADR-0011).</summary>
    public static GridSelection Empty { get; } = new([], default, default, anchorDetached: false, focusRangeIndex: null);

    private readonly CellPosition _anchor;
    private readonly CellPosition _focus;

    /// <summary>True only after a Ctrl+click toggle-off: the Anchor stands on the
    /// deselected cell, outside every range. Otherwise the Anchor is in the last range.</summary>
    private readonly bool _anchorDetached;

    /// <summary>Index of the range holding the Focus; null when the Focus is detached
    /// (right after a toggle-off). Cycling moves it between ranges (ADR-0012).</summary>
    private readonly int? _focusRangeIndex;

    private GridSelection(
        IReadOnlyList<SelectionRange> ranges,
        CellPosition anchor,
        CellPosition focus,
        bool anchorDetached,
        int? focusRangeIndex)
    {
        if (ranges.Count > 0)
        {
            if (!anchorDetached && !ranges[^1].Contains(anchor))
                throw new InvalidOperationException("Internal: an attached Anchor must lie in the last range (ADR-0012).");
            if (focusRangeIndex is int index && (index < 0 || index >= ranges.Count || !ranges[index].Contains(focus)))
                throw new InvalidOperationException("Internal: the Focus range index must name a range containing the Focus (ADR-0012).");
        }

        // Stored behind a read-only wrapper so no caller can cast Ranges back to the
        // array and mutate a rectangle in place, past the invariant checks above (the
        // FilterOperators pattern).
        Ranges = ranges switch
        {
            SelectionRange[] array => Array.AsReadOnly(array),
            List<SelectionRange> list => list.AsReadOnly(),
            _ => ranges,
        };
        _anchor = anchor;
        _focus = focus;
        _anchorDetached = anchorDetached;
        _focusRangeIndex = focusRangeIndex;
    }

    /// <summary>
    /// The rectangles in creation order — which is also the Enter/Tab cycling order
    /// (ADR-0012) and the one-overlay-per-range painting order (ADR-0008).
    /// </summary>
    public IReadOnlyList<SelectionRange> Ranges { get; }

    /// <summary>Whether nothing is selected. <see cref="Anchor"/> and <see cref="Focus"/>
    /// throw then.</summary>
    public bool IsEmpty => Ranges.Count == 0;

    /// <summary>
    /// The fixed end of range extension (ADR-0012). Always inside the last range, except
    /// right after a Ctrl+click toggle-off, when Anchor and Focus stand detached on the
    /// deselected cell outside every range.
    /// </summary>
    public CellPosition Anchor => IsEmpty
        ? throw new InvalidOperationException("An empty selection has no Anchor. Establish one with Click first (ADR-0012).")
        : _anchor;

    /// <summary>The one cell keyboard operations start from (ADR-0012).</summary>
    public CellPosition Focus => IsEmpty
        ? throw new InvalidOperationException("An empty selection has no Focus. Establish one with Click first (ADR-0012).")
        : _focus;

    /// <summary>
    /// The selected-cell count for the status display — the sum of rectangle areas, so it
    /// needs no data (ADR-0014). Overlapping ranges double-count, as Excel's status bar
    /// does.
    /// </summary>
    public long CellCount
    {
        get
        {
            long sum = 0;
            foreach (var range in Ranges)
                sum += range.CellCount;
            return sum;
        }
    }

    /// <summary>Whether the cell lies in any range. A detached Anchor and Focus stand on a
    /// cell this answers false for (ADR-0012).</summary>
    public bool Contains(CellPosition cell)
    {
        foreach (var range in Ranges)
            if (range.Contains(cell))
                return true;
        return false;
    }

    /// <summary>Click: Anchor = Focus = the cell; the selection collapses to it (ADR-0012).</summary>
    public GridSelection Click(CellPosition cell, GridExtent extent)
    {
        if (IsDegenerate(extent))
            return Empty;
        RequireInside(cell, extent);
        return Collapse(cell);
    }

    /// <summary>
    /// Shift+click: the Anchor stays, the Focus moves to the cell and the Anchor's range
    /// is redrawn between them (ADR-0012) — the target is the cell the user pointed at,
    /// so the Anchor is kept even when cycling had parked the Focus elsewhere. From a
    /// detached Anchor this starts a new range; from Empty it behaves as a plain click.
    /// </summary>
    public GridSelection ExtendTo(CellPosition cell, GridExtent extent)
    {
        if (IsDegenerate(extent))
            return Empty;
        RequireInside(cell, extent);
        if (IsEmpty)
            return Collapse(cell);
        RequireFits(extent);

        var redrawn = SelectionRange.FromCorners(_anchor, cell);
        var ranges = _anchorDetached ? Append(Ranges, redrawn) : ReplaceLast(Ranges, redrawn);
        return new(ranges, _anchor, cell, anchorDetached: false, ranges.Length - 1);
    }

    /// <summary>
    /// Ctrl+click (ADR-0012). On an unselected cell: adds a new 1×1 range and moves
    /// Anchor and Focus into it. On a selected cell: toggles it off — the cell is
    /// subtracted from every range containing it (a rectangle splits into at most four),
    /// and Anchor and Focus stand detached on the deselected cell. Toggling off the last
    /// cell yields <see cref="Empty"/>.
    /// </summary>
    public GridSelection ToggleRange(CellPosition cell, GridExtent extent)
    {
        if (IsDegenerate(extent))
            return Empty;
        RequireInside(cell, extent);
        if (IsEmpty)
            return Collapse(cell);
        RequireFits(extent);

        if (!Contains(cell))
        {
            var appended = Append(Ranges, new SelectionRange(cell.Row, cell.Column, 1, 1));
            return new(appended, cell, cell, anchorDetached: false, appended.Length - 1);
        }

        var remaining = new List<SelectionRange>();
        foreach (var range in Ranges)
        {
            if (range.Contains(cell))
                remaining.AddRange(range.Subtract(cell));
            else
                remaining.Add(range);
        }
        return remaining.Count == 0
            ? Empty
            : new(remaining, cell, cell, anchorDetached: true, focusRangeIndex: null);
    }

    /// <summary>Arrow: collapses the selection to one cell and moves, clamped at the grid
    /// edge (ADR-0012). A no-op on Empty — there is no Focus to start from.</summary>
    public GridSelection Move(GridDirection direction, GridExtent extent)
        => MoveFocusTo(extent, focus => Step(focus, direction, extent));

    /// <summary>Shift+arrow: the Focus moves one step and the Anchor's range grows or
    /// shrinks — shrinking back through the Anchor flips it (ADR-0012). When the Focus is
    /// not in the Anchor's range, re-anchors at the Focus and starts a new range.</summary>
    public GridSelection Extend(GridDirection direction, GridExtent extent)
        => ExtendFocusTo(extent, focus => Step(focus, direction, extent));

    /// <summary>Ctrl+arrow: collapses and jumps to the last / first row or column — not
    /// Excel's block edge, which the grid cannot find without the data (ADR-0011).</summary>
    public GridSelection MoveToEdge(GridDirection direction, GridExtent extent)
        => MoveFocusTo(extent, focus => EdgeOf(focus, direction, extent));

    /// <summary>Shift+Ctrl+arrow: extends the range to the edge (ADR-0012). From the
    /// first row, Ctrl+Shift+Down is effectively a whole-column selection.</summary>
    public GridSelection ExtendToEdge(GridDirection direction, GridExtent extent)
        => ExtendFocusTo(extent, focus => EdgeOf(focus, direction, extent));

    /// <summary>PageUp / PageDown (ADR-0012): collapse and move the Focus by one
    /// Viewport of rows. How many rows that is is view geometry the model never holds,
    /// so the caller passes the signed delta; the grid edge clamps it.</summary>
    public GridSelection MoveByViewport(int rowDelta, GridExtent extent)
        => MoveFocusTo(extent, focus => StepRows(focus, rowDelta, extent));

    /// <summary>Shift+PageUp / Shift+PageDown (ADR-0012): the Focus moves by one
    /// Viewport of rows and the Anchor's range is redrawn between them.</summary>
    public GridSelection ExtendByViewport(int rowDelta, GridExtent extent)
        => ExtendFocusTo(extent, focus => StepRows(focus, rowDelta, extent));

    /// <summary>
    /// Ctrl+A: every row after filtering across every visible column, as one rectangle —
    /// expressible without holding the data (ADR-0011). Anchor and Focus stay where they
    /// are; from Empty they land on (0, 0).
    /// </summary>
    public GridSelection SelectAll(GridExtent extent)
    {
        if (IsDegenerate(extent))
            return Empty;
        var all = new SelectionRange(0, 0, extent.RowCount, extent.ColumnCount);
        if (IsEmpty)
            return new([all], new(0, 0), new(0, 0), anchorDetached: false, focusRangeIndex: 0);
        RequireFits(extent);
        return new([all], _anchor, _focus, anchorDetached: false, focusRangeIndex: 0);
    }

    /// <summary>Ctrl+A under a pager (ADR-0015): every cell of the rows in context —
    /// the page — as one range. Anchor and Focus stay exactly as <see cref="SelectAll(GridExtent)"/>
    /// keeps them: a key that names a whole region needs no starting point and must not
    /// move the active cell (ADR-0012). From Empty they land on the context's first
    /// cell — as they do when they stand outside the context (the selection came from
    /// another page), because a range must contain its own Focus.</summary>
    public GridSelection SelectAll(GridExtent extent, int firstRow, int rowCount)
    {
        if (IsDegenerate(extent))
            return Empty;
        var start = Math.Clamp(firstRow, 0, extent.RowCount - 1);
        var count = Math.Clamp(rowCount, 1, extent.RowCount - start);
        var context = new SelectionRange(start, 0, count, extent.ColumnCount);
        if (IsEmpty || !context.Contains(_anchor) || !context.Contains(_focus))
            return new([context], new(start, 0), new(start, 0), anchorDetached: false, focusRangeIndex: 0);
        RequireFits(extent);
        return new([context], _anchor, _focus, anchorDetached: false, focusRangeIndex: 0);
    }

    /// <summary>Ctrl+Space: the Anchor's range expands to every row, keeping its column
    /// span (ADR-0012). From a detached Anchor, starts a new whole-column range at the
    /// deselected cell. Anchor and Focus stay. A no-op on Empty.</summary>
    public GridSelection SelectWholeColumns(GridExtent extent)
        => GrowAxis(extent,
            last => new(0, last.LeftColumn, extent.RowCount, last.ColumnCount),
            anchor => new(0, anchor.Column, extent.RowCount, 1));

    /// <summary>Shift+Space: the Anchor's range expands to every visible column, keeping
    /// its row span (ADR-0012). From a detached Anchor, starts a new whole-row range at
    /// the deselected cell. Anchor and Focus stay. A no-op on Empty.</summary>
    public GridSelection SelectWholeRows(GridExtent extent)
        => GrowAxis(extent,
            last => new(last.TopRow, 0, last.RowCount, extent.ColumnCount),
            anchor => new(anchor.Row, 0, 1, extent.ColumnCount));

    /// <summary>
    /// Enter / Tab (ADR-0012): with a range selected, only the Focus moves — Enter
    /// column-major, Tab row-major, wrapping past the last cell to the next range in
    /// creation order and from the last range back to the first; <paramref name="backward"/>
    /// (Shift+) mirrors. With a single cell, Enter moves down and Tab moves right, the
    /// selection follows, and the grid edge clamps. A detached Focus (after a toggle-off)
    /// enters the first range's first cell, or the last range's last cell going backward.
    /// </summary>
    public GridSelection CycleFocus(CycleOrder order, bool backward, GridExtent extent)
    {
        if (order is not (CycleOrder.ColumnMajor or CycleOrder.RowMajor))
            throw new ArgumentOutOfRangeException(nameof(order), order, null);
        if (IsDegenerate(extent))
            return Empty;
        if (IsEmpty)
            return Empty;
        RequireFits(extent);

        if (_focusRangeIndex is not int index)
        {
            var target = backward ? Ranges.Count - 1 : 0;
            var entered = Ranges[target];
            return WithFocus(CellAt(entered, backward ? entered.CellCount - 1 : 0, order), target);
        }

        if (Ranges is [{ CellCount: 1 }])
        {
            var direction = order == CycleOrder.ColumnMajor
                ? (backward ? GridDirection.Up : GridDirection.Down)
                : (backward ? GridDirection.Left : GridDirection.Right);
            return Collapse(Step(_focus, direction, extent));
        }

        var range = Ranges[index];
        var next = OrdinalOf(_focus, range, order) + (backward ? -1 : 1);
        if (next >= range.CellCount)
        {
            var following = (index + 1) % Ranges.Count;
            return WithFocus(CellAt(Ranges[following], 0, order), following);
        }
        if (next < 0)
        {
            var preceding = (index - 1 + Ranges.Count) % Ranges.Count;
            var entered = Ranges[preceding];
            return WithFocus(CellAt(entered, entered.CellCount - 1, order), preceding);
        }
        return WithFocus(CellAt(range, next, order), index);
    }

    /// <summary>Structural equality: two selections built by identical gestures are equal —
    /// what a ShouldRender-style "did the selection change" comparison needs (ADR-0003).</summary>
    public bool Equals(GridSelection? other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        if (Ranges.Count != other.Ranges.Count)
            return false;
        for (var i = 0; i < Ranges.Count; i++)
        {
            if (Ranges[i] != other.Ranges[i])
                return false;
        }
        if (IsEmpty)
            return true;
        return _anchor == other._anchor && _focus == other._focus
            && _anchorDetached == other._anchorDetached && _focusRangeIndex == other._focusRangeIndex;
    }

    /// <summary>Consistent with <see cref="Equals(GridSelection)"/>: the ranges, and Anchor
    /// and Focus when not empty.</summary>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var range in Ranges)
            hash.Add(range);
        if (!IsEmpty)
        {
            hash.Add(_anchor);
            hash.Add(_focus);
            hash.Add(_anchorDetached);
            hash.Add(_focusRangeIndex);
        }
        return hash.ToHashCode();
    }

    private GridSelection MoveFocusTo(GridExtent extent, Func<CellPosition, CellPosition> destination)
    {
        if (IsDegenerate(extent))
            return Empty;
        if (IsEmpty)
            return Empty;
        RequireFits(extent);
        return Collapse(destination(_focus));
    }

    private GridSelection ExtendFocusTo(GridExtent extent, Func<CellPosition, CellPosition> destination)
    {
        if (IsDegenerate(extent))
            return Empty;
        if (IsEmpty)
            return Empty;
        RequireFits(extent);

        var moved = destination(_focus);
        if (!_anchorDetached && _focusRangeIndex == Ranges.Count - 1)
        {
            // Anchor and Focus share the last range: redraw it between them (ADR-0012).
            var ranges = ReplaceLast(Ranges, SelectionRange.FromCorners(_anchor, moved));
            return new(ranges, _anchor, moved, anchorDetached: false, ranges.Length - 1);
        }

        // The Anchor is detached, or cycling parked the Focus in another range. Re-anchor
        // at the Focus and start a new range — redrawing the Anchor's range would bridge
        // the two with one keystroke and select cells the user never touched (ADR-0012).
        var appended = Append(Ranges, SelectionRange.FromCorners(_focus, moved));
        return new(appended, _focus, moved, anchorDetached: false, appended.Length - 1);
    }

    private GridSelection GrowAxis(
        GridExtent extent,
        Func<SelectionRange, SelectionRange> expandLast,
        Func<CellPosition, SelectionRange> wholeOfAnchor)
    {
        if (IsDegenerate(extent))
            return Empty;
        if (IsEmpty)
            return Empty;
        RequireFits(extent);

        if (_anchorDetached)
        {
            var appended = Append(Ranges, wholeOfAnchor(_anchor));
            return new(appended, _anchor, _focus, anchorDetached: false, _focusRangeIndex ?? appended.Length - 1);
        }
        var ranges = ReplaceLast(Ranges, expandLast(Ranges[^1]));
        return new(ranges, _anchor, _focus, anchorDetached: false, _focusRangeIndex);
    }

    private GridSelection WithFocus(CellPosition focus, int rangeIndex)
        => new(Ranges, _anchor, focus, _anchorDetached, rangeIndex);

    private static GridSelection Collapse(CellPosition cell)
        => new([new SelectionRange(cell.Row, cell.Column, 1, 1)], cell, cell, anchorDetached: false, focusRangeIndex: 0);

    private static SelectionRange[] ReplaceLast(IReadOnlyList<SelectionRange> ranges, SelectionRange replacement)
    {
        var copy = new SelectionRange[ranges.Count];
        for (var i = 0; i < ranges.Count; i++)
            copy[i] = ranges[i];
        copy[^1] = replacement;
        return copy;
    }

    private static SelectionRange[] Append(IReadOnlyList<SelectionRange> ranges, SelectionRange added)
    {
        var copy = new SelectionRange[ranges.Count + 1];
        for (var i = 0; i < ranges.Count; i++)
            copy[i] = ranges[i];
        copy[^1] = added;
        return copy;
    }

    /// <summary>A grid with no rows or no columns has nothing to select — every
    /// transition yields Empty. Not an error.</summary>
    private static bool IsDegenerate(GridExtent extent)
        => extent.RowCount <= 0 || extent.ColumnCount <= 0;

    private static void RequireInside(CellPosition cell, GridExtent extent)
    {
        if (cell.Row < 0 || cell.Row >= extent.RowCount || cell.Column < 0 || cell.Column >= extent.ColumnCount)
            throw new ArgumentOutOfRangeException(nameof(cell), cell,
                $"Outside the grid ({extent.RowCount} rows × {extent.ColumnCount} columns).");
    }

    private void RequireFits(GridExtent extent)
    {
        foreach (var range in Ranges)
        {
            if (range.BottomRow >= extent.RowCount || range.RightColumn >= extent.ColumnCount)
                throw DropWasMissed(extent);
        }
        if (_focus.Row >= extent.RowCount || _focus.Column >= extent.ColumnCount
            || _anchor.Row >= extent.RowCount || _anchor.Column >= extent.ColumnCount)
        {
            throw DropWasMissed(extent);
        }
    }

    private static InvalidOperationException DropWasMissed(GridExtent extent) => new(
        $"The selection no longer fits the grid ({extent.RowCount} rows × {extent.ColumnCount} columns). " +
        "Positions point at something different once the order changes — the holder must assign " +
        "GridSelection.Empty when the Row Sequence Version or the visible-column set changes (ADR-0011).");

    private static CellPosition Step(CellPosition origin, GridDirection direction, GridExtent extent) => direction switch
    {
        GridDirection.Up => origin with { Row = Math.Max(0, origin.Row - 1) },
        GridDirection.Down => origin with { Row = Math.Min(extent.RowCount - 1, origin.Row + 1) },
        GridDirection.Left => origin with { Column = Math.Max(0, origin.Column - 1) },
        GridDirection.Right => origin with { Column = Math.Min(extent.ColumnCount - 1, origin.Column + 1) },
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null),
    };

    // Clamped in long space: the delta is a Viewport's worth of rows, but nothing here
    // may assume the sum stays inside int just because both halves do.
    private static CellPosition StepRows(CellPosition origin, int rowDelta, GridExtent extent)
        => origin with { Row = (int)Math.Clamp((long)origin.Row + rowDelta, 0, extent.RowCount - 1) };

    private static CellPosition EdgeOf(CellPosition origin, GridDirection direction, GridExtent extent) => direction switch
    {
        GridDirection.Up => origin with { Row = 0 },
        GridDirection.Down => origin with { Row = extent.RowCount - 1 },
        GridDirection.Left => origin with { Column = 0 },
        GridDirection.Right => origin with { Column = extent.ColumnCount - 1 },
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null),
    };

    /// <summary>Column-major ordinal counts down the column first (Enter); row-major
    /// counts across the row first (Tab). Ordinal 0 is the top-left cell and the last
    /// ordinal is the bottom-right under either order (ADR-0012).</summary>
    private static long OrdinalOf(CellPosition cell, SelectionRange range, CycleOrder order)
    {
        long row = cell.Row - range.TopRow;
        long column = cell.Column - range.LeftColumn;
        return order == CycleOrder.ColumnMajor ? column * range.RowCount + row : row * range.ColumnCount + column;
    }

    private static CellPosition CellAt(SelectionRange range, long ordinal, CycleOrder order)
        => order == CycleOrder.ColumnMajor
            ? new(range.TopRow + (int)(ordinal % range.RowCount), range.LeftColumn + (int)(ordinal / range.RowCount))
            : new(range.TopRow + (int)(ordinal / range.ColumnCount), range.LeftColumn + (int)(ordinal % range.ColumnCount));
}
