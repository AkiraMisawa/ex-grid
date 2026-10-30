namespace ExSheet.Engine.Formulas;

/// <summary>
/// A draw of <c>RAND</c> or <c>RANDBETWEEN</c> (ADR-0180): a function of the Workbook's seed, the name of
/// the Sheet and the cell whose Formula calls the function, which call in that Formula it is, and the
/// count of changes since the Workbook was opened — and of nothing else. No draw advances a stream
/// another draw reads, so the order the engine computes cells in never shows in a Value (principle 6).
/// The inputs are mixed by SplitMix64's finaliser, one after another, into 64 bits spread evenly
/// whatever the inputs; the name is read as Excel compares Sheet names, ignoring case, so it draws as
/// the name the Sheet has, however a Reference spells it.
/// </summary>
internal static class Draws
{
    /// <summary>SplitMix64's increment, the golden ratio's fractional part in 64 bits.</summary>
    private const ulong Golden = 0x9E3779B97F4A7C15;

    /// <summary>The draw's 64 bits.</summary>
    public static ulong Of(ulong seed, string sheet, CellAddress cell, int place, long changes)
    {
        var h = Mix(seed);
        foreach (var c in sheet.ToUpperInvariant()) h = Mix(h ^ c);
        h = Mix(h ^ (ulong)sheet.Length);
        h = Mix(h ^ (uint)cell.Row);
        h = Mix(h ^ (uint)cell.Column);
        h = Mix(h ^ (uint)place);
        return Mix(h ^ (ulong)changes);
    }

    /// <summary>A number in [0,1), evenly spread: the draw's top 53 bits, every double of that spacing equally likely.</summary>
    public static double Unit(ulong draw) => (draw >> 11) * (1.0 / (1UL << 53));

    /// <summary>
    /// A whole number from <paramref name="bottom"/> to <paramref name="top"/>, both included, every one
    /// equally likely: the draw scaled to the count of them, a draw that would favour some numbers
    /// over others mixed again until one does not (Lemire's method, with the rejection made a
    /// function of the draw, so it stays reproducible). Both bounds are at most 2^53 in magnitude.
    /// </summary>
    public static long Between(ulong draw, long bottom, long top)
    {
        var count = (ulong)(top - bottom) + 1;
        var scaled = (UInt128)draw * count;
        if ((ulong)scaled < count)
        {
            var threshold = (0UL - count) % count;
            while ((ulong)scaled < threshold)
            {
                draw = Mix(draw);
                scaled = (UInt128)draw * count;
            }
        }
        return bottom + (long)(ulong)(scaled >> 64);
    }

    /// <summary>SplitMix64: the increment, then its finaliser.</summary>
    private static ulong Mix(ulong z)
    {
        z += Golden;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EB;
        return z ^ (z >> 31);
    }
}
