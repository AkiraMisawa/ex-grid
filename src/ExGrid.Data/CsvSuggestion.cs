using System.Globalization;

namespace ExGrid.Data;

/// <summary>
/// A Schema suggested for a file nobody has described, from its first rows (ADR-0063, Q33), with every
/// column whose reading is not clear marked, for the user to confirm. It is a proposal: nothing reads
/// the file under it until <see cref="Schema"/> — or a Schema changed from it — is handed to
/// <see cref="CsvSchema.ReadAsync(Stream, SnapshotLoadOptions?, CancellationToken)"/>.
/// </summary>
public sealed class CsvSuggestion
{
    internal CsvSuggestion(CsvSchema schema, IReadOnlyList<CsvColumnSuggestion> columns, IReadOnlyList<CsvMark> marks, int sampledRows)
    {
        Schema = schema;
        Columns = columns;
        Marks = marks;
        SampledRows = sampledRows;
    }

    /// <summary>The Schema suggested: every column of the file, in the file's order, with the file's
    /// separator, header row and encoding. Change it with <c>with</c> before handing it back.</summary>
    public CsvSchema Schema { get; }

    /// <summary>What was suggested for each column, and why, one for each of
    /// <see cref="CsvSchema.Columns"/>, in the same order.</summary>
    public IReadOnlyList<CsvColumnSuggestion> Columns { get; }

    /// <summary>What is not clear about the file as a whole: its separator, its header row, records of
    /// another length. Empty when nothing is.</summary>
    public IReadOnlyList<CsvMark> Marks { get; }

    /// <summary>The data records the suggestion was made from.</summary>
    public int SampledRows { get; }

    /// <summary>Whether anything is marked, about the file or about a column.</summary>
    public bool IsUnclear => Marks.Count > 0 || Columns.Any(c => c.IsUnclear);
}

/// <summary>What a suggestion proposes for one column of the file, and what about it is not clear.</summary>
public sealed class CsvColumnSuggestion
{
    internal CsvColumnSuggestion(CsvColumn column, IReadOnlyList<CsvMark> marks, IReadOnlyList<string> examples)
    {
        Column = column;
        Marks = marks;
        Examples = examples;
    }

    /// <summary>The column as suggested: as it stands in <see cref="CsvSuggestion.Schema"/>.</summary>
    public CsvColumn Column { get; }

    /// <summary>Why the column's kind or reading is not clear; empty when it is.</summary>
    public IReadOnlyList<CsvMark> Marks { get; }

    /// <summary>Whether the column is marked.</summary>
    public bool IsUnclear => Marks.Count > 0;

    /// <summary>A few of the column's values, as written, to show the user beside the suggestion.</summary>
    public IReadOnlyList<string> Examples { get; }
}

/// <summary>Something a suggestion is not sure of, with a sentence to show the user that says what,
/// with an example from the file.</summary>
/// <param name="Doubt">What kind of doubt it is.</param>
/// <param name="Note">The doubt in a sentence, naming what was seen and what was suggested.</param>
public sealed record CsvMark(CsvDoubt Doubt, string Note);

/// <summary>Why a suggested reading is not clear (ADR-0063, Q33).</summary>
public enum CsvDoubt
{
    /// <summary>The column is empty in every sampled row, so nothing says what it holds; it is suggested
    /// as Text.</summary>
    Empty,

    /// <summary>Digits with a leading zero, such as <c>00123</c>: suggested as Text, kept exactly, since
    /// they could be an identifier rather than a number.</summary>
    LeadingZeros,

    /// <summary>Numbers written with both a comma and a full stop: one is suggested as the decimal point
    /// and the other as the thousands separator.</summary>
    BothSeparators,

    /// <summary>A comma or a full stop that could be either the decimal point or the thousands
    /// separator, as in <c>1,234</c>.</summary>
    AmbiguousSeparator,

    /// <summary>Dates written in more than one format.</summary>
    MixedDateFormats,

    /// <summary>Dates whose day and month could be either way round, as in <c>01/02/2026</c>.</summary>
    DayOrMonth,

    /// <summary>Most values are of one kind, but some are not; the column is suggested as Text.</summary>
    MixedKinds,

    /// <summary>Texts such as <c>NULL</c> or <c>-</c> stand among values of another kind, and are
    /// suggested as Blanks.</summary>
    BlankText,

    /// <summary>Only <c>0</c> and <c>1</c>, or <c>yes</c> and <c>no</c>: the column could be a Boolean.</summary>
    CouldBeBoolean,

    /// <summary>Numbers with more digits than an Integer or a Decimal holds; suggested as Text.</summary>
    TooLong,

    /// <summary>The header is empty or repeated at this column, which is therefore matched by its
    /// position and given a name of its own.</summary>
    HeaderName,

    /// <summary>About the file: the separator could not be told for sure.</summary>
    Separator,

    /// <summary>About the file: whether the first row is a header could not be told for sure.</summary>
    Header,

    /// <summary>About the file: some records have more or fewer fields than the first, and reading the
    /// file under the Schema refuses them.</summary>
    FieldCount,
}

/// <summary>How a Schema is suggested: how much of the file to sample, and what the caller already
/// knows of it.</summary>
public sealed class CsvSuggestionOptions
{
    /// <summary>The data records to sample from the start of the file. 1,000 by default.</summary>
    public int Rows { get; init; } = 1_000;

    /// <summary>The encoding of a file that does not begin with a byte-order mark; UTF-8 by default.
    /// The sample is decoded strictly, so a file in another encoding is refused rather than misread.</summary>
    public CsvEncoding Encoding { get; init; } = CsvEncoding.Utf8;

    /// <summary>The separator, when the caller knows it; otherwise it is told from the sample.</summary>
    public CsvSeparator? Separator { get; init; }

    /// <summary>Whether the first record is a header row, when the caller knows; otherwise it is told
    /// from the sample.</summary>
    public bool? HasHeader { get; init; }

    /// <summary>The culture the user writes in, which decides what is ambiguous: whether <c>1,234</c> is
    /// suggested with a decimal comma, and <c>01/02/2026</c> with the day first. The invariant culture
    /// when <see langword="null"/>.</summary>
    public CultureInfo? Culture { get; init; }
}
