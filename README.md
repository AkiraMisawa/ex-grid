# ExGrid

[![CI](https://github.com/AkiraMisawa/ex-grid/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/AkiraMisawa/ex-grid/actions/workflows/ci.yml)
[![Line coverage](https://github.com/AkiraMisawa/ex-grid/raw/badges/coverage-line.svg)](https://github.com/AkiraMisawa/ex-grid/blob/badges/coverage.md)
[![Branch coverage](https://github.com/AkiraMisawa/ex-grid/raw/badges/coverage-branch.svg)](https://github.com/AkiraMisawa/ex-grid/blob/badges/coverage.md)

An Excel-like grid component for Blazor.

Two products share this repository and ship as separate packages
([ADR-0019](docs/adr/0019-one-repository-many-packages.md)):

- **ExGrid** — display-oriented. Fully specified; this is what gets built first.
- **ExSheet** — edit-oriented. Later.

**Current status: the specification is settled; implementation is underway** — the
pure-logic core and the component layer exist, virtualised on both axes, with pinned
columns, selection, the keyboard (including entering a cell), the Cell Editor and the
clipboard. What is left is recorded in
[`docs/implementation-status.md`](docs/implementation-status.md). The specification lives
in [`docs/adr/`](docs/adr/) (42 decision records) and the domain glossary in
[`CONTEXT.md`](CONTEXT.md).

## Using the packages

Prereleases of **ExGrid** and **ExGrid.MudBlazor** are published to NuGet from tags, at one
shared `0.1.0-beta.N` version
([ADR-0042](docs/adr/0042-prereleases-ship-before-sign-off-and-only-a-stable-version-waits-for-it.md)):

```sh
dotnet add package ExGrid --prerelease
dotnet add package ExGrid.MudBlazor --prerelease   # for a MudBlazor application
```

Setup and a first grid are in each package's readme:
[`src/ExGrid/README.md`](src/ExGrid/README.md) and
[`src/ExGrid.MudBlazor/README.md`](src/ExGrid.MudBlazor/README.md). A beta has passed every
test layer in CI. The Definition of Done's sign-off is what a stable version waits for.

## Building the repository

The toolchain is the **.NET 10 SDK** (version pinned via [`global.json`](global.json)).
You can get it through Nix or install it yourself — both are supported.

The **shipped packages target `net8.0`**, so consuming applications need .NET 8 or newer
([ADR-0022](docs/adr/0022-packages-target-net8-and-run-on-everything-newer.md)); the .NET 10
SDK builds that target just fine.

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
| [`tests/`](tests/) | The gating test layers — `ExGrid.Tests` (xUnit), `ExGrid.Components` and `ExGrid.MudBlazor.Tests` (bUnit), `ExGrid.Browser` (Playwright) — and `ExGrid.PackageSmoke`, the packages taken as a Consumer takes them |
| [`.github/workflows/`](.github/workflows/) | CI (`ci.yml`) and the prerelease publish (`release.yml`) |
| [`samples/ExGrid.DemoHost/`](samples/ExGrid.DemoHost/) | Runnable Consumer for manual verification; the browser layer's fixture. Not shipped |
| [`spikes/render-bench/`](spikes/render-bench/) | Disposable render-cost measurement harness (see its README) |
| [`AGENTS.md`](AGENTS.md) | Working rules for AI agents; useful reading for humans too |

## Ground rules

The two rules that override convenience (details in [`AGENTS.md`](AGENTS.md)):

1. **Everything committed to this repository is written in English** — documents, code,
   comments, commit messages, test names, UI strings.
2. **JavaScript is allowlisted, not "minimised"** — five permitted uses (capture-phase
   `keydown`, scroll offsets, the clipboard, a `ResizeObserver` reporting the Scrollbar
   Gutter, and a pointer report for the hover band); anything else needs a new ADR
   ([ADR-0021](docs/adr/0021-javascript-is-allowlisted-not-minimised.md)).

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
badges above follow `main` (history in `history.csv` on the `badges` branch). Windows (VZ-14)
and a real IME remain runs by hand. A fourth job packs both packages and publishes an
application that takes them from the packed files alone
([`tests/ExGrid.PackageSmoke`](tests/ExGrid.PackageSmoke/check.sh)).

Test names carry the ADR number they enforce, so a failure says which decision was
violated.

## License

[MIT](LICENSE).
