using ExGrid.Data;

namespace ExPivot.Engine;

/// <summary>
/// One field in Values, accumulated over a Snapshot's slices into the parts asked for, at each
/// leaf (ADR-0059/0065: only the parts asked for are accumulated; counts always).
/// <list type="bullet">
/// <item><b>Integer and Decimal</b> are summed exactly with no <c>decimal</c> arithmetic per row: a
/// 64-bit sum per leaf at the segment's scale — each segment of a Decimal column has its own —
/// folded into the leaf's <c>decimal</c> at the segment's end (a run that would leave 64 bits is
/// folded early). The extremes are kept likewise. A segment that holds <c>decimal</c>s because a
/// value did not fit 64 bits is summed per row in <c>decimal</c>.</item>
/// <item><b>Double</b> is summed per row with Neumaier's compensation, as the first engine did.</item>
/// <item><b>Text, Date and Boolean</b> are counted and are never a number: Sum is 0, as ADR-0059's
/// table says.</item>
/// <item>A field read through an untyped accessor whose values are of several kinds is folded per
/// row in the data's order, each value by its own kind.</item>
/// </list>
/// Every leaf's parts depend only on its own rows, in slice and offset order, and on the segments
/// they lie in — so recomputing one leaf over its rows gives, bit for bit, what a fresh pass gives.
/// </summary>
internal sealed class ValueAccumulator
{
    private readonly FieldBinding _binding;
    private readonly BoundColumn? _single;
    private readonly bool _exact;
    private readonly bool _sum;
    private readonly bool _extremes;
    private readonly bool _perRow;
    private long[] _segSum = [];
    private long[] _segMin = [];
    private long[] _segMax = [];
    private int[] _segCount = [];
    private int[] _touched = [];
    private int _touchedCount;
    private int _scale;
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
    /// be folded in (<see cref="Finished"/> makes the answer's copy).</summary>
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

    public void EnsureCapacity(int leaves)
    {
        Columns.EnsureCapacity(leaves);
        if (!_exact || _segCount.Length >= Columns.Capacity)
            return;
        var size = Columns.Capacity;
        Array.Resize(ref _segCount, size);
        Array.Resize(ref _touched, size);
        if (_sum)
            Array.Resize(ref _segSum, size);
        if (_extremes)
        {
            Array.Resize(ref _segMin, size);
            Array.Resize(ref _segMax, size);
        }
    }

    /// <summary>Starts a slice: an exact column's values are summed at this segment's scale.</summary>
    public void BeginSegment(in SnapshotSlice slice)
    {
        if (!_exact)
            return;
        _scale = _single!.Value.Role == ValueRole.Integer ? 0 : slice.Decimals((DecimalColumn)_single.Value.Column).Scale;
    }

    /// <summary>Ends a slice: the 64-bit sums and extremes of the leaves it touched are folded into
    /// their exact parts.</summary>
    public void EndSegment()
    {
        if (!_exact)
            return;
        var touched = _touched;
        for (var t = 0; t < _touchedCount; t++)
        {
            var leaf = touched[t];
            var numbers = _segCount[leaf];
            Columns.FoldExact(
                leaf,
                numbers,
                _sum ? Exactly.ToDecimal(_segSum[leaf], _scale) : 0m,
                _extremes ? Exactly.ToDecimal(_segMin[leaf], _scale) : 0m,
                _extremes ? Exactly.ToDecimal(_segMax[leaf], _scale) : 0m);
            _segCount[leaf] = 0;
            if (_sum)
                _segSum[leaf] = 0;
        }
        _touchedCount = 0;
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
    /// recomputed. <paramref name="slice"/> is the slice of the Snapshot the record was removed from.
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
            {
                var values = slice.Decimals((DecimalColumn)single.Column);
                // A segment of decimals may hold 28 digits, whose sums decimal rounds: recomputed.
                return values.Scale >= 0 && Columns.TrySubtractExact(leaf, Exactly.ToDecimal(values.Scaled[offset], values.Scale));
            }
            case ValueRole.Integer:
                return Columns.TrySubtractExact(leaf, slice.Integers((IntegerColumn)single.Column)[offset]);
            case ValueRole.Text:
                if (slice.Codes((TextColumn)single.Column)[offset] >= 0)
                    Columns.Counts[leaf].Values--;
                return true;
            default:
                Columns.Counts[leaf].Values--;
                return true;
        }
    }

    /// <summary>The finished parts of the leaves <paramref name="order"/> names, in that order: a
    /// <c>double</c> sum takes its compensation in, and an exact part is written without trailing
    /// zeros. The accumulation itself is left as it was, to fold more rows into.</summary>
    public PartColumns Finished(ReadOnlySpan<int> order)
    {
        var finished = new PartColumns(Parts, Math.Max(16, order.Length));
        for (var n = 0; n < order.Length; n++)
            finished.CopyCell(n, Columns, order[n]);
        finished.Finish(order.Length);
        finished.Canonicalize(order.Length);
        return finished;
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

    private void AccumulateDecimals(ReadOnlySpan<decimal> values, ReadOnlySpan<ulong> blanks, int from, ReadOnlySpan<int> leaves)
    {
        for (var i = 0; i < leaves.Length; i++)
        {
            var leaf = leaves[i];
            if (leaf >= 0 && !Exactly.IsSet(blanks, from + i))
                Columns.AddExact(leaf, values[from + i]);
        }
    }

    private void AccumulateScaled(ReadOnlySpan<long> values, ReadOnlySpan<ulong> blanks, int from, ReadOnlySpan<int> leaves)
    {
        var segCount = _segCount;
        var touched = _touched;
        if (!_sum && !_extremes && !_perRow)
        {
            for (var i = 0; i < leaves.Length; i++)
            {
                var leaf = leaves[i];
                if (leaf < 0 || Exactly.IsSet(blanks, from + i))
                    continue;
                if (segCount[leaf]++ == 0)
                    touched[_touchedCount++] = leaf;
            }
            return;
        }
        if (_sum && !_extremes && !_perRow)
        {
            var segSum = _segSum;
            for (var i = 0; i < leaves.Length; i++)
            {
                var leaf = leaves[i];
                if (leaf < 0 || Exactly.IsSet(blanks, from + i))
                    continue;
                if (segCount[leaf]++ == 0)
                    touched[_touchedCount++] = leaf;
                var value = values[from + i];
                var sum = segSum[leaf];
                var next = sum + value;
                if (((sum ^ next) & (value ^ next)) < 0)
                {
                    // The run would leave 64 bits: fold it into the exact sum, and start again.
                    Columns.AddToExactSum(leaf, Exactly.ToDecimal(sum, _scale));
                    next = value;
                }
                segSum[leaf] = next;
            }
            return;
        }
        for (var i = 0; i < leaves.Length; i++)
        {
            var leaf = leaves[i];
            if (leaf < 0 || Exactly.IsSet(blanks, from + i))
                continue;
            var value = values[from + i];
            var before = segCount[leaf]++;
            if (before == 0)
                touched[_touchedCount++] = leaf;
            if (_sum)
            {
                var sum = _segSum[leaf];
                var next = sum + value;
                if (((sum ^ next) & (value ^ next)) < 0)
                {
                    Columns.AddToExactSum(leaf, Exactly.ToDecimal(sum, _scale));
                    next = value;
                }
                _segSum[leaf] = next;
            }
            if (_extremes)
            {
                if (before == 0)
                {
                    _segMin[leaf] = value;
                    _segMax[leaf] = value;
                }
                else
                {
                    if (value < _segMin[leaf])
                        _segMin[leaf] = value;
                    if (value > _segMax[leaf])
                        _segMax[leaf] = value;
                }
            }
            if (_perRow)
            {
                // The product and the running variance take each number as the decimal of it
                // converts, counting the numbers folded before this segment.
                var number = Exactly.ToDouble(value, _scale);
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
