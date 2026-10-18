# 04: ExSheet's toolbar, Headings, Name Box and Format Cells take their place in the Input Order

Status: ready-for-agent
Roadmap: ExSheet — The Sheet Toolbar, its KeyTips and Format Cells used at machine speed act in the order they were used, together with the Sheet's keys and presses.

**Why:** [ADR-0240](../../../adr/0240-input-to-one-product-instance-is-taken-in-one-order.md) §7, stage 3. The Sheet's outermost element becomes the Input Root (§6). The toolbar band's own hold (`_typedOnTheWay`) and its commands, Format Cells' opening and OK, and the keyboard moving into and out of the Pointing Scope go through the order's seam; a press handed on to another instance's grid takes a number in the order of the instance holding the keyboard.

- [ ] The Input Root is the Sheet's; its grids take their place in the Sheet's order
- [ ] The toolbar's own hold replaced by the seam
- [ ] Format Cells' opening and commit in the order; its draft follows §1's rule

**Blocked by:** 02, 03.
