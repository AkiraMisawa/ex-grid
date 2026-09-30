# 15: Live data in the component

Status: ready-for-agent

**What to build:** the component's side of ADR-0066.

- **Redrawing:** changes are gathered and redrawn every 250 ms by default. The Consumer may set the
  interval, and 0 redraws on every change.
- **Keeping state:** a change of values alone keeps the Row Sequence Version, the Selection and an
  open menu or panel.
- **The Change Highlight:** `CellChangedAt` is answered by comparing painted text with the previous
  reports that are still within the highlight's duration. Only data marks a cell, and every cell of
  a new row is marked.
- **The Stale Report:** its notice, with Retry.
- **A server's source:** its "changed" notice makes ExPivot ask again.

**Blocked by:** 11, 12, change-highlight 01

- [ ] PV-35, PV-36, PV-37, PV-38, with a fake `TimeProvider`

## Comments
