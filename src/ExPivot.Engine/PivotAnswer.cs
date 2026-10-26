namespace ExPivot.Engine;

/// <summary>
/// What a Pivot Source answers a <see cref="PivotQuery"/> with: the Leaf Aggregates (ADR-0066), or
/// a refusal. An answer carries its Source Version; each row and column field's Items; and the
/// leaves — one for every combination of the row and column fields' Items that has records once
/// the Hidden Items are left out — each with one Item per row field and per column field, its
/// record count, and the parts asked for of every field in Values. ExPivot computes every cell,
/// subtotal and grand total from the leaves, without asking again (<see cref="PivotReportSource"/>).
///
/// <para>The leaves are held column by column — an array per level and per part — never as an
/// object per leaf, since an answer may hold hundreds of thousands of them. The order of the
/// leaves and of a field's Items is the source's own; the report orders Items itself. Immutable
/// and serialisable (<see cref="PivotJson"/>).</para>
///
/// <para>To a question carrying <see cref="PivotQuery.Levels"/>, a source that answers Level
/// Aggregates may answer, when the leaves would pass the cap, with the cells of the groups shown
/// instead (<see cref="IsLevels"/>, ADR-0370 section 3): each a shown row group crossed with a shown
/// column group that some record reaches, named by its Items down to its depth — an axis's
/// <see cref="PivotAnswerAxis.ItemAt"/> is −1 below it — with its records and its parts, and, beside
/// a sum that is a <c>double</c>, the sum of its numbers' magnitudes. What is said here of a leaf is
/// then said of a cell. A sibling set ordered by a value and passing the question's window limit is
/// answered as a window (<see cref="Windows"/>): its siblings from where the window starts, a margin
/// on either side, and rest cells merging the siblings beyond each margin, one a shown column
/// group.</para>
/// </summary>
public sealed class PivotAnswer
{
    private readonly string? _sourceVersion;
    private readonly PivotAnswerAxis[] _rows;
    private readonly PivotAnswerAxis[] _columns;
    private readonly PivotAnswerValues[] _values;
    private readonly PivotAnswerWindow[] _windows = [];
    private readonly int[]? _rests;

    internal PivotAnswer(
        string sourceVersion,
        PivotAnswerAxis[] rows,
        PivotAnswerAxis[] columns,
        int leafCount,
        long[] records,
        PivotAnswerValues[] values,
        bool levels = false,
        PivotAnswerWindow[]? windows = null,
        int[]? rests = null)
    {
        if (levels)
            CheckLevels(rows, columns, leafCount, values, windows ?? [], rests);
        else if (windows is { Length: > 0 })
            throw new FormatException("An answer of leaves has no window (ADR-0370 section 3).");
        IsLevels = levels;
        _windows = windows is { Length: > 0 } ? windows : [];
        _rests = windows is { Length: > 0 } ? rests : null;
        _sourceVersion = sourceVersion;
        _rows = rows;
        _columns = columns;
        LeafCount = leafCount;
        Records = records;
        _values = values;
    }

    private PivotAnswer(PivotSourceRefusal refusal)
    {
        Refusal = refusal;
        _rows = [];
        _columns = [];
        _values = [];
        Records = [];
    }

    /// <summary>The source's refusal to answer (ADR-0066): too many leaves, an unknown field, or
    /// an Aggregation it does not offer.</summary>
    public static PivotAnswer Refused(PivotSourceRefusal refusal)
    {
        ArgumentNullException.ThrowIfNull(refusal);
        return new PivotAnswer(refusal);
    }

    /// <summary>Why the source did not answer, or null when it did.</summary>
    public PivotSourceRefusal? Refusal { get; }

    /// <summary>Whether the source refused.</summary>
    public bool IsRefused => Refusal is not null;

    /// <summary>Whether the answer holds the cells of the groups shown — Level Aggregates — rather
    /// than the leaves (ADR-0370 section 3).</summary>
    public bool IsLevels { get; }

    /// <summary>Which state of the data the answer came from; a field's Items and a cell's
    /// records are asked for under it. Refused on a refusal, which came from no data.</summary>
    public string SourceVersion => _sourceVersion ?? throw RefusedError();

    /// <summary>How many leaves the answer holds; none for a refusal.</summary>
    public int LeafCount { get; }

    /// <summary>The row fields, outermost first, each with its Items and each leaf's Item.</summary>
    public IReadOnlyList<PivotAnswerAxis> Rows => IsRefused ? throw RefusedError() : _rows;

    /// <summary>The column fields, outermost first, each with its Items and each leaf's Item.</summary>
    public IReadOnlyList<PivotAnswerAxis> Columns => IsRefused ? throw RefusedError() : _columns;

    /// <summary>The fields in Values, in the question's order, each with its parts at every leaf.</summary>
    public IReadOnlyList<PivotAnswerValues> Values => IsRefused ? throw RefusedError() : _values;

    internal long[] Records { get; }

    internal PivotAnswerAxis[] RowAxes => _rows;

    internal PivotAnswerAxis[] ColumnAxes => _columns;

    internal PivotAnswerValues[] ValueColumns => _values;

    /// <summary>How many records a leaf holds, whatever their values.</summary>
    public long RecordsAt(int leaf) => Records[PivotAnswerAxis.Check(leaf, LeafCount)];

    /// <summary>The sibling sets answered as windows (ADR-0370 section 3); none in an answer of
    /// leaves.</summary>
    public IReadOnlyList<PivotAnswerWindow> Windows => IsRefused ? throw RefusedError() : _windows;

    internal PivotAnswerWindow[] WindowArray => _windows;

    /// <summary>Each cell's rest code: −1 for a cell of a group, 2w for the before side of window
    /// w, 2w + 1 for its after side; null where the answer has no window.</summary>
    internal int[]? Rests => _rests;

    /// <summary>The window and side a rest cell merges, or null for a cell of a group.</summary>
    public PivotAnswerRest? RestAt(int cell)
    {
        PivotAnswerAxis.Check(cell, LeafCount);
        if (_rests is null || _rests[cell] < 0)
            return null;
        return new PivotAnswerRest(_rests[cell] / 2, _rests[cell] % 2 == 0 ? PivotRestSide.Before : PivotRestSide.After);
    }

    /// <summary>
    /// Why this answer does not answer <paramref name="query"/>, or null when it does: the same row
    /// and column fields in the same order, the same fields in Values with at least the parts asked
    /// for, and no more leaves than the cap. A source that answered another question is refused by
    /// name rather than laid out (the spine's first principle).
    /// </summary>
    internal string? Mismatch(PivotQuery query)
    {
        if (IsRefused)
            return "the source refused: " + Refusal!.Message;
        if (IsLevels && query.Levels is null)
            return "it holds Level Aggregates, and the question asks for the leaves";
        if (!_rows.Select(a => a.Field).SequenceEqual(query.Rows.Select(f => f.Field), StringComparer.Ordinal))
            return $"its row fields are [{string.Join(", ", _rows.Select(a => a.Field))}], and the question's [{string.Join(", ", query.Rows.Select(f => f.Field))}]";
        if (!_columns.Select(a => a.Field).SequenceEqual(query.Columns.Select(f => f.Field), StringComparer.Ordinal))
            return $"its column fields are [{string.Join(", ", _columns.Select(a => a.Field))}], and the question's [{string.Join(", ", query.Columns.Select(f => f.Field))}]";
        if (!_values.Select(v => v.Field).SequenceEqual(query.Values.Select(v => v.Field), StringComparer.Ordinal))
            return $"its fields in Values are [{string.Join(", ", _values.Select(v => v.Field))}], and the question's [{string.Join(", ", query.Values.Select(v => v.Field))}]";
        for (var v = 0; v < _values.Length; v++)
        {
            var asked = query.Values[v].Parts;
            if ((_values[v].Parts & asked) != asked)
                return $"it carries the parts {_values[v].Parts} of '{_values[v].Field}', and the question asks for {asked}";
        }
        if (LeafCount > query.MaxLeaves)
            return IsLevels
                ? $"it holds {LeafCount} cells, and the question allows {query.MaxLeaves}"
                : $"it holds {LeafCount} leaves, and the question allows {query.MaxLeaves}";
        return IsLevels ? PivotLevelAnswers.Mismatch(query.Levels!, this) : null;
    }

    // A level answer's shape (ADR-0370 section 3): each cell named down to its depth on each axis and
    // by nothing below, a double sum carried with its magnitudes, and each rest cell named by its
    // window's parent.
    private static void CheckLevels(PivotAnswerAxis[] rows, PivotAnswerAxis[] columns, int cells, PivotAnswerValues[] values, PivotAnswerWindow[] windows, int[]? rests)
    {
        for (var cell = 0; cell < cells; cell++)
        {
            CheckDepth(rows, cell);
            CheckDepth(columns, cell);
        }
        CheckWindowing(rows, cells, windows, rests);
        foreach (var value in values)
        {
            var parts = value.Columns;
            if (parts.Sums is not { } sums)
                continue;
            var absolutes = parts.Absolutes;
            for (var cell = 0; cell < cells; cell++)
            {
                if (sums[cell].Inexact && !parts.Counts[cell].NonFinite && (absolutes is null || double.IsNaN(absolutes[cell])))
                    throw new FormatException($"Cell {cell}'s sum of '{value.Field}' is a double, and carries no sum of its magnitudes (ADR-0370 section 3).");
            }
        }
    }

    private static void CheckWindowing(PivotAnswerAxis[] rows, int cells, PivotAnswerWindow[] windows, int[]? rests)
    {
        if (windows.Length == 0)
        {
            if (rests is { Length: > 0 })
                throw new FormatException("The answer has rest cells and no window (ADR-0370 section 3).");
            return;
        }
        if (rests is null || rests.Length != cells)
            throw new FormatException($"The answer says whether {rests?.Length ?? 0} cells are rests, and has {cells}.");
        for (var w = 0; w < windows.Length; w++)
        {
            var window = windows[w];
            if (window.Parent.Count >= rows.Length)
                throw new FormatException($"Window {w}'s parent names {window.Parent.Count} Items, and the answer has {rows.Length} row fields; its siblings are a row field's Items (ADR-0370 section 3).");
            for (var level = 0; level < window.Parent.Count; level++)
            {
                if ((uint)window.Parent[level] >= (uint)rows[level].ItemArray.Length)
                    throw new FormatException($"Window {w}'s parent names Item {window.Parent[level]} of '{rows[level].Field}', which has {rows[level].ItemArray.Length}.");
            }
            if (window.Before < 0)
                throw new FormatException($"Window {w} has {window.Before} siblings before it; a count is a whole number, not below 0.");
            if (window.After < 0)
                throw new FormatException($"Window {w} has {window.After} siblings after it; a count is a whole number, not below 0.");
        }
        for (var cell = 0; cell < cells; cell++)
        {
            var code = rests[cell];
            if (code == -1)
                continue;
            if (code < 0 || code >= 2 * windows.Length)
                throw new FormatException($"Cell {cell} is a rest of window {code / 2}, and the answer has {windows.Length}.");
            var window = windows[code / 2];
            for (var level = 0; level < rows.Length; level++)
            {
                var named = level < window.Parent.Count ? window.Parent[level] : -1;
                if (rows[level].ItemOfLeaf[cell] != named)
                    throw new FormatException($"Cell {cell} is a rest of window {code / 2}, and is not named by its parent's Items alone (ADR-0370 section 3).");
            }
        }
    }

    private static void CheckDepth(PivotAnswerAxis[] axes, int cell)
    {
        var below = false;
        foreach (var axis in axes)
        {
            var item = axis.ItemOfLeaf[cell];
            if (item < 0)
                below = true;
            else if (below)
                throw new FormatException($"Cell {cell} names an Item of '{axis.Field}' below a field it names none of; a cell is named down to its depth (ADR-0370 section 3).");
        }
    }

    private InvalidOperationException RefusedError()
        => new($"The source refused to answer: {Refusal!.Message}");
}

/// <summary>
/// A sibling set answered as a window (ADR-0370 section 3): its parent row group, and how many of its
/// siblings come before the first one shown and after the last. Each count, when above 0, takes in a
/// margin — the nearest sibling on that side, answered as any shown group is and not shown — and the
/// siblings beyond it, merged into the window's rest cells on that side.
/// </summary>
public sealed class PivotAnswerWindow
{
    internal PivotAnswerWindow(int[] parent, int before, int after)
    {
        Parent = parent;
        Before = before;
        After = after;
    }

    /// <summary>The parent's Item at each row field above the set, as an index among that axis's
    /// Items: none for the grand total.</summary>
    public IReadOnlyList<int> Parent { get; }

    /// <summary>The siblings before the first one shown, its margin among them.</summary>
    public int Before { get; }

    /// <summary>The siblings after the last one shown, its margin among them.</summary>
    public int After { get; }
}

/// <summary>Which side of a window a rest cell merges (ADR-0370 section 3).</summary>
public enum PivotRestSide
{
    /// <summary>The siblings before the window's margin before it.</summary>
    Before,

    /// <summary>The siblings after the window's margin after it.</summary>
    After,
}

/// <summary>A rest cell's window, by its place among <see cref="PivotAnswer.Windows"/>, and its
/// side (ADR-0370 section 3).</summary>
public sealed class PivotAnswerRest
{
    internal PivotAnswerRest(int window, PivotRestSide side)
    {
        Window = window;
        Side = side;
    }

    /// <summary>The window, by its place.</summary>
    public int Window { get; }

    /// <summary>The side whose siblings beyond the margin it merges.</summary>
    public PivotRestSide Side { get; }
}

/// <summary>One row or column field of an answer (ADR-0066): its Items and each leaf's Item.</summary>
public sealed class PivotAnswerAxis
{
    private readonly PivotItemKey[] _items;

    internal PivotAnswerAxis(string field, PivotItemKey[] items, int[] itemOfLeaf, int leafCount, bool levels = false)
        : this(field, items, itemOfLeaf)
    {
        for (var leaf = 0; leaf < leafCount; leaf++)
        {
            if ((uint)itemOfLeaf[leaf] >= (uint)items.Length && !(levels && itemOfLeaf[leaf] == -1))
                throw new FormatException($"{(levels ? "Cell" : "Leaf")} {leaf} names Item {itemOfLeaf[leaf]} of '{field}', which has {items.Length}.");
        }
    }

    private PivotAnswerAxis(string field, PivotItemKey[] items, int[] itemOfLeaf)
    {
        Field = field;
        _items = items;
        ItemOfLeaf = itemOfLeaf;
    }

    /// <summary>An axis the bundled source assembled, whose every leaf names one of
    /// <paramref name="items"/> by construction: not walked again to check it, which a source's
    /// answer read from elsewhere is.</summary>
    internal static PivotAnswerAxis Made(string field, PivotItemKey[] items, int[] itemOfLeaf) => new(field, items, itemOfLeaf);

    /// <summary>The Pivot Field's name.</summary>
    public string Field { get; }

    /// <summary>The field's Items among the leaves. A text Item's key is its first spelling, which
    /// labels it (ADR-0060).</summary>
    public IReadOnlyList<PivotItemKey> Items => _items;

    internal PivotItemKey[] ItemArray => _items;

    internal int[] ItemOfLeaf { get; }

    /// <summary>The index in <see cref="Items"/> of a leaf's Item; in a level answer, −1 for a cell
    /// above this field, which stands for every Item of it.</summary>
    public int ItemAt(int leaf) => ItemOfLeaf[leaf];

    internal static int Check(int leaf, int leafCount)
        => (uint)leaf < (uint)leafCount ? leaf : throw new ArgumentOutOfRangeException(nameof(leaf), leaf, $"The answer has {leafCount} leaves.");
}

/// <summary>
/// One field in Values of an answer (ADR-0066): the parts it carries, at every leaf. The counts
/// are always there; a part that was not asked for is refused by name rather than read as zero.
/// </summary>
public sealed class PivotAnswerValues
{
    private readonly int _leafCount;

    internal PivotAnswerValues(string field, PartColumns columns, int leafCount)
    {
        Field = field;
        Columns = columns;
        _leafCount = leafCount;
    }

    /// <summary>The Pivot Field's name.</summary>
    public string Field { get; }

    /// <summary>The parts it carries besides the counts.</summary>
    public PivotParts Parts => Columns.Parts;

    internal PartColumns Columns { get; }

    /// <summary>The values at a leaf that are not Blank — Excel's <c>COUNTA</c>.</summary>
    public long ValuesAt(int leaf) => Columns.Counts[At(leaf)].Values;

    /// <summary>The numbers at a leaf — Excel's <c>COUNT</c>, non-finite ones included.</summary>
    public long NumbersAt(int leaf) => Columns.Counts[At(leaf)].Numbers;

    /// <summary>Whether a non-finite number was among a leaf's values: its every numeric
    /// Aggregation, and every total over it, is <c>#NUM!</c>.</summary>
    public bool NonFiniteAt(int leaf) => Columns.Counts[At(leaf)].NonFinite;

    /// <summary>The sum of a leaf's numbers.</summary>
    public PivotNumber SumAt(int leaf)
    {
        var sum = (Columns.Sums ?? throw NotCarried(PivotParts.Sum))[At(leaf)];
        return sum.Inexact ? PivotNumber.Double(sum.Double + sum.Compensation) : PivotNumber.Exact(sum.Exact);
    }

    /// <summary>The smallest of a leaf's numbers.</summary>
    public PivotNumber MinAt(int leaf)
    {
        var extremes = (Columns.Extremes ?? throw NotCarried(PivotParts.Extremes))[At(leaf)];
        return extremes.Inexact ? PivotNumber.Double(extremes.Min) : PivotNumber.Exact(extremes.ExactMin);
    }

    /// <summary>The largest of a leaf's numbers.</summary>
    public PivotNumber MaxAt(int leaf)
    {
        var extremes = (Columns.Extremes ?? throw NotCarried(PivotParts.Extremes))[At(leaf)];
        return extremes.Inexact ? PivotNumber.Double(extremes.Max) : PivotNumber.Exact(extremes.ExactMax);
    }

    /// <summary>The sum of the magnitudes of a cell's numbers, which a level answer carries beside a
    /// sum that is a <c>double</c>: what bounds its rounding when it is checked against its parent
    /// (ADR-0370 section 3). Null for an exact sum, and in an answer of leaves.</summary>
    public double? AbsoluteSumAt(int leaf)
    {
        var at = At(leaf);
        var sum = (Columns.Sums ?? throw NotCarried(PivotParts.Sum))[at];
        return sum.Inexact && Columns.Absolutes is { } absolutes ? absolutes[at] : null;
    }

    /// <summary>The product of a leaf's numbers.</summary>
    public double ProductAt(int leaf) => (Columns.Products ?? throw NotCarried(PivotParts.Product))[At(leaf)];

    /// <summary>The running mean of a leaf's numbers.</summary>
    public double MeanAt(int leaf) => (Columns.Variances ?? throw NotCarried(PivotParts.Variance))[At(leaf)].Mean;

    /// <summary>The sum of squared deviations from the mean of a leaf's numbers.</summary>
    public double M2At(int leaf) => (Columns.Variances ?? throw NotCarried(PivotParts.Variance))[At(leaf)].M2;

    private int At(int leaf) => PivotAnswerAxis.Check(leaf, _leafCount);

    private InvalidOperationException NotCarried(PivotParts part)
        => new($"The answer carries no {part} part for '{Field}'; the question did not ask for it (ADR-0066).");
}

/// <summary>
/// Builds a <see cref="PivotAnswer"/> leaf by leaf — for a Consumer's server that computes the
/// Leaf Aggregates itself, from SQL's <c>GROUP BY</c> for instance (ADR-0066). A leaf is added
/// with its Items and its record count, then each field in Values is given its counts and the
/// parts the question asked for. Such a server is held to <see cref="PivotSource.From(ExGrid.Data.Snapshot, IReadOnlyList{PivotField}?, PivotSlicing?)"/>'s
/// answers by tests that ask both the same questions.
/// <para>A server that answers Level Aggregates (ADR-0370 section 3) adds the cells of the groups
/// shown with <see cref="AddCell"/> instead, from <c>GROUPING SETS</c> for instance, and gives a sum
/// that is a <c>double</c> with the sum of its magnitudes; an answer holds leaves or cells, never
/// both.</para>
/// </summary>
public sealed class PivotAnswerBuilder
{
    private readonly PivotQuery _query;
    private readonly string _sourceVersion;
    private readonly int _levelCount;
    private readonly List<PivotItemKey>[] _items;
    private readonly Dictionary<PivotItemKey, int>[] _itemIndex;
    private int[][] _itemOfLeaf;
    private long[] _records = new long[16];
    private readonly PartColumns[] _values;
    private readonly Dictionary<LeafKey, int> _leaves = [];
    private readonly List<PivotAnswerWindow> _windows = [];
    private readonly Dictionary<LeafKey, int> _windowOf = [];
    private int[] _rests = new int[16];
    private bool? _levels;
    private bool _built;

    /// <summary>Starts an answer to <paramref name="query"/> under a Source Version.</summary>
    public PivotAnswerBuilder(PivotQuery query, string sourceVersion)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(sourceVersion);
        _query = query;
        _sourceVersion = sourceVersion;
        _levelCount = query.Rows.Count + query.Columns.Count;
        _items = new List<PivotItemKey>[_levelCount];
        _itemIndex = new Dictionary<PivotItemKey, int>[_levelCount];
        _itemOfLeaf = new int[_levelCount][];
        for (var level = 0; level < _levelCount; level++)
        {
            _items[level] = [];
            _itemIndex[level] = [];
            _itemOfLeaf[level] = new int[16];
        }
        _values = query.Values.Select(v => new PartColumns(v.Parts, 16)).ToArray();
    }

    /// <summary>How many leaves have been added.</summary>
    public int LeafCount { get; private set; }

    /// <summary>
    /// Adds a leaf and returns its index. Refuses a leaf of no records, one whose Items are not one
    /// per row field then one per column field, and a second leaf for the same Items — text Items
    /// being told apart ignoring case (ADR-0060). A text Item's first spelling is the one that
    /// labels it.
    /// </summary>
    /// <param name="items">One Item per row field, outermost first, then one per column field.</param>
    /// <param name="records">How many records the leaf holds.</param>
    public int AddLeaf(IReadOnlyList<PivotItemKey> items, long records)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentOutOfRangeException.ThrowIfLessThan(records, 1);
        if (items.Count != _levelCount)
            throw new ArgumentException($"A leaf names one Item per row and column field: {_levelCount}, not {items.Count}.", nameof(items));
        if (_levels == true)
            throw new InvalidOperationException("This answer holds the cells of the groups shown; it is given them with AddCell (ADR-0370 section 3).");
        _levels = false;
        var indexes = new int[_levelCount];
        for (var level = 0; level < _levelCount; level++)
            indexes[level] = IndexOf(level, items[level] ?? throw new ArgumentNullException(nameof(items), "A leaf's Item is null."));
        if (!_leaves.TryAdd(new LeafKey(indexes), LeafCount))
            throw new ArgumentException($"The answer already has a leaf for [{string.Join(", ", items)}].", nameof(items));
        return Add(indexes, records);
    }

    /// <summary>
    /// Adds the cell of a shown row group crossed with a shown column group, and returns its index
    /// (ADR-0370 section 3): an answer of Level Aggregates, to a question carrying
    /// <see cref="PivotQuery.Levels"/>. Each group is named by its Items, outermost first, down to its
    /// depth — none for a grand total. Refuses a cell of no records, more Items than an axis has
    /// fields, and a second cell for the same Items. Which cells are shown, and that each has its
    /// parent, is checked when the answer is read against the question.
    /// </summary>
    /// <param name="rowItems">The row group's Items, outermost first.</param>
    /// <param name="columnItems">The column group's Items, outermost first.</param>
    /// <param name="records">How many records the cell holds.</param>
    public int AddCell(IReadOnlyList<PivotItemKey> rowItems, IReadOnlyList<PivotItemKey> columnItems, long records)
    {
        ArgumentNullException.ThrowIfNull(rowItems);
        ArgumentNullException.ThrowIfNull(columnItems);
        ArgumentOutOfRangeException.ThrowIfLessThan(records, 1);
        if (rowItems.Count > _query.Rows.Count)
            throw new ArgumentException($"A row group names at most one Item per row field: {_query.Rows.Count}, not {rowItems.Count}.", nameof(rowItems));
        if (columnItems.Count > _query.Columns.Count)
            throw new ArgumentException($"A column group names at most one Item per column field: {_query.Columns.Count}, not {columnItems.Count}.", nameof(columnItems));
        StartCells();
        var indexes = new int[_levelCount];
        Array.Fill(indexes, -1);
        for (var level = 0; level < rowItems.Count; level++)
            indexes[level] = IndexOf(level, rowItems[level] ?? throw new ArgumentNullException(nameof(rowItems), "A cell's Item is null."));
        for (var level = 0; level < columnItems.Count; level++)
            indexes[_query.Rows.Count + level] = IndexOf(_query.Rows.Count + level, columnItems[level] ?? throw new ArgumentNullException(nameof(columnItems), "A cell's Item is null."));
        if (!_leaves.TryAdd(new LeafKey(indexes), LeafCount))
            throw new ArgumentException($"The answer already has a cell for [{string.Join(", ", rowItems)}] × [{string.Join(", ", columnItems)}].", nameof(rowItems));
        var cell = Add(indexes, records);
        foreach (var parts in _values)
        {
            if (parts.Absolutes is { } absolutes)
                absolutes[cell] = double.NaN;
        }
        return cell;
    }

    /// <summary>
    /// Adds a sibling set answered as a window, and returns its index (ADR-0370 section 3): the
    /// children of the row group <paramref name="parentRows"/> names, of which
    /// <paramref name="before"/> come before the first one shown and <paramref name="after"/> after
    /// the last, each count taking in its margin. The margins and the siblings shown are added as
    /// cells; the siblings beyond each margin as rest cells with <see cref="AddRest"/>. Refuses a
    /// second window for one parent; whether the window answers the question is checked when the
    /// answer is read against it.
    /// </summary>
    /// <param name="parentRows">The parent row group's Items, outermost first.</param>
    /// <param name="before">The siblings before the first one shown.</param>
    /// <param name="after">The siblings after the last one shown.</param>
    public int AddWindow(IReadOnlyList<PivotItemKey> parentRows, int before, int after)
    {
        ArgumentNullException.ThrowIfNull(parentRows);
        ArgumentOutOfRangeException.ThrowIfNegative(before);
        ArgumentOutOfRangeException.ThrowIfNegative(after);
        StartCells();
        if (parentRows.Count >= _query.Rows.Count)
            throw new ArgumentException($"A window's parent names fewer Items than there are row fields: {_query.Rows.Count}, not {parentRows.Count}.", nameof(parentRows));
        var parent = new int[parentRows.Count];
        for (var level = 0; level < parent.Length; level++)
            parent[level] = IndexOf(level, parentRows[level] ?? throw new ArgumentNullException(nameof(parentRows), "A parent's Item is null."));
        if (!_windowOf.TryAdd(new LeafKey(parent), _windows.Count))
            throw new ArgumentException($"The answer already has a window under [{string.Join(", ", parentRows)}].", nameof(parentRows));
        _windows.Add(new PivotAnswerWindow(parent, before, after));
        return _windows.Count - 1;
    }

    /// <summary>
    /// Adds a rest cell of a window, and returns its index (ADR-0370 section 3): the siblings on one
    /// side beyond its margin, merged, crossed with the shown column group
    /// <paramref name="columnItems"/> names. Its parts are set as a cell's. Refuses one of no records
    /// and a second one for the same side and column group.
    /// </summary>
    /// <param name="window">The window, as <see cref="AddWindow"/> returned it.</param>
    /// <param name="side">The side whose siblings beyond the margin it merges.</param>
    /// <param name="columnItems">The column group's Items, outermost first.</param>
    /// <param name="records">How many records it holds.</param>
    public int AddRest(int window, PivotRestSide side, IReadOnlyList<PivotItemKey> columnItems, long records)
    {
        ArgumentNullException.ThrowIfNull(columnItems);
        ArgumentOutOfRangeException.ThrowIfLessThan(records, 1);
        if ((uint)window >= (uint)_windows.Count)
            throw new ArgumentOutOfRangeException(nameof(window), window, $"The answer has {_windows.Count} windows.");
        if (!Enum.IsDefined(side))
            throw new ArgumentOutOfRangeException(nameof(side), side, "A rest is before or after its window.");
        if (columnItems.Count > _query.Columns.Count)
            throw new ArgumentException($"A column group names at most one Item per column field: {_query.Columns.Count}, not {columnItems.Count}.", nameof(columnItems));
        // A rest's key holds its code past the levels, so it is never a group's.
        var indexes = new int[_levelCount + 1];
        Array.Fill(indexes, -1);
        var parent = _windows[window].Parent;
        for (var level = 0; level < parent.Count; level++)
            indexes[level] = parent[level];
        for (var level = 0; level < columnItems.Count; level++)
            indexes[_query.Rows.Count + level] = IndexOf(_query.Rows.Count + level, columnItems[level] ?? throw new ArgumentNullException(nameof(columnItems), "A cell's Item is null."));
        var code = 2 * window + (side == PivotRestSide.After ? 1 : 0);
        indexes[_levelCount] = code;
        if (!_leaves.TryAdd(new LeafKey(indexes), LeafCount))
            throw new ArgumentException($"Window {window} already has a rest {side.ToString().ToLowerInvariant()} it at [{string.Join(", ", columnItems)}].", nameof(columnItems));
        var cell = Add(indexes[.._levelCount], records, code);
        foreach (var parts in _values)
        {
            if (parts.Absolutes is { } absolutes)
                absolutes[cell] = double.NaN;
        }
        return cell;
    }

    private void StartCells()
    {
        if (_query.Levels is null)
            throw new InvalidOperationException("The question asks for the leaves, and carries no levels to answer with cells (ADR-0370 section 3).");
        if (_levels == false)
            throw new InvalidOperationException("This answer holds leaves; it is given them with AddLeaf (ADR-0370 section 3).");
        if (_levels is null)
        {
            _levels = true;
            foreach (var parts in _values)
            {
                if (parts.Sums is not null)
                    parts.TrackAbsolutes();
            }
        }
    }

    private int IndexOf(int level, PivotItemKey key)
    {
        if (!_itemIndex[level].TryGetValue(key, out var index))
        {
            index = _items[level].Count;
            _items[level].Add(key);
            _itemIndex[level][key] = index;
        }
        return index;
    }

    private int Add(int[] indexes, long records, int rest = -1)
    {
        var leaf = LeafCount++;
        if (leaf == _records.Length)
        {
            Array.Resize(ref _records, leaf * 2);
            Array.Resize(ref _rests, leaf * 2);
            for (var level = 0; level < _levelCount; level++)
                Array.Resize(ref _itemOfLeaf[level], leaf * 2);
        }
        for (var level = 0; level < _levelCount; level++)
            _itemOfLeaf[level][leaf] = indexes[level];
        _records[leaf] = records;
        _rests[leaf] = rest;
        foreach (var values in _values)
            values.EnsureCapacity(LeafCount);
        return leaf;
    }

    /// <summary>A field's counts at a leaf: its values that are not Blank, its numbers (non-finite
    /// ones included), and whether any number was non-finite.</summary>
    /// <param name="leaf">The leaf's index.</param>
    /// <param name="value">The field's index in the question's Values.</param>
    /// <param name="values">Values that are not Blank.</param>
    /// <param name="numbers">Values that are numbers.</param>
    /// <param name="nonFinite">Whether a number was non-finite.</param>
    public void SetCounts(int leaf, int value, long values, long numbers, bool nonFinite = false)
    {
        var columns = Columns(leaf, value);
        if (values < 0 || numbers < 0 || numbers > values || values > _records[leaf])
            throw new ArgumentOutOfRangeException(nameof(values), $"A leaf of {_records[leaf]} records cannot hold {values} values of which {numbers} are numbers.");
        columns.Counts[leaf] = new CountsPart { Values = values, Numbers = numbers, NonFinite = nonFinite };
    }

    /// <summary>A field's sum at a leaf.</summary>
    public void SetSum(int leaf, int value, PivotNumber sum)
    {
        var sums = Columns(leaf, value).Sums ?? throw NotAsked(value, PivotParts.Sum);
        sums[leaf] = sum.IsExact
            ? new SumPart { Exact = sum.ExactValue }
            : new SumPart { Double = sum.Value, Inexact = true };
    }

    /// <summary>A field's sum at a cell of Level Aggregates, with the sum of its numbers' magnitudes,
    /// which a sum that is a <c>double</c> carries (ADR-0370 section 3): what bounds its rounding when
    /// the cell is checked against its parent.</summary>
    public void SetSum(int leaf, int value, PivotNumber sum, double absoluteSum)
    {
        if (_levels != true)
            throw new InvalidOperationException("A sum of magnitudes is carried by a cell of Level Aggregates (ADR-0370 section 3).");
        if (double.IsNaN(absoluteSum) || absoluteSum < 0)
            throw new ArgumentOutOfRangeException(nameof(absoluteSum), absoluteSum, "A sum of magnitudes is a number, not below 0 (ADR-0370 section 3).");
        SetSum(leaf, value, sum);
        _values[value].Absolutes![leaf] = absoluteSum;
    }

    /// <summary>A field's extremes at a leaf, both exact or both <c>double</c>.</summary>
    public void SetExtremes(int leaf, int value, PivotNumber min, PivotNumber max)
    {
        var extremes = Columns(leaf, value).Extremes ?? throw NotAsked(value, PivotParts.Extremes);
        if (min.IsExact != max.IsExact)
            throw new ArgumentException("A leaf's extremes are both exact or both double.", nameof(max));
        extremes[leaf] = min.IsExact
            ? new ExtremesPart { ExactMin = min.ExactValue, ExactMax = max.ExactValue }
            : new ExtremesPart { Min = min.Value, Max = max.Value, Inexact = true };
    }

    /// <summary>A field's product at a leaf.</summary>
    public void SetProduct(int leaf, int value, double product)
        => (Columns(leaf, value).Products ?? throw NotAsked(value, PivotParts.Product))[leaf] = product;

    /// <summary>A field's running variance at a leaf: the mean of its numbers and their sum of
    /// squared deviations from it.</summary>
    public void SetVariance(int leaf, int value, double mean, double m2)
        => (Columns(leaf, value).Variances ?? throw NotAsked(value, PivotParts.Variance))[leaf] = new VariancePart { Mean = mean, M2 = m2 };

    /// <summary>The answer; or, when more leaves were added than the question allows, the refusal
    /// a source gives for them (ADR-0066). A builder builds once.</summary>
    public PivotAnswer Build()
    {
        if (_built)
            throw new InvalidOperationException("This answer has been built already.");
        _built = true;
        if (LeafCount > _query.MaxLeaves)
            return PivotAnswer.Refused(PivotSourceRefusal.TooManyLeaves(_query.MaxLeaves));
        foreach (var parts in _values)
            parts.Finish(LeafCount);
        var levels = _levels == true;
        var rows = new PivotAnswerAxis[_query.Rows.Count];
        var columns = new PivotAnswerAxis[_query.Columns.Count];
        for (var level = 0; level < _levelCount; level++)
        {
            var field = level < rows.Length ? _query.Rows[level].Field : _query.Columns[level - rows.Length].Field;
            var axis = new PivotAnswerAxis(field, [.. _items[level]], _itemOfLeaf[level], LeafCount, levels);
            if (level < rows.Length)
                rows[level] = axis;
            else
                columns[level - rows.Length] = axis;
        }
        var values = _values.Select((parts, v) => new PivotAnswerValues(_query.Values[v].Field, parts, LeafCount)).ToArray();
        try
        {
            return _windows.Count == 0
                ? new PivotAnswer(_sourceVersion, rows, columns, LeafCount, _records, values, levels)
                : new PivotAnswer(_sourceVersion, rows, columns, LeafCount, _records, values, levels, [.. _windows], _rests[..LeafCount]);
        }
        catch (FormatException error)
        {
            throw new InvalidOperationException(error.Message, error);
        }
    }

    private PartColumns Columns(int leaf, int value)
    {
        if (_built)
            throw new InvalidOperationException("This answer has been built already.");
        PivotAnswerAxis.Check(leaf, LeafCount);
        if ((uint)value >= (uint)_values.Length)
            throw new ArgumentOutOfRangeException(nameof(value), value, $"The question has {_values.Length} fields in Values.");
        return _values[value];
    }

    private InvalidOperationException NotAsked(int value, PivotParts part)
        => new($"The question did not ask for the {part} part of '{_query.Values[value].Field}' (ADR-0066).");

    private readonly struct LeafKey(int[] indexes) : IEquatable<LeafKey>
    {
        private readonly int[] _indexes = indexes;

        public bool Equals(LeafKey other) => _indexes.AsSpan().SequenceEqual(other._indexes);

        public override bool Equals(object? obj) => obj is LeafKey other && Equals(other);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var index in _indexes)
                hash.Add(index);
            return hash.ToHashCode();
        }
    }
}
