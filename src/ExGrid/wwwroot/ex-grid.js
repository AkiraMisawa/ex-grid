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
            if (root) {
                root.blur();
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
