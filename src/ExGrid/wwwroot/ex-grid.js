// One of the three permitted uses of JavaScript: reading and setting scroll offsets,
// which Blazor's scroll event args do not carry and C# cannot set (ADR-0021). Anything
// else — listeners, measurement, geometry — stays in C#; adding to this file needs an
// ADR.
//
// A module returning per-instance handles, never a global: a second grid on the page
// must not reach into the first (ADR-0018). The scroll listener itself is Blazor's
// @onscroll on the instance's own element, so this file attaches nothing.

/**
 * @param {HTMLElement} scroller the instance's scroll container
 * @returns a handle owned by that one grid
 */
export function attach(scroller) {
    return {
        // Both axes in one call, deliberately. A trackpad moves them together, and two
        // calls means two round-trips with the browser free to process another scroll
        // event in between — the rows would then be painted from one moment's offset and
        // the columns from another's. One call is one snapshot.
        getScrollOffset: () => (scroller
            ? { top: scroller.scrollTop, left: scroller.scrollLeft }
            : { top: 0, left: 0 }),
        // Kept even though nothing is held yet: the handle's contract is that a grid
        // releases what it took, and Focus-follows-scroll (ADR-0012) will set offsets
        // through this same handle.
        dispose: () => { scroller = null; },
    };
}
