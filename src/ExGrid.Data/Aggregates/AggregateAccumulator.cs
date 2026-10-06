using System.Numerics;

namespace ExGrid.Data;

/// <summary>
/// One set of values summarised into every part the Aggregations read (ADR-0060/0130), through
/// <see cref="AggregateArithmetic"/> — the definition ExPivot's engine reads column by column. What
/// a value is decides where it goes:
/// <list type="bullet">
/// <item><b>null</b> is Blank: in no Aggregation.</item>
/// <item><b>An integral or <c>decimal</c> value</b> is an exact number.</item>
/// <item><b>A <c>double</c>, <c>float</c> or <c>Half</c></b> is a number in Excel's <c>double</c>;
/// a non-finite one makes every numeric Aggregation <c>#NUM!</c>.</item>
/// <item><b>Anything else</b> — text (numeric-looking text included), a Boolean, a date, a
/// <c>char</c>, an enum — is counted and is never a number.</item>
/// <item><b>An error value</b>, given through <see cref="AddError"/>, is counted and makes every
/// other Aggregation that error (ADR-0130).</item>
/// </list>
/// </summary>
public sealed class AggregateAccumulator
{
    private AggregateCounts _counts;
    private AggregateSum _sum;
    private AggregateExtremes _extremes;
    private double _product;
    private AggregateVariance _variance;
    private bool _error;
    private bool _merged;

    /// <summary>How many values were not Blank.</summary>
    public long Values => _counts.Values;

    /// <summary>How many values were numbers.</summary>
    public long Numbers => _counts.Numbers;

    /// <summary>Folds in one value, by what it is (above).</summary>
    public void Add(object? value)
    {
        switch (value)
        {
            case null:
                return;
            case decimal d:
                AddExact(d);
                return;
            case int i:
                AddExact(i);
                return;
            case long l:
                AddExact(l);
                return;
            case short s:
                AddExact(s);
                return;
            case byte b:
                AddExact(b);
                return;
            case sbyte sb:
                AddExact(sb);
                return;
            case ushort us:
                AddExact(us);
                return;
            case uint ui:
                AddExact(ui);
                return;
            case ulong ul:
                AddExact(ul);
                return;
            case double db:
                AddDouble(db);
                return;
            case float f:
                AddDouble(f);
                return;
            case Half h:
                AddDouble((double)h);
                return;
            case Int128 big:
                AddWide((BigInteger)big);
                return;
            case UInt128 ubig:
                AddWide((BigInteger)ubig);
                return;
            case BigInteger bigInteger:
                AddWide(bigInteger);
                return;
            default:
                AddOther();
                return;
        }
    }

    /// <summary>Folds in an exact number.</summary>
    public void AddExact(decimal value)
    {
        _counts.Values++;
        if (!_counts.NonFinite)
        {
            var first = _counts.Numbers == 0;
            AggregateArithmetic.Add(ref _sum, value, first);
            AggregateArithmetic.Add(ref _extremes, value, first);
            _product = first ? (double)value : _product * (double)value;
            AggregateArithmetic.Add(ref _variance, (double)value, _counts.Numbers + 1);
        }
        _counts.Numbers++;
    }

    /// <summary>Folds in a <c>double</c>: not exact, and <c>#NUM!</c> when it is not finite.</summary>
    public void AddDouble(double value)
    {
        _counts.Values++;
        if (!double.IsFinite(value))
        {
            _counts.NonFinite = true;
            _counts.Numbers++;
            return;
        }
        if (!_counts.NonFinite)
        {
            var first = _counts.Numbers == 0;
            AggregateArithmetic.Add(ref _sum, value, first);
            AggregateArithmetic.Add(ref _extremes, value, first);
            _product = first ? value : _product * value;
            AggregateArithmetic.Add(ref _variance, value, _counts.Numbers + 1);
        }
        _counts.Numbers++;
    }

    /// <summary>Folds in a value that is neither Blank nor a number: counted, never a number.</summary>
    public void AddOther() => _counts.Values++;

    /// <summary>Folds in an error value: counted, and every Aggregation but Count is then that
    /// error (ADR-0130).</summary>
    public void AddError()
    {
        _counts.Values++;
        _error = true;
    }

    /// <summary>
    /// Folds in the parts of values summarised elsewhere — a server's <c>COUNT</c>, <c>SUM</c>,
    /// <c>MIN</c> and <c>MAX</c> over a range — by the same arithmetic that merges two leaves of a
    /// pivot (ADR-0066/0130). Only the counts, the sum and the extremes travel this way, so once
    /// parts are merged, an Aggregation that reads the product or the variance is refused by name.
    /// </summary>
    public void Merge(in AggregateCounts counts, in AggregateSum sum, in AggregateExtremes extremes)
    {
        _merged = true;
        _counts.Values += counts.Values;
        _counts.NonFinite |= counts.NonFinite;
        if (counts.Numbers == 0)
            return;
        var first = _counts.Numbers == 0;
        AggregateArithmetic.Merge(ref _sum, sum, first);
        AggregateArithmetic.Merge(ref _extremes, extremes, first);
        _counts.Numbers += counts.Numbers;
    }

    /// <summary>An Aggregation's answer over the values folded in so far.</summary>
    public AggregateResult Read(Aggregation aggregation)
    {
        if (_error && aggregation != Aggregation.Count)
            return AggregateResult.Of(AggregateError.ErrorAmongValues);
        if (_merged && aggregation is Aggregation.Product or Aggregation.StdDev or Aggregation.StdDevp or Aggregation.Var or Aggregation.Varp)
            throw new InvalidOperationException($"{aggregation} reads a part that merged parts do not carry (ADR-0066).");
        var sum = _sum;
        AggregateArithmetic.Finish(ref sum);
        var extremes = _extremes;
        AggregateArithmetic.Finish(ref extremes);
        var result = AggregateArithmetic.Read(aggregation, _counts, sum, extremes, _product, _variance);
        return result.Exact is { } exact ? AggregateResult.Of(AggregateArithmetic.Canonical(exact)) : result;
    }

    // An integer wider than 64 bits: exact while a decimal holds it, Excel's double past that.
    private void AddWide(BigInteger value)
    {
        if (value >= (BigInteger)decimal.MinValue && value <= (BigInteger)decimal.MaxValue)
            AddExact((decimal)value);
        else
            AddDouble((double)value);
    }
}
