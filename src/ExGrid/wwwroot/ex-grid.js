// The six permitted uses of JavaScript (ADR-0021): the capture-phase keydown listener,
// reading and setting scroll offsets, the clipboard, being told what the scrollbar takes
// out of the box, being told about the pointer — when it moves onto another row, and
// when it comes to rest — and being told the Layout Ceiling (ADR-0053). Beside them, the notes ADR-0021 has added since: a capture-phase
// mousedown and mouseup that keep a press on the rows in its place among held keys, and hold the
// keys after one made while an edit is open; the root taking the keyboard back only while DOM
// focus is still its own; that same mousedown bringing the keyboard back to an edit left
// standing when a press returns to the rows or the headings; that same mousedown, while the grid
// is pointed at through a Pointing Scope, dispatching one event, `ex-press-handed-on`, on the root
// of the grid that points, and the listener for that event on each root, which gives the press
// its place among the keys held there (ADR-0058, "On a circuit"; ADR-0021's note of 2026-09-30);
// and the editor listener keeping the coloured text beneath a field honest (ADR-0057). Anything
// else — text measurement, overlay geometry, popovers — stays in C#; adding to this file needs an
// ADR.
//
// A module returning per-instance handles, never a global: a second grid on the page must
// not reach into the first (ADR-0018). The scroll listener itself is Blazor's @onscroll on
// the instance's own element. Every listener here is on the instance root, but one:
// `selectionchange` fires only on the document, so that one acts only while DOM focus is in
// an editor surface inside this instance's root, and is removed with the instance. One instance
// reaches another in one place only: a press handed on is told to the root its own render names,
// by a DOM event that is the whole message, and nothing of either instance is kept by the other.

/**
 * @param {HTMLElement} root the instance's root element — where keys are captured, so a
 *   grid without focus hears nothing (ADR-0018)
 * @param {HTMLElement} scroller the instance's scroll container
 * @param {object} core the .NET object reference the taken keys are forwarded to
 * @param {string[]} takenKeys canonical forms of the keys the core claims, built by
 *   GridKeys.TakenFor for this grid — this file decides nothing about which keys those are
 * @param {boolean} canEdit whether any column edits at all — a display-only grid must
 *   not take printable keys away from the page (ADR-0010)
 * @param {number} restDelayMs how long the pointer must be still before the core is told
 *   — the core's own constant, so the number lives in one place (ADR-0034)
 * @param {boolean} canFind whether a search is wired — Ctrl+F then opens the find panel,
 *   and the keys after it wait for the panel; otherwise it is refused (ADR-0055)
 * @returns a handle owned by that one grid
 */
export function attach(root, scroller, core, takenKeys, canEdit, restDelayMs, canFind) {
    let taken = new Set(takenKeys);

    // A reveal's scroll write, held until the render that paints its slice has reached the
    // DOM (ADR-0012, 2026-09-29). The core sends the write from inside that render, so on a
    // circuit it arrives as the message just ahead of the render's batch, and a frame
    // painted between the two showed the scroller at its target over the rows it had left:
    // an empty Viewport. The render carries the reveal's number on the root, and the write
    // is made in the same task that applies that render — the attribute's change is
    // observed, and a MutationObserver's callback runs before the browser can paint — so
    // the offset and the slice are painted together. It reads an attribute, never layout,
    // and observes only while a write is held. While it is held, it is where the scroller
    // stands for every read and every conditional write.
    let pendingReveal = null;
    let revealTimer = 0;
    const revealObserver = new MutationObserver(() => applyReveal(false));
    const applyReveal = (anyway) => {
        const reveal = pendingReveal;
        if (!reveal || (!anyway && Number(root.getAttribute('data-ex-reveal')) < reveal.token)) {
            return;
        }
        dropReveal();
        scroller.scrollTop = reveal.top;
        scroller.scrollLeft = reveal.left;
    };
    const holdReveal = (reveal) => {
        pendingReveal = reveal;
        revealObserver.observe(root, { attributes: true, attributeFilter: ['data-ex-reveal'] });
        // A batch that never comes — the circuit went — must not hold the write forever.
        revealTimer = setTimeout(() => applyReveal(true), 2000);
    };
    const dropReveal = () => {
        pendingReveal = null;
        revealObserver.disconnect();
        clearTimeout(revealTimer);
        revealTimer = 0;
    };

    // Whether the synchronous channel exists — WebAssembly has it, a server circuit
    // does not. Probed once with a no-op, so a real .NET failure during a copy is
    // never mistaken for "no channel" (that conflation made a failed copy silent).
    let hasSyncChannel = false;
    try {
        hasSyncChannel = core.invokeMethod('Ping') === true;
    } catch {
        hasSyncChannel = false;
    }

    // Which modifier the user reaches for. Command on an Apple keyboard; on Windows and
    // Linux the Meta key is the OS's — Win+Arrow snaps a window, Super+A opens a shell —
    // and a grid that folded it into Control would act on the ones the window manager
    // happened not to grab. Only the browser can answer this, so it is answered here and
    // handed to C#, which stays the authority on what the key then means.
    const platform = navigator.userAgentData?.platform ?? navigator.platform ?? '';
    const metaIsPrimary = /mac|iphone|ipad|ipod/i.test(platform);

    // The editing mode refines which keys the core claims (ADR-0010): Esc, Enter and
    // Tab are always the core's while editing; the arrows and Home/End only in
    // Overwrite, where they commit and move — in Caret they reach the editor and move
    // its caret. A mode change is a different set. The meaning stays on the C# side;
    // these are gates only, and their mirror is the cases of OnEditingKeyAsync, which
    // reads the same canonical form — the two must move together.
    let editing = 'none';
    // Whether each input in an editor surface reports its caret (ADR-0051), told by C# with
    // the mode: only where completion or pointing is declared.
    let reportCaret = false;
    // Whether F4 joins the editing set (ADR-0051, 2026-09-29: it cycles the Reference at the
    // caret), told by C# with the mode: only where the Consumer declared what it does. Never
    // consulted while no edit is open, where F4 stays the browser's.
    let cycleReferences = false;
    // Whether a popover's contents have a popup of their own open (ADR-0039), told by C#.
    let innerPopup = false;
    // Ctrl+F too, taken and answered with nothing: Find is disabled while a cell is being
    // edited, and the browser's own find would search only the painted rows (ADR-0055).
    const editingKeys = new Set(
        ['Escape', 'Enter', 'Shift+Enter', 'Control+Enter', 'Tab', 'Shift+Tab', 'F2',
            'Control+f', 'Control+F']);
    const overwriteKeys = new Set([
        ...editingKeys, 'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'Home', 'End']);
    // Pointing (ADR-0051's second round): Overwrite's keys and the four Shift+arrows, which
    // extend the outline rather than select text in the input. And while a completion list
    // is open, only its ↑/↓ beside the editing keys (Tab, Escape): ← and → move the caret —
    // unless the list is open over Point, where they point, as they do without it (ADR-0058,
    // the tenth Windows run).
    const pointKeys = new Set([
        ...overwriteKeys, 'Shift+ArrowUp', 'Shift+ArrowDown', 'Shift+ArrowLeft', 'Shift+ArrowRight']);
    const completionKeys = new Set([...editingKeys, 'ArrowUp', 'ArrowDown']);
    const completionOverPointKeys = new Set([...completionKeys, 'ArrowLeft', 'ArrowRight']);
    const claimedWhile = {
        overwrite: overwriteKeys, point: pointKeys, completion: completionKeys, completionOverPoint: completionOverPointKeys,
    };
    // The keys macOS binds to a scroll in a text field, where Windows and Linux move the caret
    // (ADR-0010's note of 2026-09-30, ticket 32): Home and End scroll the document there, PageUp
    // and PageDown a page. Left to the browser in an editor surface, they scrolled the grid away
    // from the open edit — the edited cell left the painted rows, the Cell Editor went, and DOM
    // focus with it. So on Apple platforms, found by the test that makes Meta the Primary
    // Modifier, Home and End left to an editor field are answered here, Shift with them or not:
    // they place the caret or extend the selection as they do elsewhere. PageUp and PageDown,
    // which move no caret in a one-line field, do nothing, on every platform (decided with the
    // user the same day): a browser scrolls the grid with them from a field anywhere. The keys
    // the core claims — Home and End in Overwrite and Point — stay the core's.
    const appleCaretKeys = new Set(['Home', 'End', 'Shift+Home', 'Shift+End']);
    const pageKeys = new Set(['PageUp', 'PageDown']);
    const listShown = () => (root ? root.querySelector('.ex-completion[data-ex-list]') : null);

    // The keys that open a popover from the root (ADR-0039): the popover takes DOM focus a
    // round trip later on a circuit, and a key typed in between must be the popover's, not
    // the grid's (ADR-0010's hold, widened).
    const popoverOpeners = new Set(['Alt+ArrowDown', 'Shift+F10', 'ContextMenu']);

    // Find's key (ADR-0055), both cases for CapsLock, and the modifiers' own keydowns, which
    // mean nothing by themselves.
    const findKeys = new Set(['Control+f', 'Control+F']);
    const modifierKeys = new Set(['Shift', 'Control', 'Alt', 'Meta']);

    // What the gate decides a key is for (ADR-0010): 'popover' — the core's, and it opens a
    // popover, so the keys after it are held until the popover holds DOM focus; 'mode' — the
    // core's, and it can change the editing mode, so the keys after it are held until it is
    // answered;
    // 'core' — the core's, and changes no mode; 'drop' — taken and never forwarded; 'caret' —
    // Home or End on an Apple platform, taken and answered here (ticket 32);
    // null — the browser's, or a control's inside the grid. Read from a snapshot of the
    // event rather than the event itself, so a held key can be gated again, against the
    // mode its predecessor's answer left.
    // The mirror of GridKeys.Canonical — the two must move together. Meta held where it is not
    // primary still appears in the form, so it cannot pass for an unmodified key: nothing in the
    // set carries it, and the browser keeps it.
    const canonicalOf = (k) => {
        const control = k.ctrlKey || (k.metaKey && metaIsPrimary);
        const foreign = k.metaKey && !metaIsPrimary;
        return (control ? 'Control+' : '') + (foreign ? 'Meta+' : '')
            + (k.shiftKey ? 'Shift+' : '') + (k.altKey ? 'Alt+' : '') + k.key;
    };

    const gate = (k) => {
        // The mirror of GridKeys.Canonical — the two must move together. It exists here
        // only to decide whether to take the key: preventDefault has to happen now, and
        // invokeMethodAsync's answer would arrive long after the event is over. What the
        // key MEANS is resolved on the C# side, from the raw fields sent below, so a
        // disagreement between the two shows up as a key that does nothing rather than as
        // a key that does something else.
        const canonical = canonicalOf(k);
        const control = k.ctrlKey || (k.metaKey && metaIsPrimary);
        const foreign = k.metaKey && !metaIsPrimary;

        if (editing === 'none') {
            if (!k.onRoot) {
                // A focusable descendant holds the keyboard — a Consumer's control in a
                // Template Column, whether clicked or entered by Space (ADR-0037). (Not
                // one of the grid's own action buttons: over its own actions the core
                // keeps the keyboard on this root and chooses by aria-activedescendant,
                // and the buttons take focus neither by Tab nor by the pointer — ADR-0020
                // /0037.) The control owns everything but the way out: Escape returns the
                // keyboard to the grid; every other key keeps its meaning in the control —
                // taken from there, Space would type nothing, arrows would move the
                // selection instead of a caret and Ctrl+A would select the grid instead
                // of the field's text.
                // Ctrl+F in one of the grid's own popovers is the grid's too (ADR-0055, settled
                // 2026-09-27): the browser's find would see only the painted rows. In the find
                // field it selects the field's text, the field's own behaviour and no meaning of
                // the core's; anywhere else in a popover it opens Find, as from the root. A
                // Consumer's control in a Template cell keeps the key, as it keeps every other.
                if (k.inPopover && findKeys.has(canonical)) {
                    if (k.inFindField) {
                        return 'select';
                    }
                    return canFind ? 'popover' : 'core';
                }
                if (canonical !== 'Escape') {
                    return null;
                }
                // …unless the control has a popup of its own open — a select's list, a
                // picker's calendar — which its design system draws outside this root while
                // keeping DOM focus on the control. That Escape is the popup's to close, and
                // the next one is the grid's (ADR-0039). The popover's contents report the
                // popup; C# tells this listener.
                //
                // Counted here, not waited for: the report that the popup closed comes a
                // round trip after this Escape on a Server circuit, and a second Escape
                // typed inside it was left to a popup that was no longer there, so nothing
                // closed the panel. A popup that ignores its Escape still gives up the
                // next one, which is the rule as ADR-0039 states it.
                if (innerPopup) {
                    innerPopup = false;
                    return null;
                }
                return 'core';
            }
            // A held Space engages once (ADR-0037). Space is a key that DOES
            // something — it fires a single action — and auto-repeat would fire it
            // again for every repeat, on the same row, for as long as it is held.
            // The repeat is taken (so the page does not scroll by it either) and
            // never forwarded. Shift+Space and Ctrl+Space name whole regions and
            // repeat harmlessly, so only the plain key is filtered.
            if (canonical === ' ' && k.repeat) {
                return 'drop';
            }
            // F2 and a printable character open the Cell Editor (ADR-0010) — only
            // on a grid that has an editable column at all: a display-only grid
            // must not eat the page's keys, round-tripping every keystroke. Whether
            // the focused cell actually edits is still resolved in C#. An AltGr
            // chord reports Control+Alt together on Windows, and is how the
            // German, French and Nordic layouts type @ { [ € — so both-held passes
            // where either alone is a shortcut and stays the browser's. Space opens
            // Overwrite on an editable cell and Interactive on a cell with several
            // actions: it is a mode change either way.
            if (canEdit && !foreign) {
                if (!control && !k.altKey && (k.key === 'F2' || k.key.length === 1)) {
                    return 'mode';
                }
                if (k.ctrlKey && k.altKey && k.key.length === 1) {
                    return 'mode';
                }
            }
            if (!taken.has(canonical)) {
                return null;
            }
            if (popoverOpeners.has(canonical)) {
                return 'popover';
            }
            // Ctrl+F opens a popover only where a search is wired; elsewhere it is refused
            // and opens nothing, and holding the keys after it for a panel that never comes
            // would stall them (ADR-0055).
            if (findKeys.has(canonical)) {
                return canFind ? 'popover' : 'core';
            }
            // Space and Backspace open an editor (ADR-0010/0035): a mode change.
            return canonical === ' ' || canonical === 'Backspace' ? 'mode' : 'core';
        }

        // Overwrite or Caret: the editor normally holds DOM focus, but a click can
        // park it on a Consumer's control mid-edit — from there the control keeps
        // its keys, exactly as it does outside editing. closest, not classList: a
        // substituted Chrome editor is a .ex-editor DIV whose focused control is a
        // descendant (ADR-0010).
        if (!k.onRoot && !k.inEditor) {
            return null;
        }
        // A list of candidates painted is open, whatever the gate was last told: on a circuit
        // the render that paints it and the message that tells the gate are two messages, and a
        // ← typed between them would be claimed as Overwrite's and swallowed. The core marks
        // the list on its box (data-ex-list) in the render itself, and a list open over Point
        // (data-ex-over-point), whose ← and → point (ADR-0058); read, not measured.
        const list = listShown();
        const claimed = list === null
            ? (claimedWhile[editing] ?? editingKeys)
            : (list.hasAttribute('data-ex-over-point') ? completionOverPointKeys : completionKeys);
        // Every key the core claims while editing commits, cancels, moves or switches
        // the mode: each is a mode change. F4 rewrites the text, and the keys after it wait
        // for the rewrite, so that each carries the text the one before it left.
        if (claimed.has(canonical) || (cycleReferences && canonical === 'F4')) {
            return 'mode';
        }
        // Left to an editor field, a key the browser would scroll the grid with is answered here:
        // Home and End on an Apple platform, PageUp and PageDown on every one (ticket 32).
        if (k.inEditor) {
            if (metaIsPrimary && appleCaretKeys.has(canonical)) {
                return 'caret';
            }
            if (pageKeys.has(canonical)) {
                return 'drop';
            }
        }
        return null;
    };

    // The scroll container is the grid's own, not a control inside it: it carries
    // tabindex -1 so that Chrome makes no tab stop of it (ADR-0033), which also lets a press
    // on the rows give it DOM focus, and C# hands focus on to the root — a round trip later
    // on a circuit. A key, a copy or a paste in between is the root's, as it would have
    // been a moment on.
    const isRoot = (target) => target === root || (!!scroller && target === scroller);
    const snapshot = (event) => ({
        key: event.key,
        ctrlKey: event.ctrlKey,
        shiftKey: event.shiftKey,
        altKey: event.altKey,
        metaKey: event.metaKey,
        repeat: event.repeat,
        onRoot: isRoot(event.target),
        inEditor: event.target instanceof Element && event.target.closest('.ex-editor') !== null,
        inPopover: popoverOf(event.target) !== null,
        inFindField: isTextField(event.target) && event.target.closest('.ex-popover-find') !== null,
    });

    // While editing, the key carries the editor's text and caret (ADR-0051): the core decides
    // what an arrow means from what the user sees now, never from an answer a round trip old.
    // Read as the key is forwarded — a held key's after the keys before it were typed — from
    // the editor surface that holds DOM focus. A read of the field's own value, not a
    // measurement: no layout is read. The selection's end goes with it for F4, which cycles
    // every Reference a selection covers, and so does whether the user moved the caret in that
    // very text: until the core's placement lands, the browser's own caret there is not the
    // user's, and the core tells the two apart as it does for a report (ADR-0051, 2026-09-29).
    const forward = (k) => {
        const input = editing !== 'none' ? editorInput() : null;
        return core.invokeMethodAsync(
            'OnKeyAsync', k.key, k.ctrlKey, k.shiftKey, k.altKey, k.metaKey, metaIsPrimary, !k.onRoot,
            input ? input.value : null, input ? (input.selectionStart ?? input.value.length) : -1,
            input ? (input.selectionEnd ?? input.value.length) : -1, input ? movedByUser(input) : false)
            .catch((error) => {
                // Disposal can overtake a key in flight, and that is not a fault. Anything
                // else is reported: a swallowed failure here means keys that silently stop
                // working.
                if (core) {
                    console.error('[ex-grid] the grid failed to handle a key', error);
                }
            });
    };

    // The caret in an editor surface is reported (ADR-0051's second round): an input event
    // carries no caret, and working it out from the change is ambiguous where letters repeat;
    // and a caret moved by ← / → or a click inside the text, with the text unchanged, would
    // leave the core pointing and completing from a caret that is no longer there. The value
    // and selection start of the field, read, not measured: no layout is read. Only while C#
    // asked for it, and never a report that says what the last one said.
    let reportedText = null;
    let reportedCaret = -1;
    // The user's own move of the caret in an editor surface — a press in its text, or a caret
    // key the browser carries out — with the text it was made in. The core places the caret a
    // round trip after it wrote the text on a circuit, and a move made in that text before the
    // placement lands is newer than the placement: the user's caret stands, and the report of
    // it says it is the user's, so the core takes it over the placement it has in flight
    // (ADR-0051's third round: a press in the text while pointing ends pointing; found on the
    // Server host, Windows, fourth run, DC-19/DC-34). Only while the field still holds that
    // text: once it has changed, the move was made in text the core no longer holds.
    let caretMoved = null;
    const noteCaretMove = (input) => {
        caretMoved = { input, text: input.value };
    };
    const movedByUser = (input) => caretMoved !== null && caretMoved.input === input && caretMoved.text === input.value;
    const reportCaretOf = (input) => {
        const caret = input.selectionStart ?? input.value.length;
        if (input.value === reportedText && caret === reportedCaret) {
            return;
        }
        reportedText = input.value;
        reportedCaret = caret;
        core.invokeMethodAsync('OnEditorCaretAsync', input.value, caret, movedByUser(input))
            .catch((error) => {
                if (core) {
                    console.error('[ex-grid] the grid failed to hear the caret', error);
                }
            });
    };
    // The editor surface of this grid's own that `element` is, or is inside: the box that wears
    // .ex-editor — the Cell Editor over this grid's rows, or its Formula Bar's text — and never
    // a surface of a grid nested in one of its cells, whose own root stands nearer (ADR-0018).
    const ownSurface = (element) => {
        const surface = element instanceof Element ? element.closest('.ex-editor') : null;
        return surface !== null && root !== null && surface.closest('.ex-grid') === root ? surface : null;
    };
    // The text field of an editor surface: of `surface`, one of this grid's own, or, given none,
    // of the first of them in the markup — the Cell Editor while its cell is painted, the
    // Formula Bar's text when it is not. A substituted Chrome's surface is a box around its
    // control (ADR-0010): the control that has DOM focus inside it, if one has, or its first.
    const surfaceField = (surface) => {
        const chosen = surface
            ?? (root ? [...root.querySelectorAll('.ex-editor')].find((box) => ownSurface(box) === box) : null);
        if (!chosen) {
            return null;
        }
        if (chosen instanceof HTMLInputElement || chosen instanceof HTMLTextAreaElement) {
            return chosen;
        }
        const active = document.activeElement;
        return (active instanceof HTMLInputElement || active instanceof HTMLTextAreaElement) && chosen.contains(active)
            ? active
            : chosen.querySelector('input, textarea');
    };

    // The editor surface that last held the keyboard here (ADR-0018, section 6): the one of this
    // grid's own in which this listener last saw a key, an input or a press. An edit stands when
    // DOM focus leaves the root, and a press back on the rows or the headings puts the keyboard
    // here (onPress). Taken afresh as an edit opens, from the surface holding DOM focus then, and
    // forgotten as it ends: the bar outlives an edit, and one typed in there must not claim the
    // next.
    let lastSurface = null;
    const noteSurface = (target) => {
        lastSurface = ownSurface(target) ?? lastSurface;
    };

    // Each input reports at once: Blazor's input event is on its way, and the core waits for
    // this report before it asks anything about the new text.
    const onEditorInput = (event) => {
        heardReferenceInput(event);
        const input = event.target;
        noteSurface(input);
        if (!reportCaret || !core || !(input instanceof HTMLInputElement || input instanceof HTMLTextAreaElement)
            || input.closest('.ex-editor') === null) {
            return;
        }
        // Typed on: the text a move was made in is gone, and so is the move.
        caretMoved = null;
        reportCaretOf(input);
    };
    root.addEventListener('input', onEditorInput, true);
    // The text field of an editor surface in this instance that holds DOM focus, or null.
    const focusedEditorField = () => {
        const active = document.activeElement;
        return root && (active instanceof HTMLInputElement || active instanceof HTMLTextAreaElement)
            && root.contains(active) && active.closest('.ex-editor') !== null
            ? active
            : null;
    };
    // Whenever the caret moves: `selectionchange` fires on the document only, so this instance
    // acts on it only while DOM focus is in one of its own editor surfaces (ADR-0018). A burst
    // — a held arrow, a drag across the text, an input's own change — is one report per
    // animation frame, of where the caret stands when the frame comes.
    let caretFrame = 0;
    const onSelectionChange = () => {
        if (watchingReferenceTexts) {
            gateReferenceTexts();
        }
        if (!reportCaret || !core || caretFrame !== 0 || focusedEditorField() === null) {
            return;
        }
        caretFrame = requestAnimationFrame(() => {
            caretFrame = 0;
            const input = reportCaret && core ? focusedEditorField() : null;
            if (input) {
                reportCaretOf(input);
            }
        });
    };
    document.addEventListener('selectionchange', onSelectionChange);

    // The coloured text (ADR-0057): beneath an editor surface, a layer the core renders with each
    // Reference in its colour stands immediately before the field, and carries the text it was
    // rendered for (data-ex-text). On a circuit that is a round trip behind the typing, and colours
    // on text typed past would stand on the wrong characters, so the layer shows — and the field's
    // own text turns transparent — only while the two texts are one: the listener sets one class on
    // the field then, and the stylesheet does the rest. Only in the surface the edit is in, as Excel
    // colours it: the field holding DOM focus, the one held keys are handed to (ADR-0051) — the
    // other surface keeps its plain text, and a press from one into the other takes the colours
    // with it. Compared on each input, when a layer's text changes (that one attribute, observed
    // while an edit is open) and whenever the selection moves, which is how a field that has just
    // taken focus, or just opened over its layer, is heard. A field holding an IME composition is
    // ahead of anything rendered, and is never shown over; every input of a composition, its last
    // included, says so, and the composition's end comes with no input after it, so the end is
    // heard too, and the colours come back then rather than at the next keystroke. The layer's
    // line scrolls with the field: the scroll-offset entry, on one more element (ADR-0021). Reads
    // values, one attribute, which element has focus and scroll offsets; no layout.
    let composingIn = null;
    let watchingReferenceTexts = false;
    const referenceTextOf = (field) => {
        const layer = field.previousElementSibling;
        return layer !== null && layer.classList.contains('ex-reference-text') ? layer : null;
    };
    const gateReferenceText = (field, layer) => {
        field.classList.toggle('ex-reference-text-shown',
            editing !== 'none' && field === document.activeElement && composingIn !== field
            && layer.getAttribute('data-ex-text') === field.value);
        layer.firstElementChild.scrollLeft = field.scrollLeft;
    };
    const gateReferenceTexts = () => {
        for (const layer of root.getElementsByClassName('ex-reference-text')) {
            const field = layer.nextElementSibling;
            if (field instanceof HTMLInputElement || field instanceof HTMLTextAreaElement) {
                gateReferenceText(field, layer);
            }
        }
    };
    const referenceTextObserver = new MutationObserver(gateReferenceTexts);
    const heardReferenceInput = (event) => {
        const field = event.target;
        const layer = field instanceof HTMLInputElement || field instanceof HTMLTextAreaElement ? referenceTextOf(field) : null;
        if (layer !== null) {
            composingIn = event.isComposing === true ? field : null;
            gateReferenceText(field, layer);
        }
    };
    const onFieldScroll = (event) => {
        const field = event.target;
        const layer = field instanceof HTMLInputElement || field instanceof HTMLTextAreaElement ? referenceTextOf(field) : null;
        if (layer !== null) {
            layer.firstElementChild.scrollLeft = field.scrollLeft;
        }
    };
    const onCompositionEnd = (event) => {
        const field = event.target;
        const layer = field instanceof HTMLInputElement || field instanceof HTMLTextAreaElement ? referenceTextOf(field) : null;
        if (layer !== null) {
            composingIn = null;
            gateReferenceText(field, layer);
        }
    };
    // Only while an edit is open, which is the only time a layer holds a text; closing takes every
    // field's class away with it.
    const watchReferenceTexts = (on) => {
        if (on === watchingReferenceTexts) {
            return;
        }
        watchingReferenceTexts = on;
        if (on) {
            referenceTextObserver.observe(root, { attributes: true, attributeFilter: ['data-ex-text'], subtree: true });
            root.addEventListener('scroll', onFieldScroll, true);
            root.addEventListener('compositionend', onCompositionEnd, true);
            return;
        }
        referenceTextObserver.disconnect();
        root.removeEventListener('scroll', onFieldScroll, true);
        root.removeEventListener('compositionend', onCompositionEnd, true);
        composingIn = null;
        for (const field of root.querySelectorAll('.ex-reference-text-shown')) {
            field.classList.remove('ex-reference-text-shown');
        }
    };

    // Keys that follow a mode change are held until it lands (ADR-0010). The mode is
    // C#'s, and it tells this listener after the fact: in-process on WebAssembly, a round
    // trip later on a Blazor Server circuit. Gated against the mode it was last told, a
    // key typed in that gap would be forwarded as the wrong thing — `1500` typed onto a
    // cell would open Overwrite with `1` and lose `500` — so while a mode-changing key is
    // unanswered every key after it waits here, in order, and is gated again once the
    // answer has landed. Plain navigation changes no mode and is never held behind.
    const held = [];
    let answering = false;
    // A press on the rows while an edit is open is a mode change too (ADR-0010, widened
    // 2026-09-29), and the keys after it wait for the core's answer to it (holdBehindPress):
    // the press still to be asked about, and the answer the keys wait for while one is awaited.
    let pressToAsk = null;
    let pressAnswer = null;
    // The answer the drain waits for first, when it is the answer to a press on another grid,
    // handed on to this root (onPressHandedOn): a press on this grid's own rows does not pass on
    // ahead of it, as it would behind one of its own — Blazor keeps this grid's presses in order,
    // and that one travels through the other grid's core.
    let handedOnAnswer = null;
    // A field beside the rows — the Formula Bar or the Name Box — still holding DOM focus only
    // because a press on the rows had the default that would have moved it suppressed: held
    // here, or passed on while an edit is open where the core keeps the keyboard in the edit
    // (onPress). Null once the field is pressed again, the hand-back has taken it, or the press
    // has been answered (replayPress, askAboutPress). Each mark is numbered, so taking one
    // press's mark off never takes a later press's.
    let staleField = null;
    let staleMarks = 0;
    const markStale = (field) => {
        staleField = field;
        return ++staleMarks;
    };

    // Whether the editor holds DOM focus. Until it does, a key typed with editing on lands
    // on the root, where no editing mode claims a printable key — it would be lost.
    const editorFocused = () => {
        const active = document.activeElement;
        return active instanceof Element && root.contains(active) && active.closest('.ex-editor') !== null;
    };
    // Whether the core's request that the open edit take the keyboard was declined, because the
    // keyboard had gone to another grid or a control of the page's before it landed (focusEditor,
    // ADR-0021's note of 2026-09-30). The edit stands without DOM focus; the keys held behind the
    // key that opened it are this edit's all the same, and go into it rather than wait for a focus
    // that will not come (ADR-0010, same day). Forgotten as an edit opens or ends, and once a press
    // back puts the keyboard in the edit.
    let focusDeclined = false;

    // Whether one of this grid's popovers holds DOM focus — where the keys held behind a key
    // that opened one are handed.
    const popoverFocused = () => {
        const active = document.activeElement;
        return active instanceof Element && root.contains(active) && active.closest('.ex-popover') !== null;
    };
    let awaitingPopover = false;

    // The keyboard handed on inside a popover, or out of it (ADR-0044): Tab, Shift+Tab or E on
    // a command moves it into the filter below, E on the value list to the search box, and a
    // sentinel either side of the filter hands it back to the commands; a command run — Enter
    // or Space on an item, a column's letter — or Enter in the filter's text field closes the
    // popover and gives it back to the root. Each lands a round trip later on a circuit, and
    // each is a change of who holds the keyboard, so ADR-0010's rule holds as it does for
    // opening a popover: the keys after it are held until DOM focus has moved, or the popover
    // is gone, then handed on in order. Which letters run a command is written by the core
    // on the commands (data-ex-letters: the enabled ones), and the value list is the element
    // its Chrome marks ex-value-list; only Tab, Shift+Tab and E are this listener's own
    // mirror of MenuKeys.ResolveInColumnMenu — the two must move together.
    let awaitingMove = null;
    const popoverOf = (target) => {
        const popover = target instanceof Element ? target.closest('.ex-popover') : null;
        return popover && root.contains(popover) ? popover : null;
    };
    const plain = (k) => !k.ctrlKey && !k.altKey && !k.metaKey;
    const runsLetter = (popover, k) => {
        const letters = popover.querySelector('.ex-popover-commands')?.getAttribute('data-ex-letters') ?? '';
        return k.key.length === 1 && letters.includes(k.key.toUpperCase());
    };
    const handsOver = (target, k) => {
        const popover = popoverOf(target);
        if (!popover || !plain(k)) {
            return null;
        }
        const closes = { from: target, closes: popover };
        const withFilter = popover.querySelector('.ex-popover-filter') !== null;
        const letterE = k.key === 'e' || k.key === 'E';
        if (target.closest('[role=menu]')) {
            if (withFilter && (k.key === 'Tab' || letterE)) {
                return { from: target, into: '.ex-popover-filter' };
            }
            if (target.matches('[role=menuitem]') && ((k.key === 'Enter' && !k.shiftKey) || (k.key === ' ' && !k.shiftKey))) {
                return closes;
            }
            return target.closest('.ex-popover-commands') && runsLetter(popover, k) ? closes : null;
        }
        if (target.closest('.ex-value-list')) {
            if (letterE) {
                return { from: target, into: '.ex-popover-filter' };
            }
            return runsLetter(popover, k) ? closes : null;
        }
        return k.key === 'Enter' && isTextField(target) && target.closest('.ex-popover-filter') ? closes : null;
    };
    const onSentinel = (target) => target instanceof Element && target.classList.contains('ex-focus-wrap')
        && popoverOf(target) !== null;
    const moved = () => {
        if (!awaitingMove) {
            return true;
        }
        if (awaitingMove.closes) {
            return !awaitingMove.closes.isConnected;
        }
        const active = document.activeElement;
        return active instanceof Element && active !== awaitingMove.from
            && root.contains(active) && active.closest(awaitingMove.into) !== null;
    };

    // The editor the answer opened, once its element is there — the render that made it
    // and the answer travel separately, and the answer can arrive first. Of the editor
    // surfaces, the one holding DOM focus: the Formula Bar when the user was typing there,
    // not the cell's editor that comes first in the markup (ADR-0051). The first one only
    // when none holds it — the two-second hold has run out.
    // Declined its focus, the edit is the surface that last held the keyboard here, as the press
    // back finds it (standingField).
    const editorInput = () => (focusDeclined ? standingField() : surfaceField(ownSurface(document.activeElement)));
    // When the hold that is standing began: two seconds from it, whatever the keys held
    // since have asked for, the rest is handed on (ADR-0010).
    let holdStartedAt = 0;
    const editorSettled = () => new Promise((resolve) => {
        const look = () => {
            // A grid disposed during the wait has nothing left to wait for, and is asked
            // first: its root is gone, and looking for DOM focus inside it would throw from
            // the next frame, after the page that held it has moved on. Two seconds is past
            // any round trip the grid is usable over; after it the held keys are replayed
            // against whatever there is, rather than held forever.
            const disposed = !core;
            const editorReady = disposed || editing === 'none' || editorFocused() || focusDeclined;
            const popoverReady = disposed || !awaitingPopover || popoverFocused();
            if (disposed || (editorReady && popoverReady && moved()) || performance.now() - holdStartedAt > 2000) {
                awaitingPopover = false;
                awaitingMove = null;
                resolve();
            } else {
                requestAnimationFrame(look);
            }
        };
        look();
    });

    // A held key a text field would have handled itself, handled as it would have: text is
    // typed at the caret, a deletion deletes, a caret key moves the caret. Anything else
    // was the browser's, and its moment has passed. Whether it was one of these is answered.
    const typeInto = (input, k) => {
        const start = input.selectionStart ?? input.value.length;
        const end = input.selectionEnd ?? start;
        if (k.key.length === 1) {
            input.setRangeText(k.key, start, end, 'end');
        } else if (k.key === 'Backspace') {
            input.setRangeText('', start === end ? Math.max(0, start - 1) : start, end, 'end');
        } else if (k.key === 'Delete') {
            input.setRangeText('', start, start === end ? Math.min(input.value.length, end + 1) : end, 'end');
        } else if (k.key === 'Home' || k.key === 'End') {
            // As the field would have: to the start or the end, Shift extending the selection, and
            // scrolled to show it. Setting the selection from script moves no view, so a held End
            // replayed on a circuit left the caret at the end and the text shown from its start
            // (ticket 32, seen in CI on the Server host).
            placeCaretAtEnd(input, k, input.closest('.ex-editor') !== null);
            return true;
        } else if (k.key === 'ArrowLeft' || k.key === 'ArrowRight') {
            const at = k.key === 'ArrowLeft' ? Math.max(0, start - 1)
                : Math.min(input.value.length, end + 1);
            if (input.closest('.ex-editor') !== null) {
                noteCaretMove(input);
            }
            input.setSelectionRange(at, at);
            return true;
        } else {
            return false;
        }
        // The field hears it as it hears typing (its @oninput).
        input.dispatchEvent(new Event('input', { bubbles: true }));
        return true;
    };
    // Home or End answered in an editor field on an Apple platform (ticket 32), as Windows and
    // Linux answer them, and a held Home or End replayed on any platform: the caret to the
    // text's start or end, or with Shift the selection extended there from its anchor; in an
    // editor surface, the user's own move. The field is scrolled to show that end — setting
    // the offset from script moves no view by itself, and a number past the far end is clamped
    // to it by the browser — so nothing is read but the field's value and selection (ADR-0021).
    const placeCaretAtEnd = (input, k, ownMove = true) => {
        const toEnd = k.key === 'End';
        const edge = toEnd ? input.value.length : 0;
        if (ownMove) {
            noteCaretMove(input);
        }
        if (k.shiftKey) {
            const anchor = input.selectionDirection === 'backward' ? input.selectionEnd : input.selectionStart;
            if (toEnd) {
                input.setSelectionRange(anchor ?? edge, edge, 'forward');
            } else {
                input.setSelectionRange(edge, anchor ?? edge, 'backward');
            }
        } else {
            input.setSelectionRange(edge, edge);
        }
        input.scrollLeft = toEnd ? Number.MAX_SAFE_INTEGER : 0;
    };
    const typeIntoEditor = (k) => {
        const input = editorInput();
        if (input && !k.ctrlKey && !k.metaKey) {
            typeInto(input, k);
        }
    };

    // The text fields a held key can be typed into: those with a caret the selection API
    // reaches.
    // Not a read-only one — a picker's display, say — whose text a held key must not change,
    // nor one a design system dresses as a select.
    const isTextField = (element) => (element instanceof HTMLTextAreaElement
        || (element instanceof HTMLInputElement && ['text', 'search', 'url', 'tel', 'password'].includes(element.type)))
        && !element.readOnly && !element.disabled && element.getAttribute('role') !== 'combobox';

    // A held key handed to the popover that now holds DOM focus, as the keydown it would
    // have received: what it means there is the popover's (ADR-0039) — its own handlers
    // run, and the core answers the menu's keys against its own place. Marked, so this
    // listener lets it through instead of holding it again. A keydown dispatched from here
    // types nothing and submits nothing, so in a text field the key is typed at the caret,
    // as the editor's are, and Enter submits the field's form, as the key itself would
    // (ADR-0010).
    let replaying = false;
    const replayInto = (target, k) => {
        replaying = true;
        try {
            target.dispatchEvent(new KeyboardEvent('keydown', {
                key: k.key, ctrlKey: k.ctrlKey, shiftKey: k.shiftKey, altKey: k.altKey, metaKey: k.metaKey,
                bubbles: true, cancelable: true,
            }));
        } finally {
            replaying = false;
        }
    };

    // Whether a held key can be handed on as it would have acted. A menu's keys and the
    // value list's letters are handlers' to answer, and a text field's are typed here; a key
    // whose effect is the browser's own — Tab moving DOM focus, Space ticking a checkbox,
    // an arrow in a select — does nothing when dispatched from script, and moving focus
    // from script is what ADR-0021 keeps out. Such a key, and every key held behind it, is
    // dropped: the typing stops short rather than going on in a field it was not meant for
    // ("Alpha", Tab, Space, Enter would otherwise search for "Alpha " and apply it).
    const caretKeys = new Set(['Backspace', 'Delete', 'ArrowLeft', 'ArrowRight', 'Home', 'End', 'Enter']);
    // The keys that move the caret in a field without changing its text.
    const caretMoveKeys = new Set(['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', 'Home', 'End', 'PageUp', 'PageDown']);
    const reproducible = (target, k) => {
        if (k.key === 'Escape' || target.closest('[role=menu]')) {
            return true;
        }
        if (isTextField(target)) {
            return !k.ctrlKey && !k.metaKey && !k.altKey && (k.key.length === 1 || caretKeys.has(k.key));
        }
        const role = target.getAttribute('role');
        return k.key.length === 1 && k.key !== ' ' && !k.ctrlKey && !k.metaKey && !k.altKey
            && target.tagName !== 'SELECT' && role !== 'combobox' && role !== 'listbox';
    };

    // A control of the grid's own that holds DOM focus — in a popover, or a Template
    // Column's field that Space has just handed the keyboard (ADR-0037) — is where a held
    // key goes, as the keydown it would have received (ADR-0010). The root, its scroller
    // and the Cell Editor are the gate's.
    const focusedControl = () => {
        const active = document.activeElement;
        return active instanceof Element && root.contains(active) && !isRoot(active)
            && active.closest('.ex-editor') === null ? active : null;
    };

    const handToPopover = (target, k) => {
        if (isTextField(target) && !k.ctrlKey && !k.metaKey && !k.altKey) {
            if (k.key === 'Enter' && target.form) {
                // The keydown first, so the field's own handlers hear what the key was — Shift
                // held turns the find panel's Enter into "previous" (ADR-0055) — then the
                // submission, which a dispatched keydown does not perform.
                replayInto(target, k);
                target.form.requestSubmit();
                return;
            }
            if (typeInto(target, k)) {
                return;
            }
        }
        replayInto(target, k);
    };

    // A hold begins: the keys typed from now are held, in order, and handed on by drain — at
    // once, or once `answer` has come, for a key the core is answering. The two-second fallback
    // counts from here. Each caller asks first whether a hold stands, and a drain already running
    // takes what it holds.
    const startHold = (answer) => {
        answering = true;
        holdStartedAt = performance.now();
        if (answer) {
            answer.then(drain);
        } else {
            drain();
        }
    };

    const drain = async () => {
        // Behind a press, its answer first (holdBehindPress); a press passed on behind it while
        // nothing was held replaces the question, and its answer comes after the first one's.
        while (pressAnswer !== null) {
            const answer = pressAnswer;
            await answer;
            if (pressAnswer === answer) {
                pressAnswer = null;
            }
            if (handedOnAnswer === answer) {
                handedOnAnswer = null;
            }
        }
        // Settled before anything else, even with nothing held yet: the answer can arrive
        // before the popover or the editor has taken DOM focus, and a key typed in that
        // gap must still be held, not gated against the root.
        await editorSettled();
        while (held.length > 0 && core) {
            const k = held.shift();
            if (k.barPress) {
                await answerBarPress();
                continue;
            }
            if (k.press) {
                await replayPress(k);
                continue;
            }
            // A press on another grid, handed on to this root: its turn has come, so its answer
            // may be given now, and the keys after it wait for that answer (onPressHandedOn).
            if (k.handedOn) {
                k.handedOn.inTurn();
                await k.handedOn.answered;
                await editorSettled();
                continue;
            }
            // A modifier's own keydown — the Shift pressed for a capital, or for Shift+Enter —
            // is held with the rest to keep their order, and means nothing by itself: the key
            // that follows carries it. Replaying it would be a key no field can reproduce, and
            // that stops the replay there, dropping every key typed after it.
            if (modifierKeys.has(k.key)) {
                continue;
            }
            const target = focusedControl();
            // Find's key held behind a popover is the grid's there too (ADR-0055), and is not
            // replayed into the popover — a menu would take it as a keydown of its own, and a
            // text field cannot reproduce a Ctrl chord at all, which would drop it and every
            // key after it. In the find field it selects the text; anywhere else in a popover
            // the core is asked to open Find, and the keys after it wait for the panel.
            if (target && popoverOf(target) && findKeys.has(canonicalOf(k))) {
                if (isTextField(target) && target.closest('.ex-popover-find')) {
                    target.select();
                    continue;
                }
                const aimed = { ...k, onRoot: false, inEditor: false, inPopover: true, inFindField: false };
                awaitingPopover = canFind;
                await forward(aimed);
                await editorSettled();
                continue;
            }
            if (target) {
                // A held key that itself sends the keyboard across the popover holds the
                // rest again, until DOM focus has followed it.
                if (!reproducible(target, k)) {
                    held.length = 0;
                    break;
                }
                const move = handsOver(target, k);
                handToPopover(target, k);
                if (move) {
                    awaitingMove = move;
                    await editorSettled();
                }
                continue;
            }
            // Aimed at the grid when it was pressed; after the answer, the grid's
            // keyboard is the editor if one stands, and the root if not.
            const rebased = {
                ...k, onRoot: editing === 'none', inEditor: editing !== 'none', inPopover: false, inFindField: false,
            };
            const verdict = gate(rebased);
            if (verdict === 'mode' || verdict === 'popover') {
                awaitingPopover = verdict === 'popover';
                await forward(rebased);
                await editorSettled();
            } else if (verdict === 'core') {
                forward(rebased);
            } else if (verdict === 'caret') {
                const input = editorInput();
                if (input) {
                    placeCaretAtEnd(input, rebased);
                }
            } else if (verdict === null && editing !== 'none') {
                typeIntoEditor(rebased);
            }
        }
        answering = false;
    };

    const onKeyDown = (event) => {
        if (!core) {
            return;
        }
        // A key typed in an editor surface: that surface holds the keyboard.
        noteSurface(event.target);
        // Mid-composition an IME owns Enter, Escape and the arrows — they choose and
        // commit a candidate. Taking them there breaks typing in any language that needs
        // one, and the grid would move under a half-finished word.
        if (event.isComposing || event.keyCode === 229) {
            return;
        }
        const k = snapshot(event);
        // While the root itself holds the keyboard, the document keeps no text selection: a
        // key here is the grid's, and so is the clipboard command it may be (ADR-0005). The
        // browser aims `copy` and `paste` at the element holding the document's selection, and
        // at the focused element only while there is none. An edit that ends leaves a collapsed
        // caret where its field stood, and the next press on the rows — user-select: none —
        // moves that caret to the nearest text the page can select, outside the grid: every
        // Ctrl+C and Ctrl+V after the first edit went there, and the root never heard them,
        // until DOM focus left the grid and came back (found on /sheet, 2026-09-29). Dropped
        // here, in the keydown, before the browser runs the command it is the default of. The
        // user's own selection is never the grid's to copy (CP-10); a selection inside a field
        // is not touched, since the root does not hold the keyboard then.
        if (k.onRoot) {
            const selection = document.getSelection();
            if (selection && selection.rangeCount > 0) {
                selection.removeAllRanges();
            }
        }
        // Held while a mode change is unanswered, and also while editing is on but the
        // editor has not yet taken DOM focus — the answer can land before the focus does.
        // Every key under the root is held then, whatever it was aimed at: one that went
        // straight to a popover that had just taken focus would overtake the keys typed
        // before it. A key this listener is handing on itself is let through.
        // A key landing on a sentinel is held too, until the commands have DOM focus.
        const sentinel = !replaying && !answering && onSentinel(event.target);
        // A hold behind a move or a close is over the moment DOM focus has followed, though
        // its check runs a frame later: a key typed in between is the new holder's already.
        const holdOver = answering && awaitingMove !== null && held.length === 0 && moved();
        if (!replaying && !holdOver && (answering || sentinel || (editing !== 'none' && k.onRoot && !editorFocused()))) {
            event.preventDefault();
            event.stopPropagation();
            held.push(k);
            if (!answering) {
                if (sentinel) {
                    // A column's popover wraps back to its commands; the find panel, which
                    // has none, to its own contents (ADR-0044/0055).
                    const into = popoverOf(event.target)?.querySelector('.ex-popover-commands')
                        ? '.ex-popover-commands'
                        : '.ex-popover-find-body';
                    awaitingMove = { from: event.target, into };
                }
                startHold();
            }
            return;
        }
        // A key that hands the keyboard on inside a popover, or out of it, is the popover's
        // to answer; only the keys after it wait. A move's own default goes: on WebAssembly
        // the core moves DOM focus within this very keydown, and the E would be typed into
        // the search box it has just been sent to. A close keeps it — Enter in the filter's
        // field submits its form by it.
        const move = replaying ? null : handsOver(event.target, k);
        if (move) {
            if (move.into) {
                event.preventDefault();
            }
            awaitingMove = move;
            // Behind a hold that is over but not yet cleared, its drain takes this one too, and
            // counts its fallback from here.
            if (answering) {
                holdStartedAt = performance.now();
            } else {
                startHold();
            }
            return;
        }
        const verdict = gate(k);
        if (verdict === null) {
            // A caret key left to an editor surface moves the caret there: the user's move.
            if (k.inEditor && caretMoveKeys.has(event.key) && isTextField(event.target) && root.contains(event.target)) {
                noteCaretMove(event.target);
            }
            return;
        }
        event.preventDefault();
        event.stopPropagation();
        if (verdict === 'drop') {
            return;
        }
        if (verdict === 'select') {
            event.target.select();
            return;
        }
        if (verdict === 'caret') {
            const input = event.target instanceof HTMLInputElement || event.target instanceof HTMLTextAreaElement
                ? event.target
                : editorInput();
            if (input) {
                placeCaretAtEnd(input, k);
            }
            return;
        }
        const answer = forward(k);
        if (verdict === 'mode' || verdict === 'popover') {
            awaitingPopover = verdict === 'popover';
            startHold(answer);
        }
    };

    // The fifth allowlist entry (ADR-0021): the moves are heard here, and two things —
    // only two — are reported. The pointer coming to REST, for the error popover
    // (ADR-0034); and the pointer moving onto another ROW, for the hover band
    // (ADR-0029). A Blazor handler on the Viewport would cost an interop call per move —
    // a wire round trip per frame on a Server circuit, which is why the grid has never
    // listened for moves outside a drag. Neither report decides anything: both carry the
    // offsets the browser hands the event, and C# resolves which cell that is from its
    // own geometry, once, in one place. The row is worked out here only to know whether
    // a move is worth reporting — from the row height C# wrote inline on the root and
    // the translation it wrote on the Viewport, both read as text, never measured — and
    // the number itself never crosses. Each report is switched on by C# only while
    // something consumes it; off, nothing here computes.
    let restTimer = 0;
    let reportRows = false;
    let reportRest = false;
    let lastPointerRow = -2;
    // The Viewport's own top edge is the first painted row's, so the offset divides into
    // rows from there. Which row that is, C# writes on the Viewport: once the height is
    // compressed the transform no longer says it (ADR-0053).
    const rowUnder = (viewport, offsetY) => {
        const rowHeight = parseFloat(root.style.getPropertyValue('--ex-row-height'));
        const written = viewport.getAttribute('data-ex-first-row');
        const firstRow = written === null ? NaN : Number(written);
        if (!(rowHeight > 0) || !Number.isInteger(firstRow)) {
            return -2;
        }
        return firstRow + Math.floor(offsetY / rowHeight);
    };
    const onPointerMove = (event) => {
        // Cells are pointer-events: none, so the Viewport is what a move over the rows
        // lands on. Anything else — the header, the message popover, a Template Column's
        // own control — is not a cell to describe, and the last report stands.
        // Cleared first, whatever this move was over: a pending rest armed on the rows and
        // then abandoned for the header would otherwise fire for a cell the pointer left.
        clearTimeout(restTimer);
        if (!core || !(event.target instanceof Element)) {
            return;
        }
        if (!event.target.classList.contains('ex-viewport')) {
            // Inside the Viewport but on an element that takes its own pointer events
            // — the editor, a Consumer's control — the band stands where it was. On the
            // header or a popover there is no row under the pointer: the band goes, and
            // only the band; the message a rest opened is still being read.
            if (reportRows && lastPointerRow !== -2 && !event.target.closest('.ex-viewport')) {
                lastPointerRow = -2;
                core.invokeMethodAsync('OnPointerAwayAsync', true).catch(() => {});
            }
            return;
        }
        const x = event.offsetX;
        const y = event.offsetY;
        if (reportRows) {
            const row = rowUnder(event.target, y);
            if (row !== lastPointerRow) {
                lastPointerRow = row;
                core.invokeMethodAsync('OnPointerRowAsync', x, y).catch((error) => {
                    if (core) {
                        console.error('[ex-grid] the grid failed to take the pointer row', error);
                    }
                });
            }
        }
        if (reportRest) {
            restTimer = setTimeout(() => {
                if (!core) {
                    return;
                }
                core.invokeMethodAsync('OnPointerRestAsync', x, y).catch(() => {
                    // Disposal can overtake a rest report, and that is not a fault.
                });
            }, restDelayMs);
        }
    };
    const onPointerLeave = () => {
        clearTimeout(restTimer);
        lastPointerRow = -2;
        // Unconditionally: a message opened while rests were reported outlives the
        // switch being turned off, and leaving is what closes it.
        if (core) {
            core.invokeMethodAsync('OnPointerAwayAsync', false).catch(() => {});
        }
    };
    root.addEventListener('mousemove', onPointerMove);
    root.addEventListener('mouseleave', onPointerLeave);

    // Capture, and on the root rather than the document: on the document every grid on
    // the page would receive every keystroke, and in the bubble phase a cell editor would
    // already have moved its caret (ADR-0010 / ADR-0018).
    root.addEventListener('keydown', onKeyDown, true);

    // A press into the Formula Bar's text, with no edit open, opens one (ADR-0051): a change of
    // editing mode, which this listener is told a round trip later on a circuit. Until then the
    // gate would take the keys typed into the bar as the browser's — F2 would do nothing and ↓
    // would only move the caret (found on the Server host, 2026-09-27) — and a key already held
    // for another reason would be handed on against "not editing" and lost. So the press takes
    // its place among held keys, and the keys after it are held until the core has answered it
    // and gated against the mode it left, as after a key that changes the mode (ADR-0010). The
    // press itself passes untouched: DOM focus onto the bar is its default, and the bar's focus
    // is what the core answers. Only a field that takes typing — a read-only bar opens nothing —
    // and only one not already holding DOM focus, which a press would not focus again.
    const opensBarEdit = (event) => {
        const target = event.target;
        return !!core && !replaying && event.button === 0 && (editing === 'none' || answering)
            && (target instanceof HTMLInputElement || target instanceof HTMLTextAreaElement)
            && !target.readOnly && !target.disabled && document.activeElement !== target
            && target.closest('.ex-formula-bar-text') !== null && root.contains(target);
    };
    const holdBehindBarPress = () => {
        held.push({ barPress: true });
        if (!answering) {
            startHold();
        }
    };
    // Asked once the press's focus has gone to the core: focusing the bar is the press's default
    // action, dispatched after the listener in the same task, so the question waits for a later
    // task. The core answers once that focus has been handled in full (ExGrid.PressAnsweredAsync).
    //
    // The press's focus reaches the core at once, but its turn among the held keys comes later:
    // a press on the rows held before it can end, in its turn, the edit the bar's focus joined
    // (a character typed onto a cell, a row pressed, the bar pressed, all within one round
    // trip). The bar still holds DOM focus, and the keys after it are the bar's — so, at its
    // turn, the core is told whether the bar is still where the keyboard is, and answers the
    // press again from there if no edit stands.
    const answerBarPress = async () => {
        await new Promise((resolve) => setTimeout(resolve));
        const active = document.activeElement;
        const inBar = active instanceof Element && root.contains(active)
            && active.closest('.ex-formula-bar-text') !== null;
        await core.invokeMethodAsync('BarPressAnsweredAsync', inBar).catch((error) => {
            if (core) {
                console.error('[ex-grid] the grid failed to answer a press', error);
            }
        });
        await editorSettled();
    };

    // A press on the rows keeps its place among held keys (ADR-0021/0010, ED-22). While keys
    // are held, or a change of editing mode is being answered, a primary-button press on the
    // rows is held too, in order, and its release with it: a click straight after Enter
    // reached C# before the held Enter, and the Enter's move carried the Focus past the
    // clicked cell. When nothing is held the press passes through untouched, and while an edit
    // is open the keys after it wait for its answer (holdBehindPress). No layout is read: the
    // press is replayed with the coordinates the browser gave it.
    const mouseInit = (event) => ({
        bubbles: true, cancelable: true, view: window, detail: event.detail,
        screenX: event.screenX, screenY: event.screenY, clientX: event.clientX, clientY: event.clientY,
        ctrlKey: event.ctrlKey, shiftKey: event.shiftKey, altKey: event.altKey, metaKey: event.metaKey,
        button: event.button, buttons: event.buttons,
    });

    // This grid's own rows and headings, not those of a grid nested in one of its cells, whose
    // own scroller stands nearer: a press on the rows lands on the Viewport (cells are
    // pointer-events: none), and one on the column headings anywhere in their band. A press into
    // the Cell Editor's own text is not one: it takes the keyboard by its own default.
    const inOwnScroller = (target) => target instanceof Element && !!scroller
        && target.closest('.ex-scroller') === scroller;
    const isOwnRows = (target) => inOwnScroller(target) && target.classList.contains('ex-viewport');
    const isOwnRowsOrHeadings = (target) => isOwnRows(target)
        || (inOwnScroller(target) && target.closest('.ex-header') !== null);
    // The text field to put the keyboard back into: the surface that last held it while it is
    // still there, or else the first of this grid's own (surfaceField).
    const standingField = () => surfaceField(ownSurface(lastSurface));
    // The hold behind a press (ADR-0010, widened 2026-09-29; ADR-0021's note of the same day). A
    // press on the rows while an edit is open commits and moves, or points, so the mode the next
    // key meets is the core's answer to it: the keys typed after it are held, in order, until
    // that answer, and handed on against the mode it leaves. `99` typed over a cell, a press on
    // another, `7` at once: where the core keeps DOM focus in the editor through the press
    // (ADR-0051), the `7` went into the editor the commit was removing, and was lost. The press
    // itself passes on untouched.
    //
    // The core answers for the press or release it heard last (ExGrid.PressAnsweredAsync), so
    // the question goes after the press has reached it and ahead of its release: from a later
    // task, or from this grid's own release (onRelease), which runs before Blazor hears it,
    // whichever comes first. A click made in one go releases before a later task runs, and a
    // release answered in the press's place overtook the press's hand-back (found on the Server
    // host). The answer also takes off the mark the press left on a field beside the rows: a
    // press that committed has had its hand-back by then, and one that pointed has left the edit
    // open, the field the user's again.
    const askAboutPress = () => {
        const press = pressToAsk;
        if (press === null) {
            return;
        }
        pressToAsk = null;
        const answered = core
            ? core.invokeMethodAsync('PressAnsweredAsync').catch((error) => {
                if (core) {
                    console.error('[ex-grid] the grid failed to answer a press', error);
                }
            })
            : Promise.resolve();
        answered.then(() => {
            if (press.mark !== null && staleMarks === press.mark) {
                staleField = null;
            }
            press.resolve();
        });
    };
    const holdBehindPress = (mark) => {
        askAboutPress();
        pressAnswer = new Promise((resolve) => {
            pressToAsk = { mark, resolve };
        });
        setTimeout(askAboutPress);
        if (!answering) {
            startHold();
        }
    };

    // A press handed on through a Pointing Scope keeps its place among the keys of the grid that
    // points (ADR-0058, "On a circuit"; ADR-0021's note of 2026-09-30). On a circuit, the text this
    // grid's Consumer writes for a press reaches the pointing grid's field a round trip later, and a
    // key typed there meanwhile would reach the field first; and this grid sends its press to its
    // own core, where it could overtake keys typed before it that the pointing grid still holds
    // (`=1+` and a press at once gave `=XLOOKUP(...)`). So, while this grid is pointed at, the render
    // that says so names the root of the grid that points (data-ex-pointed-from), and each primary
    // press on the rows or the headings that goes on to Blazor — at once, or at its replay if it
    // was held here — is told to that root by one event. The listener there (onPressHandedOn) gives
    // the press its place among its keys: it says when the keys before it have been handed on
    // (inTurn), and holds the keys after it until this grid's core has answered the press
    // (answered). This core hands the press over only once it is in turn, and answers it once its
    // Consumer has taken it — written or refused — or at once, for a press that hands nothing over
    // (ExGrid.PressHandedOnAsync). The core is told of the press here, in the capture phase, ahead
    // of Blazor's own dispatch of it, so the press it hears next is the one it was told of.
    //
    // Nothing is kept of the other grid: its root is found from this render's attribute at the
    // press, and the event is the whole message. An event nobody takes — a root with no listener —
    // leaves the press to go on at once. Reads an attribute; no layout.
    let pressesHandedOn = 0;
    const handOn = (event) => {
        const pointing = root ? root.getAttribute('data-ex-pointed-from') : null;
        if (!core || !pointing || event.button !== 0 || !isOwnRowsOrHeadings(event.target)) {
            return;
        }
        const other = root.ownerDocument.getElementById(pointing);
        if (!(other instanceof Element) || other === root || !other.classList.contains('ex-grid')) {
            return;
        }
        const press = ++pressesHandedOn;
        let told = false;
        let inTurn = false;
        let answer = null;
        const answered = new Promise((resolve) => {
            answer = resolve;
        });
        const detail = {
            inTurn: () => {
                if (inTurn) {
                    return;
                }
                inTurn = true;
                if (told && core) {
                    core.invokeMethodAsync('PressInTurn', press).catch((error) => {
                        if (core) {
                            console.error('[ex-grid] the grid failed to hand on a press', error);
                        }
                    });
                }
            },
            answered,
        };
        const taken = !other.dispatchEvent(new CustomEvent('ex-press-handed-on', { cancelable: true, detail }));
        if (!taken) {
            answer();
            return;
        }
        told = true;
        answer(core.invokeMethodAsync('PressHandedOnAsync', press, inTurn).catch((error) => {
            if (core) {
                console.error('[ex-grid] the grid failed to hand on a press', error);
            }
        }));
    };

    // A press on another grid, handed on to this root while this grid points at it (handOn, on
    // that grid's listener). It takes its place among the keys held here: with nothing held and
    // nothing being answered it is in turn at once; otherwise it waits in the queue, and is in turn
    // when the drain reaches it, after every key typed before it. The keys typed after it are held
    // until the other grid's core has answered it, and are then handed on against the mode that
    // answer leaves: the hold behind a press on this grid's own rows (holdBehindPress), whose
    // answer is the other core's because only that core knows whether it handed the press over at
    // all. Taking the event (preventDefault) says the press has its place here.
    const onPressHandedOn = (event) => {
        const hand = event.detail;
        if (!core || !hand || typeof hand.inTurn !== 'function') {
            return;
        }
        event.preventDefault();
        if (answering) {
            held.push({ handedOn: hand });
            return;
        }
        hand.inTurn();
        pressAnswer = hand.answered;
        handedOnAnswer = pressAnswer;
        startHold();
    };
    root.addEventListener('ex-press-handed-on', onPressHandedOn);

    const onPress = (event) => {
        // A press into a field beside the rows gives that field a focus of its own, which a
        // late hand-back leaves alone (reclaimFocus).
        if (event.target instanceof Element && event.target.closest('.ex-formula-bar') !== null) {
            staleField = null;
        }
        // A press into an editor surface puts the keyboard there.
        noteSurface(event.target);
        // A press in an editor surface's text puts the caret where it lands: the user's move.
        if (event.button === 0 && !replaying && isTextField(event.target) && event.target.closest('.ex-editor') !== null) {
            noteCaretMove(event.target);
        }
        if (opensBarEdit(event)) {
            holdBehindBarPress();
            return;
        }
        // An edit is left standing here when DOM focus goes elsewhere — to another grid, a
        // control of the page's, or nothing (ADR-0018, section 6). A press on this grid's rows
        // or headings puts the keyboard back into the surface that last held it, now, before
        // anything hears the press. The press then points, or commits and moves, exactly as if
        // the keyboard had never left: where the core keeps DOM focus through a press on the rows
        // (ADR-0051) it keeps it in the edit, and the hand-back after a commit finds it inside
        // this root. Handed back from C#, a round trip later, the keys typed in between would
        // reach the grid the user had just left. This is one of the three decisions about focus
        // made in script (ADR-0021, added 2026-09-29), beside reclaimFocus and focusEditor; it
        // reads document.activeElement and no layout. Held or not, the press keeps its place among
        // the keys: only where the keyboard is has changed.
        const focusAtPress = document.activeElement;
        if (core && !replaying && editing !== 'none' && !(focusAtPress instanceof Element && root.contains(focusAtPress))
            && isOwnRowsOrHeadings(event.target)) {
            standingField()?.focus({ preventScroll: true });
            focusDeclined = false;
        }
        // Only a press on this grid's own rows is held or holds the keys after it: one on a
        // nested grid's rows is that grid's to answer, and this core never hears it. A press that
        // goes on to Blazor from here, or is replayed, is told to the grid that points, if this
        // one is pointed at (handOn).
        if (!core || replaying || event.button !== 0 || !isOwnRows(event.target)) {
            handOn(event);
            return;
        }
        // The Formula Bar or the Name Box holding DOM focus, which a press on the rows would take
        // by its default — read again, after the keyboard may have been put back above.
        const focusAfterReturn = document.activeElement;
        const field = focusAfterReturn instanceof Element && root.contains(focusAfterReturn)
            && focusAfterReturn.closest('.ex-formula-bar') !== null
            ? focusAfterReturn
            : null;
        // Not held, the press goes on as it is, when there is nothing it could overtake: nothing
        // held, and no key being answered. Behind a press still being answered, with no key held
        // after it, it goes on too: Blazor keeps presses in order among themselves, and the
        // second press of a double click reaches it ahead of the double click, as it always did.
        // Not behind a press on another grid handed on to this root: that one reaches this core
        // through the other grid's, and this press, which may point too, would overtake it.
        if (held.length === 0 && (!answering || (pressAnswer !== null && pressAnswer !== handedOnAnswer))) {
            handOn(event);
            // While an edit is open, the keys after it wait for its answer (holdBehindPress).
            // Where a press may point, the core also suppresses its default so the keyboard stays
            // in the edit (ADR-0051), and a Formula Bar the edit was typed in keeps DOM focus
            // only for that. Should the press commit instead, that focus is left standing, not
            // the user's choice, and the hand-back takes it as the press would have
            // (reclaimFocus): the keyboard left in the bar with no edit open took typing that went
            // nowhere. Where the default is not suppressed, it has moved DOM focus off the bar
            // already. The mark is for that hand-back alone: a press that points leaves the edit
            // open with the keyboard in the bar, and a mark left standing would let a later
            // hand-back — the rows' focus handed on, a popover's dismissal — take the bar the
            // user is still typing in. It comes off with the press's answer.
            if (editing !== 'none') {
                holdBehindPress(field !== null ? markStale(field) : null);
            }
            return;
        }
        // Out of Blazor's sight until its turn, and so is its default, DOM focus onto the rows:
        // the rows hand focus on to the root as soon as they get it, a round trip later on a
        // circuit, and that hand-over is not held. Taken at the press, it overtook the keys
        // held before it — landing after the Cell Editor a held key had opened took DOM focus,
        // it took the keyboard from the editor, and every key and click held behind it waited
        // for an editor that no longer had focus, until the two-second fallback (Server host,
        // typing-probe-2, 2026-09-27). The keyboard stays where the held keys have it; whether
        // the press keeps an open edit is the core's to say at its turn (ADR-0051), and a
        // press that commits it hands the keyboard to the root then.
        event.preventDefault();
        event.stopPropagation();
        // The default suppressed here would have taken DOM focus off a field beside the rows
        // that held it. That focus is now only left standing, not the user's choice: the
        // hand-back after the press takes it, as the press would have (reclaimFocus).
        if (field !== null) {
            markStale(field);
        }
        held.push({ press: 'mousedown', target: event.target, init: mouseInit(event) });
    };
    // A release is held only behind its press: once the press has been handed on, the
    // release follows it to Blazor as it comes, and Blazor keeps the two in order.
    const onRelease = (event) => {
        // A passed-on press the keys wait behind is asked about now, ahead of this release
        // (holdBehindPress).
        if (!replaying) {
            askAboutPress();
        }
        if (!core || replaying || !held.some((k) => k.press === 'mousedown')) {
            return;
        }
        event.stopPropagation();
        held.push({ press: 'mouseup', target: event.target, init: mouseInit(event) });
    };
    // A held press or release, handed to Blazor as the event it was, in its place: on the
    // element it landed on, or on the Viewport if a render has replaced that one. The keys
    // held behind it wait until the core says it has answered it — a press can commit an open
    // edit and wait on the Consumer hearing it, and those keys must be gated against the mode
    // it leaves (ExGrid.PressAnsweredAsync). The listener asks straight after dispatching, so
    // the core has always heard the press first; the answer is the core's, never a guess.
    const replayPress = async (k) => {
        const target = k.target.isConnected ? k.target : root.querySelector('.ex-viewport');
        if (!target) {
            return;
        }
        replaying = true;
        try {
            target.dispatchEvent(new MouseEvent(k.press, k.init));
        } finally {
            replaying = false;
        }
        await core.invokeMethodAsync('PressAnsweredAsync').catch((error) => {
            if (core) {
                console.error('[ex-grid] the grid failed to answer a press', error);
            }
        });
        // The hand-back the press asked for, if it asked for one, has run by now.
        staleField = null;
        // An edit the press kept stands with DOM focus in it, and one it ended is gone: the
        // keys after it are gated once that has settled, as after a key.
        await editorSettled();
    };
    root.addEventListener('mousedown', onPress, true);
    root.addEventListener('mouseup', onRelease, true);

    // The clipboard (ADR-0005) — the fourth allowlist entry (ADR-0021). Both events
    // fire on the focused root — verified on the real Chrome: a non-editable,
    // user-select:none element with focus receives both — so Ctrl+C / Ctrl+V are never
    // in the keydown table above; the browser's own copy and paste commands are the
    // trigger, which is also what makes the event route prompt-free.
    // The asynchronous write (ADR-0005), shared by the two callers that need it: a
    // Ctrl+C whose selection runs beyond the Window, and every copy invoked from a menu
    // — a menu item's click fires no `copy` event, so it has no other route (ADR-0036).
    // Chrome accepts a promise as a ClipboardItem value, so the user-activation context
    // survives the wait. A rejected promise aborts the whole write and the clipboard
    // stays as it was — never a fraction of the selection.
    const writeAsync = (withHeaders) => {
        const answer = core.invokeMethodAsync('BuildCopyPayloadAsync', withHeaders)
            .catch((error) => {
                // A .NET failure — a Consumer delegate that threw, a circuit that
                // dropped. Reported here, because the null it becomes reads as a
                // refusal below and would otherwise make the copy a silent no-op.
                if (core) {
                    console.error('[ex-grid] the grid failed to build a copy', error);
                }
                return null;
            });
        const flavour = (type, field) => answer.then((p) => {
            if (!p) {
                throw new Error('the copy was refused');
            }
            return new Blob([p[field]], { type });
        });
        return navigator.clipboard.write([new ClipboardItem({
            'text/plain': flavour('text/plain', 'text'),
            'text/html': flavour('text/html', 'html'),
        })]).catch((error) => {
            // A refusal from the core: nothing landed, which is the refusing grid's
            // contract (ADR-0005), and the reason has already been raised (OnCopyRefused
            // on the C# side, or the console line above). Anything else is a failure and
            // is said so.
            if (error instanceof Error && error.message === 'the copy was refused') {
                return;
            }
            // The browser would not let the grid write. Nothing landed either — but no
            // reason has been raised yet, and a user told nothing pastes the old
            // clipboard believing it is the copy. It is a Refusal of its own (ADR-0005).
            if (error instanceof DOMException && error.name === 'NotAllowedError') {
                if (core) {
                    core.invokeMethodAsync('OnCopyWriteRejectedAsync').catch((reportError) => {
                        if (core) {
                            console.error('[ex-grid] the grid failed to report a rejected copy', reportError);
                        }
                    });
                }
                return;
            }
            if (core) {
                console.error('[ex-grid] the grid failed to write a copy', error);
            }
        });
    };
    const onCopy = (event) => {
        // Only when the root itself holds the keyboard: a control inside a Template
        // Column keeps its own clipboard behaviour (ADR-0020).
        if (!core || !isRoot(event.target)) {
            return;
        }
        // The event is synchronous, so the payload is asked for synchronously —
        // possible on WebAssembly, where invokeMethod exists (ADR-0017's premise). On
        // a host without the synchronous channel every copy takes the async route.
        // The two cases are told apart by the probe at attach, never by catching: a
        // genuine .NET failure must surface, not be re-run down the async route.
        let payload = null;
        if (hasSyncChannel) {
            try {
                payload = core.invokeMethod('BuildCopyPayload');
            } catch (error) {
                // No preventDefault: the default copy of a user-select:none element
                // carries nothing, so the clipboard stays untouched (ADR-0005) — and
                // the failure is said out loud rather than swallowed.
                console.error('[ex-grid] the grid failed to build a copy', error);
                return;
            }
        } else {
            payload = { kind: 'async' };
        }
        if (!payload || payload.kind === 'none') {
            // Refused: the clipboard stays untouched. The default copy of a
            // user-select:none element carries nothing, so nothing lands (ADR-0005).
            return;
        }
        event.preventDefault();
        if (payload.kind === 'data') {
            // Two formats in one operation: what was on screen in text/plain, and the
            // raw, locale-free value in text/html — Excel prefers the HTML flavour and
            // receives precision and type intact (ADR-0005).
            event.clipboardData.setData('text/plain', payload.text);
            event.clipboardData.setData('text/html', payload.html);
            return;
        }
        // Beyond the Window: the asynchronous route, without headers — Ctrl+C copies
        // the selection and nothing else (ADR-0005).
        writeAsync(false);
    };
    const onPaste = (event) => {
        if (!core || !isRoot(event.target)) {
            return;
        }
        // clipboardData is only readable inside the event, so both flavours are read
        // here; everything they mean — shape rules, refusals, the intent — is decided
        // in C# (ADR-0014). preventDefault regardless: pasting into a non-editable
        // element does nothing by default, and must not start doing something later.
        event.preventDefault();
        // Handed over as streams, never as two strings in one call (ADR-0005): on a
        // Blazor Server circuit that call is one hub message, and a message past the
        // hub's receive limit — 32 KB unless the application raised it — closes the
        // connection. Excel's HTML for a few hundred cells is past it. A stream is
        // Blazor's own route for large interop data and is not subject to that limit;
        // its length travels with it, so C# can refuse a paste past the grid's ceiling
        // without reading a byte. An empty flavour is sent as nothing at all.
        const encoder = new TextEncoder();
        const stream = (value) => (value
            ? DotNet.createJSStreamReference(encoder.encode(value))
            : null);
        const text = stream(event.clipboardData.getData('text/plain'));
        const html = stream(event.clipboardData.getData('text/html'));
        core.invokeMethodAsync('OnPasteStreamsAsync', text, html)
            .catch((error) => {
                if (core) {
                    console.error('[ex-grid] the grid failed to take a paste', error);
                }
            });
    };
    root.addEventListener('copy', onCopy);
    root.addEventListener('paste', onPaste);

    // The Scrollbar Gutter — how much of the declared box the scrollbars take. A classic
    // scrollbar is drawn INSIDE the element's own box, so the columns and rows get about
    // 15px less than ViewportWidth/ViewportHeight say; overlay scrollbars (macOS) take
    // nothing. The C# side cannot work it out: it depends on the platform, on the user's
    // settings, on a Consumer's `scrollbar-width`, and on whether the content overflows
    // at all — with `overflow: auto` the gutter is 0 until it does, so measuring once at
    // attach would be measuring the wrong moment.
    //
    // Observed on the CONTENT box, and this is the whole reason a ResizeObserver works
    // here: observing the border box never fires at all, because the outer size is what
    // the Consumer declared and it does not change when a scrollbar appears. The content
    // box is exactly what shrinks. Whatever a native scrollbar does under zoom — which
    // this project has not been able to measure — a change of width is a change of the
    // content box, so the notification arrives without anyone having to predict it.
    //
    // It cannot feed itself, either. `.ex-spacer` is sized from the total row and column
    // count, never from the Viewport, so a narrower content box does not resize the
    // content that made the scrollbar appear; and nothing here writes to the DOM, so the
    // "ResizeObserver loop completed with undelivered notifications" warning has no way
    // to arise.
    let gutterWidth = 0;
    let gutterHeight = 0;
    let contentWidth = -1;
    let contentHeight = -1;
    const observer = new ResizeObserver((entries) => {
        if (!core || entries.length === 0) {
            return;
        }
        const entry = entries[entries.length - 1];
        const border = entry.borderBoxSize[0];
        const content = entry.contentBoxSize[0];
        if (!border || !content) {
            return;
        }
        // `.ex-scroller` is border-box with no border and no padding, so what the border
        // box has and the content box does not is the scrollbar. Were a Consumer to add
        // padding, it would be counted in too — and rightly: it is equally unavailable
        // to the cells.
        const width = Math.max(0, border.inlineSize - content.inlineSize);
        const height = Math.max(0, border.blockSize - content.blockSize);
        // A notification carrying no news is dropped here rather than sent: it would
        // re-render the grid on every frame of a window drag. The content box size
        // rides the same report (ADR-0028) — under ViewportSize.Stretch it IS the size —
        // so a change of either is news.
        if (width === gutterWidth && height === gutterHeight
            && content.inlineSize === contentWidth && content.blockSize === contentHeight) {
            return;
        }
        gutterWidth = width;
        gutterHeight = height;
        contentWidth = content.inlineSize;
        contentHeight = content.blockSize;
        core.invokeMethodAsync('OnViewportReportAsync', width, height, contentWidth, contentHeight)
            .catch((error) => {
                if (core) {
                    console.error('[ex-grid] the grid failed to take the scrollbar gutter', error);
                }
            });
    });
    if (scroller) {
        observer.observe(scroller, { box: 'content-box' });
    }

    // The Layout Ceiling (ADR-0053, the sixth allowlist entry). Chromium clamps any layout
    // length just under 2^25 zoomed pixels, so in CSS pixels the tallest element it lays out
    // depends on the display scale and the page zoom — and devicePixelRatio does not say
    // which: emulation moves one without the other. So it is not computed: C# renders an
    // element declared 2^25 px tall inside a zero-size box, and the size the browser lays
    // it out at IS the ceiling. The observer reports it once at attach, and again only when
    // the scale or the zoom moves it; nothing here reads layout on the path to a paint, and
    // nothing writes to the DOM.
    let ceiling = -1;
    const ceilingProbe = root.querySelector(':scope > .ex-ceiling-probe > div');
    const ceilingObserver = new ResizeObserver((entries) => {
        if (!core || entries.length === 0) {
            return;
        }
        const size = entries[entries.length - 1].borderBoxSize[0];
        if (!size || size.blockSize === ceiling) {
            return;
        }
        ceiling = size.blockSize;
        core.invokeMethodAsync('OnLayoutCeilingAsync', ceiling)
            .catch((error) => {
                if (core) {
                    console.error('[ex-grid] the grid failed to take the layout ceiling', error);
                }
            });
    });
    if (ceilingProbe) {
        ceilingObserver.observe(ceilingProbe);
    }

    return {
        // The two pointer reports' switches (ADR-0021's fifth entry): rows for the
        // hover band, rest for the error popover. Told by C#, which knows who consumes
        // what; off, the listener above computes nothing for that report.
        setPointerReporting: (rows, rest) => {
            reportRows = rows === true;
            reportRest = rest === true;
            lastPointerRow = -2;
            if (!reportRest) {
                clearTimeout(restTimer);
            }
        },
        // The rows moved under a still pointer and C# dropped its band on that paint:
        // the memory of the last row goes with it, so the next movement — even within
        // the same row as before the scroll — is reported again. Told from C#, at the
        // paint, rather than heard here at the scroll event: a move landing between
        // the two would otherwise be remembered and its band already dropped.
        forgetPointer: () => {
            lastPointerRow = -2;
        },
        // Both axes in one call, deliberately. A trackpad moves them together, and two
        // calls means two round-trips with the browser free to process another scroll
        // event in between — the rows would then be painted from one moment's offset and
        // the columns from another's. One call is one snapshot.
        // Read once at attach: the mouse path needs the same answer, and Ctrl+click and
        // Cmd+click have to agree with Ctrl+A and Cmd+A about which one adds a range.
        metaIsPrimary: () => metaIsPrimary,
        // A copy invoked from a menu (ADR-0036). Clicking a menu item fires no `copy`
        // event, so there is no event route to take however small the selection is:
        // this always writes through the asynchronous API. Still the fourth allowlist
        // entry — the clipboard — and not a fifth (ADR-0021).
        writeCopy: (withHeaders) => writeAsync(withHeaders === true),
        // Which editing mode the key gate runs under (ADR-0010): 'none', 'overwrite',
        // 'caret', 'point', 'completion' (ADR-0051) or 'completionOverPoint' (ADR-0058: a list
        // open where ← and → point), whether inputs report their caret,
        // and whether F4 is claimed while editing. Set by the core when the mode changes —
        // a mode change is a different set of claimed keys. (A focusable descendant holding
        // the keyboard — ADR-0020's interactive cell — is not a mode: it is read off
        // event.target, which is true whether focus arrived by click or by key.)
        setEditing: (mode, reportsCaret, cyclesReferences) => {
            const opened = editing === 'none' && mode !== 'none';
            editing = mode;
            reportCaret = reportsCaret === true;
            cycleReferences = cyclesReferences === true;
            if (mode === 'none') {
                caretMoved = null;
            }
            // The surface an edit belongs to starts as the one holding DOM focus as it opens — the
            // Formula Bar a press opened it from — or none yet, and the Cell Editor is taken for
            // it (standingField); the edit's end forgets it.
            if (opened || mode === 'none') {
                lastSurface = opened ? ownSurface(document.activeElement) : null;
                focusDeclined = false;
            }
            watchReferenceTexts(mode !== 'none');
            // A new state starts a new conversation: a report equal to one sent before it is
            // news to the core now (the next edit can open on the same text and caret).
            reportedText = null;
            reportedCaret = -1;
            // An edit opened by a press into an editor surface's text keeps the caret where the
            // press put it (ADR-0051's third round). The press's own selectionchange can have
            // been reported before the core was editing, and was not heard, so the caret is
            // reported once more as the edit opens. Read, not measured: no layout is read. An
            // edit opened by typing or F2 is placed by the core, which disregards this report.
            if (opened && reportCaret && core) {
                const input = focusedEditorField();
                if (input) {
                    reportCaretOf(input);
                }
            }
        },
        // After the core wrote the editor's text itself — an accepted candidate, a pointed
        // Reference, F4's rewrite — the caret goes where the core says (ADR-0051's second
        // round), or the selection F4's rewrite answered, from the caret to its end. Only while
        // the surface still holds that text: typed on since, the user's own caret stands.
        // Also when an edit opens: the caret goes to the end of the opening text, placed rather
        // than assumed. The caret placed is the core's already, so it is not reported back.
        // Not after the user moved the caret in that same text, before the placement came: the
        // user's caret stands, and is reported once more as the user's — its own report can
        // have reached the core while the placement was in flight, or have been spared as a
        // repeat.
        setCaret: (text, caret, end) => {
            const input = editorInput();
            if (input && input.value === text) {
                if (movedByUser(input)) {
                    reportedText = null;
                    reportCaretOf(input);
                    return;
                }
                input.setSelectionRange(caret, end);
                reportedText = text;
                reportedCaret = caret;
            }
        },
        // A popover's contents reported a popup of their own opening or closing
        // (ADR-0039): while one is open, a descendant's Escape is left to it.
        setInnerPopup: (open) => {
            innerPopup = open;
        },
        // Which keys this grid takes, whether any column edits and whether a search is
        // wired — re-told when a parameter change changes the answer, so a grid that
        // becomes display-only stops taking printable keys, and one whose Consumer stops
        // listening for undo gives Ctrl+Z back to the page (ADR-0007/0010/0020/0055).
        setClaims: (takenKeys, editable, findable) => {
            taken = new Set(takenKeys);
            canEdit = editable;
            canFind = findable;
        },
        getScrollOffset: () => (pendingReveal
            ? { top: pendingReveal.top, left: pendingReveal.left }
            : scroller
                ? { top: scroller.scrollTop, left: scroller.scrollLeft }
                : { top: 0, left: 0 }),
        // Where the Focus is kept visible (ADR-0012). The offsets are computed in C#,
        // which is what keeps scrollIntoView out of it: that would tuck the cell under the
        // sticky header or the Pinned Columns, neither of which it knows about. A reveal
        // names the render that paints it (token), and is written as that render lands
        // (pendingReveal above); any other write is made at once, and replaces a reveal
        // still held.
        setScrollOffset: (top, left, token) => {
            if (!scroller) {
                return;
            }
            dropReveal();
            if (token !== undefined && token !== null
                && Number(root.getAttribute('data-ex-reveal')) < token) {
                holdReveal({ top, left, token });
                return;
            }
            scroller.scrollTop = top;
            scroller.scrollLeft = left;
        },
        // The first visible row kept across a change of the row height or the Layout Ceiling
        // (ADR-0028/0053), written only while the scroller still stands where the core last
        // knew it (fromTop, to a pixel). A scroll the core has not heard yet moved it, and the
        // user's scroll wins over an anchor computed from before it: answers false, and the
        // core reads the offset instead. The comparison is here because only here are the
        // write and the user's scroll ordered.
        anchorScrollTop: (top, fromTop) => {
            const standing = pendingReveal ? pendingReveal.top : scroller?.scrollTop;
            if (!scroller || Math.abs(standing - fromTop) > 1) {
                return false;
            }
            if (pendingReveal) {
                pendingReveal.top = top;
                return true;
            }
            scroller.scrollTop = top;
            return true;
        },
        // The keyboard back to this grid's root, asked for by the core a round trip after the
        // gesture that wanted it (ADR-0021, ADR-0018): only while DOM focus is still inside
        // this root, or on nothing. A second grid the user has pressed in the meantime keeps
        // its keyboard. The condition reads document.activeElement and no layout. It is one of
        // the three decisions about focus made in script, all for the same reason — made from
        // C#, a round trip late, they would take or leave the keyboard in the wrong grid; the
        // others are the press that brings the keyboard back to an edit left standing (onPress,
        // ADR-0018 section 6) and the open edit's own focus (focusEditor).
        //
        // Nor from a field beside the rows with focus of its own — the Formula Bar and the Name
        // Box, built in or drawn by a Chrome, all inside the band the core renders them into
        // (ADR-0021, widened 2026-09-28): a row press's hand-back, landing after a press into
        // one of them, took the keyboard the user had just put there. Only when the core means
        // to take the keyboard out of that field — Enter or Escape typed in it, an edit in the
        // bar ending — does it say so, and the field is left. Everything else inside the root,
        // the Cell Editor over the rows included, is taken back as before.
        // A field a press on the rows left standing (staleField) — held, or kept for an edit it
        // might have pointed into, until that press is answered — is not the user's, and is
        // taken as the press would have taken it.
        reclaimFocus: (fromField) => {
            const active = document.activeElement;
            const own = active instanceof Element && active !== staleField
                && active.closest('.ex-formula-bar') !== null;
            if (root && (!active || active === document.body || active === document.documentElement
                || (root.contains(active) && (fromField === true || !own)))) {
                staleField = null;
                root.focus({ preventScroll: true });
            }
        },
        // The core's request that the open edit's surface take the keyboard: on opening, on F2,
        // after a Reject — the Cell Editor's (bar false) or the Formula Bar's text (bar true),
        // the built-in field or a Chrome's control inside the core's box, found as the press back
        // finds it (surfaceField), so the core holds no reference to a control it did not render
        // (ADR-0010). It lands a round trip after the render that painted the surface, and is
        // granted only while DOM focus is still inside this root or on nothing, the condition
        // reclaimFocus reads: a grid or a control the user has pressed in the meantime keeps the
        // keyboard, and the edit is left standing there (ADR-0018 section 6). The third decision
        // about focus made in script (ADR-0021's note of 2026-09-30); it reads
        // document.activeElement and no layout. Declined, the keys held behind the key that
        // opened the edit go into it (focusDeclined).
        focusEditor: (bar) => {
            if (!root) {
                return;
            }
            const box = [...root.querySelectorAll('.ex-editor')]
                .find((b) => ownSurface(b) === b && b.classList.contains('ex-formula-bar-text') === (bar === true));
            const field = box ? surfaceField(box) : null;
            if (!field) {
                return;
            }
            const active = document.activeElement;
            if (!active || active === document.body || active === document.documentElement || root.contains(active)) {
                focusDeclined = false;
                // Scrolled into view as Blazor's FocusAsync scrolled it: an editor opened by keys
                // below the fold is brought on screen.
                field.focus();
            } else if (editing !== 'none') {
                // The surface the core asked for is the edit's: the held keys go into it, and a
                // press back puts the keyboard there (standingField), the bar's when it was the bar.
                lastSurface = box;
                focusDeclined = true;
            }
        },
        // Escape's way out of Enter/Tab cycling. Setting focus is Blazor's FocusAsync;
        // releasing it has no Blazor API, and it is this instance's own root either way.
        blur: () => {
            // Whatever inside the grid holds the keyboard, not only the root itself: an
            // action button that was clicked has DOM focus, and blurring the root would
            // do nothing at all.
            const active = document.activeElement;
            if (root && active instanceof HTMLElement && root.contains(active)) {
                active.blur();
            }
        },
        dispose: () => {
            // Left attached, a grid that is gone goes on eating every key its root still
            // sees, and its .NET reference is never collected. The observer holds the
            // scroller and the .NET reference the same way, and a notification arriving
            // after disposal would call into a component that no longer exists.
            observer.disconnect();
            ceilingObserver.disconnect();
            dropReveal();
            clearTimeout(restTimer);
            root.removeEventListener('mousemove', onPointerMove);
            root.removeEventListener('mouseleave', onPointerLeave);
            root.removeEventListener('keydown', onKeyDown, true);
            root.removeEventListener('mousedown', onPress, true);
            root.removeEventListener('mouseup', onRelease, true);
            root.removeEventListener('ex-press-handed-on', onPressHandedOn);
            // A press on another grid still waiting for its turn here is let go: that grid's core
            // hands it over rather than wait for keys this grid will never hand on.
            for (const k of held) {
                k.handedOn?.inTurn();
            }
            root.removeEventListener('input', onEditorInput, true);
            document.removeEventListener('selectionchange', onSelectionChange);
            cancelAnimationFrame(caretFrame);
            caretFrame = 0;
            watchReferenceTexts(false);
            root.removeEventListener('copy', onCopy);
            root.removeEventListener('paste', onPaste);
            lastSurface = null;
            staleField = null;
            // A press still to be asked about has no core left to answer it: the keys held behind
            // it are let go with the rest.
            pressToAsk?.resolve();
            pressToAsk = null;
            root = null;
            scroller = null;
            core = null;
        },
    };
}
