# A Reference Outline is dragged by its edge to move its Reference, and by a corner square to resize it

*Proposed on 2026-11-03 in the project thread "数式の参照枠をドラッグで動かす" ("Drag a Formula's Reference Outline").
The user put the gesture into the next release, 0.2.0, the same day ("4以外入れる", "everything but 4": ticket 31 was
one of the items kept). It settles what [ADR-0057](./0057-references-are-outlined-in-colour-while-a-formula-is-edited.md)
left under "Not done", and what [ticket 31](../specs/exsheet/issues/31-drag-a-reference-outline.md) asked for before an
agent could take it.*

## Context

While a Formula is edited, every Reference in it is outlined in its colour over the cells it names (ADR-0057). In
Excel the outline is also a handle: dragging its edge moves the Reference (`A1:A5` becomes `C1:C5`), and dragging one
of the small squares at its corners resizes it (`A1:A5` becomes `A1:B8`). The Formula's text is rewritten as the drag
goes. The squares were held back on 2026-09-30 so that they would never offer a drag that does nothing, and they arrive
with this gesture.

Ticket 31 named what had to be decided first: it is a new pointer gesture over the rows, and it could compete with the
fill handle ([ADR-0050](./0050-what-exsheet-asks-of-exgrids-core.md), item 5) and with a selecting drag
([ADR-0008](./0008-selection-is-painted-by-an-overlay.md), [ADR-0052](./0052-the-focus-is-excels-active-cell-and-the-extent-is-the-moving-end.md))
for the same presses. It also asked four things of Excel: the cursor over an edge and over a square, Escape mid-drag,
scrolling at the Viewport's edge, and a structured reference's outline in another grid. 0.2.0's release candidate may
not be cut while a question to Excel is open ([ADR-0208](./0208-a-question-to-excel-is-asked-when-its-feature-is-built-and-a-check-by-hand-is-repeated-only-when-its-code-changes.md)),
so this ADR answers each of them as a decision, from what Microsoft documents and what the gesture needs, and leaves
none open.

## Decision

### 1. The gesture is the grid's, and the Reference's new text is the Consumer's

As every Formula entry aid is (ADR-0051, ADR-0057), the gesture is ExGrid mechanism that a Consumer switches on and
gives meaning to. The grid knows a Reference only as a span of the editor's text and the range it names, which the
Consumer's `referencesIn` already answers. What the Reference is written as once it names other cells is the
Consumer's answer to a new option:

```ts
moveReference?: (reference: string, range: SelectionRange) => string;
```

`reference` is the Reference's text as it stands in the editor, and `range` the cells it is to name. The answer is
the text that names `range` written as `reference` was written. A non-string or empty answer is refused by name, as
`referenceText`'s is. **Without `moveReference`, nothing changes**: no grip, no square, and ADR-0057's outlines as
they are (DC-1). ExSheet declares it, and answers it from the engine:

- **Each part keeps its form.** `$A$1` moved two columns is `$C$1`; `A$1:$B2` keeps its `$` on the same parts. A
  Sheet qualifier is kept as typed (`Sheet1!A1` stays qualified).
- **A one-cell Reference resized to more cells writes both ends in its form**: `$A$1` becomes `$A$1:$B$3`. A range
  resized to one cell writes that cell once (`A1`), in its first end's form.
- **A whole column (row) that still spans every row (column) of the Sheet stays a whole column (row)**: `A:A` moved
  is `C:C`. Resized from a corner so that it no longer spans them all, it is written as the cells it names.

Blazor's ExGrid takes `moveReference` as a script function, beside the five ADR-0431 made the Consumer's script.
ExSheet's Blazor adapter needs nothing: its Sheet is the Script Core's, and declares it there.

### 2. Which outlines carry grips

While an edit is open and `moveReference` is declared, **each Reference Outline over this grid's cells carries grips**:
its four edges, and a square at each of its four corners. Point's outline is the outline of the Reference it writes
(ADR-0057), so it carries them too. Nothing else does:

- **Columns a Consumer asks to have outlined** (`outlinedColumns`, a Linked Table's column in the grid that shows it)
  carry none. That grid has no edit whose text names them, so a drag there would have nothing to rewrite. Ticket 31's
  fourth question is answered so: a structured reference's outline in another grid is not dragged.
- **Point's dashes** (ADR-0058) carry none; they mark a cell, not a Reference.
- **A Reference no cell of which is painted** draws no grip, as it draws no outline.

### 3. What a press on a grip means, and why nothing competes for it

A press of the primary button, with no Shift, Control, Meta or Alt, on an **edge** starts a **move**; on a **square**
it starts a **resize**. The press never points and never commits the edit, and the keyboard stays in the editor (it
comes back to the edit's surface, as a pointing press brings it, ADR-0058 SH-35).

- **A move** keeps the range's size and moves it by as many rows and columns as the pointer has moved, counted from
  the cell of the range nearest the press: the edge's own row or column, and along it the cell pressed. The range is
  held inside the grid's extent: it stops at an edge rather than shrinking.
- **A Reference that names cells past the grid's extent** (a generic grid's Consumer may name more rows than it has)
  is moved and resized as the range it names, never as the part drawn: what lies past the grid stays in the
  Reference. A move takes it no further past the grid than it was. The edges the extent cut are not its edges and
  carry no grip.
- **A resize** holds the opposite corner and spans from it to the cell under the pointer; past that corner the range
  turns over, as a selecting drag's does.
- **The text is rewritten each time the reached range changes**, never between: the Reference's span is replaced by
  `moveReference`'s answer, and the colouring is asked again (ADR-0057), so the outline, its colour and the text's
  colour follow. A drag that ends where it started rewrites nothing; a press released without reaching another cell
  changes nothing, not even the caret.
- **The caret stays where the user left it.** Text after the Reference moves by as much as the Reference's length
  changed, and the caret with it; a caret inside the Reference's span goes to its end. The edit's mode does not change.
  Pointing, where it stands, goes on: if the Reference dragged is the one Point writes, Point's outline is the moved
  range and its arrows go on from there; if it is another, Point's span moves with the text around it.
- **With a modifier, the press is what it was before this ADR**: Shift+press over a grip extends Point (ADR-0051).
- **Where outlines overlap, the one later in the text takes the press**: it is drawn over the earlier (ADR-0057).
- **On the cell being edited**, the Cell Editor stands over the outline (ADR-0057), and over its grips with it: an edge
  or a square is grabbed only where it lies outside the editor. Excel draws the squares over the active cell (the
  eighth Windows run); here they stay in the selection overlay with their outline, under the editor and cut by the
  Pinned Columns as the outline is, rather than in a layer of their own above both.

Nothing else on the grid wants these presses. The fill handle is drawn only while no edit is open (ADR-0050, item 5),
and grips only while one is. A selecting drag cannot start while an edit is open: a press there either points or
commits the edit first. A grip is therefore the only meaning a press on it can have, and no gesture is taken away.

### 4. The squares and the edges, as drawn

- **A square** is 5 × 5 CSS pixels in the Reference's colour, with a 1 pixel margin of the grid's ground
  (`--ex-background`) around it, centred on the range's corner (the eighth Windows run, `shots/Z01-*`, `Z23-*`). Excel keeps its size in screen
  pixels at any zoom; the grid has no zoom of its own, and a browser's zoom scales it with everything else.
- **An edge's grip** is a strip along the range's edge, wider than the outline's line so that it can be pressed: 5 CSS
  pixels, centred on the edge, so that it covers the line however wide the stylesheet draws it, and the grid pairs no
  constant with the line's width.
- **Both sizes are Grid Metrics** (ADR-0027, ADR-0028), emitted inline as the fill handle's size is, never stylesheet
  literals, and so is the square's margin; the colours are the outline's Visual Tokens, `--ex-reference-1` to `--ex-reference-7`.
- **They are elements of the selection overlay**, per outline in each layer it reaches, as the outline is, and never
  per cell. A square is drawn only where its corner is painted, and an edge only where it is a real edge of the range,
  never where a layer or the painted rows cut it, nor under the Row Headings or, in the scrollable layer, the Pinned
  Columns, where it could be neither seen nor meant.

### 5. The cursor is the browser's, from the stylesheet

Over an edge the cursor is `move`; over a square, `nwse-resize` or `nesw-resize` by its corner (ticket 31's first
question). The grips are elements that take the pointer, and the stylesheet gives each its cursor, so the browser
chooses the cursor where it already knows where the pointer is. **No listener is added for it, and nothing is read on
a move**: CLAUDE.md's rule that the grid never measures, and that the pointer is heard only when it moves onto
another row and when it rests, holds as written.

During a drag the root wears `data-ex-reference-drag`, which keeps the cursor and makes the grips take no pointer
events, so the moves of the drag reach the rows' box and are read as every drag's are (ADR-0012), by arithmetic on
their offsets. Which Reference and which grip was pressed is read from the element pressed, and where in the range
from that grip's painted place and the press's offset within it, never from the layout.

### 6. Escape mid-drag puts the Reference back, and the edit stays open

Escape during a grip drag ends the drag and puts back the text the Reference had when it was pressed; the edit stays
open, with its caret and mode as before the press (ticket 31's second question). The drag is the innermost layer
Escape peels (ADR-0012's layering): a second Escape cancels the edit as it always does. *(Recommended over Escape
cancelling the whole edit at once: a drag the user abandons should not cost the Formula they typed.)*

A release anywhere on the grid ends the drag where it last reached. Released outside the grid, the next move without
the button ends it there, as every drag on the grid ends (ADR-0008).

### 7. The edge band scrolls during the drag

Held at the Viewport's edge, the drag scrolls as a selecting drag does (ADR-0008's edge band), and the moved or resized
range follows the cell under the pointer as the rows pass beneath it (ticket 31's third question).

### 8. The outcome does not depend on how fast the drag is made

A drag's result is a function of the grip pressed and the last cell reached: a press followed by one move to the
target gives the same text as a press and a hundred moves (CLAUDE.md, the spine's sixth rule). The press is taken
against the colouring that was painted: when the editor's text is no longer the text the grips were drawn for, the
press is refused and does nothing, rather than rewriting a span that has moved.

### 9. What the drag leaves to the commit

The drag only rewrites the uncommitted text. Enter commits the rewritten Formula as typed text is committed; Escape
(a second one mid-drag) cancels the edit and every rewrite with it. A Sheet's history holds one step, the commit.

## What stays out

- **Touch.** A grip is pressed with a mouse or a pen. A tap on a grip is a press released without a move, and changes
  nothing; dragging a grip by touch is not taken in this release
  ([ADR-0260](./0260-touch-input-reports-browser-facts-and-keeps-command-meaning-in-csharp.md)).
- **Dragging a Reference onto another Sheet.** The grips move a Reference among this grid's cells only.
- **Ctrl+drag to copy a Reference.** Excel has no such gesture on its outlines.
- **A secondary press on a grip.** The Context Menu is raised from a press on the rows' own box; one on a grip raises
  none.

## Consequences

- ADR-0057's "Not done" entry for this gesture, and `docs/definition-of-done.md` §21.11's row for it, are settled by
  this ADR; ticket 31 is done when it lands.
- Two criteria are added: **DC-67**, the grid's gesture (sections 2 to 8: layer 2, and one layer 3 test on the React
  host that drags an edge and a square with the browser's own mouse events, as the fill handle's tests drag; the
  DevTools drag helper is for HTML drag and drop, which this gesture is not), and **SH-69**, ExSheet's rewrite
  (section 1: layer 1, every form the engine keeps).
- The core's public surface grows by `moveReference` on the grid's options and two Grid Metrics; Blazor's ExGrid by
  one script function parameter.
