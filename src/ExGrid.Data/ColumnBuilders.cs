using System.Globalization;
using ExGrid.Data.Storage;

namespace ExGrid.Data;

/// <summary>
/// Appends the values of one column of a <see cref="SnapshotColumnsBuilder"/>, a value at a time or a
/// span at a time. A bulk append takes an optional bit set of Blanks in the Snapshot's own form —
/// one bit per value, least significant bit first, set for a Blank — so the Blanks a slice hands out
/// can be passed straight back in.
/// </summary>
public abstract class ColumnBuilder
{
    private readonly List<ColumnData> sealedSegments = [];
    private readonly int segmentLength;

    private protected ColumnBuilder(SnapshotColumnsBuilder owner, string name, string? caption, SnapshotKind kind, ColumnWriter writer)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Owner = owner;
        Name = name;
        Caption = caption ?? name;
        Kind = kind;
        Writer = writer;
        segmentLength = owner.Tuning.SegmentLength;
        writer.Begin(Math.Min(256, segmentLength), segmentLength);
    }

    /// <summary>The column's name, unique within the Snapshot.</summary>
    public string Name { get; }

    /// <summary>The column's caption; the name unless one was declared.</summary>
    public string Caption { get; }

    /// <summary>The column's kind.</summary>
    public SnapshotKind Kind { get; }

    /// <summary>The values appended so far, Blanks included.</summary>
    public int Count => (sealedSegments.Count * segmentLength) + Writer.Count;

    private protected SnapshotColumnsBuilder Owner { get; }

    private protected ColumnWriter Writer { get; }

    /// <summary>Appends a Blank: a value that is not there, kept apart from every value.</summary>
    public void AppendBlank()
    {
        Room(1);
        Writer.AddBlank();
    }

    /// <summary>Appends <paramref name="count"/> Blanks.</summary>
    public void AppendBlanks(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        for (var i = 0; i < count; i++)
            AppendBlank();
    }

    /// <summary>How many of <paramref name="wanted"/> values fit the current segment, sealing it
    /// first when it is full.</summary>
    private protected int Room(int wanted)
    {
        Owner.CheckOpen();
        if (Writer.Count == segmentLength)
        {
            sealedSegments.Add(Writer.Seal());
            Writer.Begin(segmentLength, segmentLength);
        }
        return Math.Min(wanted, segmentLength - Writer.Count);
    }

    /// <summary>Fails the load, naming the value's row (<paramref name="row"/> is from zero) and this column.</summary>
    private protected SnapshotException Refuse(int row, string reason)
        => Owner.Refuse(new SnapshotException(row + 1L, Name, reason));

    private protected void AppendRange<TValue>(ReadOnlySpan<TValue> values, ReadOnlySpan<ulong> blanks, SpanWrite<TValue> write)
    {
        CheckBlanks(blanks, values.Length);
        var done = 0;
        while (done < values.Length)
        {
            var n = Room(values.Length - done);
            var first = Writer.Count;
            write(Writer, values.Slice(done, n));
            Writer.MarkBlanks(first, n, blanks, done);
            done += n;
        }
    }

    private protected static void CheckBlanks(ReadOnlySpan<ulong> blanks, int length)
    {
        if (!blanks.IsEmpty && blanks.Length < Bits.Words(length))
            throw new ArgumentException("The Blanks hold one bit per value.", nameof(blanks));
    }

    private protected static bool IsBlank(ReadOnlySpan<ulong> blanks, int index) => !blanks.IsEmpty && Bits.Get(blanks, index);

    internal List<ColumnData> SealAll()
    {
        if (Writer.Count > 0)
            sealedSegments.Add(Writer.Seal());
        return sealedSegments;
    }
}

/// <summary>
/// Appends Text: exactly as given, told apart ordinally, into a dictionary in the order each value
/// first appears (ADR-0063). <see langword="null"/> is a Blank; the empty string is a value.
/// </summary>
public sealed class TextColumnBuilder : ColumnBuilder
{
    private char[] chars = new char[64];

    internal TextColumnBuilder(SnapshotColumnsBuilder owner, string name, string? caption)
        : this(owner, name, caption, new TextInterner())
    {
    }

    private TextColumnBuilder(SnapshotColumnsBuilder owner, string name, string? caption, TextInterner interner)
        : base(owner, name, caption, SnapshotKind.Text, new TextColumnWriter(interner))
        => Interner = interner;

    internal TextInterner Interner { get; }

    private TextColumnWriter Texts => (TextColumnWriter)Writer;

    /// <summary>Appends a value, or a Blank for <see langword="null"/>.</summary>
    public void Append(string? value)
    {
        Room(1);
        Texts.Add(value);
    }

    /// <summary>Appends a value read from the reader's own buffer; a string is made only for text
    /// the dictionary does not hold yet.</summary>
    public void Append(ReadOnlySpan<char> value)
    {
        Room(1);
        Texts.Add(value);
    }

    /// <summary>Appends a value given as UTF-8 bytes; a string is made only for text the dictionary
    /// does not hold yet. Bytes that are not valid UTF-8 fail the load, naming the row and the column.</summary>
    /// <exception cref="SnapshotException">The bytes are not valid UTF-8.</exception>
    public void AppendUtf8(ReadOnlySpan<byte> value)
    {
        Room(1);
        if (!Utf8Text.TryDecode(value, ref chars, out var length))
            throw Refuse(Count, "the text is not valid UTF-8.");
        Texts.Add(chars.AsSpan(0, length));
    }

    /// <summary>Appends a value and returns its code in the dictionary, for a reader that remembers
    /// the codes of the bytes it has decoded.</summary>
    internal int AppendText(ReadOnlySpan<char> value)
    {
        Room(1);
        var code = Interner.Intern(value);
        Texts.AddCode(code);
        return code;
    }

    /// <summary>Appends a code <see cref="AppendText"/> returned.</summary>
    internal void AppendCode(int code)
    {
        Room(1);
        Texts.AddCode(code);
    }

    /// <summary>
    /// Appends codes <see cref="Interner"/> gave, and -1 for a Blank, as <see cref="AppendCode"/> would
    /// one at a time, looking at the segment once per run rather than once per code.
    /// <paramref name="mayHoldBlanks"/> is false only when no code is -1.
    /// </summary>
    internal void AppendCodes(ReadOnlySpan<int> codes, bool mayHoldBlanks)
    {
        var done = 0;
        while (done < codes.Length)
        {
            var n = Room(codes.Length - done);
            Texts.AddCodes(codes.Slice(done, n), mayHoldBlanks);
            done += n;
        }
    }

    /// <summary>
    /// Appends values given as codes into another producer's <paramref name="dictionary"/>, as Arrow
    /// and Parquet hold text. They are taken under the Snapshot's rules, whatever the producer's were:
    /// the Snapshot's dictionary is in the order values first appear in these rows, not the
    /// producer's order; an entry the rows never use is not taken; one text at two codes is one entry;
    /// two spellings are two. The code -1 and a <see langword="null"/> entry are Blanks. A code outside
    /// the dictionary fails the load, naming the row and the column.
    /// </summary>
    /// <exception cref="SnapshotException">A code lies outside the dictionary.</exception>
    public void AppendCodes(ReadOnlySpan<int> codes, IReadOnlyList<string?> dictionary)
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        var remap = new int[dictionary.Count];
        remap.AsSpan().Fill(-2);
        var done = 0;
        while (done < codes.Length)
        {
            var n = Room(codes.Length - done);
            var tail = Texts.Tail(n);
            var blanks = false;
            for (var i = 0; i < n; i++)
            {
                var code = codes[done + i];
                if (code == -1)
                {
                    tail[i] = -1;
                    blanks = true;
                    continue;
                }
                if ((uint)code >= (uint)remap.Length)
                {
                    Texts.Commit(i, blanks);
                    throw Refuse(Count, string.Create(CultureInfo.InvariantCulture,
                        $"the code {code} lies outside the dictionary's {remap.Length:N0} entries."));
                }
                var mapped = remap[code];
                if (mapped == -2)
                {
                    mapped = dictionary[code] is { } text ? Interner.Intern(text) : -1;
                    remap[code] = mapped;
                }
                blanks |= mapped < 0;
                tail[i] = mapped;
            }
            Texts.Commit(n, blanks);
            done += n;
        }
    }
}

/// <summary>
/// Appends Decimal values, held exactly (ADR-0063). The values are held, not the scale each was
/// written with: <c>1.5</c> and <c>1.50</c> are stored alike.
/// </summary>
public sealed class DecimalColumnBuilder : ColumnBuilder
{
    internal DecimalColumnBuilder(SnapshotColumnsBuilder owner, string name, string? caption)
        : base(owner, name, caption, SnapshotKind.Decimal, new DecimalColumnWriter())
    {
    }

    private DecimalColumnWriter Numbers => (DecimalColumnWriter)Writer;

    /// <summary>Appends a value.</summary>
    public void Append(decimal value)
    {
        Room(1);
        Span<int> bits = stackalloc int[4];
        Numbers.Add(value, bits);
    }

    /// <summary>Appends a value, or a Blank for <see langword="null"/>.</summary>
    public void Append(decimal? value)
    {
        if (value.HasValue)
            Append(value.GetValueOrDefault());
        else
            AppendBlank();
    }

    /// <summary>Appends values, with a Blank wherever <paramref name="blanks"/> has its bit set.</summary>
    public void Append(ReadOnlySpan<decimal> values, ReadOnlySpan<ulong> blanks = default)
    {
        CheckBlanks(blanks, values.Length);
        Span<int> bits = stackalloc int[4];
        for (var i = 0; i < values.Length; i++)
        {
            Room(1);
            if (IsBlank(blanks, i))
                Numbers.AddBlank();
            else
                Numbers.Add(values[i], bits);
        }
    }

    /// <summary>
    /// Appends values given as integers scaled by a power of ten — each is
    /// <c>value</c> × 10^-<paramref name="scale"/> — as Arrow's and a database's decimals are held, with
    /// a Blank wherever <paramref name="blanks"/> has its bit set.
    /// </summary>
    public void AppendScaled(ReadOnlySpan<long> values, int scale, ReadOnlySpan<ulong> blanks = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(scale);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(scale, 28);
        CheckBlanks(blanks, values.Length);
        for (var i = 0; i < values.Length; i++)
        {
            Room(1);
            if (IsBlank(blanks, i))
                Numbers.AddBlank();
            else
                Numbers.AddScaled(values[i], scale);
        }
    }

    /// <summary>Appends <paramref name="value"/> × 10^-<paramref name="scale"/>, a scale the caller has
    /// checked lies within 0 to 28.</summary>
    internal void AppendScaled(long value, int scale)
    {
        Room(1);
        Numbers.AddScaled(value, scale);
    }

    /// <summary>The scale by which <see cref="AppendRows"/> is told a row is a Blank.</summary>
    internal const byte BlankRow = 0xFF;

    /// <summary>The scale by which <see cref="AppendRows"/> is told a row is a <see cref="decimal"/>.</summary>
    internal const byte ExactRow = 0xFE;

    /// <summary>
    /// Appends rows as <see cref="ColumnBuilder.AppendBlank"/>, <see cref="AppendScaled(long, int)"/>
    /// and <see cref="Append(decimal)"/> would, one after another, looking at the segment once per run:
    /// row i is a Blank when <c>scales[i]</c> is <see cref="BlankRow"/>, <c>exacts[i]</c> when it is
    /// <see cref="ExactRow"/>, and <c>values[i]</c> × 10^-<c>scales[i]</c> otherwise, a scale within 0 to 28.
    /// </summary>
    internal void AppendRows(ReadOnlySpan<long> values, ReadOnlySpan<byte> scales, ReadOnlySpan<decimal> exacts)
    {
        Span<int> bits = stackalloc int[4];
        var done = 0;
        while (done < values.Length)
        {
            var end = done + Room(values.Length - done);
            var numbers = Numbers;
            for (var i = done; i < end; i++)
            {
                var scale = scales[i];
                if (scale == BlankRow)
                    numbers.AddBlank();
                else if (scale == ExactRow)
                    numbers.Add(exacts[i], bits);
                else
                    numbers.AddScaled(values[i], scale);
            }
            done = end;
        }
    }
}

/// <summary>Appends Double values, each held exactly as it comes, non-finite values included.</summary>
public sealed class DoubleColumnBuilder : ColumnBuilder
{
    private static readonly SpanWrite<double> Write = (writer, values) => ((DoubleColumnWriter)writer).AddRange(values);

    internal DoubleColumnBuilder(SnapshotColumnsBuilder owner, string name, string? caption)
        : base(owner, name, caption, SnapshotKind.Double, new DoubleColumnWriter())
    {
    }

    /// <summary>Appends a value.</summary>
    public void Append(double value)
    {
        Room(1);
        ((DoubleColumnWriter)Writer).Add(value);
    }

    /// <summary>Appends a value, or a Blank for <see langword="null"/>.</summary>
    public void Append(double? value)
    {
        if (value.HasValue)
            Append(value.GetValueOrDefault());
        else
            AppendBlank();
    }

    /// <summary>Appends values, with a Blank wherever <paramref name="blanks"/> has its bit set.</summary>
    public void Append(ReadOnlySpan<double> values, ReadOnlySpan<ulong> blanks = default) => AppendRange(values, blanks, Write);
}

/// <summary>Appends Integer values: 64-bit integers.</summary>
public sealed class IntegerColumnBuilder : ColumnBuilder
{
    private static readonly SpanWrite<long> Write = (writer, values) => ((IntegerColumnWriter)writer).AddRange(values);

    internal IntegerColumnBuilder(SnapshotColumnsBuilder owner, string name, string? caption)
        : base(owner, name, caption, SnapshotKind.Integer, new IntegerColumnWriter())
    {
    }

    /// <summary>Appends a value.</summary>
    public void Append(long value)
    {
        Room(1);
        ((IntegerColumnWriter)Writer).Add(value);
    }

    /// <summary>Appends a value, or a Blank for <see langword="null"/>.</summary>
    public void Append(long? value)
    {
        if (value.HasValue)
            Append(value.GetValueOrDefault());
        else
            AppendBlank();
    }

    /// <summary>Appends values, with a Blank wherever <paramref name="blanks"/> has its bit set.</summary>
    public void Append(ReadOnlySpan<long> values, ReadOnlySpan<ulong> blanks = default) => AppendRange(values, blanks, Write);
}

/// <summary>
/// Appends Date values, each held as the clock value it shows (ADR-0063): a <see cref="DateTime"/>'s
/// ticks with its <see cref="DateTime.Kind"/> ignored, a <see cref="DateOnly"/>'s midnight, a
/// <see cref="DateTimeOffset"/>'s clock with its offset dropped.
/// </summary>
public sealed class DateColumnBuilder : ColumnBuilder
{
    internal DateColumnBuilder(SnapshotColumnsBuilder owner, string name, string? caption)
        : base(owner, name, caption, SnapshotKind.Date, new DateColumnWriter())
    {
    }

    private DateColumnWriter Dates => (DateColumnWriter)Writer;

    /// <summary>Appends a <see cref="DateTime"/>, as its ticks.</summary>
    public void Append(DateTime value)
    {
        Room(1);
        Dates.Add(value.Ticks);
    }

    /// <summary>Appends a <see cref="DateOnly"/>, as its midnight.</summary>
    public void Append(DateOnly value)
    {
        Room(1);
        Dates.Add(Clock.Ticks(value));
    }

    /// <summary>Appends a <see cref="DateTimeOffset"/>, as the clock it shows.</summary>
    public void Append(DateTimeOffset value)
    {
        Room(1);
        Dates.Add(value.Ticks);
    }

    /// <summary>Appends a clock value given as ticks (<see cref="DateTime.Ticks"/>). Ticks outside
    /// <see cref="DateTime"/>'s range fail the load, naming the row and the column.</summary>
    /// <exception cref="SnapshotException">The ticks lie outside <see cref="DateTime"/>'s range.</exception>
    public void AppendTicks(long ticks)
    {
        Room(1);
        if ((ulong)ticks > (ulong)DateTime.MaxValue.Ticks)
            throw Refuse(Count, OutOfRange(ticks));
        Dates.Add(ticks);
    }

    /// <summary>Appends clock values given as ticks, with a Blank wherever <paramref name="blanks"/> has
    /// its bit set. Ticks outside <see cref="DateTime"/>'s range fail the load, naming the row and the
    /// column.</summary>
    /// <exception cref="SnapshotException">Some ticks lie outside <see cref="DateTime"/>'s range.</exception>
    public void AppendTicks(ReadOnlySpan<long> ticks, ReadOnlySpan<ulong> blanks = default)
    {
        CheckBlanks(blanks, ticks.Length);
        // As AppendBlank and AppendTicks(long) would append them one at a time, looking at the segment
        // once per run.
        var done = 0;
        while (done < ticks.Length)
        {
            var end = done + Room(ticks.Length - done);
            var dates = Dates;
            for (var i = done; i < end; i++)
            {
                if (IsBlank(blanks, i))
                {
                    dates.AddBlank();
                    continue;
                }
                var value = ticks[i];
                if ((ulong)value > (ulong)DateTime.MaxValue.Ticks)
                    throw Refuse(Count, OutOfRange(value));
                dates.Add(value);
            }
            done = end;
        }
    }

    private static string OutOfRange(long ticks)
        => string.Create(CultureInfo.InvariantCulture, $"the ticks {ticks} lie outside the range of a date.");
}

/// <summary>Appends Boolean values.</summary>
public sealed class BooleanColumnBuilder : ColumnBuilder
{
    private static readonly SpanWrite<bool> Write = (writer, values) => ((BooleanColumnWriter)writer).AddRange(values);

    internal BooleanColumnBuilder(SnapshotColumnsBuilder owner, string name, string? caption)
        : base(owner, name, caption, SnapshotKind.Boolean, new BooleanColumnWriter())
    {
    }

    /// <summary>Appends a value.</summary>
    public void Append(bool value)
    {
        Room(1);
        ((BooleanColumnWriter)Writer).Add(value);
    }

    /// <summary>Appends a value, or a Blank for <see langword="null"/>.</summary>
    public void Append(bool? value)
    {
        if (value.HasValue)
            Append(value.GetValueOrDefault());
        else
            AppendBlank();
    }

    /// <summary>Appends values, with a Blank wherever <paramref name="blanks"/> has its bit set.</summary>
    public void Append(ReadOnlySpan<bool> values, ReadOnlySpan<ulong> blanks = default) => AppendRange(values, blanks, Write);
}
