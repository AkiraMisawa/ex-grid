using System.Globalization;
using System.Text;

namespace ExSheet.Engine.Formulas;

/// <summary>
/// A Formula's stored text (ADR-0047). It keeps the whitespace it was typed with where Excel
/// keeps it, which is before a token: whitespace at the end of the Formula, and before a
/// <c>,</c>, is dropped, as Excel drops it on entry (observed, verification/2026-09-27-windows-excel).
/// Each token is written in the engine's invariant spelling — declared function names,
/// References, booleans and Error Values in upper case, numbers as Excel writes them (at most 15
/// significant digits, written out in full where Excel writes them so: <c>=1E15</c> is stored as
/// <c>=1000000000000000</c> and <c>=.5</c> as <c>=0.5</c>), a structured reference in single brackets, a Reference spanning
/// every row or every column in its whole-column or whole-row form. Rewriting References, on an
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
            // A carriage return with its line feed is kept as the line feed alone, as Excel keeps it.
            if (token.Kind is not (TokenKind.End or TokenKind.Comma)) text.Append(formula[at..token.Position].Replace("\r\n", "\n", StringComparison.Ordinal));
            WriteToken(text, formula, token, i + 1 < tokens.Count ? tokens[i + 1] : null);
            at = token.Position + token.Length;
        }
        return text.ToString();
    }

    /// <summary>
    /// The stored text with every Reference passed through <paramref name="map"/>: the new
    /// Reference, or <see langword="null"/> where the cells it named no longer exist, written
    /// <c>#REF!</c> as Excel writes it, after the Reference's Sheet qualifier when it had one
    /// (<c>Sheet1!#REF!</c>). <paramref name="requalify"/>, when given, maps the qualifier of a
    /// <c>#REF!</c> already written so (a rename). The same string when nothing changed.
    /// </summary>
    public static string RewriteReferences(string stored, Func<Reference, Reference?> map, Func<string, string>? requalify = null)
    {
        var tokens = Lexer.Tokenize(stored, 1);
        StringBuilder? text = null;
        var at = 0;
        foreach (var token in tokens)
        {
            if (token is { Kind: TokenKind.Error, SheetName: { } qualifier } && requalify is not null)
            {
                var renamed = requalify(qualifier);
                if (string.Equals(renamed, qualifier, StringComparison.Ordinal)) continue;
                text ??= new StringBuilder(stored.Length + 8);
                text.Append(stored, at, token.Position - at);
                text.Append(Reference.QuoteSheetName(renamed)).Append('!').Append(ErrorValue.Ref.ToText());
                at = token.Position + token.Length;
                continue;
            }
            if (token.Kind != TokenKind.Reference) continue;
            var mapped = map(token.Reference!);
            if (mapped is not null && mapped == token.Reference) continue;
            text ??= new StringBuilder(stored.Length + 8);
            if (mapped is null)
            {
                // The whitespace before a Reference that becomes #REF! goes with it (observed:
                // "=  A5  *  C1" with row 5 deleted is "=#REF!  *  C1"); its qualifier stays
                // (observed: "=Sheet1!A5*2" is "=Sheet1!#REF!*2").
                var kept = token.Position;
                while (kept > at && Lexer.IsFormulaWhitespace(stored[kept - 1])) kept--;
                text.Append(stored, at, kept - at);
                if (token.Reference!.SheetName is { } sheet) text.Append(Reference.QuoteSheetName(sheet)).Append('!');
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
                // As Excel writes a number constant back (ADR-0047): the form it gives the same number
                // written into text, which the observed cases agree on (ARITH-073..075, TEXT-057/058).
                text.Append(NumberText.Written(token.Number, CultureInfo.InvariantCulture, NumberText.FormulaConstantLongest));
                break;
            case TokenKind.Text:
                text.Append('"').Append(token.Text.Replace("\"", "\"\"", StringComparison.Ordinal)).Append('"');
                break;
            case TokenKind.Error:
                if (token.SheetName is { } sheet) text.Append(Reference.QuoteSheetName(sheet)).Append('!');
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

    /// <summary>
    /// A structured reference into a Linked Table, <c>Table[Column]</c> (ADR-0049), in single
    /// brackets whatever the column's name holds, with <c>'</c> before each of <c>[ ] # '</c>:
    /// Excel writes <c>Positions[[Market Value]]</c> back as <c>Positions[Market Value]</c> and
    /// <c>Positions[[Rate'#]]</c> as <c>Positions[Rate'#]</c> (observed,
    /// verification/2026-09-27-windows-excel). Double brackets are still read.
    /// </summary>
    private static void WriteStructuredReference(StringBuilder text, string table, string column)
    {
        text.Append(table).Append('[');
        foreach (var c in column)
        {
            if (c is '[' or ']' or '#' or '\'') text.Append('\'');
            text.Append(c);
        }
        text.Append(']');
    }
}
