# 08: Two instances on one page render the same root id

Status: needs-triage
Roadmap: ExGrid, ExSheet — Every grid on a page has its own id.

**Seen:** in a trace of the Hunt on the Input Order branch (2026-10-19), the Sheet's root and the
positions grid's root (`#sheet-positions`, `ex-pointed-at`) both render `id="ex14-root"`. The positions
grid carries `data-ex-pointed-from="ex14-root"`. `getElementById` resolves that to the Sheet because it
comes first in the DOM, so nothing went wrong in that trace.

**Today:** an instance's id prefix comes from a static counter (`_idPrefix` in `ExGrid.razor`).

**Why it is a defect:** anything that looks up a root by id is one DOM order away from reaching the wrong
grid (ADR-0018).

- [ ] Find how two instances get the same number.
- [ ] Fix it so each instance on a page has its own id.
- [ ] Layer 2 or 3: two instances on one page never share a root id.
