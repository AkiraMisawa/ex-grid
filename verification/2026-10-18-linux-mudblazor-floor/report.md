# WR-10: the Wrappers' MudBlazor floor run, on Linux

**Verified commit:** `f6b5e8fe6c4b87d077200616d305bdd87a723aba` (head of `release/0.1`, "Release notes for
0.1.0-rc.1 (#160)"), from a clean tree.
**Tag:** `verified/2026-10-18-linux-mudblazor-floor`. **Verdict: pass.**

Made on 2026-10-18 by the procedure in the Definition of Done, §22 Step 4, "The Wrappers' MudBlazor
floor" (ADR-0205, WR-10), as part of the eighteenth Windows run's hand-over
(`verification/2026-10-18-windows-18/`), on Ubuntu 24.04 under WSL2 (kernel 6.6.114.1) on that desktop, with
Google Chrome 152.0.7977.64 and Microsoft Edge 152.0.4191.53 installed from their vendors' packages, headed
under xvfb (`xvfb-run -a -s "-screen 0 2560x1600x24"`, from nixpkgs), so that no window reached the desktop
while the Windows run drove it.

It replaces the record made at `885bfb98` (`verification/2026-10-17-linux-mudblazor-floor/`), which no
longer held at the verified commit: `src/*.MudBlazor/PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt`,
`src/ExSheet.MudBlazor/MudFormatCellsDialog.razor` and `src/ExSheet.MudBlazor/MudMoreColours.cs` changed
after it (the release check's own list).

## Layer 3 against a DemoHost built with MudBlazor 9.6.0

`samples/ExGrid.DemoPages/ExGrid.DemoPages.csproj` was pinned to 9.6.0 for this run and restored
afterwards. Both hosts were built from clean, and the version each one loaded was read back
(`mudblazor-versions.txt`): WebAssembly `9.6.0.0`, Server `9.6.0.0`.

The command was `npx playwright test --grep "$G"`, with `G` exactly as the Definition of Done writes
it, on ports of the run's own (`EXGRID_BASE_URL` `http://localhost:5338` and `http://localhost:5337`). It ran
every project: chrome, msedge, and chrome-150's Wrapper tests. Each test ran once, and none was run again.

| Host | Log | Passed | Failed | Skipped |
|---|---|---|---|---|
| WebAssembly | `mudblazor-floor.log` | 727 | 0 | 26 |
| Server | `mudblazor-floor-server.log` | 753 | 0 | 0 |

Each host ran 372 tests on chrome, 372 on msedge and 9 on chrome-150. The 26 skipped on WebAssembly (13 on
each browser) are the tests that skip themselves off the Server host; they ran and passed on the Server host.

## Layer 2 at every published version of the range

Each Wrapper's layer-2 suite, with MudBlazor pinned in its test project to every stable 9.x from 9.6.0
listed on nuget.org at the time of the run (`versions-listed.txt`): 9.6.0, 9.7.0, 9.8.0, 9.9.0, 9.10.0
and 9.11.0. Each log names the commit, and reads back the `MudBlazor.dll` each run loaded.

| Version | ExGrid.MudBlazor.Tests | ExSheet.MudBlazor.Tests | ExPivot.MudBlazor.Tests |
|---|---|---|---|
| 9.6.0 | 316 passed | 98 passed | 84 passed |
| 9.7.0 | 316 passed | 98 passed | 84 passed |
| 9.8.0 | 316 passed | 98 passed | 84 passed |
| 9.9.0 | 316 passed | 98 passed | 84 passed |
| 9.10.0 | 316 passed | 98 passed | 84 passed |
| 9.11.0 | 316 passed | 98 passed | 84 passed |

No test failed or was skipped at any version.

## What holds it

The record holds while `src/*.MudBlazor/` and `samples/ExGrid.DemoPages/` are unchanged from
`f6b5e8fe` up to the commit tagged for release (ADR-0205's note of 2026-10-12). A MudBlazor 9.x
published after this run asks for the layer-2 runs at that version only.
