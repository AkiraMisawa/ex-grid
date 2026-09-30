# A Sheet Document holds Entries, never Values; ExSheet holds the one undo stack

*(Decided with the user, 2026-09-27, in the design grilling that started ExSheet —
[ADR-0046](./0046-exsheet-is-a-general-purpose-sheet-drawn-by-exgrid-as-its-consumer.md).)*

ExSheet is the one place in the Ex family where the component holds the data.
[ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md)'s rule that "the grid neither
holds nor executes" is about ExGrid, and ExSheet is ExGrid's Consumer, so the rule is not broken.
This ADR draws the line between ExSheet and **its** Consumer, the application.

## The Sheet Document

**ExSheet holds the Sheet in memory. It hands the application a Sheet Document, takes one back,
and says when the Sheet has changed. The application persists it; ExSheet never does.** This keeps
ADR-0007's split: what is saved and shared belongs to the application, and not to a component's
internal state.

**A Sheet Document records Entries only, and never Values.** Opening one computes every Value
again. There were two reasons:

- **A stored Value is a second truth.** Excel's files carry cached results. When a link is broken
  or a file was written by a tool that did not recalculate, Excel shows the cached number, and it
  looks current. With no Values stored, there is nothing stale to show.
- **The engine is the one implementation**
  ([ADR-0047](./0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md)).
  A document carrying results would invite a reader to trust them instead of running the engine.

The cost is that reading a saved sheet's numbers means running `ExSheet.Engine`. ADR-0047 made the
engine UI-free for exactly this reason.

**Constants are recorded already parsed, never as typed text.** `1,234` typed under `en-US` is
recorded as the number 1234. `2026/9/26` typed under `ja-JP` is recorded as that date's serial
number. If the text were recorded and parsed again on opening, a document opened under another
culture would change its numbers and nobody would notice: `1,234` read under `de-DE` is 1.234.
Formulas are recorded in the invariant syntax ADR-0047 defines.

**A Sheet has a declared culture.** It decides how typed constants are read and how Values are
displayed: separators, and the default date format. It is recorded in the Sheet Document, because
it is part of what the Entries meant when they were typed.

`.xlsx` reading and writing is a separate package when it comes, as `ExGrid.MudBlazor` is. The
Sheet Document is ExSheet's own format and does not wait for `.xlsx`.

## The one undo stack

**ExSheet holds the undo stack, and there is one.** Every edit, paste, fill, insertion and
deletion is one step, and so is a bulk operation: a paste over a thousand cells is undone with one
Ctrl+Z. **A change the application makes through ExSheet's commands goes onto the same stack.**
This is ADR-0007's reasoning: two stacks break the order, because a stack that did not see one
change undoes the wrong one. **Replacing the whole Sheet Document clears the stack.** Stepping back
into a document that has been swapped out would land on something that no longer exists.

This is not the bundled undo stack ADR-0007 promises. That one is for an ExGrid Consumer whose
edits are Overlays over a fetched base, and it stays reserved with its own trigger. ExSheet's
steps are operations on Entries, including structural ones that an Overlay cannot express.

### While an edit is open, the application's changes are refused *(decided with the user, 2026-09-29)*

Found on ExSheet's demo page. `99` was typed over the price in C4 (Plums), and "Insert a row above
row 2" was pressed while the edit was open. Plums moved to row 5, and the Cell Editor stayed at row
4, over Pears. Enter then wrote 99 into Pears' price, and the total changed, with nothing said.
Rows and columns are places on a Sheet, so its Row Sequence Version never moves
([ADR-0046](./0046-exsheet-is-a-general-purpose-sheet-drawn-by-exgrid-as-its-consumer.md)), and
the rule that drops an edit when the order changes
([ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)) never
fired. [ADR-0018](./0018-multiple-instances-must-be-independent.md), section 6, now keeps an edit
open when the keyboard leaves the grid, so a click on the application's own button is the ordinary
way to reach this.

**While an edit is open, every command that changes the Sheet is refused by name**, as ExSheet
refuses everything it will not do: `DoAsync` (an insertion, a deletion, any edit), `UndoAsync`,
`RedoAsync`, `SetNumberFormatAsync` and `SetAlignmentAsync`. Nothing changes, and the refusal names
the open edit. This is Excel's behaviour: its ribbon greys out while a cell is being edited.
*(2026-09-30: `SetCellFormatAsync` and `OpenFormatCellsAsync` join the list, and a formatting key
changes nothing and says why
([ADR-0063](./0063-a-sheets-cell-format-is-document-data-painted-on-white-paper.md)).)*
**ExSheet says whether an edit is open, and when that changes**, so the application can grey out its
own buttons in the same way. It learns this from its grid
([ADR-0050](./0050-what-exsheet-asks-of-exgrids-core.md), section 6).

- **Replacing the whole Sheet Document while an edit is open discards the edit, and says so**
  *(decided with the user the same day)*. A parameter cannot be refused. Keeping the edit open would
  write it into the new document's cell at the same place, which is the failure above. So the edit is
  dropped and announced, as ADR-0011 announces a discard, with the reason that is true of it: the
  document was replaced. The grid gains the means for a Consumer to discard an open edit with its
  own reason ([ADR-0050](./0050-what-exsheet-asks-of-exgrids-core.md), section 6).
- **A Linked Table's declaration and snapshots are not refused.** They are data arriving, not a
  command ([ADR-0049](./0049-linked-tables-are-the-consumers-data-read-by-key.md)). They change
  Values and never move a place, and the open edit holds text, not a Value.
- Considered: letting the edit follow its cell, so that C4's edit becomes C5's. It needs the grid
  to move an open editor at the Consumer's word, and it still leaves the question of what the user
  meant. Considered: dropping the edit with an announcement, as ADR-0011 does. The typed text is
  lost to a button the user pressed. Refusing keeps the text and changes nothing, which is the
  project's first principle.

## The clipboard carries Entries inside, and Values outward

- **From ExSheet to ExSheet, a copy carries Entries.** Formulas travel, and their relative
  References shift by the distance pasted, as in Excel.
- **From ExSheet to anywhere else, a copy carries Values**, as unformatted values, which is
  ExGrid's rule ([ADR-0005](./0005-copy-refuses-rather-than-truncates.md)). A Formula's text means
  nothing to another program, and the Value is what the user saw.
- **From anywhere else into ExSheet, each pasted field is taken as if the user had typed it** under
  the Sheet's culture. `=A1+1` becomes a Formula and `1,234` becomes a number. Excel does the same.

## Consequences

- **"Held by the component" has a boundary.** ExSheet holds the Sheet while it is open. It holds
  nothing across sessions, and it never talks to storage.
- **The Sheet Document is a format with a version.** A reader meeting a version it does not know
  refuses to open the document. It does not guess.

## Settled while building *(2026-09-27, decided with the user)*

**Renaming the Sheet is an undoable step**, like any other operation on the Sheet: it keeps the
rule that each user operation is one step. *(It was first recorded as a deliberate difference from
Excel, on a report that Excel does not undo a rename. Observed on 2026-09-27, Excel does undo it,
so this is no difference at all.)*

- **Copy with headers is off on a Sheet** *(decided with the user, 2026-09-27)*. A Sheet's column
  letters are addresses, not headers, and Excel has no such command. A Consumer that wants it
  switches it on; it then copies Values with the letters as the first row.
- **A copy of several ranges carries Values, not Entries**, until Excel's behaviour for such a copy
  has been observed (the behaviours list).
- **An invariant number pasted from Excel keeps every digit its double holds.** Unlike a typed
  number, it is not cut to 15 significant digits, because Excel-to-Excel pasting does not cut it.
  The case corpus confirms this.

### What the observation settled *(decided with the user, 2026-09-27)*

- **A copy to Excel carries formats.** ExSheet's HTML flavour gives each cell its Value and its
  number format in Excel's own markup (`x:num` and `mso-number-format`). A date then arrives in
  Excel as a date, not as a serial number in General. The value stays the unformatted Value, which
  ADR-0016 requires.
- **A pasted field that is a run of `#`** is Excel showing a value too wide for its column, with
  the value itself missing. It is refused by name, and the refusal says the source column was too
  narrow to show the value. It is not taken as the text `########`.
- **Pasted text that cannot be read as a Formula is taken as text**, as Excel takes `=1+`. It is
  not refused.

## The Sheet Document records Cell Format whole *(2026-09-30, decided with the user)*

[ADR-0063](./0063-a-sheets-cell-format-is-document-data-painted-on-white-paper.md) adds Font, Fill
and Border to a Sheet's Cell Format.
- **The Sheet Document records them** for each cell, row and column, beside the Number Format and
  the Alignment, at a new version.
- **A document of an older version reads with none of them.**
- **A colour is recorded as Automatic or as an RGB value.** A document holding any other kind of
  colour, such as an `.xlsx` theme colour, is refused by name, as an unread Number Format is. It is
  not guessed at.
- **The version number** is fixed when the branches merge, because version 7 is already taken on
  `claude/exsheet-pointing-scope`.
