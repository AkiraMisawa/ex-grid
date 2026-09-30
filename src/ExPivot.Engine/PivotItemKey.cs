using System.Globalization;

namespace ExPivot.Engine;

/// <summary>
/// An Item as a Pivot Layout writes it (ADR-0059): its kind and its invariant text — a number
/// in its shortest round-trip form (<c>1234.5</c>), a date as ISO (<c>2026-09-30T00:00:00</c>),
/// a Boolean as <c>TRUE</c> / <c>FALSE</c>, text as it is. A saved layout therefore names the
/// same Items under any culture. Two keys of text are equal ignoring case, as the Items they
/// name are.
/// </summary>
public sealed record PivotItemKey
{
    internal const string DateFormat = "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF";

    /// <summary>The Item <c>(blank)</c>.</summary>
    public static PivotItemKey Blank { get; } = new(PivotItemKind.Blank, null);

    /// <summary>The Item <c>#NUM!</c>: a number no one can hold.</summary>
    public static PivotItemKey Error { get; } = new(PivotItemKind.Error, ErrorText);

    internal const string ErrorText = "#NUM!";

    /// <summary>Names one Item. The value is refused unless it is the invariant text of its
    /// kind — a key that could not name any Item is a mapping bug, not an Item nobody has.</summary>
    /// <param name="kind">The Item's kind.</param>
    /// <param name="value">Its invariant text; null only for <see cref="PivotItemKind.Blank"/>.</param>
    public PivotItemKey(PivotItemKind kind, string? value)
    {
        switch (kind)
        {
            case PivotItemKind.Blank:
                if (value is not null)
                    throw new ArgumentException("A Blank Item has no value.", nameof(value));
                break;
            case PivotItemKind.Number:
                if (value is null || !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                    || !double.IsFinite(number))
                    throw new ArgumentException($"'{value}' is not the invariant text of a finite number.", nameof(value));
                break;
            case PivotItemKind.Date:
                if (value is null || !DateTime.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out _))
                    throw new ArgumentException($"'{value}' is not an ISO date ({DateFormat}).", nameof(value));
                break;
            case PivotItemKind.Boolean:
                if (value is not ("TRUE" or "FALSE"))
                    throw new ArgumentException($"'{value}' is not TRUE or FALSE.", nameof(value));
                break;
            case PivotItemKind.Text:
                ArgumentNullException.ThrowIfNull(value);
                break;
            case PivotItemKind.Error:
                if (value != ErrorText)
                    throw new ArgumentException($"The error Item is {ErrorText}.", nameof(value));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown PivotItemKind.");
        }

        Kind = kind;
        Value = value;
    }

    /// <summary>The Item's kind.</summary>
    public PivotItemKind Kind { get; }

    /// <summary>The invariant text; null for <see cref="PivotItemKind.Blank"/>.</summary>
    public string? Value { get; }

    /// <summary>The key of a text Item.</summary>
    public static PivotItemKey Text(string text) => new(PivotItemKind.Text, text);

    /// <summary>The key of a number Item.</summary>
    public static PivotItemKey Number(double number)
    {
        if (!double.IsFinite(number))
            return Error;
        if (number == 0)
            number = 0; // -0 is 0
        return new(PivotItemKind.Number, number.ToString("R", CultureInfo.InvariantCulture));
    }

    /// <summary>The key of a date Item, by its clock value.</summary>
    public static PivotItemKey Date(DateTime date)
        => new(PivotItemKind.Date, date.ToString(DateFormat, CultureInfo.InvariantCulture));

    /// <summary>The key of a Boolean Item.</summary>
    public static PivotItemKey Boolean(bool value) => new(PivotItemKind.Boolean, value ? "TRUE" : "FALSE");

    /// <summary>
    /// The key of the Item a value belongs to, as the engine sorts values into Items
    /// (ADR-0059): null is <see cref="Blank"/>; a string is text; a number of any .NET numeric
    /// type is a number, and a non-finite one is <see cref="Error"/>; <c>DateTime</c>,
    /// <c>DateOnly</c> and <c>DateTimeOffset</c> are dates by their clock value; a Boolean is
    /// itself; anything else is text, by its invariant text.
    /// </summary>
    public static PivotItemKey For(object? value) => ItemKey.Of(value).ToPublic();

    /// <summary>Equal when the kinds are equal and the texts are — ignoring case for text, as
    /// Items are told apart (ADR-0059).</summary>
    public bool Equals(PivotItemKey? other)
        => other is not null && Kind == other.Kind
            && (Kind == PivotItemKind.Text
                ? string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase)
                : string.Equals(Value, other.Value, StringComparison.Ordinal));

    /// <inheritdoc />
    public override int GetHashCode()
        => HashCode.Combine(Kind, Value is null ? 0
            : Kind == PivotItemKind.Text ? StringComparer.OrdinalIgnoreCase.GetHashCode(Value)
            : StringComparer.Ordinal.GetHashCode(Value));

    /// <summary><c>Text:East</c>, <c>Number:1234.5</c>, <c>Blank</c> — for messages and logs.</summary>
    public override string ToString() => Value is null ? Kind.ToString() : Kind + ":" + Value;

    internal DateTime DateValue => DateTime.ParseExact(Value!, DateFormat, CultureInfo.InvariantCulture);
}
