# 06: The Wrappers' dialogs and popovers join the Input Order, and their drafts are never written back

Status: ready-for-agent
Roadmap: ExGrid, ExSheet, ExPivot — The MudBlazor Wrappers behave as the Built-in Chrome does at machine speed: their menus, dialogs and popovers act in the order used.

**Why:** [ADR-0240](../../../adr/0240-input-to-one-product-instance-is-taken-in-one-order.md) §7, stage 5. A surface MudBlazor renders outside the Input Root joins the instance's handle while open (§6); its opening, commit and closing are in the order; the Format Cells dialog's own `_keysReady` gate is replaced; every unguarded `FocusAsync` from a render (`MudToolbarItem`, `MudFormatCellsFrame`, `MudExGridFindPanel`'s fallback, `PivotChrome`'s default `TakeKeyboard`) goes through the seam; a draft is never re-read from a parameter on a later render (§1). The public seam is documented on the Chrome pages, with a Change Note for each delegate whose shape changed (§9).

- [ ] Out-of-root surfaces join the handle while open
- [ ] Wrapper focus through the seam
- [ ] Drafts follow §1's rule; ticket 260's pattern swept
- [ ] Docs Site: the seam for a Consumer's Chrome, with an Example

ExPivot.MudBlazor's part landed with ticket 05: every Pivot Chrome context carries `InOrder` and
`TakeKeyboard`, the Wrapper's pivot buttons, boxes and drafts are on the order, its focus goes through the
seam, and the Docs Site's ExPivot Layout page shows the seam with an Example (ADR-0240's note on ticket 05).
What is left is ExGrid.MudBlazor's and ExSheet.MudBlazor's, and one gap of the pivot's: while the report is
empty there is no grid to hand the keyboard into a box, so the Wrapper's Field Settings… opened from a Report
Filter with nothing else placed leaves the keyboard where it was (ADR-0240's trade-offs on ticket 05).

**Blocked by:** 02, 04, 05.
