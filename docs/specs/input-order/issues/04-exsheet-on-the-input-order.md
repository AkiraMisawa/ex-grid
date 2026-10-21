# 04: ExSheet's toolbar, Headings, Name Box and Format Cells take their place in the Input Order

Status: done

**Why:** [ADR-0240](../../../adr/0240-input-to-one-product-instance-is-taken-in-one-order.md) §7, stage 3. The Sheet's outermost element becomes the Input Root (§6). The toolbar band's own hold (`_typedOnTheWay`) and its commands, Format Cells' opening and OK, and the keyboard moving into and out of the Pointing Scope go through the order's seam; a press handed on to another instance's grid takes a number in the order of the instance holding the keyboard.

- [x] The Input Root is the Sheet's; its grids take their place in the Sheet's order
- [x] The toolbar's own hold replaced by the seam (the hand-back's; the band's hold for a list on its way stays, marked `data-ex-holds`, and the MudBlazor lists' holds wait for ticket 06)
- [x] Format Cells' opening and commit in the order; its draft follows §1's rule
- [x] A press on a Sheet Tab (ADR-0331, exsheet-workbook ticket 05) numbered in the Sheet's order: the tabs stand outside the grid's root today

**Blocked by:** 02, 03.
