namespace ExPivot.Engine;

/// <summary>
/// The arithmetic of a Snapshot's exact numbers (ADR-0059/0063): a Decimal segment holds each value
/// as a 64-bit integer at the segment's scale, or as a <see cref="decimal"/>; an Integer column at
/// scale 0. These turn such an integer into the <see cref="decimal"/> it stands for and into the
/// <see cref="double"/> a <c>decimal</c> of that value converts to — the same <c>double</c>, bit for
/// bit, so an Item or a product does not depend on how a segment holds the value.
/// </summary>
internal static class Exactly
{
    // 10^k for k = 0..22: every one exactly a double, so a division by it rounds once.
    private static readonly double[] Pow10Double =
    [
        1e0, 1e1, 1e2, 1e3, 1e4, 1e5, 1e6, 1e7, 1e8, 1e9, 1e10, 1e11,
        1e12, 1e13, 1e14, 1e15, 1e16, 1e17, 1e18, 1e19, 1e20, 1e21, 1e22,
    ];

    private const long ExactDoubleLimit = 1L << 53;

    /// <summary>
    /// <paramref name="scaled"/> × 10^-<paramref name="scale"/> as a <see cref="double"/>, as the
    /// <c>decimal</c> of that value converts. Below 2^53 at a scale of 22 or less both are the one
    /// correctly rounded quotient of two exact doubles; otherwise it goes through the
    /// <c>decimal</c>.
    /// </summary>
    public static double ToDouble(long scaled, int scale)
    {
        if (scale == 0)
            return scaled;
        if (scale <= 22 && scaled > -ExactDoubleLimit && scaled < ExactDoubleLimit)
            return scaled / Pow10Double[scale];
        return (double)ToDecimal(scaled, scale);
    }

    /// <summary><paramref name="scaled"/> × 10^-<paramref name="scale"/>, without trailing zeros.</summary>
    public static decimal ToDecimal(long scaled, int scale)
    {
        if (scaled == 0)
            return 0m;
        var negative = scaled < 0;
        var magnitude = negative ? (ulong)(-(scaled + 1)) + 1 : (ulong)scaled;
        while (scale > 0 && magnitude % 10 == 0)
        {
            magnitude /= 10;
            scale--;
        }
        return new decimal((int)(uint)magnitude, (int)(uint)(magnitude >> 32), 0, negative, (byte)scale);
    }

    /// <summary>The value with its trailing zeros gone, so that one value has one form — <c>1.5</c>,
    /// never <c>1.50</c> — whatever segments its parts came from (ADR-0063). Zero is <c>0</c>, never
    /// <c>0.00</c> nor the negative zero a subtraction of two equal negative numbers leaves.</summary>
    public static decimal Canonical(decimal value)
    {
        Span<int> bits = stackalloc int[4];
        decimal.GetBits(value, bits);
        if ((bits[0] | bits[1] | bits[2]) == 0)
            return 0m;
        var scale = (bits[3] >> 16) & 0xFF;
        if (scale == 0)
            return value;
        var magnitude = ((UInt128)(uint)bits[2] << 64) | ((ulong)(uint)bits[1] << 32) | (uint)bits[0];
        if (magnitude % 10 != 0)
            return value;
        while (scale > 0 && magnitude % 10 == 0)
        {
            magnitude /= 10;
            scale--;
        }
        return new decimal((int)(uint)magnitude, (int)(uint)(magnitude >> 32), (int)(uint)(magnitude >> 64), bits[3] < 0, (byte)scale);
    }

    /// <summary>Whether bit <paramref name="index"/> is set, least significant bit first; false for
    /// an empty set.</summary>
    public static bool IsSet(ReadOnlySpan<ulong> bits, int index)
        => !bits.IsEmpty && (bits[index >> 6] & (1UL << index)) != 0;

    // ---- Exact sums as 128-bit integers ---------------------------------------------------------
    //
    // A leaf's exact sum is an integer at a power of ten: integer arithmetic is exact, so the
    // order its numbers are added and taken away in cannot change it (ADR-0066), where a decimal
    // rounds a step that needs more than 96 bits. A sum past 128 bits — beyond 10^38 at its scale —
    // is Excel's double from then on.

    // 10^k for k = 0..38: every power a 128-bit integer holds; and the largest magnitude each
    // power can multiply without leaving 128 bits.
    private static readonly Int128[] Pow10Wide = MakePow10Wide();
    private static readonly Int128[] MostScalable = [.. Pow10Wide.Select(power => Int128.MaxValue / power)];

    private static Int128[] MakePow10Wide()
    {
        var powers = new Int128[39];
        powers[0] = 1;
        for (var k = 1; k < powers.Length; k++)
            powers[k] = powers[k - 1] * 10;
        return powers;
    }

    /// <summary><paramref name="a"/> × 10^-<paramref name="aScale"/> plus <paramref name="b"/> ×
    /// 10^-<paramref name="bScale"/>, at the larger scale; false when it does not fit 128 bits.</summary>
    public static bool TryAdd(Int128 a, int aScale, Int128 b, int bScale, out Int128 sum, out int scale)
    {
        scale = Math.Max(aScale, bScale);
        sum = 0;
        if (!TryRescale(a, aScale, scale, out var x) || !TryRescale(b, bScale, scale, out var y))
            return false;
        sum = x + y;
        // Two numbers of one sign whose sum has the other have left 128 bits.
        return ((x ^ sum) & (y ^ sum)) >= 0;
    }

    private static bool TryRescale(Int128 value, int from, int to, out Int128 scaled)
    {
        scaled = value;
        if (from == to || value == 0)
            return true;
        var k = to - from;
        if (k >= Pow10Wide.Length || value > MostScalable[k] || value < -MostScalable[k])
            return false;
        scaled = value * Pow10Wide[k];
        return true;
    }

    /// <summary>A <see cref="decimal"/> as an integer and the power of ten it is divided by.</summary>
    public static (Int128 Value, int Scale) Wide(decimal value)
    {
        Span<int> bits = stackalloc int[4];
        decimal.GetBits(value, bits);
        var magnitude = ((UInt128)(uint)bits[2] << 64) | ((ulong)(uint)bits[1] << 32) | (uint)bits[0];
        var wide = (Int128)magnitude;
        return (bits[3] < 0 ? -wide : wide, (bits[3] >> 16) & 0xFF);
    }

    /// <summary>
    /// <paramref name="value"/> × 10^-<paramref name="scale"/> as a <see cref="decimal"/> without
    /// trailing zeros — the one form of the value, however it was summed; false when no decimal
    /// holds it exactly, more than 96 bits being left once the trailing zeros are gone.
    /// </summary>
    public static bool TryToDecimal(Int128 value, int scale, out decimal exact)
    {
        exact = 0m;
        Canonical(ref value, ref scale);
        if (value == 0)
            return true;
        var negative = value < 0;
        var magnitude = negative ? (UInt128)(-(value + 1)) + 1 : (UInt128)value;
        if (scale > 28 || magnitude >> 96 != 0)
            return false;
        exact = new decimal((int)(uint)magnitude, (int)(uint)(magnitude >> 32), (int)(uint)(magnitude >> 64), negative, (byte)scale);
        return true;
    }

    /// <summary>The <see cref="double"/> of an exact sum: the decimal's, where a decimal holds it, as
    /// a sum held in <c>decimal</c> converts; otherwise the quotient of the value's one form, so the
    /// same value gives the same double however it was summed.</summary>
    public static double ToDouble(Int128 value, int scale)
    {
        if (TryToDecimal(value, scale, out var exact))
            return (double)exact;
        Canonical(ref value, ref scale);
        return (double)value / Math.Pow(10, scale);
    }

    // The value's one form: no trailing zeros, and zero at scale 0.
    private static void Canonical(ref Int128 value, ref int scale)
    {
        if (value == 0)
        {
            scale = 0;
            return;
        }
        while (scale > 0 && value % 10 == 0)
        {
            value /= 10;
            scale--;
        }
    }
}
