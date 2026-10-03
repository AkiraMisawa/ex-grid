using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ExGrid.Docs.Generator;

/// <summary>
/// Turns Razor and C# source into HTML with a class on every token (ADR-0110). The classes are
/// <c>tk-</c> names the site's stylesheet colours through custom properties, so the generated
/// markup carries no colour. C# is tokenised by Roslyn's lexer; the Razor around it by the state
/// machine below, which knows the transitions an Example uses: directives, <c>@code</c> blocks,
/// control statements, explicit and implicit expressions, comments, and markup.
/// </summary>
internal static class Highlighter
{
    private static readonly HashSet<string> Directives = new()
    {
        "page", "using", "inject", "implements", "inherits", "namespace", "attribute",
        "typeparam", "layout", "rendermode", "preservewhitespace",
    };

    // The contextual keywords an Example writes, coloured as keywords. "value", "field" and the
    // like stay names: they name things far more often than they are keywords.
    private static readonly HashSet<string> ContextualKeywords = new()
    {
        "var", "async", "await", "record", "init", "required", "get", "set", "nameof", "partial",
        "dynamic", "when", "yield", "where", "with", "not", "and", "or", "is", "select",
    };

    private static readonly HashSet<string> Statements = new()
    {
        "if", "foreach", "for", "while", "switch", "do", "try", "lock",
    };

    /// <summary>The HTML for a C# file.</summary>
    public static string CSharp(string source)
    {
        var html = new StringBuilder(source.Length * 2);
        AppendCSharp(html, source);
        return html.ToString();
    }

    /// <summary>The HTML for a Razor file.</summary>
    public static string Razor(string source)
    {
        var html = new StringBuilder(source.Length * 2);
        new RazorScanner(source, html).Run();
        return html.ToString();
    }

    /// <summary>The HTML for a shell snippet: its comments marked, the rest as written.</summary>
    public static string Shell(string source)
    {
        var html = new StringBuilder(source.Length * 2);
        foreach (var line in source.Split('\n'))
        {
            if (html.Length > 0)
                html.Append('\n');
            var hash = line.IndexOf(" #", StringComparison.Ordinal);
            if (line.TrimStart().StartsWith("#", StringComparison.Ordinal))
                Span(html, "com", line);
            else if (hash >= 0)
            {
                Text(html, line.Substring(0, hash));
                Span(html, "com", line.Substring(hash));
            }
            else
                Text(html, line);
        }
        return html.ToString();
    }

    /// <summary>The HTML for a stylesheet: comments, selectors, properties and values.</summary>
    public static string Css(string source)
    {
        var html = new StringBuilder(source.Length * 2);
        var inBlock = false;
        var i = 0;
        while (i < source.Length)
        {
            if (string.CompareOrdinal(source, i, "/*", 0, 2) == 0)
            {
                var end = source.IndexOf("*/", i + 2, StringComparison.Ordinal);
                end = end < 0 ? source.Length : end + 2;
                Span(html, "com", source.Substring(i, end - i));
                i = end;
                continue;
            }
            var c = source[i];
            if (c == '{' || c == '}')
            {
                inBlock = c == '{';
                Span(html, "punc", c.ToString());
                i++;
                continue;
            }
            var stop = i;
            while (stop < source.Length && source[stop] is not ('{' or '}' or ';') && string.CompareOrdinal(source, stop, "/*", 0, 2) != 0)
                stop++;
            var piece = source.Substring(i, stop - i);
            if (!inBlock)
                Span(html, "type", piece);
            else
            {
                var colon = piece.IndexOf(':');
                if (colon < 0)
                    Text(html, piece);
                else
                {
                    var name = piece.Substring(0, colon);
                    var lead = name.Length - name.TrimStart().Length;
                    Text(html, name.Substring(0, lead));
                    Span(html, "attr", name.Substring(lead));
                    Span(html, "punc", ":");
                    Span(html, "aval", piece.Substring(colon + 1));
                }
            }
            if (stop < source.Length && source[stop] == ';')
            {
                Span(html, "punc", ";");
                stop++;
            }
            i = stop;
        }
        return html.ToString();
    }

    internal static void AppendCSharp(StringBuilder html, string source)
    {
        var tokens = SyntaxFactory.ParseTokens(source).ToList();
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            AppendTrivia(html, token.LeadingTrivia);
            Span(html, Classify(tokens, i), token.Text);
            AppendTrivia(html, token.TrailingTrivia);
        }
    }

    private static void AppendTrivia(StringBuilder html, SyntaxTriviaList trivia)
    {
        foreach (var t in trivia)
        {
            var kind = t.Kind();
            var text = t.ToFullString();
            if (kind is SyntaxKind.SingleLineCommentTrivia or SyntaxKind.MultiLineCommentTrivia
                or SyntaxKind.SingleLineDocumentationCommentTrivia or SyntaxKind.MultiLineDocumentationCommentTrivia)
                Span(html, "com", text);
            else if (SyntaxFacts.IsPreprocessorDirective(kind) || kind == SyntaxKind.DisabledTextTrivia)
                Span(html, "pre", text);
            else
                Text(html, text);
        }
    }

    private static string? Classify(List<SyntaxToken> tokens, int i)
    {
        var token = tokens[i];
        var kind = token.Kind();
        if (SyntaxFacts.IsKeywordKind(kind))
            return "kw";
        switch (kind)
        {
            case SyntaxKind.StringLiteralToken:
            case SyntaxKind.CharacterLiteralToken:
            case SyntaxKind.InterpolatedStringToken:
            case SyntaxKind.InterpolatedStringTextToken:
            case SyntaxKind.SingleLineRawStringLiteralToken:
            case SyntaxKind.MultiLineRawStringLiteralToken:
            case SyntaxKind.Utf8StringLiteralToken:
                return "str";
            case SyntaxKind.NumericLiteralToken:
                return "num";
            case SyntaxKind.IdentifierToken:
                break;
            default:
                return null;
        }

        var text = token.ValueText;
        if (ContextualKeywords.Contains(text))
            return IsDeclaredName(tokens, i) ? null : "kw";
        return LooksLikeType(tokens, i) ? "type" : null;
    }

    // "var" in "var x" is a keyword; "get" as a property's name is not.
    private static bool IsDeclaredName(List<SyntaxToken> tokens, int i) =>
        i > 0 && tokens[i - 1].IsKind(SyntaxKind.DotToken);

    // Roslyn's lexer knows no types, so a type is guessed from its shape: a capitalised identifier
    // that is not a member access, standing where a type stands.
    private static bool LooksLikeType(List<SyntaxToken> tokens, int i)
    {
        var text = tokens[i].ValueText;
        if (text.Length == 0 || !char.IsUpper(text[0]))
            return false;
        var previous = i > 0 ? tokens[i - 1].Kind() : SyntaxKind.None;
        if (previous == SyntaxKind.DotToken)
            return false;
        var next = i + 1 < tokens.Count ? tokens[i + 1].Kind() : SyntaxKind.None;
        if (previous is SyntaxKind.NewKeyword or SyntaxKind.LessThanToken or SyntaxKind.ColonToken
            or SyntaxKind.TypeOfKeyword or SyntaxKind.IsKeyword or SyntaxKind.AsKeyword)
            return true;
        if (previous == SyntaxKind.OpenParenToken && i > 1 && tokens[i - 2].IsKind(SyntaxKind.TypeOfKeyword))
            return true;
        return next switch
        {
            SyntaxKind.IdentifierToken => true,
            SyntaxKind.LessThanToken => true,
            SyntaxKind.DotToken => true,
            SyntaxKind.QuestionToken => i + 2 < tokens.Count && tokens[i + 2].IsKind(SyntaxKind.IdentifierToken),
            SyntaxKind.OpenBracketToken => i + 2 < tokens.Count && tokens[i + 2].IsKind(SyntaxKind.CloseBracketToken),
            SyntaxKind.GreaterThanToken => previous is SyntaxKind.LessThanToken or SyntaxKind.CommaToken,
            SyntaxKind.CommaToken => previous is SyntaxKind.LessThanToken or SyntaxKind.CommaToken,
            _ => false,
        };
    }

    internal static void Span(StringBuilder html, string? cls, string text)
    {
        if (text.Length == 0)
            return;
        if (cls is null)
        {
            Text(html, text);
            return;
        }
        html.Append("<span class=\"tk-").Append(cls).Append("\">");
        Text(html, text);
        html.Append("</span>");
    }

    internal static void Text(StringBuilder html, string text)
    {
        foreach (var c in text)
        {
            switch (c)
            {
                case '&': html.Append("&amp;"); break;
                case '<': html.Append("&lt;"); break;
                case '>': html.Append("&gt;"); break;
                case '"': html.Append("&quot;"); break;
                default: html.Append(c); break;
            }
        }
    }

    private sealed class RazorScanner(string s, StringBuilder html)
    {
        private int _i;
        private int _textStart;

        public void Run()
        {
            while (_i < s.Length)
            {
                var c = s[_i];
                if (c == '@')
                    Transition();
                else if (c == '<' && At("<!--"))
                    Until("-->", "com");
                else if (c == '<' && _i + 1 < s.Length && (char.IsLetter(s[_i + 1]) || s[_i + 1] == '/'))
                    Tag();
                else if (IsWordStart(c))
                {
                    var word = Word();
                    if (word == "else" && PreviousNonSpace() == '}')
                    {
                        Flush();
                        Span(html, "kw", "else");
                        _textStart = _i + 4;
                    }
                    _i += word.Length;
                }
                else
                    _i++;
            }
            Flush();
        }

        private void Transition()
        {
            if (At("@@"))
            {
                _i += 2;
                return;
            }
            Flush();
            if (At("@*"))
            {
                Until("*@", "com");
                return;
            }
            var after = _i + 1 < s.Length ? s[_i + 1] : '\0';
            if (after == '(' || after == '{')
            {
                Span(html, "razor", "@");
                _i++;
                CSharpBalanced();
                return;
            }
            if (!IsWordStart(after))
            {
                _i++;
                _textStart = _i - 1;
                return;
            }
            _i++;
            var word = Word();
            if (word is "code" or "functions")
            {
                Span(html, "razor", "@" + word);
                _i += word.Length;
                var brace = s.IndexOf('{', _i);
                if (brace < 0)
                    return;
                Text(html, s.Substring(_i, brace - _i));
                _i = brace;
                CSharpBalanced();
                return;
            }
            if (Directives.Contains(word) && AtLineStart(_i - 1))
            {
                Span(html, "razor", "@" + word);
                _i += word.Length;
                var end = s.IndexOf('\n', _i);
                if (end < 0)
                    end = s.Length;
                AppendCSharp(html, s.Substring(_i, end - _i));
                _i = end;
                _textStart = _i;
                return;
            }
            if (Statements.Contains(word))
            {
                Span(html, "razor", "@");
                Span(html, "kw", word);
                _i += word.Length;
                var j = _i;
                while (j < s.Length && (s[j] == ' ' || s[j] == '\t'))
                    j++;
                if (j < s.Length && s[j] == '(')
                {
                    Text(html, s.Substring(_i, j - _i));
                    _i = j;
                    CSharpBalanced();
                }
                _textStart = _i;
                return;
            }
            // An implicit expression: a name, then members, calls and indexers.
            Span(html, "razor", "@");
            var start = _i;
            while (_i < s.Length)
            {
                if (IsWordStart(s[_i]))
                    _i += Word().Length;
                else if (s[_i] == '.' && _i + 1 < s.Length && IsWordStart(s[_i + 1]))
                    _i++;
                else if (s[_i] == '(' || s[_i] == '[')
                    _i = SkipBalanced(_i);
                else
                    break;
            }
            AppendCSharp(html, s.Substring(start, _i - start));
            _textStart = _i;
        }

        private void Tag()
        {
            Flush();
            var close = s[_i + 1] == '/';
            Span(html, "punc", close ? "</" : "<");
            _i += close ? 2 : 1;
            var nameStart = _i;
            while (_i < s.Length && (char.IsLetterOrDigit(s[_i]) || s[_i] is '.' or '-' or ':' or '_'))
                _i++;
            var name = s.Substring(nameStart, _i - nameStart);
            Span(html, name.Length > 0 && (char.IsUpper(name[0]) || name.Contains('.')) ? "comp" : "tag", name);
            while (_i < s.Length)
            {
                var c = s[_i];
                if (c == '>')
                {
                    Span(html, "punc", ">");
                    _i++;
                    break;
                }
                if (c == '/' && At("/>"))
                {
                    Span(html, "punc", "/>");
                    _i += 2;
                    break;
                }
                if (char.IsWhiteSpace(c))
                {
                    html.Append(c);
                    _i++;
                    continue;
                }
                if (c == '=')
                {
                    Span(html, "punc", "=");
                    _i++;
                    if (_i < s.Length && (s[_i] == '"' || s[_i] == '\''))
                        AttributeValue();
                    continue;
                }
                var attrStart = _i;
                while (_i < s.Length && !char.IsWhiteSpace(s[_i]) && s[_i] is not ('=' or '>') && !At("/>"))
                    _i++;
                if (_i == attrStart)
                {
                    Text(html, c.ToString());
                    _i++;
                    continue;
                }
                var attribute = s.Substring(attrStart, _i - attrStart);
                Span(html, attribute.StartsWith("@") ? "razor" : "attr", attribute);
            }
            _textStart = _i;
        }

        private void AttributeValue()
        {
            var quote = s[_i];
            var end = s.IndexOf(quote, _i + 1);
            if (end < 0)
                end = s.Length - 1;
            var inner = s.Substring(_i + 1, end - _i - 1);
            Span(html, "aval", quote.ToString());
            if (inner.StartsWith("@"))
            {
                Span(html, "razor", "@");
                AppendCSharp(html, inner.Substring(1));
            }
            else
                Span(html, "aval", inner);
            Span(html, "aval", quote.ToString());
            _i = end + 1;
        }

        // C# from an opening bracket to its partner, highlighted as one piece.
        private void CSharpBalanced()
        {
            var end = SkipBalanced(_i);
            AppendCSharp(html, s.Substring(_i, end - _i));
            _i = end;
            _textStart = _i;
        }

        // The index after the bracket that closes the one at start, past strings and comments.
        private int SkipBalanced(int start)
        {
            var depth = 0;
            var i = start;
            while (i < s.Length)
            {
                var c = s[i];
                if (c is '(' or '[' or '{')
                    depth++;
                else if (c is ')' or ']' or '}')
                {
                    depth--;
                    if (depth == 0)
                        return i + 1;
                }
                else if (c == '"')
                {
                    var verbatim = i > 0 && s[i - 1] == '@';
                    i++;
                    while (i < s.Length && s[i] != '"')
                    {
                        if (s[i] == '\\' && !verbatim)
                            i++;
                        i++;
                    }
                }
                else if (c == '\'')
                {
                    i++;
                    while (i < s.Length && s[i] != '\'')
                    {
                        if (s[i] == '\\')
                            i++;
                        i++;
                    }
                }
                else if (c == '/' && i + 1 < s.Length && s[i + 1] == '/')
                {
                    while (i < s.Length && s[i] != '\n')
                        i++;
                }
                else if (c == '/' && i + 1 < s.Length && s[i + 1] == '*')
                {
                    var close = s.IndexOf("*/", i + 2, System.StringComparison.Ordinal);
                    i = close < 0 ? s.Length : close + 1;
                }
                i++;
            }
            return s.Length;
        }

        private void Until(string terminator, string cls)
        {
            Flush();
            var end = s.IndexOf(terminator, _i + 2, System.StringComparison.Ordinal);
            end = end < 0 ? s.Length : end + terminator.Length;
            Span(html, cls, s.Substring(_i, end - _i));
            _i = end;
            _textStart = _i;
        }

        private void Flush()
        {
            if (_i > _textStart)
                Text(html, s.Substring(_textStart, _i - _textStart));
            _textStart = _i;
        }

        private string Word()
        {
            var j = _i;
            while (j < s.Length && (char.IsLetterOrDigit(s[j]) || s[j] == '_'))
                j++;
            return s.Substring(_i, j - _i);
        }

        private bool At(string text) => string.CompareOrdinal(s, _i, text, 0, text.Length) == 0;

        private bool AtLineStart(int at)
        {
            for (var j = at - 1; j >= 0 && s[j] != '\n'; j--)
                if (!char.IsWhiteSpace(s[j]))
                    return false;
            return true;
        }

        private char PreviousNonSpace()
        {
            for (var j = _i - 1; j >= 0; j--)
                if (!char.IsWhiteSpace(s[j]))
                    return s[j];
            return '\0';
        }

        private static bool IsWordStart(char c) => char.IsLetter(c) || c == '_';
    }
}
