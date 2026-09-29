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
