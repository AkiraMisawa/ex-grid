# WR-10: the Wrappers' MudBlazor floor run, on Linux

**Verified commit:** `885bfb98dd92a1e81e905b651d03e216afa24769` (`main` after #137), from a clean tree.
**Tag:** `verified/2026-10-17-linux-mudblazor-floor`. **Verdict: pass.**

Made on 2026-10-17 by the procedure in the Definition of Done, §22 Step 4, "The Wrappers' MudBlazor
floor" (ADR-0205, WR-10), on Ubuntu 24.04 under WSL2 (kernel 6.6.114.1), with Google Chrome
152.0.7977.64 and Microsoft Edge 152.0.4191.53 installed from their vendors' packages, headed on the
WSLg display.

It replaces the record made at `0399f391` (`verification/2026-10-15-linux-mudblazor-floor/`), which no
longer held: `samples/ExGrid.DemoPages/SheetCases.cs` changed after it. The Settled Mark (#134) and
ADR-0220's specs and CI (#136, #137, #138, #139) came in between too.

## Layer 3 against a DemoHost built with MudBlazor 9.6.0

`samples/ExGrid.DemoPages/ExGrid.DemoPages.csproj` was pinned to 9.6.0 for this run and restored
afterwards. Both hosts were built from clean, and the version each one loaded was read back
(`mudblazor-versions.txt`): WebAssembly `9.6.0.0`, Server `9.6.0.0`.

The command was `npx playwright test --grep "$G"`, with `G` exactly as the Definition of Done writes
it. It ran every project: chrome, msedge, and chrome-150's Wrapper tests. Each test ran once, and none
was run again (ADR-0220 §3).

| Host | Log | Passed | Failed | Skipped |
|---|---|---|---|---|
| WebAssembly | `mudblazor-floor.log` | 725 | 0 | 24 |
| Server | `mudblazor-floor-server.log` | 749 | 0 | 0 |

Each host ran 370 tests on chrome, 370 on msedge and 9 on chrome-150. The 24 tests skipped on
WebAssembly skip themselves off the Server host (`test.skip(!SERVER, …)`), and they ran and passed on
the Server host.

## Layer 2 at every published version of the range

Each Wrapper's layer-2 suite ran with MudBlazor pinned in its test project, once for each stable 9.x
from 9.6.0 listed on nuget.org at the time of the run: 9.6.0, 9.7.0, 9.8.0, 9.9.0, 9.10.0 and 9.11.0.
Each log names the commit, and reads back the `MudBlazor.dll` that each run loaded.

| Version | ExGrid.MudBlazor.Tests | ExSheet.MudBlazor.Tests | ExPivot.MudBlazor.Tests |
|---|---|---|---|
| 9.6.0 | 316 passed | 96 passed | 84 passed |
| 9.7.0 | 316 passed | 96 passed | 84 passed |
| 9.8.0 | 316 passed | 96 passed | 84 passed |
| 9.9.0 | 316 passed | 96 passed | 84 passed |
| 9.10.0 | 316 passed | 96 passed | 84 passed |
| 9.11.0 | 316 passed | 96 passed | 84 passed |

No test failed or was skipped at any version.

## What holds it

The record holds while `src/*.MudBlazor/` and `samples/ExGrid.DemoPages/` are unchanged from
`885bfb98` up to the commit tagged for release (ADR-0205's note of 2026-10-12). A MudBlazor 9.x
published after this run asks for the layer-2 runs at that version only.
