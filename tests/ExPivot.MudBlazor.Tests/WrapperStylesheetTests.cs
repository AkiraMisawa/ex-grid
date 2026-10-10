using System.Globalization;
using System.Text.RegularExpressions;
using ExPivot.MudBlazor.Tests.Support;
using MudBlazor;
using MudBlazor.Utilities;
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

    /// <summary>The declarations of the Wrapper's <c>.mud-ex-grid</c> rule — or of
    /// <paramref name="css"/>'s, another stylesheet's.</summary>
    private static Dictionary<string, string> PaperTokens(string? css = null)
    {
        var rule = Regex.Match(WithoutComments(css ?? Wrapper()), @"(^|\})\s*\.mud-ex-grid\s*\{([^}]*)\}");
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

    // ---- A Stale Report's value cells, painted readable (ADR-0067, 2026-10-09; UX-8) ------------

    /// <summary>A colour as its sRGB channels, exact, in bytes, and its alpha.</summary>
    private readonly record struct Rgba(double R, double G, double B, double A);

    /// <summary>The colour a palette variable holds: <c>--mud-palette-text-primary</c> is
    /// <see cref="Palette.TextPrimary"/>, its alpha as MudThemeProvider writes it, in 255ths.</summary>
    private static Rgba PaletteColour(Palette palette, string variable)
    {
        var name = string.Concat(variable.Split('-').Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
        var property = typeof(Palette).GetProperty(name);
        Assert.True(property is not null, $"MudBlazor's Palette has no {name} for --mud-palette-{variable}");
        var colour = (MudColor)property!.GetValue(palette)!;
        return new(colour.R, colour.G, colour.B, colour.A / 255.0);
    }

    /// <summary>What a mapping resolves to over a palette: <c>var(--mud-palette-x)</c>, or
    /// <c>color-mix(in srgb, var(--mud-palette-x) N%, var(--mud-palette-y))</c>, mixed as CSS Color 5
    /// mixes, its channels premultiplied by their alpha.</summary>
    private static Rgba Resolve(string mapping, Palette palette)
    {
        var plain = Regex.Match(mapping, @"^var\(--mud-palette-([a-z-]+)\)$");
        if (plain.Success)
            return PaletteColour(palette, plain.Groups[1].Value);
        var mix = Regex.Match(mapping, @"^color-mix\(in srgb,\s*var\(--mud-palette-([a-z-]+)\)\s+(\d+(?:\.\d+)?)%,\s*var\(--mud-palette-([a-z-]+)\)\)$");
        Assert.True(mix.Success, $"'{mapping}' is neither a palette colour nor a mix of two in srgb, which this test reads");
        var a = PaletteColour(palette, mix.Groups[1].Value);
        var b = PaletteColour(palette, mix.Groups[3].Value);
        var w = double.Parse(mix.Groups[2].Value, CultureInfo.InvariantCulture) / 100;
        var alpha = (a.A * w) + (b.A * (1 - w));
        double Channel(double ca, double cb) => ((ca * a.A * w) + (cb * b.A * (1 - w))) / alpha;
        return new(Channel(a.R, b.R), Channel(a.G, b.G), Channel(a.B, b.B), alpha);
    }

    /// <summary>A colour painted on an opaque ground, at the alpha the browser holds: a whole number
    /// of 255ths.</summary>
    private static Rgba Over(Rgba colour, Rgba ground)
    {
        var alpha = Math.Round(colour.A * 255) / 255;
        double Channel(double c, double g) => g + ((c - g) * alpha);
        return new(Channel(colour.R, ground.R), Channel(colour.G, ground.G), Channel(colour.B, ground.B), 1);
    }

    /// <summary>WCAG 2's contrast ratio of two opaque colours.</summary>
    private static double Contrast(Rgba one, Rgba other)
    {
        static double Linear(double v) => (v /= 255) <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        static double Luminance(Rgba c) => (0.2126 * Linear(c.R)) + (0.7152 * Linear(c.G)) + (0.0722 * Linear(c.B));
        var (l1, l2) = (Luminance(one), Luminance(other));
        return (Math.Max(l1, l2) + 0.05) / (Math.Min(l1, l2) + 0.05);
    }

    [Theory] // ADR-0067/0062 (2026-10-09), UX-8: under MudBlazor's default palettes, light and dark, a Stale Report's value cells keep body text's 4.5:1 against what they stand on — the paper's surface, and a group row's tint over it — and stay visibly muted beside the ink, at no more than 85% of its contrast
    [InlineData("light")]
    [InlineData("dark")]
    public void The_stale_value_colour_keeps_body_texts_contrast_and_stays_muted(string scheme)
    {
        Palette palette = scheme == "dark" ? new PaletteDark() : new PaletteLight();
        // What the report's cells stand on, and their ink: the grid Wrapper's mapping of the grid's own
        // tokens, which the paper carries for the pivot too.
        var grid = PaperTokens(Repository.Read("src", "ExGrid.MudBlazor", "Assets", "mud-ex-grid.css"));
        var ground = Resolve(grid["--ex-background"], palette);
        Assert.Equal(1, ground.A);
        var ink = Resolve(grid["--ex-color"], palette);
        // A group row's tint: ExGrid's own default, which neither Wrapper sets.
        var tint = Regex.Match(Repository.Read("src", "ExGrid", "Assets", "ex-grid.css"),
            @"var\(--ex-row-group-background,\s*rgba\((\d+),\s*(\d+),\s*(\d+),\s*([\d.]+)\)\)");
        Assert.True(tint.Success, "ex-grid.css paints a group row with no rgba default this test reads");
        Assert.DoesNotContain("--ex-row-group-background:", WithoutComments(Wrapper()));
        Assert.DoesNotContain("--ex-row-group-background:", WithoutComments(Repository.Read("src", "ExGrid.MudBlazor", "Assets", "mud-ex-grid.css")));
        double At(int group) => double.Parse(tint.Groups[group].Value, CultureInfo.InvariantCulture);
        var groupRow = Over(new Rgba(At(1), At(2), At(3), At(4)), ground);
        var stale = Resolve(PaperTokens()["--ex-pivot-stale-value-color"], palette);

        foreach (var (where, on) in new[] { ("the surface", ground), ("a group row", groupRow) })
        {
            var muted = Contrast(Over(stale, on), on);
            var plain = Contrast(Over(ink, on), on);
            Assert.True(muted >= 4.5, $"{scheme}: the stale value colour is {muted:F2}:1 on {where}, under body text's 4.5:1");
            Assert.True(muted <= 0.85 * plain, $"{scheme}: the stale value colour is {muted:F2}:1 on {where}, beside the ink's {plain:F2}:1: not muted");
        }
    }
}
