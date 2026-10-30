using System.Collections.Generic;
using System.Text;

namespace ExGrid.Docs.Generator;

/// <summary>
/// TypeScript, JSX included, as a React Example writes it (ADR-0350 section 12), turned into the same <c>tk-</c>
/// classes as the Razor and C# a Blazor Example shows. A small state machine, as the Razor one is: code, an element's
/// tag, and an element's children, each a frame on a stack, so code inside <c>{…}</c> in a tag or among children
/// returns to them at its closing brace. It knows what an Example writes, not the whole language: a <c>&lt;</c> opens
/// an element only in a .tsx file and only where an expression starts, which is where JSX can stand, and a template
/// literal is one string.
/// </summary>
internal sealed class TypeScriptScanner(string s, StringBuilder html, bool jsx)
{
    private static readonly HashSet<string> Keywords = new()
    {
        "abstract", "as", "async", "await", "boolean", "break", "case", "catch", "class", "const", "continue",
        "declare", "default", "delete", "do", "else", "enum", "export", "extends", "false", "finally", "for",
        "from", "function", "get", "if", "implements", "import", "in", "instanceof", "interface", "keyof", "let",
        "new", "null", "number", "of", "private", "protected", "public", "readonly", "return", "satisfies", "set",
        "static", "string", "super", "switch", "this", "throw", "true", "try", "type", "typeof", "undefined",
        "unknown", "never", "void", "while", "yield", "bigint", "symbol", "object", "any",
    };

    private enum Mode { Code, Tag, Children }

    private sealed class Frame(Mode mode, bool root = false)
    {
        public Mode Mode { get; set; } = mode;
        public bool Root { get; } = root;
        public int Depth { get; set; }
    }

    private readonly Stack<Frame> _frames = new();
    private int _i;

    // The last thing code wrote that was not white space or a comment: a word, or one punctuation character. A '<'
    // after it opens an element only where an expression starts.
    private string? _last;

    public void Run()
    {
        _frames.Push(new Frame(Mode.Code, root: true));
        while (_i < s.Length)
        {
            switch (_frames.Peek().Mode)
            {
                case Mode.Code:
                    Code();
                    break;
                case Mode.Tag:
                    Tag();
                    break;
                default:
                    Children();
                    break;
            }
        }
    }

    private void Code()
    {
        var c = s[_i];
        if (At("//"))
        {
            Take("com", LineEnd());
            return;
        }
        if (At("/*"))
        {
            Take("com", BlockCommentEnd());
            return;
        }
        if (c is '\'' or '"' or '`')
        {
            Take("str", StringEnd());
            _last = "\"";
            return;
        }
        if (char.IsDigit(c))
        {
            var end = _i;
            while (end < s.Length && (char.IsLetterOrDigit(s[end]) || s[end] is '_' or '.'))
                end++;
            Take("num", end);
            _last = "0";
            return;
        }
        if (IsWordStart(c))
        {
            var end = WordEnd(_i);
            var word = s.Substring(_i, end - _i);
            var member = _i > 0 && s[_i - 1] == '.';
            Take(Keywords.Contains(word) && !member ? "kw" : char.IsUpper(word[0]) && !member ? "type" : null, end);
            _last = word;
            return;
        }
        if (c == '<' && ElementMayStart())
        {
            OpenElement();
            return;
        }
        if (char.IsWhiteSpace(c))
        {
            Take(null, _i + 1);
            return;
        }
        var frame = _frames.Peek();
        if (c == '{')
            frame.Depth++;
        else if (c == '}')
        {
            if (frame.Depth == 0 && !frame.Root)
            {
                // The code inside an element's braces ends here: back to its tag or its children.
                _frames.Pop();
                Take(null, _i + 1);
                return;
            }
            frame.Depth--;
        }
        Take(null, _i + 1);
        _last = c.ToString();
    }

    // An element's tag, from its name to its '>' or '/>': attributes, their values, and code in braces.
    private void Tag()
    {
        var c = s[_i];
        if (At("/>"))
        {
            Take("punc", _i + 2);
            _frames.Pop();
            _last = ")";
            return;
        }
        if (c == '>')
        {
            Take("punc", _i + 1);
            _frames.Peek().Mode = Mode.Children;
            return;
        }
        if (c == '{')
        {
            Take(null, _i + 1);
            _frames.Push(new Frame(Mode.Code));
            _last = "{";
            return;
        }
        if (c is '"' or '\'')
        {
            Take("aval", StringEnd());
            return;
        }
        if (c == '=')
        {
            Take("punc", _i + 1);
            return;
        }
        if (IsWordStart(c))
        {
            Take("attr", NameEnd(_i));
            return;
        }
        Take(null, _i + 1);
    }

    // An element's children: text, code in braces, nested elements, and the closing tag that ends them.
    private void Children()
    {
        var c = s[_i];
        if (c == '{')
        {
            Take(null, _i + 1);
            _frames.Push(new Frame(Mode.Code));
            _last = "{";
            return;
        }
        if (At("</"))
        {
            Take("punc", _i + 2);
            var end = NameEnd(_i);
            Take(TagClass(s.Substring(_i, end - _i)), end);
            if (_i < s.Length && s[_i] == '>')
                Take("punc", _i + 1);
            _frames.Pop();
            _last = ")";
            return;
        }
        if (c == '<' && _i + 1 < s.Length && (IsWordStart(s[_i + 1]) || s[_i + 1] == '>'))
        {
            OpenElement();
            return;
        }
        var stop = _i + 1;
        while (stop < s.Length && s[stop] is not ('<' or '{'))
            stop++;
        Take(null, stop);
    }

    // '<' and the element's name, or '<>' for a fragment, whose children follow at once.
    private void OpenElement()
    {
        Take("punc", _i + 1);
        if (_i < s.Length && s[_i] == '>')
        {
            Take("punc", _i + 1);
            _frames.Push(new Frame(Mode.Children));
            return;
        }
        var end = NameEnd(_i);
        Take(TagClass(s.Substring(_i, end - _i)), end);
        _frames.Push(new Frame(Mode.Tag));
    }

    // Where an expression starts: at the beginning, after an operator or an opening bracket, after '=>', or after
    // return. After a name or a closing bracket a '<' is a comparison or a type's argument list.
    private bool ElementMayStart()
    {
        if (!jsx || _i + 1 >= s.Length || !(IsWordStart(s[_i + 1]) || s[_i + 1] == '>'))
            return false;
        return _last is null or "(" or "," or "=" or "?" or ":" or "{" or "[" or "&" or "|" or "!" or ">" or ";"
            or "return" or "default";
    }

    private static string TagClass(string name) =>
        name.Length > 0 && (char.IsUpper(name[0]) || name.Contains(".")) ? "comp" : "tag";

    private static bool IsWordStart(char c) => char.IsLetter(c) || c is '_' or '$';

    // A name in code: letters, digits, '_' and '$'.
    private int WordEnd(int from)
    {
        var end = from;
        while (end < s.Length && (char.IsLetterOrDigit(s[end]) || s[end] is '_' or '$'))
            end++;
        return end;
    }

    // An element's or an attribute's name, which may also hold '.', '-' and ':': Theme.Provider, aria-label.
    private int NameEnd(int from)
    {
        var end = from;
        while (end < s.Length && (char.IsLetterOrDigit(s[end]) || s[end] is '_' or '$' or '.' or '-' or ':'))
            end++;
        return end;
    }

    private int LineEnd()
    {
        var end = s.IndexOf('\n', _i);
        return end < 0 ? s.Length : end;
    }

    private int BlockCommentEnd()
    {
        var end = s.IndexOf("*/", _i + 2, System.StringComparison.Ordinal);
        return end < 0 ? s.Length : end + 2;
    }

    // A string's end, past its closing quote; a quote escaped by a backslash does not close it.
    private int StringEnd()
    {
        var quote = s[_i];
        var end = _i + 1;
        while (end < s.Length && s[end] != quote)
            end += s[end] == '\\' ? 2 : 1;
        return end < s.Length ? end + 1 : s.Length;
    }

    private bool At(string text) => string.CompareOrdinal(s, _i, text, 0, text.Length) == 0;

    private void Take(string? cls, int end)
    {
        Highlighter.Span(html, cls, s.Substring(_i, end - _i));
        _i = end;
    }
}
