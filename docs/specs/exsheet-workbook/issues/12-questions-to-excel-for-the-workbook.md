# 12: Questions to Excel for the Workbook

Status: ready-for-human
Roadmap: internal

**Why:** [ADR-0331](../../../adr/0331-exsheet-holds-a-workbook-of-sheets-behind-sheet-tabs.md) §5 and §7 build nothing from reasoning about Excel. Put to Excel in the next Windows run:

- [ ] Which functions take a 3D Reference, and what the others answer
- [ ] A 3D Reference's span when a Sheet is moved into or out of it, an end Sheet is moved or deleted, and a Sheet is inserted inside it
- [ ] What a tab press does while a constant (not a Formula) is being typed
- [ ] While a Formula is typed: whether a tab press writes `Sheet2!` at once, before a cell is pointed; what a point back on the Sheet typed on writes (`B2` or `Sheet1!B2`); and whether Escape shows the Sheet typed on again, as ExSheet does since 09
- [ ] Ctrl+Z after a Sheet operation: which Sheet is shown and what is selected
- [ ] Whether a Formula entered into a General cell takes the Number Format of a cell it reads on another Sheet (`=Sheet2!A1` over a percentage), as ExSheet now does since 01
- [ ] What a Formula reading a deleted Sheet shows: `#REF!`, as ExSheet writes it since 03, or `#REF!A1`
- [ ] Whether a copied Sheet's References qualified with its source's name name the copy, as ExSheet assumes since 03
- [ ] The name Excel gives a new Sheet when `SheetN` names have gaps, and a copy's name when it would pass 31 characters

**Blocked by:** None (can start immediately).
