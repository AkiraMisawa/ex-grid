# 13: `Sheet` gives up what moved to `Workbook`

Status: ready-for-agent
Roadmap: ExSheet — The engine's API is rebuilt around Workbook; code that opened a Sheet directly opens a Workbook instead.

**Why:** [ADR-0331](../../../adr/0331-exsheet-holds-a-workbook-of-sheets-behind-sheet-tabs.md) §9 (contract): no forwarders are kept.

- [ ] `Sheet.Open`, `Sheet.ToDocument`, `Do` and the other members that moved are removed from `Sheet`, with `PublicAPI` updated
- [ ] Docs Site Examples that used them move to `Workbook`
- [ ] A breaking Change Note naming each member and what replaces it
- [ ] The Sheet Seed, the count of changes, `NowSource` and the Sheet Day move to the Workbook, so a change on any Sheet draws `RAND` again on every Sheet, as Excel does (01)

**Blocked by:** 05, 07.
