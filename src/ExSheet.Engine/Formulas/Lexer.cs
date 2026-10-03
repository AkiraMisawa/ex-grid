using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ExSheet.Engine.Formulas;

internal enum TokenKind
{
    Number,
    Text,
    Error,
    Reference,
    Name,
    StructuredReference,
    Operator,
    LeftParenthesis,
    RightParenthesis,
    Comma,
    End,
}

internal sealed record Token(TokenKind Kind, int Position, string Text, bool AfterSpace)
{
    /// <summary>How many characters of the Formula the token spans, from <see cref="Position"/>.</summary>
    public int Length { get; init; }

    public double Number { get; init; }

    public Reference? Reference { get; init; }

    public ErrorValue Error { get; init; }

    /// <summary>For a structured reference: the table's name; <see cref="Text"/> is the column's.</summary>
    public string? TableName { get; init; }

    /// <summary>For <c>#REF!</c> written after a Sheet qualifier (<c>Sheet1!#REF!</c>): the Sheet's name, unquoted.</summary>
    public string? SheetName { get; init; }
}

/// <summary>
/// Splits a Formula's invariant text into tokens: Excel's spelling, with <c>,</c> between
/// arguments and <c>.</c> as the decimal separator in every culture (ADR-0047).
/// </summary>
internal static partial class Lexer
{
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

    /// <summary><c>Sheet1!#REF!</c>: a qualified Reference whose cells were deleted, as Excel writes it.</summary>
    [GeneratedRegex(@"\G(?<sheet>'(?:[^']|'')+'|[A-Za-z_][A-Za-z0-9_.]*)!#REF!", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex QualifiedRefErrorPattern();

    [GeneratedRegex(@"\G(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?", RegexOptions.CultureInvariant)]
    private static partial Regex NumberPattern();

    [GeneratedRegex(@"\G[A-Za-z_\\][A-Za-z0-9_.]*", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();

    private static readonly string[] Operators = ["<>", "<=", ">=", "=", "<", ">", "+", "-", "*", "/", "^", "&", "%"];

    /// <summary>The largest number constant a Formula may hold: 9.99999999999999E+307, as Microsoft documents Excel's.</summary>
    private const double LargestConstant = 9.99999999999999E+307;

    /// <summary>
    /// The whitespace a Formula may hold between its tokens: a space and a line break. Excel
    /// refuses a tab (observed, verification/2026-09-27-windows-excel); a carriage return is taken
    /// with its line feed, and the pair is stored as the line feed alone (observed with real keys,
    /// verification/2026-09-27-windows-excel-2); the other space characters are refused (inferred).
    /// </summary>
    public static bool IsFormulaWhitespace(char c) => c is ' ' or '\n' or '\r';

    public static List<Token> Tokenize(string formula, int start)
    {
        var tokens = new List<Token>();
        var i = start;
        while (true)
        {
            var afterSpace = false;
            while (i < formula.Length && char.IsWhiteSpace(formula[i]))
            {
                if (!IsFormulaWhitespace(formula[i]))
                {
                    throw new FormulaSyntaxException(formula, i, "a Formula's whitespace is spaces and line breaks; Excel refuses a tab or any other space character.");
                }
                i++;
                afterSpace = true;
            }
            if (i >= formula.Length)
            {
                tokens.Add(new Token(TokenKind.End, i, "", afterSpace));
                return tokens;
            }

            var c = formula[i];
            if (c == '"')
            {
                var text = new StringBuilder();
                var j = i + 1;
                while (true)
                {
                    if (j >= formula.Length) throw new FormulaSyntaxException(formula, i, "the text has no closing quotation mark.");
                    if (formula[j] == '"')
                    {
                        if (j + 1 < formula.Length && formula[j + 1] == '"')
                        {
                            text.Append('"');
                            j += 2;
                            continue;
                        }
                        break;
                    }
                    text.Append(formula[j]);
                    j++;
                }
                tokens.Add(new Token(TokenKind.Text, i, text.ToString(), afterSpace) { Length = j + 1 - i });
                i = j + 1;
                continue;
            }

            if (c == '#')
            {
                var matched = false;
                for (var e = ErrorValue.Null; e <= ErrorValue.NA; e++)
                {
                    var spelled = e.ToText();
                    if (string.Compare(formula, i, spelled, 0, spelled.Length, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        tokens.Add(new Token(TokenKind.Error, i, spelled, afterSpace) { Error = e, Length = spelled.Length });
                        i += spelled.Length;
                        matched = true;
                        break;
                    }
                }
                if (!matched) throw new FormulaSyntaxException(formula, i, "this is not one of Excel's Error Values.");
                continue;
            }

            if (c == '(' || c == ')' || c == ',')
            {
                tokens.Add(new Token(c == '(' ? TokenKind.LeftParenthesis : c == ')' ? TokenKind.RightParenthesis : TokenKind.Comma, i, c.ToString(), afterSpace) { Length = 1 });
                i++;
                continue;
            }

            var qualifiedError = QualifiedRefErrorPattern().Match(formula, i);
            if (qualifiedError.Success)
            {
                var raw = qualifiedError.Groups["sheet"].Value;
                var sheet = raw[0] == '\'' ? raw[1..^1].Replace("''", "'", StringComparison.Ordinal) : raw;
                tokens.Add(new Token(TokenKind.Error, i, qualifiedError.Value, afterSpace) { Error = ErrorValue.Ref, SheetName = sheet, Length = qualifiedError.Length });
                i += qualifiedError.Length;
                continue;
            }

            var reference = ReferencePattern().Match(formula, i);
            if (reference.Success && TryReadReference(reference) is { } parsed)
            {
                // A1#: the Spill Range of the Formula in A1, written after one cell only (ADR-0125).
                var length = reference.Length;
                if (parsed.Shape == ReferenceShape.Cell && i + length < formula.Length && formula[i + length] == '#')
                {
                    parsed = parsed with { Spilled = true };
                    length++;
                }
                tokens.Add(new Token(TokenKind.Reference, i, formula.Substring(i, length), afterSpace) { Reference = parsed, Length = length });
                i += length;
                continue;
            }

            if (char.IsAsciiDigit(c) || c == '.')
            {
                var number = NumberPattern().Match(formula, i);
                if (!number.Success) throw new FormulaSyntaxException(formula, i, "this is not a number.");
                var value = ConstantParser.ParseFormulaNumber(number.Value);
                // Excel takes no number constant above 9.99999999999999E+307, its documented largest:
                // =1E308 is refused through COM and, typed, offered as the typo =E1308, which
                // ExSheet does not correct a Formula into (ADR-0047, second run).
                if (!double.IsFinite(value) || value > LargestConstant) throw new FormulaSyntaxException(formula, i, "the number is too large.");
                tokens.Add(new Token(TokenKind.Number, i, number.Value, afterSpace) { Number = value, Length = number.Length });
                i += number.Length;
                continue;
            }

            var name = NamePattern().Match(formula, i);
            if (name.Success)
            {
                var end = i + name.Length;
                if (end < formula.Length && formula[end] == '[')
                {
                    var column = ReadStructuredColumn(formula, end, out var after);
                    tokens.Add(new Token(TokenKind.StructuredReference, i, column, afterSpace) { TableName = name.Value, Length = after - i });
                    i = after;
                    continue;
                }
                tokens.Add(new Token(TokenKind.Name, i, name.Value, afterSpace) { Length = name.Length });
                i = end;
                continue;
            }

            var op = Array.Find(Operators, o => string.CompareOrdinal(formula, i, o, 0, o.Length) == 0);
            if (op is not null)
            {
                tokens.Add(new Token(TokenKind.Operator, i, op, afterSpace) { Length = op.Length });
                i += op.Length;
                continue;
            }

            throw new FormulaSyntaxException(formula, i, $"'{c}' is not part of the formula syntax ExSheet reads.");
        }
    }

    /// <summary>
    /// Reads <c>[Column]</c> or <c>[[Column]]</c>, with <c>'</c> escaping the next character, as
    /// Excel's structured references do. Other structured forms (<c>#All</c>, <c>@</c>, column
    /// ranges) are not read.
    /// </summary>
    private static string ReadStructuredColumn(string formula, int open, out int after)
    {
        var doubled = open + 1 < formula.Length && formula[open + 1] == '[';
        var i = open + (doubled ? 2 : 1);
        var column = new StringBuilder();
        while (true)
        {
            if (i >= formula.Length) throw new FormulaSyntaxException(formula, open, "the structured reference has no closing bracket.");
            var c = formula[i];
            if (c == '\'' && i + 1 < formula.Length)
            {
                column.Append(formula[i + 1]);
                i += 2;
                continue;
            }
            if (c == '[' || c == '#' || c == '@') throw new FormulaSyntaxException(formula, i, "only a Table[Column] structured reference is read.");
            if (c == ']') break;
            column.Append(c);
            i++;
        }
        if (doubled)
        {
            if (i + 1 >= formula.Length || formula[i + 1] != ']') throw new FormulaSyntaxException(formula, i, "only a Table[Column] structured reference is read.");
            i++;
        }
        if (column.Length == 0) throw new FormulaSyntaxException(formula, open, "the structured reference names no column.");
        after = i + 1;
        return column.ToString();
    }

    /// <summary>
    /// The Reference written at <paramref name="at"/>, as this grammar reads one there, or
    /// <see langword="null"/>: the match's groups say where its Sheet qualifier and each column
    /// and row part stand, <c>$</c> included — <c>c1</c>, <c>r1</c>, <c>c2</c>, <c>r2</c> for a
    /// cell or an area, <c>cc1</c>, <c>cc2</c> for whole columns and <c>rr1</c>, <c>rr2</c> for
    /// whole rows. Nothing that <see cref="Tokenize"/> would not read as a Reference: not a
    /// function name (<c>LOG10(</c>), a structured reference or a column past <c>XFD</c>.
    /// </summary>
    public static Match? MatchReference(string formula, int at)
    {
        var match = ReferencePattern().Match(formula, at);
        return match.Success && TryReadReference(match) is not null ? match : null;
    }

    /// <summary>
    /// The Reference <see cref="MatchReference"/> finds at <paramref name="at"/>, read, and in
    /// <paramref name="length"/> how many characters it spans; <see langword="null"/> where it finds none.
    /// </summary>
    public static Reference? ReadReference(string formula, int at, out int length)
    {
        var match = ReferencePattern().Match(formula, at);
        var reference = match.Success ? TryReadReference(match) : null;
        length = reference is null ? 0 : match.Length;
        return reference;
    }

    /// <summary>
    /// The structured reference written at <paramref name="at"/> — <c>Table[Column]</c> or
    /// <c>Table[[Column]]</c> — as <see cref="Tokenize"/> reads one there: the table's name as
    /// written, the column's with its <c>'</c> escapes read, and how many characters it spans.
    /// <see langword="null"/> where it reads none, or reads a form it refuses (<c>#All</c>, <c>@</c>).
    /// </summary>
    public static (string Table, string Column, int Length)? ReadStructuredReference(string formula, int at)
    {
        var name = NamePattern().Match(formula, at);
        var open = at + name.Length;
        if (!name.Success || open >= formula.Length || formula[open] != '[') return null;
        try
        {
            var column = ReadStructuredColumn(formula, open, out var after);
            return (name.Value, column, after - at);
        }
        catch (FormulaSyntaxException)
        {
            return null;
        }
    }

    private static Reference? TryReadReference(Match match)
    {
        string? sheet = null;
        if (match.Groups["sheet"].Success)
        {
            var raw = match.Groups["sheet"].Value;
            sheet = raw[0] == '\'' ? raw[1..^1].Replace("''", "'", StringComparison.Ordinal) : raw;
        }

        if (match.Groups["c1"].Success)
        {
            if (!TryColumn(match.Groups["c1"].Value, out var c1, out var c1Abs) || !TryRow(match.Groups["r1"].Value, out var r1, out var r1Abs)) return null;
            if (!match.Groups["c2"].Success)
            {
                return new Reference(sheet, ReferenceShape.Cell, r1, c1, r1, c1, r1Abs, c1Abs, r1Abs, c1Abs);
            }
            if (!TryColumn(match.Groups["c2"].Value, out var c2, out var c2Abs) || !TryRow(match.Groups["r2"].Value, out var r2, out var r2Abs)) return null;
            // Excel writes a rectangle from its top-left corner, whichever corner was typed first.
            if (c2 < c1) (c1, c1Abs, c2, c2Abs) = (c2, c2Abs, c1, c1Abs);
            if (r2 < r1) (r1, r1Abs, r2, r2Abs) = (r2, r2Abs, r1, r1Abs);
            return Reference.Rectangle(sheet, r1, c1, r2, c2, r1Abs, c1Abs, r2Abs, c2Abs);
        }
        if (match.Groups["cc1"].Success)
        {
            if (!TryColumn(match.Groups["cc1"].Value, out var c1, out var c1Abs) || !TryColumn(match.Groups["cc2"].Value, out var c2, out var c2Abs)) return null;
            if (c2 < c1) (c1, c1Abs, c2, c2Abs) = (c2, c2Abs, c1, c1Abs);
            // Excel holds a whole column's rows as absolute: A:XFD is written $1:$1048576.
            return Reference.Rectangle(sheet, 0, c1, Sheet.RowCount - 1, c2, true, c1Abs, true, c2Abs);
        }
        {
            if (!TryRow(match.Groups["rr1"].Value, out var r1, out var r1Abs) || !TryRow(match.Groups["rr2"].Value, out var r2, out var r2Abs)) return null;
            if (r2 < r1) (r1, r1Abs, r2, r2Abs) = (r2, r2Abs, r1, r1Abs);
            return Reference.Rectangle(sheet, r1, 0, r2, Sheet.ColumnCount - 1, r1Abs, true, r2Abs, true);
        }
    }

    private static bool TryColumn(string text, out int column, out bool absolute)
    {
        absolute = text[0] == '$';
        return CellAddress.TryParseColumn(absolute ? text[1..] : text, out column);
    }

    private static bool TryRow(string text, out int row, out bool absolute)
    {
        absolute = text[0] == '$';
        var digits = absolute ? text[1..] : text;
        row = -1;
        if (digits[0] == '0' || !int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n > Sheet.RowCount) return false;
        row = n - 1;
        return true;
    }
}
