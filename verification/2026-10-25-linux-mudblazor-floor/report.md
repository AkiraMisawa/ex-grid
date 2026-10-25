# WR-10: the Wrappers' MudBlazor floor run, on Linux

**Verified commit:** `5e3eecb44fb9dcfec074cfa8eefe03a91809fbc3` (head of `main`, "Merge pull request #258"), from a
clean tree.
**Tag:** `verified/2026-10-25-linux-mudblazor-floor`. **Verdict: pass.**

Made on 2026-10-25 by the procedure in the Definition of Done, §22 Step 4, "The Wrappers' MudBlazor floor"
(ADR-0205, WR-10), beside the nineteenth Windows run (`verification/2026-10-25-windows-19/`), on Ubuntu 24.04 under
WSL2 on that desktop, with Google Chrome 152.0.7977.64 and Microsoft Edge 152.0.4191.53 installed from their vendors'
packages, headed under xvfb (`xvfb-run -a -s "-screen 0 2560x1600x24"`, from nixpkgs), so that no window reached the
desktop while the Windows run drove it. The script is `verification/2026-10-25-windows-19/wr10.sh`.

It replaces the record made at `f6b5e8fe` (`verification/2026-10-18-linux-mudblazor-floor/`), which no longer held:
`samples/ExGrid.DemoPages/` and the Wrappers changed after it (the release check's own list, 86 files).

## Layer 3 against a DemoHost built with MudBlazor 9.6.0

**One step differs from the Definition of Done's command.** Its `sed` pins only
`samples/ExGrid.DemoPages/ExGrid.DemoPages.csproj` to 9.6.0. Since `37240f45` (2026-10-20) the demo pages reference
`samples/ExChart.Showcases`, which pins MudBlazor 9.9.0 itself, so the pin alone is a downgrade, NU1605, and neither
host builds (`build.log` of the first attempt, not kept). This run pinned `samples/ExChart.Showcases/ExChart.Showcases.csproj`
to 9.6.0 as well, with the same `sed`, and restored both afterwards. Both hosts then built from clean with 0 errors,
and the version each one loaded was read back (`mudblazor-versions.txt`): WebAssembly `9.6.0.0`, Server `9.6.0.0`.
Nothing was decided: the Definition of Done's command still names the one project.

The command was `npx playwright test --grep "$G"`, with `G` exactly as the Definition of Done writes it, on ports of
the run's own (`EXGRID_BASE_URL` `http://localhost:5338` and `http://localhost:5337`). It ran every project: chrome,
msedge, and chrome-150's Wrapper tests. Each test ran once, and none was run again.

| Host | Log | Passed | Failed | Skipped |
|---|---|---|---|---|
| WebAssembly | `mudblazor-floor.log` | 915 | 0 | 28 |
| Server | `mudblazor-floor-server.log` | 941 | 0 | 2 |

WebAssembly ran 453 tests on chrome, 453 on msedge and 9 on chrome-150; the Server host 466, 466 and 9. The 28 skipped
on WebAssembly are the tests that skip themselves off the Server host, which ran there. The 2 skipped on the Server
host are `input-text-order.spec.mjs`'s "typing followed immediately by a macOS Control-click menu command" (mud), on
each browser, which runs on macOS only.

## Layer 2 at every published version of the range

Each Wrapper's layer-2 suite, with MudBlazor pinned in its test project to every stable 9.x from 9.6.0 listed on
nuget.org at the time of the run (`versions-listed.txt`): 9.6.0, 9.7.0, 9.8.0, 9.9.0, 9.10.0 and 9.11.0. Each log
names the commit, and reads back the `MudBlazor.dll` each run loaded.

| Version | ExGrid.MudBlazor.Tests | ExSheet.MudBlazor.Tests | ExPivot.MudBlazor.Tests |
|---|---|---|---|
| 9.6.0 | 329 passed | 106 passed | 92 passed |
| 9.7.0 | 329 passed | 106 passed | 92 passed |
| 9.8.0 | 329 passed | 106 passed | 92 passed |
| 9.9.0 | 329 passed | 106 passed | 92 passed |
| 9.10.0 | 329 passed | 106 passed | 92 passed |
| 9.11.0 | 329 passed | 106 passed | 92 passed |

No test failed or was skipped at any version.

`src/ExChart.MudBlazor/` declares the same range, `[9.6.0,10.0.0)`, and has no layer-2 test project of its own. The
Definition of Done's loop names the three projects above, and this run ran those.

## What holds it

The record holds while `src/*.MudBlazor/` and `samples/ExGrid.DemoPages/` are unchanged from `5e3eecb4` up to the
commit tagged for release (ADR-0205's note of 2026-10-12). A MudBlazor 9.x published after this run asks for the
layer-2 runs at that version only.
