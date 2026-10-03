# Shipped scripts and stylesheets are minified, and their freshness is the Consumer's

*(Decided with the user on 2026-10-03.)*

Six packages serve static assets: ExGrid serves `ex-grid.js` and `ex-grid.css`, and each other UI
package serves one stylesheet. Each was served as written. ExGrid's script carries the reasons for
its code in comments, because the reasons are what a reader needs (ADR-0021). That made it 163 KB
as served, 45 KB under gzip. Measured on 2026-10-03, a minified build of the same file is 29 KB, and
9 KB under gzip. The stylesheet goes from 21 KB to 4.5 KB under gzip.

## The decision

- **What ships is minified, with a source map.** The sources move to `src/<Package>/Assets/`, which
  is not packed. `tools/assets/minify.mjs` builds `wwwroot/<name>.min.js` and
  `wwwroot/<name>.min.css` with esbuild, at a pinned version, and writes a linked source map
  beside each one. The map carries the source, so a browser's debugger shows the code as written,
  comments included.
- **The minified files are committed, so a build needs no Node.** CI builds with the .NET SDK
  alone, and so does a Consumer who builds from source. A contributor who edits a source runs the
  script and commits what it writes.
- **Each minified file names the SHA-256 of its source.** `ShippedAssetTests` compares that hash
  with the source. A source edited without building it again fails layer 2 by name. The test also
  checks that a package serves nothing but minified files and their maps, and the package check
  (`tests/ExGrid.PackageSmoke/check.sh`) checks it on the packed `.nupkg`.
- **The names say what they are,** as MudBlazor's and AG Grid's do: `_content/ExGrid/ex-grid.min.js`,
  `_content/ExGrid/ex-grid.min.css`, and so on. The packages are still prereleases, so the rename
  reaches no released Consumer.
- **Rules about how the code is written are checked on the sources.** The layer-2 tests that read
  the script or the stylesheets (ADR-0021's allowlist, the shipped stylesheet's rules) read
  `Assets/`. Layer 3 runs against what ships.

## Freshness is the Consumer's

A browser that keeps an old copy of `ex-grid.min.js` beside a newer `ExGrid.dll` runs a script that
does not match the component. Whether that can happen depends on how the application serves static
files. That is the application's own choice, and the platform has the answer: .NET's static assets
(`MapStaticAssets`) fingerprint every file, serve a fingerprinted URL as immutable, and revalidate a
plain one. The packages do not add a mechanism of their own. MudBlazor and AG Grid do not either.

The READMEs show the references that get the fingerprint:

- In a Blazor Web App, `@Assets["_content/ExGrid/ex-grid.min.css"]`, and `<ImportMap />`, through
  which the grid's `import("./_content/ExGrid/ex-grid.min.js")` resolves to the fingerprinted file.
- In a standalone WebAssembly app, `_content/ExGrid/ex-grid.min#[.{fingerprint}].css` in
  `index.html`, with `OverrideHtmlAssetPlaceholders`, and the same import map.

## Considered options

- **Minify at build time, in MSBuild.** Rejected: every build, CI's and a Consumer's from source,
  would need Node or esbuild. Committing the output and checking it by hash keeps the build .NET-only.
- **A .NET minifier (NUglify).** Rejected: its JavaScript parser does not follow the modern syntax
  the script uses, such as `?.` and `??`. A minifier that misreads the script would ship a script
  that differs from the one tested.
- **Serve the sources as well.** Rejected: the source map already carries them, and a second copy
  invites a Consumer to reference the large one.
- **A version handshake between the DLL and the script.** Rejected with the user: freshness is the
  application's to configure, and the platform already provides it.
