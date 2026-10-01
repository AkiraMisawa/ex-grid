using System.Globalization;

namespace ExGrid.Data.Csv;

/// <summary>What a field reader needs of the load it reads for: the encoding, and how to word a
/// refusal of the record at hand.</summary>
internal interface IFieldContext
{
    CsvEncoding Encoding { get; }

    /// <summary>A refusal of the record being read, in <paramref name="column"/>; the reason is a
    /// sentence without its full stop.</summary>
    SnapshotException Refuse(string? column, string reason);
}

/// <summary>
/// Reads one declared column's field of each record into its column builder: an empty field, or one
/// of the column's blank texts, as a Blank, and any other as a value of the column's kind, or a
/// refusal naming the row and the column.
/// </summary>
internal abstract class FieldReader
{
    private readonly byte[][] blanks;

    protected FieldReader(CsvColumn column, IReadOnlyList<string> blankTexts, IFieldContext context, bool isKey)
    {
        Name = column.Name;
        Context = context;
        IsKey = isKey;
        blanks = [.. blankTexts.Where(t => t.Length > 0).Select(t => CsvText.Encode(context.Encoding, t)).OfType<byte[]>()];
    }

    public string Name { get; }

    protected IFieldContext Context { get; }

    private bool IsKey { get; }

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

    /// <summary>Reads a field's content — unquoted, and with doubled quotes made single.</summary>
    public void Read(ReadOnlySpan<byte> field)
    {
        if (field.IsEmpty || IsBlankText(field))
        {
            if (IsKey)
                throw Context.Refuse(Name, "the Record Key is Blank");
            AppendBlank();
            return;
        }
        ReadValue(field);
    }

    protected abstract void AppendBlank();

    protected abstract void ReadValue(ReadOnlySpan<byte> field);

    protected string Show(ReadOnlySpan<byte> field) => CsvText.Show(Context.Encoding, field);

    private bool IsBlankText(ReadOnlySpan<byte> field)
    {
        foreach (var blank in blanks)
        {
            if (field.SequenceEqual(blank))
                return true;
        }
        return false;
    }
}

/// <summary>Text, exactly as written, into the column's dictionary; bytes seen before are not decoded
/// again.</summary>
internal sealed class TextFieldReader(CsvColumn column, IReadOnlyList<string> blankTexts, TextColumnBuilder builder, IFieldContext context, bool isKey)
    : FieldReader(column, blankTexts, context, isKey)
{
    private readonly ByteTextCache cache = new();
    private char[] chars = new char[64];

    protected override void AppendBlank() => builder.AppendBlank();

    protected override void ReadValue(ReadOnlySpan<byte> field)
    {
        if (cache.Enabled && cache.TryGet(field, out var code))
        {
            builder.AppendCode(code);
            return;
        }
        if (!CsvText.TryDecode(Context.Encoding, field, ref chars, out var length))
            throw Context.Refuse(Name, $"the text is not valid {Context.Encoding.Name}");
        code = builder.AppendText(chars.AsSpan(0, length));
        if (cache.Enabled)
            cache.Add(field, code);
    }
}

/// <summary>A Decimal, held exactly: as a scaled long while it fits one, as a <see cref="decimal"/>
/// otherwise, and refused when it has more digits or places than a decimal holds.</summary>
internal sealed class DecimalFieldReader(CsvColumn column, IReadOnlyList<string> blankTexts, DecimalColumnBuilder builder, IFieldContext context)
    : FieldReader(column, blankTexts, context, false)
{
    private readonly NumberReading reading = new(column, context.Encoding);

    protected override void AppendBlank() => builder.AppendBlank();

    protected override void ReadValue(ReadOnlySpan<byte> field)
    {
        var value = CsvText.Trim(field);
        var status = NumberText.Parse(value, reading, out var number);
        if (status == NumberStatus.Ok)
        {
            if (number.Scale <= 28 && number.TryLong(out var scaled))
            {
                builder.AppendScaled(scaled, number.Scale);
                return;
            }
            if (number.TryDecimal(out var exact))
            {
                builder.Append(exact);
                return;
            }
        }
        throw status == NumberStatus.NotANumber
            ? Context.Refuse(Name, $"{Show(field)} is not a number")
            : Context.Refuse(Name, $"{Show(field)} has more digits than a Decimal holds exactly");
    }
}

/// <summary>An Integer: a 64-bit integer, written without a decimal point.</summary>
internal sealed class IntegerFieldReader(CsvColumn column, IReadOnlyList<string> blankTexts, IntegerColumnBuilder builder, IFieldContext context, bool isKey)
    : FieldReader(column, blankTexts, context, isKey)
{
    private readonly NumberReading reading = new(column, context.Encoding);

    protected override void AppendBlank() => builder.AppendBlank();

    protected override void ReadValue(ReadOnlySpan<byte> field)
    {
        var value = CsvText.Trim(field);
        var status = NumberText.Parse(value, reading, out var number);
        if (status != NumberStatus.NotANumber && !number.HasPoint)
        {
            if (status == NumberStatus.Ok && number.TryLong(out var integer))
            {
                builder.Append(integer);
                return;
            }
            throw Context.Refuse(Name, $"{Show(field)} is outside the range of a 64-bit Integer");
        }
        throw Context.Refuse(Name, $"{Show(field)} is not an integer");
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

    protected override void AppendBlank() => builder.AppendBlank();

    protected override void ReadValue(ReadOnlySpan<byte> field)
    {
        var value = CsvText.Trim(field);
        double number;
        if (reading.Plain)
        {
            if (!double.TryParse(value, NumberText.DoubleStyle, CultureInfo.InvariantCulture, out number))
                throw Context.Refuse(Name, $"{Show(field)} is not a number");
        }
        else
        {
            if (scratch.Length < value.Length + 1)
                scratch = new byte[(value.Length + 1) * 2];
            if (NumberText.Normalize(value, reading, scratch, out var written))
            {
                if (!double.TryParse(scratch.AsSpan(0, written), NumberText.DoubleStyle, CultureInfo.InvariantCulture, out number))
                    throw Context.Refuse(Name, $"{Show(field)} is not a number");
            }
            else if (!(double.TryParse(value, NumberText.DoubleStyle, CultureInfo.InvariantCulture, out number) && !double.IsFinite(number)))
            {
                // Only NaN and Infinity are read past the column's separators.
                throw Context.Refuse(Name, $"{Show(field)} is not a number");
            }
        }
        if (double.IsInfinity(number) && HasDigit(value))
            throw Context.Refuse(Name, $"{Show(field)} is outside the range of a Double");
        builder.Append(number);
    }

    private static bool HasDigit(ReadOnlySpan<byte> value) => value.IndexOfAnyInRange((byte)'0', (byte)'9') >= 0;
}

/// <summary>A Date, under the column's formats tried in order, held as the clock value it shows.</summary>
internal sealed class DateFieldReader : FieldReader
{
    private readonly DateColumnBuilder builder;
    private readonly DateFormat[] formats;
    private char[] chars = new char[32];

    public DateFieldReader(CsvColumn column, IReadOnlyList<string> blankTexts, DateColumnBuilder builder, IFieldContext context)
        : base(column, blankTexts, context, false)
    {
        this.builder = builder;
        var culture = column.Culture ?? CultureInfo.InvariantCulture;
        formats = [.. (column.DateFormats ?? CsvColumn.IsoDateFormats).Select(f => new DateFormat(f, culture))];
    }

    protected override void AppendBlank() => builder.AppendBlank();

    protected override void ReadValue(ReadOnlySpan<byte> field)
    {
        var value = CsvText.Trim(field);
        var length = -1;
        foreach (var format in formats)
        {
            long ticks;
            if (format.ReadsBytes)
            {
                if (!format.TryRead(value, out ticks))
                    continue;
            }
            else
            {
                if (length < 0 && !CsvText.TryDecode(Context.Encoding, value, ref chars, out length))
                    break;
                if (!format.TryRead(chars.AsSpan(0, length), out ticks))
                    continue;
            }
            builder.AppendTicks(ticks);
            return;
        }
        throw Context.Refuse(Name, formats.Length == 1
            ? $"{Show(field)} is not a date in the format '{formats[0].Format}'"
            : $"{Show(field)} is not a date in any of the formats {string.Join(", ", formats.Select(f => $"'{f.Format}'"))}");
    }
}

/// <summary>A Boolean, by the column's spellings of true and false, matched ignoring case.</summary>
internal sealed class BooleanFieldReader(CsvColumn column, IReadOnlyList<string> blankTexts, BooleanColumnBuilder builder, IFieldContext context)
    : FieldReader(column, blankTexts, context, false)
{
    private readonly string[] trues = [.. column.TrueText ?? CsvColumn.ExcelTrue];
    private readonly string[] falses = [.. column.FalseText ?? CsvColumn.ExcelFalse];
    private char[] chars = new char[16];

    protected override void AppendBlank() => builder.AppendBlank();

    protected override void ReadValue(ReadOnlySpan<byte> field)
    {
        var value = CsvText.Trim(field);
        if (CsvText.TryDecode(Context.Encoding, value, ref chars, out var length))
        {
            var text = chars.AsSpan(0, length);
            if (Matches(text, trues))
            {
                builder.Append(true);
                return;
            }
            if (Matches(text, falses))
            {
                builder.Append(false);
                return;
            }
        }
        throw Context.Refuse(Name, $"{Show(field)} is not a spelling of true or false ({string.Join(", ", trues.Concat(falses).Select(s => $"'{s}'"))})");
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
