using System.Globalization;

namespace ExPivot.Engine;

/// <summary>
/// One value cell of a Pivot Report (ADR-0059): a number or an error value, with the text it is
/// shown as under its Value Field's number format and the report's culture. An empty cell is
/// null, never a value.
///
/// <para>It is <see cref="IFormattable"/> so that a grid's raw, locale-free form of it is the
/// number itself: <c>ToString(null, CultureInfo.InvariantCulture)</c> is the exact
/// <c>decimal</c> or the shortest round-trip <c>double</c>, which Excel reads back as the same
/// number, and an error value's own text. <see cref="ToString()"/> is the shown text.</para>
/// </summary>
public sealed class PivotValue : IFormattable
{
    /// <summary>Excel's error value for a division by zero.</summary>
    public const string DivideByZeroText = "#DIV/0!";

    private PivotValue(double number, decimal? exact, string? error, string text)
    {
        Number = number;
        Exact = exact;
        Error = error;
        Text = text;
    }

    /// <summary>The number, as a <c>double</c>; 0 for an error value.</summary>
    public double Number { get; }

    /// <summary>The exact <c>decimal</c>, where the Aggregation was computed in it — the sum of
    /// money, never rounded through <c>double</c> (ADR-0059).</summary>
    public decimal? Exact { get; }

    /// <summary>The error value — <c>#DIV/0!</c>, <c>#NUM!</c> — or null for a number.</summary>
    public string? Error { get; }

    /// <summary>Whether this is an error value.</summary>
    public bool IsError => Error is not null;

    /// <summary>The text the cell shows.</summary>
    public string Text { get; }

    /// <summary>The shown text.</summary>
    public override string ToString() => Text;

    /// <summary>The number in the given format and culture; an error value's own text whatever
    /// the format. With a null format and the invariant culture, the raw form a copy carries.</summary>
    public string ToString(string? format, IFormatProvider? formatProvider)
        => Error ?? (Exact is { } exact ? exact.ToString(format, formatProvider) : Number.ToString(format, formatProvider));

    internal static PivotValue From(AggregateValue value, string? numberFormat, bool percent, CultureInfo culture)
    {
        if (value.Error is { } error)
            return new PivotValue(0, null, error, error);
        var format = numberFormat ?? (percent ? "0.00%" : "G15");
        var text = value.Exact is { } exact
            ? exact.ToString(format, culture)
            : value.Number.ToString(format, culture);
        return new PivotValue(value.Number, value.Exact, null, text);
    }
}
