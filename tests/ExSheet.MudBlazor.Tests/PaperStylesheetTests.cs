using System.Text.RegularExpressions;
using Xunit;

namespace ExSheet.MudBlazor.Tests;

/// <summary>
/// SH-39 (ADR-0071, "Paper and Ink"; ADR-0027's note of 2026-09-30): the Paper and the Ink are
/// <c>--ex-sheet-paper</c> and <c>--ex-sheet-ink</c>, white and black in every scheme. ExSheet's own
/// stylesheet gives them those defaults and nothing that follows the scheme; neither MudBlazor
/// package maps them onto a palette, so a dark MudBlazor theme darkens the frame and never the Paper.
/// The gridlines on the Paper are read here too. Read from the repository's stylesheets, as SH-39
/// says: inspect.
/// </summary>
public class PaperStylesheetTests
{
    private static readonly Regex PaperToken = new(@"--ex-sheet-(paper|ink)\b", RegexOptions.CultureInvariant);

    private static string Stylesheet(params string[] path)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ExGrid.slnx")))
            directory = directory.Parent;
        Assert.True(directory is not null, $"no ExGrid.slnx above {AppContext.BaseDirectory}");
        var file = Path.Combine([directory!.FullName, .. path]);
        Assert.True(File.Exists(file), $"no stylesheet at {file}");
        return File.ReadAllText(file);
    }

    [Fact] // SH-39 / ADR-0071 / ADR-0030: ExGrid.MudBlazor's stylesheet neither sets nor reads the Paper tokens
    public void The_wrappers_stylesheet_leaves_the_paper_alone() =>
        Assert.DoesNotMatch(PaperToken, Stylesheet("src", "ExGrid.MudBlazor", "Assets", "mud-ex-grid.css"));

    [Fact] // SH-39 / ADR-0071: ExSheet.MudBlazor's stylesheet neither sets nor reads the Paper tokens
    public void The_sheet_chromes_stylesheet_leaves_the_paper_alone() =>
        Assert.DoesNotMatch(PaperToken, Stylesheet("src", "ExSheet.MudBlazor", "Assets", "mud-ex-sheet.css"));

    [Fact] // ADR-0071 / ADR-0050 item 15 (ticket 90): a Pinned Column's cell names its row gridline in --ex-row-rule and paints it from there, so a cell that draws lines (.ex-lined) keeps it beneath them
    public void A_pinned_cells_gridline_is_named_for_the_line_layer()
    {
        var css = Regex.Replace(Stylesheet("src", "ExSheet", "Assets", "ex-sheet.css"), @"/\*.*?\*/", "", RegexOptions.Singleline);
        var rule = Assert.Single(Regex.Matches(css, @"(?<selector>[^{}]*\.ex-pinned\b[^{}]*)\{(?<body>[^{}]*)\}"));
        var body = rule.Groups["body"].Value;

        // Unless the cell's own Fill covers it, as a Fill covers its gridlines.
        Assert.Contains(":not([class*=\" ex-fill-\"])", rule.Groups["selector"].Value, StringComparison.Ordinal);
        // In whole device pixels, as the row's own rule is (ticket 99).
        Assert.Matches(@"--ex-row-rule\s*:\s*linear-gradient\(to top, var\(--ex-row-rule-color\) 0 var\(--ex-rule-dp, 1px\)", body);
        Assert.Matches(@"background-image\s*:\s*var\(--ex-row-rule\)\s*;", body);
    }

    [Fact] // ADR-0206 / DC-59 / SH-54: on the Paper every wash multiplies — the pinned shade layer as one group, each scrollable tint and band — and nothing drawn on top does
    public void On_the_paper_every_wash_multiplies_and_nothing_drawn_on_top_does()
    {
        var css = Regex.Replace(Stylesheet("src", "ExSheet", "Assets", "ex-sheet.css"), @"/\*.*?\*/", "", RegexOptions.Singleline);
        var forced = Regex.Match(css, @"@media \(forced-colors: active\) \{(?<body>(?:[^{}]|\{[^{}]*\})*)\}");
        Assert.True(forced.Success);
        var outside = css.Remove(forced.Index, forced.Length);
        var rules = Regex.Matches(outside, @"(?<selectors>[^{}]+)\{(?<body>[^{}]*)\}")
            .Select(rule => (Selectors: rule.Groups["selectors"].Value.Split(',').Select(selector => selector.Trim()).ToArray(), Body: rule.Groups["body"].Value))
            .ToList();

        // What multiplies: the pinned shade layer itself, whose children are plain, and in the
        // scrollable layer each tint and each band.
        var blended = rules.Where(rule => rule.Body.Contains("mix-blend-mode: multiply", StringComparison.Ordinal))
            .SelectMany(rule => rule.Selectors).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(
            [".ex-sheet .ex-selection > .ex-focus-row", ".ex-sheet .ex-selection > .ex-hover-row", ".ex-sheet .ex-selection > .ex-range-tint", ".ex-sheet .ex-shade-pinned"],
            blended);
        // For a scrollable tint's blend to reach the rows, its layer is no group of its own on the
        // Paper; the shade layer keeps its own, since the layer is what blends.
        Assert.Contains(rules, rule => rule.Selectors.SequenceEqual([".ex-sheet .ex-selection"]) && rule.Body.Contains("z-index: auto", StringComparison.Ordinal));
        Assert.DoesNotContain(rules, rule => rule.Selectors.Any(selector => selector.Contains(".ex-shade-pinned", StringComparison.Ordinal)) && rule.Body.Contains("z-index", StringComparison.Ordinal));
        // The overrides that took the range's isolation and its tint's z-index away are gone with them:
        // no rule on the selection's layers or a range isolates, and none paints a ::before. Rules on
        // anything else are not this decision's.
        var selection = rules.Where(rule => rule.Selectors.Any(selector =>
            selector.Contains(".ex-range", StringComparison.Ordinal)
            || selector.Contains(".ex-selection", StringComparison.Ordinal)
            || selector.Contains(".ex-shade-pinned", StringComparison.Ordinal))).ToList();
        Assert.NotEmpty(selection);
        Assert.DoesNotContain(selection, rule => rule.Body.Contains("isolation", StringComparison.Ordinal));
        Assert.DoesNotContain(selection, rule => rule.Selectors.Any(selector => selector.Contains("::before", StringComparison.Ordinal)));
        // The shade is black at 22% where no token names it: #C7C7C7 over white Paper.
        Assert.Contains(rules, rule => rule.Selectors.SequenceEqual([".ex-sheet .ex-range-tint"])
            && rule.Body.Contains("background: var(--ex-selection-fill, rgba(0, 0, 0, 0.22));", StringComparison.Ordinal));
        // Under forced colors nothing blends what the browser forces.
        Assert.Matches(@"mix-blend-mode:\s*normal", forced.Groups["body"].Value);
        Assert.Contains(".ex-sheet .ex-shade-pinned", forced.Groups["body"].Value, StringComparison.Ordinal);
    }

    [Fact] // ADR-0008 (2026-10-12) / UX-19 / ADR-0071: on the Paper the Focus among several ranges is the core's thin border in the outline's colour, #217346 where no token names it, with no white line of its own
    public void On_the_paper_the_focus_among_ranges_is_the_outlines_green_with_no_white_line()
    {
        var css = Regex.Replace(Stylesheet("src", "ExSheet", "Assets", "ex-sheet.css"), @"/\*.*?\*/", "", RegexOptions.Singleline);
        var rules = Regex.Matches(css, @"(?<selectors>[^{}]+)\{(?<body>[^{}]*)\}")
            .Select(rule => (Selectors: rule.Groups["selectors"].Value.Split(',').Select(selector => selector.Trim()).ToArray(), Body: rule.Groups["body"].Value))
            .ToList();

        // Three classes, so it outranks the Focus outline's rule and its white line (two classes) in
        // any order the stylesheets load in. Its own token wins where a Theme or a Wrapper names it;
        // else the Focus outline's colour, which ExSheet.MudBlazor gives the primary; else Excel's
        // #217346, as the readings of 2026-10-12 (R4) measured the active cell's border among
        // several areas, and as the Focus outline's own fallback on the Paper is.
        var among = Assert.Single(rules, rule => rule.Selectors.Contains(".ex-sheet .ex-focus.ex-focus-among-ranges::after"));
        Assert.Contains("border-color: var(--ex-focus-among-ranges-outline, var(--ex-focus-outline, #217346));", among.Body, StringComparison.Ordinal);
        Assert.Contains(rules, rule => rule.Selectors.Contains(".ex-sheet .ex-focus::after")
            && rule.Body.Contains("border-color: var(--ex-focus-outline, #217346);", StringComparison.Ordinal));
        Assert.Contains("box-shadow: none;", among.Body, StringComparison.Ordinal);
        Assert.Contains(rules, rule => rule.Selectors.Contains(".ex-sheet .ex-focus::after")
            && rule.Body.Contains("box-shadow: inset", StringComparison.Ordinal));
        // Its width and place are the core's, one Device Pixel inside the cell: the Paper sets neither.
        Assert.DoesNotMatch(new Regex(@"\b(border|border-width|top|left|right|bottom):"), among.Body);
    }

    [Fact] // SH-39 / ADR-0071: ExSheet's stylesheet defaults the Paper to Excel's white and the Ink to Excel's black, the same in every scheme
    public void The_paper_and_the_ink_default_to_white_and_black_in_every_scheme()
    {
        var css = Stylesheet("src", "ExSheet", "Assets", "ex-sheet.css");
        var uses = Regex.Matches(css, @"var\(--ex-sheet-(paper|ink),\s*([^)]+)\)");

        Assert.NotEmpty(uses);
        Assert.All(uses, use => Assert.Equal(use.Groups[1].Value == "paper" ? "#fff" : "#000", use.Groups[2].Value.Trim()));
        // Nothing switches them by scheme: no media query on the scheme, and no system colour or
        // light-dark() as a default.
        Assert.DoesNotContain("prefers-color-scheme", css);
        Assert.DoesNotMatch(new Regex(@"var\(--ex-sheet-(paper|ink),\s*(Canvas|CanvasText|light-dark)"), css);
    }
}
