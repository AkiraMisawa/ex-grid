namespace ExPivot.Engine;

/// <summary>
/// What a Pivot Source answers a <see cref="PivotQuery"/> with: the Leaf Aggregates (ADR-0065), or
/// a refusal. An answer carries its Source Version; each row and column field's Items; and the
/// leaves — one for every combination of the row and column fields' Items that has records once
/// the Hidden Items are left out — each with one Item per row field and per column field, its
/// record count, and the parts asked for of every field in Values. ExPivot computes every cell,
/// subtotal and grand total from the leaves (<see cref="PivotEngine.Cube"/>), without asking again.
///
/// <para>The leaves are held column by column — an array per level and per part — never as an
/// object per leaf, since an answer may hold hundreds of thousands of them. The order of the
/// leaves and of a field's Items is the source's own; the report orders Items itself. Immutable
/// and serialisable (<see cref="PivotJson"/>).</para>
/// </summary>
public sealed class PivotAnswer
{
    private readonly string? _sourceVersion;
    private readonly PivotAnswerAxis[] _rows;
    private readonly PivotAnswerAxis[] _columns;
    private readonly PivotAnswerValues[] _values;

    internal PivotAnswer(
        string sourceVersion,
        PivotAnswerAxis[] rows,
        PivotAnswerAxis[] columns,
        int leafCount,
        long[] records,
        PivotAnswerValues[] values)
    {
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

    /// <summary>The source's refusal to answer (ADR-0065): too many leaves, an unknown field, or
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
            return $"it holds {LeafCount} leaves, and the question allows {query.MaxLeaves}";
        return null;
    }

    private InvalidOperationException RefusedError()
        => new($"The source refused to answer: {Refusal!.Message}");
}

/// <summary>One row or column field of an answer (ADR-0065): its Items and each leaf's Item.</summary>
public sealed class PivotAnswerAxis
{
    private readonly PivotItemKey[] _items;

    internal PivotAnswerAxis(string field, PivotItemKey[] items, int[] itemOfLeaf, int leafCount)
    {
        Field = field;
        _items = items;
        ItemOfLeaf = itemOfLeaf;
        for (var leaf = 0; leaf < leafCount; leaf++)
        {
            if ((uint)itemOfLeaf[leaf] >= (uint)items.Length)
                throw new FormatException($"Leaf {leaf} names Item {itemOfLeaf[leaf]} of '{field}', which has {items.Length}.");
        }
    }

    /// <summary>The Pivot Field's name.</summary>
    public string Field { get; }

    /// <summary>The field's Items among the leaves. A text Item's key is its first spelling, which
    /// labels it (ADR-0059).</summary>
    public IReadOnlyList<PivotItemKey> Items => _items;

    internal PivotItemKey[] ItemArray => _items;

    internal int[] ItemOfLeaf { get; }

    /// <summary>The index in <see cref="Items"/> of a leaf's Item.</summary>
    public int ItemAt(int leaf) => ItemOfLeaf[leaf];

    internal static int Check(int leaf, int leafCount)
        => (uint)leaf < (uint)leafCount ? leaf : throw new ArgumentOutOfRangeException(nameof(leaf), leaf, $"The answer has {leafCount} leaves.");
}

/// <summary>
/// One field in Values of an answer (ADR-0065): the parts it carries, at every leaf. The counts
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

    /// <summary>The product of a leaf's numbers.</summary>
    public double ProductAt(int leaf) => (Columns.Products ?? throw NotCarried(PivotParts.Product))[At(leaf)];

    /// <summary>The running mean of a leaf's numbers.</summary>
    public double MeanAt(int leaf) => (Columns.Variances ?? throw NotCarried(PivotParts.Variance))[At(leaf)].Mean;

    /// <summary>The sum of squared deviations from the mean of a leaf's numbers.</summary>
    public double M2At(int leaf) => (Columns.Variances ?? throw NotCarried(PivotParts.Variance))[At(leaf)].M2;

    private int At(int leaf) => PivotAnswerAxis.Check(leaf, _leafCount);

    private InvalidOperationException NotCarried(PivotParts part)
        => new($"The answer carries no {part} part for '{Field}'; the question did not ask for it (ADR-0065).");
}

/// <summary>
/// Builds a <see cref="PivotAnswer"/> leaf by leaf — for a Consumer's server that computes the
/// Leaf Aggregates itself, from SQL's <c>GROUP BY</c> for instance (ADR-0065). A leaf is added
/// with its Items and its record count, then each field in Values is given its counts and the
/// parts the question asked for. Such a server is held to <see cref="PivotSource.From{TRecord}"/>'s
/// answers by tests that ask both the same questions.
/// </summary>
public sealed class PivotAnswerBuilder
{
    private readonly PivotQuery _query;
    private readonly string _sourceVersion;
    private readonly int _levels;
    private readonly List<PivotItemKey>[] _items;
    private readonly Dictionary<PivotItemKey, int>[] _itemIndex;
    private int[][] _itemOfLeaf;
    private long[] _records = new long[16];
    private readonly PartColumns[] _values;
    private readonly Dictionary<LeafKey, int> _leaves = [];
    private bool _built;

    /// <summary>Starts an answer to <paramref name="query"/> under a Source Version.</summary>
    public PivotAnswerBuilder(PivotQuery query, string sourceVersion)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(sourceVersion);
        _query = query;
        _sourceVersion = sourceVersion;
        _levels = query.Rows.Count + query.Columns.Count;
        _items = new List<PivotItemKey>[_levels];
        _itemIndex = new Dictionary<PivotItemKey, int>[_levels];
        _itemOfLeaf = new int[_levels][];
        for (var level = 0; level < _levels; level++)
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
    /// being told apart ignoring case (ADR-0059). A text Item's first spelling is the one that
    /// labels it.
    /// </summary>
    /// <param name="items">One Item per row field, outermost first, then one per column field.</param>
    /// <param name="records">How many records the leaf holds.</param>
    public int AddLeaf(IReadOnlyList<PivotItemKey> items, long records)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentOutOfRangeException.ThrowIfLessThan(records, 1);
        if (items.Count != _levels)
            throw new ArgumentException($"A leaf names one Item per row and column field: {_levels}, not {items.Count}.", nameof(items));
        var indexes = new int[_levels];
        for (var level = 0; level < _levels; level++)
        {
            var key = items[level] ?? throw new ArgumentNullException(nameof(items), "A leaf's Item is null.");
            if (!_itemIndex[level].TryGetValue(key, out var index))
            {
                index = _items[level].Count;
                _items[level].Add(key);
                _itemIndex[level][key] = index;
            }
            indexes[level] = index;
        }
        if (!_leaves.TryAdd(new LeafKey(indexes), LeafCount))
            throw new ArgumentException($"The answer already has a leaf for [{string.Join(", ", items)}].", nameof(items));

        var leaf = LeafCount++;
        if (leaf == _records.Length)
        {
            Array.Resize(ref _records, leaf * 2);
            for (var level = 0; level < _levels; level++)
                Array.Resize(ref _itemOfLeaf[level], leaf * 2);
        }
        for (var level = 0; level < _levels; level++)
            _itemOfLeaf[level][leaf] = indexes[level];
        _records[leaf] = records;
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
    /// a source gives for them (ADR-0065). A builder builds once.</summary>
    public PivotAnswer Build()
    {
        if (_built)
            throw new InvalidOperationException("This answer has been built already.");
        _built = true;
        if (LeafCount > _query.MaxLeaves)
            return PivotAnswer.Refused(PivotSourceRefusal.TooManyLeaves(_query.MaxLeaves));
        foreach (var parts in _values)
            parts.Finish(LeafCount);
        var rows = new PivotAnswerAxis[_query.Rows.Count];
        var columns = new PivotAnswerAxis[_query.Columns.Count];
        for (var level = 0; level < _levels; level++)
        {
            var field = level < rows.Length ? _query.Rows[level].Field : _query.Columns[level - rows.Length].Field;
            var axis = new PivotAnswerAxis(field, [.. _items[level]], _itemOfLeaf[level], LeafCount);
            if (level < rows.Length)
                rows[level] = axis;
            else
                columns[level - rows.Length] = axis;
        }
        var values = _values.Select((parts, v) => new PivotAnswerValues(_query.Values[v].Field, parts, LeafCount)).ToArray();
        return new PivotAnswer(_sourceVersion, rows, columns, LeafCount, _records, values);
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
        => new($"The question did not ask for the {part} part of '{_query.Values[value].Field}' (ADR-0065).");

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
