# Copy refuses past its cap rather than quietly truncating

There is a cap on how large a selection can be copied to the clipboard. **Past the cap, the grid
does not copy the first N rows — it does not copy at all, and points at export instead.**

## Why not truncate

The first Consumer displays **position and risk figures**. The central use is a trader selecting
rows, pasting into Excel, and aggregating there. If only the first thousand rows land and the
warning is missed, **what was pasted looks like perfectly normal numbers with a total that is
quietly too small**. There is nothing to notice.

Failing silently in the direction of a smaller number is the worst failure this kind of tool can
have, and a warning dialog does not prevent it (design on the assumption that warnings are
missed). **Nothing happening** is the safe side to fall on.

## Why there is a cap — the rationale was replaced once

**Originally the rationale was "because it cannot be done".**

1. **The grid does not have the data.**
   [ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md) put sorting, filtering
   and fetching on the Consumer side, so the grid holds only the Window it has been pushed
   (tens of rows).
2. **The browser only permits a clipboard write immediately after a user action.** Querying a
   server and writing after an `await` can be rejected because the user-activation context has
   lapsed.

**[ADR-0017](./0017-target-chromium-browsers-only.md) narrowed the target to Chrome and Edge,
which dissolved point 2 and turned point 1 into "the Consumer can be asked for it".** The cap
stays regardless. **Only its rationale changed.**

> **Current rationale: because it would be useless even if it worked.**
> A million rows × 50 columns of TSV runs to hundreds of megabytes, which the receiving
> application cannot handle.

The cap is a **safety valve**, not a daily path — real selections are much smaller. And because
the rationale moved from "cannot" to "pointless", **the cap can be set considerably higher.**

**The decision to refuse has never changed.**

*(Refined while implementing: the default cap is **1,000,000 cells** — roughly tens of
megabytes of TSV — and the Consumer can set it higher. The unit is cells, not rows, because
width varies and it matches the count in the status area
([ADR-0014](./0014-paste-shape-rules-and-selection-count.md)). Refusal is strictly **past**
the cap: a selection of exactly the cap still copies, so a whole single column of exactly a
million rows goes through. An empty selection refuses with its own reason, which Chrome
typically renders as nothing happening. When a selection is both misaligned and past the
cap, **the misalignment is what is reported** — overlapping ranges double-count the cell
figure, so until the shapes line up there is no well-formed block for the cap to judge.)*

## Considered Options

- **Copy up to the cap and warn** — rejected, per "why not truncate" above.
- **Fetch asynchronously, then copy** — rejected at first, **now adopted** following
  [ADR-0017](./0017-target-chromium-browsers-only.md). Chrome accepts a promise as a
  `ClipboardItem` value and is lenient about the user-activation context (Safari permits it only
  under direct interaction and cannot do it asynchronously through a promise), so "ask for the
  selection, then write to the clipboard" holds together.

  The rows are requested from the Consumer, since the grid only holds the Window. That is the
  second half of the line drawn in [ADR-0009](./0009-filter-panel-contract.md) — **push for
  application state, pull is fine for transient UI data**. Rows destined for the clipboard are
  stored nowhere and never enter undo history.

## Clipboard routes — no permission prompt on everyday operations

Browsers may ask for permission when the clipboard is touched **programmatically**. They do not
ask when `event.clipboardData` is used inside a **`copy` / `paste` event** — the user pressing
Ctrl+C or Ctrl+V is itself taken as the grant.

| | Permission prompt | Can it fetch rows it does not hold? |
|---|---|---|
| `copy` event | **No** | **No** (synchronous; nothing can be awaited) |
| `navigator.clipboard.write()` | Possible | Yes (promise value, Chrome only) |

The prompt is the price of "fetch, then copy" above. **Switch route by the size of the
selection.**

- **Selection fits inside the Window → the `copy` event route.** It can be written
  synchronously, so no prompt appears. **The overwhelming majority of copies land here** (what is
  on screen, or near it).
- **Selection exceeds the Window → the asynchronous API route.** Ask the Consumer for the rows,
  then write. A prompt may appear, but this was an exceptional operation to begin with.

**Both formats (display format in `text/plain`, raw value in `text/html`) survive either route** —
on the event route, call `event.clipboardData.setData()` twice.

Rejected:
- **Always the asynchronous API** — the cap is higher, but a prompt can appear on every everyday
  operation. Too heavy.
- **Always the `copy` event** — no prompt, but copying is limited to the Window, which undoes the
  point of raising the cap.

### What wiring the routes settled *(added while implementing the clipboard)*

- **The `copy` and `paste` events do fire on the focused grid root** — a non-editable,
  `user-select: none` element — on the real Chrome. This was the assumption the whole event
  route stood on, and it was verified in a headed browser before the wiring was built, because
  a browser that only fired these events in editable contexts would have forced the hidden-
  textarea trick every other grid library carries.
- **The event route needs a synchronous answer, and only WebAssembly has the channel.** The
  `copy` event cannot await; the payload is asked for through `invokeMethod`, which exists on
  WASM (the first Consumer's premise, [ADR-0017](./0017-target-chromium-browsers-only.md)) and
  not on Blazor Server. Where the synchronous channel is missing, **every copy takes the
  asynchronous route** — correct, with the prompt-risk this ADR already priced in.
- **Ctrl+C / Ctrl+V are deliberately not in the key table.** Taking them in the capture-phase
  listener would `preventDefault` the very browser commands that fire the `copy`/`paste`
  events — the route would suppress itself.
- **A copy over an Action Column emits an empty cell** — the question
  [ADR-0020](./0020-action-and-template-columns.md) reserved for this wiring. The column has
  no value by declaration (`Value` answers null), so the empty cell is what the existing
  machinery already produces, and a refusal would make an ordinary "copy these rows" fail for
  containing a column the user cannot unselect sensibly. A Template Column copies its value
  accessor's answer, never its markup.
- **A copy beyond the Window with nobody to ask refuses by name** (`RowsUnavailable`, a
  component-level reason beside the three pure ones): a push Consumer that passed no provider
  cannot be answered for, and the fraction in hand is never emitted.

## Paste is received through the `paste` event

**Paste is in scope.** (The first draft of this ADR said it was not, on the grounds that a
display-only grid has nowhere to write back. That was superseded once editing entered scope —
the grid reports an intent and the Consumer owns the data, so paste is coherent. See
[ADR-0007](./0007-edits-are-an-overlay-owned-by-the-consumer.md).)

**No permission is needed.** And the `paste` event can read **`text/html` as well**. Excel puts
an HTML flavour on the clipboard when copying, so **reading that preserves type and precision**
(more accurate than reading the TSV text alone) — the mirror image of the point below about
Excel preferring HTML.

The shape rules for paste are in
[ADR-0014](./0014-paste-shape-rules-and-selection-count.md).

## Consequences

- **It presupposes the selection model** — rectangular ranges (anchor plus focus), whole rows and
  columns, Shift+arrow extension, Ctrl+A. Copy is only a feature on top of it. Settled in
  [ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md) and
  [ADR-0012](./0012-anchor-focus-and-keyboard-navigation.md).
- **A separate export feature is required** — a path that generates a file server-side. That is a
  Consumer feature rather than a grid feature, but the grid must be able to hand over the current
  Query (range, Filter, Sort, column order).
- **The browser's default copy behaviour has to be suppressed.** Cells are `div`s, so left alone
  the browser's own text selection is what gets copied, which will not match the rectangular
  selection. The grid is made focusable and intercepts the keys
  ([ADR-0018](./0018-multiple-instances-must-be-independent.md) scopes that per instance).
- **Copy follows the current column order and excludes hidden columns.** It depends on View State.
- **Two formats go on the clipboard at once** — the **displayed format** in `text/plain`, the
  **raw value** in `text/html`. Excel prefers the HTML flavour, so Excel receives precision and
  type intact while a text editor receives what was on screen. Excel itself works this way.

  Either format alone risks money breaking quietly. **Display format alone** loses the decimals,
  and **the thousands separator becomes a locale trap** — German-style `1.234.567,89` pasted into
  a differently-localised Excel is either treated as a string or **interpreted as a different
  number**, and the screen looks normal either way. **Raw value alone** renders dates as
  `2031-06-15T00:00:00`, which no one can read.
  Writing two formats is confirmed available on the target browsers
  ([ADR-0017](./0017-target-chromium-browsers-only.md)).
