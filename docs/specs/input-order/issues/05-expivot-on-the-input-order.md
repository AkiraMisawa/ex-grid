# 05: ExPivot's field list, popups and details take their place in the Input Order

Status: done

**Why:** [ADR-0240](../../../adr/0240-input-to-one-product-instance-is-taken-in-one-order.md) §7, stage 4. The pivot's outermost element becomes the Input Root (§6). The popups' own hold (`_popupKeys`), the field list's menus and drops, the toggles and the details go through the order's seam; settings dialogs are drafts (§1). Focus given from a render goes through the guarded seam, never a plain `FocusAsync`.

- [x] The Input Root is the pivot's; the report grid takes its place in it (each details grid keeps an order of its own, decided with the user 2026-10-22)
- [x] `_popupKeys` replaced by the seam; drops and toggles in the order
- [x] Every focus from a render through the guard
- [x] The MudBlazor Wrapper's pivot parts on the same seam (ticket 06's part for ExPivot), so the Wrapper loses nothing the local queues gave it

**Blocked by:** 02.
