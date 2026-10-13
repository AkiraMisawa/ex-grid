# Windows, 2026-10-12: ED-30's k8 with the Microsoft IME's predictive input off

**Verified commit:** `8436e45e73083944e413e92340f73dbba0b521c4` — tagged `verified/2026-10-12-windows-ed30-k8`

**The build is `8436e45e`**: the seventeenth run's DemoHosts, from its worktree, not rebuilt. The branch
`claude/exsheet-windows-ed30-k8-2026-10-12` is from `claude/exsheet-windows-verify-17` at `2fc23f56`, whose
`part-g/ed30/` holds the probe. Asked by the requesting session as its item 3, with the user at the desk, 21:41 to
21:52 BST. Run by a Claude Code session in WSL2 on the Windows desktop of the earlier runs. **Nothing was decided.**

**Why.** In the seventeenth run (and the sixteenth), k8's press on D12 landed on the IME's own prediction list, which
stands under the composition, and never reached the page. So the row's last clause was "not run as written". This run
turns the IME's predictive input off and runs k8 again. The requester's note: this build's `ex-grid.js` changes before
`0.1.0-rc.1`, so this run checks the method and looks for a defect. **It is not ED-30's record of sign-off.**

## Environment

- Windows 11 25H2 (build 26200.9457), 150%, display language and regional format English (United Kingdom).
- The Microsoft IME, `imjptip.dll` 10.0.26100.9278, Japanese; Chrome 154.0.8037.98 and Edge 155.0.4283.45.
- The DemoHosts in WSL, as in the seventeenth run: WebAssembly on `localhost:5299`, Server on `localhost:6298`, the
  demo API on 8299 and 8298. `latency-proxy.mjs` on Windows' Node: 5298 → 6298, control on 7298. Each Server record's
  `roundTripSet` reads back the round trip it asked for (`rtt=0`, `rtt=150`).
- The probe: run 17's `ime17-probe.mjs`, unchanged (byte for byte `part-g/ed30/ime-probe.mjs`), run with
  `scripts/run-k8.cmd`, which is run 17's `run-ime.cmd` writing to a folder of its own. Real keys through `SendInput`
  (one virtual key and scan code per key) and a real press, as there.

## The setting

- **The setting the requester named, "Use text suggestions", is not on this IME's General page.** The page's
  **Predictive Input** group has a list, "Select the length of input characters to see your word suggestions. You can
  turn these suggestions off if you don't use them", with the items Off and 1 to 5 characters. Beside it are
  "Use input history" and "Use system dictionary" (both On) and "Cloud suggestion" (Off).
- **Before: "1 character".** It was set to **Off** through the Settings app, by UI Automation
  (`scripts/uia-settings.ps1`, `scripts/uia-combo.ps1`). The IME's registry key before and after is in
  `logs/ime-registry.txt`:
  - Off wrote `PredictionUseInputHistory` 0 (it was 1) and added `PredictionUseSystemDictionary` 0.
  - `PredictionMinimumCompositionStringLength` stayed 1.
- **Put back** at 21:52: "1 character" chosen again. That left `PredictionUseSystemDictionary` 1, a value that was
  not there before, and it was removed.
  - The key is now the export from before but for one value: `ResetExcludeProperties\IHDS`'s `CDSSaveRequestTime`.
    It went from 2026-10-11 20:43:53 UTC to 2026-10-12 20:45:32 UTC, during the run's typing. It is the IME's own time
    stamp for saving what it learns, and it was left as the IME wrote it.

## k8, as written

The procedure's steps, unchanged: D10 selected, the IME on (VK_IME_ON), `k` `a` `n` `a`, then a real press on the
middle of D12.

The configurations: `/sheet` and `/sheet?chrome=mud`, each on WebAssembly, Server, and Server behind 150 ms, in
Chrome and in Edge. That is 12 records (`records/<page>-<host>-<browser>.json`; `summary.txt`, from
`summarise.py`). Every run exited 0. The console held one or two `info` messages and nothing else.

| State | Every configuration |
|---|---|
| the IME on | the keyboard in the Keyboard Field, Focus D10 |
| `k`, `a`, `n`, `a` | `ｋ`, `か`, `かｎ`, `かな` composing in the field over D10; no edit open |
| **the press on D12** | **`compositionend` `かな`, then the press reached the page** (mousedown and click on the viewport). **D10 `かな`, the Focus D12, no edit open, the keyboard in the Keyboard Field** (`active` key-field, its `aria-activedescendant` D12's cell) |

- **k8 is met as written in 12 of 12 configurations.** With predictive input off, no list stood under the composition,
  and the press on D12 reached the page.
- One reading as in the seventeenth run's k8x (the press on D8): the press shows two `mousedown` events about 3 ms
  apart, then one `mouseup` and one `click`. A press on a Heading (k10) shows one.
- Pictures of the WebAssembly and Chrome record are in `shots/`, cropped to the page: the composition `かな` over
  D10, then D10 `かな` and D12 selected.

## Nothing for the Docs Site

This branch adds a verification record only.
