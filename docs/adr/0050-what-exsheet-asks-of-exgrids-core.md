# What ExSheet asks of ExGrid's core — five declarations, each opt-in

*(Decided with the user, 2026-09-27, in the design grilling that started ExSheet —
[ADR-0046](./0046-exsheet-is-a-general-purpose-sheet-drawn-by-exgrid-as-its-consumer.md). Formula
entry asks for more, recorded separately in
[ADR-0051](./0051-formula-entry-completion-point-mode-and-the-formula-bar.md).)*

ExSheet draws a Sheet by being ExGrid's Consumer, and most of Excel's behaviour comes with that
for free. Five things do not. ExGrid made the opposite choice in each, and made it for a display
grid on purpose. **Each one enters the core as a declaration a Consumer makes.** A Consumer who
does not make it sees the grid exactly as before, so no existing criterion changes. Each change
has to be right for any Consumer that makes the declaration, ExSheet or not. *(A sixth was added
on 2026-09-29, and it is a notification rather than a declaration: the core tells its Consumer when
an edit opens and ends (section 6). A Consumer that does not listen still sees the grid exactly as
before. The title keeps its first count.)*

## 1. A header click that selects, and Headings

[ADR-0012](./0012-anchor-focus-and-keyboard-navigation.md) gave the header click to sorting
("the data-grid convention is adopted; Excel's is not"), and broke the tie by what happens first
on a data screen. On a Sheet, ExGrid's sort is not used at all (ADR-0046), and Excel's click is
the only meaning available.

- **A Consumer can declare that a header click selects the column.** A plain click selects the
  whole column, and Shift+click extends from the Anchor's column, as the existing Shift+click
  does. Nothing sorts.
- **Row Headings: a band beside the rows, outside the column index space.** It is painted per
  row, with a label the Consumer supplies (the row number). A click selects the whole row, and
  Shift+click extends. The corner where the two Headings meet selects all.
  **It is not a column.** A column would sit inside Selection, copy, Ctrl+A and the Enter/Tab
  cycle, so every one of those would have to learn to skip it. The header band already stands
  outside the row index space in the same way. The band is pinned, and its width is geometry
  resolved in C# ([ADR-0028](./0028-geometry-is-resolved-once-density-is-only-a-preset.md)).
- **Either Heading can be hidden.** Hiding the column header takes the header band away. That is
  also useful to an ExGrid Consumer drawing a headerless list, which today has no way to say so.
  Whole columns stay reachable with Ctrl+Space.

*(Added 2026-09-29, decided with the user after comparing the Headings with Excel — Microsoft 365
on Windows — and Google Sheets by hand. The first build left the drag out: "Dragging across Row
Headings or column headers to select several is not built; Shift+click is the route", ticket 06.
Ctrl+click was not mentioned at all. Both are Excel's, and both were observed.)*

- **A drag across Headings selects whole columns or rows.** The press selects the column or row, as
  a click does, with the Focus on the first visible row or column
  ([ADR-0052](./0052-the-focus-is-excels-active-cell-and-the-extent-is-the-moving-end.md)); each
  move then puts the Extent on the column or row under the pointer, and the range is whole columns
  (whole rows) from the pressed one to it. **The pointer leaving the Heading band for the cells
  keeps it a Heading drag**: only the pointer's column (row) counts, whatever row (column) it is
  over, as Excel does. **The edge band auto-scrolls along the Heading's axis only**
  ([ADR-0008](./0008-selection-is-painted-by-an-overlay.md)): sideways for Column Headings, down
  and up for Row Headings. Column Headings sit at the Viewport's top edge, and an auto-scroll that
  moved rows there would scroll the Sheet under a gesture that is about columns.
  **Shift+press** extends from the Focus's column (row) to the pressed one, as Shift+click does,
  and the drag goes on moving the Extent; the Focus stays. While the drag covers more than one
  column or row, its size shows in the Size Tip (ADR-0052, 2026-09-29).
- **Ctrl+click on a Heading adds or takes out a whole column or row.** On a column not wholly
  selected, it adds the whole column as a new range, with the Focus on its first visible row. On a
  column already wholly selected, it takes the column out, and the Focus follows ADR-0052's
  take-out rule. **Ctrl+drag** across Headings adds whole columns (rows) from the pressed one to the
  pointer's as one new range. Meta counts as Ctrl where Meta is Command, as it does on cells
  ([ADR-0012](./0012-anchor-focus-and-keyboard-navigation.md)).
- **Both hold for a plain ExGrid too, on its column header** (decided with the user), with one
  difference that follows from the header's click sorting there. See ADR-0012, "Added later".

## 2. Ctrl+arrow asks where the data ends

In ExGrid, Ctrl+arrow goes to the edge of the grid. The grid does not hold the data, so it cannot
know where a block of values ends. In Excel, Ctrl+arrow stops at the edge of the current block of
non-blank cells, and Excel users use it constantly.

- **A Consumer can supply an edge answer**: given a cell and a direction, the cell where Ctrl+arrow
  stops. ExGrid asks it on Ctrl+arrow and on Ctrl+Shift+arrow, and moves the Focus or extends the
  range to the answer. It is a synchronous question to in-process C#, so on a Server circuit it
  costs no round trip; the answer lives on the same side as the key handling.
- **Without it, nothing changes**: the edge of the grid, as ADR-0012 has it.

## 3. A paste may spill, and its range becomes the Selection

[ADR-0014](./0014-paste-shape-rules-and-selection-count.md) refuses "range → one cell". That paste
writes to rows nobody selected, can spill outside the Window, and would make the displayed count
disagree with the cells written. On a Sheet it is the most common paste there is.

- **A Consumer can declare that a paste may spill.** A source of m×n pasted onto a single cell
  writes the m×n block with that cell at its top-left, as Excel does.
- **After a spilled paste, the pasted block is the Selection**, with the Anchor at its top-left.
  Excel does the same. It is also what answers ADR-0014's third objection: the count on display
  is the count written.
- **A Consumer can refuse a spilled paste** *(2026-09-27, after the second Windows run)*.
  `GridPasteIntent.Refuse()` mirrors `GridFillIntent.Refuse()` (item 7): the grid then leaves the
  Selection where it was, as Excel does. Before it, the grid selected the unwritten block after
  ExSheet had refused the paste by name.
- **A spill past the grid's extent is refused by name.** On a Sheet that extent is Excel's, so the
  refusal only comes at the Sheet's own edge. Every other shape rule of ADR-0014 stands, and
  [ADR-0035](./0035-paste-and-fill-respect-the-editable-declaration.md)'s `Editable` gate is
  checked against the spilled block, before anything is written.
- **Why the declaration is right for ExSheet:** ADR-0014's other two objections rest on the grid not
  holding the data. On a Sheet, the Consumer holds every cell, so nothing lands outside what it
  holds. The spill becomes the Selection, so it is on display. And it is one step on the undo
  stack ([ADR-0048](./0048-a-sheet-document-holds-entries-and-exsheet-holds-the-one-undo-stack.md)).
  A display-grid Consumer does not declare it, and ADR-0014 stands for it unchanged.

## 4. The Consumer can place the Selection and the Focus

The Selection and the Focus are the grid's, and today nothing outside the grid can move them. The
Name Box (ADR-0051) needs to: a user types `D200` and the Focus goes there.

- **A Consumer can ask the grid to select a range and put the Focus in it.** The grid then does
  what it does after a click. It scrolls the Focus into view and announces the new extent
  ([ADR-0033](./0033-the-accessibility-surface-is-owned-by-the-root-not-by-cells.md)).
- **The Selection stays the grid's.** The Consumer asks, and the grid places. A Held Selection is
  still reconciled against the Row Sequence Version
  ([ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)).
  A request made under an older version is dropped, because it names positions that no longer
  mean what they did.

## 5. The fill handle: the gesture is the core's, the meaning is the Consumer's

The fill handle was reserved with nothing settled. [ADR-0008](./0008-selection-is-painted-by-an-overlay.md)
reserved its painting, and the Definition of Done's §21 reserved "neither the gesture nor the fill
semantics". This settles it in the shape ADR-0007 gave editing: **the grid reports the intent, and
the Consumer decides what it means.**

- **The core paints the handle** at the bottom-right corner of the Selection's last range, in the
  selection overlay, when the Consumer declares that fill is enabled. **The core owns the drag.** It
  extends along one axis, like Excel, and paints the target outline. *(Observed 2026-09-29: a
  diagonal drag from A1 towards C3 fills one axis only, in Excel — Microsoft 365 on Windows — and in
  Google Sheets alike. Filling down and across takes two drags, as it does here. A two-axis fill was
  asked for and dropped: it is not Excel's, and a series filled both ways would depend on which axis
  went first.)*
- **On release the grid raises a Fill Intent**: the source range, the target range, and the
  direction. It writes nothing. `Editable` is checked on the target first (ADR-0035). The only
  shape is one rectangle extended along one axis, and a disjoint Selection shows no handle, as in
  Excel.
- **The meaning is the Consumer's.** ExSheet fills as Excel fills. Every rule it implements matches
  Excel's result; **a pattern it has not implemented is refused, not filled with copies.** Excel
  would have continued a series there, and a column of copies is a plausible, wrong series. The
  first rules are: copy, with relative References shifted; a linear series from two or more selected
  numbers; a series of dates by day. `Item 1` → `Item 2` and the rest of Excel's patterns come
  later, and each is refused until it arrives.
- A plain ExGrid Consumer can take Fill Intents too, and resolve them into its Overlay.

*(Added 2026-09-28, when `main`'s fill keys were merged — [ADR-0035](./0035-paste-and-fill-respect-the-editable-declaration.md)'s
Ctrl+D and Ctrl+R, decided with the user.)* The keys stay a paste intent in the core, as `main`
built them: the grid reads the source's raw values and raises one `GridPasteIntent`. That intent
alone could not tell a Consumer that holds more than values — ExSheet, whose cells hold Formulas —
a fill by key from a paste of the same text, so **the intent names the range a fill key read, in
`GridPasteIntent.FillSource`**, and null for a paste from the clipboard and for Ctrl+Enter's typed
text. ExSheet copies that range's Entries over the target with relative References shifted, formats
and alignment with them, as Excel's Ctrl+D and Ctrl+R do and as the handle's copy rule does
(`SheetEdit.FillCopy`, the handle's rule with no series): a date or a number is copied, never
continued, and no pattern is refused. It also answers `OnCopyRowsNeeded` from the Sheet, so a source
row scrolled out of the Window is still read.

*(Added 2026-09-28, after the third Windows run, decided with the user.)* **Ctrl+Enter with a
Formula shifts its References, as Excel does**: `=A1` entered over B2:C3 from B2 writes `=A1`,
`=B1`, `=A2` and `=B2`. The typed text is read as entered in the Focus, and each other cell takes
it with its relative References moved by that cell's offset from the Focus, by the same rule as
`SheetEdit.FillCopy`. The intent still names no fill source: the source is the typed text, not a
range. **The intent names the cell the text was entered in, in `GridPasteIntent.EnteredAt`**, and
null for a paste from the clipboard and for a fill key: a Ctrl+Enter over a range and a clipboard
paste of one field over the same range were otherwise the same intent, field for field, and a
Consumer could not tell a typed Formula from a pasted one. *(Decided with the user, 2026-09-28,
when the implementation found them indistinguishable.)*

## 6. The core tells its Consumer when an edit opens and when it ends *(decided with the user, 2026-09-29)*

ExSheet refuses its application's changes while an edit is open
([ADR-0048](./0048-a-sheet-document-holds-entries-and-exsheet-holds-the-one-undo-stack.md), same
day), so it has to know when one is. **The grid raises a notification when an edit opens and when it
ends**, however it ends: committed, cancelled, refused and held open (which is still open), or
discarded ([ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)).
It carries no text: the uncommitted text stays the grid's
([ADR-0007](./0007-edits-are-an-overlay-owned-by-the-consumer.md)). It is raised in C#, on the
side where the editing state lives, so no round trip stands between the state and the Consumer's
reading of it. A plain ExGrid Consumer may listen too, for its own buttons.
- *One end is heard late (settled while building, the same day).* A discard caused by a parameter
  change is announced after the render that applied the change, as `OnEditDiscarded` already is, so
  on a circuit the Consumer hears that end a round trip late. It errs toward refusing: for that
  render, the Consumer still believes an edit is open. It never hears an opening late.
- *The order, settled by the full browser run the same day.* Telling the Consumer lets it
  re-render, and on a circuit that render could reach the browser before the grid's own messages:
  the editor was removed while it still held the keyboard, and the Formula Bar lost it. So the grid
  sends its own messages to the browser first (the key gate's new mode, the hand-back of the
  keyboard), then tells the Consumer, and only then waits for replies. What the edit ended in (the
  committed Edit Intent, a Ctrl+Enter fill's paste, a discard's reason) reaches the Consumer before
  the end does, so that a command the Consumer runs on hearing the end comes after the value, never
  before it. Nothing is waited for in between, so the Consumer still hears without a round trip.
- *An edit that ends waits for no reply (settled by CI the same day).* The order above first waited
  for the browser's replies after telling the Consumer. For an edit that ended, that wait put the
  rest of the gesture, the Focus move and the key's answer, a round trip behind the committed value
  the Consumer had already painted. On the Server host, the Focus stayed on the edited cell, and a
  Ctrl+C typed on seeing the value was held behind the unanswered key and lost (ED-2/ED-4 and
  CP-6/10/14 failed intermittently in CI; 22 of 50 runs failed with a 40 ms round trip injected, and
  none after this change). The wait ordered nothing, because the browser runs the requests in the
  order they were sent, ahead of the render and the answer that follow them. So an edit that ends
  waits for no reply. An edit that opens, or changes its mode and stays open, still waits for the
  gate's reply, and DC-19 depends on that.
- *Disposal is not announced.* A grid removed while an edit is open raises nothing, because the
  Consumer that removed it already knows. Raising into a Consumer that may itself be tearing down
  would be worse than silence.

**A Consumer can also discard an open edit, giving its own reason** *(decided with the user the same
day)*. The discard is announced through `OnEditDiscarded` like the grid's own discards (ADR-0011), and
the reason is the Consumer's, so it is true of what happened. ExSheet uses it when its Sheet Document
is replaced while an edit is open (ADR-0048).

## Consequences

- **ADR-0012, ADR-0014 and ADR-0008 each gain a note** saying their rule stands, and which
  declaration here lets a Consumer take Excel's behaviour instead.
- **§21's two rows are settled.** "The fill handle" is settled by item 5, and "whether ExSheet is a
  sibling or a Consumer" by ADR-0046.
- **Each declaration gets criteria in the Definition of Done**, in ExGrid's sections, because they
  are ExGrid features that any Consumer can use.
- **No new JavaScript.** The drag, the header click, the Headings and the edge answer all run on
  events the grid already listens to ([ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md)).

## Added while building *(2026-09-27, decided with the user)*

**6. A per-cell kind.** A Consumer can supply a cell's `ColumnType` per cell, overriding the
column's. Alignment, the numeric class, the `####` decision
([ADR-0016](./0016-column-width-and-overflow.md)) and the default format then follow the cell.
Sorting and filtering stay per column. Without the declaration, the column's kind decides, as
before. ADR-0046 says why a Sheet needs it.

**Item 5, refined: after a fill, the Selection is the source and the target together**, as in
Excel. The Consumer may still refuse the pattern, and then the Selection stays on the source.

## Added while building, second round *(2026-09-27, decided with the user)*

**7. A per-cell alignment.** A Consumer can supply a cell's `CellAlign`, beside its kind (item 6).
Without it, the column's alignment and then the kind's default decide, as before. A Sheet needs
it for two things: a user's own alignment, and Excel's centring of booleans and Error Values.

**8. Undo and redo reach the Consumer.** [ADR-0007](./0007-edits-are-an-overlay-owned-by-the-consumer.md)
says the grid forwards Ctrl+Z, but no route existed: the key listener never claimed it.
*(2026-09-28: `main` reached the same gap and settled it in ADR-0007's section "The forwarding this
ADR promised was never wired". When the two branches were merged, the user made that section the
one definition: `OnUndo` and `OnRedo` carry no payload, undo is Ctrl+Z and redo Ctrl+Y and
Ctrl+Shift+Z, each is claimed only while it has a listener, and never while editing. The bullets
below say the same thing, and ADR-0007 is the authority where they differ. ExSheet's undo stack is
one such listener.)*
- A Consumer can declare undo and redo callbacks. While no edit is open, the core then claims
  Ctrl+Z, Ctrl+Y and Ctrl+Shift+Z and raises them.
- While an edit is open, the keys stay the editor's own, which undo uncommitted typing (ADR-0007's
  last bullet).
- Without the declaration, the keys stay the browser's.

## Added while building, third round *(2026-09-27, decided with the user)*

**9. The Consumer may answer a copy.** A Consumer can declare a synchronous copy answer. On
both copy routes the grid asks it, with the range being copied. The answer is either the text and
HTML flavours to write, or a refusal carrying the Consumer's own sentence.
- Without the declaration, the grid builds the copy itself, as
  [ADR-0005](./0005-copy-refuses-rather-than-truncates.md) says.
- ExSheet uses it for three things: to carry Entries inside itself, to refuse a copy that reaches
  `#GETTING_DATA` ([ADR-0049](./0049-linked-tables-are-the-consumers-data-read-by-key.md)), and to
  write Values exactly as the engine gives them.
- **ExSheet recognises its own copy by content.** It carries Entries only when the pasted block
  equals, field for field, the block it last copied. A browser clipboard names no owner, so equal
  content is the strongest evidence a page has. A false match would need another program to have
  put exactly that block there, and even then it pastes the Entries that showed exactly those
  Values.

**10. A paste says where each field came from.** The paste notification marks each field as an
invariant number (Excel's `x:num` attribute, or ExGrid's own unformatted HTML) or as shown text.
ExSheet reads an invariant number as a number, and reads shown text as typed under the Sheet's
culture. Otherwise `1234.5` from Excel would be misread under `de-DE`.

**A paste over whole columns is not capped.** It is executed and measured (SH-19). Principle 5
puts caps only on what cannot be executed.

## Added while building, fourth round *(2026-09-27, decided with the user)*

**Item 10, refined.** A field is marked **invariant** when it is a value in a form no culture
changes: a number, an ISO date, or `TRUE`/`FALSE`. Such fields come from Excel's `x:num="…"` or from
ExGrid's own HTML. ExGrid marks its own HTML with `<table data-ex-grid="invariant">`, so a paste can
recognise it. Whether Chrome and Edge keep that attribute on the asynchronous clipboard route is a
layer-3 check. If they strip it, those fields are read as shown text: slower to trust, never
misread as a different number.

**11. The painted text may differ from the value's text.** A Consumer can supply a cell's painted
text, and is given the column's resolved content width and metrics. The grid paints the answer.
Where it differs from the value's own text, the grid uses the value's text as the accessible name,
as it does behind `####` ([ADR-0016](./0016-column-width-and-overflow.md)). ExSheet paints General
fitted to the column this way (ADR-0047). Without the declaration nothing changes.

**12. Resize grips without the column menu.** A Consumer can have the column-width grips painted
without the column-menu button. A Sheet has Excel's Headings, which carry no menu. Size to fit stays
on a double-click of the edge.

**A press into the Name Box over a Formula that cannot be read** is Rejected like any other commit
([ADR-0034](./0034-validation-is-a-consumer-verdict-enforced-only-at-the-editor.md)). The keyboard
goes back to the editor, and whatever is typed next lands in the Formula, where it can be seen.
Nothing typed is thrown away, so this is kept.

**13. The copy-with-headers command can be left out.** A Consumer can declare that the Context
Menu does not offer copy with headers. On a Sheet the column letters are addresses, so ExSheet
declares it by default and offers the command only when its own Consumer switches it on
([ADR-0048](./0048-a-sheet-document-holds-entries-and-exsheet-holds-the-one-undo-stack.md)).
Without the declaration nothing changes.

## Added for Cell Format *(2026-09-30, decided with the user)*

[ADR-0071](./0071-a-sheets-cell-format-is-document-data-painted-on-white-paper.md) puts Fonts,
Fills and Borders into a Sheet, with Excel's formatting keys and a Format Cells dialog. Three more
declarations follow from it. Each is opt-in, and a Consumer that declares none of them sees no
change (DC-1).

**14. Declared keys.** A Consumer can declare keys the core claims and raises. ExSheet declares
Excel's formatting keys: Ctrl+B, Ctrl+I, Ctrl+U and Ctrl+2 to Ctrl+5; Ctrl+Shift with `~`, `!`,
`@`, `#`, `$`, `%`, `^`, `&` and `_`; and Ctrl+1.
- **The core claims a declared key whether or not an edit is open, and raises it together with
  whether one is.** While an edit is open, the Consumer decides what the key does. ExSheet refuses
  it and says why (ADR-0071).
- **Without the declaration, the key stays the browser's**, as today.
- **The key table keeps its arbitration** ([ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md)).
  A declared key never takes a key the core already answers for itself. A declaration naming one is
  refused by name.
- **A declared key is raised with the Selection as the grid holds it at the key, and the Row
  Sequence Version it is written in.** `SelectionChanged` is raised after the render that shows a
  move, which on a circuit is a round trip later. So a key pressed straight after a move can reach
  the Consumer before the move does. *(Found on the Server host by `format-keys.spec.mjs`, on
  2026-10-01: Ctrl+B straight after Shift+Up formatted the Focus cell alone. The version is carried
  for the reason every positional notification carries one,
  [ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md).)*
- **A Consumer can read the Selection as the grid holds it now.** *(2026-10-01, ticket 56.)*
  `ReadSelection()` returns the grid's Held Selection, with the Row Sequence Version it is written in.
  - It is a synchronous read on the renderer's context. It is opt-in by being called, and nothing
    reaches JavaScript.
  - A Consumer reconciles it with `HeldSelection.Under(version)`, as ADR-0011 asks of positions.
  - ExSheet adopts it at each command that acts on the Selection: `SetCellFormatAsync`,
    `OpenFormatCellsAsync`, the Context Menu's "Format Cells…" and a whole-column resize's undo
    step. So a command run within a round trip of a keyboard move acts on the cells the user sees
    selected. The late `SelectionChanged` then names the same Selection, and is no move.
- **ExSheet also declares Excel's insert and delete keys** *(2026-10-02, decided with the user after
  Part C of the eleventh Windows run, case 12; ADR-0071)*: Ctrl with `+`, typed with Shift or without
  it (Ctrl+Shift+`=` on a US or UK layout, the numeric keypad's `+`), and Ctrl with `-`.
  - **They are the browser's zoom keys.** Declared, they are claimed while the keyboard is the grid's,
    so the page does not zoom from a Sheet. That is the price, taken deliberately: the instance root's
    capture listener hears only keys pressed inside the grid (ADR-0018), so outside the Sheet, and
    with Ctrl+0 or the browser's menu anywhere, the zoom stays the browser's. ExGrid declares none of
    them by default.
  - **Whole rows insert or delete rows, and whole columns insert or delete columns**, as the Context
    Menu's commands do, at the rows or columns the Selection spans.
  - **Any other Selection changes nothing and says why**: a range that is not whole rows or whole
    columns, every cell at once, or several ranges. Excel opens a dialog there that shifts cells right
    or down. ExSheet has no shifting of cells: it would rewrite References that point into part of a
    range, spread a row's or column's Cell Format into each cell it moves, and move the cells of a
    Linked Table, which are the Consumer's data. That is a decision of its own if it is ever wanted.
  - **The key acts on the Selection it carries** (above), and is refused like a formatting key while
    an edit is open.
- **The Alt key alone may be declared.** *(2026-10-02, ADR-0100.)* Its canonical form is
  `Alt+Alt`, which is what a press of the Alt key canonicalises to. It is the only modifier a
  declaration may name by itself.
  - ExSheet declares it, with `F10` and `Control+F1`, for the Sheet Toolbar's KeyTips and its
    toggle.
  - Claimed, its release cannot open Chrome's and Edge's menu on Windows. Whether it does is a
    reading until a Windows run.
  - The release is the Consumer's to hear, from a `keyup` that reaches it from the grid's root.
    No listener is added to the grid.

**15. A per-cell appearance.** A Consumer can supply a cell's Font (a colour, bold, italic,
underline and strikethrough), its Fill, and its four Border sides.
- **Without the declaration, cells look as they do today.**
- **A bold cell is judged by bold widths.** `CellTextMetrics` gains them: each character class
  measured at the bold weight. A bold cell's `####` decision
  ([ADR-0016](./0016-column-width-and-overflow.md)), and the width it hands to a Consumer's painted
  text (item 11), use them.
- **How the appearance is painted is ADR-0071's measurement to decide.** It might be an inline
  `style`, a per-cell custom property or interned classes for the Font and Fill, and a layer over
  the rows or lines inside each cell for the Border. Whatever is chosen must keep P1–P9
  ([ADR-0027](./0027-appearance-travels-in-css-geometry-travels-in-csharp.md)). A row repaints when
  its appearance changes and skips otherwise. Nothing per cell reaches JavaScript. The DOM does not
  grow with the extent. *(Decided on 2026-10-01 from ticket 44's measurement; ADR-0071, "What the
  measurement chose".)*
  - Font and Fill use interned classes in a generated stylesheet.
  - Borders are drawn inside each cell. Each cell paints its own share of Excel's centred line,
    from edges resolved once per row, outside the render. So a border change repaints the rows
    either side of the edge as well.
  - *(2026-10-01, ticket 47, the declaration as built.)*
    - **The lookup.** The grid parameter `CellAppearance` takes a
      `CellAppearanceOf<TRow>(TRow row, GridColumn<TRow> column)`. It is null by default, and then
      nothing is painted for it.
    - **What it answers.** A `CellAppearance` holds `FontColour`, `Bold`, `Italic`, `Underline`,
      `Strikethrough`, `Fill`, and `Top`, `Right`, `Bottom` and `Left`. Each side is a `Border`, a
      `BorderStyle` (None and Excel's thirteen) with an `RgbColour`.
    - **Two lines on one edge.** The grid parameter `EdgeBorder` takes an `EdgeBorderOf`. It is
      asked only when the two cells record different lines on the same edge. Without it, the upper
      or left cell's line is drawn.
    - **When a row repaints.** The row instance and the lookup signal a change, as for Cell State
      ([ADR-0006](./0006-grid-owns-a-generic-cell-state-vocabulary.md)). Each painted row is resolved
      once, outside its render, from itself and the rows either side, into an immutable object the
      row compares by reference. A row repaints only when what it paints changed.
    - **Bold widths.** `CellTextMetrics` gains `BoldWideWidthPx`, `BoldDigitWidthPx`,
      `BoldNarrowWidthPx` and `Bold`.
  - *(2026-10-01, tickets 88 and 90.)*
    - **The Cell Editor's look.** The grid parameter `EditorAppearance` (a
      `CellAppearanceOf<TRow>`) answers how the editor looks over the cell it edits: the Fill as its
      ground, and the Font as its text. Borders are not read.
      - Without the parameter, the editor uses `CellAppearance`. With neither, it is unchanged.
      - The editor's rules set `--ex-editor-background` and `--ex-editor-color`, so ADR-0057's
        coloured References read over a Fill.
      - ExSheet answers with the Font's own colour, not a Number Format's, because the editor shows
        the Entry.
    - **`--ex-row-rule`** is a layer hook on a lined cell, as `--ex-tint` is. It lies beneath the
      lines, so a cell whose ground covers the row's gridline can paint the gridline back. ExSheet
      sets it on pinned cells. *(Ticket 92: the core now sets it on a Pinned Column's cells
      itself, to its row's rule, and to none under a Fill and on group and total rows, so a theme's
      row rule no longer stops at the pinned block.)*
- **Borders are drawn as Excel draws them.** Each line is centred on the gridline. A thick line
  reaches into both cells. Lines lie above Fills and below the Focus, the Selection and the
  Reference Outlines. Which of two lines recorded on one edge is drawn is the Consumer's answer, so
  ExSheet can give Excel's rule.

**16. A Consumer's popover.** A Consumer can have the grid show its own content in the grid's
popover frame. The frame is placed inside the grid's box and bounded by it
([ADR-0040](./0040-a-popover-stays-inside-its-grids-box.md)). It takes the keyboard and returns it
on closing ([ADR-0039](./0039-a-popover-takes-the-keyboard-and-may-hold-popups-of-its-own.md)),
and it closes as a Cancel when the box shrinks below one row. ExSheet uses it for Format Cells under
the built-in Chrome.
- Without the declaration, nothing changes.
- The MudBlazor Chrome does not use it. It shows Format Cells in a `MudDialog`, whose frame is its
  own (ADR-0071; ADR-0010's note of 2026-09-30).
- *(2026-10-01, ticket 93.)* **A Consumer that opens a frame of its own hands the keyboard to it
  through the core.**
  - On a circuit, keys typed between the command that opens Format Cells and the frame taking
    focus reached the grid. A digit started an edit behind the dialog, under both Chromes, whether
    the command came from Enter on the menu, a click or Ctrl+1. That is quietly wrong (principle 1).
  - **The core's own popover:** the existing key hold waits for the popover to hold the keyboard,
    and then replays the keys to it. A declared key that opens one is held as Alt+Down's is, and a
    click on a menu item starts the hold as Enter does.
  - **A frame of the Consumer's own:** the core gains `HandKeyboardToFrameAsync()`. ExSheet calls it
    whenever it opens Format Cells in a frame of the Chrome's own. The core then:
    - keeps the keyboard on its root after the command, until the frame takes it, and holds keys
      typed on the root or a menu meanwhile;
    - replays them, in order, to the element that took focus. A frame's element takes every key
      but Tab; a tab takes its arrows, Home and End;
    - drops them if the keyboard never arrives within the hold's fallback, or goes to another grid.
    The keys are never the grid's.
    *(As built: the note first said the root would not take the keyboard back. Keys typed on `body`
    are heard only by a listener on `document`, which ADR-0021 does not allow, so the root keeps it
    and its hold holds the keys. A key typed while focus is briefly on `body`, after a click, is lost
    and never the grid's.)*
  - A dynamic call was chosen over a fixed flag on the command, because the same command opens the
    core's popover under one Chrome and a frame of its own under another.
