# The npm workspace

ExGrid's Script Core and its Framework Adapters, published to npm under the `@ex-grid` scope
([ADR-0350](../../docs/adr/0350-exgrids-core-moves-to-typescript-and-react-and-blazor-both-take-it.md)).
One directory per package:

| Directory | Package | What it is |
|---|---|---|
| `core/` | `@ex-grid/core` | The Script Core: the grid's state and what each input means, free of any UI framework |
| `chart/` | `@ex-grid/chart` | ExChart's layout: scales, ticks, Thinning, each Series Kind's steps and gaps, the scene a chart is drawn from, what its tooltip, its summary and its files say, the fits and interpolations a Consumer hands in, the 3D surface with its slices and mesh, and the Chart Spec's reader, held to the C# engine by the shared cases of `tests/cases/chart/` ([ADR-0370](../../docs/adr/0370-the-whole-family-moves-to-typescript-and-blazor-only-adapts-it.md), section 2) |
| `data/` | `@ex-grid/data` | The Snapshot in TypeScript, read from and written to Apache Arrow as `ExGrid.Data.Arrow` does, on the same cases (ADR-0370, section 3), the family's JSON reader, and a Board Input's declaration |
| `data-xlsx/` | `@ex-grid/data-xlsx` | `.xlsx` read and written as `ExGrid.Data.Xlsx` reads and writes it, with no dependency: the zip through the platform's `DecompressionStream` and `CompressionStream`, the XML pulled a batch of cells at a time, every refusal in the same words, held to it by the shared cases of `tests/cases/xlsx/` (ADR-0330, ADR-0370 section 2) |
| `board/` | `@ex-grid/board` | ExBoard's Script Core: the Board Spec's reader, held to ExBoard's by the shared cases of `tests/cases/board/`, a grid built from an input's shape, a Board bound to Snapshots, its links narrowing the views they reach and its outputs raised by a confirm, its Board Diff, and `createBoard`, which draws it with the family's grids, pivots and charts (ADR-0370, section 3) |
| `board-react/` | `@ex-grid/board-react` | ExBoard's React adapter: `ExBoard`, which mounts `createBoard` in a React tree (ADR-0370, section 1) |
| `sheet-engine/` | `@ex-grid/sheet-engine` | ExSheet's engine in TypeScript: the Workbook and its Sheets, Excel's formulas and functions, number formats, Cell Formats, edits with undo, documents and the typing aids, held to ExSheet.Engine's cases and Excel's recorded answers (ADR-0370, section 2) |
| `sheet-xlsx/` | `@ex-grid/sheet-xlsx` | A Workbook opened from and saved to `.xlsx` as `ExSheet.Xlsx` opens and saves it, over `@ex-grid/data-xlsx`: constants, Formulas, Cell Formats and column widths carried, what would change a Value refused by name, what else the file held listed in the Open Report, and each Linked Table written as an Excel Table, held to it by the shared cases of `tests/cases/sheet-xlsx/` (ADR-0330, ADR-0370 section 2) |
| `blazor/` | `@ex-grid/blazor` (private) | Blazor's ExGrid in the browser: the bridge between `ExGrid.Script.ExGrid` in .NET and the core, bundled with the core into the `ex-grid-core.min.js` the ExGrid NuGet package ships as ExGrid builds; never published to npm (ADR-0350 section 7). Its value text is held to the C# half by the shared cases of `tests/cases/blazor/` |

The design-system Wrappers, `@ex-grid/mantine` first and `@ex-grid/mui`, each with a `sheet-`, `pivot-` and
`chart-` counterpart, join it as they are built (ADR-0350 section 11, ADR-0390).

## Commands

Node comes from the flake's `browser` shell, as it does for `tools/assets`:

```sh
nix develop .#browser -c bash -c 'cd src/npm && npm ci'
nix develop .#browser -c bash -c 'cd src/npm && npm run typecheck'   # the sources and the tests
nix develop .#browser -c bash -c 'cd src/npm && npm run lint'        # oxlint, held to its fixtures first
nix develop .#browser -c bash -c 'cd src/npm && npm run build'       # tsc, esbuild, API Extractor
nix develop .#browser -c bash -c 'cd src/npm && npm test'            # Vitest: layers 1 and 2
```

- **How the code is written, and what the lint refuses, is [CONVENTIONS.md](CONVENTIONS.md).** Above
  all: a missing value or a failure is never replaced by a value nobody chose.
- **A build compiles the declarations with `tsc`, bundles the JavaScript with esbuild, and holds the
  package's public surface to its report**, `<package>/etc/<name>.api.md`, with API Extractor: the
  counterpart of a NuGet package's `PublicAPI.Unshipped.txt` and its XML doc rule
  ([ADR-0201](../../docs/adr/0201-the-public-surface-is-what-consumers-chromes-and-the-sibling-products-use.md)).
  An export that is not in the report, one that is in it and gone, and an exported member without a doc
  comment each fail the build. `npm run api` writes the report instead of comparing it: read its diff,
  because a line you did not mean is a member made public by mistake.
- **What is exported from a package's `src/index.ts` is public, and nothing else is.** A module the
  core uses inside itself is not exported there, however public its own `export` looks.
- **Layer 1's cases are shared with the C# core.** They live as JSON under
  [`tests/cases/`](../../tests/cases/README.md), run by xUnit against the C# core and by Vitest here.
  A case of behaviour the C# core has is green there before the Script Core takes it; a case of
  behaviour new to the Script Core, which the C# core will not take before it is removed, goes in on
  the Script Core alone, read by a kind no C# runner reads (ADR-0350 section 5's note of 2026-10-27).
- **`dist/` and `temp/` are written by the build and not committed.** A package is built when it is
  published.
