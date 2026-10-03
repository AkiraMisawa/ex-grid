# ExSheet, first version

Status: ready-for-agent

Decided by [ADR-0046](../../adr/0046-exsheet-is-a-general-purpose-sheet-drawn-by-exgrid-as-its-consumer.md)
to [ADR-0051](../../adr/0051-formula-entry-completion-point-mode-and-the-formula-bar.md), with
notes recorded in ADR-0007, 0008, 0012, 0014, 0016, 0019 and 0021. The vocabulary is `CONTEXT.md`'s
"Sheets" section, plus Point, Formula Bar, Name Box and Fill Intent. Exit criteria are not yet
written: the first ticket writes them into `docs/definition-of-done.md`. This spec synthesises
those decisions; where it and they disagree, they win.

## Problem Statement

A user of a line-of-business application needs to work something out: a what-if on a set of
positions, a budget, a pricing sheet. Today they export to Excel, work there, and paste the result
back, or they do not bother. The file that leaves the application has no owner, no access control,
and no link back to the data it came from. By the time anyone reads it, its numbers are stale,
and nothing in the file says so.

ExGrid cannot help. It shows data the application owns and reports edits; it has no cells of its
own, and no formulas, by design (ADR-0001, ADR-0007). A developer who wants a sheet inside the
application has to embed a third-party spreadsheet. Its keyboard, clipboard and look then differ
from the ExGrid next to it, and its formulas cannot see the application's data.

## Solution

**ExSheet**: a general-purpose sheet component, drawn by ExGrid, with a formula engine of its own.

- A user works in a **Sheet** as in Excel: cells addressed `A1`, Excel's extent, typing constants
  and **Formulas**, with the keyboard, selection and clipboard ExGrid already has.
- The Formula engine answers as Excel does, or gives `#NAME?`. It never approximates.
- Typing a Formula is aided as in Excel: completion of function names, an argument hint,
  **Point** mode, and a **Formula Bar** with a **Name Box**.
- Formulas can read the application's own data through **Linked Tables**: `SUM(Positions[PV])`,
  `XLOOKUP(key, Positions[Id], Positions[PV])`. A Linked Table is read by key, never by position,
  and shows `#GETTING_DATA` until its data has arrived.
- The application receives a **Sheet Document**, which holds Entries and never Values, and stores
  it where it likes. Opening it anywhere, a server included, computes the same Values with the
  same engine, `ExSheet.Engine`.
- Rows and columns can be inserted and deleted, a block can be pasted onto one cell, the fill
  handle continues a series, and one Ctrl+Z undoes each of those.

## User Stories

### Entering and reading

1. As a user, I want to click a cell and type a number, so that entering data works as in Excel.
2. As a user, I want to type `=A1*2` and see the result in the cell, so that I can compute from other cells.
3. As a user, I want a cell showing a Formula's result to show the Formula when I edit it, so that I can change the Formula rather than retype it.
4. As a user, I want to read a Formula without starting to edit it, so that I can check how a number was produced without risking a change.
5. As a user, I want the Name Box to say which cell I am on, so that I know where I am in a large Sheet.
6. As a user, I want to type `D200` into the Name Box and land there, so that I can jump across a large Sheet.
7. As a user, I want what I type in the Formula Bar to appear in the cell as I type, and the other way round, so that the two never disagree.
8. As a user, I want typed constants read in my Sheet's culture — `2026/9/26` as a date under `ja-JP`, `1,234.5` as a number under `en-US` — so that I type the way I write.
9. As a user, I want a Sheet saved under one culture and opened under another to show the same numbers, so that sharing a Sheet never changes it.
10. As a user, I want a number too wide for its column to show `####`, so that I never read a truncated number as a smaller one.
11. As a user, I want to format numbers with Excel's format codes (`#,##0.00`, `yyyy-mm-dd`), so that money and dates read correctly.
12. As a user, I want to set a cell's horizontal alignment, so that headings and figures line up as I need.

### Formulas

13. As a user, I want `SUM`, `AVERAGE`, `MIN`, `MAX`, `COUNT`, `COUNTA`, `IF`, `ROUND`, `IFERROR`, `ISERROR` and `XLOOKUP` to give exactly Excel's results, so that a Sheet I check against Excel agrees with it.
14. As a user, I want an unknown function to show `#NAME?`, so that a missing feature never looks like an answer.
15. As a user, I want `$A$1`, `A$1`, `A1:B2`, `A:A` and `1:1` to work as in Excel, so that my habits carry over.
16. As a user, I want a Formula that depends on itself to show `#CIRC!` in every cell of the cycle, so that I am told there is a cycle rather than shown a 0.
17. As a user, I want an Error Value to flow into every Formula that uses it, so that a broken input cannot produce a clean-looking total.
18. As a user, I want dates to be day numbers as in Excel, including Excel's 1900 date system, so that date arithmetic matches Excel's.
19. As a user, I want only the cells that depend on my change to recompute, so that a large Sheet stays responsive.
20. As a user, I want never to see a mix of old and new results, so that every number on screen comes from the same moment.

### Formula entry aids

21. As a user, I want function names offered as I type `=SU`, so that I do not have to remember exact spellings.
22. As a user, I want Tab to accept the offered function, so that completion works as in Excel.
23. As a user, I want the offered list to close on Escape before my edit is cancelled, so that closing the list does not throw away my typing.
24. As a user, I want to see a function's arguments as I type `SUM(`, so that I know what goes where.
25. As a user, I want to press ↓ after `=` and have `A2` written into my Formula, so that I can build a Formula by pointing, as in Excel.
26. As a user, I want Shift+arrow while pointing to write a range, so that I can point at `A1:C3`.
27. As a user, I want to click a cell while typing a Formula after an operator and have its Reference written in, so that the mouse points too.
28. As a user, I want typing an operator to end pointing, so that I can continue the Formula.
29. As a user, I want F2 to switch between moving the caret and pointing, so that I can fix a typo in the middle of a Formula.
30. As a user, I want the arrow keys to commit and move when I am typing a constant, so that continuous entry still works.
31. As a user, I want pointing to work from the Formula Bar as from the cell, so that long Formulas are as easy to build as short ones.

### Structure and navigation

32. As a user, I want to click a column letter to select the column, so that selecting works as in Excel.
33. As a user, I want to click a row number to select the row, so that I can act on whole rows.
34. As a user, I want Shift+click on a heading to extend the selection to that column or row, so that I can select several.
35. As a user, I want the corner of the Headings to select the whole Sheet, so that one click selects everything.
36. As a user, I want Ctrl+arrow to stop at the edge of the block of values I am in, so that I can move through data as in Excel.
37. As a user, I want Ctrl+Shift+arrow to extend to that edge, so that I can select a block of data in one key.
38. As a user, I want to insert rows and columns and have every Formula keep pointing at the same cells, so that inserting never breaks a calculation.
39. As a user, I want deleting cells a Formula reads to turn that Formula into `#REF!`, so that I can see what the deletion broke.
40. As a user, I want the selection to stay where it was after inserting a row, so that I can keep typing into the new row.
41. As a user, I want to freeze leading columns, so that labels stay in view as I scroll right.
42. As a user, I want to scroll to row 1,048,576 and column XFD, so that the Sheet is as large as Excel's.

### Clipboard and fill

43. As a user, I want copying Formulas within the Sheet to shift their relative References, so that copying works as in Excel.
44. As a user, I want copying from the Sheet into another program to give the values, so that what lands there is what I saw.
45. As a user, I want text pasted from elsewhere taken as if I had typed it, so that `=A1+1` becomes a Formula and `1,234` a number.
46. As a user, I want to paste a copied block onto a single cell, so that I do not have to select a same-sized target first.
47. As a user, I want the pasted block selected afterwards, so that I can see exactly what the paste changed.
48. As a user, I want a paste that would run past the Sheet's edge refused with a reason, so that nothing is silently cut off.
49. As a user, I want to drag the fill handle to copy a cell down, so that I can repeat a value or Formula.
50. As a user, I want two numbers dragged by the fill handle to continue as a linear series, so that 1, 2 becomes 1, 2, 3, 4.
51. As a user, I want a date dragged by the fill handle to continue day by day, so that dates fill as in Excel.
52. As a user, I want a pattern the Sheet cannot continue refused rather than filled with copies, so that I never get a column of repeated values where I expected a series.
53. As a user, I want one Ctrl+Z to undo a whole paste, fill, insertion or deletion, so that undo matches what I did.
54. As a user, I want Ctrl+Y to redo it, so that I can step back and forth.

### Linked Tables

55. As a user, I want to write `=SUM(Positions[PV])` over data the application holds, so that my Sheet uses live figures without an export.
56. As a user, I want to look up one row by its key with `XLOOKUP`, so that my Formula keeps reading the same row whatever order another screen shows it in.
57. As a user, I want a Formula reading data not yet arrived to show `#GETTING_DATA`, so that I never read 0 or an old number as the answer.
58. As a user, I want `IFERROR` not to hide `#GETTING_DATA`, so that a fallback value never stands in for data on its way.
59. As a user, I want copying a cell still waiting for data refused, so that I never paste a placeholder elsewhere.
60. As a user, I want Formulas to update when the application pushes new data, so that the Sheet stays current.
61. As a user, I want Linked Table names offered by completion, so that I can find the data I can use.

### The Consumer developer

62. As a Consumer developer, I want to hand ExSheet a Sheet Document and get one back when it changes, so that I decide where it is stored.
63. As a Consumer developer, I want the Sheet Document to hold Entries and not Values, so that no stale result is ever stored.
64. As a Consumer developer, I want to compute a saved Sheet's Values on the server with `ExSheet.Engine`, so that the server and the screen agree.
65. As a Consumer developer, I want a Sheet Document of a version the reader does not know refused, so that a newer file is never half-read.
66. As a Consumer developer, I want to declare a Linked Table by name and columns and push its rows later, so that the Sheet can open before my data has loaded.
67. As a Consumer developer, I want to replace a Linked Table's rows in one push, so that no Formula ever sees half an update.
68. As a Consumer developer, I want to hide the Column Headings, the Row Headings or the Formula Bar, so that a Sheet can look like a form.
69. As a Consumer developer, I want changes I make through ExSheet's commands to land on the user's undo stack, so that Ctrl+Z undoes them in order.
70. As a Consumer developer, I want replacing the whole Sheet Document to clear the undo stack, so that undo never reaches into a replaced document.
71. As a Consumer developer, I want a row height at which the full extent does not fit refused by name, so that the Sheet is never silently shortened.
72. As a Consumer developer, I want two ExSheets on one page to be independent, so that keys, popovers and undo never cross between them.
73. As a Consumer developer, I want ExSheet under `ExGrid.MudBlazor`'s Chrome to look like the rest of my MudBlazor application, so that nothing looks foreign.

### An ExGrid Consumer (the core declarations, usable without ExSheet)

74. As an ExGrid Consumer, I want none of the new declarations to change my grid unless I make them, so that upgrading changes nothing.
75. As an ExGrid Consumer, I want a Formula Bar showing the Focus cell's full value, so that a value shown as `####` can always be read (ADR-0016).
76. As an ExGrid Consumer, I want to take Fill Intents into my Overlay, so that my editable grid gets a fill handle too.
77. As an ExGrid Consumer, I want to hide the column header, so that a headerless list is possible.

### Cell Format (ADR-0071, added 2026-09-30)

78. As a user, I want to make a cell's text bold, italic, underlined or struck through, and to colour it, so that headings and totals stand out.
79. As a user, I want to fill a cell with a colour and draw borders around and between cells, so that a table reads as one.
80. As a user, I want a Number Format's `[Red]` to paint negative numbers red, so that `$#,##0_);[Red]($#,##0)` looks as it does in Excel.
81. As a user, I want the Sheet's cells to stay white with black text when my page is dark, so that the colours I chose read as I chose them.
82. As a user, I want Excel's formatting keys (Ctrl+B, Ctrl+Shift+$, Ctrl+Shift+&, …) to work, so that I format without reaching for the mouse.
83. As a user, I want Ctrl+1 to open Format Cells, so that I can set everything about a cell's look in one place.
84. As a user, I want a bold number too wide for its column to show `####`, so that a bold total is never cut into a different number.
85. As a Consumer developer, I want to set and read a Cell Format through commands, so that my own toolbar can format the Selection and show the Focus cell's state.
86. As a Consumer developer, I want Format Cells drawn by my design system's Chrome, or by my own, so that it looks like the rest of my application.

## Implementation Decisions

### Packages and reference direction

- **Two new packages.** `ExSheet.Engine` references nothing of ours and has no UI: the parser,
  References, Values, the function set, the dependency graph, recalculation, number formats, the
  culture rules, structural edits, and the Sheet Document. `ExSheet` is the component: it references
  `ExSheet.Engine` and `ExGrid`, and nothing references it (ADR-0019, ADR-0046, ADR-0047).
- **Everything under `src/` that ships** has XML documentation on every public member and a
  `0.0.0-dev` version, and packs through the existing release workflow (ADR-0042).
- **Namespaces avoid the traps `AGENTS.md` lists.** No Razor class named after its root namespace,
  and `global::` where a nested namespace would shadow.

### The engine (`ExSheet.Engine`)

- **A Sheet model**: sparse cells keyed by position, each holding an Entry. Values are computed.
- **The parser** reads Excel's grammar in its invariant form (ADR-0047): operators and precedence,
  References in every form above, the Sheet qualifier (recorded, one Sheet only), structured
  references. It is also the source of truth for completion candidates and for "can a Reference go
  at this caret", which the component asks (ADR-0051).
- **References are recorded in a form that survives structural edits.** Inserting or deleting rows
  and columns rewrites them; a deleted target becomes `#REF!`.
- **The function set** is exactly ADR-0047's list. Each function is admitted with tests written
  from Excel's observed behaviour, blanks, text and Error Values in ranges included.
- **The dependency graph** recalculates only dependents. A cycle marks every member and every
  dependent `#CIRC!`. A recalculation completes before any of its Values are published.
- **`#GETTING_DATA`** propagates through every dependent and is not an error to `IFERROR` or
  `ISERROR` (ADR-0049).
- **Number formats**: a parser and formatter for Excel's format codes covering numbers, percent,
  thousands separators, and date and time codes, under the Sheet's declared culture. Values show
  at most 15 significant digits.
- **Culture**: typed constants are parsed under the Sheet's culture and recorded parsed.
- **The Sheet Document**: a versioned, serialisable form holding the culture, the Entries (parsed
  constants and invariant Formulas), and each cell's, row's and column's Cell Format (ADR-0071). It
  is read with a refusal on an unknown version (ADR-0048).
- **Linked Tables**: declared by name and column names; rows replaced by a whole snapshot; resolved
  by structured references (ADR-0049).
- **Undo**: the engine exposes operations as reversible steps; the component keeps the stack.

### The core declarations (`ExGrid`, ADR-0050 and ADR-0051)

Each is off unless a Consumer declares it, and each needs criteria in ExGrid's sections of the
Definition of Done:

- **Header click selects**, with **Row Headings** as a band outside the column index space (its
  labels from the Consumer, pinned, selecting whole rows), the corner selecting all, and either
  heading hideable.
- **An edge answer for Ctrl+arrow**, asked synchronously.
- **A paste that may spill**, after which the pasted block is the Selection; a spill past the extent
  is refused by name; `Editable` is checked on the spilled block.
- **Placing the Selection and Focus** on the Consumer's request, dropped if made under an older Row
  Sequence Version.
- **The fill handle**: painted in the selection overlay, the drag owned by the core, one axis,
  a Fill Intent on release, `Editable` checked on the target, no handle on a disjoint Selection.
- **The editor's opening text** supplied per cell.
- **Editor text reporting and completion**: text and caret reported as the user types; candidates
  and a hint from the Consumer; the list painted by Chrome as the editor's Inner Popup.
- **Point**: a fourth editing state driven by the Consumer's synchronous predicate, an outline in
  the selection overlay, and the Reference text from the Consumer. The `keydown` message carries
  the editor's text and caret (ADR-0021's note).
- **The Formula Bar and Name Box**: a band inside the root above the header, height from the Grid
  Metrics, the Cell Editor's second surface.

### The component (`ExSheet`)

- **Renders one ExGrid** with Columns `A`…`XFD` built by ExSheet (their accessor reads the Value),
  `TotalCount` 1,048,576, rows as positions, and a new row instance for every row whose Values
  changed. ExGrid's sort and filter are not wired.
- **Refuses a row height** at which 1,048,576 rows exceed the scroll ceiling, by name.
- **Maps each Edit Intent, paste, Fill Intent and command** onto engine operations, pushes one
  undo step per user operation, and publishes Values only from completed recalculations.
- **Answers the core's questions**: the edge answer from the Sheet's blanks, the editor's opening
  text from the Entry, completion and Point from the parser, Name Box labels and resolution from
  addresses.
- **Clipboard**: inside ExSheet, Entries travel and relative References shift; outward, Values as
  unformatted text; inward, each field is parsed as typed. A copy reaching `#GETTING_DATA` is
  refused.
- **Fill semantics**: copy with References shifted, linear series from two or more numbers, dates
  by day; anything else refused.
- **Commands**: insert and delete rows and columns through the Context Menu (ADR-0036), undo and
  redo, and Cell Format: `SetCellFormatAsync`, `CellFormatAt`, `OpenFormatCellsAsync`, Excel's
  formatting keys, and Format Cells as a Chrome seam whose frame the Chrome chooses (ADR-0071).
- **Parameters** for the Sheet Document in and out, Linked Tables, the culture, and hiding each
  Heading and the Formula Bar.

## Testing Decisions

- **A good test observes behaviour through a public test seam and names the ADR it pins**
  (`// ADR-0047: …`). It asserts what a user or a Consumer can see, such as a Value, a painted cell,
  a notification, a refusal or the Sheet Document's contents, and never private state.
- **Layer 1, `ExSheet.Engine` (new seam, the deepest one):** parsing and References; each function
  against a table of Excel's observed results; Error Value propagation; `#CIRC!`; `#GETTING_DATA`
  and `IFERROR`; incremental recalculation (only dependents recomputed, counted); structural edits
  rewriting References and producing `#REF!`; the 1900 date system; number formats; culture
  parsing, and a document saved under one culture reading the same under another; Sheet Document
  round trip and unknown-version refusal; fill rules and refusals; Linked Table snapshots. Prior
  art: `tests/ExGrid.Tests`, the clipboard and filter-semantics suites, whose "pin every case" style
  (ADR-0023) is the model.
- **Layer 1, ExGrid's new pure rules (existing seam):** the spill paste shape and its refusal, the
  fill handle's one-axis extension, a stale placement request dropped.
- **Layer 2, ExGrid (existing seam):** each declaration off leaves the grid unchanged (the existing
  suites stay green); each on behaves as stated, with render counts showing rows still skip their
  render. Prior art: `tests/ExGrid.Components`.
- **Layer 2, ExSheet (new seam):** an edit repaints only the rows whose Values changed; inserting a
  row keeps the Selection; a spilled paste selects the block; one Ctrl+Z per operation; Headings and
  the Formula Bar hide. Prior art: the row-memoisation and edit tests.
- **Layer 3, the DemoHost (existing seam):** a Sheet page on both hosts, WebAssembly and Server,
  driven by real keys: typing, Point by keys and by mouse, completion accepted with Tab, the
  Formula Bar mirroring, the fill-handle drag, paste from the real clipboard, Ctrl+arrow, and two
  ExSheets on one page staying independent, with a clean console throughout. Prior art:
  `tests/ExGrid.Browser`.
- **The invariants still hold:** the DOM does not grow with the Sheet's extent, rows skip their
  render, and nothing per cell reaches JavaScript.
- **Performance is measured, not gated:** recalculation at a large Sheet, and per-keystroke
  completion on a Server circuit, recorded as `spikes/render-bench`-style results.

## Out of Scope

As ADR-0046's table: several Sheets, frozen rows, hiding rows and columns, sort and AutoFilter,
find and replace, protecting cells, and `.xlsx`. Also:

- ~~**Fonts, fills and borders**: they wait for a render-bench measurement and an ADR.~~ In
  since 2026-09-30 (ADR-0071); the measurement now decides only how they are painted. Still out:
  font size, typeface, wrapped text, vertical alignment, rotation, diagonal borders, rich text,
  pattern fills, theme colours, conditional formatting, Paste Special and the Format Painter.
- **Merged cells and per-row heights**: not supported (ADR-0046).
- **Partial, paged or server-aggregated Linked Tables**, another ExSheet as a source, and writing
  back to a Linked Table (ADR-0049).
- **Localised formula syntax** (ADR-0047).
- **Spilled arrays** (Excel 365's dynamic arrays): a Formula whose result is more than one cell
  is `#VALUE!`, never implicitly intersected, so that spilling can arrive later without changing
  any written sheet (ADR-0047, decided with the user 2026-09-27).
- **A Formula Bar placed outside the grid** (ADR-0051).
- **Functions beyond ADR-0047's list** (their queue is [`exsheet-functions`](../exsheet-functions/spec.md)), and fill patterns beyond copy, linear series and dates by
  day.

## Further Notes

- **The release gate is not decided here.** `docs/definition-of-done.md` says "finished" covers
  ADR-0001 to ADR-0030 and places ExSheet out of scope. Whether ADR-0050/0051's ExGrid declarations
  join ExGrid's release, and what ExSheet's own sign-off is, is the user's call, and the first
  ticket raises it.
- **A ticket that turns out to need a decision stops and records the ADR first** (`AGENTS.md`,
  "Working in parallel").
- **Only one agent at a time runs layer 3.** Several tickets below end in a browser run.

## Comments
