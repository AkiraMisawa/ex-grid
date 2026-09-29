using ExGrid.Selection;

namespace ExGrid.Cells;

/// <summary>The colour a key was given (ADR-0057), as the core tells it to the Consumer: the
/// Consumer outlines what the key names wherever it shows it, in that colour.</summary>
/// <param name="Key">The key, as the References function answered it.</param>
/// <param name="Colour">The colour its References wear.</param>
public sealed record ReferenceKeyColour(string Key, ReferenceColour Colour);

/// <summary>
/// The colours of the References in one text (ADR-0057). References that name the same cells,
/// or carry the same key, share a colour; the colours are handed out in order of first
/// appearance in the text, round a palette of <see cref="ReferenceColour.PaletteLength"/>. Pure:
/// the component asks the Consumer for the References and draws the outcome, and this decides
/// who wears what. An answer whose spans do not lie inside the text, or overlap one another, is
/// refused by name rather than coloured over the wrong characters.
/// </summary>
public sealed class ReferenceColouring
{
    private ReferenceColouring(
        IReadOnlyList<EditorReference> references,
        IReadOnlyList<ReferenceColour> colours,
        IReadOnlyList<(SelectionRange Range, ReferenceColour Colour)> ranges,
        IReadOnlyList<ReferenceKeyColour> keys)
    {
        References = references;
        Colours = colours;
        Ranges = ranges;
        Keys = keys;
    }

    /// <summary>No Reference, and so no colour.</summary>
    public static ReferenceColouring None { get; } = new([], [], [], []);

    /// <summary>The References, in the order they stand in the text.</summary>
    public IReadOnlyList<EditorReference> References { get; }

    /// <summary>The colour of each of <see cref="References"/>, at the same index.</summary>
    public IReadOnlyList<ReferenceColour> Colours { get; }

    /// <summary>Each range the text names, once however often and however it is written, with
    /// its colour, in order of first appearance: one Reference Outline each.</summary>
    public IReadOnlyList<(SelectionRange Range, ReferenceColour Colour)> Ranges { get; }

    /// <summary>Each key the text carries, once, with its colour, in order of first appearance:
    /// what the core tells the Consumer.</summary>
    public IReadOnlyList<ReferenceKeyColour> Keys { get; }

    /// <summary>The colour of <paramref name="range"/>, or null when the text does not name
    /// it.</summary>
    public ReferenceColour? ColourOf(SelectionRange range) => ColourOf(Ranges, range);

    /// <summary>
    /// Colours <paramref name="references"/>, the Consumer's answer for <paramref name="text"/>.
    /// The answer may list them in any order: first appearance is where each stands in the
    /// text. Null or empty colours nothing.
    /// </summary>
    /// <exception cref="ArgumentNullException">The text, or one of the References, is
    /// null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A span does not lie inside the
    /// text.</exception>
    /// <exception cref="ArgumentException">Two spans overlap.</exception>
    public static ReferenceColouring Of(string text, IReadOnlyList<EditorReference>? references)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (references is null || references.Count == 0)
            return None;
        foreach (var reference in references)
        {
            if (reference is null)
                throw new ArgumentNullException(nameof(references), "The References function answered a null Reference (ADR-0057).");
            if (reference.Length > text.Length - reference.Start)
            {
                throw new ArgumentOutOfRangeException(nameof(references),
                    $"The Reference at {reference.Start}+{reference.Length} does not lie inside the editor's text of {text.Length} characters (ADR-0057).");
            }
        }
        // Stable, so an answer already in order keeps it.
        var ordered = references.OrderBy(r => r.Start).ToArray();
        for (var i = 1; i < ordered.Length; i++)
        {
            if (ordered[i].Start < ordered[i - 1].Start + ordered[i - 1].Length)
            {
                throw new ArgumentException(
                    $"The References at {ordered[i - 1].Start}+{ordered[i - 1].Length} and {ordered[i].Start}+{ordered[i].Length} overlap: " +
                    "a character belongs to one Reference at most (ADR-0057).", nameof(references));
            }
        }

        var colours = new ReferenceColour[ordered.Length];
        var ranges = new List<(SelectionRange Range, ReferenceColour Colour)>();
        var keys = new List<ReferenceKeyColour>();
        for (var i = 0; i < ordered.Length; i++)
        {
            var reference = ordered[i];
            var known = reference.Range is { } range ? ColourOf(ranges, range) : ColourOf(keys, reference.Key!);
            if (known is { } colour)
            {
                colours[i] = colour;
                continue;
            }
            // Something not named before takes the next colour: one on from every range and key
            // named so far.
            colours[i] = ReferenceColour.ForAppearance(ranges.Count + keys.Count);
            if (reference.Range is { } first)
                ranges.Add((first, colours[i]));
            else
                keys.Add(new ReferenceKeyColour(reference.Key!, colours[i]));
        }
        return new ReferenceColouring(ordered, colours, ranges, keys);
    }

    // Indexed rather than enumerated: the component reads a colour every render an outline is
    // painted in, and an interface's enumerator is an allocation each time.
    private static ReferenceColour? ColourOf(IReadOnlyList<(SelectionRange Range, ReferenceColour Colour)> ranges, SelectionRange range)
    {
        for (var i = 0; i < ranges.Count; i++)
        {
            if (ranges[i].Range == range)
                return ranges[i].Colour;
        }
        return null;
    }

    private static ReferenceColour? ColourOf(IReadOnlyList<ReferenceKeyColour> keys, string key)
    {
        for (var i = 0; i < keys.Count; i++)
        {
            if (string.Equals(keys[i].Key, key, StringComparison.Ordinal))
                return keys[i].Colour;
        }
        return null;
    }
}
