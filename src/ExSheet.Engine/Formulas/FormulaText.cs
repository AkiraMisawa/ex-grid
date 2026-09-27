using System.Globalization;
using System.Text;

namespace ExSheet.Engine.Formulas;

/// <summary>
/// A Formula's stored text (ADR-0047). It keeps the whitespace it was typed with where Excel
/// keeps it, which is before a token: whitespace at the end of the Formula, and before a
/// <c>,</c>, is dropped, as Excel drops it on entry (observed, verification/2026-09-27-windows-excel).
/// Each token is written in the engine's invariant spelling — declared function names,
/// References, booleans and Error Values in upper case, numbers as the shortest text that reads
/// back as the same double. Rewriting References, on an
/// insertion, a deletion, a copy or a fill, replaces only the Reference tokens and leaves every
/// other character where it was — except that a Reference rewritten to <c>#REF!</c> takes the
/// whitespace before it with it, as Excel's does.
/// </summary>
internal static class FormulaText
{
    /// <summary>The stored text of a Formula already read into <paramref name="tokens"/>.</summary>
    public static string Normalize(string formula, IReadOnlyList<Token> tokens)
    {
        var text = new StringBuilder("=");
        var at = 1;
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            // Excel keeps whitespace only where a token follows it; the end is not one, nor a comma.
            if (token.Kind is not (TokenKind.End or TokenKind.Comma)) text.Append(formula, at, token.Position - at);
            WriteToken(text, formula, token, i + 1 < tokens.Count ? tokens[i + 1] : null);
            at = token.Position + token.Length;
        }
        return text.ToString();
    }

    /// <summary>
    /// The stored text with every Reference passed through <paramref name="map"/>: the new
    /// Reference, or <see langword="null"/> where the cells it named no longer exist, written
    /// <c>#REF!</c> as Excel writes it. The same string when no Reference changed.
    /// </summary>
    public static string RewriteReferences(string stored, Func<Reference, Reference?> map)
    {
        var tokens = Lexer.Tokenize(stored, 1);
        StringBuilder? text = null;
        var at = 0;
        foreach (var token in tokens)
        {
            if (token.Kind != TokenKind.Reference) continue;
            var mapped = map(token.Reference!);
            if (mapped is not null && mapped == token.Reference) continue;
            text ??= new StringBuilder(stored.Length + 8);
            if (mapped is null)
            {
                // The whitespace before a Reference that becomes #REF! goes with it (observed:
                // "=  A5  *  C1" with row 5 deleted is "=#REF!  *  C1").
                var kept = token.Position;
                while (kept > at && Lexer.IsFormulaWhitespace(stored[kept - 1])) kept--;
                text.Append(stored, at, kept - at);
                text.Append(ErrorValue.Ref.ToText());
            }
            else
            {
                text.Append(stored, at, token.Position - at);
                mapped.WriteTo(text);
            }
            at = token.Position + token.Length;
        }
        if (text is null) return stored;
        text.Append(stored, at, stored.Length - at);
        return text.ToString();
    }

    private static void WriteToken(StringBuilder text, string formula, Token token, Token? next)
    {
        switch (token.Kind)
        {
            case TokenKind.Number:
                text.Append(token.Number.ToString("R", CultureInfo.InvariantCulture));
                break;
            case TokenKind.Text:
                text.Append('"').Append(token.Text.Replace("\"", "\"\"", StringComparison.Ordinal)).Append('"');
                break;
            case TokenKind.Error:
                text.Append(token.Error.ToText());
                break;
            case TokenKind.Reference:
                token.Reference!.WriteTo(text);
                break;
            case TokenKind.StructuredReference:
                WriteStructuredReference(text, token.TableName!, token.Text);
                break;
            case TokenKind.Name:
                var call = next is { Kind: TokenKind.LeftParenthesis, AfterSpace: false };
                if (call) text.Append(FunctionLibrary.Find(token.Text)?.Name ?? token.Text);
                else if (token.Text.Equals("TRUE", StringComparison.OrdinalIgnoreCase)) text.Append("TRUE");
                else if (token.Text.Equals("FALSE", StringComparison.OrdinalIgnoreCase)) text.Append("FALSE");
                else text.Append(token.Text);
                break;
            case TokenKind.End:
                break;
            default:
                text.Append(formula, token.Position, token.Length);
                break;
        }
    }

    // The characters Microsoft's documentation lists as needing the column in double brackets.
    private const string Special = " \t\n\r,:.[]#'\"{}$^&*+=-></";

    /// <summary>
    /// A structured reference into a Linked Table, <c>Table[Column]</c> (ADR-0049): single brackets
    /// when the column name is plain, double brackets when it holds a character Excel's structured
    /// references treat as special.
    /// </summary>
    private static void WriteStructuredReference(StringBuilder text, string table, string column)
    {
        text.Append(table).Append('[');
        var doubled = column.AsSpan().IndexOfAny(Special) >= 0;
        if (doubled) text.Append('[');
        foreach (var c in column)
        {
            if (c is '[' or ']' or '#' or '\'') text.Append('\'');
            text.Append(c);
        }
        if (doubled) text.Append(']');
        text.Append(']');
    }
}
