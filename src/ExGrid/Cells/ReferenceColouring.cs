using ExGrid.Selection;

namespace ExGrid.Cells;

/// <summary>The colour a key was given (ADR-0057), as the core tells it to the Consumer: the
/// Consumer outlines what the key names wherever it shows it, in that colour.</summary>
/// <param name="Key">The key, as the References function answered it.</param>
/// <param name="Colour">The colour its References wear.</param>
public sealed record ReferenceKeyColour(string Key, ReferenceColour Colour);