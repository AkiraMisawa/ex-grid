using System.Text.RegularExpressions;
using Xunit;

namespace ExGrid.MudBlazor.Tests;

/// <summary>
/// What the Wrapper's stylesheet says about the core's Visual Tokens (ADR-0029/0030): it maps
/// them onto MudBlazor's palette variables and touches nothing else of the core's.
/// </summary>
public class WrapperStylesheetTests
{
    private static string WrapperStylesheet()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ExGrid.slnx")))
            directory = directory.Parent;
        Assert.True(directory is not null, $"no ExGrid.slnx above {AppContext.BaseDirectory}");
        var path = Path.Combine(directory!.FullName, "src", "ExGrid.MudBlazor", "wwwroot", "mud-ex-grid.css");
        Assert.True(File.Exists(path), $"the Wrapper's stylesheet is not at {path}");
        return File.ReadAllText(path);
    }

    [Theory] // ADR-0052 (2026-09-29) / ADR-0029 / DC-44: the Wrapper paints the Size Tip from its tokens, as MudBlazor paints a tooltip
    [InlineData("--ex-size-tip-background", "var(--mud-palette-gray-darker)")]
    [InlineData("--ex-size-tip-color", "var(--mud-palette-dark-text)")]
    [InlineData("--ex-size-tip-outline", "none")]
    public void The_size_tip_tokens_are_mapped_onto_the_palette(string token, string value)
    {
        var root = Regex.Match(WrapperStylesheet(), @"\.mud-ex-grid\s*\{([^}]*)\}");

        Assert.True(root.Success, "no .mud-ex-grid rule in the Wrapper's stylesheet");
        Assert.Matches(new Regex($@"(^|[;\s]){Regex.Escape(token)}:\s*{Regex.Escape(value)};"), root.Groups[1].Value);
    }

    [Fact] // ADR-0052/0029 / DC-44: the Wrapper paints the Size Tip through its tokens alone; its box, place and size are the core's
    public void The_wrapper_writes_no_rule_against_the_size_tip()
        => Assert.DoesNotContain(".ex-size-tip", WrapperStylesheet());

    [Fact] // ADR-0057/0030 / DC-46: the Reference colours tell References apart and are not a brand, so the Wrapper leaves their tokens and classes at the core's
    public void The_wrapper_leaves_the_reference_colours_alone()
        => Assert.DoesNotContain("ex-reference", WrapperStylesheet());

    [Fact] // ADR-0068 / ADR-0030: the Wrapper maps the Change Highlight's token onto its palette, under its paper
    public void The_change_highlight_token_is_mapped_onto_the_palette()
    {
        var root = Regex.Match(WrapperStylesheet(), @"\.mud-ex-grid\s*\{([^}]*)\}");

        Assert.True(root.Success, "no .mud-ex-grid rule in the Wrapper's stylesheet");
        // The palette's warning, translucent so the value reads through it in both schemes.
        Assert.Matches(new Regex(@"(^|[;\s])--ex-change-highlight-background:\s*rgba\(var\(--mud-palette-warning-rgb\), 0\.25\);"),
            root.Groups[1].Value);
    }

    [Fact] // ADR-0068 / ADR-0029/0030: the Wrapper paints the mark through its token alone, and writes no rule against the class
    public void The_wrapper_writes_no_rule_against_the_change_highlight()
        => Assert.DoesNotContain("ex-changed", WrapperStylesheet());

    [Fact] // ADR-0068 / ADR-0027 P8 / UX-6 / DC-66: with the Wrapper's stylesheet loaded a mark still never animates — it transitions and animates nothing
    public void The_wrapper_transitions_and_animates_nothing()
    {
        var css = Regex.Replace(WrapperStylesheet(), @"/\*.*?\*/", "", RegexOptions.Singleline);

        Assert.DoesNotMatch(new Regex(@"(?<![\w-])(transition|animation)(-[\w-]+)?\s*:|@keyframes"), css);
    }
}
