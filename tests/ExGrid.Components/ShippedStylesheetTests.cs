using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Css;
using ExGrid.Components.Tests.Support;
using AngleSharp.Css.Parser;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// Assertions about the stylesheet the package ships, read where it is written. The script these held to the same
/// rules was removed with the grid drawn in .NET (ADR-0370); the Script Core's TypeScript is held by its own lint and
/// tests.
/// </summary>
public class ShippedStylesheetTests
{
    // The sources of what ships (ADR-0123): the package serves their minified form, and
    // ShippedAssetTests holds the two together, so a rule about how the code is written is read
    // where it is written.
    private static IReadOnlyList<(string Path, string Text)> ShippedAssets()
    {
        var files = Directory.EnumerateFiles(AssetSources.Folder("ExGrid"))
            .Where(file => file.EndsWith(".css", StringComparison.Ordinal))
            .Select(file => (Path: file, Text: File.ReadAllText(file)))
            .ToList();
        Assert.NotEmpty(files);
        return files;
    }

    [Fact] // ADR-0032 / HG-13: no vertical alignment option anywhere in the grid
    public void The_shipped_stylesheet_aligns_nothing_vertically()
    {
        // The Definition of Done states this verification as a grep, and this is that
        // grep: `vertical-align`, and the two `align-items` values that would push a
        // label to the top or the bottom of its box. Centring is what a fixed row height
        // leaves as the only sensible answer (ADR-0013), so an option to move it would be
        // a knob that either does nothing or argues for unfixing the height.
        var forbidden = new Regex(@"vertical-align|align-items:\s*(start|end|flex-start|flex-end|baseline)",
            RegexOptions.IgnoreCase);

        var offenders = ShippedAssets()
            .SelectMany(asset => asset.Text.Split('\n')
                .Select((line, index) => (asset.Path, Number: index + 1, Line: line.Trim()))
                .Where(entry => forbidden.IsMatch(entry.Line)))
            .Select(entry => $"{Path.GetFileName(entry.Path)}:{entry.Number} {entry.Line}")
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact] // ADR-0031 / DIR-4: no CSS logical property in the core's stylesheet — the geometry is physical by decision, not by omission
    public void DIR4_the_shipped_stylesheet_uses_no_logical_property()
    {
        // DIR-4's grep, as the Definition of Done writes it. The root is dir="ltr" (DIR-1), so a
        // logical property paints as its physical twin today; one that crept in would follow the
        // direction the day something under the root took a dir of its own, while every offset the
        // arithmetic computes stays physical (ADR-0031).
        var forbidden = new Regex(@"inline-start|inline-end|margin-inline|padding-inline|inset-inline|text-align: *(start|end)");

        var offenders = ShippedAssets()
            .Where(asset => asset.Path.EndsWith(".css", StringComparison.Ordinal))
            .SelectMany(asset => asset.Text.Split('\n')
                .Select((line, index) => (asset.Path, Number: index + 1, Line: line.Trim()))
                .Where(entry => forbidden.IsMatch(entry.Line)))
            .Select(entry => $"{Path.GetFileName(entry.Path)}:{entry.Number} {entry.Line}")
            .ToList();

        Assert.Empty(offenders);
    }



    /// <summary>The shipped core stylesheet without its comments, and its innermost rules — a
    /// rule inside an at-rule is read as its own — each as its selectors and its body.</summary>
    internal static (string Css, IReadOnlyList<(string[] Selectors, string Body)> Rules) CoreStylesheet()
    {
        var css = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.css", StringComparison.Ordinal)).Text;
        css = Regex.Replace(css, @"/\*.*?\*/", "", RegexOptions.Singleline);
        var rules = Regex.Matches(css, @"(?<selectors>[^{}]+)\{(?<body>[^{}]*)\}")
            .Select(rule => (
                rule.Groups["selectors"].Value.Split(',').Select(selector => selector.Trim()).ToArray(),
                rule.Groups["body"].Value))
            .ToList();
        return (css, rules);
    }

    /// <summary>The shipped core stylesheet's rules that hold under every condition — those outside
    /// any at-rule, which a media or a feature query would scope — in the order declared, each as
    /// its selectors and its body.</summary>
    internal static IReadOnlyList<(string[] Selectors, string Body)> UnconditionalRules()
    {
        var css = CoreStylesheet().Css;
        var outside = new StringBuilder(css.Length);
        for (var i = 0; i < css.Length; i++)
        {
            if (css[i] != '@')
            {
                outside.Append(css[i]);
                continue;
            }
            // An at-rule is left out whole: to its semicolon, or to the brace that closes its block.
            for (var depth = 0; i < css.Length; i++)
            {
                if (css[i] == ';' && depth == 0)
                    break;
                if (css[i] == '{')
                    depth++;
                else if (css[i] == '}' && --depth == 0)
                    break;
            }
        }
        return Regex.Matches(outside.ToString(), @"(?<selectors>[^{}]+)\{(?<body>[^{}]*)\}")
            .Select(rule => (
                rule.Groups["selectors"].Value.Split(',').Select(selector => selector.Trim()).ToArray(),
                rule.Groups["body"].Value))
            .ToList();
    }

    /// <summary>The declarations of a rule's body, custom properties included, as written.</summary>
    internal static IEnumerable<(string Property, string Value)> Declarations(string body)
        => body.Split(';')
            .Select(declaration => declaration.Split(':', 2))
            .Where(parts => parts.Length == 2)
            .Select(parts => (parts[0].Trim(), parts[1].Trim()));

    /// <summary>What the shipped stylesheet's unconditional rules give an element's property, as the
    /// cascade picks it — the most specific matching selector, then the last declared — or null when

    private static readonly CssSelectorParser SelectorParser = new();

    /// <summary>A selector's specificity, as the cascade weighs it.</summary>
    internal static Priority Specificity(string selector)
        => SelectorParser.ParseSelector(selector)?.Specificity
           ?? throw new ArgumentException($"not a selector: {selector}", nameof(selector));

    [Fact] // ADR-0071 (Part C of the eleventh run) / ticket 99: a row's rule is painted in whole device pixels, so one at 150% under every rasteriser
    public void A_rows_rule_is_painted_in_whole_device_pixels()
    {
        var (css, _) = CoreStylesheet();
        var grid = Assert.Single(UnconditionalRules(), rule => rule.Selectors.SequenceEqual([".ex-grid"]) && rule.Body.Contains("--ex-rule-dp", StringComparison.Ordinal));
        // The token rounded down to the device pixel, and never thinner than the token under one.
        Assert.Equal(
            "max(min(var(--ex-rule-width, 1px), var(--ex-dp)), round(down, var(--ex-rule-width, 1px), var(--ex-dp)))",
            Declarations(grid.Body).Single(declared => declared.Property == "--ex-rule-dp").Value);

        // Every band of the row's rule reads it: at 1.5 device pixels Chrome's software rasteriser
        // drew two rows where its GPU one drew one, which a Mac never shows.
        var bands = Regex.Matches(css, @"linear-gradient\(to top, var\(--ex-row-rule-color[^;]*");
        Assert.Equal(3, bands.Count);
        Assert.All(bands, band =>
        {
            Assert.Contains("0 var(--ex-rule-dp, 1px), transparent var(--ex-rule-dp, 1px))", band.Value, StringComparison.Ordinal);
            Assert.DoesNotContain("--ex-rule-width", band.Value, StringComparison.Ordinal);
        });
    }

    [Fact] // ADR-0090: a column's rule is painted in whole device pixels too, once its edges lie on them
    public void A_columns_rule_is_painted_in_whole_device_pixels()
    {
        var (css, _) = CoreStylesheet();
        // The cell's and the Row Headings' edge, and a lined cell's layer. The Name Box's edge in the
        // Formula Bar is no column edge, and keeps the token.
        var shadows = Regex.Matches(css, @"box-shadow: inset calc\(0px - var\(--ex-rule-[a-z]+, 1px\)\) 0 var\(--ex-(column|heading)-rule-color");
        Assert.Equal(2, shadows.Count);
        Assert.All(shadows, shadow => Assert.Contains("--ex-rule-dp", shadow.Value, StringComparison.Ordinal));
        var layers = Regex.Matches(css, @"linear-gradient\(to left, var\(--ex-column-rule-color[^)]*\)[^)]*\)");
        Assert.NotEmpty(layers);
        Assert.All(layers, layer => Assert.Contains("--ex-rule-dp", layer.Value, StringComparison.Ordinal));
    }

    [Fact] // ADR-0006 / ADR-0029 / ticket 84: whatever a tone paints, a Cell State that paints it too outranks the tone, under every token
    public void A_cell_state_outranks_a_tone()
    {
        var rules = UnconditionalRules()
            .SelectMany((rule, order) => rule.Selectors.Select(selector => (Selector: selector, rule.Body, Order: order)))
            .ToList();
        var tones = rules.Where(rule => rule.Selector.StartsWith(".ex-cell.ex-tone-", StringComparison.Ordinal)).ToList();
        var states = rules.Where(rule => rule.Selector.StartsWith(".ex-cell.ex-state-", StringComparison.Ordinal)).ToList();

        // A state the Consumer named outranks a tone its rule derived (ADR-0006): on every property
        // both paint, the state's selector is the more specific, or as specific and declared later.
        // Which colour either paints is a token's, so the order is what decides, whatever a theme sets.
        var contested = new List<string>();
        foreach (var tone in tones)
        {
            foreach (var (property, _) in Declarations(tone.Body))
            {
                foreach (var state in states.Where(state => Declarations(state.Body).Any(declared => declared.Property == property)))
                {
                    contested.Add($"{state.Selector} over {tone.Selector}: {property}");
                    var order = Specificity(state.Selector).CompareTo(Specificity(tone.Selector));
                    Assert.True(order > 0 || (order == 0 && state.Order > tone.Order),
                        $"{tone.Selector} (rule {tone.Order}) outranks {state.Selector} (rule {state.Order}) on {property}");
                }
            }
        }
        // There is something to outrank: a tone paints a colour, and Stale and Error each paint one
        // of their own. Modified's mark and Missing's tint are layers a tone never paints.
        Assert.Equal(
            [
                ".ex-cell.ex-state-error over .ex-cell.ex-tone-negative: color",
                ".ex-cell.ex-state-error over .ex-cell.ex-tone-positive: color",
                ".ex-cell.ex-state-stale over .ex-cell.ex-tone-negative: color",
                ".ex-cell.ex-state-stale over .ex-cell.ex-tone-positive: color",
            ],
            contested.Order(StringComparer.Ordinal));
    }

    [Fact] // ADR-0008 (2026-09-29) / UX-18: every outline drawn in the Focus outline's width lies wholly inside its box, from one rule
    public void The_focus_outlines_width_and_inward_offset_are_written_once()
    {
        string[] outlined = [".ex-focus", ".ex-range", ".ex-range-single", ".ex-point", ".ex-action-chosen"];
        var sizing = CoreStylesheet().Rules
            .Where(rule => rule.Selectors.Any(outlined.Contains))
            .Where(rule => Regex.IsMatch(rule.Body, @"outline(-width|-offset)?:"))
            .ToList();

        // One rule gives the Focus, every range, the pointing outline and the chosen action the
        // width and the offset; no other rule for them restates either.
        var (selectors, body) = Assert.Single(sizing);
        Assert.Equal([".ex-action-chosen", ".ex-focus", ".ex-point", ".ex-range"], selectors.Order(StringComparer.Ordinal));
        // An outline straddling the edge loses its outer pixel beneath the layers painted above
        // the selection — a Pinned Column, the Headings, the header — so it is offset inward by
        // at least its own width.
        var width = double.Parse(Regex.Match(body, @"outline-width:\s*(?<px>[\d.]+)px").Groups["px"].Value, CultureInfo.InvariantCulture);
        var offset = double.Parse(Regex.Match(body, @"outline-offset:\s*(?<px>-?[\d.]+)px").Groups["px"].Value, CultureInfo.InvariantCulture);
        Assert.True(width > 0);
        Assert.True(offset <= -width, body);
    }

    [Fact] // ADR-0008/0029 (2026-09-29): a single range's outline reads --ex-selection-outline, which defaults to the Focus outline
    public void The_range_outline_reads_its_own_token_and_defaults_to_the_focus_outline()
    {
        var (css, rules) = CoreStylesheet();

        // The outline is the range's box of its own, its ::after (ADR-0008, 2026-10-01).
        var colours = rules
            .Where(rule => rule.Selectors.Contains(".ex-range-single::after"))
            .Select(rule => Regex.Match(rule.Body, @"border-color:\s*(?<value>[^;]+);"))
            .Where(colour => colour.Success)
            .Select(colour => colour.Groups["value"].Value.Trim())
            .ToList();

        // A Theme or Wrapper that sets only the Focus outline's colour gets both in it.
        Assert.Equal(["var(--ex-selection-outline, var(--ex-focus-outline, CanvasText))"], colours);
        // And the token is the range outline's alone: nothing else reads it.
        Assert.Single(Regex.Matches(css, "--ex-selection-outline"));
    }

    [Fact] // ADR-0058 / ADR-0029 (note of 2026-09-30) / DC-52: pointed at, the pointer over the rows and headers is cell, and every control there lets the press through
    public void A_grid_pointed_at_shows_the_cell_pointer_and_lets_every_press_through()
    {
        var (css, rules) = CoreStylesheet();

        Assert.Matches(new Regex(@"\.ex-pointed-at > \.ex-scroller > \.ex-spacer > :is\(\.ex-viewport, \.ex-header\) \{\s*cursor: cell;\s*\}"), css);
        var through = Regex.Match(css,
            @"\.ex-pointed-at > \.ex-scroller > \.ex-spacer > :is\(\.ex-viewport, \.ex-header\) :is\((?<controls>[^)]*)\) \{\s*pointer-events: none;\s*\}");
        Assert.True(through.Success);
        var controls = through.Groups["controls"].Value.Split(',').Select(control => control.Trim()).Order(StringComparer.Ordinal).ToList();

        // Every element the stylesheet gives a pointer of its own is among them, so a press on any
        // reaches the rows or the header, which hand it over (ADR-0058); a control added later that
        // takes its own pointer fails here until it is.
        var own = rules
            .Where(rule => Regex.IsMatch(rule.Body, @"pointer-events:\s*auto"))
            .SelectMany(rule => rule.Selectors)
            .Select(selector => selector.Split(' ')[^1])
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();
        Assert.Equal(own, controls);
    }

    [Fact] // ADR-0058 / ADR-0029 (note of 2026-09-30) / DC-53: the dashes are drawn in the Focus outline's colour, wholly inside their box, with no token of their own
    public void The_point_dashes_are_the_focus_outlines_dashes()
    {
        var (css, rules) = CoreStylesheet();

        var (_, body) = Assert.Single(rules, rule => rule.Selectors.Contains(".ex-point-dashes"));
        var outline = Regex.Match(body, @"outline:\s*(?<width>[\d.]+)px dashed var\(--ex-focus-outline, CanvasText\);");
        Assert.True(outline.Success, body);
        var width = double.Parse(outline.Groups["width"].Value, CultureInfo.InvariantCulture);
        var offset = double.Parse(Regex.Match(body, @"outline-offset:\s*(?<px>-?[\d.]+)px").Groups["px"].Value, CultureInfo.InvariantCulture);
        Assert.True(offset <= -width, body);
        Assert.DoesNotMatch(new Regex(@"--ex-point-dashes"), css);
    }


    [Fact] // ADR-0018 section 6 / ED-27: an edit whose keyboard is elsewhere has a 1px outline, from the stylesheet alone, in the token's style and colour
    public void An_edit_whose_keyboard_is_elsewhere_is_drawn_with_a_1px_outline()
    {
        var stylesheet = ShippedAssets().Single(asset => Path.GetFileName(asset.Path) == "ex-grid.css");

        // While DOM focus is outside the root, the Cell Editor over the rows — not the Formula
        // Bar's field, outlined only while it has focus — is drawn with its outline 1px wide.
        var rule = Regex.Match(stylesheet.Text, @"\.ex-grid:not\(:focus-within\) \.ex-viewport \.ex-editor \{(?<body>[^}]*)\}");
        Assert.True(rule.Success, "no rule narrows the outline of an editor whose grid does not hold DOM focus");
        // The width alone: the style and the colour stay --ex-editor-outline's, a Wrapper's too,
        // and no token is added for it.
        Assert.Equal("outline-width: 1px;", rule.Groups["body"].Value.Trim());
        Assert.Matches(new Regex(@"\.ex-editor \{[^}]*outline: var\(--ex-editor-outline, 2px solid Highlight\);"), stylesheet.Text);
    }

}
