# 02: Tracer: a Sheet typed into and drawn through ExGrid

Status: in-progress

**What to build:** The thinnest complete path. The `ExSheet.Engine` and `ExSheet` packages exist, and a DemoHost
page shows a Sheet. A user clicks a cell, types `2` into A1, `3` into B1 and `=A1+B1` into C1, and
sees 5. ExSheet renders one ExGrid, with Columns `A`…`XFD` that ExSheet builds, `TotalCount`
1,048,576, rows as positions, and a new row instance for each row whose Values changed. The
Consumer receives a Sheet Document when the Sheet changes, and can hand one in. The arithmetic is
only `+ - * /` over single-cell References. The rest of the grammar is ticket 03.

**Blocked by:** None (can start immediately)

- [x] `ExSheet.Engine` references nothing of ours; `ExSheet` references it and `ExGrid`; nothing references `ExSheet` (ADR-0046/0047)
- [x] Typing a constant and a Formula shows the Value; editing A1 updates C1 and repaints only the rows whose Values changed (layer 2, render counts)
- [x] Scrolling reaches row 1,048,576 and column XFD, and the DOM does not grow with the extent
- [x] A row height at which 1,048,576 rows exceed the scroll ceiling is refused by name (ADR-0046, VZ-8)
- [x] Pinned Columns work on a Sheet
- [x] The Sheet Document round-trips: out, in, the same Values (ADR-0048)
- [x] ExGrid's sort and filter are not wired on a Sheet
- [ ] A DemoHost page shows it on both hosts, with a clean console

## Comments

2026-09-27, engine half: `src/ExSheet.Engine` exists with no package or project reference (a
layer 1 test pins that its assembly references only the base class library, SH-1's engine half).
It holds a `Sheet` of Entries by `CellAddress` (`A1`…`XFD1048576`), takes typed text through
`Sheet.Enter`, evaluates `+ - * /` over numbers and single-cell References, and returns a
`SheetChange` naming exactly the cells whose Values changed and their rows, published only after
the recalculation completes. `Sheet.ToDocument` / `Sheet.Open` round-trip the Sheet Document
(layer 1, `tests/ExSheet.Engine.Tests`). **What remains is the component's:** the `ExSheet`
package and its reference direction, the ExGrid rendering (Columns `A`…`XFD`, `TotalCount`,
new row instances for `SheetChange.Rows`, render counts), scrolling to the extent, the row-height
refusal, Pinned Columns, sort and filter left unwired, and the DemoHost page.

2026-09-27, component half: `src/ExSheet` (Razor class library, `net10.0`, packable, XML docs,
README) references `ExSheet.Engine` and `ExGrid` and nothing else of ours; nothing references it
but the demo pages and `tests/ExSheet.Components` (`ReferenceDirectionTests`). The component,
`ExSheet.Components.ExSheet`, renders one `ExGrid<SheetRow>`: Columns `A`…`XFD` built once per
process (`SheetColumns`; Fixed at Excel's 8.43 digits in the grid's Cell Metrics), `TotalCount`
1,048,576, rows as positions (Row Sequence Version always 0), a new `SheetRow` for each row a
`SheetChange` names and the same instance otherwise, Range Requests answered from the Sheet with
50 rows read ahead, Edit Intents turned into `SheetEdit.Enter` steps on the undo stack. A Formula
the engine cannot read is rejected by the column's verdict (ADR-0034): the editor holds with the
engine's reason. `OnSortChanged` and `OnFilterChanged` are left unwired, so no column menu is
painted and `HeaderClickSelects` gives the header click Excel's meaning. A `RowHeight` past
33,554,432 / 1,048,577 (header included) is refused by name before ExGrid is reached; 31 px fits.
`Document` / `DocumentChanged` carry the Sheet Document (bindable two ways: a document the
component raised is recognised and not reopened). Layer 2: `SheetRenderingTests` (tracer, SH-2,
SH-3, SH-4, pinned, sort unwired), `SheetDocumentWiringTests`.

What remains: the DemoHost page (ticket item 5 of this stream) and its browser run on both hosts.
Scrolling to the extent is layer 2 only (the DOM bound at A1 and at XFD1048576); a real scroll to
row 1,048,576 is layer 3's, under SH-18.
