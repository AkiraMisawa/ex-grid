# 08: The Selection Summary walks only what it needs

Status: ready-for-agent

**What to do:** build [ADR-0130](../../../adr/0130-the-selection-summary-is-asked-of-the-consumer-like-find.md)'s note of 2026-10-07. The grid walks a new Window to learn whether
the rows moved only while figures stand or are being asked for, and compares only the Selection's
positions.

**Blocked by:** None

## Where it stands

`NoteRowsForSummary` (`src/ExGrid/Components/ExGrid.Summary.cs`) runs on every `ApplyState`. It compares
every position both Windows hold until the first difference, by the row type's equality. Under live data
at 10⁶ rows that is up to 11 ms on CoreCLR and about 130 ms in the browser ([ticket 01](../../../../verification/2026-10-06-macos-live-update-costs-cc/README.md)).

## Done when

- [ ] SM-14 passes (§31), counting the row comparisons
- [ ] Layer 1 and 2 green; the Selection Summary's layer-3 spec passes locally

## Comments
