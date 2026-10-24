# The npm workspace

ExGrid's Script Core and its Framework Adapters, published to npm under the `@ex-grid` scope
([ADR-0350](../../docs/adr/0350-exgrids-core-moves-to-typescript-and-react-and-blazor-both-take-it.md)).
One directory per package:

| Directory | Package | What it is |
|---|---|---|
| `core/` | `@ex-grid/core` | The Script Core: the grid's state and what each input means, free of any UI framework |

`@ex-grid/react` and `@ex-grid/mui` join it as they are built (ADR-0350, sections 3 and 11).

## Commands

Node comes from the flake's `browser` shell, as it does for `tools/assets`:

```sh
nix develop .#browser -c bash -c 'cd src/npm && npm ci'
nix develop .#browser -c bash -c 'cd src/npm && npm run typecheck'   # the sources and the tests
nix develop .#browser -c bash -c 'cd src/npm && npm run build'       # tsc, esbuild, API Extractor
nix develop .#browser -c bash -c 'cd src/npm && npm test'            # Vitest: layers 1 and 2
```

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
  A case is green on the C# core before the Script Core takes it.
- **`dist/` and `temp/` are written by the build and not committed.** A package is built when it is
  published.
