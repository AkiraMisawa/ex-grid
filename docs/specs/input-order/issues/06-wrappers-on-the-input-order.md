# 06: The Wrappers' dialogs and popovers join the Input Order, and their drafts are never written back

Status: done
Roadmap: ExGrid, ExSheet, ExPivot — The MudBlazor Wrappers behave as the Built-in Chrome does at machine speed: their menus, dialogs and popovers act in the order used.

**Why:** [ADR-0240](../../../adr/0240-input-to-one-product-instance-is-taken-in-one-order.md) §7, stage 5. A surface MudBlazor renders outside the Input Root joins the instance's handle while open (§6); its opening, commit and closing are in the order; the Format Cells dialog's own `_keysReady` gate is replaced; every unguarded `FocusAsync` from a render (`MudToolbarItem`, `MudFormatCellsFrame`, `MudExGridFindPanel`'s fallback, `PivotChrome`'s default `TakeKeyboard`) goes through the seam; a draft is never re-read from a parameter on a later render (§1). The public seam is documented on the Chrome pages, with a Change Note for each delegate whose shape changed (§9).

- [x] Out-of-root surfaces join the handle while open
- [x] Wrapper focus through the seam (the pivot's Field Settings… with no report: ADR-0350, 2026-10-24)
- [x] Drafts follow §1's rule; ticket 260's pattern swept
- [x] Docs Site: the seam for a Consumer's Chrome, with an Example

ExPivot.MudBlazor's part landed with ticket 05: every Pivot Chrome context carries `InOrder` and
`TakeKeyboard`, the Wrapper's pivot buttons, boxes and drafts are on the order, its focus goes through the
seam, and the Docs Site's ExPivot Layout page shows the seam with an Example (ADR-0240's note on ticket 05).
What is left is ExGrid.MudBlazor's and ExSheet.MudBlazor's, and one gap of the pivot's: while the report is
empty there is no grid to hand the keyboard into a box, so the Wrapper's Field Settings… opened from a Report
Filter with nothing else placed leaves the keyboard where it was (ADR-0240's trade-offs on ticket 05).

The mechanism for a surface outside the Input Root landed on 2026-10-23 (`ExGrid.JoinSurfaceAsync`,
`HandKeyboardToFrameAsync(element)`, claims that name a key), with ExSheet.MudBlazor's Format Cells dialog and
its More Colours popup on it and the Grid Appearance page's prose (ADR-0240's note on ticket 06). Still to
come: the Wrapper's toolbar lists, ExGrid.MudBlazor's Find panel fallback and toolbar items' focus through the
seam, the drafts' sweep, the pivot's gap above, and an Example of a Consumer's own Chrome on the seam.

The second part landed on 2026-10-23 (ADR-0240's note on it): the toolbar's lists under any Chrome are on the
seam (`ToolbarItemContext.InOrder`, `TakeKeyboard`, `JoinSurface`; `ChoicesClosing` and `ChoicesOpened` gone),
the MudBlazor items' buttons and lists claim their inputs, the Find panels' fallback goes through the core's
focus function, the drafts' sweep found nothing more, and the Sheet Formatting page has an Example of a
Chrome of the Consumer's own whose lists stand beside the Sheet. The pivot's Field Settings… gap with an empty
report needs a decision (ADR-0240's note) and is left open.

The pivot's gap closed on 2026-10-24 ([ADR-0350](../../../adr/0350-a-boxs-first-control-takes-the-keyboard-from-the-grids-script-where-no-grid-stands.md),
decided with the user): with no report grid, ExPivot hands the keyboard on through the grid's script, which
focuses the box's first control as the grid's hand-on does.

**Blocked by:** 02, 04, 05.
