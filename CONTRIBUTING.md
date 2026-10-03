# Contributing to ExGrid

This is the working guide for the repository: how to build it, how it is laid out, the rules
that override convenience, and the test layers. What the packages do and how to use them is in
the [README](README.md).

The specification lives in [`docs/adr/`](docs/adr/), one decision record per decision with its
reasons, and the vocabulary in [`CONTEXT.md`](CONTEXT.md). Where code disagrees with an ADR, the
ADR wins. [`AGENTS.md`](AGENTS.md) holds the working rules in full; it is written for AI agents and
is useful reading for humans too.

## Building the repository

The toolchain is the **.NET 10 SDK** (version pinned via [`global.json`](global.json)).
You can get it through Nix or install it yourself — both are supported.

The **shipped packages target `net10.0`**, so consuming applications need .NET 10 or newer
([ADR-0022](docs/adr/0022-packages-target-net10-and-run-on-everything-newer.md)); the SDK and
the target are the same .NET 10 today.

### Option A — Nix (reproducible toolchain)

The flake provides the exact SDK version. With [direnv](https://direnv.net/) the dev
shell loads automatically when you `cd` into the repository:

```sh
direnv allow   # once, after cloning
dotnet --version
```

Without direnv, prefix commands instead:

```sh
nix develop -c dotnet build
nix develop -c dotnet test
```

A second shell provides a headless Chromium and Node.js for the render spike:

```sh
nix develop .#browser -c node ...
```

To see the component running, start the demo host and open <http://localhost:5299>:

```sh
nix develop -c dotnet run --project samples/ExGrid.DemoHost
```

Stop it with `kill $(lsof -ti tcp:5299)` — not `pkill -f`, which matches the calling
shell's own command line and kills it.

The same pages run under Blazor Server from the second host, on <http://localhost:5298>:

```sh
nix develop -c dotnet run --project samples/ExGrid.DemoHost.Server
```

The pages that read a database call the demo API server, which each page finds at its own
port plus 3000: <http://localhost:8299> beside the WebAssembly host, 8298 beside the Server
host ([ADR-0069](docs/adr/0069-the-demo-pages-call-a-demo-api-server-both-hosts-share.md)).
Its first start generates a million trades into a SQLite file outside the repository;
`EXGRID_DEMO_TRADES` asks for another count:

```sh
nix develop -c dotnet run --project samples/ExGrid.DemoApi --urls http://localhost:8299
```

> **Nix gotcha:** flakes only see git-tracked files. `git add` any new file before
> building (committing is not required), or the build will not see it.

### Option B — without Nix

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) yourself;
`global.json` accepts any 10.0.x SDK at feature band 100 or later. Then the usual:

```sh
dotnet build
dotnet test
```

For the browser test layer and the render spike you additionally need **Node.js** and a
**Chromium-based browser** (Chromium browsers are the only target — 
[ADR-0017](docs/adr/0017-target-chromium-browsers-only.md)).

The `.envrc` is harmless without Nix: it detects the missing `nix` command and leaves
your environment alone.

## Repository layout

| Path | Contents |
|---|---|
| [`CONTEXT.md`](CONTEXT.md) | Domain glossary. The vocabulary used everywhere; read it first |
| [`docs/adr/`](docs/adr/) | Architecture decision records — the specification and the reasons behind it |
| [`src/ExGrid/`](src/ExGrid/) | The ExGrid package: pure-logic core and the Blazor components |
| [`src/ExGrid.MudBlazor/`](src/ExGrid.MudBlazor/) | The ExGrid.MudBlazor package: the Wrapper for MudBlazor applications |
| [`src/ExSheet.Engine/`](src/ExSheet.Engine/), [`src/ExSheet/`](src/ExSheet/), [`src/ExSheet.MudBlazor/`](src/ExSheet.MudBlazor/) | ExSheet: its Formula engine, the Sheet ExGrid draws, and its MudBlazor Wrapper |
| [`src/ExGrid.Data/`](src/ExGrid.Data/), [`src/ExGrid.Data.Arrow/`](src/ExGrid.Data.Arrow/) | The Snapshot, its loaders and Change Batches; and its Apache Arrow reader and writer |
| [`src/ExPivot.Engine/`](src/ExPivot.Engine/), [`src/ExPivot/`](src/ExPivot/), [`src/ExPivot.MudBlazor/`](src/ExPivot.MudBlazor/) | ExPivot: the pivot engine and the Pivot Source, the component, and its MudBlazor Wrapper |
| [`tests/`](tests/) | The gating test layers — xUnit for each package's logic (`ExGrid.Tests`, `ExSheet.Engine.Tests`, `ExPivot.Engine.Tests`, `ExGrid.Data.Tests`, `ExGrid.Data.Arrow.Tests`, and `ExGrid.DemoApi.Tests` for the demo server), bUnit for the components (`ExGrid.Components`, `ExGrid.MudBlazor.Tests`, `ExSheet.Components`, `ExSheet.MudBlazor.Tests`, `ExPivot.Components`, `ExPivot.MudBlazor.Tests`), `ExGrid.Browser` (Playwright) — and `ExGrid.PackageSmoke`, the packages taken as a Consumer takes them |
| [`.github/workflows/`](.github/workflows/) | CI (`ci.yml`) and the prerelease publish (`release.yml`) |
| [`samples/ExGrid.DemoPages/`](samples/ExGrid.DemoPages/) | The demo pages both hosts serve, and the browser layer's fixture. Not shipped |
| [`samples/ExGrid.DemoHost/`](samples/ExGrid.DemoHost/) | The standalone WebAssembly host for those pages — the default. Not shipped |
| [`samples/ExGrid.DemoHost.Server/`](samples/ExGrid.DemoHost.Server/) | The Blazor Server host for the same pages (`InteractiveServer`, prerendered). Not shipped |
| [`samples/ExGrid.DemoApi/`](samples/ExGrid.DemoApi/) | The demo API server both hosts' pages call: SQLite, Arrow, a Pivot Source answered in SQL, and live changes over SignalR. A Consumer's server, not shipped |
| [`spikes/render-bench/`](spikes/render-bench/) | Disposable render-cost measurement harness (see its README) |
| [`AGENTS.md`](AGENTS.md) | Working rules for AI agents; useful reading for humans too |

## Ground rules

The two rules that override convenience (details in [`AGENTS.md`](AGENTS.md)):

1. **Everything committed to this repository is written in English** — documents, code,
   comments, commit messages, test names, UI strings.
2. **JavaScript is allowlisted, not "minimised"** — used only where Blazor genuinely cannot
   do the job, or where a recorded measurement shows the Blazor-side approach is too slow;
   the permitted uses are listed in [`AGENTS.md`](AGENTS.md), and anything else needs a new
   ADR ([ADR-0021](docs/adr/0021-javascript-is-allowlisted-not-minimised.md)).

Before changing behaviour, read the relevant ADR — the reasons are written down, and
changing something without knowing the reason usually walks back into an option that was
already rejected. When a decision does change, rewrite the ADR rather than letting the
implementation drift away from it.

## Tests

Three layers, all gating; performance never gates (it swings with the environment —
watch the trend in `spikes/render-bench/results/` instead):

| Layer | Tool | Covers |
|---|---|---|
| 1. Pure logic | xUnit | Selection arithmetic, navigation, paste/copy rules, overflow, widths |
| 2. Component | bUnit (no browser) | Which rows render; whether row memoisation actually skips |
| 3. Browser | Playwright, on the installed Chrome and Edge | Capture-phase keys, clipboard, popovers, scrollbars, multi-instance independence |

**CI** ([`.github/workflows/ci.yml`](.github/workflows/ci.yml),
[ADR-0041](docs/adr/0041-ci-runs-every-layer-and-layer-three-gates-on-linux.md)) runs all
three on every push to `main` and on every pull request. Layer 3 runs
on Linux, headed under xvfb, on the runner's installed Chrome and Edge. The soak and ST-1 at
10⁶ rows run weekly, or on demand from the Actions tab. Coverage counts the shipped
assemblies only and is reported, never gated: each run's summary carries the table, and the
badges on the README follow `main` (history in `history.csv` on the `badges` branch). Windows (VZ-14)
and a real IME remain runs by hand. A fourth job packs the packages — ExGrid's two
into the release feed, and ExSheet's, ExPivot's and the data packages into feeds of their own —
and publishes an application that takes them from the packed files alone
([`tests/ExGrid.PackageSmoke`](tests/ExGrid.PackageSmoke/check.sh)).

Test names carry the ADR number they enforce, so a failure says which decision was
violated.
