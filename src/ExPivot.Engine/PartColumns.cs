using ExGrid.Data;

namespace ExPivot.Engine;

/// <summary>
/// One field in Values, held column by column over a set of cells — a question's leaves, or every
/// cell of a cube (ADR-0066). Only the parts asked for have columns, and only they are
/// accumulated (ADR-0060, "Changed when decided"). Every part combines exactly: counts and exact
/// sums add, extremes compare, products multiply and running variances merge, so a total merged
/// from its leaves is the total of its records (ADR-0060: never a total of totals).
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
        CopyRange(other, 0, count);
    }

    /// <summary>Cells [<paramref name="from"/>, <paramref name="to"/>) of <paramref name="other"/>,
    /// which holds at least these parts, as this one's same cells, which there is room for — a cube
    /// made in slices copies its leaves a piece at a time (PV-40).</summary>
    public void CopyRange(PartColumns other, int from, int to)
    {
        var count = to - from;
        Array.Copy(other.Counts, from, Counts, from, count);
        if (Sums is not null)
            Array.Copy(other.Sums!, from, Sums, from, count);
        if (Extremes is not null)
            Array.Copy(other.Extremes!, from, Extremes, from, count);
        if (Products is not null)
            Array.Copy(other.Products!, from, Products, from, count);
        if (Variances is not null)
            Array.Copy(other.Variances!, from, Variances, from, count);
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

    /// <summary>
    /// Whether cell <paramref name="cell"/> holds what <paramref name="other"/>'s cell
    /// <paramref name="from"/> holds, of the parts this one keeps (ADR-0161): the same counts, an
    /// exact part of the same value whatever its scale, and a <c>double</c> of the same bits.
    /// </summary>
    public bool SameAs(int cell, PartColumns other, int from)
    {
        var (mine, theirs) = (Counts[cell], other.Counts[from]);
        if (mine.Values != theirs.Values || mine.Numbers != theirs.Numbers || mine.NonFinite != theirs.NonFinite)
            return false;
        if (Sums is not null)
        {
            var (a, b) = (Sums[cell], other.Sums![from]);
            if (a.Inexact != b.Inexact || (a.Inexact ? !SameBits(a.Double, b.Double) || !SameBits(a.Compensation, b.Compensation) : a.Exact != b.Exact))
                return false;
        }
        if (Extremes is not null)
        {
            var (a, b) = (Extremes[cell], other.Extremes![from]);
            if (a.Inexact != b.Inexact
                || (a.Inexact ? !SameBits(a.Min, b.Min) || !SameBits(a.Max, b.Max) : a.ExactMin != b.ExactMin || a.ExactMax != b.ExactMax))
                return false;
        }
        if (Products is not null && !SameBits(Products[cell], other.Products![from]))
            return false;
        return Variances is null
            || (SameBits(Variances[cell].Mean, other.Variances![from].Mean) && SameBits(Variances[cell].M2, other.Variances![from].M2));
    }

    private static bool SameBits(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);

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
    /// trailing zeros (ADR-0064: a Decimal is a value, not the scale it was written with). A sum
    /// folded from segments at different scales then reads alike however it was made, and a
    /// batch folded in leaves it as a fresh aggregation would.
    /// </summary>
    public void Canonicalize(int count) => Canonicalize(0, count);

    /// <summary><see cref="Canonicalize(int)"/> over cells [<paramref name="from"/>,
    /// <paramref name="to"/>), a piece at a time (PV-40); each cell is its own.</summary>
    public void Canonicalize(int from, int to)
    {
        if (Sums is not null)
        {
            for (var i = from; i < to; i++)
            {
                ref var sum = ref Sums[i];
                if (!sum.Inexact)
                    sum.Exact = AggregateArithmetic.Canonical(sum.Exact);
            }
        }
        if (Extremes is not null)
        {
            for (var i = from; i < to; i++)
            {
                ref var extremes = ref Extremes[i];
                if (!extremes.Inexact)
                {
                    extremes.ExactMin = AggregateArithmetic.Canonical(extremes.ExactMin);
                    extremes.ExactMax = AggregateArithmetic.Canonical(extremes.ExactMax);
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
        => AggregateArithmetic.Fold(ref Extremes![cell], min, max, first);

    /// <summary>Folds one exact number into the parts kept in <c>double</c> — the product and the
    /// running variance — as the <c>double</c> it converts to; <paramref name="count"/> is the
    /// cell's numbers with this one.</summary>
    public void AddInexactParts(int cell, double value, long count)
    {
        if (Products is not null)
            Products[cell] = count == 1 ? value : Products[cell] * value;
        if (Variances is not null)
            AggregateArithmetic.Add(ref Variances[cell], value, count);
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
    public void AddInexactSum(int cell, double value) => AggregateArithmetic.Neumaier(ref Sums![cell], value);

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
                AggregateArithmetic.Add(ref Sums[cell], value, first);
            if (Extremes is not null)
                AggregateArithmetic.Add(ref Extremes[cell], value, first);
            if (Products is not null)
                Products[cell] = first ? (double)value : Products[cell] * (double)value;
            if (Variances is not null)
                AggregateArithmetic.Add(ref Variances[cell], (double)value, counts.Numbers + 1);
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
                AggregateArithmetic.Add(ref Sums[cell], value, first);
            if (Extremes is not null)
                AggregateArithmetic.Add(ref Extremes[cell], value, first);
            if (Products is not null)
                Products[cell] = first ? value : Products[cell] * value;
            if (Variances is not null)
                AggregateArithmetic.Add(ref Variances[cell], value, counts.Numbers + 1);
        }
        counts.Numbers++;
    }

    /// <summary>
    /// Ends accumulation over the first <paramref name="count"/> cells: a <c>double</c> sum takes
    /// its compensation in, the figures a part does not use are zeroed, and a NaN is the one NaN —
    /// so a part is one value, whatever path it took, and crosses JSON unchanged.
    /// </summary>
    public void Finish(int count) => Finish(0, count);

    /// <summary><see cref="Finish(int)"/> over cells [<paramref name="from"/>, <paramref name="to"/>),
    /// a piece at a time (PV-40); each cell is its own.</summary>
    public void Finish(int from, int to)
    {
        if (Sums is not null)
        {
            for (var i = from; i < to; i++)
                AggregateArithmetic.Finish(ref Sums[i]);
        }
        if (Extremes is not null)
        {
            for (var i = from; i < to; i++)
                AggregateArithmetic.Finish(ref Extremes[i]);
        }
        if (Products is not null)
        {
            for (var i = from; i < to; i++)
                Products[i] = AggregateArithmetic.Canonical(Products[i]);
        }
        if (Variances is not null)
        {
            for (var i = from; i < to; i++)
                AggregateArithmetic.Finish(ref Variances[i]);
        }
    }

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
            AggregateArithmetic.Merge(ref Sums[cell], other.Sums![from], first);
        if (Extremes is not null)
            AggregateArithmetic.Merge(ref Extremes[cell], other.Extremes![from], first);
        if (Products is not null)
            Products[cell] = first ? other.Products![from] : Products[cell] * other.Products![from];
        if (Variances is not null)
            AggregateArithmetic.Merge(ref Variances[cell], other.Variances![from], counts.Numbers, theirs.Numbers);
        counts.Numbers += theirs.Numbers;
    }

    // ---- Reading an Aggregation ----------------------------------------------------------------

    /// <summary>Whether this holds every part <paramref name="aggregation"/> reads.</summary>
    public bool Answers(PivotAggregation aggregation)
    {
        var needed = PivotQuery.PartsOf(aggregation);
        return (Parts & needed) == needed;
    }

    /// <summary>An Aggregation's answer at a cell, by the table in ADR-0060.</summary>
    public AggregateValue Read(int cell, PivotAggregation aggregation)
    {
        var counts = Counts[cell];
        var reads = aggregation is not (PivotAggregation.Count or PivotAggregation.CountNumbers);
        if (counts.Values != 0 && reads && !Answers(aggregation))
            throw new InvalidOperationException($"{aggregation} reads a part this field was not aggregated with ({Parts}); ask again with its parts (ADR-0066).");
        return AggregateValue.From(AggregateArithmetic.Read(
            aggregation.ToAggregation(),
            counts,
            Sums is null ? null : Sums[cell],
            Extremes is null ? null : Extremes[cell],
            Products is null ? null : Products[cell],
            Variances is null ? null : Variances[cell]));
    }
}

/// <summary>An Aggregation's answer before it is shown: nothing, a number (exact in
/// <c>decimal</c> where it could be) or an error value (ADR-0060).</summary>
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

    /// <summary>The shared definition's answer (ADR-0130), in the words a report shows.</summary>
    public static AggregateValue From(AggregateResult result) => result switch
    {
        { IsEmpty: true } => Empty,
        { Error: AggregateError.DivideByZero } => DivideByZero,
        { Error: not AggregateError.None } => NumberError,
        { Exact: { } exact } => Of(exact),
        _ => Of(result.Number),
    };

    public bool IsEmpty { get; }

    /// <summary>The number, as a <c>double</c> — the exact one rounded where there is one.</summary>
    public double Number { get; }

    /// <summary>The exact <c>decimal</c>, where the Aggregation was computed in it.</summary>
    public decimal? Exact { get; }

    /// <summary>The error value's text, or null.</summary>
    public string? Error { get; }

    public bool IsError => Error is not null;
}
