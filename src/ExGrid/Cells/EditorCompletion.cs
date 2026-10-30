namespace ExGrid.Cells;

/// <summary>
/// One candidate the Consumer offers for the text being typed (ADR-0051): what the list shows,
/// and what accepting it does to the editor's text — the span it replaces and what it writes
/// there. The grid does not know what a Formula is, so it neither chooses the span nor spells
/// the replacement: <c>=SU</c> offers <c>SUM</c>, replacing the two letters with <c>SUM(</c>.
/// The caret lands after what was written.
/// </summary>
/// <param name="Label">What the list shows for the candidate.</param>
/// <param name="Start">Where the replaced span starts in the text the answer was for.</param>
/// <param name="Length">How long the replaced span is; zero inserts at <paramref name="Start"/>.</param>
/// <param name="Insert">What accepting the candidate writes in the span's place.</param>
public sealed record CompletionCandidate(string Label, int Start, int Length, string Insert);

/// <summary>
/// The argument hint beneath the candidates (ADR-0051): a line of text, such as
/// <c>SUM(number1, [number2], …)</c>, and the span of it that names the argument the caret is
/// in, for Chrome to set off. An empty span sets off nothing.
/// </summary>
/// <param name="Text">The hint.</param>
/// <param name="EmphasisStart">Where the argument the caret is in starts, in <paramref name="Text"/>.</param>
/// <param name="EmphasisLength">How long that argument's name is; zero for none.</param>
public sealed record EditorHint(string Text, int EmphasisStart = 0, int EmphasisLength = 0)
{
    /// <summary>The hint split around its emphasis, for a Chrome to paint the middle part set
    /// off. A span that does not lie inside the text sets off nothing, rather than a guess.</summary>
    public (string Before, string Emphasis, string After) Parts()
    {
        var text = Text ?? "";
        if (EmphasisLength <= 0 || EmphasisStart < 0 || EmphasisStart > text.Length
            || EmphasisLength > text.Length - EmphasisStart)
        {
            return (text, "", "");
        }
        return (text[..EmphasisStart],
            text.Substring(EmphasisStart, EmphasisLength),
            text[(EmphasisStart + EmphasisLength)..]);
    }
}

/// <summary>
/// The Consumer's answer to the editor's text and caret (ADR-0051): the candidates to list, in
/// the order to list them, and an optional hint. Either may be empty; an answer with neither
/// shows nothing. An answer for text that has since changed is dropped, never shown.
/// </summary>
/// <param name="Candidates">The candidates, first shown first and chosen first.</param>
/// <param name="Hint">The argument hint, or null.</param>
public sealed record EditorCompletion(IReadOnlyList<CompletionCandidate> Candidates, EditorHint? Hint = null)
{
    /// <summary>Nothing to offer: no list and no hint.</summary>
    public static EditorCompletion None { get; } = new([]);

    /// <summary>Whether there is anything to show.</summary>
    public bool IsEmpty => Candidates.Count == 0 && Hint is null;
}