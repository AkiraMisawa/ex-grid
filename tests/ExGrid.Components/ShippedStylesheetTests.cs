using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
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

        var listeners = Regex.Matches(script.Text, @"addEventListener\(\s*'(?<event>[a-z]+)'")
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
        // (ticket 29, decided with the user 2026-09-30).
        string[] allowed = ["compositionend", "copy", "input", "keydown", "mousedown", "mousemove", "mouseleave", "mouseup", "paste", "scroll", "selectionchange"];
        Assert.Equal(allowed.OrderBy(name => name, StringComparer.Ordinal), listeners);
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
    private static (string Css, IReadOnlyList<(string[] Selectors, string Body)> Rules) CoreStylesheet()
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

        var colours = rules
            .Where(rule => rule.Selectors.Contains(".ex-range-single"))
            .Select(rule => Regex.Match(rule.Body, @"outline-color:\s*(?<value>[^;]+);"))
            .Where(colour => colour.Success)
            .Select(colour => colour.Groups["value"].Value.Trim())
            .ToList();

        // A Theme or Wrapper that sets only the Focus outline's colour gets both in it.
        Assert.Equal(["var(--ex-selection-outline, var(--ex-focus-outline, CanvasText))"], colours);
        // And the token is the range outline's alone: nothing else reads it.
        Assert.Single(Regex.Matches(css, "--ex-selection-outline"));
    }

    [Fact] // ADR-0012 (2026-09-29) / ADR-0021 / MEM-4: a reveal's write is held on the root's own reveal number, observed only while held and released on dispose
    public void The_reveal_write_is_held_on_the_roots_reveal_number_only()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal));

        // One attribute of this instance's own root, nothing wider and nothing measured. The
        // module's other observer is the coloured text's, on one attribute of a layer (DC-51).
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
        // Script moves DOM focus in these three places only: this, the hand-back to the root, and
        // the open edit's own focus, each only while the keyboard is this grid's (ADR-0021).
        Assert.Equal(3, Regex.Matches(script.Text, @"\.focus\(").Count);
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
        // key being answered, or only a press, which Blazor keeps in order with it (a double
        // click's second press) — and while an edit is open it starts the hold.
        Assert.Matches(new Regex(@"if \(held\.length === 0 && \(!answering \|\| pressAnswer !== null\)\) \{.*?if \(editing !== 'none'\) \{\s*holdBehindPress\(field !== null \? markStale\(field\) : null\);",
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
        var method = Regex.Match(script.Text, @"focusEditor: \(bar\) => \{.*?\n        \},", RegexOptions.Singleline);
        Assert.True(method.Success, "focusEditor is not in the handle");
        var body = method.Value;

        // Found in the core's own box, a Chrome's control included, as the press back finds it: the
        // core holds no reference to a control it did not render (ADR-0010).
        Assert.Contains("ownSurface(b) === b", body, StringComparison.Ordinal);
        Assert.Contains("surfaceField(box)", body, StringComparison.Ordinal);
        // Granted only while DOM focus is inside this root or on nothing: reclaimFocus's condition.
        Assert.Contains("!active || active === document.body || active === document.documentElement || root.contains(active)", body, StringComparison.Ordinal);
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
        Assert.Matches(new Regex(@"'OnKeyAsync'[^;]*input \? input\.value : null, input \? \(input\.selectionStart \?\? input\.value\.length\) : -1,\s*input \? \(input\.selectionEnd \?\? input\.value\.length\) : -1, input \? movedByUser\(input\) : false,\s*k\.repeat === true\)",
            RegexOptions.Singleline), script.Text);
        Assert.DoesNotMatch(new Regex(@"getBoundingClientRect|offsetWidth|offsetHeight|getComputedStyle"), script.Text);
    }

    [Fact] // ADR-0069/0021 / DC-57: the key message says whether the key is a held key's repeat — a field of the event the listener already reads, the last of the one message
    public void The_key_message_says_whether_the_key_is_a_repeat()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal));

        // Auto-repeat is visible only in the capture-phase listener: by the time a key reaches
        // .NET, a repeat looks like a press. The snapshot every key is gated and held as keeps
        // the browser's flag, and the one message to OnKeyAsync carries it last, so a held key
        // replayed after a hold says so as well. The core raises OnLeave once per press from it.
        Assert.Matches(new Regex(@"const snapshot = \(event\) => \(\{[^}]*repeat: event\.repeat,", RegexOptions.Singleline), script.Text);
        Assert.Matches(new Regex(@"'OnKeyAsync'[^;]*,\s*k\.repeat === true\)\s*\.catch\(", RegexOptions.Singleline), script.Text);
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
        Assert.Matches(new Regex(@"setClaims: \(takenKeys, editable, findable\) => \{\s*taken = new Set\(takenKeys\);"), script.Text);
        Assert.Matches(new Regex(@"if \(!taken\.has\(canonical\)\)"), script.Text);
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
        // The same call places the selection F4's rewrite answered (ADR-0051, 2026-09-29).
        Assert.Matches(new Regex(@"setCaret: \(text, caret, end\) => \{\s*const input = editorInput\(\);\s*if \(input && input\.value === text\) \{\s*if \(movedByUser\(input\)\) \{\s*reportedText = null;\s*reportCaretOf\(input\);\s*return;\s*\}\s*input\.setSelectionRange\(caret, end\);\s*reportedText = text;\s*reportedCaret = caret;",
            RegexOptions.Singleline), script.Text);
        // The user's move: a press in an editor surface's text, or a caret key left to it or
        // answered by the listener on an Apple platform (ticket 32) — only while the field still
        // holds the text it was made in.
        Assert.Matches(new Regex(@"const movedByUser = \(input\) => caretMoved !== null && caretMoved\.input === input && caretMoved\.text === input\.value;"), script.Text);
        Assert.Equal(4, Regex.Matches(script.Text, @"noteCaretMove\((event\.target|input)\);").Count);
        Assert.DoesNotMatch(new Regex(@"getBoundingClientRect|offsetWidth|offsetHeight|offsetTop|offsetLeft|clientWidth|clientHeight|getComputedStyle|getClientRects"), script.Text);
    }

    [Fact] // ADR-0057 / ADR-0021 / DC-51 / DC-47: the coloured text shows only in the focused surface and while the layer's text is the field's value, one class set, nothing measured
    public void The_listener_gates_the_coloured_text_on_its_text_and_sets_one_class()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal));

        // The comparison: the layer's one attribute against the field's value, never while no
        // edit is open, only in the surface the edit is in — the field holding DOM focus, as Excel
        // colours only that one — and never over a field composing. One class, set in one place
        // and taken away only when the edit closes.
        Assert.Matches(new Regex(@"field\.classList\.toggle\('ex-reference-text-shown',\s*editing !== 'none' && field === document\.activeElement && composingIn !== field\s*&& layer\.getAttribute\('data-ex-text'\) === field\.value\);"),
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
        // event — ...
        Assert.Matches(new Regex(@"const onEditorInput = \(event\) => \{\s*heardReferenceInput\(event\);"), script.Text);
        Assert.Matches(new Regex(@"composingIn = event\.isComposing === true \? field : null;"), script.Text);
        // ...when the layer's text changes: one attribute, in this root, observed only while an
        // edit is open and let go when it closes or the instance goes...
        Assert.Matches(new Regex(@"referenceTextObserver\.observe\(root, \{ attributes: true, attributeFilter: \['data-ex-text'\], subtree: true \}\);"), script.Text);
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
        Assert.Single(Regex.Matches(script.Text, @"addEventListener\('compositionend'"));
        Assert.Matches(new Regex(@"const onCompositionEnd = \(event\) => \{\s*const field = event\.target;\s*const layer = [^;]*referenceTextOf\(field\) : null;\s*if \(layer !== null\) \{\s*composingIn = null;\s*gateReferenceText\(field, layer\);\s*\}\s*\};"),
            script.Text);

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

    [Fact] // ADR-0051 second round / DC-31: pointing claims the Shift+arrows; an open list claims only ↑/↓ beside the editing keys
    public void The_gate_has_a_point_set_and_a_completion_set()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal));

        Assert.Matches(new Regex(@"const pointKeys = new Set\(\[\s*\.\.\.overwriteKeys, 'Shift\+ArrowUp', 'Shift\+ArrowDown', 'Shift\+ArrowLeft', 'Shift\+ArrowRight'\]\);"),
            script.Text);
        Assert.Matches(new Regex(@"const completionKeys = new Set\(\[\.\.\.editingKeys, 'ArrowUp', 'ArrowDown'\]\);"), script.Text);
        Assert.Matches(new Regex(@"const claimedWhile = \{ overwrite: overwriteKeys, point: pointKeys, completion: completionKeys \};"), script.Text);
        // A list painted is open from its own render, before the gate is told (ADR-0051/0010):
        // read off the mark the core writes on the list's box, and nothing measured.
        Assert.Matches(new Regex(@"const listShown = \(\) => !!root && root\.querySelector\('\.ex-completion\[data-ex-list\]'\) !== null;"), script.Text);
        Assert.Matches(new Regex(@"const claimed = listShown\(\) \? completionKeys : \(claimedWhile\[editing\] \?\? editingKeys\);"), script.Text);
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
        Assert.Matches(new Regex(@"input\.setSelectionRange\(edge, edge\);\s*\}\s*input\.scrollLeft = toEnd \? Number\.MAX_SAFE_INTEGER : 0;\s*\};"), script);
        // From the keydown, and for a key held behind a mode change.
        Assert.Matches(new Regex(@"if \(verdict === 'caret'\) \{[^}]*placeCaretAtEnd\(input, k\);", RegexOptions.Singleline), script);
        Assert.Matches(new Regex(@"\} else if \(verdict === 'caret'\) \{\s*const input = editorInput\(\);\s*if \(input\) \{\s*placeCaretAtEnd\(input, rebased\);"), script);
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

    [Fact] // ADR-0067 / ADR-0029: the Change Highlight's one token defaults to a tint of the system colour Mark, readable on a dark page as on a light one, and the core only reads it
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

    [Fact] // ADR-0067 / ADR-0006 / UX-16: the mark is a tint over the cell's ground that wins over a stripe and a role, beneath a Missing state's tint and a total row's rule
    public void The_change_highlight_is_a_tint_ordered_against_the_other_grounds()
    {
        var (rules, _) = ForcedColorsSplit();
        int IndexOf(string selector) => rules.ToList().FindIndex(rule => rule.Selectors.Contains(selector));
        const string mark = "linear-gradient(var(--ex-change-highlight-background, color-mix(in srgb, Mark 40%, transparent)), var(--ex-change-highlight-background, color-mix(in srgb, Mark 40%, transparent)))";

        var marked = rules.Where(rule => rule.Selectors.Any(s => s.Contains("ex-changed", StringComparison.Ordinal))).ToList();
        Assert.Equal(
            [".ex-cell.ex-changed", ".ex-row-stripe .ex-pinned.ex-changed", ".ex-row-group .ex-cell.ex-changed",
             ".ex-row-total .ex-cell.ex-changed", ".ex-cell.ex-state-missing.ex-changed"],
            marked.Select(rule => Assert.Single(rule.Selectors)));
        foreach (var (selectors, body) in marked)
        {
            // An image layer, never the shorthand or a colour: either would take the Pinned
            // Column's opaque ground away, and a translucent mark would let the columns passing
            // beneath show through (ADR-0006).
            Assert.Matches(new Regex(@"(?<![\w-])background-image:"), body);
            Assert.DoesNotMatch(new Regex(@"(?<![\w-])background(-color)?\s*:"), body);
            var layers = Regex.Replace(Regex.Match(body, @"background-image:\s*(?<value>[^;]+);").Groups["value"].Value, @"\s+", " ");
            // The mark is the top layer, except under a total row's rule and a Missing state's
            // tint: a line stays a line, and a state is never the thing that disappears.
            var beneath = selectors[0] is ".ex-row-total .ex-cell.ex-changed" or ".ex-cell.ex-state-missing.ex-changed";
            Assert.Equal(beneath, !layers.StartsWith(mark, StringComparison.Ordinal));
            Assert.Contains(mark, layers, StringComparison.Ordinal);
        }
        // At equal specificity the later rule wins: the mark is declared after the stripes, the
        // Row Kinds and Cell State, whose grounds it outranks or keeps, and the combinations in
        // the order stripe, role, state, as those grounds rank among themselves.
        foreach (var ground in new[] { ".ex-row-stripe .ex-pinned", ".ex-row-group .ex-cell", ".ex-row-total .ex-cell", ".ex-cell.ex-state-missing" })
            Assert.True(IndexOf(".ex-cell.ex-changed") > IndexOf(ground), $"{ground} is declared after the mark");
    }

    [Fact] // ADR-0067 / ADR-0027 / DC-55 / UX-7: the forced-colors block restates the mark as a painted outline, before the states so a state keeps its own
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

    [Fact] // ADR-0067 / ADR-0027 P8 / UX-6 / DC-55: a mark comes and goes in one step — nothing in the core's stylesheet transitions or animates
    public void Nothing_in_the_core_stylesheet_transitions_or_animates()
    {
        var (css, _) = CoreStylesheet();

        Assert.DoesNotMatch(new Regex(@"(?<![\w-])(transition|animation)(-[\w-]+)?\s*:|@keyframes"), css);
    }
}
