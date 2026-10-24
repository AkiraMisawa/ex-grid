# ExGrid's core moves to TypeScript; React takes it first, and Blazor follows if it holds

*Decided with the user, 2026-10-24, for 0.3.0. It began from the user's question of what it would take
to use ExGrid from React. The user chose the recommended strategy of three, the hybrid below, and set
the order: the core in TypeScript and React in 0.3.0, Blazor's ExGrid on the same core once it holds.
Sections 9 to 13 answer the five questions the first draft left open, decided with the user the same
day.*

## Context

ExGrid is usable only from Blazor. A React developer cannot write what a Consumer writes today: an
`IGridSource<TRow>`, a `GridColumn` whose value is a `Func<TRow, object?>` and whose template is a
`RenderFragment`, a Chrome built of Razor components.

Measured on `main` at 5799246, the code that depends on Blazor is narrower than the family:

| Layer | Projects | Lines | Depends on Blazor |
|---|---|---|---|
| Engines and data | ExSheet.Engine, ExPivot.Engine, ExGrid.Data, ExChart.Engine, the Xlsx and Arrow packages | ~61,700 C# | no (`Microsoft.NET.Sdk`, no Razor) |
| ExGrid's pure logic | Selection, Clipboard, Columns, Keys, Rows, Filtering and the rest outside `Components/` | ~20,600 C# | barely; a few public types carry `RenderFragment` or a `Func` |
| ExGrid's component | `ExGrid.razor` (11,067, of which ~10,000 are `@code`), its partials (11,704), `ExGridRow.razor` | ~23,500 | wholly |
| Browser script | `ex-grid.js` | 6,169 | built around the round trip to .NET |
| Appearance | `ex-grid.css`, 250 `--ex-` tokens | 1,362 | no |

Much of the component and the script exists to make a split between the browser and .NET
deterministic: the JavaScript allowlist ([ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md)),
the Input Order with its held and replayed input
([ADR-0240](./0240-input-to-one-product-instance-is-taken-in-one-order.md)), the Settled Stamp. On Blazor
Server every input crosses a circuit. In a React application every input is handled on the browser's
one thread, where that split does not exist.

React has no counterpart of Blazor Server. Server Components render on the server, but anything that
answers a key or a click runs in the browser and holds its state there. A grid for React is therefore a
grid whose core runs in the browser.

## Options considered

| | A. Embed the Blazor component | B. Rewrite everything in TypeScript | C. Rewrite ExGrid's core in TypeScript, keep the engines in C# (chosen) |
|---|---|---|---|
| How | Register the Blazor WebAssembly component as a custom element and place `<ex-grid>` in React | Port the core, the products and the engines | Port ExGrid's component, script and pure logic; run the engines on a server over Arrow or in the browser as .NET WebAssembly |
| What a React developer gets | A .NET runtime to download and start; no cell template or Chrome written in React | The most natural package | A plain TypeScript package for ExGrid; ExSheet alone would carry a WebAssembly engine |
| Cost | Small | Largest: ~145,000 lines, and the formula engine re-proved against Excel ([ADR-0047](./0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md)) | ~44,000 lines of component and logic, much of the round-trip machinery not needed |
| Behaviour | Identical, the same code | Held by the ADRs and the tests | UI held by the ADRs and layer 3; every computed value from the same engine |

A was refused because its download and its missing React templates are the adoption barrier the work
exists to remove. B was refused because it re-proves the engines first, where the risk is highest and
the gain smallest: a grid's speed is decided by its DOM, not by its arithmetic.

## Decision

### 1. 0.3.0 ships ExGrid's core in TypeScript, and a React package over it

The **Script Core** is ExGrid's core written in TypeScript. It holds the instance's state and decides
what each input means: the Selection, the Focus and the Extent, the keys, the Cell Editor's modes, the
copy and paste rules, the overflow decision, Auto width, the Range Requests. It is the C# core's
behaviour as the ADRs state it, moved, not redesigned. It depends on no UI framework.

0.3.0 covers ExGrid only. ExSheet, ExPivot, ExChart, ExBoard and the MudBlazor Wrappers stay on Blazor
and on the C# core in 0.3.0. Which of them follow, and how, is a later decision.

### 2. The Script Core owns the DOM inside the instance root

The Script Core creates and updates the instance's elements itself. A row is plain markup and the row
is the only unit that is skipped or repainted
([ADR-0003](./0003-cells-are-plain-markup-by-default-not-components.md)). No framework renders rows: a
React component per row would repeat, through `memo`, the fight with `ShouldRender` that ADR-0003
measured, once per framework.

P1 to P9 of [ADR-0027](./0027-appearance-travels-in-css-geometry-travels-in-csharp.md) hold for the
Script Core as written, with "in .NET" read as "in the Script Core". The Definition of Done's criteria
that cite them are run against it.

### 3. A Framework Adapter is thin, and the first is React's

A **Framework Adapter** puts the Script Core into one framework's component model. React's is first.
It mounts the core on an element it renders, hands the core its props, hands the core's events to the
framework's callbacks, and unmounts it. It holds no state of the grid's, draws nothing inside the
instance root, and decides nothing.

React and Blazor are the only frameworks the family serves (section 10). The Script Core stays free of
both, so that the boundary between core and adapter stays honest, not so that a third adapter is built.

A Chrome seam ([ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md)) and a Template cell
([ADR-0020](./0020-action-and-template-columns.md)) become a frame the core owns and a slot the adapter
fills: React fills it through a portal. The core still owns where the frame appears and what its input
means; the Consumer's component only renders and calls back.

### 4. The DOM contract and the stylesheet are shared, so layer 3 judges both cores

The Script Core writes the same DOM contract as the C# core: the `ex-` classes, the `data-ex-*`
attributes, the accessibility surface ([ADR-0033](./0033-the-accessibility-surface-is-owned-by-the-root-not-by-cells.md)),
the geometry emitted inline from the Grid Metrics
([ADR-0028](./0028-geometry-is-resolved-once-density-is-only-a-preset.md)), and the Settled Mark
([ADR-0220](./0220-a-test-reads-once-the-grid-says-it-has-settled-and-a-red-is-never-settled-by-a-re-run.md)).
It reads `ex-grid.css` and its Visual Tokens unchanged.

Layer 3 runs the ExGrid specs against a React host as it runs them against the Server and WebAssembly
hosts today. A spec that needs to know which host it is on says why. The React host is a third DemoHost,
and the same page catalogue is served by all three.

### 5. While two cores exist, their pure logic is held to the same cases

A layer-1 case that both cores implement (selection arithmetic, paste shape, copy refusal, overflow,
Auto width, Enter and Tab cycling) is written once as data, its input and its expected result, and run by
xUnit and by the Script Core's test runner. A case written only in one suite is a defect while both
cores ship.

### 6. The Script Core is not under ADR-0021's allowlist; what the allowlist protects still holds

ADR-0021 governs JavaScript beside a Blazor core, and keeps governing the Blazor packages unchanged.
The Script Core is JavaScript, so an allowlist of its uses means nothing. What the allowlist protects
holds in it: no layout read on the path to a paint (P4), nothing per cell bound to a listener, every
listener scoped to the instance root ([ADR-0018](./0018-multiple-instances-must-be-independent.md)),
and the grid told when the browser knows something has changed, never measuring it. The Input Order's
rules that do not come from the circuit still hold: input is taken one at a time in the order it was
made, nothing waits on a clock (its sections 1, 3 and 4), and where a Consumer answers later than the
input, a gesture carries what it was taken against and is refused if that no longer holds (spine rule
6). The machinery that held and replayed input across a round trip is not ported.

### 7. Blazor's ExGrid moves onto the Script Core only after it holds

0.3.0 does not change Blazor's ExGrid. Moving it onto the Script Core is decided by a later ADR, once:

- the ExGrid layer-3 specs pass on the React host, in both browsers;
- the Definition of Done's ExGrid criteria pass against the Script Core;
- a spike runs Blazor's ExGrid on the Script Core on both Blazor hosts, measures what crosses the
  circuit on Server, and shows whether ExSheet and ExPivot, which are ExGrid's Consumers, can be drawn
  by it.

Moving would put the grid's decisions in the browser on Blazor Server too, which rewrites ADR-0021's
premise and the Input Order's. That is why it waits for its own ADR.

### 8. The engines stay in C#

ExGrid.Data, ExPivot.Engine and ExSheet.Engine are not ported. A React application reaches them on a
server, through the Arrow Snapshot ([ADR-0065](./0065-a-snapshot-travels-as-apache-arrow.md)) and the
server pivot's API ([ADR-0151](./0151-server-pivots-send-report-windows-and-share-the-local-engine.md)),
or in the browser as .NET WebAssembly. Whether one is later ported to TypeScript or Rust is decided on a
recorded measurement of the browser case, not before (CLAUDE.md, "Measure before claiming anything about
performance").

### 9. The npm packages are `@exgrid/core`, `@exgrid/react` and `@exgrid/mui`

npm has no rule for how a scope is spelled; a scope is simply the name of the npm organisation that
owns it. The convention is the brand as people type it, in lower case: one word stays one word
(`@tanstack`, `@mui`), a hyphenated brand keeps its hyphen (`ag-grid-community`). The brand and the
NuGet identifiers are `ExGrid`, so the scope is `@exgrid`. The unscoped name `exgrid` belongs to an
unrelated grid on npm (checked 2026-10-24), which a scope keeps apart. The organisation `exgrid` is
created on npm by the user before the first publish.

The later products, if they follow (section 1), take names under the same scope.

### 10. React and Blazor only

No adapter for Vue, Angular or any other framework is built, and no custom element is published.
Section 3's custom element is withdrawn: it existed for frameworks the family does not serve.

### 11. The MUI Wrapper ships in 0.3.0

`@exgrid/mui` is React's counterpart of ExGrid.MudBlazor, under [ADR-0030](./0030-what-a-design-system-wrapper-owns-and-what-it-may-not-touch.md)'s limits as written: it maps
MUI's palette to a Theme and its density words onto Density, supplies Cell Metrics for its font and
fills the Chrome seams with MUI's components. It owns no geometry, no DOM inside the instance root and no
state (P9).

### 12. The Docs Site switches between frameworks

The Docs Site gains a **framework switch**, Blazor or React, chosen once for the whole site and
remembered, as ag-grid.com does. Each ExGrid Example exists in both and shows the code of the one
chosen; the existing Built-in / MudBlazor switch becomes Built-in / MUI under React. A page whose feature
is not in the React packages yet says so where its Example would be, rather than showing Blazor's in its
place.

The site stays the Blazor WebAssembly app of
[ADR-0110](./0110-the-docs-site-is-a-webassembly-app-on-github-pages-and-its-examples-show-the-code-they-run.md).
A React Example is built ahead of time into the site's static files and shown in a frame of its own,
the way ag-grid.com shows each Example, so React never runs inside the Blazor app and the site's own
JavaScript does not grow. The site's build fails when a React Example does not build, as it fails today
for a Blazor one.

### 13. Where the Script Core lives, and how it is tested

- **Source.** An npm workspace at `src/npm/`, with a package per directory: `src/npm/core`,
  `src/npm/react`, `src/npm/mui`. Shipped code stays under `src/`, as it does for NuGet. TypeScript is
  compiled with `tsc` for the declarations and bundled with esbuild, which `tools/assets` already uses.
- **The public surface.** API Extractor writes each package's surface to a report file kept in the
  repository, and the build fails on an export that is not in it or a member without a doc comment:
  the counterpart of `PublicAPI.Unshipped.txt` and the XML doc rule
  ([ADR-0201](./0201-the-public-surface-is-what-consumers-chromes-and-the-sibling-products-use.md)).
- **Layer 1.** Vitest in Node, for the Script Core's pure logic. The cases shared with xUnit
  (section 5) live as JSON under `tests/cases/`, read by both.
- **Layer 2.** Vitest on a DOM emulation: which rows the core creates, and that a row whose values did
  not change is not touched, counted as bUnit counts renders today. The React adapter's own tests use
  React Testing Library.
- **Layer 3.** The existing Playwright suite, with a third host, `samples/ExGrid.DemoHost.React`, built
  with Vite and serving the same page catalogue (section 4).
- **The toolchain** stays nix's: the flake's `browser` shell already carries Node, and the Script Core's
  build and tests run there, as `tools/assets` does. CI gains the npm
  build, the two Vitest layers and the React host's layer 3.

## Consequences

- ExGrid has two implementations from 0.3.0 until the later ADR of section 7. Section 5 and the shared
  layer 3 are what keep them from drifting.
- The family publishes to npm as well as NuGet. The release workflow
  ([ADR-0042](./0042-prereleases-ship-before-sign-off-and-only-a-stable-version-waits-for-it.md),
  [ADR-0205](./0205-0-1-0-is-grown-on-a-release-branch-rehearsed-by-release-candidates-and-signed-off-before-its-tag.md))
  gains a second registry.
- The Docs Site needs React Examples for ExGrid behind a framework switch (section 12).
- The toolchain gains a TypeScript build and a TypeScript test runner (section 13).
- `ADR-0122`'s time zone report is not needed in the Script Core: the browser's zone is read directly.
- [ADR-0017](./0017-target-chromium-browsers-only.md) (Chromium only) and
  [ADR-0045](./0045-exgrid-is-a-desktop-grid.md) (desktop and touch) apply to the Script Core as written.
