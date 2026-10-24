# 12: Questions to Excel for the Workbook

Status: done
Roadmap: internal

**Why:** [ADR-0331](../../../adr/0331-exsheet-holds-a-workbook-of-sheets-behind-sheet-tabs.md) §5 and §7 build nothing from reasoning about Excel. Put to Excel in the next Windows run:

- [x] Which functions take a 3D Reference, and what the others answer
- [x] A 3D Reference's span when a Sheet is moved into or out of it, an end Sheet is moved or deleted, and a Sheet is inserted inside it
- [x] A 3D Reference when a Sheet of its span is deleted (not an end), and when a Sheet is copied into it
- [x] A 3D Reference when rows or columns are inserted or deleted on one Sheet of its span, under what it reads
- [x] A copy of a Sheet whose own Formula holds a 3D Reference that begins or ends on it: which Sheet the copy's begins or ends on
- [x] How Excel writes a span typed last to first (`Sheet3:Sheet1!B2`) and one of a single Sheet (`Sheet2:Sheet2!B2`), and whether a Formula entered into a General cell takes a format from a 3D Reference
- [x] What a tab press does while a constant (not a Formula) is being typed
- [x] While a Formula is typed: whether a tab press writes `Sheet2!` at once, before a cell is pointed; what a point back on the Sheet typed on writes (`B2` or `Sheet1!B2`); and whether Escape shows the Sheet typed on again, as ExSheet does since 09
- [x] Ctrl+Z after a Sheet operation: which Sheet is shown and what is selected
- [x] Whether a Formula entered into a General cell takes the Number Format of a cell it reads on another Sheet (`=Sheet2!A1` over a percentage), as ExSheet now does since 01
- [x] What a Formula reading a deleted Sheet shows: `#REF!`, as ExSheet writes it since 03, or `#REF!A1`
- [x] Whether a copied Sheet's References qualified with its source's name name the copy, as ExSheet assumes since 03
- [x] The name Excel gives a new Sheet when `SheetN` names have gaps, and a copy's name when it would pass 31 characters

**Blocked by:** None (can start immediately).

**Asked on 2026-10-24** at `5799246a`, through COM and with real keys:
[`verification/2026-10-24-windows-workbook-12/report.md`](../../../../verification/2026-10-24-windows-workbook-12/report.md),
which answers each line above in its order, and lists where Excel differs from ExSheet. In short:

- A 3D Reference is read as one range, Sheet by Sheet in tab order, by `SUM`, `AVERAGE`, `COUNT`, `COUNTA`, `MAX`, `MIN`,
  `PRODUCT`, `STDEV.S`, `STDEV.P`, `VAR.S`, `VAR.P`, `MEDIAN`, `AND`, `OR`, `XOR`, `CONCAT`, `TEXTJOIN`, `LARGE`, `SMALL`
  and `RANK.EQ`; it is `#VALUE!` where a function asks for a range of one Sheet, `#REF!` where it is read as one value,
  `#NAME?` in parentheses, and `IF`, `CHOOSE` and `LET` hand it on.
- A span is the Sheets between its two ends in tab order. An end moved past the other, or deleted, hands its end to the
  Sheet next to it inward; a span of one Sheet is written as a Reference to it. Rows and columns inserted or deleted on
  a Sheet of a span never rewrite it. In a copy of a Sheet at an end of a span its own Formula holds, that span is
  `#REF!`.
- A span is written with its ends in tab order and their own case, a span of one Sheet as that Sheet, and gives no
  format. A tab press enters a constant; while a Formula is typed it writes `Sheet2!` at once, a point back writes
  `Sheet1!B2`, and Escape shows the Sheet typed on. A Reference to a deleted Sheet is `#REF!B2`. New Sheets are numbered
  from a count the workbook keeps.
