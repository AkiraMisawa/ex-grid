namespace ExPivot.Engine;

/// <summary>The counts every field in Values carries at a cell (ADR-0065).</summary>
internal struct CountsPart
{
    /// <summary>Values that are not Blank — Excel's <c>COUNTA</c>.</summary>
    public long Values;

    /// <summary>Values that are numbers — Excel's <c>COUNT</c> — non-finite ones included.</summary>
    public long Numbers;

    /// <summary>A non-finite number was among the values: every numeric Aggregation is
    /// <c>#NUM!</c>, and the other parts are not read.</summary>
    public bool NonFinite;
}

/// <summary>The sum part: exact while every number was, <c>double</c> once one was not or the
/// exact sum overflowed. <see cref="Compensation"/> is Neumaier's running error while a sum is
/// being accumulated; a finished part has folded it in. While an Integer or Decimal column is
/// read, its exact sum is kept apart as an integer (<see cref="ValueAccumulator"/>), and only a
/// finished part holds it in <see cref="Exact"/>.</summary>
internal struct SumPart
{
    public decimal Exact;
    public double Double;
    public double Compensation;
    public bool Inexact;
}

/// <summary>The extremes part: exact while every number was, <c>double</c> once one was not.</summary>
internal struct ExtremesPart
{
    public decimal ExactMin;
    public decimal ExactMax;
    public double Min;
    public double Max;
    public bool Inexact;
}

/// <summary>The running variance: Welford's mean and sum of squared deviations, merged by Chan's
/// rule. The count is <see cref="CountsPart.Numbers"/>.</summary>
internal struct VariancePart
{
    public double Mean;
    public double M2;
}

/// <summary>
/// One field in Values, held column by column over a set of cells — a question's leaves, or every
/// cell of a cube (ADR-0065). Only the parts asked for have columns, and only they are
/// accumulated (ADR-0059, "Changed when decided"). Every part combines exactly: counts and exact
/// sums add, extremes compare, products multiply and running variances merge, so a total merged
/// from its leaves is the total of its records (ADR-0059: never a total of totals).
/// </summary>
internal sealed class PartColumns
{
    public PartColumns(PivotParts parts, int capacity)
    {
        Parts = parts;
        Counts = new CountsPart[capacity];
        if ((parts & PivotParts.Sum) != 0)
            Sums = new SumPart[capacity];
        if ((parts & PivotParts.Extremes) != 0)
            Extremes = new ExtremesPart[capacity];
        if ((parts & PivotParts.Product) != 0)
            Products = new double[capacity];
        if ((parts & PivotParts.Variance) != 0)
            Variances = new VariancePart[capacity];
    }

    public PivotParts Parts { get; }

    public CountsPart[] Counts;
    public SumPart[]? Sums;
    public ExtremesPart[]? Extremes;
    public double[]? Products;
    public VariancePart[]? Variances;

    public int Capacity => Counts.Length;

    /// <summary>Makes room for <paramref name="count"/> cells, doubling as it grows.</summary>
    public void EnsureCapacity(int count)
    {
        if (count <= Counts.Length)
            return;
        var capacity = Math.Max(count, Math.Max(16, Counts.Length * 2));
        Array.Resize(ref Counts, capacity);
        if (Sums is not null)
            Array.Resize(ref Sums, capacity);
        if (Extremes is not null)
            Array.Resize(ref Extremes, capacity);
        if (Products is not null)
            Array.Resize(ref Products, capacity);
        if (Variances is not null)
            Array.Resize(ref Variances, capacity);
    }

    /// <summary>The first <paramref name="count"/> cells of <paramref name="other"/>, which holds
    /// at least these parts, as this one's first cells.</summary>
    public void CopyFrom(PartColumns other, int count)
    {
        EnsureCapacity(count);
        Array.Copy(other.Counts, Counts, count);
        if (Sums is not null)
            Array.Copy(other.Sums!, Sums, count);
        if (Extremes is not null)
            Array.Copy(other.Extremes!, Extremes, count);
        if (Products is not null)
            Array.Copy(other.Products!, Products, count);
        if (Variances is not null)
            Array.Copy(other.Variances!, Variances, count);
    }

    /// <summary>Cell <paramref name="from"/> of <paramref name="other"/>, which holds at least these
    /// parts, as this one's cell <paramref name="cell"/>.</summary>
    public void CopyCell(int cell, PartColumns other, int from)
    {
        Counts[cell] = other.Counts[from];
        if (Sums is not null)
            Sums[cell] = other.Sums![from];
        if (Extremes is not null)
            Extremes[cell] = other.Extremes![from];
        if (Products is not null)
            Products[cell] = other.Products![from];
        if (Variances is not null)
            Variances[cell] = other.Variances![from];
    }

    /// <summary>A cell back to no value at all, as a new cell starts.</summary>
    public void ResetCell(int cell)
    {
        Counts[cell] = default;
        if (Sums is not null)
            Sums[cell] = default;
        if (Extremes is not null)
            Extremes[cell] = default;
        if (Products is not null)
            Products[cell] = 0;
        if (Variances is not null)
            Variances[cell] = default;
    }

    /// <summary>
    /// Writes each exact sum and exact extreme of the first <paramref name="count"/> cells without
    /// trailing zeros (ADR-0063: a Decimal is a value, not the scale it was written with). A sum
    /// folded from segments at different scales then reads alike however it was made, and a
    /// batch folded in leaves it as a fresh aggregation would.
    /// </summary>
    public void Canonicalize(int count)
    {
        if (Sums is not null)
        {
            for (var i = 0; i < count; i++)
            {
                ref var sum = ref Sums[i];
                if (!sum.Inexact)
                    sum.Exact = Exactly.Canonical(sum.Exact);
            }
        }
        if (Extremes is not null)
        {
            for (var i = 0; i < count; i++)
            {
                ref var extremes = ref Extremes[i];
                if (!extremes.Inexact)
                {
                    extremes.ExactMin = Exactly.Canonical(extremes.ExactMin);
                    extremes.ExactMax = Exactly.Canonical(extremes.ExactMax);
                }
            }
        }
    }

    // ---- Exact numbers a run at a time ----------------------------------------------------------
    //
    // An exact column's numbers are counted and their extremes kept a run of rows at a time, and
    // their sum is a 128-bit integer the reader keeps (ValueAccumulator); these take what a run
    // folds in.

    /// <summary>Counts <paramref name="numbers"/> exact numbers into a cell, each a value that is not
    /// Blank.</summary>
    public void CountNumbers(int cell, int numbers)
    {
        ref var counts = ref Counts[cell];
        counts.Values += numbers;
        counts.Numbers += numbers;
    }

    /// <summary>Folds a run's smallest and largest exact numbers into a cell's extremes;
    /// <paramref name="first"/> when the cell held no number before the run.</summary>
    public void FoldExtremes(int cell, decimal min, decimal max, bool first)
    {
        ref var extremes = ref Extremes![cell];
        if (first)
        {
            extremes.ExactMin = min;
            extremes.ExactMax = max;
        }
        else if (extremes.Inexact)
        {
            AddToDoubleExtremes(ref extremes, (double)min);
            AddToDoubleExtremes(ref extremes, (double)max);
        }
        else
        {
            if (min < extremes.ExactMin)
                extremes.ExactMin = min;
            if (max > extremes.ExactMax)
                extremes.ExactMax = max;
        }
    }

    /// <summary>Folds one exact number into the parts kept in <c>double</c> — the product and the
    /// running variance — as the <c>double</c> it converts to; <paramref name="count"/> is the
    /// cell's numbers with this one.</summary>
    public void AddInexactParts(int cell, double value, long count)
    {
        if (Products is not null)
            Products[cell] = count == 1 ? value : Products[cell] * value;
        if (Variances is not null)
            AddToVariance(ref Variances[cell], value, count);
    }

    /// <summary>Whether a cell's sum has left exactness for Excel's <c>double</c>.</summary>
    public bool IsInexactSum(int cell) => Sums![cell].Inexact;

    /// <summary>A cell's sum leaves exactness for Excel's <c>double</c>, starting from
    /// <paramref name="start"/> — the exact sum it had, converted.</summary>
    public void ToInexactSum(int cell, double start)
    {
        ref var sum = ref Sums![cell];
        sum.Inexact = true;
        sum.Exact = 0;
        sum.Double = start;
        sum.Compensation = 0;
    }

    /// <summary>Adds to a cell's <c>double</c> sum, compensated.</summary>
    public void AddInexactSum(int cell, double value) => Neumaier(ref Sums![cell], value);

    // ---- One record's value ------------------------------------------------------------------

    // The three typed ways in, which a reader of typed columns calls without boxing a value.

    /// <summary>Folds in a value that is neither Blank nor a number — text, a Boolean, a date:
    /// counted, never a number.</summary>
    public void AddOther(int cell) => Counts[cell].Values++;

    /// <summary>Folds in an exact number: an integral or <c>decimal</c> value.</summary>
    public void AddExact(int cell, decimal value)
    {
        ref var counts = ref Counts[cell];
        counts.Values++;
        // Once a non-finite number is in, every numeric Aggregation is #NUM! and no part is read.
        if (!counts.NonFinite)
        {
            var first = counts.Numbers == 0;
            if (Sums is not null)
                AddToSum(ref Sums[cell], value, first);
            if (Extremes is not null)
                AddToExtremes(ref Extremes[cell], value, first);
            if (Products is not null)
                Products[cell] = first ? (double)value : Products[cell] * (double)value;
            if (Variances is not null)
                AddToVariance(ref Variances[cell], (double)value, counts.Numbers + 1);
        }
        counts.Numbers++;
    }

    /// <summary>Folds in a <c>double</c>: not exact, and <c>#NUM!</c> when it is not finite.</summary>
    public void AddDouble(int cell, double value)
    {
        ref var counts = ref Counts[cell];
        counts.Values++;
        if (!double.IsFinite(value))
        {
            counts.NonFinite = true;
            counts.Numbers++;
            return;
        }
        if (!counts.NonFinite)
        {
            var first = counts.Numbers == 0;
            if (Sums is not null)
                AddToSum(ref Sums[cell], value, first);
            if (Extremes is not null)
                AddToExtremes(ref Extremes[cell], value, first);
            if (Products is not null)
                Products[cell] = first ? value : Products[cell] * value;
            if (Variances is not null)
                AddToVariance(ref Variances[cell], value, counts.Numbers + 1);
        }
        counts.Numbers++;
    }

    private static void AddToSum(ref SumPart sum, decimal value, bool first)
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
            // Money stays exact until it cannot: then double, Excel's own arithmetic (ADR-0059).
            ToDouble(ref sum);
            Neumaier(ref sum, (double)value);
        }
    }

    private static void AddToSum(ref SumPart sum, double value, bool first)
    {
        if (first)
        {
            sum.Inexact = true;
            sum.Double = value;
            return;
        }
        if (!sum.Inexact)
            ToDouble(ref sum);
        Neumaier(ref sum, value);
    }

    private static void ToDouble(ref SumPart sum)
    {
        sum.Inexact = true;
        sum.Double = (double)sum.Exact;
        sum.Compensation = 0;
        sum.Exact = 0;
    }

    // Neumaier's compensated sum, so a double total does not depend on the order its numbers
    // arrived in beyond the last bit.
    private static void Neumaier(ref SumPart sum, double value)
    {
        var total = sum.Double + value;
        sum.Compensation += Math.Abs(sum.Double) >= Math.Abs(value)
            ? (sum.Double - total) + value
            : (value - total) + sum.Double;
        sum.Double = total;
    }

    private static void AddToExtremes(ref ExtremesPart extremes, decimal value, bool first)
    {
        if (extremes.Inexact)
        {
            AddToDoubleExtremes(ref extremes, (double)value);
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

    private static void AddToExtremes(ref ExtremesPart extremes, double value, bool first)
    {
        if (first)
        {
            extremes.Inexact = true;
            extremes.Min = value;
            extremes.Max = value;
            return;
        }
        if (!extremes.Inexact)
            ToDouble(ref extremes);
        AddToDoubleExtremes(ref extremes, value);
    }

    // The conversion is monotonic, so the double extremes of exact numbers are their exact
    // extremes converted.
    private static void ToDouble(ref ExtremesPart extremes)
    {
        extremes.Inexact = true;
        extremes.Min = (double)extremes.ExactMin;
        extremes.Max = (double)extremes.ExactMax;
        extremes.ExactMin = 0;
        extremes.ExactMax = 0;
    }

    private static void AddToDoubleExtremes(ref ExtremesPart extremes, double value)
    {
        if (value < extremes.Min)
            extremes.Min = value;
        if (value > extremes.Max)
            extremes.Max = value;
    }

    // Welford's running mean and sum of squared deviations; count is the numbers with this one.
    private static void AddToVariance(ref VariancePart variance, double value, long count)
    {
        var delta = value - variance.Mean;
        variance.Mean += delta / count;
        variance.M2 += delta * (value - variance.Mean);
    }

    /// <summary>
    /// Ends accumulation over the first <paramref name="count"/> cells: a <c>double</c> sum takes
    /// its compensation in, the figures a part does not use are zeroed, and a NaN is the one NaN —
    /// so a part is one value, whatever path it took, and crosses JSON unchanged.
    /// </summary>
    public void Finish(int count)
    {
        if (Sums is not null)
        {
            for (var i = 0; i < count; i++)
            {
                ref var sum = ref Sums[i];
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
        }
        if (Extremes is not null)
        {
            for (var i = 0; i < count; i++)
            {
                ref var extremes = ref Extremes[i];
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
        }
        if (Products is not null)
        {
            for (var i = 0; i < count; i++)
                Products[i] = Canonical(Products[i]);
        }
        if (Variances is not null)
        {
            for (var i = 0; i < count; i++)
            {
                ref var variance = ref Variances[i];
                variance.Mean = Canonical(variance.Mean);
                variance.M2 = Canonical(variance.M2);
            }
        }
    }

    private static double Canonical(double value) => double.IsNaN(value) ? double.NaN : value;

    // ---- Merging cells -------------------------------------------------------------------------

    /// <summary>Folds <paramref name="other"/>'s cell <paramref name="from"/>, of the same field,
    /// into cell <paramref name="cell"/>. The parts merged are this one's; the other holds at least
    /// them.</summary>
    public void Merge(int cell, PartColumns other, int from)
    {
        ref var counts = ref Counts[cell];
        var theirs = other.Counts[from];
        counts.Values += theirs.Values;
        counts.NonFinite |= theirs.NonFinite;
        if (theirs.Numbers == 0)
            return;
        var first = counts.Numbers == 0;
        if (Sums is not null)
            MergeSum(ref Sums[cell], other.Sums![from], first);
        if (Extremes is not null)
            MergeExtremes(ref Extremes[cell], other.Extremes![from], first);
        if (Products is not null)
            Products[cell] = first ? other.Products![from] : Products[cell] * other.Products![from];
        if (Variances is not null)
            MergeVariance(ref Variances[cell], other.Variances![from], counts.Numbers, theirs.Numbers);
        counts.Numbers += theirs.Numbers;
    }

    private static void MergeSum(ref SumPart sum, in SumPart other, bool first)
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
                ToDouble(ref sum);
                Neumaier(ref sum, (double)other.Exact);
                return;
            }
        }
        if (!sum.Inexact)
            ToDouble(ref sum);
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

    private static void MergeExtremes(ref ExtremesPart extremes, in ExtremesPart other, bool first)
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
            ToDouble(ref extremes);
        AddToDoubleExtremes(ref extremes, other.Inexact ? other.Min : (double)other.ExactMin);
        AddToDoubleExtremes(ref extremes, other.Inexact ? other.Max : (double)other.ExactMax);
    }

    // Chan, Golub and LeVeque's pairwise update of a running variance.
    private static void MergeVariance(ref VariancePart variance, in VariancePart other, long count, long otherCount)
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

    // ---- Reading an Aggregation ----------------------------------------------------------------

    /// <summary>Whether this holds every part <paramref name="aggregation"/> reads.</summary>
    public bool Answers(PivotAggregation aggregation)
    {
        var needed = PivotQuery.PartsOf(aggregation);
        return (Parts & needed) == needed;
    }

    /// <summary>An Aggregation's answer at a cell, by the table in ADR-0059.</summary>
    public AggregateValue Read(int cell, PivotAggregation aggregation)
    {
        var counts = Counts[cell];
        if (counts.Values == 0)
            return AggregateValue.Empty;
        if (aggregation == PivotAggregation.Count)
            return AggregateValue.Of((decimal)counts.Values);
        if (aggregation == PivotAggregation.CountNumbers)
            return AggregateValue.Of((decimal)counts.Numbers);
        if (!Answers(aggregation))
            throw new InvalidOperationException($"{aggregation} reads a part this field was not aggregated with ({Parts}); ask again with its parts (ADR-0065).");

        var noNumber = counts.Numbers == 0;
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
        if (counts.NonFinite)
            return AggregateValue.NumberError;

        var n = counts.Numbers;
        switch (aggregation)
        {
            case PivotAggregation.Sum:
            {
                var sum = Sums![cell];
                return sum.Inexact ? Checked(sum.Double + sum.Compensation) : AggregateValue.Of(sum.Exact);
            }
            case PivotAggregation.Average:
            {
                var sum = Sums![cell];
                return sum.Inexact ? Checked((sum.Double + sum.Compensation) / n) : AggregateValue.Of(sum.Exact / n);
            }
            case PivotAggregation.Max:
            {
                var extremes = Extremes![cell];
                return extremes.Inexact ? Checked(extremes.Max) : AggregateValue.Of(extremes.ExactMax);
            }
            case PivotAggregation.Min:
            {
                var extremes = Extremes![cell];
                return extremes.Inexact ? Checked(extremes.Min) : AggregateValue.Of(extremes.ExactMin);
            }
            case PivotAggregation.Product:
                return Checked(Products![cell]);
            case PivotAggregation.StdDev:
                return n < 2 ? AggregateValue.DivideByZero : Checked(Math.Sqrt(Math.Max(0, Variances![cell].M2) / (n - 1)));
            case PivotAggregation.StdDevp:
                return Checked(Math.Sqrt(Math.Max(0, Variances![cell].M2) / n));
            case PivotAggregation.Var:
                return n < 2 ? AggregateValue.DivideByZero : Checked(Math.Max(0, Variances![cell].M2) / (n - 1));
            default:
                return Checked(Math.Max(0, Variances![cell].M2) / n);
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
