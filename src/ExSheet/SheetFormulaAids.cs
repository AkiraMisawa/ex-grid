using ExGrid.Cells;
using ExSheet.Engine;
using GridCandidate = ExGrid.Cells.CompletionCandidate;

namespace ExSheet;

/// <summary>
/// The answers ExSheet gives ExGrid's formula entry aids (ADR-0051): the grid reports the
/// editor's text and caret and does not know what a Formula is; the engine's
/// <see cref="FormulaEntry"/> does. This is only the translation between the two — the engine's
/// span and replacement onto the grid's candidates, and its argument onto the grid's hint.
/// </summary>
internal static class SheetFormulaAids
{
    /// <summary>
    /// Completion and the argument hint for the editor's text at the caret: the declared
    /// functions and the Sheet's Linked Tables beginning with the name being typed, each
    /// replacing the engine's span with what the engine writes (<c>SUM(</c>, or a table's name),
    /// and the signature of the function whose argument list holds the caret, the argument the
    /// caret is in set off. Null for text that is not a Formula, and when there is neither.
    /// </summary>
    internal static EditorCompletion? Complete(Sheet sheet, string text, int caret)
    {
        if (text.Length == 0 || text[0] != '=' || caret < 0 || caret > text.Length) return null;
        var completion = sheet.Complete(text, caret);
        var hint = HintOf(FormulaEntry.HintAt(text, caret));
        if (completion is null && hint is null) return null;
        IReadOnlyList<GridCandidate> candidates = completion is null
            ? []
            : [.. completion.Candidates.Select(c => new GridCandidate(c.Name, completion.Start, completion.Length, c.InsertText))];
        return new EditorCompletion(candidates, hint);
    }

    /// <summary>
    /// The hint line for an argument hint: the function's signature, <c>SUM(number1,
    /// [number2], ...)</c>, with the name of the argument the caret is in set off — none past
    /// the last argument the function takes.
    /// </summary>
    internal static EditorHint? HintOf(ArgumentHint? hint)
    {
        if (hint is null) return null;
        var function = hint.Function;
        var signature = function.Signature;
        if (hint.CurrentArgument is not { } current) return new EditorHint(signature);
        // The signature is the name, "(", the arguments joined by ", ", and ")": the argument's
        // place in it is counted over the names before it, never searched for as text, so a
        // name that is a prefix of another is set off where it stands.
        var names = function.Arguments.Split(", ");
        var at = function.Name.Length + 1;
        foreach (var name in names)
        {
            if (name == current) return new EditorHint(signature, at, current.Length);
            at += name.Length + 2;
        }
        return new EditorHint(signature);
    }
}
