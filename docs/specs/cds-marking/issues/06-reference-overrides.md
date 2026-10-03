# 06: Overrides of Reference Curves

Status: ready-for-agent

**What to build:** the Override dialog and its actions on the Reference tabs (spec, "Reference
Curves").

- **The dialog,** from the Context Menu: a level or an offset, and a reason. It sends the value it
  replaces, and the server refuses it when that value has changed, saying who changed it and when.
- **Recovery rates** are overridden the same way.
- **The flag:** a level whose underlying quote has moved by more than the page's tolerance is
  flagged, and the readiness panel warns.
- **The actions:**
  - T-1, for a Point or a curve: the previous business day's last successful Marking, read from the
    stored Markings. Until ticket 11 has written one there is none, and the action is disabled.
  - Interpolate from neighbours.
  - Carry the on-the-run on a roll day.
- **Painting:** an overridden Point is Cell State Modified.
- **Lifetime:** every Override ends with the day, and is recorded.

**Blocked by:** 04

- [ ] Layer 1: an Override against a changed value is refused; an offset follows the quote; the flag
  appears past the tolerance; Overrides end with the day
- [ ] Layer 3: an Override from the dialog paints Modified on both hosts
