# 12: A redraw that runs out of memory leaves the report stale

Status: done

**What to do:** build ADR-0161's last decision (ADR-0067's note of 2026-10-07). Catch an
`OutOfMemoryException` raised while the cube or the report is made, drop what was being built, keep the
report on screen as a Stale Report with the reason, and ask again on the next change.

**Blocked by:** None

## Where it stands

The exception reaches the renderer from `ExPivot.AskAsync` and the page stops
([the out-of-memory record](../../../../verification/2026-10-06-macos-pivot-oom/README.md)).

## Done when

- [x] PV-47 passes (§29), with an engine step that throws
- [x] The Stale reason has words in ExPivot's word table and in ExPivot.MudBlazor
- [x] Layer 1 and 2 green

## Comments

2026-10-07: Built. `ExPivot.Asking.cs` catches an `OutOfMemoryException` raised while a redraw for newer data
makes its cube (`AskAsync`) or lays out its report (`LayOutAsync`), and only there: a layout the user asked
for is not a redraw, and still fails as before. *(2026-10-08: no longer — a layout change that runs out of
memory is refused, the layout goes back and the Pivot Toolbar says so, PV-47; the user confirmed it on
2026-10-09. The catch now sits in the Codex track's computation, which replaced this one.)* What was being built is dropped, the report on screen
stays as a Stale Report whose reason is `stale-out-of-memory` ("memory ran out while the newest data was
laid out."; Japanese in `PivotWords`), and the next change asks again. `LastError` is left alone: memory
running out is not the source's failure. The engine's Order Key no longer wraps an
`OutOfMemoryException` in its own `InvalidOperationException` (`ItemLabels.cs`), so the redraw sees it as
what it is. PV-47's tests: `StaleReportTests` (the report's step through an Order Key, the cube's
through a slice's yield, and the Japanese words) and `MudPivotStaleReportTests` (the MudAlert, in English
and Japanese); each checks that nothing reached the renderer.
