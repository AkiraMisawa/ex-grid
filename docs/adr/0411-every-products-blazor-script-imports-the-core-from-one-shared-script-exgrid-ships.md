# Every product's Blazor script imports the core from one Shared Script that ExGrid ships

*Decided on 2026-10-28 in the project thread "ExPivotのBlazor包みを仕上げる" ("Finish ExPivot's Blazor wrapper"),
which the coordinator gave the job of one core bundle shared by every product's Blazor script, before ExSheet's,
ExChart's and ExBoard's Blazor adapters each bundle a copy of their own.*

This fills in how [ADR-0370](./0370-the-whole-family-moves-to-typescript-and-blazor-only-adapts-it.md) section 1's
Blazor adapters ship the Script Core, and extends
[ADR-0123](./0123-shipped-scripts-and-stylesheets-are-minified-and-each-package-asks-for-the-files-it-was-built-with.md)'s
handshake to a module that imports another.

## Context

On 2026-10-28 (`main` at `4931ac2`):

- **Each product's Blazor module bundled the core whole.** ExGrid's `ex-grid-core.min.js` (487 KB) held the bridge,
  `@ex-grid/core` and `@ex-grid/data`; ExPivot's `ex-pivot.min.js` (899 KB) held its bridge, the pivot, its engine
  and its own copy of the core and the data. A page with both loaded the core twice, and ran two copies of it side by
  side: two of every module-level registry, each grid's state in its own.
- **ExSheet, ExChart and ExBoard are about to add three more copies**, each of whose Blazor threads waits on this.
- **A module's address is chosen by the host.** The .NET component imports its module at the fingerprinted address
  `ComponentBase.Assets` answers, or at the plain one with `?v=` (ADR-0123), so a module cannot name another
  module's fingerprinted address in a static `import`.
- **Every product package depends on exactly the ExGrid it was built with** (ADR-0042), so the ExGrid package that
  stands beside an ExPivot package in an application is the one ExPivot was built against.

## Decision

### 1. The core and the data are bundled once, into the Shared Script

ExGrid's package ships `_content/ExGrid/ex-grid-script.min.js`, the **Shared Script**: `@ex-grid/core` and
`@ex-grid/data`, every export of each, and its own stamp. ExGrid's bridge, `ex-grid-core.min.js`, keeps its name and
its place, and shrinks to the bridge (25 KB). Every product's Blazor module bundles its own packages and imports
`@ex-grid/core` and `@ex-grid/data` from the Shared Script.

### 2. One address, so one module

Every product's module imports the Shared Script by its plain address relative to itself, with the first twelve hex
digits of its stamp, the SHA-256 of its sources, as `?v=`: `./ex-grid-script.min.js?v=…` from ExGrid's bridge,
`../ExGrid/ex-grid-script.min.js?v=…` from any other package's `_content/` folder. All resolve to the same URL under
the application's base, whatever address the host gave the importing module, so the browser holds one Shared Script
for the page, and every product on it runs the same core. `src/npm/scripts/build-blazor.mjs` writes the address; ExGrid's
build writes the Shared Script beside its bridge, and every other product's build reads the same sources for its
stamp, and builds nothing of it. The version is the stamp's and not the content's, unlike ADR-0123's: two builds of
the same sources by another esbuild, in another job, still name one address, so the page still loads one module.

### 3. A module refuses a Shared Script it was not built against

Each product's module carries the stamp of the Shared Script it was built against, and its stamp covers that stamp
with its own sources. Its `stamp()` answers its own stamp only while the Shared Script it was handed answers the one
it expects; otherwise it answers a text that is no stamp, and the component refuses the module as it refuses another
version's, with the notice ADR-0123 shows. So a cache or a CDN that holds an old Shared Script under a new `?v=`
(ADR-0123 measured GitHub Pages' cache ignoring the query) is a notice, never a grid drawn by a core it was not
built against.

### 4. The same for every product that follows

ExSheet's, ExChart's and ExBoard's Blazor modules are built by the same script, and import the core and the data from
the Shared Script the same way. A package of the family that only one product uses (`@ex-grid/pivot-engine`,
`@ex-grid/sheet-engine`) stays in that product's module.

## Consequences

- A page with ExGrid and ExPivot over the Script Core loads 1.06 MB of script where it loaded 1.39 MB, and runs one
  core. Each product added saves the core again.
- A page with ExGrid alone loads 650 KB where it loaded 487 KB: the Shared Script is every export of the core and
  the data, not what ExGrid's bridge uses of them. That is the price of one module every product can import, and it
  falls once a second product is on the page.
- The Shared Script's address is plain, served `no-cache` with an ETag by `MapStaticAssets`, not `immutable`: one
  revalidation per load, where the fingerprinted address took none.
- A Content Security Policy that lists the package's files names `ex-grid-script.min.js` too.
- `ShippedAssetTests` holds ExGrid's bridge to the Shared Script shipped beside it, ExPivot's `ShippedFilesTests`
  holds its module to the same address and version, and the package smoke check holds the pack to shipping it.
