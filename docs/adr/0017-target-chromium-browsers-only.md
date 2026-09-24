# The target browsers are Chrome and Edge. Both are required

**Chrome and Edge are both hard requirements** — not "any Chromium build will probably work", but
**both are targets and both are verified**. Safari and Firefox are out of scope.

The first Consumer is an **authenticated, LAN, all-day internal tool**, so the browser is
controlled. This is not a site handed to the public.

## What this unblocks

Three things were held open pending the browser decision.

### 1. Popovers being clipped

The filter panel and column menu ([ADR-0009](./0009-filter-panel-contract.md) /
[ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md)) float above the grid, but the grid
root is an `overflow: auto` scroll container, so placing them inside **clips them at its edge**
(the filter on the rightmost column especially).

**Use the Popover API and CSS Anchor Positioning.** Clipping and stacking order are then the
browser's problem, there is no need to portal to `document.body`, and **coordinates and z-index do
not tangle across multiple instances**
([ADR-0018](./0018-multiple-instances-must-be-independent.md)). It also keeps popovers off the
JavaScript allowlist entirely ([ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md)).

*(Replaced on 2026-09-24 by [ADR-0040](./0040-a-popover-stays-inside-its-grids-box.md). What was
built was never this: every popover stood under the instance root, and nothing recorded it. When a
grid in a dialog had its menu cut off, the answer above was measured and failed on two counts.
The top layer is entered only by `showPopover()` or a button click, and the grid's popovers open
from keys and a right-click, so script would be needed. And it buries every popup a design
system draws, so a select inside the grid's own panel could not be used. A popover now stays inside
its grid's box and scrolls rather than being cut. The "confine them inside the root" option below
was rejected for being cut off at the edge; ADR-0040 keeps it and bounds it, so nothing is.)*

Rejected:
- **Portal to `document.body`** — not clipped, but z-index and outside-click detection tangle
  across instances and the coordinates must be recomputed in absolute terms.
- **Confine them inside the root** — fully contained per instance, but the panel is cut off at the
  edge.

### 2. Writing two clipboard formats

[ADR-0005](./0005-copy-refuses-rather-than-truncates.md) puts the display format in `text/plain`
and the raw value in `text/html` simultaneously. Chrome supports a `ClipboardItem` with multiple
MIME types.

### 3. The copy cap's rationale changes (ADR-0005's revisit condition)

ADR-0005 **rejected "fetch asynchronously, then copy" but explicitly recorded that it was worth
revisiting "if the target is confirmed to be Chrome/Edge only"**. The condition is met.

Chrome accepts **a promise as a `ClipboardItem` value** (it can wait for it before writing) and is
**lenient about the user-activation context** (Safari permits it only under direct interaction and
cannot go asynchronous through a promise). So "ask for the selection, then write to the clipboard"
holds together.

**The cap remains, but its rationale changes.**

| | Rationale for the cap |
|---|---|
| Before | the data is not held **and** an asynchronous write cannot reach the clipboard |
| **After** | **it would be useless even if it worked** — a million rows × 50 columns of TSV is hundreds of megabytes, which the receiving application cannot handle |

**From "refuse because it cannot be done" to "refuse because it would be pointless."** The
decision to refuse (never truncate) is unchanged, but **the cap can be set considerably higher.**

## Two things to check, specific to Edge

An Edge deployed inside an organisation differs from Chrome in practice despite sharing Chromium.
**Both of these bear directly on features this design depends on**, so settle them before
implementation.

- **The minimum Edge version deployed.** Update policy can pin versions in an enterprise. **CSS
  Anchor Positioning is comparatively recent**, so if older versions are in the field the popover
  positioning does not hold and the fallback is portalling to `document.body` (the option rejected
  above).
- **Enterprise clipboard policy.** Edge has managed settings that can restrict clipboard access
  for data-loss prevention. Copy (ADR-0005) is central to this component, so **confirm no such
  restriction is in force**. If there is one, the design changes to make server-side export the
  primary path rather than copy.

Neither of these degrades quietly — **if they do not work, the feature does not work at all** — so
clear them early.

## Consequences

- **It is consistent with the Blazor WebAssembly premise.** The first Consumer is a standalone
  WASM client and already requires a modern browser.
- **Decide what happens on an unsupported browser.** Do not degrade silently; state that it is
  unsupported. This follows the component's standing principle — rather than be quietly wrong, say
  it cannot be done.
- **Verification runs on both Chrome and Edge.** Passing on one does not satisfy the requirement.
- **If this decision is reversed, all three items above swing back.** If the component is ever
  distributed as general-purpose open source, popovers and the clipboard are where the rework
  starts.
- **Browser-specific API use is confined to those two places.** Everything else is written against
  standard DOM and CSS, and the JavaScript allowlist
  ([ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md)) keeps it that way.
