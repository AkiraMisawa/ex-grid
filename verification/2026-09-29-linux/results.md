# Layer 3 in the container, after merging main's ADR-0056 *(2026-09-29, at `34d369c`)*

Headed under xvfb, Chromium (`chromium-local`) plus the `chrome-150` selection run with
`--force-device-scale-factor=1.5` and the viewport left to the window. Edge is not installed here.

| Host | Result |
|---|---|
| WebAssembly | 378 passed, 15 skipped, 0 failed (19.0 min) |
| Server (`EXGRID_HOSTING=server`) | 380 passed, 13 skipped, 0 failed (9.9 min) |

The same suite at `41a11bf`, before the merge, took 29.3 min and 10.9 min. The spec files now boot
the app once each (ADR-0056). The first run after the merge failed 9 and 8 tests: this branch's
specs left globals behind, which ADR-0056's harness fails by name, and `sheet-vs-excel.spec.mjs`
stacked its keyboard pacing once per test on the shared page. Both were fixed in `34d369c`.

`console.json` holds only the harness's own error on purpose. `metrics.json` holds the
observational numbers, recorded and never gated.

## At `00ba84b`, after the fourth Windows run's fixes and the equality run *(2026-09-29)*

The same setup. This run includes the grid's own scrollbar (ADR-0029), the accepted two device pixels
deep in a compressed spacer (ADR-0053), the plain-text paste into one cell (ADR-0014), comparison
at 15 significant digits (ADR-0047), and the fixes for DC-19/DC-34, WR-7 and item 23.

| Host | Result |
|---|---|
| WebAssembly | 386 passed, 15 skipped, 0 failed (16.4 min) |
| Server (`EXGRID_HOSTING=server`) | 388 passed, 13 skipped, 0 failed (9.9 min) |

## At `dd63bb6`, after the fifth Windows run's fixes *(2026-09-29)*

This run includes the reveal that paints its target slice in the same render (ADR-0012), with the
held scroll write (ADR-0021). It also includes the live region emptied on a collapse (ADR-0033), and
the test fixes for DC-13, UX-16/UX-13 and WR-6.

| Host | Result |
|---|---|
| WebAssembly | 391 passed, 15 skipped, 0 failed (16.8 min) |
| Server (`EXGRID_HOSTING=server`) | 393 passed, 13 skipped, 0 failed (10.2 min) |
