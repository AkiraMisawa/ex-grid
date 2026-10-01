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
  surface the edit is in, the Cell Editor or the Formula Bar. Its cells on the grid are outlined in
  the same colour. *(Corrected 2026-09-30 by the eighth Windows run. This bullet first said the text
  was coloured in both surfaces at once; Excel colours only the one being edited. See "What the eighth
  Windows run settled".)*
- **The outlines show in every editing state while a Formula is open**: Overwrite, Caret and Point.
  They show however the edit was opened: typed onto a cell, F2, a double click, or a click into the
  Formula Bar. They go when the edit commits or is cancelled. Selecting a cell that holds a Formula,
  without editing it, shows none. Neither does text that is not a Formula.
- **Point's outline is the Reference Outline of the Reference it is writing.** It takes that
  Reference's colour. What Excel draws differently for it, a dashed or moving line, is a reading
  (below). *(Settled by the eighth Windows run: the outline is drawn as any other, and a dashed line
  in the Focus outline's colour is laid over it.)*
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
  - **The corner squares wait for it** *(decided with the user, 2026-09-30)*. Excel draws a 5×5 px
    square in the Reference's colour at each corner of every outline, with a 1 px margin of white
    (the eighth Windows run). They are this gesture's handles. Drawn without the gesture, they would
    offer a drag that does nothing. So they are not drawn, and they arrive with the gesture.
  - **The gesture is kept in view** in `docs/definition-of-done.md` §21.11 and in ticket 31 of
    `docs/specs/exsheet/issues/`, which holds what the eighth run saw of the squares. Whoever builds
    the gesture builds the squares with it.
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
  *(Excel has seven: see "What the eighth Windows run settled".)*
  Until then, the defaults are provisional.
- **The same cells share a colour however they are written**: `=A1+A1` and `=A1+$A$1` each outline
  A1 once, in one colour. `=B2:A1` outlines A1:B2.
- **`Sheet1!A1`, with this Sheet's own name, is outlined.** A Reference qualified with another name
  names no cells (it is `#REF!`, ADR-0046) and is not coloured.
- **A structured reference is coloured only when its table and column are declared** *(decided
  with the user the same day)*. `Nope[PV]` and `Positions[Nope]` name nothing, as a Reference to
  another Sheet names nothing, and are not coloured: a colour would say they read something, where
  they read `#NAME?` or `#REF!`. Nothing about them reaches the Consumer either. A declared table
  still waiting for its data (`#GETTING_DATA`) is coloured, because the column it names exists.
- **A whole column or row** (`A:A`, `1:1`) is outlined across the whole of it.
- **An unfinished Formula is coloured as far as it goes.** `=SUM(A1,` colours `A1`. Nothing inside a
  string, and no function name, is coloured.
- **A range typed up to its colon colours nothing** until its second corner is typed: `=SUM(A1:`
  leaves `A1` uncoloured, because the grammar reads no Reference in `A1:`. *(Taken while building
  ticket 27. Excel colours `A1` there: see "What cases 24–32 settled".)*
- **Only text beginning with `=` is a Formula while it is typed**, as for F4 and Point. `+A1` and
  `-B2` colour nothing, although ExSheet enters them as `=+A1` and `=-B2`. *(Taken while building
  ticket 27. Excel colours both: see "What cases 24–32 settled".)*
- **Point's outline is dashed**, in its Reference's colour. Every other outline is solid, over a pale
  wash of its colour.

## What the eighth Windows run settled *(2026-09-30)*

Part A of `verify-on-windows-8.md` asked Excel (Microsoft 365, Version 2609) every reading above, and
recorded what it draws in `verification/2026-09-29-windows-excel-8/range-finder.md`. Most readings
agreed. Where one did not, the rule is now Excel's, as this ADR said it would be.

- **The palette has seven colours, not eight.** In order of first appearance they are `#326ac7`,
  `#c0353e`, `#8157b7`, `#007c20`, `#b03e84`, `#b64900` and `#267392`, and then the order starts
  again. An outline's line and its Reference's text wear the colour. Its fill is a pale wash of it
  (`#ebf0f9` for the first). These are the defaults of `--ex-reference-1` to `--ex-reference-7`, and
  the palette's length in C# is seven.
- **The text is coloured only in the surface the edit is in.** While the edit is in the cell, the
  Formula Bar's text stays plain. While it is in the Formula Bar, the cell's text stays plain. Both
  surfaces carry the layer, because either can hold the edit.
- **Point's outline is its Reference's outline, with the Focus outline's dashes laid over it.** Excel
  draws the pointed Reference's outline as it draws any other: the line and the fill in the
  Reference's colour. Over the line it lays a dashed line in the active cell's border colour
  (`#217346`). So the dashes take `--ex-focus-outline`, not the Reference's colour. The dashes stand
  still. This corrects the reading "Point's outline is dashed, in its Reference's colour".
- **Each Reference draws its own outline**, even where another names the same cells. `=A1+A1` puts
  one outline in one colour over A1, but draws it twice, so the fill is laid twice and reads darker
  (`#d9e2f4` against `#ebf0f9`). Two outlines that overlap lay their fills together in the same way.
  The rule that the same cells share one colour stands.
- **Where two outlines meet, the one drawn later covers the shared edge.**
- **Point starts again from the edited cell after an operator.** Case 20 of the procedure pointed at
  D11 twice for that reason. It was the procedure's mistake, not Excel's. With the keys corrected
  (case `20x`), Excel agrees with the reading.

### Where ExSheet is deliberately unlike Excel *(decided with the user, 2026-09-30)*

- **The outline's line lies inside the cells it outlines.** Excel draws the line over the gridline
  and 1 px outside the cells, so in row 1 and in column A it goes under the headings' border. Here
  it lies inside, as the pointing outline already did. Nothing drawn above the overlay (the
  headings, a Pinned Column, the header) can then cover part of it. Inside the line, a 1 px gap of
  the cell's own ground separates it from the wash, as Excel's white gap does.
- **One palette serves both surfaces.** Excel's Formula Bar shows the seven colours in slightly
  different shades from the cell's (`#006cbe` for `#326ac7`). The shades name the same seven
  References, and a second set of tokens would buy nothing a reader can use.
- **A column the Consumer asks to outline twice is outlined twice** (`OutlinedColumns`). Two Sheets
  on a page can both read `Positions[PV]` in different colours, and a Consumer that passes both
  notifications to the positions grid has done nothing wrong. Both outlines are drawn, the later
  over the earlier, as two References to the same cells are. A null entry is still refused, since
  that is a mistake in the Consumer's code. A name the grid does not show is outlined nowhere, as a
  Reference to cells the grid does not have is.

## What cases 24–32 settled *(2026-09-30)*

The eighth run was extended with cases 24–32, asked of the same Excel. They are recorded in the same
file.

- **Text beginning with `+` or `-` is coloured as a Formula is** (cases 24, 25). `+A1` colours and
  outlines A1, and `-B2` does the same for B2, as they would after `=`. ExSheet enters such text as
  `=+A1` and `=-B2` already, so colouring it is consistent. This corrects the reading "Only text
  beginning with `=` is a Formula while it is typed". F4 and Point still act only after `=`
  (ADR-0051). Whether Excel points or cycles after a leading sign was not asked.
- **A range typed up to its colon colours its first corner** (case 26). `=SUM(A1:` colours and
  outlines A1, and leaves the colon plain. This corrects the reading that it colours nothing. The same
  holds, as a reading, while the second corner is still incomplete (`=SUM(A1:B`).
- **A table or a column that is not there is not coloured** (cases 27, 28), as the reading said.
- **The Reference Point is writing is shown selected, unless it follows the Formula's `=` directly**
  (cases 19, 20, `20x`, 29–32). Its text lies on a grey ground (`#c6c6c6`), in a darker shade of its
  colour (`#0401a2` for the first, `#630101` for the second). The grey shows for `=SUM(D11`,
  `=1+D11` and `=D11+D12`, and not for `=D11` however often it was pointed. It shows only in the
  surface the edit is in. *(Built, decided with the user: ADR-0051's Point gains this look.)*
- **The grey is not a selection that typing replaces** (case 32). A `5` typed after pointing at D12
  gives `=D11+D125`, and Point ends, as ADR-0051 has it: the digit follows the Reference. Excel's
  Formula Bar reports the grey span as its selection through UI Automation, but a keystroke does not
  replace it. So ExSheet paints the grey as a look on the layer, and never selects the field's text.

## Settled while building the coloured text *(2026-09-30, decided with the user)*

- **Which surface shows the colours is read from DOM focus**, in the listener, rather than from the
  core's record of where the edit was opened. The core does not always hear a press that moves the
  edit between the cell and the Formula Bar in time: a press into the bar can pass without a render,
  and a press back into the cell is not heard until the next key. A Chrome's editor has no focus
  callback either. DOM focus is what the listener already uses to send held keys to the right
  surface (ADR-0051), and it follows every move at once.
- **A composition's end brings the colours back.** Every `input` of an IME composition is marked as
  composing, and none follows its `compositionend`. Without a listener for that event, the colours
  would stay off after each committed composition until the next key, which is every Japanese word
  typed into a string. The editor listener therefore also hears `compositionend` on the root while an
  edit is open (ADR-0021's note).
- **The layer drifts by a fraction of a pixel over many References, and that is accepted.** The layer
  draws each span as its own run of text, and the browser rounds each run's width to its layout
  unit. The field's text is one run. At the far end of a Formula the layer is about 0.1 px out with
  ten References, 0.6 px with forty and 1.3 px with eighty (measured 2026-09-30). That is never a
  character, and below twenty References it does not show. If long Formulas show it, the fix is to
  draw the layer as one run and colour it with the CSS Custom Highlight API, which needs script of its
  own and a decision.
- **Two moments leave the colours a keystroke behind, and both are accepted.**
  - Focus moved mid-edit to something that is not a text field (a button in a Consumer's cell) leaves
    the previous field coloured until the next key or caret move. The colours are still over the
    right characters.
  - A Cell Editor recreated because its cell scrolled out of the painted rows and back starts plain
    until the next key.

  Both err towards plain text or stale placement of correct colours. Neither can put a colour on the
  wrong characters.

## The Wrapper's shape is required for the coloured text under `ExGrid.MudBlazor` *(2026-09-30, decided with the user)*

The layer relies on its Chrome's field having no padding and no background, and on the field
filling the core's box, as the Chrome contract on `CellEditorContext.ReferenceText` says.
`ExGrid.MudBlazor`'s fields meet that contract through `mud-ex-grid.css`, whose rules are all scoped
under `.mud-ex-grid` (ADR-0030). `/sheet?chrome=mud` loaded neither the stylesheet nor
`MudExGridPaper`. Its Mud fields were bare browser inputs, 154 px wide, on an opaque white ground.
The layer sat beneath that ground, so while a Formula was edited the Mud Cell Editor and Formula
Bar showed no text at all.

- **The Wrapper's documented shape, `MudExGridPaper` with `mud-ex-grid.css`, is the supported way
  to use `ExGrid.MudBlazor`'s Chrome where References are coloured.** `/sheet?chrome=mud` uses it.
  The Chrome used alone, without the Wrapper, is not supported with coloured text.
- **The core keeps a mistake from hiding the text.** While a field's layer shows, the core's own
  stylesheet makes that field's ground transparent, so a page that forgets the Wrapper's stylesheet
  still shows the text: the layer's, in colour. What such a page can still get wrong is where the
  layer's text lies, if its field is narrower than the core's box. That is a mistake in the page's
  setup, and it shows in plain sight.

## What Part B of the eighth Windows run settled *(2026-09-30)*

Part B of `docs/specs/exsheet/verify-on-windows-8.md` typed every case of Part A into ExSheet on
`/sheet`, under Chrome and Edge, on both hosts and behind 150 ms, and compared what it drew with
Excel. It is recorded in `verification/2026-09-30-windows-8/reference-outlines.md`. All six
configurations drew the same.

- **What agreed**: the seven colours in order of first appearance and round again, a colour shared
  by the same cells, `Sheet1!A1`, nothing in a string or on a function name, whole columns and rows,
  the Linked Table's columns outlined in the positions grid in the text's colour, nothing for an
  undeclared table or column, `+A1`, `-B2` and `=SUM(A1:`, the colours only in the surface the edit
  is in, the grey on the pointed Reference except straight after `=`, and `=D11+D125`. Typed as fast
  as the run's driver sends keys behind 150 ms, no frame showed coloured text over characters it did
  not belong to (DC-47).
- **The dashes over Point's outline are black on `/sheet`, and green in Excel.** They take the Focus
  outline's colour, as decided. `/sheet` sets none, so it is `CanvasText`; Excel's active cell is
  green. The rule stands.
- **The fills differ from Excel's by a unit or two** (`#eaf0f9` against `#ebf0f9`). Excel's own two
  runs read both values for the same fill. This is taken as rounding, and nothing changes.
- **The pointed Reference's text is Excel's dark shade of its colour** *(decided with the user)*. The
  shade was approximated by mixing the colour 55% toward black, which gave `#1b3a6d` and `#6a1d22`
  where Excel draws `#0401a2` and `#630101`. Each place in the palette now has a Visual Token for its
  pointed shade, and a Theme may set all seven. Their defaults are Excel's: `#0401a2` and `#630101`,
  and, as the tenth Windows run read them (`verification/2026-09-30-windows-excel-10/`, group 2),
  `#44007c`, `#003600`, `#550059`, `#531c00` and `#00323f`. Over a dark ground each is the colour mixed
  toward white: Excel keeps its cells white under its Black theme, with the same seven shades, so it
  has no dark ground to take them from. The tenth run also saw Excel leave the grey off `=1+D11` in
  4 passes of 5 when ↓ came 30 ms after the `+`, and always show it after a second's wait. That is
  taken as Excel's timing, and ExSheet shows the grey every time.
- **Seen with no Excel reading to compare**:
  - In the Formula Bar, ↓ after `=` does not point. Excel's bar does not either (2026-09-27, item 12).
  - **In the Formula Bar, `Home` committed the Formula and moved the Focus.** F2 had taken the bar's
    edit to Overwrite, where `Home` moves between cells. This was first taken for a defect. The tenth
    Windows run showed that Excel's F2 takes its bar to Enter, and that `Home` then commits and moves
    too, so it is Excel's behaviour. ADR-0051's note of the same day records it, with the two
    defects the case led to (typing, and an edit carried into the bar, left the bar in Overwrite).
  - A Reference whose range crosses the Pinned Columns is drawn as two elements, one in each layer,
    as a selected range is ([ADR-0008](./0008-selection-is-painted-by-an-overlay.md)). DC-46 said
    "one element per Reference". It now says one per layer the Reference crosses, never per cell
    *(decided with the user)*.
