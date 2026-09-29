# References are outlined in colour while a Formula is edited

*(Decided with the user, 2026-09-29, in a design grilling. The request was Excel's range finder:
"while a Formula is typed, Excel and Google Sheets colour its cells, and name them in colour.")*

While a Formula is being edited, Excel colours each Reference in its text and draws an outline of
the same colour around the cells that Reference names. A user reads a Formula against the Sheet this
way: which argument is which block of cells. ExSheet takes it. The outline is a **Reference
Outline** (`CONTEXT.md`). As with every aid of
[ADR-0051](./0051-formula-entry-completion-point-mode-and-the-formula-bar.md), it is ExGrid
mechanism that a Consumer switches on and supplies the meaning for. The grid does not know what a
Formula is.

## What shows, and when

- **Every Reference in the Formula being edited has a colour.** Its text wears that colour in the
  Cell Editor and in the Formula Bar. Its cells on the grid are outlined in the same colour.
- **The outlines show in every editing state while a Formula is open**: Overwrite, Caret and Point.
  They show however the edit was opened: typed onto a cell, F2, a double click, or a click into the
  Formula Bar. They go when the edit commits or is cancelled. Selecting a cell that holds a Formula,
  without editing it, shows none. Neither does text that is not a Formula.
- **Point's outline is the Reference Outline of the Reference it is writing.** It takes that
  Reference's colour. What Excel draws differently for it, a dashed or moving line, is a reading
  (below).
- **The outline is drawn in the selection overlay**
  ([ADR-0008](./0008-selection-is-painted-by-an-overlay.md)) as a range is drawn there: one element
  per outlined range, never a class on each cell, and cut to the painted rows as a range is. A
  Reference none of whose cells are painted draws nothing, and the grid never scrolls to show a
  Reference.

## Who decides what

- **What is a Reference, and which cells it names, is the Consumer's answer.** The Consumer supplies
  a synchronous function over the editor's text. It answers each Reference in the text as a span
  (start and length), together with **either** the cells it names on this grid, **or** a key naming
  something this grid does not hold (a Linked Table's column, below). ExSheet answers it from its
  parser. The parser has to answer for a Formula that is not finished (`=SUM(A1,`), because a
  Formula is unfinished for as long as it is being typed. Without the function, nothing changes.
- **The colours are the core's.** References that name the same cells, or carry the same key,
  share a colour. The colours are handed out in order of first appearance, round a palette of fixed
  length. The palette's length is behaviour, because it decides which References share a colour, so
  it lives in C#. The colours are appearance: Visual Tokens `--ex-reference-1` to `--ex-reference-N`,
  read by the stylesheet
  ([ADR-0027](./0027-appearance-travels-in-css-geometry-travels-in-csharp.md),
  [ADR-0029](./0029-the-presentation-surface-is-a-short-list-of-classes-and-tokens.md)). **A Consumer
  never picks a colour.**
- **The core tells the Consumer which colour each key was given**, whenever that changes. When the
  edit ends, the list it tells is empty. A Reference that names cells on this grid needs no telling,
  because the core outlines it itself.

## A structured reference is outlined by whoever shows its table

`SUM(Positions[PV])` reads a **Linked Table**. ExSheet holds only the snapshot its Consumer pushed.
It does not know where, or whether, the table is on screen, and it never reads another component
instance ([ADR-0049](./0049-linked-tables-are-the-consumers-data-read-by-key.md),
[ADR-0018](./0018-multiple-instances-must-be-independent.md)). So ExSheet cannot draw the outline.
The Consumer can.

- **The text of a structured reference is coloured like any Reference.** ExSheet answers its span
  with a key naming the table and the column.
- **ExSheet tells its Consumer which Linked Table columns the edited Formula reads, and in which
  colour.** This is a notification, in the pattern of the rest of the grid: the grid tells, the
  Consumer acts.
- **ExGrid takes, from any Consumer, a list of columns to outline and the colour for each.** It draws
  a Reference Outline over the column's body, across all its rows. The Consumer passes ExSheet's
  notification to the grid that shows the table. `/sheet` in the DemoHost does this for the
  positions grid beside its Sheet.
- **The grid cannot check that it shows the rows the Formula reads.** A grid filtered to some of the
  table's rows would outline the rows it shows, while the Formula reads all of them. Whether the grid
  shows the table the Formula reads is the Consumer's to vouch for, and only the Consumer can.

### Considered options

- **Colour the text of a structured reference and outline nothing.** This was proposed first, on the
  grounds that ExSheet has nowhere to draw the outline. It was rejected when the user asked whether
  that was really so. It is true of ExSheet alone. It is not true of the application, which has the
  table on screen, and the notification lets the application do it.
- **Leave structured references uncoloured.** This was rejected as the same mistake: the Consumer
  can make the colour mean what it means in Excel.

## The coloured text is a layer that shows only while it is up to date

An `<input>` cannot colour part of its text. Both editor surfaces, under both Chromes, are
`<input>`s. On a Server circuit, the answer "where are the References" also arrives a round trip
after the keystroke that asked for it. Colouring text the user has typed past would put the colours
on the wrong characters. That is SRV-7's failure (`…123456789` became `…1289`) in another form.

- **Beneath each editor surface, a layer draws the same text in the same font, with each Reference
  in its colour.** The core supplies the layer. A Chrome places it behind its field, as it places
  the field itself, because a Chrome may not bring script of its own
  ([ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md), Consequences).
- **The field's own text becomes transparent only while the layer's text equals the field's
  value.** The caret stays visible. While the field is ahead of the layer (typed past, or holding an
  IME composition), the layer is hidden and the field's own uncoloured text shows. On WebAssembly
  the colours follow each keystroke. On a circuit behind 150 ms, they go while the user types fast
  and come back when the user pauses. **Uncoloured for a moment is chosen over coloured on the wrong
  characters.**
- **Text selected in the field stays readable.** The field's selection colour is set so that the
  selected text is drawn by the field, not left transparent.
- **Two additions to the editor listener that ADR-0021 already allows**, neither of which reads
  layout:
  - On each input, and when the layer's text changes, the listener compares the two texts and sets
    or clears one class. It notices the layer's change through a `MutationObserver` on one attribute
    of the layer, as the reveal's write waits on one attribute of the root.
  - It keeps the layer's horizontal scroll equal to the field's, reading the field's `scrollLeft` and
    setting the layer's. That is the scroll-offset entry's API, used for one more element.
- **The ground for the JavaScript is ADR-0021's first: technically required.** Blazor cannot know
  that the browser's value is ahead of the value the server rendered for. Only the browser holds
  both.

### Considered options

- **A `contenteditable` editor, as Excel on the web and Google Sheets use.** It colours the text
  where it is typed. It was rejected. Blazor rewriting the children of an element the browser is
  editing fights the caret and IME composition. The `@bind` rule that fixed SRV-7 cannot hold there.
  The listener, which reads `value` and `selectionStart`, and both Chromes' editors would all have to
  be rewritten.
- **Colour in JavaScript, from a tokenizer of its own.** It colours with no round trip. It was
  rejected because a second grammar that drifts from ExSheet's colours text that is not a Reference,
  and nothing shows the drift. The grammar is ExSheet's, in C#
  ([ADR-0047](./0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md),
  ADR-0051).
- **No coloured text: the outlines alone, with colour swatches in the argument hint.** It was
  rejected because the user asked for the text coloured, as Excel does. Without it, the reader has
  to match an outline to an argument by position.

## Not done

- **Dragging an outline, or its corner, to rewrite the Reference.** In Excel, dragging a Reference
  Outline's edge moves it (`A1:A5` becomes `C1:C5`), and dragging its corner resizes it. It is a new
  pointer gesture. It would compete with the fill handle and with a selecting drag for the same
  presses, and it needs a decision of its own.
- **Telling Reference Outlines apart under forced colours.** Forced colours replace every colour
  with a system colour, so all the outlines, and all the coloured text, come out alike. The outlines
  still show which cells are read, but not which argument reads them. Line styles could tell three or
  four apart, not a palette. Excel was not checked under high contrast.
- **A design system's palette.** `ExGrid.MudBlazor` leaves `--ex-reference-*` at the core's
  defaults ([ADR-0030](./0030-what-a-design-system-wrapper-owns-and-what-it-may-not-touch.md)).
  The colours only tell References apart; they are not a brand. Mapped onto a design system's error
  or warning colour, a Reference would read as a state.

## Readings, until the eighth Windows run observes them

The rules above are what Excel is seen to do. The edges below are read, taken for the
implementation, and asked of Excel in `docs/specs/exsheet/verify-on-windows-8.md`. A reading that
Excel contradicts is corrected here after the run.

- **The palette** has eight colours. Its length, order and colours are taken from Excel in the run.
  Until then, the defaults are provisional.
- **The same cells share a colour however they are written**: `=A1+A1` and `=A1+$A$1` each outline
  A1 once, in one colour. `=B2:A1` outlines A1:B2.
- **`Sheet1!A1`, with this Sheet's own name, is outlined.** A Reference qualified with another name
  names no cells (it is `#REF!`, ADR-0046) and is not coloured.
- **A whole column or row** (`A:A`, `1:1`) is outlined across the whole of it.
- **An unfinished Formula is coloured as far as it goes.** `=SUM(A1,` colours `A1`. Nothing inside a
  string, and no function name, is coloured.
- **A range typed up to its colon colours nothing** until its second corner is typed: `=SUM(A1:`
  leaves `A1` uncoloured, because the grammar reads no Reference in `A1:`. *(Taken while building
  ticket 27.)*
- **Only text beginning with `=` is a Formula while it is typed**, as for F4 and Point. `+A1` and
  `-B2` colour nothing, although ExSheet enters them as `=+A1` and `=-B2`. *(Taken while building
  ticket 27.)*
- **Point's outline is dashed**, in its Reference's colour. Every other outline is solid, over a pale
  wash of its colour.
