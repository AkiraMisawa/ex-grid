# 16: Linked Tables

Status: done

**What to build:** The Consumer declares a Linked Table by name and columns, and pushes whole snapshots. Formulas
read it with structured references and `XLOOKUP`. Until the first snapshot arrives, readers show
`#GETTING_DATA`. That propagates, `IFERROR` and `ISERROR` do not catch it, and a copy reaching it is
refused. A new snapshot recalculates only its readers. Table names join completion.

**Blocked by:** 04, 14

- [x] `=SUM(Positions[PV])` and `XLOOKUP` by key read the snapshot (ADR-0049)
- [x] Before the first push: `#GETTING_DATA`, and `=IFERROR(…, 0)` also shows `#GETTING_DATA`
- [x] A snapshot replaces the previous one in one step; no recalculation sees a mix
- [x] A copy reaching a waiting cell is refused
- [x] An undeclared table name is `#NAME?`
- [x] The DemoHost shows a Sheet reading the data an ExGrid on the same page shows

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

2026-09-27, decided with the user (ADR-0049): declaring a table again is no longer refused. An
identical declaration changes nothing; other columns replace the held one and drop its rows.
`Sheet.DeclareLinkedTable` implements it; `LinkedTableTests` pins both.

2026-09-27, ExSheet wiring: the component has three new public members.
`DeclareLinkedTableAsync(name, columns)` and `PushLinkedTableAsync(name, rows)` delegate to the
engine and run on the renderer's dispatcher. `LinkedTables` lists the declarations. The engine's
`SheetChange` goes through the same path as an edit, so only the rows whose Values changed get
new instances, and every other row skips its render. A declaration that changes what is declared
raises `DocumentChanged`, because the Sheet Document records declarations. A snapshot never does,
because the document never holds a table's rows. Neither is a step on the undo stack: both are the
Consumer's data, not a user's operation. The engine's exceptions (a bad name, an undeclared table,
a ragged row) reach the caller. Completion offers the tables' names through `Sheet.Complete`
(ticket 10). Layer 2, in `LinkedTableWiringTests`: `#NAME?` undeclared, then `#GETTING_DATA`
through `IFERROR`; `SUM` and `XLOOKUP` over a snapshot; a newer snapshot replacing the last;
render counts; what is raised and what is not; `=SUM(Po` offering `Positions`; and a push to an
undeclared table refused. The DemoHost's `/sheet` page now has a positions ExGrid beside the
Sheet, fed from the page's own array. The Sheet reads it as `Positions`: `=SUM(Positions[PV])`,
`=XLOOKUP("R-4471", Positions[Id], Positions[PV])` and `=COUNTA(Positions[Id])`. The first
snapshot is pushed 1.5 s after the first render, so `#GETTING_DATA` shows first, and *Revalue*
pushes a new snapshot that both show. It was checked in a real Chromium on both hosts with a
scratch spec (not committed): `#GETTING_DATA` shows, then the values, revalue, the table offered
by completion, pointing, the Context Menu insertion and a fill drag, all with a clean console.
**Still open:** the fourth criterion, a copy reaching a waiting cell being refused. It is blocked
on the core's copy hook (ticket 14's comment): today such a copy carries `#GETTING_DATA` as text.

2026-09-27, ExSheet wiring, the copy: the fourth criterion is met. ExSheet answers ExGrid's copy
(`CopyAnswer`, ADR-0050 item 9) from the engine's `Sheet.Copy`, so a copy reaching a waiting cell
is refused with the engine's sentence ("A2 is waiting for a Linked Table's data
(#GETTING_DATA); ..."), the grid announces it, ExSheet's notice says it, and the clipboard keeps
what it held (`ClipboardWiringTests.A_copy_reaching_getting_data_is_refused_in_the_engines_words`).
Every criterion is met.

2026-09-27, layer 3 (ticket 18), run locally under xvfb with Playwright's Chromium (build 1194; this machine has neither Google Chrome nor Edge, so the committed config's `chrome` and `msedge` projects are CI's to run), against the WebAssembly host and the Server host behind the latency proxy. `sheet.spec.mjs`: B11 and B12 read `#GETTING_DATA` until the page's first snapshot, then 1189.4 and 318.25 (COUNTA 5), and Revalue changes B12 to 321.43 (SH-16, SH-18). Green on both hosts. Seen along the way: every change to the Sheet clears ExSheet's notice, a Consumer's push included, so a refusal said just before a push disappears with it.
