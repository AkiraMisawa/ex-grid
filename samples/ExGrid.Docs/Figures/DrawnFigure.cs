namespace ExGrid.Docs.Figures;

/// <summary>
/// A figure tools/readme-media/figures.mjs drew from a page of this site (ADR-0110): its size in CSS
/// pixels (its PNG has twice as many), what it shows, and its numbered parts in order. Written into
/// DrawnFigures.g.cs by the same script, never by hand.
/// </summary>
internal sealed record DrawnFigure(string Name, int Width, int Height, string Alt, IReadOnlyList<DrawnFigurePart> Parts);

/// <summary>One numbered part of a figure: its term in CONTEXT.md, and a note on what it is.</summary>
internal sealed record DrawnFigurePart(string Term, string Note);
