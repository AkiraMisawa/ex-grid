using System.Globalization;

namespace ExSheet.Engine;

/// <summary>The four kinds of Value, as in Excel (ADR-0047). A blank cell is not a fifth kind: it has no Value.</summary>
public enum ValueKind
{
    /// <summary>An IEEE double, as in Excel. A date is a number shown with a date format.</summary>
    Number,

    /// <summary>Text.</summary>
    Text,

    /// <summary><c>TRUE</c> or <c>FALSE</c>.</summary>
    Boolean,

    /// <summary>An <see cref="ErrorValue"/>.</summary>
    Error,
}

/// <summary>
/// What a cell evaluates to: a number, text, a boolean or an Error Value (CONTEXT.md). A
/// constant Entry is its own Value; a Formula's Value is its result. Values are never recorded
/// in a Sheet Document (ADR-0048).
/// </summary>
public readonly struct Value : IEquatable<Value>
{
    private readonly double _number;
    private readonly string? _text;

    private Value(ValueKind kind, double number, string? text)
    {
        Kind = kind;
        _number = number;
        _text = text;
    }

    /// <summary>Which of the four kinds this Value is.</summary>
    public ValueKind Kind { get; }

    /// <summary>A number. NaN and the infinities are not numbers a cell can hold; Excel's answer to them is <c>#NUM!</c>.</summary>
    public static Value FromNumber(double number) =>
        double.IsFinite(number)
            ? new(ValueKind.Number, number == 0 ? 0 : number, null)
            : throw new ArgumentOutOfRangeException(nameof(number), number, "A Value is a finite number; Excel's answer to anything else is #NUM!.");

    /// <summary>Text, possibly empty.</summary>
    public static Value FromText(string text) => new(ValueKind.Text, 0, text ?? throw new ArgumentNullException(nameof(text)));

    /// <summary><c>TRUE</c> or <c>FALSE</c>.</summary>
    public static Value FromBoolean(bool value) => new(ValueKind.Boolean, value ? 1 : 0, null);

    /// <summary>An Error Value.</summary>
    public static Value FromError(ErrorValue error) => new(ValueKind.Error, (int)error, null);

    /// <summary>Whether this Value is an Error Value.</summary>
    public bool IsError => Kind == ValueKind.Error;

    /// <summary>The number; throws unless <see cref="Kind"/> is <see cref="ValueKind.Number"/>.</summary>
    public double Number => Kind == ValueKind.Number ? _number : throw Wrong(ValueKind.Number);

    /// <summary>The text; throws unless <see cref="Kind"/> is <see cref="ValueKind.Text"/>.</summary>
    public string Text => Kind == ValueKind.Text ? _text! : throw Wrong(ValueKind.Text);

    /// <summary>The boolean; throws unless <see cref="Kind"/> is <see cref="ValueKind.Boolean"/>.</summary>
    public bool Boolean => Kind == ValueKind.Boolean ? _number != 0 : throw Wrong(ValueKind.Boolean);

    /// <summary>The Error Value; throws unless <see cref="Kind"/> is <see cref="ValueKind.Error"/>.</summary>
    public ErrorValue Error => Kind == ValueKind.Error ? (ErrorValue)(int)_number : throw Wrong(ValueKind.Error);

    private InvalidOperationException Wrong(ValueKind asked) => new($"This Value is {Kind}, not {asked}.");

    /// <summary>
    /// Exact equality: the same kind and the same number, text (ordinal, case-sensitive),
    /// boolean or Error Value. This is "did the Value change", not Excel's <c>=</c>, which
    /// ignores case.
    /// </summary>
    public bool Equals(Value other) =>
        Kind == other.Kind && _number.Equals(other._number) && string.Equals(_text, other._text, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Value other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Kind, _number, _text);

    /// <summary>Exact equality, as <see cref="Equals(Value)"/>.</summary>
    public static bool operator ==(Value left, Value right) => left.Equals(right);

    /// <summary>Exact inequality, as <see cref="Equals(Value)"/>.</summary>
    public static bool operator !=(Value left, Value right) => !left.Equals(right);

    /// <summary>
    /// A diagnostic form, culture-invariant (a number round-trips; text is as held). What a cell
    /// shows is <see cref="Sheet.GetDisplay(CellAddress, double)"/>'s, not this.
    /// </summary>
    public override string ToString() => Kind switch
    {
        ValueKind.Number => _number.ToString("R", CultureInfo.InvariantCulture),
        ValueKind.Text => _text!,
        ValueKind.Boolean => Boolean ? "TRUE" : "FALSE",
        _ => Error.ToText(),
    };
}
