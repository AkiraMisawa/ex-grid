# Formula entry: completion, Point mode, and a Formula Bar inside the grid's root

*(Decided with the user, 2026-09-27, in the design grilling that started ExSheet —
[ADR-0046](./0046-exsheet-is-a-general-purpose-sheet-drawn-by-exgrid-as-its-consumer.md). The
question that shaped the last part was "where does a formula bar even go, in a browser?")*

Typing a Formula into a bare `<input>` is typing it blind. Excel gives four aids, and ExSheet
takes all four: **completion** of function names, the **argument hint**, **Point mode**, and the
**Formula Bar with its Name Box**. A user who reaches for any of them and finds it missing
concludes the sheet is broken. Every aid is built as ExGrid mechanism that a Consumer switches on
and supplies meaning to, in the pattern of
[ADR-0050](./0050-what-exsheet-asks-of-exgrids-core.md). The grid does not know what a Formula is.

## The Cell Editor opens on the Entry

A cell holding `=A1*2` shows its Value, say 84. Editing it has to start from `=A1*2`.
**A Consumer can supply the text the editor opens on**, per cell, and ExSheet supplies the Entry.
Without it, the editor opens on the value, as it does today.

## Completion and the argument hint

- **The core reports the editor's text and caret to the Consumer as the user types**, when the
  Consumer asks for it. The Consumer answers with **candidates** (`SU` → `SUM`, `SUMIF`, …; the
  engine's declared functions and the Linked Tables' names) and with an optional **hint** (`SUM(`
  → `SUM(number1, [number2], …)`). ExSheet is the one that knows a Formula's grammar.
- **Chrome paints both, and the core decides what the keys do**, the split
  [ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md) makes everywhere. The list is an
  Inner Popup of the editor ([ADR-0039](./0039-a-popover-takes-the-keyboard-and-may-hold-popups-of-its-own.md))
  and stays inside the grid's box ([ADR-0040](./0040-a-popover-stays-inside-its-grids-box.md)).
  While it is open, ↑/↓ choose, Tab accepts (Excel's key), and Escape closes the list before it
  cancels the edit.

## Point mode

In Excel, while a Formula is being typed at a place where a Reference can go (after `=`, an
operator, `(` or `,`), the arrow keys and the mouse **point**. They move an outline over the Sheet
and write its Reference into the Formula, where otherwise they would commit the edit and move.
`=` ↓ ↓ gives `=A3`. Shift extends the outline to a range. Typing an operator ends pointing, and
F2 switches between moving the caret and pointing.

- **Point is a fourth editing state, beside Overwrite, Caret and Interactive.** In it, arrows and
  clicks move the pointing outline, which is painted in the selection overlay
  ([ADR-0008](./0008-selection-is-painted-by-an-overlay.md)). The Selection and the Focus do not
  move: the cell being edited stays where it is.
- **Whether the caret is at a place where a Reference can go is the Consumer's answer.** The
  Consumer supplies a synchronous predicate over the text and the caret. ExSheet answers it from
  its parser. **The Reference text written for the pointed range is the Consumer's too.** The grid
  knows positions, and `B7:C9` is a sheet's name for them.
- **Without the predicate, nothing changes.** Overwrite and Caret keep the meanings
  [ADR-0012](./0012-anchor-focus-and-keyboard-navigation.md) gives the arrow keys.

## Every decision at a keystroke is made from the text that keystroke carries

On a Server circuit, the answers above can arrive after the user has typed further. If the grid
decided an arrow's meaning from the last answer it had, a fast typist would get pointing where
the text no longer allows it. **So the key message carries the editor's current text and caret,
and the core decides from those**, with the predicate answering synchronously in C# on the same
side as the key handling. A candidate list that comes back for text that has since changed is
dropped, not shown.

Carrying the text and caret needs nothing new in JavaScript. The capture-phase `keydown` listener
already runs for this key ([ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md)). It adds
the value and caret of the input it is capturing for, which it reads at no cost. This is not a
measurement: no layout is read, and nothing is synchronous on the path to a paint. The text sent
as the user types goes by a Blazor input event. That is one message per keystroke, about ten a
second, which is not ADR-0021's per-frame wire trip.

## The Formula Bar and the Name Box

A Formula's cell shows its Value, so **the Formula Bar is the only place the Entry can be read
without editing the cell.** [ADR-0016](./0016-column-width-and-overflow.md) already asked for "the
focused cell's full value at all times. The same role as Excel's formula bar".

- **The Formula Bar is an ExGrid band, inside the grid's root, above the header.** A Consumer
  switches it on. It holds the **Name Box** (where the Focus is: the Consumer supplies the label,
  so `D200` on a Sheet) and the Focus cell's text (the Entry on a Sheet, the full value on a
  display grid, which is ADR-0016's display).
- **It is the Cell Editor's second surface: there is one uncommitted text, shown in two places.**
  Typing in either updates both, and committing from either commits once. The uncommitted text is
  still the grid's ([ADR-0007](./0007-edits-are-an-overlay-owned-by-the-consumer.md)). The bar
  gives it somewhere else to be seen.
- **Typing an address into the Name Box** moves the Selection and the Focus there. The Consumer
  resolves the text to a position and asks the grid to place them (ADR-0050, item 4). **A press
  into the Name Box selects its text**, as Excel's does, so what is typed replaces the address
  shown *(decided with the user, 2026-10-01: the fifteenth Windows run found ExSheet's caret left
  after `D10`, and a composition appended to it; ticket 78)*.
- **Its height is geometry, resolved in C#** with the rest of the Grid Metrics
  ([ADR-0028](./0028-geometry-is-resolved-once-density-is-only-a-preset.md)), and the rows take
  what it leaves. A Consumer that does not switch it on gets no band.

**Why inside the root.** Three existing rules all hold there without being touched:

- The key listener is on the root ([ADR-0018](./0018-multiple-instances-must-be-independent.md)),
  so the bar's keys are captured first, and Point mode works from the bar as it does from the cell.
- Popovers stay inside the grid's box (ADR-0040), so the completion list under the bar is bounded
  the same way.
- The uncommitted text never leaves the component that holds it.

### Considered options

- **A separate component the Consumer places anywhere, such as the application's toolbar.** It
  was rejected. The bar would sit outside the root, where the key listener cannot see it. Point
  mode from the bar would cross two roots. With two grids on a page, which one the bar follows
  would have to be wired by hand, which is the kind of coupling ADR-0018 rules out. If a Consumer
  asks for this placement, that request is the trigger, and it comes with its own ADR.
- **No bar: a Cell Editor that grows for long Formulas, and a tooltip for the Entry.** This was the
  cheapest option, and it was rejected: a user could not read a Formula without starting to edit
  it.

## Consequences

- **`CONTEXT.md`'s editing states become four**: Overwrite, Caret, Interactive, and **Point**.
- **ADR-0012 and ADR-0021 each gain a note**: the arrow keys' meaning while pointing, and the text
  and caret riding on the allowlisted `keydown`.
- **The Chrome seams grow**: the completion list, the argument hint, and the Formula Bar's two
  fields. Each is painted by Chrome from what the core hands it, under the built-in Chrome and
  `ExGrid.MudBlazor`'s alike ([ADR-0030](./0030-what-a-design-system-wrapper-owns-and-what-it-may-not-touch.md)).

## Added while building *(2026-09-27, decided with the user)*

**Keys held while a mode change is in flight go to the surface that has DOM focus.** The
capture-phase listener holds keys typed before the round trip that changes the editing mode has
returned, then types them into an editor. It used to type them into the first `.ex-editor` in the
markup, which is the cell's editor even when the user was typing in the Formula Bar. Characters
could then land at the wrong caret. The listener now types them into the editor surface that holds
DOM focus. This is the same allowlisted listener doing the same job
([ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md)).

## Added while building, second round *(2026-09-27, decided with the user)*

- **Shift+arrow points too.** While a pointing outline stands, the key listener claims the four
  Shift+arrows as well, so they extend the outline instead of selecting text in the input.
  *(2026-10-12, after the readings of 2026-10-12, R1 (viii).)* **Shift+Home is claimed with them**,
  and extends the outline from its fixed end to the row's first column, pointing going on: in Excel,
  `=`, ↓, Shift+Home in D10 gave `=A11:D11`, still in Point (observed). From a range already pointed
  (`=D11:D12`), it extends from the range's fixed end (`=A11:D12`), as Shift+Home extends a
  Selection: a reading. The second round of the seventeenth run's corrections had left it to the
  editor, which selected text.
- **While the completion list is open, only ↑, ↓, Tab and Escape are claimed.** ← and → move the
  caret, as they do in Excel.
- **The editor's caret is reported and set, never inferred.** An input event carries no caret, and
  working it out from the change is ambiguous where letters repeat. A wrong caret would make
  completion replace the wrong span, and quietly change a Formula. So the listener reports the
  caret with each input. After the core rewrites the text (an accepted candidate, a written
  Reference), the listener places the caret where the core says. Neither is a measurement: no
  layout is read ([ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md)).

- **The caret is reported whenever it moves, not only on input** *(decided with the user the same
  day)*. The listener also reports the caret on `selectionchange` inside an editor surface. Without
  that, moving it with ← or → in Caret, or with a click inside the text, would leave the core
  pointing from a caret that is no longer there, and a Reference would land in the wrong place.
  **Opening an edit places the caret explicitly** at the end of the opening text, rather than
  assuming the browser left it there.

## Added while building, third round *(2026-09-27, decided with the user)*

- **A click into the Formula Bar's text keeps the caret where it was clicked**, as Excel's does.
  Placing the caret at the end on opening applies only to an edit opened by typing or by F2.
- **Moving the caret while pointing (←, → or a click in the text) ends pointing and enters
  Caret**, the equivalent of Excel's Edit mode. It does not enter Overwrite.

- **While pointing, the Name Box names the pointed cell** *(observed in Excel, 2026-09-27)*. It
  names the edited cell again when pointing ends.

*(2026-09-27, with [ADR-0052](./0052-the-focus-is-excels-active-cell-and-the-extent-is-the-moving-end.md).)*
While a selecting drag's button is down over more than one cell, the grid asks a second function,
`NameBoxSizeLabel`, for the Name Box's text, and ExSheet answers `4R x 3C`. It is separate from
`NameBoxLabel` so that neither signature changes. While pointing, the Name Box names the pointing
outline's Focus, its fixed end: `=A3:B3` typed shows A3, and a pointing drag shows where it started.
No size is offered during a pointing drag or a fill drag.

## F4 cycles the Reference at the caret *(2026-09-29, decided with the user)*

In Excel, while a Formula is being entered or edited, F4 cycles the Reference at the caret through
its four forms: `=B2`, F4, gives `=$B$2`, then `=B$2`, `=$B2` and `=B2` again. It is how an Excel
user fixes a Reference before filling or copying it, and the Reference grammar already reads all
four forms ([ADR-0047](./0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md),
SH-6). Nothing in ExGrid or ExSheet did it.

- **Only while an edit is open**, in the Cell Editor or the Formula Bar. Outside an edit, Excel's F4
  repeats the last action. That is not built: most of the actions it would repeat (formatting,
  inserting, deleting) are not ExSheet's, and F4 is left to the browser there.
- **The rewrite is the Consumer's**, as the Reference text written for a pointed range already is
  (Point mode, above). The Consumer supplies a synchronous function over the editor's text and its
  selection (start and end), answering the new text and the new selection, or nothing. ExSheet
  answers it from its parser; the core never reads a Formula. Without the function, F4 is not
  claimed and nothing changes.
- **The text and caret are the ones the F4 key message carries**, as every decision at a keystroke
  is (above), and the new selection is placed by the editor listener that already places the caret
  after a rewrite. **No JavaScript is added**
  ([ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md)): the capture-phase listener
  claims one more key while an edit is open, and only when the function is declared.
  *(Settled while building, decided with the user the same day.)* The F4 key message also says
  whether the user moved the caret in that text. On a circuit, writing the answered text leaves the
  browser's caret at the end until the core's placement lands, and a second F4 in that gap would
  cycle whatever Reference ends the text: `=A1+B2`, caret after `A1`, F4 twice, gave `=$A$1+$B$2`
  where `=A$1+B2` is right. **A placement still in flight wins over the browser's own caret, unless
  the user moved it**, which is the rule the second round (above) already applies to caret reports.
- **While pointing, F4 cycles the Reference the pointing outline wrote**, and pointing goes on.
  *(2026-10-12.)* The whole of it: the function is asked with a selection over the outline's
  Reference, so a pointed range's two ends cycle together, as Excel's did (observed, the readings of
  2026-10-12, R1 (vi): `=`, ↓, Shift+↓, F4 in D10 gave `=$D$11:$D$12`). Asked at a caret at its end,
  only the second end would cycle, now that each end is a Reference of its own.
- **On a Mac it is F4 only.** Excel for Mac's own key is ⌘+T, which a browser keeps for a new tab and
  never gives a page. On a Mac laptop's default keyboard settings, F4 is Fn+F4.

**What Excel does at the edges** *(observed by the seventeenth Windows run, 2026-10-11, Excel
16.0.20430 on Windows 11, `verification/2026-10-11-windows-17/report.md`, Part C, and by the readings
of 2026-10-12, R1, `verification/2026-10-12-windows-readings/report.md`; settled here 2026-10-12, as
SH-28 and ADR-0202 ask)*. The rules above are Excel's documented behaviour. The bullets below were
first the closest readings of what it does at the edges, taken for the implementation and asked of
Excel in `docs/specs/exsheet/verify-on-windows-7.md`; the seventeenth run put them to Excel, typed
into a cell (Enter mode) and with F2 first (Edit mode), and R1 in the Formula Bar and while pointing.
Where a reading was wrong, the bullet says what Excel does and keeps what was read:

- **The Reference at the caret** is the one the caret is inside or touching, on either side: `=A1|+B2`
  and `=|A1+B2` both cycle `A1` (observed, cases 2 to 4). **Each end of a cell range is a Reference of
  its own** *(observed, cases 5, 6 and 22)*: `=SUM(A1:B2|)` and `=SUM(A1:|B2)` cycle `B2`, and
  `=SUM(A1|:B2)` cycles `A1`. A caret touching no Reference changes nothing, as does F4 in a function
  name (observed, case 12), a number (case 13) or text that is not a Formula (case 14), and in a
  string, a reading by generalisation: no case asked it.
- **A cell range does not cycle as one: the end at the caret cycles, from its own form** *(corrected
  after the seventeenth Windows run, cases 5, 6 and 22. The reading was that a range cycles as one,
  `A1:B2` → `$A$1:$B$2` → `A$1:B$2` → `$A1:$B2` → `A1:B2`, its next form taken from the first end and
  given to both; Excel cycles only the end the caret touches)*. `=SUM(A1:B2)` with the caret after
  `B2` gives `=SUM(A1:$B$2)`, `=SUM(A1:B$2)`, `=SUM(A1:$B2)` and `=SUM(A1:B2)`; `=$A1:B2` gives
  `=$A1:$B$2`, the other end left; `=B2:A1` gives `=B2:$A$1`. A selection over both ends cycles both,
  as it cycles every Reference it covers (below), each to the first end's next form, and so does a
  range being pointed at (above; observed, R1 (vi)). Whole columns and whole rows stay one Reference
  (below).
- **A selection cycles every Reference it covers, overlaps, or touches at either end**, each to the
  next form of the first one. *(Observed by the user in Excel, Microsoft 365 on Windows,
  2026-09-29, for the touching case: in `=A1+B1`, selecting only `+` and pressing F4 gives
  `=$A$1+$B$1`. The first reading, "a selection that overlaps no Reference changes nothing", was
  wrong. The overlapping case and the form taken from the first Reference were observed by the
  seventeenth Windows run, cases 19 and 20: `1+B` selected in `=A1+B2` gave `=$A$1+$B$2`, and
  `A1+$B$2` selected gave `=$A$1+$B$2`.)*
- **The rewritten Reference's column letters are written in upper case** *(corrected after the
  seventeenth Windows run, cases 21 and 22. The reading was that only `$` signs are written, so that
  `=b2` gave `=$b$2`; Excel gives `=$B$2`)*. **The rest of the text keeps the case and the order it was
  typed in**, a reading: no case observed lower-case text outside the rewritten Reference. `=B2:A1`
  gives `=B2:$A$1`, its order kept. A Sheet qualifier is not rewritten and keeps
  its case: it was not asked.
- **Whole columns and whole rows have two forms**: `A:A` ↔ `$A:$A`, `1:1` ↔ `$1:$1` (observed, cases
  8 and 9: the third press gives `$A:$A` and `$1:$1` again).
- **A Sheet qualifier is kept**, and only the cell part cycles: `Sheet2!A1` → `Sheet2!$A$1` (observed,
  case 10).
- **A structured reference does not cycle**: `Positions[PV]` is left as it is (observed, case 11).
- **After F4, the rewritten Reference is selected where the caret had been moved, and a caret stands
  at its end where typing left it** *(corrected after the seventeenth Windows run and the readings of
  2026-10-12. The reading was a caret at the end in every case; a first correction, taken from Part C
  alone, tied the selection to Edit mode, and R1 (iv) contradicted it in the Formula Bar)*. Observed:
  a caret moved with ←, or with Home and →, after F2 selected what was rewritten (`$A$1` in cases 2
  to 4, `$B$2` in case 5, the whole `$A:$A` and `$1:$1` in cases 8 and 9), and so did ← in the bar
  (R1 (v): `=SUM(A1:B2)`, ←, F4 gave `=SUM(A1:$B$2)` with `$B$2` selected); a selection stayed over
  what was rewritten (cases 7 and 18 to 20); and with the caret where typing left it, the caret stood
  at the end, typed into a cell (cases 1, 10, 21 and 22) and into the bar (R1 (iv): `=B2`, F4
  gave `=$B$2`, nothing selected). The Consumer answers the span of what was rewritten, a Sheet
  qualifier included before the first end, for a caret as for a selection. The F4 key message
  already says whether the user moved the caret in that text (above), and the grid selects the span
  where it says so or where the key carried a selection, and collapses it to a caret at its end
  otherwise, in any editing state. A press in the text counts as a move, and a caret where F2 opened
  the edit, or where an earlier F4 placed it, as one typing left: readings, by generalisation. A
  placement still in flight carries the selection as it carries a caret (the rule settled while
  building, above), so the next F4 cycles the same Reference. *(Whether a selection in Edit mode
  starts at a Sheet qualifier was not asked: case 10 was typed.)*
- **After F4 while pointing, the rewritten Reference is the one Point is writing**: the caret stands
  at its end, so a key typed next follows it (observed, case 16: `=`, ↓, F4, `+` gave `=$A$2+`).
  Excel reported it as the bar's selection (R1 (vi): UI Automation gave `$D$11:$D$12`); ExSheet
  shows it as Point shows the Reference it writes, with Point's look and never as a selection of the
  field's text, which a key typed would replace (Reference Outlines, below; ADR-0057, case 32).
  **A further move writes the new Reference as pointing writes it, in the relative form** (observed,
  case 15b: `=A3`, the reading's first option).

## Reference Outlines *(2026-09-29, decided with the user)*

[ADR-0057](./0057-references-are-outlined-in-colour-while-a-formula-is-edited.md) adds a fifth aid:
while a Formula is edited, each Reference in its text is coloured and its cells are outlined in the
same colour, as Excel's range finder does. **The pointing outline becomes the Reference Outline of
the Reference Point is writing**, in that Reference's colour, instead of a single outline in the
Focus outline's colour. What Point decides (which keys point, what is written, where the Name Box
points) is unchanged.

**The Reference being written is shown selected** *(2026-09-30, decided with the user, observed in
Excel by the eighth Windows run)*. While pointing, the Reference Point is writing lies on a grey
ground, unless it follows the Formula's leading `=` directly. It is a look on ADR-0057's coloured
layer, not a selection of the field's text. A key typed next follows the Reference, as it always did
(`=D11+D12`, then `5`, gives `=D11+D125`).


## Point reaches other grids through a Pointing Scope *(2026-09-30, decided with the user)*

[ADR-0058](./0058-a-formula-points-across-grids-through-a-pointing-scope.md) carries Point past the
Sheet's own rows. It goes as far as the grids its Consumer registered with the Sheet in a Pointing
Scope, where those grids show Linked Tables. What Point decides at a keystroke is unchanged: the
predicate says whether a Reference can go at the caret position, from the text each key carries.

- **A press on a registered grid writes where Point writes**: `XLOOKUP("R-4471", Positions[Id],
  Positions[PV])` for a cell, `Positions[PV]` for a column header. A further press, on a registered
  grid or on the Sheet, replaces what this Point wrote, as a further press on the Sheet always did.
- **After such a press, the arrow keys point inside that grid**: ↑ and ↓ by a row in its current
  order, ← and → to the next column the table has, and the grid scrolls to keep the pointed cell in
  view. Shift+arrow is refused as a range is. *(Corrected after the ninth Windows run, decided with
  the user: this bullet first said the arrow keys move nothing until Excel was observed. Excel moves
  inside another workbook once it points there. ADR-0058, "The keyboard".)*
- **F4 after such a press changes nothing.** Neither `XLOOKUP(...)` nor a structured reference cycles
  (above).
- **The Name Box is empty** while Point writes from a registered grid. The pressed cell has no
  address in the Sheet's words. *(Corrected after the ninth Windows run, decided with the user: this
  bullet first said the Name Box names the edited cell. Excel's names the pointed cell.)*
- **The keys typed right after such a press keep their order** behind the text it writes, on a circuit
  too ([ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md), note of 2026-09-30).

**Completion follows Excel's triggers** (ADR-0058, "Completion, aligned with Excel"). Nothing is
listed until a name's first letter is typed, so a list never takes ↓ from Point. One trigger that
Excel was seen to have is added now: **at an argument whose values are a fixed list, the values are
listed** (`XLOOKUP`'s `match_mode`: `0 - Exact match`, …). ↑ and ↓ choose, Tab accepts and writes the
value, and Escape closes the list first, as above. The ninth Windows run observed the other three
triggers, and each is settled in ADR-0058. After `Table[` the list offers the table's column names only,
where Excel also lists `@ - This Row`, `#All` and the rest. Backspace back into a name lists again.
F3 is left to the browser: Excel shows nothing without a Name, its Paste Name never lists a table, and
ExSheet has no Names.

*(Corrected by the tenth Windows run, 2026-09-30.)* The second round's rule, "while the completion list
is open, only ↑, ↓, Tab and Escape are claimed; ← and → move the caret", was seen in a list of names,
where the caret never stands at a Reference's place. At an open value list it does, and Excel's →
points there, closing the list. So a list takes only ↑, ↓, Tab and Escape, and ← and → do what they do
without it: point where Point can, and move the caret elsewhere. Tab closes any list, and the grid does
not open it again on the text Tab wrote.

*(Widened by Part B of the ninth Windows run, 2026-10-01, decided with the user; ADR-0058.)* Where
Point can go, `Home`, `End` and the Shift+arrows do the same: they close the list and do what Point
does with them, as Excel's do in Enter mode. Elsewhere they stay the editor's. Where a Reference can
go and no outline stands, `Home` starts pointing at the row's first column and `End` writes nothing,
list or no list (Q53); they had started nothing, and fell to Overwrite's commit. A value list opens only
while nothing of the argument stands after the caret: with the caret before a value, Excel lists
nothing.

## An edit in the Formula Bar is in Caret unless F2 takes it out *(2026-09-30, decided with the user)*

*(Retitled the same day, after the tenth Windows run. It was "An edit in the Formula Bar never enters
Overwrite"; see the last bullet.)*

Part B of the eighth Windows run typed `=A1+B1` into the Formula Bar, pressed F2, then `Home`. The
Formula was committed, and the Focus moved to column A. The edit had opened in Caret, as a press into
the bar opens it, and F2 had taken it to Overwrite (Excel's Enter mode), where `Home`, the arrows and
`End` move between cells. F2 goes from Caret to Overwrite wherever no Reference can go, and it did
not ask which surface the edit is in. Typing that ends Point is a second way into Overwrite, reached
after F2 has pointed from the bar or a press has pointed. Both are right in the cell, and wrong in
the bar: Excel's Formula Bar is always in Edit (2026-09-27, item 12, "status Edit"). *(Corrected the
same day, when ticket 42 was built. This paragraph first gave typing that ends Point as the cause of
the run's case. It was F2.)*

- **While the edit is in the Formula Bar, typing keeps it in Caret.** Typing that ends Point there
  returns to Caret, not Overwrite. `Home`, `End`, ← and → move the caret, as they do in Caret
  anywhere, and Enter, Tab and Escape keep their meanings. Excel's bar stays in Edit while it is typed
  in (the tenth Windows run, cases 14–16). *(Observed again by the readings of 2026-10-12, R1 (i) to
  (iii): a press into Excel's bar, `=`, ↓ stayed in Edit with the text `=` and nothing outlined; F4
  and ↓ changed nothing, and `+` typed on. ExSheet's bar does the same: the seventeenth run's Part C
  read `=` and `=+` there, cases 15, 15b and 16, and its "difference" 10 is none.)*
- **An edit that moves from the cell into the bar goes into Caret**, and one that moves back into the
  cell keeps the mode it has. Excel's cell does not return to Enter mode once it is in Edit (the
  tenth run, cases 18 and 19).
- **F2 in the bar does what it does in the cell** *(decided with the user after the tenth Windows run,
  replacing a decision of the same morning)*. From Caret it points where a Reference can go, and
  anywhere else it goes to Overwrite, whose `Home` and arrows enter the Formula and move; F2 again
  returns to Caret. Excel's F2 takes its bar from Edit to Enter and back (cases 17, 23). From Enter,
  its `Home`, → and ↓ entered the Formula and moved the active cell to A10, E10 and D11 (cases
  20–22), and where a Reference can go ↓ pointed (case 24).
  - **So case `7k` was Excel's behaviour, not a defect.** F2 had taken the bar to Overwrite, and
    `Home` entered the Formula and moved, as Excel's does. The defects the run led to are the two
    above: typing that ended Point left the bar in Overwrite, and an edit carried into the bar kept
    it. Excel's bar is in Edit in both.
  - **What was decided first, and why it went.** Before Excel's F2 in its bar was observed, the user
    chose (Q46) that F2 there move only between Caret and Point, so that the bar was never in
    Overwrite and `Home` there never committed. The tenth run showed that Excel's bar does go to
    Enter, and the user chose Excel's behaviour (Q47).
