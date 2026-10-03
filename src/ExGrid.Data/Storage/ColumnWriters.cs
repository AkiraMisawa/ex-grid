using System.Numerics;

namespace ExGrid.Data.Storage;

/// <summary>Writes a span of values into a writer, as a bulk append does.</summary>
internal delegate void SpanWrite<TValue>(ColumnWriter writer, ReadOnlySpan<TValue> values);

/// <summary>
/// Fills one column of one segment, then seals it into immutable <see cref="ColumnData"/>. A writer
/// is begun with the rows it will hold when that is known, so the arrays it seals are the ones it
/// filled; otherwise it grows, never past the segment's length.
/// </summary>
internal abstract class ColumnWriter
{
    private int limit;

    protected int count;
    protected ulong[]? blanks;

    public int Count => count;

    public abstract int Capacity { get; }

    /// <summary>Starts a segment: room for <paramref name="capacity"/> rows now, and never more than
    /// <paramref name="limit"/>.</summary>
    public void Begin(int capacity, int limit)
    {
        this.limit = limit;
        count = 0;
        blanks = null;
        Reset(capacity);
    }

    public abstract void AddBlank();

    /// <summary>
    /// Turns into Blanks the rows of the <paramref name="length"/> just written from
    /// <paramref name="first"/> whose bit is set in <paramref name="blankBits"/> — bit
    /// <paramref name="bitOffset"/> + i stands for row <paramref name="first"/> + i — and sets their
    /// slots to the kind's zero.
    /// </summary>
    public void MarkBlanks(int first, int length, ReadOnlySpan<ulong> blankBits, int bitOffset)
    {
        var end = bitOffset + length;
        for (var w = bitOffset >> 6; w < blankBits.Length && (w << 6) < end; w++)
        {
            var word = blankBits[w];
            while (word != 0)
            {
                var bit = (w << 6) + BitOperations.TrailingZeroCount(word);
                word &= word - 1;
                if (bit < bitOffset)
                    continue;
                if (bit >= end)
                    return;
                MarkBlank(first + bit - bitOffset);
                ClearSlot(first + bit - bitOffset);
            }
        }
    }

    public abstract ColumnData Seal();

    protected abstract void Reset(int capacity);

    protected abstract void ClearSlot(int index);

    protected void MarkBlank(int index)
    {
        var words = Bits.Words(Capacity);
        if (blanks is null)
            blanks = new ulong[words];
        else if (blanks.Length < words)
            Array.Resize(ref blanks, words);
        Bits.Set(blanks, index);
    }

    /// <summary>Makes room for <paramref name="more"/> rows past <see cref="Count"/>.</summary>
    protected void Ensure<TValue>(ref TValue[] array, int more)
    {
        var needed = count + more;
        if (needed <= array.Length)
            return;
        if (needed > limit)
            throw new InvalidOperationException("A segment was given more rows than it holds.");
        Array.Resize(ref array, Math.Min(limit, Math.Max(needed, Math.Max(16, array.Length * 2))));
    }

    protected static TValue[] Trim<TValue>(TValue[] array, int length)
        => array.Length == length ? array : array.AsSpan(0, length).ToArray();
}

/// <summary>Codes into a text dictionary: interned by <paramref name="interner"/> as values arrive, or
/// given as codes already the lineage's (then the writer needs no interner).</summary>
internal sealed class TextColumnWriter(TextInterner? interner) : ColumnWriter
{
    private int[] codes = [];
    private bool anyBlank;

    public override int Capacity => codes.Length;

    public void Add(string? text)
    {
        Ensure(ref codes, 1);
        if (text is null)
        {
            anyBlank = true;
            codes[count++] = -1;
        }
        else
        {
            codes[count++] = interner!.Intern(text);
        }
    }

    public void Add(ReadOnlySpan<char> text)
    {
        Ensure(ref codes, 1);
        codes[count++] = interner!.Intern(text);
    }

    /// <summary>Appends a code the lineage's dictionary already holds, or -1 for a Blank.</summary>
    public void AddCode(int code)
    {
        Ensure(ref codes, 1);
        if (code < 0)
        {
            anyBlank = true;
            code = -1;
        }
        codes[count++] = code;
    }

    /// <summary>Appends a run of codes the lineage's dictionary already holds; -1 is a Blank.</summary>
    public void AddCodes(ReadOnlySpan<int> source, bool mayHoldBlanks)
    {
        Ensure(ref codes, source.Length);
        source.CopyTo(codes.AsSpan(count));
        count += source.Length;
        anyBlank |= mayHoldBlanks;
    }

    /// <summary>Room for <paramref name="more"/> codes, to be written through <see cref="Tail"/>.</summary>
    public Span<int> Tail(int more)
    {
        Ensure(ref codes, more);
        return codes.AsSpan(count, more);
    }

    /// <summary>Takes the <paramref name="written"/> codes written into <see cref="Tail"/>.</summary>
    public void Commit(int written, bool withBlanks)
    {
        count += written;
        anyBlank |= withBlanks;
    }

    public override void AddBlank()
    {
        Ensure(ref codes, 1);
        anyBlank = true;
        codes[count++] = -1;
    }

    public override ColumnData Seal()
    {
        var sealedCodes = Trim(codes, count);
        ulong[]? bits = null;
        if (anyBlank)
        {
            for (var i = 0; i < sealedCodes.Length; i++)
            {
                if (sealedCodes[i] < 0)
                    Bits.Set(bits ??= new ulong[Bits.Words(count)], i);
            }
        }
        return new TextData(sealedCodes, bits);
    }

    protected override void Reset(int capacity)
    {
        codes = new int[capacity];
        anyBlank = false;
    }

    protected override void ClearSlot(int index)
    {
        codes[index] = -1;
        anyBlank = true;
    }
}

/// <summary>
/// Decimal values, held exactly (ADR-0064). While every value fits a long at one power of ten, the
/// segment holds scaled longs at the largest number of decimal places among its values; the first
/// value that does not fit turns the segment to <see cref="decimal"/>. Trailing zeros are never
/// places, so <c>1.5</c> and <c>1.50</c> are stored, and read back, alike.
/// </summary>
internal sealed class DecimalColumnWriter : ColumnWriter
{
    private long[] scaled = [];
    private decimal[]? exact;
    private int scale;
    private bool mayShrink;

    public override int Capacity => exact?.Length ?? scaled.Length;

    /// <summary>
    /// Appends a run of values held at <paramref name="valueScale"/>, as another segment holds them,
    /// with no look at each value's places: the scale is brought down to its least once the segment
    /// is sealed.
    /// </summary>
    public void AddScaledRange(ReadOnlySpan<long> values, int valueScale)
    {
        if (exact is null)
        {
            Ensure(ref scaled, values.Length);
            if (valueScale <= scale || TryRescale(valueScale))
            {
                var up = scale - valueScale;
                var done = 0;
                if (up == 0)
                {
                    values.CopyTo(scaled.AsSpan(count));
                    done = values.Length;
                }
                else if (up <= DecimalMath.MaxLongScale)
                {
                    var max = DecimalMath.MaxBeforeScaling[up];
                    var factor = DecimalMath.Pow10[up];
                    for (; done < values.Length; done++)
                    {
                        var value = values[done];
                        if (value > max || value < -max)
                            break;
                        scaled[count + done] = value * factor;
                    }
                }
                count += done;
                mayShrink = true;
                if (done == values.Length)
                    return;
                values = values[done..];
            }
            GoExact();
        }
        Ensure(ref exact!, values.Length);
        foreach (var value in values)
            exact[count++] = DecimalMath.FromScaled(value, valueScale);
    }

    /// <summary>Appends a value; <paramref name="bits"/> is four ints of the caller's scratch space.</summary>
    public void Add(decimal value, Span<int> bits)
    {
        if (exact is null)
        {
            Ensure(ref scaled, 1);
            DecimalMath.Split(value, bits, out var low64, out var high32, out var valueScale, out var negative);
            if (high32 == 0 && low64 <= long.MaxValue && TryAdd((long)low64, valueScale, negative))
                return;
            GoExact();
        }
        Ensure(ref exact!, 1);
        exact[count++] = DecimalMath.Canonical(value);
    }

    /// <summary>Appends <paramref name="value"/> × 10^-<paramref name="valueScale"/>.</summary>
    public void AddScaled(long value, int valueScale)
    {
        if (exact is null)
        {
            Ensure(ref scaled, 1);
            if (value != long.MinValue && TryAdd(Math.Abs(value), valueScale, value < 0))
                return;
            GoExact();
        }
        Ensure(ref exact!, 1);
        exact[count++] = DecimalMath.FromScaled(value, valueScale);
    }

    public override void AddBlank()
    {
        if (exact is null)
        {
            Ensure(ref scaled, 1);
            MarkBlank(count);
            scaled[count++] = 0;
        }
        else
        {
            Ensure(ref exact, 1);
            MarkBlank(count);
            exact[count++] = 0m;
        }
    }

    public override ColumnData Seal()
    {
        var bits = Bits.Trim(blanks, count);
        if (exact is not null)
            return DecimalData.FromExact(Trim(exact, count), bits);
        if (mayShrink)
            Shrink();
        return DecimalData.FromScaled(Trim(scaled, count), scale, bits);
    }

    protected override void Reset(int capacity)
    {
        scaled = new long[capacity];
        exact = null;
        scale = 0;
        mayShrink = false;
    }

    /// <summary>Brings the scale down to the largest number of places among the values, trailing
    /// zeros not counted, as a value at a time would have left it.</summary>
    private void Shrink()
    {
        var values = scaled.AsSpan(0, count);
        var places = 0;
        foreach (var value in values)
        {
            if (value == 0)
                continue;
            var p = scale;
            var rest = value;
            while (p > places && rest % 10 == 0)
            {
                rest /= 10;
                p--;
            }
            if (p > places)
            {
                places = p;
                if (places == scale)
                    return;
            }
        }
        while (scale > places)
        {
            var step = Math.Min(DecimalMath.MaxLongScale, scale - places);
            var divisor = DecimalMath.Pow10[step];
            for (var i = 0; i < values.Length; i++)
                values[i] /= divisor;
            scale -= step;
        }
    }

    protected override void ClearSlot(int index)
    {
        if (exact is null)
            scaled[index] = 0;
        else
            exact[index] = 0m;
    }

    private bool TryAdd(long magnitude, int valueScale, bool negative)
    {
        if (magnitude == 0)
        {
            scaled[count++] = 0;
            return true;
        }
        if (valueScale > scale)
        {
            // Trailing zeros are not places: 1.50 needs one.
            while (valueScale > scale && magnitude % 10 == 0)
            {
                magnitude /= 10;
                valueScale--;
            }
            if (valueScale > scale && !TryRescale(valueScale))
                return false;
        }
        var up = scale - valueScale;
        if (up > 0)
        {
            if (up > DecimalMath.MaxLongScale || magnitude > DecimalMath.MaxBeforeScaling[up])
                return false;
            magnitude *= DecimalMath.Pow10[up];
        }
        scaled[count++] = negative ? -magnitude : magnitude;
        return true;
    }

    /// <summary>Moves every value written so far to <paramref name="newScale"/>, or leaves them all as
    /// they were when one would not fit.</summary>
    private bool TryRescale(int newScale)
    {
        var up = newScale - scale;
        var values = scaled.AsSpan(0, count);
        if (up > DecimalMath.MaxLongScale)
        {
            foreach (var value in values)
            {
                if (value != 0)
                    return false;
            }
        }
        else
        {
            var max = DecimalMath.MaxBeforeScaling[up];
            foreach (var value in values)
            {
                if (value > max || value < -max)
                    return false;
            }
            var factor = DecimalMath.Pow10[up];
            for (var i = 0; i < values.Length; i++)
                values[i] *= factor;
        }
        scale = newScale;
        return true;
    }

    private void GoExact()
    {
        var values = new decimal[Math.Max(Capacity, count + 1)];
        for (var i = 0; i < count; i++)
            values[i] = DecimalMath.FromScaled(scaled[i], scale);
        exact = values;
        scaled = [];
    }
}

internal sealed class DoubleColumnWriter : ColumnWriter
{
    private double[] values = [];

    public override int Capacity => values.Length;

    public void Add(double value)
    {
        Ensure(ref values, 1);
        values[count++] = value;
    }

    public void AddRange(ReadOnlySpan<double> source)
    {
        Ensure(ref values, source.Length);
        source.CopyTo(values.AsSpan(count));
        count += source.Length;
    }

    public override void AddBlank()
    {
        Ensure(ref values, 1);
        MarkBlank(count);
        values[count++] = 0;
    }

    public override ColumnData Seal() => new DoubleData(Trim(values, count), Bits.Trim(blanks, count));

    protected override void Reset(int capacity) => values = new double[capacity];

    protected override void ClearSlot(int index) => values[index] = 0;
}

internal sealed class IntegerColumnWriter : ColumnWriter
{
    private long[] values = [];

    public override int Capacity => values.Length;

    public void Add(long value)
    {
        Ensure(ref values, 1);
        values[count++] = value;
    }

    public void AddRange(ReadOnlySpan<long> source)
    {
        Ensure(ref values, source.Length);
        source.CopyTo(values.AsSpan(count));
        count += source.Length;
    }

    public override void AddBlank()
    {
        Ensure(ref values, 1);
        MarkBlank(count);
        values[count++] = 0;
    }

    public override ColumnData Seal() => new IntegerData(Trim(values, count), Bits.Trim(blanks, count));

    protected override void Reset(int capacity) => values = new long[capacity];

    protected override void ClearSlot(int index) => values[index] = 0;
}

/// <summary>Clock-value ticks, noting whether any is not a midnight.</summary>
internal sealed class DateColumnWriter : ColumnWriter
{
    private long[] ticks = [];
    private bool hasTime;

    public override int Capacity => ticks.Length;

    /// <summary>Appends ticks the caller has already checked lie within <see cref="DateTime"/>'s range.</summary>
    public void Add(long value)
    {
        Ensure(ref ticks, 1);
        if (value % TimeSpan.TicksPerDay != 0)
            hasTime = true;
        ticks[count++] = value;
    }

    /// <summary>Appends a run of another segment's ticks; whether one is not a midnight is settled
    /// when the segment is sealed.</summary>
    public void AddRange(ReadOnlySpan<long> source, bool mayHoldTime)
    {
        Ensure(ref ticks, source.Length);
        source.CopyTo(ticks.AsSpan(count));
        count += source.Length;
        hasTime |= mayHoldTime;
    }

    public override void AddBlank()
    {
        Ensure(ref ticks, 1);
        MarkBlank(count);
        ticks[count++] = 0;
    }

    public override ColumnData Seal()
    {
        var sealedTicks = Trim(ticks, count);
        var sealedBlanks = Bits.Trim(blanks, count);
        // A segment that took a Blank over a value in bulk may have lost its only time.
        var time = hasTime && AnyTime(sealedTicks);
        return new DateData(sealedTicks, sealedBlanks, time);
    }

    protected override void Reset(int capacity)
    {
        ticks = new long[capacity];
        hasTime = false;
    }

    protected override void ClearSlot(int index) => ticks[index] = 0;

    private static bool AnyTime(long[] values)
    {
        foreach (var value in values)
        {
            if (value % TimeSpan.TicksPerDay != 0)
                return true;
        }
        return false;
    }
}

internal sealed class BooleanColumnWriter : ColumnWriter
{
    private bool[] values = [];

    public override int Capacity => values.Length;

    public void Add(bool value)
    {
        Ensure(ref values, 1);
        values[count++] = value;
    }

    public void AddRange(ReadOnlySpan<bool> source)
    {
        Ensure(ref values, source.Length);
        source.CopyTo(values.AsSpan(count));
        count += source.Length;
    }

    public override void AddBlank()
    {
        Ensure(ref values, 1);
        MarkBlank(count);
        values[count++] = false;
    }

    public override ColumnData Seal() => new BooleanData(Trim(values, count), Bits.Trim(blanks, count));

    protected override void Reset(int capacity) => values = new bool[capacity];

    protected override void ClearSlot(int index) => values[index] = false;
}
