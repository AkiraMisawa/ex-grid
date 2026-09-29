using ExGrid.Selection;

namespace ExGrid.Cells;

/// <summary>
/// One Reference in the text being edited, as the Consumer's References function answers it
/// (ADR-0057): the span of the text it occupies, and <em>either</em> the cells it names on this
/// grid <em>or</em> a key naming something this grid does not hold, such as a Linked Table's
/// column. The grid does not know what a Reference is: it colours what it is told, outlines the
/// cells, and tells the Consumer the colour each key was given. A span covers at least one
/// character, and a key is not empty; either refused is refused by name.
/// </summary>
public sealed record EditorReference
{
    /// <summary>A Reference that names cells on this grid.</summary>
    /// <param name="start">Where the Reference starts in the text.</param>
    /// <param name="length">How many characters it covers; at least one.</param>
    /// <param name="range">The cells it names, however the text wrote them: <c>A1</c> and
    /// <c>$A$1</c> name one range, and so share a colour and an outline.</param>
    /// <exception cref="ArgumentOutOfRangeException">The span starts before the text or covers
    /// nothing.</exception>
    public EditorReference(int start, int length, SelectionRange range)
    {
        (Start, Length) = Span(start, length);
        Range = range;
    }

    /// <summary>A Reference that names something this grid does not hold.</summary>
    /// <param name="start">Where the Reference starts in the text.</param>
    /// <param name="length">How many characters it covers; at least one.</param>
    /// <param name="key">What it names, in the Consumer's words. Compared as written
    /// (ordinally): the Consumer answers one key for one thing, however the text spells it.</param>
    /// <exception cref="ArgumentOutOfRangeException">The span starts before the text or covers
    /// nothing.</exception>
    /// <exception cref="ArgumentException">The key is empty.</exception>
    public EditorReference(int start, int length, string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        (Start, Length) = Span(start, length);
        Key = key;
    }

    /// <summary>Where the Reference starts in the text.</summary>
    public int Start { get; }

    /// <summary>How many characters it covers; at least one.</summary>
    public int Length { get; }

    /// <summary>The cells it names on this grid, or null for a Reference named by
    /// <see cref="Key"/>.</summary>
    public SelectionRange? Range { get; }

    /// <summary>What it names that this grid does not hold, or null for a Reference to
    /// <see cref="Range"/>.</summary>
    public string? Key { get; }

    private static (int Start, int Length) Span(int start, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfLessThan(length, 1);
        return (start, length);
    }
}
