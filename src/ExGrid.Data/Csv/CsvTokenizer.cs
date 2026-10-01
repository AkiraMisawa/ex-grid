namespace ExGrid.Data.Csv;

/// <summary>What cutting a record from the bytes at hand came to.</summary>
internal enum CutResult
{
    /// <summary>A whole record was cut; its fields are in the tokenizer.</summary>
    Record,

    /// <summary>The bytes end inside a record, or where the next byte decides it: read more and cut
    /// again from the record's start.</summary>
    NeedMore,

    /// <summary>The data has ended, and no record is left.</summary>
    End,

    /// <summary>The record breaks RFC 4180; <see cref="CsvTokenizer.Error"/> says how.</summary>
    Malformed,
}

/// <summary>How a record broke RFC 4180.</summary>
internal enum CsvFault
{
    None,

    /// <summary>A quoted field is still open where the data ends.</summary>
    Unclosed,

    /// <summary>A quote stands inside a field that does not begin with one.</summary>
    QuoteInside,

    /// <summary>A quoted field's closing quote is followed by something other than a separator or a
    /// line end.</summary>
    AfterQuote,
}

/// <summary>
/// Cuts records into fields under RFC 4180, over bytes: a field in double quotes may hold the
/// separator, a line break and a doubled quote; a record ends at CR LF, LF or CR outside quotes. It
/// keeps only where each field lies, so no text is made; a record cut short by the end of the bytes
/// at hand is cut again, from its start, once more bytes have come.
/// <para>
/// The separator, CR, LF and the quote are ASCII, and in UTF-8 and Shift-JIS no byte of a multi-byte
/// character is any of them, so the bytes are cut without decoding them.
/// </para>
/// </summary>
internal sealed class CsvTokenizer
{
    public const byte Quote = (byte)'"';
    public const byte Cr = (byte)'\r';
    public const byte Lf = (byte)'\n';

    /// <summary>A field is in quotes.</summary>
    public const byte Quoted = 1;

    /// <summary>A quoted field holds a doubled quote, which stands for one.</summary>
    public const byte Doubled = 2;

    private readonly byte separator;

    /// <summary>For each byte: whether it ends an unquoted field (1) or may not stand in one (2).</summary>
    private readonly byte[] stops = new byte[256];

    private int[] starts = new int[16];
    private int[] lengths = new int[16];
    private byte[] flags = new byte[16];

    public CsvTokenizer(byte separator)
    {
        this.separator = separator;
        stops[separator] = 1;
        stops[Cr] = 1;
        stops[Lf] = 1;
        stops[Quote] = 2;
    }

    /// <summary>The fields of the record last cut.</summary>
    public int FieldCount { get; private set; }

    /// <summary>The line breaks inside the quoted fields of the record last cut.</summary>
    public int Breaks { get; private set; }

    /// <summary>How the record last cut broke RFC 4180, when it did.</summary>
    public CsvFault Error { get; private set; }

    /// <summary>The field where the record broke RFC 4180, counted from zero.</summary>
    public int ErrorField { get; private set; }

    /// <summary>Where, in the bytes cut, the record broke RFC 4180.</summary>
    public int ErrorAt { get; private set; }

    public int Start(int field) => starts[field];

    public int Length(int field) => lengths[field];

    public byte Flags(int field) => flags[field];

    /// <summary>
    /// Cuts the record that begins at <paramref name="pos"/> in <paramref name="data"/>. When
    /// <paramref name="final"/>, the data ends where <paramref name="data"/> does; otherwise more may
    /// follow, and a record that reaches the end is not cut until it has. <paramref name="next"/> is
    /// where the next record begins, when a record was cut.
    /// </summary>
    public CutResult Cut(ReadOnlySpan<byte> data, int pos, bool final, out int next)
    {
        next = pos;
        if (pos >= data.Length)
            return final ? CutResult.End : CutResult.NeedMore;
        FieldCount = 0;
        Breaks = 0;
        Error = CsvFault.None;
        var p = pos;
        while (true)
        {
            int start;
            int length;
            byte flag = 0;
            if (p < data.Length && data[p] == Quote)
            {
                var q = p + 1;
                while (true)
                {
                    var found = data[q..].IndexOf(Quote);
                    if (found < 0)
                        return final ? Fault(CsvFault.Unclosed, p) : CutResult.NeedMore;
                    q += found;
                    if (q + 1 < data.Length)
                    {
                        if (data[q + 1] != Quote)
                            break;
                        flag = Doubled;
                        q += 2;
                        continue;
                    }
                    // The quote is the last byte at hand: the next byte says whether it is doubled.
                    if (!final)
                        return CutResult.NeedMore;
                    break;
                }
                start = p + 1;
                length = q - start;
                flag |= Quoted;
                Breaks += CountBreaks(data.Slice(start, length));
                p = q + 1;
                if (p < data.Length && stops[data[p]] != 1)
                    return Fault(CsvFault.AfterQuote, p);
            }
            else
            {
                var q = p;
                var stop = stops;
                while (q < data.Length && stop[data[q]] == 0)
                    q++;
                if (q < data.Length && data[q] == Quote)
                    return Fault(CsvFault.QuoteInside, q);
                if (q == data.Length && !final)
                    return CutResult.NeedMore;
                start = p;
                length = q - p;
                p = q;
            }

            Add(start, length, flag);
            if (p >= data.Length)
            {
                next = p;
                return CutResult.Record;
            }
            var b = data[p];
            if (b == separator)
            {
                p++;
                continue;
            }
            if (b == Lf)
            {
                next = p + 1;
                return CutResult.Record;
            }
            // CR, alone or before LF.
            if (p + 1 < data.Length)
            {
                next = data[p + 1] == Lf ? p + 2 : p + 1;
                return CutResult.Record;
            }
            if (!final)
                return CutResult.NeedMore;
            next = p + 1;
            return CutResult.Record;
        }
    }

    /// <summary>The line breaks in a quoted field's content: CR LF, LF and CR each count as one.</summary>
    private static int CountBreaks(ReadOnlySpan<byte> content)
    {
        var breaks = 0;
        var at = content.IndexOfAny(Cr, Lf);
        while (at >= 0)
        {
            breaks++;
            if (content[at] == Cr && at + 1 < content.Length && content[at + 1] == Lf)
                at++;
            content = content[(at + 1)..];
            at = content.IndexOfAny(Cr, Lf);
        }
        return breaks;
    }

    private CutResult Fault(CsvFault fault, int at)
    {
        Error = fault;
        ErrorField = FieldCount;
        ErrorAt = at;
        return CutResult.Malformed;
    }

    private void Add(int start, int length, byte flag)
    {
        var field = FieldCount;
        if (field == starts.Length)
        {
            Array.Resize(ref starts, field * 2);
            Array.Resize(ref lengths, field * 2);
            Array.Resize(ref flags, field * 2);
        }
        starts[field] = start;
        lengths[field] = length;
        flags[field] = flag;
        FieldCount = field + 1;
    }
}
