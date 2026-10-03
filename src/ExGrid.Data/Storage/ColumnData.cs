namespace ExGrid.Data.Storage;

/// <summary>
/// One column's values in one segment. Immutable once made, and shared by every version that holds
/// the segment. Every array is exactly the segment's length; the slot of a Blank holds the kind's
/// zero, and the Blank is in <see cref="Blanks"/>.
/// </summary>
internal abstract class ColumnData(ulong[]? blanks)
{
    /// <summary>One bit per row, set for a Blank; <see langword="null"/> when the segment holds none.</summary>
    public ulong[]? Blanks { get; } = blanks;

    public bool IsBlank(int offset) => Blanks is { } bits && Bits.Get(bits, offset);
}

internal sealed class TextData(int[] codes, ulong[]? blanks) : ColumnData(blanks)
{
    /// <summary>Codes into the lineage's dictionary; -1 is a Blank.</summary>
    public int[] Codes { get; } = codes;
}

internal sealed class DecimalData : ColumnData
{
    private DecimalData(long[]? scaled, int scale, decimal[]? exact, ulong[]? blanks)
        : base(blanks)
    {
        Scaled = scaled;
        Scale = scale;
        Exact = exact;
    }

    /// <summary>value × 10^<see cref="Scale"/> for every row, when <see cref="Scale"/> is 0 or more.</summary>
    public long[]? Scaled { get; }

    /// <summary>The power of ten every value is scaled by, or -1 when the segment holds <see cref="Exact"/>.</summary>
    public int Scale { get; }

    /// <summary>Canonical decimals, when some value did not fit a long at one scale.</summary>
    public decimal[]? Exact { get; }

    public static DecimalData FromScaled(long[] scaled, int scale, ulong[]? blanks) => new(scaled, scale, null, blanks);

    public static DecimalData FromExact(decimal[] exact, ulong[]? blanks) => new(null, -1, exact, blanks);

    public decimal ValueAt(int offset) => Scaled is { } scaled ? DecimalMath.FromScaled(scaled[offset], Scale) : Exact![offset];
}

internal sealed class DoubleData(double[] values, ulong[]? blanks) : ColumnData(blanks)
{
    public double[] Values { get; } = values;
}

internal sealed class IntegerData(long[] values, ulong[]? blanks) : ColumnData(blanks)
{
    public long[] Values { get; } = values;
}

internal sealed class DateData(long[] ticks, ulong[]? blanks, bool hasTime) : ColumnData(blanks)
{
    /// <summary>Clock-value ticks.</summary>
    public long[] Ticks { get; } = ticks;

    /// <summary>Whether any value the segment stores is not a midnight.</summary>
    public bool HasTime { get; } = hasTime;
}

internal sealed class BooleanData(bool[] values, ulong[]? blanks) : ColumnData(blanks)
{
    public bool[] Values { get; } = values;
}
