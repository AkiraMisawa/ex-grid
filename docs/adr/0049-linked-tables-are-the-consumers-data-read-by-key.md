# Linked Tables: the Consumer's data, pushed whole and read by key

*(Decided with the user, 2026-09-27, in the design grilling that started ExSheet. It began as "it
would be good if a Formula could read cells of another grid outside the Sheet". This ADR is what
that became.)*

A Formula in ExSheet can read data the application holds: the positions an ExGrid on the same
screen is showing, or FX rates from a service. **The data reaches the Formula as a Linked Table.**
The Consumer supplies a named table of rows, and Formulas read it with Excel's structured
references:

```
=SUM(Positions[PV])
=XLOOKUP("R-4471", Positions[Id], Positions[PV])
```

This is the ground [ADR-0046](./0046-exsheet-is-a-general-purpose-sheet-drawn-by-exgrid-as-its-consumer.md)
gives for building ExSheet at all.

## Three rules

**1. ExSheet never reads another component instance.** The data comes from the Consumer, which
already holds it: the same source that feeds its ExGrid. An ExGrid holds only its Window
([ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md)). Row 50,000 of the
positions grid is not in it, so reaching into the grid could not work even in principle. It would
also tie two instances together, and [ADR-0018](./0018-multiple-instances-must-be-independent.md)
keeps instances independent.

**2. A Linked Table is read by key, never by position.** A reference like `Positions!B5` is not
offered. The user of the positions grid can sort it, and then the same Formula would read another
row's value with nobody having edited anything. A single row is reached through a function that
matches a key (`XLOOKUP`). A whole column is reached by name (`Positions[PV]`), and its order does
not matter to the aggregates that take it.

**3. It is Excel's syntax, not a new one.** Structured references are how Excel names a column of
a Table (a `ListObject`), so no new grammar is invented. Excel's own term for reading another
workbook, an "external reference", is avoided because it means that, and a Linked Table is not a
workbook.

## Arrival: whole snapshots, and waiting is not an error you can catch

**The Consumer pushes a Linked Table as one whole snapshot**, whenever it has one. ExSheet never
calls back and waits in the middle of a recalculation. That is the push model of ADR-0001, and it
makes "not here yet" cheap. A Linked Table can be declared by name and columns before any rows
arrive.

- **Until the first snapshot arrives, a Formula that reads the table shows `#GETTING_DATA`**,
  Excel's own Error Value for data on its way. It never shows 0, and it never shows an older value.
  A table name that was never declared is `#NAME?`, as for any unknown name.
- **Waiting propagates, and cannot be caught.** Every Formula that depends on a waiting one also
  waits. **`IFERROR` and `ISERROR` do not treat `#GETTING_DATA` as an error.** In Excel they do, so
  `=IFERROR(XLOOKUP(…), 0)` shows 0 while the data is in transit: a plausible number standing in
  for one that does not exist yet. **This is a deliberate difference from Excel**, and it is
  documented where the two functions are.
- **A table is never partly there.** A snapshot replaces the previous one in one step, and a
  recalculation sees either the old snapshot or the new one. Pushing rows in batches is not offered:
  a `SUM` over the half that had arrived would be a plausible, wrong total.
- **A copy that reaches a waiting cell is refused**, as a copy is refused rather than truncated
  ([ADR-0005](./0005-copy-refuses-rather-than-truncates.md)). The clipboard would carry an
  `#GETTING_DATA` that means nothing where it lands.
- **A newer snapshot replaces the old one**, and only the Formulas that read the table
  recalculate. The Sheet Document holds the Formulas that name the table and never the table's
  rows ([ADR-0048](./0048-a-sheet-document-holds-entries-and-exsheet-holds-the-one-undo-stack.md)).
  Opening a document therefore shows `#GETTING_DATA` until the Consumer pushes.

## Not in the first version

- **Partial or paged tables**, and **aggregates computed where the data lives** (a server summing
  a hundred million rows and sending back the total). Both need the engine to reason about a table
  it does not have in full, and they are the next step once a Consumer needs them.
- **Another ExSheet as a source.** Two ExSheets reading each other are two Sheets of one workbook
  (ADR-0046), and a workbook is where that belongs.
- **Writing back.** A Linked Table is read-only to Formulas and to the user.

## Settled while building *(2026-09-27, decided with the user)*

- **The Sheet Document records each Linked Table's declaration: its name and its column names,
  never its rows.** Opening a document therefore shows `#GETTING_DATA` until the Consumer pushes,
  as this ADR says. It does not show `#NAME?` until the Consumer declares again.
- **A column the table does not have is `#REF!`.**
- **A table column used where one value is expected** gives its value when the column has exactly
  one row, and `#VALUE!` otherwise. Implicit intersection is refused for the same reason
  [ADR-0047](./0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md)
  refuses it.
- **In the first version a table is declared once and never undeclared.**

- **Declaring a table again** *(decided with the user the same day)*. A Consumer declares its
  tables at start-up, and a document it opens may already carry the declarations.
  - A declaration identical to the one held, with the same name and the same columns, changes
    nothing.
  - One with other columns replaces the held declaration, because the table's shape is the
    Consumer's. The rows held so far are dropped, readers wait again with `#GETTING_DATA`, and a
    Formula naming a column that is gone reads `#REF!`. Nothing turns into a plausible value.

- **An unknown table or column is still accepted on entry** *(decided with the user, 2026-09-27)*.
  Excel refuses such a Formula on entry. ExSheet shows `#NAME?` (no such table) or `#REF!` (no
  such column) instead, because a Linked Table can be declared after the Sheet Document is opened.
  Refusing on entry would make a saved document impossible to reopen before its tables arrive.

## Outlined by whoever shows the table *(2026-09-29, decided with the user)*

While a Formula that reads a Linked Table is edited, **ExSheet tells its Consumer which of the
table's columns the Formula reads, and in which colour**, so that the grid showing the table can
outline them as Excel outlines a Table's column
([ADR-0057](./0057-references-are-outlined-in-colour-while-a-formula-is-edited.md)). Rule 1 holds:
ExSheet reaches no other instance. The Consumer passes the notification on, because only it knows
which grid shows the table, and whether that grid shows all of the rows the Formula reads.

## A key, declared with the table, and checked on every snapshot *(2026-09-30, decided with the user)*

A Pointing Scope ([ADR-0058](./0058-a-formula-points-across-grids-through-a-pointing-scope.md)) writes
what reads a pointed cell: `XLOOKUP("R-4471", Positions[Id], Positions[PV])` for a cell,
`Positions[PV]` for a column. It never writes an address, so rule 2 holds. Reading a cell by key
needs a key, and `XLOOKUP` over a column in which a key repeats returns the first match without a
word. That is a plausible wrong value.

- **A Linked Table may be declared with a key column**, one of its columns:
  `DeclareLinkedTableAsync(name, columns, key)`. It is declared once, with the table, and not per grid
  that shows the table. The Scope reads it from the declaration of the Sheet that points. The Sheet
  Document records it with the rest of the declaration. A declaration with another key is a
  declaration with another shape, and it replaces the held one as other columns do (above).
- **One column.** A table whose rows are told apart by several columns gets a column that joins them
  (`ACME|5Y`) from its Consumer, and that column is the key. ADR-0058 records why several columns wait,
  and the test that fails when they no longer need to.
- **Every snapshot of a keyed table is checked.** No value may appear twice in the key column. Values
  are compared as `XLOOKUP`'s exact match compares them, so `r-4471` and `R-4471` are the same key. A
  blank key is not a key: any number of rows may have none, and a press on such a row's cell writes
  nothing (ADR-0058).
- **A snapshot in which a key repeats is refused by name.** The push throws, naming the table, the key
  column and a value that repeats. The table goes back to waiting, so every Formula that reads it
  shows `#GETTING_DATA`, and `IFERROR` does not catch that (above). The previous snapshot is not kept:
  it would show an older value, which this ADR refuses.
  - **A catchable Error Value was rejected.** `#VALUE!` in every reader would be caught by the common
    `IFERROR(XLOOKUP(…), 0)` and show 0.
  - **An Error Value of ExSheet's own was rejected.** `#GETTING_DATA` already means what a reader needs
    to know, that there is no table to read yet. The reason is the Consumer's to act on, and the
    refusal names it.
