# 16: Linked Tables

Status: ready-for-agent

**What to build:** The Consumer declares a Linked Table by name and columns, and pushes whole snapshots. Formulas
read it with structured references and `XLOOKUP`. Until the first snapshot arrives, readers show
`#GETTING_DATA`. That propagates, `IFERROR` and `ISERROR` do not catch it, and a copy reaching it is
refused. A new snapshot recalculates only its readers. Table names join completion.

**Blocked by:** 04, 14

- [ ] `=SUM(Positions[PV])` and `XLOOKUP` by key read the snapshot (ADR-0049)
- [ ] Before the first push: `#GETTING_DATA`, and `=IFERROR(…, 0)` also shows `#GETTING_DATA`
- [ ] A snapshot replaces the previous one in one step; no recalculation sees a mix
- [ ] A copy reaching a waiting cell is refused
- [ ] An undeclared table name is `#NAME?`
- [ ] The DemoHost shows a Sheet reading the data an ExGrid on the same page shows

## Comments
