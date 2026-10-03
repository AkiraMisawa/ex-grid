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
