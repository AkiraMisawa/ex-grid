# The accessibility surface is owned by the root. No cell carries selection

**`role="grid"` and every count, index and name the ARIA grid pattern needs are emitted — but
`aria-selected` is not, on any element.** The root states the totals, points at the Focus, and
announces the selection *extent* in words. The cells stay what
[ADR-0003](./0003-cells-are-plain-markup-by-default-not-components.md) made them: plain markup
that no selection change touches.

[ADR-0029](./0029-the-presentation-surface-is-a-short-list-of-classes-and-tokens.md) recorded this
surface as **not designed** while listing what it must contain. Most of that list turned out to be
derivable from decisions already taken. Exactly one item was a genuine choice, and it is the whole
of this ADR.

## What derives, and from where

None of this needed a decision. It is written down so that nobody re-derives it, and so that the
one line that *was* a decision is visible as the exception.

| Emitted | Derived from |
|---|---|
| `role="grid"` on the root, `row` / `gridcell` / `columnheader` beneath | the ARIA grid pattern, unmodified |
| `aria-rowcount` / `aria-colcount` = **the total**, not what is in the DOM | virtualisation. The DOM holds a Window ([ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md)); `TotalCount` is already a parameter |
| `aria-rowindex` / `aria-colindex` = **the absolute index**, on every painted row and cell | the same lie, on both axes. Column virtualisation slices horizontally too, so a cell's position among its siblings is wrong in exactly the way a row's is |
| one tab stop; `tabindex="0"` on the root and `aria-activedescendant` naming the Focus cell | the capture-phase listener is already on the instance root ([ADR-0018](./0018-multiple-instances-must-be-independent.md), [ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md)), so DOM focus already lives there and nowhere else. `aria-activedescendant` is the only way to say "the Focus is over there" without moving it. [ADR-0020](./0020-action-and-template-columns.md) already committed to the one-tab-stop pattern |
| `aria-sort` on the sorted header | `SortSpec` is already there ([ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)) |
| the real value as the accessible name of a `####` cell | [ADR-0016](./0016-column-width-and-overflow.md) already promises it. Overflow is already decided per cell before paint, so the attribute is written only on cells that hash |
| `aria-busy="true"` on Placeholder rows | [ADR-0004](./0004-cap-the-cells-touched-per-frame.md). A row whose values have not arrived must not be read out as though they had |
| `aria-busy="true"` on the root, and **no** `tabindex`, while the grid is **Prerendered** | *(Added 2026-09-25, with the Blazor Server host — a decision, not a derivation.)* A prerendered grid looks complete and answers nothing: a key pressed into it is gone. Leaving it out of the tab sequence means the keyboard cannot land somewhere that will not listen, and `aria-busy` says why. Both are lifted by its first interactive render. The rows keep their own `aria-busy` where they are Placeholders |
| `aria-hidden="true"` on the selection Overlay elements | [ADR-0008](./0008-selection-is-painted-by-an-overlay.md). They are paint, and a `role="grid"` whose rows are interleaved with unnamed `div`s is a malformed grid |

Every id is prefixed per instance, for the reason ADR-0018 exists.

## The one decision: selection is described, never attributed

ADR-0029 states the constraint from the other side — "selection deliberately has no per-cell
attribute to select on at all" — because ADR-0008 removed it deliberately. Restoring it for ARIA
is not a small addition:

> **Selection state never has to enter the row's parameters.** With the class approach, the row
> must know about selection. — ADR-0008

`aria-selected` on a cell **is** selection entering the row's parameters. `ShouldRender()` then
returns true for every row the selection touches, and dragging a range re-renders rows — the path
ADR-0008 measured at **19.70 ms at its maximum** and rejected. The row memoisation of ADR-0003 is
the thing that pays for it.

So the root does the talking:

```
root   role="grid"  aria-multiselectable="true"
       aria-rowcount="1000000"  aria-colcount="40"
       aria-activedescendant="ex-7-r412-c3"
row    aria-rowindex="413"
cell   aria-colindex="4"  id="ex-7-r412-c3"
       — and no aria-selected, here or anywhere
live   "4 rows by 3 columns selected, B2 to D5"
```

**What is given up is real and is not hidden:** a screen reader cannot ask a cell whether it is
selected, so cell-by-cell exploration of a range does not report membership. What is kept is that
the answer to "what is selected" is always available, in one sentence, at any moment — which is
the question a person actually asks. This is the first entry in the project's spine: **describing
the range plainly beats a per-cell attribute that quietly costs the grid its memoisation.**

Rejected on the way here:

- **`aria-selected` on painted selected cells.** Standard, and correct to an assistive technology.
  It overturns ADR-0008's central consequence and ADR-0003's memoisation together, and the DoD's
  re-render criteria with them. Bought for cell-level exploration of a rectangle whose bounds were
  already announced.
- **`aria-selected` on the Focus cell alone.** One attribute, memoisation intact — and it says
  "one cell is selected" while four hundred are. It is the shape of failure this project refuses
  first: not missing information, but **plausible wrong information**.
- **Nothing at all.** Cheapest, and leaves a selection-centred component mute about selection.

## When the announcement is made

**On settle, not on every intermediate rectangle.** A pointer drag crosses hundreds of rectangles
and a held `Shift+Down` repeats; announcing each one floods the queue and the user hears the
journey instead of the destination. The live region is `polite` and is written after the same
settle the loading path already uses (ADR-0004) — one sentence, naming the extent and the corner
cells.

A keystroke that moves the Focus without changing the selection announces nothing: the Focus moved,
`aria-activedescendant` moved with it, and the assistive technology reads the new cell itself.

## The live region has a second writer

*(Added by [ADR-0034](./0034-validation-is-a-consumer-verdict-enforced-only-at-the-editor.md).)*
The region was written for the selection extent and nothing else. A **Reject** writes to it too:
a rejected commit is a keypress that deliberately does nothing, and a user who cannot see the
editor's border and its popover would otherwise be told nothing at all.

It stays **one region and stays `polite`**. A Reject changes no selection, so it never races the
sentence this ADR was written for, and `polite` is right for a message the user provoked and is
waiting on. The criteria that make this region quiet — a Focus move announces nothing, a drag
announces once on settle — are untouched, because neither is a commit.

The grid still writes no sentence of its own: the text is the Consumer's, carried in the verdict
and relayed verbatim. What the grid owns is the region, which is what this ADR said it owned.

## The dangling reference, which will be hit in implementation

`aria-activedescendant` must name an element that is **in the DOM**. The Focus normally is —
[ADR-0012](./0012-anchor-focus-and-keyboard-navigation.md) reveals it after every keyboard move.
**A wheel or scrollbar scroll moves the Viewport without moving the Focus**, so the Focus cell can
leave the Window entirely, and the attribute is then pointing at an id that no longer exists.

The attribute is **cleared while the Focus is not painted, and restored when it is painted again.**
Not left dangling (invalid, and assistive technologies differ in what they do with it), and not
prevented by forcing a scroll back to the Focus, which would make the grid fight the user's own
scrolling — the opposite of ADR-0012, which reveals the Focus in response to *moving* it.

## Consequences

- **The Wrapper still may not touch any of this** ([ADR-0030](./0030-what-a-design-system-wrapper-owns-and-what-it-may-not-touch.md)). This ADR is what
  that prohibition was reserving.
- **The class list of ADR-0029 is unchanged.** Semantics went onto the root and onto rows and
  cells as *indices*; no state joined the class vocabulary, and no ARIA attribute became a
  styling hook.
- **`aria-multiselectable="true"` is stated even though no cell carries `aria-selected`.** It
  describes what the component permits, and it is what makes the live region's sentence
  intelligible rather than surprising.
- **This is the second reason the Focus cell is never merely "the cell with DOM focus".** The
  first was the capture-phase listener; this is `aria-activedescendant`. Anything that moves DOM
  focus into a cell breaks both at once. *(The first decision made against this line was
  entering a cell, [ADR-0037](./0037-entering-a-cell-never-reaches-into-content-the-core-did-not-render.md),
  and it kept both: over the core's own actions the keyboard stays on the root and
  `aria-activedescendant` moves to the chosen action's **button** — the attribute may name any
  descendant, and Chromium's accessibility tree resolves it to that button (checked over CDP;
  what a screen reader then says is still owed a real one, like the wording below). Only a
  Template's control takes DOM focus, which it already did when clicked; leaving it by Escape
  puts both back.)*
- **Layer 3 owns the verification.** Counts and indices are assertable in bUnit, but "the Focus is
  reachable by one tab, and the announcement is made once per settled selection" is a real-browser
  question. It joins the list in [ADR-0026](./0026-layer-three-runs-on-playwright-against-the-installed-chrome.md).

## Open

- **Which conformance target applies**, if any, is the first Consumer's to state. Nothing here
  depends on the answer: the surface above is the ARIA grid pattern, and a stated target would
  add an audit, not a redesign.
- **The Cell Editor's own semantics** are settled when the editor is built (ADR-0010), like its
  classes.
- ~~**Interactive mode's semantics**~~ — settled with the mode
  ([ADR-0037](./0037-entering-a-cell-never-reaches-into-content-the-core-did-not-render.md)): the
  grid announces nothing new. A chosen action is exposed through `aria-activedescendant` by the
  name ADR-0020 already requires every action to carry; a Template's control by its own label
  when it takes focus. The live region keeps its two writers.
- **Whether the announcement should name the corner cells by column header or by index** is a
  wording question, settled against a real screen reader in layer 3 rather than in prose here.
