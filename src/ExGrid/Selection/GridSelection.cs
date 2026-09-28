namespace ExGrid.Selection;

/// <summary>
/// The selection: a list of rectangles in position space, one Focus — Excel's active cell,
/// which typing enters and the Name Box names — and the range that holds it
/// (ADR-0011 / 0012 / 0052). Immutable — every gesture is a pure transition returning a new
/// value, and each takes the current <see cref="GridExtent"/> because the model never holds
/// data-dependent bounds.
///
/// <para>Extending a range moves its <see cref="Extent"/>, the end opposite the Focus; the
/// Focus stays where it is (ADR-0052). The Extent is kept with the Focus's range and follows
/// from where the Focus stands in it: on each axis, the edge opposite the Focus's edge, or,
/// where the Focus is on neither edge — Enter or Tab walked it inside — the Focus's own row
/// or column, and an extension along that axis changes nothing, as in Excel.</para>
///
/// <para>Which range holds the Focus is <em>stored</em>, never re-derived from geometry:
/// ranges can overlap, and inferring membership from coordinates picks an arbitrary one of
/// them. The Focus is always inside the range it names (ADR-0052 withdrew ADR-0012's
/// detached state).</para>
///
/// <para>Dropping the selection is the holder's act: when the Row Sequence Version (or the
/// visible-column set) changes, the holder assigns <see cref="Empty"/> (ADR-0011). A
/// transition applied to a state that no longer fits the extent throws — that is a missed
/// drop, and positions would quietly point at different cells.</para>
///
/// <para>Mouse drag is not a distinct transition: the holder maps mousedown to
/// <see cref="Click"/> and mousemove to <see cref="ExtendTo"/>. Do not invent a third path
/// in the binding layer.</para>
/// </summary>
public sealed record GridSelection
{
    /// <summary>Nothing selected: no range, and no Focus or Extent. What a holder assigns
    /// when the Row Sequence Version or the visible-column set changes (ADR-0011).</summary>
    public static GridSelection Empty { get; } = new([], default, focusRangeIndex: 0);

    private readonly CellPosition _focus;

    /// <summary>Index of the range holding the Focus. Meaningless on Empty.</summary>
    private readonly int _focusRangeIndex;

    /// <summary>
    /// For each range, which range the user made it as — a number that rises with creation
    /// order. The fragments a take-out leaves share the number of the range they were cut
    /// from, which is what "the range made last" means after a take-out (ADR-0052, "What the
    /// third run settled"). Never exposed: the ranges themselves stay the public shape.
    /// </summary>
    private readonly int[] _origins;

    private GridSelection(
        IReadOnlyList<SelectionRange> ranges, CellPosition focus, int focusRangeIndex, int[]? origins = null)
    {
        if (ranges.Count > 0
            && (focusRangeIndex < 0 || focusRangeIndex >= ranges.Count || !ranges[focusRangeIndex].Contains(focus)))
        {
            throw new InvalidOperationException("Internal: the Focus range index must name a range containing the Focus (ADR-0052).");
        }
        origins ??= ranges.Count switch
        {
            0 => [],
            1 => [0],
            _ => throw new InvalidOperationException("Internal: several ranges need their origins (ADR-0052)."),
        };
        if (origins.Length != ranges.Count)
            throw new InvalidOperationException("Internal: every range has one origin (ADR-0052).");

        // Stored behind a read-only wrapper so no caller can cast Ranges back to the
        // array and mutate a rectangle in place, past the invariant check above (the
        // FilterOperators pattern).
        Ranges = ranges switch
        {
            SelectionRange[] array => Array.AsReadOnly(array),
            List<SelectionRange> list => list.AsReadOnly(),
            _ => ranges,
        };
        _focus = focus;
        _focusRangeIndex = focusRangeIndex;
        _origins = origins;
    }

    /// <summary>
    /// The rectangles in creation order — which is also the Enter/Tab cycling order
    /// (ADR-0012) and the one-overlay-per-range painting order (ADR-0008). A take-out puts a
    /// range's fragments where the range stood, bottom to top, which is the order Excel's
    /// <c>Selection.Address</c> lists them in (ADR-0052, "What the third run settled").
    /// </summary>
    public IReadOnlyList<SelectionRange> Ranges { get; }

    /// <summary>Whether nothing is selected. <see cref="Focus"/>, <see cref="Extent"/> and
    /// <see cref="FocusRange"/> throw then.</summary>
    public bool IsEmpty => Ranges.Count == 0;

    /// <summary>
    /// Excel's active cell (ADR-0052): the one cell typing enters, the Cell Editor opens on,
    /// the Name Box names and <c>aria-activedescendant</c> points at. It stays where it is
    /// while a range is extended — that end is the <see cref="Extent"/> — and Enter / Tab
    /// cycling moves it inside the Selection without changing the Selection. Always inside
    /// <see cref="FocusRange"/>.
    /// </summary>
    public CellPosition Focus => IsEmpty
        ? throw new InvalidOperationException("An empty selection has no Focus. Establish one with Click first (ADR-0012).")
        : _focus;

    /// <summary>
    /// The end of <see cref="FocusRange"/> that moves when it is extended — by Shift+arrow,
    /// Shift+click, Ctrl+Shift+arrow or a drag — and that the holder keeps in view while
    /// extending (ADR-0052). On each axis it is the edge opposite the Focus; where the Focus
    /// is on neither edge of that axis it is the Focus's own row or column, and an
    /// extension along that axis changes nothing.
    /// </summary>
    public CellPosition Extent => IsEmpty
        ? throw new InvalidOperationException("An empty selection has no Extent. Establish one with Click first (ADR-0052).")
        : ExtentOf(Ranges[_focusRangeIndex], _focus);

    /// <summary>The range holding the <see cref="Focus"/> — the one an extension, Ctrl+Space,
    /// Shift+Space and Ctrl+. act on (ADR-0052).</summary>
    public SelectionRange FocusRange => IsEmpty
        ? throw new InvalidOperationException("An empty selection has no Focus range. Establish one with Click first (ADR-0052).")
        : Ranges[_focusRangeIndex];

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

    /// <summary>Whether the cell lies in any range.</summary>
    public bool Contains(CellPosition cell)
    {
        foreach (var range in Ranges)
            if (range.Contains(cell))
                return true;
        return false;
    }

    /// <summary>Click: the Focus is the cell and the selection collapses to it (ADR-0012).</summary>
    public GridSelection Click(CellPosition cell, GridExtent extent)
    {
        if (IsDegenerate(extent))
            return Empty;
        RequireInside(cell, extent);
        return Collapse(cell);
    }

    /// <summary>
    /// Shift+click, and each move of a drag: the Focus stays, and the range holding it is
    /// redrawn from the Focus to the cell, which becomes its Extent (ADR-0052). The target is
    /// the absolute cell the user pointed at, so the range is redrawn even where Enter or Tab
    /// had walked the Focus inside it. The other ranges stand. From Empty it behaves as a
    /// plain click.
    /// </summary>
    public GridSelection ExtendTo(CellPosition cell, GridExtent extent)
    {
        if (IsDegenerate(extent))
            return Empty;
        RequireInside(cell, extent);
        if (IsEmpty)
            return Collapse(cell);
        RequireFits(extent);

        return new(ReplaceAt(Ranges, _focusRangeIndex, SelectionRange.FromCorners(_focus, cell)), _focus, _focusRangeIndex, _origins);
    }

    /// <summary>
    /// Ctrl+click (ADR-0012 / 0052). On an unselected cell: adds a new 1×1 range holding the
    /// Focus, the range made last. On a selected cell: takes it out — the cell is subtracted
    /// from every range containing it, each rectangle giving way, in place, to its fragments
    /// listed bottom to top as Excel lists them (<see cref="SelectionRange.Subtract"/>) — and
    /// the Focus goes to the first remaining cell, by rows, of the range made last, wherever
    /// Enter or Tab had moved it (ADR-0052, "What the third run settled"). The only selected
    /// cell cannot be taken out: the selection is returned unchanged.
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
            var origins = new int[_origins.Length + 1];
            _origins.CopyTo(origins, 0);
            origins[^1] = _origins[^1] + 1;
            return new(appended, cell, appended.Length - 1, origins);
        }

        var remaining = new List<SelectionRange>();
        var cameFrom = new List<int>();
        for (var i = 0; i < Ranges.Count; i++)
        {
            foreach (var piece in Ranges[i].Subtract(cell))
            {
                remaining.Add(piece);
                cameFrom.Add(_origins[i]);
            }
        }
        if (remaining.Count == 0)
            return this;

        // The range made last is the one with the highest origin still holding a cell. When
        // the take-out emptied it — a 1×1 range made last, clicked again — the latest range
        // still standing takes its place (not observed in Excel; the nearest reading).
        var last = cameFrom[^1];
        var focusIndex = -1;
        for (var j = 0; j < remaining.Count; j++)
        {
            if (cameFrom[j] != last)
                continue;
            var first = new CellPosition(remaining[j].TopRow, remaining[j].LeftColumn);
            if (focusIndex < 0 || IsBefore(first, remaining[focusIndex]))
                focusIndex = j;
        }
        var focusRange = remaining[focusIndex];
        return new(remaining, new(focusRange.TopRow, focusRange.LeftColumn), focusIndex, [.. cameFrom]);

        // By rows: the earlier row, then the earlier column.
        static bool IsBefore(CellPosition cell, SelectionRange than)
            => cell.Row < than.TopRow || (cell.Row == than.TopRow && cell.Column < than.LeftColumn);
    }

    /// <summary>Arrow: collapses the selection to one cell and moves from the Focus, clamped
    /// at the grid edge (ADR-0012). A no-op on Empty — there is no Focus to start from.</summary>
    public GridSelection Move(GridDirection direction, GridExtent extent)
        => MoveFocusTo(extent, focus => Step(focus, direction, extent));

    /// <summary>Shift+arrow: the Extent moves one step and the Focus stays, so the range holding
    /// the Focus grows or shrinks — shrinking back through the Focus flips it (ADR-0052). Where
    /// the Focus is on neither edge of the axis, the key changes nothing. The other axis keeps
    /// its span, so whole columns stay whole under ← / → and whole rows under ↑ / ↓
    /// (ADR-0012, 2026-09-25).</summary>
    public GridSelection Extend(GridDirection direction, GridExtent extent)
        => ExtendAlong(extent, IsHorizontal(direction), from => Step(from, direction, extent));

    /// <summary>Ctrl+arrow: collapses and jumps to the last / first row or column — not
    /// Excel's block edge, which the grid cannot find without the data (ADR-0011).</summary>
    public GridSelection MoveToEdge(GridDirection direction, GridExtent extent)
        => MoveFocusTo(extent, focus => EdgeOf(focus, direction, extent));

    /// <summary>Shift+Ctrl+arrow: the Extent runs to the edge (ADR-0012 / 0052). From the
    /// first row, Ctrl+Shift+Down is effectively a whole-column selection.</summary>
    public GridSelection ExtendToEdge(GridDirection direction, GridExtent extent)
        => ExtendAlong(extent, IsHorizontal(direction), from => EdgeOf(from, direction, extent));

    /// <summary>Ctrl+arrow with the Consumer's edge answer (ADR-0050, item 2): collapses and
    /// moves the Focus to the cell <paramref name="edge"/> names for the Focus and the
    /// direction — where the data ends, which only the Consumer can know. Null is
    /// <see cref="MoveToEdge(GridDirection, GridExtent)"/>: the grid's edge (ADR-0012). An
    /// answer off the Focus's line, behind it, or outside the grid is refused by name
    /// rather than followed.</summary>
    public GridSelection MoveToEdge(
        GridDirection direction, GridExtent extent, Func<CellPosition, GridDirection, CellPosition>? edge)
        => edge is null
            ? MoveToEdge(direction, extent)
            : MoveFocusTo(extent, focus => Answered(edge, focus, direction, extent));

    /// <summary>Ctrl+Shift+arrow with the Consumer's edge answer (ADR-0050, item 2): the
    /// Extent runs to the cell <paramref name="edge"/> names for the Extent, as
    /// <see cref="ExtendToEdge(GridDirection, GridExtent)"/> runs it to the grid's edge —
    /// the moving end is asked, so a second press goes on from where the first stopped
    /// (ADR-0052). Whole columns and whole rows stay whole the same way. Null is the grid's
    /// edge.</summary>
    public GridSelection ExtendToEdge(
        GridDirection direction, GridExtent extent, Func<CellPosition, GridDirection, CellPosition>? edge)
        => edge is null
            ? ExtendToEdge(direction, extent)
            : ExtendAlong(extent, IsHorizontal(direction), from => Answered(edge, from, direction, extent));

    /// <summary>The Consumer's edge answer, checked: on the asked cell's own row or column,
    /// not behind it, inside the grid. Anything else would move the selection somewhere the
    /// key never pointed, which is worse than refusing (the spine's first rule).</summary>
    private static CellPosition Answered(
        Func<CellPosition, GridDirection, CellPosition> edge, CellPosition from, GridDirection direction, GridExtent extent)
    {
        var answer = edge(from, direction);
        var inside = answer.Row >= 0 && answer.Row < extent.RowCount
            && answer.Column >= 0 && answer.Column < extent.ColumnCount;
        var onLine = direction switch
        {
            GridDirection.Up => answer.Column == from.Column && answer.Row <= from.Row,
            GridDirection.Down => answer.Column == from.Column && answer.Row >= from.Row,
            GridDirection.Left => answer.Row == from.Row && answer.Column <= from.Column,
            GridDirection.Right => answer.Row == from.Row && answer.Column >= from.Column,
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null),
        };
        if (!inside || !onLine)
        {
            throw new InvalidOperationException(
                $"The edge answer for {direction} from {from} was {answer}, which is " +
                (inside ? "not on that cell's line in that direction" : $"outside the grid ({extent.RowCount} rows × {extent.ColumnCount} columns)") +
                ". Ctrl+arrow moves along one line only; answer a cell on it, or the cell asked about itself (ADR-0050).");
        }
        return answer;
    }

    /// <summary>PageUp / PageDown (ADR-0012): collapse and move the Focus by one
    /// Viewport of rows. How many rows that is is view geometry the model never holds,
    /// so the caller passes the signed delta; the grid edge clamps it.</summary>
    public GridSelection MoveByViewport(int rowDelta, GridExtent extent)
        => MoveFocusTo(extent, focus => StepRows(focus, rowDelta, extent));

    /// <summary>Shift+PageUp / Shift+PageDown (ADR-0012 / 0052): the Extent moves by one
    /// Viewport of rows and the Focus stays.</summary>
    public GridSelection ExtendByViewport(int rowDelta, GridExtent extent)
        => ExtendAlong(extent, horizontal: false, from => StepRows(from, rowDelta, extent));

    /// <summary>
    /// Ctrl+A: every row after filtering across every visible column, as one rectangle —
    /// expressible without holding the data (ADR-0011). The Focus stays where it is
    /// (ADR-0052, case 10); from Empty it lands on (0, 0).
    /// </summary>
    public GridSelection SelectAll(GridExtent extent)
    {
        if (IsDegenerate(extent))
            return Empty;
        var all = new SelectionRange(0, 0, extent.RowCount, extent.ColumnCount);
        if (IsEmpty)
            return new([all], new(0, 0), 0);
        RequireFits(extent);
        return new([all], _focus, 0);
    }

    /// <summary>Ctrl+A under a pager (ADR-0015): every cell of the rows in context —
    /// the page — as one range. The Focus stays exactly as <see cref="SelectAll(GridExtent)"/>
    /// keeps it: a key that names a whole region needs no starting point and must not
    /// move the active cell (ADR-0012). From Empty it lands on the context's first cell —
    /// as it does when it stands outside the context (the selection came from another
    /// page), because a range must contain its own Focus.</summary>
    public GridSelection SelectAll(GridExtent extent, int firstRow, int rowCount)
    {
        if (IsDegenerate(extent))
            return Empty;
        var start = Math.Clamp(firstRow, 0, extent.RowCount - 1);
        var count = Math.Clamp(rowCount, 1, extent.RowCount - start);
        var context = new SelectionRange(start, 0, count, extent.ColumnCount);
        if (IsEmpty || !context.Contains(_focus))
            return new([context], new(start, 0), 0);
        RequireFits(extent);
        return new([context], _focus, 0);
    }

    /// <summary>Ctrl+Space: the range holding the Focus expands to every row, keeping its
    /// column span (ADR-0012 / 0052). The Focus stays. A no-op on Empty.</summary>
    public GridSelection SelectWholeColumns(GridExtent extent)
        => GrowFocusRange(extent, range => new(0, range.LeftColumn, extent.RowCount, range.ColumnCount));

    /// <summary>Shift+Space: the range holding the Focus expands to every visible column,
    /// keeping its row span (ADR-0012 / 0052). The Focus stays. A no-op on Empty.</summary>
    public GridSelection SelectWholeRows(GridExtent extent)
        => GrowFocusRange(extent, range => new(range.TopRow, 0, range.RowCount, extent.ColumnCount));

    /// <summary>
    /// Enter / Tab (ADR-0012): with a range selected, only the Focus moves — Enter
    /// column-major, Tab row-major, wrapping past the last cell to the next range in
    /// creation order and from the last range back to the first; <paramref name="backward"/>
    /// (Shift+) mirrors. With a single cell, Enter moves down and Tab moves right, the
    /// selection follows, and the grid edge clamps. The <see cref="Extent"/> follows the
    /// Focus to its new place in its range (ADR-0052).
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

        if (Ranges is [{ CellCount: 1 }])
        {
            var direction = order == CycleOrder.ColumnMajor
                ? (backward ? GridDirection.Up : GridDirection.Down)
                : (backward ? GridDirection.Left : GridDirection.Right);
            return Collapse(Step(_focus, direction, extent));
        }
        return StepInside(order, backward);
    }

    /// <summary>
    /// Ctrl+. (period): the Focus moves to the next corner of the range holding it,
    /// clockwise — top-left, top-right, bottom-right, bottom-left — and the Selection does not
    /// change (ADR-0052, case 8). A corner the range shares with another (a range one row or
    /// one column deep) is passed over, so every press moves. From a cell on an edge but not
    /// at a corner, the next corner clockwise along that edge; from inside the range, the
    /// top-left. A single cell does not move.
    /// </summary>
    public GridSelection MoveFocusToNextCorner(GridExtent extent)
    {
        if (IsDegenerate(extent))
            return Empty;
        if (IsEmpty)
            return Empty;
        RequireFits(extent);

        var range = Ranges[_focusRangeIndex];
        CellPosition[] corners =
        [
            new(range.TopRow, range.LeftColumn),
            new(range.TopRow, range.RightColumn),
            new(range.BottomRow, range.RightColumn),
            new(range.BottomRow, range.LeftColumn),
        ];
        var at = Array.IndexOf(corners, _focus);
        if (at < 0)
        {
            // Not at a corner: the corner that ends the edge the Focus is on, walking
            // clockwise; from inside the range, the first corner.
            var next = _focus.Row == range.TopRow ? corners[1]
                : _focus.Column == range.RightColumn ? corners[2]
                : _focus.Row == range.BottomRow ? corners[3]
                : corners[0];
            return new(Ranges, next, _focusRangeIndex, _origins);
        }
        for (var step = 1; step < corners.Length; step++)
        {
            var candidate = corners[(at + step) % corners.Length];
            if (candidate != _focus)
                return new(Ranges, candidate, _focusRangeIndex, _origins);
        }
        return this;
    }

    /// <summary>Shift+Backspace: the Selection collapses to the Focus (ADR-0052, case 7). A
    /// no-op on Empty.</summary>
    public GridSelection CollapseToFocus(GridExtent extent)
    {
        if (IsDegenerate(extent))
            return Empty;
        if (IsEmpty)
            return Empty;
        RequireFits(extent);
        return Collapse(_focus);
    }

    /// <summary>
    /// Moves the Focus onto a cell inside the selection and leaves the ranges where they are —
    /// what a Find step does when it searches the selection (ADR-0055): the next step searches
    /// the same ranges. The range holding the cell becomes the <see cref="FocusRange"/>; with the
    /// cell in several, the latest-created one. The <see cref="Extent"/> follows the Focus, as it
    /// does after Enter / Tab cycling (ADR-0052). Throws when the cell is selected nowhere:
    /// moving the Focus out of the selection is <see cref="Click"/>.
    /// </summary>
    public GridSelection FocusOn(CellPosition cell)
    {
        for (var i = Ranges.Count - 1; i >= 0; i--)
        {
            if (Ranges[i].Contains(cell))
                return new(Ranges, cell, i, _origins);
        }
        throw new ArgumentOutOfRangeException(nameof(cell), cell,
            "The cell is not in the selection; moving the Focus out of it is Click (ADR-0055).");
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
        return _focus == other._focus && _focusRangeIndex == other._focusRangeIndex
            && _origins.AsSpan().SequenceEqual(other._origins);
    }

    /// <summary>Consistent with <see cref="Equals(GridSelection)"/>: the ranges, and the
    /// Focus, its range and which range each was made as when not empty.</summary>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var range in Ranges)
            hash.Add(range);
        if (!IsEmpty)
        {
            hash.Add(_focus);
            hash.Add(_focusRangeIndex);
            foreach (var origin in _origins)
                hash.Add(origin);
        }
        return hash.ToHashCode();
    }

    /// <summary>
    /// The Extent of <paramref name="range"/> for a Focus inside it (ADR-0052): on each axis
    /// the edge opposite the Focus's edge, the Focus's own coordinate where the range is one
    /// cell deep, and also where the Focus is on neither edge — the axis on which an
    /// extension changes nothing.
    /// </summary>
    private static CellPosition ExtentOf(SelectionRange range, CellPosition focus) => new(
        focus.Row == range.TopRow ? range.BottomRow : focus.Row == range.BottomRow ? range.TopRow : focus.Row,
        focus.Column == range.LeftColumn ? range.RightColumn : focus.Column == range.RightColumn ? range.LeftColumn : focus.Column);

    private GridSelection MoveFocusTo(GridExtent extent, Func<CellPosition, CellPosition> destination)
    {
        if (IsDegenerate(extent))
            return Empty;
        if (IsEmpty)
            return Empty;
        RequireFits(extent);
        return Collapse(destination(_focus));
    }

    /// <summary>
    /// A keyboard extension along one axis (ADR-0052): the Extent moves to
    /// <paramref name="destination"/> of itself, and the range holding the Focus spans from
    /// the Focus to it on that axis, keeping its span on the other — which is what keeps a
    /// whole-column range whole under ← / → (ADR-0012, 2026-09-25). Where the Focus is on
    /// neither edge of the axis, the key changes nothing (case 3).
    /// </summary>
    private GridSelection ExtendAlong(
        GridExtent extent, bool horizontal, Func<CellPosition, CellPosition> destination)
    {
        if (IsDegenerate(extent))
            return Empty;
        if (IsEmpty)
            return Empty;
        RequireFits(extent);

        var range = Ranges[_focusRangeIndex];
        var interior = horizontal
            ? _focus.Column > range.LeftColumn && _focus.Column < range.RightColumn
            : _focus.Row > range.TopRow && _focus.Row < range.BottomRow;
        if (interior)
            return this;

        var moved = destination(ExtentOf(range, _focus));
        var redrawn = horizontal
            ? new SelectionRange(range.TopRow, Math.Min(_focus.Column, moved.Column), range.RowCount, Math.Abs(moved.Column - _focus.Column) + 1)
            : new SelectionRange(Math.Min(_focus.Row, moved.Row), range.LeftColumn, Math.Abs(moved.Row - _focus.Row) + 1, range.ColumnCount);
        return new(ReplaceAt(Ranges, _focusRangeIndex, redrawn), _focus, _focusRangeIndex, _origins);
    }

    /// <summary>
    /// Shift+click on a column header (ADR-0012, 2026-09-25; ADR-0052): whole columns from the
    /// Focus's column to <paramref name="column"/>, which is where the Extent goes. The Focus
    /// stays, and its range is replaced; the other ranges stand. From Empty, the clicked
    /// column alone, with the Focus on <paramref name="focusRowIfEmpty"/> — the holder passes
    /// its first visible row, so the Viewport does not move (KB-9).
    /// </summary>
    public GridSelection ExtendToColumn(int column, GridExtent extent, int focusRowIfEmpty = 0)
    {
        if (IsDegenerate(extent))
            return Empty;
        if (column < 0 || column >= extent.ColumnCount)
            throw new ArgumentOutOfRangeException(nameof(column), column,
                $"Outside the grid ({extent.ColumnCount} columns).");
        if (IsEmpty)
            return SelectColumn(column, extent, focusRowIfEmpty);
        RequireFits(extent);

        var left = Math.Min(_focus.Column, column);
        var whole = new SelectionRange(0, left, extent.RowCount, Math.Abs(column - _focus.Column) + 1);
        return new(ReplaceAt(Ranges, _focusRangeIndex, whole), _focus, _focusRangeIndex, _origins);
    }

    /// <summary>
    /// A Consumer's placement (ADR-0050, item 4): <paramref name="range"/> becomes the one
    /// range, with the Focus on <paramref name="focus"/> — what a click leaves, only over a
    /// range the Consumer named. Every other range goes. A range reaching outside the grid,
    /// or a Focus outside the range, is refused by name rather than clamped: the Consumer
    /// named cells that are not there.
    /// </summary>
    public GridSelection Place(SelectionRange range, CellPosition focus, GridExtent extent)
    {
        if (IsDegenerate(extent))
            return Empty;
        if (range.TopRow < 0 || range.LeftColumn < 0
            || range.BottomRow >= extent.RowCount || range.RightColumn >= extent.ColumnCount)
        {
            throw new ArgumentOutOfRangeException(nameof(range), range,
                $"Outside the grid ({extent.RowCount} rows × {extent.ColumnCount} columns) (ADR-0050).");
        }
        if (!range.Contains(focus))
        {
            throw new ArgumentOutOfRangeException(nameof(focus), focus,
                "The Focus is placed inside the range it belongs to (ADR-0050/0052).");
        }
        return new([range], focus, 0);
    }

    /// <summary>
    /// A plain click on a column header that a Consumer declared selects (ADR-0050, item
    /// 1): the whole column, as one range, with the Focus on <paramref name="focusRow"/> of
    /// it — the holder passes its first visible row, so the Viewport does not move for a
    /// click on the header (KB-9's rule, as Shift+click already has it). Every other range
    /// goes, as a plain click on a cell collapses.
    /// </summary>
    public GridSelection SelectColumn(int column, GridExtent extent, int focusRow)
    {
        if (IsDegenerate(extent))
            return Empty;
        if (column < 0 || column >= extent.ColumnCount)
            throw new ArgumentOutOfRangeException(nameof(column), column,
                $"Outside the grid ({extent.ColumnCount} columns).");
        var focus = new CellPosition(Math.Clamp(focusRow, 0, extent.RowCount - 1), column);
        return new([new SelectionRange(0, column, extent.RowCount, 1)], focus, 0);
    }

    /// <summary>
    /// A plain click on a Row Heading (ADR-0050, item 1): the whole row, every visible
    /// column, as one range, with the Focus on <paramref name="focusColumn"/> of it — the
    /// holder passes its first visible column. The Row Headings stand outside the column
    /// index space, so nothing here names them.
    /// </summary>
    public GridSelection SelectRow(int row, GridExtent extent, int focusColumn)
    {
        if (IsDegenerate(extent))
            return Empty;
        if (row < 0 || row >= extent.RowCount)
            throw new ArgumentOutOfRangeException(nameof(row), row,
                $"Outside the grid ({extent.RowCount} rows).");
        var focus = new CellPosition(row, Math.Clamp(focusColumn, 0, extent.ColumnCount - 1));
        return new([new SelectionRange(row, 0, 1, extent.ColumnCount)], focus, 0);
    }

    /// <summary>
    /// Shift+click on a Row Heading (ADR-0050 / 0052): whole rows from the Focus's row to
    /// <paramref name="row"/> — <see cref="ExtendToColumn"/> on the other axis. The Focus
    /// stays, and its range is replaced. From Empty, the clicked row alone, with the Focus on
    /// <paramref name="focusColumnIfEmpty"/>.
    /// </summary>
    public GridSelection ExtendToRow(int row, GridExtent extent, int focusColumnIfEmpty = 0)
    {
        if (IsDegenerate(extent))
            return Empty;
        if (row < 0 || row >= extent.RowCount)
            throw new ArgumentOutOfRangeException(nameof(row), row,
                $"Outside the grid ({extent.RowCount} rows).");
        if (IsEmpty)
            return SelectRow(row, extent, focusColumnIfEmpty);
        RequireFits(extent);

        var top = Math.Min(_focus.Row, row);
        var whole = new SelectionRange(top, 0, Math.Abs(row - _focus.Row) + 1, extent.ColumnCount);
        return new(ReplaceAt(Ranges, _focusRangeIndex, whole), _focus, _focusRangeIndex, _origins);
    }

    /// <summary>
    /// The columns covered by a range that spans every row, ascending, each once — what
    /// resizing several whole columns at once acts on (ADR-0016, 2026-09-25). A range
    /// short of any row selects cells, not columns, and contributes nothing.
    /// </summary>
    public IReadOnlyList<int> WholeColumns(GridExtent extent)
    {
        if (IsEmpty || IsDegenerate(extent))
            return [];
        var columns = new SortedSet<int>();
        foreach (var range in Ranges)
        {
            if (!range.SpansEveryRow(extent))
                continue;
            for (var c = range.LeftColumn; c <= range.RightColumn; c++)
                columns.Add(c);
        }
        return [.. columns];
    }

    private static bool IsHorizontal(GridDirection direction)
        => direction is GridDirection.Left or GridDirection.Right;

    private GridSelection GrowFocusRange(GridExtent extent, Func<SelectionRange, SelectionRange> expand)
    {
        if (IsDegenerate(extent))
            return Empty;
        if (IsEmpty)
            return Empty;
        RequireFits(extent);
        return new(ReplaceAt(Ranges, _focusRangeIndex, expand(Ranges[_focusRangeIndex])), _focus, _focusRangeIndex, _origins);
    }

    /// <summary>One step of Enter / Tab inside the Selection: the next cell of the Focus's
    /// range in <paramref name="order"/>, or on into the next range in creation order,
    /// wrapping (ADR-0012). The selection is known not to be empty.</summary>
    private GridSelection StepInside(CycleOrder order, bool backward)
    {
        var range = Ranges[_focusRangeIndex];
        var next = OrdinalOf(_focus, range, order) + (backward ? -1 : 1);
        if (next >= range.CellCount)
        {
            var following = (_focusRangeIndex + 1) % Ranges.Count;
            return new(Ranges, CellAt(Ranges[following], 0, order), following, _origins);
        }
        if (next < 0)
        {
            var preceding = (_focusRangeIndex - 1 + Ranges.Count) % Ranges.Count;
            var entered = Ranges[preceding];
            return new(Ranges, CellAt(entered, entered.CellCount - 1, order), preceding, _origins);
        }
        return new(Ranges, CellAt(range, next, order), _focusRangeIndex, _origins);
    }

    private static GridSelection Collapse(CellPosition cell)
        => new([new SelectionRange(cell.Row, cell.Column, 1, 1)], cell, 0);

    private static SelectionRange[] ReplaceAt(IReadOnlyList<SelectionRange> ranges, int index, SelectionRange replacement)
    {
        var copy = new SelectionRange[ranges.Count];
        for (var i = 0; i < ranges.Count; i++)
            copy[i] = ranges[i];
        copy[index] = replacement;
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
        if (_focus.Row >= extent.RowCount || _focus.Column >= extent.ColumnCount)
            throw DropWasMissed(extent);
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
