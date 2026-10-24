# 11: 3D References

Status: done
Roadmap: ExSheet — `=SUM(Sheet1:Sheet3!B2)` adds the same cell across a span of Sheets, as Excel does.

**Why:** [ADR-0331](../../../adr/0331-exsheet-holds-a-workbook-of-sheets-behind-sheet-tabs.md) §7.

- [x] The parser and the engine read `First:Last!Range` over the Sheets in tab order
- [x] Only the functions Excel accepts one in take it, among those ExSheet supports
- [x] Any other answers as Excel does, as ticket 12 read it
- [x] Moving, inserting and deleting Sheets inside or at the ends of the span changes it as ticket 12 read from Excel,
      and so do rows and columns inserted or deleted under it on one of its Sheets
- [x] Layer 1 cases from Excel's readings

**Blocked by:** 03, 12.

**Settled while building (2026-10-22; its refusals superseded below):** what does not wait on Excel is in, and
what does is refused by name rather than built from reasoning (ADR-0331 §7, the spine's first rule).

- The lexer reads `Sheet1:Sheet3!B2` and `'Q1 Sales:Sheet3'!B2` (quoted whole, as Excel writes one); the
  Reference carries the span's last Sheet beside its first. It is written bare when both names are bare
  and quoted whole otherwise. A span named last to first reads the same Sheets. The readers the Formula
  Bar and `INDIRECT` use still read a Reference of one Sheet, so pointing, F4 and colouring pass a 3D
  Reference over, and `INDIRECT("Sheet1:Sheet3!B2")` stays `#REF!`.
- `SUM`, `AVERAGE`, `COUNT`, `COUNTA`, `MAX`, `MIN`, `PRODUCT`, `STDEV.S`, `STDEV.P`, `VAR.S` and `VAR.P`
  take one as a direct argument (Microsoft's documented list, among the declared functions;
  `FunctionDefinition.TakesThreeD`), reading it Sheet by Sheet in tab order as one range. Anywhere else —
  on its own, in an operator, in parentheses, in any other function — the Formula is refused
  (`Evaluator.Refuse`), a `#VALUE!` nothing catches. A span naming a Sheet the Workbook lacks is `#REF!`.
- The Formula depends on its rectangle on every Sheet of the span, its own included.
- A rename rewrites either end. A move that keeps the span's Sheets is done.
  `SheetRefusalReason.ChangesThreeDReference` refuses, naming the Reference: inserting, copying or moving a
  Sheet into a span or onto an end; moving a Sheet of a span out of it, or an end anywhere that changes its
  Sheets; deleting a Sheet of a span; copying a Sheet whose own Formulas hold a span it begins or ends; and
  inserting or deleting rows or columns that reach what a span reads, on a Sheet it spans.
- A copy or a fill shifts its relative parts (ADR-0048). A Formula entered into a General cell takes no
  format from one. An `.xlsx` stores it as written, and the reader reads it back.
- Ticket 12 gained the questions these refusals wait on.

**Built from Excel's answers (2026-10-24):** ticket 12's run answered every refusal above, and each gave way to
Excel's behaviour (ADR-0331 §7's note of that date). `ThreeDExcelReadingsTests` asks the engine every case the run
asked Excel, from `tests/ExSheet.Engine.Tests/ThreeDReadings/excel-readings.json`, which the run's `to-fixture.py`
writes from its records: 336 Formulas, 34 Sheet operations, 15 row and column changes and 10 copies.
`SheetRefusalReason.ChangesThreeDReference` is gone with the refusals.
