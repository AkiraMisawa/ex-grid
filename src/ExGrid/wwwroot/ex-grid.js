// The four permitted uses of JavaScript (ADR-0021): the capture-phase keydown
// listener, reading and setting scroll offsets, the clipboard, and being told what the
// scrollbar takes out of the box. Anything else — text measurement, overlay geometry,
// popovers — stays in C#; adding to this file needs an ADR.
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
 * @param {boolean} canEdit whether any column edits at all — a display-only grid must
 *   not take printable keys away from the page (ADR-0010)
 * @returns a handle owned by that one grid
 */
export function attach(root, scroller, core, takenKeys, canEdit) {
    const taken = new Set(takenKeys);

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
    // these are gates only.
    let editing = 'none';
    const editingKeys = new Set(
        ['Escape', 'Enter', 'Shift+Enter', 'Control+Enter', 'Tab', 'Shift+Tab', 'F2']);
    const overwriteKeys = new Set([
        ...editingKeys, 'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'Home', 'End']);

    const onKeyDown = (event) => {
        if (!core) {
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
        const control = event.ctrlKey || (event.metaKey && metaIsPrimary);
        // Meta held where it is not primary still appears in the form, so it cannot pass
        // for an unmodified key: nothing in the set carries it, and the browser keeps it.
        const foreign = event.metaKey && !metaIsPrimary;
        const prefix = (control ? 'Control+' : '') + (foreign ? 'Meta+' : '')
            + (event.shiftKey ? 'Shift+' : '') + (event.altKey ? 'Alt+' : '');
        const canonical = prefix + event.key;

        if (editing === 'none') {
            if (event.target !== root) {
                // A focusable descendant holds the keyboard — a Consumer's control in a
                // Template Column, or one of the grid's own action buttons, both of
                // which take focus when clicked (ADR-0020). The control owns everything
                // but the way out: Escape returns the keyboard to the grid; every other
                // key keeps its meaning in the control — taken from there, Space would
                // type nothing, arrows would move the selection instead of a caret and
                // Ctrl+A would select the grid instead of the field's text.
                if (canonical !== 'Escape') {
                    return;
                }
            } else {
                let take = taken.has(canonical);
                // F2 and a printable character open the Cell Editor (ADR-0010) — only
                // on a grid that has an editable column at all: a display-only grid
                // must not eat the page's keys, round-tripping every keystroke. Whether
                // the focused cell actually edits is still resolved in C#. An AltGr
                // chord reports Control+Alt together on Windows, and is how the
                // German, French and Nordic layouts type @ { [ € — so both-held passes
                // where either alone is a shortcut and stays the browser's.
                if (!take && canEdit && !foreign) {
                    if (!control && !event.altKey) {
                        take = event.key === 'F2' || event.key.length === 1;
                    } else if (event.ctrlKey && event.altKey) {
                        take = event.key.length === 1;
                    }
                }
                if (!take) {
                    return;
                }
            }
        } else {
            // Overwrite or Caret: the editor normally holds DOM focus, but a click can
            // park it on a Consumer's control mid-edit — from there the control keeps
            // its keys, exactly as it does outside editing. closest, not classList: a
            // substituted Chrome editor is a .ex-editor DIV whose focused control is a
            // descendant (ADR-0010).
            if (event.target !== root
                && !(event.target instanceof Element && event.target.closest('.ex-editor'))) {
                return;
            }
            const claimed = editing === 'overwrite' ? overwriteKeys : editingKeys;
            if (!claimed.has(canonical)) {
                return;
            }
        }

        event.preventDefault();
        event.stopPropagation();
        core.invokeMethodAsync(
            'OnKeyAsync', event.key, event.ctrlKey, event.shiftKey, event.altKey, event.metaKey, metaIsPrimary,
            event.target !== root)
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
            // A refusal from the core or a denied clipboard permission: nothing landed,
            // which is the refusing grid's contract (ADR-0005) — the reason has already
            // been raised (OnCopyRefused on the C# side, or the console line above).
            // Anything else is a failure and is said so.
            if (error instanceof Error && error.message === 'the copy was refused') {
                return;
            }
            if (error instanceof DOMException && error.name === 'NotAllowedError') {
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
        if (!core || event.target !== root) {
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
        if (!core || event.target !== root) {
            return;
        }
        // clipboardData is only readable inside the event, so both flavours are read
        // here; everything they mean — shape rules, refusals, the intent — is decided
        // in C# (ADR-0014). preventDefault regardless: pasting into a non-editable
        // element does nothing by default, and must not start doing something later.
        event.preventDefault();
        const text = event.clipboardData.getData('text/plain');
        const html = event.clipboardData.getData('text/html');
        core.invokeMethodAsync('OnPasteAsync', text, html)
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
        // rides the same report (ADR-0028) — under ViewportSize.Fill it IS the size —
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

    return {
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
        // Which editing mode the key gate runs under (ADR-0010): 'none', 'overwrite'
        // or 'caret'. Set by the core when the mode changes — a mode change is a
        // different set of claimed keys. (A focusable descendant holding the keyboard
        // — ADR-0020's interactive cell — is not a mode: it is read off event.target,
        // which is true whether focus arrived by click or by key.)
        setEditing: (mode) => {
            editing = mode;
        },
        // Whether any column edits — re-told when the column set changes, so a grid
        // that becomes display-only stops taking printable keys (ADR-0010/0020).
        setCanEdit: (value) => {
            canEdit = value;
        },
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
            // sees, and its .NET reference is never collected. The observer holds the
            // scroller and the .NET reference the same way, and a notification arriving
            // after disposal would call into a component that no longer exists.
            observer.disconnect();
            root.removeEventListener('keydown', onKeyDown, true);
            root.removeEventListener('copy', onCopy);
            root.removeEventListener('paste', onPaste);
            root = null;
            scroller = null;
            core = null;
        },
    };
}
