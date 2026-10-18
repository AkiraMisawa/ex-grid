# Run 18, Part B on macOS: KB-3 and FN-12e

**Verified commit: `f6b5e8fe6c4b87d077200616d305bdd87a723aba`** (head of `release/0.1` when the run started, "Release
notes for 0.1.0-rc.1 (#160)"), tagged `verified/2026-10-18-macos-18`.

`release/0.1` moved to `3241b05a` during the run (#161, the Windows run 18 and WR-10 records). That commit changes
`verification/` and `tests/ExGrid.Release.Tests/RepositoryTests.cs` only, none of the paths either row covers, so the
tag stays on `f6b5e8fe` and these records are committed on top of `3241b05a`.

Run by Claude on the user's Mac, unattended: Playwright drove both browsers; nobody pressed a key.

## The machine

| | |
|---|---|
| OS | macOS 26.6.2, Apple silicon |
| Region and language | en-GB (`AppleLocale` `en_GB`, `AppleLanguages` `en-GB`), left as found |
| Chrome | 155.0.8059.80 |
| Edge | 155.0.4283.62, installed for this run (Homebrew cask `microsoft-edge`, at the user's request) |
| Build | `nix develop -c dotnet build ExGrid.slnx`: 0 warnings, 0 errors |
| Mode | headless (`EXGRID_HEADLESS=1`), as the row asks; one rerun headed (below) |

## What ran

1. **The full layer 3**, `npx playwright test --project=chrome --project=msedge`, on WebAssembly and then on the Server
   host (`EXGRID_HOSTING=server`). Logs: `layer3-wasm.log`, `layer3-server.log`; `exit-codes.txt`; the fixtures'
   `console.json` and `metrics.json`.

   | Host | Passed | Failed | Skipped | Time |
   |---|---|---|---|---|
   | WebAssembly | 2422 | 34 | 154 | 1.3 h |
   | Server | 2546 | 32 | 32 | 1.3 h |

2. **The failures rerun headed** on WebAssembly, both browsers (`rerun-headed-wasm.log`): 30 passed, 20 failed. The
   Edge half of this rerun overlapped step 3's first attempt, which shared its host and clipboard; its Edge clipboard
   results are not used below. Its Chrome results were complete before the overlap.

3. **A reading, not a test**: `sheet-vs-excel.spec.mjs` copied to a scratch file with `Control+Enter`, `Control+C` and
   `Control+V` replaced by `Meta+…`, its Ctrl+Enter and clipboard tests run on both hosts and both browsers, headless,
   then the copy deleted (`reading-meta-wasm.log`, `reading-meta-server.log`): 18 passed on each host. No test in the
   repository was changed.

## FN-12e (macOS): pass

The two paintings, by name, passed in Chrome and Edge on both hosts:

- `sizing.spec.mjs`'s "a bold total the estimate fits paints whole, and a #### run fits its cell (FN-12e, ADR-0016)";
- `part-c.spec.mjs`'s "FN-12e (Part C, case 3c): #### fills its cell with whole # in the face it is painted in, and no
  more fit (ADR-0016)".

The macOS widths were not measured again; layer 1 holds them.

## KB-3 (macOS): pass

The Definition of Done's check, "Cmd+Enter fills where Meta is Command", by name. Each test presses
`ControlOrMeta+Enter`, which is Command here:

- `features.spec.mjs`'s "Ctrl+Enter fill across a non-editable column is refused, and says so (CP-16, ADR-0035,
  ADR-0052)": the editor's key path (the edit is open when Command+Enter is pressed). Passed, Chrome and Edge, both
  hosts.
- `write-lands.spec.mjs`'s "LV-13: a Ctrl+Enter fill whose target changed since the key was pressed lands as entered
  (ADR-0142, ADR-0035)": the editor's path behind a 150 ms round trip. Passed, Chrome and Edge, on the Server host; it
  skips itself on WebAssembly by design.
- The Sheet's editor path is not pressed with Command by any test in the suite (difference 1). Step 3 read it: with
  `Meta+Enter`, "active cell, cases 12 and 13" and "third run, main keys case 5" passed in Chrome and Edge, on both
  hosts.

*From `f4-enter-mode.md`, item 9's macOS half*, `features.spec.mjs`'s two ED-2 tests, which press Option for a word on
an Apple platform (`wordKeys`):

- "in Caret Ctrl+← and Ctrl+→ move the caret by a word, with Shift extend the selection, and Ctrl+Backspace and
  Ctrl+Delete delete a word, in the field itself (ED-2, ADR-0010)": not held. Passed, Chrome and Edge, both hosts.
- "with a 150 ms round trip, End, Ctrl+←, Shift+End typed straight after F2 move and extend as the field would; so do
  Ctrl+→, Ctrl+Shift+→, Ctrl+Backspace and Ctrl+Delete (ED-2, ADR-0010)": held behind F2. Passed, Chrome and Edge, on
  the Server host; it skips itself on WebAssembly by design.
- Neither test presses ⌘+←/→ (difference 2).

## Every other failure

Listed, not judged. The same tests failed in both browsers unless the line says otherwise.

| Test | Hosts | Headed rerun (Chrome) | What it reported |
|---|---|---|---|
| `scrollbar.spec.mjs:240` and `:266`, "the Focus is never behind a scrollbar" | both | not rerun | "the scrollbars are not occupying layout, so this test proves nothing (measured 0x0)" |
| `gestures.spec.mjs:225`, UX-10 | both | not rerun | the gutter measured 0 where 8 was expected |
| `stripes.spec.mjs:275`, "the vertical scrollbar paints a thumb in its gutter" (UX-10) | both | not rerun | "the bar occupies layout": measured 0 |
| `mud-app.spec.mjs:137` and `:191`, WR-7 | both | failed | "the scrollbars are not occupying layout, so this test proves nothing (measured 0x0)" |
| `declarations.spec.mjs:1904`, DC-13 edge auto-scroll | both | **passed** | headless: a reading expected above 840 was 0 |
| `declarations.spec.mjs:2114`, DC-12/DC-13 with row 1 scrolled away | none | **failed** | passed headless on both hosts; headed, `aria-rowindex` expected "3", received "5" |
| `pointing-scope.spec.mjs:731`, ADR-0058/SH-35/DC-55 | both, Chrome only | **passed** | headless: 492.875 against a bound of 492.375 |
| `features.spec.mjs:230`, KB-8/KB-44, Escape held then Tab | WebAssembly | **passed** | headless: the root "inactive", not focused |
| `features.spec.mjs:101`, KB-8, Escape then Tab | WebAssembly, Edge only | **passed** | as above |
| `freshness.spec.mjs:176`, ERR-9 / ADR-0123 | both | not rerun | the test's own `freshness-app` failed to build in its temporary copy under `/tmp` (CS0400/CS0246 in the Razor generator's output); nothing of the page was read |
| `sheet-vs-excel.spec.mjs` 541, 554, 583, 588, 593, 599, 621 (items 16, 17, 19, the fourth run's active cell item 3, one cell copied inside the Sheet) | both | failed | the clipboard still held the test's `SENTINEL`. Step 3 with `Meta+C` / `Meta+V`: all seven passed, both browsers, both hosts (difference 3) |

## Differences

1. **KB-3, the Sheet's editor.** `sheet-vs-excel.spec.mjs` presses `Control+Enter` literally, so on a Mac it asks
   Control+Enter, not Command+Enter; no test in layer 3 presses Command+Enter on the Sheet. Read by step 3 only: it
   fills, on both hosts and both browsers. (KB-3's row; DoD KB-3, "on every key path, the editor's included".)
2. **ED-2's macOS half, ⌘+←/→.** `f4-enter-mode.md` item 9 names "⌘+←/→ go to the text's ends"; the two ED-2 tests
   press Option for a word and never press ⌘+←/→. Not read in this run. (`f4-enter-mode.md`, item 9; ED-2.)
3. **The Sheet's clipboard tests on macOS.** Seven tests in `sheet-vs-excel.spec.mjs` press `Control+C` / `Control+V`,
   which a Mac browser does not turn into copy or paste; they fail on macOS on both hosts, headed and headless, and
   pass with Command (step 3). (ADR-0048, ADR-0050, ADR-0014; KB-3's `tests/ExGrid.Browser/` path.)
4. **The scrollbar tests.** Headless on macOS the scrollbars are overlays; the tests that need them in layout refuse,
   as ADR-0026 says they must. Headed, WR-7's two still measured 0x0 on this Mac. CI's Linux run is where they count.
5. **DC-12/DC-13 at `declarations.spec.mjs:2114`** passed headless on both hosts and failed headed on WebAssembly in
   both browsers (`aria-rowindex` "5" for "3").
6. **ERR-9's fixture app** does not build in its temporary copy on this Mac (`freshness.spec.mjs:176`), so ERR-9 was
   not read here.
7. **Headless-only failures**: DC-13 at 1904, DC-55 at `pointing-scope.spec.mjs:731` (half a pixel), and KB-8 at
   `features.spec.mjs:101` and `:230` on WebAssembly passed when rerun headed.

## Filed

`verification/by-hand.json`: KB-3 (macOS) and FN-12e (macOS), each with this commit, this tag, this report and the
verdict above.
