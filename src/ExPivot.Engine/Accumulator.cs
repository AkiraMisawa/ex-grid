namespace ExPivot.Engine;

/// <summary>
/// What one cell of the cube keeps of one field's values (ADR-0059): enough to read every
/// Aggregation from, so a Value Field changed from Sum to Average is read again, never
/// aggregated again. Counts, an exact <c>decimal</c> sum, a compensated <c>double</c> sum, the
/// extremes in both, the product, and the running mean and squared deviation for the
/// variances — each of which merges, so a cell's totals are merged from its leaves rather than
/// from a second pass over the records.
/// </summary>
internal struct Accumulator
{
    /// <summary>Records in the cell, whatever their value.</summary>
    public int Records;

    /// <summary>Values that are not Blank — Excel's <c>COUNTA</c>.</summary>
    public int NonBlank;

    /// <summary>Values that are numbers — Excel's <c>COUNT</c>.</summary>
    public int Numbers;

    /// <summary>A <c>double</c> or <c>float</c> was among the numbers, so <c>decimal</c> is not exact.</summary>
    public bool AnyDouble;

    /// <summary>The <c>decimal</c> sum overflowed; <c>double</c> is Excel's own arithmetic from here.</summary>
    public bool DecimalOverflow;

    /// <summary>A non-finite number was among the values: <c>#NUM!</c>.</summary>
    public bool NonFinite;

    public decimal DecimalSum;
    public decimal DecimalMin;
    public decimal DecimalMax;

    // Neumaier's compensated sum, so a cell's double total does not depend on the order its
    // parts were merged in beyond the last bit.
    public double Sum;
    public double SumCompensation;
    public double Min;
    public double Max;
    public double Product;

    // Welford's running mean and sum of squared deviations, merged by Chan's rule.
    public double Mean;
    public double M2;

    /// <summary>Whether the <c>decimal</c> figures are exact and to be read.</summary>
    public readonly bool Exact => !AnyDouble && !DecimalOverflow;

    /// <summary>Folds in one record's value.</summary>
    public void Add(object? value)
    {
        Records++;
        if (value is null)
            return;
        NonBlank++;
        switch (value)
        {
            case decimal m: AddDecimal(m); break;
            case int i: AddDecimal(i); break;
            case long l: AddDecimal(l); break;
            case short s: AddDecimal(s); break;
            case byte b: AddDecimal(b); break;
            case sbyte s: AddDecimal(s); break;
            case uint u: AddDecimal(u); break;
            case ulong u: AddDecimal(u); break;
            case ushort u: AddDecimal(u); break;
            case double d: AddDouble(d); break;
            case float f: AddDouble(f); break;
            // Text, a Boolean, a date: counted, never a number (ADR-0059).
        }
    }

    private void AddDecimal(decimal value)
    {
        if (!DecimalOverflow)
        {
            try
            {
                DecimalSum = Numbers == 0 ? value : checked(DecimalSum + value);
            }
            catch (OverflowException)
            {
                DecimalOverflow = true;
            }
        }
        if (Numbers == 0 || value < DecimalMin) DecimalMin = value;
        if (Numbers == 0 || value > DecimalMax) DecimalMax = value;
        AddNumber((double)value);
    }

    private void AddDouble(double value)
    {
        AnyDouble = true;
        if (!double.IsFinite(value))
        {
            NonFinite = true;
            Numbers++;
            return;
        }
        AddNumber(value);
    }

    private void AddNumber(double value)
    {
        if (Numbers == 0)
        {
            Min = value;
            Max = value;
            Product = value;
        }
        else
        {
            if (value < Min) Min = value;
            if (value > Max) Max = value;
            Product *= value;
        }
        AddToSum(value);
        Numbers++;
        var delta = value - Mean;
        Mean += delta / Numbers;
        M2 += delta * (value - Mean);
    }

    private void AddToSum(double value)
    {
        var t = Sum + value;
        SumCompensation += Math.Abs(Sum) >= Math.Abs(value) ? (Sum - t) + value : (value - t) + Sum;
        Sum = t;
    }

    /// <summary>Folds in another cell's accumulation of the same field.</summary>
    public void Merge(in Accumulator other)
    {
        Records += other.Records;
        NonBlank += other.NonBlank;
        if (other.Numbers == 0)
            return;
        AnyDouble |= other.AnyDouble;
        NonFinite |= other.NonFinite;
        if (Numbers == 0)
        {
            var records = Records;
            var nonBlank = NonBlank;
            this = other;
            Records = records;
            NonBlank = nonBlank;
            return;
        }

        DecimalOverflow |= other.DecimalOverflow;
        if (!DecimalOverflow)
        {
            try
            {
                DecimalSum = checked(DecimalSum + other.DecimalSum);
            }
            catch (OverflowException)
            {
                DecimalOverflow = true;
            }
        }
        if (other.DecimalMin < DecimalMin) DecimalMin = other.DecimalMin;
        if (other.DecimalMax > DecimalMax) DecimalMax = other.DecimalMax;
        if (other.Min < Min) Min = other.Min;
        if (other.Max > Max) Max = other.Max;
        Product *= other.Product;
        AddToSum(other.Sum);
        SumCompensation += other.SumCompensation;

        var n = (double)Numbers + other.Numbers;
        var delta = other.Mean - Mean;
        Mean += delta * other.Numbers / n;
        M2 += other.M2 + (delta * delta * Numbers * other.Numbers / n);
        Numbers += other.Numbers;
    }

    /// <summary>The Aggregation's answer, by the table in ADR-0059.</summary>
    public readonly AggregateValue Read(PivotAggregation aggregation)
    {
        if (NonBlank == 0)
            return AggregateValue.Empty;
        if (aggregation == PivotAggregation.Count)
            return AggregateValue.Of((decimal)NonBlank);
        if (aggregation == PivotAggregation.CountNumbers)
            return AggregateValue.Of((decimal)Numbers);

        var noNumber = Numbers == 0;
        switch (aggregation)
        {
            case PivotAggregation.Sum:
            case PivotAggregation.Max:
            case PivotAggregation.Min:
            case PivotAggregation.Product:
                if (noNumber)
                    return AggregateValue.Of(0m);
                break;
            case PivotAggregation.Average:
            case PivotAggregation.StdDev:
            case PivotAggregation.StdDevp:
            case PivotAggregation.Var:
            case PivotAggregation.Varp:
                if (noNumber)
                    return AggregateValue.DivideByZero;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(aggregation), aggregation, "Unknown PivotAggregation.");
        }
        if (NonFinite)
            return AggregateValue.NumberError;

        switch (aggregation)
        {
            case PivotAggregation.Sum:
                return Exact ? AggregateValue.Of(DecimalSum) : Checked(Sum + SumCompensation);
            case PivotAggregation.Average:
                return Exact ? AggregateValue.Of(DecimalSum / Numbers) : Checked((Sum + SumCompensation) / Numbers);
            case PivotAggregation.Max:
                return Exact ? AggregateValue.Of(DecimalMax) : Checked(Max);
            case PivotAggregation.Min:
                return Exact ? AggregateValue.Of(DecimalMin) : Checked(Min);
            case PivotAggregation.Product:
                return Checked(Product);
            case PivotAggregation.StdDev:
                return Numbers < 2 ? AggregateValue.DivideByZero : Checked(Math.Sqrt(Math.Max(0, M2) / (Numbers - 1)));
            case PivotAggregation.StdDevp:
                return Checked(Math.Sqrt(Math.Max(0, M2) / Numbers));
            case PivotAggregation.Var:
                return Numbers < 2 ? AggregateValue.DivideByZero : Checked(Math.Max(0, M2) / (Numbers - 1));
            default:
                return Checked(Math.Max(0, M2) / Numbers);
        }
    }

    private static AggregateValue Checked(double value)
        => double.IsFinite(value) ? AggregateValue.Of(value) : AggregateValue.NumberError;
}

/// <summary>An Aggregation's answer before it is shown: nothing, a number (exact in
/// <c>decimal</c> where it could be) or an error value (ADR-0059).</summary>
internal readonly struct AggregateValue
{
    private AggregateValue(bool isEmpty, double number, decimal? exact, string? error)
    {
        IsEmpty = isEmpty;
        Number = number;
        Exact = exact;
        Error = error;
    }

    public static AggregateValue Empty => new(true, 0, null, null);

    public static AggregateValue DivideByZero => new(false, 0, null, PivotValue.DivideByZeroText);

    public static AggregateValue NumberError => new(false, 0, null, PivotItemKey.ErrorText);

    public static AggregateValue Of(decimal value) => new(false, (double)value, value, null);

    public static AggregateValue Of(double value) => new(false, value, null, null);

    public bool IsEmpty { get; }

    /// <summary>The number, as a <c>double</c> — the exact one rounded where there is one.</summary>
    public double Number { get; }

    /// <summary>The exact <c>decimal</c>, where the Aggregation was computed in it.</summary>
    public decimal? Exact { get; }

    /// <summary>The error value's text, or null.</summary>
    public string? Error { get; }

    public bool IsError => Error is not null;
}
