# JavaScript is allowlisted, not merely "minimised"

JavaScript is used **only** where Blazor genuinely cannot do the job, or where a measurement
shows the Blazor-side approach is too slow. "Keep JS to a minimum" is too vague to enforce,
so the rule is stronger: **there is a list, and anything not on it needs a new ADR.**

## The allowlist

Every entry names the reason it cannot be done from Blazor.

| Interop | Why Blazor cannot do it |
|---|---|
| **Capture-phase `keydown` on the grid root** (with `blur()` on that same root — see below) | Blazor's `@onkeydown` only sees the bubble phase, by which point the cell editor has already moved the caret. Capture is the whole point ([ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md)), and the listener must be scoped to the instance root, not `document` ([ADR-0018](./0018-multiple-instances-must-be-independent.md)). |
| **Reading and setting `scrollTop` / `scrollLeft`** | Blazor's scroll event args carry no scroll offset, and there is no way to set it from C#. Virtualisation needs both directions ([ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md), [ADR-0012](./0012-anchor-focus-and-keyboard-navigation.md) — Focus must stay visible). |
| **Clipboard: `copy` / `paste` events and the async Clipboard API** | Writing two MIME types in one operation, and resolving a `ClipboardItem` from a promise, have no C# equivalent ([ADR-0005](./0005-copy-refuses-rather-than-truncates.md), [ADR-0017](./0017-target-chromium-browsers-only.md)). |
| **A `ResizeObserver` reporting the Scrollbar Gutter** | Added while wiring the keyboard; the paragraph below is the argument. |

That is the entire list. **Four entries.**

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

## What deliberately stays out of JavaScript

These are the places where reaching for JS would be the easy answer, and where we do not.

- **Popovers.** The Popover API is driven by the `popover` and `popovertarget` **attributes**,
  and CSS Anchor Positioning is CSS. Filter panels and column menus
  ([ADR-0009](./0009-filter-panel-contract.md), ADR-0010) need **no JS**, and that is a reason
  we restricted the browser target ([ADR-0017](./0017-target-chromium-browsers-only.md)).
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
  than left as an unremarked fourth use.)*
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
  would tuck the revealed cell underneath them.)*
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
  calls back; if it needs script to do that, the seam is wrong (ADR-0010).
- **If the browser target ever widens, this list grows.** Popovers would need positioning code,
  and the clipboard path would need reworking (ADR-0017). That cost belongs in the decision to
  widen, not in this ADR.
