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

## A cell boundary is a tab, and is never guessed *(added later)*

The clipboard's tabular convention is **TSV**. Excel, Sheets and every spreadsheet put
tab-separated text in `text/plain` and a table in `text/html`; none of them put commas there.
That single fact answers both halves of a question that looks like it needs a heuristic:

- **`1,234` pasted as text is one cell.** A thousands separator is not a cell boundary, because
  nothing that produces a real multi-cell copy would have used a comma to say so.
- **A CSV opened in Excel and copied back out keeps its structure**, because by then it is no
  longer CSV — Excel has parsed it into cells and writes tabs. Nothing needs to be guessed to
  preserve it.

**Decision: the grid never sniffs a delimiter. A tab and an HTML table cell are the only cell
boundaries; a line break is the only row boundary.** This was true of the implementation from the
first day and was written down nowhere, which is the state a convenience feature walks into.

The convenience in question looks harmless — *when the text holds no tabs, read it as CSV* — and
its worked example is the one this component cannot afford. A column of prices copied out of a
text editor,

```
1,234
5,678
```

is exactly the shape that heuristic reads as two rows of two columns. It is consistent, it is
plausible, and it is wrong; and the person who discovers it is the one who pasted money into a
grid that displays money. The same heuristic takes a European `1.234,56` apart, and a currency
string with it.

Rejected: **sniffing CSV when no tab is present**, above. Rejected: **a `PasteDelimiter`
parameter**, which keeps the default safe and makes the same grid read the clipboard differently
per Consumer — and in its CSV mode `1,234` still splits, so it does not solve the problem, it
relocates it to a Consumer who did not know they were choosing it. A Consumer that genuinely
needs CSV converts it before it reaches the clipboard; the grid does not, deliberately.

## Copy with headers *(added when the context menu was designed)*

[ADR-0036](./0036-the-context-menu-is-the-column-menu-shape-over-a-selection.md) needed a
"copy with headers" item and found there was no such capability: this ADR had never mentioned
headers and `BuildCopyPayload` had no notion of them. Most of what it needs derives.

- **The text is the column's `Header`, not its `Name`.** A column already carries both, and
  `Header` is what is on screen — which is what `text/plain` is for. It is the full declared
  header, never the truncated paint, by the same rule that keeps `####` off the clipboard
  ([ADR-0016](./0016-column-width-and-overflow.md)).
- **It needs no new refusal, in either orientation.** A `CopyPlan`'s vertical segments share a
  column span and stack, so one header row stands above the stack; its horizontal segments share
  a row span and concatenate, so the header row is those names in the order the cells are
  emitted. A disjoint selection has a well-defined header row either way.
- **It goes into both formats** — a `<th>` row in `text/html` as well as the first TSV line.
  Excel prefers the HTML flavour, and a header row that reached the text editor but not the
  spreadsheet would miss the case the feature exists for.

Two things were decisions.

**The header row counts against the cap.** The cap is applied to the `CopyPlan` — to what is
actually copied — not to the selection, and it should keep meaning that. A selection sitting
exactly on the cap therefore copies plainly and refuses with headers, which is explicable in one
sentence: the payload is bigger. The alternative, redefining the cap as a property of the
selection, buys consistency between two commands at the price of a cap that under-reports what it
lets through.

**The copied block is one row taller than the selection, and that is not hidden.** Pasting it
back lands on [ADR-0014](./0014-paste-shape-rules-and-selection-count.md)'s shape rules and is
refused by name — the first copy this grid produces that does not round-trip into it. That is the
right failure and needs no special case: it is loud, it names its rule, and the advice attached
to a shape refusal — reselect a target of the same shape — is true here, because a selection one
row taller does take it.

## A menu copy has no `copy` event *(added with the same design)*

The route table above switches **by the size of the selection**, on the premise that the everyday
copy is a Ctrl+C, whose `copy` event is itself the permission grant. A context-menu item breaks
that premise from a direction the table does not cover: **clicking a menu item fires no `copy`
event**, so a menu copy cannot take the event route however small the selection is.

**Decision: clipboard commands invoked from a menu always take the asynchronous API route.** No
new JavaScript use is needed — the clipboard is already on the allowlist
([ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md)) — and the alternative,
synthesising a `copy` event with `document.execCommand('copy')` inside the click handler, buys
the prompt-free property with a deprecated API this component would then depend on.

**Whether a prompt actually appears is not asserted here.** This ADR's caution about prompts was
written before [ADR-0017](./0017-target-chromium-browsers-only.md) narrowed the target, and
Chromium grants `clipboard-write` to the active tab — so the answer for the browsers this
component supports is probably "no prompt", and probably is not good enough for something a user
meets every day. **Layer 3 is where that answer lives**: the suite asserts that a menu copy
writes without a prompt on Chrome and on Edge, and if one appears that is a finding, recorded
against this section, not a surprise in the field.

## What the Blazor Server host settled *(added 2026-09-25)*

The routes above were wired and verified on WebAssembly only. Designing the Server host
(the grilling session that also produced [ADR-0019](./0019-one-repository-many-packages.md)'s
second host) read them against a circuit and found two ways each could fail quietly, and one
way paste could take the whole session down. Nothing above is reversed; three things are
added.

**Paste crosses as a stream, on both hosts.** The `paste` event handed both flavours to .NET
as two strings in one interop call. On a circuit that call is one incoming hub message, and
SignalR closes the connection on a message over its receive limit — 32 KB unless the
Consumer's application raises it. Excel's `text/html` for an ordinary few hundred cells is
past that, so the most ordinary paste this component exists for would have ended the user's
session. The flavours now cross as JS stream references (Blazor's own mechanism for large
interop data), which the hub limit does not apply to. One route for both hosts, because a
branch by host is a second path that only one host's tests would exercise. This is a change
of wiring inside the clipboard entry of
[ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md), not a new use.

**A stream needs a ceiling, and a paste past it is refused whole** — `PasteByteCap`, 16 MB
across both flavours by default, a Consumer parameter like the copy cap, raised as a
**Refusal** with its own reason (`PasteRefusalReason.TooLarge`) before any of it is read on
the .NET side. On Server the ceiling is also what stops one client from filling the server's
memory. **There is no fallback to `text/plain` when only the HTML flavour is too large.** The
HTML flavour is preferred because Excel's `x:num` carries the raw value at full precision
(the paste section above); the text flavour is the display format, already rounded. Falling back would paste rounded money exactly where
the paste is largest and least checkable — the smaller number, quietly. The cap sits on what
cannot be executed, not on the selection
([ADR-0014](./0014-paste-shape-rules-and-selection-count.md)).

**A write the browser refuses is a Refusal, not a silence.** `navigator.clipboard.write`
rejecting with `NotAllowedError` was swallowed on the reasoning that nothing landed, which
is this ADR's contract. But this ADR's refusals are *named*: a user told nothing believes the
copy happened and pastes whatever the clipboard held before. It is raised through
`OnCopyRefused` with its own reason (`CopyRefusalReason.ClipboardUnavailable`). Every host
can meet it; a Server host meets it more often, because a menu copy's write starts only after
the click has made a round trip.

**The event route does not exist on Server, as already recorded above** — the sync channel
is WebAssembly's. So on a Server host every copy, however small, takes the asynchronous route
and CP-6's "the `copy` event route" is a WebAssembly criterion; what it protects — no
permission prompt on an everyday copy — is asserted on Server instead (§24 of the
[Definition of Done](../definition-of-done.md)).

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
