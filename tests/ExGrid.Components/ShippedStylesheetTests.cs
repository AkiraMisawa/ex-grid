using ExGrid.Components.Tests.Support;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Css;
using AngleSharp.Css.Parser;
using AngleSharp.Dom;
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
        // leaves the grid (ADR-0012), and keyup, which says that a key the IME took in the field has
        // been told whole, so the editor's request for the keyboard is granted in order with the IME's
        // events rather than a moment after a composition's end (ADR-0080, 2026-10-12). And the eighth (ADR-0090): change on a media query of the
        // current resolution, which tells the grid its Device Pixel when the scale or the zoom moves.
        // And the clipboard entry once more: copy, cut and paste in the capture phase, taking the
        // event a clipboard key fires while a hold stands into its place among the held keys
        // (ADR-0010's note of 2026-10-02) — the same events, no new use. And the clipboard entry a
        // last time (ADR-0170): clipboardchange on navigator.clipboard, which tells the grid that the
        // clipboard changed by a write other than its own copy, so the Copied Range can go.
        // ADR-0210: a temporary blur, change and keydown listener only on a field changed by held
        // typing (keydown since 2026-10-17, for an Enter typed there once the hold is over).
        // ADR-0240 section 2: the capture-phase click, dblclick and contextmenu on the root, which number a
        // click on a control of the grid's own that claims it, and hold it in its place while the gate
        // holds input (the entry ADR-0021 gains with ticket 02 of the Input Order), and the capture-phase
        // submit and focus on the window, filtered to this root, which number a form of the grid's own
        // submitted and a popover's sentinel focused (ADR-0240 section 4): Blazor hears both on the
        // document in the capture phase, ahead of any listener on the root.
        string[] allowed = ["blur", "change", "change", "click", "clipboardchange", "compositionend", "compositionend", "compositionstart", "contextmenu", "copy", "copy", "cut", "dblclick", "ex-press-handed-on", "focus", "focus", "focusout", "input", "keydown", "keydown", "keyup", "mousedown", "mousemove", "mouseleave", "mouseup", "paste", "paste", "scroll", "selectionchange", "submit"];
        Assert.Equal(allowed.OrderBy(name => name, StringComparer.Ordinal), listeners);
    }

    [Fact] // ADR-0010 (2026-10-02) / ADR-0005 / ED-22: a clipboard key typed while a hold stands is let through, and the event it fires is taken among the held keys, never dropped
    public void A_clipboard_key_is_let_through_a_hold_and_its_event_is_held()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal)).Text;

        // Built by the core and handed at attach (KB-2): the script names none of them.
        Assert.Contains("const clipboardKeys = gateSet('clipboard');", script, StringComparison.Ordinal);
        var letThrough = GridKeys.GateSets["clipboard"].ToHashSet();

        // Every key the core answers through the browser's own copy or paste event, which is why a
        // Consumer may not declare it, is one the hold lets through: held as a key, it was lost.
        foreach (var key in new[] { "Control+c", "Control+C", "Control+Insert", "Control+v", "Control+V", "Shift+Insert" })
        {
            var refusal = Assert.Throws<ArgumentException>(() => GridKeys.Declare([key]));
            Assert.Matches("copy event|paste event", refusal.Message);
            Assert.Contains(key, letThrough);
        }
        // Cut and paste as plain text are not the core's, so a Consumer may declare them; declared,
        // they are its keys, held as keys, and the listener asks before it lets one through.
        foreach (var key in new[] { "Control+x", "Control+X", "Shift+Delete", "Control+Shift+v", "Control+Shift+V" })
        {
            Assert.Contains(key, GridKeys.Declare([key]));
            Assert.Contains(key, letThrough);
        }
        Assert.Equal(11, letThrough.Count);
        Assert.Matches(new Regex(@"return clipboardKeys\.has\(canonical\) && !declared\.has\(canonical\);"), script);

        // In the hold, such a key's default is not prevented and it is not pushed as a key: the
        // event its default fires takes its place, in the capture phase, before any field hears it.
        Assert.Matches(new Regex(@"if \(isClipboardKey\(k\)\) \{\s*clipboardKeyHeld = true;[^}]*\}\);\s*\} else \{\s*event\.preventDefault\(\);\s*held\.push\(k\);\s*\}"), script);
        foreach (var type in new[] { "copy", "cut", "paste" })
        {
            Assert.Contains($"root.addEventListener('{type}', onHeldClipboard, true);", script);
            Assert.Contains($"root.removeEventListener('{type}', onHeldClipboard, true);", script);
        }
        // Dropped with the keys before it, a held copy writes nothing: the clipboard keeps what it held.
        Assert.Matches(new Regex(@"const dropHeld = \(\) => \{\s*for \(const k of held\) \{\s*if \(k\.clipboard === 'copy'\) \{\s*k\.take\(false\);"), script);
        Assert.DoesNotContain("held.length = 0;\n                break;", script);
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

    [Fact] // ADR-0012 (2026-09-29) / ADR-0021 / MEM-4: a reveal's write is held on the root's own reveal number, observed only while held and released on dispose
    public void The_reveal_write_is_held_on_the_roots_reveal_number_only()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal));

        // One attribute of this instance's own root, nothing wider and nothing measured. The
        // module's other observers are the coloured text's, on a layer's text and colours (DC-51),
        // the page follow's, on the root's own follow number (ADR-0204), and the Settled Mark's, on
        // the two attributes the core's render writes on the root (ADR-0220).
        Assert.Equal(4, Regex.Matches(script.Text, @"new MutationObserver\(").Count);
        Assert.Matches(new Regex(@"revealObserver\.observe\(root, \{ attributes: true, attributeFilter: \['data-ex-reveal'\] \}\)"), script.Text);
        // Let go as soon as the write is made or replaced, and with the instance.
        Assert.Matches(new Regex(@"const dropReveal = \(\) => \{[^}]*revealObserver\.disconnect\(\);", RegexOptions.Singleline), script.Text);
        Assert.Matches(new Regex(@"dispose: \(\) => \{[^}]*dropReveal\(\);", RegexOptions.Singleline), script.Text);
    }

    [Fact] // ADR-0021/0070 (the sixth decision about focus, 2026-10-02; DC-61): the keyboard is handed on to the Consumer's control only while it is still this grid's — DOM focus inside this root and not in a field of its own beside the rows, or on nothing — and only to a control still on the page, nothing measured
    public void The_hand_on_gives_the_consumers_control_the_keyboard_only_while_it_is_still_this_grids()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal)).Text;
        var handOn = Regex.Match(script, @"handKeyboardTo: \(element, inTurn\) => \{.*?\n        \},", RegexOptions.Singleline);
        Assert.True(handOn.Success, "handKeyboardTo(element, inTurn) is not in the module");
        var body = handOn.Value;

        // The hand-back's condition: this root's own focus, not a nested grid's, or none (ADR-0018)...
        Assert.Contains("ownsFocus(active)", body, StringComparison.Ordinal);
        Assert.Contains("active === document.body", body, StringComparison.Ordinal);
        Assert.Contains("active === document.documentElement", body, StringComparison.Ordinal);
        // ...and not a field beside the rows with focus of its own, unless a press on the rows left
        // it standing (ADR-0021, widened 2026-09-28).
        Assert.Contains("active.closest('.ex-formula-bar') !== null", body, StringComparison.Ordinal);
        Assert.Contains("active !== staleField", body, StringComparison.Ordinal);
        Assert.DoesNotContain("fromField", body, StringComparison.Ordinal);
        // Only an element still on the page, focused as its own FocusAsync would focus it, scrolled
        // into view; the field a press left standing is given up with the keyboard.
        Assert.Contains("element instanceof HTMLElement && element.isConnected", body, StringComparison.Ordinal);
        Assert.Matches(new Regex(@"staleField = null;[\s\S]*target\.focus\(\);"), body);
        Assert.Contains("element.querySelectorAll('input, textarea')].find(isTextField)", body, StringComparison.Ordinal);
        Assert.Contains("target === firstText && isTextField(target)", body, StringComparison.Ordinal);
        Assert.Contains("target.select()", body, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(body, @"\.(focus|blur)\("));
        // While the keyboard is on its way to a frame of the Consumer's own and a hold stands, the
        // control takes it in turn, from the hold, with the keys held for it (ADR-0050 item 16's
        // note of 2026-10-12): the same call, made then.
        Assert.Contains("if (inTurn !== true && handOff === 'frame' && answering", body, StringComparison.Ordinal);
        Assert.Contains("handle.handKeyboardTo(frame, true);", script, StringComparison.Ordinal);
        // A read of document.activeElement, never of layout, and no listener of its own.
        Assert.DoesNotMatch(new Regex(@"getBoundingClientRect|offsetWidth|offsetHeight|getComputedStyle|addEventListener"), body);
    }

    [Fact] // ADR-0021 (widened 2026-09-28) / ADR-0018: the hand-back leaves the fields beside the rows alone, found by the core's band, nothing measured
    public void The_hand_back_leaves_the_formula_bar_and_the_name_box_alone()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal));
        var reclaim = Regex.Match(script.Text, @"reclaimFocus: \(fromField, frame\) => \{.*?\n        \},", RegexOptions.Singleline);
        Assert.True(reclaim.Success, "reclaimFocus(fromField, frame) is not in the module");
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
        // A frame of the Consumer's own that gives the keyboard back is the one place outside the
        // root it is taken from (ADR-0050 item 16's note of 2026-10-12): only while DOM focus is on it.
        Assert.Contains("(frame instanceof Element && frame.contains(active))", body, StringComparison.Ordinal);
        Assert.Contains("const granted = handle.reclaimFocus(false, element) === true;", script.Text, StringComparison.Ordinal);
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
        // Script moves DOM focus in these seven places only (ADR-0021, five since ADR-0080, six since
        // 2026-10-02, seven since ADR-0204): this, the hand-back to the root or its Keyboard Field
        // (granted from a frame of the Consumer's own too since 2026-10-12, the same decision),
        // the open edit's own focus, the keyboard handed on to a control of the Consumer's, and a
        // popover's control taking it a round trip after a gesture, each only while the keyboard is
        // this grid's; the root's own focus passed on to its Keyboard Field; and the field given up
        // by a press during a composition.
        Assert.Equal(7, Regex.Matches(script.Text, @"\.(focus|blur)\(").Count);
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
        // or holds the keys after it: this core never hears the nested one's press. A secondary press
        // is held as a primary one is, so the context menu behind it keeps its place (ADR-0240).
        Assert.Contains("if (!core || replaying || (event.button !== 0 && event.button !== 2) || !isOwnRows(event.target)) {", body, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"event\.target\.classList\.contains\('ex-viewport'\)"), body);
        Assert.Matches(new Regex(@"const isOwnRows = \(target\) => inOwnScroller\(target\) && target\.classList\.contains\('ex-viewport'\);"), script.Text);
        // The hold: the keys after it are held from now, as behind a key, and the drain waits for
        // the press's answer before anything else.
        Assert.Matches(new Regex(@"const holdBehindPress = \(mark\) => \{\s*askAboutPress\(\);\s*pressAnswer = new Promise\(\(resolve\) => \{\s*pressToAsk = \{ mark, resolve \};\s*\}\);\s*setTimeout\(askAboutPress\);\s*if \(!answering\) \{\s*startHold\(\);"),
            script.Text);
        // One way a hold begins, whoever begins it: a key, a move in a popover, a press into the
        // bar, a press on the rows.
        Assert.Single(Regex.Matches(script.Text, @"answering = true;"));
        // Nothing in it counts time: a hold ends on what it waits for (ADR-0240 section 4).
        Assert.Matches(new Regex(@"const startHold = \(answer\) => \{\s*answering = true;\s*if \(answer\)"), script.Text);
        Assert.DoesNotContain("holdStartedAt", script.Text, StringComparison.Ordinal);
        Assert.Matches(new Regex(@"const drain = async \(\) => \{.*?while \(pressAnswer !== null\) \{\s*const answer = pressAnswer;\s*await answer;.*?await editorSettled\(\);",
            RegexOptions.Singleline), script.Text);
        // The core answers for the press or the release it heard last, so the question goes after
        // the press and ahead of its release: from a later task (above), or from this grid's own
        // release, whichever comes first.
        var release = Regex.Match(script.Text, @"const onRelease = \(event\) => \{.*?\n    \};", RegexOptions.Singleline);
        Assert.True(release.Success, "onRelease is not in the module");
        Assert.Matches(new Regex(@"^const onRelease = \(event\) => \{\s*(lookSettled\(\);\s*)?(//[^\n]*\s*)*if \(!replaying\) \{\s*askAboutPress\(\);"), release.Value);
        Assert.Matches(new Regex(@"const askAboutPress = \(\) => \{.*?core\.invokeMethodAsync\('PressAnsweredAsync', nextInput\(\)\).*?press\.resolve\(\);",
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
        Assert.Matches(new Regex(@"if \(!core \|\| replaying \|\| \(event\.button !== 0 && event\.button !== 2\) \|\| !isOwnRows\(event\.target\)\) \{\s*handOn\(event\);\s*tellTaken\('mousedown', taken\);"), press.Value);
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
        // The window hears only a form's submission and a sentinel's focus, ahead of Blazor's document
        // listener for both (ADR-0240 section 4), and only those whose target is the instance's own.
        Assert.DoesNotMatch(new Regex(@"window\.addEventListener\('(?!(submit|focus)', onClaimedAhead, true\))|window\.dispatchEvent|document\.dispatchEvent|document\.addEventListener\('ex-"), script.Text);
        Assert.Matches(new Regex(@"const onClaimedAhead = \(event\) => \{\s*if \(!root \|\| !\(event\.target instanceof Node\) \|\| !root\.contains\(event\.target\)\) \{\s*return;"), script.Text);
        // And no state outside an instance: the module's top-level statements are attach, and the
        // stamp the grid reads before it (ADR-0123), which holds nothing; so everything above lives
        // in one instance's closure and no instance keeps another's.
        var topLevel = script.Text.Split('\n')
            .Where(line => line.Length > 0 && !char.IsWhiteSpace(line[0]) && !line.StartsWith("//", StringComparison.Ordinal)
                && !line.StartsWith("/**", StringComparison.Ordinal))
            .ToList();
        Assert.Equal(["export function stamp() {", "}", "export function attach(root, scroller, core, takenKeys, canEdit, restDelayMs, canFind, declaredKeys, reportTimeZone, leaveDeclared, gateKeys) {", "}"], topLevel);
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
        // added to: six since 2026-10-02 and seven since ADR-0204, none of them this one's).
        Assert.Equal(2, Regex.Matches(script, @"nameBox(Selected)?\.select\(\);").Count);
        Assert.Equal(7, Regex.Matches(script, @"\.(focus|blur)\(").Count);
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
        var method = Regex.Match(script.Text, @"focusEditor: \(bar, fromField, toward\) => \{.*?\n        \},", RegexOptions.Singleline);
        Assert.True(method.Success, "focusEditor is not in the handle");
        var body = method.Value;

        // Found in the core's own box, a Chrome's control included, as the press back finds it: the
        // core holds no reference to a control it did not render (ADR-0010).
        Assert.Contains("ownSurface(b) === b", body, StringComparison.Ordinal);
        Assert.Contains("surfaceField(box)", body, StringComparison.Ordinal);
        // Granted only while DOM focus is inside this root, not in a grid nested in it, or on nothing:
        // reclaimFocus's condition (ED-22 under the hunt, 2026-10-19).
        Assert.Contains("!active || active === document.body || active === document.documentElement || ownsFocus(active)", body, StringComparison.Ordinal);
        Assert.Matches(new Regex(@"const ownsFocus = \(active\) => !!root && active instanceof Element && root\.contains\(active\)\s*&& active\.closest\('\.ex-grid'\) === root;"), script.Text);
        // Where DOM focus is, or, granted as the keyboard leaves the Keyboard Field, where it is going:
        // the browser has it on nothing while it tells the departure (ADR-0080, 2026-10-12).
        Assert.Contains("const active = toward instanceof Element ? toward : document.activeElement;", body, StringComparison.Ordinal);
        // Nor from a field beside the rows with focus of its own, a field a press on the rows left
        // standing aside, unless the core means to take the keyboard out of it, as the hand-back
        // leaves those fields (ADR-0021, 2026-09-28).
        Assert.Contains("active !== staleField", body, StringComparison.Ordinal);
        Assert.Contains("active.closest('.ex-formula-bar') !== null", body, StringComparison.Ordinal);
        Assert.Contains("fromField !== true", body, StringComparison.Ordinal);
        // Not scrolled by the focus itself: the page follows the editor as it follows a move, to the
        // nearest edge, once it has painted (ADR-0204).
        Assert.Contains("field.focus({ preventScroll: true });", body, StringComparison.Ordinal);
        Assert.Contains("followEditor(field);", body, StringComparison.Ordinal);
        // Declined, the surface asked for is the edit's, where the held keys and a press back go.
        Assert.Contains("lastSurface = box;", body, StringComparison.Ordinal);
        Assert.Contains("focusDeclined = true", body, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"getBoundingClientRect|offsetWidth|offsetHeight|getComputedStyle"), body);
        // A decline ends the hold's wait for the editor's focus, and the held keys go into the edit
        // the keyboard last held, never to the grid it went to (ADR-0010, same day).
        Assert.Contains("editing === 'none' || editorFocused() || focusDeclined", script.Text, StringComparison.Ordinal);
        Assert.Contains("focusDeclined ? standingField()", script.Text, StringComparison.Ordinal);
    }

    [Fact] // ADR-0204 / KB-45: the page follows a move through the scroll entry — scrollIntoView to the nearest edge, waiting on the root's own follow number, after the paint, only while the keyboard is this grid's and for the Focus it was taken for, nothing measured
    public void KB45_the_page_follows_a_move_through_the_scroll_entry_and_measures_nothing()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal)).Text;

        // The one call that moves the page, to the nearest edge, with the grid's own scroller put back
        // where its reveal left it.
        Assert.Single(Regex.Matches(script, @"scrollIntoView\("));
        var bring = Regex.Match(script, @"const bringIntoWindow = \(element\) => \{.*?\n    \};", RegexOptions.Singleline);
        Assert.True(bring.Success, "bringIntoWindow is not in the module");
        Assert.Contains("element.scrollIntoView({ block: 'nearest', inline: 'nearest' });", bring.Value, StringComparison.Ordinal);
        Assert.Contains("scroller.scrollTop = top;", bring.Value, StringComparison.Ordinal);
        Assert.Contains("scroller.scrollLeft = left;", bring.Value, StringComparison.Ordinal);

        // Held on the root's own follow number, as a reveal is on its own, and observed only while held.
        Assert.Matches(new Regex(@"followObserver\.observe\(root, \{ attributes: true, attributeFilter: \['data-ex-follow'\] \}\)"), script);
        Assert.Matches(new Regex(@"const dropFollow = \(\) => \{.*?followObserver\.disconnect\(\);", RegexOptions.Singleline), script);
        Assert.Matches(new Regex(@"dispose: \(\) => \{.*?dropFollow\(\);", RegexOptions.Singleline), script);

        // After the frame that paints it, in the task after its paint, and after a reveal's write and
        // repaint: nothing is read on the path to a paint.
        var after = Regex.Match(script, @"const afterPaint = \(request, run\) => \{.*?\n    \};", RegexOptions.Singleline);
        Assert.True(after.Success, "afterPaint is not in the module");
        Assert.Contains("requestAnimationFrame(", after.Value, StringComparison.Ordinal);
        Assert.Contains("setTimeout(", after.Value, StringComparison.Ordinal);
        Assert.Contains("if (pendingReveal || repaint)", after.Value, StringComparison.Ordinal);

        // Only while the keyboard is still this grid's, and only for the Focus it was taken for — as the
        // root names it, painted or not (ADR-0052: the Focus may be off the painted rows while the
        // Extent is followed) — found inside this root by the id its own render wrote.
        var awaited = Regex.Match(script, @"const awaitFollowedRender = \(\) => \{.*?\n    \};", RegexOptions.Singleline);
        Assert.True(awaited.Success, "awaitFollowedRender is not in the module");
        Assert.Contains("!keyboardStillHere() || root.getAttribute('data-ex-follow-focus') !== request.focus", awaited.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-activedescendant", awaited.Value, StringComparison.Ordinal);
        Assert.Contains("root.querySelector(`#${CSS.escape(request.target)}`)", awaited.Value, StringComparison.Ordinal);
        Assert.Matches(new Regex(@"const keyboardStillHere = \(\) => \{\s*const active = document\.activeElement;\s*return !!root && \(!active \|\| active === document\.body \|\| active === document\.documentElement \|\| ownsFocus\(active\)\);"), script);

        // No layout is read anywhere in it.
        foreach (var part in new[] { bring.Value, after.Value, awaited.Value })
            Assert.DoesNotMatch(new Regex(@"getBoundingClientRect|offsetWidth|offsetHeight|offsetTop|offsetLeft|clientWidth|clientHeight|scrollWidth|scrollHeight|getComputedStyle|getClientRects"), part);
    }

    [Fact] // ADR-0204 / KB-45, SRV-5: a popover's focus taken a round trip after a gesture asks the editor's question first, and leaves a field of the grid's own beside the rows, nothing measured
    public void KB45_a_popovers_focus_asks_whether_the_keyboard_is_still_here()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal)).Text;
        var method = Regex.Match(script, @"takeFocus: \(element, preventScroll, consumerPopover\) => \{.*?\n        \},", RegexOptions.Singleline);
        Assert.True(method.Success, "takeFocus is not in the handle");
        var body = method.Value;

        Assert.Contains("!active || active === document.body || active === document.documentElement", body, StringComparison.Ordinal);
        Assert.Contains("(ownsFocus(active) && !own)", body, StringComparison.Ordinal);
        Assert.Contains("active.closest('.ex-formula-bar') !== null", body, StringComparison.Ordinal);
        Assert.Contains("element.isConnected", body, StringComparison.Ordinal);
        Assert.Contains("target.focus({ preventScroll: preventScroll === true });", body, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"getBoundingClientRect|offsetWidth|offsetHeight|getComputedStyle"), body);

        // Ticket 240 (ADR-0204's note of 2026-10-17): a popover's first scripted focus is refused where a
        // press inside the popover has put the keyboard before it landed, a Consumer's popover's included,
        // which alone skips the editor's question.
        Assert.Contains("|| openingOvertaken(element)", body, StringComparison.Ordinal);
        Assert.Contains("consumerPopover === true ||", body, StringComparison.Ordinal);
        var overtaken = Regex.Match(script, @"const openingOvertaken = \(element\) => \{.*?\n    \};", RegexOptions.Singleline);
        Assert.True(overtaken.Success, "openingOvertaken is not in the module");
        Assert.Contains("openedPopovers.has(popover)", overtaken.Value, StringComparison.Ordinal);
        Assert.Contains("pressedPopovers.has(popover) && active instanceof Element && popover.contains(active)", overtaken.Value, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"getBoundingClientRect|offsetWidth|offsetHeight|getComputedStyle"), overtaken.Value);
        Assert.Contains("pressedPopovers.add(pressedIn);", ListenerBody(script, "onPress"), StringComparison.Ordinal);
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
        // on the Viewport, and the scroll offsets (the second entry), the vertical one placing a fill
        // handle held at the readable area's top (ADR-0050, item 5). No layout is read.
        foreach (var read in new[] { "event.offsetX", "event.offsetY", "'data-ex-first-row'", "'data-ex-sequence'", "'data-ex-layout'", "scroller.scrollLeft", "scroller.scrollTop" })
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

    [Fact] // ADR-0100's KeyTips, ADR-0021's seventh entry, MEM-4 (decided with the user, 2026-10-13): the release of a declared Alt that went down alone is heard on the root by the one keyup listener the seventh entry has, held in its place while keys are held and handed on in its turn after its press; the keys after a release wait for its answer; no key is inferred to be a release
    public void The_alt_keys_release_is_held_in_order_by_the_roots_one_keyup_listener()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal)).Text;
        var keyUp = ListenerBody(script, "onRootKeyUp");
        var keyDown = ListenerBody(script, "onKeyDown");
        var drain = Regex.Match(script, @"const drain = async \(\) => \{.*?\n    \};", RegexOptions.Singleline).Value;
        Assert.NotEmpty(drain);

        // One keyup listener, on the root, in the capture phase: no listener is added (MEM-4's count).
        Assert.Single(Regex.Matches(script, @"addEventListener\('keyup'"));
        Assert.Contains("root.addEventListener('keyup', onRootKeyUp, true);", script, StringComparison.Ordinal);
        // While keys are held, the release is kept from the Consumer's handler and held in its place.
        // Comments do not change the sequence. The held release names its held press, so the press's
        // landing decides whether the pair is an Alt alone (ADR-0050 item 16, 2026-10-14).
        var keyUpStatements = Regex.Replace(keyUp, @"//[^\r\n]*", "");
        Assert.Matches(new Regex(@"if \(answering\) \{\s*event\.stopPropagation\(\);\s*event\.preventDefault\(\);\s*held\.push\(\{ altUp: true, altAlone: alone, of: pending\?\.entry \?\? undefined, key: 'Alt' \}\);\s*return;\s*\}"), keyUpStatements);
        // Otherwise it reaches the Consumer now, and the keys after it wait for the answer to it; a chord's
        // release, or one whose press was not heard, never reaches the Consumer: the gate tells which.
        Assert.Matches(new Regex(@"if \(!alone\) \{\s*event\.stopPropagation\(\);\s*return;\s*\}\s*startHold\(altReleaseAnswered\(\)\);"), keyUp);
        // A release this listener hands on itself is not held again, and only where the Consumer declares the Alt key alone.
        Assert.Contains("if (!core || replaying || event.key !== 'Alt' || !declared.has('Alt+Alt')) {", keyUp, StringComparison.Ordinal);
        // Its turn: handed on as the keyup it was, then its answer asked; its press is not skipped as a
        // modifier's, so the Consumer hears the press before the release.
        Assert.Matches(new Regex(@"if \(k\.altUp\) \{\s*if \(releasesAlone\(k\)\) \{\s*await replayAltRelease\(\);\s*await editorSettled\(\);"), drain);
        Assert.Contains("const releasesAlone = (k) => (k.of ? k.of.altAlone === true : k.altAlone === true);", script, StringComparison.Ordinal);
        Assert.Contains("if (modifierKeys.has(k.key) && !(k.key === 'Alt' && k.altAlone === true", drain, StringComparison.Ordinal);
        Assert.True(drain.IndexOf("if (k.altUp)", StringComparison.Ordinal) < drain.IndexOf("if (modifierKeys.has(k.key)", StringComparison.Ordinal));
        // A held Alt is noted as it is held, so its release finds it, and a chord unmarks it.
        Assert.Contains("noteAlt(k, isClipboardKey(k) ? null : k, true);", keyDown, StringComparison.Ordinal);
        // Asked in the task after the release's dispatch: behind every listener of the keyup, Blazor's
        // among them, which sends the release on later in the same dispatch than this listener runs.
        Assert.Matches(new Regex(@"const altReleaseAnswered = \(\) => new Promise\(\(dispatched\) => \{\s*setTimeout\(dispatched\);\s*\}\)\.then\(\(\) => \(core \? core\.invokeMethodAsync\('Ping'\)"), script);
        // A held Alt is noted wherever under the root it was typed: its meaning is where it is handed on.
        Assert.Contains("&& (held === true || k.onRoot || k.inEditor);", script, StringComparison.Ordinal);
        Assert.Contains("if (altAlone(k, held)) {", script, StringComparison.Ordinal);
        // The keys a frame heard on the way back carry their Alts and releases, noted as the gate's own are.
        Assert.Contains("held.unshift(...withAltsNoted((Array.isArray(typed) ? typed : []).map((t) => ({", drain, StringComparison.Ordinal);
        Assert.Contains("altUp: t.type === 'keyup' ? true : undefined,", drain, StringComparison.Ordinal);
        // The release is no longer inferred from the key after it, and no Alt overtakes the keys held before it.
        Assert.DoesNotContain("afterAltAlone", script, StringComparison.Ordinal);
        Assert.DoesNotContain("altGoesFirst", script, StringComparison.Ordinal);
        Assert.DoesNotContain("altReleased", script, StringComparison.Ordinal);
        // Nothing measured.
        Assert.DoesNotMatch(new Regex(@"getBoundingClientRect|offsetWidth|offsetHeight|offsetTop|offsetLeft|clientWidth|clientHeight|scrollWidth|scrollHeight|getComputedStyle|getClientRects"), keyUp);
    }

    [Fact] // ADR-0070 / ADR-0010 (decided with the user, 2026-10-13: keys typed while the keyboard leaves are held and handed on in order): a grid whose Consumer hears OnLeave holds the keys after a plain Escape on the root for its answer; told to pass them out, it hands what it holds and what is typed on its root after to the element around the root, in order, and acts on none; no listener is added
    public void The_keys_after_a_leaving_escape_go_out_to_the_element_around_the_root()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal)).Text;
        var keyDown = ListenerBody(script, "onKeyDown");
        var drain = Regex.Match(script, @"const drain = async \(\) => \{.*?\n    \};", RegexOptions.Singleline).Value;
        var passOut = Regex.Match(script, @"const passHeldOut = \(\) => \{.*?\n    \};", RegexOptions.Singleline).Value;
        Assert.NotEmpty(passOut);

        // Told with the claims whether the Consumer hears OnLeave.
        Assert.Contains("leaves = leaveDeclared === true;", script, StringComparison.Ordinal);
        // The Escape the Consumer may answer by taking the keyboard out: the keys after it wait for its answer.
        Assert.Matches(new Regex(@"\} else if \(verdict === 'core' && leaves && k\.onRoot && k\.key === 'Escape' && plain\(k\) && !k\.shiftKey\s*&& k\.repeat !== true && editing === 'none'\) \{.*?startHold\(answer\);", RegexOptions.Singleline), keyDown);
        // Told to pass them out, the held keys go to the root's parent, as the keydowns they were, in order.
        // At once, whatever the hold is waiting for: every key held then was typed after the Escape, and one
        // typed later, or the keyboard leaving, must not overtake them.
        var request = Regex.Match(script, @"passKeysOut: \(\) => \{.*?\n        \},", RegexOptions.Singleline).Value;
        Assert.NotEmpty(request);
        // The destination is already answered: a pending Escape must not delay older keys behind
        // a later key bubbling out or the report collecting the frame's queue (ADR-0070).
        Assert.Matches(new Regex(@"keysOut = true;\s*(//[^\n]*\s*)*passHeldOut\(\);"), request);
        Assert.DoesNotContain("if (!answering)", request, StringComparison.Ordinal);
        Assert.Contains("const parent = root?.parentElement;", passOut, StringComparison.Ordinal);
        Assert.Contains("const keys = held.splice(0, held.length);", passOut, StringComparison.Ordinal);
        Assert.Matches(new Regex(@"parent\.dispatchEvent\(new KeyboardEvent\("), passOut);
        Assert.Matches(new Regex(@"while \(held\.length > 0 && core\) \{\s*(//[^\n]*\s*)*if \(keysOut\) \{\s*passHeldOut\(\);\s*break;"), drain);
        // A key typed meanwhile is not the grid's: its default goes, and it travels on to the element around.
        Assert.Matches(new Regex(@"if \(keysOut && !replaying\) \{\s*event\.preventDefault\(\);\s*return;\s*\}"), keyDown);
        Assert.DoesNotContain("stopPropagation", Regex.Match(keyDown, @"if \(keysOut && !replaying\) \{.*?\}", RegexOptions.Singleline).Value, StringComparison.Ordinal);
        // Ended by the keyboard leaving, a press, and nothing else of its own.
        Assert.Equal(2, Regex.Matches(script, @"(?<!let )keysOut = false;").Count);
        Assert.DoesNotMatch(new Regex(@"getBoundingClientRect|offsetWidth|offsetHeight|getComputedStyle"), passOut);
    }

    [Fact] // ADR-0061 (decided with the user, 2026-10-13) / ADR-0050 item 16: the keys a frame hands back while a hand-off stands wait for it, and go on there
    public void Keys_a_frame_hands_back_while_a_hand_off_stands_wait_for_it()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal)).Text;
        var drain = Regex.Match(script, @"const drain = async \(\) => \{.*?\n    \};", RegexOptions.Singleline).Value;
        var typedOnTheWay = Regex.Match(drain, @"if \(k\.typedOnTheWay !== undefined\) \{.*?\n                continue;\n            \}", RegexOptions.Singleline).Value;
        Assert.NotEmpty(typedOnTheWay);

        // In their place, ahead of every key typed on the root since; and, a hand-off standing — the keyboard
        // going on from this root to a frame of the Consumer's own — held until it arrives, then handed there.
        Assert.Matches(new Regex(@"held\.unshift\(.*?\)\)\)\);\s*(//[^\n]*\s*)*if \(handOff !== null\) \{\s*await editorSettled\(\);\s*\}\s*continue;", RegexOptions.Singleline), typedOnTheWay);
    }

    [Fact] // ADR-0080 / ADR-0021's seventh entry / ADR-0018: the Keyboard Field's listeners are on the root, on every grid, and go with the instance
    public void ADR0080_the_keyboard_fields_listeners_are_on_every_root_and_go_with_the_instance()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal)).Text;

        // At attach's own level, behind no condition: every grid hears them, with a field or not —
        // the release of Tab ends on a display-only grid too (ADR-0012).
        Assert.Matches(new Regex(@"\n    root\.addEventListener\('compositionstart', onKeyFieldCompositionStart, true\);\n    root\.addEventListener\('compositionend', onKeyFieldCompositionEnd, true\);\n    root\.addEventListener\('keyup', onRootKeyUp, true\);"), script);
        Assert.Matches(new Regex(@"\n    root\.addEventListener\('focus', onFocused, true\);\n    root\.addEventListener\('focusout', onFocusLeft, true\);"), script);
        var dispose = Regex.Match(script, @"dispose: \(\) => \{.*?root = null;", RegexOptions.Singleline).Value;
        foreach (var removed in new[]
        {
            "root.removeEventListener('compositionstart', onKeyFieldCompositionStart, true);",
            "root.removeEventListener('compositionend', onKeyFieldCompositionEnd, true);",
            "root.removeEventListener('keyup', onRootKeyUp, true);",
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
        Assert.Matches(new Regex(@"const next = event\.relatedTarget;\s*(//[^\n]*\s*)*const stays = event\.target === document\.activeElement \|\| \(next instanceof Node && root && root\.contains\(next\)\);"), left);
        Assert.Matches(new Regex(@"if \(stays\) \{\s*return;\s*\}\s*tabReleased = false;\s*releaseEndsHeard\+\+;"), left);
        // Counted with the presses, so a release the core answers for an Escape forwarded before the
        // departure is not granted.
        Assert.Contains("releaseEndsAtEscape = releaseEndsHeard;", script, StringComparison.Ordinal);
        Assert.Matches(new Regex(@"releaseTab: \(\) => \{\s*tabReleased = releaseEndsHeard === releaseEndsAtEscape;\s*\},"), script);
        Assert.Equal(3, Regex.Matches(script, @"releaseEndsHeard\+\+;").Count);
        Assert.Contains("releaseEndsHeard++;", ListenerBody(script, "onPress"), StringComparison.Ordinal);
        // ADR-0230 section 1: a release ended in the gate counts too, so a releaseTab still on its way
        // for an Escape that released at its keydown grants nothing after the release was spent.
        Assert.Matches(new Regex(@"const endRelease = \(\) => \{\s*if \(tabReleased\) \{\s*tabReleased = false;\s*releaseEndsHeard\+\+;\s*\}\s*\};"), script);
    }

    [Fact] // ADR-0230 section 1 / ticket 220: an Escape the stamp says leaves, once every input that could add a layer is answered, releases Tab at its keydown and holds nothing
    public void ADR0230_an_escape_the_stamp_says_leaves_releases_tab_at_its_keydown()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal)).Text;
        Assert.Matches(new Regex(@"const escapeLeavesNow = \(k\) => !leaves && k\.repeat !== true && stamp !== null\s*&& stamp\.hasAttribute\('data-ex-escape-leaves'\)\s*&& Number\(stamp\.getAttribute\('data-ex-answered'\)\) >= layerInputAt;"), script);
        Assert.Matches(new Regex(@"if \(canonical === 'Escape'\) \{\s*releaseEndsAtEscape = releaseEndsHeard;\s*if \(escapeLeavesNow\(k\)\) \{\s*tabReleased = true;\s*return 'core';\s*\}\s*return 'mode';\s*\}"), script);
        // Only the keys on the root that move the Focus or peel a layer leave the stamp to be trusted.
        Assert.Matches(new Regex(@"function nextInput\(neutral = false\) \{\s*inputsSent \+= 1;\s*if \(!neutral\) \{\s*layerInputAt = inputsSent;\s*\}"), script);
        Assert.Contains("const neutral = editing === 'none' && neutralKeys.has(canonicalOf(k)) && !declared.has(canonicalOf(k));", script, StringComparison.Ordinal);
        Assert.Contains("layerInputAt = inputsSent + 1;", ListenerBody(script, "onPress"), StringComparison.Ordinal);
        // (b): a held Tab that meets the release is refused aloud, not dropped silently.
        Assert.Matches(new Regex(@"if \(verdict === 'out'\) \{\s*(?://[^\n]*\n\s*)+core\?\.invokeMethodAsync\('OnHeldTabRefusedAsync', nextInput\(true\)\)"), script);
    }

    [Fact] // ADR-0080 / ADR-0021 (decisions about focus): focus on the root itself, or on its scroller, goes on to its Keyboard Field, the hand-back puts the keyboard there, and DOM focus never moves while the field composes
    public void ADR0080_the_keyboard_goes_to_the_field_and_never_moves_while_it_composes()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal)).Text;

        // The root's own focus and its scroller's — a press on the rows' default — passed on at once,
        // without scrolling; heard in the capture phase (ADR-0080's note of 2026-10-21).
        Assert.Matches(new Regex(@"if \(event\.target === root \|\| event\.target === scroller\) \{\s*field\.focus\(\{ preventScroll: true \}\);"), ListenerBody(script, "onFocused"));
        // The hand-back: the field where the grid has one, the root where it has none.
        var reclaim = Regex.Match(script, @"reclaimFocus: \(fromField, frame\) => \{.*?\n        \},", RegexOptions.Singleline).Value;
        Assert.Contains("(keyFieldOf() ?? root).focus({ preventScroll: true });", reclaim, StringComparison.Ordinal);
        Assert.Matches(new Regex(@"const keyFieldOf = \(\) => \{\s*const field = root \? root\.querySelector\('\.ex-key-field'\) : null;\s*return field !== null && isKeyField\(field\) \? field : null;"), script);
        // The editor's request for the keyboard waits while the field composes, and while a key the
        // IME took there is still being told; it is granted after that (the next fact).
        var focusEditor = Regex.Match(script, @"focusEditor: \(bar, fromField, toward\) => \{.*?\n        \},", RegexOptions.Singleline).Value;
        Assert.Matches(new Regex(@"if \(keyFieldComposing \|\| imeKeyTold\) \{\s*deferredEditorFocus = \{ bar, fromField \};\s*return;\s*\}"), focusEditor);
        // A primary press during a composition ends it first, by the field giving up the keyboard,
        // before anything else the press does.
        Assert.Matches(new Regex(@"^const onPress = \(event\) => \{\s*(lookSettled\(\);\s*)?(//[^\n]*\s*)*if \(keyFieldComposing && core && !replaying && event\.button === 0\) \{\s*keyFieldOf\(\)\?\.blur\(\);\s*\}"), ListenerBody(script, "onPress"));
        // The composition's start is told to the core only with no edit open.
        Assert.Matches(new Regex(@"if \(editing === 'none' && !answering\) \{\s*core\.invokeMethodAsync\('OnKeyFieldCompositionStartAsync', nextInput\(\)\)"), ListenerBody(script, "onKeyFieldCompositionStart"));
        Assert.Single(Regex.Matches(script, @"'OnKeyFieldTextAsync'"));
        // The document's selection is the field's caret, which the IME needs: a key on the field
        // does not drop it, as one on the root does (ADR-0021's clipboard note).
        Assert.Contains("if (k.onRoot && !isKeyField(event.target)) {", ListenerBody(script, "onKeyDown"), StringComparison.Ordinal);
    }

    [Fact] // ED-30 / ADR-0080 (the sixteenth Windows run's k6; the third composition, 2026-10-12): the editor's request is granted only once the key the IME took has been told whole, its keyup, and never while the field composes again — told by the key's events, not by a time
    public void ED30_the_editors_request_is_granted_only_once_the_key_the_IME_took_has_been_told_whole()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal)).Text;
        var carry = Regex.Match(script, @"const carryKeyFieldText = \(field\) => \{.*?\n    \};", RegexOptions.Singleline).Value;
        var grant = Regex.Match(script, @"const grantEditorFocus = \(toward\) => \{.*?\n    \};", RegexOptions.Singleline).Value;
        var keyUp = Regex.Match(script, @"const onRootKeyUp = \(event\) => \{.*?\n    \};", RegexOptions.Singleline).Value;
        Assert.NotEmpty(carry);
        Assert.NotEmpty(grant);
        Assert.NotEmpty(keyUp);

        // A key the IME takes in the field is being told from its keydown, which the listener leaves
        // to the IME, to a keyup: on WebAssembly the core's answer to a composition's text — the Cell
        // Editor opened, and its request for the keyboard — runs inside the end, before the browser
        // says whether the same key started another composition.
        Assert.Matches(new Regex(@"if \(event\.isComposing \|\| event\.keyCode === 229\) \{\s*if \(isKeyField\(event\.target\)\) \{\s*imeKeyTold = true;\s*\}\s*return;\s*\}"), ListenerBody(script, "onKeyDown"));
        Assert.Matches(new Regex(@"if \(imeKeyTold\) \{\s*imeKeyTold = false;\s*grantEditorFocus\(\);\s*\}"), keyUp);
        // The end grants what waited only through grantEditorFocus, after the text has taken its
        // place among the held keys: at once when no such key is being told, else at its keyup.
        Assert.Matches(new Regex(@"held\.push\(\{ text, paint: keyFieldPaint \}\);.*grantEditorFocus\(\);\s*\};$", RegexOptions.Singleline), carry);
        Assert.DoesNotContain("focusEditor(", carry, StringComparison.Ordinal);
        Assert.Matches(new Regex(@"const pending = deferredEditorFocus;\s*if \(pending !== null && !keyFieldComposing && !imeKeyTold\) \{\s*deferredEditorFocus = null;\s*handle\.focusEditor\(pending\.bar, pending\.fromField, toward\);\s*\}"), grant);
        // The one place a waiting request is granted, and no time in it: the task after a
        // composition's end, a timer of no delay, missed a start told a moment later (ED-30's
        // third composition read `かなｄ感じです`).
        Assert.Single(Regex.Matches(script, @"handle\.focusEditor\("));
        Assert.DoesNotMatch(new Regex(@"setTimeout|requestAnimationFrame|keyFieldEndTimer"), carry + grant + keyUp);
        Assert.DoesNotContain("keyFieldEndTimer", script, StringComparison.Ordinal);
        var dispose = Regex.Match(script, @"dispose: \(\) => \{.*?root = null;", RegexOptions.Singleline).Value;
        Assert.Contains("imeKeyTold = false;", dispose, StringComparison.Ordinal);
        // No listener of its own beyond the seventh entry's, and nothing measured.
        Assert.DoesNotMatch(new Regex(@"addEventListener"), carry + grant + keyUp);
        Assert.DoesNotMatch(new Regex(@"getBoundingClientRect|offsetWidth|offsetHeight|offsetTop|offsetLeft|clientWidth|clientHeight|scrollWidth|scrollHeight|getComputedStyle|getClientRects"), carry + grant + keyUp);
    }

    [Fact] // ED-30 / ADR-0080 (the sixteenth Windows run's k15, 2026-10-12): a key the IME took ends as the keyboard leaves — Alt+Tab mid-composition told its keyup to the other window — and the request that waited for it is decided against where the keyboard is going
    public void ED30_a_key_the_IME_took_ends_as_the_keyboard_leaves()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal)).Text;
        var left = ListenerBody(script, "onFocusLeft");

        // Every focusout the root hears ends it, the window switch's among them, and decides the
        // waiting request against the next holder: DOM focus is on nothing while a move is told, and
        // a control the user moved to outside the grid keeps the keyboard.
        Assert.Matches(new Regex(@"if \(imeKeyTold\) \{\s*imeKeyTold = false;\s*grantEditorFocus\(next\);\s*\}"), left);
        // After the departure has been told from the window losing focus by where DOM focus was, and
        // before anything returns: a grant moves DOM focus, and a window switch read after one would
        // end the release of Tab.
        var stays = left.IndexOf("const stays = ", StringComparison.Ordinal);
        var granted = left.IndexOf("grantEditorFocus(next);", StringComparison.Ordinal);
        Assert.True(stays >= 0 && stays < granted);
        Assert.True(granted < left.IndexOf("return;", StringComparison.Ordinal));
        Assert.Single(Regex.Matches(left, @"return;"));
        // One place marks the key, and each way it can end clears it: its keyup heard on the root,
        // the keyboard leaving, and the instance's end. Nothing else leaves a request waiting on it.
        Assert.Single(Regex.Matches(script, @"imeKeyTold = true;"));
        Assert.Equal(3, Regex.Matches(script, @"(?<!let )imeKeyTold = false;").Count);
        Assert.Matches(new Regex(@"const onRootKeyUp = \(event\) => \{\s*(lookSettled\(\);\s*)?if \(imeKeyTold\) \{\s*imeKeyTold = false;\s*grantEditorFocus\(\);"), script);
        // The grant goes through focusEditor's own rule, judged where the keyboard is going.
        Assert.Contains("handle.focusEditor(pending.bar, pending.fromField, toward);", script, StringComparison.Ordinal);
        Assert.Contains("const active = toward instanceof Element ? toward : document.activeElement;", script, StringComparison.Ordinal);
        // No listener of its own: focusout is the seventh entry's already (ADR-0021).
        Assert.Single(Regex.Matches(script, @"addEventListener\('focusout'"));
    }

    [Fact] // ED-30 / ADR-0080 / ADR-0010 (the third composition, 2026-10-12): as the listener gives the editor the keyboard, what the hold has for the edit goes into it at once, never a frame later, when a composition the IME started there would come ahead of it
    public void ED30_the_text_held_for_the_edit_goes_into_it_as_the_editor_takes_the_keyboard()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal)).Text;
        var focusEditor = Regex.Match(script, @"focusEditor: \(bar, fromField, toward\) => \{.*?\n        \},", RegexOptions.Singleline).Value;
        var settled = Regex.Match(script, @"const editorSettled = \(\) => new Promise\(\(settle\) => \{.*?\n    \}\);", RegexOptions.Singleline).Value;
        Assert.NotEmpty(focusEditor);
        Assert.NotEmpty(settled);

        // Straight after the focus it gives, the hold's waits are looked at again: the held text is
        // typed in a microtask, before the browser tells the page anything else. An IME's composition
        // cannot be held, and one started in the editor before the next frame came ahead of the text
        // still held for it: D10 read `かなｄ感じです`.
        Assert.Matches(new Regex(@"field\.focus\(\{ preventScroll: true \}\);\s*followEditor\(field\);\s*(//[^\n]*\s*)*lookAgain\(\);"), focusEditor);
        Assert.Matches(new Regex(@"const lookAgain = \(\) => \{\s*for \(const again of \[\.\.\.settledWaits\]\) \{\s*again\(\);\s*\}\s*\};"), script);
        // Every wait of the hold's is one lookAgain reaches, and leaves the set as it settles, by
        // whichever path; looked at again, it leaves no second frame of its own behind, and once
        // settled it is never looked at again.
        Assert.Matches(new Regex(@"const resolve = \(\) => \{\s*waiting = false;\s*settledWaits\.delete\(again\);\s*settle\(\);\s*lookSettled\(\);\s*\};"), settled);
        Assert.Matches(new Regex(@"const again = \(\) => \{\s*if \(waiting\) \{\s*cancelAnimationFrame\(lookFrame\);\s*look\(\);\s*\}\s*\};\s*settledWaits\.add\(again\);\s*look\(\);"), settled);
        Assert.Contains("lookFrame = requestAnimationFrame(look);", settled, StringComparison.Ordinal);
        // The look settles the wait only through resolve, so that no path leaves it in the set.
        var look = Regex.Match(settled, @"const look = \(\) => \{.*?\n        \};", RegexOptions.Singleline).Value;
        Assert.NotEmpty(look);
        Assert.DoesNotContain("settle(", look, StringComparison.Ordinal);
        Assert.Contains("resolve();", look, StringComparison.Ordinal);
    }

    [Fact] // ADR-0240 §4: no wait ends on a clock — the reveal, the close hold, the mark's wait, and the editor's, popover's and moves' waits end on what they wait for
    public void ADR0240_no_wait_ends_on_a_clock()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal)).Text;

        Assert.DoesNotContain("revealTimer", script, StringComparison.Ordinal);
        Assert.DoesNotContain("fallback: setTimeout", script, StringComparison.Ordinal);
        Assert.DoesNotContain("holdStartedAt", script, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"performance\.now\(\) - "), script);
        Assert.DoesNotMatch(new Regex(@"\b2000\b"), script);
        // Judged instead once the core has answered every input the wait is behind and owes no focus.
        Assert.Contains("!stamp.hasAttribute('data-ex-focus-owed')", script, StringComparison.Ordinal);
        Assert.Contains("core.invokeMethodAsync('OnHeldInputsRefusedAsync', nextInput(true))", script, StringComparison.Ordinal);
        // The keys, submissions and sentinel focus a popover answers itself are told, so moves wait on numbers.
        Assert.Contains("claimOf(event.target, 'keydown') !== null ? tellClaim('keydown')", script, StringComparison.Ordinal);
        // Heard on the window in the capture phase, ahead of Blazor's document listener for both.
        Assert.Contains("window.addEventListener('submit', onClaimedAhead, true);", script, StringComparison.Ordinal);
        Assert.Contains("window.addEventListener('focus', onClaimedAhead, true);", script, StringComparison.Ordinal);
        Assert.Contains("tellClaim('focus');", script, StringComparison.Ordinal);
    }

    [Fact] // ADR-0039 (note of 2026-10-01) / ADR-0010 / ADR-0080: a composition finished in the Keyboard Field while a popover or a Consumer's frame takes the keyboard is handed to it as a held character is — typed into its text field, dropped with every key behind it on any other control — and the core is asked to open an edit only when neither has the keyboard
    public void ADR0039_a_compositions_text_held_while_a_popover_opens_is_the_popovers()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal)).Text;
        var drain = Regex.Match(script, @"const drain = async \(\) => \{.*?\n    \};", RegexOptions.Singleline).Value;
        Assert.NotEmpty(drain);
        var branch = Regex.Match(drain, @"\n            if \(k\.text !== undefined\) \{.*?\n            \}\n", RegexOptions.Singleline).Value;
        Assert.NotEmpty(branch);

        // What holds the keyboard is asked first, as it is for every held key: a control of the
        // grid's own (a popover's: Format Cells' tab, a column's commands, the find field), or the
        // Consumer's frame the core handed the keyboard to. Asked of the core instead, the text
        // opened a Cell Editor behind the popover.
        Assert.Matches(new Regex(
            @"^\s*if \(k\.text !== undefined\) \{\s*"
            + @"const holder = focusedControl\(\) \?\? \(deliveringOutside \? outsideControl\(\) : null\);\s*"
            + @"if \(holder\) \{\s*"
            // A cancelled composition typed nothing: nothing to hand on, and nothing dropped.
            + @"if \(k\.text\.length === 0\) \{\s*continue;\s*\}\s*"
            // Text no keydown carries is a key script cannot reproduce on a control that is not a
            // text field: dropped, with every key held behind it, and refused aloud (ADR-0010, ADR-0240 section 4).
            + @"if \(!isTextField\(holder\)\) \{\s*refuseHeld\(true\);\s*break;\s*\}\s*"
            // In a text field it is typed at the caret, as a held character is.
            + @"typeInto\(holder, k\);\s*continue;\s*\}\s*"
            // Only with the keyboard still the grid's is the core asked to open an edit holding it.
            + @"if \(editing === 'none'\) \{\s*const opened = await core\.invokeMethodAsync\('OnKeyFieldTextAsync', k\.text, k\.paint, nextInput\(\)\)"),
            branch);
        // The one place the core is asked to open an edit for a composition's text.
        Assert.Single(Regex.Matches(script, @"'OnKeyFieldTextAsync'"));
        // Where a held key goes is read from what holds DOM focus, as for every held key: nothing is
        // measured, and no focus is moved from here (ADR-0021).
        Assert.DoesNotMatch(new Regex(@"\.focus\(|getBoundingClientRect|getComputedStyle|offset(Width|Height|Top|Left)"), branch);
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
        Assert.Matches(new Regex(@"^const onKeyDown = \(event\) => \{\s*(lookSettled\(\);\s*)?if \(!core\) \{\s*return;\s*\}\s*(//[^\n]*\s*)*keyboardByPress = false;"), ListenerBody(script, "onKeyDown"));
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
        Assert.Matches(new Regex(@"'OnKeyAsync'[^;]*input \? input\.value : null, input \? \(input\.selectionStart \?\? input\.value\.length\) : -1,\s*input \? \(input\.selectionEnd \?\? input\.value\.length\) : -1, input \? movedByUser\(input\) : false,\s*k\.repeat === true, k\.paint, nextInput\(neutral\)\)",
            RegexOptions.Singleline), script.Text);
        Assert.DoesNotMatch(new Regex(@"getBoundingClientRect|offsetWidth|offsetHeight|getComputedStyle"), script.Text);
    }

    [Fact] // ADR-0070/0021 / DC-62: the key message says whether the key is a held key's repeat — a field of the event the listener already reads, the last of the one message
    public void The_key_message_says_whether_the_key_is_a_repeat()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal));

        // Auto-repeat is visible only in the capture-phase listener: by the time a key reaches
        // .NET, a repeat looks like a press. The snapshot every key is gated and held as keeps
        // the browser's flag, and the one message to OnKeyAsync carries it, so a held key
        // replayed after a hold says so as well. The core raises OnLeave once per press from it.
        // Only the render the key was pressed against comes after it (ADR-0142, LV-14).
        Assert.Matches(new Regex(@"const snapshot = \(event\) => \(\{[^}]*repeat: event\.repeat,", RegexOptions.Singleline), script.Text);
        Assert.Matches(new Regex(@"'OnKeyAsync'[^;]*,\s*k\.repeat === true, k\.paint, nextInput\(neutral\)\)\s*\.catch\(", RegexOptions.Singleline), script.Text);
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
        Assert.Matches(new Regex(@"setEditing: \(mode, reportsCaret, cyclesReferences, version\) => \{[^}]*cycleReferences = cyclesReferences === true;", RegexOptions.Singleline), script.Text);
    }

    [Fact] // ADR-0007 / KB-39 / DC-30: the gate takes undo and redo only from C#'s list, and never while editing
    public void The_history_keys_are_claimed_only_through_the_cores_list()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal));

        // The listener names no history key of its own: they reach it only in the set C#
        // hands it, at attach or re-told through setClaims, and that set is consulted only
        // while no edit is open. While one is, the editing sets decide, and they carry none.
        Assert.DoesNotMatch(new Regex(@"'Control\+(Shift\+)?[zZyY]'"), script.Text);
        Assert.Matches(new Regex(@"setClaims: \(takenKeys, editable, findable, declaredKeys, leaveDeclared\) => \{\s*taken = new Set\(takenKeys\);"), script.Text);
        Assert.Matches(new Regex(@"if \(!taken\.has\(canonical\)\)"), script.Text);
    }

    [Fact] // ADR-0050 item 14 / ADR-0021 / DC-57 / DC-24: the gate claims declared keys only from C#'s list, beside the editor's own, and names none itself
    public void The_gate_claims_declared_keys_only_from_the_cores_list()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal));
        var gate = Regex.Match(script.Text, @"const gate = \(k\) => \{.*?\n    \};", RegexOptions.Singleline);
        Assert.True(gate.Success, "the gate is not in the module");

        // Handed at attach and re-told with the claims, per instance.
        Assert.Matches(new Regex(@"export function attach\([^)]*, declaredKeys, reportTimeZone, leaveDeclared, gateKeys\) \{"), script.Text);
        Assert.Matches(new Regex(@"let declared = new Set\(declaredKeys \?\? \[\]\);"), script.Text);
        Assert.Matches(new Regex(@"setClaims: \(takenKeys, editable, findable, declaredKeys, leaveDeclared\) => \{[^}]*declared = new Set\(declaredKeys \?\? \[\]\);", RegexOptions.Singleline), script.Text);
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
        Assert.Matches(new Regex(@"setCaret: \(text, caret, end\) => \{\s*const input = editorInput\(\);\s*(?://[^\n]*\n\s*)*const moved = input \? movedByUser\(input\) : false;\s*if \(input && input\.value === text\) \{\s*if \(moved\) \{\s*(?://[^\n]*\n\s*)*showCaret\(input, input\.selectionDirection === 'backward' \? input\.selectionStart : input\.selectionEnd\);\s*reportedText = null;\s*reportCaretOf\(input\);\s*return;\s*\}\s*(?://[^\n]*\n\s*)*showCaret\(input, end\);\s*input\.setSelectionRange\(caret, end\);\s*reportedText = text;\s*reportedCaret = caret;",
            RegexOptions.Singleline), script.Text);
        // The user's move: a press in an editor surface's text, or a caret key left to it or
        // answered by the listener on an Apple platform (ticket 32) — only while the field still
        // holds the text it was made in, and forgotten once it does not, so that an equal text written
        // later is not taken for it (ADR-0051; the seventeenth Windows run, Part C, cases 8 and 9).
        Assert.Matches(new Regex(@"const movedByUser = \(input\) => \{\s*if \(caretMoved === null \|\| caretMoved\.input !== input\) \{\s*return false;\s*\}\s*if \(caretMoved\.text !== input\.value\) \{\s*caretMoved = null;\s*return false;\s*\}\s*return true;\s*\};"), script.Text);
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
        // A held arrow replayed moves the caret, or with Shift the selection's moving end, and shows
        // where it went (ADR-0010).
        Assert.Matches(new Regex(@"if \(!k\.shiftKey\) \{\s*input\.setSelectionRange\(to, to\);\s*\} else if \(to < anchor\) \{\s*input\.setSelectionRange\(to, anchor, 'backward'\);\s*\} else \{\s*input\.setSelectionRange\(anchor, to, 'forward'\);\s*\}\s*showCaret\(input, to\);\s*return true;"), script);
        Assert.Matches(new Regex(@"showCaret\(input, input\.selectionEnd\);\s*(?://[^\n]*\n\s*)*input\.dispatchEvent\(new Event\('input', \{ bubbles: true \}\)\);\s*return true;\s*\};"), script);
        Assert.Equal(4, Regex.Matches(script, @"showCaret\(input, (end|edge|to|input\.selectionEnd)\);").Count);
        // The user's own move in the text the placement came for is shown as the placement would have
        // shown it: an End typed before it, with the caret at the end already, moved no view (ticket 32).
        Assert.Matches(new Regex(@"if \(moved\) \{\s*(?://[^\n]*\n\s*)*showCaret\(input, input\.selectionDirection === 'backward' \? input\.selectionStart : input\.selectionEnd\);"), script);
        Assert.Equal(5, Regex.Matches(script, @"showCaret\(").Count);
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
        Assert.Matches(new Regex(@"setEditing: \(mode, reportsCaret, cyclesReferences, version\) => \{(?:(?!\n        \},).)*?watchReferenceTexts\(mode !== 'none'\);", RegexOptions.Singleline), script.Text);
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

    [Fact] // ADR-0051 second round / DC-31, ADR-0058 / SH-36: pointing claims the Shift+arrows and Shift+Home; an open list claims only ↑/↓ beside the editing keys, and ←, →, Home, End and the Shift+arrows too while it is open over Point
    public void The_gate_has_a_point_set_and_a_completion_set()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal));

        // The arrows, Home and End, the four Shift+arrows and Shift+Home, which extends the outline to
        // the row's first column (ADR-0010, ED-2; observed in Excel, the readings of 2026-10-12, R1
        // (viii)), and the Primary Modifier's arrows with Shift or without, which point at the data
        // edge (ADR-0051; observed in Excel, run 18a). Built by the core and handed at attach (KB-2).
        Assert.Equal(
            [.. GridEditingKeys.Editing, "ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight", "Home", "End",
                "Shift+ArrowUp", "Shift+ArrowDown", "Shift+ArrowLeft", "Shift+ArrowRight", "Shift+Home",
                "Control+ArrowUp", "Control+ArrowDown", "Control+ArrowLeft", "Control+ArrowRight",
                "Control+Shift+ArrowUp", "Control+Shift+ArrowDown", "Control+Shift+ArrowLeft", "Control+Shift+ArrowRight"],
            GridKeys.GateSets["point"]);
        Assert.Equal([.. GridEditingKeys.Editing, "ArrowUp", "ArrowDown"], GridKeys.GateSets["completion"]);
        // ADR-0058 (the tenth Windows run; Part B of the ninth, Q51) / SH-36: a list open over Point
        // takes only ↑, ↓, Tab and Escape, and ←, →, Home, End and the four Shift+arrows are claimed
        // beside them, to point; nothing else.
        Assert.Equal(
            [.. GridKeys.GateSets["completion"], "ArrowLeft", "ArrowRight", "Home", "End",
                "Shift+ArrowUp", "Shift+ArrowDown", "Shift+ArrowLeft", "Shift+ArrowRight"],
            GridKeys.GateSets["completionOverPoint"]);
        // ADR-0058 ("The keyboard") / SH-35: Point written from outside has a set of its own beside them,
        // each read by the name the core tells the gate.
        Assert.Matches(new Regex(@"const claimedWhile = Object\.fromEntries\(\['overwrite', 'point', 'pointed', 'completion', 'completionOverPoint'\]\s*\.map\(\(mode\) => \[mode, gateSet\(mode\)\]\)\);"), script.Text);
        Assert.Contains("const editingKeys = gateSet('editing');", script.Text, StringComparison.Ordinal);
        // A list painted is open from its own render, before the gate is told (ADR-0051/0010):
        // read off the marks the core writes on the list's box, and nothing measured — whether it
        // is open over Point too.
        Assert.Matches(new Regex(@"const listShown = \(\) => \(root \? root\.querySelector\('\.ex-completion\[data-ex-list\]'\) : null\);"), script.Text);
        Assert.Matches(new Regex(@"const list = listShown\(\);\s*const claimed = list === null\s*\? \(claimedWhile\[editingShown\(\)\] \?\? editingKeys\)\s*: \(list\.hasAttribute\('data-ex-over-point'\) \? completionOverPointKeys : completionKeys\);"), script.Text);
        Assert.Single(Regex.Matches(script.Text, @"listShown\(\)"));
        // ADR-0230 section 2 / ticket 221: every other mode is read off the edit's surfaces too, in
        // the render that shows it, where that mark is newer than what the gate was told.
        Assert.Matches(new Regex(@"root\.querySelectorAll\('\.ex-editor\[data-ex-mode\]'\)"), script.Text);
        Assert.Matches(new Regex(@"if \(marked > version && surface\.closest\('\.ex-grid'\) === root\) \{"), script.Text);
        Assert.Single(Regex.Matches(script.Text, @"editingShown\(\)"));
    }

    [Fact] // ADR-0010 / KB-2: the sets of keys the gate reads are built by the core and handed at attach; the script spells none of them
    public void KB2_the_script_names_no_set_of_keys()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal)).Text;

        // A set is made only from what the core handed: the taken and declared keys, and the gate's
        // named sets. The script spells only the sets of what it answers itself, as KB-2 names them:
        // Home and End on an Apple platform and PageUp and PageDown in an editor field (ADR-0010's note
        // of 2026-09-30, ticket 32), the modifiers' own keydowns, which name no key, a text field's
        // input types, which are not keys (those that hold a form's implicit submission back too, ADR-0240), and the keys a held key is handed on as (ADR-0010's notes on
        // the hold): what a text field, a tab and a caret do with them.
        var spelled = Regex.Matches(script, @"new Set\(\[\s*'[^']*'[^\]]*\]\)").Select(match => match.Value).ToList();
        Assert.Equal(
            [
                "new Set(['Home', 'End', 'Shift+Home', 'Shift+End'])",
                "new Set(['PageUp', 'PageDown'])",
                "new Set(['Shift', 'Control', 'Alt', 'Meta'])",
                "new Set(['text', 'search', 'url', 'tel', 'email', 'password', 'date', 'month', 'week', 'time', 'datetime-local', 'number'])",
                "new Set(['text', 'search', 'url', 'tel', 'password', 'email', 'number'])",
                "new Set(['Backspace', 'Delete', 'ArrowLeft', 'ArrowRight', 'Home', 'End', 'Enter'])",
                "new Set(['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', 'Home', 'End'])",
                "new Set(['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', 'Home', 'End', 'PageUp', 'PageDown'])",
            ],
            spelled);
        foreach (var name in new[] { "editing", "overwrite", "point", "pointed", "completion", "completionOverPoint", "neutral", "popoverOpeners", "find", "clipboard" })
            Assert.Contains(name, GridKeys.GateSets.Keys);
        Assert.Contains("const gateSet = (name) => new Set(gateKeys?.[name] ?? []);", script, StringComparison.Ordinal);
        Assert.Equal(["Alt+ArrowDown", "ContextMenu", "Shift+F10"], GridKeys.GateSets["popoverOpeners"].Order(StringComparer.Ordinal));
        Assert.Equal(["Control+F", "Control+f"], GridKeys.GateSets["find"].Order(StringComparer.Ordinal));
    }

    [Fact] // ADR-0058 ("The keyboard") / SH-35: while what Point wrote came from outside, the gate claims Point's keys and the Primary Modifier's arrows, with Shift or without, and nothing else
    public void The_gate_has_a_set_for_point_written_from_outside()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal)).Text;

        // Point's own set holds the Primary Modifier's arrows, with Shift or without (ADR-0051, run 18a).
        Assert.Equal(GridKeys.GateSets["point"], GridKeys.GateSets["pointed"]);
        Assert.Contains("'pointed'", script, StringComparison.Ordinal);
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
        Assert.Matches(new Regex(@"const inEditor = input\.closest\('\.ex-editor'\) !== null;"), script);
        Assert.Matches(new Regex(@"\} else if \(k\.key === 'Home' \|\| k\.key === 'End'\) \{(?:[^{}]*\{[^{}]*\})?[^{}]*placeCaretAtEnd\(input, k, inEditor\);\s*return true;", RegexOptions.Singleline), script);
        Assert.Matches(new Regex(@"const anchor = input\.selectionDirection === 'backward' \? input\.selectionEnd : input\.selectionStart;"), script);
        Assert.Matches(new Regex(@"input\.setSelectionRange\(anchor \?\? edge, edge, 'forward'\);"), script);
        Assert.Matches(new Regex(@"input\.setSelectionRange\(edge, anchor \?\? edge, 'backward'\);"), script);
        Assert.Matches(new Regex(@"input\.setSelectionRange\(edge, edge\);\s*\}\s*showCaret\(input, edge\);\s*\};"), script);
        // From the keydown, and for a key held behind a mode change.
        Assert.Matches(new Regex(@"if \(verdict === 'caret'\) \{[^}]*placeCaretAtEnd\(input, k\);", RegexOptions.Singleline), script);
        Assert.Matches(new Regex(@"\} else if \(verdict === 'caret'\) \{\s*const input = editorInput\(\);\s*if \(input\) \{\s*placeCaretAtEnd\(input, rebased\);"), script);
        Assert.DoesNotMatch(new Regex(@"getBoundingClientRect|offsetWidth|offsetHeight|offsetTop|offsetLeft|clientWidth|clientHeight|scrollWidth|scrollHeight|getComputedStyle|getClientRects"), script);
    }

    [Fact] // ADR-0010 / ED-2 / ADR-0021: a held Ctrl+arrow, Shift+arrow, Ctrl+Backspace or Ctrl+Delete left to an edit is replayed as the field would handle it, from the field's value and selection alone
    public void A_held_chord_left_to_an_edit_is_replayed_as_the_field_would_handle_it()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal)).Text;

        // The edit's field is handed every held key the gate left to it, chords among them: a
        // Ctrl+← held behind F2 was dropped while the End before it moved the caret, so where the
        // caret landed turned on whether the keys arrived before F2's answer (seen in CI on Edge,
        // WebAssembly).
        Assert.Matches(new Regex(@"const typeIntoEditor = \(k\) => \{\s*const input = editorInput\(\);\s*if \(input\) \{\s*typeInto\(input, k\);\s*\}\s*\};"), script);
        // A chord types no character: Ctrl+A is not an `a`.
        Assert.Matches(new Regex(@"if \(k\.text !== undefined \|\| \(k\.key\.length === 1 && !k\.ctrlKey && !k\.metaKey\)\) \{"), script);
        // By a word with Ctrl, and with Option where ⌘ is the Primary Modifier; ⌘'s arrows to the
        // text's ends there, as a held Home or End.
        Assert.Matches(new Regex(@"const byWord = \(k\) => \(metaIsPrimary \? k\.altKey && !k\.ctrlKey : k\.ctrlKey && !k\.altKey\) && !k\.metaKey;"), script);
        Assert.Matches(new Regex(@"if \(metaIsPrimary && primaryOnly\(k\)\) \{\s*placeCaretAtEnd\(input, \{ key: forward \? 'End' : 'Home', shiftKey: k\.shiftKey \}, inEditor\);"), script);
        // A word's edges are the browser's Unicode word breaks over the field's own text; forward,
        // the end of the word, or on Windows the start of the next.
        Assert.Contains("new Intl.Segmenter(undefined, { granularity: 'word' })", script, StringComparison.Ordinal);
        Assert.Matches(new Regex(@"const wordMovePassesSpaces = /\^win/i\.test\(platform\);"), script);
        // The moving end of the selection is its focus; Shift keeps the anchor.
        Assert.Matches(new Regex(@"const anchor = backward \? end : start;\s*const focus = backward \? start : end;"), script);
        // A deletion by a word goes through the same edit, and the field hears it as typing.
        Assert.Matches(new Regex(@"const reach = word \? wordEdge\(input\.value, start, forward\)"), script);
        Assert.DoesNotMatch(new Regex(@"getBoundingClientRect|offsetWidth|offsetHeight|offsetTop|offsetLeft|clientWidth|clientHeight|scrollWidth|scrollHeight|getComputedStyle|getClientRects"), script);
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
}
