using System.Globalization;

namespace ExGrid.Data.Csv;

/// <summary>
/// Reads a stream under a Schema into a <see cref="SnapshotColumnsBuilder"/> (ADR-0064): the bytes
/// are read into a buffer, cut into records, and each declared field read straight into its column.
/// The load works in batches of records, checking in with the builder between them, which reports
/// progress and yields once a slice has spent its budget.
/// </summary>
internal static class CsvLoad
{
    /// <summary>
    /// The bytes read at a time, at most. A record longer than this grows the buffer. Large, because a
    /// browser application's file comes through InputFile's stream, which makes a call into JavaScript
    /// for every read: measured, a million-row file of 89.7 MiB took 1.1 s to copy in reads of
    /// 256 KiB, and 0.45 s in reads of 4 MiB (ticket 07).
    /// </summary>
    public const int DefaultBufferSize = 1 << 22;

    /// <summary>The longest record read. Excel holds at most 32,767 characters in a cell, so a record
    /// this long is a quote left open, and the buffer is not grown to the size of the file to find it.</summary>
    public const int MaxRecordBytes = 64 << 20;

    public static FileStream OpenFile(string path)
        => new(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1, FileOptions.Asynchronous | FileOptions.SequentialScan);

    public static async ValueTask<Snapshot> ReadAsync(CsvSchema schema, Stream stream, SnapshotLoadOptions? options, CancellationToken cancellationToken, int bufferSize)
    {
        var plan = CsvPlan.Of(schema);
        cancellationToken.ThrowIfCancellationRequested();
        var builder = new SnapshotColumnsBuilder(options, cancellationToken);
        var read = new CsvRead(plan, builder);
        var total = Remaining(stream);

        // A stream that knows its length gets no larger a buffer than it needs, and a byte more, so
        // that the read that fills it finds the end too.
        var size = total is { } known && known < bufferSize ? (int)known + 1 : bufferSize;
        var buffer = new byte[Math.Max(4, size)];
        var (end, eof) = await FillAsync(stream, buffer, 0, cancellationToken).ConfigureAwait(false);
        var start = Preamble(buffer.AsSpan(0, end), plan.Schema.Encoding, out var encoding);
        read.Begin(encoding);
        long consumed = start;
        while (true)
        {
            var status = read.Parse(buffer.AsSpan(start, end - start), eof, out var used);
            start += used;
            consumed += used;
            await builder.CheckpointAsync(consumed, total).ConfigureAwait(false);
            if (status == ParseStatus.Done)
                break;
            if (status == ParseStatus.NeedMore)
            {
                if (start > 0)
                {
                    Buffer.BlockCopy(buffer, start, buffer, 0, end - start);
                    end -= start;
                    start = 0;
                }
                if (end == buffer.Length)
                {
                    if (buffer.Length >= MaxRecordBytes)
                        throw read.TooLong();
                    Array.Resize(ref buffer, (int)Math.Min(MaxRecordBytes, buffer.Length * 2L));
                }
                (end, eof) = await FillAsync(stream, buffer, end, cancellationToken).ConfigureAwait(false);
            }
        }
        read.Finish();
        builder.TotalRows = read.Rows;
        builder.Report(new SnapshotProgress(read.Rows, read.Rows, consumed, total ?? consumed));
        return await builder.BuildAsync().ConfigureAwait(false);
    }

    /// <summary>The bytes the stream has left to give, when it knows.</summary>
    internal static long? Remaining(Stream stream)
    {
        try
        {
            return stream.CanSeek ? Math.Max(0, stream.Length - stream.Position) : null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>Reads until the buffer is full or the stream ends.</summary>
    internal static async ValueTask<(int End, bool Eof)> FillAsync(Stream stream, byte[] buffer, int end, CancellationToken cancellationToken)
    {
        while (end < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(end), cancellationToken).ConfigureAwait(false);
            if (read == 0)
                return (end, true);
            end += read;
        }
        return (end, false);
    }

    /// <summary>
    /// The length of the byte-order mark the file begins with, and the encoding it is read in: UTF-8
    /// after UTF-8's mark, whatever the Schema declares, and the declared encoding otherwise. A file
    /// that begins with UTF-16's mark is refused, since a CSV is read only in UTF-8 or Shift-JIS.
    /// </summary>
    internal static int Preamble(ReadOnlySpan<byte> head, CsvEncoding declared, out CsvEncoding encoding)
    {
        if (head.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]))
        {
            encoding = CsvEncoding.Utf8;
            return 3;
        }
        if (head.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xFE]) || head.StartsWith((ReadOnlySpan<byte>)[0xFE, 0xFF]))
            throw new SnapshotException(null, null, $"The file begins with UTF-16's byte-order mark; a CSV is read in UTF-8 or Shift-JIS, and the Schema declares {declared.Name}.");
        encoding = declared;
        return 0;
    }
}

/// <summary>Where a batch of records left the load.</summary>
internal enum ParseStatus
{
    /// <summary>The bytes at hand end inside a record.</summary>
    NeedMore,

    /// <summary>A batch is done and more records may follow in the bytes at hand.</summary>
    Paused,

    /// <summary>The file has ended and every record is read.</summary>
    Done,
}

/// <summary>
/// One load of a CSV: the header matched to the declared columns, then the records read into them a
/// batch at a time — the records cut first, then each declared column's field of every record of the
/// batch read in one loop. It knows the line of the file each record of the batch begins on, and its
/// row among the data records, from one, so every refusal names them. A batch is refused as the
/// records would be one after another: at the first record, and in it the first declared column,
/// that cannot be read.
/// </summary>
internal sealed class CsvRead : IFieldContext
{
    private const int Batch = 1024;

    private readonly CsvPlan plan;
    private readonly ColumnBuilder[] targets;
    private readonly CsvTokenizer tokenizer;
    private readonly int[] fieldOf;
    private readonly long[] lines = new long[Batch];
    private FieldReader[] readers = [];
    private Dictionary<int, string>? columnAt;
    private string[]? header;
    private int expectedFields = -1;
    private bool headerPending;

    public CsvRead(CsvPlan plan, SnapshotColumnsBuilder builder)
    {
        this.plan = plan;
        targets = new ColumnBuilder[plan.Columns.Length];
        for (var c = 0; c < targets.Length; c++)
        {
            var column = plan.Columns[c];
            targets[c] = column.Kind switch
            {
                SnapshotKind.Text => builder.Text(column.Name, column.Caption),
                SnapshotKind.Decimal => builder.Decimal(column.Name, column.Caption),
                SnapshotKind.Double => builder.Double(column.Name, column.Caption),
                SnapshotKind.Integer => builder.Integer(column.Name, column.Caption),
                SnapshotKind.Date => builder.Date(column.Name, column.Caption),
                _ => builder.Boolean(column.Name, column.Caption),
            };
        }
        if (plan.KeyOrdinal >= 0)
            builder.Key(plan.Columns[plan.KeyOrdinal].Name);
        tokenizer = new CsvTokenizer(plan.Separator);
        fieldOf = new int[targets.Length];
        headerPending = plan.Schema.HasHeader;
        Encoding = plan.Schema.Encoding;
    }

    public CsvEncoding Encoding { get; private set; }

    /// <summary>The data records read.</summary>
    public int Rows { get; private set; }

    /// <summary>The line of the file the record at hand begins on, from one.</summary>
    public long Line { get; private set; } = 1;

    /// <summary>Starts reading in <paramref name="encoding"/>, which the file's preamble settled.</summary>
    public void Begin(CsvEncoding encoding)
    {
        Encoding = encoding;
        readers = new FieldReader[targets.Length];
        for (var c = 0; c < targets.Length; c++)
            readers[c] = FieldReader.Create(plan.Columns[c], plan.BlankTexts[c], targets[c], this, c == plan.KeyOrdinal);
    }

    /// <summary>Reads a batch of records from <paramref name="data"/>; <paramref name="final"/> when the
    /// file ends where it does.</summary>
    public ParseStatus Parse(ReadOnlySpan<byte> data, bool final, out int consumed)
    {
        var pos = 0;
        consumed = 0;
        if (headerPending)
        {
            var cut = tokenizer.Cut(data, pos, final, out var next);
            if (cut == CutResult.NeedMore)
                return ParseStatus.NeedMore;
            if (cut == CutResult.End)
                return ParseStatus.Done;
            if (cut == CutResult.Malformed)
                throw Malformed(data);
            MatchHeader(data);
            Line += 1 + tokenizer.Breaks;
            pos = next;
        }

        // The records first, each beside the line it begins on; a record that ends the batch short —
        // one that breaks RFC 4180, or has as many fields as no other — is refused once the records
        // before it are read, as it would be read after them.
        tokenizer.Clear();
        var records = 0;
        var status = ParseStatus.Paused;
        var malformed = false;
        var miscounted = false;
        while (records < Batch)
        {
            var cut = tokenizer.Append(data, pos, final, out var next);
            if (cut == CutResult.NeedMore)
            {
                status = ParseStatus.NeedMore;
                break;
            }
            if (cut == CutResult.End)
            {
                status = ParseStatus.Done;
                break;
            }
            if (cut == CutResult.Malformed)
            {
                malformed = true;
                break;
            }
            var count = tokenizer.FieldCount;
            if (expectedFields < 0)
                Place(count);
            if (count != expectedFields)
            {
                miscounted = true;
                break;
            }
            lines[records++] = Line;
            Line += 1 + tokenizer.Breaks;
            pos = next;
        }

        if (records > 0)
            ReadBatch(data, records);
        if (malformed)
            throw Malformed(data);
        if (miscounted)
            throw FieldCount(tokenizer.FieldCount);
        consumed = pos;
        return status;
    }

    /// <summary>
    /// Reads the batch's records into the columns, column by column. A column refused at a record
    /// leaves the columns after it to read only the records before that one, so the refusal thrown is
    /// the one a record-by-record read meets first.
    /// </summary>
    private void ReadBatch(ReadOnlySpan<byte> data, int records)
    {
        var batch = new CsvRecords(data, tokenizer, expectedFields);
        var rows = records;
        SnapshotException? first = null;
        for (var c = 0; c < readers.Length; c++)
        {
            var read = readers[c].Read(batch, fieldOf[c], rows, out var refusal);
            if (refusal is not null)
            {
                first = refusal;
                rows = read;
            }
        }
        if (first is not null)
            throw first;
        Rows += records;
    }

    /// <summary>Ends the load once the file has.</summary>
    public void Finish()
    {
        if (headerPending)
            throw new SnapshotException(null, null, "The file is empty.");
    }

    /// <summary>A refusal of the record at hand — the one after the last read — in <paramref name="column"/>.</summary>
    public SnapshotException Refuse(string? column, string reason)
        => new(Rows + 1L, column, string.Create(CultureInfo.InvariantCulture, $"{reason} (line {Line:N0})."));

    public SnapshotException Refuse(int row, string? column, string reason)
        => new(Rows + row + 1L, column, string.Create(CultureInfo.InvariantCulture, $"{reason} (line {lines[row]:N0})."));

    /// <summary>The refusal of a record longer than <see cref="CsvLoad.MaxRecordBytes"/>.</summary>
    public SnapshotException TooLong()
    {
        var reason = $"is longer than {CsvLoad.MaxRecordBytes >> 20} MiB, which no spreadsheet writes; a quote may be left open";
        return headerPending
            ? new SnapshotException(null, null, string.Create(CultureInfo.InvariantCulture, $"The header {reason} (line {Line:N0})."))
            : Refuse(null, $"the record {reason}");
    }

    /// <summary>Matches the declared columns to the header row: by position where one is declared, and
    /// by the exact header otherwise.</summary>
    private void MatchHeader(ReadOnlySpan<byte> data)
    {
        var count = tokenizer.FieldCount;
        header = new string[count];
        var first = new Dictionary<string, int>(StringComparer.Ordinal);
        var repeats = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var f = 0; f < count; f++)
        {
            var text = CsvText.Decode(Encoding, Field(data, f))
                ?? throw new SnapshotException(null, null, string.Create(CultureInfo.InvariantCulture, $"The header is not valid {Encoding.Name} (line {Line:N0})."));
            header[f] = text;
            if (!first.TryAdd(text, f))
                repeats[text] = repeats.GetValueOrDefault(text, 1) + 1;
        }

        var missing = new List<CsvColumn>();
        for (var c = 0; c < fieldOf.Length; c++)
        {
            var column = plan.Columns[c];
            if (column.Position is { } at)
            {
                if (at >= count)
                    throw new SnapshotException(null, column.Name, $"it is declared at position {at}, but the header has {Fields(count)}.");
                fieldOf[c] = at;
                continue;
            }
            var wanted = column.MatchedHeader;
            if (!first.TryGetValue(wanted, out var field))
            {
                missing.Add(column);
                continue;
            }
            if (repeats.TryGetValue(wanted, out var times))
                throw new SnapshotException(null, column.Name, $"the header holds '{wanted}' {Times(times)}, so which field it is cannot be told; declare its Position.");
            fieldOf[c] = field;
        }
        if (missing.Count > 0)
            throw Missing(missing);
        expectedFields = count;
        Indexed();
        headerPending = false;
    }

    /// <summary>Places the declared columns in a file without a header row, by their positions, once
    /// its first record says how many fields a record has.</summary>
    private void Place(int count)
    {
        for (var c = 0; c < fieldOf.Length; c++)
        {
            var column = plan.Columns[c];
            var at = column.Position ?? c;
            if (at >= count)
                throw new SnapshotException(null, column.Name, $"it is declared at position {at}, but the first record has {Fields(count)}.");
            fieldOf[c] = at;
        }
        expectedFields = count;
        Indexed();
    }

    private void Indexed()
    {
        columnAt = [];
        for (var c = 0; c < fieldOf.Length; c++)
            columnAt.TryAdd(fieldOf[c], plan.Columns[c].Name);
    }

    private SnapshotException Missing(List<CsvColumn> missing)
    {
        var column = missing[0];
        var wanted = column.MatchedHeader;
        var reason = wanted == column.Name ? "it is missing from the header" : $"its header, '{wanted}', is missing from the header row";
        if (missing.Count > 1)
        {
            var others = missing.Skip(1).Select(c => $"'{c.Name}'").ToList();
            reason += others.Count == 1 ? $", and so is {others[0]}" : $", and so are {string.Join(", ", others.SkipLast(1))} and {others[^1]}";
        }
        var near = Array.Find(header!, h => string.Equals(h.Trim(), wanted.Trim(), StringComparison.OrdinalIgnoreCase));
        if (near is not null)
            reason += $"; the header has '{near}'";
        return new SnapshotException(null, column.Name, reason + ".");
    }

    private SnapshotException FieldCount(int count)
    {
        var source = header is null ? "the first record" : "the header";
        if (count == 1 && tokenizer.Length(0) == 0 && tokenizer.Flags(0) == 0)
            return Refuse(null, $"the line is empty, where {source} has {Fields(expectedFields)}");
        return Refuse(null, string.Create(CultureInfo.InvariantCulture, $"the record has {Fields(count)} where {source} has {expectedFields:N0}"));
    }

    private SnapshotException Malformed(ReadOnlySpan<byte> data)
    {
        var field = tokenizer.ErrorField;
        var reason = tokenizer.Error switch
        {
            CsvFault.Unclosed => "a quoted field is not closed before the file ends",
            CsvFault.QuoteInside => "a quote stands inside a field that does not begin with one",
            _ => $"a quoted field's closing quote is followed by {CsvText.Show(Encoding, data.Slice(tokenizer.ErrorAt, 1))} rather than a separator or a line end",
        };
        if (headerPending)
            return new SnapshotException(null, null, string.Create(CultureInfo.InvariantCulture, $"The header's field {field + 1}: {reason} (line {Line:N0})."));
        string? column = null;
        if (columnAt is not null && !columnAt.TryGetValue(field, out column) && header is not null && field < header.Length)
            column = header[field];
        return column is null ? Refuse(null, $"in field {field + 1} of the record, {reason}") : Refuse(column, reason);
    }

    /// <summary>A header field's content: what lies between its quotes, with a doubled quote made single.</summary>
    private ReadOnlySpan<byte> Field(ReadOnlySpan<byte> data, int field)
    {
        var content = data.Slice(tokenizer.Start(field), tokenizer.Length(field));
        if ((tokenizer.Flags(field) & CsvTokenizer.Doubled) == 0)
            return content;
        var unescaped = new byte[content.Length];
        var written = 0;
        for (var i = 0; i < content.Length; i++)
        {
            unescaped[written++] = content[i];
            if (content[i] == CsvTokenizer.Quote)
                i++;
        }
        return unescaped.AsSpan(0, written);
    }

    private static string Fields(int count) => count == 1 ? "1 field" : string.Create(CultureInfo.InvariantCulture, $"{count:N0} fields");

    private static string Times(int times) => times == 2 ? "twice" : $"{times} times";
}
