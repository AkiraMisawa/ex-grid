# The eighteenth Windows run: ARIA's row indices

Gathered from `claude/run17cf-aria-rows` (2026-10-14) for the next Windows run, to be folded into its spec when it
is written. No screen reader is asked: since 2026-10-13 the accessibility surface is verified against the
accessibility tree, and what a screen reader says is learned from users' reports (ADR-0033, "Verified against the
accessibility tree, not a screen reader").

1. **The ExPivot probe's row numbers** (ExPivot; ADR-0033, A11Y-2, A11Y-3). The seventeenth run's
   `verification/2026-10-11-windows-17/part-e/pivot-probe.mjs` records each painted row's `aria-rowindex` and the
   report's `aria-rowcount`. Since 2026-10-12 both count the header's rows first: a row per tier of the column
   Items' Header Groups, then the header row. `compare.py` matches the body by row label, so a comparison is
   unchanged. A run that reuses the probe, and reads its `row` or `rowCount` as the report's own numbers,
   subtracts the header's rows, counted as `headerRowsOf` in `tests/browser/fixtures.mjs` counts them.
