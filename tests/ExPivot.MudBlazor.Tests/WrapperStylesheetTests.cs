using System.Text.RegularExpressions;
using ExPivot.MudBlazor.Tests.Support;
using Xunit;

namespace ExPivot.MudBlazor.Tests;

/// <summary>
/// What the Wrapper's stylesheet says (ADR-0062, held to ADR-0029/0030 as the grid Wrapper's is):
/// it maps every one of ExPivot's Visual Tokens onto MudBlazor's palette under the paper, sets no
/// Geometry Token, and writes no rule against a class of ExPivot's or ExGrid's own.
/// </summary>
public class WrapperStylesheetTests
{
    private static string Wrapper() => Repository.Read("src", "ExPivot.MudBlazor", "Assets", "mud-ex-pivot.css");

    private static string Core() => Repository.Read("src", "ExPivot", "Assets", "ex-pivot.css");

    private static string WithoutComments(string css) => Regex.Replace(css, @"/\*.*?\*/", "", RegexOptions.Singleline);

    /// <summary>The declarations of the Wrapper's <c>.mud-ex-grid</c> rule.</summary>
    private static Dictionary<string, string> PaperTokens()
    {
        var rule = Regex.Match(WithoutComments(Wrapper()), @"(^|\})\s*\.mud-ex-grid\s*\{([^}]*)\}");
        Assert.True(rule.Success, "no .mud-ex-grid rule in the Wrapper's stylesheet");
        return Regex.Matches(rule.Groups[2].Value, @"(--[a-z0-9-]+)\s*:\s*([^;]+);")
            .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value.Trim());
    }

    /// <summary>ExPivot's Visual Tokens: those its stylesheet reads with a system-colour default.
    /// The Geometry Tokens are read without one — C# writes them inline (ADR-0027).</summary>
    private static string[] VisualTokens()
    {
        var tokens = Regex.Matches(Core(), @"var\((--ex-pivot-[a-z0-9-]+)\s*,").Select(m => m.Groups[1].Value).Distinct().ToArray();
        Assert.NotEmpty(tokens);
        return tokens;
    }

    [Fact] // ADR-0062/0030: every Visual Token ExPivot reads is mapped onto the palette, so a dark/light switch recolours the pivot
    public void Every_visual_token_is_mapped_onto_the_palette()
    {
        var mapped = PaperTokens();

        var missing = VisualTokens().Where(token => !mapped.ContainsKey(token)).ToArray();
        Assert.Empty(missing);
        var off = VisualTokens()
            .Where(token => !(mapped[token].Contains("var(--mud-", StringComparison.Ordinal) || mapped[token] == "transparent"))
            .ToArray();
        Assert.Empty(off);
    }

    [Theory] // ADR-0027/0062: the Field List's width and a level's indent are written inline from C#; the Wrapper sets neither
    [InlineData("--ex-pivot-field-list-width")]
    [InlineData("--ex-pivot-indent")]
    public void The_wrapper_sets_no_geometry_token(string token)
        => Assert.DoesNotContain(token, WithoutComments(Wrapper()));

    [Fact] // ADR-0030/0062: no rule against a class of ExPivot's or ExGrid's own — its own classes, the paper and MudBlazor's
    public void The_wrapper_writes_no_rule_against_a_core_class()
    {
        var selectors = Regex.Matches(WithoutComments(Wrapper()), @"([^{}]+)\{").Select(m => m.Groups[1].Value.Trim()).ToArray();
        Assert.NotEmpty(selectors);

        var classes = selectors.SelectMany(s => Regex.Matches(s, @"\.([A-Za-z0-9_-]+)").Select(m => m.Groups[1].Value)).Distinct().ToArray();
        Assert.Contains("mud-ex-pivot-pane", classes);
        Assert.DoesNotContain(classes, c => c.StartsWith("ex-", StringComparison.Ordinal));
        Assert.All(classes, c => Assert.True(
            c == "mud-ex-grid" || c.StartsWith("mud-ex-pivot-", StringComparison.Ordinal) || c.StartsWith("mud-button-", StringComparison.Ordinal),
            $"'.{c}' is neither the paper, this package's own, nor MudBlazor's"));
    }

    [Fact] // ADR-0030/0062: every rule of the Wrapper's own elements is scoped to its own class, so it holds with or without the paper
    public void Each_rule_is_scoped_to_the_paper_or_an_own_class()
    {
        var selectors = Regex.Matches(WithoutComments(Wrapper()), @"([^{}]+)\{")
            .SelectMany(m => m.Groups[1].Value.Split(','))
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToArray();

        Assert.All(selectors, s => Assert.Matches(@"^\.(mud-ex-grid|mud-ex-pivot-[a-z0-9-]+)\b", s));
    }
}
