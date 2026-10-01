using ExGrid.Data.Csv;

namespace ExGrid.Data;

/// <summary>
/// How a delimited text file is read into a Snapshot (ADR-0063, the second way in): each column's
/// header, kind and reading, and the file's encoding, separator and header row. It is declared,
/// never guessed — guessing is what reads the account number <c>00123</c> as the number 123. A
/// Schema suggested from a file's first rows (<see cref="SuggestAsync(Stream, CsvSuggestionOptions?, CancellationToken)"/>)
/// is a proposal, read under only once it is handed back.
/// <para>
/// The file is read as bytes, straight into the Snapshot's columns, with no string made per cell.
/// Quoting follows RFC 4180: a field in double quotes may hold the separator, a line break, kept as
/// written, and a quote doubled. A record ends at a line break — CR LF, LF or CR — outside quotes.
/// </para>
/// <para>
/// A load either yields a Snapshot or fails whole (<see cref="SnapshotException"/>), naming the
/// record — the row, counted from one among the data records — and the column, with the file's line
/// where the record begins: a value its kind cannot read, a record with more or fewer fields than
/// the header, a quote left open or out of place, text not valid in the encoding, a declared column
/// missing from the header, and a Record Key that is Blank or carried twice.
/// </para>
/// </summary>
/// <param name="Columns">The columns to read, in the order the Snapshot holds them. A column of the
/// file that none of them matches is skipped.</param>
public sealed record CsvSchema(IReadOnlyList<CsvColumn> Columns)
{
    /// <summary>The character between fields. A comma by default.</summary>
    public CsvSeparator Separator { get; init; } = CsvSeparator.Comma;

    /// <summary>Whether the file's first record is a header row, which names its columns. True by
    /// default.</summary>
    public bool HasHeader { get; init; } = true;

    /// <summary>The encoding of a file that does not begin with a byte-order mark: UTF-8 by default,
    /// or <see cref="CsvEncoding.ShiftJis"/>. A file that begins with UTF-8's mark is read as UTF-8.</summary>
    public CsvEncoding Encoding { get; init; } = CsvEncoding.Utf8;

    /// <summary>
    /// The texts that are a Blank, besides the empty field, in every column that declares no
    /// <see cref="CsvColumn.BlankText"/> of its own — <c>NULL</c>, <c>-</c> — matched exactly.
    /// None by default.
    /// </summary>
    public IReadOnlyList<string>? BlankText { get; init; }

    /// <summary>
    /// The name of the column that is the Record Key: a Text or Integer column whose value tells each
    /// record from every other. A Blank key, and a key carried by two records, fail the load. None by
    /// default.
    /// </summary>
    public string? RecordKey { get; init; }

    /// <summary>
    /// Reads <paramref name="stream"/>, from where it stands to its end, into a Snapshot, in slices
    /// sized by time that yield between them and report progress — the data records read, the bytes
    /// read, and the bytes there are when the stream knows its length (<see cref="SnapshotLoadOptions"/>).
    /// A cancelled load throws <see cref="OperationCanceledException"/> and yields nothing. The stream
    /// is read, never closed.
    /// </summary>
    /// <exception cref="ArgumentException">The Schema contradicts itself; the message says how.</exception>
    /// <exception cref="SnapshotException">The file is refused, naming the row and the column.</exception>
    public ValueTask<Snapshot> ReadAsync(Stream stream, SnapshotLoadOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return CsvLoad.ReadAsync(this, stream, options, cancellationToken, CsvLoad.DefaultBufferSize);
    }

    /// <summary>Reads the file at <paramref name="path"/> into a Snapshot, as
    /// <see cref="ReadAsync(Stream, SnapshotLoadOptions?, CancellationToken)"/> reads a stream.</summary>
    /// <exception cref="ArgumentException">The Schema contradicts itself; the message says how.</exception>
    /// <exception cref="SnapshotException">The file is refused, naming the row and the column.</exception>
    public async ValueTask<Snapshot> ReadAsync(string path, SnapshotLoadOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        await using var stream = CsvLoad.OpenFile(path);
        return await ReadAsync(stream, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Suggests a Schema for a file nobody has described, from its first rows (ADR-0063, Q33): each
    /// column's kind and reading, with every column whose kind is not clear marked, for the user to
    /// confirm. Nothing applies it: the file is read under it only when it — or a Schema changed from
    /// it — is handed to <see cref="ReadAsync(Stream, SnapshotLoadOptions?, CancellationToken)"/>.
    /// The stream is read only as far as the rows sampled, and never closed.
    /// </summary>
    /// <exception cref="SnapshotException">The sample cannot be read at all: it is not valid in the
    /// encoding, or a quote is left open.</exception>
    public static ValueTask<CsvSuggestion> SuggestAsync(Stream stream, CsvSuggestionOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return CsvSuggester.SuggestAsync(stream, options ?? new CsvSuggestionOptions(), cancellationToken);
    }

    /// <summary>Suggests a Schema for the file at <paramref name="path"/>, as
    /// <see cref="SuggestAsync(Stream, CsvSuggestionOptions?, CancellationToken)"/> does for a stream.</summary>
    /// <exception cref="SnapshotException">The sample cannot be read at all.</exception>
    public static async ValueTask<CsvSuggestion> SuggestAsync(string path, CsvSuggestionOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        await using var stream = CsvLoad.OpenFile(path);
        return await SuggestAsync(stream, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Whether two Schemas are declared alike, their columns compared one by one.</summary>
    public bool Equals(CsvSchema? other)
        => other is not null
            && Separator == other.Separator
            && HasHeader == other.HasHeader
            && ReferenceEquals(Encoding, other.Encoding)
            && RecordKey == other.RecordKey
            && CsvColumn.Same(BlankText, other.BlankText)
            && (ReferenceEquals(Columns, other.Columns)
                || (Columns is not null && other.Columns is not null && Columns.SequenceEqual(other.Columns)));

    /// <summary>A hash of what <see cref="Equals(CsvSchema?)"/> compares.</summary>
    public override int GetHashCode() => HashCode.Combine(Separator, HasHeader, Encoding, RecordKey, Columns?.Count);
}
