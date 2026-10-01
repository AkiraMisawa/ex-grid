using System.Globalization;

namespace ExGrid.Data;

/// <summary>
/// One column of a CSV's Schema (ADR-0063): which field of the file it is, the Snapshot column it
/// becomes, and how its text is read. Nothing is guessed: a value its kind cannot read under the
/// declared reading fails the load, naming the row and the column, so the account number
/// <c>00123</c> read as Text stays <c>00123</c>.
/// <para>
/// An empty field is a Blank in every kind, quoted or not, as Excel reads it; so is each declared
/// <see cref="BlankText"/>. Text is kept exactly as written. The other kinds are read with the ASCII
/// spaces around the value set aside.
/// </para>
/// </summary>
/// <param name="Name">The Snapshot column's name, unique within the Schema. It is also the header
/// the column matches, unless <see cref="Header"/> says otherwise.</param>
/// <param name="Kind">The kind every value of the column is read as.</param>
public sealed record CsvColumn(string Name, SnapshotKind Kind)
{
    /// <summary>
    /// The formats a Date column reads when it declares none: ISO 8601's date, or its date and time
    /// with a <c>T</c> or a space between them and up to seven decimal places of a second, with no
    /// offset.
    /// </summary>
    public static IReadOnlyList<string> IsoDateFormats { get; } = Array.AsReadOnly(
    [
        "yyyy-MM-dd",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss.FFFFFFF",
        "yyyy-MM-dd HH:mm:ss.FFFFFFF",
    ]);

    /// <summary>The spellings of true a Boolean column reads when it declares none: <c>TRUE</c>, as
    /// Excel writes it.</summary>
    public static IReadOnlyList<string> ExcelTrue { get; } = Array.AsReadOnly(["TRUE"]);

    /// <summary>The spellings of false a Boolean column reads when it declares none: <c>FALSE</c>, as
    /// Excel writes it.</summary>
    public static IReadOnlyList<string> ExcelFalse { get; } = Array.AsReadOnly(["FALSE"]);

    /// <summary>
    /// The header the column matches, exactly, when the file has a header row and the column declares
    /// no <see cref="Position"/>; <see cref="Name"/> when <see langword="null"/>. A declared column
    /// missing from the header fails the load by name; a column of the file the Schema does not
    /// declare is skipped.
    /// </summary>
    public string? Header { get; init; }

    /// <summary>
    /// The field the column is, counted from zero. It is how a column is matched in a file without a
    /// header row, where it defaults to the column's place among <see cref="CsvSchema.Columns"/>. In a
    /// file with one, a declared position is matched instead of the header.
    /// </summary>
    public int? Position { get; init; }

    /// <summary>The Snapshot column's caption; <see cref="Name"/> when <see langword="null"/>.</summary>
    public string? Caption { get; init; }

    /// <summary>
    /// The culture a number's decimal point, thousands separator and negative sign are read in, and a
    /// date's names, separators and calendar; the invariant culture when <see langword="null"/>.
    /// <see cref="DecimalPoint"/> and <see cref="ThousandsSeparator"/>, when declared, win over it.
    /// A hyphen-minus is read as the negative sign in every culture.
    /// </summary>
    public CultureInfo? Culture { get; init; }

    /// <summary>A number's decimal point: when <see langword="null"/>, the <see cref="Culture"/>'s, or
    /// <c>.</c> when no culture is declared.</summary>
    public string? DecimalPoint { get; init; }

    /// <summary>
    /// A number's thousands separator: when <see langword="null"/>, the <see cref="Culture"/>'s, or none
    /// when no culture is declared; the empty string for none at all. It is read only between groups
    /// of digits of the culture's sizes — three, unless the culture says otherwise — so <c>1,5</c> is
    /// refused rather than read as fifteen.
    /// </summary>
    public string? ThousandsSeparator { get; init; }

    /// <summary>
    /// The exact formats a Date column reads, tried in order, in .NET's custom format strings
    /// (<c>yyyy-MM-dd</c>, <c>dd.MM.yyyy HH:mm</c>); <see cref="IsoDateFormats"/> when
    /// <see langword="null"/>. A value with an offset (<c>zzz</c>, <c>K</c>) is held as the clock it
    /// shows, with its offset dropped (ADR-0063), and so is one marked UTC (<c>Z</c>, <c>GMT</c>),
    /// wherever it is read. A format without a date reads a time on the first day; one that would take
    /// part of a date from the day it is read — a month or a day without a year, an offset without a
    /// date — is refused.
    /// </summary>
    public IReadOnlyList<string>? DateFormats { get; init; }

    /// <summary>The spellings a Boolean column reads as true, matched ignoring case;
    /// <see cref="ExcelTrue"/> when <see langword="null"/>.</summary>
    public IReadOnlyList<string>? TrueText { get; init; }

    /// <summary>The spellings a Boolean column reads as false, matched ignoring case;
    /// <see cref="ExcelFalse"/> when <see langword="null"/>.</summary>
    public IReadOnlyList<string>? FalseText { get; init; }

    /// <summary>
    /// The texts that are a Blank in this column besides the empty field — <c>NULL</c>, <c>-</c> —
    /// matched exactly, quoted or not. When <see langword="null"/>, the Schema's
    /// <see cref="CsvSchema.BlankText"/>; a declared list, the empty one included, replaces it.
    /// </summary>
    public IReadOnlyList<string>? BlankText { get; init; }

    /// <summary>Whether two columns are declared alike, their lists compared item by item.</summary>
    public bool Equals(CsvColumn? other)
        => other is not null
            && Name == other.Name
            && Kind == other.Kind
            && Header == other.Header
            && Position == other.Position
            && Caption == other.Caption
            && Equals(Culture, other.Culture)
            && DecimalPoint == other.DecimalPoint
            && ThousandsSeparator == other.ThousandsSeparator
            && Same(DateFormats, other.DateFormats)
            && Same(TrueText, other.TrueText)
            && Same(FalseText, other.FalseText)
            && Same(BlankText, other.BlankText);

    /// <summary>A hash of what <see cref="Equals(CsvColumn?)"/> compares.</summary>
    public override int GetHashCode() => HashCode.Combine(Name, Kind, Header, Position, Caption);

    /// <summary>The header the column matches when it is matched by header.</summary>
    internal string MatchedHeader => Header ?? Name;

    internal static bool Same(IReadOnlyList<string>? first, IReadOnlyList<string>? second)
        => ReferenceEquals(first, second)
            || (first is not null && second is not null && first.SequenceEqual(second, StringComparer.Ordinal));
}
