using System.Globalization;
using System.Text;

namespace ExGrid.Data.Csv;

/// <summary>
/// Suggests a Schema from a file's first rows (ADR-0063, Q33). It reads only the sample, proposes a
/// kind and a reading for each column under which the whole sample reads, and marks every column
/// whose reading is not clear — the account number <c>00123</c>, a comma that could be the decimal
/// point, a date that could be the first of February or the second of January — rather than settle
/// it quietly. Each reading is checked with the reader's own parsers, so the sample reads under it.
/// </summary>
internal static class CsvSuggester
{
    private const int Chunk = 1 << 16;
    private const int MaxSampleBytes = 64 << 20;
    private const int MaxExamples = 5;
    private const int MaxNamed = 3;

    /// <summary>Texts that stand for a value that is not there, which a suggestion proposes as Blanks
    /// in a column of another kind.</summary>
    private static readonly string[] BlankWords = ["NULL", "N/A", "NA", "#N/A", "-", "--", "nil", "None", "(null)", "(blank)"];

    /// <summary>The readings of a number a suggestion tries, simplest first: the decimal point, then
    /// the thousands separator.</summary>
    private static readonly (string Point, string Thousands)[] ReadingTexts =
    [
        (".", ""),
        (",", ""),
        (".", ","),
        (",", "."),
        (",", " "),
        (",", " "),
    ];

    private static readonly NumberReading[] Readings = [.. ReadingTexts.Select(r => new NumberReading(
        new CsvColumn("n", SnapshotKind.Decimal) { DecimalPoint = r.Point, ThousandsSeparator = r.Thousands },
        CsvEncoding.Utf8))];

    /// <summary>The date formats a suggestion tries: ISO 8601, then year first, day first and month
    /// first, as spreadsheets write them.</summary>
    private static readonly string[] DateTexts =
    [
        "yyyy-MM-dd",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss.FFFFFFF",
        "yyyy-MM-dd HH:mm:ss.FFFFFFF",
        "yyyy-MM-ddTHH:mm",
        "yyyy-MM-dd HH:mm",
        "yyyy-MM-ddTHH:mm:ss.FFFFFFFK",
        "yyyy/M/d",
        "yyyy/M/d H:mm",
        "yyyy/M/d H:mm:ss",
        "d/M/yyyy",
        "d/M/yyyy H:mm",
        "d/M/yyyy H:mm:ss",
        "M/d/yyyy",
        "M/d/yyyy H:mm",
        "M/d/yyyy H:mm:ss",
        "M/d/yyyy h:mm tt",
        "M/d/yyyy h:mm:ss tt",
        "d.M.yyyy",
        "d.M.yyyy H:mm",
        "d.M.yyyy H:mm:ss",
    ];

    private static readonly DateFormat[] DateFormats = [.. DateTexts.Select(f => new DateFormat(f, CultureInfo.InvariantCulture))];

    private static readonly CsvSeparator[] Separators = [CsvSeparator.Comma, CsvSeparator.Tab, CsvSeparator.Semicolon];

    public static async ValueTask<CsvSuggestion> SuggestAsync(Stream stream, CsvSuggestionOptions options, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(options.Rows, 1, nameof(options));
        if (options.Encoding is null)
            throw new ArgumentException("The suggestion's options declare no encoding.", nameof(options));
        if (options.Separator is { } declared && !Enum.IsDefined(declared))
            throw new ArgumentException($"The separator {(int)declared} is not one a CSV is read with: a comma, a tab or a semicolon.", nameof(options));
        cancellationToken.ThrowIfCancellationRequested();

        var (sample, eof) = await SampleAsync(stream, options.Rows + 1, cancellationToken).ConfigureAwait(false);
        var start = CsvLoad.Preamble(sample, options.Encoding, out var encoding);
        var bytes = sample.AsMemory(start);
        var culture = options.Culture ?? CultureInfo.InvariantCulture;
        var fileMarks = new List<CsvMark>();

        var separator = options.Separator ?? Detect(bytes.Span, eof, options.Rows + 1, culture, fileMarks);
        var records = Records(bytes.Span, Byte(separator), eof, options.Rows + 1, encoding);
        var hasHeader = options.HasHeader ?? HeaderRow(records, separator, culture, fileMarks);
        var data = (hasHeader ? records.Skip(1) : records).Take(options.Rows).ToList();
        var headers = hasHeader && records.Count > 0 ? records[0] : [];
        var width = hasHeader ? headers.Length : Modal([.. data.Select(r => r.Length)]);

        var other = data.Select((r, i) => (Row: i + 1, r.Length)).Where(r => r.Length != width).Select(r => r.Row).ToList();
        if (other.Count > 0)
        {
            fileMarks.Add(new CsvMark(CsvDoubt.FieldCount, string.Create(CultureInfo.InvariantCulture,
                $"{Rows(other)} {(other.Count == 1 ? "has" : "have")} other than the {width:N0} fields {(hasHeader ? "the header has" : "the first record has")}; reading the file under this Schema refuses {(other.Count == 1 ? "it" : "them")}.")));
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        var repeated = headers.GroupBy(h => h, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.Ordinal);
        var columns = new List<CsvColumn>();
        var suggestions = new List<CsvColumnSuggestion>();
        for (var i = 0; i < width; i++)
        {
            var marks = new List<CsvMark>();
            var values = Column(data, i);
            var proposal = Propose(values, separator, culture, marks);

            string name;
            int? position = null;
            var text = i < headers.Length ? headers[i] : "";
            if (hasHeader && text.Length > 0 && !repeated.Contains(text) && names.Add(text))
            {
                name = text;
            }
            else
            {
                name = Unique(names, $"Column{i + 1}");
                if (hasHeader)
                {
                    position = i;
                    marks.Add(new CsvMark(CsvDoubt.HeaderName, text.Length == 0
                        ? $"The header is empty here, so the column is matched by its position, {i}, and named '{name}'."
                        : $"The header '{text}' stands more than once, so the column is matched by its position, {i}, and named '{name}'."));
                }
            }

            var column = new CsvColumn(name, proposal.Kind)
            {
                Position = position,
                DecimalPoint = proposal.Point,
                ThousandsSeparator = proposal.Thousands,
                DateFormats = proposal.DateFormats,
                BlankText = proposal.BlankText,
            };
            columns.Add(column);
            var examples = values.Select(v => v.Text).Distinct(StringComparer.Ordinal).Take(MaxExamples).ToArray();
            suggestions.Add(new CsvColumnSuggestion(column, marks.AsReadOnly(), Array.AsReadOnly(examples)));
        }

        var schema = new CsvSchema(columns.AsReadOnly())
        {
            Separator = separator,
            HasHeader = hasHeader,
            Encoding = encoding,
        };
        return new CsvSuggestion(schema, suggestions.AsReadOnly(), fileMarks.AsReadOnly(), data.Count);
    }

    /// <summary>Reads from the start of the stream until it holds more line breaks than twice
    /// <paramref name="records"/> — a CR LF counts twice, and a quoted field may hold breaks — or ends.</summary>
    private static async ValueTask<(byte[] Sample, bool Eof)> SampleAsync(Stream stream, int records, CancellationToken cancellationToken)
    {
        var buffer = new byte[Chunk];
        var length = 0;
        long breaks = 0;
        while (true)
        {
            if (length == buffer.Length)
            {
                if (buffer.Length >= MaxSampleBytes)
                    return (buffer[..length], false);
                Array.Resize(ref buffer, Math.Min(MaxSampleBytes, buffer.Length * 2));
            }
            var read = await stream.ReadAsync(buffer.AsMemory(length), cancellationToken).ConfigureAwait(false);
            if (read == 0)
                return (buffer[..length], true);
            var fresh = buffer.AsSpan(length, read);
            breaks += fresh.Count((byte)'\n') + fresh.Count((byte)'\r');
            length += read;
            if (breaks > (2L * records) + 2)
                return (buffer[..length], false);
        }
    }

    /// <summary>The separator that splits every record of the sample into the same number of fields,
    /// two or more. When several do, the one whose fields read best as numbers and dates is suggested,
    /// then the culture's list separator, and the file is marked.</summary>
    private static CsvSeparator Detect(ReadOnlySpan<byte> bytes, bool eof, int records, CultureInfo culture, List<CsvMark> marks)
    {
        var even = new List<(CsvSeparator Separator, int Typed)>();
        var uneven = new List<(CsvSeparator Separator, int Alike)>();
        var anySplit = false;
        foreach (var candidate in Separators)
        {
            var counts = Widths(bytes, Byte(candidate), eof, records, out var faulted, out var typed);
            if (faulted || counts.Count == 0)
                continue;
            anySplit |= counts.Exists(c => c > 1);
            var modal = Modal(counts);
            if (modal > 1 && counts.TrueForAll(c => c == modal))
                even.Add((candidate, typed));
            else if (modal > 1)
                uneven.Add((candidate, counts.Count(c => c == modal)));
        }
        if (even.Count == 1)
            return even[0].Separator;
        if (even.Count > 1)
        {
            var listSeparator = culture.TextInfo.ListSeparator;
            var chosen = even
                .OrderByDescending(e => e.Typed)
                .ThenByDescending(e => Character(e.Separator) == listSeparator)
                .First().Separator;
            marks.Add(new CsvMark(CsvDoubt.Separator,
                $"Every record splits evenly at {string.Join(" and at ", even.Select(e => Shown(e.Separator)))}; {Shown(chosen)} is suggested, since its fields read best as numbers and dates."));
            return chosen;
        }
        if (!anySplit || uneven.Count == 0)
            return CsvSeparator.Comma;
        var best = uneven.OrderByDescending(u => u.Alike).First().Separator;
        marks.Add(new CsvMark(CsvDoubt.Separator,
            $"No separator splits every record into the same number of fields; {Shown(best)} is suggested, since it splits the most records alike."));
        return best;
    }

    /// <summary>The number of fields of each record of the sample cut at <paramref name="separator"/>,
    /// and how many fields after the first record read as a number, a date or a Boolean.</summary>
    private static List<int> Widths(ReadOnlySpan<byte> bytes, byte separator, bool eof, int records, out bool faulted, out int typed)
    {
        var tokenizer = new CsvTokenizer(separator);
        var counts = new List<int>();
        typed = 0;
        faulted = false;
        var pos = 0;
        while (counts.Count < records)
        {
            var cut = tokenizer.Cut(bytes, pos, eof, out var next);
            if (cut is CutResult.NeedMore or CutResult.End)
                break;
            if (cut == CutResult.Malformed)
            {
                faulted = true;
                break;
            }
            counts.Add(tokenizer.FieldCount);
            if (counts.Count > 1)
            {
                for (var f = 0; f < tokenizer.FieldCount; f++)
                {
                    var field = bytes.Slice(tokenizer.Start(f), tokenizer.Length(f));
                    if (field.Length > 0 && Typed(Encoding.UTF8.GetString(field)))
                        typed++;
                }
            }
            pos = next;
        }
        return counts;
    }

    /// <summary>The sample's records, each field decoded strictly; a record cut short by the end of
    /// the sample is left out.</summary>
    private static List<string[]> Records(ReadOnlySpan<byte> bytes, byte separator, bool eof, int records, CsvEncoding encoding)
    {
        var tokenizer = new CsvTokenizer(separator);
        var result = new List<string[]>();
        var pos = 0;
        long line = 1;
        while (result.Count < records)
        {
            var cut = tokenizer.Cut(bytes, pos, eof, out var next);
            if (cut is CutResult.NeedMore or CutResult.End)
                break;
            if (cut == CutResult.Malformed)
            {
                var reason = tokenizer.Error switch
                {
                    CsvFault.Unclosed => "a quoted field is not closed before the sample ends",
                    CsvFault.QuoteInside => "a quote stands inside a field that does not begin with one",
                    _ => "a quoted field's closing quote is followed by something other than a separator or a line end",
                };
                throw new SnapshotException(null, null, string.Create(CultureInfo.InvariantCulture, $"Line {line:N0}: {reason}, so no Schema can be suggested."));
            }
            var fields = new string[tokenizer.FieldCount];
            for (var f = 0; f < fields.Length; f++)
            {
                var content = Unescape(bytes.Slice(tokenizer.Start(f), tokenizer.Length(f)), tokenizer.Flags(f));
                fields[f] = CsvText.Decode(encoding, content)
                    ?? throw new SnapshotException(null, null, string.Create(CultureInfo.InvariantCulture,
                        $"Line {line:N0}: the text is not valid {encoding.Name}; if the file is in another encoding, say which in the suggestion's options."));
            }
            result.Add(fields);
            line += 1 + tokenizer.Breaks;
            pos = next;
        }
        return result;
    }

    /// <summary>Whether the first record is a header row: it is, unless every column whose other
    /// values are numbers, dates or Booleans reads its first value as one of them too.</summary>
    private static bool HeaderRow(List<string[]> records, CsvSeparator separator, CultureInfo culture, List<CsvMark> marks)
    {
        if (records.Count < 2)
        {
            marks.Add(new CsvMark(CsvDoubt.Header, "The sample holds one record at most, which is taken as the header row."));
            return true;
        }
        var first = records[0];
        var typedColumns = 0;
        var fitting = 0;
        for (var i = 0; i < first.Length; i++)
        {
            var values = Column(records.Skip(1).ToList(), i);
            if (Propose(values, separator, culture, []).Kind == SnapshotKind.Text)
                continue;
            typedColumns++;
            if (first[i].Length == 0 || Propose([(first[i], 0), .. values], separator, culture, []).Kind != SnapshotKind.Text)
                fitting++;
        }
        if (typedColumns == 0)
        {
            marks.Add(new CsvMark(CsvDoubt.Header, "Every column holds text, so whether the first record is a header row cannot be told; it is taken as one."));
            return true;
        }
        return fitting < typedColumns;
    }

    /// <summary>The kind and reading suggested for a column's non-empty values, with their rows, and
    /// its marks.</summary>
    private static Proposal Propose(List<(string Text, int Row)> values, CsvSeparator separator, CultureInfo culture, List<CsvMark> marks)
    {
        if (values.Count == 0)
        {
            marks.Add(new CsvMark(CsvDoubt.Empty, "The column is empty in every sampled row; it is suggested as Text."));
            return new Proposal(SnapshotKind.Text);
        }
        var blanks = values.Where(v => IsBlankWord(v.Text)).Select(v => v.Text).Distinct(StringComparer.Ordinal).ToArray();
        var rest = blanks.Length == 0 ? values : values.Where(v => !IsBlankWord(v.Text)).ToList();
        if (rest.Count == 0)
        {
            marks.Add(new CsvMark(CsvDoubt.Empty, $"The column holds nothing but {List(blanks)} in the sampled rows; it is suggested as Text."));
            return new Proposal(SnapshotKind.Text);
        }

        var proposal = Booleans(rest) ?? Numbers(rest, separator, culture, marks) ?? Dates(rest, culture, marks);
        if (proposal is null)
        {
            var spellings = rest.Select(v => v.Text.Trim(' ')).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (spellings.TrueForAll(s => s.ToUpperInvariant() is "YES" or "NO" or "Y" or "N"))
                marks.Add(new CsvMark(CsvDoubt.CouldBeBoolean, $"The column holds only {List(spellings)}; it could be a Boolean, with those spellings declared."));
            else
                Mixed(rest, separator, culture, marks);
            return new Proposal(SnapshotKind.Text);
        }
        if (proposal.Kind != SnapshotKind.Text && blanks.Length > 0)
        {
            proposal = proposal with { BlankText = blanks };
            marks.Add(new CsvMark(CsvDoubt.BlankText,
                $"{List(blanks)} {(blanks.Length == 1 ? "stands" : "stand")} among the {Describe(proposal.Kind)} and {(blanks.Length == 1 ? "is" : "are")} read as {(blanks.Length == 1 ? "a Blank" : "Blanks")}."));
        }
        return proposal;
    }

    private static Proposal? Booleans(List<(string Text, int Row)> values)
        => values.TrueForAll(v => v.Text.Trim(' ').ToUpperInvariant() is "TRUE" or "FALSE") ? new Proposal(SnapshotKind.Boolean) : null;

    /// <summary>The simplest reading of a number under which every value reads, or none.</summary>
    private static Proposal? Numbers(List<(string Text, int Row)> values, CsvSeparator separator, CultureInfo culture, List<CsvMark> marks)
    {
        var bytes = values.Select(v => Encoding.UTF8.GetBytes(v.Text.Trim(' '))).ToArray();
        var passing = new List<(int Reading, decimal[] Values, bool AnyPoint)>();
        List<int>? tooLong = null;
        for (var r = 0; r < Readings.Length; r++)
        {
            var read = new decimal[bytes.Length];
            var anyPoint = false;
            var numbers = true;
            var longOnes = new List<int>();
            for (var i = 0; i < bytes.Length && numbers; i++)
            {
                var status = NumberText.Parse(bytes[i], Readings[r], out var number);
                if (status == NumberStatus.NotANumber)
                    numbers = false;
                else if (status == NumberStatus.TooLong || !number.TryDecimal(out read[i]))
                    longOnes.Add(i);
                else
                    anyPoint |= number.HasPoint;
            }
            if (!numbers)
                continue;
            if (longOnes.Count > 0)
            {
                tooLong ??= longOnes;
                continue;
            }
            passing.Add((r, read, anyPoint));
        }

        if (passing.Count == 0)
        {
            if (tooLong is null)
                return Floating(bytes);
            marks.Add(new CsvMark(CsvDoubt.TooLong,
                $"{Named(tooLong.Select(i => values[i]))} {(tooLong.Count == 1 ? "has" : "have")} more digits than a Decimal holds; the column is suggested as Text."));
            return new Proposal(SnapshotKind.Text);
        }

        // Readings that read every value alike are one reading. When two read some value apart, the
        // culture decides which is suggested, and the column is marked.
        var chosen = passing[0];
        if (passing.Exists(p => !p.Values.AsSpan().SequenceEqual(chosen.Values)))
        {
            var comma = separator == CsvSeparator.Semicolon || culture.NumberFormat.NumberDecimalSeparator == ",";
            var preferred = passing.FindIndex(p => ReadingTexts[p.Reading].Point == (comma ? "," : "."));
            if (preferred >= 0)
                chosen = passing[preferred];
            var apart = Enumerable.Range(0, values.Count).First(i => passing.Exists(p => p.Values[i] != chosen.Values[i]));
            var otherwise = passing.First(p => p.Values[apart] != chosen.Values[apart]);
            marks.Add(new CsvMark(CsvDoubt.AmbiguousSeparator, string.Create(CultureInfo.InvariantCulture,
                $"'{values[apart].Text}' (row {values[apart].Row}) reads as {chosen.Values[apart]} with '{ReadingTexts[chosen.Reading].Point}' as the decimal point, and as {otherwise.Values[apart]} with '{ReadingTexts[otherwise.Reading].Point}'; '{ReadingTexts[chosen.Reading].Point}' is suggested.")));
        }
        var (point, thousands) = ReadingTexts[chosen.Reading];
        var both = values.FindIndex(v => v.Text.Contains(',', StringComparison.Ordinal) && v.Text.Contains('.', StringComparison.Ordinal));
        if (both >= 0)
        {
            marks.Add(new CsvMark(CsvDoubt.BothSeparators,
                $"'{values[both].Text}' (row {values[both].Row}) is written with both ',' and '.'; '{point}' is read as the decimal point and '{thousands}' as the thousands separator."));
        }

        var zeros = values.Where(v => LeadingZero(v.Text, point, thousands)).ToList();
        if (zeros.Count > 0)
        {
            marks.Add(new CsvMark(CsvDoubt.LeadingZeros,
                $"{Named(zeros)} {(zeros.Count == 1 ? "has a leading zero" : "have leading zeros")}, so the column could be an identifier rather than a number; it is suggested as Text, kept exactly."));
            return new Proposal(SnapshotKind.Text);
        }

        var integer = !chosen.AnyPoint && Array.TrueForAll(chosen.Values, v => v is >= long.MinValue and <= long.MaxValue);
        if (integer && Array.TrueForAll(chosen.Values, v => v is 0 or 1))
            marks.Add(new CsvMark(CsvDoubt.CouldBeBoolean, "The column holds only 0 and 1; it could be a Boolean, with those spellings declared."));
        return new Proposal(integer ? SnapshotKind.Integer : SnapshotKind.Decimal)
        {
            Point = point == "." ? null : point,
            Thousands = thousands.Length == 0 ? null : thousands,
        };
    }

    /// <summary>Numbers that read only as floating point — with an exponent, or NaN and Infinity — as a
    /// Double.</summary>
    private static Proposal? Floating(byte[][] bytes)
    {
        for (var r = 0; r < 2; r++)
        {
            var all = true;
            for (var i = 0; i < bytes.Length && all; i++)
            {
                var scratch = new byte[bytes[i].Length + 1];
                all = NumberText.Normalize(bytes[i], Readings[r], scratch, out var written)
                    ? double.TryParse(scratch.AsSpan(0, written), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number)
                    : double.TryParse(bytes[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var symbol) && !double.IsFinite(symbol);
            }
            if (all)
                return new Proposal(SnapshotKind.Double) { Point = r == 0 ? null : "," };
        }
        return null;
    }

    /// <summary>The fewest formats under which every value reads as a date, or none.</summary>
    private static Proposal? Dates(List<(string Text, int Row)> values, CultureInfo culture, List<CsvMark> marks)
    {
        var ticks = new long?[DateFormats.Length][];
        for (var f = 0; f < DateFormats.Length; f++)
        {
            ticks[f] = new long?[values.Count];
            for (var i = 0; i < values.Count; i++)
                ticks[f][i] = DateFormats[f].TryRead(values[i].Text.Trim(' ').AsSpan(), out var t) ? t : null;
        }
        var whole = Enumerable.Range(0, DateFormats.Length).Where(f => Array.TrueForAll(ticks[f], t => t.HasValue)).ToList();
        if (whole.Count > 0)
        {
            var chosen = whole[0];
            if (whole.Exists(f => !ticks[f].AsSpan().SequenceEqual(ticks[chosen])))
            {
                var first = DayFirst(culture) ? 'd' : 'M';
                var preferred = whole.FindIndex(f => DateTexts[f][0] == first);
                if (preferred >= 0)
                    chosen = whole[preferred];
                var apart = Enumerable.Range(0, values.Count).First(i => whole.Exists(f => ticks[f][i] != ticks[chosen][i]));
                marks.Add(new CsvMark(CsvDoubt.DayOrMonth,
                    $"'{values[apart].Text}' (row {values[apart].Row}) could have its day or its month first; '{DateTexts[chosen]}' is suggested."));
            }
            return new Proposal(SnapshotKind.Date) { DateFormats = [DateTexts[chosen]] };
        }

        var uncovered = Enumerable.Range(0, values.Count).ToHashSet();
        var cover = new List<int>();
        while (uncovered.Count > 0)
        {
            var (best, count) = Enumerable.Range(0, DateFormats.Length)
                .Select(f => (f, uncovered.Count(i => ticks[f][i].HasValue)))
                .MaxBy(x => x.Item2);
            if (count == 0)
                return null;
            cover.Add(best);
            uncovered.RemoveWhere(i => ticks[best][i].HasValue);
        }
        var formats = cover.Select(f => DateTexts[f]).ToArray();
        var examples = cover.Select(f => values[Array.FindIndex(ticks[f], t => t.HasValue)]).ToArray();
        marks.Add(new CsvMark(CsvDoubt.MixedDateFormats,
            $"The dates are written in {formats.Length} formats: {string.Join(", ", formats.Zip(examples, (f, e) => $"'{f}', as '{e.Text}' (row {e.Row})"))}; all of them are suggested."));
        if (formats.Any(f => f[0] == 'd') && formats.Any(f => f[0] == 'M'))
            marks.Add(new CsvMark(CsvDoubt.DayOrMonth, "Some dates have the day first and others the month first; which is which cannot be told."));
        return new Proposal(SnapshotKind.Date) { DateFormats = formats };
    }

    /// <summary>Marks a column suggested as Text whose values are, all but a few, numbers or dates.</summary>
    private static void Mixed(List<(string Text, int Row)> values, CsvSeparator separator, CultureInfo culture, List<CsvMark> marks)
    {
        if (values.Count < 10)
            return;
        var misfits = values.Where(v => !Typed(v.Text)).ToList();
        if (misfits.Count == 0 || misfits.Count * 10 > values.Count)
            return;
        var kind = Propose(values.Where(v => Typed(v.Text)).ToList(), separator, culture, []).Kind;
        if (kind == SnapshotKind.Text)
            return;
        marks.Add(new CsvMark(CsvDoubt.MixedKinds,
            $"Most values are {Describe(kind)}, but {Named(misfits)} {(misfits.Count == 1 ? "is" : "are")} not; the column is suggested as Text."));
    }

    /// <summary>Whether a field reads as a number, a date or a Boolean under some reading.</summary>
    private static bool Typed(string text)
    {
        var trimmed = text.Trim(' ');
        if (trimmed.ToUpperInvariant() is "TRUE" or "FALSE")
            return true;
        var bytes = Encoding.UTF8.GetBytes(trimmed);
        foreach (var reading in Readings)
        {
            if (NumberText.Parse(bytes, reading, out _) == NumberStatus.Ok)
                return true;
        }
        foreach (var format in DateFormats)
        {
            if (format.TryRead(trimmed.AsSpan(), out _))
                return true;
        }
        return false;
    }

    private static List<(string Text, int Row)> Column(List<string[]> records, int index)
    {
        var values = new List<(string Text, int Row)>();
        for (var r = 0; r < records.Count; r++)
        {
            if (index < records[r].Length && records[r][index].Length > 0)
                values.Add((records[r][index], r + 1));
        }
        return values;
    }

    /// <summary>Whether the digits before the decimal point begin with a zero that is not the only digit.</summary>
    private static bool LeadingZero(string text, string point, string thousands)
    {
        var digits = text.Trim(' ').TrimStart('-', '+');
        var end = digits.IndexOf(point, StringComparison.Ordinal);
        var whole = end < 0 ? digits : digits[..end];
        if (thousands.Length > 0)
            whole = whole.Replace(thousands, "", StringComparison.Ordinal);
        return whole.Length >= 2 && whole[0] == '0';
    }

    private static bool IsBlankWord(string text)
        => Array.Exists(BlankWords, w => w.Equals(text.Trim(' '), StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether the culture writes a date with its day before its month.</summary>
    private static bool DayFirst(CultureInfo culture)
    {
        foreach (var c in culture.DateTimeFormat.ShortDatePattern)
        {
            if (c == 'd')
                return true;
            if (c is 'M' or 'y')
                return false;
        }
        return false;
    }

    private static int Modal(List<int> counts)
        => counts.Count == 0 ? 0 : counts.GroupBy(c => c).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).First().Key;

    private static ReadOnlySpan<byte> Unescape(ReadOnlySpan<byte> content, byte flags)
    {
        if ((flags & CsvTokenizer.Doubled) == 0)
            return content;
        var copy = new byte[content.Length];
        var written = 0;
        for (var i = 0; i < content.Length; i++)
        {
            copy[written++] = content[i];
            if (content[i] == CsvTokenizer.Quote)
                i++;
        }
        return copy.AsSpan(0, written);
    }

    private static byte Byte(CsvSeparator separator) => (byte)Character(separator)[0];

    private static string Character(CsvSeparator separator) => separator switch
    {
        CsvSeparator.Comma => ",",
        CsvSeparator.Tab => "\t",
        _ => ";",
    };

    private static string Shown(CsvSeparator separator) => separator switch
    {
        CsvSeparator.Comma => "a comma",
        CsvSeparator.Tab => "a tab",
        _ => "a semicolon",
    };

    private static string Describe(SnapshotKind kind) => kind switch
    {
        SnapshotKind.Decimal or SnapshotKind.Double or SnapshotKind.Integer => "numbers",
        SnapshotKind.Date => "dates",
        SnapshotKind.Boolean => "true or false",
        _ => "text",
    };

    private static string Unique(HashSet<string> names, string name)
    {
        var unique = name;
        for (var n = 2; !names.Add(unique); n++)
            unique = string.Create(CultureInfo.InvariantCulture, $"{name}_{n}");
        return unique;
    }

    private static string List(IEnumerable<string> texts)
    {
        var quoted = texts.Select(t => $"'{t}'").ToList();
        return quoted.Count <= 1 ? string.Concat(quoted) : $"{string.Join(", ", quoted.SkipLast(1))} and {quoted[^1]}";
    }

    /// <summary>A few values with their rows, for a mark: <c>'00123' (row 3) and '007' (row 9)</c>.</summary>
    private static string Named(IEnumerable<(string Text, int Row)> values)
    {
        var all = values.ToList();
        var named = all.Take(MaxNamed).Select(v => $"'{v.Text}' (row {v.Row})").ToList();
        if (all.Count > MaxNamed)
            named.Add(string.Create(CultureInfo.InvariantCulture, $"{all.Count - MaxNamed:N0} more"));
        return named.Count <= 1 ? string.Concat(named) : $"{string.Join(", ", named.SkipLast(1))} and {named[^1]}";
    }

    private static string Rows(List<int> rows)
    {
        var named = rows.Take(MaxNamed).Select(r => r.ToString(CultureInfo.InvariantCulture)).ToList();
        if (rows.Count > MaxNamed)
            named.Add(string.Create(CultureInfo.InvariantCulture, $"{rows.Count - MaxNamed:N0} more"));
        var list = named.Count <= 1 ? string.Concat(named) : $"{string.Join(", ", named.SkipLast(1))} and {named[^1]}";
        return (rows.Count == 1 ? "Row " : "Rows ") + list;
    }

    /// <summary>What is suggested for a column.</summary>
    private sealed record Proposal(SnapshotKind Kind)
    {
        public string? Point { get; init; }

        public string? Thousands { get; init; }

        public IReadOnlyList<string>? DateFormats { get; init; }

        public IReadOnlyList<string>? BlankText { get; init; }
    }
}
