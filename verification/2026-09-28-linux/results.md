# Verification — 2026-09-28, Linux container

**Scope:** the whole layer 3 suite on the merged tree carrying ADR-0052 (the Focus is Excel's
active cell) and ADR-0053 (the compressed scroll height), which meet in the reveal code. Run in
the development container, not on Windows: it does not discharge VZ-14 or the §22 Step 4 record.

**Verified commit:** the merge of `claude/adr-0053-compressed-scroll` into
`claude/exsheet-start-8cx3v1` (parent of `8b89d2b`, which only adds ADR text).

## Environment

- Linux, Google Chrome as installed (`/usr/bin/google-chrome`), headed under `xvfb-run` with a
  2560×1600 screen. Microsoft Edge is not installed, so the `msedge` project was not run
- Projects `chrome` and `chrome-150` (Chrome launched with `--force-device-scale-factor=1.5` and
  the viewport left to the window, ADR-0053)

## Results

| Host | Result |
|---|---|
| WebAssembly | 305 passed, 15 skipped, 0 failed (22.9 min) |
| Server (`EXGRID_HOSTING=server`) | 307 passed, 13 skipped, 0 failed (9.9 min) |

The skips are by design: MEM-5/6's soak, VZ-14 off Windows, and the tests that skip themselves by
host. `console.json` holds no console errors or warnings. `metrics.json` holds the observational
numbers, recorded and never gated.

DC-19/DC-34 on the Server host passed in this run. It failed 2 of 30 times on the ADR-0053 branch
before the merge, so one pass does not settle it; the Server Formula Bar work is looking at it.

## After the third Windows run's fixes *(2026-09-28, at `41a11bf`)*

Layer 3 in the container, headed under xvfb, Chromium (`chromium-local`) plus the `chrome-150`
selection run with `--force-device-scale-factor=1.5` and the viewport left to the window:
**WebAssembly 356 passed, 15 skipped (29.3 min); Server 358 passed, 13 skipped (10.9 min); 0
failed.** Edge is not installed here. `metrics.json` holds this run's observational numbers.
