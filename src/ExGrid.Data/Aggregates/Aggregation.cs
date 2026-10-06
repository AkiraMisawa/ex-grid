namespace ExGrid.Data;

/// <summary>
/// How a set of values is summarised — Excel's "Summarize Values By", and the figures of its
/// status bar (ADR-0060, ADR-0130). ExPivot's Value Fields and ExGrid's Selection Summary both
/// mean exactly this, through <see cref="AggregateArithmetic.Read"/>, so a pivot cell and the status
/// bar cannot disagree over the same values.
///
/// <para>What counts: a number — any integral, <c>decimal</c> or <c>double</c> value — is in every
/// Aggregation. Text, a Boolean and a date are counted by <see cref="Count"/> and are never a
/// number (ADR-0060/0064: a date is not a serial number here). A Blank is in none.</para>
/// </summary>
public enum Aggregation
{
    /// <summary>The sum of the numbers; <c>0</c> where there are values but none is a number.</summary>
    Sum = 0,

    /// <summary>How many values are not Blank, numbers or not — Excel's <c>COUNTA</c>.</summary>
    Count,

    /// <summary>The mean of the numbers; <c>#DIV/0!</c> where there are values but none is a number.</summary>
    Average,

    /// <summary>The largest number.</summary>
    Max,

    /// <summary>The smallest number.</summary>
    Min,

    /// <summary>The product of the numbers.</summary>
    Product,

    /// <summary>How many values are numbers — Excel's <c>COUNT</c>.</summary>
    CountNumbers,

    /// <summary>The sample standard deviation, of two or more numbers.</summary>
    StdDev,

    /// <summary>The population standard deviation.</summary>
    StdDevp,

    /// <summary>The sample variance, of two or more numbers.</summary>
    Var,

    /// <summary>The population variance.</summary>
    Varp,
}

/// <summary>The error an Aggregation answers with, where it answers with one (ADR-0060).</summary>
public enum AggregateError
{
    /// <summary>No error: the answer is empty or a number.</summary>
    None = 0,

    /// <summary><c>#DIV/0!</c>: an Average, a deviation or a variance of too few numbers.</summary>
    DivideByZero,

    /// <summary><c>#NUM!</c>: a non-finite number was among the values, or the answer is not finite.</summary>
    NotANumber,

    /// <summary>An error value was among the values (ExSheet's <c>#N/A</c> and the rest): every
    /// Aggregation but <see cref="Aggregation.Count"/> is that error, as in Excel (ADR-0130).</summary>
    ErrorAmongValues,
}

/// <summary>
/// An Aggregation's answer before it is shown: nothing (no value at all), a number — exact in
/// <c>decimal</c> where it could be — or an error (ADR-0060).
/// </summary>
public readonly struct AggregateResult : IEquatable<AggregateResult>
{
    private AggregateResult(bool isEmpty, double number, decimal? exact, AggregateError error)
    {
        IsEmpty = isEmpty;
        Number = number;
        Exact = exact;
        Error = error;
    }

    /// <summary>No value was summarised at all.</summary>
    public static AggregateResult Empty => new(true, 0, null, AggregateError.None);

    /// <summary>An error answer.</summary>
    public static AggregateResult Of(AggregateError error)
    {
        if (error == AggregateError.None)
            throw new ArgumentOutOfRangeException(nameof(error), error, "An error answer names an error.");
        return new(false, 0, null, error);
    }

    /// <summary>An exact answer.</summary>
    public static AggregateResult Of(decimal value) => new(false, (double)value, value, AggregateError.None);

    /// <summary>An answer in <c>double</c>, Excel's own arithmetic.</summary>
    public static AggregateResult Of(double value) => new(false, value, null, AggregateError.None);

    /// <summary>Whether nothing was summarised.</summary>
    public bool IsEmpty { get; }

    /// <summary>The number, as a <c>double</c> — the exact one converted where there is one.</summary>
    public double Number { get; }

    /// <summary>The exact <c>decimal</c>, where the Aggregation was computed in it.</summary>
    public decimal? Exact { get; }

    /// <summary>The error, or <see cref="AggregateError.None"/>.</summary>
    public AggregateError Error { get; }

    /// <summary>Whether the answer is a number.</summary>
    public bool IsNumber => !IsEmpty && Error == AggregateError.None;

    /// <summary>Equal when both are the same kind of answer with the same exact value, or the same
    /// <c>double</c> bits.</summary>
    public bool Equals(AggregateResult other)
        => IsEmpty == other.IsEmpty && Error == other.Error && Exact == other.Exact
            && BitConverter.DoubleToInt64Bits(Number) == BitConverter.DoubleToInt64Bits(other.Number);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is AggregateResult other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(IsEmpty, Error, Exact, Number);

    /// <summary>Whether two answers are equal.</summary>
    public static bool operator ==(AggregateResult left, AggregateResult right) => left.Equals(right);

    /// <summary>Whether two answers differ.</summary>
    public static bool operator !=(AggregateResult left, AggregateResult right) => !left.Equals(right);

    /// <summary>For messages and logs.</summary>
    public override string ToString() => IsEmpty ? "(empty)"
        : Error != AggregateError.None ? Error.ToString()
        : Exact is { } exact ? exact.ToString(System.Globalization.CultureInfo.InvariantCulture)
        : Number.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "d";
}
