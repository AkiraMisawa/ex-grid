# The Docs Site is a WebAssembly app on GitHub Pages, and its Examples show the code they run

*(Decided with the user on 2026-10-03, while rewriting the README after comparing it with
ag-grid's. Modelled on MudBlazor's documentation, which the user named as the reference.)*

A Consumer learns ExGrid today from three places, none of them made for it:

- **The README** says what the products are and gives one snippet.
- **Each package's readme** gives setup and a first component. It goes to nuget.org, so it stays
  short.
- **The demo pages** (`samples/ExGrid.DemoPages`) run every feature. But they are layer 3's
  fixture (ADR-0019). Their prose is written for the person reading a failing test, and their ids,
  status lines and second instances are there for the suite. A page made to teach would change
  them, and the suite would break.

There is no place where a Consumer can try a component, read the code that makes it, and look
up its parameters. MudBlazor has one, and it is the model: a navigation by component, and on
each page a description, live examples with their code, and the API.

## The decision

- **A Docs Site documents ExGrid, ExSheet and ExPivot from its first release.** The data packages
  (`ExGrid.Data`, `ExGrid.Data.Arrow`) have pages too. It is one site for the family, not one per
  product.
- **It is a standalone Blazor WebAssembly application, `samples/ExGrid.Docs`.** It is not shipped,
  like every project under `samples/`.
  - It runs the real components in the reader's browser. A page about selection *is* a grid you
    can select in.
  - It is static files, so GitHub Pages can serve it. No server is involved.
- **It is not the demo pages.** `ExGrid.DemoPages` stays layer 3's fixture, and the Docs Site
  references neither it nor the DemoHosts. A page written to teach and a page written to be
  driven by a test want different things. One project serving both would bend one of them.
- **The site is built with MudBlazor.**
  - Its frame — the navigation, the app bar, the light/dark switch, the tabs — is MudBlazor's. A
    MudBlazor reader sees an application like their own, and the Wrappers sit in it naturally.
  - Only `ExGrid.Docs` references MudBlazor directly. The reference direction of ADR-0019 does not
    change: no shipped core package references the Docs Site or MudBlazor.
- **A page is a description, Examples, and an API table**, in that order.
  - **An Example** is a live component with the code it runs beneath it. It shows one thing:
    selection, sorting and filtering, live data, a Formula, a Field Settings dialog.
  - **The API table** lists a component's parameters, events and methods with their XML doc
    comments. It is generated at build time from the shipped assemblies' documentation files.
    Every public member already has one (a missing comment fails the build), so nothing is
    written twice.
- **Where a Wrapper exists, an Example has a Built-in / MudBlazor switch.**
  - The switch changes the Chrome and nothing else: the same rows, the same columns, the same
    gestures. This is principle 4 shown, not stated. Swapping the Chrome does not change
    behaviour.
  - The code beneath follows the switch. The MudBlazor side shows the code a MudBlazor
    application writes.
- **A Showcase is a page for one use case, with no prose.** There is one per product, under the
  built-in Chrome and under MudBlazor's:
  - ExGrid: a trade blotter, with live prices, pinned columns, a filter and a copy to Excel
  - ExSheet: a budget sheet, with Formulas typed and completed, References outlined, the fill
    handle and Format Cells
  - ExPivot: a sales analysis, with fields dragged in the Fields pane, Show Values As, collapse
    and Show Details

  The page fills the window. Its explanation lives on the product's Docs Site page, which links to
  it. The data is invented from a fixed seed, so every visit, and every recording, shows the same
  rows.
- **The README's GIFs are recorded from the Showcases, by a script in the repository**
  (`tools/readme-media/`). The script runs Playwright against a local build of the Docs Site and
  turns the recording into the GIF. When the UI changes, the GIFs are recorded again, not edited.
  Each GIF links to its Showcase on the Docs Site.
- **Nothing on the site calls a server.**
  - Data is generated in the browser. `ExGrid.Data` loads a million rows there in slices, so the
    scale is the same as on the demo pages.
  - Live data is simulated by a timer that makes Change Batches, as `/pivot-live` receives them
    from the demo API.
  - The demo pages that read the demo API (`/pivot-db`, `/pivot-live`, `/grid-live`) are not
    ported as they are.

### An Example shows the code it runs

- **The code beneath an Example is read from the Example's own source file**, as the demo pages'
  "The code" already is (`DemoCode`). The file is embedded in the assembly, and a marked region
  is shown. What a page shows cannot drift from what it runs.
  - A region that is missing is an error the page states, never an empty box or other code
    (principle 1).
- **The code is highlighted at build time, in C#.** A build step turns each embedded source into
  HTML with a class on every token. The browser receives finished markup.
  - **No JavaScript highlights.** There is no flash of plain text before the colours arrive, and
    no highlighter to load.
  - **Razor is tokenised by a tokenizer of the site's own.** Razor interleaves markup, C# and
    directives (`@code`, `@bind`, `@if`). The C# highlighters that exist handle C# and HTML
    separately. C# blocks may be handed to a library; the Razor around them is ours.
  - **The colours are CSS custom properties**, redefined for dark mode, as Visual Tokens are for
    the grid (ADR-0027). No colour is written into the generated markup.
- **The code view has MudBlazor's affordances**: show and hide, a tab per file when an Example
  has more than one (the `.razor` and the `.cs` that makes its data), and a copy button.

### JavaScript

ADR-0021's allowlist governs the shipped packages: the grid and what it executes in a Consumer's
page. The Docs Site is a Consumer. It still takes JavaScript only where Blazor cannot do the job,
and records each use here:

- **The copy button writes the code to the clipboard** with `navigator.clipboard.writeText`. C#
  has no clipboard.

That is the whole list. Highlighting, navigation and theme switching are C#. There are no
analytics.

### Publishing

- **The site is published from a `v*` tag, and on demand.** A workflow of its own
  (`.github/workflows/docs.yml`) builds it in Release and deploys it to GitHub Pages.
  - On a tag, the site describes the version on nuget.org. The site states that version.
  - Publishing on every push to `main` was rejected: the site would document parameters a
    Consumer cannot install yet.
  - On demand (`workflow_dispatch`) publishes `main`, for a correction that cannot wait for a
    tag. The site then states the commit it was built from.
- **GitHub Pages serves the site under `/ex-grid/`.** The workflow rewrites `<base href>`. It
  copies `index.html` to `404.html` so a deep link opens the app, and adds `.nojekyll` so
  `_framework/` is served.
- **CI builds the Docs Site** as part of `ExGrid.slnx`. An Example that no longer compiles fails
  the build, because its code is the code that runs. Layer 3 does not run against the site. The
  demo pages remain its fixture, and the components are tested there.

## Considered and rejected

- **Docs pages added to `ExGrid.DemoPages`.** One project would serve both a reader and a test
  suite. The fixture's ids and prose are there for the suite; a page made to teach would change
  them.
- **A static site generator (DocFX, MkDocs) with screenshots.** A grid's claim is how it feels
  under the keyboard and the pointer. A picture cannot show it. Live components there would mean
  an iframe per Example, and a second toolchain beside .NET.
- **Highlighting in the browser (highlight.js, Prism).** It needs JavaScript, it paints plain
  text first, and neither library tokenises Razor better than a tokenizer of our own would.
- **A site per product.** The products share a grid, a Chrome and data. One navigation lets a
  reader of ExPivot find the ExGrid page about selection, which is the same selection.

## Consequences

- **`samples/` gains `ExGrid.Docs`**, in `ExGrid.slnx`. It references the shipped packages and
  MudBlazor, and nothing references it.
- **`tools/readme-media/` holds the recording script**, and the README's GIFs come from it.
- **GitHub Pages must be enabled for the repository**, with "GitHub Actions" as its source. That
  is a setting, and a person makes it.
- **The README links to the site.** The package readmes stay as they are: they go to nuget.org
  and must stand alone.
- **`CONTEXT.md` gains Docs Site, Example and Showcase.**
