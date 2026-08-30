# ExGrid and ExSheet live in one repository; only the packages are separate

`ExGrid` (display-oriented), `ExSheet` (edit-oriented) and the integration packages are developed
in **the same repository**. **The NuGet packages are separate** — repository structure and package
structure are different questions.

```
ex-grid/                      ← one repository
├── CONTEXT.md                ← one glossary
├── AGENTS.md
├── docs/adr/                 ← one ADR sequence
├── src/
│   ├── ExGrid/               → NuGet: ExGrid            (no dependencies)
│   ├── ExGrid.MudBlazor/     → NuGet: ExGrid.MudBlazor  (a Chrome implementation)
│   ├── ExGrid.Fluxor/        → NuGet: ExGrid.Fluxor     (push interface ↔ store)
│   └── ExSheet/              → NuGet: ExSheet           (future)
├── tests/
│   ├── ExGrid.Tests/         ← pure logic (xUnit)
│   ├── ExGrid.Components/    ← component (bUnit)
│   └── ExGrid.Browser/       ← browser (CDP driver)
└── spikes/render-bench/      ← render-cost measurement. disposable
```

A Consumer that only wants the grid references only `ExGrid`. **Splitting the repository is not
needed for that.**

## Reasons

**Far more is shared than differs.** Early in the design, three options were left open — a shared
kernel, two entirely separate products, or one product with the other as an option. The
measurements and decisions since then answered it.

```
shared     Viewport, virtualisation, row memoisation, Column, Selection, Anchor/Focus,
           keyboard behaviour, clipboard, Row Identity, the Chrome seams
           in ADR terms: 0002–0006, 0008–0014, 0016, 0018, 0020 — nearly all of them

differs    data ownership (ADR-0001) and the formula engine
```

**The glossary and the ADRs already cover both.** `CONTEXT.md` defines ExGrid and ExSheet side by
side, and row memoisation and the selection model apply to ExSheet unchanged. Splitting the
repository would mean **splitting or duplicating the glossary and the ADRs**, and both degrade.

**There is no external consumer yet.** The advantage of separate repositories is independent
release cadence, and nothing is forcing that. What would come first instead is the cost of
publishing a package on every core change and making ExSheet follow it.

## Is ExSheet a sibling of ExGrid, or a Consumer of it? — open

[ADR-0007](./0007-edits-are-an-overlay-owned-by-the-consumer.md) established that the grid only
reports an Edit Intent, and the Consumer owns the committed state and pushes back a Window with
the Overlay applied. **ExSheet is precisely "a Consumer that owns a mutable cell model".**

```
ExSheet (owns the cell model and the formula engine)
   ↓ pushes a Window / receives Edit Intents
ExGrid (painting, selection, keyboard)
```

If that holds, there is no need to extract a shared kernel into a separate package at all. **This
is another place the push interface pays off.** And the always-visible focused-cell value added in
[ADR-0016](./0016-column-width-and-overflow.md) is a formula bar as it stands.

There is friction. Inserting a row in ExSheet bumps the `RowSequenceVersion`, so **the selection
would be cleared on every row insertion**
([ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)), which
may be too aggressive for a sheet.

**Do not decide now.** Building ExGrid will make what ExSheet additionally needs concrete.

## Consequences

- **One ADR sequence.** Decisions specific to ExSheet go into the same `docs/adr/`. Most decisions
  affect both, and splitting them would produce a mesh of cross-references.
- **CI verifies everything in one run.** When the core changes, ExSheet and the integration
  packages build at the same time. Across repositories the breakage would be noticed later.
- **`ExGrid` has no dependencies.** MudBlazor and Fluxor stay inside the integration packages
  ([ADR-0017](./0017-target-chromium-browsers-only.md) /
  [ADR-0018](./0018-multiple-instances-must-be-independent.md) /
  [ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md)). Sharing a repository and mixing
  dependencies are different things. **Enforce it structurally through project reference
  direction.**
- **The repository name stays `ex-grid`.** When ExSheet is actually built, whether an umbrella
  name is wanted can be reconsidered then. No single umbrella noun is invented now.
