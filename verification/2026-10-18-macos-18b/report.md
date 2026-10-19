# Run 18b on macOS: KB-3 and FN-12e at 0.1.0-rc.3's commit

**Verified commit: `28ab5e1bf8c1e4c71dbcb19e9bd430cbd0612f48`** (head of `release/0.1`, "0.1.0-rc.3: the sign-off's
three fixes, and rc.3's notes are cut (#169)"), tagged `verified/2026-10-18-macos-18b`.

Run 18's records (`verification/2026-10-18-macos-18/`, at `f6b5e8fe`) no longer held: #168 changed `ex-grid.js`,
`ExGrid.razor` and the `Keys/` files, within both rows' paths. `release/0.1` has since moved to `83d2afac` (#171), which
changes `verification/` only, so these records are committed on top of it and the tag stays on `28ab5e1b`.

Run by Claude on the user's Mac, unattended: Playwright drove both browsers.

## The machine

macOS 26.6.2, Apple silicon, en-GB, left as found; Chrome 155.0.8059.80; Edge 155.0.4283.62; headless
(`EXGRID_HEADLESS=1`). The build at the verified commit had 0 warnings and 0 errors.

## What ran

Narrower than run 18, at the user's wish that this not take long (2026-10-18): the tests the two rows are judged by,
by name, which are the ones run 18's verdicts named.

1. **The named tests**, Chrome and Edge, on WebAssembly and then on the Server host (`named-wasm.log`,
   `named-server.log`):

   ```sh
   npx playwright test --project=chrome --project=msedge sizing.spec.mjs part-c.spec.mjs features.spec.mjs write-lands.spec.mjs \
     --grep "FN-12e|CP-16, ADR-0035, ADR-0052|LV-13: a Ctrl\+Enter fill|in Caret Ctrl\+← and Ctrl\+→ move the caret|with a 150 ms round trip, End, Ctrl\+←"
   ```

   WebAssembly: 8 passed, 4 skipped (the two tests that skip themselves there by design, in each browser). Server: 12
   passed.
2. **A reading, not a test**, as run 18's step 3: `sheet-vs-excel.spec.mjs` copied to a scratch file with
   `Control+Enter` replaced by `Meta+Enter`, its two Ctrl+Enter tests run in both browsers on both hosts, then the copy
   deleted (`reading-meta-wasm.log`, `reading-meta-server.log`): 4 passed on each host. No test in the repository was
   changed.
3. **The full layer 3 on WebAssembly** (`layer3-wasm.log`), Chrome and Edge, before the run was narrowed. It reached
   its last test, 2610 of 2610, and the session running it ended before Playwright wrote its summary: 2416 passed, 40
   failed and 154 skipped, counted from its lines. Its failures are run 18's, less the KB-8 Escape-then-Tab ones,
   plus one: Edge's "keys typed at machine speed under the mud Chrome (2026-10-12) › SH-52/ADR-0100/ADR-0050: Escape
   three times in a list a KeyTip opened, then Alt, H, H…" (`sheet-toolbar.spec.mjs:750`). It is listed, not judged.
   The full run on the Server host was not made.

## FN-12e (macOS): pass

In Chrome and Edge, on both hosts:

- `sizing.spec.mjs`'s "a bold total the estimate fits paints whole, and a #### run fits its cell (FN-12e, ADR-0016)";
- `part-c.spec.mjs`'s "FN-12e (Part C, case 3c): #### fills its cell with whole # in the face it is painted in, and no
  more fit (ADR-0016)".

## KB-3 (macOS): pass

Command+Enter (`ControlOrMeta+Enter`) fills on the editor's path:

- `features.spec.mjs`'s "Ctrl+Enter fill across a non-editable column is refused, and says so (CP-16, ADR-0035,
  ADR-0052)": Chrome and Edge, both hosts.
- `write-lands.spec.mjs`'s "LV-13: a Ctrl+Enter fill whose target changed since the key was pressed lands as entered
  (ADR-0142, ADR-0035)": Chrome and Edge, Server host (it skips itself on WebAssembly).
- The Sheet's editor, by step 2's reading only: Chrome and Edge, both hosts.

`features.spec.mjs`'s two ED-2 tests, Option for a word on an Apple platform:

- "in Caret Ctrl+← and Ctrl+→ move the caret by a word, …": Chrome and Edge, both hosts.
- "with a 150 ms round trip, End, Ctrl+←, Shift+End typed straight after F2 …": Chrome and Edge, Server host (it skips
  itself on WebAssembly).

## Differences

Run 18's differences 1 to 3 stand unchanged (`verification/2026-10-18-macos-18/report.md`): no layer-3 test presses
Command+Enter on the Sheet's editor; the ED-2 tests never press ⌘+←/→; the Sheet's clipboard tests press
`Control+C` / `Control+V`, which fail on a Mac. New in this run: the SH-52 failure in step 3, Edge on WebAssembly.
