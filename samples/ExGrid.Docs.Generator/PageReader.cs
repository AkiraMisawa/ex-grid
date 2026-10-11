using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;

namespace ExGrid.Docs.Generator;

/// <summary>What the site map holds of one routed page (ADR-0110).</summary>
/// <param name="Path">The file, from the site's folder: <c>Pages/Grid/GridOverview.razor</c>.</param>
/// <param name="Routes">Its <c>@page</c> routes, without the leading slash: <c>exgrid</c>; the start page is empty.</param>
/// <param name="Product">The <c>DocsPage</c>'s Product, when it names one.</param>
/// <param name="Title">The <c>DocsPage</c>'s Title, or a Showcase's; <see langword="null"/> when the page has neither.</param>
/// <param name="Lead">The <c>DocsPage</c>'s Lead, when it has one.</param>
/// <param name="Sections">Its <c>DocsSection</c>s, in the order they appear.</param>
/// <param name="Line">The line of its first route, 1-based.</param>
public sealed record SitePage(
    string Path,
    ImmutableArray<string> Routes,
    string? Product,
    string? Title,
    string? Lead,
    ImmutableArray<SiteSection> Sections,
    int Line);

/// <summary>A section of a page, as the site map holds it.</summary>
/// <param name="Title">Its heading.</param>
/// <param name="Anchor">The anchor the site renders for it (<see cref="DocsAnchor"/>).</param>
/// <param name="Examples">The Examples inside it.</param>
/// <param name="Types">The types its API tables list, as the page names them: <c>ExGrid&lt;&gt;</c>, <c>ExSheet.Engine.Sheet</c>.</param>
/// <param name="Line">The line it opens on, 1-based.</param>
public sealed record SiteSection(
    string Title,
    string Anchor,
    ImmutableArray<SiteExample> Examples,
    ImmutableArray<string> Types,
    int Line);

/// <summary>An Example, as the site map holds it.</summary>
/// <param name="Title">Its heading, when it has one.</param>
/// <param name="HasMud">Whether it has a MudBlazor twin, so that it runs under either Chrome.</param>
/// <param name="Line">The line it opens on, 1-based.</param>
public sealed record SiteExample(string? Title, bool HasMud, int Line);

/// <summary>What is wrong with a page, for the site map's purposes.</summary>
public enum PageProblemKind
{
    /// <summary>A title the site map reads is not text it can read: an expression, a character reference, or nothing.</summary>
    TitleNotLiteral,

    /// <summary>Two sections of one page have one anchor.</summary>
    DuplicateAnchor,

    /// <summary>The page's markup cannot be read: a tag, an attribute or a comment never closed, or a section closed twice.</summary>
    Unreadable,
}

/// <summary>A problem found while reading a page, with where it is.</summary>
/// <param name="Kind">What is wrong.</param>
/// <param name="Message">What is wrong and how to fix it, in a sentence or two.</param>
/// <param name="Start">The offset in the file where it is.</param>
/// <param name="Length">How many characters it covers.</param>
/// <param name="Line">Its line, 1-based.</param>
/// <param name="Column">Its column, 1-based.</param>
public sealed record PageProblem(PageProblemKind Kind, string Message, int Start, int Length, int Line, int Column);

/// <summary>A page as read: what the site map takes from it, and what stops it.</summary>
/// <param name="Page">The page; its routes are empty when the file declares none.</param>
/// <param name="RouteSpans">Where each of its routes' <c>@page</c> is written, as offset and length, in the routes' order.</param>
/// <param name="Problems">What is wrong with it, in the order found.</param>
public sealed record PageScan(SitePage Page, ImmutableArray<(int Start, int Length)> RouteSpans, ImmutableArray<PageProblem> Problems);

/// <summary>
/// Reads a <c>.razor</c> file for the site map (ADR-0110): its <c>@page</c> routes, its
/// <c>DocsPage</c>'s Product, Title and Lead, a Showcase's Title, and each <c>DocsSection</c> with
/// its Examples and API tables.
/// </summary>
/// <remarks>
/// It reads the markup, not C#: it skips Razor and HTML comments and <c>@code</c> blocks, and
/// steps over a C# expression inside an attribute (<c>Files="@(["Examples/Data/Trade.cs"])"</c>).
/// A title is read only when it is text written in the file, because only then does the anchor the
/// site map makes equal the one the page renders.
/// </remarks>
public static class PageReader
{
    private static readonly Regex CharacterReference = new(@"&(#[0-9]+|#[xX][0-9a-fA-F]+|[A-Za-z][A-Za-z0-9]*);", RegexOptions.CultureInvariant);

    /// <summary>Reads <paramref name="text"/>, the contents of the file at <paramref name="path"/>.</summary>
    /// <param name="path">The file, from the site's folder, as the site map names it.</param>
    /// <param name="text">The file's contents.</param>
    public static PageScan Read(string path, string text) => new Reader(path, text).Read();

    private sealed class Reader(string path, string text)
    {
        private readonly List<int> _lineStarts = LineStarts(text);
        private readonly List<PageProblem> _problems = [];
        private readonly List<string> _routes = [];
        private readonly List<(int Start, int Length)> _routeSpans = [];
        private readonly List<SectionBuilder> _sections = [];
        private readonly Stack<SectionBuilder> _open = new();
        private int _firstRouteLine;
        private string? _product;
        private string? _title;
        private string? _lead;
        private bool _hasDocsPage;
        private bool _stopped;
        private int _i;

        public PageScan Read()
        {
            while (_i < text.Length)
            {
                if (At("@*"))
                {
                    if (!SkipPast("*@", _i + 2))
                    {
                        Stop(_i, 2, "This Razor comment is never closed, so the rest of the page cannot be read. Close it with *@.");
                        break;
                    }
                    continue;
                }
                if (At("<!--"))
                {
                    if (!SkipPast("-->", _i + 4))
                    {
                        Stop(_i, 4, "This HTML comment is never closed, so the rest of the page cannot be read. Close it with -->.");
                        break;
                    }
                    continue;
                }
                if (text[_i] == '@' && AtLineStart(_i))
                {
                    if (AtWord("@page"))
                    {
                        ReadRoute();
                        continue;
                    }
                    if (AtWord("@code") || AtWord("@functions"))
                    {
                        SkipCodeBlock();
                        continue;
                    }
                }
                if (At("</DocsSection") && !IsNameChar(CharAt(_i + "</DocsSection".Length)))
                {
                    CloseSection();
                    continue;
                }
                if (text[_i] == '<' && IsNameStart(CharAt(_i + 1)))
                {
                    ReadTag();
                    continue;
                }
                _i++;
            }

            // A page read to its end and still holding open sections left them open; one whose reading
            // stopped has said why already.
            foreach (var section in _stopped ? Enumerable.Empty<SectionBuilder>() : _open.Reverse())
            {
                Problem(PageProblemKind.Unreadable, section.Start, "<DocsSection".Length,
                    "This DocsSection is never closed. Close it with </DocsSection>, or write it as <DocsSection Title=\"…\" />.");
            }

            var sections = _sections.Select(s => s.Build()).ToImmutableArray();
            var anchors = new Dictionary<string, SectionBuilder>(StringComparer.Ordinal);
            foreach (var section in _sections)
            {
                if (section.Anchor.Length == 0)
                    continue;
                if (anchors.TryGetValue(section.Anchor, out var first))
                {
                    Problem(PageProblemKind.DuplicateAnchor, section.TitleStart, section.TitleLength,
                        $"This section's anchor is #{section.Anchor}, and so is the section \"{first.Title}\" at line {LineOf(first.Start)}. "
                        + "A link to #" + section.Anchor + " reaches only the first: give one of them another title.");
                }
                else
                {
                    anchors.Add(section.Anchor, section);
                }
            }

            var page = new SitePage(
                path,
                _routes.ToImmutableArray(),
                _product,
                _title,
                _lead,
                sections,
                _firstRouteLine);
            return new PageScan(page, _routeSpans.ToImmutableArray(), _problems.OrderBy(p => p.Start).ToImmutableArray());
        }

        private void ReadRoute()
        {
            var start = _i;
            _i += "@page".Length;
            SkipSpaces();
            if (CharAt(_i) != '"')
            {
                Problem(PageProblemKind.Unreadable, start, "@page".Length,
                    "This @page has no route in quotes. Write it as @page \"/exgrid/columns\".");
                SkipToLineEnd();
                return;
            }
            var close = text.IndexOf('"', _i + 1);
            var lineEnd = text.IndexOf('\n', _i);
            if (close < 0 || (lineEnd >= 0 && close > lineEnd))
            {
                Problem(PageProblemKind.Unreadable, start, "@page".Length,
                    "This @page's route is never closed. Close it with a quote on the same line.");
                SkipToLineEnd();
                return;
            }
            var route = text.Substring(_i + 1, close - _i - 1).Trim();
            if (_routes.Count == 0)
                _firstRouteLine = LineOf(start);
            _routes.Add(route.Trim('/'));
            _routeSpans.Add((start, close + 1 - start));
            _i = close + 1;
        }

        private void ReadTag()
        {
            var start = _i;
            var nameStart = _i + 1;
            var nameEnd = nameStart;
            while (nameEnd < text.Length && IsNameChar(text[nameEnd]))
                nameEnd++;
            var name = text.Substring(nameStart, nameEnd - nameStart);
            if (name is not ("DocsPage" or "DocsSection" or "DocsExample" or "ApiTable" or "ShowcaseFrame"))
            {
                _i = nameEnd;
                return;
            }

            _i = nameEnd;
            var attributes = ReadAttributes(start, name, out var selfClosing);
            if (attributes is null)
                return;

            switch (name)
            {
                case "DocsPage":
                    _hasDocsPage = true;
                    _title = Literal(attributes, "Title", start, name, required: true);
                    _product = Literal(attributes, "Product", start, name, required: false);
                    _lead = Literal(attributes, "Lead", start, name, required: false);
                    break;
                case "ShowcaseFrame":
                    var showcase = Literal(attributes, "Title", start, name, required: true);
                    if (!_hasDocsPage)
                        _title = showcase;
                    break;
                case "DocsSection":
                    var title = Literal(attributes, "Title", start, name, required: true) ?? "";
                    var at = attributes.TryGetValue("Title", out var titleValue) ? titleValue : null;
                    var section = new SectionBuilder(title, start, at?.Start ?? start, at?.Length ?? name.Length + 1, LineOf(start));
                    _sections.Add(section);
                    if (!selfClosing)
                        _open.Push(section);
                    break;
                case "DocsExample":
                    var example = Literal(attributes, "Title", start, name, required: false);
                    if (_open.Count == 0)
                    {
                        Problem(PageProblemKind.Unreadable, start, name.Length + 1,
                            "This Example sits outside every DocsSection, so the site map cannot say where it is. Put it inside the section it belongs to.");
                        break;
                    }
                    _open.Peek().Examples.Add(new SiteExample(example, attributes.ContainsKey("Mud"), LineOf(start)));
                    break;
                case "ApiTable":
                    if (_open.Count == 0)
                    {
                        Problem(PageProblemKind.Unreadable, start, name.Length + 1,
                            "This ApiTable sits outside every DocsSection, so the site map cannot say where it is. Put it inside the page's API section.");
                        break;
                    }
                    if (attributes.TryGetValue("Type", out var type))
                        _open.Peek().Types.Add(TypeName(type.Value));
                    break;
            }
        }

        // The attributes up to the tag's end, by name; null, with a problem, when the tag never ends.
        private Dictionary<string, AttributeValue>? ReadAttributes(int tagStart, string name, out bool selfClosing)
        {
            selfClosing = false;
            var attributes = new Dictionary<string, AttributeValue>(StringComparer.Ordinal);
            while (true)
            {
                SkipWhitespace();
                if (_i >= text.Length)
                {
                    Stop(tagStart, name.Length + 1,
                        $"This <{name}> tag is never closed, so the rest of the page cannot be read. Close it with > or />.");
                    return null;
                }
                if (At("/>"))
                {
                    selfClosing = true;
                    _i += 2;
                    return attributes;
                }
                if (text[_i] == '>')
                {
                    _i++;
                    return attributes;
                }
                if (text[_i] == '<')
                {
                    Stop(tagStart, name.Length + 1,
                        $"This <{name}> tag is never closed before the next tag begins, so the rest of the page cannot be read. Close it with > or />.");
                    return null;
                }

                var attrStart = _i;
                while (_i < text.Length && !char.IsWhiteSpace(text[_i]) && text[_i] is not ('=' or '>' or '"' or '\'')
                       && !At("/>"))
                {
                    _i++;
                }
                var attrName = text.Substring(attrStart, _i - attrStart);
                if (attrName.Length == 0)
                {
                    Problem(PageProblemKind.Unreadable, _i, 1,
                        $"This <{name}> tag has a value with no attribute name before it. Write it as Name=\"value\".");
                    _i++;
                    continue;
                }
                SkipWhitespace();
                if (CharAt(_i) != '=')
                {
                    attributes[attrName] = new AttributeValue("", attrStart, attrName.Length);
                    continue;
                }
                _i++;
                SkipWhitespace();
                var value = ReadValue(name, tagStart);
                if (value is null)
                    return null;
                attributes[attrName] = value;
            }
        }

        private AttributeValue? ReadValue(string name, int tagStart)
        {
            var quote = CharAt(_i);
            if (quote is '"' or '\'')
            {
                var start = _i;
                _i++;
                while (_i < text.Length && text[_i] != quote)
                {
                    if (At("@("))
                    {
                        _i++;
                        if (!CSharp.SkipBalanced(text, ref _i, '(', ')'))
                            return Unclosed(start);
                        continue;
                    }
                    _i++;
                }
                if (_i >= text.Length)
                    return Unclosed(start);
                _i++;
                return new AttributeValue(text.Substring(start + 1, _i - start - 2), start + 1, _i - start - 2);
            }

            var unquoted = _i;
            if (At("@("))
            {
                _i++;
                if (!CSharp.SkipBalanced(text, ref _i, '(', ')'))
                    return Unclosed(unquoted);
            }
            while (_i < text.Length && !char.IsWhiteSpace(text[_i]) && text[_i] != '>' && !At("/>"))
                _i++;
            return new AttributeValue(text.Substring(unquoted, _i - unquoted), unquoted, _i - unquoted);

            AttributeValue? Unclosed(int at)
            {
                Stop(at, 1,
                    $"An attribute of this <{name}> tag (line {LineOf(tagStart)}) is never closed, so the rest of the page cannot be read. Close its value with a matching quote.");
                return null;
            }
        }

        // An attribute the site map reads as text: written in the file, with no Razor in it.
        private string? Literal(Dictionary<string, AttributeValue> attributes, string attribute, int tagStart, string name, bool required)
        {
            if (!attributes.TryGetValue(attribute, out var value))
            {
                if (required)
                {
                    Problem(PageProblemKind.TitleNotLiteral, tagStart, name.Length + 1,
                        $"This <{name}> has no {attribute}, so the site map cannot name it. Give it one: {attribute}=\"…\".");
                }
                return null;
            }
            var raw = value.Value;
            if (HasRazor(raw))
            {
                Problem(PageProblemKind.TitleNotLiteral, value.Start, Math.Max(value.Length, 1),
                    $"This {name}'s {attribute} is a C# expression, so the site map cannot read it at build time. "
                    + $"Write it as text: {attribute}=\"Status-bar figures\".");
                return null;
            }
            if (CharacterReference.Match(raw) is { Success: true } reference)
            {
                Problem(PageProblemKind.TitleNotLiteral, value.Start + reference.Index, reference.Length,
                    $"This {name}'s {attribute} holds the character reference {reference.Value}, which the site map would read as written. "
                    + "Write the character itself.");
                return null;
            }
            var literal = raw.Replace("@@", "@").Trim();
            if (literal.Length == 0 && required)
            {
                Problem(PageProblemKind.TitleNotLiteral, value.Start, Math.Max(value.Length, 1),
                    $"This {name}'s {attribute} is empty, so it has no anchor. Give it a title.");
                return null;
            }
            return literal;
        }

        private static bool HasRazor(string value)
        {
            for (var k = 0; k < value.Length; k++)
            {
                if (value[k] != '@')
                    continue;
                if (k + 1 < value.Length && value[k + 1] == '@')
                {
                    k++;
                    continue;
                }
                return true;
            }
            return false;
        }

        // typeof(global::ExSheet.Engine.Sheet) as ExSheet.Engine.Sheet; anything else as written.
        private static string TypeName(string value)
        {
            var name = value.Trim();
            if (name.StartsWith("@(", StringComparison.Ordinal) && name.EndsWith(")", StringComparison.Ordinal))
                name = name.Substring(2, name.Length - 3).Trim();
            if (name.StartsWith("typeof(", StringComparison.Ordinal) && name.EndsWith(")", StringComparison.Ordinal))
                name = name.Substring("typeof(".Length, name.Length - "typeof(".Length - 1).Trim();
            return name.Replace("global::", "");
        }

        private void CloseSection()
        {
            var start = _i;
            var end = text.IndexOf('>', _i);
            _i = end < 0 ? text.Length : end + 1;
            if (_open.Count == 0)
            {
                Problem(PageProblemKind.Unreadable, start, "</DocsSection".Length,
                    "This </DocsSection> closes a section that was never opened. Remove it, or open the section it ends.");
                return;
            }
            _open.Pop();
        }

        private void SkipCodeBlock()
        {
            var start = _i;
            var brace = text.IndexOf('{', _i);
            if (brace < 0)
            {
                _i = text.Length;
                return;
            }
            _i = brace;
            if (!CSharp.SkipBalanced(text, ref _i, '{', '}'))
            {
                Stop(start, 5, "This code block's braces never balance, so the rest of the page cannot be read. Close the block with }.");
            }
        }

        private bool SkipPast(string end, int from)
        {
            var at = text.IndexOf(end, from, StringComparison.Ordinal);
            if (at < 0)
            {
                return false;
            }
            _i = at + end.Length;
            return true;
        }

        private void SkipToLineEnd()
        {
            var end = text.IndexOf('\n', _i);
            _i = end < 0 ? text.Length : end + 1;
        }

        private void SkipSpaces()
        {
            while (_i < text.Length && text[_i] is ' ' or '\t')
                _i++;
        }

        private void SkipWhitespace()
        {
            while (_i < text.Length && char.IsWhiteSpace(text[_i]))
                _i++;
        }

        private bool At(string s) => string.CompareOrdinal(text, _i, s, 0, s.Length) == 0;

        private bool AtWord(string s) => At(s) && !IsNameChar(CharAt(_i + s.Length));

        private bool AtLineStart(int at)
        {
            for (var k = at - 1; k >= 0; k--)
            {
                if (text[k] == '\n')
                    return true;
                if (text[k] is not (' ' or '\t' or '\r'))
                    return false;
            }
            return true;
        }

        private char CharAt(int at) => at < text.Length ? text[at] : '\0';

        private static bool IsNameStart(char c) => char.IsLetter(c) || c == '_';

        private static bool IsNameChar(char c) => char.IsLetterOrDigit(c) || c is '_' or '.' or ':' or '-';

        private int LineOf(int offset)
        {
            var index = _lineStarts.BinarySearch(offset);
            return (index >= 0 ? index : ~index - 1) + 1;
        }

        // A problem after which nothing more of the page can be read: reading ends there.
        private void Stop(int start, int length, string message)
        {
            Problem(PageProblemKind.Unreadable, start, length, message);
            _stopped = true;
            _i = text.Length;
        }

        private void Problem(PageProblemKind kind, int start, int length, string message)
        {
            var line = LineOf(start);
            _problems.Add(new PageProblem(kind, message, start, length, line, start - _lineStarts[line - 1] + 1));
        }

        private static List<int> LineStarts(string text)
        {
            var starts = new List<int> { 0 };
            for (var k = 0; k < text.Length; k++)
            {
                if (text[k] == '\n')
                    starts.Add(k + 1);
            }
            return starts;
        }

        private sealed class SectionBuilder(string title, int start, int titleStart, int titleLength, int line)
        {
            public string Title { get; } = title;
            public string Anchor { get; } = DocsAnchor.Of(title);
            public int Start { get; } = start;
            public int TitleStart { get; } = titleStart;
            public int TitleLength { get; } = titleLength;
            public List<SiteExample> Examples { get; } = [];
            public List<string> Types { get; } = [];
            public int Line { get; } = line;

            public SiteSection Build() => new(Title, Anchor, Examples.ToImmutableArray(), Types.ToImmutableArray(), Line);
        }

        private sealed record AttributeValue(string Value, int Start, int Length);
    }
}
