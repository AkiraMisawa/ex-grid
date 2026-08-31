# JavaScript is allowlisted, not merely "minimised"

JavaScript is used **only** where Blazor genuinely cannot do the job, or where a measurement
shows the Blazor-side approach is too slow. "Keep JS to a minimum" is too vague to enforce,
so the rule is stronger: **there is a list, and anything not on it needs a new ADR.**

## The allowlist

Every entry names the reason it cannot be done from Blazor.

| Interop | Why Blazor cannot do it |
|---|---|
| **Capture-phase `keydown` on the grid root** | Blazor's `@onkeydown` only sees the bubble phase, by which point the cell editor has already moved the caret. Capture is the whole point ([ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md)), and the listener must be scoped to the instance root, not `document` ([ADR-0018](./0018-multiple-instances-must-be-independent.md)). |
| **Reading and setting `scrollTop` / `scrollLeft`** | Blazor's scroll event args carry no scroll offset, and there is no way to set it from C#. Virtualisation needs both directions ([ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md), [ADR-0012](./0012-anchor-focus-and-keyboard-navigation.md) — Focus must stay visible). |
| **Clipboard: `copy` / `paste` events and the async Clipboard API** | Writing two MIME types in one operation, and resolving a `ClipboardItem` from a promise, have no C# equivalent ([ADR-0005](./0005-copy-refuses-rather-than-truncates.md), [ADR-0017](./0017-target-chromium-browsers-only.md)). |

That is the entire list. **Three entries.**

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
- **Focus.** `ElementReference.FocusAsync()` is enough.
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
