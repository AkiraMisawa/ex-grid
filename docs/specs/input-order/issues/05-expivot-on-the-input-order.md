# 05: ExPivot's field list, popups and details take their place in the Input Order

Status: ready-for-agent
Roadmap: ExPivot — The field list, its menus and popups, a layout dropped and the details used at machine speed act in the order they were used.

**Why:** [ADR-0240](../../../adr/0240-input-to-one-product-instance-is-taken-in-one-order.md) §7, stage 4. The pivot's outermost element becomes the Input Root (§6). The popups' own hold (`_popupKeys`), the field list's menus and drops, the toggles and the details go through the order's seam; settings dialogs are drafts (§1). Focus given from a render goes through the guarded seam, never a plain `FocusAsync`.

- [ ] The Input Root is the pivot's; the report grid takes its place in it
- [ ] `_popupKeys` replaced by the seam; drops and toggles in the order
- [ ] Every focus from a render through the guard

**Blocked by:** 02.
