# ExGrid

An Excel-like grid component for Blazor.

Two products share this repository and ship as separate packages
([ADR-0019](docs/adr/0019-one-repository-many-packages.md)):

- **ExGrid** — display-oriented. Fully specified; this is what gets built first.
- **ExSheet** — edit-oriented. Later.

**Current status: the specification is settled; there is no implementation yet.**
The specification lives in [`docs/adr/`](docs/adr/) (22 decision records) and the
domain glossary in [`CONTEXT.md`](CONTEXT.md).

## Getting started

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
| [`spikes/render-bench/`](spikes/render-bench/) | Disposable render-cost measurement harness (see its README) |
| [`AGENTS.md`](AGENTS.md) | Working rules for AI agents; useful reading for humans too |

## Ground rules

The two rules that override convenience (details in [`AGENTS.md`](AGENTS.md)):

1. **Everything committed to this repository is written in English** — documents, code,
   comments, commit messages, test names, UI strings.
2. **JavaScript is allowlisted, not "minimised"** — three permitted uses (capture-phase
   `keydown`, scroll offsets, clipboard); anything else needs a new ADR
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
| 3. Browser | CDP driver | Capture-phase keys, clipboard, popovers, multi-instance independence |

Test names carry the ADR number they enforce, so a failure says which decision was
violated.
