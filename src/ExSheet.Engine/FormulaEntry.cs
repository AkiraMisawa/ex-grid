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

    /// <summary>A column of the Linked Table named before <c>[</c> (ADR-0058).</summary>
    LinkedTableColumn,

    /// <summary>One of the values an argument takes from a fixed list (<see cref="T:ExSheet.Engine.ArgumentValue"/>, ADR-0058).</summary>
    ArgumentValue,
}

/// <summary>One candidate completion offers (ADR-0051, ADR-0058).</summary>
/// <param name="Name">What the list shows: the name as declared, or for an argument's value, Excel's text for it (<c>0 - Exact match</c>).</param>
/// <param name="Kind">What it names.</param>
/// <param name="InsertText">
/// What accepting it writes in place of what was typed: a function's name and its <c>(</c>, a
/// table's name, a column's name with any character the grammar reads specially escaped by
/// <c>'</c>, an argument's value (<c>0</c>).
/// </param>
/// <param name="Description">One sentence for a function; <see langword="null"/> for the others.</param>
public sealed record CompletionCandidate(string Name, CompletionKind Kind, string InsertText, string? Description);

/// <summary>
/// The candidates for what is being typed at the caret (ADR-0051): accepting one replaces the
/// <see cref="Length"/> characters from <see cref="Start"/> with its <see cref="CompletionCandidate.InsertText"/>.
/// </summary>
/// <param name="Start">Where what is being typed starts in the text: a name, a column after <c>[</c>, an argument's value.</param>
/// <param name="Length">How long it is, including any part of it after the caret.</param>
/// <param name="Candidates">
/// The candidates, in the order to list them; never empty. Names are in alphabetical order, a
/// table's columns in the table's order, and an argument's values in Excel's.
/// </param>
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
    /// What completion offers at the caret (ADR-0051, ADR-0058), as
    /// <see cref="Complete(string, int, IEnumerable{string}, Func{string, IReadOnlyList{string}?})"/>
    /// offers it where no table's columns are known: nothing is listed after <c>Table[</c>.
    /// </summary>
    public static FormulaCompletion? Complete(string text, int caret, IEnumerable<string> linkedTables) =>
        Complete(text, caret, linkedTables, static _ => null);

    /// <summary>
    /// What completion offers at the caret, following Excel's triggers (ADR-0051, ADR-0058):
    /// <list type="bullet">
    /// <item>at the end of a name standing where an operand can start, the declared functions and
    /// <paramref name="linkedTables"/> beginning with it, without regard to case;</item>
    /// <item>inside the brackets of <c>Table[</c>, the columns of that table beginning with the column
    /// typed so far, in the table's order, and nothing else: not <c>@</c> or <c>#All</c>, which the
    /// grammar refuses (ADR-0047);</item>
    /// <item>at an argument that takes one of a fixed list of values (<see cref="DeclaredFunction.ValuesOf"/>),
    /// those values, in Excel's order, and nothing else: whatever is typed there, no function and
    /// no table is listed (the thirteenth Windows run, Q54). They are listed before anything is
    /// typed, and only while nothing of the argument stands after the caret, white space included:
    /// before a value, inside one or before white space, nothing is listed (Part B of the ninth
    /// Windows run, Q49; the thirteenth, Q55). A number lists the value it is alone (<c>0</c> lists
    /// <c>0 - Exact match</c>) and nothing when it is no value (<c>4</c>); any other text lists every
    /// value, as Excel does not narrow a value list by what is typed (<c>-</c>, <c>1+</c>, <c>A1</c>,
    /// <c>X</c> and <c>"</c> list all five of <c>match_mode</c>'s; the tenth and thirteenth Windows
    /// runs).</item>
    /// </list>
    /// <see langword="null"/> anywhere else — after <c>=</c>, an operator, <c>(</c> or <c>,</c> at any
    /// other argument or inside a grouping parenthesis, inside text in quotes, a Reference, a number
    /// — and when nothing matches.
    /// </summary>
    /// <param name="text">The text being edited.</param>
    /// <param name="caret">Where the caret stands in it.</param>
    /// <param name="linkedTables">The Linked Tables' names.</param>
    /// <param name="columnsOf">
    /// The columns of the Linked Table a name names, found without regard to case, in the table's
    /// order; <see langword="null"/> for a name no table has.
    /// </param>
    public static FormulaCompletion? Complete(string text, int caret, IEnumerable<string> linkedTables, Func<string, IReadOnlyList<string>?> columnsOf)
    {
        ArgumentNullException.ThrowIfNull(linkedTables);
        ArgumentNullException.ThrowIfNull(columnsOf);
        if (!IsFormula(text, caret)) return null;
        var tokens = Scan(text);
        // An argument whose values are a fixed list is completed with them alone: letters there
        // list no function and no table, as Excel's do not (ADR-0058, the thirteenth run, Q54).
        if (ValueArgumentAt(text, tokens, caret) is { } argument)
            return CompleteValue(text, caret, tokens, argument.Start, argument.Values);
        var index = tokens.FindIndex(t => t.Start < caret && caret <= t.End);
        if (index >= 0 && tokens[index] is { Kind: TokenKind.Operand, HasBrackets: true } bracketed)
            return OperandMayStart(tokens, index) ? CompleteColumn(text, caret, bracketed, columnsOf) : null;
        return index >= 0 ? CompleteName(text, caret, tokens, index, linkedTables) : null;
    }

    /// <summary>The functions and tables beginning with the name that ends at the caret.</summary>
    private static FormulaCompletion? CompleteName(string text, int caret, List<Token> tokens, int index, IEnumerable<string> linkedTables)
    {
        var token = tokens[index];
        if (token.Kind != TokenKind.Operand || token.Unterminated) return null;
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
    /// The columns after <c>Table[</c> (ADR-0058): the caret stands inside the one pair of brackets
    /// the grammar reads, after a declared table's name, and what is typed before it is the
    /// beginning of a column, <c>'</c> escapes read. Accepting a column writes its name, escaped,
    /// over the column typed — up to the closing bracket when there is one — and leaves the
    /// closing bracket to the user, as for a table's name.
    /// </summary>
    private static FormulaCompletion? CompleteColumn(string text, int caret, Token token, Func<string, IReadOnlyList<string>?> columnsOf)
    {
        var open = text.IndexOf('[', token.Start, token.End - token.Start);
        if (open < 0 || caret <= open) return null;
        var table = text[token.Start..open];
        if (!NamePattern().IsMatch(table) || columnsOf(table) is not { Count: > 0 } columns) return null;

        var typed = new StringBuilder();
        var end = -1;
        for (var i = open + 1; i < token.End; i++)
        {
            var c = text[i];
            if (c == '\'')
            {
                // An escape the caret splits leaves no column typed that can be read.
                if (i < caret && i + 1 >= caret) return null;
                if (i < caret) typed.Append(text[i + 1]);
                i++;
                continue;
            }
            if (c == ']')
            {
                end = i;
                break;
            }
            // A second bracket, #All and @ are forms the grammar refuses (ADR-0047). Past the
            // caret, only a second bracket matters: the first ] would not close this one.
            if (c == '[' || (i < caret && c is '#' or '@')) return null;
            if (i < caret) typed.Append(c);
        }
        if (end >= 0 && end < caret) return null;
        var prefix = typed.ToString();
        var candidates = columns
            .Where(column => column.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(column => new CompletionCandidate(column, CompletionKind.LinkedTableColumn, EscapeColumn(column), null))
            .ToList();
        var start = open + 1;
        return candidates.Count == 0 ? null : new FormulaCompletion(start, (end >= 0 ? end : caret) - start, candidates);
    }

    /// <summary>A column's name as a structured reference writes it: <c>'</c> before each character the grammar reads specially.</summary>
    private static string EscapeColumn(string column)
    {
        if (column.IndexOfAny(ColumnSpecials) < 0) return column;
        var escaped = new StringBuilder(column.Length + 2);
        foreach (var c in column)
        {
            if (Array.IndexOf(ColumnSpecials, c) >= 0) escaped.Append('\'');
            escaped.Append(c);
        }
        return escaped.ToString();
    }

    private static readonly char[] ColumnSpecials = ['[', ']', '#', '@', '\''];

    /// <summary>
    /// Where the argument the caret stands at starts, and its values, when the innermost
    /// parenthesis open at the caret is a declared function's and that argument takes one of a
    /// fixed list (ADR-0058); <see langword="null"/> anywhere else. Inside a grouping parenthesis
    /// the caret stands in an expression of its own, as inside a call of its own.
    /// </summary>
    private static (int Start, IReadOnlyList<ArgumentValue> Values)? ValueArgumentAt(string text, List<Token> tokens, int caret)
    {
        if (FramesAt(text, tokens, caret) is not { Count: > 0 } frames) return null;
        var call = frames.Peek();
        if (call.Function is null || DeclaredFunction.Find(call.Function) is not { } function) return null;
        var values = function.ValuesOf(call.Commas);
        return values.Count == 0 ? null : (call.ArgumentStart, values);
    }

    /// <summary>
    /// The values of an argument that takes one of a fixed list (ADR-0058), while nothing of it
    /// stands after the caret: the caret is at the argument's end, before the <c>,</c> or <c>)</c>
    /// that ends it. Before a value, inside one or before white space Excel lists nothing (Part B
    /// of the ninth Windows run, Q49; the thirteenth, Q55). A number lists the value it is alone,
    /// and nothing when it is no value; any other text — nothing yet, the beginning of a value,
    /// letters, a Reference, text in quotes — lists every value, the first to be chosen (the tenth
    /// and thirteenth Windows runs, Q54). Accepting one writes it over everything typed in the
    /// argument.
    /// </summary>
    private static FormulaCompletion? CompleteValue(string text, int caret, List<Token> tokens, int argumentStart, IReadOnlyList<ArgumentValue> values)
    {
        if (tokens.Exists(t => t.Start < caret && caret < t.End)) return null;
        if (caret < text.Length && text[caret] is not (',' or ')')) return null;

        var start = argumentStart;
        while (start < caret && char.IsWhiteSpace(text[start])) start++;
        IReadOnlyList<ArgumentValue> listed = NumberOf(text[start..caret]) is { } number
            ? [.. values.Where(v => NumberOf(v.Value) == number)]
            : values;
        if (listed.Count == 0) return null;
        var candidates = listed
            .Select(v => new CompletionCandidate(v.Text, CompletionKind.ArgumentValue, v.Value, null))
            .ToList();
        return new FormulaCompletion(start, caret - start, candidates);
    }

    /// <summary>
    /// The number <paramref name="typed"/> is, read as the grammar reads a number constant with
    /// one sign before it and white space after it (<c>-1</c>, <c>+1</c>, <c>1.0</c>, <c>1 </c>), or
    /// <see langword="null"/> for text that is not a number (<c>-</c>, <c>1+</c>, <c>A1</c>).
    /// </summary>
    private static double? NumberOf(string typed)
    {
        var match = SignedNumberPattern().Match(typed);
        if (!match.Success) return null;
        var number = ConstantParser.ParseFormulaNumber(match.Groups["number"].Value);
        return match.Groups["sign"].Value == "-" ? -number : number;
    }

    [GeneratedRegex(@"^(?<sign>[+-]?)(?<number>(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?)\s*\z", RegexOptions.CultureInvariant)]
    private static partial Regex SignedNumberPattern();

    /// <summary>
    /// The hint for the innermost function call whose argument list holds the caret, counting
    /// arguments by the commas at its own depth; grouping parentheses inside it belong to it, and
    /// text in quotes is one argument whatever it holds. <see langword="null"/> outside any call,
    /// or when that function is not declared.
    /// </summary>
    public static ArgumentHint? HintAt(string text, int caret)
    {
        if (!IsFormula(text, caret)) return null;
        foreach (var (function, commas, _) in FramesAt(text, Scan(text), caret))
        {
            if (function is null) continue;
            if (DeclaredFunction.Find(function) is not { } declared) return null;
            return new ArgumentHint(declared, commas, ArgumentName(declared, commas));
        }
        return null;
    }

    /// <summary>A parenthesis open at the caret: the function it calls (<see langword="null"/> for a grouping one), how many of its commas stand before the caret, and where the argument the caret is in starts.</summary>
    private readonly record struct Frame(string? Function, int Commas, int ArgumentStart);

    /// <summary>The parentheses open at the caret, the innermost on top.</summary>
    private static Stack<Frame> FramesAt(string text, List<Token> tokens, int caret)
    {
        var frames = new Stack<Frame>();
        for (var i = 0; i < tokens.Count && tokens[i].End <= caret; i++)
        {
            var token = tokens[i];
            switch (token.Kind)
            {
                case TokenKind.Open:
                    var callee = i > 0 && tokens[i - 1].Kind == TokenKind.Operand && tokens[i - 1].End == token.Start && NamePattern().IsMatch(text[tokens[i - 1].Start..tokens[i - 1].End])
                        ? text[tokens[i - 1].Start..tokens[i - 1].End]
                        : null;
                    frames.Push(new Frame(callee, 0, token.End));
                    break;
                case TokenKind.Close:
                    if (frames.Count > 0) frames.Pop();
                    break;
                case TokenKind.Comma:
                    if (frames.Count > 0)
                    {
                        var frame = frames.Pop();
                        frames.Push(new Frame(frame.Function, frame.Commas + 1, token.End));
                    }
                    break;
            }
        }
        return frames;
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
    /// What Point writes for a pressed column of a Linked Table, through a Pointing Scope
    /// (ADR-0058): Excel's structured reference, <c>Positions[PV]</c>, written as the engine writes
    /// one back — single brackets, with <c>'</c> before each of <c>[ ] # '</c> in the column's name.
    /// </summary>
    /// <param name="table">The table's name, as declared.</param>
    /// <param name="column">The column's name, as declared.</param>
    /// <exception cref="ArgumentException">Either name is null or empty.</exception>
    public static string StructuredReferenceText(string table, string column)
    {
        ArgumentException.ThrowIfNullOrEmpty(table);
        ArgumentException.ThrowIfNullOrEmpty(column);
        var text = new StringBuilder();
        Formulas.FormulaText.WriteStructuredReference(text, table, column);
        return text.ToString();
    }

    /// <summary>
    /// A Value written as a Formula writes a constant of its kind (ADR-0058): text in double quotes,
    /// with any <c>"</c> in it doubled (<c>"R-4471"</c>); a number in the invariant form the engine
    /// writes a number constant in (<c>1250</c>, <c>-0.5</c>); a boolean as <c>TRUE</c> or
    /// <c>FALSE</c>. <see langword="null"/> for an Error Value: a lookup by one finds nothing, so
    /// nothing is written for it.
    /// </summary>
    public static string? ConstantText(Value value) => value.Kind switch
    {
        ValueKind.Text => "\"" + value.Text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"",
        ValueKind.Number => NumberText.Written(value.Number, System.Globalization.CultureInfo.InvariantCulture, NumberText.FormulaConstantLongest),
        ValueKind.Boolean => value.Boolean ? "TRUE" : "FALSE",
        _ => null,
    };

    /// <summary>
    /// What Point writes for a pressed cell of a Linked Table, through a Pointing Scope (ADR-0058):
    /// the lookup that reads the cell by its row's key,
    /// <c>XLOOKUP("R-4471", Positions[Id], Positions[PV])</c> — the key as a constant of its kind
    /// (<see cref="ConstantText"/>), then the key column and the pressed column as structured
    /// references (<see cref="StructuredReferenceText"/>). It names no position, so the Formula
    /// reads the same row after the grid that showed it is sorted (ADR-0049, rule 2).
    /// </summary>
    /// <param name="table">The table's name, as declared.</param>
    /// <param name="keyColumn">The table's key column, as declared.</param>
    /// <param name="key">The row's key: text, a number or a boolean.</param>
    /// <param name="column">The pressed column, as declared.</param>
    /// <exception cref="ArgumentException">A name is null or empty, or the key is an Error Value,
    /// which no lookup finds.</exception>
    public static string LookupText(string table, string keyColumn, Value key, string column)
    {
        var constant = ConstantText(key)
            ?? throw new ArgumentException("An Error Value is not a key a lookup finds (ADR-0058).", nameof(key));
        return "XLOOKUP(" + constant + ", " + StructuredReferenceText(table, keyColumn) + ", " + StructuredReferenceText(table, column) + ")";
    }

    /// <summary>
    /// Whether the lookup <see cref="LookupText"/> writes for <paramref name="key"/> finds a row whose
    /// key is <paramref name="candidate"/>: <c>XLOOKUP</c>'s exact match, which tells a number from
    /// text and text apart without regard to case, and never matches a blank or an Error Value. A
    /// Pointing Scope finds the row a press was written for by it, wherever the grid that shows the
    /// table has sorted that row to, and in a Window of new row instances alike (ADR-0058, "What is
    /// drawn").
    /// </summary>
    /// <param name="key">The key the lookup was written for.</param>
    /// <param name="candidate">A row's key; <see langword="null"/> is a blank.</param>
    public static bool LookupFinds(Value key, Value? candidate) =>
        candidate is { } value && value.Kind == key.Kind && !key.IsError && Formulas.Evaluator.Compare(value, key) == 0;

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
    /// qualified with another Sheet's name, which names no cells. A range typed as far as its colon
    /// (<c>=SUM(A1:</c>) answers its first corner, the colon left out. Text beginning with <c>+</c> or
    /// <c>-</c> is answered as a Formula is, as Excel colours it and ExSheet enters it (<c>=+A1</c>);
    /// other text is answered with nothing (the eighth Windows run, cases 24–26).
    /// </summary>
    /// <param name="text">The text being edited.</param>
    /// <param name="sheetName">This Sheet's name: a Reference qualified with it, without regard to case, names this Sheet's cells.</param>
    public static IReadOnlyList<FormulaReference> References(string text, string sheetName)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(sheetName);
        if (text.Length == 0 || text[0] is not ('=' or '+' or '-')) return [];

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
            else if (ReadOperandReference(text, token.Start, length) is { } read && Sheet.Names(read.Reference.SheetName, sheetName))
            {
                var area = read.Reference.Area;
                var cells = new CellRange(new CellAddress(area.Row1, area.Column1), new CellAddress(area.Row2, area.Column2));
                references.Add(new FormulaReference(token.Start, read.Length, cells, null));
            }
        }
        return references;
    }

    /// <summary>
    /// The operand at <paramref name="start"/> read as a Reference over the whole of it, as F4 reads
    /// one; or, while a range is typed as far as its colon or into its second corner (<c>A1:</c>,
    /// <c>A1:B</c>), its first corner, which Excel colours (the eighth Windows run, case 26). The
    /// grammar reads no Reference with a colon after it, so the corner is read from the text cut at
    /// the colon. The colon looked for is the one after a Sheet qualifier's <c>!</c>, not one inside a
    /// quoted Sheet name, and what follows it must be the start of a second corner and no more:
    /// <c>A1:B2:C3</c> is not a range being typed, and answers nothing as before.
    /// </summary>
    private static (Formulas.Reference Reference, int Length)? ReadOperandReference(string text, int start, int length)
    {
        if (Formulas.Lexer.ReadReference(text, start, out var read) is { } whole && read == length) return (whole, read);
        var bang = text.LastIndexOf('!', start + length - 1, length);
        var from = bang >= start ? bang + 1 : start;
        var colon = text.IndexOf(':', from, start + length - from);
        if (colon <= start || !SecondCornerBegun().IsMatch(text.AsSpan(colon + 1, start + length - colon - 1))) return null;
        return Formulas.Lexer.ReadReference(text[..colon], start, out var corner) is { } first && corner == colon - start
            ? (first, corner)
            : null;
    }

    /// <summary>What may follow the colon of a range still being typed: nothing yet, or the start of
    /// a cell's address (<c>B</c>, <c>$B$</c>, <c>B1</c> before the grammar reads it whole).</summary>
    [GeneratedRegex(@"^\$?[A-Za-z]{0,3}\$?[0-9]{0,7}$", RegexOptions.CultureInvariant)]
    private static partial Regex SecondCornerBegun();

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
    /// Completion for the Formula being typed (ADR-0051, ADR-0058): the declared functions and this
    /// Sheet's Linked Tables whose names begin with the name at the caret, the columns of one of its
    /// tables after <c>Table[</c>, and an argument's values where it takes one of a fixed list
    /// (<see cref="FormulaEntry.Complete(string, int, IEnumerable{string}, Func{string, IReadOnlyList{string}?})"/>).
    /// </summary>
    public FormulaCompletion? Complete(string text, int caret) =>
        FormulaEntry.Complete(
            text,
            caret,
            _tables.Values.OrderBy(t => t.Order).Select(t => t.Name),
            name => _tables.TryGetValue(name, out var table) ? table.Columns : null);

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
