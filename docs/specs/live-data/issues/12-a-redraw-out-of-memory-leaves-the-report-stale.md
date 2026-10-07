# 12: A redraw that runs out of memory leaves the report stale

Status: ready-for-agent

**What to do:** build ADR-0161's last decision (ADR-0067's note of 2026-10-07). Catch an
`OutOfMemoryException` raised while the cube or the report is made, drop what was being built, keep the
report on screen as a Stale Report with the reason, and ask again on the next change.

**Blocked by:** None

## Where it stands

The exception reaches the renderer from `ExPivot.AskAsync` and the page stops
([the out-of-memory record](../../../../verification/2026-10-06-macos-pivot-oom/README.md)).

## Done when

- [ ] PV-47 passes (§29), with an engine step that throws
- [ ] The Stale reason has words in ExPivot's word table and in ExPivot.MudBlazor
- [ ] Layer 1 and 2 green

## Comments
