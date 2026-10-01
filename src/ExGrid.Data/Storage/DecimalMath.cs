namespace ExGrid.Data.Storage;

/// <summary>
/// Exact decimal arithmetic for the Decimal kind (ADR-0063): a value's parts, its decimal places once
/// trailing zeros are gone, and the canonical <see cref="decimal"/> a value reads back as, so that
/// <c>1.5</c> and <c>1.50</c> read back alike.
/// </summary>
internal static class DecimalMath
{
    public const int MaxLongScale = 18;

    /// <summary>10^k for k = 0..18.</summary>
    public static readonly long[] Pow10 = CreatePow10();

    /// <summary>The largest magnitude that can be multiplied by 10^k without leaving a long.</summary>
    public static readonly long[] MaxBeforeScaling = CreateMaxBeforeScaling();

    /// <summary>
    /// Splits a value into its magnitude and scale. <paramref name="bits"/> is four ints of scratch
    /// space the caller owns, so a loop over a million values allocates nothing.
    /// </summary>
    public static void Split(decimal value, Span<int> bits, out ulong low64, out uint high32, out int scale, out bool negative)
    {
        decimal.GetBits(value, bits);
        low64 = (uint)bits[0] | ((ulong)(uint)bits[1] << 32);
        high32 = (uint)bits[2];
        scale = (bits[3] >> 16) & 0xFF;
        negative = bits[3] < 0;
    }

    /// <summary>The value with its trailing zeros gone; zero is <c>0</c>, unsigned, with no places.</summary>
    public static decimal Canonical(decimal value)
    {
        Span<int> bits = stackalloc int[4];
        Split(value, bits, out var low64, out var high32, out var scale, out var negative);
        var magnitude = ((UInt128)high32 << 64) | low64;
        if (magnitude == 0)
            return 0m;
        if (scale == 0)
            return value;
        while (scale > 0 && magnitude % 10 == 0)
        {
            magnitude /= 10;
            scale--;
        }
        return new decimal((int)(uint)magnitude, (int)(uint)(magnitude >> 32), (int)(uint)(magnitude >> 64), negative, (byte)scale);
    }

    /// <summary>The canonical decimal that <paramref name="scaled"/> × 10^-<paramref name="scale"/> is.</summary>
    public static decimal FromScaled(long scaled, int scale)
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

    private static long[] CreatePow10()
    {
        var pow = new long[MaxLongScale + 1];
        pow[0] = 1;
        for (var k = 1; k < pow.Length; k++)
            pow[k] = pow[k - 1] * 10;
        return pow;
    }

    private static long[] CreateMaxBeforeScaling()
    {
        var max = new long[MaxLongScale + 1];
        for (var k = 0; k < max.Length; k++)
            max[k] = long.MaxValue / Pow10[k];
        return max;
    }
}
