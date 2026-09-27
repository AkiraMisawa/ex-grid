# 16: Linked Tables

Status: ready-for-agent

**What to build:** The Consumer declares a Linked Table by name and columns, and pushes whole snapshots. Formulas
read it with structured references and `XLOOKUP`. Until the first snapshot arrives, readers show
`#GETTING_DATA`. That propagates, `IFERROR` and `ISERROR` do not catch it, and a copy reaching it is
refused. A new snapshot recalculates only its readers. Table names join completion.

**Blocked by:** 04, 14

- [ ] `=SUM(Positions[PV])` and `XLOOKUP` by key read the snapshot (ADR-0049)
- [ ] Before the first push: `#GETTING_DATA`, and `=IFERROR(…, 0)` also shows `#GETTING_DATA`
- [ ] A snapshot replaces the previous one in one step; no recalculation sees a mix
- [ ] A copy reaching a waiting cell is refused
- [ ] An undeclared table name is `#NAME?`
- [ ] The DemoHost shows a Sheet reading the data an ExGrid on the same page shows

## Comments

2026-09-27, engine half: `Sheet.DeclareLinkedTable(name, columns)` declares a table before any
rows arrive, and `Sheet.PushLinkedTable(name, rows)` replaces its data with a whole snapshot,
built completely before it replaces the last, then recalculates only the Formulas that name the
table and what depends on them (counted through `SheetChange.Recalculated`). Structured
references `Name[Column]` (and `Name[[Column With Spaces]]`) read a column as a range: the
aggregates skip text and blanks in it as in a range of cells, `COUNTA` counts them, Error Values
propagate, and `XLOOKUP` looks up by key in one column and returns from another (or from a
range of cells of the same length). Before the first snapshot every Formula that names the
table is `#GETTING_DATA` whatever it would compute, the wait propagates to every dependent, and
`IFERROR` and `ISERROR` do not catch it; a name never declared is `#NAME?`; a copy reaching a
waiting cell is refused (`SheetRefusalReason.WaitingForData`). Names and columns match without
regard to case; a name Excel refuses for a Table (a cell address, `R1C1`, `R`, `C`, `TRUE`) or a
structured reference cannot carry is refused on declaration. `Sheet.LinkedTables` lists them
for completion (ticket 10) (`LinkedTableTests`). Covers the engine side of the first five
criteria. **What remains is the component's:** declaring and pushing from the Consumer's
parameters, the layer 2 half of SH-16, and the DemoHost page reading the data an ExGrid on the
same page shows. Reported for a decision: a column the table does not have is `#REF!` (Excel
refuses such a Formula on entry, but a declaration can change); a column of more than one row
used as one Value is `#VALUE!`, of exactly one row reads as that Value; declarations are not
recorded in the Sheet Document, so an opened document shows `#NAME?` until the Consumer
declares its tables, and `#GETTING_DATA` from then until it pushes; a table cannot be declared
twice or undeclared.
