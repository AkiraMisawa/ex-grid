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
        // note of ADR-0057, DC-51).
        string[] allowed = ["copy", "input", "keydown", "mousedown", "mousemove", "mouseleave", "mouseup", "paste", "scroll", "selectionchange"];
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
        // was made in that field, or a held press on the rows left its focus standing.
        Assert.Contains("active.closest('.ex-formula-bar') !== null", body, StringComparison.Ordinal);
        Assert.Contains("fromField === true", body, StringComparison.Ordinal);
        Assert.Contains("active !== staleField", body, StringComparison.Ordinal);
        // A read of document.activeElement, never of layout.
        Assert.DoesNotMatch(new Regex(@"getBoundingClientRect|offsetWidth|offsetHeight|getComputedStyle"), body);
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

    [Fact] // ADR-0057 / ADR-0021 / DC-51 / DC-47: the coloured text shows only while the layer's text is the field's value, one class set, nothing measured
    public void The_listener_gates_the_coloured_text_on_its_text_and_sets_one_class()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal));

        // The comparison: the layer's one attribute against the field's value, never while no
        // edit is open and never over a field composing. One class, set in one place and taken
        // away only when the edit closes.
        Assert.Matches(new Regex(@"field\.classList\.toggle\('ex-reference-text-shown',\s*editing !== 'none' && composingIn !== field && layer\.getAttribute\('data-ex-text'\) === field\.value\);"),
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
        Assert.Matches(new Regex(@"setEditing: \(mode, reportsCaret, cyclesReferences\) => \{[^}]*\}\s*watchReferenceTexts\(mode !== 'none'\);", RegexOptions.Singleline), script.Text);
        Assert.Matches(new Regex(@"dispose: \(\) => \{.*?watchReferenceTexts\(false\);.*?root = null;", RegexOptions.Singleline), script.Text);
        // ...and when the caret moves in one of this instance's editor fields, while it is open.
        Assert.Matches(new Regex(@"const onSelectionChange = \(\) => \{\s*if \(watchingReferenceTexts && focusedEditorField\(\) !== null\) \{\s*gateReferenceTexts\(\);\s*\}"), script.Text);

        // The layer's line scrolls with its field: the field's scroll offset read, the line's set
        // — the scroll-offset entry — on the field's scroll, heard on this root while an edit is
        // open, and after each comparison.
        Assert.Equal(2, Regex.Matches(script.Text, @"layer\.firstElementChild\.scrollLeft = field\.scrollLeft;").Count);
        Assert.Matches(new Regex(@"root\.addEventListener\('scroll', onFieldScroll, true\);"), script.Text);
        Assert.Matches(new Regex(@"root\.removeEventListener\('scroll', onFieldScroll, true\);"), script.Text);

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
        // and a selection drawn by the field in the selection's own colours.
        var transparent = Regex.Matches(sheet, @"(?m)^[^\n{]*\{[^}]*(?<![\w-])(?:-webkit-text-fill-color|color): transparent[^}]*\}")
            .Select(match => match.Value.Split('{')[0].Trim())
            .ToList();
        Assert.Equal([".ex-reference-text + .ex-reference-text-shown"], transparent);
        Assert.Matches(new Regex(@"\.ex-reference-text \+ \.ex-reference-text-shown \{ -webkit-text-fill-color: transparent; caret-color: currentColor; \}"), sheet);
        Assert.Matches(new Regex(@"\.ex-reference-text \+ \.ex-reference-text-shown::selection \{ color: HighlightText; -webkit-text-fill-color: HighlightText; background-color: Highlight; \}"), sheet);

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
}
