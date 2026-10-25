# Windows, twentieth run: the by-hand rows at `release/0.2` before `0.2.0-rc.1` (ADR-0208)

**Verified commit: `1e85052e3449dca2541b96a8a33712e7c8d98b8f`** (`release/0.2`, "Merge pull request #286"), tagged
`verified/2026-10-25-windows-20`. The procedure is `docs/specs/exsheet/verify-on-windows-18.md`, Part A, run exactly
as the nineteenth run ran it (`verification/2026-10-25-windows-19/`), with its tools and scripts copied here unchanged
but for paths.

Run on 2026-10-25, from 17:20 to 18:25 UTC, by the same Claude Code session on the same Windows desktop, driving
WSL2 for the build. The tag was pushed before the first row ran. **Nothing was decided.** No ADR, `CONTEXT.md` or
`docs/definition-of-done.md` was changed, and no code or test.

**Why again.** `release/0.2` was cut at run 19's commit, `5e3eecb4`. The Input Order fixes taken onto it before
`0.2.0-rc.1` (#271, #284, #282 and #295, merged with #286) change `src/ExGrid/Assets/ex-grid.js`,
`src/ExGrid/Components/ExGrid.razor` and `src/ExPivot.MudBlazor/MudPivotPress.cs`. The release check,
`gate --tag v0.2.0-rc.1 --commit 1e85052e --fetch-missing`, run at the start, refused every Windows row and WR-10
because their paths changed since run 19's records.

**The user's authorisation** is run 19's: the Windows by-hand rows are run on this machine, driven automatically,
and the IME's Predictive input is turned off for k8 and k8x and put back without asking each time.

## Files

As run 19's (`verification/2026-10-25-windows-19/report.md`, "Files"), but `part-g/ed30/against-19.txt` in place
of `against-18b.txt`, and without run 19's own checks of k13x's paste. WR-10's record is
`verification/2026-10-25-linux-mudblazor-floor-20/`.

## Environment

Run 19's, unchanged: Windows 11 Pro 25H2 build 26200.9711, one display at 150% (DPI 144), display language English
(United Kingdom), regional format en-GB, the Microsoft IME `imjptip.dll` 10.0.26100.9711 at Predictive input "1
character", **Chrome 156.0.8078.47** and **Edge 156.0.4314.42**. The DemoHosts were built at the verified commit in
a worktree of their own in WSL, with 0 warnings and 0 errors, on run 19's ports, and `tests/ExGrid.Browser` was
copied again at the verified commit for Windows' Node, with `npm ci`.

## Verdicts

| Row | Commit | Browser | Host | Scale | Verdict |
|---|---|---|---|---|---|
| VZ-14 | `1e85052e` | Chrome 156, Edge 156 | WebAssembly, Server | 150% (DPR 1.5) | **pass** |
| VZ-16, a real browser zoom told as it changes | `1e85052e` | Chrome 156, Edge 156 | WebAssembly | display 150%, zoom 67% to 200% | **pass** |
| VZ-16, a thin line and a gridline at a real 2.25 | `1e85052e` | Chrome 156, Edge 156 | WebAssembly | display 150%, zoom 150% (DPR 2.25) | **pass** |
| FN-12e (Windows), the two paintings | `1e85052e` | Chrome 156, Edge 156 | WebAssembly, Server | 150% | **pass** |
| ED-30 | `1e85052e` | Chrome 156, Edge 156 | WebAssembly, Server, Server behind 150 ms | 150% | **pass** |
| SH-42 | `1e85052e` | Chrome 156, Edge 156 | WebAssembly, Server | 150% | **pass** |
| SH-51 | `1e85052e` | Chrome 156, Edge 156 | WebAssembly, Server | 150% | **pass** |
| SH-52 | `1e85052e` | Chrome 156, Edge 156 | WebAssembly, Server, Server behind 80 ms | 150% | **pass** |

## What was read

Every reading is run 19's, but ED-30's k15 (below).

- **VZ-14** (`part-a/vz14/`): 4 of 4 passed.
- **FN-12e** (`part-a/fn12e/`): 8 of 8 passed, the two paintings under both Chromes on both hosts.
- **SH-42** (`part-a/sh42/`): Ctrl+1 opened Format Cells, Ctrl+2 to Ctrl+5 made D10 bold, italic, underlined and
  struck through, the keys reaching the Keyboard Field with no tab switched, in all four configurations. Every line
  is run 19's.
- **SH-51 and SH-52** (`part-d-summary.md`; `records/sh52-speed-*.jsonl`): `part-d-summary.md` is run 19's, line for
  line, but for the generated ids of Format Cells' tab list. At machine speed, every state read after each sequence
  is run 19's in all four configurations; the console records differ only in their times and circuit ids.
- **VZ-16** (`records/part-g-vz16-*.jsonl`): every reading is run 19's: `--ex-dp` was 1 / `devicePixelRatio` after
  every zoom step, and at the real 2.25 B2's thin Border was one black Device Pixel and E8's gridline two Device
  Pixels of `#E0E0E0`, under both Chromes.
- **ED-30** (`part-g/ed30/`): the main pass ran 36 records, 17:35 to 18:16, with no error and exit 0 throughout.
  - **k8 and k8x with Predictive input Off**: 12 records, 18:16 to 18:21. Off at 18:16:23, back to "1 character" at
    18:21:19. The IME's key after is identical to the export before (`logs/`).
  - **The pass column**, clause by clause, every configuration, as run 19 read it: D10 shows the composition while
    no edit is open (k2); after its end the Cell Editor holds `かな` with DOM focus (k2); Enter commits it and moves to
    D11 (k2); after a press on D12 mid-composition, D10 holds `かな` and D12 is selected, with no edit open (k8).
  - `against-19.txt` compares every state with run 19's records: 8 of 1992 differ, every one in k15's two Alt+Tab
    states. The composition went on in the Keyboard Field beside an open Cell Editor in 6 of 12 configurations, as
    in run 19, but four configurations swapped; which ones changes from run to run, as before (ADR-0080).
  - k13x reads as in run 19: behind 150 ms, D12 is still empty when the probe reads it 900 ms after Ctrl+V, the
    paste still in flight (run 19's report, "Differences").
  - **t3a and t3b** (`records/ed30-t3-*.jsonl`): every state is run 19's, in all four configurations.

## Differences

None from run 19 but ED-30's k15, a case to record whose configurations change from run to run.

## Filed

`verification/by-hand.json`: VZ-14, VZ-16, FN-12e (Windows), ED-30, SH-42, SH-51 and SH-52 name this record, the
verified commit `1e85052e3449dca2541b96a8a33712e7c8d98b8f` and its tag `verified/2026-10-25-windows-20`, with the
verdict pass. WR-10 is filed by its own record, `verification/2026-10-25-linux-mudblazor-floor-20/`. FN-12e and KB-3
on macOS and VZ-18 stay Known limitations of `0.2.0` (decided with the user, 2026-10-25).
