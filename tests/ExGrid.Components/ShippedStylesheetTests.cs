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

    [Fact] // ADR-0051/0021 / DC-24: the key message carries the editor's text and caret, read from the field, nothing measured
    public void The_key_message_carries_the_editors_text_and_caret()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal));

        // The inspection DC-24 names: the one message to OnKeyAsync, with the value and the
        // selection start of the editor surface — a read of the field, no layout read.
        Assert.Single(Regex.Matches(script.Text, @"'OnKeyAsync'"));
        Assert.Matches(new Regex(@"'OnKeyAsync'[^;]*input \? input\.value : null, input \? \(input\.selectionStart \?\? input\.value\.length\) : -1\)",
            RegexOptions.Singleline), script.Text);
        Assert.DoesNotMatch(new Regex(@"getBoundingClientRect|offsetWidth|offsetHeight|getComputedStyle"), script.Text);
    }

    [Fact] // ADR-0050 item 8 / DC-30: the gate takes undo and redo only from C#'s list, and never while editing
    public void The_history_keys_are_claimed_only_through_the_cores_list()
    {
        var script = ShippedAssets().Single(asset => asset.Path.EndsWith("ex-grid.js", StringComparison.Ordinal));

        // The listener names no history key of its own: they reach it only in the set C#
        // hands it, at attach or re-told through setTaken, and that set is consulted only
        // while no edit is open. While one is, the editing sets decide, and they carry none.
        Assert.DoesNotMatch(new Regex(@"'Control\+(Shift\+)?[zZyY]'"), script.Text);
        Assert.Matches(new Regex(@"setTaken: \(keys\) => \{\s*taken = new Set\(keys\);\s*\}"), script.Text);
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
        Assert.Matches(new Regex(@"'OnEditorCaretAsync', input\.value, caret\)"), script.Text);
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
        // placed is not reported back.
        Assert.Matches(new Regex(@"setCaret: \(text, caret\) => \{\s*const input = editorInput\(\);\s*if \(input && input\.value === text\) \{\s*input\.setSelectionRange\(caret, caret\);\s*reportedText = text;\s*reportedCaret = caret;",
            RegexOptions.Singleline), script.Text);
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
