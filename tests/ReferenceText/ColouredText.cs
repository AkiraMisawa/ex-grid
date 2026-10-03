using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Xunit;

namespace ReferenceText;

/// <summary>
/// What a coloured-text layer (ADR-0057) colours, written as markup the tests can read: the text,
/// each Reference a span with its colour's class, <c>ex-reference-N</c>, and what Point wrote a span
/// with <c>ex-reference-pointed</c> — on the Reference's own span where it stands exactly there,
/// around the References inside it otherwise. The layer draws none of these spans: since ADR-0057's
/// note of 2026-10-01 its text is one run, and the core writes on it (<c>data-ex-colours</c>) which
/// highlight covers which characters, for the editor listener to colour. This reads that, checks it
/// is consistent — the line holds the layer's text and nothing else, each Reference on the grey wears
/// its pointed shade and none other does, every name is the grid's — and writes it down. Compiled into
/// each test project that reads a layer.
/// </summary>
internal static partial class ColouredText
{
    [GeneratedRegex(@"^(ex\d+-)reference-(\d+)(-pointed)?$")]
    private static partial Regex ReferenceName();

    [GeneratedRegex(@"^(ex\d+-)reference-pointed$")]
    private static partial Regex GroundName();

    /// <summary>The layer's colouring as markup (above).</summary>
    public static string Of(IElement layer)
    {
        var line = Assert.Single(layer.Children);
        Assert.Equal("ex-reference-text-line", line.ClassName);
        Assert.Empty(line.Children);
        var text = layer.GetAttribute("data-ex-text") ?? "";
        Assert.Equal(text, line.TextContent);

        var references = new List<(int Start, int End, int Place, bool Shaded)>();
        (int Start, int End)? ground = null;
        string? prefix = null;
        foreach (var entry in (layer.GetAttribute("data-ex-colours") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = entry.Split(',');
            Assert.Equal(3, fields.Length);
            var start = int.Parse(fields[0], System.Globalization.CultureInfo.InvariantCulture);
            var end = start + int.Parse(fields[1], System.Globalization.CultureInfo.InvariantCulture);
            Assert.InRange(end, start + 1, text.Length);
            if (GroundName().Match(fields[2]) is { Success: true } groundName)
            {
                Assert.Null(ground);
                ground = (start, end);
                prefix ??= groundName.Groups[1].Value;
                Assert.Equal(prefix, groundName.Groups[1].Value);
                continue;
            }
            var name = ReferenceName().Match(fields[2]);
            Assert.True(name.Success, $"not a highlight of the grid's: {fields[2]}");
            prefix ??= name.Groups[1].Value;
            Assert.Equal(prefix, name.Groups[1].Value);
            references.Add((start, end, int.Parse(name.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture), name.Groups[3].Success));
        }
        references.Sort((a, b) => a.Start.CompareTo(b.Start));
        foreach (var reference in references)
        {
            var onGround = ground is { } g && g.Start <= reference.Start && reference.End <= g.End;
            Assert.True(onGround == reference.Shaded, "a Reference wears its pointed shade exactly where it lies on the grey");
        }

        var markup = new StringBuilder();
        var at = 0;
        void Plain(int to)
        {
            if (to > at)
                markup.Append(Escaped(text[at..to]));
            at = Math.Max(at, to);
        }
        void Span(string classes, int to, Action? inside = null)
        {
            markup.Append("<span class=\"").Append(classes).Append("\">");
            if (inside is null)
                Plain(to);
            else
                inside();
            Plain(to);
            markup.Append("</span>");
        }
        var exact = ground is { } on ? references.FindIndex(r => r.Start == on.Start && r.End == on.End) : -1;
        for (var i = 0; i < references.Count; i++)
        {
            var reference = references[i];
            if (ground is { } span && exact < 0 && span.Start <= reference.Start && span.Start >= at)
            {
                Plain(span.Start);
                Span("ex-reference-pointed", span.End, () =>
                {
                    for (; i < references.Count && references[i].End <= span.End; i++)
                    {
                        Plain(references[i].Start);
                        Span($"ex-reference-{references[i].Place}", references[i].End);
                    }
                    i--;
                });
                continue;
            }
            Plain(reference.Start);
            Span(i == exact ? $"ex-reference-{reference.Place} ex-reference-pointed" : $"ex-reference-{reference.Place}", reference.End);
        }
        if (ground is { } last && exact < 0 && last.Start >= at)
        {
            Plain(last.Start);
            Span("ex-reference-pointed", last.End);
        }
        Plain(text.Length);
        return markup.ToString();
    }

    /// <summary>Text as markup serialises it: what AngleSharp's <c>InnerHtml</c> escapes, and nothing else.</summary>
    private static string Escaped(string text)
        => text.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal).Replace("\u00A0", "&nbsp;", StringComparison.Ordinal);
}
