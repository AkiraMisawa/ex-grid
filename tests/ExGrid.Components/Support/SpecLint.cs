using System.Text;
using System.Text.RegularExpressions;

namespace ExGrid.Components.Tests.Support;

/// <summary>
/// ADR-0220 §6: the lint for layer 3's specs. It refuses a spec that waits for a fixed time, or that
/// reads the DOM, a scroll offset or a picture straight after an input with nothing between them that
/// waits for the grid to have answered: an awaited expectation that retries, <c>circuitQuiet()</c>, or
/// the Settled Mark (<c>settled(</c>). It does not judge what a test asserts, with one exception: a test
/// that records where its keys land (<c>watchWhereKeysLand</c>) under a round trip
/// (<c>setRoundTrip</c> above 0) types them with the host's answers held (<c>holdAnswers(true)</c>),
/// because where a key lands depends on what stands when it is typed, and a round trip only makes it
/// likely that what stood still stands (2026-10-20).
/// <para>
/// It reads a spec as statements, not lines: the text is cut at every <c>;</c>, <c>{</c> and <c>}</c>
/// outside a string, a template, a regular expression and a comment, so a call written over several
/// lines is one statement, and a callback's body is statements of its own. Walking a test's statements
/// in order, an input leaves the grid owing an answer until a statement that settles it, and a read
/// while it is owed is an early read. A test, a hook or a function starts afresh.
/// </para>
/// <para>
/// A statement the lint cannot judge is allowed by a comment on it, or in the comment block straight
/// above it, that names the rule and why: <c>// spec-lint: fixed-wait: &lt;why&gt;</c> or
/// <c>// spec-lint: early-read: &lt;why&gt;</c> or <c>// spec-lint: round-trip: &lt;why&gt;</c>. An allowance without a reason is itself a finding.
/// </para>
/// </summary>
public static class SpecLint
{
    /// <summary>
    /// What a finding refuses: a fixed wait, an early read, a key whose landing is recorded under a round
    /// trip with the host's answers flowing, or an allowance that names no reason.
    /// </summary>
    public enum Rule { FixedWait, EarlyRead, RoundTrip, Allowance }

    /// <summary>One finding: its rule, the line its statement starts on, the statement, and why.</summary>
    public sealed record Finding(Rule Rule, int Line, string Statement, string Why);

    private sealed record Statement(string Text, int FirstLine, int LastLine);

    // What the user does, raw: keys, the pointer, a locator's action, the viewport, and the helpers that
    // only press (sheet-helpers.mjs). Each is the start of a statement, awaited or not.
    private static readonly Regex Input = new(
        @"^(?:await\s+)?(?:[\w$]+\.(?:keyboard|mouse)\.\w+\(" +
        @"|[\w$]+\.setViewportSize\(" +
        @"|(?:(?!\bawait\b)[^;])*?\.(?:click|dblclick|press|pressSequentially|fill|type|tap|check|uncheck|setChecked|selectOption|selectText|dispatchEvent|hover|dragTo|setInputFiles|focus|clear|blur)\(" +
        @"|(?:clickCell|pressAt|clickBarEnd|chooseNumberFormat)\()",
        RegexOptions.CultureInvariant);

    // An input whose helper waits for the grid's answer before it returns (sheet-helpers.mjs,
    // fixtures.mjs): the press and the expectation in one.
    private static readonly Regex SettledInput = new(
        @"^await\s+(?:pressCell|goTo|typeIntoNameBox|enter|openSheet|scrollRowToTop|typeSteadily|[\w$]+\.goto|[\w$]+\.reload)\(",
        RegexOptions.CultureInvariant);

    // What waits for the grid: Playwright's retrying assertions (awaited, with a matcher that retries),
    // expect.poll, the circuit's quiet, the Settled Mark, a picture that waits to be still, and the
    // helpers that wait through one of those.
    private static readonly Regex Settles = new(
        @"\bawait\s+(?:" +
        @"expect(?:\.soft)?\((?!\s*await\b)(?:(?!\bawait\b)[^;])*?\)\s*(?:\.not)?\s*\.(?:toHave\w*|toContainText|toPass|toBe(?:Visible|Hidden|Attached|Focused|Enabled|Disabled|Checked|Editable|Empty|InViewport))\(" +
        @"|expect(?:\.soft)?\.poll\(|expect\(\s*async\b" +
        @"|circuitQuiet\(|settled\(|stillPictures\(|revealRepainted\(|layoutCeilingTold\(" +
        @"|expect(?:FocusAt|ActiveDescendant|KeyboardOn|Covers|SelectionIsCell|CaretShown|TabStopTaken|CommandsGreyedOut|CommandsOffered)\(" +
        @"|(?:(?!\bawait\b)[^;])*?\.waitFor(?:Function|Selector|Event|URL|LoadState|Response|Request)?\()",
        RegexOptions.CultureInvariant);

    // A read whose value the test goes on to use: an awaited call that answers what the page holds,
    // anywhere but at the head of its statement (a bare `await x.evaluate(…)` is an action).
    private static readonly Regex Read = new(
        @"(?:[=(,?:!+\-*/&|\[]|\breturn\b)\s*\(?\s*await\s+(?:" +
        @"(?:(?!\bawait\b)[^;])*?\.(?:evaluate|evaluateAll|textContent|innerText|innerHTML|inputValue|getAttribute|boundingBox|count|isVisible|isHidden|isChecked|isEnabled|isDisabled|isEditable|screenshot|allTextContents|allInnerTexts|ariaSnapshot)\(" +
        @"|(?:boxOf|spanOf|painted|readClipboard|keySeenUntouched|headerRowsOf|dataRowCount|rowAt|stretchesOf|caretInField|activeDescendant|keyboardIsOn)\()",
        RegexOptions.CultureInvariant);

    private static readonly Regex FixedWait = new(
        @"\.waitForTimeout\(|(?<![\w$.])setTimeout\(|\bwindow\.setTimeout\(|(?<![\w$.])sleep\(",
        RegexOptions.CultureInvariant);

    // A test, a hook, or a function of the file's own: what was owed before it is not its business, and
    // what a function's definition reads is read when it is called, not where it is written.
    private static readonly Regex Starts = new(
        @"^(?:test(?:\.\w+)*\(|(?:export\s+)?(?:async\s+)?function\b|(?:const|let)\s+[\w$]+\s*=\s*(?:async\s+)?(?:\([^)]*\)|[\w$]+)\s*=>)",
        RegexOptions.CultureInvariant);

    // A round trip set above 0, or taken back to 0; the host's answers held or let go; keys watched for
    // where they land (fixtures.mjs, sheet-toolbar.spec.mjs, popovers.spec.mjs).
    private static readonly Regex RoundTripOn = new(@"\bsetRoundTrip\(\s*(?!0\s*\))[^)]", RegexOptions.CultureInvariant);
    private static readonly Regex RoundTripOff = new(@"\bsetRoundTrip\(\s*0\s*\)", RegexOptions.CultureInvariant);
    private static readonly Regex Hold = new(@"\bholdAnswers\(\s*true\s*\)", RegexOptions.CultureInvariant);
    private static readonly Regex LetGo = new(@"\bholdAnswers\(\s*false\s*\)", RegexOptions.CultureInvariant);
    private static readonly Regex Watches = new(@"\bwatchWhereKeysLand\(", RegexOptions.CultureInvariant);
    private static readonly Regex Bound = new(@"^(?:const|let)\s+(?<name>[\w$]+)\s*=\s*await\b", RegexOptions.CultureInvariant);
    private static readonly Regex KeysTyped = new(@"^(?:await\s+)?keyTips\(", RegexOptions.CultureInvariant);

    private static readonly Regex Allowance = new(
        @"spec-lint:\s*(?<rule>[\w-]+)\s*:?\s*(?<why>.*)$", RegexOptions.CultureInvariant);

    private const int ShortestReason = 12;

    /// <summary>Every finding in one spec's text, in the order of the text.</summary>
    public static IReadOnlyList<Finding> Check(string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var (statements, comments) = Split(lines);
        var findings = new List<Finding>();
        foreach (var (line, comment) in comments)
        {
            var allowance = Allowance.Match(comment);
            if (!allowance.Success)
            {
                continue;
            }
            var rule = allowance.Groups["rule"].Value;
            if (rule is not ("fixed-wait" or "early-read" or "round-trip"))
            {
                findings.Add(new(Rule.Allowance, line, comment.Trim(), $"\"{rule}\" is no rule of the lint's: it allows fixed-wait, early-read or round-trip"));
            }
            else if (allowance.Groups["why"].Value.Trim().Length < ShortestReason)
            {
                findings.Add(new(Rule.Allowance, line, comment.Trim(), "an allowance names why the lint cannot judge the statement"));
            }
        }

        int? owed = null;
        // The round-trip rule: the first key typed while the round trip is up, the answers flow and its
        // landing is watched; it is a finding once the test asserts where its keys landed.
        bool slowed = false, held = false, said = false;
        var watchers = new List<string>();
        Statement? typed = null;
        foreach (var statement in statements)
        {
            var s = statement.Text;
            if (Starts.IsMatch(s) && s.StartsWith("test", StringComparison.Ordinal))
            {
                (slowed, held, said, typed) = (false, false, false, null);
                watchers.Clear();
            }
            if (RoundTripOn.IsMatch(s)) slowed = true;
            if (RoundTripOff.IsMatch(s)) slowed = false;
            if (Hold.IsMatch(s)) held = true;
            if (LetGo.IsMatch(s)) held = false;
            // const landed = await watchWhereKeysLand(page), and const keys = await landed(): both name the record.
            if (Bound.Match(s) is { Success: true } bound
                && (Watches.IsMatch(s) || watchers.Any((w) => s.Contains(w + "()", StringComparison.Ordinal))))
            {
                watchers.Add(bound.Groups["name"].Value);
            }
            if (slowed && !held && typed is null && watchers.Count > 0
                && (Input.IsMatch(s) || SettledInput.IsMatch(s) || KeysTyped.IsMatch(s))
                && !Allowed(statement, "round-trip", lines, comments))
            {
                typed = statement;
            }
            if (typed is { } key && !said && s.Contains("expect(", StringComparison.Ordinal)
                && watchers.Any((w) => System.Text.RegularExpressions.Regex.IsMatch(s, $@"(?<![\w$]){System.Text.RegularExpressions.Regex.Escape(w)}\b")))
            {
                said = true;
                findings.Add(new(Rule.RoundTrip, key.FirstLine, key.Text,
                    $"where this key lands is asserted on line {statement.FirstLine}, under a round trip with the host's answers flowing: hold them across the keys (holdAnswers), for the round trip only makes it likely that what stood still stands (ADR-0220 §6)"));
            }
            if (FixedWait.IsMatch(s) && !Allowed(statement, "fixed-wait", lines, comments))
            {
                findings.Add(new(Rule.FixedWait, statement.FirstLine, s,
                    "a fixed wait stands in for the state the test needs: wait for that state (ADR-0220 §2)"));
            }
            if (Starts.IsMatch(s))
            {
                owed = null;
                continue;
            }
            if (Settles.IsMatch(s) || SettledInput.IsMatch(s))
            {
                owed = null;
                continue;
            }
            if (owed is { } input && Read.IsMatch(s) && !Allowed(statement, "early-read", lines, comments))
            {
                findings.Add(new(Rule.EarlyRead, statement.FirstLine, s,
                    $"read straight after the input on line {input}, before anything waits for the grid's answer (ADR-0220 §2)"));
            }
            if (Input.IsMatch(s))
            {
                owed = statement.FirstLine;
            }
        }
        return [.. findings.OrderBy((f) => f.Line)];
    }

    // Allowed by a comment on one of the statement's lines, or in the block of comment lines straight
    // above its first.
    private static bool Allowed(Statement statement, string rule, string[] lines, Dictionary<int, string> comments)
    {
        bool Names(int line) =>
            comments.TryGetValue(line, out var comment)
            && Allowance.Match(comment) is { Success: true } m
            && m.Groups["rule"].Value == rule
            && m.Groups["why"].Value.Trim().Length >= ShortestReason;

        for (var line = statement.FirstLine; line <= statement.LastLine; line++)
        {
            if (Names(line))
            {
                return true;
            }
        }
        for (var line = statement.FirstLine - 1; line >= 1 && lines[line - 1].TrimStart().StartsWith("//", StringComparison.Ordinal); line--)
        {
            if (Names(line))
            {
                return true;
            }
        }
        return false;
    }

    // Cuts the text into statements and collects each line's comment. A regular expression literal is
    // told from a division by what comes before the slash, as a JavaScript parser tells them.
    private static (List<Statement>, Dictionary<int, string>) Split(string[] lines)
    {
        var statements = new List<Statement>();
        var comments = new Dictionary<int, string>();
        var text = new StringBuilder();
        var first = 0;
        var templateDepth = new Stack<int>(); // brace depth at which each open ${…} returns to its template
        var blocks = new Stack<bool>(); // for each open brace outside a template: whether it opened a block
        var braces = 0;
        var inTemplate = false;
        var inBlockComment = false;
        var lastSignificant = '\0';

        void Cut(int line)
        {
            var s = Regex.Replace(text.ToString(), @"\s+", " ").Trim();
            if (s.Length > 0)
            {
                statements.Add(new(s, first, line));
            }
            text.Clear();
            first = 0;
        }

        for (var n = 0; n < lines.Length; n++)
        {
            var line = lines[n];
            var number = n + 1;
            var i = 0;
            void Keep(char c)
            {
                if (first == 0 && !char.IsWhiteSpace(c))
                {
                    first = number;
                }
                text.Append(c);
                if (!char.IsWhiteSpace(c))
                {
                    lastSignificant = c;
                }
            }

            while (i < line.Length)
            {
                var c = line[i];
                if (inBlockComment)
                {
                    var end = line.IndexOf("*/", i, StringComparison.Ordinal);
                    comments[number] = comments.GetValueOrDefault(number, "") + line[i..(end < 0 ? line.Length : end)];
                    if (end < 0)
                    {
                        i = line.Length;
                    }
                    else
                    {
                        inBlockComment = false;
                        i = end + 2;
                    }
                    continue;
                }
                if (inTemplate)
                {
                    if (c == '\\')
                    {
                        text.Append(line, i, Math.Min(2, line.Length - i));
                        i += 2;
                    }
                    else if (c == '`')
                    {
                        Keep(c);
                        inTemplate = false;
                        i++;
                    }
                    else if (c == '$' && i + 1 < line.Length && line[i + 1] == '{')
                    {
                        text.Append("${");
                        templateDepth.Push(braces);
                        braces++;
                        inTemplate = false;
                        i += 2;
                    }
                    else
                    {
                        text.Append(c);
                        i++;
                    }
                    continue;
                }
                if (c == '/' && i + 1 < line.Length && line[i + 1] == '/')
                {
                    comments[number] = comments.GetValueOrDefault(number, "") + line[(i + 2)..];
                    break;
                }
                if (c == '/' && i + 1 < line.Length && line[i + 1] == '*')
                {
                    inBlockComment = true;
                    i += 2;
                    continue;
                }
                if (c is '\'' or '"')
                {
                    var j = i + 1;
                    while (j < line.Length && line[j] != c)
                    {
                        j += line[j] == '\\' ? 2 : 1;
                    }
                    Keep(c);
                    text.Append(line, i + 1, Math.Min(j, line.Length) - i - 1);
                    if (j < line.Length)
                    {
                        text.Append(c);
                    }
                    i = j + 1;
                    continue;
                }
                if (c == '`')
                {
                    Keep(c);
                    inTemplate = true;
                    i++;
                    continue;
                }
                if (c == '/' && (lastSignificant == '\0' || "(,=:[!&|?{};+-*%<>~^".Contains(lastSignificant) || EndsWithKeyword(text)))
                {
                    var j = i + 1;
                    var inClass = false;
                    while (j < line.Length && (inClass || line[j] != '/'))
                    {
                        if (line[j] == '\\')
                        {
                            j++;
                        }
                        else if (line[j] == '[')
                        {
                            inClass = true;
                        }
                        else if (line[j] == ']')
                        {
                            inClass = false;
                        }
                        j++;
                    }
                    Keep(c);
                    text.Append(line, i + 1, Math.Min(j + 1, line.Length) - i - 1);
                    lastSignificant = '/';
                    i = j + 1;
                    while (i < line.Length && char.IsAsciiLetter(line[i]))
                    {
                        text.Append(line[i++]);
                    }
                    continue;
                }
                if (c == '{')
                {
                    braces++;
                    // A block — a function's body, an if's, a loop's — is statements of its own, and is
                    // cut. An object literal is part of the call or the expression it sits in, and is not:
                    // `await expect(page.getByRole('button', { name: 'Copy' })).toBeFocused()` is one
                    // statement, which settles.
                    var block = lastSignificant is ')' or '>' or '\0' or ';' or '{' or '}' || EndsWithBlockKeyword(text);
                    blocks.Push(block);
                    Keep(c);
                    if (block)
                    {
                        Cut(number);
                    }
                    i++;
                    continue;
                }
                if (c == '}')
                {
                    braces--;
                    if (templateDepth.Count > 0 && templateDepth.Peek() == braces)
                    {
                        templateDepth.Pop();
                        text.Append(c);
                        inTemplate = true;
                        i++;
                        continue;
                    }
                    if (blocks.Count > 0 && !blocks.Pop())
                    {
                        Keep(c);
                        i++;
                        continue;
                    }
                    Cut(number);
                    lastSignificant = '}';
                    i++;
                    continue;
                }
                if (c == ';')
                {
                    Cut(number);
                    lastSignificant = ';';
                    i++;
                    continue;
                }
                Keep(c);
                i++;
            }
            if (!inTemplate)
            {
                text.Append(' ');
            }
            else
            {
                text.Append('\n');
            }
        }
        Cut(lines.Length);
        return (statements, comments);
    }

    private static bool EndsWithBlockKeyword(StringBuilder text)
    {
        var s = text.ToString().TrimEnd();
        return s.EndsWith("else", StringComparison.Ordinal) || s.EndsWith("try", StringComparison.Ordinal)
            || s.EndsWith("finally", StringComparison.Ordinal) || s.EndsWith("do", StringComparison.Ordinal);
    }

    private static bool EndsWithKeyword(StringBuilder text)
    {
        var s = text.ToString().TrimEnd();
        return s.EndsWith("return", StringComparison.Ordinal) || s.EndsWith("typeof", StringComparison.Ordinal);
    }
}
