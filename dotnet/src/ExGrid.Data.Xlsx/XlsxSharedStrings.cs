using System.Collections;

namespace ExGrid.Data.Xlsx;

/// <summary>
/// The workbook's shared strings (<c>xl/sharedStrings.xml</c>): the one part held whole, because cells refer to its texts by
/// index (ADR-0330 §9). A text written in runs of their own formatting is held as its text, and marked
/// (<see cref="IsRich"/>); a run's formatting and a phonetic guide are not read.
/// </summary>
public sealed class XlsxSharedStrings : IReadOnlyList<string>
{
    private readonly string[] texts;
    private readonly HashSet<int> rich;

    /// <summary>No shared strings: a workbook with no text, or no shared strings part.</summary>
    public static XlsxSharedStrings Empty { get; } = new([], []);

    internal XlsxSharedStrings(string[] texts, HashSet<int> rich)
    {
        this.texts = texts;
        this.rich = rich;
    }

    /// <summary>The text at <paramref name="index"/>.</summary>
    /// <param name="index">The index a cell's value names, from zero.</param>
    public string this[int index] => texts[index];

    /// <summary>The texts there are.</summary>
    public int Count => texts.Length;

    /// <summary>Whether the text at <paramref name="index"/> came in runs of their own formatting, which are not carried.</summary>
    /// <param name="index">The text's index.</param>
    public bool IsRich(int index) => rich.Contains(index);

    /// <summary>The texts in order.</summary>
    public IEnumerator<string> GetEnumerator() => ((IEnumerable<string>)texts).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => texts.GetEnumerator();
}
