using System.Text;
using System.Text.RegularExpressions;
using ExSheet.Engine;

namespace ExSheet.Xlsx;

/// <summary>Why a Formula cannot be written as Excel stores it.</summary>
internal enum WrittenFormulaProblem
{
    None,

    /// <summary>
    /// A structured reference (<c>Positions[PV]</c>) to a table, or to a column of one, the Workbook does not declare: the
    /// engine shows <c>#NAME?</c> or <c>#REF!</c>, and the file would hold no Excel Table for Excel to read.
    /// </summary>
    StructuredReference,

    /// <summary>
    /// A structured reference to a Linked Table's column whose name begins or ends with a space, which a structured reference
    /// in Excel does not keep; holds <c>@</c>, which Excel reads at the start as the row the Formula stands in; or holds a
    /// character whose form in a structured reference has not been seen in Excel (<see cref="WrittenFormula.UnseenInColumns"/>). Ticket 07
    /// asks Excel how a file stores each.
    /// </summary>
    ColumnName,

    /// <summary>
    /// A Linked Table's name read as a name of its own (<c>=SUM(Positions)</c>): ExSheet shows <c>#NAME?</c> there, and Excel,
    /// once the file holds the Excel Table, reads the table's rows.
    /// </summary>
    TableName,

    /// <summary>The text does not split into the tokens the engine reads, or its names do not bind as the engine binds them.</summary>
    Unreadable,

    /// <summary>A Reference names a Sheet the Workbook does not have: Excel would read it as another workbook's.</summary>
    MissingSheet,

    /// <summary>
    /// A name a <c>LET</c> binds or a <c>LAMBDA</c> takes is called, and the engine declares no function of that name: the
    /// engine reads the call as one of an unknown function (<c>#NAME?</c>), and Excel calls what the name is bound to.
    /// </summary>
    BoundCall,

    /// <summary>A text constant in the Formula is longer than the 255 characters Excel takes in one.</summary>
    LongText,

    /// <summary>Calls are nested deeper than the 64 levels Excel takes.</summary>
    TooDeep,

    /// <summary>A call has more than the 255 arguments Excel takes.</summary>
    TooManyArguments,
}

/// <summary>
/// A Formula as ExSheet holds it, in the engine's invariant syntax (ADR-0047), written as an <c>.xlsx</c> stores it
/// (ADR-0330 §4), the reverse of <see cref="StoredFormula"/>: <c>_xlfn.</c> put back before each function newer than Excel
/// 2007, <c>_xlfn._xlws.</c> before <c>FILTER</c> and <c>SORT</c>, <c>_xlpm.</c> before each name a <c>LET</c> binds or a
/// <c>LAMBDA</c> takes, and the Spill Range reference <c>A1#</c> written <c>_xlfn.ANCHORARRAY(A1)</c>. Without its prefix,
/// Excel reads a newer function as a name it does not know, and shows <c>#NAME?</c>. Text in quotes, a Sheet's name, a
/// Reference and a number are never touched. A structured reference to a Linked Table, which is written as an Excel Table of
/// its name (ADR-0330 §6), is written as the engine stores it, which is Excel's own form: single brackets whatever the
/// column's name holds, with <c>'</c> before each of <c>[ ] # '</c> (<c>Positions[Market Value]</c>, <c>Positions[Rate'#]</c>;
/// observed, verification/2026-09-27-windows-excel, TEXT-021 and TEXT-022).
/// </summary>
/// <remarks>
/// The text is split as the engine's lexer splits it. Which names a <c>LET</c> or a <c>LAMBDA</c> binds is read from the
/// calls' arguments, and checked against what the engine itself found unbound (<see cref="Entry.UnboundNames"/>): where the
/// two disagree, the Formula is refused rather than written with a prefix in the wrong place.
/// </remarks>
internal static partial class WrittenFormula
{
    /// <summary>
    /// Excel's functions newer than Excel 2007, which a file stores with <c>_xlfn.</c> before them (ECMA-376's future
    /// functions, and the ones Excel has added since), every one of Excel's, declared by the engine or not: a Formula naming
    /// one the engine does not declare is carried as written, and Excel computes it.
    /// </summary>
    internal static readonly HashSet<string> Newer = new(StringComparer.OrdinalIgnoreCase)
    {
        "ACOT", "ACOTH", "AGGREGATE", "ARABIC", "ARRAYTOTEXT", "BASE", "BETA.DIST", "BETA.INV", "BINOM.DIST",
        "BINOM.DIST.RANGE", "BINOM.INV", "BITAND", "BITLSHIFT", "BITOR", "BITRSHIFT", "BITXOR", "BYCOL", "BYROW",
        "CEILING.MATH", "CEILING.PRECISE", "CHISQ.DIST", "CHISQ.DIST.RT", "CHISQ.INV", "CHISQ.INV.RT", "CHISQ.TEST",
        "CHOOSECOLS", "CHOOSEROWS", "COMBINA", "CONCAT", "CONFIDENCE.NORM", "CONFIDENCE.T", "COPILOT", "COT", "COTH",
        "COVARIANCE.P", "COVARIANCE.S", "CSC", "CSCH", "DAYS", "DECIMAL", "DETECTLANGUAGE", "DROP", "ECMA.CEILING",
        "ENCODEURL", "ERF.PRECISE", "ERFC.PRECISE", "EXPAND", "EXPON.DIST", "F.DIST", "F.DIST.RT", "F.INV", "F.INV.RT",
        "F.TEST", "FIELDVALUE", "FILTER", "FILTERXML", "FLOOR.MATH", "FLOOR.PRECISE", "FORECAST.ETS",
        "FORECAST.ETS.CONFINT", "FORECAST.ETS.SEASONALITY", "FORECAST.ETS.STAT", "FORECAST.LINEAR", "FORMULATEXT",
        "GAMMA", "GAMMA.DIST", "GAMMA.INV", "GAMMALN.PRECISE", "GAUSS", "GROUPBY", "HSTACK", "HYPGEOM.DIST", "IFNA", "IFS",
        "IMAGE", "IMCOSH", "IMCOT", "IMCSC", "IMCSCH", "IMSEC", "IMSECH", "IMSINH", "IMTAN", "ISFORMULA", "ISOMITTED",
        "ISOWEEKNUM", "LAMBDA", "LET", "LOGNORM.DIST", "LOGNORM.INV", "MAKEARRAY", "MAP", "MAXIFS", "MINIFS", "MODE.MULT",
        "MODE.SNGL", "MUNIT", "NEGBINOM.DIST", "NETWORKDAYS.INTL", "NORM.DIST", "NORM.INV", "NORM.S.DIST", "NORM.S.INV",
        "NUMBERVALUE", "PDURATION", "PERCENTILE.EXC", "PERCENTILE.INC", "PERCENTOF", "PERCENTRANK.EXC", "PERCENTRANK.INC",
        "PERMUTATIONA", "PHI", "PIVOTBY", "POISSON.DIST", "QUARTILE.EXC", "QUARTILE.INC", "RANDARRAY", "RANK.AVG",
        "RANK.EQ", "REDUCE", "REGEXEXTRACT", "REGEXREPLACE", "REGEXTEST", "RRI", "SCAN", "SEC", "SECH", "SEQUENCE",
        "SHEET", "SHEETS", "SKEW.P", "SORT", "SORTBY", "STDEV.P", "STDEV.S", "STOCKHISTORY", "SWITCH", "T.DIST",
        "T.DIST.2T", "T.DIST.RT", "T.INV", "T.INV.2T", "T.TEST", "TAKE", "TEXTAFTER", "TEXTBEFORE", "TEXTJOIN",
        "TEXTSPLIT", "TOCOL", "TOROW", "TRANSLATE", "TRIMRANGE", "UNICHAR", "UNICODE", "UNIQUE", "VALUETOTEXT", "VAR.P",
        "VAR.S", "VSTACK", "WEBSERVICE", "WEIBULL.DIST", "WORKDAY.INTL", "WRAPCOLS", "WRAPROWS", "XLOOKUP", "XMATCH",
        "XOR", "Z.TEST",
    };

    /// <summary>The functions Excel stores as worksheet-only as well as newer: <c>_xlfn._xlws.</c>.</summary>
    private static readonly HashSet<string> WorksheetOnly = new(StringComparer.OrdinalIgnoreCase) { "FILTER", "SORT" };

    private static readonly string[] Errors = ["#NULL!", "#DIV/0!", "#VALUE!", "#REF!", "#NAME?", "#NUM!", "#N/A"];

    private enum Kind
    {
        Space,
        Text,
        Error,
        Reference,
        Number,
        Name,
        Open,
        Close,
        Comma,
        Operator,
        StructuredReference,
    }

    private sealed record Token(Kind Kind, string Text)
    {
        /// <summary>A Reference written with <c>#</c> after it: the Spill Range of the Formula in that cell.</summary>
        public bool Spilled { get; init; }

        /// <summary>The Sheet a Reference names, unquoted, or null for one of its own Sheet; for a 3D Reference, the Sheet its span begins on.</summary>
        public string? Sheet { get; init; }

        /// <summary>For a 3D Reference (<c>Sheet1:Sheet3!B2</c>), the Sheet its span ends on, unquoted; a file stores one as the engine writes it.</summary>
        public string? LastSheet { get; init; }

        /// <summary>A name that a <c>(</c> follows: a function's.</summary>
        public bool Called { get; init; }

        /// <summary>A name a <c>LET</c> binds or a <c>LAMBDA</c> takes, or a use of one where it is bound.</summary>
        public bool Bound { get; set; }
    }

    /// <summary>The Formula's text as the file stores it, without its <c>=</c>, or the problem that refuses it.</summary>
    /// <param name="entry">A Formula Entry.</param>
    /// <param name="hasSheet">Whether the Workbook written has a Sheet of a name a Reference qualifies, without regard to case; null to take every name.</param>
    /// <param name="problem">Why it cannot be written; <see cref="WrittenFormulaProblem.None"/> when it can.</param>
    /// <param name="columnsOf">
    /// The columns of the Linked Table of a name, without regard to case, as declared, or null where none is; null to declare
    /// none, so that every structured reference is refused.
    /// </param>
    public static string Write(Entry entry, Func<string, bool>? hasSheet, out WrittenFormulaProblem problem, Func<string, IReadOnlyList<string>?>? columnsOf = null)
    {
        var formula = entry.Formula!;
        var tokens = Split(formula, columnsOf, out problem);
        if (problem != WrittenFormulaProblem.None) return formula[1..];
        if (hasSheet is not null && tokens.Any(t => (t.Sheet is { } sheet && !hasSheet(sheet)) || (t.LastSheet is { } last && !hasSheet(last))))
        {
            problem = WrittenFormulaProblem.MissingSheet;
            return formula[1..];
        }
        problem = Bind(tokens);
        if (problem != WrittenFormulaProblem.None) return formula[1..];
        if (columnsOf is not null && tokens.Any(t => t is { Kind: Kind.Name, Called: false, Bound: false } && columnsOf(t.Text) is not null))
        {
            problem = WrittenFormulaProblem.TableName;
            return formula[1..];
        }
        if (tokens.Any(t => t.Kind == Kind.Text && t.Text.Length - 2 - Count(t.Text, "\"\"") > MaxText))
        {
            problem = WrittenFormulaProblem.LongText;
            return formula[1..];
        }
        if (!SameUnbound(tokens, entry.UnboundNames))
        {
            problem = WrittenFormulaProblem.Unreadable;
            return formula[1..];
        }
        var text = new StringBuilder(formula.Length + 16);
        foreach (var token in tokens)
        {
            switch (token)
            {
                case { Kind: Kind.Reference, Spilled: true }:
                    text.Append("_xlfn.ANCHORARRAY(").Append(token.Text).Append(')');
                    break;
                case { Kind: Kind.Name, Bound: true }:
                    text.Append("_xlpm.").Append(token.Text);
                    break;
                case { Kind: Kind.Name, Called: true } when Newer.Contains(token.Text):
                    text.Append(WorksheetOnly.Contains(token.Text) ? "_xlfn._xlws." : "_xlfn.").Append(token.Text);
                    break;
                default:
                    text.Append(token.Text);
                    break;
            }
        }
        return text.ToString();
    }

    /// <summary>The longest text constant Excel takes in a Formula, in characters.</summary>
    internal const int MaxText = 255;

    /// <summary>The deepest Excel nests calls in a Formula.</summary>
    internal const int MaxDepth = 64;

    /// <summary>The most arguments Excel takes in one call.</summary>
    internal const int MaxArguments = 255;

    private static int Count(string text, string of)
    {
        var n = 0;
        for (var at = text.IndexOf(of, StringComparison.Ordinal); at >= 0; at = text.IndexOf(of, at + of.Length, StringComparison.Ordinal)) n++;
        return n;
    }

    /// <summary>The Formula's tokens after its <c>=</c>, split as the engine's lexer splits them.</summary>
    private static List<Token> Split(string formula, Func<string, IReadOnlyList<string>?>? columnsOf, out WrittenFormulaProblem problem)
    {
        problem = WrittenFormulaProblem.None;
        var tokens = new List<Token>();
        var i = 1;
        while (i < formula.Length)
        {
            var c = formula[i];
            if (c is ' ' or '\n' or '\r')
            {
                var start = i;
                while (i < formula.Length && formula[i] is ' ' or '\n' or '\r') i++;
                tokens.Add(new Token(Kind.Space, formula[start..i]));
                continue;
            }
            if (c == '"')
            {
                var j = i + 1;
                while (j < formula.Length && !(formula[j] == '"' && (j + 1 >= formula.Length || formula[j + 1] != '"')))
                    j += formula[j] == '"' ? 2 : 1;
                if (j >= formula.Length) return Unreadable(out problem);
                tokens.Add(new Token(Kind.Text, formula[i..(j + 1)]));
                i = j + 1;
                continue;
            }
            if (c == '#')
            {
                if (Errors.FirstOrDefault(e => string.Compare(formula, i, e, 0, e.Length, StringComparison.OrdinalIgnoreCase) == 0) is not { } error)
                    return Unreadable(out problem);
                tokens.Add(new Token(Kind.Error, formula.Substring(i, error.Length)));
                i += error.Length;
                continue;
            }
            if (c is '(' or ')' or ',')
            {
                tokens.Add(new Token(c == '(' ? Kind.Open : c == ')' ? Kind.Close : Kind.Comma, c.ToString()));
                i++;
                continue;
            }
            if (QualifiedRefError().Match(formula, i) is { Success: true } qualifiedError)
            {
                tokens.Add(new Token(Kind.Error, qualifiedError.Value) { Sheet = SheetOf(qualifiedError) });
                i += qualifiedError.Length;
                continue;
            }
            if (SheetSpanPattern().Match(formula, i) is { Success: true } span
                && ReferencePattern().Match(formula, i + span.Length) is { Success: true } place && !place.Groups["sheet"].Success && IsReference(place))
            {
                tokens.Add(new Token(Kind.Reference, span.Value + place.Value)
                {
                    Sheet = span.Groups["first"].Value.Replace("''", "'", StringComparison.Ordinal),
                    LastSheet = span.Groups["last"].Value.Replace("''", "'", StringComparison.Ordinal),
                });
                i += span.Length + place.Length;
                continue;
            }
            if (ReferencePattern().Match(formula, i) is { Success: true } reference && IsReference(reference))
            {
                var spilled = reference.Groups["c1"].Success && !reference.Groups["c2"].Success && i + reference.Length < formula.Length && formula[i + reference.Length] == '#';
                tokens.Add(new Token(Kind.Reference, reference.Value) { Spilled = spilled, Sheet = SheetOf(reference) });
                i += reference.Length + (spilled ? 1 : 0);
                continue;
            }
            if (char.IsAsciiDigit(c) || c == '.')
            {
                var number = NumberPattern().Match(formula, i);
                if (!number.Success) return Unreadable(out problem);
                tokens.Add(new Token(Kind.Number, number.Value));
                i += number.Length;
                continue;
            }
            if (NamePattern().Match(formula, i) is { Success: true } name)
            {
                var end = i + name.Length;
                if (end < formula.Length && formula[end] == '[')
                {
                    if (StructuredColumn(formula, end, out var after, out var at) is not { } column)
                    {
                        if (!at) return Unreadable(out problem);
                        problem = WrittenFormulaProblem.ColumnName;
                        return tokens;
                    }
                    var declared = columnsOf?.Invoke(name.Value);
                    if (declared is null || !declared.Contains(column, StringComparer.OrdinalIgnoreCase))
                    {
                        problem = WrittenFormulaProblem.StructuredReference;
                        return tokens;
                    }
                    if (column[0] == ' ' || column[^1] == ' ' || column.AsSpan().IndexOfAny(UnseenInColumns) >= 0)
                    {
                        problem = WrittenFormulaProblem.ColumnName;
                        return tokens;
                    }
                    tokens.Add(new Token(Kind.StructuredReference, name.Value + StoredColumn(column)));
                    i = after;
                    continue;
                }
                tokens.Add(new Token(Kind.Name, name.Value) { Called = end < formula.Length && formula[end] == '(' });
                i = end;
                continue;
            }
            if (Operators.FirstOrDefault(o => string.CompareOrdinal(formula, i, o, 0, o.Length) == 0) is { } op)
            {
                tokens.Add(new Token(Kind.Operator, op));
                i += op.Length;
                continue;
            }
            return Unreadable(out problem);
        }
        return tokens;
    }

    /// <summary>
    /// The column a structured reference names, read from its <c>[</c> as the engine's lexer reads it — <c>[Column]</c> or
    /// <c>[[Column]]</c>, with <c>'</c> taking the next character as it is — or null where the engine reads none; then
    /// <paramref name="at"/> says whether it met an unescaped <c>@</c>, which the engine reads in no column's name.
    /// </summary>
    private static string? StructuredColumn(string formula, int open, out int after, out bool at)
    {
        after = open;
        at = false;
        var doubled = open + 1 < formula.Length && formula[open + 1] == '[';
        var i = open + (doubled ? 2 : 1);
        var column = new StringBuilder();
        while (true)
        {
            if (i >= formula.Length) return null;
            var c = formula[i];
            if (c == '\'' && i + 1 < formula.Length)
            {
                column.Append(formula[i + 1]);
                i += 2;
                continue;
            }
            if (c is '[' or '#' or '@')
            {
                at = c == '@';
                return null;
            }
            if (c == ']') break;
            column.Append(c);
            i++;
        }
        if (doubled)
        {
            if (i + 1 >= formula.Length || formula[i + 1] != ']') return null;
            i++;
        }
        if (column.Length == 0) return null;
        after = i + 1;
        return column.ToString();
    }

    /// <summary>
    /// The characters of a column's name whose form in a structured reference Excel has not been seen to write: <c>@</c>, and
    /// those Microsoft's documentation of structured references lists as needing the column in double brackets, which single
    /// brackets (TEXT-021, TEXT-022) may not carry as Excel stores them. Ticket 07 asks.
    /// </summary>
    internal const string UnseenInColumns = "@,:.\"{}$^&*+=-<>/\t\n\r";

    /// <summary>
    /// A column of a structured reference as the engine stores it and Excel writes it back (TEXT-021, TEXT-022): in single
    /// brackets, with <c>'</c> before each of <c>[ ] # '</c>.
    /// </summary>
    private static string StoredColumn(string column)
    {
        var text = new StringBuilder(column.Length + 4).Append('[');
        foreach (var c in column)
        {
            if (c is '[' or ']' or '#' or '\'') text.Append('\'');
            text.Append(c);
        }
        return text.Append(']').ToString();
    }

    /// <summary>The Sheet a match's qualifier names, unquoted, or null where it has none.</summary>
    private static string? SheetOf(Match match)
    {
        var group = match.Groups["sheet"];
        if (!group.Success) return null;
        var raw = group.Value;
        return raw[0] == '\'' ? raw[1..^1].Replace("''", "'", StringComparison.Ordinal) : raw;
    }

    private static readonly string[] Operators = ["<>", "<=", ">=", "=", "<", ">", "+", "-", "*", "/", "^", "&", "%"];

    private static List<Token> Unreadable(out WrittenFormulaProblem problem)
    {
        problem = WrittenFormulaProblem.Unreadable;
        return [];
    }

    /// <summary>One call, or one pair of parentheses, as its arguments stand: the indexes of each argument's tokens.</summary>
    private sealed class Frame(Token? function)
    {
        public Token? Function { get; } = function;

        public List<List<int>> Arguments { get; } = [[]];
    }

    /// <summary>
    /// Marks every name a <c>LET</c> binds or a <c>LAMBDA</c> takes, and each use of one where it is bound; or the problem
    /// that refuses the Formula: calls nested or argued beyond Excel's limits, or a bound name called.
    /// </summary>
    private static WrittenFormulaProblem Bind(List<Token> tokens)
    {
        // Each binding: the name, and the tokens it is bound over.
        var bindings = new List<(string Name, int First, int Last)>();
        var frames = new Stack<Frame>();
        frames.Push(new Frame(null));
        var calls = 0;
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            switch (token.Kind)
            {
                case Kind.Open:
                    frames.Peek().Arguments[^1].Add(i);
                    var called = Previous(tokens, i) is { Kind: Kind.Name, Called: true } function ? function : null;
                    if (called is not null && ++calls > MaxDepth) return WrittenFormulaProblem.TooDeep;
                    frames.Push(new Frame(called));
                    break;
                case Kind.Comma when frames.Count > 1:
                    frames.Peek().Arguments.Add([]);
                    if (frames.Peek().Function is not null && frames.Peek().Arguments.Count > MaxArguments) return WrittenFormulaProblem.TooManyArguments;
                    break;
                case Kind.Close when frames.Count > 1:
                    var frame = frames.Pop();
                    if (frame.Function is not null) calls--;
                    frames.Peek().Arguments[^1].Add(i);
                    Binds(frame, tokens, i, bindings);
                    break;
                case Kind.Space:
                    break;
                default:
                    frames.Peek().Arguments[^1].Add(i);
                    break;
            }
        }
        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i] is not { Kind: Kind.Name } name
                || !bindings.Any(b => b.First <= i && i <= b.Last && string.Equals(b.Name, name.Text, StringComparison.OrdinalIgnoreCase)))
                continue;
            // A call is of a function wherever the engine declares one of its name, bound or not, as the engine's parser
            // takes it; a bound name called otherwise is a call the engine and Excel read differently.
            if (!name.Called)
                name.Bound = true;
            else if (DeclaredFunction.Find(name.Text) is null)
                return WrittenFormulaProblem.BoundCall;
        }
        return WrittenFormulaProblem.None;
    }

    /// <summary>What a closed <c>LET</c> or <c>LAMBDA</c> call binds: a <c>LET</c>'s names over what follows each in the call, and a <c>LAMBDA</c>'s parameters over its last argument.</summary>
    private static void Binds(Frame frame, List<Token> tokens, int close, List<(string Name, int First, int Last)> bindings)
    {
        if (frame.Function is not { } function) return;
        var arguments = frame.Arguments;
        if (function.Text.Equals("LET", StringComparison.OrdinalIgnoreCase))
        {
            for (var k = 0; k + 1 < arguments.Count; k += 2)
            {
                if (arguments[k] is [var at] && tokens[at] is { Kind: Kind.Name, Called: false } name)
                    bindings.Add((name.Text, at, close));
            }
        }
        else if (function.Text.Equals("LAMBDA", StringComparison.OrdinalIgnoreCase) && arguments.Count > 0 && arguments[^1].Count > 0)
        {
            var body = arguments[^1];
            for (var k = 0; k + 1 < arguments.Count; k++)
            {
                if (arguments[k] is [var at] && tokens[at] is { Kind: Kind.Name, Called: false } name)
                {
                    bindings.Add((name.Text, at, at));
                    bindings.Add((name.Text, body[0], body[^1]));
                }
            }
        }
    }

    private static Token? Previous(List<Token> tokens, int i) => i > 0 ? tokens[i - 1] : null;

    /// <summary>
    /// Whether the names left unbound here are the ones the engine found unbound: what it parsed is what is written. A name of
    /// another script is one the engine takes only where a <c>LET</c> binds it, and lists in neither.
    /// </summary>
    private static bool SameUnbound(List<Token> tokens, IReadOnlyList<string> engine)
    {
        var unbound = tokens
            .Where(t => t is { Kind: Kind.Name, Called: false, Bound: false } && !IsBoolean(t.Text) && t.Text.All(char.IsAscii))
            .Select(t => t.Text)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return unbound.SetEquals(engine);
    }

    private static bool IsBoolean(string name) => name.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || name.Equals("FALSE", StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether a match of <see cref="ReferencePattern"/> is a Reference the engine reads: no column past <c>XFD</c>, no row past the Sheet's last.</summary>
    private static bool IsReference(Match match)
    {
        static bool Column(Group g) => !g.Success || CellAddress.TryParseColumn(g.Value.TrimStart('$'), out _);
        static bool Row(Group g)
        {
            if (!g.Success) return true;
            var digits = g.Value.TrimStart('$');
            return digits[0] != '0' && int.TryParse(digits, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n) && n <= Sheet.RowCount;
        }
        var g = match.Groups;
        return Column(g["c1"]) && Column(g["c2"]) && Column(g["cc1"]) && Column(g["cc2"]) && Row(g["r1"]) && Row(g["r2"]) && Row(g["rr1"]) && Row(g["rr2"]);
    }

    // The engine's own grammar (ExSheet.Engine's Lexer), which the invariant text it holds follows.

    [GeneratedRegex(
        """
        \G(?:(?<sheet>'(?:[^']|'')+'|[A-Za-z_][A-Za-z0-9_.]*)!)?
        (?:
            (?<c1>\$?[A-Za-z]{1,3})(?<r1>\$?[0-9]{1,7})(?::(?<c2>\$?[A-Za-z]{1,3})(?<r2>\$?[0-9]{1,7}))?
          | (?<cc1>\$?[A-Za-z]{1,3}):(?<cc2>\$?[A-Za-z]{1,3})
          | (?<rr1>\$?[0-9]{1,7}):(?<rr2>\$?[0-9]{1,7})
        )
        (?![A-Za-z0-9_.(\[!:$])
        """,
        RegexOptions.IgnorePatternWhitespace | RegexOptions.CultureInvariant)]
    private static partial Regex ReferencePattern();

    [GeneratedRegex(@"\G(?<sheet>'(?:[^']|'')+'|[A-Za-z_][A-Za-z0-9_.]*)!#REF!", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex QualifiedRefError();

    /// <summary>A 3D Reference's span before its <c>!</c>, as the engine's lexer reads one: <c>Sheet1:Sheet3!</c>, <c>'Q1 Sales:Sheet3'!</c>.</summary>
    [GeneratedRegex(
        @"\G(?:'(?<first>(?:[^':]|'')+):(?<last>(?:[^':]|'')+)'|(?<first>[A-Za-z_][A-Za-z0-9_.]*):(?<last>[A-Za-z_][A-Za-z0-9_.]*))!",
        RegexOptions.CultureInvariant)]
    private static partial Regex SheetSpanPattern();

    [GeneratedRegex(@"\G(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?", RegexOptions.CultureInvariant)]
    private static partial Regex NumberPattern();

    [GeneratedRegex(@"\G[\p{L}_\\][\p{L}0-9_.]*", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();
}
