# Windows, eighteenth run, at 0.1.0-rc.3's commit: the by-hand rows again (ADR-0208)

**Verified commit: `28ab5e1bf8c1e4c71dbcb19e9bd430cbd0612f48`** (head of `release/0.1`, "0.1.0-rc.3: the sign-off's
three fixes, and rc.3's notes are cut (#169)"), tagged `verified/2026-10-18-windows-18b`. The procedure is
`docs/specs/exsheet/verify-on-windows-18.md`, Part A, as `verification/2026-10-18-windows-18/` ran it.

The sign-off on `0.1.0-rc.2` failed three MUST rows (RR-9, FN-18 and KB-2). Their fix (#168, brought to the branch
by #169) touches the paths of every Windows row but WR-10, so `release/0.1` moved, and the rows recorded at
`f6b5e8fe` no longer hold at it. As the procedure says for a branch that moves, this directory holds the rows run
again at the new commit. Run on 2026-10-18, from 12:30 to 13:50 local time (BST), by the same Claude Code session on
the same Windows desktop, the same way, with the same tools and scripts (`verification/2026-10-18-windows-18/`'s,
copied here unchanged), and with the same authorisation. **Nothing was decided**; no ADR, `CONTEXT.md`,
`docs/definition-of-done.md`, code or test was changed.

**WR-10** still holds at this commit: `git diff --name-only verified/2026-10-18-linux-mudblazor-floor 28ab5e1b --
':(glob)src/*.MudBlazor/**' samples/ExGrid.DemoPages/` lists nothing. It was not run again.

## Environment

As in `2026-10-18-windows-18/report.md`, except for these:

- **Edge 155.0.4283.62** throughout, the version it updated itself to during the earlier run. **Chrome
  155.0.8059.82**.
- **The DemoHosts** were built at `28ab5e1b` in a worktree of their own in WSL, with 0 warnings and 0 errors, on the
  same ports as before (WebAssembly 5318, Server 6317, the demo API 8318 and 8317, the latency proxy 5317 → 6317,
  control on 7317). `tests/ExGrid.Browser` was copied again at `28ab5e1b` for Windows' Node, with `npm ci`.
- Display 150%, display language English (United Kingdom), and regional format en-GB, none of them changed.
  The Microsoft IME's Predictive input is the user's setting, "1 character", except while k8 and k8x ran.

## Verdicts

| Row | Commit | Browser | Host | Scale | Verdict |
|---|---|---|---|---|---|
| VZ-14 | `28ab5e1b` | Chrome 155, Edge 155 | WebAssembly, Server | 150% (DPR 1.5) | **pass** |
| VZ-16, a real browser zoom told as it changes | `28ab5e1b` | Chrome 155, Edge 155 | WebAssembly | display 150%, zoom 67% to 200% | **pass** |
| VZ-16, a thin line and a gridline at a real 2.25 | `28ab5e1b` | Chrome 155, Edge 155 | WebAssembly | display 150%, zoom 150% (DPR 2.25) | **pass** |
| FN-12e (Windows), the two paintings | `28ab5e1b` | Chrome 155, Edge 155 | WebAssembly, Server | 150% | **pass** |
| ED-30 | `28ab5e1b` | Chrome 155, Edge 155 | WebAssembly, Server, Server behind 150 ms | 150% | **pass** |
| SH-42 | `28ab5e1b` | Chrome 155, Edge 155 | WebAssembly, Server | 150% | **pass** |
| SH-51 | `28ab5e1b` | Chrome 155, Edge 155 | WebAssembly, Server | 150% | **pass** |
| SH-52 | `28ab5e1b` | Chrome 155, Edge 155 | WebAssembly, Server, Server behind 80 ms | 150% | **pass** |

## What was read

Every reading is the same as at `f6b5e8fe` (`2026-10-18-windows-18/report.md`), but for ED-30's k15 (below).

- **VZ-14** (`part-a/vz14/`): 4 of 4 passed. `devicePixelRatio` 1.5, the gutter 12 × 12 CSS px, the Focus inside the
  client box at End and at Home.
- **FN-12e** (`part-a/fn12e/`): 8 of 8 passed, the two paintings under both Chromes on both hosts.
- **SH-42** (`part-a/sh42/`): Ctrl+1 opened Format Cells, Ctrl+2 to Ctrl+5 made D10 bold, italic, underlined and
  struck through. The keys reached the Keyboard Field, and no tab switched, in all four configurations.
- **SH-51** (`part-d-summary.md`, d5): Ctrl+F1 hid the toolbar and Ctrl+F1 showed it, in all four.
- **SH-52** (`part-d-summary.md`, d3, d4, d6; `records/sh52-speed-*.jsonl`):
  - Alt alone and F10 showed the KeyTips H and Y, and the browser's menu did not open.
  - The Home row's letters were 1, 2, 3, 4, FC, H, B, AL, AC, AR, N, P, K, OE, each running its item. Escape from a
    list went back one level at a time. Under the MudBlazor Chrome the lists closed with Escape (`part_d_extra.py`).
  - With an edit open, Alt and F10 started nothing.
  - Typed at once, each sequence had the outcome it has typed slowly, on WebAssembly and behind 80 ms: Alt alone;
    Enter on an open edit, then Alt, H, 1; Alt, Alt, H, 1. Alt+↓ with the Alt released first showed no KeyTips.
  - The console held `info` only. `part-d-summary.md` is the earlier run's, line for line, but for the generated ids
    of Format Cells' tab list.
- **VZ-16** (`records/part-g-vz16-*.jsonl`): `--ex-dp` was 1 / `devicePixelRatio` after every zoom step, the same
  figures as before. At the real 2.25, B2's thin Border was one black Device Pixel and E8's gridline two Device
  Pixels of `#E0E0E0`, under both Chromes.
- **ED-30** (`part-g/ed30/`): the main pass ran 36 records, 12:51 to 13:31, with no error and exit 0 throughout.
  - **k8 and k8x with Predictive input Off**: 12 records, 13:32 to 13:37. Off at 13:32:11, back to "1 character" at
    13:37:05. `PredictionUseSystemDictionary`, which putting it back added, was removed at 13:37:06. The IME's key
    after is identical to the export before (`logs/`).
  - **The pass column**, clause by clause, every configuration:
    - D10 shows the composition while no edit is open (k2);
    - after its end, the Cell Editor holds `かな` with DOM focus (k2);
    - Enter commits it and moves to D11 (k2);
    - after a press on D12 mid-composition, D10 holds `かな` and D12 is selected, with no edit open (k8).
  - `against-17.txt` compares every state with run 17's records: 10 of 1848 differ. `against-18.txt` compares every
    state with `2026-10-18-windows-18`'s: 14 of 1992 differ. Every difference is in k15.
  - **k15** (Alt+Tab mid-composition, a case to record). In 5 of 12 configurations the composition went on in the
    Keyboard Field beside an open Cell Editor holding `かな`, rather than ending:
    - `/sheet` on WebAssembly in Edge;
    - `/sheet?chrome=mud` on WebAssembly in Chrome, on Server in Chrome and Edge, and behind 150 ms in Edge.

    In the other 7 it ended, the Cell Editor holding `かな` with the keyboard. At `f6b5e8fe` it went on in 2 of 12
    (Server in Chrome on `/sheet`, and behind 150 ms in Chrome on `/sheet?chrome=mud`), and those two ended here.
    Enter, Enter then gave D10 `かな` and the Focus D12 in all 12, as before. Which configurations go on differs from
    run to run.
  - **t3a and t3b** (`records/ed30-t3-*.jsonl`), the same as before in all four configurations:
    - 半角/全角 turned the IME off and reached the page as a keyup alone. Alt+半角/全角 was a chord. Neither showed
      the KeyTips.
    - Alt, H, 1 typed at once made D10 bold, then `kana` composed over D10 from its first key. Enter, Enter gave D10
      `かな`, bold, and the Focus D11, at both speeds.

## Differences

Every row is a pass. ED-30's k15 reads differently from both earlier runs: it is a case to record, not a clause of
the row's pass column, and the configurations where the composition survives Alt+Tab change from run to run (ADR-0080).

## Filed

`verification/by-hand.json`: VZ-14, VZ-16, FN-12e (Windows), ED-30, SH-42, SH-51 and SH-52 name this record, the
verified commit `28ab5e1bf8c1e4c71dbcb19e9bd430cbd0612f48` and its tag `verified/2026-10-18-windows-18b`, with the
verdict pass. WR-10's record (`2026-10-18-linux-mudblazor-floor`) is unchanged and holds.
