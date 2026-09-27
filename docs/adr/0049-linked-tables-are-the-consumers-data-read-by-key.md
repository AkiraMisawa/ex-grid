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
