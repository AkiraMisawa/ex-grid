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
/// onto the grid's rewrite, the References in the text onto the grid's Reference Outlines, and the
/// keys the grid told colours for back onto the Linked Table columns they name.
/// </summary>
internal static class SheetFormulaAids
{
    /// <summary>
    /// Completion and the argument hint for the editor's text at the caret: the declared
    /// functions and the Sheet's Linked Tables beginning with the name being typed, a table's
    /// columns after <c>Table[</c>, or an argument's values where it takes one of a fixed list
    /// (ADR-0058), each shown as the engine names it and replacing the engine's span with what
    /// the engine writes (<c>SUM(</c>, a table's or a column's name, or <c>0</c> for
    /// <c>0 - Exact match</c>); and the signature of the function whose argument list holds the
    /// caret, the argument the caret is in set off. Null for text that is not a Formula, and
    /// when there is neither.
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
    /// and the cells it names on <paramref name="sheet"/>, for the grid to colour and outline. A
    /// structured reference names no cells here, so it carries the key of its Linked Table's column
    /// instead. Nothing for text that is not a Formula.
    /// </summary>
    internal static IReadOnlyList<EditorReference> References(Sheet sheet, string text)
    {
        var found = sheet.References(text);
        if (found.Count == 0) return [];
        var references = new List<EditorReference>(found.Count);
        foreach (var reference in found)
            references.Add(reference.Cells is { } cells
                ? new EditorReference(reference.Start, reference.Length, SelectionOf(cells))
                : new EditorReference(reference.Start, reference.Length, KeyOf(reference.LinkedColumn!)));
        return references;
    }

    /// <summary>
    /// The key a Linked Table's column is coloured under (ADR-0057). One per column, however the
    /// Formula cased its names, since the Sheet finds tables and columns ignoring case.
    /// </summary>
    internal static string KeyOf(LinkedTableColumn column) =>
        (column.Table + "[" + column.Column + "]").ToUpperInvariant();

    /// <summary>
    /// The Linked Table columns behind the keys the grid told colours for (ADR-0057), in the
    /// grid's order, each under the names the Consumer declared: the key is the Formula's casing,
    /// upper-cased, and the Consumer knows its table by the names it gave. A key no declared column
    /// carries names nothing, and is left out.
    /// </summary>
    internal static LinkedColumnColours LinkedColumnsOf(Sheet sheet, IReadOnlyList<ReferenceKeyColour> keys)
    {
        if (keys.Count == 0) return LinkedColumnColours.None;
        var declared = new Dictionary<string, LinkedTableColumn>(StringComparer.Ordinal);
        foreach (var table in sheet.LinkedTables)
        {
            foreach (var column in table.Columns)
            {
                var named = new LinkedTableColumn(table.Name, column);
                declared[KeyOf(named)] = named;
            }
        }
        var columns = new List<LinkedColumnColour>(keys.Count);
        foreach (var key in keys)
        {
            if (declared.TryGetValue(key.Key, out var column)) columns.Add(new LinkedColumnColour(column, key.Colour));
        }
        return new LinkedColumnColours(columns);
    }
}
