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
// that same mousedown and mouseup, and the keydown, selecting the Name Box's text for the press
// that gives it the keyboard (ADR-0051, ticket 78); that same mousedown and mouseup telling the
// core what each press on the rows was taken against, so a held one lands where it was made
// (ED-31, ADR-0021's note of 2026-10-02); the keydown, the paste and that same mousedown and
// mouseup on an action reading the render the rows on screen were painted by (data-ex-paint), so
// that a write is checked against the order it was aimed under and a press on an action finds the
// row it was made on (ADR-0142; ADR-0021's notes of 2026-10-05 and 2026-10-07);
// and the editor listener keeping the coloured text beneath a field honest (ADR-0057). And the
// seventh entry (ADR-0080): the Keyboard Field's composition and focus, heard on the root —
// `compositionstart` and `compositionend`, always on, so a composition on a selected cell takes its
// place among the held keys, and `keyup`, which says that a key the IME took has been told whole, so
// that the editor's request for the keyboard is granted in order with the IME's events, and — the same
// listener, no other added — the release of a declared Alt that went down alone, held in its place among
// the held keys (ADR-0100's KeyTips; decided with the user, 2026-10-13); the root's
// `focus`, passing focus that lands on the root itself on to its field; and the root's `focusout`,
// emptying the field as it is left, ending the release of Tab when DOM focus leaves the grid
// (ADR-0012), and ending a key the IME took whose keyup the keyboard left before it was told
// (ADR-0080, 2026-10-12). The eighth entry (ADR-0090): a media query
// telling the Device Pixel. And the ninth (ADR-0122): the browser's time zone, read once at
// attach and only when the Consumer asked for it, so TODAY() is the user's day. The tenth
// (ADR-0210): temporary blur and change listeners on the text field changed by held typing,
// supplying its missing commit when it is left and retiring on a normal commit. The Settled Mark
// (ADR-0220; ADR-0021's note of 2026-10-16) adds no listener: the listeners above write
// `data-ex-settled` on the root, with one MutationObserver on two attributes the core's render
// writes on the root's first child, as the reveal's and the follow's observe theirs. Anything
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
 * The SHA-256 of this file's source, which the grid compares with the one it was built with before
 * it attaches anything (ADR-0123; ADR-0021's note of 2026-10-11). A page that kept another
 * version's script beside a new component, or a module older than this export, is refused and said
 * to be reloaded, instead of running code that does not match. A file cannot hold its own hash, so
 * tools/assets/minify.mjs writes the source's in place of the identifier below; this source is
 * never served. It reads nothing from the browser and adds no listener. Its name and its answer, a
 * string, never change between versions: they are how two versions tell each other apart.
 * @returns {string} the SHA-256 of Assets/ex-grid.js, as the shipped file's banner names it
 */
export function stamp() {
    // eslint-disable-next-line no-undef
    return typeof EX_SOURCE_SHA256 === 'string' ? EX_SOURCE_SHA256 : '';
}

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
 * @param {string[]} declaredKeys canonical forms of the keys the Consumer declared (ADR-0050,
 *   item 14), already among takenKeys: the editing branch claims them too
 * @param {boolean} reportTimeZone whether the Consumer asked for the browser's time zone
 *   (ADR-0122): only then is it read, once, and reported
 * @param {Object<string, string[]>} gateKeys every other set of keys the gate reads, by name,
 *   built by GridKeys.GateSets (ADR-0010): the keys the core claims in each editing mode, and the
 *   neutral, popover-opening, find and clipboard keys — this file names none of them
 * @returns a handle owned by that one grid
 */
export function attach(root, scroller, core, takenKeys, canEdit, restDelayMs, canFind, declaredKeys, reportTimeZone, leaveDeclared, gateKeys) {
    let taken = new Set(takenKeys);
    const gateSet = (name) => new Set(gateKeys?.[name] ?? []);
    // The Consumer's declared keys (ADR-0050, item 14), handed by C# like the core's own. With no
    // edit open they are in `taken`; while one is open they are claimed beside the editor's keys,
    // and the Consumer is told an edit is open. This file names none of them.
    let declared = new Set(declaredKeys ?? []);
    // Whether the Consumer hears the Escape that leaves the grid (OnLeave, ADR-0070), and may take the
    // keyboard out of it on that Escape: the keys after a plain Escape on the root then wait for its
    // answer (passKeysOut).
    let leaves = leaveDeclared === true;
    // The keyboard is leaving this grid for the Consumer's element around it (passKeysOut): the keys
    // held behind the Escape that led here, and those typed on the root until DOM focus leaves it, are
    // not this grid's. They are passed to the element around the root, in order, as the keydowns they
    // were, for the Consumer's handler there to hold and hand on with the keyboard. A Consumer's dialog
    // that closes on that Escape: on a circuit its grid holds the keyboard for a round trip or more
    // after the Escape, and ↓ typed straight after it moved in the closing dialog's grid and was lost
    // to the report the keyboard went back to (ExPivot's Show Details, 2026-10-13). Ended by DOM focus
    // leaving the root, a press on the grid, and disposal.
    let keysOut = false;

    // The Settled Mark (ADR-0220; ADR-0021's note of 2026-10-16): `data-ex-settled` on the root while
    // every input the gate has taken has been handled and its effect is in the DOM, so that a test, or
    // any automation driving the grid at machine speed, reads only then. Written here, from what the
    // gate already knows, and from two attributes the core's own render writes on the root's first
    // child (SettledStamp, found by its data-ex-answered) — never measured from the page:
    // - no key, press, paste or composition held, no hold standing, no press waiting on its answer,
    //   no hand-off of the keyboard under way, and no wait for the editor's or a popover's focus;
    // - every input sent to the core answered, and that answer's render in the DOM: each key, press on
    //   the rows, paste, composition and copy carries a number (nextInput), and the stamp names the
    //   last one answered (data-ex-answered) once everything that input left owed the script has been
    //   told. An interop call's answer is not that: on a circuit it can arrive ahead of
    //   the render it answers for;
    // - no render owed on a timer of the core's own (data-ex-owing): the error message, the
    //   description of a selection, the Placeholders filling;
    // - a reveal's write, its repaint, and the page's follow over;
    // - no copy's write to the clipboard in flight.
    // Text typed into a field — the Cell Editor, the Formula Bar, the Name Box — is the field's, and
    // goes to the core as Blazor's own input events: the mark does not wait on it.
    // It changes once as a burst of input starts and once as it ends, on the root only. It is looked
    // at in a microtask after anything that may change it, so a state passed through within one task
    // is never shown. An observer of the two attributes the core writes is the one thing added, and it
    // observes only while the mark is off, as the reveal's and the follow's observe only while they wait.
    let stamp = root.querySelector(':scope > [data-ex-answered]');
    let inputsSent = Number(stamp?.getAttribute('data-ex-answered')) || 0;
    // The last input that could add a layer an Escape peels — a popover or a menu, an Interactive
    // cell, a Copied Range, an edit — and so make the stamp's data-ex-escape-leaves stale until it is
    // answered (ADR-0230 section 1). Every input can, but the keys on the root that only move the Focus
    // or peel a layer (neutral), unless the Consumer declared one: the arrows, Tab and Shift+Tab, Home
    // and End and their Control forms, the page keys, and Escape. A press
    // the core hears without a number of its own (a ▾, a heading) counts as the next input's, so only
    // an answer that came after it is trusted.
    let layerInputAt = inputsSent;
    let clipboardWrites = 0;
    let settledShown = null;
    const answeredWaits = [];
    let settledLook = false;
    function lookSettled() {
        if (!settledLook) {
            settledLook = true;
            queueMicrotask(showSettled);
        }
    }
    function showSettled() {
        settledLook = false;
        // The waits on an input's answer (answeredUpTo) that the stamp now meets, or all of them with
        // the instance gone.
        for (let i = answeredWaits.length - 1; i >= 0; i--) {
            const wait = answeredWaits[i];
            if (!core || stamp === null || Number(stamp.getAttribute('data-ex-answered')) >= wait.upTo) {
                answeredWaits.splice(i, 1);
                wait.resolve();
            }
        }
        if (!core) {
            return;
        }
        const settled = held.length === 0 && !answering && pressAnswer === null && pressToAsk === null
            && handOff === null && settledWaits.size === 0 && pendingReveal === null && repaint === null
            && follow === null && clipboardWrites === 0
            && stamp !== null && Number(stamp.getAttribute('data-ex-answered')) >= inputsSent
            && !stamp.hasAttribute('data-ex-owing');
        if (settled !== settledShown) {
            settledShown = settled;
            root.toggleAttribute('data-ex-settled', settled);
            // Observed only while the mark is off, as the reveal's and the follow's observers are only
            // while they wait.
            if (settled) {
                dropSettledObserver();
            } else if (stamp && settledObserver === null) {
                settledObserver = new MutationObserver(lookSettled);
                settledObserver.observe(stamp, { attributes: true, attributeFilter: ['data-ex-answered', 'data-ex-owing'] });
            }
        }
    }
    // The number the next input sent to the core carries (data-ex-answered). A neutral one cannot add a
    // layer an Escape peels (layerInputAt).
    function nextInput(neutral = false) {
        inputsSent += 1;
        if (!neutral) {
            layerInputAt = inputsSent;
        }
        lookSettled();
        return inputsSent;
    }
    // The keys on the root that cannot add a layer an Escape peels (ADR-0230 section 1).
    const neutralKeys = gateSet('neutral');
    let settledObserver = null;
    function dropSettledObserver() {
        settledObserver?.disconnect();
        settledObserver = null;
    }

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
        repaintAfterReveal(reveal.top, reveal.left);
        lookSettled();
    };
    const holdReveal = (reveal) => {
        pendingReveal = reveal;
        lookSettled();
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

    // A reveal's repaint (ADR-0012, 2026-10-08). On a PC reached through Citrix, with the browser's
    // hardware acceleration off, a far reveal left the Viewport white: the DOM, the offset and the
    // slice were all right, and the rows stayed unpainted on screen until the offset moved once
    // more. That PC's browser also turned out to show the site as a remote browser isolation
    // mirror, so which of the two layers dropped the paint is open (ADR-0012, 2026-10-09). So the
    // offset moves on by one pixel in the frame after the reveal's and back in the frame after
    // that, which is the move that painted them there. The core hears nothing of it: a read
    // meanwhile answers where the reveal left the scroller, so no slice is painted for it. A
    // scroll the user makes in between stands: the move back is not made, and the core is told to
    // read the offset again, through the scroller's own scroll event, since one it already read
    // answered the reveal's offset. The move back writes the value the reveal wrote, on the moved
    // axis only: at a fractional scale an offset read back and written again can land a fraction of
    // a pixel elsewhere. It reads and writes scroll offsets only (ADR-0021).
    let repaint = null;
    const dropRepaint = () => {
        if (repaint) {
            cancelAnimationFrame(repaint.frame);
            repaint = null;
            lookSettled();
        }
    };
    const yieldRepaint = () => {
        dropRepaint();
        scroller.dispatchEvent(new Event('scroll'));
    };
    const repaintAfterReveal = (writtenTop, writtenLeft) => {
        dropRepaint();
        const current = { top: scroller.scrollTop, left: scroller.scrollLeft, frame: 0 };
        repaint = current;
        current.frame = requestAnimationFrame(() => {
            if (repaint !== current) {
                return;
            }
            if (scroller.scrollTop !== current.top || scroller.scrollLeft !== current.left) {
                yieldRepaint();
                return;
            }
            // Away from the edge the offset stands at, so the move is never clamped to nothing;
            // across, where the rows cannot scroll at all.
            scroller.scrollTop = current.top + (current.top > 0 ? -1 : 1);
            const across = scroller.scrollTop === current.top;
            if (across) {
                scroller.scrollLeft = current.left + (current.left > 0 ? -1 : 1);
            }
            const moved = { top: scroller.scrollTop, left: scroller.scrollLeft };
            if (moved.top === current.top && moved.left === current.left) {
                dropRepaint();
                return;
            }
            current.frame = requestAnimationFrame(() => {
                if (repaint !== current) {
                    return;
                }
                if (scroller.scrollTop !== moved.top || scroller.scrollLeft !== moved.left) {
                    yieldRepaint();
                    return;
                }
                if (across) {
                    scroller.scrollLeft = writtenLeft;
                } else {
                    scroller.scrollTop = writtenTop;
                }
                repaint = null;
                lookSettled();
            });
        });
    };

    // The page follows what a move reveals (ADR-0204). After a move the grid makes — the arrows,
    // Ctrl+arrows, Home and End, PageUp and PageDown, Enter and Tab cycling, Ctrl+Backspace — the
    // grid has revealed the Focus in its own Viewport by its own arithmetic (ADR-0012), or the
    // Extent while extending (ADR-0052); the browser is then asked to bring the Keyboard Field,
    // which stands over that cell (ADR-0080) — or the cell itself, on a grid without a field, and
    // the Extent's — into the window at the nearest edge. A cell already in the window moves
    // nothing, and neither does a page the user scrolled meanwhile so that the cell shows. The
    // request names the render that paints the move (data-ex-follow, as a reveal names its own) and
    // waits for it; it then waits for the frame that paints it, and for a reveal's write and repaint,
    // and runs in the task after that frame, so nothing is read on the path to a paint. It runs only
    // while the keyboard is still this grid's — DOM focus inside this root or on nothing, the
    // editor's question — and only for the Focus it was taken for, which the root names whether or
    // not that cell is painted (data-ex-follow-focus: while extending, the Focus may be far above the
    // painted rows, ADR-0052): a request whose Focus has moved on, or whose keyboard has left, is
    // dropped. The Cell Editor opening is followed by the same
    // rule (focusEditor). The grid's own scroller is the grid's, and is put back where the reveal
    // left it if the browser moved it. The scroll entry of ADR-0021, applied to the page.
    let follow = null;
    const followObserver = new MutationObserver(() => awaitFollowedRender());
    const dropFollow = () => {
        if (follow) {
            cancelAnimationFrame(follow.frame);
            clearTimeout(follow.timer);
            follow = null;
            lookSettled();
        }
        followObserver.disconnect();
    };
    // DOM focus inside this root, or on nothing: the keyboard is still this grid's.
    const keyboardStillHere = () => {
        const active = document.activeElement;
        return !!root && (!active || active === document.body || active === document.documentElement || root.contains(active));
    };
    // After the frame that paints what is held, and after a reveal's write and repaint: a rAF puts
    // the request in the frame, and a timer of no delay in the task after its paint.
    const afterPaint = (request, run) => {
        request.frame = requestAnimationFrame(() => {
            request.timer = setTimeout(() => {
                if (follow !== request) {
                    return;
                }
                if (pendingReveal || repaint) {
                    afterPaint(request, run);
                    return;
                }
                follow = null;
                run();
                lookSettled();
            }, 0);
        });
    };
    const awaitFollowedRender = () => {
        const request = follow;
        if (!request || request.token === undefined) {
            return;
        }
        if (Number(root.getAttribute('data-ex-follow')) < request.token) {
            followObserver.observe(root, { attributes: true, attributeFilter: ['data-ex-follow'] });
            return;
        }
        followObserver.disconnect();
        request.token = undefined;
        afterPaint(request, () => {
            if (!keyboardStillHere() || root.getAttribute('data-ex-follow-focus') !== request.focus) {
                return;
            }
            // Found inside this root, by the id its own render wrote: never another instance's cell.
            const cell = root.querySelector(`#${CSS.escape(request.target)}`);
            const element = request.target === request.focus ? (keyFieldOf() ?? cell) : cell;
            if (element) {
                bringIntoWindow(element);
            }
        });
    };
    // The browser places, the grid does not measure (ADR-0204): the page moves, at the nearest edge,
    // and a page with a fixed bar says how much of the window it covers with scroll-padding.
    const bringIntoWindow = (element) => {
        const top = scroller.scrollTop;
        const left = scroller.scrollLeft;
        element.scrollIntoView({ block: 'nearest', inline: 'nearest' });
        if (scroller.scrollTop !== top) {
            scroller.scrollTop = top;
        }
        if (scroller.scrollLeft !== left) {
            scroller.scrollLeft = left;
        }
    };
    // The editor's surface, focused without scrolling, followed by the same rule once it has painted.
    const followEditor = (field) => {
        dropFollow();
        const request = { frame: 0, timer: 0 };
        follow = request;
        afterPaint(request, () => {
            if (keyboardStillHere() && field.isConnected && root.contains(field)) {
                bringIntoWindow(field);
            }
        });
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
    // Which change of mode the gate was last told of (ADR-0230 section 2). The edit's surfaces mark
    // the mode in the render that shows it (data-ex-mode), with the same count
    // (data-ex-mode-version); a mark newer than the tell is the mode on screen, and the tell, a
    // message after that render, has not come yet.
    let editingVersion = 0;
    // Whether each input in an editor surface reports its caret (ADR-0051), told by C# with
    // the mode: only where completion or pointing is declared.
    let reportCaret = false;
    // Whether F4 joins the editing set (ADR-0051, 2026-09-29: it cycles the Reference at the
    // caret), told by C# with the mode: only where the Consumer declared what it does. Never
    // consulted while no edit is open, where F4 stays the browser's.
    let cycleReferences = false;
    // Whether a popover's contents have a popup of their own open (ADR-0039), told by C#.
    let innerPopup = false;
    // Whether Escape with nothing to dismiss has released Tab (ADR-0012, rewritten 2026-10-01),
    // told by C#: the next Tab or Shift+Tab on the root is the browser's. Spent by that Tab, and
    // ended by any other key (gate), a press on the grid (onPress), or DOM focus leaving the grid
    // (onFocusLeft; ADR-0080). The presses this root has heard and the times DOM focus has left it,
    // and how many there had been when the last Escape was forwarded: a press the hold does not
    // take — on a heading, in the Name Box — reaches the core at once, DOM focus can leave while
    // the Escape is being answered, and a release answered for an Escape either came after is not
    // granted (releaseTab).
    let tabReleased = false;
    let releaseEndsHeard = 0;
    let releaseEndsAtEscape = 0;
    // A release ended here — spent by its Tab, or ended by another key — counts as an end heard, so a
    // releaseTab still on its way for the Escape that granted it early (escapeLeavesNow) does not grant
    // it again (ADR-0230 section 1).
    const endRelease = () => {
        if (tabReleased) {
            tabReleased = false;
            releaseEndsHeard++;
        }
    };
    // Whether a plain Escape on the root leaves the grid now, read from the stamp the core renders with
    // the inputs it has answered (data-ex-escape-leaves, ADR-0230 section 1): trusted once every input
    // that could have added a layer is answered (layerInputAt). Then the release is taken at the keydown
    // and nothing after the Escape is held, so a Tab typed at once leaves the grid as one typed slowly
    // does. A Consumer that hears the Escape (OnLeave) still has it held for its answer. Reads two
    // attributes; no layout.
    const escapeLeavesNow = (k) => !leaves && k.repeat !== true && stamp !== null
        && stamp.hasAttribute('data-ex-escape-leaves')
        && Number(stamp.getAttribute('data-ex-answered')) >= layerInputAt;
    // The keys the core claims while a cell is being edited, one set for each mode (ADR-0010,
    // ADR-0051, ADR-0058), built by GridEditingKeys and handed at attach like the core's own: this
    // file names none of them. `editing` is every mode's (commit, cancel, Tab, F2, Ctrl+F); the
    // others hold it, with the arrows and their forms each mode claims.
    const claimedWhile = Object.fromEntries(['overwrite', 'point', 'pointed', 'completion', 'completionOverPoint']
        .map((mode) => [mode, gateSet(mode)]));
    const editingKeys = gateSet('editing');
    const completionKeys = claimedWhile.completion;
    const completionOverPointKeys = claimedWhile.completionOverPoint;
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
    // The editing mode the gate judges a key in (ADR-0230 section 2): the one this grid's edit
    // surfaces mark, where the mark is newer than what the gate was told, else the one told. Read,
    // not measured.
    const editingShown = () => {
        let mode = editing;
        let version = editingVersion;
        if (root) {
            for (const surface of root.querySelectorAll('.ex-editor[data-ex-mode]')) {
                const marked = Number(surface.getAttribute('data-ex-mode-version'));
                if (marked > version && surface.closest('.ex-grid') === root) {
                    mode = surface.getAttribute('data-ex-mode');
                    version = marked;
                }
            }
        }
        return mode;
    };

    // The keys whose meaning in an editor field differs between the modes (ADR-0230 section 2): the
    // arrows, Home, End and their Shift and Primary Modifier forms. Escape, Enter, Tab and F2 are the
    // core's in every mode, and a character is typed in every one.
    const modeDependentKeys = new Set(Object.values(claimedWhile).flatMap((keys) => [...keys])
        .filter((canonical) => !editingKeys.has(canonical)));
    // A mode mark is the mode of the text its surface showed in that render (data-ex-mode-text, ADR-0230
    // section 2, note of 2026-10-17), and a field typed into faster than the core answers already holds
    // text the core has not heard. On a circuit `=SUM(` typed at once painted the list of names the core
    // opened for `=S` while the field held `=SUM(`; Home, read as a list's, moved the caret, where typed
    // slowly it points at A10. So a key whose meaning depends on the mode, typed in a field whose
    // surface's mark answers another text, waits, with every key after it, until the core's render has
    // answered the field's text, and is gated then. A character never waits on it: typing goes on at
    // the browser's speed, as section 2 decided (its option (b), rejected, stays rejected).
    const markBehind = (target, k) => {
        if (!k.inEditor || !isTextField(target) || !root.contains(target) || !modeDependentKeys.has(canonicalOf(k))) {
            return null;
        }
        const surface = target.closest('.ex-editor[data-ex-mode-text]');
        return surface !== null && surface.closest('.ex-grid') === root
            && surface.getAttribute('data-ex-mode-text') !== target.value ? surface : null;
    };
    // Resolves once the render answering `field`'s text is in the DOM: its surface's mark is of the
    // text the field holds, or the surface or the field is gone (the edit closed). The keys typed
    // meanwhile are held, so the field's text does not move under it. Read from the attribute the
    // core writes, looked at every animation frame while the key waits, as editorSettled's waits
    // are: nothing is measured, and no observer is added. Bounded by two seconds, the hold's own
    // fallback (ADR-0010).
    const markAnswering = (field) => new Promise((resolve) => {
        const since = performance.now();
        const look = () => {
            const surface = field.isConnected ? field.closest('.ex-editor[data-ex-mode-text]') : null;
            if (!core || surface === null || surface.getAttribute('data-ex-mode-text') === field.value
                || performance.now() - since > 2000) {
                resolve();
            } else {
                requestAnimationFrame(look);
            }
        };
        look();
    });

    // The keys that open a popover from the root (ADR-0039): the popover takes DOM focus a
    // round trip later on a circuit, and a key typed in between must be the popover's, not
    // the grid's (ADR-0010's hold, widened).
    const popoverOpeners = gateSet('popoverOpeners');

    // Find's key (ADR-0055), both cases for CapsLock, and the modifiers' own keydowns, which
    // mean nothing by themselves.
    const findKeys = gateSet('find');
    const modifierKeys = new Set(['Shift', 'Control', 'Alt', 'Meta']);

    // What the gate decides a key is for (ADR-0010): 'popover' — the core's, and it opens a
    // popover, so the keys after it are held until the popover holds DOM focus; 'mode' — the
    // core's, and it can change the editing mode, so the keys after it are held until it is
    // answered;
    // 'core' — the core's, and changes no mode; 'drop' — taken and never forwarded; 'caret' —
    // Home or End on an Apple platform, taken and answered here (ticket 32); 'out' — a Tab
    // released by Escape, the browser's, which takes the keyboard out of the grid (ADR-0012);
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

    // The clipboard's keys: the browser's own copy, cut and paste commands are their default, and
    // the events those fire are what the core answers (ADR-0005, ADR-0014) and what a field acts on.
    // Built by GridKeys.ClipboardKeys: the ones the core answers, with cut, which the core does not
    // answer and a field does, and paste as plain text; one the Consumer declared is its own, and is
    // a key.
    // Read only while a hold stands, where such a key is not held but taken by its event
    // (onHeldClipboard; ADR-0010, 2026-10-02).
    const clipboardKeys = gateSet('clipboard');
    const isClipboardKey = (k) => {
        const canonical = canonicalOf(k);
        return clipboardKeys.has(canonical) && !declared.has(canonical);
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

        // Escape with nothing to dismiss released Tab (ADR-0012, rewritten 2026-10-01): the next
        // Tab or Shift+Tab on the root is left to the browser, which moves to the page's next or
        // previous element, so a keyboard user is never trapped. Any other key ends the release
        // and keeps its meaning; a modifier's own keydown — the Shift of Shift+Tab — is not a
        // key, and nor is a held Escape's repeat, which is the press that released Tab (KB-44;
        // the core answers it with nothing, ADR-0070). Counted here rather than at the keydown,
        // as an Inner Popup's Escape is: a key held behind the Escape ends the release in its
        // turn, after the answer that granted it.
        if (tabReleased && !modifierKeys.has(k.key) && !(k.repeat === true && k.key === 'Escape')) {
            if (editing === 'none' && k.onRoot && (canonical === 'Tab' || canonical === 'Shift+Tab')) {
                return 'out';
            }
            endRelease();
        }

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
            // Escape may release Tab, which changes the keys this gate claims (ADR-0012,
            // rewritten 2026-10-01): a mode change, so the keys after it wait for its answer.
            // Unless the stamp already says this Escape leaves (escapeLeavesNow): then the release is
            // taken here, and the Escape is forwarded for the core to say the same (ADR-0230 section 1).
            if (canonical === 'Escape') {
                releaseEndsAtEscape = releaseEndsHeard;
                if (escapeLeavesNow(k)) {
                    tabReleased = true;
                    return 'core';
                }
                return 'mode';
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
            // A declared key is the Consumer's to answer, and may open a popover or a frame of
            // the Consumer's own (Format Cells' Ctrl+1): the keys after it wait for its answer,
            // and then for wherever the answer said the keyboard is going (handOff; ADR-0050
            // item 16, 2026-10-01).
            if (declared.has(canonical)) {
                return 'mode';
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
        // A declared key changes nothing in the editor and no mode: the core raises it with the
        // edit open, and the Consumer decides (ADR-0050, item 14). Taken, so the browser's own
        // meaning — Ctrl+U's page source — does not run either.
        if (declared.has(canonical)) {
            return 'core';
        }
        // A list of candidates painted is open, whatever the gate was last told: on a circuit
        // the render that paints it and the message that tells the gate are two messages, and a
        // ← typed between them would be claimed as Overwrite's and swallowed. The core marks
        // the list on its box (data-ex-list) in the render itself, and a list open over Point
        // (data-ex-over-point), whose ←, →, Home, End and Shift+arrows point (ADR-0058); read,
        // not measured. Every other change of mode is marked on the edit's surfaces the same way
        // (editingShown; ADR-0230 section 2): a list closed, Point ended by typing, F2's switch.
        const list = listShown();
        const claimed = list === null
            ? (claimedWhile[editingShown()] ?? editingKeys)
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
    //
    // The Keyboard Field (ADR-0080). While the grid has an editable column, the keyboard with no
    // edit open is held by a text field of the grid's own instead of the root itself, so that an
    // IME has somewhere to start (the fifteenth Windows run: on the root, Chrome and Edge give it
    // no input context). It stands for the root in everything here — ADR-0010's guard widens to
    // it: a key, a copy or a paste aimed at it is the root's. It is this grid's own, never a nested
    // grid's.
    const isKeyField = (target) => target instanceof HTMLInputElement && target.classList.contains('ex-key-field')
        && root !== null && target.closest('.ex-grid') === root;
    const isRoot = (target) => target === root || (!!scroller && target === scroller) || isKeyField(target);
    // This grid's Keyboard Field, where the grid has one: the first in its markup, the Viewport's
    // first child, ahead of any grid nested in a cell.
    const keyFieldOf = () => {
        const field = root ? root.querySelector('.ex-key-field') : null;
        return field !== null && isKeyField(field) ? field : null;
    };
    // The render whose cells are on screen now (ADR-0142; ADR-0021's note of 2026-10-05): the name
    // the painting render wrote on this grid's own Viewport (data-ex-paint), the first in the
    // scroller, ahead of any grid nested in a cell. A key and a paste carry it from the moment they
    // are taken, held or not, as a press on the rows carries its own (takenAt): a write they make
    // by position is checked against the order it was aimed under, never against one a render
    // brought in before the write landed; what the cells hold is never judged (ADR-0142, rewritten
    // 2026-10-07). Reads an attribute; nothing is measured, and nothing per cell crosses.
    const paintNow = () => {
        const viewport = scroller ? scroller.querySelector('.ex-viewport') : null;
        const value = viewport ? viewport.getAttribute('data-ex-paint') : null;
        return value === null ? -1 : Number(value);
    };
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
        paint: paintNow(),
    });

    // While editing, the key carries the editor's text and caret (ADR-0051): the core decides
    // what an arrow means from what the user sees now, never from an answer a round trip old.
    // Read as the key is forwarded — a held key's after the keys before it were typed — from
    // the editor surface that holds DOM focus. A read of the field's own value, not a
    // measurement: no layout is read. The selection's end goes with it for F4, which cycles
    // every Reference a selection covers, and so does whether the user moved the caret in that
    // very text: until the core's placement lands, the browser's own caret there is not the
    // user's, and the core tells the two apart as it does for a report (ADR-0051, 2026-09-29);
    // and a caret the user moved leaves F4's rewrite selected, where one typing left stays a
    // caret, as Excel's did (the readings of 2026-10-12, R1 (iv) and (v)).
    // Whether the key is a held key's repeat goes with it too: auto-repeat is visible only here,
    // and by the time a key reaches .NET a repeat looks like a press. The core raises a
    // Consumer's OnLeave once per press, however long Escape is held (ADR-0070). A field of the
    // event the listener already reads; no listener is added and no layout is read. And the render
    // the key was pressed against, read at its keydown (paintNow): a held key keeps the one it was
    // pressed on, as a held press does (ADR-0142, LV-14).
    const forward = (k) => {
        const input = editing !== 'none' ? editorInput() : null;
        // A key on the root that cannot add a layer an Escape peels (layerInputAt, ADR-0230 section 1).
        const neutral = editing === 'none' && neutralKeys.has(canonicalOf(k)) && !declared.has(canonicalOf(k));
        return core.invokeMethodAsync(
            'OnKeyAsync', k.key, k.ctrlKey, k.shiftKey, k.altKey, k.metaKey, metaIsPrimary, !k.onRoot,
            input ? input.value : null, input ? (input.selectionStart ?? input.value.length) : -1,
            input ? (input.selectionEnd ?? input.value.length) : -1, input ? movedByUser(input) : false,
            k.repeat === true, k.paint, nextInput(neutral))
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
    // text: once it has changed, the move was made in text the core no longer holds, and it is
    // forgotten there and then. A move belongs to the text it was made in, not to an equal text
    // written later: F4 cycling a whole column back to =SUM(A:A), the text a ← was pressed in,
    // took the browser's caret at the end for the user's, and the third F4 touched no Reference
    // (the seventeenth Windows run, Part C, cases 8 and 9).
    let caretMoved = null;
    const noteCaretMove = (input) => {
        caretMoved = { input, text: input.value };
    };
    const movedByUser = (input) => {
        if (caretMoved === null || caretMoved.input !== input) {
            return false;
        }
        if (caretMoved.text !== input.value) {
            caretMoved = null;
            return false;
        }
        return true;
    };
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
        // Text an IME inserts into the Keyboard Field without composing it — a full-width space, a
        // character some IMEs commit at once — is carried as a composition's text is; anything
        // else put there outside a composition — the browser's own undo, a drop — is not typing
        // the grid takes, and goes (ADR-0080). The field is no editor surface.
        if (isKeyField(event.target)) {
            if (!event.isComposing && !keyFieldComposing) {
                if (event.inputType === 'insertText' || event.inputType === 'insertReplacementText') {
                    event.target.classList.add('ex-key-field-composing');
                    carryKeyFieldText(event.target);
                } else {
                    event.target.value = event.target.value.slice(0, keyFieldCarried);
                }
            }
            return;
        }
        heardReferenceInput(event);
        // Text put into the Name Box without a key — pasted from a menu, dropped — is the user's,
        // and the first key after it types on (nameBoxSelected).
        if (event.target === nameBoxSelected) {
            nameBoxSelected = null;
        }
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

    // The coloured text (ADR-0057): beneath an editor surface, a layer the core renders with the
    // field's text stands immediately before the field, and carries the text it was rendered for
    // (data-ex-text). On a circuit that is a round trip behind the typing, and colours on text typed
    // past would stand on the wrong characters, so the layer shows — and the field's own text turns
    // transparent — only while the two texts are one: the listener sets one class on the field then,
    // and the stylesheet does the rest. Only in the surface the edit is in, as Excel colours it: the
    // field holding DOM focus, the one held keys are handed to (ADR-0051) — the other surface keeps
    // its plain text, and a press from one into the other takes the colours with it. Compared on each
    // input, when a layer's text or colours change (those two attributes, observed while an edit is
    // open) and whenever the selection moves, which is how a field that has just taken focus, or just
    // opened over its layer, is heard. A field holding an IME composition is ahead of anything
    // rendered, and is never shown over; every input of a composition, its last included, says so,
    // and the composition's end comes with no input after it, so the end is heard too, and the
    // colours come back then rather than at the next keystroke. The layer's line scrolls with the
    // field: the scroll-offset entry, on one more element (ADR-0021). Reads values, two attributes,
    // which element has focus and scroll offsets; no layout.
    //
    // The layer's text is one run, as the field's is, and its References are coloured by the CSS
    // Custom Highlight API (ADR-0057 and ADR-0021, notes of 2026-10-01): when the layer shows, one
    // Range per stretch the core wrote on it (data-ex-colours: "start,length,name" each), over its
    // one text node, in the highlight of that name; when it hides, they are taken out again. The
    // names are this grid's own, and so are the highlights registered under them, which go with the
    // grid: CSS.highlights is one registry per document, and a shared name would let one grid clear
    // another's colours (ADR-0018). The grid's generated stylesheet paints them.
    let composingIn = null;
    let watchingReferenceTexts = false;
    const highlights = new Map();
    const colouredLayers = new Map();
    const referenceTextOf = (field) => {
        const layer = field.previousElementSibling;
        return layer !== null && layer.classList.contains('ex-reference-text') ? layer : null;
    };
    const uncolour = (layer) => {
        const coloured = colouredLayers.get(layer);
        if (coloured !== undefined) {
            for (const [highlight, range] of coloured.ranges) {
                highlight.delete(range);
            }
            colouredLayers.delete(layer);
        }
    };
    // Built again only when the layer's text, its colours or its run has changed since, or a range
    // no longer spans what it was built over — a change to the run's text moves the ranges in it.
    const colour = (layer) => {
        const text = layer.getAttribute('data-ex-text');
        const colours = layer.getAttribute('data-ex-colours') ?? '';
        const run = layer.firstElementChild?.firstChild;
        const coloured = colouredLayers.get(layer);
        if (coloured !== undefined && coloured.text === text && coloured.colours === colours && coloured.run === run
            && coloured.ranges.every(([, range, length]) => range.endOffset - range.startOffset === length)) {
            return;
        }
        uncolour(layer);
        if (!(run instanceof Text) || colours === '' || typeof Highlight !== 'function') {
            return;
        }
        const ranges = [];
        for (const stretch of colours.split(' ')) {
            const [start, length, name] = stretch.split(',');
            const from = Number(start);
            const to = from + Number(length);
            if (!(from >= 0 && to <= run.length)) {
                continue;
            }
            let highlight = highlights.get(name);
            if (highlight === undefined) {
                highlight = new Highlight();
                highlights.set(name, highlight);
                CSS.highlights.set(name, highlight);
            }
            const range = new Range();
            range.setStart(run, from);
            range.setEnd(run, to);
            highlight.add(range);
            ranges.push([highlight, range, to - from]);
        }
        colouredLayers.set(layer, { text, colours, run, ranges });
    };
    const gateReferenceText = (field, layer) => {
        const shown = editing !== 'none' && field === document.activeElement && composingIn !== field
            && layer.getAttribute('data-ex-text') === field.value;
        field.classList.toggle('ex-reference-text-shown', shown);
        if (shown) {
            colour(layer);
        } else {
            uncolour(layer);
        }
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
            referenceTextObserver.observe(root, { attributes: true, attributeFilter: ['data-ex-text', 'data-ex-colours'], subtree: true });
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
        for (const layer of [...colouredLayers.keys()]) {
            uncolour(layer);
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
    // A clipboard key's keydown let through a hold, until the copy, cut or paste event its default
    // fires in the same task takes its place among the held keys (onHeldClipboard).
    let clipboardKeyHeld = false;
    // ST-2's question (ADR-0007's note of 2026-10-11, refined the same day; ADR-0021's note of that
    // day). An edit whose text is still the one it opened with closes when its row leaves the Window,
    // but whether it is still untyped is the browser's to say: the core's copy of the text is a round
    // trip behind the typing on a circuit. So the core asks (askUntouched), and the edit's fields
    // answer in their turn among the held keys: whether every one of them still holds the text the
    // edit opened with, and nothing composes in them. Answered "untouched", they hold what follows
    // until the core has closed the edit (closeAnswered): the first entry's hold stands, so the keys
    // and presses after the answer wait, and are the grid's once the edit has gone, and a paste by its
    // key is held among them (onHeldClipboard); the fields are read-only meanwhile, so that nothing
    // writes into them behind the hold — an IME, which a read-only field does not engage, sends plain
    // keys, which the hold takes; a menu's paste is not offered; a drop is refused by the browser.
    // Input sent before the answer reaches the core ahead of it, so a keystroke is never lost. Reads
    // the fields' values, never layout, and adds no listener.
    // Whether an IME composes in one of the edit's fields (onKeyFieldCompositionStart).
    let surfaceComposing = false;
    // The questions asked while a hold stood, answered once its keys have gone where they were going.
    const questionsAfterHold = [];
    // The hold an "untouched" answer started: the fields it made read-only, with what they were, the
    // answer the drain waits for, and its two-second fallback.
    let closing = null;
    // The text fields of the open edit's surfaces, this grid's own: the Cell Editor's and the
    // Formula Bar's, a Chrome's control inside either box.
    const editFields = () => (root ? [...root.querySelectorAll('.ex-editor')] : [])
        .filter((box) => ownSurface(box) === box)
        .map((box) => surfaceField(box))
        .filter((field) => field instanceof HTMLInputElement || field instanceof HTMLTextAreaElement);
    const holdForClose = (fields) => {
        let answered = null;
        const answer = new Promise((resolve) => {
            answered = resolve;
        });
        closing = {
            fields: fields.map((field) => ({ field, readOnly: field.readOnly })),
            answered,
            // Past any round trip the grid is usable over, as the hold's own fallback (editorSettled):
            // what was held then goes where it can, rather than wait for a core that cannot answer.
            fallback: setTimeout(() => endCloseHold(), 2000),
        };
        for (const field of fields) {
            field.readOnly = true;
        }
        startHold(answer);
    };
    const endCloseHold = () => {
        const ending = closing;
        if (ending === null) {
            return;
        }
        closing = null;
        clearTimeout(ending.fallback);
        for (const { field, readOnly } of ending.fields) {
            if (field.isConnected) {
                field.readOnly = readOnly;
            }
        }
        ending.answered();
    };
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
    // A popover's first scripted focus — its opening focus — lands a round trip after the gesture that
    // opened it (ADR-0039). A press the user makes inside the popover before then has put the keyboard
    // where the user wanted it, and the opening focus, taken against a popover nobody had touched, is
    // refused rather than pull the keyboard back to the first control: Percentage pressed, OK focused
    // and Enter typed at once, the Enter landed on Format Cells' first tab and did nothing (ticket 240,
    // ADR-0204's note of 2026-10-17). Each popover element is its own opening, as the core keys it.
    // Only the first scripted focus is asked: a key's move, or Tab's wrap, answers a key typed there.
    const pressedPopovers = new WeakSet();
    const openedPopovers = new WeakSet();
    const openingOvertaken = (element) => {
        const popover = element.closest('.ex-popover');
        if (!popover || openedPopovers.has(popover)) {
            return false;
        }
        openedPopovers.add(popover);
        // Only where the press left the keyboard inside it: a press on the popover's own ground, which
        // takes no focus, leaves the opening focus to place it.
        const active = document.activeElement;
        return pressedPopovers.has(popover) && active instanceof Element && popover.contains(active)
            && !active.classList.contains('ex-focus-wrap');
    };
    let staleMarks = 0;
    const markStale = (field) => {
        staleField = field;
        return ++staleMarks;
    };

    // This grid's Name Box (ADR-0051): the built-in input, or a Chrome's control inside the core's
    // box — never one of a grid nested in a cell — while it takes typing.
    const ownNameBox = (element) => (element instanceof HTMLInputElement && root !== null
        && element.closest('.ex-name-box')?.closest('.ex-grid') === root && !element.readOnly && !element.disabled
        ? element : null);
    // A press that gives the Name Box the keyboard selects its whole text, as Excel's does, so what
    // is typed replaces the address shown (ADR-0051, decided with the user 2026-10-01; ticket 78).
    // The press keeps its default, which gives the field the keyboard and puts the caret where it
    // landed; its release selects the text, and takes the release's default, which would put the
    // caret back there. No focus is moved from here. A press into the Name Box while it holds the
    // keyboard is the field's own: it places the caret, and a drag selects.
    let nameBoxPressed = null;
    // The Name Box so selected, until the first key, input or press after it. A render that renames
    // it meanwhile — the commit the press made, the pointed cell giving way to the Focus, or a render
    // a round trip behind the press on a circuit — writes the new name over the selection, and the
    // browser leaves the caret after it: the first key selects the whole of it again (onKeyDown).
    let nameBoxSelected = null;

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

    // Where the core has said the keyboard is going (handOff; ADR-0039 and ADR-0050 item 16,
    // 2026-10-01): 'popover' — a Consumer's popover the core opened, whose contents take the
    // keyboard on their count — or 'frame' — a frame of the Consumer's own, outside this root
    // (HandKeyboardToFrameAsync). Until it has arrived, the keys typed on the root — its Keyboard
    // Field among them (isRoot; ADR-0080) — or the menu are held, and then handed to what took
    // it; if it never arrives within the hold's fallback they are dropped, never gated against
    // the grid: a digit there would open an edit behind the frame. Set by C# ahead of the key's
    // answer and of the render that closes the menu the command ran from. Meanwhile the root
    // keeps the keyboard where it can (the hand-back after a command, into the Keyboard Field on
    // a grid that edits), so the keys are heard here: one typed on nothing reaches no grid at all.
    let handOff = null;
    // The control outside this root that held DOM focus when a hand-off to a frame was told: the frame
    // the keyboard is coming from — ExPivot's button whose panel is opening, ExSheet's toolbar closing a
    // list as Format Cells opens — not the one it is going to, which the hand-off has reached only once
    // another control outside the root holds the keyboard. The keys come on through this root, handed
    // back with those typed on the way (takeKeyboardBack), and go on to the frame in order (decided with
    // the user, 2026-10-13: ADR-0061's keys typed while a menu or panel opens; ADR-0050 item 16).
    let handOffFrom = null;
    // ExSheet's KeyTips (ADR-0100; ADR-0050 item 16's note of 2026-10-12): the Alt key gone down alone, on
    // the root or in an edit, while the Consumer declares it (Alt+Alt), and not yet released — and, while
    // keys are held, the held keydown it was. The release is the Consumer's to answer — a Blazor keyup on
    // its own element — and it may then take the keyboard to a frame of its own outside this root
    // (handOff), a round trip later on a circuit, where the letters typed meanwhile would open an edit in
    // the cell (decided with the user, 2026-10-12, decision (C): they wait for the KeyTips). This
    // listener hears that release on the root, in the capture phase, with the root's other keyup
    // (onRootKeyUp; decided with the user, 2026-10-13, which reverses ADR-0100's "rather than adding a
    // listener to the grid"). While keys are held, the release is held too, in its place among them, and
    // handed on in its turn where it was typed, after the press it releases: the Consumer hears a press
    // and its release in the order they were typed, whatever the round trip. Its press, held, reaches the
    // core in its turn too, rather than being skipped as a modifier's. After a release, held or not, the
    // keys behind it wait until the Consumer has answered it: an empty question to the core (Ping),
    // asked once the release has been dispatched, travels behind it, so its answer comes after whatever
    // the Consumer told this listener on hearing it. Handed off, the keys go to the frame, or are dropped
    // with every key behind one it cannot take (a Tab), as a hand-off's are; told nothing, they are the
    // grid's. A chord (a key that carries Alt while it is down) makes it no Alt alone: its release is
    // not handed on, nor its held press. A press on the grid and DOM focus leaving it forget it.
    // An Alt held is handed on where the keyboard is at its turn, not where it was typed: one typed in a
    // popover closing, or anywhere the keys are held, is noted as alone, and is the Consumer's only if
    // it lands on the root, in an edit or on its frame — typed slowly, it went there (decided with the
    // user, 2026-10-13: Escape three times in a list a KeyTip opened, then Alt, H, H, all typed at once,
    // have the outcome they have typed slowly). Landing in a popover's control, or a text field, it and
    // its release are no one's, as an Alt typed there is not the Consumer's.
    let altPending = null;
    // The control of a frame of the Consumer's own the core asked to hand the keyboard to while the
    // keyboard is on its way there (handOff 'frame'; handKeyboardTo): given it in turn, by the hold,
    // rather than at once, where a key typed straight on it could overtake the keys still held for it
    // — Alt, H, O typed at once, the O on the band before the held H (ADR-0100's KeyTips, 2026-10-12).
    let frameInTurn = null;
    // Whether `k` is a declared Alt going down alone, on the root or in an edit — or anywhere, while it is
    // held (`held`), where its turn decides (altPending).
    const altAlone = (k, held) => k.key === 'Alt' && k.repeat !== true && canonicalOf(k) === 'Alt+Alt' && declared.has('Alt+Alt')
        && (held === true || k.onRoot || k.inEditor);
    // A keydown noted against the Alt that may be down: a declared Alt alone starts one, held or not, and
    // a key that carries Alt makes the one down a chord's.
    const noteAlt = (k, entry, held) => {
        if (altAlone(k, held)) {
            altPending = { alone: true, entry };
            if (entry) {
                entry.altAlone = true;
            }
        } else if (altPending !== null && k.altKey && k.key !== 'Alt') {
            altPending.alone = false;
            if (altPending.entry) {
                altPending.entry.altAlone = false;
            }
        }
    };
    // Whether a held release is handed on: its press's, where its press is held with it — a chord made of
    // it, or its landing where no Alt alone is heard, are the press's (altPending) — else its own.
    const releasesAlone = (k) => (k.of ? k.of.altAlone === true : k.altAlone === true);
    // The answer to an Alt's release, once the release has been dispatched: the task after this one,
    // which every listener of the keyup has run before. Asked from the listener itself, the question
    // would overtake the release, which Blazor's listener sends later in the same dispatch.
    const altReleaseAnswered = () => new Promise((dispatched) => {
        setTimeout(dispatched);
    }).then(() => (core ? core.invokeMethodAsync('Ping').catch(() => false) : false));
    // The keys held here, passed on to the element around the root (keysOut), in order, as the keydowns
    // — and an Alt's release, the keyup — they were. What is not a key — a press, a composition's text,
    // a clipboard event — stays this grid's, and goes.
    const passHeldOut = () => {
        const parent = root?.parentElement;
        const keys = held.splice(0, held.length);
        for (const k of keys) {
            if (!parent || typeof k.key !== 'string' || k.key.length === 0 || (k.altUp === true && !releasesAlone(k))) {
                continue;
            }
            parent.dispatchEvent(new KeyboardEvent(k.altUp === true ? 'keyup' : 'keydown', {
                key: k.key, ctrlKey: k.ctrlKey === true, shiftKey: k.shiftKey === true, altKey: k.altKey === true,
                metaKey: k.metaKey === true, repeat: k.repeat === true, bubbles: true, cancelable: true,
            }));
        }
    };
    // The keys a frame heard on the way back, with each declared Alt that went down alone among them
    // noted, and each release of one noted as its press was: a chord among them makes it a chord's.
    const withAltsNoted = (keys) => {
        let pressed = null;
        for (const t of keys) {
            if (t.altUp === true) {
                t.altAlone = pressed?.altAlone === true;
                t.of = pressed ?? undefined;
                pressed = null;
            } else if (altAlone(t)) {
                t.altAlone = true;
                pressed = t;
            } else if (pressed !== null && t.altKey && t.key !== 'Alt') {
                pressed.altAlone = false;
            }
        }
        return keys;
    };
    // A held release handed on in its turn: as the keyup it was, where the keyboard is now — the
    // control or frame it was handed on to, or the root — so the Consumer's handler on an element
    // around it hears it, and then the answer to it. A frame of the Consumer's own the keys are being
    // handed to answers them itself, in the order they reach it, holding those that come while it hands
    // the keyboard on: the keys after the release go on to it in the same task, as the keys before it
    // did, rather than after a question to the core. Waited for, they reached it behind the keyboard's
    // way back from it, and were gated against the root ahead of the keys it held (Escape three times in
    // a list a KeyTip opened, then Alt, H, H, typed at once: H, H opened an edit in the cell).
    const replayAltRelease = async () => {
        const control = focusedControl();
        const frame = control === null && deliveringOutside ? outsideControl() : null;
        const target = control ?? frame ?? keyFieldOf() ?? root;
        replaying = true;
        try {
            target.dispatchEvent(new KeyboardEvent('keyup', { key: 'Alt', bubbles: true, cancelable: true }));
        } finally {
            replaying = false;
        }
        if (frame === null) {
            await core.invokeMethodAsync('Ping').catch(() => false);
        }
    };
    // The hand-off the drain is delivering: 'frame' once DOM focus has left the root for the
    // frame, and whether the keyboard never arrived, so the held keys are dropped.
    let deliveringOutside = false;
    let handOffMissed = false;
    // Outside this root and not on nothing: where a frame of the Consumer's own has the keyboard.
    const outsideControl = () => {
        const active = document.activeElement;
        return active instanceof Element && active !== document.body && active !== document.documentElement
            && !(root && root.contains(active)) ? active : null;
    };
    // Another grid has the keyboard: the user moved there before it reached what this one handed it
    // to, and the keys held here are not that grid's (ADR-0018).
    const inOtherGrid = () => outsideControl()?.closest('.ex-grid') != null;
    const handedOver = () => handOff === null
        || (handOff === 'popover' ? popoverFocused()
            : outsideControl() !== null && outsideControl() !== handOffFrom && !inOtherGrid());

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
            // A choice among others — a Consumer's list of them, the Sheet Toolbar's colours and
            // formats — closes its menu on Enter, as ARIA's menu pattern has it, and the keys after it
            // are the grid's once the menu has gone: a second Enter typed before chose again
            // (ADR-0100's note of 2026-10-12). Space may leave such a menu open, so only Enter holds.
            if (target.matches('[role=menuitemradio]') && k.key === 'Enter' && !k.shiftKey) {
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
        // A button that closes its popover — a panel's OK, Apply or Cancel, which the Chrome marks
        // ex-popover-closes — pressed from the keys: Enter, or Space, whose click comes at its keyup.
        // It closes the popover as a command run does, a round trip later on a circuit, and a key
        // typed meanwhile was the button's: ↓ was lost, and Enter pressed it again (ADR-0010's rule
        // for a key that closes a popover; ADR-0044's note of 2026-10-12).
        const closing = target.closest('.ex-popover-closes');
        if (closing && !closing.matches(':disabled, [aria-disabled=true]') && !k.shiftKey && (k.key === 'Enter' || k.key === ' ')) {
            return closes;
        }
        if (k.key !== 'Enter') {
            return null;
        }
        // Enter in a field submits its form: the filter's, or one whose OK closes the popover.
        const submitsClosing = target instanceof HTMLInputElement && implicitSubmitTypes.has(target.type)
            && target.form?.querySelector('button[type=submit].ex-popover-closes:not(:disabled)');
        return (isTextField(target) && target.closest('.ex-popover-filter')) || submitsClosing ? closes : null;
    };
    // The fields in which Enter submits the form they stand in, as the browser's implicit submission has it.
    const implicitSubmitTypes = new Set(['text', 'search', 'url', 'tel', 'password', 'email', 'number']);
    const onSentinel = (target) => target instanceof Element && target.classList.contains('ex-focus-wrap')
        && popoverOf(target) !== null;
    // A popover closing, and the keyboard in flight back from it (decided with the user on
    // 2026-10-12, ADR-0044's note of that day): Escape in one of this grid's popovers closes it as the
    // core answers it, as a command or a choice made in one does (handsOver, onPress). On a circuit the
    // popover stands a round trip after it, and a key typed meanwhile was the popover's: ↓ moved in a
    // menu about to go and was lost. So the keys after it are held, as behind a command run, until the
    // popover is gone and the keyboard has landed — on the root, or wherever the core or the Consumer
    // handed it on meanwhile (handOff) — and are then handed on there in order. Not an Escape a popup of
    // the contents' own answers (innerPopup), which closes only that popup and leaves the keyboard where
    // it is, nor a held Escape's repeat, which the core answers with nothing (ADR-0070).
    const escapeCloses = (target, k) => {
        if (k.key !== 'Escape' || !plain(k) || k.shiftKey || k.repeat === true || innerPopup || editing !== 'none') {
            return null;
        }
        const popover = popoverOf(target);
        return popover ? { from: target, closes: popover } : null;
    };
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
    // The waits standing in editorSettled. Each is looked at every animation frame, and once more
    // the moment this listener gives the editor the keyboard itself (focusEditor): what the hold
    // has for the edit is then typed into it in a microtask, before the browser hands the page
    // anything else. An IME's composition cannot be held — it reaches whatever has DOM focus — and
    // one started in the editor before the next frame was typed ahead of the text the hold still
    // had for it: `かな`, `感じ` held, `ｄ` composed in the editor, and D10 read `かなｄ感じです`
    // (ED-30, the third composition, WebAssembly).
    const settledWaits = new Set();
    const lookAgain = () => {
        for (const again of [...settledWaits]) {
            again();
        }
    };
    const editorSettled = () => new Promise((settle) => {
        // Settled once: a wait looked at again leaves no second frame of its own behind, and none
        // looks after it has settled.
        let waiting = true;
        let lookFrame = 0;
        const resolve = () => {
            waiting = false;
            settledWaits.delete(again);
            settle();
            lookSettled();
        };
        const look = () => {
            // A grid disposed during the wait has nothing left to wait for, and is asked
            // first: its root is gone, and looking for DOM focus inside it would throw from
            // the next frame, after the page that held it has moved on. Two seconds is past
            // any round trip the grid is usable over; after it the held keys are replayed
            // against whatever there is, rather than held forever.
            const disposed = !core;
            const editorReady = disposed || editing === 'none' || editorFocused() || focusDeclined;
            const popoverReady = disposed || !awaitingPopover || popoverFocused();
            // A frame of the Consumer's own whose control is to take the keyboard in turn (frameInTurn)
            // takes it now, when everything held before it has gone where it was going: the keys held
            // for the frame are handed to it in this same task, so none typed after can overtake them.
            // An Alt's release held among them is one of them: handed on in its turn, it reaches the
            // frame after its press, as typed (altPending).
            if (!disposed && handOff === 'frame' && frameInTurn !== null && editorReady && popoverReady && moved()) {
                const frame = frameInTurn;
                frameInTurn = null;
                handle.handKeyboardTo(frame, true);
            }
            const handOffReady = disposed || handedOver() || (handOff !== null && inOtherGrid());
            if (disposed || (editorReady && popoverReady && handOffReady && moved()) || performance.now() - holdStartedAt > 2000) {
                awaitingPopover = false;
                awaitingMove = null;
                if (handOff !== null) {
                    handOffMissed = !disposed && !handedOver();
                    deliveringOutside = handOff === 'frame' && !handOffMissed;
                    handOff = null;
                    handOffFrom = null;
                }
                frameInTurn = null;
                resolve();
            } else {
                lookFrame = requestAnimationFrame(look);
            }
        };
        const again = () => {
            if (waiting) {
                cancelAnimationFrame(lookFrame);
                look();
            }
        };
        settledWaits.add(again);
        look();
    });

    // How a field moves its caret further than a character, on the platform it runs on, for a held
    // key replayed as the field would have moved it (ADR-0010). By a word with Ctrl on Windows and
    // Linux, and with Option on an Apple platform, where ⌘'s arrows take it to the start or the end
    // of the text instead — as a held Home or End does there (ticket 32) — and Ctrl's arrows are
    // the system's. Alt's arrows are the browser's history on Windows and Linux, and move no caret.
    // Ctrl's Home and End are the text's start and end on Windows and Linux, as Home and End are in
    // a field of one line; and the Primary Modifier's A selects the whole text, on every platform.
    const byWord = (k) => (metaIsPrimary ? k.altKey && !k.ctrlKey : k.ctrlKey && !k.altKey) && !k.metaKey;
    const primaryOnly = (k) => (metaIsPrimary ? k.metaKey && !k.ctrlKey : k.ctrlKey && !k.metaKey) && !k.altKey;
    const unmodified = (k) => !k.ctrlKey && !k.metaKey && !k.altKey;
    // Forward, a word move ends at the end of the word on macOS, Linux and ChromeOS, and on Windows
    // at the start of the next one, past the spaces after this one: the browser's own conventions.
    const wordMovePassesSpaces = /^win/i.test(platform);
    // The word-like stretches of a field's text, as [start, end) offsets: Intl.Segmenter's words,
    // from the Unicode word breaks the browser moves by itself, with the spaces and punctuation
    // between them left out. Where a browser has no segmenter, runs of letters, marks, digits and
    // underscores.
    let wordSegmenter;
    const wordsOf = (text) => {
        if (wordSegmenter === undefined) {
            wordSegmenter = typeof Intl.Segmenter === 'function'
                ? new Intl.Segmenter(undefined, { granularity: 'word' })
                : null;
        }
        if (wordSegmenter === null) {
            return [...text.matchAll(/[\p{L}\p{M}\p{N}_]+/gu)].map((run) => [run.index, run.index + run[0].length]);
        }
        const words = [];
        for (const piece of wordSegmenter.segment(text)) {
            if (piece.isWordLike) {
                words.push([piece.index, piece.index + piece.segment.length]);
            }
        }
        return words;
    };
    // Where a word move from `at` lands: back, at the start of the word the caret is in, or of the
    // word before it; forward, at the end of the word the caret is in or of the next one — on
    // Windows at the start of the next word — and at the text's end where there is none. The
    // field's own text is all that is read (ADR-0021).
    const wordEdge = (text, at, forward) => {
        const words = wordsOf(text);
        if (!forward) {
            let edge = 0;
            for (const [start] of words) {
                if (start >= at) {
                    break;
                }
                edge = start;
            }
            return edge;
        }
        for (const [start, end] of words) {
            if (wordMovePassesSpaces ? start > at : end > at) {
                return wordMovePassesSpaces ? start : end;
            }
        }
        return text.length;
    };

    // A held key a text field would have handled itself, handled as it would have: text is
    // typed at the caret, a deletion deletes, a caret key moves the caret — Shift extending the
    // selection from its anchor, and a word or the text's edge at a time with the modifier the
    // platform does that with (byWord, primaryOnly). Anything else was the browser's, and its moment
    // has passed. Whether it was one of these is answered. Nothing is read but the field's value
    // and selection (ADR-0021).
    const typeInto = (input, k) => {
        const start = input.selectionStart ?? input.value.length;
        const end = input.selectionEnd ?? start;
        const inEditor = input.closest('.ex-editor') !== null;
        // A composition's text from the Keyboard Field (ADR-0080) is typed as a character is.
        if (k.text !== undefined || (k.key.length === 1 && !k.ctrlKey && !k.metaKey)) {
            input.setRangeText(k.text ?? k.key, start, end, 'end');
        } else if (k.key === 'Backspace' || k.key === 'Delete') {
            // A character, or with the word modifier a word, before the caret or after it; a
            // selection is deleted whole, with or without it.
            const word = byWord(k) && !k.shiftKey;
            if (!word && !unmodified(k)) {
                return false;
            }
            const forward = k.key === 'Delete';
            let from = start;
            let to = end;
            if (start === end) {
                const reach = word ? wordEdge(input.value, start, forward)
                    : forward ? Math.min(input.value.length, start + 1) : Math.max(0, start - 1);
                from = Math.min(start, reach);
                to = Math.max(start, reach);
            }
            input.setRangeText('', from, to, 'end');
        } else if (k.key === 'Home' || k.key === 'End') {
            // As the field would have: to the start or the end, Shift extending the selection, and
            // scrolled to show it. Setting the selection from script moves no view, so a held End
            // replayed on a circuit left the caret at the end and the text shown from its start
            // (ticket 32, seen in CI on the Server host).
            if (!unmodified(k) && (metaIsPrimary || !primaryOnly(k))) {
                return false;
            }
            placeCaretAtEnd(input, k, inEditor);
            return true;
        } else if (k.key === 'ArrowLeft' || k.key === 'ArrowRight') {
            const forward = k.key === 'ArrowRight';
            if (metaIsPrimary && primaryOnly(k)) {
                placeCaretAtEnd(input, { key: forward ? 'End' : 'Home', shiftKey: k.shiftKey }, inEditor);
                return true;
            }
            const word = byWord(k);
            if (!word && !unmodified(k)) {
                return false;
            }
            // The end of the selection a caret key moves is its focus, the other its anchor: the
            // start of a backward selection is its focus, the end of any other. A selection with
            // the plain arrow collapses to its own edge on that side; anything else moves the focus.
            const backward = input.selectionDirection === 'backward';
            const anchor = backward ? end : start;
            const focus = backward ? start : end;
            const to = word ? wordEdge(input.value, focus, forward)
                : start !== end && !k.shiftKey ? (forward ? end : start)
                    : forward ? Math.min(input.value.length, focus + 1) : Math.max(0, focus - 1);
            if (inEditor) {
                noteCaretMove(input);
            }
            if (!k.shiftKey) {
                input.setSelectionRange(to, to);
            } else if (to < anchor) {
                input.setSelectionRange(to, anchor, 'backward');
            } else {
                input.setSelectionRange(anchor, to, 'forward');
            }
            showCaret(input, to);
            return true;
        } else if ((k.key === 'a' || k.key === 'A') && !k.shiftKey && primaryOnly(k)) {
            input.setSelectionRange(0, input.value.length, 'forward');
            return true;
        } else {
            return false;
        }
        // Shown as the field shows what is typed: the text set from script moves no view, so the
        // keys held while an edit opened on a circuit left the caret at the end of the text and
        // the field showing its start (ticket 75, found by its test on the Server host).
        showCaret(input, input.selectionEnd);
        // The field hears it as it hears typing (its @oninput).
        input.dispatchEvent(new Event('input', { bubbles: true }));
        return true;
    };
    // A caret placed from script at either end of the text, brought into view as the field brings
    // the user's own: setting the value or the selection from script moves no view (ticket 32;
    // ticket 75, where a Reference Point wrote at the end of a Formula stood past the Cell
    // Editor's right edge). At the start the field shows its start; at the end the offset is set
    // past the far end, and the browser clamps it, so the caret stands at the field's right edge.
    // Nothing is read but the field's value (ADR-0021). A caret short of the end is left where the
    // browser shows it: no place for it can be set without the width of the text before it
    // (ticket 75's Comments). The layer that colours the field follows the offset through the
    // field's scroll event, as it does when the user types (ADR-0057).
    const showCaret = (input, at) => {
        if (at <= 0) {
            input.scrollLeft = 0;
        } else if (at >= input.value.length) {
            input.scrollLeft = Number.MAX_SAFE_INTEGER;
        }
    };
    // Home or End answered in an editor field on an Apple platform (ticket 32), as Windows and
    // Linux answer them, and a held Home or End replayed on any platform: the caret to the
    // text's start or end, or with Shift the selection extended there from its anchor; in an
    // editor surface, the user's own move. The field is scrolled to show that end (showCaret),
    // so nothing is read but the field's value and selection (ADR-0021).
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
        showCaret(input, edge);
    };
    // A held key the edit's field is left, handled as the field would have handled it (typeInto):
    // chords among them, so that a Ctrl+← held behind F2 moves the caret by a word as it does when
    // it is not held, rather than be dropped while the End before it moved the caret — an outcome
    // that turned on whether the keys arrived before F2's answer (ED-2, seen in CI on Edge,
    // WebAssembly). A chord typeInto does not know is the browser's, and is dropped.
    const typeIntoEditor = (k) => {
        const input = editorInput();
        if (input) {
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
    // The Primary Modifier's A, which selects a text field's whole text (typeInto): held behind the
    // Name Box's press, it is typed into the Name Box at its turn (ADR-0230 section 3).
    const selectsAll = (k) => (k.key === 'a' || k.key === 'A') && !k.shiftKey && primaryOnly(k);
    // The keys a tab of ARIA's tabs pattern answers itself.
    const tabKeys = new Set(['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', 'Home', 'End']);
    // The keys that move the caret in a field without changing its text.
    const caretMoveKeys = new Set(['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', 'Home', 'End', 'PageUp', 'PageDown']);
    const reproducible = (target, k) => {
        if (k.key === 'Escape' || target.closest('[role=menu]')) {
            return true;
        }
        if (isTextField(target)) {
            return (!k.ctrlKey && !k.metaKey && !k.altKey && (k.key.length === 1 || caretKeys.has(k.key)))
                || selectsAll(k);
        }
        // A frame of the Consumer's own took the keyboard (HandKeyboardToFrameAsync): what a key
        // means there is its handlers' to say, and every key but Tab — the browser's alone to
        // act on — is handed to it.
        if (root && !root.contains(target)) {
            return k.key !== 'Tab';
        }
        const role = target.getAttribute('role');
        // A tab of ARIA's tabs pattern answers its arrows, Home and End with its own handlers.
        if (role === 'tab' && tabKeys.has(k.key) && !k.ctrlKey && !k.metaKey && !k.altKey) {
            return true;
        }
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

    // Script-written text does not become a native edit. Keep the missing commit only for
    // the field the hold actually changed (ADR-0210). One record per instance also bounds
    // the lifetime of a detached field, for which the browser need not send blur.
    let heldTextEdit = null;
    const forgetHeldText = () => {
        if (!heldTextEdit) return;
        const { field } = heldTextEdit;
        heldTextEdit = null;
        field.removeEventListener('blur', commitHeldText);
        field.removeEventListener('change', forgetHeldText);
        field.removeEventListener('keydown', commitOnEnter);
    };
    const commitHeldText = () => {
        if (!heldTextEdit) return;
        const { field, before } = heldTextEdit;
        forgetHeldText();
        if (field.isConnected && before !== field.value) {
            field.dispatchEvent(new Event('change', { bubbles: true }));
        }
    };
    // An Enter typed once the hold is over reaches the field natively, and the browser commits
    // only a value that differs from the one script last wrote: none, so the held text went
    // uncommitted while the field showed it (More Colours' HEX, seen in CI on WebAssembly). It
    // commits here, before the field's handlers hear the key; where typing since has changed the
    // value, the browser's own change comes and this one would be its duplicate. A held Enter is
    // committed where it is handed on (handToPopover).
    const commitOnEnter = (event) => {
        if (!replaying && event.key === 'Enter' && !event.isComposing
            && heldTextEdit?.written === heldTextEdit?.field.value) {
            commitHeldText();
        }
    };
    const rememberHeldText = (field, before) => {
        if (heldTextEdit?.field === field) return;
        forgetHeldText();
        heldTextEdit = { field, before, written: field.value };
        field.addEventListener('blur', commitHeldText);
        field.addEventListener('change', forgetHeldText);
        field.addEventListener('keydown', commitOnEnter);
    };

    const handToPopover = (target, k) => {
        if (isTextField(target) && ((!k.ctrlKey && !k.metaKey && !k.altKey) || selectsAll(k))) {
            if (k.key === 'Enter') {
                // A native Enter commits a text field's change before submitting its form.
                // A dispatched keydown does neither: a change-bound field such as More Colours'
                // HEX would show the held text while its Consumer still held the old value.
                // Keep the key first for handlers such as Find's Shift+Enter (ADR-0055).
                replayInto(target, k);
                if (heldTextEdit?.field === target) commitHeldText();
                target.form?.requestSubmit();
                return;
            }
            const before = target.value;
            if (typeInto(target, k)) {
                if (before !== target.value) rememberHeldText(target, before);
                if (heldTextEdit?.field === target) heldTextEdit.written = target.value;
                return;
            }
        }
        replayInto(target, k);
    };

    // The held keys dropped, every one behind the key that could not be handed on (ADR-0010). A
    // copy among them is abandoned, and its write lands nothing: the clipboard keeps what it held,
    // as it does when a copy is refused (ADR-0005).
    const dropHeld = () => {
        for (const k of held) {
            if (k.clipboard === 'copy') {
                k.take(false);
            }
        }
        held.length = 0;
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

    // The answer to the Name Box's Enter (ExGrid.NameBoxAnsweredAsync): asked from the task after
    // the Enter's, whose default has by then submitted the form, so the core has heard the
    // submission before the question, on a circuit as in WebAssembly. Ordered by the core, never
    // by time; an Enter that submitted nothing finds the last answer, long complete.
    const nameBoxAnswered = () => new Promise((submitted) => {
        setTimeout(submitted);
    }).then(() => (core ? core.invokeMethodAsync('NameBoxAnsweredAsync', nextInput()).catch((error) => {
        if (core) {
            console.error('[ex-grid] the grid failed to answer the Name Box', error);
        }
    }) : undefined));
    // The keys after the Name Box's Enter wait for its answer, in their place behind it: a mark
    // among the held keys, which the drain waits on in its turn (onKeyDown).
    const holdBehindNameBoxEnter = () => {
        held.push({ nameBoxEnter: nameBoxAnswered() });
        if (answering) {
            holdStartedAt = performance.now();
        } else {
            startHold();
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
            // The keyboard is leaving for the Consumer's element around the root: the keys held are its.
            if (keysOut) {
                passHeldOut();
                break;
            }
            // The keyboard never reached what the core handed it to: the keys are dropped, not
            // gated against the grid (handOff).
            if (handOffMissed) {
                dropHeld();
                break;
            }
            const k = held.shift();
            // The Name Box's Enter (holdBehindNameBoxEnter): the keys after it go on once the core
            // has answered it, where the keyboard then is.
            if (k.nameBoxEnter !== undefined) {
                await k.nameBoxEnter;
                await editorSettled();
                continue;
            }
            if (k.barPress) {
                await answerBarPress(k);
                continue;
            }
            if (k.nameBoxPress) {
                await answerNameBoxPress(k);
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
            // The release of an Alt that went down alone, held in its place (altPending): handed on as
            // the keyup it was, and the keys after it wait for the Consumer's answer to it — a hand-off
            // to a frame of its own (handOff), which they then wait for and go to, or nothing. A chord's
            // release is not handed on: its press was not either.
            if (k.altUp) {
                if (releasesAlone(k)) {
                    await replayAltRelease();
                    await editorSettled();
                }
                continue;
            }
            // Keys a frame of the Consumer's own heard on the way back to this root (takeKeyboardBack):
            // in their place, ahead of every key typed on the root since, and the grid's from here.
            if (k.typedOnTheWay !== undefined) {
                const typed = await k.typedOnTheWay;
                held.unshift(...withAltsNoted((Array.isArray(typed) ? typed : []).map((t) => ({
                    altUp: t.type === 'keyup' ? true : undefined,
                    key: String(t.key ?? ''),
                    ctrlKey: t.ctrlKey === true,
                    shiftKey: t.shiftKey === true,
                    altKey: t.altKey === true,
                    metaKey: t.metaKey === true,
                    repeat: t.repeat === true,
                    onRoot: true,
                    inEditor: false,
                    inPopover: false,
                    inFindField: false,
                    paint: k.paint,
                })).filter((t) => t.key.length > 0 && (t.altUp !== true || t.key === 'Alt'))));
                // The keyboard taken back through this root to go on to a frame of the Consumer's own, told
                // while the frame's keys were asked for — ExPivot's report filter whose Filter… is opening
                // (decided with the user, 2026-10-13, ADR-0061): they wait for it to arrive and go there, in
                // order, as every key held while a hand-off stands does, and none is gated against the root
                // (ADR-0050 item 16's note of 2026-10-12).
                if (handOff !== null) {
                    await editorSettled();
                }
                continue;
            }
            // A composition that ended in the Keyboard Field (ADR-0080): with no edit open, the
            // core opens one holding its text, as a typed character opens one holding it, and the
            // keys after it wait for the editor; with one open — a second composition the IME
            // finished there before the first one's editor had the keyboard — it is typed into
            // the edit at its caret, as a held character is.
            //
            // Unless a control of the grid's, or a frame of the Consumer's own, has taken the
            // keyboard meanwhile: the composition was finished while a popover opened — Format
            // Cells' Ctrl+1, a column's Alt+↓, Ctrl+F's panel, a command run in a menu's place —
            // or while a frame did, and the keys typed then are never the grid's (ADR-0039, note
            // of 2026-10-01). Asked of the core, the text opened a Cell Editor behind the popover,
            // and the editor took the keyboard from it. It is handed to what holds the keyboard,
            // as a held character is: typed at the caret of a text field (typeInto); on any other
            // control it is text no keydown carries, a key script cannot reproduce, and it is
            // dropped with every key held behind it (ADR-0010). A cancelled composition typed
            // nothing, and hands nothing on.
            if (k.text !== undefined) {
                const holder = focusedControl() ?? (deliveringOutside ? outsideControl() : null);
                if (holder) {
                    if (k.text.length === 0) {
                        continue;
                    }
                    if (!isTextField(holder)) {
                        dropHeld();
                        break;
                    }
                    typeInto(holder, k);
                    continue;
                }
                if (editing === 'none') {
                    const opened = await core.invokeMethodAsync('OnKeyFieldTextAsync', k.text, k.paint, nextInput()).catch((error) => {
                        if (core) {
                            console.error('[ex-grid] the grid failed to take a composition', error);
                        }
                        return false;
                    });
                    if (!opened) {
                        settleKeyField();
                    }
                    await editorSettled();
                } else {
                    const input = editorInput();
                    if (input && k.text.length > 0) {
                        typeInto(input, k);
                    }
                }
                continue;
            }
            // A copy, cut or paste the keys before it held (onHeldClipboard), done where the
            // keyboard is now.
            if (k.clipboard !== undefined) {
                await takeClipboard(k);
                continue;
            }
            // A modifier's own keydown — the Shift pressed for a capital, or for Shift+Enter —
            // is held with the rest to keep their order, and means nothing by itself: the key
            // that follows carries it. Replaying it would be a key no field can reproduce, and
            // that stops the replay there, dropping every key typed after it.
            // A declared Alt that went down alone is handed on in its turn, so that its release, handed on
            // after it, finds it (altPending): to the root or the edit, or to a frame of the Consumer's own.
            // Landing on a control inside this root — a popover's — or in a text field, which cannot
            // reproduce it, it is no Alt alone, and its release goes with it.
            const target = focusedControl() ?? (deliveringOutside ? outsideControl() : null);
            if (k.key === 'Alt' && k.altAlone === true && target && (isTextField(target) || root.contains(target))) {
                k.altAlone = false;
            }
            if (modifierKeys.has(k.key) && !(k.key === 'Alt' && k.altAlone === true)) {
                continue;
            }
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
                    dropHeld();
                    break;
                }
                const move = handsOver(target, k) ?? escapeCloses(target, k);
                handToPopover(target, k);
                // A held Enter handed to this grid's Name Box submitted its form: the keys behind it
                // wait for its answer, as behind one typed there (holdBehindNameBoxEnter).
                if (k.key === 'Enter' && ownNameBox(target) !== null) {
                    await nameBoxAnswered();
                    await editorSettled();
                    continue;
                }
                if (!root.contains(target) && isTextField(target) && k.key === 'Escape' && plain(k) && !k.shiftKey
                    && k.repeat !== true) {
                    // A Consumer frame may hand the keyboard on from Escape, before its handler's
                    // first await. Acknowledge that handler's entry before interpreting the next
                    // held key as a text field's: otherwise Down is dropped there, or a letter is
                    // typed into the field that is closing. This is the ordered circuit question
                    // already used for Alt's release (ADR-0050), not a delay or a timing estimate.
                    // Only a text field: there this drain decides what the next key does (typeInto,
                    // reproducible), and no handler of the frame's hears it. Any other control of a
                    // frame hears the next key as the keydown it was, after the Escape, and answers
                    // its keys in the order they reach it, as with an Alt's release (replayAltRelease):
                    // the keys behind go on to it in this same task. Asked there, the question left the
                    // frame holding the keyboard with keys still held for it, and a key typed straight
                    // on it overtook them: F10, H, Escape, H, then A typed on ExSheet's toolbar band as
                    // it took the keyboard, ended the KeyTips with the A, and the held H opened an edit
                    // in the cell (ADR-0050 item 16: a control takes the keyboard in turn, and the keys
                    // held for it are handed to it in the same task).
                    await core?.invokeMethodAsync('Ping').catch(() => false);
                    await editorSettled();
                }
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
            // Held behind another key while the field's surface marked the mode of an older text: gated
            // once the core has answered the field's text, as at its keydown (markBehind).
            const field = rebased.inEditor ? editorInput() : null;
            if (field && markBehind(field, rebased) !== null) {
                await markAnswering(field);
            }
            const verdict = gate(rebased);
            if (verdict === 'out') {
                // A released Tab held behind its Escape: the browser's move of DOM focus cannot be
                // made from script (ADR-0021), so it is dropped with every key behind it, as a held
                // Tab is in a popover, rather than hand the grid keys meant for the page's next
                // element. The release stands, for the Tab the user presses again, and the live
                // region says so: refused, not dropped silently (ADR-0230 section 1 (b)).
                core?.invokeMethodAsync('OnHeldTabRefusedAsync', nextInput(true)).catch((error) => {
                    if (core) {
                        console.error('[ex-grid] the grid failed to refuse a held Tab', error);
                    }
                });
                dropHeld();
                break;
            }
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
        // A hand-off told while the hold drained — to a frame of the Consumer's own, or its popover —
        // and not yet reached: the hold stands until the keyboard arrives, so that the keys typed
        // meanwhile go there too, and none is gated against the root (ADR-0050 item 16's note of
        // 2026-10-12).
        if (core && handOff !== null) {
            await editorSettled();
            if (held.length > 0 && core) {
                return drain();
            }
        }
        answering = false;
        handOffMissed = false;
        lookSettled();
        deliveringOutside = false;
        // A question the core asked while this hold stood is answered now, in its turn among the keys
        // (askUntouched); one that starts a hold of its own leaves the rest to that hold's end.
        while (questionsAfterHold.length > 0 && !answering) {
            questionsAfterHold.shift()();
        }
    };

    const onKeyDown = (event) => {
        lookSettled();
        if (!core) {
            return;
        }
        // The keyboard is in use: whatever puts it in the Keyboard Field next did not come of a
        // press on the grid (KB-12, onFocused).
        keyboardByPress = false;
        // A key typed in an editor surface: that surface holds the keyboard.
        noteSurface(event.target);
        // The first key after a press selected the Name Box's text replaces the whole name, whatever
        // a render has written over that selection since (nameBoxSelected) — an IME's first
        // composing key too, so it is done before those are let through.
        if (nameBoxSelected !== null) {
            if (event.target === nameBoxSelected) {
                nameBoxSelected.select();
            }
            nameBoxSelected = null;
        }
        // Mid-composition an IME owns Enter, Escape and the arrows — they choose and
        // commit a candidate. Taking them there breaks typing in any language that needs
        // one, and the grid would move under a half-finished word. One the IME takes in the
        // Keyboard Field is told until its keyup (imeKeyTold, ADR-0080).
        if (event.isComposing || event.keyCode === 229) {
            if (isKeyField(event.target)) {
                imeKeyTold = true;
            }
            return;
        }
        // Typed while the keyboard leaves for the Consumer's element around the root (keysOut): that
        // element's handler hears it, after every key passed on before it; its default would act on a
        // grid the keyboard is leaving.
        if (keysOut && !replaying) {
            event.preventDefault();
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
        // Not from the Keyboard Field (ADR-0080): the document's selection is the caret inside it
        // then, which the IME needs, and copy and paste are aimed at the field already.
        if (k.onRoot && !isKeyField(event.target)) {
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
        const holdOver = answering && awaitingMove !== null && held.length === 0 && moved() && handedOver();
        if (!replaying && (!holdOver && (answering || sentinel || (editing !== 'none' && k.onRoot && !editorFocused())))) {
            event.stopPropagation();
            // Noted as held: its release is held behind it, and both are handed on in their turn, where the
            // keyboard is then (altPending).
            noteAlt(k, isClipboardKey(k) ? null : k, true);
            // A clipboard key is not held as a key: a copy or a paste dispatched from script does
            // nothing, so held it was lost — the clipboard kept what it held before, and a paste
            // never landed (CP-6 on the Server host, 2026-10-02). Its default runs, and the copy,
            // cut or paste event it fires is taken in its place among the held keys
            // (onHeldClipboard): the paste's data read now, which only the event can read, and
            // the copy's write started now, which only the event may start. What either does is
            // decided at its turn, against the mode and the Selection the keys before it leave.
            if (isClipboardKey(k)) {
                clipboardKeyHeld = true;
                setTimeout(() => {
                    clipboardKeyHeld = false;
                });
            } else {
                event.preventDefault();
                held.push(k);
            }
            if (!answering) {
                if (sentinel) {
                    // A column's popover wraps back to its commands; the find panel and a
                    // Consumer's popover, which have none, to their own contents
                    // (ADR-0044/0055; ADR-0050 item 16).
                    const into = popoverOf(event.target)?.querySelector('.ex-popover-commands')
                        ? '.ex-popover-commands'
                        : '.ex-popover-body';
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
        // Enter in this grid's Name Box hands the keyboard back to the root and has the Consumer
        // place the Selection where the name says (ADR-0051): its form's implicit submission, which
        // keeps its default here. On a circuit both land a round trip later, and the keys typed
        // meanwhile went into the Name Box the hand-back then left, and were lost — `abc` typed
        // straight after naming the Focus opened no edit (ticket 200). So the keys after it are
        // held, in order, until the core has answered the submission (nameBoxAnswered), and are
        // then handed on where the keyboard is: on the root, against the Focus the placement left.
        if (!replaying && k.key === 'Enter' && ownNameBox(event.target) !== null) {
            // Any key but a modifier's own ends a release of Tab, as the gate has it.
            endRelease();
            holdBehindNameBoxEnter();
            return;
        }
        // A mode-dependent key typed while its field's surface marks the mode of an older text
        // (markBehind): held until the core has answered the field's text, then gated in its turn.
        if (!replaying && markBehind(event.target, k) !== null) {
            event.preventDefault();
            event.stopPropagation();
            noteAlt(k, k, true);
            held.push(k);
            startHold(markAnswering(event.target));
            return;
        }
        if (!replaying) {
            noteAlt(k, null);
        }
        // Read before the gate, which spends an Inner Popup's Escape (escapeCloses).
        const closing = replaying ? null : escapeCloses(event.target, k);
        const verdict = gate(k);
        if (verdict === 'out') {
            // The browser takes the keyboard out of the grid with it, and the release is spent.
            endRelease();
            return;
        }
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
        } else if (verdict === 'core' && closing !== null) {
            // Escape closing a popover: the keys after it wait until it is gone (escapeCloses).
            awaitingMove = closing;
            if (answering) {
                holdStartedAt = performance.now();
            } else {
                startHold(answer);
            }
        } else if (verdict === 'core' && leaves && k.onRoot && k.key === 'Escape' && plain(k) && !k.shiftKey
            && k.repeat !== true && editing === 'none') {
            // An Escape the Consumer may answer by taking the keyboard out of the grid (OnLeave): the
            // keys after it wait for its answer, and go out with the keyboard if it is told so
            // (keysOut), or are this grid's as ever.
            if (answering) {
                holdStartedAt = performance.now();
            } else {
                startHold(answer);
            }
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

    // The seventh entry (ADR-0021, ADR-0080): the Keyboard Field's composition. The listener above
    // leaves every composing key alone (ADR-0010), so an IME composes in the field that holds the
    // keyboard. The field is drawn over the Focus cell while it composes (a class, which the
    // stylesheet turns into the Cell Editor's look), and the core is told, so the Focus is
    // revealed if a scroll had taken it away. When the composition ends, its text takes its place
    // among the held keys (drain): the core opens the Cell Editor in Overwrite holding it, and the
    // keys typed after it wait for the editor, as after a typed character. The field keeps showing
    // the text until the keyboard has gone to the editor, so nothing blinks out for a round trip.
    // Heard always, where the coloured text's own compositionend is heard only while an edit is
    // open (ADR-0057): this one is for a composition with no edit open.
    //
    // DOM focus never moves while a composition lasts: moving it would end the composition there
    // and then, and the next keys would start another — `kana` would come out `ｋあ`, as the
    // fifteenth Windows run saw `k穴` in i1y. So a request that the editor take the keyboard
    // waits while the field composes (focusEditor), and a press that would take it — on the rows,
    // the headings, or a field beside them — ends the composition first, so that its text
    // reaches the core ahead of the press (onPress).
    let keyFieldComposing = false;
    // How much of the field's value has already been handed on: a second composition finished in
    // the field before the keyboard left it is appended to the first, and only its own text goes.
    let keyFieldCarried = 0;
    // The render the composition was typed against, read at its compositionstart (paintNow), as a
    // key carries the one its keydown was typed against: a composition aimed with a Selection that an
    // order move or a replaced Source has dropped since opens nothing (ADR-0142, 2026-10-08). Reads an
    // attribute; nothing is measured.
    let keyFieldPaint = -1;
    // The editor's request for the keyboard, made while the field was composing or while a key the
    // IME took was still being told: granted once neither holds (grantEditorFocus).
    let deferredEditorFocus = null;
    // A key the IME took in the field is still being told: from its keydown, which the listener
    // above leaves to the IME, to the next keyup heard on the root, its own as a rule. The key that
    // ends one composition can start the next: the sixteenth Windows run's k6, where the `k` of
    // `kanji` ended `かな` and started `ｋ`. On WebAssembly the end's own listeners ran the core's
    // whole answer to the text, the Cell Editor opened and its request for the keyboard made, 22 ms
    // of it in that run; granted then, between the end and the start, the request moved DOM focus,
    // the IME carried `ｋ` into the editor without its romaji, and the `a` composed `あ`: `かな暗示`.
    // Every event the IME owes a key reaches the page before that key's keyup, and before any later
    // key's events, as the browser takes them in order — so the keyup is the moment the next
    // composition, if the key started one, has said so, whatever the time between them. It was the
    // task after the end until 2026-10-12, which a start told a moment later missed: a script
    // driving the IME, whose end and start are two commands, had the request granted between them,
    // the start composed in the editor ahead of the text still held for it, and D10 read `かなｄ感じです`
    // (ED-30, the third composition, WebAssembly). A composition that ends with no such key being
    // told — a candidate chosen with the pointer, text a script commits — has nothing more to come,
    // and the request is granted at its end. And the keyboard leaving the field ends the key too
    // (onFocusLeft): whatever it still owes is told wherever the keyboard went, and none of it can
    // start a composition here. Alt+Tab mid-composition is that key — the sixteenth Windows run's
    // k15: its keydown was the IME's, the window switch ended the composition, and in 7 of 12
    // configurations its keyup went to the other window, so a keyup alone would have kept the
    // request waiting for whatever key came next. No layout is read (ADR-0021's seventh entry).
    let imeKeyTold = false;
    // The field goes back to being unseen and empty: once the keyboard has left it, or when the
    // composition's text opened nothing (a cell that does not edit).
    const settleKeyField = () => {
        const field = keyFieldOf();
        if (field === null || keyFieldComposing) {
            return;
        }
        field.value = '';
        keyFieldCarried = 0;
        field.classList.remove('ex-key-field-composing');
    };
    const onKeyFieldCompositionStart = (event) => {
        lookSettled();
        // A composition in one of the edit's fields: the edit is being typed in (askUntouched).
        if (ownSurface(event.target) !== null) {
            surfaceComposing = true;
        }
        if (!core || !isKeyField(event.target)) {
            return;
        }
        keyFieldComposing = true;
        keyFieldPaint = paintNow();
        event.target.classList.add('ex-key-field-composing');
        if (editing === 'none' && !answering) {
            core.invokeMethodAsync('OnKeyFieldCompositionStartAsync', nextInput()).catch((error) => {
                if (core) {
                    console.error('[ex-grid] the grid failed to hear a composition', error);
                }
            });
        }
    };
    const onKeyFieldCompositionEnd = (event) => {
        lookSettled();
        const field = event.target;
        if (ownSurface(field) !== null) {
            surfaceComposing = false;
        }
        if (!core || !isKeyField(field)) {
            return;
        }
        keyFieldComposing = false;
        carryKeyFieldText(field);
    };
    // The text the field holds past what has already gone takes its place among the held keys,
    // and a request for the editor's focus that waited for it is granted, unless the key the IME
    // took is still being told (imeKeyTold).
    const carryKeyFieldText = (field) => {
        const text = field.value.slice(keyFieldCarried);
        keyFieldCarried = field.value.length;
        held.push({ text, paint: keyFieldPaint });
        if (!answering) {
            startHold();
        }
        // Ended by the keyboard leaving the field, the text stays only as long as it is on its way.
        if (document.activeElement !== field) {
            settleKeyField();
        }
        grantEditorFocus();
    };
    // The editor's request, granted once the field has stopped composing and no key the IME took is
    // still being told; a key that started another composition leaves it waiting for that one's end.
    // `toward` is where the keyboard is going when the field is being left (onFocusLeft).
    const grantEditorFocus = (toward) => {
        const pending = deferredEditorFocus;
        if (pending !== null && !keyFieldComposing && !imeKeyTold) {
            deferredEditorFocus = null;
            handle.focusEditor(pending.bar, pending.fromField, toward);
        }
    };
    // The root's one keyup listener. The key the IME took has been told whole: everything it owed the
    // page came before this. And, where the Consumer declares the Alt key alone, the Alt key's release
    // (altPending; decided with the user, 2026-10-13): held in its place while keys are held — kept from
    // the Consumer's handler until its turn — and otherwise left to reach it, with the keys typed after
    // it held until it has been answered. Whether it was alone is told here, where every key between
    // its press and its release is heard in order: a chord's release, or one whose press this listener
    // did not hear, never reaches the Consumer. The releases of other keys go their way, and the
    // Consumer does not read them: held keys' would reach it out of turn. A release this listener hands
    // on itself is let through.
    const onRootKeyUp = (event) => {
        lookSettled();
        if (imeKeyTold) {
            imeKeyTold = false;
            grantEditorFocus();
        }
        if (!core || replaying || event.key !== 'Alt' || !declared.has('Alt+Alt')) {
            return;
        }
        const pending = altPending;
        altPending = null;
        const alone = pending?.alone === true;
        if (answering) {
            event.stopPropagation();
            event.preventDefault();
            // With its press, where that is held too: its turn decides whether it was alone (releasesAlone).
            held.push({ altUp: true, altAlone: alone, of: pending?.entry ?? undefined, key: 'Alt' });
            return;
        }
        if (!alone) {
            event.stopPropagation();
            return;
        }
        startHold(altReleaseAnswered());
    };
    root.addEventListener('compositionstart', onKeyFieldCompositionStart, true);
    root.addEventListener('compositionend', onKeyFieldCompositionEnd, true);
    root.addEventListener('keyup', onRootKeyUp, true);

    // The seventh entry's focus listeners (ADR-0021, ADR-0080), on every grid, with a field or not.
    //
    // KB-12: the root's ring says the grid holds the keyboard. A text field matches :focus-visible
    // on every focus, a click's too, so on a grid that edits the root is marked instead
    // (data-ex-focus-visible, which the stylesheet draws as it draws .ex-grid:focus-visible) while
    // its field holds the keyboard that did not come of a press on the grid. The capture-phase
    // mousedown and keydown above say which came last (keyboardByPress), and DOM focus leaving the
    // grid forgets it: Tab back in is the keyboard's. An attribute, which no render of the core
    // touches: the root's class is rewritten whole whenever it changes. Reads no layout.
    let keyboardByPress = false;
    const markFocusVisible = (on) => {
        if (root) {
            root.toggleAttribute('data-ex-focus-visible', on);
        }
    };
    // Focus that lands on the root itself — a press on a part of it that takes no focus of its
    // own, a Consumer's or the scroller's hand-back — goes on to the Keyboard Field at once, before
    // the next key: Blazor's own focus would land a round trip later on a circuit. One of the five
    // decisions about focus made in script (ADR-0021). Heard in the capture phase, where the
    // field's own focus, which does not bubble, is heard too.
    const onFocused = (event) => {
        const field = keyFieldOf();
        if (field === null) {
            return;
        }
        if (event.target === root) {
            field.focus({ preventScroll: true });
        } else if (event.target === field) {
            markFocusVisible(!keyboardByPress);
        }
    };
    // The Keyboard Field left: emptied and unseen again, and the root's ring goes with it. DOM
    // focus leaving the grid — for another grid, a control of the page's, or nothing — ends the
    // release of Tab (ADR-0012, ADR-0080): Escape, a press elsewhere, then Tab back into the grid,
    // and Tab cycles inside the selection again. Counted, so a release answered for an Escape the
    // departure came after is not granted (releaseTab). The window losing focus is not a
    // departure: the browser says so with no next holder too, but DOM focus stays where it is,
    // still the document's active element while the event is told, and comes back there.
    //
    // Whichever it is, a key the IME took that is still being told ends here (imeKeyTold): its
    // keyup, if it comes, is told wherever the keyboard went — on a window switch, to another
    // window — and a request for the editor that waited for it is decided now. Decided against
    // where the keyboard is going, `next`, since DOM focus is on nothing while a move is told: a
    // control outside the grid that the user moved to keeps the keyboard and the request is
    // declined, as it would be once there; on a window switch DOM focus is still the field's, and
    // the editor takes the keyboard back there.
    const onFocusLeft = (event) => {
        if (isKeyField(event.target)) {
            settleKeyField();
            markFocusVisible(false);
        }
        const next = event.relatedTarget;
        // Read before the grant below, which moves DOM focus.
        const stays = event.target === document.activeElement || (next instanceof Node && root && root.contains(next));
        if (imeKeyTold) {
            imeKeyTold = false;
            grantEditorFocus(next);
        }
        if (stays) {
            return;
        }
        tabReleased = false;
        releaseEndsHeard++;
        keyboardByPress = false;
        // An Alt still down when the keyboard left is not released here (altPending).
        altPending = null;
        // The keyboard has left: the keys typed from now are wherever it went (keysOut).
        keysOut = false;
    };
    root.addEventListener('focus', onFocused, true);
    root.addEventListener('focusout', onFocusLeft, true);

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
    //
    // With an edit open in the cell the press is held the same way. It moves the edit into the
    // bar in Caret (ED-29), a change of mode too; and on a circuit the bar's text is a round trip
    // behind the typing in the cell. A key typed into the bar before the core's answer went into
    // that older text, and the render of the cell's last input then wrote the cell's text over
    // it: the keys were gone from the page, while the core held the older text with them, which
    // Enter would have committed (found on CI, the Server host, 2026-10-01). Held, they are typed
    // once the answer is in, into the text the answer left.
    const holdsBarPress = (event) => {
        const target = event.target;
        return !!core && !replaying && event.button === 0
            && (target instanceof HTMLInputElement || target instanceof HTMLTextAreaElement)
            && !target.readOnly && !target.disabled && document.activeElement !== target
            && target.closest('.ex-formula-bar-text') !== null && root.contains(target);
    };
    // The press into the bar keeps the text it was made on, and the caret it placed there, read at
    // its release (barPressTaken), while the field still holds that text (ticket 202, decided with
    // the user on 2026-10-16). On a circuit the renders of the inputs before it — Enter's move, a
    // press on the cell — can land after it, and each that writes the bar's text puts the browser's
    // caret at the end: F4 then cycled the last Reference, not the one pressed. At its turn, where
    // the bar holds the text the press was made on, the caret goes back where it was pressed, and
    // is the user's move (noteCaretMove), as a press made once those renders had landed would have
    // left it. Where the text is another, the press placed no caret in it, and nothing is restored.
    // Reads the field's value and selection; no layout is read.
    let barPressTaken = null;
    const holdBehindBarPress = (field) => {
        const entry = { barPress: true, field, text: field.value, selection: null };
        barPressTaken = entry;
        held.push(entry);
        if (!answering) {
            startHold();
        }
    };
    // The release of a press into the bar held among the keys: the caret its press placed, if the
    // field still holds the text it was pressed on (holdBehindBarPress).
    const noteBarPressCaret = () => {
        const entry = barPressTaken;
        barPressTaken = null;
        if (entry !== null && document.activeElement === entry.field && entry.field.value === entry.text) {
            entry.selection = [entry.field.selectionStart, entry.field.selectionEnd, entry.field.selectionDirection];
        }
    };
    // At the press's turn (answerBarPress): the caret it placed, back where it was, in the text it
    // was pressed on.
    const restoreBarPressCaret = (k) => {
        const field = k.field;
        if (!k.selection || !field || document.activeElement !== field || field.value !== k.text) {
            return;
        }
        const [start, end, direction] = k.selection;
        if (field.selectionStart !== start || field.selectionEnd !== end) {
            field.setSelectionRange(start, end, direction ?? 'none');
        }
        noteCaretMove(field);
        if (reportCaret && editing !== 'none' && core) {
            reportedText = null;
            reportCaretOf(field);
        }
    };
    // A press into the Name Box behind an input still unanswered — a press on the rows, which nothing
    // holds while no edit is open — takes its place among the held keys, as the bar's does (ADR-0230
    // section 3; ticket 222). The press keeps its default, which gives the Name Box the keyboard, and
    // the keys typed into it are held behind the input. Typed at once, they went into the box before
    // the render answering the row press renamed it, and that render wrote the new address over them;
    // the first key re-selected the text only where the render came before it (nameBoxSelected). At
    // its turn, every input sent before it is answered and its render, the box's new name with it, is
    // in the DOM (data-ex-answered): the box's whole text is selected (ticket 78), and the keys held
    // behind it are typed over it. The outcome is the one a press made a moment later gives. Reads
    // attributes and the field's value; no layout is read.
    const holdsNameBoxPress = (event) => {
        const target = event.target;
        return !!core && !replaying && event.button === 0 && ownNameBox(target) !== null
            && document.activeElement !== target
            && (answering || held.length > 0 || unanswered());
    };
    const unanswered = () => stamp !== null && Number(stamp.getAttribute('data-ex-answered')) < inputsSent;
    const holdBehindNameBoxPress = (field) => {
        held.push({ nameBoxPress: true, field, upTo: inputsSent });
        if (answering) {
            holdStartedAt = performance.now();
        } else {
            startHold();
        }
    };
    // Every input sent up to `upTo` answered, and its render in the DOM: seen by the Settled Mark's
    // own look at the stamp (answeredWaits), which observes it while any input is unanswered.
    const answeredUpTo = (upTo) => new Promise((resolve) => {
        answeredWaits.push({ upTo, resolve });
        lookSettled();
    });
    // At its turn: the Name Box's whole text selected, if it holds the keyboard still.
    const answerNameBoxPress = async (k) => {
        await answeredUpTo(k.upTo);
        if (document.activeElement === k.field && k.field.isConnected) {
            k.field.select();
            nameBoxSelected = k.field;
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
    const answerBarPress = async (k) => {
        await new Promise((resolve) => setTimeout(resolve));
        const active = document.activeElement;
        const inBar = active instanceof Element && root.contains(active)
            && active.closest('.ex-formula-bar-text') !== null;
        await core.invokeMethodAsync('BarPressAnsweredAsync', inBar, nextInput()).catch((error) => {
            if (core) {
                console.error('[ex-grid] the grid failed to answer a press', error);
            }
        });
        await editorSettled();
        restoreBarPressCaret(k);
    };

    // A press on the rows keeps its place among held keys (ADR-0021/0010, ED-22). While keys
    // are held, or a change of editing mode is being answered, a primary-button press on the
    // rows is held too, in order, and its release with it: a click straight after Enter
    // reached C# before the held Enter, and the Enter's move carried the Focus past the
    // clicked cell. When nothing is held the press passes through untouched, and while an edit
    // is open the keys after it wait for its answer (holdBehindPress). No layout is read.
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
    // An action's button on this grid's own rows, not a nested grid's (ADR-0020), or null.
    const ownAction = (target) => (inOwnScroller(target) ? target.closest('.ex-action') : null);
    // A press on an action carries the render its row was painted by (ADR-0142, LV-12), so the core
    // finds the row component that painted the button, and its address in that render: the paint the
    // Viewport named at the press, and the row and column the button's cell id names and which of
    // the cell's actions it is, all read together at the mousedown. A keyed row can move the same
    // button between the press and the release (ADR-0140): ids read at the release would be the row's
    // new place, which in the old paint names another row. They are told to the core at the release
    // on the same button — the click that fires the action follows that release, and Blazor
    // dispatches it after this message, so the press the core hears next is the one it was told of.
    // A release elsewhere is no click, and tells nothing. Nor does a press the platform makes a
    // context menu of instead of a click — Control with the primary button where Meta is the primary
    // modifier (macOS): no click follows it, and told, it would wait for one until a render disposed
    // its row, then act for a click nobody made (principle 1). Reads attributes and ids the render
    // wrote, as takenAt does; nothing is measured.
    let actionPress = null;
    // A press on one of this grid's own marks (ADR-0043) — a row's checkbox, the header's, or "Mark all
    // N rows" on its status line — carries the render it was made on, as a press on an action does
    // (ADR-0142, MK-9): the paint the Viewport named at the mousedown; for the header's under a pager,
    // the first row of the page it named and how many rows that page holds (data-ex-page,
    // data-ex-page-rows); and for a row's checkbox, the row its own cell's id names — read together then.
    // They are told to the core at the release on the same mark, before Blazor dispatches the click that
    // follows it. The core judges the click against them: it refuses a press made on what a Source since
    // replaced painted, or one naming rows by position under an order since moved, and lines up the page
    // a press named though the page has turned, or the pager gone, since. The row lets it
    // find the row component that painted the checkbox, and answer a press whose click Blazor will not
    // deliver, as it does an action's. A release elsewhere is no click, and tells nothing, nor does a
    // press the platform makes a context menu of. Reads attributes and ids the render wrote, as takenAt
    // does; nothing is measured.
    let markPress = null;
    // One of this grid's own marks, not a nested grid's: the nearest root above it is this one.
    const ownMark = (target) => {
        const mark = target instanceof Element ? target.closest('.ex-mark, .ex-mark-result') : null;
        return mark !== null && root !== null && mark.closest('.ex-grid') === root ? mark : null;
    };
    const isOwnRowsOrHeadings = (target) => isOwnRows(target)
        || (inOwnScroller(target) && target.closest('.ex-header') !== null);
    // What a press or release on this grid's own rows was taken against (ED-31; ADR-0021, note of
    // 2026-10-02): the offsets the browser gave it, the first row the Viewport painted, the
    // scroll on both axes, and the row order and the columns and row height the painting render
    // carried (data-ex-sequence, data-ex-layout). A replayed event is measured again, against the
    // rows painted at the replay, so a held press replayed after a key had moved the view landed on
    // the row the move brought there (`1` Enter PageDown `9` and a press on F6 at once, on the
    // Server host: the `2` typed next went into F18). The core is told this just before Blazor
    // dispatches the event — at once, or at the replay of a held one — and resolves the cell
    // against it. Reads attributes and the scroll offset; nothing is measured.
    const takenAt = (event) => {
        const viewport = event.target;
        const number = (name) => {
            const value = viewport.getAttribute(name);
            return value === null ? -1 : Number(value);
        };
        return {
            x: event.offsetX, y: event.offsetY, first: number('data-ex-first-row'),
            left: scroller ? scroller.scrollLeft : 0,
            // Where a fill handle held at the readable area's top stands (ADR-0050, item 5).
            top: scroller ? scroller.scrollTop : 0,
            sequence: number('data-ex-sequence'), layout: number('data-ex-layout'),
        };
    };
    // This grid's own Cell Editor — the built-in input, or the box a Chrome's control stands in — on
    // its rows, not the Formula Bar's surface, nor a nested grid's editor, or null.
    const ownCellEditor = (target) => {
        const box = inOwnScroller(target) ? target.closest('.ex-editor') : null;
        return box !== null && box.hasAttribute('data-ex-cell') ? box : null;
    };
    // A press or release on the Cell Editor while the keys before it are held (onPress), taken against
    // the rows the editor stands over (ticket 201): the cell its box stands on, which the core wrote on
    // it (data-ex-cell) — the box is the cell's own (ADR-0028) — the press's offsets within it, and the
    // Viewport's first row, scroll, order and layout, as takenAt has them. If the edit has ended by its
    // turn, the press is a press on that cell, as it would have been with the editor gone; the core
    // places it there (ExGrid.PressTakenAt). Reads attributes and the scroll offset; nothing is measured.
    const editorTakenAt = (event, box) => {
        const viewport = scroller ? scroller.querySelector('.ex-viewport') : null;
        const cell = /^(\d+) (\d+)$/.exec(box.getAttribute('data-ex-cell') ?? '');
        if (!viewport || !cell) {
            return null;
        }
        const number = (name) => {
            const value = viewport.getAttribute(name);
            return value === null ? -1 : Number(value);
        };
        return {
            x: event.offsetX, y: event.offsetY, first: number('data-ex-first-row'),
            left: scroller.scrollLeft, top: scroller.scrollTop,
            sequence: number('data-ex-sequence'), layout: number('data-ex-layout'),
            editorRow: Number(cell[1]), editorColumn: Number(cell[2]),
        };
    };
    const tellTaken = (kind, taken) => {
        if (!core || !taken) {
            return;
        }
        // Numbered as it goes (nextInput): the press, and the double click its release ends, which the
        // browser fires next and the core answers in its turn (ADR-0220).
        core.invokeMethodAsync('PressTakenAt', kind, taken.x, taken.y, taken.first, taken.left, taken.top,
            taken.sequence, taken.layout, taken.editorRow ?? -1, taken.editorColumn ?? -1, nextInput(),
            taken.doubleClick === true ? nextInput() : 0).catch((error) => {
            if (core) {
                console.error('[ex-grid] the grid failed to hear where a press was taken', error);
            }
        });
    };

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
            ? core.invokeMethodAsync('PressAnsweredAsync', nextInput()).catch((error) => {
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
        lookSettled();
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
        lookSettled();
        // A press while the Keyboard Field composes (ADR-0080): the press would end the
        // composition by its default, after the core had heard the press, and the composed text
        // would open an edit wherever the press had moved the Focus. So the composition ends
        // here first — the field gives up the keyboard, and the IME ends it there — and its text
        // is held ahead of the press, which is then held behind it as behind any key. One of the
        // six decisions about focus made in script (ADR-0021).
        if (keyFieldComposing && core && !replaying && event.button === 0) {
            keyFieldOf()?.blur();
        }
        // A press inside one of this grid's popovers keeps the keyboard there from its opening focus
        // (openingOvertaken).
        if (!replaying && event.target instanceof Element) {
            const pressedIn = event.target.closest('.ex-popover');
            if (pressedIn && root.contains(pressedIn)) {
                pressedPopovers.add(pressedIn);
            }
        }
        // A press on the grid ends a release of Tab: the user has come back to it (ADR-0012,
        // 2026-10-01). A held press ends it again at its replay, after the answer it waited for.
        // And the keyboard this press brings to the Keyboard Field shows no ring (KB-12).
        // A press the core hears without a number of its own counts as the next input's (layerInputAt).
        const ended = tabReleased;
        tabReleased = false;
        if (!replaying || ended) {
            releaseEndsHeard++;
        }
        if (!replaying) {
            layerInputAt = inputsSent + 1;
            keyboardByPress = true;
            // A press between an Alt and its release: the release starts nothing here (altPending).
            altPending = null;
            // A press on the grid takes the keyboard back into it (keysOut).
            keysOut = false;
        }
        // A press into a field beside the rows gives that field a focus of its own, which a
        // late hand-back leaves alone (reclaimFocus).
        if (event.target instanceof Element && event.target.closest('.ex-formula-bar') !== null) {
            staleField = null;
        }
        // A press that gives the Name Box the keyboard has its text selected at its release
        // (nameBoxPressed).
        const nameBox = event.button === 0 && !replaying ? ownNameBox(event.target) : null;
        nameBoxPressed = nameBox !== document.activeElement ? nameBox : null;
        nameBoxSelected = null;
        // A press on an action of this grid's rows keeps the render it was made on and its address
        // in that render (actionPress); one the platform makes a context menu of keeps nothing.
        if (!replaying) {
            const contextPress = metaIsPrimary && event.ctrlKey;
            const button = event.button === 0 && !contextPress ? ownAction(event.target) : null;
            const cell = button !== null ? button.closest('[role=gridcell]') : null;
            const at = cell !== null ? /r(\d+)c(\d+)$/.exec(cell.id) : null;
            actionPress = button !== null ? {
                button,
                paint: paintNow(),
                row: at ? Number(at[1]) : -1,
                column: at ? Number(at[2]) : -1,
                index: cell !== null ? Array.prototype.indexOf.call(cell.querySelectorAll('.ex-action'), button) : -1,
            } : null;
            // And a press on one of its marks, the render it was made on, the page it named — its first row
            // and how many rows it holds — and, for a row's checkbox, the row its cell's id names (markPress):
            // the cell inside this root, for the header's checkbox and "Mark all N rows" stand in none, though
            // a grid nested in a cell stands in one.
            const mark = event.button === 0 && !contextPress ? ownMark(event.target) : null;
            const page = mark !== null ? mark.getAttribute('data-ex-page') : null;
            const pageRows = mark !== null ? mark.getAttribute('data-ex-page-rows') : null;
            const markCell = mark !== null ? mark.closest('[role=gridcell]') : null;
            const markAt = markCell !== null && root.contains(markCell) ? /r(\d+)c(\d+)$/.exec(markCell.id) : null;
            markPress = mark !== null ? {
                mark,
                paint: paintNow(),
                page: page === null ? -1 : Number(page),
                pageRows: pageRows === null ? -1 : Number(pageRows),
                row: markAt ? Number(markAt[1]) : -1,
            } : null;
        }
        // A press into an editor surface puts the keyboard there.
        noteSurface(event.target);
        // A press in an editor surface's text puts the caret where it lands: the user's move.
        if (event.button === 0 && !replaying && isTextField(event.target) && event.target.closest('.ex-editor') !== null) {
            noteCaretMove(event.target);
        }
        // Any other press leaves the bar press before it with the caret it had (noteBarPressCaret).
        if (!replaying) {
            barPressTaken = null;
        }
        if (holdsBarPress(event)) {
            holdBehindBarPress(event.target);
            return;
        }
        if (holdsNameBoxPress(event)) {
            holdBehindNameBoxPress(event.target);
            return;
        }
        // An edit is left standing here when DOM focus goes elsewhere — to another grid, a
        // control of the page's, or nothing (ADR-0018, section 6). A press on this grid's rows
        // or headings puts the keyboard back into the surface that last held it, now, before
        // anything hears the press. The press then points, or commits and moves, exactly as if
        // the keyboard had never left: where the core keeps DOM focus through a press on the rows
        // (ADR-0051) it keeps it in the edit, and the hand-back after a commit finds it inside
        // this root. Handed back from C#, a round trip later, the keys typed in between would
        // reach the grid the user had just left. This is one of the six decisions about focus
        // made in script (ADR-0021, added 2026-09-29), beside reclaimFocus, focusEditor, the
        // root's own focus passed on to its Keyboard Field (onFocused), the field given up by a
        // press during a composition (above; ADR-0080) and the keyboard handed on to a control of
        // the Consumer's (handKeyboardTo); it reads document.activeElement and no layout. Held or
        // not, the press keeps its place among the keys: only where the keyboard is has changed.
        const focusAtPress = document.activeElement;
        if (core && !replaying && editing !== 'none' && !(focusAtPress instanceof Element && root.contains(focusAtPress))
            && isOwnRowsOrHeadings(event.target)) {
            standingField()?.focus({ preventScroll: true });
            focusDeclined = false;
        }
        // A press on one of the menu's items runs its command and closes the menu, as Enter on it
        // does (handsOver): a change of who holds the keyboard, and the keys typed after it wait
        // until the keyboard has followed — to the root, or to what the command opened (handOff;
        // ADR-0039, 2026-10-01). So does a press on one choice among others (menuitemradio), which
        // makes it and closes its list: the keyboard is in flight back from a closing popover
        // (ADR-0044's note of 2026-10-12, decided with the user). And a press on a button that closes
        // its popover, a panel's OK, Apply or Cancel (ex-popover-closes; handsOver).
        if (core && !replaying && event.button === 0 && event.target instanceof Element) {
            const item = event.target.closest('[role=menuitem], [role=menuitemradio], .ex-popover-closes');
            const menu = item ? popoverOf(item) : null;
            if (item && menu && !item.matches(':disabled, [aria-disabled=true]')) {
                awaitingMove = { from: item, closes: menu };
                if (answering) {
                    holdStartedAt = performance.now();
                } else {
                    startHold();
                }
            }
        }
        // A press on this grid's Cell Editor is the editor's own: it places the caret. Made while keys
        // are held, or a key is being answered, it keeps its place among them (ADR-0010), for the editor
        // may be gone by its turn: Enter typed into an edit and a press on the same cell at once, and on
        // a circuit the press met the editor the Enter was committing, and was lost (ticket 201). Its
        // default stands, which only places a caret in the editor that holds the keyboard; at its turn
        // it is the editor's still if the edit is, and otherwise a press on the cell the editor stood
        // over (replayPress). One made on an editor that stands with no edit open — the render removing
        // it not yet painted — is that press on the rows, held for its turn, and its default, which
        // would put the keyboard in a field about to go, is suppressed.
        const onEditor = core && !replaying && event.button === 0 ? ownCellEditor(event.target) : null;
        if (onEditor !== null && (answering || held.length > 0 || editing === 'none')) {
            if (editing === 'none') {
                event.preventDefault();
                event.stopPropagation();
            }
            held.push({
                press: 'mousedown', target: onEditor, init: mouseInit(event), taken: editorTakenAt(event, onEditor),
                onEditor: true,
            });
            if (!answering) {
                startHold();
            }
            return;
        }
        // Only a press on this grid's own rows is held or holds the keys after it: one on a
        // nested grid's rows is that grid's to answer, and this core never hears it. A press that
        // goes on to Blazor from here, or is replayed, is told to the grid that points, if this
        // one is pointed at (handOn).
        // What the press was taken against, told as it goes on to Blazor (takenAt).
        const taken = core && !replaying && isOwnRows(event.target) ? takenAt(event) : null;
        if (!core || replaying || event.button !== 0 || !isOwnRows(event.target)) {
            handOn(event);
            tellTaken('mousedown', taken);
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
            tellTaken('mousedown', taken);
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
        held.push({ press: 'mousedown', target: event.target, init: mouseInit(event), taken });
    };
    // A release is held only behind its press: once the press has been handed on, the
    // release follows it to Blazor as it comes, and Blazor keeps the two in order.
    const onRelease = (event) => {
        lookSettled();
        // A passed-on press the keys wait behind is asked about now, ahead of this release
        // (holdBehindPress).
        if (!replaying) {
            askAboutPress();
            noteBarPressCaret();
        }
        // The click this release makes on an action is told the render it was pressed on, and the
        // address the press read in it (actionPress).
        const action = replaying ? null : actionPress;
        if (!replaying) {
            actionPress = null;
        }
        if (core && action !== null && event.button === 0 && ownAction(event.target) === action.button) {
            // What it pressed — the row and column its cell's id named at the press, and which of the
            // cell's actions — so that the core can answer a press whose click Blazor will not
            // deliver: one whose row component a render the browser has not seen yet has disposed
            // (ADR-0142, 2026-10-06). Read at the press, not now: a keyed row's button may stand
            // elsewhere by now, and its ids would name another row in the paint it was pressed on.
            core.invokeMethodAsync('ActionPressTakenAt', action.paint, action.row, action.column, action.index).catch((error) => {
                if (core) {
                    console.error('[ex-grid] the grid failed to hear where an action was pressed', error);
                }
            });
        }
        // The click this release makes on a mark is told the render it was pressed on, and the page, its
        // rows and the row the press read in it (markPress).
        const mark = replaying ? null : markPress;
        if (!replaying) {
            markPress = null;
        }
        if (core && mark !== null && event.button === 0 && ownMark(event.target) === mark.mark) {
            core.invokeMethodAsync('MarkPressTakenAt', mark.paint, mark.page, mark.row, mark.pageRows).catch((error) => {
                if (core) {
                    console.error('[ex-grid] the grid failed to hear where a mark was pressed', error);
                }
            });
        }
        // The press gave the Name Box the keyboard: its whole text is selected now, and stays so
        // (nameBoxPressed).
        const nameBox = nameBoxPressed;
        nameBoxPressed = null;
        if (nameBox !== null && event.button === 0 && !replaying && document.activeElement === nameBox) {
            event.preventDefault();
            nameBox.select();
            nameBoxSelected = nameBox;
        }
        const taken = core && !replaying && isOwnRows(event.target) ? takenAt(event) : null;
        if (!core || replaying || !held.some((k) => k.press === 'mousedown')) {
            // A release that ends a double click: the browser fires the double click on the rows
            // next, and its answer is awaited as an input of its own (ADR-0220). A release held and
            // replayed is not one: the browser's double click has gone ahead of it.
            if (taken && !replaying && event.button === 0 && event.detail === 2) {
                taken.doubleClick = true;
            }
            tellTaken('mouseup', taken);
            return;
        }
        event.stopPropagation();
        // A release on the Cell Editor goes as its press does (replayPress): taken against the cell the
        // editor stands over.
        const onEditor = ownCellEditor(event.target);
        if (onEditor !== null) {
            held.push({
                press: 'mouseup', target: onEditor, init: mouseInit(event), taken: editorTakenAt(event, onEditor),
                onEditor: true,
            });
            return;
        }
        held.push({ press: 'mouseup', target: event.target, init: mouseInit(event), taken });
    };
    // A held press or release, handed to Blazor as the event it was, in its place: on the
    // element it landed on, or on the Viewport if a render has replaced that one, with the core
    // told first what it was taken against (takenAt): its own offsets and rows, not the replay's.
    // The keys held behind it wait until the core says it has answered it — a press can commit
    // an open edit and wait on the Consumer hearing it, and those keys must be gated against the
    // mode it leaves (ExGrid.PressAnsweredAsync). The listener asks straight after dispatching, so
    // the core has always heard the press first; the answer is the core's, never a guess.
    const replayPress = async (k) => {
        // A press held on the Cell Editor (onPress): the editor's own while its edit stands, whose
        // default has placed the caret already; once the edit has ended, a press on the rows over the
        // cell the editor stood on, which the core places from what it was taken against.
        if (k.onEditor && k.target.isConnected && editing !== 'none') {
            return;
        }
        if (k.onEditor && !k.taken) {
            return;
        }
        const viewport = scroller ? scroller.querySelector('.ex-viewport') : null;
        const target = k.onEditor ? viewport : (k.target.isConnected ? k.target : root.querySelector('.ex-viewport'));
        if (!target) {
            return;
        }
        tellTaken(k.press, k.taken);
        replaying = true;
        try {
            target.dispatchEvent(new MouseEvent(k.press, k.init));
        } finally {
            replaying = false;
        }
        await core.invokeMethodAsync('PressAnsweredAsync', nextInput()).catch((error) => {
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
    const buildCopy = (withHeaders) => core.invokeMethodAsync('BuildCopyPayloadAsync', withHeaders, nextInput())
        .catch((error) => {
            // A .NET failure — a Consumer delegate that threw, a circuit that
            // dropped. Reported here, because the null it becomes reads as a
            // refusal below and would otherwise make the copy a silent no-op.
            if (core) {
                console.error('[ex-grid] the grid failed to build a copy', error);
            }
            return null;
        });
    const writeAsync = (withHeaders) => writeCopy(buildCopy(withHeaders));
    // The write of a copy whose payload is `answer`: a null one is a refusal, and lands nothing.
    // A payload is counted as the grid's own write the moment it is in hand, before the browser
    // writes it, so the clipboardchange it causes finds it counted; a write that then fails gives
    // its count back (ADR-0170).
    const writeCopy = (answer) => {
        // In flight until written or refused, the copy holds the Settled Mark (showSettled).
        clipboardWrites += 1;
        lookSettled();
        let counted = false;
        const payload = answer.then((p) => {
            if (p && clipboardWatched) {
                ownWrites += 1;
                counted = true;
            }
            return p;
        });
        const flavour = (type, field) => payload.then((p) => {
            if (!p) {
                throw new Error('the copy was refused');
            }
            return new Blob([p[field]], { type });
        });
        return navigator.clipboard.write([new ClipboardItem({
            'text/plain': flavour('text/plain', 'text'),
            'text/html': flavour('text/html', 'html'),
        })]).then(() => payload.then(copyLanded), (error) => {
            if (counted) {
                ownWrites -= 1;
            }
            // A refusal from the core: nothing landed, which is the refusing grid's
            // contract (ADR-0005), and the reason has already been raised (OnCopyRefused
            // on the C# side, or the console line above). Anything else is a failure and
            // is said so.
            if (error instanceof Error && error.message === 'the copy was refused') {
                return;
            }
            copyNotWritten(error);
        }).finally(() => {
            clipboardWrites -= 1;
            lookSettled();
        });
    };
    // The Copied Range (ADR-0170): the grid outlines what it copied while the clipboard still
    // holds it, and only a browser that says when the clipboard changed lets it know that. Without
    // clipboardchange no landing is reported, so no outline is drawn. Each instance counts the
    // writes of its own copy that the event has yet to report: an event that finds one counted is
    // that write, and any other is a change — another application's, another grid's, a text
    // field's — which drops the outline. A change crosses to .NET only while there may be an
    // outline to drop. The listener is the clipboard entry of ADR-0021, not a new one.
    const clipboardWatched = typeof globalThis.ClipboardChangeEvent === 'function'
        && typeof navigator.clipboard?.addEventListener === 'function';
    let ownWrites = 0;
    let outlined = false;
    const copyLanded = (payload) => {
        if (!clipboardWatched || !core || !payload || !payload.landing) {
            return;
        }
        outlined = true;
        core.invokeMethodAsync('OnCopyLandedAsync', payload.landing, nextInput()).catch((error) => {
            if (core) {
                console.error('[ex-grid] the grid failed to outline a copy', error);
            }
        });
    };
    const onClipboardChange = () => {
        if (ownWrites > 0) {
            ownWrites -= 1;
            return;
        }
        if (!outlined || !core) {
            return;
        }
        outlined = false;
        core.invokeMethodAsync('OnClipboardChangedAsync').catch((error) => {
            if (core) {
                console.error('[ex-grid] the grid failed to drop a copy\'s outline', error);
            }
        });
    };
    // A write the browser would not make. Nothing landed — but no reason has been raised
    // yet, and a user told nothing pastes the old clipboard believing it is the copy. It is
    // a Refusal of its own (ADR-0005).
    const copyNotWritten = (error) => {
        if (error instanceof DOMException && error.name === 'NotAllowedError') {
            if (core) {
                core.invokeMethodAsync('OnCopyWriteRejectedAsync', nextInput()).catch((reportError) => {
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
    };
    const onCopy = (event) => {
        lookSettled();
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
            // Set here, the write cannot fail: it is the grid's own, and it has landed (ADR-0170).
            if (clipboardWatched) {
                ownWrites += 1;
            }
            copyLanded(payload);
            return;
        }
        // Beyond the Window: the asynchronous route, without headers — Ctrl+C copies
        // the selection and nothing else (ADR-0005).
        writeAsync(false);
    };
    const onPaste = (event) => {
        lookSettled();
        if (!core || !isRoot(event.target)) {
            return;
        }
        // clipboardData is only readable inside the event, so both flavours are read
        // here; everything they mean — shape rules, refusals, the intent — is decided
        // in C# (ADR-0014). preventDefault regardless: pasting into a non-editable
        // element does nothing by default, and must not start doing something later.
        event.preventDefault();
        sendPaste(event.clipboardData.getData('text/plain'), event.clipboardData.getData('text/html'), paintNow());
    };
    // Handed over as streams, never as two strings in one call (ADR-0005): on a
    // Blazor Server circuit that call is one hub message, and a message past the
    // hub's receive limit — 32 KB unless the application raised it — closes the
    // connection. Excel's HTML for a few hundred cells is past it. A stream is
    // Blazor's own route for large interop data and is not subject to that limit;
    // its length travels with it, so C# can refuse a paste past the grid's ceiling
    // without reading a byte. An empty flavour is sent as nothing at all. With them goes the
    // render the paste was taken against, read at its event (paintNow): the paste is refused when
    // the order that render was painted under has moved since (ADR-0142, LV-13/LV-14).
    const sendPaste = (plain, markup, paint) => {
        const encoder = new TextEncoder();
        const stream = (value) => (value
            ? DotNet.createJSStreamReference(encoder.encode(value))
            : null);
        return core.invokeMethodAsync('OnPasteStreamsAsync', stream(plain), stream(markup), paint, nextInput())
            .catch((error) => {
                if (core) {
                    console.error('[ex-grid] the grid failed to take a paste', error);
                }
            });
    };

    // A copy, cut or paste fired by a clipboard key let through a hold (onKeyDown), taken in the
    // key's place among the held keys and done at its turn (takeClipboard; ADR-0010, 2026-10-02).
    // Taken before any other listener hears it, and before the default of a field that holds the
    // keyboard: done now, it would overtake the keys typed before it — a paste into a field ahead
    // of the text it follows, a copy of the Selection a held press is about to move.
    const onHeldClipboard = (event) => {
        lookSettled();
        if (!clipboardKeyHeld || !core) {
            return;
        }
        clipboardKeyHeld = false;
        event.preventDefault();
        event.stopPropagation();
        if (event.type === 'paste') {
            held.push({
                clipboard: 'paste',
                plain: event.clipboardData.getData('text/plain'),
                markup: event.clipboardData.getData('text/html'),
                paint: paintNow(),
            });
        } else if (event.type === 'copy') {
            // The write started inside the event, as the browser lets a copy write
            // (writeAsync), and built at its turn: the grid's copy of the Selection then, or
            // nothing at all.
            let take = null;
            const turn = new Promise((resolve) => {
                take = resolve;
            });
            const answer = turn.then((grids) => (grids ? buildCopy(false) : null));
            writeCopy(answer);
            held.push({ clipboard: 'copy', take, answer });
        } else {
            held.push({ clipboard: 'cut' });
        }
        if (!answering) {
            startHold();
        }
    };

    // A held copy, cut or paste at its turn, done where the keyboard is then, as the browser
    // would have done it there. With the keyboard the grid's, the grid's copy of the Selection or
    // its paste, and the keys after it wait for the core's answer; no cut, which the grid does
    // not make unheld either. In a text field — a popover's, the Name Box, the Formula Bar, the
    // Cell Editor — the field's own: its selection copied as plain text, cut, or replaced by what
    // is pasted, with each line break a space in a one-line field, as Chromium pastes it there.
    // On any other control, nothing, as there.
    const takeClipboard = async (k) => {
        // Where the keyboard is: a control, the open edit's surface, or with no edit open the
        // Formula Bar a press has given it — a read-only one, over a cell that is not Editable,
        // where a paste does nothing. Only with none of these is it the grid's.
        const active = document.activeElement;
        let field = focusedControl() ?? (deliveringOutside ? outsideControl() : null);
        if (field === null && editing !== 'none') {
            field = editorInput();
        } else if (field === null && active instanceof Element && root.contains(active) && !isRoot(active)) {
            field = active;
        }
        if (field === null && editing === 'none') {
            if (k.clipboard === 'copy') {
                k.take(true);
                await k.answer;
            } else if (k.clipboard === 'paste') {
                await sendPaste(k.plain, k.markup, k.paint);
            }
            return;
        }
        if (k.clipboard === 'copy') {
            k.take(false);
        }
        const writable = field !== null && isTextField(field);
        const readable = writable || (field instanceof HTMLInputElement && field.type === 'text')
            || field instanceof HTMLTextAreaElement;
        if (!readable || (k.clipboard !== 'copy' && !writable)) {
            return;
        }
        if (k.clipboard === 'paste') {
            // Typed at the caret as a held key's text is (typeInto). No text, no change.
            if (k.plain === '') {
                return;
            }
            typeInto(field, { text: field instanceof HTMLInputElement ? k.plain.replace(/\r\n|\r|\n/g, ' ') : k.plain });
            return;
        }
        const start = field.selectionStart ?? field.value.length;
        const end = field.selectionEnd ?? start;
        if (start === end || field.type === 'password') {
            return;
        }
        await navigator.clipboard.writeText(field.value.slice(start, end)).catch(copyNotWritten);
        if (k.clipboard === 'cut') {
            // The selection deleted as a held Delete deletes it (typeInto).
            typeInto(field, { key: 'Delete' });
        }
    };
    root.addEventListener('copy', onCopy);
    root.addEventListener('paste', onPaste);
    if (clipboardWatched) {
        navigator.clipboard.addEventListener('clipboardchange', onClipboardChange);
    }
    root.addEventListener('copy', onHeldClipboard, true);
    root.addEventListener('cut', onHeldClipboard, true);
    root.addEventListener('paste', onHeldClipboard, true);

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
    //
    // Nor may C# write to it inside the callback, and on WebAssembly it would: there
    // invokeMethodAsync runs the .NET method at once, on this thread, and a render it asks
    // for is applied to the DOM before the call returns. Told from inside the callback, the
    // render that answered the first report — the gutter's strip appearing — moved the
    // scroller's content box again within the same observation, and the browser reported
    // the loop on window (CON-4: /density at attach, 2026-10-13; the Server host, whose call
    // crosses the circuit, never did). So each observer tells C# from a task of its own,
    // queued once however many notifications come before it runs and carrying the latest
    // values: what is told does not depend on when it runs, only that it is outside the
    // observation. ADR-0013 accepts the frame this costs.
    const toldAfterTheObservation = (tell) => {
        let queued = false;
        return () => {
            if (queued) {
                return;
            }
            queued = true;
            setTimeout(() => {
                queued = false;
                if (core) {
                    tell();
                }
            }, 0);
        };
    };
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
        tellViewport();
    });
    const tellViewport = toldAfterTheObservation(() => {
        core.invokeMethodAsync('OnViewportReportAsync', gutterWidth, gutterHeight, contentWidth, contentHeight)
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
        tellCeiling();
    });
    const tellCeiling = toldAfterTheObservation(() => {
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

    // The Device Pixel (ADR-0090, the eighth allowlist entry). Lines are drawn in Device
    // Pixels and column edges are put on them, so C# needs devicePixelRatio at every
    // resolution, not only at the stylesheet's steps. It is told, never read on the path to
    // a paint: a media query on the current resolution stops matching when the display
    // scale or the page zoom moves, and only then is the new ratio reported and the query
    // armed again on it. Never per render, and nothing here writes to the DOM.
    let devicePixelRatio = -1;
    let resolutionQuery = null;
    const armResolution = () => {
        resolutionQuery?.removeEventListener('change', armResolution);
        resolutionQuery = null;
        if (!core) {
            return;
        }
        const ratio = window.devicePixelRatio;
        resolutionQuery = window.matchMedia(`(resolution: ${ratio}dppx)`);
        resolutionQuery.addEventListener('change', armResolution);
        if (ratio === devicePixelRatio) {
            return;
        }
        devicePixelRatio = ratio;
        core.invokeMethodAsync('OnDevicePixelAsync', ratio)
            .catch((error) => {
                if (core) {
                    console.error('[ex-grid] the grid failed to take the device pixel', error);
                }
            });
    };
    armResolution();

    // The browser's time zone (ADR-0122, the ninth allowlist entry), read once and only when
    // the Consumer asked: TODAY() is the day on the user's device (ADR-0121), and on Blazor
    // Server .NET runs where the server's zone is. Browsers raise no event when the zone
    // changes, so nothing listens for one. No layout is read and nothing is written.
    if (reportTimeZone === true) {
        let zone = null;
        try {
            zone = Intl.DateTimeFormat().resolvedOptions().timeZone;
        } catch {
            zone = null;
        }
        if (typeof zone === 'string' && zone.length > 0) {
            core.invokeMethodAsync('OnTimeZoneAsync', zone)
                .catch((error) => {
                    if (core) {
                        console.error('[ex-grid] the grid failed to take the time zone', error);
                    }
                });
        }
    }

    const handle = {
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
        // 'caret', 'point', 'completion' (ADR-0051), 'completionOverPoint' (ADR-0058: a list
        // open where ←, →, Home, End and the Shift+arrows point) or 'pointed' (ADR-0058: what
        // Point wrote came from a press on another grid), whether inputs report their caret,
        // and whether F4 is claimed while editing. Set by the core when the mode changes —
        // a mode change is a different set of claimed keys. (A focusable descendant holding
        // the keyboard — ADR-0020's interactive cell — is not a mode: it is read off
        // event.target, which is true whether focus arrived by click or by key.)
        setEditing: (mode, reportsCaret, cyclesReferences, version) => {
            const opened = editing === 'none' && mode !== 'none';
            editing = mode;
            editingVersion = Number(version) || 0;
            reportCaret = reportsCaret === true;
            cycleReferences = cyclesReferences === true;
            if (mode === 'none') {
                caretMoved = null;
                surfaceComposing = false;
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
            // Read first: a move made in text the field no longer holds is forgotten now, before the
            // field can come back to that text (movedByUser).
            const moved = input ? movedByUser(input) : false;
            if (input && input.value === text) {
                if (moved) {
                    reportedText = null;
                    reportCaretOf(input);
                    return;
                }
                // In view: what Point wrote at the end of a long Formula is read as it is written,
                // as is the end of F4's selection there, where the browser's own move would show it
                // (ADR-0058, the thirteenth Windows run; ticket 75).
                showCaret(input, end);
                input.setSelectionRange(caret, end);
                reportedText = text;
                reportedCaret = caret;
            }
        },
        // ST-2's question (closing, above): whether the open edit's fields still hold `text`, the text
        // it opened with, and nothing composes in them. Answered in its turn: while a hold stands,
        // once its keys have gone where they were going, so a key typed before the question counts.
        // "Untouched" starts the hold that keeps the fields as they are until closeAnswered; every
        // input sent before the answer travels ahead of it. Reads the fields' values, never layout.
        askUntouched: (text) => new Promise((resolve) => {
            const ask = () => {
                const fields = core && editing !== 'none' ? editFields() : [];
                const untouched = fields.length > 0 && !surfaceComposing && fields.every((field) => field.value === text);
                if (untouched) {
                    holdForClose(fields);
                }
                resolve(untouched);
            };
            if (answering) {
                questionsAfterHold.push(ask);
            } else {
                ask();
            }
        }),
        // The core has answered an "untouched": the edit closed, or stays (its row came back). The
        // fields are given back what they were, and the held keys go on, against the mode the gate
        // was told before this.
        closeAnswered: () => {
            endCloseHold();
        },
        // Where the keyboard is going (handOff above): to a Consumer's popover the core has
        // opened, or to a frame of the Consumer's own outside this root. Told before the key's
        // answer and before the render that closes the menu a command ran from, so the hold
        // already standing waits for it; with none standing, one starts, for the keys typed on
        // the root until the keyboard arrives. No focus is moved and no layout is read: what
        // holds DOM focus is read, as the hold always has.
        handOff: (to) => {
            // The core closed a Consumer's popover: the keyboard is no longer on its way there,
            // even if its contents had focus while a choice's close still held the keys. Withdraw
            // that destination before the hand-back, rather than wait for focus on contents now gone
            // and drop the held keys. A frame the Consumer handed the keyboard to stays its destination
            // (ADR-0044; ADR-0050 item 16; ADR-0100's CloseRequest).
            if (to === 'none') {
                if (handOff === 'popover') {
                    handOff = null;
                }
                return;
            }
            if (!core || (to !== 'popover' && to !== 'frame')) {
                return;
            }
            handOff = to;
            handOffFrom = to === 'frame' ? outsideControl() : null;
            handOffMissed = false;
            if (answering) {
                holdStartedAt = performance.now();
            } else {
                startHold();
            }
        },
        // The keyboard is leaving this grid for the Consumer's element around it (keysOut; ExGrid's
        // PassKeysOutAsync): told before the answer to the Escape
        // that led here, so the keys held behind that Escape go out too, in order.
        passKeysOut: () => {
            if (!core) {
                return;
            }
            // Every key held now was typed after the Escape that led here: out they go, at once, ahead of any
            // typed from now, which go out as they are typed. Kept until the Escape is answered, they could be
            // overtaken by a key typed meanwhile, or by the keyboard leaving first, which ends this (keysOut).
            keysOut = true;
            // The Consumer has answered where these keys go, even while the Escape's interop
            // answer is pending. Hand them out now, before a later key bubbles out or the
            // receiving grid takes focus and asks the frame for its queue. Waiting for drain
            // lets either overtake them; focus leaving also ends keysOut before drain runs.
            passHeldOut();
        },
        // A popover's contents reported a popup of their own opening or closing
        // (ADR-0039): while one is open, a descendant's Escape is left to it.
        setInnerPopup: (open) => {
            innerPopup = open;
        },
        // Which keys this grid takes, whether any column edits and whether a search is
        // wired — re-told when a parameter change changes the answer, so a grid that
        // becomes display-only stops taking printable keys, and one whose Consumer stops
        // listening for undo gives Ctrl+Z back to the page (ADR-0007/0010/0020/0055) — and
        // which of them the Consumer declared (ADR-0050, item 14).
        setClaims: (takenKeys, editable, findable, declaredKeys, leaveDeclared) => {
            taken = new Set(takenKeys);
            canEdit = editable;
            canFind = findable;
            declared = new Set(declaredKeys ?? []);
            leaves = leaveDeclared === true;
        },
        getScrollOffset: () => {
            if (pendingReveal) {
                return { top: pendingReveal.top, left: pendingReveal.left };
            }
            // A reveal's repaint is the browser's business, not the core's (repaintAfterReveal).
            if (repaint) {
                return { top: repaint.top, left: repaint.left };
            }
            return scroller ? { top: scroller.scrollTop, left: scroller.scrollLeft } : { top: 0, left: 0 };
        },
        // Where the Focus is kept visible (ADR-0012). The offsets are computed in C#,
        // which is what keeps scrollIntoView out of it: that would tuck the cell under the
        // sticky header or the Pinned Columns, neither of which it knows about. A reveal
        // names the render that paints it (token), and is written as that render lands
        // (pendingReveal above), then repainted (repaintAfterReveal); any other write is made at
        // once, and replaces a reveal still held or a repaint still under way.
        setScrollOffset: (top, left, token) => {
            if (!scroller) {
                return;
            }
            dropReveal();
            dropRepaint();
            const reveal = token !== undefined && token !== null;
            if (reveal && Number(root.getAttribute('data-ex-reveal')) < token) {
                holdReveal({ top, left, token });
                return;
            }
            scroller.scrollTop = top;
            scroller.scrollLeft = left;
            if (reveal) {
                repaintAfterReveal(top, left);
            }
        },
        // The first visible row kept across a change of the row height or the Layout Ceiling
        // (ADR-0028/0053), written only while the scroller still stands where the core last
        // knew it (fromTop, to a pixel). A scroll the core has not heard yet moved it, and the
        // user's scroll wins over an anchor computed from before it: answers false, and the
        // core reads the offset instead. The comparison is here because only here are the
        // write and the user's scroll ordered.
        anchorScrollTop: (top, fromTop) => {
            const standing = pendingReveal ? pendingReveal.top : repaint ? repaint.top : scroller?.scrollTop;
            if (!scroller || Math.abs(standing - fromTop) > 1) {
                return false;
            }
            if (pendingReveal) {
                pendingReveal.top = top;
                return true;
            }
            dropRepaint();
            scroller.scrollTop = top;
            return true;
        },
        // The page follows a move the grid made (ADR-0204, above): focus names the Focus it was taken
        // for, as the root's data-ex-follow-focus does, target the id of the cell to bring into the
        // window (the Extent's while extending), and token the render (data-ex-follow). A newer
        // request replaces one still waiting.
        followFocus: (focus, target, token) => {
            if (!root || !scroller) {
                return;
            }
            dropFollow();
            follow = { focus, target, token, frame: 0, timer: 0 };
            awaitFollowedRender();
        },
        // A focus the core takes a round trip after a gesture of this grid's (ADR-0204): a built-in
        // popover's opening focus, a key's move inside a menu, and a Chrome's popover control, which is
        // handed the core's focus function as a Chrome's editor is (ADR-0010). It asks the editor's
        // question first — DOM focus inside this root or on nothing — and leaves a field beside the
        // rows with focus of its own, as reclaimFocus does: another grid, or a control of the page's
        // the user has pressed in the meantime, keeps the keyboard. An element that cannot take focus
        // itself hands it to the first of its descendants that can, so a Chrome may name the box of a
        // control whose own element it cannot reach. Reads document.activeElement and no layout; the
        // seventh decision about focus made in script (ADR-0021's note of 2026-10-11).
        //
        // A Consumer's popover's control (ADR-0050 item 16) is handed this function too, with
        // `consumerPopover`: the editor's question is not asked of it, since a Consumer's popover may
        // open from a control of the page's, outside this root, as its contents' own focus never asked
        // it. Of every popover's control, only whether a press inside the popover has overtaken its
        // opening is asked (openingOvertaken; ticket 240).
        takeFocus: (element, preventScroll, consumerPopover) => {
            const active = document.activeElement;
            const own = active instanceof Element && active !== staleField
                && active.closest('.ex-formula-bar') !== null;
            if (!root || !(element instanceof HTMLElement) || !element.isConnected
                || !(consumerPopover === true || !active || active === document.body || active === document.documentElement
                    || (root.contains(active) && !own))
                || openingOvertaken(element)) {
                return;
            }
            // The element itself where it takes focus; else, inside it, a field first — a design
            // system's adornment or clear button stands before the field it decorates — then any
            // other control that takes focus by Tab.
            const field = 'input:not([disabled]):not([type="hidden"]), select:not([disabled]), textarea:not([disabled])';
            const control = 'button:not([disabled]), a[href], [tabindex]:not([tabindex="-1"])';
            const target = element.matches(`${field}, ${control}, [tabindex]`) ? element
                : element.querySelector(field) ?? element.querySelector(control);
            if (target instanceof HTMLElement) {
                staleField = null;
                target.focus({ preventScroll: preventScroll === true });
            }
        },
        // The keyboard back to this grid's root — to its Keyboard Field where it has one, the
        // element that holds the keyboard with no edit open (ADR-0080) — asked for by the core a
        // round trip after the gesture that wanted it (ADR-0021, ADR-0018): only while DOM focus
        // is still inside this root, or on nothing. A second grid the user has pressed in the
        // meantime keeps its keyboard. The condition reads document.activeElement and no layout.
        // It is one of the six decisions about focus made in script, all for the same reason —
        // made from C#, a round trip late, they would take or leave the keyboard in the wrong
        // place, or end a composition; the others are the press that brings the keyboard back to
        // an edit left standing (onPress, ADR-0018 section 6), the open edit's own focus
        // (focusEditor), the root's own focus passed on to its Keyboard Field (onFocused), the
        // field given up by a press during a composition (onPress) and the keyboard handed on to
        // a control of the Consumer's (handKeyboardTo).
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
        //
        // Widened 2026-10-12 (ADR-0050 item 16's note of that day): granted too while DOM focus is still
        // on `frame`, or inside it — a frame of the Consumer's own giving the keyboard back
        // (takeKeyboardBack), which then need not give it up to nothing first. From a frame, a keyboard
        // already inside this root — in a popover a command from the frame opened — is this grid's,
        // and stays where it is. Answers whether it was granted.
        reclaimFocus: (fromField, frame) => {
            const active = document.activeElement;
            const own = active instanceof Element && active !== staleField
                && active.closest('.ex-formula-bar') !== null;
            if (root && (!active || active === document.body || active === document.documentElement
                || (root.contains(active) && (fromField === true || !own))
                || (frame instanceof Element && frame.contains(active)))) {
                if (frame instanceof Element && active instanceof Element && root.contains(active)) {
                    return true;
                }
                staleField = null;
                (keyFieldOf() ?? root).focus({ preventScroll: true });
                return true;
            }
            return false;
        },
        // The keyboard back from a frame of the Consumer's own outside this root that holds it — a
        // toolbar, a list of its own (ReturnKeyboardAsync with a frame; ADR-0050 item 16's note of
        // 2026-10-12, the mirror of handOff 'frame') — with the keys typed on the frame on the way.
        // Granted where reclaimFocus is, and also while DOM focus is still on the frame or inside it,
        // so the frame need not first give it up to nothing: a key typed while it is on nothing reaches
        // no grid. The keys the frame heard are the core's to ask for, now that the keyboard has left
        // it: the question travels behind every key the frame heard before this moment, so its answer
        // holds them all, and they are held here, ahead of any typed on the root since, and then gated
        // as the root's (ADR-0010). Declined — the user has put the keyboard elsewhere (ADR-0018) — the
        // frame is still asked, so it stops holding, and its keys are dropped with the hand-back. It is
        // reclaimFocus's decision, widened to the frame: no other focus is moved here.
        takeKeyboardBack: async (element, ticket) => {
            if (!core) {
                return;
            }
            const granted = handle.reclaimFocus(false, element) === true;
            const typed = core.invokeMethodAsync('KeysTypedOnTheWay', ticket).catch((error) => {
                if (core) {
                    console.error('[ex-grid] the grid failed to take the keys typed on the way', error);
                }
                return [];
            });
            if (!granted) {
                await typed;
                return;
            }
            held.push({ typedOnTheWay: typed, paint: paintNow() });
            if (answering) {
                holdStartedAt = performance.now();
            } else {
                startHold();
            }
            // The Consumer may remove its frame and clear its key queue once this call completes.
            // Asking for the queue starts another interop call: returning before its answer lets
            // that removal overtake collection on a circuit. Complete only after the keys are ours,
            // whether the hand-back was granted or declined. The drain already holds their place.
            await typed;
        },
        // The keyboard this grid holds, handed on to a control of the Consumer's own outside it —
        // the first control of a frame or a tab the Consumer opened from the grid, a dialog's Close
        // (HandKeyboardToAsync; ADR-0070's note of 2026-10-02) — in place of that control's own
        // focus. It lands a round trip after the render that drew the control, and is granted only
        // while the keyboard is still this grid's: DOM focus inside this root or on nothing, the
        // condition reclaimFocus reads. A press the user made meanwhile — on the frame's own
        // content, another grid, a control of the page's — keeps the keyboard where it put it
        // (ADR-0018), and so does a field beside the rows with focus of its own, as reclaimFocus
        // leaves it; a field a press on the rows left standing (staleField) is not one, and gives
        // the keyboard up as it does to reclaimFocus. The sixth decision about focus made in script
        // (ADR-0021's note of 2026-10-02); it reads document.activeElement and no layout. The
        // control scrolls into view, as its own focus would have had it.
        //
        // While the keyboard is on its way to a frame of the Consumer's own (handOff 'frame') and a
        // hold stands, the control takes it in turn instead (frameInTurn, inTurn): when the hold has
        // reached it, with the keys held for the frame handed to it in the same task (ADR-0050 item
        // 16's note of 2026-10-12). The condition is the same, read then.
        handKeyboardTo: (element, inTurn) => {
            if (inTurn !== true && handOff === 'frame' && answering && element instanceof HTMLElement
                && !(root && root.contains(element))) {
                frameInTurn = element;
                return;
            }
            const active = document.activeElement;
            const own = active instanceof Element && active !== staleField
                && active.closest('.ex-formula-bar') !== null;
            if (root && element instanceof HTMLElement && element.isConnected
                && (!active || active === document.body || active === document.documentElement
                    || (root.contains(active) && !own))) {
                staleField = null;
                // A Chrome can name the box of a design-system control whose input it cannot
                // reference in .NET (More Colours, ADR-0039). A box starts at its first enabled
                // text field, before other controls such as a colour slider. Selecting its
                // value makes the first held character replace the opening value. This is
                // still the same conditional hand-off: an early choice outside the root wins.
                const direct = element.matches('input, textarea, select, button, a[href], [tabindex]');
                const firstText = direct ? null : [...element.querySelectorAll('input, textarea')].find(isTextField);
                const target = direct ? element : firstText
                    ?? element.querySelector('button:not([disabled]), select:not([disabled]), a[href], [tabindex]:not([tabindex="-1"])');
                if (target instanceof HTMLElement) {
                    target.focus();
                    if (target === firstText && isTextField(target)) {
                        target.select();
                    }
                }
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
        // opened the edit go into it (focusDeclined). While the Keyboard Field composes it waits
        // (ADR-0080, below).
        //
        // Nor from a field beside the rows with focus of its own, as reclaimFocus leaves it — the
        // Formula Bar's text or the Name Box the user pressed while the request was on its way,
        // whose press the core heard only after asking — unless the core means to take the
        // keyboard out of that field (fromField: a Reject the press into the Name Box met). The
        // keyboard stays where the user put it, and the edit goes on there: a request for the
        // Cell Editor, landing after a press into the bar, took the keyboard from the bar, and the
        // key typed next opened an edit in the cell (found on CI, msedge, the Server host).
        //
        // A waiting request granted as the keyboard leaves the Keyboard Field (onFocusLeft) is
        // judged where the keyboard is going, `toward`: DOM focus is on nothing while the browser
        // tells that departure. The core never passes it.
        focusEditor: (bar, fromField, toward) => {
            if (!root) {
                return;
            }
            // While the Keyboard Field composes, the request waits for the composition to end
            // there, and is granted then (carryKeyFieldText; ADR-0080): moving DOM focus now would
            // end it half-typed. So it does while a key the IME took there is still being told,
            // before the browser has told the page whether that key started the next (imeKeyTold).
            if (keyFieldComposing || imeKeyTold) {
                deferredEditorFocus = { bar, fromField };
                return;
            }
            const box = [...root.querySelectorAll('.ex-editor')]
                .find((b) => ownSurface(b) === b && b.classList.contains('ex-formula-bar-text') === (bar === true));
            const field = box ? surfaceField(box) : null;
            if (!field) {
                return;
            }
            const active = toward instanceof Element ? toward : document.activeElement;
            const fieldOfItsOwn = active instanceof Element && active !== staleField && !field.contains(active)
                && root.contains(active) && active.closest('.ex-formula-bar') !== null;
            if (fieldOfItsOwn && fromField !== true) {
                return;
            }
            if (!active || active === document.body || active === document.documentElement || root.contains(active)) {
                focusDeclined = false;
                // Not scrolled by the focus itself: the page follows the editor as it follows a move,
                // to the nearest edge, once it has painted (ADR-0204) — an editor opened by keys below
                // the fold is brought on screen, and one already in the window moves nothing.
                field.focus({ preventScroll: true });
                followEditor(field);
                // What the hold has for the edit goes into it now, ahead of any composition the IME
                // starts there next (settledWaits).
                lookAgain();
            } else if (editing !== 'none') {
                // The surface the core asked for is the edit's: the held keys go into it, and a
                // press back puts the keyboard there (standingField), the bar's when it was the bar.
                lastSurface = box;
                focusDeclined = true;
            }
        },
        // Escape's way out of Enter/Tab cycling (ADR-0012, rewritten 2026-10-01): the next Tab
        // or Shift+Tab is the browser's, and DOM focus stays where it is meanwhile. Told before
        // the Escape's answer, so the keys held behind it meet the release.
        releaseTab: () => {
            tabReleased = releaseEndsHeard === releaseEndsAtEscape;
        },
        dispose: () => {
            // Left attached, a grid that is gone goes on eating every key its root still
            // sees, and its .NET reference is never collected. The observer holds the
            // scroller and the .NET reference the same way, and a notification arriving
            // after disposal would call into a component that no longer exists.
            forgetHeldText();
            dropSettledObserver();
            observer.disconnect();
            ceilingObserver.disconnect();
            resolutionQuery?.removeEventListener('change', armResolution);
            resolutionQuery = null;
            dropReveal();
            dropRepaint();
            dropFollow();
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
            // A hold for a close the core will not answer now ends.
            endCloseHold();
            root.removeEventListener('input', onEditorInput, true);
            root.removeEventListener('compositionstart', onKeyFieldCompositionStart, true);
            root.removeEventListener('compositionend', onKeyFieldCompositionEnd, true);
            root.removeEventListener('keyup', onRootKeyUp, true);
            root.removeEventListener('focus', onFocused, true);
            root.removeEventListener('focusout', onFocusLeft, true);
            imeKeyTold = false;
            deferredEditorFocus = null;
            markFocusVisible(false);
            document.removeEventListener('selectionchange', onSelectionChange);
            cancelAnimationFrame(caretFrame);
            caretFrame = 0;
            watchReferenceTexts(false);
            // The grid's highlights go with it: their names are its own, and nothing else paints
            // or empties them.
            for (const [name, highlight] of highlights) {
                if (CSS.highlights.get(name) === highlight) {
                    CSS.highlights.delete(name);
                }
            }
            highlights.clear();
            root.removeEventListener('copy', onCopy);
            if (clipboardWatched) {
                navigator.clipboard.removeEventListener('clipboardchange', onClipboardChange);
            }
            root.removeEventListener('paste', onPaste);
            root.removeEventListener('copy', onHeldClipboard, true);
            root.removeEventListener('cut', onHeldClipboard, true);
            root.removeEventListener('paste', onHeldClipboard, true);
            lastSurface = null;
            staleField = null;
            nameBoxPressed = null;
            nameBoxSelected = null;
            // A press still to be asked about has no core left to answer it: the keys held behind
            // it are let go with the rest.
            pressToAsk?.resolve();
            pressToAsk = null;
            // A copy still held has no grid left to copy from: its write lands nothing.
            dropHeld();
            root = null;
            scroller = null;
            // The stamp too: the handle outlives this dispose until .NET releases it, a round trip
            // later on a circuit, and the stamp's parent is the whole grid (MEM-2).
            stamp = null;
            core = null;
            // A question the core still waits on is answered, with nothing left to hold.
            for (const ask of questionsAfterHold.splice(0)) {
                ask();
            }
            // And a wait on an input's answer (answeredUpTo).
            for (const wait of answeredWaits.splice(0)) {
                wait.resolve();
            }
        },
    };
    // Settled or not from the start: an attach on a circuit that has answered inputs before, under
    // a script that has not, takes up the root's count (inputsSent).
    lookSettled();
    return handle;
}
