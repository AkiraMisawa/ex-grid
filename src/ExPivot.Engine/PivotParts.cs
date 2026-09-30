using System.Globalization;

namespace ExPivot.Engine;

/// <summary>
/// The parts of a Leaf Aggregate a question asks for, for one field in Values (ADR-0065). The
/// counts always travel: the records at the leaf, the values that are not Blank, the numbers,
/// and whether any number was non-finite. The others travel only when asked, because only the
/// parts asked for are accumulated (ADR-0059, "Changed when decided").
/// <see cref="PivotQuery.PartsOf"/> says which parts an Aggregation reads.
/// </summary>
[Flags]
public enum PivotParts
{
    /// <summary>The counts alone, which travel with every field in Values whatever else is asked:
    /// Count and Count Numbers read nothing more. Zero, so it is part of every combination.</summary>
    Counts = 0,

    /// <summary>The sum of the numbers — exact in <c>decimal</c> while every number was, a
    /// <c>double</c> otherwise. Sum and Average read it.</summary>
    Sum = 1,

    /// <summary>The smallest and the largest number, exact or <c>double</c> as the sum is. Max
    /// and Min read them.</summary>
    Extremes = 2,

    /// <summary>The product of the numbers, in <c>double</c>. Product reads it.</summary>
    Product = 4,

    /// <summary>The numbers' running mean and sum of squared deviations, in <c>double</c>,
    /// merged by Chan's rule. StdDev, StdDevp, Var and Varp read it.</summary>
    Variance = 8,
}

/// <summary>
/// A number a part carries (ADR-0059/0065): exact, as a <c>decimal</c>, while every number it
/// was made of was an integral or <c>decimal</c> value and the sum did not overflow; a
/// <c>double</c> — Excel's own arithmetic — otherwise. A sum or an extreme is one of these at
/// every leaf.
/// </summary>
public readonly struct PivotNumber : IEquatable<PivotNumber>
{
    private readonly decimal _exact;
    private readonly double _double;

    private PivotNumber(bool isExact, decimal exact, double value)
    {
        IsExact = isExact;
        _exact = exact;
        _double = value;
    }

    /// <summary>An exact number.</summary>
    public static PivotNumber Exact(decimal value) => new(true, value, 0);

    /// <summary>A number in <c>double</c>; non-finite values included, which a report shows as
    /// <c>#NUM!</c>.</summary>
    public static PivotNumber Double(double value) => new(false, 0, value);

    /// <summary>Whether the number is exact.</summary>
    public bool IsExact { get; }

    /// <summary>The exact number. Refused for a <c>double</c>: rounding one to a <c>decimal</c>
    /// would claim an exactness it never had.</summary>
    public decimal ExactValue => IsExact
        ? _exact
        : throw new InvalidOperationException($"{this} is a double, not an exact number.");

    /// <summary>The number as a <c>double</c> — the exact one converted.</summary>
    public double Value => IsExact ? (double)_exact : _double;

    /// <summary>Equal when both are exact and equal as <c>decimal</c>s, or both are
    /// <c>double</c>s with the same bits (so <c>NaN</c> equals itself and <c>-0</c> is not
    /// <c>0</c>) — the equality "to the last bit" that sources are held to.</summary>
    public bool Equals(PivotNumber other)
        => IsExact == other.IsExact
            && (IsExact
                ? _exact == other._exact
                : BitConverter.DoubleToInt64Bits(_double) == BitConverter.DoubleToInt64Bits(other._double));

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PivotNumber other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => IsExact ? _exact.GetHashCode() : _double.GetHashCode();

    /// <summary>Whether two numbers are equal (<see cref="Equals(PivotNumber)"/>).</summary>
    public static bool operator ==(PivotNumber left, PivotNumber right) => left.Equals(right);

    /// <summary>Whether two numbers differ (<see cref="Equals(PivotNumber)"/>).</summary>
    public static bool operator !=(PivotNumber left, PivotNumber right) => !left.Equals(right);

    /// <summary><c>180</c> for an exact number, <c>0.30000000000000004d</c> for a double — for
    /// messages and logs.</summary>
    public override string ToString() => IsExact
        ? _exact.ToString(CultureInfo.InvariantCulture)
        : _double.ToString("R", CultureInfo.InvariantCulture) + "d";
}
