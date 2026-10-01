using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Apache.Arrow;
using Apache.Arrow.Types;

namespace ExGrid.Data.Arrow;

/// <summary>
/// Writes one Snapshot column as one Arrow column (ADR-0064): its Arrow type settled, and every value
/// checked to fit it, when the writer is made; then a record batch at a time, into buffers the writer
/// owns and reuses. A Blank is a null slot, and its slot holds 0.
/// </summary>
internal abstract class ColumnWriter
{
    private readonly byte[] validity;

    protected ColumnWriter(Snapshot snapshot, SnapshotColumn column, IArrowType type, int capacity)
    {
        Snapshot = snapshot;
        Column = column;
        KeyValuePair<string, string>[]? metadata = column.Caption == column.Name ? null : [new(SnapshotArrowMetadata.Caption, column.Caption)];
        Field = new Field(column.Name, type, nullable: true, metadata);
        validity = new byte[Bitmaps.Words(capacity) * 8];
    }

    /// <summary>The column's field in the stream's schema.</summary>
    public Field Field { get; }

    protected Snapshot Snapshot { get; }

    protected SnapshotColumn Column { get; }

    /// <summary>The writer for <paramref name="column"/>.</summary>
    /// <exception cref="SnapshotException">A value of the column cannot be written exactly in its Arrow type.</exception>
    public static ColumnWriter For(Snapshot snapshot, SnapshotColumn column, RowOrder order, int capacity) => column switch
    {
        TextColumn text => new TextWriter(snapshot, text, order, capacity),
        DecimalColumn number => new DecimalWriter(snapshot, number, order, capacity),
        DoubleColumn number => new DoubleWriter(snapshot, number, capacity),
        IntegerColumn number => new IntegerWriter(snapshot, number, capacity),
        DateColumn date => new DateWriter(snapshot, date, order, capacity),
        BooleanColumn flag => new BooleanWriter(snapshot, flag, capacity),
        _ => throw new InvalidOperationException($"The column '{column.Name}' is of no kind a Snapshot has."),
    };

    /// <summary>The column's array for a record batch of <paramref name="length"/> rows made of
    /// <paramref name="pieces"/>; valid until the next batch is made.</summary>
    public abstract IArrowArray Batch(int length, Piece[] pieces);

    /// <summary>The batch's validity bitmap, or none when it holds no Blank.</summary>
    protected (ArrowBuffer Buffer, int Nulls) Validity(int length, Piece[] pieces)
    {
        var bytes = Bitmaps.Words(length) * 8;
        var words = MemoryMarshal.Cast<byte, ulong>(validity.AsSpan(0, bytes));
        words.Clear();
        foreach (var piece in pieces)
            Bitmaps.ValidityFromBlanks(Snapshot.Slice(piece.Slice).Blanks(Column), piece.Offset, piece.Count, words, piece.At);
        var nulls = length - Bitmaps.Count(words, length);
        return nulls == 0 ? (ArrowBuffer.Empty, 0) : (new ArrowBuffer(validity.AsMemory(0, bytes)), nulls);
    }

    /// <summary>Refuses the write, naming the row by its 1-based place in the Snapshot's order.</summary>
    protected SnapshotException Refuse(int row, string reason) => new(row + 1L, Column.Name, reason);
}

/// <summary>
/// Text as <c>dictionary&lt;values=utf8, indices=int32&gt;</c>: the Snapshot's own dictionary, encoded
/// once and sent once, and its codes as the indices, a Blank a null index.
/// </summary>
internal sealed class TextWriter : ColumnWriter
{
    private static readonly UTF8Encoding Strict = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly DictionaryType Type = new(Int32Type.Default, StringType.Default, ordered: false);

    private readonly TextColumn column;
    private readonly StringArray dictionary;
    private readonly byte[] indices;

    public TextWriter(Snapshot snapshot, TextColumn column, RowOrder order, int capacity)
        : base(snapshot, column, Type, capacity)
    {
        this.column = column;
        dictionary = Encode(snapshot, column, order);
        indices = new byte[capacity * 4];
    }

    public override IArrowArray Batch(int length, Piece[] pieces)
    {
        var target = MemoryMarshal.Cast<byte, int>(indices.AsSpan(0, length * 4));
        foreach (var piece in pieces)
        {
            var codes = Snapshot.Slice(piece.Slice).Codes(column).Slice(piece.Offset, piece.Count);
            var to = target.Slice(piece.At, piece.Count);
            for (var i = 0; i < codes.Length; i++)
                to[i] = Math.Max(codes[i], 0);
        }
        var (validity, nulls) = Validity(length, pieces);
        var data = new ArrayData(Int32Type.Default, length, nulls, 0, [validity, new ArrowBuffer(indices.AsMemory(0, length * 4))]);
        return new DictionaryArray(Type, new Int32Array(data), dictionary);
    }

    /// <summary>
    /// The Snapshot's dictionary as UTF-8, every entry at its code. Text holding a lone surrogate,
    /// which no UTF-8 carries, is refused by the first row that holds it; an entry no row of this
    /// version holds — left behind by a Change Batch — is written with U+FFFD for its lone
    /// surrogates, since no value read back depends on it.
    /// </summary>
    private static StringArray Encode(Snapshot snapshot, TextColumn column, RowOrder order)
    {
        var entries = column.Dictionary;
        var offsets = new byte[(entries.Count + 1) * 4];
        var ends = MemoryMarshal.Cast<byte, int>(offsets.AsSpan());
        HashSet<int>? replaced = null;
        long total = 0;
        for (var code = 0; code < entries.Count; code++)
        {
            var text = entries[code];
            try
            {
                total += Strict.GetByteCount(text);
            }
            catch (EncoderFallbackException)
            {
                if (FirstRowHolding(snapshot, column, order, code) is { } row)
                    throw new SnapshotException(row + 1L, column.Name, "the text is not valid Unicode — it holds a lone surrogate — so UTF-8 cannot carry it.");
                (replaced ??= []).Add(code);
                total += Encoding.UTF8.GetByteCount(text);
            }
            if (total > int.MaxValue)
                throw new SnapshotException(null, column.Name, "the column's distinct texts come to more than the 2 GiB a utf8 dictionary holds.");
            ends[code + 1] = (int)total;
        }
        var bytes = new byte[total];
        for (var code = 0; code < entries.Count; code++)
        {
            var encoding = replaced is not null && replaced.Contains(code) ? Encoding.UTF8 : Strict;
            encoding.GetBytes(entries[code], bytes.AsSpan(ends[code], ends[code + 1] - ends[code]));
        }
        return new StringArray(entries.Count, new ArrowBuffer(offsets), new ArrowBuffer(bytes), ArrowBuffer.Empty);
    }

    /// <summary>The place in the Snapshot's order of the first row that holds <paramref name="code"/>,
    /// or <see langword="null"/> when no row of this version does.</summary>
    private static int? FirstRowHolding(Snapshot snapshot, TextColumn column, RowOrder order, int code)
    {
        foreach (var run in order.Runs)
        {
            var at = snapshot.Slice(run.Slice).Codes(column).Slice(run.Offset, run.Length).IndexOf(code);
            if (at >= 0)
                return run.Start + at;
        }
        return null;
    }
}

/// <summary>
/// Decimal as <c>decimal128(38, scale)</c>, at the largest scale among the slices that hold the
/// version's rows: a slice of scaled integers at its own scale, a slice of decimals at each value's
/// places. A slice held at a smaller scale is multiplied up, and a value that would then pass 38
/// digits is refused by row and column, since no <c>decimal128(38, scale)</c> holds it exactly.
/// </summary>
internal sealed class DecimalWriter : ColumnWriter
{
    private static readonly Int128[] Pow10 = CreatePow10();
    private static readonly UInt128 Limit = (UInt128)Pow10[38] - 1;

    private readonly DecimalColumn column;
    private readonly int scale;
    private readonly byte[] values;

    public DecimalWriter(Snapshot snapshot, DecimalColumn column, RowOrder order, int capacity)
        : this(snapshot, column, ScaleOf(snapshot, column, order), capacity)
        => Check(order);

    private DecimalWriter(Snapshot snapshot, DecimalColumn column, int scale, int capacity)
        : base(snapshot, column, new Decimal128Type(38, scale), capacity)
    {
        this.column = column;
        this.scale = scale;
        values = new byte[capacity * 16];
    }

    public override IArrowArray Batch(int length, Piece[] pieces)
    {
        var words = MemoryMarshal.Cast<byte, long>(values.AsSpan(0, length * 16));
        Span<int> bits = stackalloc int[4];
        foreach (var piece in pieces)
        {
            var held = Snapshot.Slice(piece.Slice).Decimals(column);
            var to = words.Slice(2 * piece.At, 2 * piece.Count);
            if (held.Scale >= 0)
            {
                var scaled = held.Scaled.Slice(piece.Offset, piece.Count);
                var up = scale - held.Scale;
                for (var i = 0; i < scaled.Length; i++)
                {
                    if (up == 0)
                    {
                        to[2 * i] = scaled[i];
                        to[(2 * i) + 1] = scaled[i] >> 63;
                    }
                    else
                    {
                        Put(to, i, scaled[i] * Pow10[up]);
                    }
                }
            }
            else
            {
                var exact = held.Exact.Slice(piece.Offset, piece.Count);
                for (var i = 0; i < exact.Length; i++)
                    Put(to, i, Unscaled(exact[i], bits));
            }
        }
        var (validity, nulls) = Validity(length, pieces);
        return new Decimal128Array(new ArrayData(Field.DataType, length, nulls, 0, [validity, new ArrowBuffer(values.AsMemory(0, length * 16))]));
    }

    private static void Put(Span<long> words, int i, Int128 value)
    {
        words[2 * i] = (long)(ulong)value;
        words[(2 * i) + 1] = (long)(value >> 64);
    }

    /// <summary>The largest scale among the slices that hold the version's rows.</summary>
    private static int ScaleOf(Snapshot snapshot, DecimalColumn column, RowOrder order)
    {
        var scale = 0;
        foreach (var run in order.Runs)
        {
            var held = snapshot.Slice(run.Slice).Decimals(column);
            if (held.Scale >= 0)
            {
                scale = Math.Max(scale, held.Scale);
                continue;
            }
            foreach (var value in held.Exact.Slice(run.Offset, run.Length))
                scale = Math.Max(scale, value.Scale);
        }
        return scale;
    }

    /// <summary>Refuses a value that would pass 38 digits at the column's scale. A slice of scaled
    /// integers less than 20 places below it cannot: no 64-bit integer has more than 19 digits.</summary>
    private void Check(RowOrder order)
    {
        Span<int> bits = stackalloc int[4];
        foreach (var run in order.Runs)
        {
            var slice = Snapshot.Slice(run.Slice);
            var held = slice.Decimals(column);
            if (held.Scale >= 0 && scale - held.Scale < 20)
                continue;
            for (var i = 0; i < run.Length; i++)
            {
                var offset = run.Offset + i;
                bool fits;
                if (held.Scale >= 0)
                {
                    var value = held.Scaled[offset];
                    var magnitude = value < 0 ? (UInt128)(-(Int128)value) : (UInt128)value;
                    fits = magnitude <= Limit / (UInt128)Pow10[scale - held.Scale];
                }
                else
                {
                    var magnitude = Magnitude(held.Exact[offset], bits, out var places);
                    fits = magnitude <= Limit / (UInt128)Pow10[scale - places];
                }
                if (!fits)
                {
                    throw Refuse(run.Start + i, string.Create(CultureInfo.InvariantCulture,
                        $"the value {held[offset]} cannot be written exactly as decimal128(38, {scale}): at the column's {scale} places it passes 38 digits."));
                }
            }
        }
    }

    private Int128 Unscaled(decimal value, Span<int> bits)
    {
        var magnitude = Magnitude(value, bits, out var places);
        var unscaled = (Int128)magnitude * Pow10[scale - places];
        return bits[3] < 0 ? -unscaled : unscaled;
    }

    private static UInt128 Magnitude(decimal value, Span<int> bits, out int places)
    {
        decimal.GetBits(value, bits);
        places = (bits[3] >> 16) & 0xFF;
        return (uint)bits[0] | ((UInt128)(uint)bits[1] << 32) | ((UInt128)(uint)bits[2] << 64);
    }

    private static Int128[] CreatePow10()
    {
        var pow = new Int128[39];
        pow[0] = 1;
        for (var k = 1; k < pow.Length; k++)
            pow[k] = pow[k - 1] * 10;
        return pow;
    }
}

/// <summary>Double as <c>float64</c>, each value's bits as they are.</summary>
internal sealed class DoubleWriter(Snapshot snapshot, DoubleColumn column, int capacity)
    : ColumnWriter(snapshot, column, DoubleType.Default, capacity)
{
    private readonly byte[] values = new byte[capacity * 8];

    public override IArrowArray Batch(int length, Piece[] pieces)
    {
        var target = MemoryMarshal.Cast<byte, double>(values.AsSpan(0, length * 8));
        foreach (var piece in pieces)
            Snapshot.Slice(piece.Slice).Doubles(column).Slice(piece.Offset, piece.Count).CopyTo(target[piece.At..]);
        var (validity, nulls) = Validity(length, pieces);
        return new DoubleArray(new ArrowBuffer(values.AsMemory(0, length * 8)), validity, length, nulls, 0);
    }
}

/// <summary>Integer as <c>int64</c>.</summary>
internal sealed class IntegerWriter(Snapshot snapshot, IntegerColumn column, int capacity)
    : ColumnWriter(snapshot, column, Int64Type.Default, capacity)
{
    private readonly byte[] values = new byte[capacity * 8];

    public override IArrowArray Batch(int length, Piece[] pieces)
    {
        var target = MemoryMarshal.Cast<byte, long>(values.AsSpan(0, length * 8));
        foreach (var piece in pieces)
            Snapshot.Slice(piece.Slice).Integers(column).Slice(piece.Offset, piece.Count).CopyTo(target[piece.At..]);
        var (validity, nulls) = Validity(length, pieces);
        return new Int64Array(new ArrowBuffer(values.AsMemory(0, length * 8)), validity, length, nulls, 0);
    }
}

/// <summary>
/// Date as <c>date32</c> when every value the version holds is a midnight, and otherwise as a
/// <c>timestamp</c> without a time zone in the coarsest unit that holds every value exactly: seconds,
/// milliseconds, microseconds, or nanoseconds (ADR-0064). Nanoseconds reach only from 1677 to 2262,
/// so a column that needs them and holds a date outside those years is refused by row and column.
/// </summary>
internal sealed class DateWriter : ColumnWriter
{
    private static readonly long EpochTicks = DateTime.UnixEpoch.Ticks;
    private static readonly long EpochDays = EpochTicks / TimeSpan.TicksPerDay;

    /// <summary>How far from 1970 a nanosecond timestamp reaches, in ticks.</summary>
    private const long NanosecondReach = long.MaxValue / 100;

    private readonly DateColumn column;
    private readonly DateUnit unit;
    private readonly byte[] values;

    public DateWriter(Snapshot snapshot, DateColumn column, RowOrder order, int capacity)
        : this(snapshot, column, UnitOf(snapshot, column, order), capacity)
    {
        if (unit == DateUnit.Nanoseconds)
            CheckNanoseconds(order);
    }

    private DateWriter(Snapshot snapshot, DateColumn column, DateUnit unit, int capacity)
        : base(snapshot, column, TypeOf(unit), capacity)
    {
        this.column = column;
        this.unit = unit;
        values = new byte[capacity * (unit == DateUnit.Days ? 4 : 8)];
    }

    public override IArrowArray Batch(int length, Piece[] pieces)
    {
        var width = unit == DateUnit.Days ? 4 : 8;
        var days = MemoryMarshal.Cast<byte, int>(values.AsSpan(0, length * width));
        var stamps = MemoryMarshal.Cast<byte, long>(values.AsSpan(0, length * width));
        var divisor = unit switch
        {
            DateUnit.Seconds => TimeSpan.TicksPerSecond,
            DateUnit.Milliseconds => TimeSpan.TicksPerMillisecond,
            _ => TimeSpan.TicksPerMicrosecond,
        };
        foreach (var piece in pieces)
        {
            var slice = Snapshot.Slice(piece.Slice);
            var ticks = slice.Ticks(column).Slice(piece.Offset, piece.Count);
            var blanks = slice.Blanks(column);
            for (var i = 0; i < ticks.Length; i++)
            {
                var blank = Bitmaps.Get(blanks, piece.Offset + i);
                var at = piece.At + i;
                switch (unit)
                {
                    case DateUnit.Days:
                        days[at] = blank ? 0 : (int)((ticks[i] / TimeSpan.TicksPerDay) - EpochDays);
                        break;
                    case DateUnit.Nanoseconds:
                        stamps[at] = blank ? 0 : (ticks[i] - EpochTicks) * 100;
                        break;
                    default:
                        stamps[at] = blank ? 0 : (ticks[i] - EpochTicks) / divisor;
                        break;
                }
            }
        }
        var (validity, nulls) = Validity(length, pieces);
        return ArrowArrayFactory.BuildArray(new ArrayData(Field.DataType, length, nulls, 0, [validity, new ArrowBuffer(values.AsMemory(0, length * width))]));
    }

    private static IArrowType TypeOf(DateUnit unit) => unit switch
    {
        DateUnit.Days => Date32Type.Default,
        DateUnit.Seconds => new TimestampType(TimeUnit.Second, (string?)null),
        DateUnit.Milliseconds => new TimestampType(TimeUnit.Millisecond, (string?)null),
        DateUnit.Microseconds => new TimestampType(TimeUnit.Microsecond, (string?)null),
        _ => new TimestampType(TimeUnit.Nanosecond, (string?)null),
    };

    /// <summary>The coarsest unit that holds every value the version holds exactly.</summary>
    private static DateUnit UnitOf(Snapshot snapshot, DateColumn column, RowOrder order)
    {
        var unit = DateUnit.Days;
        foreach (var run in order.Runs)
        {
            var slice = snapshot.Slice(run.Slice);
            var ticks = slice.Ticks(column).Slice(run.Offset, run.Length);
            var blanks = slice.Blanks(column);
            for (var i = 0; i < ticks.Length; i++)
            {
                var value = ticks[i];
                if (value % TimeSpan.TicksPerDay == 0 || Bitmaps.Get(blanks, run.Offset + i))
                    continue;
                var needs = value % TimeSpan.TicksPerSecond == 0 ? DateUnit.Seconds
                    : value % TimeSpan.TicksPerMillisecond == 0 ? DateUnit.Milliseconds
                    : value % TimeSpan.TicksPerMicrosecond == 0 ? DateUnit.Microseconds
                    : DateUnit.Nanoseconds;
                if (needs > unit)
                    unit = needs;
            }
        }
        return unit;
    }

    private void CheckNanoseconds(RowOrder order)
    {
        foreach (var run in order.Runs)
        {
            var slice = Snapshot.Slice(run.Slice);
            var ticks = slice.Ticks(column).Slice(run.Offset, run.Length);
            var blanks = slice.Blanks(column);
            for (var i = 0; i < ticks.Length; i++)
            {
                var from1970 = ticks[i] - EpochTicks;
                if (from1970 is >= -NanosecondReach and <= NanosecondReach || Bitmaps.Get(blanks, run.Offset + i))
                    continue;
                var date = new DateTime(ticks[i]).ToString("yyyy-MM-ddTHH:mm:ss.fffffff", CultureInfo.InvariantCulture);
                throw Refuse(run.Start + i, $"the date {date} cannot be written: the column holds a time finer than a microsecond, "
                    + "so it is written in nanoseconds, which reach only from 1677-09-21 to 2262-04-11.");
            }
        }
    }
}

/// <summary>Boolean as <c>bool</c>: Arrow's bits.</summary>
internal sealed class BooleanWriter(Snapshot snapshot, BooleanColumn column, int capacity)
    : ColumnWriter(snapshot, column, BooleanType.Default, capacity)
{
    private readonly byte[] values = new byte[Bitmaps.Words(capacity) * 8];

    public override IArrowArray Batch(int length, Piece[] pieces)
    {
        var bytes = Bitmaps.Words(length) * 8;
        var bits = MemoryMarshal.Cast<byte, ulong>(values.AsSpan(0, bytes));
        bits.Clear();
        foreach (var piece in pieces)
        {
            var flags = Snapshot.Slice(piece.Slice).Booleans(column).Slice(piece.Offset, piece.Count);
            for (var i = 0; i < flags.Length; i++)
            {
                if (flags[i])
                {
                    var at = piece.At + i;
                    bits[at >> 6] |= 1UL << at;
                }
            }
        }
        var (validity, nulls) = Validity(length, pieces);
        return new BooleanArray(new ArrowBuffer(values.AsMemory(0, bytes)), validity, length, nulls, 0);
    }
}
