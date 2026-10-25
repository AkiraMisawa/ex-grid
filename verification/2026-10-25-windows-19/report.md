# Windows, nineteenth run: the by-hand rows at `main` before `0.2.0-rc.1` (ADR-0208)

**Verified commit: `5e3eecb44fb9dcfec074cfa8eefe03a91809fbc3`** (head of `main`, "Merge pull request #258"), tagged
`verified/2026-10-25-windows-19`. The procedure is `docs/specs/exsheet/verify-on-windows-18.md`, Part A, as
`verification/2026-10-18-windows-18/` and `-18b/` ran it, with their tools and scripts copied here unchanged but for
paths.

Run on 2026-10-25, from 01:55 to 03:15 UTC, by a Claude Code session on the Windows desktop of the earlier runs,
driving WSL2 for the build. The tag was pushed before the first row ran. **Nothing was decided.** No ADR,
`CONTEXT.md` or `docs/definition-of-done.md` was changed, and no code or test.

**Why at `main`.** `0.2.0` has no release branch yet. The release check, `gate --tag v0.2.0-rc.1 --commit origin/main
--fetch-missing`, run at the start, refused every by-hand row: VZ-14, VZ-16, FN-12e (Windows and macOS), ED-30, SH-42,
SH-51, SH-52, KB-3 and WR-10 because their paths changed since their records, and VZ-18 because it was never recorded
(its Known limitation reaches `0.1.0` only). These records hold at a later commit only while the rows' paths are
unchanged up to it.

**The user's authorisation.** The user asked, through the project, for the Windows by-hand rows to be run on this
machine, driven automatically ("Windowsで手動確認のやつやっていいよ。手動といっても自動で動かして欲しい"). The IME's
Predictive input need not be asked about each time (the user, 2026-10-18). It was turned off for k8 and k8x only, and
put back (ED-30, below).

## Files

| | |
|---|---|
| `report.md` | this report |
| `hosts.sh` | the DemoHosts in WSL at the verified commit |
| `pg.py`, `pg-server.mjs`, `input-server.ps1`, `browser-input.ps1` | run 17's tools, as run 18 used them: a headed Windows Chrome or Edge, real keys and presses through `SendInput` |
| `part-a/pw.cmd`, `part-a/vz14/`, `part-a/fn12e/` | VZ-14 and FN-12e; Playwright's list and JSON output |
| `sh42-probe.mjs`, `sh42.cmd`, `part-a/sh42/` | SH-42 |
| `part_d_sheet.py`, `part_d_extra.py`, `sh52_speed.py`, `part-d-summary.py`, `part-d-summary.md`, `run-sh.sh` | SH-51 and SH-52, then VZ-16, one after another |
| `part_g_vz16.py` | VZ-16 |
| `part-g/ed30/` | ED-30: the probe and scripts, the IME setting's scripts, its registry exports and the times it was changed (`logs/`), `summary.txt`, `against-18b.txt`, the records. `ime19-probe.mjs` and `run-ime19.cmd` add this run's own checks of k13x's paste (k13y and k13w, below) |
| `t3_ime.py`, `run-t3.sh` | ED-30: the IME and the Alt key, t3a and t3b |
| `records/` | the `pg.py` probes' records |
| `shots/` | the page's own pictures: ED-30's for WebAssembly and Chrome, SH-52's, t3's, VZ-16's device-pixel clips |
| `wr10.sh` | WR-10's run, recorded in `verification/2026-10-25-linux-mudblazor-floor/` |

## Environment

- **Windows 11 Pro 25H2**, build 26200.9711. One display, 3840 × 2160 device px at **150%** (DPI 144), not changed.
  Display language **English (United Kingdom)**; regional format **en-GB**, not changed. The language list: en-GB,
  en-US, and ja with the Microsoft IME.
- **The Microsoft IME** (Japanese), `imjptip.dll` 10.0.26100.9711. Its Predictive input: **1 character** (the user's
  setting), Off for k8 and k8x only.
- **Chrome 156.0.8078.47** and **Edge 156.0.4314.42** throughout (run 18b ran 155).
- **How it ran.** As run 18: the DemoHosts built (0 warnings, 0 errors) and run in WSL2 (Ubuntu 24.04, .NET SDK
  10.0.203 through nix) from a worktree at the verified commit: WebAssembly on `localhost:5318`, the Server host on
  `localhost:6317`, the demo API on 8318 and 8317 (20,000 trades). Windows' own browsers, launched by Playwright under
  Windows' portable Node v24.14.1 from a copy of `tests/ExGrid.Browser` at the verified commit (`npm ci` on Windows).
  `latency-proxy.mjs` on Windows' Node: 5317 → 6317, control on 7317.

## Which rows ran

Every Windows row's paths changed between its record (`28ab5e1b`, `verification/2026-10-18-windows-18b/`) and the
verified commit (`git diff --name-only 28ab5e1b 5e3eecb4 -- <the row's paths>`): VZ-14 46 files, VZ-16 59, FN-12e 61,
ED-30 94, SH-42 60, SH-51 65, SH-52 90. Every one ran.

## Verdicts

| Row | Commit | Browser | Host | Scale | Verdict |
|---|---|---|---|---|---|
| VZ-14 | `5e3eecb4` | Chrome 156, Edge 156 | WebAssembly, Server | 150% (DPR 1.5) | **pass** |
| VZ-16, a real browser zoom told as it changes | `5e3eecb4` | Chrome 156, Edge 156 | WebAssembly | display 150%, zoom 67% to 200% | **pass** |
| VZ-16, a thin line and a gridline at a real 2.25 | `5e3eecb4` | Chrome 156, Edge 156 | WebAssembly | display 150%, zoom 150% (DPR 2.25) | **pass** |
| FN-12e (Windows), the two paintings | `5e3eecb4` | Chrome 156, Edge 156 | WebAssembly, Server | 150% | **pass** |
| ED-30 | `5e3eecb4` | Chrome 156, Edge 156 | WebAssembly, Server, Server behind 150 ms | 150% | **pass** |
| SH-42 | `5e3eecb4` | Chrome 156, Edge 156 | WebAssembly, Server | 150% | **pass** |
| SH-51 | `5e3eecb4` | Chrome 156, Edge 156 | WebAssembly, Server | 150% | **pass** |
| SH-52 | `5e3eecb4` | Chrome 156, Edge 156 | WebAssembly, Server, Server behind 80 ms | 150% | **pass** |

## What was read

- **VZ-14** (`part-a/vz14/`): 4 of 4 passed.
- **FN-12e** (`part-a/fn12e/`): 8 of 8 passed, the two paintings under both Chromes on both hosts.
- **SH-42** (`part-a/sh42/`): Ctrl+1 opened Format Cells, Ctrl+2 to Ctrl+5 made D10 bold, italic, underlined and
  struck through. The keys reached the Keyboard Field, and no tab switched, in all four configurations. Every line
  is run 18b's.
- **SH-51** (`part-d-summary.md`, d5): Ctrl+F1 hid the toolbar and Ctrl+F1 showed it, in all four.
- **SH-52** (`part-d-summary.md`, d3, d4, d6; `records/sh52-speed-*.jsonl`):
  - `part-d-summary.md` is run 18b's, line for line, but for the generated ids of Format Cells' tab list.
  - At machine speed, every state read after each sequence is run 18b's, in all four configurations. The events the
    probe heard differ in one sequence, Alt, Alt, H, 1 typed at once, on the Server host behind 80 ms (both Chromes)
    and on WebAssembly in Edge: here the keys the gate held were not heard again on the toolbar after it took the
    keyboard. The outcome is the same.
- **VZ-16** (`records/part-g-vz16-*.jsonl`): every reading is run 18b's: `--ex-dp` was 1 / `devicePixelRatio` after
  every zoom step, and at the real 2.25 B2's thin Border was one black Device Pixel and E8's gridline two Device
  Pixels of `#E0E0E0`, under both Chromes.
- **ED-30** (`part-g/ed30/`): the main pass ran 36 records, 02:15 to 02:55, with no error and exit 0 throughout.
  - **k8 and k8x with Predictive input Off**: 12 records, 02:56 to 03:01. Off at 02:56:34, back to "1 character" at
    03:01:25. Off wrote `PredictionUseInputHistory` 0; putting it back wrote 1, and the key after is identical to the
    export before (`logs/`). No value was left behind this time.
  - **The pass column**, clause by clause, every configuration, as run 18b read it:
    - D10 shows the composition while no edit is open (k2);
    - after its end, the Cell Editor holds `かな` with DOM focus (k2);
    - Enter commits it and moves to D11 (k2);
    - after a press on D12 mid-composition, D10 holds `かな` and D12 is selected, with no edit open (k8).
  - `against-18b.txt` compares every state with run 18b's records: 14 of 1992 differ. Ten are in k15 and four in
    k13x.
  - **k15** (Alt+Tab mid-composition, a case to record): the composition went on in the Keyboard Field beside an
    open Cell Editor in 6 of 12 configurations (run 18b: 5), and ended in the other 6. Five configurations read the
    other way from run 18b, in the two Alt+Tab states only; every later state of the case is run 18b's. Which
    configurations go on changes from run to run, as before (ADR-0080).
  - **k13x**, see "Differences".
  - **t3a and t3b** (`records/ed30-t3-*.jsonl`): every state is run 18b's, in all four configurations.

## Differences

**A paste lands later on the Server host behind 150 ms.** It is not a clause of ED-30's pass column. The paste is not
lost.

- k13x types `150` in D10, copies it with Ctrl+C, presses D12, then types Ctrl+V. On the Server host behind 150 ms,
  in all four configurations (`/sheet` and `/sheet?chrome=mud`, Chrome and Edge), D12 was still empty when the probe
  read it, 900 ms after the key. Run 18b read `D12 '150'` there; WebAssembly and the Server host with no added
  latency read `D12 '150'` here too.
- The probe reads at 900 ms behind latency, and its own settled check does not wait for the Settled Mark, so the
  paste was still in flight (the root held `ex-pasting`, as the project's Input Order thread read on Linux). The
  Delete that follows cleared D12 after the paste landed.
- Read again 2 s after Ctrl+V (`ime19-probe.mjs`, k13w; `records/k13wy-*`), D12 held `150` under both Chromes, with
  the IME on (k13w) and left off (k13y). An earlier pass of this run read k13y only at 900 ms and took it for a lost
  paste (`records/k13y-*`, `records/k13x-repeat-*`); that reading was wrong.
- What remains is the time: at `0.1.0` the paste landed within the probe's 900 ms; here it takes longer (about
  1.35 s on Linux, by the Input Order thread's reading). That is a slowdown to record, not a defect.

Every other reading matches run 18b's.

## Filed

`verification/by-hand.json`: VZ-14, VZ-16, FN-12e (Windows), ED-30, SH-42, SH-51 and SH-52 name this record, the
verified commit `5e3eecb44fb9dcfec074cfa8eefe03a91809fbc3` and its tag `verified/2026-10-25-windows-19`, with the
verdict pass. The run read a paste landing later behind 150 ms (k13x), recorded under "Differences". WR-10 is filed by its own record, `verification/2026-10-25-linux-mudblazor-floor/`.

Still without a record that holds at the verified commit: FN-12e and KB-3 on macOS (the user's Mac), and VZ-18 (the
remote-isolation PC; its Known limitation reaches `0.1.0` only).
