# WR-10: the Wrappers' MudBlazor floor run, on Linux

**Verified commit:** `0399f391ba2c89202aa477a1cdc42642994faaca` (`main` after #119), from a clean tree.
**Tag:** `verified/2026-10-15-linux-mudblazor-floor`. **Verdict: pass.**

Made on 2026-10-15 by the procedure in the Definition of Done, §22 Step 4, "The Wrappers' MudBlazor
floor" (ADR-0205, WR-10), in a Claude Code cloud container: Ubuntu 24.04, Google Chrome 155.0.8059.79
and Microsoft Edge 155.0.4283.45 installed from their vendors' packages, headed under xvfb
(`-screen 0 2560x1600x24`), as CI runs layer 3.

## Layer 3 against a DemoHost built with MudBlazor 9.6.0

`samples/ExGrid.DemoPages/ExGrid.DemoPages.csproj` was pinned to 9.6.0 for the run only, and put back
afterwards. Both hosts were built from clean, and the version each loaded was read back
(`mudblazor-versions.txt`): WebAssembly `9.6.0.0`, Server `9.6.0.0`.

`npx playwright test --grep "$G"`, with `G` exactly as the Definition of Done writes it, every project
(chrome, msedge, and chrome-150's Wrapper tests):

| Host | Log | Passed | Failed | Skipped |
|---|---|---|---|---|
| WebAssembly | `mudblazor-floor.log` | 721 | 0 | 24 |
| Server | `mudblazor-floor-server.log` | 745 | 0 | 0 |

Each host ran 368 tests on chrome, 368 on msedge and 9 on chrome-150. The 24 skipped on WebAssembly
are the tests that skip themselves off the Server host (`test.skip(!SERVER, …)`); they ran and passed
on the Server host.

## Layer 2 at every published version of the range

Each Wrapper's layer-2 suite, with MudBlazor pinned in its test project to every stable 9.x from 9.6.0
listed on nuget.org at the time of the run: 9.6.0, 9.7.0, 9.8.0, 9.9.0, 9.10.0 and 9.11.0. Each log
names the commit, and reads back the `MudBlazor.dll` each run loaded.

| Version | ExGrid.MudBlazor.Tests | ExSheet.MudBlazor.Tests | ExPivot.MudBlazor.Tests |
|---|---|---|---|
| 9.6.0 | 316 passed | 95 passed | 84 passed |
| 9.7.0 | 316 passed | 95 passed | 84 passed |
| 9.8.0 | 316 passed | 95 passed | 84 passed |
| 9.9.0 | 316 passed | 95 passed | 84 passed |
| 9.10.0 | 316 passed | 95 passed | 84 passed |
| 9.11.0 | 316 passed | 95 passed | 84 passed |

No test failed or was skipped at any version.

## What holds it

The record holds while `src/*.MudBlazor/` and `samples/ExGrid.DemoPages/` are unchanged from
`0399f391` up to the commit tagged for release (ADR-0205's note of 2026-10-12). A MudBlazor 9.x
published after this run asks for the layer-2 runs at that version only.
