# 13: Measure after the changes

Status: done

**What to do:** repeat ticket 01's measurement once 04 to 12 are built, and record the before and after
beside [ticket 01's record](../../../../verification/2026-10-06-macos-live-update-costs-cc/README.md). Nothing in `src/` changes. Every number is observational and never
gates.

**Blocked by:** 04 to 12

## What to measure

- **PV-48:** ExPivot's redraw step by step on CoreCLR, and Apply to the painted frame in the browser, at
  about 10⁴, 10⁵ and 4×10⁵ report rows, with the collector's pauses. PV-21 beside it.
- **LV-23:** the out-of-memory loop at 101,001 rows, 20 redraws, the managed heap after a full collection.
- **LV-15's parts again**, and the pushed 10⁶-row Window's update after ticket 07.
- **Ticket 01's traps hold:** Release, `DOTNET_TieredCompilation=0`, the least of 7 to 15 runs, steps
  isolated, fixed result paths, idle machine checks.

## Done when

- [x] A `verification/<date>-<platform>-live-update-costs-after/README.md` in ticket 01's shape, with the
  before and after side by side
- [x] `metrics.json` carries PV-48 and LV-23

## Comments

2026-10-07: Done on `claude/live-data-next-cc-measure`:
[`2026-10-07-macos-live-update-costs-after`](../../../../verification/2026-10-07-macos-live-update-costs-after/README.md),
with its [`metrics.json`](../../../../verification/2026-10-07-macos-live-update-costs-after/metrics.json). Before is
`41c8d8c8` as the records of 2026-10-06 measured it, and the same code run again the same day where a figure
was missing.
- **PV-48.** A live ExPivot redraw, CoreCLR least of 15: 3.53 → 0.481 ms at 10,001 report rows, 60.2 → 4.81 at
  101,001, 268.5 → 19.0 at 401,001 (one change); in the browser, Apply to the painted report, 57 → 28, 529 →
  143 and 2,338 (one redraw) → 532 ms. With a full collection before each redraw it no longer collects; in
  steady state the collector's pause per redraw falls from 28.6 to 0.57 ms at 101,001 rows. PV-21's own
  1,000-change figure on `/pivot-live` is unchanged (27 → 28 ms median).
- **LV-23.** The out-of-memory is gone: 101,001 rows stay at 89.5 MB over 20 redraws (before +41 MB a redraw),
  401,001 at 329.7 MB over 15 and 80 (before out of memory on the 7th); one report alive, the one on screen.
- **LV-15's parts** are as they were except the Selection Summary's walk, which is gone: 1,000 changes to 10⁶
  rows 22.4 / 28.5 ms; one change to 10⁶ rows 91.4 → 6.8 ms at the median; the bytes per update the same.
- **The pushed 10⁶-row Window (ticket 07's open half)**: 590 / 632 ms unvouched, 23.7 / 29.7 vouched, in the
  browser; its check alone 267 / 280 ms (before 269 / 278).
- **Slower**: a data redraw laid out afresh, one in 64 here (after a compaction of the source), now costs
  more than every redraw did (CoreCLR 394.5 against 268.5 ms at 401,001 rows; browser 3,329 against 2,338),
  because it compares every row for the Change Highlight; an unvouched pushed Window's key check +8–21% on
  CoreCLR. Not separated: where the browser's redraw spends the time beyond `NextCube` and `NextReport`
  (111 of 532 ms at 401,001 rows).
