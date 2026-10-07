# 08: The Selection Summary walks only what it needs

Status: done

**What to do:** build [ADR-0130](../../../adr/0130-the-selection-summary-is-asked-of-the-consumer-like-find.md)'s note of 2026-10-07. The grid walks a new Window to learn whether
the rows moved only while figures stand or are being asked for, and compares only the Selection's
positions.

**Blocked by:** None

## Where it stands

`NoteRowsForSummary` (`src/ExGrid/Components/ExGrid.Summary.cs`) runs on every `ApplyState`. It compares
every position both Windows hold until the first difference, by the row type's equality. Under live data
at 10⁶ rows that is up to 11 ms on CoreCLR and about 130 ms in the browser ([ticket 01](../../../../verification/2026-10-06-macos-live-update-costs-cc/README.md)).

## Done when

- [x] SM-14 passes (§31), counting the row comparisons
- [x] Layer 1 and 2 green; the Selection Summary's layer-3 spec passes locally

## Comments

2026-10-07: Built. `NoteRowsForSummary` walks only while a question stands, and only the positions of
its ranges; with no question, or a question asked under another order, the stamp moves and nothing is
walked. The row count moves the figures only where it cuts through the Selection: a row added after
it is a change outside it (SM-14). Layer 2: `SelectionSummaryTests`, counting the row type's equality
calls (0 with no figure, 2 for a two-row Selection over a 500-row Window). Full suite green;
`selection-summary.spec.mjs` passes locally on WebAssembly under Chrome, headless (6 of 6).

2026-10-07, after the code review: the row-count rule above left the figures standing when a row was
added before a Selection the Window had moved away from, since neither Window could show the shift. A
changed count now moves the figures unless every selected row is one both Windows hold, and those are
compared in place. A row added after the Selection still moves nothing. This reads ADR-0130's earlier
"and the row count" through SM-14's "a change outside the Selection moves no figure". The reading is
put to the lead to confirm.

2026-10-07, after the second review: the line above is withdrawn. Under the same Row Sequence Version a
changed count moved no row: rows added or removed bump the version (ADR-0011), and under live data a
batch that moves any row does (LV-7). So a row added before the Selection always drops the Selection,
and its figures with it. The withdrawn rule guarded only a broken contract, and it asked again on every
append while the Selection was out of sight, against SM-14. A changed count moves the figures where it
cuts through the Selection, as first built. The shortcut that skips the walk for the same list now also
needs the same start. For the lead to confirm: ADR-0130's earlier "and the row count" reads, under SM-14,
as "a row count that cuts through the Selection".

2026-10-07, layer 3 on the final code (2fcec664 and dc2cd665): `selection-summary`, `grid-live-local`,
`grid-live` and `pivot-live` pass locally on WebAssembly under Chrome, headless, 20 of 20, with a clean
console.
