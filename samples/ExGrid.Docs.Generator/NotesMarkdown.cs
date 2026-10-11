using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.Text;

namespace ExGrid.Docs.Generator;

/// <summary>What is wrong with a Release Notes file (ADR-0190), and so which error says it.</summary>
public enum NotesProblemKind
{
    /// <summary>A construct the notes' Markdown subset does not have.</summary>
    Subset,

    /// <summary>The file's shape: its first line, its header lines, its headings and what they hold.</summary>
    Shape,

    /// <summary>A link that leads nowhere: an ADR with no file, a route or an anchor the site does not have.</summary>
    Link,

    /// <summary>The version this build publishes is not cut: its file is missing, or a Change Note is left.</summary>
    Cut,
}

/// <summary>A problem in a Release Notes file, with where it is.</summary>
/// <param name="Kind">What is wrong.</param>
/// <param name="Start">The offset in the file where it is.</param>
/// <param name="Length">How many characters it covers.</param>
/// <param name="Message">What is wrong and how to fix it.</param>
public sealed record NotesProblem(NotesProblemKind Kind, int Start, int Length, string Message);

/// <summary>
/// A run of inline text — a paragraph's, or a bullet's — read from one or more lines of a file, with
/// the offset in the file of each of its characters, so that a problem inside it names its line.
/// </summary>
public sealed class NotesText
{
    private readonly List<int> _offsets = [];
    private readonly StringBuilder _text = new();

    /// <summary>The text, its lines joined by <c>\n</c>.</summary>
    public string Text => _text.ToString();

    /// <summary>The offset in the file of the character at <paramref name="index"/>; the end of the text past it.</summary>
    public int OffsetOf(int index) =>
        _offsets.Count == 0 ? 0 : index < _offsets.Count ? _offsets[index] : _offsets[_offsets.Count - 1] + 1;

    /// <summary>Adds a line's text, which starts at <paramref name="offset"/> in the file.</summary>
    public void AddLine(string line, int offset)
    {
        if (_text.Length > 0)
        {
            _offsets.Add(_offsets[_offsets.Count - 1] + 1);
            _text.Append('\n');
        }
        for (var k = 0; k < line.Length; k++)
        {
            _offsets.Add(offset + k);
            _text.Append(line[k]);
        }
    }
}

/// <summary>A block of a Release Notes file.</summary>
/// <param name="Start">The offset in the file where it starts.</param>
/// <param name="Line">Its first line, 1-based.</param>
public abstract record NotesBlock(int Start, int Line);

/// <summary>A <c>##</c> heading.</summary>
/// <param name="Title">Its text.</param>
/// <param name="Start">Where the heading's line starts.</param>
/// <param name="Line">Its line, 1-based.</param>
public sealed record NotesHeading(string Title, int Start, int Line) : NotesBlock(Start, Line);

/// <summary>A paragraph: one or more lines of inline text.</summary>
/// <param name="Text">Its text.</param>
/// <param name="Start">Where it starts.</param>
/// <param name="Line">Its first line, 1-based.</param>
public sealed record NotesParagraph(NotesText Text, int Start, int Line) : NotesBlock(Start, Line);

/// <summary>A bullet, with the bullets nested under it (one level, no deeper).</summary>
/// <param name="Text">Its text, after the <c>- </c>.</param>
/// <param name="Children">The bullets nested under it.</param>
/// <param name="Start">Where its <c>-</c> stands.</param>
/// <param name="Line">Its first line, 1-based.</param>
public sealed record NotesBullet(NotesText Text, ImmutableArray<NotesBullet> Children, int Start, int Line) : NotesBlock(Start, Line);

/// <summary>A link an inline text holds.</summary>
/// <param name="Target">Its address: an <c>https://</c> address, or a route of the site with its anchor.</param>
/// <param name="Start">Where the address is in the file.</param>
/// <param name="Length">How long the address is.</param>
/// <param name="External">Whether it leaves the site.</param>
public sealed record NotesLink(string Target, int Start, int Length, bool External);

/// <summary>What the inline text of a paragraph or a bullet renders as.</summary>
/// <param name="Html">The HTML the site shows.</param>
/// <param name="Text">The plain text, for the search.</param>
/// <param name="Links">The links it holds, in order.</param>
public sealed record NotesInline(string Html, string Text, ImmutableArray<NotesLink> Links);

/// <summary>What the inline text needs to render: the decision records and where links to them point.</summary>
/// <param name="Adrs">The decision records' file names, by number.</param>
/// <param name="GitRef">The tag or commit a link into the repository points at.</param>
/// <param name="AllowRef">Whether <c>{ref}</c>, the prerelease note's placeholder, may appear.</param>
public sealed record NotesContext(ImmutableDictionary<string, string> Adrs, string GitRef, bool AllowRef);

/// <summary>
/// The Release Notes' Markdown subset (ADR-0190): <c>##</c> headings, bullets with one nested level,
/// paragraphs, bold, code spans and links, and nothing else. Anything else is refused with the line it
/// is on, because the same file is the GitHub release's text, and a construct the site renders one way
/// and GitHub another would make the two say different things. So is what the release workflow would
/// rewrite where the site does not — an <c>ADR-NNNN</c> inside a link's text or a code span, or joined to the
/// text before it — and what GitHub alone would link: a bare address, and an <c>@</c> joined to a name, which
/// GitHub makes a mention that notifies whoever has the name.
/// </summary>
public static class NotesMarkdown
{
    /// <summary>The repository's address on GitHub.</summary>
    public const string Repository = "https://github.com/AkiraMisawa/ex-grid";

    private static readonly Regex OrderedItem = new(@"^[0-9]+[.)](\s|$)", RegexOptions.CultureInvariant);
    private static readonly Regex Rule = new(@"^(-{3,}|\*{3,}|_{3,}|={3,})\s*$", RegexOptions.CultureInvariant);
    private static readonly Regex Entity = new(@"^&(#[0-9]+|#[xX][0-9a-fA-F]+|[A-Za-z][A-Za-z0-9]*);", RegexOptions.CultureInvariant);
    private static readonly Regex AdrMention = new(@"^ADR-([0-9]{4})(?![0-9A-Za-z])", RegexOptions.CultureInvariant);
    private static readonly Regex PullMention = new(@"^#([0-9]{1,6})(?![0-9A-Za-z])", RegexOptions.CultureInvariant);

    // What the release workflow rewrites wherever it stands, a code span included (release.yml, "The release's
    // text"): an ADR-NNNN, made a link to the record, and a link's ]( with a route, made a whole address. Inside a
    // code span the site shows either as code, and GitHub would show the rewritten Markdown as code.
    private static readonly Regex AdrInCode = new(@"ADR-[0-9]{4}", RegexOptions.CultureInvariant);
    private static readonly Regex RouteInCode = new(@"\]\((?!https://)[^)\s]+\)", RegexOptions.CultureInvariant);

    /// <summary>
    /// Reads the blocks of <paramref name="text"/> from line <paramref name="firstLine"/> (0-based) to
    /// its end: headings, paragraphs and bullets. A construct outside the subset is a problem, and is
    /// left out of the blocks.
    /// </summary>
    public static ImmutableArray<NotesBlock> ReadBlocks(SourceText text, int firstLine, List<NotesProblem> problems)
    {
        var blocks = ImmutableArray.CreateBuilder<NotesBlock>();
        NotesParagraph? paragraph = null;
        BulletBuilder? item = null;
        BulletBuilder? child = null;
        var afterBlank = false;

        void CloseItem()
        {
            if (item is not null)
                blocks.Add(item.Build());
            item = null;
            child = null;
        }

        for (var k = firstLine; k < text.Lines.Count; k++)
        {
            var line = text.Lines[k];
            var raw = line.ToString();
            var start = line.Start;
            var tab = raw.IndexOf('\t');
            if (tab >= 0)
            {
                problems.Add(new NotesProblem(NotesProblemKind.Subset, start + tab, 1,
                    "A tab is not in the notes' subset: indent with spaces, two for a nested bullet."));
                raw = raw.Replace('\t', ' ');
            }
            if (raw.Trim().Length == 0)
            {
                paragraph = null;
                afterBlank = true;
                continue;
            }

            var indent = 0;
            while (indent < raw.Length && raw[indent] == ' ')
                indent++;
            var body = raw.Substring(indent).TrimEnd();

            if (body.StartsWith("#", StringComparison.Ordinal) && !PullMention.IsMatch(body))
            {
                var level = 0;
                while (level < body.Length && body[level] == '#')
                    level++;
                if (level == 2 && body.Length > 3 && body[2] == ' ' && indent == 0)
                {
                    CloseItem();
                    paragraph = null;
                    blocks.Add(new NotesHeading(body.Substring(3).Trim(), start, k + 1));
                }
                else if (level == 1)
                {
                    problems.Add(new NotesProblem(NotesProblemKind.Shape, start, raw.Length,
                        "A # heading stands only on a version's first line, and names the version. Its sections are ## headings."));
                }
                else
                {
                    problems.Add(new NotesProblem(NotesProblemKind.Subset, start, raw.Length,
                        "Only ## headings, written at the start of the line with a space after them, are in the notes' subset. "
                        + "Make this a ## heading of the notes, a bold lead-in (**Like this:**), or a paragraph."));
                }
                afterBlank = false;
                continue;
            }

            if (body == "-" || body.StartsWith("- ", StringComparison.Ordinal))
            {
                var itemText = new NotesText();
                var textStart = start + indent + Math.Min(2, body.Length);
                itemText.AddLine(body.Length > 2 ? body.Substring(2) : "", textStart);
                if (indent == 0)
                {
                    CloseItem();
                    paragraph = null;
                    item = new BulletBuilder(itemText, start + indent, k + 1, 0);
                }
                else if (item is null)
                {
                    problems.Add(new NotesProblem(NotesProblemKind.Subset, start, raw.Length,
                        "This bullet is indented with no bullet above it to nest under. Start it at the line's start."));
                }
                else if (indent < 2)
                {
                    problems.Add(new NotesProblem(NotesProblemKind.Subset, start, raw.Length,
                        "A nested bullet is indented by two spaces under its bullet; one space nests nothing on GitHub."));
                }
                else if (item.ChildIndent > 0 && indent > item.ChildIndent)
                {
                    problems.Add(new NotesProblem(NotesProblemKind.Subset, start, raw.Length,
                        "This bullet nests under a nested bullet, and the notes' subset has one nested level. "
                        + "Indent it as its neighbours, or make it a bullet of its own."));
                }
                else
                {
                    if (item.ChildIndent == 0)
                        item.ChildIndent = indent;
                    child = new BulletBuilder(itemText, start + indent, k + 1, indent);
                    item.Children.Add(child);
                }
                afterBlank = false;
                continue;
            }

            if (indent < 4 && Outside(body) is { } construct)
            {
                problems.Add(new NotesProblem(NotesProblemKind.Subset, start + indent, body.Length, construct));
                afterBlank = false;
                continue;
            }

            if (item is not null && (!afterBlank || indent >= 2))
            {
                if (afterBlank)
                {
                    problems.Add(new NotesProblem(NotesProblemKind.Subset, start, raw.Length,
                        "A second paragraph inside a bullet is not in the notes' subset. Join it to the bullet's text, "
                        + "or make it a nested bullet (two spaces, then - )."));
                }
                else
                {
                    (child ?? item).Text.AddLine(body, start + indent);
                }
                afterBlank = false;
                continue;
            }

            if (indent >= 4 && paragraph is null)
            {
                problems.Add(new NotesProblem(NotesProblemKind.Subset, start, raw.Length,
                    "A line indented by four spaces is a code block, which is not in the notes' subset. "
                    + "Start it at the line's start, or write code in a code span (`like this`)."));
                afterBlank = false;
                continue;
            }

            if (paragraph is not null)
            {
                paragraph.Text.AddLine(body, start + indent);
            }
            else
            {
                CloseItem();
                var paragraphText = new NotesText();
                paragraphText.AddLine(body, start + indent);
                paragraph = new NotesParagraph(paragraphText, start + indent, k + 1);
                blocks.Add(paragraph);
            }
            afterBlank = false;
        }
        CloseItem();
        return blocks.ToImmutable();
    }

    // What a line starts that the subset does not have, said with its fix; null for text.
    private static string? Outside(string body)
    {
        if (body.StartsWith(">", StringComparison.Ordinal))
            return "A quotation (>) is not in the notes' subset. Write it as a paragraph.";
        if (body.StartsWith("```", StringComparison.Ordinal) || body.StartsWith("~~~", StringComparison.Ordinal))
            return "A fenced code block is not in the notes' subset. Write code in a code span (`like this`).";
        if (body.StartsWith("|", StringComparison.Ordinal))
            return "A table is not in the notes' subset. Write its rows as bullets.";
        if (Rule.IsMatch(body))
            return "A rule or an underlined heading is not in the notes' subset. Leave a blank line, or use a ## heading of the notes.";
        if (body.StartsWith("* ", StringComparison.Ordinal) || body.StartsWith("+ ", StringComparison.Ordinal))
            return "A bullet is written with - in the notes' subset. Write - in place of * or +.";
        if (OrderedItem.IsMatch(body))
            return "A numbered list is not in the notes' subset. Write its items as bullets (- ).";
        if (body.Length > 1 && body[0] == '<' && (char.IsLetter(body[1]) || body[1] is '/' or '!' or '?'))
            return "HTML is not in the notes' subset. Write a name in a code span, as `<ExGrid>`.";
        return null;
    }

    /// <summary>
    /// Renders <paramref name="text"/>, or the part of it from <paramref name="from"/>, as HTML and as
    /// plain text, and finds its links. A construct outside the subset, an ADR with no file and a
    /// <c>{ref}</c> outside the prerelease note are problems.
    /// </summary>
    public static NotesInline Render(NotesText text, NotesContext context, List<NotesProblem> problems, int from = 0)
    {
        var renderer = new InlineRenderer(text, context, problems);
        renderer.Run(from, text.Text.Length, inLink: false);
        return new NotesInline(renderer.Html.ToString(), Collapse(renderer.Plain.ToString()), renderer.Links.ToImmutable());
    }

    private static string Collapse(string text) => Regex.Replace(text, @"\s+", " ").Trim();

    private sealed class BulletBuilder(NotesText text, int start, int line, int indent)
    {
        public NotesText Text { get; } = text;

        public List<BulletBuilder> Children { get; } = [];

        public int ChildIndent { get; set; }

        public int Indent { get; } = indent;

        public NotesBullet Build() =>
            new(Text, Children.Select(c => c.Build()).ToImmutableArray(), start, line);
    }

    private sealed class InlineRenderer(NotesText source, NotesContext context, List<NotesProblem> problems)
    {
        private readonly string _s = source.Text;

        public StringBuilder Html { get; } = new();

        public StringBuilder Plain { get; } = new();

        public ImmutableArray<NotesLink>.Builder Links { get; } = ImmutableArray.CreateBuilder<NotesLink>();

        private void Problem(NotesProblemKind kind, int index, int length, string message)
        {
            var start = source.OffsetOf(index);
            var end = source.OffsetOf(Math.Max(index, index + length - 1)) + 1;
            problems.Add(new NotesProblem(kind, start, Math.Max(1, end - start), message));
        }

        private void Char(char c)
        {
            if (c == '\n')
                c = ' ';
            Highlighter.Text(Html, c.ToString());
            Plain.Append(c);
        }

        private static bool IsEscapable(char c) => c is '\\' or '`' or '*' or '_' or '[' or ']' or '(' or ')' or '#' or '<' or '>' or '!' or '{' or '}' or '-' or '~' or '&' or '|';

        public void Run(int from, int to, bool inLink)
        {
            var bold = -1;
            var i = from;
            while (i < to)
            {
                var c = _s[i];
                var next = i + 1 < to ? _s[i + 1] : '\0';
                if (c == '\\' && IsEscapable(next))
                {
                    Char(next);
                    i += 2;
                    continue;
                }
                if (c == '`')
                {
                    if (next == '`')
                    {
                        Problem(NotesProblemKind.Subset, i, 2,
                            "A code span opened by two backticks is not in the notes' subset. Write it between single backticks.");
                        Char('`');
                        Char('`');
                        i += 2;
                        continue;
                    }
                    var close = _s.IndexOf('`', i + 1);
                    if (close < 0 || close >= to)
                    {
                        Problem(NotesProblemKind.Subset, i, 1, "This code span is never closed. Close it with a backtick (`).");
                        Char('`');
                        i++;
                        continue;
                    }
                    var code = _s.Substring(i + 1, close - i - 1).Replace('\n', ' ');
                    Html.Append("<code>");
                    Highlighter.Text(Html, code);
                    Html.Append("</code>");
                    Plain.Append(code);
                    if (code.Contains("{ref}") && !context.AllowRef)
                        RefOutside(i + 1 + code.IndexOf("{ref}", StringComparison.Ordinal));
                    if (AdrInCode.Match(code) is { Success: true } adrInCode)
                    {
                        Problem(NotesProblemKind.Subset, i + 1 + adrInCode.Index, adrInCode.Length,
                            $"{adrInCode.Value} in a code span is made a link inside the code by the GitHub release, which then shows the link's Markdown as code, "
                            + "and stays code on the site. Write it outside the code span.");
                    }
                    if (RouteInCode.Match(code) is { Success: true } routeInCode)
                    {
                        Problem(NotesProblemKind.Subset, i + 1 + routeInCode.Index, routeInCode.Length,
                            "A ]( in a code span is read by the GitHub release as a link to the site, whose address it makes whole inside the code, "
                            + "and stays code on the site. Write the code without it, or write a link outside the code span.");
                    }
                    i = close + 1;
                    continue;
                }
                if (c == '*' && next == '*')
                {
                    if (bold < 0)
                    {
                        bold = i;
                        Html.Append("<strong>");
                    }
                    else
                    {
                        bold = -1;
                        Html.Append("</strong>");
                    }
                    i += 2;
                    continue;
                }
                if (c == '*')
                {
                    Problem(NotesProblemKind.Subset, i, 1,
                        "A single * makes italics, which the notes' subset does not have. Write **bold**, or \\* for a star.");
                    Char(c);
                    i++;
                    continue;
                }
                if (c == '_' && OpensItalics(i, to))
                {
                    Problem(NotesProblemKind.Subset, i, 1,
                        "_This_ makes italics, which the notes' subset does not have. Write **bold**, a name in a code span, or \\_ for an underscore.");
                    Char(c);
                    i++;
                    continue;
                }
                if (c == '~' && next == '~')
                {
                    Problem(NotesProblemKind.Subset, i, 2,
                        "~~Strikethrough~~ is not in the notes' subset. Say in words what no longer holds.");
                    Char(c);
                    Char(next);
                    i += 2;
                    continue;
                }
                if (c == '!' && next == '[')
                {
                    Problem(NotesProblemKind.Subset, i, 2,
                        "An image is not in the notes' subset. Link the page that shows it instead.");
                    Char(c);
                    i++;
                    continue;
                }
                if (c == '[')
                {
                    if (Link(ref i, to, inLink))
                        continue;
                    Char(c);
                    i++;
                    continue;
                }
                if (c == '<' && (char.IsLetter(next) || next is '/' or '!' or '?'))
                {
                    Problem(NotesProblemKind.Subset, i, 2,
                        "HTML, and an address in <angle brackets>, are not in the notes' subset. Write a name in a code span, as `<ExGrid>`, and an address as a link, [text](https://…).");
                    Char(c);
                    i++;
                    continue;
                }
                if (c == '&' && Entity.Match(_s.Substring(i, Math.Min(12, to - i))) is { Success: true } entity)
                {
                    Problem(NotesProblemKind.Subset, i, entity.Length,
                        $"The character reference {entity.Value} reads as itself on the site and as a character on GitHub. Write the character itself.");
                    Char(c);
                    i++;
                    continue;
                }
                if (c == '@' && (char.IsLetterOrDigit(next) || next == '_'))
                {
                    Problem(NotesProblemKind.Subset, i, 2,
                        "An @ joined to a name is a mention on the GitHub release, which links that account and notifies it, or part of an address "
                        + "GitHub links, and is plain text on the site. Write it in a code span, as `@bind`, or leave the @ out.");
                    Char(c);
                    i++;
                    continue;
                }
                if (!inLink && (At(i, "https://") || At(i, "http://") || (At(i, "www.") && (i == 0 || !char.IsLetterOrDigit(_s[i - 1])))))
                {
                    var end = i;
                    while (end < to && !char.IsWhiteSpace(_s[end]) && _s[end] is not (')' or ']'))
                        end++;
                    Problem(NotesProblemKind.Subset, i, end - i,
                        "A bare address is linked by GitHub and not by the site. Write it as a link, [text](https://…).");
                    for (var k = i; k < end; k++)
                        Char(_s[k]);
                    i = end;
                    continue;
                }
                if (c == 'A' && (i == 0 || !char.IsLetterOrDigit(_s[i - 1])) && AdrMention.Match(_s.Substring(i, Math.Min(9, to - i))) is { Success: true } adr)
                {
                    var number = adr.Groups[1].Value;
                    var before = i > 0 ? _s[i - 1] : ' ';
                    var after = i + adr.Length < _s.Length ? _s[i + adr.Length] : ' ';
                    if (inLink)
                    {
                        Problem(NotesProblemKind.Subset, i, adr.Length,
                            $"{adr.Value} inside a link's text is made a second link, inside the first, by the GitHub release, and stays text on the site. "
                            + "Write it after the link, as [Selection](exgrid/selection) (ADR-0012).");
                        Html.Append(adr.Value);
                    }
                    else if (before is '[' or '/' or '-' or '_' || after == '_')
                    {
                        Problem(NotesProblemKind.Subset, i, adr.Length,
                            $"{adr.Value} is joined to the text beside it by '{(after == '_' ? after : before)}', so the GitHub release leaves it unlinked "
                            + "while the site links it. Put a space between them.");
                        Html.Append(adr.Value);
                    }
                    else if (!context.Adrs.TryGetValue(number, out var file))
                    {
                        Problem(NotesProblemKind.Link, i, adr.Length,
                            $"{adr.Value} names no decision record: docs/adr/ has no file {number}-….md. Correct the number.");
                        Html.Append(adr.Value);
                    }
                    else
                    {
                        Html.Append("<a href=\"").Append(Repository).Append("/blob/").Append(context.GitRef).Append("/docs/adr/")
                            .Append(file).Append("\" target=\"_blank\" rel=\"noopener\">").Append(adr.Value).Append("</a>");
                    }
                    Plain.Append(adr.Value);
                    i += adr.Length;
                    continue;
                }
                if (c == '#' && !inLink && (i == 0 || char.IsWhiteSpace(_s[i - 1]) || _s[i - 1] is '(' or ',')
                    && PullMention.Match(_s.Substring(i, Math.Min(8, to - i))) is { Success: true } pull)
                {
                    Html.Append("<a href=\"").Append(Repository).Append("/pull/").Append(pull.Groups[1].Value)
                        .Append("\" target=\"_blank\" rel=\"noopener\">").Append(pull.Value).Append("</a>");
                    Plain.Append(pull.Value);
                    i += pull.Length;
                    continue;
                }
                if (c == '{' && At(i, "{ref}"))
                {
                    if (!context.AllowRef)
                        RefOutside(i);
                    Html.Append("{ref}");
                    Plain.Append("{ref}");
                    i += 5;
                    continue;
                }
                Char(c);
                i++;
            }
            if (bold >= 0)
            {
                Problem(NotesProblemKind.Subset, bold, 2, "This bold is never closed. Close it with **.");
                Html.Append("</strong>");
            }
        }

        private void RefOutside(int index) =>
            Problem(NotesProblemKind.Shape, index, 5,
                "{ref} is the prerelease note's place for the commit, and is filled in only there. Write the version or the commit itself.");

        private bool At(int i, string word) =>
            string.CompareOrdinal(_s, i, word, 0, word.Length) == 0;

        // An underscore that opens italics as GitHub reads it: after no letter or digit, before a
        // character that is not a space, and closed by another before no letter or digit.
        private bool OpensItalics(int i, int to)
        {
            if (i > 0 && char.IsLetterOrDigit(_s[i - 1]))
                return false;
            if (i + 1 >= to || char.IsWhiteSpace(_s[i + 1]) || _s[i + 1] == '_')
                return false;
            for (var k = i + 2; k < to; k++)
            {
                if (_s[k] == '_' && !char.IsWhiteSpace(_s[k - 1]) && (k + 1 >= to || !char.IsLetterOrDigit(_s[k + 1])))
                    return true;
            }
            return false;
        }

        // [text](address): renders it and moves past it. False, with a problem, for a [ that opens none.
        private bool Link(ref int i, int to, bool inLink)
        {
            var close = -1;
            for (var k = i + 1; k < to; k++)
            {
                if (_s[k] == '\\')
                {
                    k++;
                    continue;
                }
                if (_s[k] == '`')
                {
                    var end = _s.IndexOf('`', k + 1);
                    if (end > 0 && end < to)
                        k = end;
                    continue;
                }
                if (_s[k] == ']')
                {
                    close = k;
                    break;
                }
                if (_s[k] == '[')
                    break;
            }
            if (close < 0 || close + 1 >= to || _s[close + 1] != '(')
            {
                var why = close >= 0 && close + 1 < to && _s[close + 1] == '['
                    ? "A reference link ([text][name]) is not in the notes' subset. Write the address in the link: [text](address)."
                    : "This [ starts no link. Write a link as [text](address), or \\[ for a bracket.";
                Problem(NotesProblemKind.Subset, i, 1, why);
                return false;
            }
            var targetStart = close + 2;
            var targetEnd = _s.IndexOf(')', targetStart);
            if (targetEnd < 0 || targetEnd >= to)
            {
                Problem(NotesProblemKind.Subset, close + 1, 1, "This link's address is never closed. Close it with ).");
                return false;
            }
            if (inLink)
            {
                Problem(NotesProblemKind.Subset, i, targetEnd + 1 - i, "A link inside a link's text is not in the notes' subset.");
                return false;
            }
            var target = _s.Substring(targetStart, targetEnd - targetStart);
            var external = target.StartsWith("https://", StringComparison.Ordinal);
            if (target.Length == 0 || target.Any(char.IsWhiteSpace))
            {
                Problem(NotesProblemKind.Subset, targetStart, Math.Max(1, target.Length),
                    "A link's address has no spaces, and is not empty. Write a route of the site, as exgrid/selection#status-bar-figures, or a whole https:// address.");
            }
            else if (!external && (target.Contains("://") || target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)))
            {
                Problem(NotesProblemKind.Link, targetStart, target.Length,
                    $"'{target}' is neither a route of the site nor an https:// address. Only https:// addresses leave the site.");
            }
            else if (!external && target.StartsWith("/", StringComparison.Ordinal))
            {
                Problem(NotesProblemKind.Link, targetStart, target.Length,
                    $"A route of the site is written without its leading slash, as {target.TrimStart('/')}: the site is served under /ex-grid/, and the GitHub release makes the route whole.");
            }
            if (target.Contains("{ref}") && !context.AllowRef)
                RefOutside(targetStart + target.IndexOf("{ref}", StringComparison.Ordinal));
            Links.Add(new NotesLink(target, source.OffsetOf(targetStart), Math.Max(1, target.Length), external));

            Html.Append("<a href=\"");
            Highlighter.Text(Html, target);
            Html.Append(external ? "\" target=\"_blank\" rel=\"noopener\">" : "\">");
            Run(i + 1, close, inLink: true);
            Html.Append("</a>");
            i = targetEnd + 1;
            return true;
        }
    }
}
