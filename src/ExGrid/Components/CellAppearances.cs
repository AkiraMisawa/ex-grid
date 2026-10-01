using ExGrid.Cells;
using ExGrid.Selection;

namespace ExGrid.Components;

/// <summary>
/// Resolves each painted row's <see cref="RowAppearance"/> once, outside the row's render
/// (ADR-0050, item 15; ADR-0063, "What the measurement chose"). A cell's lines come from both cells
/// of each edge — its top line is the edge it shares with the row above, and a thick line there
/// reaches into it — so a row is resolved from its own answers and those of the rows either side.
///
/// <para>The Consumer is asked once per row instance for the painted columns and one more on each
/// side, and asked again only when the row instance, the lookup, the columns or the painted columns
/// change: the identities ADR-0003/0006 make the change signal. A row resolved again keeps the
/// instance it had when it would paint the same, so a new instance repaints its own row, and its
/// neighbours only where a line they share has moved.</para>
/// </summary>
internal sealed class CellAppearances<TRow> where TRow : class
{
    private readonly Dictionary<int, Entry> _byPosition = [];
    private readonly Dictionary<TRow, Asked> _asked = new(ReferenceEqualityComparer.Instance);

    private CellAppearanceOf<TRow>? _lookup;
    private EdgeBorderOf? _edge;
    private IReadOnlyList<GridColumn<TRow>> _columns = [];
    private int _pinned;
    private ColumnRange? _scrollable;
    private int _context;
    private int _stamp;

    // The columns asked about: the Pinned Columns and the one after them, then the scrollable
    // columns on screen and one either side — every painted cell's neighbours.
    private int _pinnedEnd;
    private int _scrollFrom;
    private int _scrollTo;

    /// <summary>The grid's classes and stylesheet.</summary>
    public AppearanceStyles Styles { get; } = new();

    /// <summary>
    /// Starts one render of the rows: what is asked, of which columns. A change of any of it
    /// forgets every answer, since the answers or the cells they are for have changed; what each
    /// row painted is kept, so a row whose cells resolve the same keeps its instance.
    /// </summary>
    public void Begin(
        CellAppearanceOf<TRow> lookup, EdgeBorderOf? edge, IReadOnlyList<GridColumn<TRow>> columns,
        int pinned, ColumnRange? scrollable)
    {
        _stamp++;
        if (!ReferenceEquals(lookup, _lookup) || !ReferenceEquals(edge, _edge) || !ReferenceEquals(columns, _columns)
            || pinned != _pinned || scrollable != _scrollable)
        {
            _lookup = lookup;
            _edge = edge;
            _columns = columns;
            _pinned = Math.Min(pinned, columns.Count);
            _scrollable = scrollable;
            _context++;
            _pinnedEnd = Math.Min(_pinned + 1, columns.Count);
            (_scrollFrom, _scrollTo) = scrollable is { } s
                ? (Math.Max(s.Start - 1, 0), Math.Min(s.Start + s.Count + 1, columns.Count))
                : (0, 0);
            _asked.Clear();
        }

        // Only rows painted by the last render are kept: a fling replaces them all, and a slow
        // scroll keeps all but one.
        if (_byPosition.Count > 256)
            Forget(_byPosition, static entry => entry.Stamp);
        if (_asked.Count > 256)
            Forget(_asked, static asked => asked.Stamp);
    }

    /// <summary>The appearance of the row at absolute <paramref name="position"/>, from the rows
    /// above and below it in the Window (null where the Window holds none), or null when none of
    /// its painted cells has any.</summary>
    public RowAppearance? At(int position, TRow row, TRow? above, TRow? below)
    {
        if (_byPosition.TryGetValue(position, out var entry) && entry.Context == _context
            && ReferenceEquals(entry.Row, row) && ReferenceEquals(entry.Above, above) && ReferenceEquals(entry.Below, below))
        {
            entry.Stamp = _stamp;
            return entry.Appearance;
        }

        var resolved = Resolve(row, above, below);
        if (entry is null)
        {
            entry = new Entry();
            _byPosition[position] = entry;
        }
        else if (entry.Appearance is { } previous && resolved is not null && previous.SameAs(resolved))
        {
            resolved = previous;
        }

        entry.Context = _context;
        entry.Row = row;
        entry.Above = above;
        entry.Below = below;
        entry.Appearance = resolved;
        entry.Stamp = _stamp;
        return resolved;
    }

    private RowAppearance? Resolve(TRow row, TRow? above, TRow? below)
    {
        var own = Ask(row);
        var up = above is null ? null : Ask(above);
        var down = below is null ? null : Ask(below);

        var scrollStart = _scrollable?.Start ?? 0;
        var painted = _pinned + (_scrollable?.Count ?? 0);
        var classes = new string?[painted];
        var bold = new bool[painted];
        var any = false;
        for (var slot = 0; slot < painted; slot++)
        {
            var c = slot < _pinned ? slot : scrollStart + slot - _pinned;
            if (c >= _columns.Count || !_columns[c].PaintsValue)
                continue;

            var cell = At(own, c);
            var right = At(own, c + 1);
            var left = At(own, c - 1);
            var cellAbove = At(up, c);
            var cellBelow = At(down, c);

            var bottomLine = Choose(cell.Bottom, cellBelow.Top);
            var rightLine = Choose(cell.Right, right.Left);
            var topLine = Choose(cellAbove.Bottom, cell.Top);
            var leftLine = Choose(left.Right, cell.Left);

            // This cell holds the gridline of its bottom and right edges: a line there is its share,
            // and with no line a neighbour's Fill covers it, as that Fill covers its own edges.
            var bottom = !bottomLine.IsNone ? Share.Line(bottomLine)
                : cell.Fill is null && cellBelow.Fill is { } fillBelow ? Share.Cover(fillBelow) : Share.None;
            var rightShare = !rightLine.IsNone ? Share.Line(rightLine)
                : cell.Fill is null && right.Fill is { } fillRight ? Share.Cover(fillRight) : Share.None;
            var top = AppearanceStyles.ReachesPast(topLine.Style) ? Share.Line(topLine) : Share.None;
            var leftShare = AppearanceStyles.ReachesPast(leftLine.Style) ? Share.Line(leftLine) : Share.None;

            classes[slot] = Styles.ClassFor(cell, top, rightShare, bottom, leftShare);
            bold[slot] = cell.Bold;
            any |= classes[slot] is not null || cell.Bold;
        }

        return any ? new RowAppearance(_pinned, scrollStart, classes, bold, Styles) : null;
    }

    /// <summary>The line drawn on an edge: the one recorded, where one cell records it; the
    /// Consumer's answer where both record different lines; the upper or left one without an
    /// answer (<c>CONTEXT.md</c>, Border).</summary>
    private Border Choose(Border upperOrLeft, Border lowerOrRight)
    {
        if (upperOrLeft.IsNone)
            return lowerOrRight;
        if (lowerOrRight.IsNone || upperOrLeft == lowerOrRight)
            return upperOrLeft;
        return _edge?.Invoke(upperOrLeft, lowerOrRight) ?? upperOrLeft;
    }

    private CellAppearance[] Ask(TRow row)
    {
        if (_asked.TryGetValue(row, out var asked))
        {
            asked.Stamp = _stamp;
            return asked.Cells;
        }

        var cells = new CellAppearance[_pinnedEnd + Math.Max(0, _scrollTo - _scrollFrom)];
        for (var c = 0; c < _pinnedEnd; c++)
            cells[c] = AskOne(row, c);
        for (var c = _scrollFrom; c < _scrollTo; c++)
            cells[_pinnedEnd + c - _scrollFrom] = c < _pinnedEnd ? cells[c] : AskOne(row, c);
        _asked[row] = new Asked { Cells = cells, Stamp = _stamp };
        return cells;
    }

    // Asked of value cells only, as the per-cell kind is (ADR-0050, item 6).
    private CellAppearance AskOne(TRow row, int column)
        => _columns[column].PaintsValue ? _lookup!(row, _columns[column]) : default;

    private CellAppearance At(CellAppearance[]? cells, int column)
    {
        if (cells is null || column < 0 || column >= _columns.Count)
            return default;
        if (column < _pinnedEnd)
            return cells[column];
        if (column >= _scrollFrom && column < _scrollTo)
            return cells[_pinnedEnd + column - _scrollFrom];
        return default;
    }

    private void Forget<TKey, TValue>(Dictionary<TKey, TValue> entries, Func<TValue, int> stampOf) where TKey : notnull
    {
        List<TKey>? stale = null;
        foreach (var (key, value) in entries)
        {
            if (stampOf(value) < _stamp - 1)
                (stale ??= []).Add(key);
        }
        if (stale is not null)
        {
            foreach (var key in stale)
                entries.Remove(key);
        }
    }

    private sealed class Entry
    {
        public int Context;
        public int Stamp;
        public TRow? Row;
        public TRow? Above;
        public TRow? Below;
        public RowAppearance? Appearance;
    }

    private sealed class Asked
    {
        public required CellAppearance[] Cells;
        public int Stamp;
    }
}
