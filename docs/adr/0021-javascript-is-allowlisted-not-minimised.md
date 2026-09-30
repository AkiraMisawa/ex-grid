# JavaScript is allowlisted, not merely "minimised"

JavaScript is used **only** where Blazor genuinely cannot do the job, or where a measurement
shows the Blazor-side approach is too slow. "Keep JS to a minimum" is too vague to enforce,
so the rule is stronger: **there is a list, and anything not on it needs a new ADR.**

## The allowlist

Every entry names the reason it cannot be done from Blazor.

| Interop | Why Blazor cannot do it |
|---|---|
| **Capture-phase `keydown` on the grid root** (with `blur()` on that same root — see below) | Blazor's `@onkeydown` only sees the bubble phase, by which point the cell editor has already moved the caret. Capture is the whole point ([ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md)), and the listener must be scoped to the instance root, not `document` ([ADR-0018](./0018-multiple-instances-must-be-independent.md)). *(Since 2026-09-25 it also holds the keys that follow a mode-changing key until C# has answered, and replays them in order — ADR-0010. When a taken key is forwarded is the same decision as whether it is: it cannot wait for a round trip.)* |
| **Reading and setting `scrollTop` / `scrollLeft`** | Blazor's scroll event args carry no scroll offset, and there is no way to set it from C#. Virtualisation needs both directions ([ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md), [ADR-0012](./0012-anchor-focus-and-keyboard-navigation.md) — Focus must stay visible). *(Since 2026-09-29, decided with the user: a reveal's write is held until the render that paints its slice has landed. On Blazor Server the write and the render's batch are separate messages, and a frame painted between them showed the new offset over the old rows (ADR-0012, "a reveal paints where it is going"). The render marks the root with a reveal number (`data-ex-reveal`), and the write waits for that number. It waits through a `MutationObserver` on that one attribute of the grid's own root, which runs only while a write is held, reads no layout, and gives up after 2 s. It is part of setting the offset, not a new entry.)* |
| **Clipboard: `copy` / `paste` events and the async Clipboard API** | Writing two MIME types in one operation, and resolving a `ClipboardItem` from a promise, have no C# equivalent ([ADR-0005](./0005-copy-refuses-rather-than-truncates.md), [ADR-0017](./0017-target-chromium-browsers-only.md)). *(Since 2026-09-25 a paste crosses as JS stream references rather than two strings in one call, so a Blazor Server hub's message limit cannot end the circuit — ADR-0005.)* *(Since 2026-09-29 the capture-phase keydown, when a key lands on the root, drops the document's text selection. Chrome sends `copy` and `paste` to whatever holds that selection, and an edit's end left a collapsed caret that a later click moved to page text outside the grid, so Ctrl+C and Ctrl+V stopped reaching the grid. It is the same two entries, not a new one.)* |
| **A `ResizeObserver` reporting the Scrollbar Gutter** | Added while wiring the keyboard; the paragraph below is the argument. |
| **A `ResizeObserver` reporting the Layout Ceiling** | Added 2026-09-27 ([ADR-0053](./0053-the-scroll-height-is-compressed-above-the-browsers-layout-ceiling.md)). The tallest element the browser will lay out depends on the display scale and the page zoom, and `devicePixelRatio` does not reliably say which (emulation moves one without the other). A hidden element declared 2²⁵ px tall is observed; the size reported is the ceiling. It fires at attach and when the scale or zoom changes, never per render. Blazor has no resize observation at all. |
| **A `mousemove` listener reporting the pointer — when it moves onto another row, and when it comes to rest** | Added when two decisions needed it at once; the section after the gutter's is the argument. Blazor's `@onmousemove` has no client-side predicate: every event crosses to .NET, and on Blazor Server every crossing is a wire round trip. |

That is the entire list. **Six entries.**

### The fourth entry, and why it is not the text measurement this ADR refuses

A classic scrollbar is drawn **inside** the box the element declares, so the columns and rows
get about 15px less than `ViewportWidth` and `ViewportHeight` say; macOS's overlay scrollbars
take nothing. Built on the outer size, the geometry put the Focus behind the bar on Windows and
Linux and [ADR-0012](./0012-anchor-focus-and-keyboard-navigation.md)'s "the Focus must always be
visible" was broken there — invisibly, because on the machine this is developed on every
measurement is 0 ([ADR-0013](./0013-fixed-row-height.md) records the decision this replaced).

**The grid does not measure it. The browser reports it.** That distinction is the whole of why
this is admissible where `getBoundingClientRect` on a cell is not. A measurement is a
**synchronous read the grid performs**, at a moment it chose, forcing layout and paying a round
trip on the path to a paint. This is a **notification the browser delivers** when a number it
already knows changes — no read on the render path, and nothing to call at all in the ordinary
case, which is the case where the gutter never changes.

- **What is observed is the CONTENT box, not the border box.** This is not a detail: observing
  the border box never fires at all, because the outer size is what the Consumer declared and it
  does not change when a scrollbar appears. The content box is exactly what shrinks.
- **Re-measuring happens on change and on nothing else.** No polling, no read per render.
- **A Consumer that sets `scrollbar-width: thin` is followed automatically**, because the
  observation is of the outcome rather than of a platform.
- *(Refined while designing the presentation contract: the same notification is how a Viewport
  declared as `Stretch` learns its size — the observer already watches the content box, so the
  report grows a field rather than this list growing an entry
  ([ADR-0028](./0028-geometry-is-resolved-once-density-is-only-a-preset.md)). The distinction
  this section draws is unchanged: the grid is told; it never asks.)*

Rejected on the way here:

- **Assume a constant, or let the Consumer subtract one.** The decision this replaced. The same
  application ships to macOS and to Windows, so any constant is wrong for half its users.
- **`scrollbar-gutter: stable` in CSS.** Reserves the vertical strip only, and does nothing at
  all where the scrollbars are overlays — so it cannot make the platforms agree, which was the
  only reason to reach for it.
- **Measure the gutter once, at attach.** Sound-looking and wrong twice over. With
  `overflow: auto` there is no bar until the content overflows, so attach is exactly the moment
  the answer is 0; and **what a native scrollbar is worth in CSS pixels under zoom could not be
  measured** on this machine at all — macOS forces overlay scrollbars and
  `--disable-features=OverlayScrollbar` does not turn them off. Observing sidesteps the unknown
  rather than guessing at it: if the width changes under zoom the notification arrives, and if
  it does not, nothing needed to happen.
- **`tests/ExGrid.Browser/scrollbar.spec.mjs` is where the zoom answer lives.** It asserts the
  invariant that holds either way — all of the Focus inside the readable area, at every zoom
  level, after moving again — rather than a guessed number of pixels. Run on Windows it either
  passes, or it names the zoom level at which the chain breaks.

### The fifth entry: the pointer, reported when it moves onto another row and when it rests

**Why a decision was needed at all — twice, on the same day.** Two decisions, taken on two
branches, arrived asking for the same missing fact, *where the pointer is while nothing is
being dragged*, and each wrote a fifth entry of its own:

- [ADR-0034](./0034-validation-is-a-consumer-verdict-enforced-only-at-the-editor.md) decided
  that the validation popover opens after 300 ms of stillness **on hover as well as on Focus**.
  The Focus half is C#'s; the hover half needs to know where the pointer *stopped*. That
  branch wrote an entry reporting **the rest** — offsets, once, when the pointer has been still.
- The `ExGrid.MudBlazor` survey ([ADR-0030](./0030-what-a-design-system-wrapper-owns-and-what-it-may-not-touch.md))
  found `Hover` on every MudBlazor table — the pointer's row highlighted, following the
  pointer — and [ADR-0029](./0029-the-presentation-surface-is-a-short-list-of-classes-and-tokens.md)
  had declared `--ex-row-hover-background` and recorded it as unimplementable. That branch wrote
  an entry reporting **the cell, on change** — indices, computed in JavaScript from column
  edges C# pushed to it.

The two met at the merge. Neither alone was right: a rest report cannot move a band that has
to follow the pointer, and a change report that mirrors `ColumnGeometry.ColumnAt` in JavaScript
is the "must move together" pairing ADR-0027 exists to forbid, one language over. What stands
is one listener with **two reports**, both carrying nothing but the event's own offsets:

- **Rest** — the pointer has been still for the core's `PopoverDelay`, passed in at attach so
  the number lives in one place. Consumed by the error popover.
- **Row change** — the pointer has moved onto another row. Consumed by the hover band. Whether
  the row changed is decided in JavaScript from the row height C# wrote inline on the root and
  the translation it wrote on the Viewport — the Geometry Tokens of ADR-0027, read as text,
  never measured — and *that number never crosses*: the report is offsets, and C# resolves
  the cell exactly as it does for a press, in `CellUnder`, once, in one place. JavaScript
  filters; it decides nothing.

Each report is switched on by C# only while something consumes it — rows while the band is on
(`HighlightHoverRow`, or a Wrapper's cascaded default), rests while a `CellMessageOf` can be
asked — and off, the listener computes nothing for it. Leaving the instance reports *away*
once, for both. A scroll under a still pointer drops the band and the listener's memory of
the last row together, so the next movement is reported even onto the same row.

**Why the obstacle is one this project chose.** Rows and cells are `pointer-events: none` so
that the row Viewport is the target of every mouse event and the cell is **arithmetic over the
event's own offsets** rather than a measured element
([ADR-0008](./0008-selection-is-painted-by-an-overlay.md)). That keeps ~220 painted cells free
of handlers and `getBoundingClientRect` off the paint path — and its side effect is that
`:hover` never matches a row, so the CSS route to either feature is closed. Giving rows their
pointer events back would reopen `:hover` and take the arithmetic away: the offsets would
arrive relative to the cell, and `MouseEventArgs` carries nothing that says which cell.
Rejected likewise: **a Blazor handler on flagged cells only** — those cells would need
`pointer-events: auto`, a press on one would no longer reach the Viewport, and the workaround
puts a delegate into the row's parameters against
[ADR-0003](./0003-cells-are-plain-markup-by-default-not-components.md)'s memoisation; and
**dropping the hover trigger** — an error a mouse user can see but not read is the
half-measure this component's first principle exists to refuse.

**Why not C#.** Blazor can attach `@onmousemove` to the Viewport; nothing is technically
impossible about it. But ADR-0008 already refused exactly that — *the move handler is attached
only while a drag is running, because a pointer merely crossing the grid would otherwise raise
an event per frame; for a Blazor Server Consumer, a wire round trip each time* — pinned by a
test named *`The_grid_does_not_listen_for_moves_until_a_drag_begins`*. That decision is not
re-measured here; the claim this entry makes is narrower than "too slow": **Blazor offers no
way to filter the event before it crosses**. The API that does not exist is a client-side
predicate on `@onmousemove`. That is ground 1 of the two below, not ground 2.

**What each report costs, and what is still owed.** A rest costs one call per pause. A row
change costs one call per row crossed — a fast vertical sweep is perhaps twenty to thirty a
second, and each paints one overlay element per layer, the Focus band's mechanism, which
ADR-0008 measured at 2.1 ms worst case. The number that is *not* known is a Server host under
a sweep while scrolling. *(Until 2026-09-25 the repository had no Blazor Server host — the
DemoHost's "Server" page was a WebAssembly page driving a fetching source, and is now named
`/fetch`. `samples/ExGrid.DemoHost.Server` now exists
([ADR-0019](./0019-one-repository-many-packages.md)), and the measurement is an opt-in layer-3
spec, `EXGRID_MEASURE=pointer`, run against it: the calls that cross the circuit per second of
a sweep, and the lag from the pointer crossing a row to the band repainting, at an injected
round trip of 0, 50 and 150 ms — a loopback TCP proxy delays both directions, since the
browser's own network throttling is not reliably applied to a WebSocket. The numbers are
recorded in `verification/`, never as a gate.)* A `spikes/render-bench` mode for the band's own
paint is still to be added, and is still owed. Timing never gates
([definition of done](../definition-of-done.md)); if the measured cost is bad on Server, the
answer is the switch each report already has, not a return to per-frame events.

**Verified by.** Layer 2: a rest on a flagged cell asks once and opens (ED-17b); a row report
onto the row already held renders nothing, and a band move re-renders no row (RR-11). Layer 3:
a real pointer resting on a flagged cell opens its message and leaving closes it; a real mouse
moved down a column paints the band and moves it, in the hovered instance only, and the band
vanishes on leave (UX-13); reading `ex-grid.js` against this table matches exactly (PF-2).

## What deliberately stays out of JavaScript

These are the places where reaching for JS would be the easy answer, and where we do not.

- **Popovers.** Filter panels, column menus and the Context Menu
  ([ADR-0009](./0009-filter-panel-contract.md), ADR-0010,
  [ADR-0036](./0036-the-context-menu-is-the-column-menu-shape-over-a-selection.md)) need **no
  JS**: each stands under the instance root and stays inside the grid's box, bounded by a height
  the core writes inline ([ADR-0040](./0040-a-popover-stays-inside-its-grids-box.md)).
  *(Corrected 2026-09-24. This entry used to give the reason as the Popover API being driven
  by the `popover` and `popovertarget` attributes. That holds only for a popover opened by a
  click on a button. The grid's popovers also open from keys and a right-click, which would
  need `showPopover()`. And the top layer would bury the popups a design system draws inside
  them.)*
- **Text measurement for overflow.** `####` could be decided by measuring rendered text.
  Instead numeric columns assume `font-variant-numeric: tabular-nums` and estimate from digit
  count ([ADR-0016](./0016-column-width-and-overflow.md)). No measurement round-trip, and it
  works before the cell is painted.
- **Focus.** `ElementReference.FocusAsync()` is enough. *(Refined while wiring the keyboard:
  that covers **taking** focus. **Releasing** it has no Blazor API, and Escape has to release
  it — Enter and Tab never leave the selection ([ADR-0012](./0012-anchor-focus-and-keyboard-navigation.md)),
  so without an exit the keyboard is trapped in the grid. The key handle therefore has a
  `blur()` on the instance's own root, alongside the listener already attached to it. It is
  one line, it reaches nothing outside this grid, and it is listed in the table above rather
  than left as an unremarked fourth use.)* *(Refined again when entering a cell by key was
  built, [ADR-0037](./0037-entering-a-cell-never-reaches-into-content-the-core-did-not-render.md):
  it needed **no** JavaScript, and that was a constraint on the design rather than luck. Over
  its own actions the core never moves DOM focus at all — the keyboard stays on the root and
  `aria-activedescendant` names the chosen button — and a Template's control focuses itself
  with its own `FocusAsync` when the core asks through the fragment's context. "Focus the first
  focusable thing in the cell" was the JavaScript answer and is recorded there as rejected. The
  one change to `ex-grid.js` is inside the first entry's filter: a repeated plain Space is taken
  and dropped, so a held Space engages once.)* *(Three decisions about focus are now made in script,
  all in notes at the end of this ADR: the hand-back of 2026-09-27, the press that brings the
  keyboard back to an edit left standing, 2026-09-29, and the editor's own focus, taken only while
  the keyboard is still this grid's, 2026-09-30.)*
- **Measuring the scrollbar.** The gutter is *reported*, never read — see the fourth entry above
  for why those are different things. Nothing in the grid calls `getBoundingClientRect`,
  `clientWidth` or `offsetWidth` on the path to a paint.
- **Selection and editor geometry — and which cell the pointer is on.** Overlays are positioned
  by arithmetic over a fixed row height ([ADR-0008](./0008-selection-is-painted-by-an-overlay.md),
  [ADR-0013](./0013-fixed-row-height.md)) — no layout reads. *(The hit test is the place this was
  most tempting, and it is written down here because "just call `getBoundingClientRect` on the
  cell" is the obvious answer: the grid already knows where every row and column is, so the
  pointer's own offsets are enough. Cells are `pointer-events: none`, the row Viewport is
  therefore the event's target, and the offsets arrive measured from it. `scrollIntoView()` is
  refused for the mirror-image reason — it knows nothing of a sticky header or a pinned block and
  would tuck the revealed cell underneath them.)* **The fifth entry does not move this line**:
  during a drag the cell still comes from C# over the event's offsets, and JS still measures
  nothing. What the fifth entry adds is a *filter* on idle movement — whether the row changed,
  read off the Geometry Tokens C# wrote — so that only a row change or a rest crosses to .NET.
  It reports the event's offsets, never an index it computed, and never positions an element.
- **Click dispatch for action columns.** Blazor event handlers on plain markup are cheap
  enough at ~40 visible rows ([ADR-0020](./0020-action-and-template-columns.md)).

## Why an allowlist rather than a guideline

A guideline ("use JS sparingly") is a matter of taste, and taste drifts. Every convenience is
locally justifiable, and a component ends up with a JS layer nobody planned. An allowlist makes
the growth **visible**: adding an entry means writing down why Blazor cannot do it, which is
exactly the argument that should be made out loud.

It also keeps the component honest about what it is. A Blazor component whose behaviour lives
in JavaScript is a JavaScript component with a C# wrapper.

## Adding to the list

Two grounds, and only two.

1. **Technically required.** Name the Blazor API that does not exist or does not carry the
   information needed. Speculation does not count — check first.
2. **Required for performance.** **Record the measurement.** A claim that "Blazor is too slow
   here" without numbers is not a reason; this project has already been wrong twice about
   performance by reasoning instead of measuring (see
   [ADR-0003](./0003-cells-are-plain-markup-by-default-not-components.md) and
   [ADR-0008](./0008-selection-is-painted-by-an-overlay.md), where the recorded predictions
   were wrong). `spikes/render-bench` exists for this.

## Consequences

- **The JS that does exist is a module with per-instance handles**, not a global. The spike's
  `window.bench` is the shape to avoid ([ADR-0018](./0018-multiple-instances-must-be-independent.md)).
- **`ExGrid.MudBlazor` and `ExGrid.Fluxor` inherit this rule.** A companion package is not an
  excuse to add script.
- **Chrome implementations must not smuggle JS in.** A `IGridChrome` implementation renders and
  calls back; if it needs script to do that, the seam is wrong (ADR-0010). *(Narrowed on
  2026-09-24 by [ADR-0039](./0039-a-popover-takes-the-keyboard-and-may-hold-popups-of-its-own.md):
  this means script a Wrapper or a Chrome **adds** — its own `.js`, its own interop calls. The
  scripts a design system's own components run for themselves, loaded by the Consumer's choice of
  that design system, are the design system's, not entries on this list.)*
- **If the browser target ever widens, this list grows.** Popovers would need positioning code,
  and the clipboard path would need reworking (ADR-0017). That cost belongs in the decision to
  widen, not in this ADR.

*(Added 2026-09-27 by [ADR-0051](./0051-formula-entry-completion-point-mode-and-the-formula-bar.md):
the capture-phase `keydown` message now also carries the value and caret of the input it is
capturing keys for. This is the same listener and the same allowlisted use. Reading an input's own
value is not a measurement: no layout is read.)*

*(Added 2026-09-27 by ADR-0051's second round: the editor listener also reports the caret with each
input, and places the caret after the core rewrites the editor's text. It claims Shift+arrows while
pointing, and only ↑, ↓, Tab and Escape while a completion list is open. This is still the one
allowlisted keyboard/editor use. Reading or setting an input's selection reads no layout.)*

*(Added the same day: the editor listener also reports the caret on `selectionchange` inside an
editor surface, and places it when an edit opens. It is the same use, and reads no layout.)*

*(Added 2026-09-29 by ADR-0051, "F4 cycles the Reference at the caret": while an edit is open and
the Consumer has declared the Reference cycling function, the listener claims F4 and sends it with
the editor's text and selection, as every claimed key already goes, and with the listener's own
note of whether the user moved the caret in that text. It then places the selection the core
answers, as it places the caret after any rewrite. It is the same use, and reads no layout.)*

*(Added 2026-09-27, decided with the user: when the grid hands the keyboard back to its own root, a
round trip after the gesture that asked for it, the call now does so only if DOM focus is still
inside that root or on nothing (`body`). Otherwise a second grid the user has since moved to would
have its keyboard taken. The call was already a JavaScript focus call made through Blazor. The
condition reads `document.activeElement` and no layout. This is the one decision about focus made
in JavaScript, and the reason is recorded here: [ADR-0018](./0018-multiple-instances-must-be-independent.md)'s
independence cannot be kept on a circuit otherwise.)* *(No longer the only one: see the note of
2026-09-29 at the end.)*

*(Added 2026-09-27, decided with the user: a capture-phase `mousedown` and `mouseup` on the
instance root. They exist so that a primary-button press on the rows keeps its place among held keys.
While keys are held, or a change of editing mode is being answered, such a press is held too and
replayed in order behind them. Without this, a click straight after Enter reaches C# before the held
Enter, and the Enter's move carries the Focus past the clicked cell: a value typed next lands a row
too low, which was measured on the Server host at 0–60 ms and on WebAssembly at 0 ms. When nothing
is held, the press passes through untouched. No layout is read.)*

*(Widened 2026-09-28, decided with the user, after the second Windows run's Server failures.)* The
same `mousedown` covers two more things *(and, since 2026-09-29, a third that does move focus: see the
last note)*. Neither adds a listener, reads layout, or moves focus from
script:

- **A press into an editable, unfocused Formula Bar is held among the keys too.** It opens an edit,
  but the core hears of it a round trip later. Keys typed in that gap — F2, ↓, a character held
  for another reason — were read as keys outside an edit, and were lost or did the wrong thing. The
  press is queued as a marker, and the keys after it wait for the core's answer to it.
- **A held press on the rows suppresses its default**, which would move DOM focus onto the rows.
  The rows hand focus back to the root a round trip later, and that hand-over is not held; landing
  just after the Cell Editor took focus, it pulled the keyboard off the editor and every later key
  waited for the listener's two-second fallback. A press that is not held keeps its default.

**The hand-back to the root is narrowed with it** (the note above). It no longer takes focus from
a field that stands beside the rows and has focus of its own: the Formula Bar and the Name Box,
built in or drawn by a Chrome. Before this, a row click's late hand-back took the keyboard from a
Formula Bar pressed after it. Everything else inside the root is still taken back, the Cell Editor
included, since the hand-back is also how the keyboard returns to the root after an edit
commits The band both fields are drawn in is what is checked, so a Chrome's control
inside it is covered. An edit the user ends with a key in the bar, or Enter in the Name Box, still
hands the keyboard back; one ended by a press elsewhere does not take it from a field.
*(Settled while implementing:)* the bar's focus event reaches C# at once, ahead of a row press held
before it, so the press into the bar is answered again in its turn. If the bar still holds focus
and no edit is open when its turn comes, the core opens the bar's edit then.)*

*(Added 2026-09-29 by [ADR-0057](./0057-references-are-outlined-in-colour-while-a-formula-is-edited.md),
decided with the user: the editor listener keeps the layer that colours a Formula's References honest.
On each input, and when the layer's text changes, it compares the layer's text with the field's value
and sets or clears one class, so the coloured text shows only while it is the text in the field. It
notices the layer's change through a `MutationObserver` on one attribute of the layer, as the
reveal's write waits on one attribute of the root. It also keeps the layer's `scrollLeft` equal to
the field's, which is the scroll-offset entry's API on one more element. *(Extended 2026-09-30, decided
with the user: while an edit is open it also hears `compositionend` on the root, in the capture phase,
because no `input` follows the end of an IME composition and the colours would otherwise wait for the
next key. It re-runs the same comparison, and nothing else.)* The ground is the first:
Blazor cannot know that the browser's value is ahead of the value the server rendered for. Neither
addition reads layout. They are the editor listener and the scroll offsets, not a new entry.)*

*(Added 2026-09-29, decided with the user, with
[ADR-0018](./0018-multiple-instances-must-be-independent.md), section 6: the capture-phase `mousedown`
on the root also brings the keyboard back to an edit left standing. When a press lands on this
root's rows or headings while an edit is open here and DOM focus is outside this root, the listener
focuses the editor surface that last held the keyboard before the press goes on. This is the second
decision about focus made in script, made for the same reason as the first: done from C#, a round
trip later, the keys typed in between would reach the grid the user had just left. The condition
reads `document.activeElement` and no layout, and the listener is the one already attached.)*

*(Decided with the user 2026-09-29, found while building ADR-0018 section 6; the 2026-09-28
sentence could be read the other way, and the user chose this reading.)* "One ended by a press elsewhere
does not take it from a field" (the 2026-09-28 note) means a press into another field, such as the
Name Box, which keeps the focus it was given. A press on the rows is not one. Where pointing is
declared, the core suppresses the default of every press on the rows while an edit is open
(ADR-0051), so that a press which points leaves the keyboard in the edit. When such a press
commits an edit typed in the Formula Bar instead, the bar's focus is only left standing: the
keyboard stayed in the bar with no edit open, and typing there went nowhere. That focus is now
marked as left standing, as a held press's already was, and the hand-back after the commit takes
it.

*(Added 2026-09-29, decided with the user, with
[ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md)'s widened hold: a press on the rows
while an edit is open starts a hold in the same listener. The keys typed after it are held, in
order, until the core has answered the press (`PressAnsweredAsync`, already asked for a held
press), and are then replayed against the mode the answer leaves. No listener is added and no
layout is read.)*

*(Added 2026-09-30, decided with the user, with
[ADR-0018](./0018-multiple-instances-must-be-independent.md), section 6: the core's own request that
an editor surface take the keyboard goes through the module, and is granted only while DOM focus is
inside this root or on nothing, the condition `reclaimFocus` already reads.)* The request is the one
made after the render that paints the Cell Editor, or the edit in the Formula Bar's text: on opening,
on F2, and after a Reject. Blazor's `FocusAsync` takes focus wherever focus is, and on a circuit it
runs when the browser acknowledges that render, a round trip after it.

- **Found by CI**, on msedge against the Server host: ED-26's test on `/sheets`. `=` was typed on
  the left Sheet and the right Sheet was pressed. Then the left editor's `FocusAsync` landed and took
  the keyboard back to the left.
- **The race was on the base before any change.** With 40 ms injected, the same steps failed 9 runs
  in 40 on the base and 8 in 40 on the branch under test. Every failure logged the same order: the
  press on the right, then the left editor taking focus.
- **Only script can tell.** C# cannot know where DOM focus is without measuring. A `focusout` from
  the other grid reaches the core too late to cancel a call already sent. Only script, at the moment
  of focusing, can tell whether the keyboard is still this grid's.
- **This is the third decision about focus made in script.** It reads `document.activeElement` and
  no layout, and it adds no listener.
- **When the request is declined, the edit is left standing**, as section 6 defines, and a press
  on this grid's rows brings the keyboard back. Keys held behind the key that opened the edit do not
  wait for a focus that will not come
  ([ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md), note of the same day).
- **A Chrome's editor control no longer focuses itself.** Its context hands it a function of the
  core's, which it calls on a request it has not answered yet, once its control is painted. The core
  finds the control inside its own box, as the press back already does, so it still holds no
  reference to a control it did not render (ADR-0010). Leaving Chromes to focus themselves, and
  recording the gap, was rejected: swapping Chrome would then change who gets the keyboard.
- **Open, not decided here:** a popover's opening focus (the column menu, the filter, Find) and a
  Template cell's own focus. Each is taken a round trip after a gesture in this grid too, so the same
  race exists there in principle. Nothing has been seen to fail there. It is recorded, not built.
