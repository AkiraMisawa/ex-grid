# Shipped scripts and stylesheets are minified, and each package asks for the files it was built with

*(Decided with the user on 2026-10-03. Its second half was rewritten with the user on 2026-10-11, and the
title with it. It was "…and their freshness is the Consumer's". What it said, and why that was wrong, is
kept at the end.)*

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

## Each package asks for the files it was built with

A browser that keeps an old copy of `ex-grid.min.js` or `ex-grid.min.css` beside a newer `ExGrid.dll`
runs a script, or paints with a stylesheet, that does not match the component. No user should have
to know that a hard refresh exists. So a normal reload, and a new visit, must always bring the files
the running assemblies were built with. When that cannot be had, the grid says so, rather than look
plausible and be wrong.

What happens today was measured on 2026-10-11:
- **A Blazor Web App on `MapStaticAssets`:** fresh on every load. It serves a plain URL `no-cache` with
  an ETag, and a fingerprinted one `immutable`.
- **A standalone WebAssembly app on GitHub Pages** (`max-age=600`, as the Docs Site is served): a
  normal reload after a deploy ran the new DLL with the old script and the old stylesheet, both from
  the memory cache, with no request.
- **A standalone app on a host that sends no `Cache-Control`** (nginx's default): the same, for as long
  as the browser's heuristic allows. The package file keeps its old modification time, so that can be
  days.
- **A new DLL that needed an export the old script lacked:** the grid never attached. It stayed dimmed
  and busy, and nothing was written to the console.

## How each package asks for its files

- **The grid imports its script by the content it was built with.**
  - When the host knows the file's fingerprinted address (`ComponentBase.Assets`, a Blazor Web App on
    `MapStaticAssets`), the grid imports that address. It is served `immutable`, and the import map is
    no longer needed for the grid's script.
  - Otherwise it imports the plain address with the first twelve hex digits of the shipped file's
    SHA-256 as `?v=`.
  - Either way, a new version is a new address in every hosting model, and the Consumer writes nothing.
- **The version is the content's, not the assembly's.** The SDK gives `0.1.0-beta.3` and `0.1.0-beta.4`
  the same AssemblyVersion. `tools/assets/minify.mjs` writes the hash beside each file it builds,
  `ShippedAssetTests` checks it, and the build still needs no Node.
- **Each UI package renders its own stylesheet link.**
  - The six are ExGrid, ExGrid.MudBlazor, ExSheet, ExSheet.MudBlazor, ExPivot and ExPivot.MudBlazor.
  - Each ships a component, such as `<ExGridStylesheet />`, that renders the `<link>` at the same kind
    of address as the script: the fingerprinted one, or the plain one with `?v=`.
  - A Consumer places these components in the head and no longer writes a `_content/` path. In a Blazor
    Web App they go in `App.razor`'s `<head>`, rendered with the page. In a standalone app they go
    through `<HeadContent>` or as a root component in the head; the package READMEs say which.
  - A standalone app's first visit, and its first load after an upgrade, can paint the grid unstyled
    until the stylesheet arrives. That was measured at about 0.4 s at 300 ms latency, then styled. It
    is the price of an address the package chooses, and it is said where the components are documented.
- **The grid refuses a script that is not its own, and says so.**
  - The module exports the SHA-256 of its source, which its minified header already names. Before it
    attaches anything, the grid compares that stamp with the one it was built with.
  - When the stamp differs, or is missing (a module older than this rule), the grid attaches nothing.
    In place of its rows it shows, in the built-in Chrome's words (ADR-0203) and in its live region
    (ADR-0033), that the page holds another version's script and is to be reloaded.
  - A script that does not load at all is shown the same way, never left busy.
  - A grid that refused its script is neither busy nor a tab stop. It waits for nothing, and nothing
    would hear a key (ADR-0033).
  - The outcome depends on what arrived, never on timing or on a cache: the DLL carries what it was
    built against, and refuses the script when that no longer holds. A caller that is still wrong, such
    as a CDN that ignores the query or a tab opened before the deploy, is caught by this.
- **A stylesheet that is not its own shows a notice, and this takes no script.**
  - Each product's root renders a notice element that carries its build's stylesheet stamp, hidden by
    default.
  - Each stylesheet shows the notice for any stamp and hides it for its own (`minify.mjs` appends the
    two rules, since a file cannot contain its own hash).
  - A stylesheet from another version therefore shows the notice. A stylesheet not yet loaded shows
    nothing, so there is no flash on a cold load. A missing stylesheet leaves the grid visibly unstyled,
    which is not plausible.
  - The notice's appearance is Visual Tokens (ADR-0028); no colour comes from C#.
  - **A Wrapper's stylesheet is checked the same way.** The Wrappers have no root element of their
    own (ADR-0062), so each Chrome declares the stylesheets it brings: a default interface member,
    empty for the built-in Chrome. The product's root renders a notice for each, beside its own.
    *(2026-10-11, at the implementation. The other ways were rejected: a notice in `MudExGridPaper`
    reaches only one of the three Wrappers, and a notice inside a seam such as the toolbar fails
    when a Consumer fills that seam.)*
  - **The notice's markup is a contract between versions, and never changes.** A stylesheet of one
    version has to find the notice a DLL of any other version renders. So the notice is
    `data-ex-notice="stylesheet"`, `data-ex-stylesheet="<the file's name>"` and
    `data-ex-stamp="<the stamp>"`, and the script's is `data-ex-notice="script"`. These are
    attributes, not classes, so they are on neither of ADR-0029's lists, and no Consumer styles by
    them. An internal class "may change without notice", which this selector must not.
- **The address's shape follows a measurement of GitHub Pages' CDN** (decided with the user: measure,
  then decide). Two deploys of the Docs Site, read immediately after the second, show whether the CDN
  serves an old body under a new `?v=`. If it does, the hash goes into the file's name
  (`ex-grid.<hash>.min.js`) instead of the query, and this ADR records the result. Until then, `?v=`.
  *(Measured on 2026-10-11, between deploys, every 21 seconds for two hours: the CDN's cache key
  ignores the query. A `?v=` never asked for before is a hit, with the plain address's age. So a new
  `?v=` meets an old body unless a deploy purges the CDN, and only the two deploys show that. Either
  way the handshake refuses what arrives, so the outcome is a notice and never a wrong grid.)*

## What this replaced, and why

The decision of 2026-10-03 held that freshness was the Consumer's: "the platform has the answer". It
gave a standalone recipe: `_content/ExGrid/ex-grid.min#[.{fingerprint}].css` in `index.html`, with
`OverrideHtmlAssetPlaceholders`, "and the same import map". It also said that MudBlazor and AG Grid add
no mechanism of their own. On 2026-10-11:

- **The standalone recipe was false.**
  - The SDK resolves the placeholders only for `.js` and `.mjs`, and at publish only for
    hard-fingerprinted files. A package's files are soft-fingerprinted.
  - A published app, the Docs Site among them, linked the plain names, and its import map held only
    `_framework` files.
- **"The platform has the answer" held only for a Blazor Web App on `MapStaticAssets`.** A standalone
  app on the platform's own static hosting was measured stale, as above.
- **"MudBlazor and AG Grid do not either" was literally true and misleading.**
  - MudBlazor's documentation leaves the problem to "a suitable cache busting strategy". Its
    maintainers answer stale-script reports with Ctrl+F5, and one of them has proposed the very
    handshake rejected here (MudBlazor#13486).
  - Fluent UI Blazor v4, Blazorise, DevExpress and Radzen (its theme) each build versioned addresses in
    their own C#. These are the only patterns that keep a standalone app fresh without the Consumer.
  - AG Grid ships its CSS inside its JS bundle, so the two cannot drift.
- **The rejected handshake** had been rejected because "the platform already provides it". The
  measurement showed that it does not, and that the failure it would catch is silent today.

## Considered options

- **Minify at build time, in MSBuild.** Rejected: every build, CI's and a Consumer's from source,
  would need Node or esbuild. Committing the output and checking it by hash keeps the build .NET-only.
- **A .NET minifier (NUglify).** Rejected: its JavaScript parser does not follow the modern syntax
  the script uses, such as `?.` and `??`. A minifier that misreads the script would ship a script
  that differs from the one tested.
- **Serve the sources as well.** Rejected: the source map already carries them, and a second copy
  invites a Consumer to reference the large one.
- **A version handshake between the DLL and the script.** Rejected with the user on 2026-10-03: freshness
  was taken to be the application's to configure, and the platform to provide it. *(Taken on
  2026-10-11, above: the platform does not provide it for a standalone app.)*
- **The SDK's hard fingerprint on the package's files.** Rejected: packed, the fingerprint token is lost,
  and a standalone app's import of the packaged script 404s (measured 2026-10-11).
- **A `?v=` of the AssemblyVersion.** Rejected: consecutive prereleases share it.
- **A version the Consumer bumps by hand in `index.html`.** Rejected: it is forgotten. MudBlazor
  documented it and withdrew it after it broke a deployment (MudBlazor#12829).
- **A JavaScript initializer (`*.lib.module.js`) that injects stamped stylesheet links before the first
  render.** Not taken: it would remove the cold-load flash, but it is new JavaScript outside ADR-0021's
  list, and a companion package adds no script.
- **Leaving it to the Consumer, documented: "serve `_content/` with `Cache-Control: no-cache`".**
  Rejected: GitHub Pages lets no one set it, and the mismatch it leaves elsewhere stays silent.
