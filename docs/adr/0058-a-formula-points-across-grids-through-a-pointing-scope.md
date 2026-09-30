# A Formula points across grids through a Pointing Scope the Consumer declares

*(Decided with the user in a design grilling on 2026-09-29, and closed on 2026-09-30 once
[ADR-0057](./0057-references-are-outlined-in-colour-while-a-formula-is-edited.md) was merged. It
began with a defect on ExSheet's demo page: `=` typed into a cell, then a click on the positions grid
beside it, left the edit where nothing could reach it.
[ADR-0018](./0018-multiple-instances-must-be-independent.md), section 6, fixes that defect. This ADR
answers what the user asked next: "In Excel, while a Formula is typed, clicking a cell of another
table refers to it. Without that, a user who does not know the name `Positions` will find Formulas
hard to use.")*

**Point** ([ADR-0051](./0051-formula-entry-completion-point-mode-and-the-formula-bar.md)) writes a
Reference for the cells the user points at, inside the Sheet being edited. **A Pointing Scope lets
it point at the other grids on the page that show Linked Tables**
([ADR-0049](./0049-linked-tables-are-the-consumers-data-read-by-key.md)), and write what reads the
pointed cell by key. The Consumer declares which Sheets and grids take part. Nothing is joined that
the Consumer did not join.

## What Excel writes cannot be brought over

Pointing from outside a Table at one of its cells, Excel writes the cell's address: `B3`, or
`[Book2]Sheet1!$B$3` from another workbook. *(Recalled when this was decided, and observed by the
ninth Windows run: see "What the ninth Windows run settled".)* An address is the positional
Reference that ADR-0049's rule 2 refuses. Once the user of the positions grid sorts it, the same
Formula reads another row, and nobody has edited anything. What is brought over is the gesture:
pointing writes a name.

## Why the grids cannot do it alone

- **ExSheet never reads another component instance** (ADR-0049, rule 1).
- **The grid that shows the positions does not know that it shows a table called `Positions`**, or
  which of its columns is the key. It has rows and columns.
- **Two grids on a page know nothing of each other** (ADR-0018).
- **A press on the positions grid moves DOM focus to it, and moves its Selection.** To point, that
  grid would have to decide at the press to do neither, and to hand the press on instead. It would
  decide that from the fact that a Sheet is pointing, and it does not have that fact.

Only the Consumer knows which grid shows which table. So the Consumer says so.

## The Pointing Scope

- **The Consumer makes a Pointing Scope and registers Sheets and grids in it, any number of each.**
  A grid is registered with:
  - the name of the Linked Table it shows;
  - which of its columns are which of the table's columns. A grid column with the same name as its
    table column needs no entry.

  The key column is the table's own. It is declared with the table, once (ADR-0049, note of
  2026-09-30), and the Scope reads it from the declaration of the Sheet that points.
- **Only the Sheet that holds the keyboard points.** There is one keyboard. Two Sheets in one Scope
  can each point, one at a time.
- **A grid in no Scope behaves as it always does.** So does a grid in a Scope while no Sheet of that
  Scope points.
- **Two Scopes divide a page.** A Sheet on the left points only at the grids in its own Scope.
- **A Sheet is never pointed at through a Scope.** Pointing by position stays inside one Sheet (`=B2`).
  Two ExSheets that read each other are a workbook, which ADR-0049 leaves out of the first version. A
  press on another Sheet in the same Scope is an ordinary press. The keyboard goes there, and the edit
  stands (ADR-0018, section 6).
- **The Scope is ExSheet's; ExGrid gains only a mechanism.** The Scope lives in the ExSheet package,
  which already depends on ExGrid. Nothing in ExGrid depends on ExSheet. What ExGrid gains is this:
  while a Consumer declares a grid pointed at, a press on it hands over where it landed instead of
  acting, and the grid draws what it is told to draw. The grid still does not know what a Formula is
  (ADR-0051).

## While a Sheet points

A registered grid is **pointed at** while a Sheet of its Scope holds the keyboard, has an edit open,
and is in Point, that is, its caret position stands where a Reference can go (ADR-0051). After `)`,
for instance, no grid is pointed at. **A registered grid with an open edit of its own is not pointed
at.** A press on it goes to its edit, as ADR-0018 section 6 says.

While a grid is pointed at:

- **A press on its rows or its column headers moves neither DOM focus nor its Selection and
  Focus.** The keyboard never leaves the Sheet. The `+` typed next is the Sheet's, and Escape still
  cancels the edit.
- **Its header does nothing of its own**: no sort, no column menu, no reorder, no Heading drag.
- **The pointer over its rows and headers is `cell`.** Nothing else marks the grid.

### What is written

**A name is always the table's column name, never the header's label.** The Consumer's work is to
make a table's column names readable. Under a Header Group
([ADR-0032](./0032-tiered-headers-are-declared-rectangles-not-a-column-tree.md)), the label `Before`
stands under CVA, FVA and MVA alike, so it cannot be a name.

| Pressed | Written |
|---|---|
| One cell, in a column the table has | `XLOOKUP("R-4471", Positions[Id], Positions[PV])`: the row's key, the key column and the pressed column |
| The header of a column the table has | `Positions[PV]`, Excel's own structured reference |
| More than one cell (Shift+press, or a drag); several columns; a Header Group's rectangle; a column the table does not have (a display column, an Action Column); a cell of a table declared without a key; a cell whose key is blank | nothing, and the reason is told |

- **The key is written as Excel writes a constant of its kind.** Text is written in double quotes,
  with any `"` inside doubled (`"R-4471"`). A number is written in the invariant form, and a boolean
  as `TRUE` or `FALSE`.
- **The reason is told to ExSheet's Consumer** as a notification carrying it, in the manner of the
  grid's Refusals. The Sheet's text is left as it was. `/sheet` shows the reason.
- **The text is written where Point writes.** A further press, on a registered grid or on the Sheet,
  replaces what this Point wrote (ADR-0051).
- **A drag that reaches another cell takes back what its press wrote** *(decided with the user,
  2026-09-30, while ticket 37 was handed out)*. The grid hands a press over at once, so that a click
  writes without waiting for the release, and a drag is known only when it reaches another cell. The
  text then returns to what it was before the press, the dashes go, and the reason is told. Keeping
  the first cell's `XLOOKUP(...)` was rejected: a user who meant a range would be left with a Formula
  that reads one cell of it.
- **While Point writes from a registered grid, the Name Box is empty.** Excel's names the pointed
  cell, but the pressed cell has no address in the Sheet's words, and naming the edited cell would
  read as pointing there. The Name Box is empty while a Size Tip shows for the same kind of reason
  ([ADR-0052](./0052-the-focus-is-excels-active-cell-and-the-extent-is-the-moving-end.md)).
  *(Corrected 2026-09-30, decided with the user after the ninth Windows run. This bullet first said
  the Name Box names the edited cell.)*

### What is drawn

ADR-0057 decides the colours. This section says how they reach a registered grid.

- **The Scope wires ADR-0057's Reference Outlines into its grids itself.** While a Formula is edited,
  ExSheet tells which Linked Table columns it reads, and in which colour. The Scope already knows
  which grid shows which table and which grid column is which table column, so it outlines them
  there. A page with no Scope wires `OutlinedColumns` by hand, as before.
  - **Leaving the wiring to the page, beside the Scope, was rejected.** The page would then state the
    same correspondence of columns twice. A correction made in one place only would outline the wrong
    column, or none, and that is hard to see on screen.
- **The pressed cell gets only a dashed line, in the Focus outline's colour.** These are the dashes
  ADR-0057 lays over Point's outline. The cell gets no outline of its own.
  `XLOOKUP("R-4471", Positions[Id], Positions[PV])` holds two References, `Positions[Id]` and
  `Positions[PV]`, and each is outlined over its column. The pressed cell is not a Reference:
  `"R-4471"` is text.
  - **Outlining the pressed cell in PV's colour was rejected.** It would say the Formula reads that
    cell by position. The Formula reads by key, and after a sort it follows its row somewhere else.
  - **The dashed cell is remembered by its key**, so it follows its row through a sort. When the row
    is not painted, nothing is drawn, and the grid never scrolls to it (ADR-0057).
  - **A pressed column header dashes the whole of that column's body.**
  - **The dashes go when Point ends**: an operator is typed, the caret is moved, or the edit commits
    or is cancelled. The column outlines stay while the edit is open, as every Reference Outline does.
- **The whole written `XLOOKUP(...)` lies on ADR-0057's grey ground** while Point goes on, unless it
  follows the Formula's `=` directly (ADR-0057, "What cases 24–32 settled"). The two column
  references inside it wear the darker shades of their colours. The grey covers exactly what a further
  press would replace.

### The keyboard

- **When the keyboard leaves the pointing Sheet, the Scope stops pointing.** No grid is pointed at,
  and the edit stands (ADR-0018, section 6). A press back on the Sheet brings the keyboard back, and
  pointing can go on. This is what keeps the Scope clear of ADR-0018 section 6's rule that an edit
  which opens takes the keyboard only while it is still its grid's.
- **After a press on a registered grid, the arrow keys point inside that grid**, as Excel's arrow
  keys move inside another workbook once it is pointed at (the ninth Windows run). The text is rewritten for the
  cell they reach, replacing what this Point wrote, and the dashes move with it.
  - **↑ and ↓ move one row in the grid's current order.** At its first or last row, nothing moves.
  - **← and → move to the next column the table has**, passing over the grid's columns that the table
    does not have. With no such column that way, nothing moves.
  - **The grid scrolls to keep the pointed cell in view**, as the Sheet does while it points, and as
    Excel scrolls the other workbook. ADR-0057's "the grid never scrolls to show a Reference" is about
    the References a Formula holds, not about a cell the user is moving.
  - **A row that has not arrived yet** (a Placeholder) writes nothing, and the reason is told.
  - **Shift+arrow is refused as a range is**: nothing is written, the text stays as it was, and the
    reason is told. **Ctrl+arrow is not built**: it moves nothing, and the reason is told. Excel's goes
    to the edge of the data, and the grid holds only its Window.
  - The keys reach the grid through C#: the Sheet's core hears them, as it hears every key in Point,
    and the Scope asks the grid for the next cell. No script is added.

  *(Corrected 2026-09-30, decided with the user after the ninth Windows run. This bullet first said
  the arrow keys move nothing until Excel was observed, which the user chose on 2026-09-30 as a3,
  with a1 to follow the observation.)*

## On a circuit

Two round trips are involved on a Server circuit, and they are treated differently. On WebAssembly
there is neither.

- **From `=` to the grids being pointed at, the gap is accepted.** Whether a Sheet points is decided
  in C#, by the Consumer's predicate (ADR-0051). A registered grid learns it from the render that
  follows. A press within that round trip is an ordinary press: DOM focus goes to that grid, and the
  edit stands (ADR-0018, section 6). Closing the gap would need the grids' scripts to share a state of
  pointing. A module with state shared between instances is what ADR-0018 section 3 and
  [ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md) rule out.
- **From a press on a registered grid to the text being written, keys are held.** The press travels
  to the grid's C#, then to the Scope, then to the Sheet, so the text reaches the Sheet's field a round
  trip after the press. A key typed in that time reaches the field first. The field's text is the
  newest the core hears of (ADR-0051), so the written text would be lost. For some texts a different
  Formula would stand: `=SUM(1,`, then a press, then `)` and Enter at once, commits `=SUM(1,)`.
  Pointing with the mouse while typing operators with the other hand is how Excel is used, so this is
  not a rare case.
  - **So the pressed grid tells the pointing Sheet, in script, that a press was handed on.** Its
    capture-phase `mousedown`, the listener ADR-0021 already allows, dispatches one event on the
    Sheet's root. The Sheet's listener holds the keys typed after it, as it holds keys behind a press
    on its own rows, and replays them once its core has answered
    ([ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md)'s hold). ADR-0021 records the
    event.
  - **This holds no state between instances.** The grid learns which root to tell from the render
    that makes it pointed at, and only what the Consumer put in one Scope is joined. That is the
    difference from the first gap. Here the script keeps an order; it does not decide a meaning.

## Completion, aligned with Excel

The first proposal also listed the table names before anything was typed: after `=`, an operator,
`(` or `,`. It was meant for users who do not know the names. It was withdrawn. Excel lists nothing
until a name's first letter is typed, and a list open after `=` would take ↓, which Point needs for
`=` ↓ ↓. **Completion is aligned with Excel instead.** A list of names opens only once a letter is
typed, and by then the caret no longer stands where a Reference can go, so it never takes ↓ from
Point. A user who does not know a table's name meets it by pointing. The one list that opens before
anything is typed is an argument's value list, below. While it is open, ↑ and ↓ choose in it, as the
user asked of every open list and as Excel's does. Escape closes it, and ↓ then points. → points
while it is open, and closes it (the tenth Windows run). A press on the grid still points while it is
open. *(Corrected 2026-09-30 while the tickets were handed out: this
paragraph first said that the list and Point never compete, which the value list contradicts.)*

| After | Excel | ExSheet |
|---|---|---|
| `=`, an operator, `(`, `,` | no list | no list, as today |
| a name's first letter | functions and names, by prefix | functions and Linked Tables, as today (observed 2026-09-27, item 13) |
| an argument whose values are a fixed list, such as XLOOKUP's `match_mode` | the values: `0 - Exact match`, … (observed 2026-09-27, item 14) | **the values, built now** |
| `Table[` | `@ - This Row`, the columns, `#All`, `#Data`, `#Headers`, `#Totals` (observed by the ninth run) | **the columns only, built now** |
| F3 | nothing without a Name; Paste Name, which lists Names and never tables, with one (observed) | nothing: ExSheet has no Names, and F3 is left to the browser |
| Backspace back into a name | the list again (observed) | **the list again, built now** |

- **Argument value lists are built now**, because Excel was seen to show them and ExSheet lacks them.
  It is a completion list: ↑ and ↓ choose, Tab accepts and writes the value's number, and Escape
  closes the list before it cancels the edit (ADR-0051). The texts are Excel's, and the ninth Windows
  run observed both lists: `search_mode`'s last two end in "order)", as `2 - Binary search (sorted
  ascending order)`.
- **After `Table[`, the list offers the table's column names only.** ExSheet reads
  `Table[Column]` and nothing else: its grammar refuses `#All`, `@` and a range of columns
  ([ADR-0047](./0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md)). A
  candidate the grammar refuses would break the Formula the moment it was chosen. This is a
  deliberate difference from Excel.

**Readings, until Excel is observed** *(taken while building ticket 39, 2026-09-30, and accepted by
the user as readings)*. `docs/specs/exsheet/verify-on-windows-10.md` asks Excel. A reading
that Excel contradicts is corrected here after the run.

- **A value typed whole lists nothing.** With `0` typed at `match_mode` and the caret after it, no
  list is shown; a prefix of a value, such as `-`, still lists the values it begins. Were the whole
  value listed, the grid would ask again after Tab accepted it, the list would open again, and Tab
  could never move on.
- **Tab on a column name writes the name without the `]`**: `Positions[` and Tab on `PV` gives
  `Positions[PV`, as Tab on a table's name writes the name alone.
- **As built, and asked with them:** while any list is open, ← and → move the caret (ADR-0051), so →
  does not point from an open value list. Accepting a table's or a column's name leaves the list open
  on that same name, as accepting a table's name already did.

**What the tenth Windows run settled** *(2026-09-30, `verification/2026-09-30-windows-excel-10/`)*.
Excel contradicted the first reading and the two lines built with it, and agreed with the second. The
rules are now Excel's:

- **A value typed whole lists that value alone, selected.** `0` at `match_mode` shows
  `0 - Exact match` and nothing else.
- **Text that is not a whole value lists every value, with the first selected.** `-` shows all five
  of `match_mode`'s values, `0 - Exact match` selected. Excel does not narrow a value list by prefix.
- **Tab closes the list**, whether it accepted a value (`-1`), a column (`Positions[PV`, without the
  `]`, as read) or a table's name (`Positions`). The grid does not open the list again on the text it
  has just written, so the reason for the first reading is gone.
- **→ with a value list open points**, where the caret stands at a Reference's place: `,,` then →
  writes `E10`, shown selected, and the list closes. A list takes only ↑, ↓, Tab and Escape; ← and →
  do what they do without it: they point where Point can, and move the caret elsewhere, as they do in
  a list of names (ADR-0051).

**Readings taken while building ticket 44** *(2026-09-30)*, where the tenth run did not look. The
ninth run's Part B asks Excel; a reading it contradicts is corrected here.

- **"Typed whole" is read on the text before the caret.** With the caret before or inside a value
  (`,,|1)`, `,,-|1)`), every value is listed, and the one chosen replaces the whole of it.
- **Text that begins no value lists nothing** (`4`, `A1`, `1+` at `match_mode`). Read literally, "any
  other text lists every value" would open a list after an operator, which the table above rules out
  and which would take ↓ from Point.
- **`Home`, `End` and the Shift+arrows stay the editor's while any list is open**; only ← and → are
  given to Point, where it can.

## Not in the first version, and what watches for it

- **A key of several columns.** The Consumer adds a column that joins them (`ACME|5Y`), and declares
  it as the key. Writing the pair itself, `XLOOKUP(1, (Cds[Entity]="ACME")*(Cds[Tenor]="5Y"),
  Cds[Spread])`, needs array operations that the engine does not have. A table column compared with a
  value is `#VALUE!` over more than one row (ADR-0049). `SUMIFS` reads the pair without them, but it
  answers 0 for a row that is not there, which is a plausible wrong number.
- **A range of columns**, such as `Xva[[CVA Before]:[CVA Diff]]` for a Header Group or for several
  columns pointed at together. The grammar refuses it. It would also need the group's members to lie
  next to each other in the table's order. The grid does not show that order, because the user
  reorders columns ([ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)).
  ADR-0032 declares a group's members by name for the same reason.
- **Two layer 1 tests pin today's refusals**, so that the day either one goes cannot pass unnoticed.
  One pins that `(Cds[Entity]="ACME")` over two rows is `#VALUE!`. The other pins that the grammar
  refuses `Xva[[A]:[B]]`. Each fails when its refusal goes. Its message says what to do next: open
  this section, decide with the user how a key of several columns (or a range of columns) is pointed
  at, and **do not make the test pass by changing its expectation**. A test skipped until a decision
  is made would stay silent. These do not.

## Considered options

- **The page wires each grid's `SelectionChanged` to the Sheet.** It needs nothing new in ExGrid. It
  was rejected. By the time the page can act, the press has already moved DOM focus to the grid and
  moved its Selection. On a circuit, the keys typed meanwhile go to the grid. That is the defect this
  began with, built in as the design. A page with four grids would also write the wiring four times.
- **Completion that lists table names before a letter is typed, instead of pointing.** It was
  rejected, as above. It takes ↓ from Point. And the more tables a page has, the less a name alone
  says which is which.
- **Every grid on the page pointable, without a Scope.** It was rejected. ADR-0018 keeps instances
  independent, and a grid that shows no Linked Table has nothing a Formula could read.
- **An outline in the pressed column's colour on the pressed cell.** It was rejected, as above.

## Consequences

- ADR-0018, ADR-0021, ADR-0029, ADR-0049, ADR-0051 and ADR-0057 each gain a note.
- `CONTEXT.md` gains **Pointing Scope**.
- `/sheet` registers its positions grid in a Scope, and stops wiring the Reference Outlines by hand.
  `/sheets` gives each side a Scope of its own.
- The criteria are DC-52 to DC-55 and SH-32 to SH-37 in `docs/definition-of-done.md`. The tickets are
  34 to 41 in `docs/specs/exsheet/issues/`. Excel is asked in `docs/specs/exsheet/verify-on-windows-9.md`.

## What the ninth Windows run settled *(2026-09-30)*

Part A of `docs/specs/exsheet/verify-on-windows-9.md` asked Excel (Microsoft 365, Version 2609) what
it writes and shows while it points into a Table and into another workbook, and what it lists. It is
recorded in `verification/2026-09-30-windows-excel-9/pointing.md`. Where Excel and a reading differed,
the rule above is now Excel's, or the difference is decided here with the user.

- **Excel writes an address**: `=B3` inside one workbook, `=[Book2]Sheet1!$B$3` from another. That
  is what this ADR declines to bring over.
- **Excel points into another workbook only from the second click.** The first click on that
  workbook's window writes nothing, and the arrow keys then point from that workbook's own active
  cell. From the second click on, the arrow keys move inside it, and Shift+arrow extends there. So the
  arrow keys point inside a registered grid (above).
- **A registered grid points from the first press** *(decided with the user)*. Excel's first click
  switches the workbook it points into. A Scope's grids are on one page, and a press that did nothing
  would look broken. This is a deliberate difference from Excel.
- **Excel's Name Box names the pointed cell.** The Name Box is empty here instead (above, decided with
  the user).
- **A press on a registered grid's header writes the column** (`Positions[PV]`), as decided. Excel,
  clicking a Table's header cell, wrote `Positions[[#Headers],[PV]]`, because that cell is a row of the
  Table. A grid's header is not a row, and it stands for the column, as a drag over the whole column's
  data does in Excel (`=Positions[PV]`).
- **Every column of a Table at once**, dragged in Excel, wrote `=Positions`. A registered grid refuses
  several columns, as decided.
- **After `Table[`, Excel lists `@ - This Row` first**, then the columns and `#All`, `#Data`,
  `#Headers`, `#Totals`. ExSheet lists the columns only, as decided.
- **F3 shows nothing in a workbook without a Name.** With one, Paste Name lists it and never a Table.
  ExSheet has no Names, so there is nothing to build, and F3 is left to the browser, as F4 is outside
  an edit (ADR-0051).
- **Backspace back into a name lists again**, and both argument lists read as the table above.
- **Seen and not asked:** Excel shows the pointed Reference's value in a tip above the edited cell
  while it points (`20`, `{10;20;30}`). Nothing here decides it.

## Settled while building ticket 37 *(2026-09-30)*

- **Three more reasons are told**, beside the table's: the pointing Sheet has not declared the table
  a grid shows; the pressed row's key is an Error Value, which no constant can write; and a press
  that arrives after pointing has ended, which on a circuit a grid still painted pointed at can
  send. Nothing is written for any of them.
- **A grid with an open edit of its own is not pointed at, whatever it is told.** The Scope never
  tells it so, and the grid checks it too.
- **The Sheet learns where DOM focus is from Blazor's `focusin` and `focusout`**, on an element that
  wraps ExSheet's markup and takes no box (`display: contents`). No script is added. A `focusout`
  waits one turn, because a move inside the Sheet, from its root to the Cell Editor, raises a
  `focusout` and a `focusin` together.
- **The reason is told to the Consumer only.** ExSheet draws nothing of its own for it; `/sheet` and
  `/sheets` show it below their grids.
- **Until the arrow keys point inside a registered grid** (ticket 41), after a press on one the
  arrow keys, `Home` and `End` move nothing.
- **A press overtook keys typed before it.** On the Server host, `=1+` typed and the positions grid
  pressed at once gave `=XLOOKUP(...)` in 2 runs of 4: the `1+` was still held behind the `=` by the
  Sheet's listener, and the press, which the grid sends to the core itself, arrived first. The hold of
  "On a circuit" now keeps the keys typed before a press ahead of it as well (ADR-0021's note, widened
  the same day; DC-54; ticket 35).
