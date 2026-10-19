using System.Globalization;

namespace ExChart.Engine;

/// <summary>
/// A series' value, held in the type it was handed in — <see cref="decimal"/> or <see cref="double"/> —
/// so the tooltip and the CSV write the digits the cell shows; it becomes a <see cref="double"/> only
/// where a position is computed (ADR-0251).
/// </summary>
public readonly record struct ChartValue
{
    private readonly decimal _decimal;
    private readonly double _double;

    private ChartValue(decimal value)
    {
        _decimal = value;
        IsDecimal = true;
    }

    private ChartValue(double value) => _double = value;

    /// <summary>Whether the value was handed in as a <see cref="decimal"/>.</summary>
    public bool IsDecimal { get; }

    /// <summary>The value as a <see cref="double"/>, for geometry only.</summary>
    public double ToDouble() => IsDecimal ? (double)_decimal : _double;

    /// <summary>The value as handed in, boxed.</summary>
    public object Value => IsDecimal ? _decimal : _double;

    /// <summary>A value handed in as a <see cref="decimal"/>.</summary>
    public static ChartValue Of(decimal value) => new(value);

    /// <summary>A value handed in as a <see cref="double"/>; NaN and the infinities are refused.</summary>
    public static ChartValue Of(double value) =>
        double.IsFinite(value) ? new(value) : throw new ArgumentOutOfRangeException(nameof(value), value, "A chart value is finite; a missing value is a gap, handed in as null.");

    /// <summary>A value handed in as a <see cref="decimal"/>.</summary>
    public static implicit operator ChartValue(decimal value) => Of(value);

    /// <summary>A value handed in as a <see cref="double"/>.</summary>
    public static implicit operator ChartValue(double value) => Of(value);

    /// <summary>The value written invariantly, with every digit it was handed in with.</summary>
    public string ToInvariantString() =>
        IsDecimal ? _decimal.ToString(CultureInfo.InvariantCulture) : _double.ToString("R", CultureInfo.InvariantCulture);

    /// <inheritdoc />
    public override string ToString() => ToInvariantString();
}
