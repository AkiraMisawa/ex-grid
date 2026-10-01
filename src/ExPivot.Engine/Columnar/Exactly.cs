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
}
