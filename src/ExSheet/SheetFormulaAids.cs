using System.Text.RegularExpressions;
using ExGrid.Cells;
using ExGrid.Selection;
using ExSheet.Engine;
using GridCandidate = ExGrid.Cells.CompletionCandidate;

namespace ExSheet;

/// <summary>
/// The answers ExSheet gives ExGrid's formula entry aids (ADR-0051): the grid reports the
/// editor's text and caret and does not know what a Formula is; the engine's
/// <see cref="FormulaEntry"/> does. This is only the translation between the two — the engine's
/// span and replacement onto the grid's candidates, its argument onto the grid's hint, its
/// insertion site onto the grid's yes or no, a pointed range onto its Reference text, F4's cycle
/// onto the grid's rewrite, and the References in the text onto the grid's Reference Outlines.
/// </summary>
internal static partial class SheetFormulaAids
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

    /// <summary>Point mode's predicate (ADR-0051): whether a Reference can be written at the caret.</summary>
    internal static bool PointAt(string text, int caret) =>
        caret >= 0 && caret <= text.Length && FormulaEntry.PointAt(text, caret) is not null;

    /// <summary>The Reference Point mode writes for a pointed range: <c>A3</c>, or <c>B7:C9</c> from its top-left.</summary>
    internal static string ReferenceText(SelectionRange range) =>
        FormulaEntry.ReferenceText(RangeOf(range));

    /// <summary>
    /// F4 (ADR-0051, 2026-09-29): the Reference at the caret, or every Reference the selection
    /// covers, overlaps or touches, cycled to its next form by the engine (<see cref="FormulaEntry.CycleReference"/>),
    /// with the caret at the end of what was rewritten or the selection over it. Null where
    /// nothing changes: no Reference there, or text that is not a Formula.
    /// </summary>
    internal static EditorRewrite? CycleReference(string text, int selectionStart, int selectionEnd) =>
        FormulaEntry.CycleReference(text, selectionStart, selectionEnd) is { } cycle
            ? new EditorRewrite(cycle.Text, cycle.SelectionStart, cycle.SelectionEnd)
            : null;

    /// <summary>The Sheet's name for a range of the grid's positions: rows and columns are places (ADR-0046).</summary>
    internal static CellRange RangeOf(SelectionRange range) =>
        new(new CellAddress(range.TopRow, range.LeftColumn), new CellAddress(range.BottomRow, range.RightColumn));

    /// <summary>The grid's positions for a range of the Sheet: the other way round from <see cref="RangeOf"/>.</summary>
    internal static SelectionRange SelectionOf(CellRange range) =>
        new(range.First.Row, range.First.Column, range.RowCount, range.ColumnCount);

    /// <summary>
    /// The References function (ADR-0057): each Reference in the text being edited, with its span
    /// and the cells it names on <paramref name="sheet"/>, for the grid to colour and outline.
    /// Nothing for text that is not a Formula.
    /// </summary>
    internal static IReadOnlyList<EditorReference> References(Sheet sheet, string text)
    {
        if (text.Length == 0 || text[0] != '=') return [];
        return PlainReferences(text);
    }

    // PLACEHOLDER until ticket 27 lands. Ticket 27 writes the engine's own answer — the tolerant
    // scan F4 reads, a Sheet qualifier with this Sheet's name (hence the Sheet above), structured
    // references as keys, no function name — and References above takes it over, translating its
    // cells onto the grid's positions with SelectionOf. Until then this reads only plain Sheet
    // References (A1, $A$1, B2:C3, B2:A1) outside text in quotes: enough for =A1+B2:C3.
    private static List<EditorReference> PlainReferences(string text)
    {
        var references = new List<EditorReference>();
        var quoted = false;
        for (var at = 1; at < text.Length; at++)
        {
            if (text[at] == '"') quoted = !quoted;
            if (quoted || !char.IsAsciiLetter(text[at]) && text[at] != '$') continue;
            if (char.IsAsciiLetterOrDigit(text[at - 1]) || text[at - 1] is '_' or '.' or '$' or '!' or ']' or '"') continue;
            var match = PlainReference().Match(text, at);
            if (!match.Success || !CellRange.TryParse(match.Value.Replace("$", "", StringComparison.Ordinal), out var range)) continue;
            references.Add(new EditorReference(at, match.Length, SelectionOf(range)));
            at += match.Length - 1;
        }
        return references;
    }

    [GeneratedRegex(@"\G\$?[A-Za-z]{1,3}\$?[0-9]{1,7}(?::\$?[A-Za-z]{1,3}\$?[0-9]{1,7})?(?![A-Za-z0-9_.(\[!:$])", RegexOptions.CultureInvariant)]
    private static partial Regex PlainReference();
}
