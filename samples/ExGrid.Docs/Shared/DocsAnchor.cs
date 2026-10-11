namespace ExGrid.Docs;

/// <summary>
/// The anchor a heading is reached by: its text in lower case, with every character that is not a
/// letter or a digit made a hyphen. "Status-bar figures" is <c>status-bar-figures</c>, and the
/// version <c>0.1.0-beta.2</c> is <c>0-1-0-beta-2</c> (ADR-0190).
/// </summary>
internal static class DocsAnchor
{
    /// <summary>The anchor for <paramref name="heading"/>.</summary>
    public static string Of(string heading) =>
        new(heading.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
}
