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
        // the rows in its place among held keys (ADR-0021's note of 2026-09-27). Scrolling is
        // Blazor's own @onscroll and the gutter is a ResizeObserver, so neither appears here.
        string[] allowed = ["copy", "input", "keydown", "mousedown", "mousemove", "mouseleave", "mouseup", "paste", "selectionchange"];
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

        // One attribute of this instance's own root, nothing wider and nothing measured.
        Assert.Single(Regex.Matches(script.Text, @"new MutationObserver\("));
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
        Assert.Matches(new Regex(@"return claimed\.has\(canonical\) \|\| \(cycleReferences && canonical === 'F4'\) \? 'mode' : null;"), gate.Value);
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
        // The user's move: a press in an editor surface's text, or a caret key left to it — only
        // while the field still holds the text it was made in.
        Assert.Matches(new Regex(@"const movedByUser = \(input\) => caretMoved !== null && caretMoved\.input === input && caretMoved\.text === input\.value;"), script.Text);
        Assert.Equal(3, Regex.Matches(script.Text, @"noteCaretMove\((event\.target|input)\);").Count);
        Assert.DoesNotMatch(new Regex(@"getBoundingClientRect|offsetWidth|offsetHeight|offsetTop|offsetLeft|clientWidth|clientHeight|getComputedStyle|getClientRects"), script.Text);
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
}
