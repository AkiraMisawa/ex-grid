# Windows, eighteenth run: the by-hand rows at the commit `0.1.0-rc.1` is to be tagged on (ADR-0208)

**Verified commit: `f6b5e8fe6c4b87d077200616d305bdd87a723aba`** (head of `release/0.1`, "Release notes for 0.1.0-rc.1
(#160)"), tagged `verified/2026-10-18-windows-18`. The procedure is `docs/specs/exsheet/verify-on-windows-18.md`.

Run on 2026-10-18, from 03:10 to 04:45 local time (BST), by a Claude Code session running natively on the Windows
desktop of the earlier runs, driving WSL2 for the build. The tag was pushed before the first Part. **Nothing was
decided.** No ADR, `CONTEXT.md` or `docs/definition-of-done.md` was changed, and no code or test.

**Which Parts ran.** The request that handed this run over (the release driver, through the project's coordinator)
asked for Part A's rows, VZ-14, VZ-16, FN-12e, ED-30, SH-42, SH-51 and SH-52, and for WR-10 again (Part B's), whose
record no longer held. Parts C, D and E were not asked for, and did not run: Part C's open corpus cases were settled
before the release candidate by another run (#156, #158). WR-10 ran in WSL, on Linux, and has a record and tag of its
own, `verification/2026-10-18-linux-mudblazor-floor/` (below, "WR-10"). FN-12e on macOS, KB-3 and VZ-18 are not this
desk's.

**The branch** is `claude/project-thread-shea8d` (the name the session was given), from the verified commit, in place
of the procedure's `claude/exsheet-windows-verify-18`. `release/0.1` did not move during the run.

**The user's authorisation.** The user asked, through the project, for these rows to be run on this machine without
them ("手で確かめるやつも各マシンで自動で確認してくれよ"), and said in this run's thread that the IME's Predictive input
need not be asked about each time ("いちいちきかなくていいよ"). It was turned off for k8 and k8x only, and put back
(ED-30, below).

## Files

| | |
|---|---|
| `report.md` | this report |
| `pg.py`, `pg-server.mjs`, `input-server.ps1`, `browser-input.ps1` | run 17's tools: a headed Windows Chrome or Edge, real keys and presses through `SendInput`. Changed only in ports and paths, but `input-server.ps1`, which gained `raw` (key events in an order of the run's own, with a scan code when given) |
| `part-a/pw.cmd`, `part-a/vz14/`, `part-a/fn12e/` | VZ-14 and FN-12e: run 17's `vz14.cmd` and `fn12e.cmd` as one script; Playwright's list and JSON output |
| `sh42-probe.mjs`, `sh42.cmd`, `part-a/sh42/` | SH-42: run 17's probe; its records |
| `part_d_sheet.py`, `part_d_extra.py`, `part-d-summary.py`, `part-d-summary.md` | SH-51 and SH-52: run 17's Part D d3 to d6, and its summary |
| `sh52_speed.py` | SH-52: the keys at machine speed (new) |
| `part_g_vz16.py` | VZ-16: run 17's probe |
| `part-g/ed30/` | ED-30: run 17's probe, scripts and summariser; `run-all.sh` with a pass for k8; the IME setting's scripts (from `2026-10-12-windows-ed30-k8/`), its registry exports (`logs/`) and the times it was changed (`logs/predictive.log`); `summary.txt`, `against-17.txt`; the records |
| `t3_ime.py` | ED-30: the IME and the Alt key, t3a and t3b (new) |
| `records/` | the `pg.py` probes' records |
| `shots/` | the page's own pictures: ED-30's for WebAssembly and Chrome (as run 17 kept them), SH-52's and t3's, VZ-16's device-pixel clips. Whole-window pictures stay on the machine |

## Environment

- **Windows 11 Pro 25H2**, build 26200.9711. One display, 3840 × 2160 device px at **150%** (DPI 144), not changed.
  Display language **English (United Kingdom)**; regional format **en-GB** at the start and at the end, not changed.
  The language list: en-GB (keyboard English (UK)), en-US, and ja with the Microsoft IME.
- **The Microsoft IME** (Japanese), `imjptip.dll` 10.0.26100.9711. Its Predictive input: **1 character** (the user's
  setting), Off for k8 and k8x only.
- **Chrome 155.0.8059.82**. **Edge 155.0.4283.45**, which updated itself to **155.0.4283.62** during ED-30's run: every
  record names its version (ED-30's `sheet-wasm-msedge` and `sheet-server-msedge` ran on .45, its later Edge records on
  .62; VZ-14, FN-12e, SH-42, SH-51, SH-52 and VZ-16 on .45; t3 on .62).
- **How it ran.** Windows has no .NET 10 SDK, so the DemoHosts were built and run in WSL2 (Ubuntu 24.04, the .NET SDK
  10.0.203 through nix), from a worktree at the verified commit: WebAssembly on `localhost:5318`, the Server host on
  `localhost:6317`, the demo API on 8318 and 8317 (20,000 trades). The browsers are Windows' own, launched by
  Playwright 1.62.1 under Windows' portable Node v24.14.1 from a copy of `tests/ExGrid.Browser` at the verified commit
  (`npm ci` on Windows), reaching the hosts through WSL's localhost forwarding. `latency-proxy.mjs` ran on Windows'
  Node: 5317 → 6317, control on 7317. No other checkout on this desktop used these ports.

## Part A — the by-hand rows (ADR-0208)

**Which rows to run**, at the verified commit (`git diff --name-only <record commit> f6b5e8fe -- <the row's paths>`):

| Row | Record before this run | Files changed since |
|---|---|---|
| VZ-14 | `16552992` (`verified/2026-10-01-windows-15`), pass | 69 |
| VZ-16 | none | — |
| FN-12e (Windows) | none | — |
| ED-30 | `bf13de19`, not a pass | 142 |
| SH-42 | `76d3866c`, pass | 67 |
| SH-51 | none | — |
| SH-52 | none | — |
| WR-10 | `885bfb98`, pass | 8 |

Every row ran. The display stayed at 150% throughout.

### Verdicts

| Row | Commit | Browser | Host | Scale | Verdict |
|---|---|---|---|---|---|
| VZ-14 | `f6b5e8fe` | Chrome 155, Edge 155 | WebAssembly, Server | 150% (DPR 1.5) | **pass** |
| VZ-16, a real browser zoom told as it changes | `f6b5e8fe` | Chrome 155, Edge 155 | WebAssembly | display 150%, zoom 67% to 200% | **pass** |
| VZ-16, a thin line and a gridline at a real 2.25 | `f6b5e8fe` | Chrome 155, Edge 155 | WebAssembly | display 150%, zoom 150% (DPR 2.25) | **pass** |
| FN-12e (Windows), the two paintings | `f6b5e8fe` | Chrome 155, Edge 155 | WebAssembly, Server | 150% | **pass** |
| ED-30 | `f6b5e8fe` | Chrome 155, Edge 155 | WebAssembly, Server, Server behind 150 ms | 150% | **pass** |
| SH-42 | `f6b5e8fe` | Chrome 155, Edge 155 | WebAssembly, Server | 150% | **pass** |
| SH-51 | `f6b5e8fe` | Chrome 155, Edge 155 | WebAssembly, Server | 150% | **pass** |
| SH-52 | `f6b5e8fe` | Chrome 155, Edge 155 | WebAssembly, Server, Server behind 80 ms | 150% | **pass** |

### VZ-14

`scrollbar.spec.mjs`'s block "on a Windows desktop at fractional display scaling (VZ-14)", headed, on Chrome and Edge,
on WebAssembly and on the Server host (`part-a/pw.cmd`; `part-a/vz14/`). 4 of 4 passed.

- The browser reported `Windows`; `devicePixelRatio` 1.5.
- The gutter: 12 × 12 CSS px, the grid's own bar. The scroller's device content box 1332 × 882.
- The Focus lay inside the client box at End and at Home (`deviceRounding` 0 and 1.33 on the way).
- **The deferred report** (`toldAfterTheObservation`, since `2d8695d9`; from `dod-gaps-browser.md`, item 1): read at
  this fractional scale for the first time, the gutter and the Focus's place read as run 17 read them before it.

### VZ-16

Run 17's `part_g_vz16.py`, Chrome and Edge, WebAssembly (`records/part-g-vz16-<browser>.jsonl`).

**The zoom**, on `/wide`, by real keys outside any Sheet: Ctrl+0, Ctrl+= five times, Ctrl+0, Ctrl+- four times, Ctrl+0.
After each, the grid's root carried `--ex-dp` = 1 / `devicePixelRatio`. Chrome and Edge read the same, and the same as
run 17: 1.5 / 0.667, 1.65 / 0.606, 1.875 / 0.533, 2.25 / 0.444, 2.625 / 0.381, 3 / 0.333, back to 1.5; 1.35 / 0.741,
1.2 / 0.833, 1.125 / 0.889, 1 / 1; back to 1.5.

**The real 2.25**: the display's 150% and the browser's 150% (Ctrl+= three times on `/wide`), then `/sheet?case=11` on
the same origin: `devicePixelRatio` 2.25, `--ex-dp` 0.444. Scrolled 400 CSS px; the compositor's picture of a
16 × 4 CSS px clip across each edge, given in the document's units before the zoom (run 17's fourth pass). Both
browsers:

| Line | Device pixels across it, not the white Paper | Reading |
|---|---|---|
| B2's thin Border, right edge | `#E1E1E1`, `#000000` | the Border one Device Pixel, black |
| B2's thin Border, bottom edge | `#E0E0E0`, `#000000` | the same |
| E8's gridline, right edge | `#E0E0E0`, `#E0E0E0` | the gridline two Device Pixels, no third blended |
| E8's gridline, bottom edge | `#E0E0E0`, `#E0E0E0` | the same |

The same as run 17. ADR-0206's tints and pinned shade, which landed after run 17's record, changed none of these
pixels. The console held `info` messages only. "Nothing per render reaches JavaScript" is P1–P9's, not read here.

### FN-12e (Windows)

The two paintings, headed, Chrome and Edge, WebAssembly and Server (`part-a/fn12e/`): `sizing.spec.mjs`'s "a bold
total the estimate fits paints whole, and a #### run fits its cell (FN-12e, ADR-0016)" and `part-c.spec.mjs`'s
"FN-12e (Part C, case 3c): #### fills its cell with whole # in the face it is painted in, and no more fit
(ADR-0016)". **8 of 8 passed.** The display language is English (United Kingdom), so `system-ui` is Segoe UI. The
measurement is not the row's since 2026-10-16, and was not asked.

### ED-30

Run 17's probe (`part-g/ed30/ime-probe.mjs`, run as `ime17-probe.mjs`) and keys, on `/sheet` and `/sheet?chrome=mud`,
Chrome and Edge, WebAssembly, Server, and Server behind 150 ms: 12 records per case; k7 behind 150 ms only, k11 on
`/features` and `/features?chrome=mud`, k14 on `/sheets` and `/sheets?chrome=mud`.

- **The main pass** (03:41 to 04:22): every case but k8 and k8x, at the user's Predictive input setting, 1 character.
  36 records, every run exit 0, no error; every console message `info`.
- **k8 and k8x with Predictive input off** (04:23 to 04:27), on `/sheet` and `/sheet?chrome=mud`, in the six
  configurations each: 12 records (`records/k8-*`).
  - The IME's registry key was exported before (`logs/imejp-before-k8.reg`). **Off at 04:22:54** through the Settings
    app's General page by UI Automation (`uia-settings.ps1`, `uia-combo.ps1`, as `2026-10-12-windows-ed30-k8/`): it
    wrote `PredictionUseInputHistory` 0 (was 1) and added `PredictionUseSystemDictionary` 0 (`logs/imejp-off.reg`).
  - **Put back to "1 character" at 04:27:46.** That left `PredictionUseSystemDictionary` 1, which was not there
    before, as the k8 run of 2026-10-12 found; it was removed at 04:27:55. **The key after
    (`logs/imejp-after.reg`) is identical to the export before**, and to the one taken at the start of the run.
- **The readings**: `part-g/ed30/summary.txt` groups each state across the configurations. `against-17.txt` compares
  every state with run 17's record of the same case, state and configuration: **4 of 1848 states read differently**,
  all of them k15's (below). (The file's heading, from run 17's script, says "sixteenth"; it compared run 17's
  records.) The k8 records have no run-17 counterpart, k8 not having reached the page there.

**The row** (the Definition of Done's pass column, clause by clause, as run 17 wrote them):

| Clause | Verdict |
|---|---|
| "D10 shows the composition while no edit is open" | **pass** (k2, every configuration) |
| "after its end the Cell Editor holds the text and has DOM focus" | **pass** (k2) |
| "Enter commits it and moves to D11" | **pass** (k2) |
| "after a press on D12 mid-composition, D10 holds the text and D12 is selected" | **pass** (k8, Predictive input off, 12 of 12: `kana` composing over D10, then the press on D12: D10 `かな`, the Focus D12, no edit open, the keyboard in the Keyboard Field) |

**Every case** (the same in all its configurations unless the row says otherwise; beside run 17):

| # | Read at `f6b5e8fe` | Beside run 17 |
|---|---|---|
| k1 | the IME on, Hiragana; the keyboard in the Keyboard Field | the same |
| k2 | `ｋ`, `か`, `かｎ`, `かな` composing in the field over D10, no edit; Enter: the Cell Editor holding `かな` with the keyboard; Enter: D10 `かな`, D11 | the same |
| k6, k6x, k6y | two compositions: the Cell Editor opens on the first while the keyboard stays in the Keyboard Field for the second; D10 `かな感じ`, nothing lost, doubled or reordered, in every configuration. This is what the procedure expects of k6 since #157 (ADR-0080). The last record's difference 1 (a key lost on WebAssembly) is not read | the same, state by state |
| k3 to k5, k7, k9 to k14, k13x, k16, dc47 | as run 17 read them | the same, state by state |
| k8 | with Predictive input off: D10 `かな`, the Focus D12, no edit open, the keyboard in the field | run 17 ran it with the prediction list on, and the press never reached the page |
| k8x | with Predictive input off: the press on D8: D10 `かな`, the Focus D8, no edit | the same |
| k15 | Alt+Tab away and back. **10 configurations**: the composition ended there, the Cell Editor open holding `かな`, with the keyboard. **Server Chrome on `/sheet`, and Server behind 150 ms Chrome on `/sheet?chrome=mud`**: the Cell Editor open holding `かな`, but the keyboard still in the Keyboard Field, `かな` still composing there, opacity 1, the coloured layer hidden, through away and back. Then Enter, Enter, in all 12: D10 `かな`, the Focus D11, then D12 | **differs** in those two configurations: run 17 read the composition ended and the keyboard in the Cell Editor in all 12. k15 is a case to record, not a clause of the row |

**The IME and the Alt key** (*from `toolbar.md`, item 3*; `t3_ime.py`; `records/ed30-t3-<host>-<browser>.jsonl`), on
`/sheet` with the toolbar shown, the window on the Japanese keyboard, Chrome and Edge, WebAssembly and Server:

- **t3a**, the Microsoft IME on and nothing composed. This keyboard is the Japanese one, which has no Alt+\`, so the
  toggles are 半角/全角 (`VK_OEM_AUTO`, scan code 0x29) and Alt+半角/全角:
  - **半角/全角**: the IME turned off. **No KeyTips**, the keyboard in the Keyboard Field. The page heard **no keydown
    and one keyup** (`Hankaku`): the IME took the press as its hotkey.
  - **Alt+半角/全角**: the page heard keydown Alt, keydown `Hankaku`, keyup `Hankaku`, keyup Alt. **No KeyTips**: the
    Alt was a chord's. The IME stayed on.
  - Alt tapped alone with the IME on, for comparison: the KeyTips H and Y, the keyboard on the toolbar.
  - The same in all four configurations.
- **t3b**, Alt, H, 1 typed at once (one `SendInput` call) with the IME off, then the IME on, `kana` typed at once,
  Enter, Enter; and the same typed slowly. In all four configurations, at both speeds: **D10 bold** after Alt, H, 1,
  the keyboard back in the Keyboard Field; `kana` composing in the field over D10 from its first key; the first Enter
  ends it, the Cell Editor holding `かな`; the second commits: **D10 `かな`, bold, the Focus D11**. Nothing lost or
  doubled.
  - Typed at once, the probe's listener heard some of the burst's keydowns twice (`h` and `1` on the Server host, `h`
    under Chrome on WebAssembly), the second without its own keyup; the outcome was as typed slowly.
- **A first pass of t3b is kept and is not the record** (`records/ed30-t3-first-pass-*.jsonl`): the probe left the IME
  on before the burst, so `h` and `1` were composed (`ｈ１`) instead of typed. With the IME on, Alt still showed the
  KeyTips under Edge and on the Server host, and they stayed shown while `ｈ１` was composed; under Chrome on
  WebAssembly they did not show. D10 was not made bold in any configuration. The probe then turned the IME off and read
  it back before the burst, and the record was run again.

### SH-42

Run 17's `sh42-probe.mjs` (`part-a/sh42/`), in a profile of its own with seven tabs, `/sheet` third, Chrome and Edge,
WebAssembly and Server. Ctrl+1 to Ctrl+5 on D10, each followed by Escape or Ctrl+Z:

| Key | What the page received | The Sheet | The browser |
|---|---|---|---|
| Ctrl+1 | `key=1`, ctrl, on the Keyboard Field | **Format Cells opened** | no tab switched |
| Ctrl+2 | `key=2` | **D10 bold** | no tab switched |
| Ctrl+3 | `key=3` | **D10 italic** | no tab switched |
| Ctrl+4 | `key=4` | **D10 underlined** | no tab switched |
| Ctrl+5 | `key=5` | **D10 struck through** | no tab switched |

The same in all four configurations, and the same as run 17. The selected tab stayed `sheet-verify-17` (the probe's
title). As in run 17, UI Automation's tab search reached Format Cells' own tabs after Ctrl+1 ("Number" in the
probe's `selectedTab`), inside the page.

### SH-51

Run 17's d5 (`part_d_sheet.py`): Ctrl+F1 on D10, then again, on `/sheet` (which binds `ShowToolbar`): **the toolbar
hidden, then shown**, the keyboard in the Keyboard Field, the browser's window in front and no window of its own
opened. The same in all four configurations.

### SH-52

Run 17's d3, d4 and d6 (`part_d_sheet.py`; `part-d-summary.md`), on `/sheet` and `/sheet?chrome=mud`, and the keys at
machine speed (`sh52_speed.py`). After every step the browser's window was in front and no window of the browser's own
(a menu) appeared; the document kept focus.

| # | Read (the same in all four configurations, under both Chromes) | Beside run 17 |
|---|---|---|
| d3 | Alt alone: **the KeyTips H (Home) and Y (This page)**, the keyboard on the toolbar; the browser's menu did not open. Escape: back in the Keyboard Field. F10: the same KeyTips. Escape: back | the same |
| d4 | Alt, H: **1, 2, 3, 4, FC, H, B, AL, AC, AR, N, P, K, OE**. Each on D10 (`1234.5`): 1 bold, 2 italic, 3 underline, 4 a line through; FC, H, B and N opened their lists, the keyboard in the list; AL left, AC centre, AR pressed; P `123450%`; K changed nothing; **OE** opened Format Cells. After a list, the first Escape went back to the Home row's letters and the second to the top level's, H and Y | **differs**: Format Cells' letters are **OE**, not O, as SH-52 now words them (Excel's Format menu, O, E). After a list's Escape, run 17 read the keyboard back in the field at once; now each Escape goes back one level, as SH-52 now words it (R2 (a) and (b)) |
| d4, MudBlazor | the same letters, and the lists **closed with Escape**: none stayed open, none piled up (`part_d_extra.py`: after FC and B, no `.mud-popover-open` after one Escape) | **differs**: run 17 found FC, H, B and N staying open after Escape and piling up |
| d6 | F2 on D10, then Alt: nothing starts, the keyboard in the Cell Editor; F10: nothing; Escape: back | the same |
| console | `info` only, on both hosts, under both Chromes | **differs**: run 17's `aria-hidden` warning on the Server host under the MudBlazor Chrome did not appear |

**Keys at machine speed** (*from `toolbar.md`, item 2*), on `/sheet` with the toolbar shown, Chrome and Edge, on
WebAssembly and on the Server host behind 80 ms. Each sequence from a fresh page, D10 holding 7, typed in one
`SendInput` call and, beside it, with 250 ms between keys:

| Sequence | At once | Slowly | The browser |
|---|---|---|---|
| Alt released alone | the KeyTips H and Y, the keyboard on the toolbar | the same | menu closed, the page in front, the document focused |
| Enter on an open edit (F2 on D10), then Alt, H, 1 | the edit committed, the Focus D11, **D11 bold**, no KeyTips, the keyboard in the field | the same | the same |
| Alt, Alt, H, 1 | the KeyTips shown and hidden; `h1` typed into an edit of D10 | the same | the same |
| Alt+↓, the Alt released before the ↓ (one call: Alt down, ↓ down, Alt up, ↓ up) | **no KeyTips**, nothing opened, the Focus D10 | — | the same |

The same in all four configurations: typed at once, each had the outcome it has typed slowly.

## WR-10 — the Wrappers' MudBlazor floor run

Run in WSL (Ubuntu 24.04) from a second worktree at the verified commit, by the Definition of Done's §22 Step 4, "The
Wrappers' MudBlazor floor". Its record is **`verification/2026-10-18-linux-mudblazor-floor/report.md`**, tagged
`verified/2026-10-18-linux-mudblazor-floor`: see there.

## Differences

Every by-hand row this run asked is a pass. What read differently from run 17, or did not match:

- **ED-30, k15** (a case to record): in two of twelve configurations (Server Chrome on `/sheet`; Server behind 150 ms
  Chrome on `/sheet?chrome=mud`), Alt+Tab mid-composition left the composition going in the Keyboard Field, beside an
  open Cell Editor holding the same text, instead of ending it. Enter, Enter then gave D10 `かな` as in the other ten.
  The Definition of Done's ED-30 names k15 among layer 3's cases ("after which the Cell Editor has the keyboard holding
  the text"), not in its by-hand pass column.
- **ED-30, t3b typed at once**: the probe heard some of the burst's keydowns twice; the outcome was as typed slowly
  (ADR-0100, ADR-0021's seventh entry).
- **ED-30, t3a**: 半角/全角 reaches the page as a keyup alone; Alt+半角/全角 as a chord. Neither starts the KeyTips.
- **SH-52, d4**: Format Cells' letters OE, and Escape going back one level from a list; both as the row now words them.
  Under the MudBlazor Chrome the lists close with Escape; run 17's pile-up is not read.
- **SH-52, console**: run 17's `aria-hidden` warning is not read.
- **Edge updated itself** from 155.0.4283.45 to 155.0.4283.62 during ED-30's main pass; each record names its version.

## Filed

`verification/by-hand.json`: VZ-14, VZ-16, FN-12e (Windows), ED-30, SH-42, SH-51 and SH-52 name this record, the
verified commit and its tag, with the verdict pass. WR-10 names its own record (above).
