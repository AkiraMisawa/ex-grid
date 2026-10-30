using ExGrid.Components.Tests.Support;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Css;
using AngleSharp.Css.Parser;
using AngleSharp.Dom;
using ExGrid.Chrome;
using ExGrid.Keys;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// Assertions about the stylesheet and the script the package actually ships, read
/// through the build's own static-web-asset manifest rather than a guessed path — so
/// they follow the files if the files move, and fail loudly if the package stops
/// shipping them at all.
/// </summary>
public class ShippedStylesheetTests
{
    // The sources of what ships (ADR-0123): the package serves their minified form, and
    // ShippedAssetTests holds the two together, so a rule about how the code is written is read
    // where it is written.
    private static IReadOnlyList<(string Path, string Text)> ShippedAssets()
    {
        var files = Directory.EnumerateFiles(AssetSources.Folder("ExGrid"))
            .Where(file => file.EndsWith(".css", StringComparison.Ordinal)
                        || file.EndsWith(".js", StringComparison.Ordinal))
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
    /// none sets it. A pseudo-element's rule styles the pseudo-element, never the element.</summary>
    internal static string? Winning(IElement element, string property)
        => Winning(element, UnconditionalRules(), property)?.Value;

    /// <summary>The declaration the cascade picks for an element out of <paramref name="rules"/>, in the
    /// order it meets them, among <paramref name="properties"/> — a shorthand and its longhand compete
    /// as one: the most specific matching selector, then the last declared; or null when none sets any.
    /// A pseudo-element's rule styles the pseudo-element, never the element.</summary>
    internal static (string Property, string Value)? Winning(IElement element, IReadOnlyList<(string[] Selectors, string Body)> rules, params string[] properties)
        => rules
            .SelectMany((rule, order) => Declarations(rule.Body)
                .Where(declared => properties.Contains(declared.Property))
                .SelectMany(declared => rule.Selectors
                    .Where(selector => !selector.Contains("::", StringComparison.Ordinal) && element.Matches(selector))
                    .Select(selector => (Specificity: Specificity(selector), Order: order, declared.Property, declared.Value))))
            .OrderBy(candidate => candidate.Specificity)
            .ThenBy(candidate => candidate.Order)
            .Select(candidate => ((string Property, string Value)?)(candidate.Property, candidate.Value))
            .LastOrDefault();

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

    private static string ListenerBody(string script, string name)
    {
        var match = Regex.Match(script, @"const " + name + @" = \(event\) => \{.*?\n    \};", RegexOptions.Singleline);
        Assert.True(match.Success, $"{name} is not in the module");
        return match.Value;
    }

    [Fact] // ADR-0057 / DC-47 / DC-48: the field's own text is transparent only beside its layer and under the listener's class; the caret and a selection stay
    public void The_stylesheet_hides_the_fields_text_only_under_the_listeners_class()
    {
        var sheet = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.css", StringComparison.Ordinal)).Text;

        // Hidden unless the field right after it wears the class; paint only; every space kept.
        var layer = Regex.Match(sheet, @"\n\.ex-reference-text \{([^}]*)\}");
        Assert.True(layer.Success, "no .ex-reference-text rule");
        Assert.Contains("visibility: hidden;", layer.Groups[1].Value, StringComparison.Ordinal);
        Assert.Contains("pointer-events: none;", layer.Groups[1].Value, StringComparison.Ordinal);
        Assert.Contains("white-space: pre;", layer.Groups[1].Value, StringComparison.Ordinal);
        Assert.Matches(new Regex(@"\.ex-reference-text:has\(\+ \.ex-reference-text-shown\) \{ visibility: visible; \}"), sheet);

        // Transparent only under the class and beside a layer, the caret in the field's colour,
        // and a selection drawn by the field in the selection's own colours. A word the spelling
        // or grammar check marks, which Chrome draws again in its highlight's colour, is the
        // field's own text as well (DC-48).
        var transparent = Regex.Matches(sheet, @"(?m)^[^\n{]*\{[^}]*(?<![\w-])(?:-webkit-text-fill-color|color): transparent[^}]*\}")
            .Select(match => match.Value.Split('{')[0].Trim())
            .ToList();
        Assert.Equal(
            [
                ".ex-reference-text + .ex-reference-text-shown",
                ".ex-reference-text + .ex-reference-text-shown::spelling-error, .ex-reference-text + .ex-reference-text-shown::grammar-error",
            ],
            transparent);
        // Every field turns see-through while its layer shows, a Chrome's control included, so a
        // page that loses the Chrome's stylesheet shows the layer's text rather than nothing
        // (ADR-0057, "The Wrapper's shape is required…"); the ground is painted beneath the layer.
        Assert.Matches(new Regex(@"\.ex-reference-text \+ \.ex-reference-text-shown \{ -webkit-text-fill-color: transparent; caret-color: currentColor; background: transparent; \}"), sheet);
        Assert.DoesNotMatch(new Regex(@"input\.ex-editor\.ex-reference-text-shown"), sheet);
        Assert.Matches(new Regex(@"\.ex-editor,\s*\.ex-reference-text\.ex-reference-text-cell \{[^}]*background: var\(--ex-editor-background, Canvas\);"), sheet);
        Assert.Matches(new Regex(@"\.ex-reference-text \+ \.ex-reference-text-shown::selection \{ color: HighlightText; -webkit-text-fill-color: HighlightText; background-color: Highlight; \}"), sheet);
        Assert.Matches(new Regex(@"::grammar-error \{ color: transparent; \}"), sheet);

        // The core's own Cell Editor and its layer share one rule for box, padding, line and
        // colours, and the bar's field and its layer one for padding (DC-48).
        Assert.Matches(new Regex(@"\.ex-editor,\s*\.ex-reference-text\.ex-reference-text-cell \{"), sheet);
        Assert.Matches(new Regex(@"\.ex-name-box, \.ex-formula-bar-text, \.ex-reference-text\.ex-reference-text-bar \{ padding: 0 var\(--ex-cell-padding-x, 8px\); \}"), sheet);
    }

    [Fact] // ADR-0057/0029 (2026-09-30) / DC-56: the pointed Reference's text wears one Visual Token per place in the palette, Excel's shade for all seven, the approximation over a dark ground
    public void DC56_the_pointed_shade_is_one_token_per_place_in_the_palette()
    {
        var sheet = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.css", StringComparison.Ordinal)).Text;

        // Since ADR-0057's note of 2026-10-01 the layer's References are highlights, and the grid's
        // generated stylesheet paints each from a property the layer declares: the colour of its place,
        // its pointed shade, and the pointed ground.
        for (var place = 1; place <= Cells.ReferenceColour.PaletteLength; place++)
        {
            // Excel's: the first two as Part B of the eighth Windows run read them, the rest the tenth run.
            var light = place switch
            {
                1 => "#0401a2", 2 => "#630101", 3 => "#44007c", 4 => "#003600",
                5 => "#550059", 6 => "#531c00", _ => "#00323f",
            };
            // Over a dark ground, the Reference's own colour mixed 55% toward white; ADR-0058 / SH-34:
            // a Reference inside the XLOOKUP(...) a press on another grid wrote takes the same shade.
            Assert.Contains(
                $"--ex-reference-text-{place}-pointed: var(--ex-reference-{place}-pointed, light-dark({light}, color-mix(in srgb, var(--ex-reference-text-{place}) 55%, white)));",
                sheet, StringComparison.Ordinal);
            Assert.Contains($"--ex-reference-text-{place}: var(--ex-reference-{place}, light-dark(", sheet, StringComparison.Ordinal);
        }
        // Those properties alone paint the pointed text: one per place, none past the palette's end.
        Assert.Equal(Cells.ReferenceColour.PaletteLength,
            Regex.Matches(sheet, @"--ex-reference-text-\d+-pointed: var\(--ex-reference-\d+-pointed,").Count);
        Assert.DoesNotContain($"--ex-reference-{Cells.ReferenceColour.PaletteLength + 1}-pointed", sheet, StringComparison.Ordinal);
        Assert.DoesNotContain($"--ex-reference-text-{Cells.ReferenceColour.PaletteLength + 1}", sheet, StringComparison.Ordinal);

        // The ground is unchanged, and the one shade for all seven is retired everywhere shipped.
        Assert.Contains(
            "--ex-reference-text-pointed: var(--ex-reference-pointed-background, light-dark(#c6c6c6, #4b4b4b));",
            sheet, StringComparison.Ordinal);
        Assert.All(ShippedAssets(), asset => Assert.DoesNotContain("--ex-reference-pointed-color", asset.Text, StringComparison.Ordinal));
    }

    [Fact] // ADR-0057 (note of 2026-10-01) / ADR-0027/0029: under forced colours the References read as the rest of the text, and the ground goes, as the spans they replaced did
    public void Under_forced_colours_the_highlights_take_the_system_colours()
    {
        var sheet = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.css", StringComparison.Ordinal)).Text;
        var forced = sheet[sheet.IndexOf("@media (forced-colors: active)", StringComparison.Ordinal)..];

        // Chrome paints a custom highlight in Highlight and HighlightText under forced colours,
        // whatever its rule says; the line is left as authored, and its colours are the system's.
        Assert.Contains(".ex-reference-text-line { forced-color-adjust: none; color: CanvasText; }", forced, StringComparison.Ordinal);
        for (var place = 1; place <= Cells.ReferenceColour.PaletteLength; place++)
        {
            Assert.Contains($"--ex-reference-text-{place}: CanvasText;", forced, StringComparison.Ordinal);
            Assert.Contains($"--ex-reference-text-{place}-pointed: CanvasText;", forced, StringComparison.Ordinal);
        }
        Assert.Contains("--ex-reference-text-pointed: transparent;", forced, StringComparison.Ordinal);
    }

    /// <summary>The core stylesheet without its comments, split into the rules outside the
    /// forced-colors block and the rules inside it, each in source order.</summary>
    private static (IReadOnlyList<(string[] Selectors, string Body)> Outside, IReadOnlyList<(string[] Selectors, string Body)> ForcedColors) ForcedColorsSplit()
    {
        var (css, _) = CoreStylesheet();
        var media = Regex.Match(css, @"@media \(forced-colors: active\) \{(?<body>(?:[^{}]|\{[^{}]*\})*)\}");
        Assert.True(media.Success, "no @media (forced-colors: active) block in the core stylesheet");
        return (RulesOf(css.Remove(media.Index, media.Length)), RulesOf(media.Groups["body"].Value));

        static IReadOnlyList<(string[] Selectors, string Body)> RulesOf(string css)
            => Regex.Matches(css, @"(?<selectors>[^{}]+)\{(?<body>[^{}]*)\}")
                .Select(rule => (
                    rule.Groups["selectors"].Value.Split(',').Select(selector => selector.Trim()).ToArray(),
                    rule.Groups["body"].Value))
                .ToList();
    }

    [Fact] // ADR-0068 / ADR-0029: the Change Highlight's one token defaults to a tint of the system colour Mark, readable on a dark page as on a light one, and the core only reads it
    public void The_change_highlight_token_defaults_to_a_tint_of_mark_and_is_only_read()
    {
        var (css, _) = CoreStylesheet();

        // Every read of the token carries the same fallback: Mark at 40% over the cell's own
        // ground, because Mark itself stays yellow on a dark page whose text is light.
        var reads = Regex.Matches(css, @"var\(--ex-change-highlight-background");
        Assert.NotEmpty(reads);
        Assert.Equal(reads.Count, Regex.Matches(css,
            @"var\(--ex-change-highlight-background, color-mix\(in srgb, Mark 40%, transparent\)\)").Count);
        // A theme sets it; the core never does (ADR-0027).
        Assert.DoesNotMatch(new Regex(@"--ex-change-highlight-background\s*:"), css);
    }

    [Fact] // ADR-0068 / ADR-0006 / UX-16: the mark is a tint laid over exactly the layers the cell paints without it — over a stripe, a role and a Pinned Column's row rule, beneath a Missing state's tint, a total row's rule and a cell's lines
    public void The_change_highlight_is_a_tint_ordered_against_the_other_grounds()
    {
        var (rules, _) = ForcedColorsSplit();
        int IndexOf(string selector) => rules.ToList().FindIndex(rule => rule.Selectors.Contains(selector));
        const string mark = "linear-gradient(var(--ex-change-highlight-background, color-mix(in srgb, Mark 40%, transparent)), var(--ex-change-highlight-background, color-mix(in srgb, Mark 40%, transparent)))";
        const string rowRule = "var(--ex-row-rule, none)";
        const string lines = "var(--ex-line-t, none), var(--ex-line-r, none), var(--ex-line-b, none), var(--ex-line-l, none)";

        var marked = rules.Where(rule => rule.Selectors.Any(s => s.Contains("ex-changed", StringComparison.Ordinal))).ToList();
        Assert.Equal(
            [".ex-cell.ex-changed", ".ex-row-stripe .ex-pinned.ex-changed", ".ex-row-group .ex-cell.ex-changed",
             ".ex-row-total .ex-cell.ex-changed", ".ex-cell.ex-state-missing.ex-changed:not(.ex-lined)",
             ".ex-cell.ex-lined.ex-changed", ".ex-row-total .ex-cell.ex-lined.ex-changed, .ex-cell.ex-lined.ex-state-missing.ex-changed"],
            marked.Select(rule => string.Join(", ", rule.Selectors)));
        foreach (var (selectors, body) in marked)
        {
            var name = string.Join(", ", selectors);
            // An image layer, never the shorthand or a colour: either would take the Pinned
            // Column's opaque ground away, and a translucent mark would let the columns passing
            // beneath show through (ADR-0006).
            Assert.Matches(new Regex(@"(?<![\w-])background-image:"), body);
            Assert.DoesNotMatch(new Regex(@"(?<![\w-])background(-color)?\s*:"), body);
            var layers = Regex.Replace(Regex.Match(body, @"background-image:\s*(?<value>[^;]+);").Groups["value"].Value, @"\s+", " ");
            Assert.Single(Regex.Matches(layers, Regex.Escape(mark)));
            // A cell's lines lie above every ground, the mark among them (DC-59, ADR-0050 item 15).
            var lined = name.Contains(".ex-lined.", StringComparison.Ordinal);
            Assert.Equal(lined, layers.StartsWith(lines, StringComparison.Ordinal));
            var grounds = lined ? layers[(lines.Length + 2)..] : layers;
            // Of the grounds, the mark is the top one, except under a total row's rule and a
            // Missing state's tint: a line stays a line, and a state is never the thing that
            // disappears.
            var beneath = name.Contains("ex-row-total", StringComparison.Ordinal) || name.Contains("ex-state-missing", StringComparison.Ordinal);
            Assert.Equal(beneath, !grounds.StartsWith(mark, StringComparison.Ordinal));
            if (beneath)
                Assert.StartsWith("var(--ex-tint", grounds, StringComparison.Ordinal);
            // And it takes no layer away: wherever a Pinned Column's cell paints its row's rule —
            // everywhere but a group or total row, which names none — the rule stays, beneath the
            // mark, as the row's own rule lies beneath the scrollable cells beside it.
            if (name is not (".ex-row-group .ex-cell.ex-changed" or ".ex-row-total .ex-cell.ex-changed"))
                Assert.True(layers.IndexOf(rowRule, StringComparison.Ordinal) > layers.IndexOf(mark, StringComparison.Ordinal), $"{name} keeps the row's rule beneath the mark");
        }
        // A marked cell with lines paints .ex-lined's layers and the mark: one layer more, and a
        // size and a position for every layer. The others are .ex-lined's own, each as it is
        // written there, so a layer .ex-lined changes changes under the mark too: when ADR-0090
        // put the column rule in device pixels, the marked copies kept the old width.
        var (_, plainLined) = Assert.Single(rules, rule => rule.Selectors.SequenceEqual([".ex-cell.ex-lined"]));
        foreach (var (_, body) in marked.Where(rule => rule.Selectors.Any(s => s.Contains(".ex-lined.", StringComparison.Ordinal))))
        {
            Assert.Equal(ImagesOf(plainLined), ImagesOf(body).Replace(mark + ", ", "", StringComparison.Ordinal));
            Assert.Equal(LayersOf(plainLined, "background-image") + 1, LayersOf(body, "background-image"));
            Assert.Equal(LayersOf(body, "background-image"), LayersOf(body, "background-size"));
            Assert.Equal(LayersOf(body, "background-image"), LayersOf(body, "background-position"));
        }
        // At equal specificity the later rule wins: the mark is declared after the stripes, the
        // Row Kinds, Cell State and the lines, whose grounds it outranks or keeps, and the
        // combinations in the order stripe, role, state, lines, as those grounds rank among themselves.
        foreach (var ground in new[] { ".ex-row-stripe .ex-pinned", ".ex-row-group .ex-cell", ".ex-row-total .ex-cell", ".ex-cell.ex-state-missing", ".ex-cell.ex-lined" })
            Assert.True(IndexOf(".ex-cell.ex-changed") > IndexOf(ground), $"{ground} is declared after the mark");
        foreach (var combination in new[] { ".ex-row-stripe .ex-pinned.ex-changed", ".ex-row-group .ex-cell.ex-changed", ".ex-row-total .ex-cell.ex-changed" })
            Assert.True(IndexOf(".ex-cell.ex-lined.ex-changed") > IndexOf(combination), $"{combination} is declared after the lined mark");

        // A rule's background-image, its whitespace made single spaces.
        static string ImagesOf(string body)
            => Regex.Replace(Regex.Match(body, @"(?<![\w-])background-image:\s*(?<value>[^;]+);").Groups["value"].Value, @"\s+", " ").Trim();

        // The layers a background property lists: its value split at the commas outside parentheses.
        static int LayersOf(string body, string property)
        {
            var value = Regex.Match(body, $@"(?<![\w-]){property}:\s*(?<value>[^;]+);").Groups["value"].Value;
            var (count, depth) = (1, 0);
            foreach (var c in value)
            {
                if (c == '(')
                    depth++;
                else if (c == ')')
                    depth--;
                else if (c == ',' && depth == 0)
                    count++;
            }
            return count;
        }
    }

    [Fact] // ADR-0068 / ADR-0027 / DC-66 / UX-7: the forced-colors block restates the mark as a painted outline, before the states so a state keeps its own
    public void The_forced_colors_block_restates_the_change_highlight()
    {
        var (_, forced) = ForcedColorsSplit();
        int IndexOf(string selector) => forced.ToList().FindIndex(rule => rule.Selectors.Contains(selector));

        var restated = Assert.Single(forced, rule => rule.Selectors.Contains(".ex-cell.ex-changed"));
        // Not on a background alone, which forced colours may discard; painted, never laid out.
        Assert.Matches(new Regex(@"(?<![\w-])outline:\s*\d+px dashed Highlight;"), restated.Body);
        Assert.Matches(new Regex(@"outline-offset:\s*-\d+px;"), restated.Body);
        Assert.DoesNotMatch(new Regex(@"background|border|margin|padding|width|height"), restated.Body);
        // A state that is restated by an outline too keeps it on a cell carrying both.
        Assert.True(IndexOf(".ex-cell.ex-changed") < IndexOf(".ex-cell.ex-state-missing"));
        Assert.True(IndexOf(".ex-cell.ex-changed") < IndexOf(".ex-cell.ex-state-modified"));
    }

    [Fact] // ADR-0206 / DC-59: the core's stylesheet blends nothing — ExGrid and ExPivot paint the bands, then the tints, then the boxes, source over — and nothing on the tint or the pinned shade layer transitions
    public void The_core_stylesheet_blends_nothing_and_the_new_layers_do_not_transition()
    {
        var (css, rules) = CoreStylesheet();

        Assert.DoesNotContain("mix-blend-mode", css, StringComparison.Ordinal);
        // And no range isolates its own painting: the outlines lie over the tints by tree order.
        Assert.DoesNotContain(rules, rule => rule.Selectors.Any(selector => selector.StartsWith(".ex-range", StringComparison.Ordinal))
            && rule.Body.Contains("isolation", StringComparison.Ordinal));
        var named = rules.Where(rule => rule.Selectors.Any(selector => selector.Contains(".ex-range-tint", StringComparison.Ordinal) || selector.Contains(".ex-shade-pinned", StringComparison.Ordinal))).ToList();
        Assert.NotEmpty(named);
        Assert.All(named, rule => Assert.DoesNotMatch(new Regex(@"(?<![\w-])(transition|animation|isolation|mix-blend-mode)(-[\w-]+)?\s*:"), rule.Body));
        // The range's box paints no tint of its own any more: nothing draws a ::before on it.
        Assert.DoesNotContain(rules, rule => rule.Selectors.Any(selector => selector.StartsWith(".ex-range::before", StringComparison.Ordinal)));
    }

    [Fact] // ADR-0206 / ADR-0008 / ADR-0029: the tint paints the selection's fill, clipped by the hole it carries; the pinned shade layer is a sticky, unclickable layer above the pinned cells, as the pinned selection layer is
    public void The_tint_paints_the_fill_through_its_hole_and_the_shade_layer_stands_as_the_pinned_layer_does()
    {
        var rules = UnconditionalRules();

        var tint = Assert.Single(rules, rule => rule.Selectors.SequenceEqual([".ex-range-tint"]));
        Assert.Contains("background: var(--ex-selection-fill, rgba(60, 120, 216, 0.18));", tint.Body, StringComparison.Ordinal);
        Assert.Contains("clip-path: var(--ex-range-hole);", tint.Body, StringComparison.Ordinal);
        Assert.Contains("--ex-range-hole: none;", tint.Body, StringComparison.Ordinal);

        // The shade layer is placed exactly as the pinned selection layer: one rule for both.
        var place = Assert.Single(rules, rule => rule.Selectors.Contains(".ex-shade-pinned") && rule.Body.Contains("position", StringComparison.Ordinal));
        Assert.Contains(".ex-selection-pinned", place.Selectors);
        Assert.Matches(new Regex(@"position:\s*sticky;.*z-index:\s*3;"), place.Body);
        Assert.Contains(rules, rule => rule.Selectors.Contains(".ex-shade-pinned") && rule.Body.Contains("pointer-events: none", StringComparison.Ordinal));
    }

    [Fact] // ADR-0206 / UX-7: under forced colors no tint is painted, stated rather than left to how a browser forces a translucent background; every range keeps its box's outline
    public void Under_forced_colours_no_tint_is_painted()
    {
        var (_, forced) = ForcedColorsSplit();

        var tint = Assert.Single(forced, rule => rule.Selectors.Contains(".ex-range-tint"));
        Assert.Matches(new Regex(@"display:\s*none;"), tint.Body);
        var box = Assert.Single(forced, rule => rule.Selectors.Contains(".ex-range"));
        Assert.Matches(new Regex(@"outline-style:\s*solid;"), box.Body);
    }

    [Fact] // ADR-0008 (2026-10-12) / UX-19 / ADR-0090: among several ranges the Focus's border is one Device Pixel inside its cell, in a token of its own that defaults to the Focus outline's colour
    public void Among_several_ranges_the_focus_border_is_one_device_pixel_in_its_own_token()
    {
        var (css, _) = CoreStylesheet();
        var (outside, forced) = ForcedColorsSplit();

        var rule = Assert.Single(outside, rule => rule.Selectors.Contains(".ex-focus.ex-focus-among-ranges::after"));
        var declared = Declarations(rule.Body).ToDictionary(d => d.Property, d => d.Value);
        // One Device Pixel, in the token's colour (2026-10-12, decided with the user: where nothing
        // names the token it is the Focus outline's colour), so a Theme or a Wrapper that colours the
        // Focus colours this border too, with no mapping of its own; CanvasText where nothing names
        // either, as the Focus outline's own default.
        Assert.Equal("var(--ex-dp, 1px) solid var(--ex-focus-among-ranges-outline, var(--ex-focus-outline, CanvasText))", declared["border"]);
        // Inside the cell: a Device Pixel in from its top and left edges and from the gridline it holds
        // on its right and bottom, so every side is whole Device Pixels from an edge that lies on one.
        Assert.Equal("var(--ex-dp, 1px)", declared["top"]);
        Assert.Equal("var(--ex-dp, 1px)", declared["left"]);
        Assert.Equal("calc(var(--ex-rule-dp, 1px) + var(--ex-dp, 1px))", declared["right"]);
        Assert.Equal("calc(var(--ex-rule-dp, 1px) + var(--ex-dp, 1px))", declared["bottom"]);
        // It changes the Focus's box of its own and nothing else: its content and its place in the
        // cascade are the Focus outline's, which it outranks.
        Assert.DoesNotContain("content", declared.Keys);
        Assert.True(Specificity(".ex-focus.ex-focus-among-ranges::after").CompareTo(Specificity(".ex-focus::after")) > 0);
        // The token is that border's alone.
        Assert.Single(Regex.Matches(css, "--ex-focus-among-ranges-outline"));

        // D2: the restored border stays inside except at Heading-covered sides. Multi-range
        // rectangles still have their outlines, while the Focus has its own visible border.
        var restored = Assert.Single(forced, rule => rule.Selectors.Contains(".ex-focus.ex-focus-among-ranges::after"));
        var restore = Declarations(restored.Body).ToDictionary(d => d.Property, d => d.Value);
        Assert.Equal("\"\"", restore["content"]);
        Assert.Equal("var(--ex-outline-width, calc(2 * var(--ex-dp, 1px))) solid Highlight", restore["border"]);
        Assert.Contains("--ex-outline-heading-t", restore["top"], StringComparison.Ordinal);
        Assert.Contains("--ex-outline-heading-l", restore["left"], StringComparison.Ordinal);
        Assert.Equal("0", restore["right"]);
        Assert.Equal("0", restore["bottom"]);
        Assert.Contains(forced, rule => rule.Selectors.Contains(".ex-range") && Regex.IsMatch(rule.Body, @"outline-style:\s*solid;"));
    }

    [Fact] // ADR-0008 (2026-10-13) / ADR-0029 / ADR-0090: the Heading Mark is a line of two Device Pixels on the Headings' edge toward the cells, in a token of its own that is the Focus outline's colour unless set; forced colors keep it, in Highlight
    public void The_heading_mark_is_two_device_pixels_on_the_edge_toward_the_cells_in_its_own_token()
    {
        var (css, _) = CoreStylesheet();
        var (outside, forced) = ForcedColorsSplit();
        IReadOnlyDictionary<string, string> Declared(string selector)
            => Declarations(Assert.Single(outside, rule => rule.Selectors.Contains(selector)).Body)
                .ToDictionary(d => d.Property, d => d.Value);

        // The box is the Headings' own, placed inline by the core; the line is its border on one edge,
        // so it adds nothing to the box and lies wholly inside it.
        var mark = Declared(".ex-heading-mark");
        Assert.Equal("absolute", mark["position"]);
        Assert.Equal("border-box", mark["box-sizing"]);
        Assert.Equal("none", mark["pointer-events"]);
        Assert.Equal("solid", mark["border-style"]);
        Assert.Equal("0", mark["border-width"]);
        // Its token, and where nothing names it the Focus outline's colour (2026-10-13, decided with the
        // user), so a Theme or a Wrapper that colours the Focus colours the mark too; CanvasText where
        // nothing names either, as the Focus outline's own default.
        Assert.Equal("var(--ex-heading-mark, var(--ex-focus-outline, CanvasText))", mark["border-color"]);
        // D1 reuses the ink for letters and the selected corner, with separate shade tokens.
        Assert.Equal("var(--ex-heading-mark, var(--ex-focus-outline, CanvasText))", Declared(".ex-header-cell.ex-heading-marked")["color"]);
        Assert.Equal("var(--ex-heading-mark, var(--ex-focus-outline, CanvasText))", Declared(".ex-row:where(:has(+ .ex-heading-state.ex-heading-marked)) > .ex-row-heading")["color"]);
        Assert.Contains("--ex-heading-mark-background", Declared(".ex-row:where(:has(+ .ex-heading-state.ex-heading-marked)) > .ex-row-heading")["background-color"], StringComparison.Ordinal);
        Assert.Contains("--ex-heading-mark-whole-background", Declared(".ex-row:where(:has(+ .ex-heading-state.ex-heading-whole)) > .ex-row-heading")["background-color"], StringComparison.Ordinal);
        Assert.Equal("var(--ex-heading-mark, var(--ex-focus-outline, CanvasText))", Declared(".ex-headings-corner-marked::after")["background-color"]);
        // Two Device Pixels on the edge toward the cells: a column's Heading's bottom, a row's right.
        Assert.Equal(new Dictionary<string, string> { ["border-bottom-width"] = "calc(2 * var(--ex-dp, 1px))" }, Declared(".ex-heading-mark-column"));
        Assert.Equal(new Dictionary<string, string> { ["border-right-width"] = "calc(2 * var(--ex-dp, 1px))" }, Declared(".ex-heading-mark-row"));

        // The layers: the band's pinned layer above the pinned header cells and beneath the Headings'
        // corner; the Row Headings' layer above the Row Headings; a scrollable column's box above the
        // header's cells and beneath the pinned ones, as the columns pass beneath them.
        int ZIndex(string selector) => int.Parse(
            Assert.Single(outside
                .Where(rule => rule.Selectors.Contains(selector))
                .SelectMany(rule => Declarations(rule.Body)), d => d.Property == "z-index").Value,
            CultureInfo.InvariantCulture);
        Assert.InRange(ZIndex(".ex-heading-marks-pinned"), ZIndex(".ex-header .ex-pinned") + 1, ZIndex(".ex-headings-corner") - 1);
        Assert.True(ZIndex(".ex-heading-marks-rows") > ZIndex(".ex-row-heading"));
        Assert.Equal("none", Declared(".ex-heading-state")["display"]);
        Assert.InRange(ZIndex(".ex-heading-mark"), 1, ZIndex(".ex-header .ex-pinned") - 1);
        var layers = Declarations(Assert.Single(outside, rule => rule.Selectors.SequenceEqual([".ex-heading-marks-pinned", ".ex-heading-marks-rows"])).Body)
            .ToDictionary(d => d.Property, d => d.Value);
        Assert.Equal(new Dictionary<string, string>
        {
            ["position"] = "sticky", ["left"] = "0", ["width"] = "0", ["height"] = "0", ["pointer-events"] = "none",
        }, layers);

        // Under forced colors the line is kept, a border in a system colour, the one a range's outline
        // is restated in (ADR-0008).
        var restated = Assert.Single(forced, rule => rule.Selectors.SequenceEqual([".ex-heading-mark"]));
        Assert.Matches(new Regex(@"^\s*border-color:\s*Highlight;\s*$"), restated.Body);
        Assert.Contains(forced, rule => rule.Selectors.Contains(".ex-range") && Regex.IsMatch(rule.Body, @"outline-color:\s*Highlight;"));
    }

    [Fact] // ADR-0068 / ADR-0027 P8 / UX-6 / DC-66: a mark comes and goes in one step — nothing in the core's stylesheet transitions or animates
    public void Nothing_in_the_core_stylesheet_transitions_or_animates()
    {
        var (css, _) = CoreStylesheet();

        Assert.DoesNotMatch(new Regex(@"(?<![\w-])(transition|animation)(-[\w-]+)?\s*:|@keyframes"), css);
    }

    [Fact] // ADR-0332 §4 / ADR-0029: the Match Mark's two tokens default to tints of one orange, the current stronger, and neither is the Change Highlight's tint of Mark; the core only reads them
    public void ADR0332_the_match_mark_defaults_are_its_own()
    {
        var rules = UnconditionalRules();
        var match = Assert.Single(rules, rule => rule.Selectors.SequenceEqual([".ex-cell.ex-match-mark::before"]));
        var current = Assert.Single(rules, rule => rule.Selectors.SequenceEqual([".ex-cell.ex-match-mark-current::before"]));

        Assert.Contains("background: var(--ex-match-mark-fill, color-mix(in srgb, #ff9632 25%, transparent));", match.Body, StringComparison.Ordinal);
        Assert.Contains("background: var(--ex-match-mark-current-fill, color-mix(in srgb, #ff9632 55%, transparent));", current.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("Mark 40%", match.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("Mark 40%", current.Body, StringComparison.Ordinal);
        var (css, _) = CoreStylesheet();
        Assert.DoesNotMatch(new Regex(@"--ex-match-mark(-current)?-fill\s*:"), css);
    }

    [Fact] // ADR-0332 §4 / ADR-0068 / UX-7: under forced colours a match is a frame of its own, not the cell's outline, so on a cell both changed and matching the Change Highlight's dashed outline stands beside it
    public void ADR0332_under_forced_colours_a_match_and_a_change_stay_apart()
    {
        var (_, forced) = ForcedColorsSplit();

        // Nothing restates the mark on the cell's own outline, which the Change Highlight and the
        // states hold.
        Assert.DoesNotContain(forced, rule => rule.Selectors.Any(selector => selector.StartsWith(".ex-cell.ex-match-mark", StringComparison.Ordinal) && !selector.EndsWith("::before", StringComparison.Ordinal)));
        Assert.DoesNotContain(forced, rule => rule.Selectors.Any(selector => selector.Contains("ex-match-mark", StringComparison.Ordinal))
            && Regex.IsMatch(rule.Body, @"(?<![\w-])(outline|box-shadow)(-[\w-]+)?\s*:"));

        // A dotted border on the ::before, inset past the two-pixel band the outlines lie in, and
        // solid for the current match.
        var match = Assert.Single(forced, rule => rule.Selectors.SequenceEqual([".ex-cell.ex-match-mark::before"]));
        Assert.Matches(new Regex(@"background:\s*none;"), match.Body);
        Assert.Matches(new Regex(@"inset:\s*3px;"), match.Body);
        Assert.Matches(new Regex(@"border:\s*2px dotted Highlight;"), match.Body);
        var current = Assert.Single(forced, rule => rule.Selectors.SequenceEqual([".ex-cell.ex-match-mark-current::before"]));
        Assert.Matches(new Regex(@"border-style:\s*solid;"), current.Body);

        // The Change Highlight keeps its own, dashed outline.
        var changed = Assert.Single(forced, rule => rule.Selectors.Contains(".ex-cell.ex-changed"));
        Assert.Matches(new Regex(@"(?<![\w-])outline:\s*2px dashed Highlight;"), changed.Body);
    }
}
