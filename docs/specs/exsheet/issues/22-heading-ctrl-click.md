# 22: Ctrl+click and Ctrl+drag on Headings

Status: ready-for-agent

**What to build:** ADR-0050, item 1 (added 2026-09-29) and ADR-0012 (2026-09-29). Ctrl+click on a
Heading, or on a plain ExGrid's column header, adds the whole column (row) as a new range, or takes
a wholly selected one out. Ctrl+drag adds the columns (rows) crossed as one range. Neither sorts.
The user observed both in Excel (Microsoft 365, Windows).

**Blocked by:** 21 (the Heading drag mode Ctrl+drag runs on)

- [ ] Ctrl+click on a column not wholly selected adds it as a new range, with the Focus on its
      first visible row (SR-2e)
- [ ] Ctrl+click on a wholly selected column takes it out, and the Focus follows ADR-0052's
      take-out rule (the first remaining cell, by rows, of the range made last) (SR-2e)
- [ ] Ctrl+drag adds whole columns from the pressed one to the pointer's as one range (SR-2e)
- [ ] The same on Row Headings, by rows (DC-42)
- [ ] Meta counts as Ctrl only on an Apple platform, as it does on cells (ADR-0012) (SR-2e)
- [ ] Neither sorts on a plain ExGrid (SR-2e)

## Comments
