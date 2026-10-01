using System.Globalization;
using System.Runtime.CompilerServices;

namespace ExGrid.Data.Csv;

/// <summary>
/// How one column reads a number: its decimal point, its thousands separator and the sizes of the
/// groups it separates, and the culture's signs — each as the bytes it is written as in the file's
/// encoding.
/// </summary>
internal sealed class NumberReading
{
    public NumberReading(CsvColumn column, CsvEncoding encoding)
    {
        var format = (column.Culture ?? CultureInfo.InvariantCulture).NumberFormat;
        PointText = column.DecimalPoint ?? (column.Culture is null ? "." : format.NumberDecimalSeparator);
        ThousandsText = column.ThousandsSeparator ?? (column.Culture is null ? "" : format.NumberGroupSeparator);
        // A point the encoding cannot write cannot stand in a field: no value has one.
        Point = CsvText.Encode(encoding, PointText) ?? [];
        Thousands = ThousandsText.Length == 0 ? null : CsvText.Encode(encoding, ThousandsText);
        GroupSizes = column.Culture is null ? [3] : format.NumberGroupSizes;
        if (column.Culture is not null)
        {
            if (format.NegativeSign != "-")
                Minus = CsvText.Encode(encoding, format.NegativeSign);
            if (format.PositiveSign != "+")
                Plus = CsvText.Encode(encoding, format.PositiveSign);
        }
        Plain = Point is [(byte)'.'] && Thousands is null && Minus is null && Plus is null;
    }

    /// <summary>The decimal point, as declared or given by the culture.</summary>
    public string PointText { get; }

    /// <summary>The thousands separator, as declared or given by the culture; empty for none.</summary>
    public string ThousandsText { get; }

    public byte[] Point { get; }

    public byte[]? Thousands { get; }

    public int[] GroupSizes { get; }

    /// <summary>The culture's negative sign, when it is not a hyphen-minus, which is always read.</summary>
    public byte[]? Minus { get; }

    /// <summary>The culture's positive sign, when it is not a plus, which is always read.</summary>
    public byte[]? Plus { get; }

    /// <summary>Whether a number is written as .NET's invariant culture writes one, with no thousands
    /// separator, so that a Double is parsed from the field's own bytes.</summary>
    public bool Plain { get; }
}

/// <summary>What reading a field as a number came to.</summary>
internal enum NumberStatus
{
    Ok,

    /// <summary>The field is not a number as the column reads one.</summary>
    NotANumber,

    /// <summary>The field is a number with more digits than any kind holds exactly.</summary>
    TooLong,
}

/// <summary>
/// A number read from a field without a string: its digits as an integer magnitude, the decimal
/// places among them, and its sign. Trailing zeros after the decimal point are not places, so
/// <c>1.50</c> is 15 at one place.
/// </summary>
internal ref struct ParsedNumber
{
    public ulong Small;
    public UInt128 Large;
    public bool IsLarge;
    public int Scale;
    public bool Negative;
    public bool HasPoint;

    /// <summary>The magnitude as a long, when it fits one.</summary>
    public readonly bool TryLong(out long value)
    {
        if (!IsLarge && Small <= long.MaxValue)
        {
            value = Negative ? -(long)Small : (long)Small;
            return true;
        }
        if (!IsLarge && Negative && Small == 1UL << 63)
        {
            value = long.MinValue;
            return true;
        }
        value = 0;
        return false;
    }

    /// <summary>The number as a <see cref="decimal"/>, exactly; false when it has more than 28 places or
    /// more digits than a decimal's 96 bits hold.</summary>
    public readonly bool TryDecimal(out decimal value)
    {
        var magnitude = IsLarge ? Large : Small;
        if (Scale > 28 || magnitude > (UInt128.One << 96) - 1)
        {
            value = 0;
            return false;
        }
        value = new decimal((int)(uint)magnitude, (int)(uint)(magnitude >> 32), (int)(uint)(magnitude >> 64), Negative, (byte)Scale);
        return true;
    }
}

/// <summary>
/// Reads numbers from bytes by hand: a sign, digits — with the thousands separator only between
/// groups of the declared sizes — and the decimal point followed by digits. Nothing else is a number:
/// no currency, no percent, no exponent but a Double's.
/// </summary>
internal static class NumberText
{
    /// <summary>How a Double's text is parsed once its separators are the invariant culture's: a sign,
    /// a decimal point and an exponent, and no white space — the spaces around a value are set aside
    /// before, and a tab or a line break in it is no number.</summary>
    public const NumberStyles DoubleStyle = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent;

    private const int MaxGroups = 32;

    /// <summary>Reads <paramref name="s"/>, already trimmed, as a fixed-point number.</summary>
    public static NumberStatus Parse(ReadOnlySpan<byte> s, NumberReading reading, out ParsedNumber number)
    {
        number = default;
        var i = Sign(s, reading, ref number);

        // The integer part, with its groups.
        Span<int> groups = stackalloc int[MaxGroups];
        var groupCount = 0;
        var inGroup = 0;
        var integerDigits = 0;
        var thousands = reading.Thousands;
        while (i < s.Length)
        {
            var digit = (uint)(s[i] - (byte)'0');
            if (digit <= 9)
            {
                if (!Push(ref number, digit))
                    return NumberStatus.TooLong;
                inGroup++;
                integerDigits++;
                i++;
                continue;
            }
            if (thousands is not null && s[i] == thousands[0] && s[i..].StartsWith(thousands))
            {
                if (inGroup == 0 || groupCount == MaxGroups - 1)
                    return NumberStatus.NotANumber;
                groups[groupCount++] = inGroup;
                inGroup = 0;
                i += thousands.Length;
                continue;
            }
            break;
        }
        if (groupCount > 0)
        {
            if (inGroup == 0)
                return NumberStatus.NotANumber;
            groups[groupCount++] = inGroup;
            if (!Grouped(groups[..groupCount], reading.GroupSizes))
                return NumberStatus.NotANumber;
        }

        // The decimal places; zeros are held back until a digit follows, so trailing ones are not places.
        var point = reading.Point;
        var places = 0;
        if (point.Length > 0 && i < s.Length && s[i] == point[0] && s[i..].StartsWith(point))
        {
            number.HasPoint = true;
            i += point.Length;
            var zeros = 0;
            while (i < s.Length)
            {
                var digit = (uint)(s[i] - (byte)'0');
                if (digit > 9)
                    break;
                if (digit == 0)
                {
                    zeros++;
                }
                else
                {
                    for (; zeros > 0; zeros--)
                    {
                        if (!Push(ref number, 0))
                            return NumberStatus.TooLong;
                        places++;
                    }
                    if (!Push(ref number, digit))
                        return NumberStatus.TooLong;
                    places++;
                }
                i++;
            }
            if (zeros > 0 && integerDigits + places == 0)
                integerDigits = 1; // ".00" is zero, written with digits
        }
        if (i != s.Length || integerDigits + places == 0)
            return NumberStatus.NotANumber;
        number.Scale = places;
        return NumberStatus.Ok;
    }

    /// <summary>
    /// Writes <paramref name="s"/>, already trimmed, as .NET's invariant culture writes a floating-point
    /// number — a hyphen-minus, digits without separators, a full stop, and an exponent — into
    /// <paramref name="ascii"/>; false when it is not a number as the column reads one.
    /// </summary>
    public static bool Normalize(ReadOnlySpan<byte> s, NumberReading reading, Span<byte> ascii, out int written)
    {
        written = 0;
        ParsedNumber sign = default;
        var i = Sign(s, reading, ref sign);
        if (sign.Negative)
            ascii[written++] = (byte)'-';
        Span<int> groups = stackalloc int[MaxGroups];
        var groupCount = 0;
        var inGroup = 0;
        var digits = 0;
        var thousands = reading.Thousands;
        while (i < s.Length)
        {
            var b = s[i];
            if ((uint)(b - (byte)'0') <= 9)
            {
                ascii[written++] = b;
                inGroup++;
                digits++;
                i++;
                continue;
            }
            if (thousands is not null && b == thousands[0] && s[i..].StartsWith(thousands))
            {
                if (inGroup == 0 || groupCount == MaxGroups - 1)
                    return false;
                groups[groupCount++] = inGroup;
                inGroup = 0;
                i += thousands.Length;
                continue;
            }
            break;
        }
        if (groupCount > 0)
        {
            if (inGroup == 0)
                return false;
            groups[groupCount++] = inGroup;
            if (!Grouped(groups[..groupCount], reading.GroupSizes))
                return false;
        }
        var point = reading.Point;
        if (point.Length > 0 && i < s.Length && s[i] == point[0] && s[i..].StartsWith(point))
        {
            ascii[written++] = (byte)'.';
            i += point.Length;
            while (i < s.Length && (uint)(s[i] - (byte)'0') <= 9)
            {
                ascii[written++] = s[i++];
                digits++;
            }
        }
        if (digits == 0)
            return false;
        if (i < s.Length && (s[i] | 0x20) == (byte)'e')
        {
            ascii[written++] = (byte)'e';
            i++;
            if (i < s.Length && (s[i] == (byte)'+' || s[i] == (byte)'-'))
                ascii[written++] = s[i++];
            var exponent = 0;
            while (i < s.Length && (uint)(s[i] - (byte)'0') <= 9)
            {
                ascii[written++] = s[i++];
                exponent++;
            }
            if (exponent == 0)
                return false;
        }
        return i == s.Length;
    }

    /// <summary>Whether the groups, left to right, have the sizes a culture writes: the rightmost of
    /// the first size, each further left of the next, the last size repeating, and the leftmost of at
    /// most its size.</summary>
    internal static bool Grouped(ReadOnlySpan<int> groups, int[] sizes)
    {
        if (sizes.Length == 0)
            return false;
        var next = 0;
        for (var g = groups.Length - 1; g >= 0; g--)
        {
            var size = sizes[Math.Min(next, sizes.Length - 1)];
            if (size <= 0)
                return false;
            if (g == 0)
                return groups[0] >= 1 && groups[0] <= size;
            if (groups[g] != size)
                return false;
            next++;
        }
        return true;
    }

    /// <summary>Reads a sign — a hyphen-minus or a plus, or the culture's own — and returns where the
    /// digits begin.</summary>
    private static int Sign(ReadOnlySpan<byte> s, NumberReading reading, ref ParsedNumber number)
    {
        if (s.IsEmpty)
            return 0;
        if (s[0] == (byte)'-')
        {
            number.Negative = true;
            return 1;
        }
        if (s[0] == (byte)'+')
            return 1;
        if (reading.Minus is { Length: > 0 } minus && s.StartsWith(minus))
        {
            number.Negative = true;
            return minus.Length;
        }
        if (reading.Plus is { Length: > 0 } plus && s.StartsWith(plus))
            return plus.Length;
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Push(ref ParsedNumber number, uint digit)
    {
        if (!number.IsLarge)
        {
            if (number.Small <= (ulong.MaxValue - 9) / 10)
            {
                number.Small = (number.Small * 10) + digit;
                return true;
            }
            number.IsLarge = true;
            number.Large = number.Small;
        }
        if (number.Large > (UInt128.MaxValue - 9) / 10)
            return false;
        number.Large = (number.Large * 10) + digit;
        return true;
    }
}
