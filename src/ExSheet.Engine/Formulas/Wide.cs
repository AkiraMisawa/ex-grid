namespace ExSheet.Engine.Formulas;

/// <summary>
/// A number carried as the unevaluated sum of two doubles, about 106 bits, normalised so that
/// <see cref="High"/> is the double nearest the sum. Every operation is the standard error-free one
/// (Dekker, Knuth; the QD library's accurate forms, each with a relative error of a few times 2^-106), so
/// the result is the same on every platform. <see cref="CorrectlyRounded"/> computes its exponential and
/// logarithm in it before it rounds them.
/// </summary>
internal readonly record struct Wide(double High, double Low)
{
    public static implicit operator Wide(double value) => new(value, 0);

    /// <summary>a + b exactly (Knuth's two-sum).</summary>
    public static Wide Sum(double a, double b)
    {
        var sum = a + b;
        var b2 = sum - a;
        return new(sum, (a - (sum - b2)) + (b - b2));
    }

    /// <summary>a + b exactly, where |a| is at least |b| or a is 0.</summary>
    public static Wide QuickSum(double a, double b)
    {
        var sum = a + b;
        return new(sum, b - (sum - a));
    }

    /// <summary>a * b exactly, the rounding error recovered by a fused multiply-add.</summary>
    public static Wide Product(double a, double b)
    {
        var product = a * b;
        return new(product, Math.FusedMultiplyAdd(a, b, -product));
    }

    public static Wide operator -(Wide a) => new(-a.High, -a.Low);

    public static Wide operator +(Wide a, Wide b)
    {
        var high = Sum(a.High, b.High);
        var low = Sum(a.Low, b.Low);
        var sum = QuickSum(high.High, high.Low + low.High);
        return QuickSum(sum.High, sum.Low + low.Low);
    }

    public static Wide operator -(Wide a, Wide b) => a + -b;

    public static Wide operator *(Wide a, Wide b)
    {
        var product = Product(a.High, b.High);
        return QuickSum(product.High, product.Low + ((a.High * b.Low) + (a.Low * b.High)));
    }

    public static Wide operator /(Wide a, Wide b)
    {
        var first = a.High / b.High;
        var rest = a - (b * first);
        var second = rest.High / b.High;
        rest -= b * second;
        var third = rest.High / b.High;
        return QuickSum(first, second) + third;
    }
}
