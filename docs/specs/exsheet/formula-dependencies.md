# Formula dependencies: Excel beside ExSheet

Status: research note (2026-10-01). It decides nothing. Where ExSheet has no decision, the last
section names the decision for the user to make.

**Question.** How does Microsoft Excel handle formula dependencies, how does ExSheet handle them
today, where do the two agree or differ, and what has ExSheet not decided?

**Sources.** Excel's side is Microsoft's own documentation, cited by URL. ExSheet's side is this
repository, cited by path and line, as of the working tree on 2026-10-01.

**How each claim is marked:**

- **[MS]** is documented by Microsoft at the cited URL.
- **[observed]** was seen in a real Excel by this project's Windows runs: Microsoft 365
  16.0.20326.20158, recorded under `verification/2026-09-2*-windows-excel*/`. It is not documented
  behaviour, and a later build may differ.
- **[unconfirmed]** comes only from a secondary source, or from reasoning, and no primary source
  was found for it.
- **[ExSheet]** is from this repository: an ADR, a spec, an issue, or the engine's code and tests.

Some Microsoft pages were read through a summarising fetch. Text in quotation marks is
Microsoft's wording as fetched; anything else from those pages is a paraphrase.

## Summary

| Topic | Excel | ExSheet today | Verdict |
|---|---|---|---|
| What a dependency is | Each formula's precedents are tracked in a dependency tree, and a calculation chain orders the formulas [MS] | A graph from each cell, or each rectangle, to the Formulas that name it; Linked Table readers are tracked by table name [ExSheet] | agree |
| Recalculation scope | Only dirty cells, their dependents, and volatile functions ("smart recalculation") [MS] | Only the changed cells and what they reach, in dependency order, counted [ExSheet] | agree |
| Recalculation order | A dynamic calculation chain; a formula can be calculated more than once per pass [MS] | A topological order (Kahn), each Formula once per change [ExSheet] | differ in mechanism, same result |
| Dependents of an unchanged value | Still recalculated [MS] | Still recalculated: there is no pruning on an unchanged value [ExSheet] | agree |
| Calculation modes | Automatic, Automatic except data tables, Manual; F9, Shift+F9, Ctrl+Alt+F9, Ctrl+Alt+Shift+F9 [MS] | Always automatic. There is no mode, and no key [ExSheet] | undecided |
| Volatile functions | NOW, TODAY, RAND, RANDBETWEEN, OFFSET, INDIRECT, CELL, INFO, and others [MS] | None is in the declared function set, and there is no volatility mechanism [ExSheet] | undecided |
| Circular reference, display | A warning dialog; the cell shows 0 or its last value; "Circular References: A1" in the status bar [MS, observed] | `#CIRC!` in every member and every dependent; `IFERROR` does not catch it [ExSheet] | differ by decision (ADR-0047) |
| Circular reference, detection | "detects the circular reference and warns" [MS] | Static, from the Formula text: a branch that is not taken still counts [ExSheet] | differ by decision (ADR-0047); Excel's rule for a branch not taken is unobserved |
| Iterative calculation | Optional; 100 iterations and a change of 0.001 by default [MS] | None [ExSheet] | undecided |
| Error propagation | Error values flow into the formulas that use them [observed] | The same, checked against Excel by the case corpus [ExSheet] | agree |
| Blank precedents | A dependency on an empty cell exists, so typing into it recalculates [unconfirmed as a documented rule] | The dependency is on the address, whether or not a cell is there [ExSheet] | agree (Excel's side is assumed) |
| Insert and delete rows and columns | References follow their cells, and a deleted target becomes `#REF!` [MS, observed] | The same, checked against Excel [ExSheet] | agree |
| Moving a formula, or the cells it reads (cut and paste) | A moved formula keeps its references [MS]. What happens to the dependents of moved cells is not found in Microsoft's pages | There is no cut or move operation [ExSheet] | undecided |
| Copy and fill | Relative references shift and absolute ones stay [MS] | The same; a reference shifted off the Sheet is `#REF!` [ExSheet] | agree |
| Pasting over a referenced cell | Can cause `#REF!` ("pasted over") [MS] | A paste writes Entries; dependents recompute [ExSheet] | unverified |
| Other sheets and workbooks | Cross-sheet and cross-workbook dependencies are tracked [MS] | One Sheet. Another sheet's qualifier is `#REF!` and is not a dependency; Linked Tables stand in for external data [ExSheet] | differ by decision (ADR-0046, ADR-0049) |
| Defined names | A name is a dependency; adding, changing or deleting one triggers a recalculation [MS] | No defined names; an unknown name is `#NAME?` [ExSheet] | undecided (out of scope so far) |
| Undo | Ctrl+Z restores deleted rows and columns [MS] | One step per operation; undo restores Entries and structure, then recalculates [ExSheet] | agree |
| Spilled arrays | A spill updates with its source, the `#` operator refers to it, and `#SPILL!` reports a block [MS] | Refused: a multi-cell result is `#VALUE!` [ExSheet] | differ by decision (ADR-0047) |
| Partial results | Not shown: a manual-mode sheet says "Calculate" [MS] | No Value is published from an unfinished recalculation [ExSheet] | agree in spirit; the layer 2 half is open |
| Threads and limits | Multithreaded; 4 billion formulas may depend on one cell [MS] | Single-threaded and synchronous; no limit is stated; no measurement is recorded [ExSheet] | undecided (SH-19) |

## 1. Dependency tracking

### Excel

- **[MS]** Calculation is "a three-stage process": building a dependency tree, building a
  calculation chain, and recalculating cells. "The dependency tree informs Excel about which cells
  depend on which others." When a structural change is made, such as entering a new formula,
  "Excel reconstructs the dependency tree and calculation chain."
  (https://learn.microsoft.com/en-us/office/client-developer/excel/excel-recalculation)
- **[MS]** The smart recalculation engine tracks "both the precedents and dependencies for each
  formula (the cells referenced by the formula)." Names are part of it: a name that changed, or
  depends on something that needs recalculation, is recalculated with its dependents.
  (https://learn.microsoft.com/en-us/office/vba/excel/concepts/excel-performance/excel-improving-calculation-performance)
- **[MS] Whole columns.** "Many Excel built-in functions (**SUM**, **SUMIF**) calculate whole column
  references efficiently because they automatically recognize the last used row in the column".
  Array formulas over a whole column calculate every cell, "including empty cells".
  (https://learn.microsoft.com/en-us/office/vba/excel/concepts/excel-performance/excel-tips-for-optimizing-performance-obstructions)
- **[MS] Defined names.** "A defined name is evaluated every time that a formula that refers to it
  is evaluated". "Names that are not referred to by any formula are not calculated even by a full
  calculation." (improving-calculation-performance, as above)
- **[MS] Other sheets and workbooks.** Each recalculation "resolves any dependencies within and
  between workbooks and worksheets" (improving-calculation-performance). The limits table allows
  "64,000 worksheets that can refer to other sheets"
  (https://support.microsoft.com/en-us/office/excel-specifications-and-limits-1672b34d-7043-467e-8e27-269d656771c3).
- **[MS] Auditing.** Trace Precedents and Trace Dependents draw arrows between a formula and the
  cells it reads or that read it: blue for no error, red for a cell that causes one
  (https://support.microsoft.com/en-us/office/display-the-relationships-between-formulas-and-cells-a59bef2b-3701-46bf-8ff1-d3518771d507).

### ExSheet

- **[ExSheet]** "The engine keeps a dependency graph from each cell to the cells whose Formulas
  read it." (`docs/adr/0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md:72`)
- **[ExSheet] Representation.** A single-cell Reference is held in `_cellDependents` (target to
  readers). A multi-cell rectangle, `A:A` and `1:1` included, is held per reading Formula in
  `_areaPrecedents`, and it is matched by `Area.Contains` when the dependents of a cell are asked
  for (`src/ExSheet.Engine/Sheet.cs:24-28`, `:312-331`, `:350-366`). Whole columns are therefore
  never expanded into cells.
- **[ExSheet] Linked Tables.** A structured reference (`Positions[PV]`) registers its Formula as a
  reader of the table by name. A new snapshot recalculates only those readers and what depends on
  them (`src/ExSheet.Engine/Sheet.LinkedTables.cs:90-134`;
  `docs/adr/0049-linked-tables-are-the-consumers-data-read-by-key.md:61-64`). Until the first
  snapshot arrives, readers and their dependents are `#GETTING_DATA`, which `IFERROR` does not
  catch (ADR-0049 `:47-55`; `Sheet.cs:458-478`, `Taint`).
- **[ExSheet] Other sheets.** One Sheet exists. A qualifier naming this Sheet is local
  (`src/ExSheet.Engine/Sheet.Name.cs:48-57`; NAME-009/010 make a cycle through `Sheet1!`). Any other
  qualifier evaluates to `#REF!` (`src/ExSheet.Engine/Formulas/Evaluator.cs:55`) and is not
  registered as a dependency (`Sheet.cs:317`). Renaming the Sheet recalculates every Formula whose
  qualifier starts or stops naming it (`Sheet.Name.cs:40-46`, `:95-110`).
- **[ExSheet] Defined names** do not exist; an unknown name is `#NAME?`
  (`tests/ExSheet.Engine.Tests/ExcelCases/references.json`, the first cases).
- **[ExSheet] Auditing.** There is no Trace Precedents or Trace Dependents. While a Formula is
  edited, its References are outlined in colour (ADR-0057), which shows the precedents of the
  Formula being edited only.

**Verdict:** the model agrees. ExSheet has no names, no other sheets and no auditing arrows.

## 2. Recalculation: trigger and order

### Excel

- **[MS] Triggers** include entering data (in Automatic mode), inserting or deleting a row or
  column, adding, editing or deleting a defined name, renaming a worksheet, hiding or unhiding rows,
  and some AutoFilter actions (excel-recalculation). Opening a workbook in automatic mode is also a
  trigger, and opening one last calculated by another Excel version usually forces a full
  calculation (improving-calculation-performance, "Volatile actions").
- **[MS] Dirty marking.** "All direct and indirect dependents are marked as dirty"
  (excel-recalculation).
- **[MS] Order.** "Excel does not calculate cells in a fixed order, or by row or column." If a
  formula depends on one not yet calculated, "the formula is sent down the chain to be calculated
  again later. This means that a formula can be calculated multiple times per recalculation."
  (improving-calculation-performance)
- **[MS] No pruning.** "Excel continues calculating cells that depend on previously calculated cells
  even if the value of the previously calculated cell does not change when it is calculated."
  (improving-calculation-performance)
- **[MS]** A formula is evaluated immediately when it is entered or edited, even in manual mode
  (improving-calculation-performance, "Formula and name evaluation circumstances").

### ExSheet

- **[ExSheet]** `Recalculate` works in three steps (`src/ExSheet.Engine/Sheet.cs:370-455`):
  1. It collects everything the change reaches, breadth first.
  2. It orders the Formulas to recompute topologically (Kahn) and evaluates each once into a
     staging set.
  3. It publishes every staged Value at once.

  There is no pruning when a value is unchanged.
- **[ExSheet]** Every mutation recalculates through that path: an entry, or several as one change
  (`Sheet.cs:286-309`); an insertion or deletion (`Sheet.Structure.cs:139-140`); a rename
  (`Sheet.Name.cs:108-109`); a Linked Table snapshot (`Sheet.LinkedTables.cs:130-134`); an undo
  (`Sheet.Steps.cs:59-72`). Opening a Sheet Document recomputes everything, because no Value is
  stored (`Sheet.cs:53-79`; ADR-0048 `:18-28`).
- **[ExSheet] Tests:** only dependents recompute, counted; a diamond recomputes each cell once,
  after its precedents; a long chain does not overflow the stack; a batch publishes only end-state
  Values (`tests/ExSheet.Engine.Tests/RecalculationTests.cs:9-100`; issue 03 `:12-16`, `:20-28`).
- **[ExSheet]** "No Value is ever shown from a recalculation that has not finished" (ADR-0047 `:73-75`).
  The engine half is done. The component half, that no Window is pushed with unfinished Values, is
  still open in layer 2 (`docs/specs/exsheet/issues/03-references-and-recalculation.md:16`, `:28-29`).

**Verdict:** the results agree. Excel's chain may evaluate a formula more than once; ExSheet's
topological order evaluates each Formula once.

## 3. Calculation modes

- **[MS]** There are three modes: Automatic, Automatic Except Tables, and Manual
  (excel-recalculation). The keys
  (https://support.microsoft.com/en-us/office/change-formula-recalculation-iteration-or-precision-in-excel-73fc7dac-91cf-4d36-86e8-67124f6bcce4;
  excel-recalculation):
  - **F9** recalculates the changed formulas in all open workbooks.
  - **Shift+F9** recalculates the active worksheet.
  - **Ctrl+Alt+F9** recalculates all formulas.
  - **Ctrl+Alt+Shift+F9** rebuilds the dependencies, then recalculates everything.

  In manual mode, the status bar shows **Calculate** when a recalculation is needed
  (improving-calculation-performance).
- **[MS]** The mode and the iteration settings are application-wide, set from the first workbook
  opened, and saved into each workbook (improving-calculation-performance, "Controlling
  calculation options").
- **[ExSheet]** There is no mode. Every change recalculates synchronously before it returns
  (`Sheet.cs:286-309`). No ADR, spec or issue mentions manual calculation or F9 (a search of
  `docs/` and `CONTEXT.md` found none). Excel's data tables (What-If) have no counterpart either.

**Verdict: undecided.** With no volatile functions and no iteration, Automatic is the only mode
that changes anything today.

## 4. Volatile functions

- **[MS]** "A volatile function is always recalculated at each recalculation even if it does not
  seem to have any changed precedents." The volatile functions are RAND, NOW, TODAY, OFFSET, CELL,
  INDIRECT and INFO. INDEX, ROWS, COLUMNS and AREAS are "not in fact volatile"
  (improving-calculation-performance). The older reference also lists RANDBETWEEN, and INFO, CELL
  and SUMIF "depending on its arguments" (excel-recalculation; that page applies to Excel 2013).
- **[MS]** With dynamic arrays, a size that keeps changing between passes resolves to `#SPILL!`.
  This is "generally associated with the use of RAND, RANDARRAY, and RANDBETWEEN"
  (https://support.microsoft.com/en-us/office/-spill-volatile-size-05aad07c-947e-4c9b-bd6f-7b1f8ae6a7dc).
- **[ExSheet]** The declared functions are SUM, AVERAGE, MIN, MAX, COUNT, COUNTA, IF, ROUND, IFERROR,
  ISERROR and XLOOKUP (ADR-0047 `:23-26`). None of them is volatile, and the engine has no notion of
  volatility: a search of `src/ExSheet.Engine` for `Volatile`, `NOW`, `TODAY`, `RAND`, `OFFSET` and
  `INDIRECT` found nothing. Any of these functions typed today is `#NAME?`.
- **[ExSheet] Related.** ADR-0048 stores no Values, so a volatile result could never go stale in a
  document. But "the same Entries give the same Values" would no longer hold.

**Verdict: undecided.** It becomes a decision the day any volatile function is proposed under
ADR-0047's admission rule.

## 5. Circular references

### Excel

- **[MS]** "If a cell depends, directly or indirectly, on itself, Excel detects the circular
  reference and warns the user" (excel-recalculation).
- **[MS]** After the warning is closed, the cell shows "either 0 or the last calculated value". The
  status bar shows "Circular References" with an address, or without one when the cycle is on
  another sheet. Formulas > Error Checking > Circular References lists the cells
  (https://support.microsoft.com/en-us/office/remove-or-allow-a-circular-reference-in-excel-8540bd0f-6e97-4483-bcf7-1b49cd50d123).
- **[MS] Iteration** is off by default ("Excel cannot calculate a formula that refers to its own
  cell"). When it is on, the defaults are a Maximum Iterations of 100 and a Maximum Change of 0.001
  (change-formula-recalculation page; circular-reference page). The limit on iterations is 32,767
  (specifications-and-limits). Circular references and iterative data tables always calculate on
  one thread (improving-calculation-performance).
- **[observed]** Typing `A1: =B1`, then `B1: =A1`, shows one dialog when the cycle closes. A1 and B1
  show 0, `C1: =IFERROR(A1,0)` shows 0 with no second dialog, the status bar reads
  `Circular References: A1`, and blue tracer arrows join A1 and B1
  (`verification/2026-09-27-windows-excel/results.md:320-330`; `behaviours.md:122-123`).
- **[observed]** Through COM, with A1 `=B1+1`, B1 `=A1`, C1 `=A1*2`, D1 `=C1&"x"`: A1 shows 1, B1 0,
  C1 2 and D1 `2x`, "one pass of stale values". The other cyclic cases show 0
  (`verification/2026-09-27-windows-excel/results.md:228-231`). Runs 2 to 4 repeated this
  (`verification/2026-09-29-windows-excel-4/results.md:214-215`).
- **[unconfirmed]** No Microsoft page found says whether a reference in an IF branch that is not
  taken closes a cycle for Excel's detection.

### ExSheet

- **[ExSheet] Decision.** A cycle is `#CIRC!` in every member and every dependent. It is the only
  Error Value ExSheet adds. Excel's 0 is rejected as "the quietly wrong answer", and `#REF!` as
  misleading (ADR-0047 `:63-68`). `IFERROR` and `ISERROR` do not catch it, and a branch that is not
  taken still counts: `=IF(TRUE, 1, A1)` is `#CIRC!` (ADR-0047 `:114-119`). Typed, `#CIRC!` stays text
  (ADR-0047 `:173-175`). `CONTEXT.md:609-613` defines it.
- **[ExSheet] Detection is static.** The edges come from the References in the Formula text. A
  Formula that never becomes ready in the Kahn pass is a member of a cycle or downstream of one, and
  it is staged as `#CIRC!` (`Sheet.cs:396-434`). A Formula that reads a `#CIRC!` cell is tainted
  before it is evaluated (`Sheet.cs:458-478`).
- **[ExSheet] Cases.** Members and dependents (ERR-078..081, 083, 084); a range containing its own
  cell, `=SUM(A:A)` in A1 (ERR-093); breaking a cycle recovers (ERR-085..); two cycles stay
  independent (ERR-091/092); IF-015; IFERROR-010; STRUCT-021..023 (deleting a cell of a cycle breaks
  it). See `tests/ExSheet.Engine.Tests/ExcelCases/errors.json:624-770`, `if.json:134-137`,
  `iferror.json:89-92`, `structure.json:187-210`. Each is marked `engineDiffersByDecision`.
  Reopening a document yields `#CIRC!` again (`RecalculationTests.cs:102-115`).
- **[ExSheet]** There is no iterative calculation, no status-bar notice and no dialog. The display
  differs by decision (`excel-behaviours.md:123-124`, item 27).

**Verdict: differ by decision.** Iteration is undecided.

## 6. Error propagation through dependents

- **[MS]** Microsoft's pages describe causes of errors, such as `#REF!` "when a formula refers to a
  cell that's not valid" (https://support.microsoft.com/en-us/excel/how-to-correct-a-ref-error). No
  single Microsoft page was found that states the general propagation rule.
- **[observed]** The case corpus pins propagation against Excel, including left-to-right precedence
  (`ErrorPropagationTests`, issue 03 `:26-27`; oracle runs listed in ADR-0047 `:159-360`).
- **[ExSheet]** "Error Values are data. They propagate through every Formula that uses them, as in
  Excel" (ADR-0047 `:59-61`). There are two deliberate exceptions, which `IFERROR` and `ISERROR` do
  not catch: `#CIRC!` and `#GETTING_DATA` (ADR-0047 `:114-116`; ADR-0049 `:49-55`). Excel's
  `IFERROR` does catch `#GETTING_DATA`, by ADR-0049's own account [unconfirmed against a Microsoft
  page].

**Verdict: agree,** apart from the two documented exceptions.

## 7. Empty and blank precedents

- **[MS]** Smart recalculation recalculates "cells, formulas, values, or names that have changed",
  and their dependents (improving-calculation-performance). A cell that changes from empty to a
  value is a changed value. No Microsoft page found says outright that a dependency on an empty
  cell is kept, so that half is **[unconfirmed]**, though it is the plain reading.
- **[ExSheet]** A dependency is keyed on the address, whether or not a cell exists there
  (`Sheet.cs:318-323`), and an area is matched by containment (`Sheet.cs:356-365`). So typing into an
  empty cell that is read recomputes its readers. A blank is not a fifth kind of Value; it is 0 in
  arithmetic and ignored by SUM over a range (ADR-0047 `:43-45`). `IFERROR` over an empty cell gives
  an empty value, not 0, as observed (ADR-0047 `:227-228`).

**Verdict: agree.**

## 8. Structural edits: insert, delete, move, and `#REF!`

### Excel

- **[MS]** Inserting or deleting rows, columns or cells is a "volatile action" that triggers a
  recalculation (improving-calculation-performance). Deleting a column that `=SUM(B2,C2,D2)` reads
  gives `=SUM(B2,#REF!,C2)`, while a range reference "automatically adjust[s]"
  (how-to-correct-a-ref-error). `#REF!` also follows when referenced cells are "pasted over" (the
  same page, as summarised).
- **[MS]** "When you move a formula, the cell references within the formula stay the same". When
  you copy a formula, relative references change and `$` parts stay
  (https://support.microsoft.com/en-us/office/move-or-copy-a-formula-1f5cf825-9b07-41b1-8719-bf88b07450c6).
  **Conflict:** "Move or copy cells, rows, and columns"
  (https://support.microsoft.com/en-us/excel/move-or-copy-cells-rows-and-columns) says, as fetched,
  "When you copy cells that contain a formula, the relative cell references are not adjusted". That
  contradicts the page above. Treat it as **[unconfirmed]** until Excel is observed.
- **[unconfirmed]** It is commonly stated that cutting and pasting a cell that other formulas read
  rewrites those formulas to the new address. No Microsoft page found states it.
- **[observed]** Inserting two rows above B3:C4 keeps the Selection, takes the formatting of the row
  above, and rewrites `=B3` to `=B5`. Deleting row 5 gives `=A5*2` → `=#REF!*2`,
  `=SUM(A4:A6)` → `=SUM(A4:A5)`, and `=A5` → `=#REF!`
  (`verification/2026-09-27-windows-excel/behaviours.md:112-113`).

### ExSheet

- **[ExSheet]** "Inserting or deleting rows and columns rewrites them; a deleted target becomes
  `#REF!`" (`docs/specs/exsheet/spec.md:166-167`; user stories 38 and 39, `spec.md:93-94`).
- **[ExSheet] Code.** `Restructure` moves every cell and rewrites every Reference:
  - A range moves, grows, or shrinks.
  - A range whose cells are all deleted is written `#REF!` in the stored Formula.
  - `A:A` keeps its text but is still recomputed.
  - It then rebuilds the whole graph and recalculates only the rewritten or reached Formulas
    (`src/ExSheet.Engine/Sheet.Structure.cs:1-140`; issue 13 `:19-36`).

  An insertion that would push an Entry off the Sheet is refused. A Reference pushed off is cut at
  the edge, or becomes `#REF!` (`Sheet.Structure.cs:6-15`, `:51-68`; STRUCT-026..029). That
  supersedes issue 13's first note that such an insertion was refused.
- **[ExSheet] Copy and fill** shift relative parts and keep `$` parts. A Reference shifted off the
  Sheet is `#REF!` (`src/ExSheet.Engine/Formulas/ReferenceShift.cs:1-19`; `Sheet.Fill.cs:29`).
  Copying `=A1` from B1 to B3 is checked against Excel (`excel-behaviours.md:88`, item 16).
- **[ExSheet]** There is **no cut or move**. No cut, Ctrl+X or move-cells operation was found in the
  engine, the ADRs or the spec. Pasting over a referenced cell writes a new Entry, and its readers
  recompute; whether Excel would make them `#REF!` there has not been compared.

**Verdict:** insert and delete agree, and are observed. Cut and move are undecided.

## 9. Undo

- **[MS]** After deleting rows or columns by accident, Ctrl+Z restores them (how-to-correct-a-ref-error).
  No Microsoft page was found on how undo interacts with recalculation.
- **[observed]** Excel undoes a sheet rename (ADR-0048 `:112-116`).
- **[ExSheet]** There is one undo stack, and each edit, paste, fill, insertion or deletion is one
  step (ADR-0048 `:44-51`). Undoing a structural edit applies the inverse edit, puts the rewritten
  Formulas and dropped cells back, and recalculates (`Sheet.Steps.cs:59-72`). One Ctrl+Z restores
  the structure and every Reference, in layers 1 to 3 (issue 13 `:12-15`, `:60-69`). Replacing the
  Sheet Document clears the stack (ADR-0048 `:50-51`). A Linked Table snapshot is data arriving,
  not an undoable step (ADR-0048 `:84-86`). So an undo after a newer snapshot recomputes against the
  newer data.

**Verdict: agree,** for what has been compared.

## 10. Spilled arrays (dynamic arrays)

- **[MS]** A formula returning several values spills into the neighbouring cells, and the spill
  adjusts when its source changes. Only the first cell is editable, and a blockage is `#SPILL!`
  (https://support.microsoft.com/en-us/excel/dynamic-array-formulas-and-spilled-array-behavior).
  `A2#` refers to the whole spill range and follows it as it grows or shrinks
  (https://support.microsoft.com/en-us/office/spilled-range-operator-3dd5899f-bca2-4b9d-a172-3eae9ac22efd).
  A spill can force extra calculation passes (spill-volatile-size page, as above).
- **[observed]** `=A1:A3` entered in C1 spills to C1:C3 (`verification/2026-09-27-windows-excel/results.md`, §7).
- **[ExSheet]** A multi-cell result is `#VALUE!`. It is never implicitly intersected, so that
  spilling can arrive later without changing any written sheet, and spilling gets "an ADR of its
  own" (ADR-0047 `:108-113`; `spec.md:271-274`). Typed `#SPILL!` is data only (ADR-0047 `:223-226`).

**Verdict: differ by decision.** The dependency model a spill would need (a range whose extent is
a computed Value) is undecided.

## 11. Performance and limits

- **[MS]** Multithreaded recalculation (since Excel 2007) runs independent parts of the chain on up
  to 1024 threads. INDIRECT, and CELL with "format" or "address", are among the functions that are
  not thread-safe
  (https://learn.microsoft.com/en-us/office/client-developer/excel/multithreaded-recalculation-in-excel).
  "4 billion formulas that can depend on a single cell" (specifications-and-limits).
  `ForceFullCalculation` turns smart recalculation off where maintaining the tree costs more than
  it saves (improving-calculation-performance).
- **[ExSheet]** The engine is single-threaded and "not thread-safe" (`Sheet.cs:13`). Recalculation
  is synchronous inside each mutating call.
- **[ExSheet] Cost to know.** Asking for a cell's dependents scans every Formula that holds a range
  (`Sheet.cs:356-365`), so its cost grows with the number of range-reading Formulas. Every
  structural edit and every rename rebuilds the whole graph (`Sheet.State.cs:79-90`).
- **[ExSheet]** Recalculation time at a large Sheet is OBSERVATIONAL and never gated (SH-19,
  `docs/definition-of-done.md:1070`; `spec.md:258-259`; ADR-0047 `:74-76`). No SH-19 measurement
  was found in `spikes/` or `verification/`. ADR-0050 `:270` leans on SH-19 for an uncapped paste
  over whole columns.

**Verdict: undecided.** It is to be measured, not asserted.

## Open questions: decisions ExSheet has not made

Each is phrased as a choice for the user. None is made here.

1. **Calculation mode.** Should ExSheet stay always-automatic, or offer a manual mode with
   F9-family keys and a "needs calculation" notice? Under principle 1, how would a stale Value be
   shown?
2. **Volatile functions.** If NOW, TODAY, RAND, OFFSET or INDIRECT are proposed, may a Value change
   without an edit? What triggers its recalculation: a timer, a Consumer push, or a key? And are
   OFFSET and INDIRECT, whose precedents are dynamic, admissible under a static graph at all?
3. **Iterative calculation.** Should `#CIRC!` stay final, or may a Sheet opt in to Excel's
   iteration (100 iterations, 0.001 change)? If it may, where is the setting recorded (Excel saves
   it in the workbook), and what is shown when iteration does not converge?
4. **A branch not taken.** Should Excel be observed on `=IF(TRUE,1,A1)` beside a cycle (warning or
   none) and the result recorded in `excel-behaviours.md`, even though ADR-0047 keeps its strict
   rule either way?
5. **Telling the user where a cycle is.** Should ExSheet have a counterpart to Excel's status bar
   ("Circular References: A1"), or to Formulas > Error Checking > Circular References, beyond
   `#CIRC!` in the cells?
6. **Cut and move.** Should ExSheet support cut and paste, or a drag-move of cells? If so, do the
   Formulas that read the moved cells follow them, and what becomes `#REF!`? (Microsoft's two pages
   disagree on copy; the move semantics need observing.)
7. **Pasting over a referenced cell.** Should a paste over a cell that Formulas read be compared
   against Excel? Microsoft names "pasted over" as a cause of `#REF!`.
8. **Defined names.** Are workbook names (`=Rate*B2`) in scope? If they are, they become nodes of
   the graph, and adding or deleting one must trigger a recalculation as in Excel.
9. **Several Sheets.** When a workbook arrives (ADR-0046), are cross-sheet dependencies one graph?
   Does a qualifier for a sheet that does not exist stay `#REF!` or become `#NAME?`, and does adding
   that sheet later recalculate it?
10. **Spill dependencies.** In the spilling ADR that ADR-0047 defers: how a reference to a spill
    (`A2#`) is tracked when the spill's extent is itself a computed Value.
11. **Performance target and scaling.** Which large-Sheet case fixes SH-19? Is the linear scan over
    range-reading Formulas, and the full graph rebuild on every insertion, acceptable until it is
    measured?
12. **Recalculation off the UI thread.** Should a long recalculation stay synchronous, or run in
    the background while the last completed Values stay on screen (ADR-0047 already forbids partial
    Values)? Can an edit made meanwhile cancel it?
13. **Auditing.** Should ExSheet offer Trace Precedents and Trace Dependents beyond ADR-0057's
    outlines while editing?
