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

**Renaming the Sheet is an undoable step**, like any other operation on the Sheet. Excel is
reported not to undo a rename. This is a deliberate difference: undoing a rename never shows a
wrong value, and it keeps the rule that each user operation is one step.

- **Copy with headers is off on a Sheet** *(decided with the user, 2026-09-27)*. A Sheet's column
  letters are addresses, not headers, and Excel has no such command. A Consumer that wants it
  switches it on; it then copies Values with the letters as the first row.
- **A copy of several ranges carries Values, not Entries**, until Excel's behaviour for such a copy
  has been observed (the behaviours list).
- **An invariant number pasted from Excel keeps every digit its double holds.** Unlike a typed
  number, it is not cut to 15 significant digits, because Excel-to-Excel pasting does not cut it.
  The case corpus confirms this.
