# 36: A Linked Table's key, checked on every snapshot

Status: ready-for-agent

**What to build:** ADR-0049's note of 2026-09-30. A Linked Table may be declared with one key column.
Every snapshot of a keyed table is checked, and one in which a key repeats is refused by name.

**Blocked by:** None (can start immediately)

- [ ] `DeclareLinkedTableAsync(name, columns, key)` on ExSheet, and `Sheet.DeclareLinkedTable` in the
      engine (`Sheet.LinkedTables.cs`). The key names one of the columns; any other name is refused
      by name, as a repeated column name already is (`WhyNotALinkedTable`) (SH-33)
- [ ] The Sheet Document records the key with the declaration, and a document without one still
      opens. A declaration with another key replaces the held one: rows dropped, readers wait (SH-33)
- [ ] On push, a keyed table's key column is checked. Values are compared as `XLOOKUP`'s exact match
      compares them (`SameKindEqual`), so case does not tell keys apart, and blanks are not compared
      (SH-33)
- [ ] A repeated key refuses the snapshot: the push throws, naming the table, the key column and a
      repeated value. The table goes back to waiting, and every reader shows `#GETTING_DATA`. No
      earlier snapshot's value is shown (SH-33)
- [ ] The declared key is readable from the Sheet, for the Scope (ticket 37)
- [ ] Layer 1: a clean snapshot; a repeat (`R-1` twice); a repeat by case (`r-1`, `R-1`); a number and
      text of the same digits (not a repeat); blanks; `IFERROR(XLOOKUP(…), 0)` over a refused table
      shows `#GETTING_DATA`, never 0; a redeclaration with another key (SH-33)
- [ ] `/sheet` and `/sheets` declare `Positions` with the key `Id`
