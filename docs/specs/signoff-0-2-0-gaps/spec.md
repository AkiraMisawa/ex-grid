# The gaps the 0.2.0 sign-off left

Status: ready-for-agent
Roadmap: internal

**Why:** the 0.2.0 sign-off ([`verification/2026-11-09-linux-signoff/results.md`](../../../verification/2026-11-09-linux-signoff/results.md),
at 0326ce27, v0.2.0-rc.4's commit) found no MUST failing, and left what the user decided on 2026-11-07 to release
with: clauses and rows no test asserts, accepted as they stand; DA-18, ED-34, PV-50 and eight LV rows as Known
limitations of 0.2.0; and the findings its "For after 0.2.0" lists. Each is a ticket below. SM-5's restatement and
ADR-0130's note, the one decision the sign-off left for `main` after the tag, landed with these tickets. A ticket
that turns out to need a decision stops and records it first (`docs/agents/issue-tracker.md`).

- [01](issues/01-tests-for-the-unasserted-clauses.md): a test for each of the 110 clauses no test asserts.
- [02](issues/02-tests-for-the-rows-with-no-test.md): a test for each of the 24 rows with no test of their own.
- [03](issues/03-one-definition-of-the-aggregations.md): one definition of the Aggregations in `@ex-grid/data` (DA-18).
- [04](issues/04-a-cancel-after-a-synchronous-write-names-its-outcome-on-blazor.md): on Blazor, a cancel after a
  synchronous write is said as what happened (ED-34).
- [05](issues/05-the-lv-rows-and-pv-50-for-the-leaf-aggregate-boundary.md): the LV rows and PV-50's keys rewritten
  for the Script Core's ExPivot, with LV-15's grid frame.
- [06](issues/06-a-held-press-retargeted-at-its-point.md): whether ADR-0021 reaches a held press retargeted at its
  point.
- [07](issues/07-pst-6-at-the-sizes-its-row-names.md): PST-6 at 10⁵ and 10⁶ cells.
- [08](issues/08-mem-7-over-the-script-core.md): MEM-7 over the Script Core.
- [09](issues/09-pv-21s-longest-task-near-the-cap.md): what holds the page near the combination cap (PV-21).
- [10](issues/10-a-deferred-update-refused-names-the-moves-together.md): a refused deferred Update names the moves
  together.
