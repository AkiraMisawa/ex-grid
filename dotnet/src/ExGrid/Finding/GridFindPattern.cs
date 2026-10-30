namespace ExGrid.Finding;

/// <summary>
/// What the find panel holds (ADR-0055, ADR-0332 §4): the text to look for and the two options,
/// without anything that belongs to one step — where it starts, which way it runs, what it is
/// read in. A Match Mark judges each painted cell against it, and
/// <see cref="GridFind.Matches(string, GridFindPattern)"/> is the reference rule it is judged by.
/// </summary>
/// <param name="Text">What to look for. The grid never judges a cell against an empty one: an
/// empty field marks nothing.</param>
/// <param name="MatchCase">Off: compared <c>OrdinalIgnoreCase</c>, ADR-0023's rule. On:
/// <c>Ordinal</c>.</param>
/// <param name="WholeCell">Off: the displayed text contains <paramref name="Text"/>. On: it
/// equals it.</param>
public sealed record GridFindPattern(string Text, bool MatchCase, bool WholeCell);
