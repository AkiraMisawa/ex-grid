# ExSheet holds a Workbook of Sheets behind Sheet Tabs

*(Numbered ADR-0321 until 2026-10-20. That block had been reserved on `main` by ExChart's Chart Spec
first, so this branch moved into the block [`docs/agents/numbering.md`](../agents/numbering.md) reserves
for it. Commit messages before then use the old number.)*

*(Decided with the user, 2026-10-20, in a grilling (W1 to W9) that began as `.xlsx` reading and
writing ([ADR-0330](./0330-exsheet-opens-and-saves-xlsx-with-a-reader-and-writer-of-its-own.md)).
Two of that ADR's answers only worked around ExSheet's one Sheet: opening one worksheet of a
workbook, and writing a Linked Table's rows to a sheet of their own. The user decided that ExSheet
holds several Sheets first, and that `.xlsx` is then read and written whole. The user took the
recommendation of every question except W7 and W9.)*

[ADR-0046](./0046-exsheet-is-a-general-purpose-sheet-drawn-by-exgrid-as-its-consumer.md) said "One
Sheet for now": References already carry their Sheet (`Sheet2!A1`), and when several Sheets come,
the engine belongs to the workbook, not to an ExSheet instance. This ADR is that step.

## Decision

### 1. One ExSheet shows one Workbook, with Sheet Tabs at its foot (W1)

A **Workbook** is an ordered set of Sheets with one engine. **An ExSheet instance shows one Workbook**
and, at its foot, a **Sheet Tab** for each Sheet, as Excel does. The toolbar, the Formula Bar, the
Name Box, the undo stack and the Input Order
([ADR-0240](./0240-input-to-one-product-instance-is-taken-in-one-order.md)) stay one per instance.
[ADR-0100](./0100-the-sheet-toolbar-ships-as-an-opt-in-part-of-exsheet.md) rejected one toolbar for
several ExSheets; this is one ExSheet, so nothing there changes.

*Rejected:* one ExSheet per Sheet, several instances sharing one Workbook. The undo stack and the
order of input would span instances, against ADR-0240's one order per instance. It can be added by an
ADR of its own if it is ever needed.

**One ExGrid draws every Sheet** and switches to the Sheet shown. Each Sheet keeps its own selection,
active cell, scroll offsets and column widths, restored when its tab is selected again. One grid per
Sheet would grow the DOM with the number of Sheets, against P1 to P9
([ADR-0027](./0027-appearance-travels-in-css-geometry-travels-in-csharp.md)). *(Settled by those
invariants, not put to the user.)*

### 2. A Workbook Document, beside the Sheet Document (W2)

- **A new format, the Workbook Document**, holds the Sheets in order, the Sheet shown, the culture
  and the Linked Tables. It has a version of its own, and a reader meeting a version it does not know
  refuses it, as ADR-0048 says of the Sheet Document.
- **ExSheet gains a `Workbook` parameter.** The `Document` parameter and the Sheet Document keep
  working: a Sheet Document opens as a Workbook of one Sheet. **Both set at once is refused by name.**
- **The culture belongs to the Workbook**, not to each Sheet: under two cultures, the same typed
  `1,234` would be two different numbers on two Sheets of one file. A Sheet Document's culture becomes
  its Workbook's.
- **Linked Tables are declared on the Workbook** and are read from any Sheet, as a table's name is
  unique within an Excel workbook ([ADR-0049](./0049-linked-tables-are-the-consumers-data-read-by-key.md)).

*Rejected:* a Sheet Document of version 10 holding several Sheets. Its `Name`, `Cells` and `Culture`
would stop saying which Sheet they mean, and an application's code would change meaning with nothing
said.

### 3. One undo stack, and every Sheet operation is a step, deletion included (W3)

- **The Workbook has one undo stack.** Undoing a step shows the Sheet it was taken on and selects the
  range it changed, as Excel does.
  - *Narrowed while building (workbook ticket 08, 2026-10-21):* the grid places one selected range
    (ADR-0050, item 4), so a step over several ranges that do not make one rectangle together selects
    the first in Selection order, the one it acted from, where Excel re-selects them all. Selecting all
    of them waits on the grid placing several. The Focus goes to the range's top-left cell.
  - A Sheet operation's undo or redo selects nothing. It shows the step's Sheet while the Workbook holds
    it, else the Sheet it was done from (the one a Sheet was added beside or copied from), else the Sheet
    the Workbook shows once the deletion is redone.
  - On the Sheet shown, the range is placed under the version read before any Consumer handler hears the
    change, so two undos in a row, or a handler that opens another document, cannot leave one step's
    range over another's rows (spine rule 6).
- **Adding, deleting, renaming, moving and copying a Sheet are each one step.** A Formula that
  referred to a deleted Sheet shows `#REF!`, and undoing the deletion gives it back its Formula.
- **A deliberate difference from Excel:** Excel asks before deleting a Sheet, and the deletion
  empties its undo stack. ExSheet asks nothing and the deletion can be undone, which keeps
  ADR-0048's rule that every user operation is one step, with no exception.

### 4. What the Sheet Tabs do in the first version (W4)

Switching (a click, Ctrl+PageUp and Ctrl+PageDown), adding (the `+` button, Shift+F11), renaming (a
double click edits the name on the tab), deleting, moving (a drag, and Move left / Move right on the
tab's menu) and copying. **Every one of them can be done from the keyboard alone.** Each is an input
in the Input Order.

*(As built, 2026-10-20, exsheet-workbook ticket 05: switching. The tabs show under one Sheet too, as
Excel shows one, and `ShowSheetTabs="false"` hides them. Each Sheet keeps its Selection and scroll
offsets, which the grid takes back with the Row Sequence Version the Sheet is shown under
(`GridView`), so a gesture aimed at the Sheet left is refused, never applied to the one shown.
Ctrl+PageUp and Ctrl+PageDown are numbered in the Input Order as declared keys; a press on a tab is
not yet, because the tabs stand outside the grid's root, and joins the order with ExSheet's own
surfaces in input-order ticket 04. Until §5 is built, another Sheet is not shown while an edit is
open, and the Sheet's notice says so.)*

**Later, each by an ADR:** tab colours, hiding and showing Sheets (references, selection and Find
across a hidden Sheet need rules of their own, as hidden rows do in ADR-0046), and grouped Sheets,
which edit Sheets the user cannot see.

**Sheet names follow Excel's rules**: at most 31 characters, none of `[ ] : * ? / \`, not empty,
and unique without regard to case. A name that breaks one is refused by name.

### 5. Pointing reaches across Sheets, as in Excel (W5)

While a Formula is being typed, selecting another Sheet's tab and pointing at a cell there writes
`Sheet2!A1`, and Enter commits the Formula on the Sheet it was typed on and shows that Sheet again.
[ADR-0058](./0058-a-formula-points-across-grids-through-a-pointing-scope.md)'s Pointing Scope is
widened to the Sheets of its Workbook. What a tab press does while a constant (not a Formula) is being
typed is a question to Excel, put in the next Windows run, and built as observed.

*(As built, 2026-10-20, exsheet-workbook ticket 09: a press on a tab, Ctrl+PageUp and Ctrl+PageDown
show another Sheet while the edit's text is a Formula, and are refused as before while it is a
constant. The grid keeps the edit open with the rows it was opened among: on another Sheet its editor
holds the keyboard unseen, the Formula Bar shows its text, and Point points from that Sheet's own
Focus, writing `Sheet2!A1`, quoted as a Reference quotes a name. What was pointed on the Sheet left is
replaced by the next point. The commit lands on the row the edit was opened on, and ExSheet shows that
Sheet again before the move that completes the commit, so Enter moves down there; Escape, or any other
end of the edit, shows it again too. Only the References naming the Sheet shown are outlined on it.
Excel writing `Sheet2!` at the tab press itself, before a cell is pointed, and what a point back on the
first Sheet writes, are put to the next Windows run with the constant case.)*

### 6. Commands and addresses name their Sheet (W6)

- **A command or an address may name its Sheet.** A command that names none is taken only while the
  Workbook has one Sheet; with more, it is **refused by name**. Taking it against the Sheet shown would
  make its outcome depend on whether the user's tab press reached the instance before it, which the
  spine's sixth rule refuses.
- **The application gets commands** to add, delete, rename, move and copy a Sheet, and hears each
  change. Every one goes onto the one undo stack (ADR-0048).
- *As built, 2026-10-20:* a command that acts on the Selection — the Cell Format commands, and the
  toolbar's buttons through them — names no Sheet and is still taken with several: the Selection is
  the shown Sheet's, and the command reads the Selection and its Sheet together when it runs, so what
  it acts on does not depend on when a tab press arrived any more than which cells are selected does.
  An address names its Sheet: `DoAsync(sheet, edit)` and `CellFormatAt(sheet, address)`; the ones that
  name none are refused with `SheetRefusalReason.SheetNotNamed`. The application hears each change to
  the Sheets through `SheetsChanged`.
- *As built, 2026-10-20:* the Name Box takes an address that names its Sheet, `Sheet2!B5`, and shows
  that Sheet with the place selected and the Focus revealed. The place is handed to the grid with the
  Sheet's rows, under the version that brings them (`ShowViewWith(view, range, focus)`), so it never
  depends on when the grid takes that version. While an edit is open it is refused, as a tab press is.

### 7. 3D references are in the first version (W7)

`=SUM(Sheet1:Sheet3!B2)` reads the same range on every Sheet from the first to the last named, in tab
order. *(Recommended against, and chosen: refusing 3D references until later.)* ADR-0047's rule
holds unchanged:

- **Only the functions Excel accepts a 3D reference in** take one (`SUM`, `AVERAGE`, `COUNT`,
  `COUNTA`, `MAX`, `MIN` and the like, among the functions ExSheet supports). Any other gives the
  answer Excel gives.
- **What happens to the span** when a Sheet is moved into or out of it, when an end Sheet is deleted
  or moved, and when a Sheet is inserted inside it, is observed in Excel and put in the next Windows
  run before it is built. Nothing is built from reasoning about it.

### 8. Find and Replace search the Sheet or the Workbook (W8)

As Excel's dialog does: **Within: Sheet** (the default) or **Workbook**. A match on another Sheet
shows that Sheet and selects the cell.

### 9. The engine's API is rebuilt around the Workbook (W9)

`ExSheet.Engine` gains a **`Workbook`**, which holds the engine, the undo stack and the Linked Tables.
**`Sheet` becomes one Sheet of a Workbook.** The members that move are moved, not kept as
compatibility forwarders: `Sheet.Open`, `ToDocument`, the commands and the undo stack belong to the
Workbook. *(Recommended against, and chosen: keeping `Sheet.Open(SheetDocument)` and its peers as
forwarders to a Workbook of one Sheet.)* The surface goes out in `0.2.0`, which may break; each member
that leaves is a Change Note (ADR-0190).

The component's `Document` parameter is kept (§2). Only the engine's API breaks.

*(As built, 2026-10-20: the Sheet Day, `NowSource`, the volatile recalculation, the Sheet Seed and the
count of changes are the Workbook's too, so a change on any Sheet draws `RAND` again on every Sheet, as
Excel does; the draw names its Sheet, decided with the user and recorded in ADR-0180.)*

## Consequences

- `CONTEXT.md` gains **Workbook**, **Workbook Document**, **Sheet Tab** and **3D Reference**.
- The engine moves from one dependency graph per Sheet to one per Workbook. Inserting a row on one
  Sheet rewrites the References to it on every Sheet, as ADR-0046 foresaw.
- Both Chromes draw the Sheet Tabs; the MudBlazor Wrapper's tabs render and call back, and the core
  decides what each press means, as for every other Chrome surface.
- A Windows run gains the questions §5 and §7 put to Excel.
- ADR-0330 is rewritten to read and write a whole Workbook.
- The Docs Site's ExSheet pages and their Examples move to the Workbook, and the Showcases that show a
  Sheet are re-recorded if they change.
