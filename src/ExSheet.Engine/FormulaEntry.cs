using System.Text;
using System.Text.RegularExpressions;

namespace ExSheet.Engine;

/// <summary>What a completion candidate names.</summary>
public enum CompletionKind
{
    /// <summary>A function of the declared set (<see cref="DeclaredFunction"/>).</summary>
    Function,

    /// <summary>A Linked Table declared on the Sheet (ADR-0049).</summary>
    LinkedTable,
}

/// <summary>One name completion offers (ADR-0051).</summary>
/// <param name="Name">The name, as declared.</param>
/// <param name="Kind">What it names.</param>
/// <param name="InsertText">What accepting it writes in place of what was typed: a function's name and its <c>(</c>, a table's name.</param>
/// <param name="Description">One sentence for a function; <see langword="null"/> for a table.</param>
public sealed record CompletionCandidate(string Name, CompletionKind Kind, string InsertText, string? Description);

/// <summary>
/// The candidates for the name being typed at the caret (ADR-0051): accepting one replaces the
/// <see cref="Length"/> characters from <see cref="Start"/> with its <see cref="CompletionCandidate.InsertText"/>.
/// </summary>
/// <param name="Start">Where the name being typed starts in the text.</param>
/// <param name="Length">How long it is, including any part of it after the caret.</param>
/// <param name="Candidates">The names that begin with what was typed, in alphabetical order; never empty.</param>
public sealed record FormulaCompletion(int Start, int Length, IReadOnlyList<CompletionCandidate> Candidates);

/// <summary>The argument hint (ADR-0051): the function whose argument list holds the caret, and which argument it is in.</summary>
/// <param name="Function">The declared function.</param>
/// <param name="ArgumentIndex">Which argument the caret is in, from 0.</param>
/// <param name="CurrentArgument">
/// That argument's name as <see cref="DeclaredFunction.Arguments"/> lists it — for an open-ended
/// function past its named arguments, the one that repeats — or <see langword="null"/> when the
/// caret is past the last argument the function takes.
/// </param>
public sealed record ArgumentHint(DeclaredFunction Function, int ArgumentIndex, string? CurrentArgument);

/// <summary>Where Point mode writes a Reference: the <see cref="Length"/> characters from <see cref="Start"/> are replaced (none, for an insertion at the caret).</summary>
/// <param name="Start">Where the Reference goes in the text.</param>
/// <param name="Length">How many characters it replaces.</param>
public readonly record struct PointSite(int Start, int Length);

/// <summary>What F4 makes of the text being edited (ADR-0051): the new text, and the selection in it — collapsed to a caret, or over what was rewritten.</summary>
/// <param name="Text">The text with the Reference or References cycled.</param>
/// <param name="SelectionStart">Where the selection starts in <paramref name="Text"/>.</param>
/// <param name="SelectionEnd">Where it ends; equal to <paramref name="SelectionStart"/> for a caret.</param>
public sealed record ReferenceCycle(string Text, int SelectionStart, int SelectionEnd);

/// <summary>
/// A Linked Table's column, as a structured reference names it (<c>Positions[PV]</c>): the key a
/// Reference Outline is told by (ADR-0057). Two are equal when they name the same table and the
/// same column without regard to case, as the Sheet finds a table and its column (ADR-0049).
/// </summary>
/// <param name="Table">The table's name, as written.</param>
/// <param name="Column">The column's name, as written, with its <c>'</c> escapes read.</param>
public sealed record LinkedTableColumn(string Table, string Column)
{
    /// <summary>Whether <paramref name="other"/> names the same table and column, without regard to case.</summary>
    public bool Equals(LinkedTableColumn? other) =>
        other is not null
        && string.Equals(Table, other.Table, StringComparison.OrdinalIgnoreCase)
        && string.Equals(Column, other.Column, StringComparison.OrdinalIgnoreCase);

    /// <summary>A hash that ignores case, as <see cref="Equals(LinkedTableColumn)"/> does.</summary>
    public override int GetHashCode() =>
        HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(Table), StringComparer.OrdinalIgnoreCase.GetHashCode(Column));
}

/// <summary>
/// A Reference in the text of a Formula being edited (ADR-0057): where it is written, and what it
/// names — cells on this Sheet, or a Linked Table's column. Exactly one of <see cref="Cells"/> and
/// <see cref="LinkedColumn"/> is set.
/// </summary>
/// <param name="Start">Where it starts in the text, whose <c>=</c> is at 0.</param>
/// <param name="Length">How many characters it spans, its Sheet qualifier included.</param>
/// <param name="Cells">
/// The cells it names, held from their top-left corner however it was written (<c>B2:A1</c> names
/// A1:B2), whole columns for <c>A:A</c> and whole rows for <c>1:1</c>; <see langword="null"/> for a
/// structured reference.
/// </param>
/// <param name="LinkedColumn">The Linked Table column a structured reference reads; <see langword="null"/> for cells.</param>
public readonly record struct FormulaReference(int Start, int Length, CellRange? Cells, LinkedTableColumn? LinkedColumn);

/// <summary>
/// Pure answers over the text of a Formula being typed and its caret, for the aids ADR-0051 asks
/// of the Consumer: completion candidates, the argument hint, whether a Reference can go at the
/// caret (Point mode), the Reference text for a pointed range, and what F4 makes of the Reference
/// at the caret; and, for ADR-0057's Reference Outlines, every Reference in the text. Each works
/// on unfinished text: nothing here requires the Formula to parse. Text not beginning with
/// <c>=</c> is not a Formula, and gets no aid.
/// </summary>
public static partial class FormulaEntry
{
    /// <summary>
    /// The names beginning with the name being typed at the caret, without regard to case: the
    /// declared functions and <paramref name="linkedTables"/>. <see langword="null"/> when the caret
    /// is not at the end of a name standing where an operand can start (inside text in quotes, a
    /// Reference with <c>$</c>, a number, a column in brackets), or when nothing matches.
    /// </summary>
    public static FormulaCompletion? Complete(string text, int caret, IEnumerable<string> linkedTables)
    {
        ArgumentNullException.ThrowIfNull(linkedTables);
        if (!IsFormula(text, caret)) return null;
        var tokens = Scan(text);
        var index = tokens.FindIndex(t => t.Start < caret && caret <= t.End);
        if (index < 0) return null;
        var token = tokens[index];
        if (token.Kind != TokenKind.Operand || token.Unterminated || token.HasBrackets) return null;
        var prefix = text[token.Start..caret];
        if (!NamePattern().IsMatch(prefix) || !OperandMayStart(tokens, index)) return null;

        var candidates = DeclaredFunction.All
            .Where(f => f.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(f => new CompletionCandidate(f.Name, CompletionKind.Function, f.Name + "(", f.Description))
            .Concat(linkedTables
                .Where(t => t.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(t => new CompletionCandidate(t, CompletionKind.LinkedTable, t, null)))
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return candidates.Count == 0 ? null : new FormulaCompletion(token.Start, token.End - token.Start, candidates);
    }

    /// <summary>
    /// The hint for the innermost function call whose argument list holds the caret, counting
    /// arguments by the commas at its own depth; grouping parentheses inside it belong to it, and
    /// text in quotes is one argument whatever it holds. <see langword="null"/> outside any call,
    /// or when that function is not declared.
    /// </summary>
    public static ArgumentHint? HintAt(string text, int caret)
    {
        if (!IsFormula(text, caret)) return null;
        var tokens = Scan(text);
        var frames = new Stack<(string? Function, int Commas)>();
        for (var i = 0; i < tokens.Count && tokens[i].End <= caret; i++)
        {
            var token = tokens[i];
            switch (token.Kind)
            {
                case TokenKind.Open:
                    var callee = i > 0 && tokens[i - 1].Kind == TokenKind.Operand && tokens[i - 1].End == token.Start && NamePattern().IsMatch(text[tokens[i - 1].Start..tokens[i - 1].End])
                        ? text[tokens[i - 1].Start..tokens[i - 1].End]
                        : null;
                    frames.Push((callee, 0));
                    break;
                case TokenKind.Close:
                    if (frames.Count > 0) frames.Pop();
                    break;
                case TokenKind.Comma:
                    if (frames.Count > 0)
                    {
                        var (function, commas) = frames.Pop();
                        frames.Push((function, commas + 1));
                    }
                    break;
            }
        }
        foreach (var (function, commas) in frames)
        {
            if (function is null) continue;
            if (DeclaredFunction.Find(function) is not { } declared) return null;
            return new ArgumentHint(declared, commas, ArgumentName(declared, commas));
        }
        return null;
    }

    private static string? ArgumentName(DeclaredFunction function, int index)
    {
        var names = function.Arguments.Split(", ");
        if (index < names.Length && names[index] != "...") return names[index];
        return names[^1] == "..." ? names[^2] : null;
    }

    /// <summary>
    /// Whether a Reference can be written at the caret (ADR-0051's Point predicate): the Formula
    /// stands after its <c>=</c>, an operator (not <c>%</c>), <c>(</c> or <c>,</c>, with nothing
    /// after the caret that the Reference would run into. <see langword="null"/> when it cannot.
    /// </summary>
    public static PointSite? PointAt(string text, int caret)
    {
        if (!IsFormula(text, caret)) return null;
        var tokens = Scan(text);
        if (tokens.Any(t => t.Start < caret && caret < t.End) || tokens.Any(t => t.Unterminated && t.Start < caret)) return null;
        var before = tokens.FindLastIndex(t => t.End <= caret);
        if (!OperandMayStart(tokens, before + 1) || !MayFollowOperand(tokens, caret)) return null;
        return new PointSite(caret, 0);
    }

    /// <summary>
    /// The Reference that ends at the caret and stands where Point mode could have written it —
    /// the one a further arrow key replaces while the user is pointing (ADR-0051). Only the
    /// component knows whether it is pointing; typed by hand, the same text is not pointed.
    /// <see langword="null"/> when no Reference ends at the caret in such a place.
    /// </summary>
    public static PointSite? PointedReferenceAt(string text, int caret)
    {
        if (!IsFormula(text, caret)) return null;
        var tokens = Scan(text);
        var index = tokens.FindIndex(t => t.End == caret);
        if (index < 0) return null;
        var token = tokens[index];
        if (token.Kind != TokenKind.Operand || !PointedPattern().IsMatch(text[token.Start..token.End])) return null;
        if (!OperandMayStart(tokens, index) || !MayFollowOperand(tokens, caret)) return null;
        return new PointSite(token.Start, token.End - token.Start);
    }

    /// <summary>The Reference Point mode writes for a pointed range: <c>B7</c> for one cell, <c>B7:C9</c> from its top-left otherwise.</summary>
    public static string ReferenceText(CellRange range) => range.ToString();

    /// <summary>
    /// F4 (ADR-0051, 2026-09-29): the Reference at the caret cycled to its next form —
    /// <c>A1</c> → <c>$A$1</c> → <c>A$1</c> → <c>$A1</c> → <c>A1</c> — and the caret at the end of
    /// it. The Reference at the caret is the one the caret is inside or touching, on either side.
    /// With a selection, every Reference it covers, overlaps or touches at either end cycles —
    /// <c>+</c> selected in <c>=A1+B1</c> cycles both — each to the next form of the first one,
    /// and the selection covers what was rewritten. A range cycles as one, its
    /// next form taken from its first end and given to both; whole columns and whole rows have
    /// two forms (<c>A:A</c> ↔ <c>$A:$A</c>); a Sheet qualifier is kept, and only the cell part
    /// cycles. Only the <c>$</c> signs change: every other character stays as it was typed.
    /// <see langword="null"/> — nothing changes — for text that is not a Formula, and where no
    /// Reference is at the caret or in the selection: a function name, a number, text in
    /// quotes, a structured reference. A Reference is what the formula grammar reads as one.
    /// </summary>
    /// <param name="text">The text being edited.</param>
    /// <param name="selectionStart">Where the selection starts, or the caret.</param>
    /// <param name="selectionEnd">Where the selection ends; <paramref name="selectionStart"/> for a caret.</param>
    /// <exception cref="ArgumentOutOfRangeException">The selection does not lie inside the text.</exception>
    public static ReferenceCycle? CycleReference(string text, int selectionStart, int selectionEnd)
    {
        ArgumentNullException.ThrowIfNull(text);
        if ((uint)selectionStart > (uint)text.Length) throw new ArgumentOutOfRangeException(nameof(selectionStart), selectionStart, "The selection starts 0 to the text's length.");
        if (selectionEnd < selectionStart || selectionEnd > text.Length) throw new ArgumentOutOfRangeException(nameof(selectionEnd), selectionEnd, "The selection ends from its start to the text's length.");
        if (text.Length == 0 || text[0] != '=') return null;

        // Inside or touching, for a caret and a selection alike (observed in Excel: + selected
        // in =A1+B1 gives =$A$1+$B$1). A caret takes the first Reference it touches.
        var caret = selectionStart == selectionEnd;
        var targets = new List<Match>();
        foreach (var token in Scan(text))
        {
            var touched = token.Start <= selectionEnd && selectionStart <= token.End;
            if (!touched || token.Kind != TokenKind.Operand || token.Unterminated || token.HasBrackets) continue;
            // The operand is a Reference only where the grammar reads one over the whole of it.
            if (Formulas.Lexer.MatchReference(text, token.Start) is { } reference && reference.Index + reference.Length == token.End)
            {
                targets.Add(reference);
                if (caret) break;
            }
        }
        if (targets.Count == 0) return null;

        var form = NextForm(targets[0]);
        var rewritten = new StringBuilder(text.Length + 4 * targets.Count);
        var at = 0;
        var start = targets[0].Index;
        foreach (var reference in targets)
        {
            rewritten.Append(text, at, reference.Index - at);
            at = reference.Index;
            foreach (var (part, column) in Parts(reference))
            {
                rewritten.Append(text, at, part.Index - at);
                if (column ? form.Column : form.Row) rewritten.Append('$');
                rewritten.Append(part.Value.TrimStart('$'));
                at = part.Index + part.Length;
            }
            rewritten.Append(text, at, reference.Index + reference.Length - at);
            at = reference.Index + reference.Length;
        }
        var end = rewritten.Length;
        rewritten.Append(text, at, text.Length - at);
        return new ReferenceCycle(rewritten.ToString(), caret ? end : start, end);
    }

    /// <summary>
    /// The form F4 gives next, from the first end of <paramref name="reference"/>: whether
    /// columns and rows are written with <c>$</c>. A cell's four forms run relative, both, the
    /// row alone, the column alone; whole columns and whole rows have two, relative and
    /// absolute, and give both axes the one they take, so that in a selection a cell after them
    /// follows the same way.
    /// </summary>
    private static (bool Column, bool Row) NextForm(Match reference)
    {
        if (reference.Groups["cc1"].Success) return reference.Groups["cc1"].Value[0] == '$' ? (false, false) : (true, true);
        if (reference.Groups["rr1"].Success) return reference.Groups["rr1"].Value[0] == '$' ? (false, false) : (true, true);
        return (reference.Groups["c1"].Value[0] == '$', reference.Groups["r1"].Value[0] == '$') switch
        {
            (false, false) => (true, true),
            (true, true) => (false, true),
            (false, true) => (true, false),
            _ => (false, false),
        };
    }

    /// <summary>The column and row parts of a Reference the grammar matched, in the order written, each with whether it is a column.</summary>
    private static IEnumerable<(Group Part, bool Column)> Parts(Match reference)
    {
        foreach (var (name, column) in PartNames)
        {
            var group = reference.Groups[name];
            if (group.Success) yield return (group, column);
        }
    }

    private static readonly (string Name, bool Column)[] PartNames =
        [("c1", true), ("r1", false), ("c2", true), ("r2", false), ("cc1", true), ("cc2", true), ("rr1", false), ("rr2", false)];

    /// <summary>
    /// Every Reference in the text of a Formula being edited, in the order written (ADR-0057): its
    /// span, and the cells it names or, for a structured reference, the Linked Table column it
    /// reads. A Reference is what the formula grammar reads as one over the whole of an operand, as
    /// for F4. The text need not parse: an unfinished Formula (<c>=SUM(A1,</c>) is answered as far
    /// as it goes, and an unclosed string, quoted Sheet name or bracket hides only what follows it.
    /// Nothing inside a string is a Reference, nor is a function name (<c>LOG10(</c>), nor one
    /// qualified with another Sheet's name, which names no cells. Text not beginning with <c>=</c>
    /// is answered with nothing.
    /// </summary>
    /// <param name="text">The text being edited.</param>
    /// <param name="sheetName">This Sheet's name: a Reference qualified with it, without regard to case, names this Sheet's cells.</param>
    public static IReadOnlyList<FormulaReference> References(string text, string sheetName)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(sheetName);
        if (text.Length == 0 || text[0] != '=') return [];

        var references = new List<FormulaReference>();
        foreach (var token in Scan(text))
        {
            if (token.Kind != TokenKind.Operand || token.Unterminated) continue;
            var length = token.End - token.Start;
            if (token.HasBrackets)
            {
                if (Formulas.Lexer.ReadStructuredReference(text, token.Start) is { } structured && structured.Length == length)
                {
                    references.Add(new FormulaReference(token.Start, length, null, new LinkedTableColumn(structured.Table, structured.Column)));
                }
            }
            else if (Formulas.Lexer.ReadReference(text, token.Start, out var read) is { } reference && read == length && Sheet.Names(reference.SheetName, sheetName))
            {
                var area = reference.Area;
                var cells = new CellRange(new CellAddress(area.Row1, area.Column1), new CellAddress(area.Row2, area.Column2));
                references.Add(new FormulaReference(token.Start, length, cells, null));
            }
        }
        return references;
    }

    private static bool IsFormula(string text, int caret)
    {
        ArgumentNullException.ThrowIfNull(text);
        if ((uint)caret > (uint)text.Length) throw new ArgumentOutOfRangeException(nameof(caret), caret, "The caret is 0 to the text's length.");
        return text.Length > 0 && text[0] == '=' && caret >= 1;
    }

    /// <summary>Whether an operand can start at token <paramref name="index"/>: right after the <c>=</c>, an operator that takes a right side, <c>(</c> or <c>,</c>.</summary>
    private static bool OperandMayStart(List<Token> tokens, int index)
    {
        if (index == 0) return true;
        var previous = tokens[index - 1];
        return previous.Kind is TokenKind.Open or TokenKind.Comma || (previous.Kind == TokenKind.Operator && previous.Text != "%");
    }

    /// <summary>Whether what follows the caret leaves room for an operand: the end, <c>)</c>, <c>,</c> or an operator.</summary>
    private static bool MayFollowOperand(List<Token> tokens, int caret)
    {
        var next = tokens.Find(t => t.Start >= caret);
        return next is null || next.Kind is TokenKind.Close or TokenKind.Comma or TokenKind.Operator;
    }

    [GeneratedRegex(@"^[A-Za-z_\\][A-Za-z0-9_.]*$", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();

    [GeneratedRegex(@"^\$?[A-Za-z]{1,3}\$?[1-9][0-9]{0,6}(?::\$?[A-Za-z]{1,3}\$?[1-9][0-9]{0,6})?$", RegexOptions.CultureInvariant)]
    private static partial Regex PointedPattern();

    private enum TokenKind
    {
        Operand,
        Text,
        Operator,
        Open,
        Close,
        Comma,
    }

    private sealed record Token(TokenKind Kind, int Start, int End, string Text, bool Unterminated = false, bool HasBrackets = false);

    private static readonly string[] Operators = ["<>", "<=", ">=", "=", "<", ">", "+", "-", "*", "/", "^", "&", "%"];

    /// <summary>
    /// A tolerant scan of a Formula's text after its <c>=</c>: text in quotes, parentheses,
    /// commas, operators, and operands — a run of name, number, Reference or Error Value
    /// characters, with a quoted Sheet name and a structured reference's brackets inside it. It
    /// never fails: an unclosed quote or bracket runs to the end and is marked so.
    /// </summary>
    private static List<Token> Scan(string text)
    {
        var tokens = new List<Token>();
        var i = 1;
        while (i < text.Length)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }
            var start = i;
            if (c == '"')
            {
                i++;
                var closed = false;
                while (i < text.Length)
                {
                    if (text[i] == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            i += 2;
                            continue;
                        }
                        i++;
                        closed = true;
                        break;
                    }
                    i++;
                }
                tokens.Add(new Token(TokenKind.Text, start, i, text[start..i], !closed));
                continue;
            }
            if (c is '(' or ')' or ',')
            {
                tokens.Add(new Token(c == '(' ? TokenKind.Open : c == ')' ? TokenKind.Close : TokenKind.Comma, i, i + 1, c.ToString()));
                i++;
                continue;
            }
            var op = Array.Find(Operators, o => string.CompareOrdinal(text, i, o, 0, o.Length) == 0);
            if (op is not null)
            {
                tokens.Add(new Token(TokenKind.Operator, i, i + op.Length, op));
                i += op.Length;
                continue;
            }

            // An Error Value, whose spelling holds characters that are operators elsewhere (#DIV/0!).
            if (c == '#')
            {
                i++;
                while (i < text.Length && (char.IsAsciiLetterOrDigit(text[i]) || text[i] is '/' or '!' or '?' or '_')) i++;
                tokens.Add(new Token(TokenKind.Operand, start, i, text[start..i]));
                continue;
            }

            // An operand: everything up to the next space, quote, parenthesis, comma or operator,
            // with a quoted Sheet name and bracketed columns read whole.
            var unterminated = false;
            var brackets = false;
            while (i < text.Length && !unterminated)
            {
                var d = text[i];
                if (d == '\'')
                {
                    i++;
                    unterminated = true;
                    while (i < text.Length)
                    {
                        if (text[i] == '\'' && !(i + 1 < text.Length && text[i + 1] == '\''))
                        {
                            i++;
                            unterminated = false;
                            break;
                        }
                        i += text[i] == '\'' ? 2 : 1;
                    }
                    continue;
                }
                if (d == '[')
                {
                    brackets = true;
                    var depth = 0;
                    unterminated = true;
                    while (i < text.Length)
                    {
                        if (text[i] == '\'' && i + 1 < text.Length)
                        {
                            i += 2;
                            continue;
                        }
                        if (text[i] == '[') depth++;
                        else if (text[i] == ']' && --depth == 0)
                        {
                            i++;
                            unterminated = false;
                            break;
                        }
                        i++;
                    }
                    continue;
                }
                if (char.IsWhiteSpace(d) || d is '"' or '(' or ')' or ',' || Array.Exists(Operators, o => o[0] == d)) break;
                i++;
            }
            if (i == start) i++;
            tokens.Add(new Token(TokenKind.Operand, start, i, text[start..i], unterminated, brackets));
        }
        return tokens;
    }
}

public sealed partial class Sheet
{
    /// <summary>
    /// Completion for the Formula being typed (ADR-0051): the declared functions and this Sheet's
    /// Linked Tables whose names begin with the name at the caret (<see cref="FormulaEntry.Complete"/>).
    /// </summary>
    public FormulaCompletion? Complete(string text, int caret) =>
        FormulaEntry.Complete(text, caret, _tables.Values.OrderBy(t => t.Order).Select(t => t.Name));

    /// <summary>
    /// The References in the Formula being edited (ADR-0057), a qualifier naming this Sheet by its
    /// <see cref="Name"/> as it is now (<see cref="FormulaEntry.References"/>). A structured
    /// reference is kept only when its Linked Table is declared with that column: one naming a table
    /// or a column that is not names nothing, as a Reference to another Sheet does not. A declared
    /// table still waiting for its data is kept.
    /// </summary>
    public IReadOnlyList<FormulaReference> References(string text)
    {
        var references = FormulaEntry.References(text, Name);
        return references.Any(r => r.LinkedColumn is { } column && !IsDeclared(column))
            ? [.. references.Where(r => r.LinkedColumn is not { } column || IsDeclared(column))]
            : references;
    }

    private bool IsDeclared(LinkedTableColumn column) =>
        _tables.TryGetValue(column.Table, out var table) && table.ColumnIndex.ContainsKey(column.Column);
}
