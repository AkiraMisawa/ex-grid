# 36: A Linked Table's key, checked on every snapshot

Status: done

**What to build:** ADR-0049's note of 2026-09-30. A Linked Table may be declared with one key column.
Every snapshot of a keyed table is checked, and one in which a key repeats is refused by name.

**Blocked by:** None (can start immediately)

- [x] `DeclareLinkedTableAsync(name, columns, key)` on ExSheet, and `Sheet.DeclareLinkedTable` in the
      engine (`Sheet.LinkedTables.cs`). The key names one of the columns; any other name is refused
      by name, as a repeated column name already is (`WhyNotALinkedTable`) (SH-33)
- [x] The Sheet Document records the key with the declaration, and a document without one still
      opens. A declaration with another key replaces the held one: rows dropped, readers wait (SH-33)
- [x] On push, a keyed table's key column is checked. Values are compared as `XLOOKUP`'s exact match
      compares them (`SameKindEqual`), so case does not tell keys apart, and blanks are not compared
      (SH-33)
- [x] A repeated key refuses the snapshot: the push throws, naming the table, the key column and a
      repeated value. The table goes back to waiting, and every reader shows `#GETTING_DATA`. No
      earlier snapshot's value is shown (SH-33)
- [x] The declared key is readable from the Sheet, for the Scope (ticket 37)
- [x] Layer 1: a clean snapshot; a repeat (`R-1` twice); a repeat by case (`r-1`, `R-1`); a number and
      text of the same digits (not a repeat); blanks; `IFERROR(XLOOKUP(…), 0)` over a refused table
      shows `#GETTING_DATA`, never 0; a redeclaration with another key (SH-33)
- [x] `/sheet` and `/sheets` declare `Positions` with the key `Id`

## Comments

2026-09-30, done (branch `agent/pointing-scope-36`).

- **API.** `Sheet.DeclareLinkedTable(name, columns, string? key = null)` and
  `ExSheet.DeclareLinkedTableAsync(name, columns, string? key = null)`: an optional parameter, so the
  two-argument calls compile unchanged and the engine still has one method of that name (the test that
  no undeclare exists lists the methods). The key names a column without regard to case, and is held
  as the columns spell it, so `key: "id"` over `Id` is the same declaration as `key: "Id"`. Any other
  name is an `ArgumentException` (`ParamName` `key`): "The key 'Book' is not one of the columns of
  'Positions'." `LinkedTable` gains `Key` (a last positional parameter, `null` by default), read
  through `Sheet.LinkedTables` and `ExSheet.LinkedTables`; that is what ticket 37 reads.
- **The Sheet Document is version 7.** A table's object gains `"key"`, written only when the table has
  one. Every earlier field was added the same way (version 2 the Linked Tables, 3 the axis formats, 4
  to 6 the widths), so this follows the precedent rather than deciding anything new: versions 2 to 6
  open with every table unkeyed, `"key"` in a version 6 document is refused as anything a version
  does not define is, and a key that is not one of the columns is refused by name. A version 6 reader
  refuses a version 7 document by its version (ADR-0048). `SheetDocumentTable` gains `Key`.
- **The check.** `PushLinkedTable` builds the snapshot, then looks for the first two rows whose keys
  XLOOKUP's exact match (`SameKindEqual`) cannot tell apart. To do that without comparing every pair,
  `TextOrder.EqualityKey` gives a text two texts share exactly when `TextOrder.Compare` is 0 (primary
  weights, accents, and the count of hyphens and apostrophes passed over); numbers and booleans are
  compared as themselves, and the kind first. A blank (`null`) key is passed over, and so is an Error
  Value, which `SameKindEqual` never matches. Empty text is text, not a blank, as XLOOKUP reads it.
  `A_repeat_is_what_xlookup_cannot_tell_apart` pins the two to each other over case, `ß`/`ss`,
  accents, hyphens, a trailing space, number against text and boolean against text.
- **The refusal.** `RepeatedKeyException`, an `ArgumentException` for `rows` as the push's other
  refusals of its data are, with `Table`, `KeyColumn`, `Key` (as the first of its rows holds it),
  `FirstRow`, `SecondRow` (from 0, as the push's other messages count rows) and `Change`. The message
  writes the key as a Formula writes the constant (ADR-0058): `The snapshot of 'Positions' is refused:
  its key column 'Id' holds "r-1" in row 0 and "R-1" in row 1, which XLOOKUP does not tell apart. The
  table waits for a snapshot in which no key repeats, and every Formula that reads it shows
  #GETTING_DATA.` `SheetRefusedException` was not used: its contract is that nothing changed, and this
  refusal changes the table back to waiting. The table's rows are dropped and its readers recalculated
  before the throw; the exception carries that `SheetChange`, because the component references only
  the engine's public API and has to repaint those readers. `ExSheet.PushLinkedTableAsync` applies it
  and rethrows, so the Consumer's `await` sees the refusal after the readers show `#GETTING_DATA`.
  A push refused for another reason (a short row, `#GETTING_DATA` in the data) still changes nothing.
- **Tests.** Layer 1, `LinkedTableTests` (SH-33): a clean snapshot; `R-1` twice, after an earlier
  good snapshot, with no earlier Value shown; `r-1`/`R-1`; the number 1, the text `1`, `TRUE` and the
  text `TRUE` as four keys; blanks; `IFERROR(XLOOKUP(…), 0)` over a refused table; a redeclaration
  with the same key spelt otherwise (nothing changes), with another key (rows dropped, readers wait,
  the new key is the one checked) and with none; the key in the document, a version 6 document
  without one, and five bad keys in a document. `SheetDocumentTests` and `ColumnWidthTests` expect
  version 7 written and 8 unknown. Layer 2, `LinkedTableWiringTests`: the key is readable, recorded
  and raised once, and another key raises again; a refused push throws by name and repaints every
  reader as `#GETTING_DATA`, and the next clean push reads again.
- **`/sheet` and `/sheets`** declare `Positions` with the key `Id`, in the opening documents and, on
  `/sheet`, at start-up too. They build and layers 1 and 2 pass; they were not opened in a browser
  here (layer 3 was not run, as briefed).
