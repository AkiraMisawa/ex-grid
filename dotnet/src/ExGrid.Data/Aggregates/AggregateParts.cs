namespace ExGrid.Data;

/// <summary>The counts every summarised set of values carries (ADR-0060/0066).</summary>
public struct AggregateCounts
{
    /// <summary>Values that are not Blank — Excel's <c>COUNTA</c>.</summary>
    public long Values;

    /// <summary>Values that are numbers — Excel's <c>COUNT</c> — non-finite ones included.</summary>
    public long Numbers;

    /// <summary>A non-finite number was among the values: every numeric Aggregation is
    /// <c>#NUM!</c>, and the other parts are not read.</summary>
    public bool NonFinite;
}

/// <summary>The sum part: exact while every number was, <c>double</c> once one was not or the exact
/// sum overflowed. <see cref="Compensation"/> is Neumaier's running error while a sum is being
/// accumulated; a finished part has folded it in (<see cref="AggregateArithmetic.Finish(ref AggregateSum)"/>).</summary>
public struct AggregateSum
{
    /// <summary>The exact sum, while <see cref="Inexact"/> is false.</summary>
    public decimal Exact;

    /// <summary>The <c>double</c> sum, once <see cref="Inexact"/>.</summary>
    public double Double;

    /// <summary>Neumaier's running error of <see cref="Double"/>.</summary>
    public double Compensation;

    /// <summary>Whether the sum has left exactness for Excel's <c>double</c>.</summary>
    public bool Inexact;
}

/// <summary>The extremes part: exact while every number was, <c>double</c> once one was not.</summary>
public struct AggregateExtremes
{
    /// <summary>The smallest number, while exact.</summary>
    public decimal ExactMin;

    /// <summary>The largest number, while exact.</summary>
    public decimal ExactMax;

    /// <summary>The smallest number, once <see cref="Inexact"/>.</summary>
    public double Min;

    /// <summary>The largest number, once <see cref="Inexact"/>.</summary>
    public double Max;

    /// <summary>Whether the extremes have left exactness for <c>double</c>.</summary>
    public bool Inexact;
}

/// <summary>The running variance: Welford's mean and sum of squared deviations, merged by Chan's
/// rule. Its count is <see cref="AggregateCounts.Numbers"/>.</summary>
public struct AggregateVariance
{
    /// <summary>The running mean.</summary>
    public double Mean;

    /// <summary>The running sum of squared deviations from the mean.</summary>
    public double M2;
}

/// <summary>
/// The arithmetic of the Aggregations' parts (ADR-0060/0066/0130): how one number is folded into
/// each part, how two parts of the same field merge, how a part is finished, and how an
/// Aggregation is read from the parts. Every part combines exactly — counts and exact sums add,
/// extremes compare, products multiply and running variances merge — so a total merged from its
/// pieces is the total of its values, never a total of totals. Money stays exact until it cannot:
/// then <c>double</c>, Excel's own arithmetic.
///
/// <para>This is the one definition: ExPivot's engine keeps its parts column by column and calls
/// these; ExGrid's Selection Summary keeps one of each in an <see cref="AggregateAccumulator"/>.</para>
/// </summary>
public static class AggregateArithmetic
{
    // ---- One number --------------------------------------------------------------------------

    /// <summary>Folds an exact number into a sum; <paramref name="first"/> when the sum held no
    /// number before.</summary>
    public static void Add(ref AggregateSum sum, decimal value, bool first)
    {
        if (sum.Inexact)
        {
            Neumaier(ref sum, (double)value);
            return;
        }
        if (first)
        {
            sum.Exact = value;
            return;
        }
        try
        {
            sum.Exact += value;
        }
        catch (OverflowException)
        {
            ToInexact(ref sum);
            Neumaier(ref sum, (double)value);
        }
    }

    /// <summary>Folds a finite <c>double</c> into a sum; <paramref name="first"/> when the sum
    /// held no number before.</summary>
    public static void Add(ref AggregateSum sum, double value, bool first)
    {
        if (first)
        {
            sum.Inexact = true;
            sum.Double = value;
            return;
        }
        if (!sum.Inexact)
            ToInexact(ref sum);
        Neumaier(ref sum, value);
    }

    /// <summary>A sum leaves exactness for <c>double</c>, starting from the exact sum it had,
    /// converted.</summary>
    public static void ToInexact(ref AggregateSum sum)
    {
        sum.Inexact = true;
        sum.Double = (double)sum.Exact;
        sum.Compensation = 0;
        sum.Exact = 0;
    }

    /// <summary>Adds to a <c>double</c> sum with Neumaier's compensation, so a total does not depend
    /// on the order its numbers arrived in beyond the last bit.</summary>
    public static void Neumaier(ref AggregateSum sum, double value)
    {
        var total = sum.Double + value;
        sum.Compensation += Math.Abs(sum.Double) >= Math.Abs(value)
            ? (sum.Double - total) + value
            : (value - total) + sum.Double;
        sum.Double = total;
    }

    /// <summary>Folds an exact number into extremes; <paramref name="first"/> when they held no
    /// number before.</summary>
    public static void Add(ref AggregateExtremes extremes, decimal value, bool first)
    {
        if (extremes.Inexact)
        {
            AddInexact(ref extremes, (double)value);
            return;
        }
        if (first)
        {
            extremes.ExactMin = value;
            extremes.ExactMax = value;
            return;
        }
        if (value < extremes.ExactMin)
            extremes.ExactMin = value;
        if (value > extremes.ExactMax)
            extremes.ExactMax = value;
    }

    /// <summary>Folds a finite <c>double</c> into extremes; <paramref name="first"/> when they held
    /// no number before.</summary>
    public static void Add(ref AggregateExtremes extremes, double value, bool first)
    {
        if (first)
        {
            extremes.Inexact = true;
            extremes.Min = value;
            extremes.Max = value;
            return;
        }
        if (!extremes.Inexact)
            ToInexact(ref extremes);
        AddInexact(ref extremes, value);
    }

    /// <summary>Folds a run's smallest and largest exact numbers into extremes;
    /// <paramref name="first"/> when they held no number before the run.</summary>
    public static void Fold(ref AggregateExtremes extremes, decimal min, decimal max, bool first)
    {
        if (first)
        {
            extremes.ExactMin = min;
            extremes.ExactMax = max;
        }
        else if (extremes.Inexact)
        {
            AddInexact(ref extremes, (double)min);
            AddInexact(ref extremes, (double)max);
        }
        else
        {
            if (min < extremes.ExactMin)
                extremes.ExactMin = min;
            if (max > extremes.ExactMax)
                extremes.ExactMax = max;
        }
    }

    // The conversion is monotonic, so the double extremes of exact numbers are their exact
    // extremes converted.
    private static void ToInexact(ref AggregateExtremes extremes)
    {
        extremes.Inexact = true;
        extremes.Min = (double)extremes.ExactMin;
        extremes.Max = (double)extremes.ExactMax;
        extremes.ExactMin = 0;
        extremes.ExactMax = 0;
    }

    private static void AddInexact(ref AggregateExtremes extremes, double value)
    {
        if (value < extremes.Min)
            extremes.Min = value;
        if (value > extremes.Max)
            extremes.Max = value;
    }

    /// <summary>Folds a number into a running variance by Welford's rule; <paramref name="count"/>
    /// is the numbers with this one.</summary>
    public static void Add(ref AggregateVariance variance, double value, long count)
    {
        var delta = value - variance.Mean;
        variance.Mean += delta / count;
        variance.M2 += delta * (value - variance.Mean);
    }

    // ---- Merging -----------------------------------------------------------------------------

    /// <summary>Merges another sum of the same field into this one; <paramref name="first"/> when
    /// this one held no number.</summary>
    public static void Merge(ref AggregateSum sum, in AggregateSum other, bool first)
    {
        if (first)
        {
            sum = other;
            return;
        }
        if (!sum.Inexact && !other.Inexact)
        {
            try
            {
                sum.Exact += other.Exact;
                return;
            }
            catch (OverflowException)
            {
                ToInexact(ref sum);
                Neumaier(ref sum, (double)other.Exact);
                return;
            }
        }
        if (!sum.Inexact)
            ToInexact(ref sum);
        if (other.Inexact)
        {
            Neumaier(ref sum, other.Double);
            sum.Compensation += other.Compensation;
        }
        else
        {
            Neumaier(ref sum, (double)other.Exact);
        }
    }

    /// <summary>Merges other extremes of the same field into these; <paramref name="first"/> when
    /// these held no number.</summary>
    public static void Merge(ref AggregateExtremes extremes, in AggregateExtremes other, bool first)
    {
        if (first)
        {
            extremes = other;
            return;
        }
        if (!extremes.Inexact && !other.Inexact)
        {
            if (other.ExactMin < extremes.ExactMin)
                extremes.ExactMin = other.ExactMin;
            if (other.ExactMax > extremes.ExactMax)
                extremes.ExactMax = other.ExactMax;
            return;
        }
        if (!extremes.Inexact)
            ToInexact(ref extremes);
        AddInexact(ref extremes, other.Inexact ? other.Min : (double)other.ExactMin);
        AddInexact(ref extremes, other.Inexact ? other.Max : (double)other.ExactMax);
    }

    /// <summary>Merges another running variance by Chan, Golub and LeVeque's pairwise rule;
    /// <paramref name="count"/> and <paramref name="otherCount"/> are each one's numbers.</summary>
    public static void Merge(ref AggregateVariance variance, in AggregateVariance other, long count, long otherCount)
    {
        if (count == 0)
        {
            variance = other;
            return;
        }
        var n = (double)count + otherCount;
        var delta = other.Mean - variance.Mean;
        variance.Mean += delta * otherCount / n;
        variance.M2 += other.M2 + (delta * delta * count * otherCount / n);
    }

    // ---- Finishing ---------------------------------------------------------------------------

    /// <summary>Ends a sum: a <c>double</c> takes its compensation in, the figures it does not use
    /// are zeroed and a NaN is the one NaN, so a part is one value whatever path it took.</summary>
    public static void Finish(ref AggregateSum sum)
    {
        if (sum.Inexact)
        {
            sum.Double = Canonical(sum.Double + sum.Compensation);
            sum.Compensation = 0;
            sum.Exact = 0;
        }
        else
        {
            sum.Double = 0;
            sum.Compensation = 0;
        }
    }

    /// <summary>Ends extremes, as <see cref="Finish(ref AggregateSum)"/> ends a sum.</summary>
    public static void Finish(ref AggregateExtremes extremes)
    {
        if (extremes.Inexact)
        {
            extremes.Min = Canonical(extremes.Min);
            extremes.Max = Canonical(extremes.Max);
            extremes.ExactMin = 0;
            extremes.ExactMax = 0;
        }
        else
        {
            extremes.Min = 0;
            extremes.Max = 0;
        }
    }

    /// <summary>Ends a running variance: a NaN is the one NaN.</summary>
    public static void Finish(ref AggregateVariance variance)
    {
        variance.Mean = Canonical(variance.Mean);
        variance.M2 = Canonical(variance.M2);
    }

    /// <summary>A NaN is the one NaN, so a part crosses JSON and compares unchanged.</summary>
    public static double Canonical(double value) => double.IsNaN(value) ? double.NaN : value;

    /// <summary>An exact value without trailing zeros (ADR-0064: a Decimal is a value, not the scale
    /// it was written with), so a sum reads alike however it was made.</summary>
    public static decimal Canonical(decimal value) => Storage.DecimalMath.Canonical(value);

    // ---- Reading an Aggregation ----------------------------------------------------------------

    /// <summary>
    /// An Aggregation's answer from its parts, by the table in ADR-0060. A part the Aggregation
    /// does not read may be null; one it reads must be given. Count and Count Numbers read the
    /// counts alone; Sum and Average the sum; Max and Min the extremes; Product the product;
    /// StdDev, StdDevp, Var and Varp the running variance.
    /// </summary>
    public static AggregateResult Read(
        Aggregation aggregation,
        in AggregateCounts counts,
        AggregateSum? sum = null,
        AggregateExtremes? extremes = null,
        double? product = null,
        AggregateVariance? variance = null)
    {
        if (counts.Values == 0)
            return AggregateResult.Empty;
        if (aggregation == Aggregation.Count)
            return AggregateResult.Of((decimal)counts.Values);
        if (aggregation == Aggregation.CountNumbers)
            return AggregateResult.Of((decimal)counts.Numbers);

        var noNumber = counts.Numbers == 0;
        switch (aggregation)
        {
            case Aggregation.Sum:
            case Aggregation.Max:
            case Aggregation.Min:
            case Aggregation.Product:
                if (noNumber)
                    return AggregateResult.Of(0m);
                break;
            case Aggregation.Average:
            case Aggregation.StdDev:
            case Aggregation.StdDevp:
            case Aggregation.Var:
            case Aggregation.Varp:
                if (noNumber)
                    return AggregateResult.Of(AggregateError.DivideByZero);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(aggregation), aggregation, "Unknown Aggregation.");
        }
        if (counts.NonFinite)
            return AggregateResult.Of(AggregateError.NotANumber);

        var n = counts.Numbers;
        switch (aggregation)
        {
            case Aggregation.Sum:
            {
                var s = Need(sum, aggregation);
                return s.Inexact ? Checked(s.Double + s.Compensation) : AggregateResult.Of(s.Exact);
            }
            case Aggregation.Average:
            {
                var s = Need(sum, aggregation);
                return s.Inexact ? Checked((s.Double + s.Compensation) / n) : AggregateResult.Of(s.Exact / n);
            }
            case Aggregation.Max:
            {
                var e = Need(extremes, aggregation);
                return e.Inexact ? Checked(e.Max) : AggregateResult.Of(e.ExactMax);
            }
            case Aggregation.Min:
            {
                var e = Need(extremes, aggregation);
                return e.Inexact ? Checked(e.Min) : AggregateResult.Of(e.ExactMin);
            }
            case Aggregation.Product:
                return Checked(Need(product, aggregation));
            case Aggregation.StdDev:
                return n < 2 ? AggregateResult.Of(AggregateError.DivideByZero)
                    : Checked(Math.Sqrt(Math.Max(0, Need(variance, aggregation).M2) / (n - 1)));
            case Aggregation.StdDevp:
                return Checked(Math.Sqrt(Math.Max(0, Need(variance, aggregation).M2) / n));
            case Aggregation.Var:
                return n < 2 ? AggregateResult.Of(AggregateError.DivideByZero)
                    : Checked(Math.Max(0, Need(variance, aggregation).M2) / (n - 1));
            default:
                return Checked(Math.Max(0, Need(variance, aggregation).M2) / n);
        }
    }

    private static T Need<T>(T? part, Aggregation aggregation) where T : struct
        => part ?? throw new InvalidOperationException($"{aggregation} reads a part that was not accumulated (ADR-0066).");

    private static AggregateResult Checked(double value)
        => double.IsFinite(value) ? AggregateResult.Of(value) : AggregateResult.Of(AggregateError.NotANumber);
}
