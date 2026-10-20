# 13: `Sheet` gives up what moved to `Workbook`

Status: ready-for-agent
Roadmap: ExSheet — The engine's API is rebuilt around Workbook; code that opened a Sheet directly opens a Workbook instead.

**Why:** [ADR-0331](../../../adr/0331-exsheet-holds-a-workbook-of-sheets-behind-sheet-tabs.md) §9 (contract): no forwarders are kept.

- [x] `Sheet.Open`, `Sheet.ToDocument`, `Do` and the other members that moved are removed from `Sheet`, with `PublicAPI` updated
- [x] Docs Site Examples that used them move to `Workbook`
- [x] A breaking Change Note naming each member and what replaces it
- [ ] The Sheet Seed, the count of changes, `NowSource` and the Sheet Day move to the Workbook, so a change on any Sheet draws `RAND` again on every Sheet, as Excel does (01)

**Blocked by:** 05, 07.

**Progress (2026-10-20):** the members that moved are internal on `Sheet`, kept for the engine's own
layer 1 and 2 through `InternalsVisibleTo`, and public on `Workbook`; the Sheet Day, `NowSource` and the
volatile recalculation are the Workbook's. The Sheet Seed and the count of changes stay on `Sheet` until
the draw names its Sheet: with one seed for the Workbook, `=RAND()` at A1 of two Sheets would draw the
same number, and ADR-0180 says a draw is a function of the seed, the cell, the call and the count alone.
That is a decision to make first.
