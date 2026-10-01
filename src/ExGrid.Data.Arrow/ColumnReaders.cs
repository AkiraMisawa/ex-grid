using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Apache.Arrow;
using Apache.Arrow.Types;

namespace ExGrid.Data.Arrow;

/// <summary>Scratch space a read reuses for every chunk of every column.</summary>
internal sealed class ReadScratch
{
    public ulong[] Blanks { get; } = new ulong[Bitmaps.Words(ArrowSnapshotReader.ChunkRows)];

    public long[] Longs { get; } = new long[ArrowSnapshotReader.ChunkRows];

    public double[] Doubles { get; } = new double[ArrowSnapshotReader.ChunkRows];

    public bool[] Booleans { get; } = new bool[ArrowSnapshotReader.ChunkRows];

    public int[] Codes { get; } = new int[ArrowSnapshotReader.ChunkRows];
}

/// <summary>
/// Reads one Arrow column into one Snapshot column, a chunk of rows at a time, from Arrow's buffers —
/// never through Arrow's per-value accessors, which were measured 10–40 times slower (ADR-0065).
/// A value the column's kind cannot hold fails the load, naming its row and the column.
/// </summary>
internal abstract class ColumnReader(ColumnBuilder column)
{
    /// <summary>UTC's names, any case: the IANA names of UTC, each also under <c>Etc/</c>, and the
    /// offsets and designator that say UTC (ADR-0065).</summary>
    private static readonly string[] UtcZones =
    [
        "UTC", "Etc/UTC", "GMT", "Etc/GMT", "UCT", "Etc/UCT", "Universal", "Etc/Universal", "Zulu", "Etc/Zulu",
        "Greenwich", "Etc/Greenwich", "GMT0", "Etc/GMT0", "GMT+0", "Etc/GMT+0", "GMT-0", "Etc/GMT-0",
        "+00:00", "-00:00", "Z",
    ];

    /// <summary>The column's name.</summary>
    public string Name => column.Name;

    /// <summary>The column's kind.</summary>
    public SnapshotKind Kind => column.Kind;

    /// <summary>
    /// Declares the Snapshot column <paramref name="field"/> maps to, by ADR-0065's table, and returns
    /// what reads it.
    /// </summary>
    /// <exception cref="SnapshotException">The field's type is outside the table, or is a timestamp in a
    /// time zone other than UTC; the refusal names the column and the type.</exception>
    public static ColumnReader For(Field field, string? caption, SnapshotColumnsBuilder builder)
    {
        var name = field.Name;
        var type = field.DataType;
        switch (type.TypeId)
        {
            case ArrowTypeId.String:
                return new Utf8Reader(builder.Text(name, caption), large: false);
            case ArrowTypeId.LargeString:
                return new Utf8Reader(builder.Text(name, caption), large: true);
            case ArrowTypeId.StringView:
                return new Utf8ViewReader(builder.Text(name, caption));
            case ArrowTypeId.Dictionary when type is DictionaryType dictionary
                && dictionary.ValueType.TypeId is ArrowTypeId.String or ArrowTypeId.LargeString or ArrowTypeId.StringView
                && IsIndex(dictionary.IndexType.TypeId):
                return new DictionaryReader(builder.Text(name, caption), dictionary.IndexType.TypeId, dictionary.ValueType.TypeId);
            case ArrowTypeId.Decimal32:
                return new DecimalReader(builder.Decimal(name, caption), width: 4, ((Decimal32Type)type).Scale, ArrowTypeNames.Of(type));
            case ArrowTypeId.Decimal64:
                return new DecimalReader(builder.Decimal(name, caption), width: 8, ((Decimal64Type)type).Scale, ArrowTypeNames.Of(type));
            case ArrowTypeId.Decimal128:
                return new DecimalReader(builder.Decimal(name, caption), width: 16, ((Decimal128Type)type).Scale, ArrowTypeNames.Of(type));
            case ArrowTypeId.Decimal256:
                return new DecimalReader(builder.Decimal(name, caption), width: 32, ((Decimal256Type)type).Scale, ArrowTypeNames.Of(type));
            case ArrowTypeId.Double:
                return new DoubleReader(builder.Double(name, caption), single: false);
            case ArrowTypeId.Float:
                return new DoubleReader(builder.Double(name, caption), single: true);
            case ArrowTypeId.Int8 or ArrowTypeId.Int16 or ArrowTypeId.Int32 or ArrowTypeId.Int64
                or ArrowTypeId.UInt8 or ArrowTypeId.UInt16 or ArrowTypeId.UInt32 or ArrowTypeId.UInt64:
                return new IntegerReader(builder.Integer(name, caption), type.TypeId);
            case ArrowTypeId.Date32:
                return new DateReader(builder.Date(name, caption), DateUnit.Days, "date32");
            case ArrowTypeId.Date64:
                return new DateReader(builder.Date(name, caption), DateUnit.Milliseconds, "date64");
            case ArrowTypeId.Timestamp:
                var timestamp = (TimestampType)type;
                var zone = timestamp.Timezone;
                if (!string.IsNullOrWhiteSpace(zone) && !System.Array.Exists(UtcZones, z => string.Equals(z, zone.Trim(), StringComparison.OrdinalIgnoreCase)))
                {
                    throw new SnapshotException(null, name,
                        $"the Arrow type {ArrowTypeNames.Of(type)} is a timestamp in the time zone '{zone}', which a Snapshot does not read: "
                        + "converting it would need a time zone database. Write it in UTC, or as clock values without a time zone.");
                }
                return new DateReader(builder.Date(name, caption), UnitOf(timestamp.Unit), ArrowTypeNames.Of(type));
            case ArrowTypeId.Time32:
                return new TimeReader(builder.Date(name, caption), wide: false, UnitOf(((Time32Type)type).Unit), ArrowTypeNames.Of(type));
            case ArrowTypeId.Time64:
                return new TimeReader(builder.Date(name, caption), wide: true, UnitOf(((Time64Type)type).Unit), ArrowTypeNames.Of(type));
            case ArrowTypeId.Boolean:
                return new BooleanReader(builder.Boolean(name, caption));
            default:
                throw new SnapshotException(null, name, $"the Arrow type {ArrowTypeNames.Of(type)} is not one a Snapshot reads.");
        }
    }

    /// <summary>Appends rows [<paramref name="start"/>, <paramref name="start"/> + <paramref name="length"/>)
    /// of this column's array in one record batch; <paramref name="rowBase"/> rows came before them.</summary>
    public abstract void Read(ArrayData data, int start, int length, long rowBase, ReadScratch scratch);

    /// <summary>Fails the load, naming the row — the <paramref name="index"/>th of a chunk after
    /// <paramref name="rowBase"/> rows — and this column.</summary>
    protected SnapshotException Refuse(long rowBase, int index, string reason) => new(rowBase + index + 1, Name, reason);

    /// <summary>Fails the load, naming this column, for an array whose buffers do not hold what its
    /// type and length say.</summary>
    protected SnapshotException Malformed(string what) => new(null, Name, $"the Arrow stream is malformed: {what}.");

    /// <summary>The chunk's Blanks from the array's validity bitmap, or empty when it holds none.</summary>
    protected ReadOnlySpan<ulong> BlanksOf(ArrayData data, int start, int length, ReadScratch scratch)
    {
        if (data.NullCount == 0)
            return default;
        var validity = data.Buffers.Length > 0 ? data.Buffers[0] : ArrowBuffer.Empty;
        if (validity.IsEmpty)
            return data.NullCount < 0 ? default : throw Malformed("its nulls have no validity bitmap");
        var bits = validity.Span;
        if (bits.Length * 8L < (long)data.Offset + data.Length)
            throw Malformed("its validity bitmap is shorter than its rows");
        var blanks = scratch.Blanks.AsSpan(0, Bitmaps.Words(length));
        return Bitmaps.BlanksFromValidity(bits, data.Offset + start, length, blanks) ? blanks : default;
    }

    /// <summary>The chunk's values in the array's value buffer, <paramref name="lanes"/> of
    /// <typeparamref name="T"/> to a value.</summary>
    protected ReadOnlySpan<T> ValuesOf<T>(ArrayData data, int start, int length, int lanes = 1)
        where T : unmanaged
    {
        var all = data.Buffers.Length > 1 ? MemoryMarshal.Cast<byte, T>(data.Buffers[1].Span) : default;
        var first = ((long)data.Offset + start) * lanes;
        var count = (long)length * lanes;
        if (first + count > all.Length)
            throw Malformed("its value buffer holds fewer values than its rows");
        return all.Slice((int)first, (int)count);
    }

    private static bool IsIndex(ArrowTypeId type) => type is ArrowTypeId.Int8 or ArrowTypeId.Int16 or ArrowTypeId.Int32 or ArrowTypeId.Int64
        or ArrowTypeId.UInt8 or ArrowTypeId.UInt16 or ArrowTypeId.UInt32 or ArrowTypeId.UInt64;

    private static DateUnit UnitOf(TimeUnit unit) => unit switch
    {
        TimeUnit.Second => DateUnit.Seconds,
        TimeUnit.Millisecond => DateUnit.Milliseconds,
        TimeUnit.Microsecond => DateUnit.Microseconds,
        _ => DateUnit.Nanoseconds,
    };
}

/// <summary><c>utf8</c> and <c>large_utf8</c>: each value's UTF-8 bytes go straight to the column, which
/// makes a string only for text its dictionary does not hold yet.</summary>
internal sealed class Utf8Reader(TextColumnBuilder text, bool large) : ColumnReader(text)
{
    public override void Read(ArrayData data, int start, int length, long rowBase, ReadScratch scratch)
    {
        var blanks = BlanksOf(data, start, length, scratch);
        var bytes = data.Buffers.Length > 2 ? data.Buffers[2].Span : default;
        if (large)
            Append(OffsetsOf<long>(data, start, length), bytes, blanks, rowBase);
        else
            Append(OffsetsOf<int>(data, start, length), bytes, blanks, rowBase);
    }

    private ReadOnlySpan<T> OffsetsOf<T>(ArrayData data, int start, int length)
        where T : unmanaged
    {
        var all = data.Buffers.Length > 1 ? MemoryMarshal.Cast<byte, T>(data.Buffers[1].Span) : default;
        var first = (long)data.Offset + start;
        if (first + length + 1 > all.Length)
            throw Malformed("its offsets are fewer than its rows");
        return all.Slice((int)first, length + 1);
    }

    private void Append<T>(ReadOnlySpan<T> offsets, ReadOnlySpan<byte> bytes, ReadOnlySpan<ulong> blanks, long rowBase)
        where T : unmanaged, IBinaryInteger<T>
    {
        for (var i = 0; i < offsets.Length - 1; i++)
        {
            if (Bitmaps.Get(blanks, i))
            {
                text.AppendBlank();
                continue;
            }
            var from = long.CreateTruncating(offsets[i]);
            var to = long.CreateTruncating(offsets[i + 1]);
            if (from < 0 || to < from || to > bytes.Length)
                throw Refuse(rowBase, i, "the Arrow stream is malformed: the value's offsets lie outside its data.");
            text.AppendUtf8(bytes.Slice((int)from, (int)(to - from)));
        }
    }
}

/// <summary>
/// Arrow's views of text, <c>utf8_view</c>: sixteen bytes a value, the first four its length in bytes.
/// Text of twelve bytes or fewer follows in the view itself. Longer text lies in one of the array's
/// data buffers, which follow its validity bitmap and its views; the view goes on with the text's
/// first four bytes, which data buffer holds it, and where in that buffer it starts.
/// </summary>
internal static class TextViews
{
    /// <summary>The bytes of one view.</summary>
    public const int Size = 16;

    /// <summary>The most bytes of text a view holds itself.</summary>
    private const int Inline = 12;

    /// <summary>
    /// <paramref name="data"/>'s views, and whether they reach as far as view <paramref name="end"/>,
    /// counted from the buffer's start — the array's offset included, as a view's index is.
    /// </summary>
    public static bool TryViews(ArrayData data, long end, out ReadOnlySpan<byte> views)
    {
        views = data.Buffers.Length > 1 ? data.Buffers[1].Span : default;
        return end * Size <= views.Length;
    }

    /// <summary>
    /// The UTF-8 bytes view <paramref name="index"/> of <paramref name="views"/> holds or points to —
    /// <paramref name="index"/> counted from the buffer's start, within what <see cref="TryViews"/>
    /// checked — or false when the view's length is negative or it points outside the array's data.
    /// </summary>
    public static bool TryText(ArrayData data, ReadOnlySpan<byte> views, int index, out ReadOnlySpan<byte> text)
    {
        var view = views.Slice(index * Size, Size);
        var length = BinaryPrimitives.ReadInt32LittleEndian(view);
        text = default;
        if (length < 0)
            return false;
        if (length <= Inline)
        {
            text = view.Slice(4, length);
            return true;
        }
        var buffer = BinaryPrimitives.ReadInt32LittleEndian(view[8..]);
        var offset = BinaryPrimitives.ReadInt32LittleEndian(view[12..]);
        if (buffer < 0 || buffer >= data.Buffers.Length - 2 || offset < 0)
            return false;
        var bytes = data.Buffers[2 + buffer].Span;
        if (length > bytes.Length - offset)
            return false;
        text = bytes.Slice(offset, length);
        return true;
    }
}

/// <summary><c>utf8_view</c>, as Polars writes text by default: each value's UTF-8 bytes, in its view or
/// in the data buffer the view points into, go straight to the column, which makes a string only for
/// text its dictionary does not hold yet.</summary>
internal sealed class Utf8ViewReader(TextColumnBuilder text) : ColumnReader(text)
{
    public override void Read(ArrayData data, int start, int length, long rowBase, ReadScratch scratch)
    {
        var blanks = BlanksOf(data, start, length, scratch);
        var first = data.Offset + start;
        if (!TextViews.TryViews(data, (long)first + length, out var views))
            throw Malformed("its views are fewer than its rows");
        for (var i = 0; i < length; i++)
        {
            if (Bitmaps.Get(blanks, i))
            {
                text.AppendBlank();
                continue;
            }
            if (!TextViews.TryText(data, views, first + i, out var bytes))
                throw Refuse(rowBase, i, "the Arrow stream is malformed: the value's view points outside its data.");
            text.AppendUtf8(bytes);
        }
    }
}

/// <summary>
/// A dictionary of <c>utf8</c>, <c>large_utf8</c> or <c>utf8_view</c>, with indices of any integer type,
/// taken under the Snapshot's rules whatever order, case or nulls the producer's dictionary has
/// (ADR-0064/0065): the Snapshot's dictionary is in the order values first appear in the rows, one
/// entry per exact text, and a null index or a null entry is a Blank.
/// <para>
/// Each chunk's indices are first turned into codes into a small list of the entries the chunk uses,
/// which the column then interns. Each entry is decoded once for each dictionary the stream sends,
/// so a dictionary replaced or extended between record batches — Arrow's replacement and delta
/// dictionary batches — costs what the rows use of it, never its whole size again.
/// </para>
/// </summary>
internal sealed class DictionaryReader(TextColumnBuilder text, ArrowTypeId indexType, ArrowTypeId valueType) : ColumnReader(text)
{
    private static readonly UTF8Encoding Strict = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly List<string?> used = [];
    private ArrayData? entries;
    private int entryCount;
    private int dictionaryGeneration;
    private int chunkGeneration;
    private int[] usedIn = [];
    private int[] usedCode = [];
    private string?[] decoded = [];
    private int[] decodedIn = [];

    public override void Read(ArrayData data, int start, int length, long rowBase, ReadScratch scratch)
    {
        var dictionary = data.Dictionary ?? throw Malformed("a dictionary-encoded column has no dictionary");
        if (!ReferenceEquals(dictionary, entries))
            Adopt(dictionary);
        chunkGeneration++;
        used.Clear();
        var blanks = BlanksOf(data, start, length, scratch);
        var codes = scratch.Codes.AsSpan(0, length);
        switch (indexType)
        {
            case ArrowTypeId.Int8:
                Map(ValuesOf<sbyte>(data, start, length), blanks, codes, rowBase);
                break;
            case ArrowTypeId.Int16:
                Map(ValuesOf<short>(data, start, length), blanks, codes, rowBase);
                break;
            case ArrowTypeId.Int32:
                Map(ValuesOf<int>(data, start, length), blanks, codes, rowBase);
                break;
            case ArrowTypeId.Int64:
                Map(ValuesOf<long>(data, start, length), blanks, codes, rowBase);
                break;
            case ArrowTypeId.UInt8:
                Map(ValuesOf<byte>(data, start, length), blanks, codes, rowBase);
                break;
            case ArrowTypeId.UInt16:
                Map(ValuesOf<ushort>(data, start, length), blanks, codes, rowBase);
                break;
            case ArrowTypeId.UInt32:
                Map(ValuesOf<uint>(data, start, length), blanks, codes, rowBase);
                break;
            default:
                Map(ValuesOf<ulong>(data, start, length), blanks, codes, rowBase);
                break;
        }
        text.AppendCodes(codes, used);
    }

    private void Adopt(ArrayData dictionary)
    {
        if (valueType == ArrowTypeId.StringView)
        {
            if (!TextViews.TryViews(dictionary, (long)dictionary.Offset + dictionary.Length, out _))
                throw Malformed("its dictionary's views are fewer than its entries");
        }
        else
        {
            var offsets = dictionary.Buffers.Length > 1 ? dictionary.Buffers[1].Length / (valueType == ArrowTypeId.LargeString ? 8 : 4) : 0;
            if (dictionary.Length > 0 && (long)dictionary.Offset + dictionary.Length + 1 > offsets)
                throw Malformed("its dictionary's offsets are fewer than its entries");
        }
        entries = dictionary;
        entryCount = dictionary.Length;
        dictionaryGeneration++;
        if (usedIn.Length < entryCount)
        {
            var size = (int)Math.Min(System.Array.MaxLength, Math.Max(entryCount, usedIn.Length * 2L));
            System.Array.Resize(ref usedIn, size);
            System.Array.Resize(ref usedCode, size);
            System.Array.Resize(ref decoded, size);
            System.Array.Resize(ref decodedIn, size);
        }
    }

    private void Map<T>(ReadOnlySpan<T> indices, ReadOnlySpan<ulong> blanks, Span<int> codes, long rowBase)
        where T : unmanaged, IBinaryInteger<T>
    {
        var count = (ulong)entryCount;
        for (var i = 0; i < indices.Length; i++)
        {
            if (Bitmaps.Get(blanks, i))
            {
                codes[i] = -1;
                continue;
            }
            // A negative index sign-extends past every count.
            var index = ulong.CreateTruncating(indices[i]);
            if (index >= count)
            {
                throw Refuse(rowBase, i, string.Create(CultureInfo.InvariantCulture,
                    $"the dictionary index {indices[i]} lies outside the dictionary's {count:N0} entries."));
            }
            var k = (int)index;
            if (usedIn[k] != chunkGeneration)
            {
                usedIn[k] = chunkGeneration;
                usedCode[k] = used.Count;
                used.Add(Entry(k, rowBase, i));
            }
            codes[i] = usedCode[k];
        }
    }

    /// <summary>Dictionary entry <paramref name="k"/>'s text, or null for a null entry; decoded once per dictionary.</summary>
    private string? Entry(int k, long rowBase, int i)
    {
        if (decodedIn[k] == dictionaryGeneration)
            return decoded[k];
        var dictionary = entries!;
        var at = dictionary.Offset + k;
        string? value = null;
        var validity = dictionary.Buffers.Length > 0 ? dictionary.Buffers[0] : ArrowBuffer.Empty;
        if (dictionary.NullCount != 0 && !validity.IsEmpty && validity.Length * 8L <= at)
            throw Malformed("its dictionary's validity bitmap is shorter than its entries");
        var isNull = dictionary.NullCount != 0 && !validity.IsEmpty && !Bitmaps.GetByte(validity.Span, at);
        if (!isNull)
        {
            if (!TryBytes(dictionary, at, out var bytes))
            {
                var where = valueType == ArrowTypeId.StringView ? "view points" : "offsets lie";
                throw Refuse(rowBase, i, string.Create(CultureInfo.InvariantCulture, $"the Arrow stream is malformed: the dictionary's entry {k}'s {where} outside its data."));
            }
            try
            {
                value = Strict.GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                throw Refuse(rowBase, i, string.Create(CultureInfo.InvariantCulture, $"the dictionary's entry {k} is not valid UTF-8."));
            }
        }
        decoded[k] = value;
        decodedIn[k] = dictionaryGeneration;
        return value;
    }

    /// <summary>The UTF-8 bytes of the dictionary's entry at <paramref name="at"/>, its offset included,
    /// by its offsets or its view; false when they lie outside its data.</summary>
    private bool TryBytes(ArrayData dictionary, int at, out ReadOnlySpan<byte> bytes)
    {
        if (valueType == ArrowTypeId.StringView)
            return TextViews.TryText(dictionary, dictionary.Buffers[1].Span, at, out bytes);
        long from, to;
        if (valueType == ArrowTypeId.LargeString)
        {
            var offsets = MemoryMarshal.Cast<byte, long>(dictionary.Buffers[1].Span);
            (from, to) = (offsets[at], offsets[at + 1]);
        }
        else
        {
            var offsets = MemoryMarshal.Cast<byte, int>(dictionary.Buffers[1].Span);
            (from, to) = (offsets[at], offsets[at + 1]);
        }
        var data = dictionary.Buffers.Length > 2 ? dictionary.Buffers[2].Span : default;
        bytes = default;
        if (from < 0 || to < from || to > data.Length)
            return false;
        bytes = data.Slice((int)from, (int)(to - from));
        return true;
    }
}

/// <summary>
/// <c>decimal32</c>, <c>decimal64</c>, <c>decimal128</c> and <c>decimal256</c> — a value
/// <paramref name="width"/> bytes wide — held exactly (ADR-0064). A chunk whose values all fit a
/// 64-bit integer — every <c>decimal32</c> and <c>decimal64</c> does; of the wider ones, those whose
/// high words are only the low word's sign — at a scale a decimal holds goes in as scaled integers in
/// one bulk append; otherwise each value is made a <see cref="decimal"/> exactly, or refused by row
/// and column when no decimal holds it.
/// </summary>
internal sealed class DecimalReader(DecimalColumnBuilder number, int width, int scale, string typeName) : ColumnReader(number)
{
    private static readonly UInt128 DecimalLimit = UInt128.One << 96;
    private static readonly BigInteger BigLimit = BigInteger.One << 96;

    /// <summary>The 64-bit words of a value: one for <c>decimal32</c>, widened, and <c>decimal64</c>.</summary>
    private readonly int lanes = Math.Max(1, width / 8);

    public override void Read(ArrayData data, int start, int length, long rowBase, ReadScratch scratch)
    {
        var blanks = BlanksOf(data, start, length, scratch);
        var words = width == 4 ? Widen(ValuesOf<int>(data, start, length), scratch.Longs.AsSpan(0, length)) : ValuesOf<long>(data, start, length, lanes);
        if (scale is >= 0 and <= 28 && (lanes == 1 || FitLongs(words, blanks, length)))
        {
            if (lanes == 1)
            {
                number.AppendScaled(words, scale, blanks);
                return;
            }
            var longs = scratch.Longs.AsSpan(0, length);
            for (var i = 0; i < length; i++)
                longs[i] = words[i * lanes];
            number.AppendScaled(longs, scale, blanks);
            return;
        }
        for (var i = 0; i < length; i++)
        {
            if (Bitmaps.Get(blanks, i))
                number.AppendBlank();
            else
                number.Append(ToDecimal(words.Slice(i * lanes, lanes), rowBase, i));
        }
    }

    /// <summary><c>decimal32</c>'s values as 64-bit words, in <paramref name="longs"/>.</summary>
    private static ReadOnlySpan<long> Widen(ReadOnlySpan<int> values, Span<long> longs)
    {
        for (var i = 0; i < values.Length; i++)
            longs[i] = values[i];
        return longs;
    }

    /// <summary>Whether every value that is not a Blank is its low word, sign-extended.</summary>
    private bool FitLongs(ReadOnlySpan<long> words, ReadOnlySpan<ulong> blanks, int length)
    {
        for (var i = 0; i < length; i++)
        {
            if (Bitmaps.Get(blanks, i))
                continue;
            var at = i * lanes;
            var sign = words[at] >> 63;
            for (var lane = 1; lane < lanes; lane++)
            {
                if (words[at + lane] != sign)
                    return false;
            }
        }
        return true;
    }

    private decimal ToDecimal(ReadOnlySpan<long> value, long rowBase, int i)
    {
        // A one-word value's high word is its sign.
        var high = lanes == 1 ? value[0] >> 63 : value[1];
        var sign = high >> 63;
        var fits128 = lanes <= 2 || (value[2] == sign && value[3] == sign);
        if (fits128)
        {
            var whole = new Int128((ulong)high, (ulong)value[0]);
            var negative = Int128.IsNegative(whole);
            var magnitude = negative ? (UInt128)(-(whole + 1)) + 1 : (UInt128)whole;
            if (TryToDecimal(magnitude, negative, scale, out var result, out var places))
                return result;
            throw Refusal(whole.ToString(CultureInfo.InvariantCulture), places, rowBase, i);
        }
        var big = new BigInteger(MemoryMarshal.AsBytes(value), isUnsigned: false, isBigEndian: false);
        if (TryToDecimal(BigInteger.Abs(big), big.Sign < 0, scale, out var exact, out var tooManyPlaces))
            return exact;
        throw Refusal(big.ToString(CultureInfo.InvariantCulture), tooManyPlaces, rowBase, i);
    }

    private SnapshotException Refusal(string unscaled, bool places, long rowBase, int i)
        => Refuse(rowBase, i, places
            ? $"the {typeName} value {Show(unscaled, scale)} has more decimal places than the 28 a decimal holds."
            : $"the {typeName} value {Show(unscaled, scale)} lies beyond the range of a decimal.");

    /// <summary>± <paramref name="magnitude"/> × 10^-<paramref name="places"/> as a decimal, exactly; false,
    /// saying whether it was the places, when no decimal holds it.</summary>
    private static bool TryToDecimal(UInt128 magnitude, bool negative, int places, out decimal value, out bool tooManyPlaces)
    {
        value = 0m;
        tooManyPlaces = false;
        for (; places < 0; places++)
        {
            if (magnitude > UInt128.MaxValue / 10)
                return false;
            magnitude *= 10;
        }
        while (places > 28 || magnitude >= DecimalLimit)
        {
            if (places == 0 || magnitude % 10 != 0)
            {
                tooManyPlaces = places > 28 && magnitude < DecimalLimit;
                return false;
            }
            magnitude /= 10;
            places--;
        }
        value = new decimal((int)(uint)magnitude, (int)(uint)(magnitude >> 32), (int)(uint)(magnitude >> 64), negative && magnitude != 0, (byte)places);
        return true;
    }

    private static bool TryToDecimal(BigInteger magnitude, bool negative, int places, out decimal value, out bool tooManyPlaces)
    {
        value = 0m;
        tooManyPlaces = false;
        if (places < 0)
        {
            magnitude *= BigInteger.Pow(10, -places);
            places = 0;
        }
        while (places > 28 || magnitude >= BigLimit)
        {
            if (places == 0)
                return false;
            var quotient = BigInteger.DivRem(magnitude, 10, out var remainder);
            if (!remainder.IsZero)
            {
                tooManyPlaces = places > 28 && magnitude < BigLimit;
                return false;
            }
            magnitude = quotient;
            places--;
        }
        return TryToDecimal((UInt128)magnitude, negative, places, out value, out tooManyPlaces);
    }

    /// <summary>An unscaled integer's digits with the decimal point put in at <paramref name="places"/>.</summary>
    private static string Show(string unscaled, int places)
    {
        var negative = unscaled.StartsWith('-');
        var digits = negative ? unscaled[1..] : unscaled;
        if (places > 0)
        {
            digits = digits.PadLeft(places + 1, '0');
            digits = digits[..^places] + "." + digits[^places..];
        }
        else if (places < 0)
        {
            digits += new string('0', -places);
        }
        return negative ? "-" + digits : digits;
    }
}

/// <summary><c>float64</c>, read in place, and <c>float32</c>, widened exactly; non-finite values are kept.</summary>
internal sealed class DoubleReader(DoubleColumnBuilder number, bool single) : ColumnReader(number)
{
    public override void Read(ArrayData data, int start, int length, long rowBase, ReadScratch scratch)
    {
        var blanks = BlanksOf(data, start, length, scratch);
        if (!single)
        {
            number.Append(ValuesOf<double>(data, start, length), blanks);
            return;
        }
        var values = ValuesOf<float>(data, start, length);
        var doubles = scratch.Doubles.AsSpan(0, length);
        for (var i = 0; i < length; i++)
            doubles[i] = values[i];
        number.Append(doubles, blanks);
    }
}

/// <summary>
/// <c>int8</c> to <c>int64</c> and <c>uint8</c> to <c>uint64</c>, as 64-bit integers; a <c>uint64</c>
/// above <see cref="long.MaxValue"/> is refused by row and column.
/// </summary>
internal sealed class IntegerReader(IntegerColumnBuilder number, ArrowTypeId type) : ColumnReader(number)
{
    public override void Read(ArrayData data, int start, int length, long rowBase, ReadScratch scratch)
    {
        var blanks = BlanksOf(data, start, length, scratch);
        if (type == ArrowTypeId.Int64)
        {
            number.Append(ValuesOf<long>(data, start, length), blanks);
            return;
        }
        var longs = scratch.Longs.AsSpan(0, length);
        switch (type)
        {
            case ArrowTypeId.Int8:
                Widen(ValuesOf<sbyte>(data, start, length), longs);
                break;
            case ArrowTypeId.Int16:
                Widen(ValuesOf<short>(data, start, length), longs);
                break;
            case ArrowTypeId.Int32:
                Widen(ValuesOf<int>(data, start, length), longs);
                break;
            case ArrowTypeId.UInt8:
                Widen(ValuesOf<byte>(data, start, length), longs);
                break;
            case ArrowTypeId.UInt16:
                Widen(ValuesOf<ushort>(data, start, length), longs);
                break;
            case ArrowTypeId.UInt32:
                Widen(ValuesOf<uint>(data, start, length), longs);
                break;
            default:
                var values = ValuesOf<ulong>(data, start, length);
                for (var i = 0; i < length; i++)
                {
                    if (values[i] > long.MaxValue && !Bitmaps.Get(blanks, i))
                    {
                        throw Refuse(rowBase, i, string.Create(CultureInfo.InvariantCulture,
                            $"the uint64 value {values[i]} lies beyond the range of a 64-bit integer."));
                    }
                    longs[i] = (long)values[i];
                }
                break;
        }
        number.Append(longs, blanks);
    }

    private static void Widen<T>(ReadOnlySpan<T> values, Span<long> longs)
        where T : unmanaged, IBinaryInteger<T>
    {
        for (var i = 0; i < values.Length; i++)
            longs[i] = long.CreateTruncating(values[i]);
    }
}

/// <summary>How a date column's Arrow values count: from 1970-01-01 for a date or a timestamp, from
/// midnight for a time.</summary>
internal enum DateUnit
{
    Days,
    Seconds,
    Milliseconds,
    Microseconds,
    Nanoseconds,
}

/// <summary>A <see cref="DateUnit"/>'s ticks and name.</summary>
internal static class DateUnits
{
    /// <summary>The ticks in one unit. A nanosecond is a hundredth of a tick, so 1 stands for it, and a
    /// reader divides nanoseconds by 100 instead of multiplying.</summary>
    public static long TicksPer(DateUnit unit) => unit switch
    {
        DateUnit.Days => TimeSpan.TicksPerDay,
        DateUnit.Seconds => TimeSpan.TicksPerSecond,
        DateUnit.Milliseconds => TimeSpan.TicksPerMillisecond,
        DateUnit.Microseconds => TimeSpan.TicksPerMicrosecond,
        _ => 1,
    };

    /// <summary>The unit's name, counted: <c>days</c>, <c>seconds</c>, and so on.</summary>
    public static string Counted(DateUnit unit) => unit switch
    {
        DateUnit.Days => "days",
        DateUnit.Seconds => "seconds",
        DateUnit.Milliseconds => "milliseconds",
        DateUnit.Microseconds => "microseconds",
        _ => "nanoseconds",
    };

    /// <summary>Why a nanosecond value that is not a whole number of ticks is refused.</summary>
    public static string Finer(string typeName, long nanoseconds)
        => string.Create(CultureInfo.InvariantCulture, $"the {typeName} value {nanoseconds} is finer than the 100 nanoseconds a date holds.");
}

/// <summary>
/// <c>date32</c>, <c>date64</c>, and a <c>timestamp</c> without a time zone or in UTC, as the clock
/// value written (ADR-0065). A value outside a date's range, or a nanosecond timestamp finer than the
/// 100 nanoseconds a date holds, is refused by row and column rather than moved.
/// </summary>
internal sealed class DateReader(DateColumnBuilder date, DateUnit unit, string typeName) : ColumnReader(date)
{
    private static readonly long EpochTicks = DateTime.UnixEpoch.Ticks;

    private readonly long ticksPerUnit = DateUnits.TicksPer(unit);

    public override void Read(ArrayData data, int start, int length, long rowBase, ReadScratch scratch)
    {
        var blanks = BlanksOf(data, start, length, scratch);
        var ticks = scratch.Longs.AsSpan(0, length);
        if (unit == DateUnit.Days)
        {
            var days = ValuesOf<int>(data, start, length);
            for (var i = 0; i < length; i++)
            {
                if (!Bitmaps.Get(blanks, i))
                    ticks[i] = Ticks(days[i], rowBase, i);
            }
        }
        else
        {
            var values = ValuesOf<long>(data, start, length);
            for (var i = 0; i < length; i++)
            {
                if (!Bitmaps.Get(blanks, i))
                    ticks[i] = unit == DateUnit.Nanoseconds ? FromNanoseconds(values[i], rowBase, i) : Ticks(values[i], rowBase, i);
            }
        }
        date.AppendTicks(ticks, blanks);
    }

    private long Ticks(long value, long rowBase, int i)
    {
        var min = -(EpochTicks / ticksPerUnit);
        var max = (DateTime.MaxValue.Ticks - EpochTicks) / ticksPerUnit;
        if (value < min || value > max)
        {
            throw Refuse(rowBase, i, string.Create(CultureInfo.InvariantCulture,
                $"the {typeName} value {value} ({DateUnits.Counted(unit)} since 1970-01-01) lies outside the range of a date, 0001-01-01 to 9999-12-31."));
        }
        return (value * ticksPerUnit) + EpochTicks;
    }

    private long FromNanoseconds(long nanoseconds, long rowBase, int i)
    {
        if (nanoseconds % 100 != 0)
            throw Refuse(rowBase, i, DateUnits.Finer(typeName, nanoseconds));
        return (nanoseconds / 100) + EpochTicks;
    }
}

/// <summary>
/// <c>time32</c> and <c>time64</c>, a time of day, as the clock time on the first day a date holds,
/// 0001-01-01 — as a database's <see cref="TimeOnly"/> is read (ADR-0064/0065). Each is read by its
/// own width (<paramref name="wide"/> for <c>time64</c>) and unit. A value outside a day, or a
/// nanosecond value finer than the 100 nanoseconds a date holds, is refused by row and column rather
/// than moved.
/// </summary>
internal sealed class TimeReader(DateColumnBuilder date, bool wide, DateUnit unit, string typeName) : ColumnReader(date)
{
    private readonly long ticksPerUnit = DateUnits.TicksPer(unit);

    /// <summary>The units in a day: a time of day is at least 0 and fewer than these.</summary>
    private readonly long perDay = unit == DateUnit.Nanoseconds ? TimeSpan.TicksPerDay * 100 : TimeSpan.TicksPerDay / DateUnits.TicksPer(unit);

    public override void Read(ArrayData data, int start, int length, long rowBase, ReadScratch scratch)
    {
        var blanks = BlanksOf(data, start, length, scratch);
        var ticks = scratch.Longs.AsSpan(0, length);
        if (wide)
        {
            var values = ValuesOf<long>(data, start, length);
            for (var i = 0; i < length; i++)
            {
                if (!Bitmaps.Get(blanks, i))
                    ticks[i] = Ticks(values[i], rowBase, i);
            }
        }
        else
        {
            var values = ValuesOf<int>(data, start, length);
            for (var i = 0; i < length; i++)
            {
                if (!Bitmaps.Get(blanks, i))
                    ticks[i] = Ticks(values[i], rowBase, i);
            }
        }
        date.AppendTicks(ticks, blanks);
    }

    private long Ticks(long value, long rowBase, int i)
    {
        if (value < 0 || value >= perDay)
        {
            throw Refuse(rowBase, i, string.Create(CultureInfo.InvariantCulture,
                $"the {typeName} value {value} ({DateUnits.Counted(unit)} since midnight) lies outside a day, 00:00:00 to 23:59:59.9999999."));
        }
        if (unit != DateUnit.Nanoseconds)
            return value * ticksPerUnit;
        if (value % 100 != 0)
            throw Refuse(rowBase, i, DateUnits.Finer(typeName, value));
        return value / 100;
    }
}

/// <summary><c>bool</c>: Arrow's bits, unpacked.</summary>
internal sealed class BooleanReader(BooleanColumnBuilder flag) : ColumnReader(flag)
{
    public override void Read(ArrayData data, int start, int length, long rowBase, ReadScratch scratch)
    {
        var blanks = BlanksOf(data, start, length, scratch);
        var bits = data.Buffers.Length > 1 ? data.Buffers[1].Span : default;
        var first = data.Offset + start;
        if (bits.Length * 8L < (long)first + length)
            throw Malformed("its value bitmap is shorter than its rows");
        var values = scratch.Booleans.AsSpan(0, length);
        for (var i = 0; i < length; i++)
            values[i] = Bitmaps.GetByte(bits, first + i);
        flag.Append(values, blanks);
    }
}
