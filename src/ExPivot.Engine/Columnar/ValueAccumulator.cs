using ExGrid.Data;

namespace ExPivot.Engine;

/// <summary>
/// One field in Values, accumulated over a Snapshot's slices into the parts asked for, at each
/// leaf (ADR-0059/0065: only the parts asked for are accumulated; counts always).
/// <list type="bullet">
/// <item><b>Integer and Decimal</b> are summed exactly with no <c>decimal</c> arithmetic per row. A
/// slice holds its values as 64-bit integers at a scale of its own (ADR-0063); each leaf sums a
/// <b>run</b> of them in 64 bits, across slices of one scale, and a run is folded into the leaf's
/// exact sum — a 128-bit integer at a power of ten — when the scale changes, when 64 bits would
/// not hold it, and at the pass's end. Integer arithmetic is exact, so the order the numbers are
/// added and taken away in cannot change a sum (ADR-0066), and the finished sum is a
/// <c>decimal</c> without trailing zeros. A slice that holds <c>decimal</c>s, because a value did
/// not fit 64 bits at one scale, is summed per row into the same 128 bits. Past 128 bits, the sum
/// is Excel's <c>double</c> from then on; one no decimal holds is a <c>double</c> when finished.
/// The extremes are kept a run at a time likewise.</item>
/// <item><b>Double</b> is summed per row with Neumaier's compensation, as the first engine did.</item>
/// <item><b>Text, Date and Boolean</b> are counted and are never a number: Sum is 0, as ADR-0059's
/// table says.</item>
/// <item>A field read through an untyped accessor whose values are of several kinds is folded per
/// row in the data's order, each value by its own kind.</item>
/// </list>
/// Every leaf's parts depend only on its own rows, in slice and offset order — so recomputing one
/// leaf over its rows gives, bit for bit, what a fresh pass gives.
/// </summary>
internal sealed class ValueAccumulator
{
    // A leaf's open run: its count and sum side by side, so that a row touches one cache line.
    private struct Run
    {
        public long Sum;
        public int Count;
    }

    private const int NoRun = int.MinValue;

    private readonly FieldBinding _binding;
    private readonly BoundColumn? _single;
    private readonly bool _exact;
    private readonly bool _sum;
    private readonly bool _extremes;
    private readonly bool _perRow;

    // Each leaf's open run of an exact column's numbers: how many, their sum and their extremes, at
    // _runScale. A leaf with no number in the run has a count of 0.
    private Run[] _runs = [];
    private long[] _runMin = [];
    private long[] _runMax = [];
    private int _runScale = NoRun;
    private int _sliceScale;
    private int _leafCount;

    // Each leaf's exact sum: an integer at a power of ten, while the sum is exact.
    private Int128[] _wide = [];
    private byte[] _wideScale = [];

    private byte[] _kindOf = [];

    public ValueAccumulator(FieldBinding binding, PivotParts parts, int capacity)
    {
        _binding = binding;
        Parts = parts;
        Columns = new PartColumns(parts, Math.Max(16, capacity));
        _single = binding.Single;
        _exact = _single is { Role: ValueRole.Exact or ValueRole.Integer } && binding.Part is null;
        _sum = (parts & PivotParts.Sum) != 0;
        _extremes = (parts & PivotParts.Extremes) != 0;
        _perRow = (parts & (PivotParts.Product | PivotParts.Variance)) != 0;
        EnsureCapacity(Columns.Capacity);
    }

    public string Field => _binding.Name;

    public PivotParts Parts { get; }

    /// <summary>The parts at each leaf, as accumulated so far: not finished, so that more rows can
    /// be folded in (<see cref="Finished"/> makes the answer's copy). An exact sum is kept apart,
    /// as an integer, until it is finished.</summary>
    public PartColumns Columns { get; }

    /// <summary>
    /// Whether a removed record's value can be taken out of a leaf by subtraction (ADR-0066): a
    /// field of one column whose parts are exact — the counts, and the sum of an Integer or Decimal
    /// column. Every other part is recomputed from the leaf's records.
    /// </summary>
    public bool Subtracts
        => _single is { } single && _binding.Part is null
            ? single.Role switch
            {
                ValueRole.Exact or ValueRole.Integer => (Parts & ~PivotParts.Sum) == 0,
                ValueRole.Double => false,
                _ => true,
            }
            : _single is not null; // a date part in Values is counted, and counts subtract

    /// <summary>Whether the field's sum is asked for and kept exactly, as an integer: an Integer or
    /// Decimal column's.</summary>
    public bool SumsExactly => _exact && _sum;

    public void EnsureCapacity(int leaves)
    {
        Columns.EnsureCapacity(leaves);
        _leafCount = Math.Max(_leafCount, leaves);
        if (!_exact || _runs.Length >= Columns.Capacity)
            return;
        var size = Columns.Capacity;
        Array.Resize(ref _runs, size);
        if (_sum)
        {
            Array.Resize(ref _wide, size);
            Array.Resize(ref _wideScale, size);
        }
        if (_extremes)
        {
            Array.Resize(ref _runMin, size);
            Array.Resize(ref _runMax, size);
        }
    }

    /// <summary>Starts reading a slice: an exact column's runs go on across slices of one scale, and
    /// every open run is folded when the scale changes.</summary>
    public void BeginSegment(in SnapshotSlice slice)
    {
        if (ScaleChanges(slice))
            Flush();
        _runScale = _sliceScale;
    }

    /// <summary>Starts reading a slice for one leaf's recompute: only that leaf has a run open.</summary>
    public void BeginSegment(in SnapshotSlice slice, int leaf)
    {
        if (ScaleChanges(slice))
            Flush(leaf);
        _runScale = _sliceScale;
    }

    private bool ScaleChanges(in SnapshotSlice slice)
    {
        if (!_exact)
            return false;
        _sliceScale = _single!.Value.Role == ValueRole.Integer ? 0 : slice.Decimals((DecimalColumn)_single.Value.Column).Scale;
        return _sliceScale != _runScale;
    }

    /// <summary>Folds every open run into its leaf: when the scale changes, and at the end of a pass.
    /// Runs span every slice of one scale, so this is rare, and it looks at every leaf.</summary>
    public void Flush()
    {
        if (!_exact)
            return;
        for (var leaf = 0; leaf < _leafCount; leaf++)
        {
            if (_runs[leaf].Count != 0)
                Flush(leaf);
        }
    }

    /// <summary>Folds one leaf's open run into it.</summary>
    public void Flush(int leaf)
    {
        if (!_exact)
            return;
        ref var run = ref _runs[leaf];
        var numbers = run.Count;
        if (numbers == 0)
            return;
        var first = Columns.Counts[leaf].Numbers == 0;
        Columns.CountNumbers(leaf, numbers);
        if (_sum)
        {
            AddToSum(leaf, run.Sum, _runScale);
        }
        if (_extremes)
            Columns.FoldExtremes(leaf, Exactly.ToDecimal(_runMin[leaf], _runScale), Exactly.ToDecimal(_runMax[leaf], _runScale), first);
        run = default;
    }

    /// <summary>A leaf back to no value at all, before it is recomputed from its rows.</summary>
    public void Reset(int leaf)
    {
        Columns.ResetCell(leaf);
        if (!_exact)
            return;
        _runs[leaf] = default;
        if (_sum)
        {
            _wide[leaf] = 0;
            _wideScale[leaf] = 0;
        }
    }

    /// <summary>Folds rows [<paramref name="from"/>, +<c>leaves.Length</c>) of the current slice into
    /// their leaves; a row whose leaf is −1 is not read.</summary>
    public void Accumulate(in SnapshotSlice slice, int from, ReadOnlySpan<int> leaves)
    {
        if (_single is not { } single)
        {
            AccumulateVariant(slice, from, leaves);
            return;
        }
        if (_binding.Part is not null)
        {
            CountValues(slice.Blanks(single.Column), from, leaves);
            return;
        }
        switch (single.Role)
        {
            case ValueRole.Exact:
            {
                var values = slice.Decimals((DecimalColumn)single.Column);
                if (values.Scale >= 0)
                    AccumulateScaled(values.Scaled, slice.Blanks(single.Column), from, leaves);
                else
                    AccumulateDecimals(values.Exact, slice.Blanks(single.Column), from, leaves);
                return;
            }
            case ValueRole.Integer:
                AccumulateScaled(slice.Integers((IntegerColumn)single.Column), slice.Blanks(single.Column), from, leaves);
                return;
            case ValueRole.Double:
                AccumulateDoubles(slice.Doubles((DoubleColumn)single.Column), slice.Blanks(single.Column), from, leaves);
                return;
            case ValueRole.Text:
                CountCodes(slice.Codes((TextColumn)single.Column), from, leaves);
                return;
            default:
                CountValues(slice.Blanks(single.Column), from, leaves);
                return;
        }
    }

    /// <summary>
    /// Takes a removed record's value out of its leaf by subtraction (ADR-0066) when
    /// <see cref="Subtracts"/>: false, and nothing changed, when it cannot be — the leaf is then
    /// recomputed. <paramref name="slice"/> is the slice of the Snapshot the record was removed
    /// from. Called with every run folded (<see cref="Flush()"/>).
    /// </summary>
    public bool TrySubtract(int leaf, in SnapshotSlice slice, int offset)
    {
        if (!Subtracts)
            return false;
        var single = _single!.Value;
        if (Exactly.IsSet(slice.Blanks(single.Column), offset))
            return true;
        switch (_binding.Part is null ? single.Role : ValueRole.Date)
        {
            case ValueRole.Exact:
            case ValueRole.Integer:
            {
                if (_sum)
                {
                    if (Columns.IsInexactSum(leaf))
                        return false;
                    var (value, scale) = WideAt(single, slice, offset);
                    if (!Exactly.TryAdd(_wide[leaf], _wideScale[leaf], -value, scale, out var sum, out var sumScale))
                        return false;
                    _wide[leaf] = sum;
                    _wideScale[leaf] = (byte)sumScale;
                }
                Columns.CountNumbers(leaf, -1);
                return true;
            }
            case ValueRole.Text:
                if (slice.Codes((TextColumn)single.Column)[offset] >= 0)
                    Columns.Counts[leaf].Values--;
                return true;
            default:
                Columns.Counts[leaf].Values--;
                return true;
        }
    }

    /// <summary>The finished parts of the leaves <paramref name="order"/> names, in that order: an
    /// exact sum as a <c>decimal</c> without trailing zeros (a <c>double</c> where no decimal holds
    /// it), a <c>double</c> sum with its compensation taken in. The accumulation itself is left as
    /// it was, to fold more rows into.</summary>
    public PartColumns Finished(ReadOnlySpan<int> order)
    {
        var finished = new PartColumns(Parts, Math.Max(16, order.Length));
        for (var n = 0; n < order.Length; n++)
        {
            var leaf = order[n];
            finished.CopyCell(n, Columns, leaf);
            if (_exact && _sum && !Columns.IsInexactSum(leaf))
            {
                if (Exactly.TryToDecimal(_wide[leaf], _wideScale[leaf], out var exact))
                    finished.Sums![n] = new SumPart { Exact = exact };
                else
                    finished.ToInexactSum(n, Exactly.ToDouble(_wide[leaf], _wideScale[leaf]));
            }
        }
        finished.Finish(order.Length);
        finished.Canonicalize(order.Length);
        return finished;
    }

    // ---- The exact sum ---------------------------------------------------------------------------

    // Adds value × 10^-scale to a leaf's exact sum; past 128 bits, the sum is a double from then on.
    private void AddToSum(int leaf, Int128 value, int scale)
    {
        if (Columns.IsInexactSum(leaf))
        {
            Columns.AddInexactSum(leaf, Exactly.ToDouble(value, scale));
            return;
        }
        if (Exactly.TryAdd(_wide[leaf], _wideScale[leaf], value, scale, out var sum, out var sumScale))
        {
            _wide[leaf] = sum;
            _wideScale[leaf] = (byte)sumScale;
            return;
        }
        // Money stays exact until it cannot: then double, Excel's own arithmetic (ADR-0059).
        Columns.ToInexactSum(leaf, Exactly.ToDouble(_wide[leaf], _wideScale[leaf]));
        Columns.AddInexactSum(leaf, Exactly.ToDouble(value, scale));
    }

    private static (Int128 Value, int Scale) WideAt(BoundColumn column, in SnapshotSlice slice, int offset)
    {
        if (column.Role == ValueRole.Integer)
            return (slice.Integers((IntegerColumn)column.Column)[offset], 0);
        var values = slice.Decimals((DecimalColumn)column.Column);
        return values.Scale >= 0 ? (values.Scaled[offset], values.Scale) : Exactly.Wide(values.Exact[offset]);
    }

    // ---- The loops ----------------------------------------------------------------------------

    private void CountCodes(ReadOnlySpan<int> codes, int from, ReadOnlySpan<int> leaves)
    {
        var counts = Columns.Counts;
        for (var i = 0; i < leaves.Length; i++)
        {
            var leaf = leaves[i];
            if (leaf >= 0 && codes[from + i] >= 0)
                counts[leaf].Values++;
        }
    }

    private void CountValues(ReadOnlySpan<ulong> blanks, int from, ReadOnlySpan<int> leaves)
    {
        var counts = Columns.Counts;
        for (var i = 0; i < leaves.Length; i++)
        {
            var leaf = leaves[i];
            if (leaf >= 0 && !Exactly.IsSet(blanks, from + i))
                counts[leaf].Values++;
        }
    }

    private void AccumulateDoubles(ReadOnlySpan<double> values, ReadOnlySpan<ulong> blanks, int from, ReadOnlySpan<int> leaves)
    {
        for (var i = 0; i < leaves.Length; i++)
        {
            var leaf = leaves[i];
            if (leaf >= 0 && !Exactly.IsSet(blanks, from + i))
                Columns.AddDouble(leaf, values[from + i]);
        }
    }

    // A slice held as decimals, one at a time into each leaf's exact parts; no run is open here,
    // since the slice's scale is not a run's.
    private void AccumulateDecimals(ReadOnlySpan<decimal> values, ReadOnlySpan<ulong> blanks, int from, ReadOnlySpan<int> leaves)
    {
        for (var i = 0; i < leaves.Length; i++)
        {
            var leaf = leaves[i];
            if (leaf < 0 || Exactly.IsSet(blanks, from + i))
                continue;
            var value = values[from + i];
            var first = Columns.Counts[leaf].Numbers == 0;
            if (_sum)
            {
                var (wide, scale) = Exactly.Wide(value);
                AddToSum(leaf, wide, scale);
            }
            if (_extremes)
                Columns.FoldExtremes(leaf, value, value, first);
            if (_perRow)
                Columns.AddInexactParts(leaf, (double)value, Columns.Counts[leaf].Numbers + 1);
            Columns.CountNumbers(leaf, 1);
        }
    }

    private void AccumulateScaled(ReadOnlySpan<long> values, ReadOnlySpan<ulong> blanks, int from, ReadOnlySpan<int> leaves)
    {
        var runs = _runs;
        if (!_sum && !_extremes && !_perRow)
        {
            for (var i = 0; i < leaves.Length; i++)
            {
                var leaf = leaves[i];
                if (leaf >= 0 && !Exactly.IsSet(blanks, from + i))
                    runs[leaf].Count++;
            }
            return;
        }
        if (_sum && !_extremes && !_perRow)
        {
            for (var i = 0; i < leaves.Length; i++)
            {
                var leaf = leaves[i];
                if (leaf < 0 || Exactly.IsSet(blanks, from + i))
                    continue;
                ref var run = ref runs[leaf];
                run.Count++;
                var value = values[from + i];
                var sum = run.Sum;
                var next = sum + value;
                if (((sum ^ next) & (value ^ next)) < 0)
                {
                    // The run's sum would leave 64 bits: it is folded into the exact sum, and the
                    // run's sum starts again.
                    AddToSum(leaf, sum, _runScale);
                    next = value;
                }
                run.Sum = next;
            }
            return;
        }
        for (var i = 0; i < leaves.Length; i++)
        {
            var leaf = leaves[i];
            if (leaf < 0 || Exactly.IsSet(blanks, from + i))
                continue;
            var value = values[from + i];
            ref var run = ref runs[leaf];
            var before = run.Count++;
            if (_sum)
            {
                var sum = run.Sum;
                var next = sum + value;
                if (((sum ^ next) & (value ^ next)) < 0)
                {
                    AddToSum(leaf, sum, _runScale);
                    next = value;
                }
                run.Sum = next;
            }
            if (_extremes)
            {
                if (before == 0)
                {
                    _runMin[leaf] = value;
                    _runMax[leaf] = value;
                }
                else
                {
                    if (value < _runMin[leaf])
                        _runMin[leaf] = value;
                    if (value > _runMax[leaf])
                        _runMax[leaf] = value;
                }
            }
            if (_perRow)
            {
                // The product and the running variance take each number as the decimal of it
                // converts, counting the numbers folded before this run.
                var number = Exactly.ToDouble(value, _runScale);
                var count = Columns.Counts[leaf].Numbers + before + 1;
                Columns.AddInexactParts(leaf, number, count);
            }
        }
    }

    // A field of several kinds, read through an untyped accessor: each row's value in the column of
    // its kind, folded in the data's order by that kind, as the first engine folded it.
    private void AccumulateVariant(in SnapshotSlice slice, int from, ReadOnlySpan<int> leaves)
    {
        if (_kindOf.Length < leaves.Length)
            _kindOf = new byte[Math.Max(leaves.Length, 1024)];
        var kindOf = _kindOf.AsSpan(0, leaves.Length);
        kindOf.Fill(byte.MaxValue);
        var columns = _binding.Columns;
        for (var c = 0; c < columns.Length; c++)
        {
            var column = columns[c];
            if (column.Role == ValueRole.Text)
            {
                var codes = slice.Codes((TextColumn)column.Column);
                for (var i = 0; i < kindOf.Length; i++)
                {
                    if (codes[from + i] >= 0)
                        kindOf[i] = (byte)c;
                }
            }
            else
            {
                var blanks = slice.Blanks(column.Column);
                for (var i = 0; i < kindOf.Length; i++)
                {
                    if (!Exactly.IsSet(blanks, from + i))
                        kindOf[i] = (byte)c;
                }
            }
        }
        for (var i = 0; i < leaves.Length; i++)
        {
            var leaf = leaves[i];
            var c = kindOf[i];
            if (leaf < 0 || c == byte.MaxValue)
                continue;
            var column = columns[c];
            var o = from + i;
            switch (column.Role)
            {
                case ValueRole.Exact:
                    Columns.AddExact(leaf, slice.Decimals((DecimalColumn)column.Column)[o]);
                    break;
                case ValueRole.Integer:
                    Columns.AddExact(leaf, slice.Integers((IntegerColumn)column.Column)[o]);
                    break;
                case ValueRole.Double:
                    Columns.AddDouble(leaf, slice.Doubles((DoubleColumn)column.Column)[o]);
                    break;
                default:
                    Columns.AddOther(leaf);
                    break;
            }
        }
    }
}
