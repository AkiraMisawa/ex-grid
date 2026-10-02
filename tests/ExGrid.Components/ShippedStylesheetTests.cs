using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Css;
using AngleSharp.Css.Parser;
using AngleSharp.Dom;
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
    private static IReadOnlyList<(string Path, string Text)> ShippedAssets()
    {
        var manifest = Path.Combine(AppContext.BaseDirectory, "ExGrid.staticwebassets.runtime.json");
        Assert.True(File.Exists(manifest), $"the package ships no static assets at all ({manifest})");

        using var document = JsonDocument.Parse(File.ReadAllText(manifest));
        var roots = document.RootElement.GetProperty("ContentRoots")
            .EnumerateArray().Select(root => root.GetString()!).ToList();
        Assert.NotEmpty(roots);

        var files = roots
            .Where(Directory.Exists)
            .SelectMany(root => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
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

    [Fact] // ADR-0021: the allowlist is a count anyone can check, not a claim in a comment
    public void The_module_installs_only_the_listeners_the_allowlist_names()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal));

        var listeners = Regex.Matches(script.Text, @"addEventListener\(\s*'(?<event>[a-z-]+)'")
            .Select(match => match.Groups["event"].Value)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        // keydown (the capture-phase gate), input and selectionchange (the same keyboard/editor
        // use, reporting the caret with each input and whenever it moves: ADR-0021's notes of
        // ADR-0051's second round), copy and paste (the clipboard), and the two that report a
        // pointer coming to rest, and the capture-phase mousedown and mouseup that keep a press on
        // the rows in its place among held keys (ADR-0021's note of 2026-09-27). The grid's own
        // scrolling is Blazor's @onscroll and the gutter is a ResizeObserver, so neither appears
        // here; scroll is an editor field's, heard while an edit is open so the coloured text
        // beneath it scrolls with it — the scroll-offset entry on one more element (ADR-0021's
        // note of ADR-0057, DC-51); and compositionend is the same editor listener hearing an IME
        // composition end, which comes with no input after it, so the coloured text can show again
        // (ticket 29, decided with the user 2026-09-30); and ex-press-handed-on, the one event a grid
        // pointed at through a Pointing Scope dispatches on the root of the grid that points, heard on
        // that root so the press keeps its place among the keys held there (ADR-0021's note of
        // 2026-09-30, ADR-0058, DC-54). And the seventh entry (ADR-0080): the Keyboard Field's own
        // compositionstart and compositionend, always on, so a composition on a selected cell takes
        // its place among the held keys — a second compositionend, beside the coloured text's —
        // and the root's focus, passing focus that lands on the root itself on to its field, and
        // focusout, emptying the field as it is left and ending the release of Tab when DOM focus
        // leaves the grid (ADR-0012). And the eighth (ADR-0090): change on a media query of the
        // current resolution, which tells the grid its Device Pixel when the scale or the zoom moves.
        string[] allowed = ["change", "compositionend", "compositionend", "compositionstart", "copy", "ex-press-handed-on", "focus", "focusout", "input", "keydown", "mousedown", "mousemove", "mouseleave", "mouseup", "paste", "scroll", "selectionchange"];
        Assert.Equal(allowed.OrderBy(name => name, StringComparer.Ordinal), listeners);
    }

    [Fact] // ADR-0090 / ADR-0021's eighth entry: the Device Pixel is told by a media query on the window's resolution, released on dispose
    public void The_device_pixel_is_told_by_a_resolution_query_released_on_dispose()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal));

        Assert.Contains("window.matchMedia(`(resolution: ${ratio}dppx)`)", script.Text, StringComparison.Ordinal);
        Assert.Contains("core.invokeMethodAsync('OnDevicePixelAsync', ratio)", script.Text, StringComparison.Ordinal);
        // Armed and released as one listener, so the dispose finds the one it added.
        Assert.Equal(2, Regex.Matches(script.Text, @"resolutionQuery\?\.removeEventListener\('change', armResolution\)").Count);
        Assert.Single(Regex.Matches(script.Text, @"resolutionQuery\.addEventListener\('change', armResolution\)"));
    }

    [Fact] // ADR-0053 / ADR-0021's sixth entry / MEM-4: the Layout Ceiling is told by an observer the instance disconnects
    public void The_layout_ceiling_is_observed_on_the_instances_probe_and_released_on_dispose()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal));

        // The instance's own probe, found under its own root — never the document's.
        Assert.Matches(new Regex(@"root\.querySelector\(':scope > \.ex-ceiling-probe > div'\)"), script.Text);
        Assert.Matches(new Regex(@"ceilingObserver\.observe\(ceilingProbe\)"), script.Text);
        // Reported from one place, only when the size moved.
        Assert.Single(Regex.Matches(script.Text, @"'OnLayoutCeilingAsync'"));
        Assert.Matches(new Regex(@"size\.blockSize === ceiling"), script.Text);
        // And released with the instance, beside the gutter's observer.
        Assert.Matches(new Regex(@"dispose: \(\) => \{[^}]*observer\.disconnect\(\);\s*ceilingObserver\.disconnect\(\);", RegexOptions.Singleline), script.Text);
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
        => UnconditionalRules()
            .SelectMany((rule, order) => Declarations(rule.Body)
                .Where(declared => declared.Property == property)
                .SelectMany(declared => rule.Selectors
                    .Where(selector => !selector.Contains("::", StringComparison.Ordinal) && element.Matches(selector))
                    .Select(selector => (Specificity: Specificity(selector), Order: order, declared.Value))))
            .OrderBy(candidate => candidate.Specificity)
            .ThenBy(candidate => candidate.Order)
            .Select(candidate => candidate.Value)
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

    [Fact] // ADR-0012 (2026-09-29) / ADR-0021 / MEM-4: a reveal's write is held on the root's own reveal number, observed only while held and released on dispose
    public void The_reveal_write_is_held_on_the_roots_reveal_number_only()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal));

        // One attribute of this instance's own root, nothing wider and nothing measured. The
        // module's other observer is the coloured text's, on a layer's text and colours (DC-51).
        Assert.Equal(2, Regex.Matches(script.Text, @"new MutationObserver\(").Count);
        Assert.Matches(new Regex(@"revealObserver\.observe\(root, \{ attributes: true, attributeFilter: \['data-ex-reveal'\] \}\)"), script.Text);
        // Let go as soon as the write is made or replaced, and with the instance.
        Assert.Matches(new Regex(@"const dropReveal = \(\) => \{[^}]*revealObserver\.disconnect\(\);", RegexOptions.Singleline), script.Text);
        Assert.Matches(new Regex(@"dispose: \(\) => \{[^}]*dropReveal\(\);", RegexOptions.Singleline), script.Text);
    }

    [Fact] // ADR-0021 (widened 2026-09-28) / ADR-0018: the hand-back leaves the fields beside the rows alone, found by the core's band, nothing measured
    public void The_hand_back_leaves_the_formula_bar_and_the_name_box_alone()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal));
        var reclaim = Regex.Match(script.Text, @"reclaimFocus: \(fromField\) => \{.*?\n        \},", RegexOptions.Singleline);
        Assert.True(reclaim.Success, "reclaimFocus(fromField) is not in the module");
        var body = reclaim.Value;

        // Only this root's own focus, or none, is taken back (ADR-0018)...
        Assert.Contains("root.contains(active)", body, StringComparison.Ordinal);
        Assert.Contains("active === document.body", body, StringComparison.Ordinal);
        // ...and not a field inside the band the core renders the Formula Bar and the Name Box
        // into — a Chrome's control sits inside the same band — unless the core says the gesture
        // was made in that field, or a press on the rows left its focus standing.
        Assert.Contains("active.closest('.ex-formula-bar') !== null", body, StringComparison.Ordinal);
        Assert.Contains("fromField === true", body, StringComparison.Ordinal);
        Assert.Contains("active !== staleField", body, StringComparison.Ordinal);
        // A read of document.activeElement, never of layout.
        Assert.DoesNotMatch(new Regex(@"getBoundingClientRect|offsetWidth|offsetHeight|getComputedStyle"), body);
    }

    [Fact] // ADR-0018 section 6 / ADR-0021 (added 2026-09-29) / ED-26: the keyboard comes back to an edit left standing in the allowlisted mousedown, into this root's own surface, nothing measured
    public void A_press_back_on_the_rows_brings_the_keyboard_back_in_the_existing_mousedown()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal));
        var press = Regex.Match(script.Text, @"const onPress = \(event\) => \{.*?\n    \};", RegexOptions.Singleline);
        Assert.True(press.Success, "onPress is not in the module");
        var body = press.Value;

        // Only while an edit is open here and DOM focus is outside this root (ADR-0018)...
        Assert.Contains("editing !== 'none' && !(focusAtPress instanceof Element && root.contains(focusAtPress))", body, StringComparison.Ordinal);
        // ...for a press on this grid's own rows or headings, not a nested grid's...
        Assert.Contains("isOwnRowsOrHeadings(event.target)", body, StringComparison.Ordinal);
        Assert.Matches(new Regex(@"const inOwnScroller = \(target\) => [^;]*target\.closest\('\.ex-scroller'\) === scroller;"), script.Text);
        Assert.Matches(new Regex(@"const isOwnRowsOrHeadings = \(target\) => isOwnRows\(target\)\s*\|\| \(inOwnScroller\(target\) && target\.closest\('\.ex-header'\) !== null\);"), script.Text);
        // ...into the surface that last held the keyboard, one of this grid's own.
        Assert.Contains("standingField()?.focus({ preventScroll: true })", body, StringComparison.Ordinal);
        Assert.Contains("const standingField = () => surfaceField(ownSurface(lastSurface));", script.Text, StringComparison.Ordinal);
        // Script moves DOM focus in these five places only (ADR-0021, five since ADR-0080): this,
        // the hand-back to the root or its Keyboard Field, the open edit's own focus, each only
        // while the keyboard is this grid's; the root's own focus passed on to its Keyboard Field;
        // and the field given up by a press during a composition.
        Assert.Equal(5, Regex.Matches(script.Text, @"\.(focus|blur)\(").Count);
        Assert.DoesNotMatch(new Regex(@"getBoundingClientRect|offsetWidth|offsetHeight|getComputedStyle"), body);
        // The surface is forgotten with the instance.
        Assert.Matches(new Regex(@"dispose: \(\) => \{.*lastSurface = null;", RegexOptions.Singleline), script.Text);
    }

    [Fact] // ADR-0010 (widened 2026-09-29) / ADR-0021 / ED-22: a press on the rows while an edit is open holds the keys after it until the core has answered it
    public void A_press_on_the_rows_while_editing_holds_the_keys_after_it_until_answered()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal));
        var press = Regex.Match(script.Text, @"const onPress = \(event\) => \{.*?\n    \};", RegexOptions.Singleline);
        Assert.True(press.Success, "onPress is not in the module");
        var body = press.Value;

        // The press itself passes on when there is nothing it could overtake — nothing held, and no
        // key being answered, or only a press of this grid's own, which Blazor keeps in order with it
        // (a double click's second press), never one handed on from a grid this one points at
        // (ADR-0058, DC-54) — and while an edit is open it starts the hold.
        Assert.Matches(new Regex(@"if \(held\.length === 0 && \(!answering \|\| \(pressAnswer !== null && pressAnswer !== handedOnAnswer\)\)\) \{\s*handOn\(event\);.*?if \(editing !== 'none'\) \{\s*holdBehindPress\(field !== null \? markStale\(field\) : null\);",
            RegexOptions.Singleline), body);
        // Only a press on this grid's own rows, not on a grid nested in one of its cells, is held
        // or holds the keys after it: this core never hears the nested one's press.
        Assert.Contains("if (!core || replaying || event.button !== 0 || !isOwnRows(event.target)) {", body, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"event\.target\.classList\.contains\('ex-viewport'\)"), body);
        Assert.Matches(new Regex(@"const isOwnRows = \(target\) => inOwnScroller\(target\) && target\.classList\.contains\('ex-viewport'\);"), script.Text);
        // The hold: the keys after it are held from now, as behind a key, and the drain waits for
        // the press's answer before anything else.
        Assert.Matches(new Regex(@"const holdBehindPress = \(mark\) => \{\s*askAboutPress\(\);\s*pressAnswer = new Promise\(\(resolve\) => \{\s*pressToAsk = \{ mark, resolve \};\s*\}\);\s*setTimeout\(askAboutPress\);\s*if \(!answering\) \{\s*startHold\(\);"),
            script.Text);
        // One way a hold begins, whoever begins it: a key, a move in a popover, a press into the
        // bar, a press on the rows.
        Assert.Single(Regex.Matches(script.Text, @"answering = true;"));
        Assert.Matches(new Regex(@"const startHold = \(answer\) => \{\s*answering = true;\s*holdStartedAt = performance\.now\(\);"), script.Text);
        Assert.Matches(new Regex(@"const drain = async \(\) => \{.*?while \(pressAnswer !== null\) \{\s*const answer = pressAnswer;\s*await answer;.*?await editorSettled\(\);",
            RegexOptions.Singleline), script.Text);
        // The core answers for the press or the release it heard last, so the question goes after
        // the press and ahead of its release: from a later task (above), or from this grid's own
        // release, whichever comes first.
        var release = Regex.Match(script.Text, @"const onRelease = \(event\) => \{.*?\n    \};", RegexOptions.Singleline);
        Assert.True(release.Success, "onRelease is not in the module");
        Assert.Matches(new Regex(@"^const onRelease = \(event\) => \{\s*(//[^\n]*\s*)*if \(!replaying\) \{\s*askAboutPress\(\);"), release.Value);
        Assert.Matches(new Regex(@"const askAboutPress = \(\) => \{.*?core\.invokeMethodAsync\('PressAnsweredAsync'\).*?press\.resolve\(\);",
            RegexOptions.Singleline), script.Text);
        Assert.DoesNotMatch(new Regex(@"getBoundingClientRect|offsetWidth|offsetHeight|getComputedStyle"), body);
    }

    [Fact] // ADR-0058 ("On a circuit") / ADR-0021 (note of 2026-09-30) / ADR-0018 section 7 / DC-54: a press handed on is told by one event to the root the render names, which holds its keys around it; nothing shared, nothing on the document or the window, nothing measured
    public void A_press_handed_on_is_told_to_the_pointing_root_by_one_event()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal));
        var handOn = Regex.Match(script.Text, @"const handOn = \(event\) => \{.*?\n    \};", RegexOptions.Singleline);
        Assert.True(handOn.Success, "handOn is not in the module");
        var heard = Regex.Match(script.Text, @"const onPressHandedOn = \(event\) => \{.*?\n    \};", RegexOptions.Singleline);
        Assert.True(heard.Success, "onPressHandedOn is not in the module");
        var press = Regex.Match(script.Text, @"const onPress = \(event\) => \{.*?\n    \};", RegexOptions.Singleline);
        Assert.True(press.Success, "onPress is not in the module");

        // One event, dispatched in one place: on the root this grid's own render names at the press,
        // found then and kept nowhere, for a primary press on this grid's own rows or headings.
        Assert.Single(Regex.Matches(script.Text, @"new CustomEvent\("));
        Assert.Contains("other.dispatchEvent(new CustomEvent('ex-press-handed-on', { cancelable: true, detail }))", handOn.Value, StringComparison.Ordinal);
        Assert.Contains("root.getAttribute('data-ex-pointed-from')", handOn.Value, StringComparison.Ordinal);
        Assert.Contains("root.ownerDocument.getElementById(pointing)", handOn.Value, StringComparison.Ordinal);
        Assert.Contains("event.button !== 0 || !isOwnRowsOrHeadings(event.target)", handOn.Value, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(script.Text, @"getElementById\("));
        // The whole message, built afresh for each press inside the pressing instance: when the
        // press may be answered (inTurn), and the promise of its answer, which the pressing grid's
        // own core settles (answered). The listener that hears it keeps nothing of the other grid
        // but what it queues for that one press.
        Assert.Matches(new Regex(@"const answered = new Promise\(\(resolve\) => \{\s*answer = resolve;\s*\}\);\s*const detail = \{\s*inTurn: \(\) => \{.*?\},\s*answered,\s*\};", RegexOptions.Singleline), handOn.Value);
        Assert.Contains("answer(core.invokeMethodAsync('PressHandedOnAsync', press, inTurn)", handOn.Value, StringComparison.Ordinal);
        Assert.Contains("const hand = event.detail;", heard.Value, StringComparison.Ordinal);
        // Told as the press goes on to Blazor, and only then: where it passes on untouched, and where
        // it leaves the listener — a heading's, or one replayed after it was held here.
        Assert.Equal(2, Regex.Matches(press.Value, @"handOn\(event\);").Count);
        Assert.Matches(new Regex(@"if \(!core \|\| replaying \|\| event\.button !== 0 \|\| !isOwnRows\(event\.target\)\) \{\s*handOn\(event\);\s*tellTaken\('mousedown', taken\);\s*return;"), press.Value);
        // This grid's core is told of it, and of its turn when it comes, from one place each.
        Assert.Single(Regex.Matches(script.Text, @"'PressHandedOnAsync'"));
        Assert.Single(Regex.Matches(script.Text, @"'PressInTurn'"));

        // Heard by one listener on each instance's own root, removed with the instance, which lets go
        // of a press still waiting for its turn there.
        Assert.Matches(new Regex(@"\n    root\.addEventListener\('ex-press-handed-on', onPressHandedOn\);"), script.Text);
        Assert.Matches(new Regex(@"root\.removeEventListener\('ex-press-handed-on', onPressHandedOn\);.*?for \(const k of held\) \{\s*k\.handedOn\?\.inTurn\(\);", RegexOptions.Singleline), script.Text);
        // It starts the hold a press on the rows starts: in turn at once when nothing is held or being
        // answered, its answer waited for first; otherwise in its place in the queue, behind every key
        // typed before it, and given its turn where the drain reaches it.
        Assert.Matches(new Regex(@"event\.preventDefault\(\);\s*if \(answering\) \{\s*held\.push\(\{ handedOn: hand \}\);\s*return;\s*\}\s*hand\.inTurn\(\);\s*pressAnswer = hand\.answered;\s*handedOnAnswer = pressAnswer;\s*startHold\(\);"), heard.Value);
        Assert.Matches(new Regex(@"if \(k\.handedOn\) \{\s*k\.handedOn\.inTurn\(\);\s*await k\.handedOn\.answered;\s*await editorSettled\(\);"), script.Text);

        // Nothing measured, nothing on the document or the window.
        Assert.DoesNotMatch(new Regex(@"getBoundingClientRect|offsetWidth|offsetHeight|offsetTop|offsetLeft|clientWidth|clientHeight|getComputedStyle|getClientRects"), handOn.Value + heard.Value);
        Assert.DoesNotMatch(new Regex(@"window\.addEventListener|window\.dispatchEvent|document\.dispatchEvent|document\.addEventListener\('ex-"), script.Text);
        // And no state outside an instance: the module's one top-level statement is attach, so
        // everything above lives in one instance's closure and no instance keeps another's.
        var topLevel = script.Text.Split('\n')
            .Where(line => line.Length > 0 && !char.IsWhiteSpace(line[0]) && !line.StartsWith("//", StringComparison.Ordinal)
                && !line.StartsWith("/**", StringComparison.Ordinal))
            .ToList();
        Assert.Equal(["export function attach(root, scroller, core, takenKeys, canEdit, restDelayMs, canFind, declaredKeys) {", "}"], topLevel);
    }

    [Fact] // ADR-0021 (2026-09-28/29) / ED-26: a bar a passed-on press leaves holding DOM focus is the hand-back's to take only until that press is answered
    public void A_passed_on_press_marks_the_bar_only_until_the_core_has_answered_it()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal));

        // A Formula Bar that a press on the rows leaves holding DOM focus while an edit is open is
        // the hand-back's to take, should the press commit (reclaimFocus), and only that far: the
        // mark comes off with the press's answer. A press that pointed has left the edit open, and a
        // later hand-back must not take the bar the user is typing in.
        Assert.Matches(new Regex(@"const askAboutPress = \(\) => \{.*?if \(press\.mark !== null && staleMarks === press\.mark\) \{\s*staleField = null;\s*\}",
            RegexOptions.Singleline), script.Text);
        // Every mark is numbered, a held press's too, so taking one off never takes a later one's.
        Assert.Matches(new Regex(@"const markStale = \(field\) => \{\s*staleField = field;\s*return \+\+staleMarks;"), script.Text);
        Assert.Single(Regex.Matches(script.Text, @"staleField = field;"));
    }

    [Fact] // ADR-0051 (2026-10-01) / ADR-0021 / ticket 78: the press that gives the Name Box the keyboard selects its whole text, at its release, in the listeners already attached; no focus moved, nothing measured
    public void ADR0051_the_press_that_gives_the_name_box_the_keyboard_selects_its_text()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal)).Text;
        string Body(string name)
        {
            var match = Regex.Match(script, @"const " + name + @" = \(event\) => \{.*?\n    \};", RegexOptions.Singleline);
            Assert.True(match.Success, $"{name} is not in the module");
            return match.Value;
        }

        // This grid's own Name Box, the built-in input or a Chrome's control inside the core's box,
        // never one of a grid nested in a cell, and only while it takes typing.
        Assert.Matches(new Regex(@"const ownNameBox = \(element\) => \(element instanceof HTMLInputElement && root !== null\s*&& element\.closest\('\.ex-name-box'\)\?\.closest\('\.ex-grid'\) === root && !element\.readOnly && !element\.disabled\s*\? element : null\);"),
            script);
        // The press is noted only when it gives the Name Box the keyboard: a press into it while it
        // holds DOM focus is the field's own, and places the caret or drags a selection. Any press
        // takes the first key's mark off.
        Assert.Matches(new Regex(@"const nameBox = event\.button === 0 && !replaying \? ownNameBox\(event\.target\) : null;\s*nameBoxPressed = nameBox !== document\.activeElement \? nameBox : null;\s*nameBoxSelected = null;"),
            Body("onPress"));
        // Its release selects the whole text, once the browser has given the field the keyboard,
        // and takes the release's default, which would put the caret back where the press landed.
        Assert.Matches(new Regex(@"const nameBox = nameBoxPressed;\s*nameBoxPressed = null;\s*if \(nameBox !== null && event\.button === 0 && !replaying && document\.activeElement === nameBox\) \{\s*event\.preventDefault\(\);\s*nameBox\.select\(\);\s*nameBoxSelected = nameBox;\s*\}"),
            Body("onRelease"));
        // A render that renames it before the first key writes over the selection: that key, an
        // IME's first composing key included, selects the whole name again. Any key takes the mark
        // off, and so does an input that came without a key.
        var key = Body("onKeyDown");
        Assert.Matches(new Regex(@"if \(nameBoxSelected !== null\) \{\s*if \(event\.target === nameBoxSelected\) \{\s*nameBoxSelected\.select\(\);\s*\}\s*nameBoxSelected = null;\s*\}"), key);
        Assert.True(key.IndexOf("nameBoxSelected.select();", StringComparison.Ordinal) < key.IndexOf("event.isComposing", StringComparison.Ordinal),
            "the first key's selection must come before the IME's keys are let through");
        Assert.Matches(new Regex(@"if \(event\.target === nameBoxSelected\) \{\s*nameBoxSelected = null;\s*\}"), Body("onEditorInput"));
        Assert.Matches(new Regex(@"dispose: \(\) => \{.*nameBoxPressed = null;\s*nameBoxSelected = null;", RegexOptions.Singleline), script);

        // Selected in those two places alone, by no listener of its own, and no focus is moved: the
        // press's default gives the field the keyboard (ADR-0021's decisions about focus are not
        // added to: five since ADR-0080, none of them this one's).
        Assert.Equal(2, Regex.Matches(script, @"nameBox(Selected)?\.select\(\);").Count);
        Assert.Equal(5, Regex.Matches(script, @"\.(focus|blur)\(").Count);
        Assert.DoesNotMatch(new Regex(@"getBoundingClientRect|offsetWidth|offsetHeight|offsetTop|offsetLeft|clientWidth|clientHeight|scrollWidth|scrollHeight|getComputedStyle|getClientRects"), script);
    }

    [Fact] // ADR-0018 section 6 / ED-27: an edit whose keyboard is elsewhere has a 1px outline, from the stylesheet alone, in the token's style and colour
    public void An_edit_whose_keyboard_is_elsewhere_is_drawn_with_a_1px_outline()
    {
        var assets = ShippedAssets();
        var stylesheet = assets.Single(asset => Path.GetFileName(asset.Path) == "ex-grid.css");
        var script = assets.Single(asset => Path.GetFileName(asset.Path) == "ex-grid.js");

        // While DOM focus is outside the root, the Cell Editor over the rows — not the Formula
        // Bar's field, outlined only while it has focus — is drawn with its outline 1px wide.
        var rule = Regex.Match(stylesheet.Text, @"\.ex-grid:not\(:focus-within\) \.ex-viewport \.ex-editor \{(?<body>[^}]*)\}");
        Assert.True(rule.Success, "no rule narrows the outline of an editor whose grid does not hold DOM focus");
        // The width alone: the style and the colour stay --ex-editor-outline's, a Wrapper's too,
        // and no token is added for it.
        Assert.Equal("outline-width: 1px;", rule.Groups["body"].Value.Trim());
        Assert.Matches(new Regex(@"\.ex-editor \{[^}]*outline: var\(--ex-editor-outline, 2px solid Highlight\);"), stylesheet.Text);
        // No script is involved: the module writes no outline.
        Assert.DoesNotMatch(new Regex(@"\.style\.outline|outlineWidth|setProperty\('outline"), script.Text);
    }

    [Fact] // ADR-0018 / ED-26: the surface the keyboard comes back to is this grid's own, never one of a grid nested in its cells
    public void The_keyboard_comes_back_only_to_this_grids_own_surfaces()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal));

        // A surface is this grid's when this root is the nearest grid root above it: a grid nested
        // in a cell stands nearer to its own editor, and root.contains alone would not tell them apart.
        Assert.Matches(new Regex(@"const ownSurface = \(element\) => \{[^}]*surface\.closest\('\.ex-grid'\) === root", RegexOptions.Singleline), script.Text);
        // The surface noted as holding the keyboard, and the one an edit opens in, go through it...
        Assert.Contains("lastSurface = ownSurface(target) ?? lastSurface;", script.Text, StringComparison.Ordinal);
        Assert.Contains("lastSurface = opened ? ownSurface(document.activeElement) : null;", script.Text, StringComparison.Ordinal);
        // ...and so does the first surface in the markup, taken when none is known, for the field a
        // held key is typed into as much as for the keyboard's return: one helper for both.
        Assert.Matches(new Regex(@"const surfaceField = \(surface\) => \{\s*const chosen = surface\s*\?\? \(root \? \[\.\.\.root\.querySelectorAll\('\.ex-editor'\)\]\.find\(\(box\) => ownSurface\(box\) === box\) : null\);"), script.Text);
        Assert.Contains("const editorInput = () => (focusDeclined ? standingField() : surfaceField(ownSurface(document.activeElement)));", script.Text, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"root\.querySelector\('\.ex-editor'\)"), script.Text);
    }

    [Fact] // ADR-0021 (note of 2026-09-30) / ADR-0018 section 6 / ED-28: the open edit's focus is granted only while the keyboard is this grid's, and a decline lets the held keys into the edit
    public void The_open_edits_focus_is_granted_only_while_the_keyboard_is_this_grids()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal));
        var method = Regex.Match(script.Text, @"focusEditor: \(bar, fromField\) => \{.*?\n        \},", RegexOptions.Singleline);
        Assert.True(method.Success, "focusEditor is not in the handle");
        var body = method.Value;

        // Found in the core's own box, a Chrome's control included, as the press back finds it: the
        // core holds no reference to a control it did not render (ADR-0010).
        Assert.Contains("ownSurface(b) === b", body, StringComparison.Ordinal);
        Assert.Contains("surfaceField(box)", body, StringComparison.Ordinal);
        // Granted only while DOM focus is inside this root or on nothing: reclaimFocus's condition.
        Assert.Contains("!active || active === document.body || active === document.documentElement || root.contains(active)", body, StringComparison.Ordinal);
        // Nor from a field beside the rows with focus of its own, a field a press on the rows left
        // standing aside, unless the core means to take the keyboard out of it, as the hand-back
        // leaves those fields (ADR-0021, 2026-09-28).
        Assert.Contains("active !== staleField", body, StringComparison.Ordinal);
        Assert.Contains("active.closest('.ex-formula-bar') !== null", body, StringComparison.Ordinal);
        Assert.Contains("fromField !== true", body, StringComparison.Ordinal);
        // Scrolled into view as Blazor's FocusAsync did: no preventScroll here.
        Assert.Contains("field.focus();", body, StringComparison.Ordinal);
        Assert.DoesNotContain("preventScroll", body, StringComparison.Ordinal);
        // Declined, the surface asked for is the edit's, where the held keys and a press back go.
        Assert.Contains("lastSurface = box;", body, StringComparison.Ordinal);
        Assert.Contains("focusDeclined = true", body, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"getBoundingClientRect|offsetWidth|offsetHeight|getComputedStyle"), body);
        // A decline ends the hold's wait for the editor's focus, and the held keys go into the edit
        // the keyboard last held, never to the grid it went to (ADR-0010, same day).
        Assert.Contains("editing === 'none' || editorFocused() || focusDeclined", script.Text, StringComparison.Ordinal);
        Assert.Contains("focusDeclined ? standingField()", script.Text, StringComparison.Ordinal);
    }

    /// <summary>The body of the module's <c>const name = (event) =&gt; { … };</c>, at attach's level.</summary>
    [Fact] // ED-31 / ADR-0021's note of 2026-10-02: a press on the rows is told with what it was taken against, as it goes on to Blazor or at its replay, and nothing is measured
    public void ED31_a_press_on_the_rows_is_told_with_what_it_was_taken_against()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal)).Text;
        var taken = Regex.Match(script, @"const takenAt = \(event\) => \{.*?\n    \};", RegexOptions.Singleline);
        Assert.True(taken.Success, "takenAt is not in the module");
        var press = ListenerBody(script, "onPress");
        var release = ListenerBody(script, "onRelease");
        var replay = Regex.Match(script, @"const replayPress = async \(k\) => \{.*?\n    \};", RegexOptions.Singleline);
        Assert.True(replay.Success, "replayPress is not in the module");

        // What it was taken against: the browser's offsets, the attributes the painting render wrote
        // on the Viewport, and the scroll offset (the second entry). No layout is read.
        foreach (var read in new[] { "event.offsetX", "event.offsetY", "'data-ex-first-row'", "'data-ex-sequence'", "'data-ex-layout'", "scroller.scrollLeft" })
            Assert.Contains(read, taken.Value, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"getBoundingClientRect|offsetWidth|offsetHeight|offsetTop|offsetLeft|clientWidth|clientHeight|getComputedStyle|getClientRects|elementFromPoint"), taken.Value);

        // Noted for this grid's own rows only, and never for a replay, which tells its own.
        Assert.Contains("const taken = core && !replaying && isOwnRows(event.target) ? takenAt(event) : null;", press, StringComparison.Ordinal);
        Assert.Contains("const taken = core && !replaying && isOwnRows(event.target) ? takenAt(event) : null;", release, StringComparison.Ordinal);
        // Told where the event goes on to Blazor now, and kept with it where it is held.
        Assert.Equal(2, Regex.Matches(press, @"tellTaken\('mousedown', taken\);").Count);
        Assert.Contains("held.push({ press: 'mousedown', target: event.target, init: mouseInit(event), taken });", press, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(release, @"tellTaken\('mouseup', taken\);"));
        Assert.Contains("held.push({ press: 'mouseup', target: event.target, init: mouseInit(event), taken });", release, StringComparison.Ordinal);
        // At the replay, told before the event is dispatched, so the event the core hears next is
        // the one it was told of.
        Assert.Matches(new Regex(@"tellTaken\(k\.press, k\.taken\);\s*replaying = true;\s*try \{\s*target\.dispatchEvent\(new MouseEvent\(k\.press, k\.init\)\);"), replay.Value);
        // One message, from one place.
        Assert.Single(Regex.Matches(script, @"'PressTakenAt'"));
    }

    private static string ListenerBody(string script, string name)
    {
        var match = Regex.Match(script, @"const " + name + @" = \(event\) => \{.*?\n    \};", RegexOptions.Singleline);
        Assert.True(match.Success, $"{name} is not in the module");
        return match.Value;
    }

    [Fact] // ADR-0080 / ADR-0021's seventh entry / ADR-0018: the Keyboard Field's listeners are on the root, on every grid, and go with the instance
    public void ADR0080_the_keyboard_fields_listeners_are_on_every_root_and_go_with_the_instance()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal)).Text;

        // At attach's own level, behind no condition: every grid hears them, with a field or not —
        // the release of Tab ends on a display-only grid too (ADR-0012).
        Assert.Matches(new Regex(@"\n    root\.addEventListener\('compositionstart', onKeyFieldCompositionStart, true\);\n    root\.addEventListener\('compositionend', onKeyFieldCompositionEnd, true\);"), script);
        Assert.Matches(new Regex(@"\n    root\.addEventListener\('focus', onFocused, true\);\n    root\.addEventListener\('focusout', onFocusLeft, true\);"), script);
        var dispose = Regex.Match(script, @"dispose: \(\) => \{.*?root = null;", RegexOptions.Singleline).Value;
        foreach (var removed in new[]
        {
            "root.removeEventListener('compositionstart', onKeyFieldCompositionStart, true);",
            "root.removeEventListener('compositionend', onKeyFieldCompositionEnd, true);",
            "root.removeEventListener('focus', onFocused, true);",
            "root.removeEventListener('focusout', onFocusLeft, true);",
        })
        {
            Assert.Contains(removed, dispose, StringComparison.Ordinal);
        }
        // The field is this grid's own, never a nested grid's, and stands for the root: a key, a
        // copy or a paste aimed at it is the root's (ADR-0010's guard, widened).
        Assert.Matches(new Regex(@"const isKeyField = \(target\) => target instanceof HTMLInputElement && target\.classList\.contains\('ex-key-field'\)\s*&& root !== null && target\.closest\('\.ex-grid'\) === root;"), script);
        Assert.Contains("const isRoot = (target) => target === root || (!!scroller && target === scroller) || isKeyField(target);", script, StringComparison.Ordinal);
        // Nothing measured in any of them.
        var bodies = string.Concat(new[] { "onKeyFieldCompositionStart", "onKeyFieldCompositionEnd", "onFocused", "onFocusLeft" }
            .Select(name => ListenerBody(script, name)));
        Assert.DoesNotMatch(new Regex(@"getBoundingClientRect|offsetWidth|offsetHeight|offsetTop|offsetLeft|clientWidth|clientHeight|scrollWidth|scrollHeight|getComputedStyle|getClientRects"), bodies);
    }

    [Fact] // ADR-0012 (2026-10-02) / ADR-0080 / KB-8: the release of Tab ends when DOM focus leaves the grid, told by the root's focusout, and a release answered after the departure is not granted
    public void ADR0080_the_release_of_tab_ends_when_dom_focus_leaves_the_grid()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal)).Text;
        var left = ListenerBody(script, "onFocusLeft");

        // Leaving: the next holder is nothing, or outside this root. Not the window losing focus,
        // which leaves DOM focus where it is, the document's active element still.
        Assert.Matches(new Regex(@"const next = event\.relatedTarget;\s*if \(event\.target === document\.activeElement \|\| \(next instanceof Node && root && root\.contains\(next\)\)\) \{\s*return;\s*\}\s*tabReleased = false;\s*releaseEndsHeard\+\+;"), left);
        // Counted with the presses, so a release the core answers for an Escape forwarded before the
        // departure is not granted.
        Assert.Contains("releaseEndsAtEscape = releaseEndsHeard;", script, StringComparison.Ordinal);
        Assert.Matches(new Regex(@"releaseTab: \(\) => \{\s*tabReleased = releaseEndsHeard === releaseEndsAtEscape;\s*\},"), script);
        Assert.Equal(2, Regex.Matches(script, @"releaseEndsHeard\+\+;").Count);
        Assert.Contains("releaseEndsHeard++;", ListenerBody(script, "onPress"), StringComparison.Ordinal);
    }

    [Fact] // ADR-0080 / ADR-0021 (five decisions about focus): focus on the root itself goes on to its Keyboard Field, the hand-back puts the keyboard there, and DOM focus never moves while the field composes
    public void ADR0080_the_keyboard_goes_to_the_field_and_never_moves_while_it_composes()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal)).Text;

        // The root's own focus, passed on at once, without scrolling; heard in the capture phase.
        Assert.Matches(new Regex(@"if \(event\.target === root\) \{\s*field\.focus\(\{ preventScroll: true \}\);"), ListenerBody(script, "onFocused"));
        // The hand-back: the field where the grid has one, the root where it has none.
        var reclaim = Regex.Match(script, @"reclaimFocus: \(fromField\) => \{.*?\n        \},", RegexOptions.Singleline).Value;
        Assert.Contains("(keyFieldOf() ?? root).focus({ preventScroll: true });", reclaim, StringComparison.Ordinal);
        Assert.Matches(new Regex(@"const keyFieldOf = \(\) => \{\s*const field = root \? root\.querySelector\('\.ex-key-field'\) : null;\s*return field !== null && isKeyField\(field\) \? field : null;"), script);
        // The editor's request for the keyboard waits while the field composes, and through the task
        // a composition ended in; it is granted after that (the next fact).
        var focusEditor = Regex.Match(script, @"focusEditor: \(bar, fromField\) => \{.*?\n        \},", RegexOptions.Singleline).Value;
        Assert.Matches(new Regex(@"if \(keyFieldComposing \|\| keyFieldEndTimer !== 0\) \{\s*deferredEditorFocus = \{ bar, fromField \};\s*return;\s*\}"), focusEditor);
        // A primary press during a composition ends it first, by the field giving up the keyboard,
        // before anything else the press does.
        Assert.Matches(new Regex(@"^const onPress = \(event\) => \{\s*(//[^\n]*\s*)*if \(keyFieldComposing && core && !replaying && event\.button === 0\) \{\s*keyFieldOf\(\)\?\.blur\(\);\s*\}"), ListenerBody(script, "onPress"));
        // The composition's start is told to the core only with no edit open.
        Assert.Matches(new Regex(@"if \(editing === 'none' && !answering\) \{\s*core\.invokeMethodAsync\('OnKeyFieldCompositionStartAsync'\)"), ListenerBody(script, "onKeyFieldCompositionStart"));
        Assert.Single(Regex.Matches(script, @"'OnKeyFieldTextAsync'"));
        // The document's selection is the field's caret, which the IME needs: a key on the field
        // does not drop it, as one on the root does (ADR-0021's clipboard note).
        Assert.Contains("if (k.onRoot && !isKeyField(event.target)) {", ListenerBody(script, "onKeyDown"), StringComparison.Ordinal);
    }

    [Fact] // ED-30 / ADR-0080 (the sixteenth Windows run's k6): the editor's request is never granted in the task a composition ended in, where the key that ended it may have started the next, but in the task after, and only while the field is not composing again
    public void ED30_the_editors_request_is_granted_only_after_the_task_a_composition_ended_in()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal)).Text;
        var carry = Regex.Match(script, @"const carryKeyFieldText = \(field\) => \{.*?\n    \};", RegexOptions.Singleline).Value;
        var ended = Regex.Match(script, @"const keyFieldEnded = \(\) => \{.*?\n    \};", RegexOptions.Singleline).Value;
        Assert.NotEmpty(carry);
        Assert.NotEmpty(ended);

        // The end starts the wait before anything else it does: on WebAssembly the core's answer to
        // the text — the Cell Editor opened, and its request for the keyboard — runs inside this
        // task, before the browser says whether the same key started another composition.
        Assert.Matches(new Regex(@"^const carryKeyFieldText = \(field\) => \{\s*clearTimeout\(keyFieldEndTimer\);\s*keyFieldEndTimer = setTimeout\(keyFieldEnded, 0\);\s*const text = "), carry);
        // Nothing is granted at the end itself: on WebAssembly, granted there, the request moved DOM
        // focus between one composition's end and the next one's start, and `kanji` lost its `k`.
        Assert.DoesNotContain("focusEditor", carry, StringComparison.Ordinal);
        // The task after: granted unless the field composes again, when it waits for that end.
        Assert.Matches(new Regex(@"keyFieldEndTimer = 0;\s*const pending = deferredEditorFocus;\s*if \(pending !== null && !keyFieldComposing\) \{\s*deferredEditorFocus = null;\s*handle\.focusEditor\(pending\.bar, pending\.fromField\);\s*\}"), ended);
        // The one place a waiting request is granted, and the one timer, which goes with the instance.
        Assert.Single(Regex.Matches(script, @"handle\.focusEditor\("));
        Assert.Single(Regex.Matches(script, @"keyFieldEndTimer = setTimeout\("));
        var dispose = Regex.Match(script, @"dispose: \(\) => \{.*?root = null;", RegexOptions.Singleline).Value;
        Assert.Contains("clearTimeout(keyFieldEndTimer);", dispose, StringComparison.Ordinal);
        // No listener of its own, and nothing measured (ADR-0021's seventh entry).
        Assert.DoesNotMatch(new Regex(@"addEventListener"), carry + ended);
        Assert.DoesNotMatch(new Regex(@"getBoundingClientRect|offsetWidth|offsetHeight|offsetTop|offsetLeft|clientWidth|clientHeight|scrollWidth|scrollHeight|getComputedStyle|getClientRects"), carry + ended);
    }

    [Fact] // KB-12 / ADR-0080: the root's ring is drawn from the script's mark while its field holds a keyboard that did not come of a press on the grid, as from :focus-visible on a grid without one
    public void KB12_the_roots_ring_is_drawn_from_the_scripts_mark_on_a_grid_that_edits()
    {
        var assets = ShippedAssets();
        var script = assets.Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal)).Text;
        var (_, rules) = CoreStylesheet();

        // One rule draws the ring for both, so they cannot drift apart; :focus-visible stays for a
        // grid without a field.
        var (selectors, body) = Assert.Single(rules, rule => rule.Selectors.Contains(".ex-grid:focus-visible"));
        Assert.Equal([".ex-grid:focus-visible", ".ex-grid[data-ex-focus-visible]"], selectors.Order(StringComparer.Ordinal));
        Assert.Contains("outline: var(--ex-grid-focus-outline, 2px solid Highlight);", body, StringComparison.Ordinal);
        // The field draws no ring of its own.
        Assert.Contains(rules, rule => rule.Selectors.SequenceEqual([".ex-key-field"]) && Regex.IsMatch(rule.Body, @"outline:\s*none;"));

        // An attribute, set in one place: the root's class is the core's render, and a render
        // rewrites it whole.
        Assert.Single(Regex.Matches(script, @"'data-ex-focus-visible'"));
        Assert.Contains("root.toggleAttribute('data-ex-focus-visible', on);", script, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"classList\.(add|toggle)\('ex-focus-visible'"), script);
        // Marked as the field takes the keyboard, from how it came; unmarked as the field gives it up,
        // and with the instance.
        Assert.Matches(new Regex(@"\} else if \(event\.target === field\) \{\s*markFocusVisible\(!keyboardByPress\);"), ListenerBody(script, "onFocused"));
        Assert.Matches(new Regex(@"if \(isKeyField\(event\.target\)\) \{\s*settleKeyField\(\);\s*markFocusVisible\(false\);"), ListenerBody(script, "onFocusLeft"));
        Assert.Matches(new Regex(@"dispose: \(\) => \{.*?markFocusVisible\(false\);.*?root = null;", RegexOptions.Singleline), script);
        // A press on the grid says the keyboard came by a press; any key, or DOM focus leaving the
        // grid — Tab back in is the keyboard's — says it did not. Read off the listeners already
        // allowlisted: no listener of its own.
        Assert.Contains("keyboardByPress = true;", ListenerBody(script, "onPress"), StringComparison.Ordinal);
        Assert.Matches(new Regex(@"^const onKeyDown = \(event\) => \{\s*if \(!core\) \{\s*return;\s*\}\s*(//[^\n]*\s*)*keyboardByPress = false;"), ListenerBody(script, "onKeyDown"));
        Assert.Matches(new Regex(@"releaseEndsHeard\+\+;\s*keyboardByPress = false;"), ListenerBody(script, "onFocusLeft"));
        Assert.Equal(3, Regex.Matches(script, @"(?<!let )keyboardByPress = (true|false);").Count);
    }

    [Fact] // ADR-0037 / KB-26: a held Space engages once — the gate takes and drops a repeated plain Space
    public void The_key_gate_drops_a_repeated_space()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal));

        // The Definition of Done states this one as an inspection, and this is it: the
        // filter has to exist in the capture-phase listener, because auto-repeat is only
        // visible there — by the time a key reaches .NET, a repeat looks like a press.
        // Layer 3 holds the key for real.
        Assert.Matches(new Regex(@"canonical === ' ' && k\.repeat"), script.Text);
    }

    [Fact] // ADR-0051/0021 / DC-24 / DC-45: the key message carries the editor's text and selection, read from the field, nothing measured
    public void The_key_message_carries_the_editors_text_and_caret()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal));

        // The inspection DC-24 names: the one message to OnKeyAsync, with the value and the
        // selection of the editor surface, and whether the user moved the caret in that very
        // text (F4, ADR-0051 2026-09-29) — reads of the field and of the listener's own note,
        // no layout read.
        Assert.Single(Regex.Matches(script.Text, @"'OnKeyAsync'"));
        Assert.Matches(new Regex(@"'OnKeyAsync'[^;]*input \? input\.value : null, input \? \(input\.selectionStart \?\? input\.value\.length\) : -1,\s*input \? \(input\.selectionEnd \?\? input\.value\.length\) : -1, input \? movedByUser\(input\) : false\)",
            RegexOptions.Singleline), script.Text);
        Assert.DoesNotMatch(new Regex(@"getBoundingClientRect|offsetWidth|offsetHeight|getComputedStyle"), script.Text);
    }

    [Fact] // ADR-0051 (2026-09-29) / ADR-0021 / DC-45 / DC-24: F4 is claimed only while an edit is open, and only where C# says the Consumer declared what it does
    public void The_gate_claims_F4_only_while_editing_and_only_when_declared()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal));
        var gate = Regex.Match(script.Text, @"const gate = \(k\) => \{.*?\n    \};", RegexOptions.Singleline);
        Assert.True(gate.Success, "the gate is not in the module");

        // Named once in the whole module, in the gate's editing branch: after the branch for no
        // edit open, which returns on its last line — F4 is the browser's there (DC-45).
        Assert.Single(Regex.Matches(script.Text, @"'F4'"));
        var noEditReturns = gate.Value.IndexOf("return canonical === ' ' || canonical === 'Backspace' ? 'mode' : 'core';", StringComparison.Ordinal);
        Assert.True(noEditReturns > 0, "the gate's branch for no edit open has changed shape");
        Assert.True(gate.Value.IndexOf("'F4'", StringComparison.Ordinal) > noEditReturns);
        Assert.Matches(new Regex(@"if \(claimed\.has\(canonical\) \|\| \(cycleReferences && canonical === 'F4'\)\) \{\s*return 'mode';\s*\}"), gate.Value);
        // Told by C# with the editing mode, per instance, off until told.
        Assert.Matches(new Regex(@"let cycleReferences = false;"), script.Text);
        Assert.Matches(new Regex(@"setEditing: \(mode, reportsCaret, cyclesReferences\) => \{[^}]*cycleReferences = cyclesReferences === true;", RegexOptions.Singleline), script.Text);
    }

    [Fact] // ADR-0007 / KB-39 / DC-30: the gate takes undo and redo only from C#'s list, and never while editing
    public void The_history_keys_are_claimed_only_through_the_cores_list()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal));

        // The listener names no history key of its own: they reach it only in the set C#
        // hands it, at attach or re-told through setClaims, and that set is consulted only
        // while no edit is open. While one is, the editing sets decide, and they carry none.
        Assert.DoesNotMatch(new Regex(@"'Control\+(Shift\+)?[zZyY]'"), script.Text);
        Assert.Matches(new Regex(@"setClaims: \(takenKeys, editable, findable, declaredKeys\) => \{\s*taken = new Set\(takenKeys\);"), script.Text);
        Assert.Matches(new Regex(@"if \(!taken\.has\(canonical\)\)"), script.Text);
    }

    [Fact] // ADR-0050 item 14 / ADR-0021 / DC-57 / DC-24: the gate claims declared keys only from C#'s list, beside the editor's own, and names none itself
    public void The_gate_claims_declared_keys_only_from_the_cores_list()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal));
        var gate = Regex.Match(script.Text, @"const gate = \(k\) => \{.*?\n    \};", RegexOptions.Singleline);
        Assert.True(gate.Success, "the gate is not in the module");

        // Handed at attach and re-told with the claims, per instance.
        Assert.Matches(new Regex(@"export function attach\([^)]*, declaredKeys\) \{"), script.Text);
        Assert.Matches(new Regex(@"let declared = new Set\(declaredKeys \?\? \[\]\);"), script.Text);
        Assert.Matches(new Regex(@"setClaims: \(takenKeys, editable, findable, declaredKeys\) => \{[^}]*declared = new Set\(declaredKeys \?\? \[\]\);", RegexOptions.Singleline), script.Text);
        // With no edit open they are among the taken keys, and only a taken one is asked about: the
        // Consumer answers it, and may open a popover or a frame of its own (Format Cells' Ctrl+1),
        // so the keys after it are held until that answer, and then until the keyboard has arrived
        // where the answer sent it (ADR-0050 item 16 and ADR-0039, 2026-10-01). While an edit is
        // open it is answered as the core's, which changes no mode and holds no key after it.
        var noEditReturns = gate.Value.IndexOf("return canonical === ' ' || canonical === 'Backspace' ? 'mode' : 'core';", StringComparison.Ordinal);
        var takenAt = gate.Value.IndexOf("if (!taken.has(canonical))", StringComparison.Ordinal);
        var heldAt = gate.Value.IndexOf("if (declared.has(canonical)) {\n                return 'mode';", StringComparison.Ordinal);
        Assert.True(takenAt > 0 && heldAt > takenAt && heldAt < noEditReturns, "a declared key with no edit open is not held behind until it is answered");
        Assert.Matches(new Regex(@"if \(declared\.has\(canonical\)\) \{\s*return 'core';\s*\}"), gate.Value[noEditReturns..]);
        // The module names no formatting key of its own: they reach it only in C#'s list.
        Assert.DoesNotMatch(new Regex(@"'Control\+(Shift\+)?[bBiIuU2-5~!@#$%^&_]'"), script.Text);
    }

    [Fact] // ADR-0051 second round / ADR-0021 / DC-24 / DC-31: the caret is reported with each input and whenever it moves, and set when the core says, nothing measured
    public void The_listener_reports_and_places_the_caret()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal));

        // Reported: from one place, with the field's value and selection start, never twice
        // running the same, and only while C# asked for it (DC-1: a grid declaring neither
        // completion nor pointing sends nothing more).
        Assert.Single(Regex.Matches(script.Text, @"'OnEditorCaretAsync'"));
        Assert.Matches(new Regex(@"const caret = input\.selectionStart \?\? input\.value\.length;\s*if \(input\.value === reportedText && caret === reportedCaret\) \{\s*return;",
            RegexOptions.Singleline), script.Text);
        Assert.Matches(new Regex(@"'OnEditorCaretAsync', input\.value, caret, movedByUser\(input\)\)"), script.Text);
        Assert.Matches(new Regex(@"reportCaret = reportsCaret === true;"), script.Text);

        // With each input in an editor surface: on the instance root, removed on dispose.
        Assert.Matches(new Regex(@"if \(!reportCaret \|\|[^;]*closest\('\.ex-editor'\) === null\)", RegexOptions.Singleline), script.Text);
        Assert.Matches(new Regex(@"root\.addEventListener\('input', onEditorInput, true\)"), script.Text);
        Assert.Matches(new Regex(@"root\.removeEventListener\('input', onEditorInput, true\)"), script.Text);

        // Whenever it moves: selectionchange fires on the document only, so it acts only while
        // DOM focus is in an editor surface inside this instance's root (ADR-0018), coalesced
        // to one report per animation frame, and is removed — and its frame cancelled — on
        // dispose.
        Assert.Single(Regex.Matches(script.Text, @"addEventListener\('selectionchange'"));
        Assert.Matches(new Regex(@"document\.addEventListener\('selectionchange', onSelectionChange\);"), script.Text);
        Assert.Matches(new Regex(@"document\.removeEventListener\('selectionchange', onSelectionChange\);\s*cancelAnimationFrame\(caretFrame\);"), script.Text);
        Assert.Matches(new Regex(@"root\.contains\(active\) && active\.closest\('\.ex-editor'\) !== null"), script.Text);
        Assert.Matches(new Regex(@"if \(!reportCaret \|\| !core \|\| caretFrame !== 0 \|\| focusedEditorField\(\) === null\) \{\s*return;\s*\}\s*caretFrame = requestAnimationFrame\(",
            RegexOptions.Singleline), script.Text);
        // No other document-level listener: the rest stay on the instance root.
        Assert.Single(Regex.Matches(script.Text, @"document\.addEventListener\("));

        // Placed: only while the surface still holds the text the core wrote, and the caret it
        // placed is not reported back. Not over the user's own move in that text, made before
        // the placement came: that caret stands, and is reported again as the user's.
        // The same call places the selection F4's rewrite answered (ADR-0051, 2026-09-29). It is
        // brought into view first (ticket 75; A_caret_placed_by_the_core_is_brought_into_view).
        Assert.Matches(new Regex(@"setCaret: \(text, caret, end\) => \{\s*const input = editorInput\(\);\s*if \(input && input\.value === text\) \{\s*if \(movedByUser\(input\)\) \{\s*reportedText = null;\s*reportCaretOf\(input\);\s*return;\s*\}\s*(?://[^\n]*\n\s*)*showCaret\(input, end\);\s*input\.setSelectionRange\(caret, end\);\s*reportedText = text;\s*reportedCaret = caret;",
            RegexOptions.Singleline), script.Text);
        // The user's move: a press in an editor surface's text, or a caret key left to it or
        // answered by the listener on an Apple platform (ticket 32) — only while the field still
        // holds the text it was made in.
        Assert.Matches(new Regex(@"const movedByUser = \(input\) => caretMoved !== null && caretMoved\.input === input && caretMoved\.text === input\.value;"), script.Text);
        Assert.Equal(4, Regex.Matches(script.Text, @"noteCaretMove\((event\.target|input)\);").Count);
        Assert.DoesNotMatch(new Regex(@"getBoundingClientRect|offsetWidth|offsetHeight|offsetTop|offsetLeft|clientWidth|clientHeight|getComputedStyle|getClientRects"), script.Text);
    }

    [Fact] // ADR-0058 (the thirteenth Windows run, seen and not asked) / ADR-0021 / ticket 75 / DC-48: a caret placed from script at the text's end is brought into view by the field's scroll offset, which the browser clamps; nothing is measured
    public void A_caret_placed_by_the_core_is_brought_into_view()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal)).Text;

        // One way to show a caret placed from script, which the browser does not bring into view
        // itself: at the start, the field's start; at the end, the offset past the far end, which
        // the browser clamps. Short of the end nothing is set: the place would need the width of
        // the text before the caret. The field's value is the only thing read, and nothing is
        // written to it.
        Assert.Matches(new Regex(@"const showCaret = \(input, at\) => \{\s*if \(at <= 0\) \{\s*input\.scrollLeft = 0;\s*\} else if \(at >= input\.value\.length\) \{\s*input\.scrollLeft = Number\.MAX_SAFE_INTEGER;\s*\}\s*\};"),
            script);
        // Every Point write — an arrow, Home, a Shift+arrow, a press on the Sheet or on a grid of its
        // Pointing Scope — and F4's rewrite, an accepted candidate and an edit's opening reach the
        // field through setCaret, which shows the moving end of what it places (CaretTests,
        // CompletionOverPointTests, PointWrittenFromOutsideTests); Home and End the listener answers
        // go the same way, and so does a key held while a mode change was answered, replayed into
        // the field from script (found by ticket 75's test on the Server host). Nothing else sets a
        // field's offset.
        Assert.Matches(new Regex(@"showCaret\(input, end\);\s*input\.setSelectionRange\(caret, end\);"), script);
        Assert.Matches(new Regex(@"showCaret\(input, edge\);\s*\};"), script);
        Assert.Matches(new Regex(@"input\.setSelectionRange\(at, at\);\s*showCaret\(input, at\);\s*return true;"), script);
        Assert.Matches(new Regex(@"showCaret\(input, input\.selectionEnd\);\s*(?://[^\n]*\n\s*)*input\.dispatchEvent\(new Event\('input', \{ bubbles: true \}\)\);\s*return true;\s*\};"), script);
        Assert.Equal(4, Regex.Matches(script, @"showCaret\(input, (end|edge|at|input\.selectionEnd)\);").Count);
        Assert.Equal(4, Regex.Matches(script, @"showCaret\(").Count);
        Assert.Equal(2, Regex.Matches(script, @"input\.scrollLeft = ").Count);
        // The coloured layer follows the offset through the field's own scroll event, as it does
        // when the user types (DC-48).
        Assert.Matches(new Regex(@"const onFieldScroll = \(event\) => \{\s*const field = event\.target;\s*const layer = [^;]*referenceTextOf\(field\) : null;\s*if \(layer !== null\) \{\s*layer\.firstElementChild\.scrollLeft = field\.scrollLeft;\s*\}\s*\};"),
            script);
        Assert.DoesNotMatch(new Regex(@"getBoundingClientRect|offsetWidth|offsetHeight|offsetTop|offsetLeft|clientWidth|clientHeight|scrollWidth|scrollHeight|getComputedStyle|getClientRects|measureText"), script);
    }

    [Fact] // ADR-0057 / ADR-0021 / DC-51 / DC-47: the coloured text shows only in the focused surface and while the layer's text is the field's value, one class set, nothing measured
    public void The_listener_gates_the_coloured_text_on_its_text_and_sets_one_class()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal));

        // The comparison: the layer's text against the field's value, never while no edit is open,
        // only in the surface the edit is in — the field holding DOM focus, as Excel colours only
        // that one — and never over a field composing. One class, set in one place and taken away
        // only when the edit closes; the layer is coloured exactly while it is set.
        Assert.Matches(new Regex(@"const shown = editing !== 'none' && field === document\.activeElement && composingIn !== field\s*&& layer\.getAttribute\('data-ex-text'\) === field\.value;\s*field\.classList\.toggle\('ex-reference-text-shown', shown\);\s*if \(shown\) \{\s*colour\(layer\);\s*\} else \{\s*uncolour\(layer\);\s*\}"),
            script.Text);
        Assert.Single(Regex.Matches(script.Text, @"classList\.toggle\('ex-reference-text-shown'"));
        Assert.DoesNotMatch(new Regex(@"classList\.add\('ex-reference-text-shown'"), script.Text);
        Assert.Matches(new Regex(@"field\.classList\.remove\('ex-reference-text-shown'\);"), script.Text);
        // The layer is the field's own: the element immediately before it, found by the core's
        // class — never another instance's, never measured.
        Assert.Matches(new Regex(@"const layer = field\.previousElementSibling;\s*return layer !== null && layer\.classList\.contains\('ex-reference-text'\) \? layer : null;"),
            script.Text);
        Assert.Matches(new Regex(@"for \(const layer of root\.getElementsByClassName\('ex-reference-text'\)\)"), script.Text);

        // On each input — from the editor listener itself, which reads the composition off the
        // event — ... (first of all, but for an input into the Keyboard Field, which is no editor
        // surface and has no layer: that one is the field's own, ADR-0080)...
        Assert.Matches(new Regex(@"const onEditorInput = \(event\) => \{\s*(?://[^\n]*\s*)*if \(isKeyField\(event\.target\)\) \{.*?\s*return;\s*\}\s*heardReferenceInput\(event\);", RegexOptions.Singleline),
            script.Text);
        Assert.Matches(new Regex(@"composingIn = event\.isComposing === true \? field : null;"), script.Text);
        // ...when the layer's text or its colours change: two attributes, in this root, observed
        // only while an edit is open and let go when it closes or the instance goes...
        Assert.Matches(new Regex(@"referenceTextObserver\.observe\(root, \{ attributes: true, attributeFilter: \['data-ex-text', 'data-ex-colours'\], subtree: true \}\);"), script.Text);
        Assert.Single(Regex.Matches(script.Text, @"referenceTextObserver\.observe\("));
        Assert.Matches(new Regex(@"referenceTextObserver\.disconnect\(\);"), script.Text);
        Assert.Matches(new Regex(@"setEditing: \(mode, reportsCaret, cyclesReferences\) => \{(?:(?!\n        \},).)*?watchReferenceTexts\(mode !== 'none'\);", RegexOptions.Singleline), script.Text);
        Assert.Matches(new Regex(@"dispose: \(\) => \{.*?watchReferenceTexts\(false\);.*?root = null;", RegexOptions.Singleline), script.Text);
        // ...and whenever the selection moves while it is open, as it does when DOM focus moves
        // from one surface into the other.
        Assert.Matches(new Regex(@"const onSelectionChange = \(\) => \{\s*if \(watchingReferenceTexts\) \{\s*gateReferenceTexts\(\);\s*\}"), script.Text);

        // The layer's line scrolls with its field: the field's scroll offset read, the line's set
        // — the scroll-offset entry — on the field's scroll, heard on this root while an edit is
        // open, and after each comparison.
        Assert.Equal(2, Regex.Matches(script.Text, @"layer\.firstElementChild\.scrollLeft = field\.scrollLeft;").Count);
        Assert.Matches(new Regex(@"root\.addEventListener\('scroll', onFieldScroll, true\);"), script.Text);
        Assert.Matches(new Regex(@"root\.removeEventListener\('scroll', onFieldScroll, true\);"), script.Text);

        // An IME composition's end, which comes with no input after it: heard on this root in the
        // capture phase while an edit is open, as the field's scroll is, and let go with it. It
        // clears the composing mark and compares again; nothing else.
        Assert.Matches(new Regex(@"root\.addEventListener\('scroll', onFieldScroll, true\);\s*root\.addEventListener\('compositionend', onCompositionEnd, true\);"), script.Text);
        Assert.Matches(new Regex(@"root\.removeEventListener\('scroll', onFieldScroll, true\);\s*root\.removeEventListener\('compositionend', onCompositionEnd, true\);"), script.Text);
        Assert.Single(Regex.Matches(script.Text, @"addEventListener\('compositionend', onCompositionEnd, true\)"));
        var watch = Regex.Match(script.Text, @"const watchReferenceTexts = \(on\) => \{.*?\n    \};", RegexOptions.Singleline);
        Assert.True(watch.Success, "watchReferenceTexts is not in the module");
        Assert.Contains("root.addEventListener('compositionend', onCompositionEnd, true);", watch.Value, StringComparison.Ordinal);
        Assert.Matches(new Regex(@"const onCompositionEnd = \(event\) => \{\s*const field = event\.target;\s*const layer = [^;]*referenceTextOf\(field\) : null;\s*if \(layer !== null\) \{\s*composingIn = null;\s*gateReferenceText\(field, layer\);\s*\}\s*\};"),
            script.Text);
        // The module's one other compositionend is the Keyboard Field's (ADR-0080), always on, for a
        // composition with no edit open: told apart by its handler, which hears only this grid's own
        // field and touches neither the composing mark nor any layer.
        var compositionEnds = Regex.Matches(script.Text, @"addEventListener\('compositionend', (?<handler>\w+), true\)")
            .Select(match => match.Groups["handler"].Value)
            .Order(StringComparer.Ordinal)
            .ToList();
        Assert.Equal(["onCompositionEnd", "onKeyFieldCompositionEnd"], compositionEnds);
        var fieldEnd = Regex.Match(script.Text, @"const onKeyFieldCompositionEnd = \(event\) => \{.*?\n    \};", RegexOptions.Singleline);
        Assert.True(fieldEnd.Success, "onKeyFieldCompositionEnd is not in the module");
        Assert.Contains("!isKeyField(field)", fieldEnd.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("composingIn", fieldEnd.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("gateReferenceText", fieldEnd.Value, StringComparison.Ordinal);

        // The colours (ADR-0057 and ADR-0021, notes of 2026-10-01; DC-51): one Range per stretch the
        // core wrote on the layer, over its one run of text, in the highlight of that name, which
        // this instance registers itself and takes back with it. The names are the core's, carrying
        // the grid's id; the script makes up none, and keeps no highlight another grid made.
        Assert.Matches(new Regex(@"const colours = layer\.getAttribute\('data-ex-colours'\) \?\? '';"), script.Text);
        Assert.Matches(new Regex(@"const run = layer\.firstElementChild\?\.firstChild;"), script.Text);
        Assert.Matches(new Regex(@"const \[start, length, name\] = stretch\.split\(','\);"), script.Text);
        Assert.Single(Regex.Matches(script.Text, @"new Highlight\(\)"));
        Assert.Matches(new Regex(@"highlights\.set\(name, highlight\);\s*CSS\.highlights\.set\(name, highlight\);"), script.Text);
        Assert.Single(Regex.Matches(script.Text, @"CSS\.highlights\.set\("));
        Assert.Matches(new Regex(@"range\.setStart\(run, from\);\s*range\.setEnd\(run, to\);\s*highlight\.add\(range\);"), script.Text);
        Assert.Matches(new Regex(@"for \(const \[highlight, range\] of coloured\.ranges\) \{\s*highlight\.delete\(range\);"), script.Text);
        Assert.Matches(new Regex(@"dispose: \(\) => \{.*?for \(const \[name, highlight\] of highlights\) \{\s*if \(CSS\.highlights\.get\(name\) === highlight\) \{\s*CSS\.highlights\.delete\(name\);", RegexOptions.Singleline), script.Text);
        Assert.Single(Regex.Matches(script.Text, @"CSS\.highlights\.delete\("));
        // Closing the edit takes every colour away with the class.
        Assert.Matches(new Regex(@"for \(const layer of \[\.\.\.colouredLayers\.keys\(\)\]\) \{\s*uncolour\(layer\);"), script.Text);

        // No layout is read anywhere in the module.
        Assert.DoesNotMatch(new Regex(@"getBoundingClientRect|offsetWidth|offsetHeight|offsetTop|offsetLeft|clientWidth|clientHeight|scrollWidth|scrollHeight|getComputedStyle|getClientRects"),
            script.Text);
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

    [Fact] // ADR-0051 second round / DC-31, ADR-0058 / SH-36: pointing claims the Shift+arrows; an open list claims only ↑/↓ beside the editing keys, and ←, →, Home, End and the Shift+arrows too while it is open over Point
    public void The_gate_has_a_point_set_and_a_completion_set()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal));

        Assert.Matches(new Regex(@"const pointKeys = new Set\(\[\s*\.\.\.overwriteKeys, 'Shift\+ArrowUp', 'Shift\+ArrowDown', 'Shift\+ArrowLeft', 'Shift\+ArrowRight'\]\);"),
            script.Text);
        Assert.Matches(new Regex(@"const completionKeys = new Set\(\[\.\.\.editingKeys, 'ArrowUp', 'ArrowDown'\]\);"), script.Text);
        // ADR-0058 (the tenth Windows run; Part B of the ninth, Q51) / SH-36: a list open over Point
        // takes only ↑, ↓, Tab and Escape, and ←, →, Home, End and the four Shift+arrows are claimed
        // beside them, to point; nothing else.
        Assert.Matches(new Regex(
            @"const completionOverPointKeys = new Set\(\[\s*\.\.\.completionKeys, 'ArrowLeft', 'ArrowRight', 'Home', 'End',\s*"
            + @"'Shift\+ArrowUp', 'Shift\+ArrowDown', 'Shift\+ArrowLeft', 'Shift\+ArrowRight'\]\);"),
            script.Text);
        // ADR-0058 ("The keyboard") / SH-35: Point written from outside has a set of its own beside them.
        Assert.Matches(new Regex(@"const claimedWhile = \{\s*overwrite: overwriteKeys, point: pointKeys, pointed: pointedKeys, completion: completionKeys,\s*completionOverPoint: completionOverPointKeys,\s*\};"), script.Text);
        // A list painted is open from its own render, before the gate is told (ADR-0051/0010):
        // read off the marks the core writes on the list's box, and nothing measured — whether it
        // is open over Point too.
        Assert.Matches(new Regex(@"const listShown = \(\) => \(root \? root\.querySelector\('\.ex-completion\[data-ex-list\]'\) : null\);"), script.Text);
        Assert.Matches(new Regex(@"const list = listShown\(\);\s*const claimed = list === null\s*\? \(claimedWhile\[editing\] \?\? editingKeys\)\s*: \(list\.hasAttribute\('data-ex-over-point'\) \? completionOverPointKeys : completionKeys\);"), script.Text);
        Assert.Single(Regex.Matches(script.Text, @"listShown\(\)"));
    }

    [Fact] // ADR-0058 ("The keyboard") / SH-35: while what Point wrote came from outside, the gate claims Point's keys and the Primary Modifier's arrows, with Shift or without, and nothing else
    public void The_gate_has_a_set_for_point_written_from_outside()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal)).Text;

        Assert.Matches(new Regex(
            @"const pointedKeys = new Set\(\[\s*\.\.\.pointKeys, 'Control\+ArrowUp', 'Control\+ArrowDown', 'Control\+ArrowLeft', 'Control\+ArrowRight',\s*"
            + @"'Control\+Shift\+ArrowUp', 'Control\+Shift\+ArrowDown', 'Control\+Shift\+ArrowLeft', 'Control\+Shift\+ArrowRight'\]\);"),
            script);
        Assert.Contains("pointed: pointedKeys", script, StringComparison.Ordinal);
    }

    [Fact] // ADR-0010's note of 2026-09-30 / ticket 32 / DC-24: on Apple platforms Home and End left to an editor field are answered by the listener, and on every platform PageUp and PageDown are taken, so nothing scrolls the grid away from an open edit
    public void On_apple_platforms_the_listener_answers_the_keys_macOS_scrolls_with()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal)).Text;

        // Apple platforms, by the same test that makes Meta the Primary Modifier.
        Assert.Matches(new Regex(@"const metaIsPrimary = /mac\|iphone\|ipad\|ipod/i\.test\(platform\);"), script);
        // The keys macOS binds to a scroll in a text field (Playwright's macOS key-binding table:
        // Home, End, PageUp and PageDown scroll; Shift+Home and Shift+End move the selection),
        // and nothing else.
        Assert.Matches(new Regex(@"const appleCaretKeys = new Set\(\['Home', 'End', 'Shift\+Home', 'Shift\+End'\]\);"), script);
        // PageUp and PageDown scroll the grid from an editor field on every platform, and are taken
        // on every platform (decided with the user, 2026-09-30).
        Assert.Matches(new Regex(@"const pageKeys = new Set\(\['PageUp', 'PageDown'\]\);"), script);
        // Only a key the core has not claimed — Home and End in Overwrite and Point stay the
        // core's — and only in an editor field.
        Assert.Matches(new Regex(@"if \(claimed\.has\(canonical\) \|\| \(cycleReferences && canonical === 'F4'\)\) \{\s*return 'mode';\s*\}\s*(?://[^\n]*\n\s*)+if \(k\.inEditor\) \{\s*if \(metaIsPrimary && appleCaretKeys\.has\(canonical\)\) \{\s*return 'caret';\s*\}\s*if \(pageKeys\.has\(canonical\)\) \{\s*return 'drop';\s*\}\s*\}\s*return null;"),
            script);
        Assert.Single(Regex.Matches(script, @"return 'caret';"));
        // Answered as Windows and Linux answer them: the caret to the text's start or end, or
        // the selection extended there from its anchor; the user's own move; the field scrolled
        // to that end by setting its offset, clamped by the browser. Nothing is read but the
        // field's value and selection.
        Assert.Matches(new Regex(@"const placeCaretAtEnd = \(input, k, ownMove = true\) => \{\s*const toEnd = k\.key === 'End';\s*const edge = toEnd \? input\.value\.length : 0;\s*if \(ownMove\) \{\s*noteCaretMove\(input\);\s*\}"), script);
        // A held Home or End replayed on any platform goes the same way, scrolled to its end: setting
        // the selection alone moved no view (ticket 32, seen in CI on the Server host).
        Assert.Matches(new Regex(@"\} else if \(k\.key === 'Home' \|\| k\.key === 'End'\) \{[^}]*placeCaretAtEnd\(input, k, input\.closest\('\.ex-editor'\) !== null\);\s*return true;", RegexOptions.Singleline), script);
        Assert.Matches(new Regex(@"const anchor = input\.selectionDirection === 'backward' \? input\.selectionEnd : input\.selectionStart;"), script);
        Assert.Matches(new Regex(@"input\.setSelectionRange\(anchor \?\? edge, edge, 'forward'\);"), script);
        Assert.Matches(new Regex(@"input\.setSelectionRange\(edge, anchor \?\? edge, 'backward'\);"), script);
        Assert.Matches(new Regex(@"input\.setSelectionRange\(edge, edge\);\s*\}\s*showCaret\(input, edge\);\s*\};"), script);
        // From the keydown, and for a key held behind a mode change.
        Assert.Matches(new Regex(@"if \(verdict === 'caret'\) \{[^}]*placeCaretAtEnd\(input, k\);", RegexOptions.Singleline), script);
        Assert.Matches(new Regex(@"\} else if \(verdict === 'caret'\) \{\s*const input = editorInput\(\);\s*if \(input\) \{\s*placeCaretAtEnd\(input, rebased\);"), script);
        Assert.DoesNotMatch(new Regex(@"getBoundingClientRect|offsetWidth|offsetHeight|offsetTop|offsetLeft|clientWidth|clientHeight|scrollWidth|scrollHeight|getComputedStyle|getClientRects"), script);
    }
}
