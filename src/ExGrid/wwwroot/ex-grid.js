// Two of the three permitted uses of JavaScript (ADR-0021): the capture-phase keydown
// listener, and reading and setting scroll offsets. Anything else — measurement,
// geometry, popovers — stays in C#; adding to this file needs an ADR.
//
// A module returning per-instance handles, never a global: a second grid on the page must
// not reach into the first (ADR-0018). The scroll listener itself is Blazor's @onscroll on
// the instance's own element, so the only listener attached here is the key one.

/**
 * @param {HTMLElement} root the instance's root element — where keys are captured, so a
 *   grid without focus hears nothing (ADR-0018)
 * @param {HTMLElement} scroller the instance's scroll container
 * @param {object} core the .NET object reference the taken keys are forwarded to
 * @param {string[]} takenKeys canonical forms of the keys the core claims, built by
 *   GridKeys.Taken — this file decides nothing about which keys those are
 * @returns a handle owned by that one grid
 */
export function attach(root, scroller, core, takenKeys) {
    const taken = new Set(takenKeys);

    const onKeyDown = (event) => {
        if (!core) {
            return;
        }
        // Only when the grid itself holds the keyboard. The listener captures on the
        // root, so it sees keys aimed at anything inside it too — a Consumer's control in
        // a Template Column, or one of the grid's own action buttons, both of which take
        // focus when clicked (ADR-0020). Taken from there, Space would type nothing,
        // arrows would move the selection instead of a caret and Ctrl+A would select the
        // grid instead of the field's text.
        //
        // This is the mode-free form of ADR-0010's table: with no editor and no
        // Interactive mode yet, "something inside has focus" is the whole of the case
        // where the core does not arbitrate. When those modes arrive the set of keys
        // becomes mode-dependent and this guard is what they refine.
        if (event.target !== root) {
            return;
        }
        // Mid-composition an IME owns Enter, Escape and the arrows — they choose and
        // commit a candidate. Taking them there breaks typing in any language that needs
        // one, and the grid would move under a half-finished word.
        if (event.isComposing || event.keyCode === 229) {
            return;
        }
        // The mirror of GridKeys.Canonical — the two must move together. It exists here
        // only to decide whether to take the key: preventDefault has to happen now, and
        // invokeMethodAsync's answer would arrive long after the event is over. What the
        // key MEANS is resolved on the C# side, from the raw fields sent below, so a
        // disagreement between the two shows up as a key that does nothing rather than as
        // a key that does something else.
        const control = event.ctrlKey || event.metaKey;
        const prefix = (control ? 'Control+' : '') + (event.shiftKey ? 'Shift+' : '') + (event.altKey ? 'Alt+' : '');
        if (!taken.has(prefix + event.key)) {
            return;
        }

        event.preventDefault();
        event.stopPropagation();
        core.invokeMethodAsync('OnKeyAsync', event.key, event.ctrlKey, event.shiftKey, event.altKey, event.metaKey)
            .catch((error) => {
                // Disposal can overtake a key in flight, and that is not a fault. Anything
                // else is reported: a swallowed failure here means keys that silently stop
                // working.
                if (core) {
                    console.error('[ex-grid] the grid failed to handle a key', error);
                }
            });
    };

    // Capture, and on the root rather than the document: on the document every grid on
    // the page would receive every keystroke, and in the bubble phase a cell editor would
    // already have moved its caret (ADR-0010 / ADR-0018).
    root.addEventListener('keydown', onKeyDown, true);

    return {
        // Both axes in one call, deliberately. A trackpad moves them together, and two
        // calls means two round-trips with the browser free to process another scroll
        // event in between — the rows would then be painted from one moment's offset and
        // the columns from another's. One call is one snapshot.
        getScrollOffset: () => (scroller
            ? { top: scroller.scrollTop, left: scroller.scrollLeft }
            : { top: 0, left: 0 }),
        // Where the Focus is kept visible (ADR-0012). The offsets are computed in C#,
        // which is what keeps scrollIntoView out of it: that would tuck the cell under the
        // sticky header or the Pinned Columns, neither of which it knows about.
        setScrollOffset: (top, left) => {
            if (scroller) {
                scroller.scrollTop = top;
                scroller.scrollLeft = left;
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
            // sees, and its .NET reference is never collected.
            root.removeEventListener('keydown', onKeyDown, true);
            root = null;
            scroller = null;
            core = null;
        },
    };
}
