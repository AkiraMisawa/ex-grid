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

/// <summary>
/// What the editor's text becomes, for the pieces of formula entry the core carries out itself
/// (ADR-0051): accepting a candidate and writing a pointed Reference. Pure: the component holds
/// the text, this answers what an operation does to it. Where the caret stands after the user's
/// own typing is not answered here: the browser reports it, because working it out from the
/// change is ambiguous where letters repeat (ADR-0051's second round).
/// </summary>
public static class EditorTextRules
{
    /// <summary>
    /// <paramref name="text"/> with the span from <paramref name="start"/> of
    /// <paramref name="length"/> characters replaced by <paramref name="insert"/>, and the caret
    /// after what was written. A span outside the text is refused by name rather than clamped
    /// into a plausible edit of the wrong characters.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The span does not lie inside the text.</exception>
    public static (string Text, int Caret) Replace(string text, int start, int length, string insert)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(insert);
        if (start < 0 || length < 0 || start > text.Length || length > text.Length - start)
        {
            throw new ArgumentOutOfRangeException(nameof(start),
                $"The span {start}+{length} does not lie inside the editor's text of {text.Length} characters (ADR-0051).");
        }
        return (string.Concat(text.AsSpan(0, start), insert, text.AsSpan(start + length)), start + insert.Length);
    }

    /// <summary>The text and caret after accepting <paramref name="candidate"/> in
    /// <paramref name="text"/> (ADR-0051).</summary>
    /// <exception cref="ArgumentOutOfRangeException">The candidate's span does not lie inside
    /// the text.</exception>
    public static (string Text, int Caret) Accept(string text, CompletionCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return Replace(text, candidate.Start, candidate.Length, candidate.Insert);
    }
}
