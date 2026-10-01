using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

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

    private readonly Vector128<byte> separatorVector;
    private readonly Vector128<byte> crVector = Vector128.Create(Cr);
    private readonly Vector128<byte> lfVector = Vector128.Create(Lf);
    private readonly Vector128<byte> quoteVector = Vector128.Create(Quote);

    private int[] starts = new int[16];
    private int[] lengths = new int[16];
    private byte[] flags = new byte[16];

    /// <summary>Where the fields of the record last cut begin among those held.</summary>
    private int first;

    /// <summary>The fields held: those of the records appended since <see cref="Clear"/>.</summary>
    private int held;

    public CsvTokenizer(byte separator)
    {
        this.separator = separator;
        separatorVector = Vector128.Create(separator);
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

    /// <summary>Where each field held begins in the bytes cut, record after record.</summary>
    public ReadOnlySpan<int> Starts => starts.AsSpan(0, held);

    /// <summary>The length of each field held.</summary>
    public ReadOnlySpan<int> Lengths => lengths.AsSpan(0, held);

    /// <summary>Whether each field held is quoted, and holds a doubled quote.</summary>
    public ReadOnlySpan<byte> FieldFlags => flags.AsSpan(0, held);

    public int Start(int field) => starts[first + field];

    public int Length(int field) => lengths[first + field];

    public byte Flags(int field) => flags[first + field];

    /// <summary>Lets go of the fields held, to append the records of a new batch.</summary>
    public void Clear() => held = 0;

    /// <summary>
    /// Cuts the record that begins at <paramref name="pos"/> in <paramref name="data"/>. When
    /// <paramref name="final"/>, the data ends where <paramref name="data"/> does; otherwise more may
    /// follow, and a record that reaches the end is not cut until it has. <paramref name="next"/> is
    /// where the next record begins, when a record was cut. The fields held before are let go.
    /// </summary>
    public CutResult Cut(ReadOnlySpan<byte> data, int pos, bool final, out int next)
    {
        held = 0;
        return Append(data, pos, final, out next);
    }

    /// <summary>
    /// Cuts the record that begins at <paramref name="pos"/>, as <see cref="Cut"/> does, and holds its
    /// fields after those of the records appended before it. A record not cut leaves those as they were.
    /// </summary>
    public CutResult Append(ReadOnlySpan<byte> data, int pos, bool final, out int next)
    {
        next = pos;
        if (pos >= data.Length)
            return final ? CutResult.End : CutResult.NeedMore;
        FieldCount = 0;
        Breaks = 0;
        Error = CsvFault.None;
        first = held;
        // The scans below look at sixteen bytes at a time where the hardware can, and at one byte at a
        // time for the last few: the same bytes are found either way. They are written out in this
        // one method, rather than called, because a browser runs .NET in an interpreter, where a call
        // costs more than the comparisons it would save (ticket 07's profile).
        ref var origin = ref MemoryMarshal.GetReference(data);
        var end = data.Length;
        var stop = stops;
        var fields = 0;
        var breaks = 0;
        var p = pos;
        while (true)
        {
            int start;
            int length;
            byte flag;
            if (p < end && data[p] == Quote)
            {
                // A quoted field: its content runs to a quote that is not doubled. The line breaks in
                // it are counted on the way, CR LF as one.
                flag = Quoted;
                start = p + 1;
                var q = start;
                while (true)
                {
                    var at = -1;
                    if (Vector128.IsHardwareAccelerated)
                    {
                        while (q + Vector128<byte>.Count <= end)
                        {
                            var bytes = Vector128.LoadUnsafe(ref origin, (nuint)q);
                            var hits = (Vector128.Equals(bytes, quoteVector) | Vector128.Equals(bytes, crVector) | Vector128.Equals(bytes, lfVector)).ExtractMostSignificantBits();
                            if (hits != 0)
                            {
                                at = q + BitOperations.TrailingZeroCount(hits);
                                break;
                            }
                            q += Vector128<byte>.Count;
                        }
                    }
                    if (at < 0)
                    {
                        for (; q < end; q++)
                        {
                            var c = data[q];
                            if (c == Quote || c == Cr || c == Lf)
                            {
                                at = q;
                                break;
                            }
                        }
                        if (at < 0)
                            return final ? Fault(CsvFault.Unclosed, p, fields) : CutResult.NeedMore;
                    }
                    var found = data[at];
                    if (found != Quote)
                    {
                        // CR, LF, or the LF of a CR LF already counted at its CR.
                        if (found == Cr || at == start || data[at - 1] != Cr)
                            breaks++;
                        q = at + 1;
                        continue;
                    }
                    if (at + 1 < end)
                    {
                        if (data[at + 1] != Quote)
                        {
                            q = at;
                            break;
                        }
                        flag = Quoted | Doubled;
                        q = at + 2;
                        continue;
                    }
                    // The quote is the last byte at hand: the next byte says whether it is doubled.
                    if (!final)
                        return CutResult.NeedMore;
                    q = at;
                    break;
                }
                length = q - start;
                p = q + 1;
                if (p < end && stop[data[p]] != 1)
                    return Fault(CsvFault.AfterQuote, p, fields);
            }
            else
            {
                // An unquoted field runs to the separator or a line end; a quote may not stand in it.
                flag = 0;
                start = p;
                var q = p;
                var found = false;
                if (Vector128.IsHardwareAccelerated)
                {
                    while (q + Vector128<byte>.Count <= end)
                    {
                        var bytes = Vector128.LoadUnsafe(ref origin, (nuint)q);
                        var hits = (Vector128.Equals(bytes, separatorVector) | Vector128.Equals(bytes, crVector)
                            | Vector128.Equals(bytes, lfVector) | Vector128.Equals(bytes, quoteVector)).ExtractMostSignificantBits();
                        if (hits != 0)
                        {
                            q += BitOperations.TrailingZeroCount(hits);
                            found = true;
                            break;
                        }
                        q += Vector128<byte>.Count;
                    }
                }
                if (!found)
                {
                    while (q < end && stop[data[q]] == 0)
                        q++;
                }
                if (q < end && data[q] == Quote)
                    return Fault(CsvFault.QuoteInside, q, fields);
                if (q == end && !final)
                    return CutResult.NeedMore;
                length = q - p;
                p = q;
            }

            var index = first + fields;
            if (index == starts.Length)
                Grow();
            starts[index] = start;
            lengths[index] = length;
            flags[index] = flag;
            fields++;
            if (p >= end)
            {
                next = p;
                return Done(fields, breaks);
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
                return Done(fields, breaks);
            }
            // CR, alone or before LF.
            if (p + 1 < end)
            {
                next = data[p + 1] == Lf ? p + 2 : p + 1;
                return Done(fields, breaks);
            }
            if (!final)
                return CutResult.NeedMore;
            next = p + 1;
            return Done(fields, breaks);
        }
    }

    private CutResult Done(int fields, int breaks)
    {
        FieldCount = fields;
        Breaks = breaks;
        held = first + fields;
        return CutResult.Record;
    }

    private CutResult Fault(CsvFault fault, int at, int field)
    {
        FieldCount = field;
        Error = fault;
        ErrorField = field;
        ErrorAt = at;
        return CutResult.Malformed;
    }

    private void Grow()
    {
        var length = starts.Length * 2;
        Array.Resize(ref starts, length);
        Array.Resize(ref lengths, length);
        Array.Resize(ref flags, length);
    }
}
