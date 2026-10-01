using System.Globalization;
using ExGrid.Data.Storage;

namespace ExGrid.Data.Csv;

/// <summary>What a field reader needs of the load it reads for: the encoding, and how to word a
/// refusal of a record of the batch at hand.</summary>
internal interface IFieldContext
{
    CsvEncoding Encoding { get; }

    /// <summary>A refusal of the batch's record <paramref name="row"/>, counted from zero, in
    /// <paramref name="column"/>; the reason is a sentence without its full stop.</summary>
    SnapshotException Refuse(int row, string? column, string reason);
}

/// <summary>
/// The fields of a batch of records, as the tokenizer holds them, over the bytes they were cut from:
/// field <c>f</c> of record <c>r</c> is at <c>r × Width + f</c>, since every record of a batch has
/// the same number of fields.
/// </summary>
internal readonly ref struct CsvRecords
{
    public CsvRecords(ReadOnlySpan<byte> data, CsvTokenizer tokenizer, int width)
    {
        Data = data;
        Starts = tokenizer.Starts;
        Lengths = tokenizer.Lengths;
        Flags = tokenizer.FieldFlags;
        Width = width;
    }

    public ReadOnlySpan<byte> Data { get; }

    public ReadOnlySpan<int> Starts { get; }

    public ReadOnlySpan<int> Lengths { get; }

    public ReadOnlySpan<byte> Flags { get; }

    public int Width { get; }
}

/// <summary>
/// Reads one declared column's field of each record of a batch into its column builder: an empty
/// field, or one of the column's blank texts, as a Blank, and any other as a value of the column's
/// kind, or a refusal naming the row and the column. A batch is read column by column, each column
/// in one loop over the records, and appended to the builder at once (ticket 07): a browser runs .NET
/// in an interpreter, where a call per field cost more than the reading.
/// </summary>
internal abstract class FieldReader
{
    private readonly byte[][] blanks;
    private byte[] unescaped = new byte[64];
    private ulong[] blankBits = [];

    protected FieldReader(CsvColumn column, IReadOnlyList<string> blankTexts, IFieldContext context, bool isKey)
    {
        Name = column.Name;
        Context = context;
        IsKey = isKey;
        blanks = [.. blankTexts.Where(t => t.Length > 0).Select(t => CsvText.Encode(context.Encoding, t)).OfType<byte[]>()];
    }

    public string Name { get; }

    protected IFieldContext Context { get; }

    protected bool IsKey { get; }

    public static FieldReader Create(CsvColumn column, IReadOnlyList<string> blankTexts, ColumnBuilder builder, IFieldContext context, bool isKey)
        => column.Kind switch
        {
            SnapshotKind.Text => new TextFieldReader(column, blankTexts, (TextColumnBuilder)builder, context, isKey),
            SnapshotKind.Decimal => new DecimalFieldReader(column, blankTexts, (DecimalColumnBuilder)builder, context),
            SnapshotKind.Double => new DoubleFieldReader(column, blankTexts, (DoubleColumnBuilder)builder, context),
            SnapshotKind.Integer => new IntegerFieldReader(column, blankTexts, (IntegerColumnBuilder)builder, context, isKey),
            SnapshotKind.Date => new DateFieldReader(column, blankTexts, (DateColumnBuilder)builder, context),
            SnapshotKind.Boolean => new BooleanFieldReader(column, blankTexts, (BooleanColumnBuilder)builder, context),
            _ => throw new ArgumentOutOfRangeException(nameof(column), column.Kind, "Not a kind a Snapshot holds."),
        };

    /// <summary>
    /// Reads field <paramref name="field"/> of the batch's first <paramref name="rows"/> records, in
    /// order, and returns how many were read: <paramref name="rows"/>, once they are appended to the
    /// column, or the batch row of the first one refused, with <paramref name="refusal"/> saying why
    /// and nothing appended.
    /// </summary>
    public abstract int Read(in CsvRecords records, int field, int rows, out SnapshotException? refusal);

    /// <summary>A field's content: what lies between its quotes, with a doubled quote made single.</summary>
    protected ReadOnlySpan<byte> Content(in CsvRecords records, int index)
    {
        var content = records.Data.Slice(records.Starts[index], records.Lengths[index]);
        return (records.Flags[index] & CsvTokenizer.Doubled) == 0 ? content : Unescape(content);
    }

    /// <summary>Whether a field's content is a Blank: empty, or one of the column's blank texts.</summary>
    protected bool IsBlank(ReadOnlySpan<byte> content)
    {
        if (content.IsEmpty)
            return true;
        foreach (var blank in blanks)
        {
            if (content.SequenceEqual(blank))
                return true;
        }
        return false;
    }

    /// <summary>A field's content without the ASCII spaces around it, which the kinds other than Text
    /// set aside; <paramref name="content"/> is not empty.</summary>
    protected static ReadOnlySpan<byte> Trimmed(ReadOnlySpan<byte> content)
        => content[0] != (byte)' ' && content[^1] != (byte)' ' ? content : CsvText.Trim(content);

    /// <summary>The refusal of a Blank where the Record Key is read.</summary>
    protected SnapshotException BlankKey(int row) => Context.Refuse(row, Name, "the Record Key is Blank");

    protected string Show(ReadOnlySpan<byte> field) => CsvText.Show(Context.Encoding, field);

    /// <summary>Room for a batch of <paramref name="rows"/> in a scratch array the reader keeps.</summary>
    protected static TValue[] Room<TValue>(ref TValue[] array, int rows)
    {
        if (array.Length < rows)
            array = new TValue[Math.Max(rows, array.Length * 2)];
        return array;
    }

    /// <summary>The bits that mark the Blanks of a batch of <paramref name="rows"/>, all clear.</summary>
    protected ulong[] ClearBlanks(int rows)
    {
        var words = Bits.Words(rows);
        if (blankBits.Length < words)
            blankBits = new ulong[words];
        else
            Array.Clear(blankBits, 0, words);
        return blankBits;
    }

    /// <summary>The Blanks of a batch of <paramref name="rows"/>, as <see cref="ClearBlanks"/> gave them and the reader set them.</summary>
    protected ReadOnlySpan<ulong> Blanks(int rows) => blankBits.AsSpan(0, Bits.Words(rows));

    private ReadOnlySpan<byte> Unescape(ReadOnlySpan<byte> content)
    {
        if (unescaped.Length < content.Length)
            unescaped = new byte[content.Length * 2];
        var written = 0;
        for (var i = 0; i < content.Length; i++)
        {
            unescaped[written++] = content[i];
            if (content[i] == CsvTokenizer.Quote)
                i++;
        }
        return unescaped.AsSpan(0, written);
    }
}

/// <summary>Text, exactly as written, into the column's dictionary; bytes seen before are not decoded
/// again.</summary>
internal sealed class TextFieldReader(CsvColumn column, IReadOnlyList<string> blankTexts, TextColumnBuilder builder, IFieldContext context, bool isKey)
    : FieldReader(column, blankTexts, context, isKey)
{
    private readonly ByteTextCache cache = new();
    private char[] chars = new char[64];
    private int[] codes = [];

    public override int Read(in CsvRecords records, int field, int rows, out SnapshotException? refusal)
    {
        var width = records.Width;
        var batch = Room(ref codes, rows);
        var anyBlank = false;
        for (var r = 0; r < rows; r++)
        {
            var value = Content(records, (r * width) + field);
            if (IsBlank(value))
            {
                if (IsKey)
                {
                    refusal = BlankKey(r);
                    return r;
                }
                batch[r] = -1;
                anyBlank = true;
                continue;
            }
            if (cache.Enabled && cache.TryGet(value, out var code))
            {
                batch[r] = code;
                continue;
            }
            if (!CsvText.TryDecode(Context.Encoding, value, ref chars, out var length))
            {
                refusal = Context.Refuse(r, Name, $"the text is not valid {Context.Encoding.Name}");
                return r;
            }
            code = builder.Interner.Intern(chars.AsSpan(0, length));
            batch[r] = code;
            if (cache.Enabled)
                cache.Add(value, code);
        }
        builder.AppendCodes(batch.AsSpan(0, rows), anyBlank);
        refusal = null;
        return rows;
    }
}

/// <summary>A Decimal, held exactly: as a scaled long while it fits one, as a <see cref="decimal"/>
/// otherwise, and refused when it has more digits or places than a decimal holds.</summary>
internal sealed class DecimalFieldReader(CsvColumn column, IReadOnlyList<string> blankTexts, DecimalColumnBuilder builder, IFieldContext context)
    : FieldReader(column, blankTexts, context, false)
{
    private readonly NumberReading reading = new(column, context.Encoding);
    private long[] values = [];
    private byte[] scales = [];
    private decimal[] exacts = [];

    public override int Read(in CsvRecords records, int field, int rows, out SnapshotException? refusal)
    {
        var width = records.Width;
        var batch = Room(ref values, rows);
        var batchScales = Room(ref scales, rows);
        var shortcut = reading.Short;
        for (var r = 0; r < rows; r++)
        {
            var content = Content(records, (r * width) + field);
            if (IsBlank(content))
            {
                batchScales[r] = DecimalColumnBuilder.BlankRow;
                continue;
            }
            var value = Trimmed(content);
            if (shortcut && NumberText.TryParseShort(value, reading, out var scaled, out var scale, out _))
            {
                batch[r] = scaled;
                batchScales[r] = (byte)scale;
                continue;
            }
            var status = NumberText.Parse(value, reading, out var number);
            if (status == NumberStatus.Ok)
            {
                if (number.Scale <= 28 && number.TryLong(out scaled))
                {
                    batch[r] = scaled;
                    batchScales[r] = (byte)number.Scale;
                    continue;
                }
                if (number.TryDecimal(out var exact))
                {
                    Room(ref exacts, rows)[r] = exact;
                    batchScales[r] = DecimalColumnBuilder.ExactRow;
                    continue;
                }
            }
            refusal = status == NumberStatus.NotANumber
                ? Context.Refuse(r, Name, $"{Show(content)} is not a number")
                : Context.Refuse(r, Name, $"{Show(content)} has more digits than a Decimal holds exactly");
            return r;
        }
        builder.AppendRows(batch.AsSpan(0, rows), batchScales.AsSpan(0, rows), exacts);
        refusal = null;
        return rows;
    }
}

/// <summary>An Integer: a 64-bit integer, written without a decimal point.</summary>
internal sealed class IntegerFieldReader(CsvColumn column, IReadOnlyList<string> blankTexts, IntegerColumnBuilder builder, IFieldContext context, bool isKey)
    : FieldReader(column, blankTexts, context, isKey)
{
    private readonly NumberReading reading = new(column, context.Encoding);
    private long[] values = [];

    public override int Read(in CsvRecords records, int field, int rows, out SnapshotException? refusal)
    {
        var width = records.Width;
        var batch = Room(ref values, rows);
        var blankBits = ClearBlanks(rows);
        var shortcut = reading.Short;
        for (var r = 0; r < rows; r++)
        {
            var content = Content(records, (r * width) + field);
            if (IsBlank(content))
            {
                if (IsKey)
                {
                    refusal = BlankKey(r);
                    return r;
                }
                Bits.Set(blankBits, r);
                batch[r] = 0;
                continue;
            }
            var value = Trimmed(content);
            if (shortcut && NumberText.TryParseShort(value, reading, out var read, out _, out var hasPoint) && !hasPoint)
            {
                batch[r] = read;
                continue;
            }
            var status = NumberText.Parse(value, reading, out var number);
            if (status != NumberStatus.NotANumber && !number.HasPoint)
            {
                if (status == NumberStatus.Ok && number.TryLong(out var integer))
                {
                    batch[r] = integer;
                    continue;
                }
                refusal = Context.Refuse(r, Name, $"{Show(content)} is outside the range of a 64-bit Integer");
                return r;
            }
            refusal = Context.Refuse(r, Name, $"{Show(content)} is not an integer");
            return r;
        }
        builder.Append(batch.AsSpan(0, rows), Blanks(rows));
        refusal = null;
        return rows;
    }
}

/// <summary>A Double, as .NET reads one under the invariant culture once the column's separators are
/// taken out: an exponent is read, and so are NaN and Infinity; a finite number too large for a
/// double is refused rather than read as infinity.</summary>
internal sealed class DoubleFieldReader(CsvColumn column, IReadOnlyList<string> blankTexts, DoubleColumnBuilder builder, IFieldContext context)
    : FieldReader(column, blankTexts, context, false)
{
    private readonly NumberReading reading = new(column, context.Encoding);
    private byte[] scratch = new byte[64];
    private double[] values = [];

    public override int Read(in CsvRecords records, int field, int rows, out SnapshotException? refusal)
    {
        var width = records.Width;
        var batch = Room(ref values, rows);
        var blankBits = ClearBlanks(rows);
        for (var r = 0; r < rows; r++)
        {
            var content = Content(records, (r * width) + field);
            if (IsBlank(content))
            {
                Bits.Set(blankBits, r);
                batch[r] = 0;
                continue;
            }
            var reason = TryRead(content, out var number);
            if (reason is not null)
            {
                refusal = Context.Refuse(r, Name, reason);
                return r;
            }
            batch[r] = number;
        }
        builder.Append(batch.AsSpan(0, rows), Blanks(rows));
        refusal = null;
        return rows;
    }

    /// <summary>Reads a field's content; the reason it cannot be read, or <see langword="null"/>.</summary>
    private string? TryRead(ReadOnlySpan<byte> content, out double number)
    {
        var value = CsvText.Trim(content);
        if (reading.Plain)
        {
            if (!double.TryParse(value, NumberText.DoubleStyle, CultureInfo.InvariantCulture, out number))
                return $"{Show(content)} is not a number";
        }
        else
        {
            if (scratch.Length < value.Length + 1)
                scratch = new byte[(value.Length + 1) * 2];
            if (NumberText.Normalize(value, reading, scratch, out var written))
            {
                if (!double.TryParse(scratch.AsSpan(0, written), NumberText.DoubleStyle, CultureInfo.InvariantCulture, out number))
                    return $"{Show(content)} is not a number";
            }
            else if (!(double.TryParse(value, NumberText.DoubleStyle, CultureInfo.InvariantCulture, out number) && !double.IsFinite(number)))
            {
                // Only NaN and Infinity are read past the column's separators.
                return $"{Show(content)} is not a number";
            }
        }
        if (double.IsInfinity(number) && HasDigit(value))
            return $"{Show(content)} is outside the range of a Double";
        return null;
    }

    private static bool HasDigit(ReadOnlySpan<byte> value) => value.IndexOfAnyInRange((byte)'0', (byte)'9') >= 0;
}

/// <summary>A Date, under the column's formats tried in order, held as the clock value it shows.</summary>
internal sealed class DateFieldReader : FieldReader
{
    private readonly DateColumnBuilder builder;
    private readonly DateFormat[] formats;
    private char[] chars = new char[32];
    private long[] values = [];

    public DateFieldReader(CsvColumn column, IReadOnlyList<string> blankTexts, DateColumnBuilder builder, IFieldContext context)
        : base(column, blankTexts, context, false)
    {
        this.builder = builder;
        var culture = column.Culture ?? CultureInfo.InvariantCulture;
        formats = [.. (column.DateFormats ?? CsvColumn.IsoDateFormats).Select(f => new DateFormat(f, culture))];
    }

    public override int Read(in CsvRecords records, int field, int rows, out SnapshotException? refusal)
    {
        var width = records.Width;
        var batch = Room(ref values, rows);
        var blankBits = ClearBlanks(rows);
        for (var r = 0; r < rows; r++)
        {
            var content = Content(records, (r * width) + field);
            if (IsBlank(content))
            {
                Bits.Set(blankBits, r);
                batch[r] = 0;
                continue;
            }
            if (!TryRead(CsvText.Trim(content), out var ticks))
            {
                refusal = Context.Refuse(r, Name, formats.Length == 1
                    ? $"{Show(content)} is not a date in the format '{formats[0].Format}'"
                    : $"{Show(content)} is not a date in any of the formats {string.Join(", ", formats.Select(f => $"'{f.Format}'"))}");
                return r;
            }
            batch[r] = ticks;
        }
        builder.AppendTicks(batch.AsSpan(0, rows), Blanks(rows));
        refusal = null;
        return rows;
    }

    /// <summary>Reads a trimmed value under the first of the column's formats that reads it.</summary>
    private bool TryRead(ReadOnlySpan<byte> value, out long ticks)
    {
        var length = -1;
        foreach (var format in formats)
        {
            if (format.ReadsBytes)
            {
                if (format.TryRead(value, out ticks))
                    return true;
                continue;
            }
            if (length < 0 && !CsvText.TryDecode(Context.Encoding, value, ref chars, out length))
                break;
            if (format.TryRead(chars.AsSpan(0, length), out ticks))
                return true;
        }
        ticks = 0;
        return false;
    }
}

/// <summary>A Boolean, by the column's spellings of true and false, matched ignoring case.</summary>
internal sealed class BooleanFieldReader(CsvColumn column, IReadOnlyList<string> blankTexts, BooleanColumnBuilder builder, IFieldContext context)
    : FieldReader(column, blankTexts, context, false)
{
    private readonly string[] trues = [.. column.TrueText ?? CsvColumn.ExcelTrue];
    private readonly string[] falses = [.. column.FalseText ?? CsvColumn.ExcelFalse];
    private char[] chars = new char[16];
    private bool[] values = [];

    public override int Read(in CsvRecords records, int field, int rows, out SnapshotException? refusal)
    {
        var width = records.Width;
        var batch = Room(ref values, rows);
        var blankBits = ClearBlanks(rows);
        for (var r = 0; r < rows; r++)
        {
            var content = Content(records, (r * width) + field);
            if (IsBlank(content))
            {
                Bits.Set(blankBits, r);
                batch[r] = false;
                continue;
            }
            var value = CsvText.Trim(content);
            if (CsvText.TryDecode(Context.Encoding, value, ref chars, out var length))
            {
                var text = chars.AsSpan(0, length);
                if (Matches(text, trues))
                {
                    batch[r] = true;
                    continue;
                }
                if (Matches(text, falses))
                {
                    batch[r] = false;
                    continue;
                }
            }
            refusal = Context.Refuse(r, Name, $"{Show(content)} is not a spelling of true or false ({string.Join(", ", trues.Concat(falses).Select(s => $"'{s}'"))})");
            return r;
        }
        builder.Append(batch.AsSpan(0, rows), Blanks(rows));
        refusal = null;
        return rows;
    }

    private static bool Matches(ReadOnlySpan<char> text, string[] spellings)
    {
        foreach (var spelling in spellings)
        {
            if (text.Equals(spelling, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
