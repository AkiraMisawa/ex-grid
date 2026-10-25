# The Docs Site is a WebAssembly app on GitHub Pages, and its Examples show the code they run

*(Decided with the user on 2026-10-03, while rewriting the README after comparing it with
ag-grid's. Modelled on MudBlazor's documentation, which the user named as the reference.)*

*(Rewritten in part on 2026-10-11, decided with the user after comparing the site with
mudblazor.com and ag-grid.com: the site's frame and its reference pages, including Release Notes
([ADR-0190](./0190-release-notes-are-written-with-each-change-and-published-from-the-repository.md))
and a Roadmap ([ADR-0191](./0191-the-roadmap-is-read-from-the-tickets-and-the-adrs-and-carries-no-dates.md));
an Example's code shown whole; and how the site is published. Each passage that changed says so
where it changed.)*

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

### The frame and the reference pages *(added 2026-10-11)*

mudblazor.com and ag-grid.com both put a reader one click from four things the site lacked: what
changed in each version, what is coming, the whole API, and a search. The frame stays MudBlazor's.

- **An app bar of sections**, as ag-grid's: Get started, a Products menu (ExGrid, ExSheet, ExPivot,
  Data, and the Feature matrix), Showcases, Release notes, Roadmap, and a search box. The Chrome
  switch, the light/dark switch and the GitHub link stay where they are. Below the large breakpoint
  the text links fold into the drawer; the site stays desktop-first, as the grid is (ADR-0045).
- **The drawer's groups collapse**, as mudblazor.com's do, and only the current one opens.
  `DocsNav` stays the one ordered list. A group belongs to the Docs, Showcases, Reference or Project
  section, and "previous / next" walks only the Docs groups.
- **A rail, "On this page"**, lists the page's sections at wide widths. It is a static list read from
  the site map below, with no scroll spy. Beneath it, "Source of this page" links the page's file at
  the commit the site was built from.
- **A footer** of the same links, and the line the site was built from: its version or commit, MIT,
  no analytics.
- **A second kind of page, the reference page**, beside the topic page. It has no Examples, and every
  fact on it is read from a named source, at build time or by reflection over the shipped assemblies.
  It says what it is "as of": a version, or a commit.
  - **Release notes** ([ADR-0190](./0190-release-notes-are-written-with-each-change-and-published-from-the-repository.md)).
  - **Roadmap** ([ADR-0191](./0191-the-roadmap-is-read-from-the-tickets-and-the-adrs-and-carries-no-dates.md)).
  - **Packages**: the ten packages by product, their nuget.org descriptions read from the assemblies,
    which to install, and why every family reference is exact (ADR-0042).
  - **Feature matrix**: where the family runs (.NET, browsers, devices, direction, hosting, licence,
    maturity), each row citing its ADR; which surfaces each Wrapper fills; and, generated from the site
    map, which sections each product's pages hold and whether each has a MudBlazor Example. It stands
    where ag-grid's "Community vs. Enterprise" stands; this family has no paid edition, so its axes
    are where it runs and which Chrome draws what.
  - **Functions**: ExSheet's supported functions, from the engine's own list, by the catalogue's
    categories.
  - **Keyboard**: every key in the three products, each linked to the page that explains it.
  - **The API reference is not built yet.** A page per public type would publish a surface nobody has
    reviewed, and Consumers would start depending on types that are public only so the tests can reach
    them (ADR-0042). It waits for the decision about the public C# surface. Until then each topic page
    keeps its API table. *(2026-10-12, after
    [ADR-0201](./0201-the-public-surface-is-what-consumers-chromes-and-the-sibling-products-use.md): the
    **API reference** is built, at `/reference/api`. It lists every public type of the packages by product,
    in ADR-0042's order, read by the build from the shipped assemblies, each with its summary; a type's
    name opens its API table, the same table a topic page places. What is public and not the contract —
    the types marked `[EditorBrowsable(Never)]`, and the members of contract types the grid's script calls
    or that are so marked — is listed apart under "Not the contract: may change in any version", and every
    API table lists such members apart the same way. The topic pages keep their API tables.)*
- **A site map is generated at build time** from the pages: each route, title, section and Example.
  It feeds the rail, the search, the Feature matrix and the checks on Release Notes' links. A route
  missing from the navigation, a navigation entry with no page, a section title that is not a literal
  and two sections with one anchor are build errors, as a missing Example region already was.
- **Search is C#**, over the site map: pages, sections, Examples, Release Notes and Roadmap items. No
  index service, and no keyboard shortcut to open it, because every page holds live grids that take
  keys.
- **A navigation entry appears only with a page behind it.** A planned page is not listed early; an
  entry with nothing behind it would be quietly wrong.

### An Example shows the code it runs

- **The code beneath an Example is read from the Example's own source file**, as the demo pages'
  "The code" already is (`DemoCode`). The file is embedded in the assembly, and a marked region
  is shown. What a page shows cannot drift from what it runs.
  - A region that is missing is an error the page states, never an empty box or other code
    (principle 1).
  - *(Rewritten 2026-10-11.)* **The whole file is shown, and there are no regions.** That is how the
    site was built: every Example file is written to be read whole, and a region would hide the
    `@using` lines and the data a reader needs to run it. A file that is missing is still an error the
    page states.
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

### The framework switch *(added 2026-10-25)*

[ADR-0350](./0350-exgrids-core-moves-to-typescript-and-react-and-blazor-both-take-it.md) §12 gives the
site a framework switch, Blazor or React, before any React package exists. What it does until then:

- **It sits in the app bar beside the Chrome switch**, for the whole site, and is remembered between
  visits (below, under JavaScript). A first visit reads the site for Blazor. Under React the Chrome
  switch reads Built-in / MUI, and cannot be changed while no Example runs under React: a choice made
  there would change nothing on the page.
- **Read for React, an Example with no React counterpart says so in its frame**, where it would run,
  and neither the Blazor component nor its code is shown. An ExGrid Example says that it is not in
  `@ex-grid/react` yet; any other product's says that the product has no React package. The product is
  the Example's folder under `Examples/`, and an Example in no product's folder fails the page rather
  than be called something it is not.
- **A product's page, and a page that describes the Blazor packages** (Installation, Packages,
  Showcases, the Feature matrix and the API reference), says the same under its lead, because its prose
  is Blazor's. A page about the whole family
  that holds no Blazor code (Release notes, Roadmap, Keyboard) says nothing.
- **Each such note offers to read the site for Blazor**, which changes the one switch, not the Example
  alone.
- **The Showcases stay Blazor's**, opened as before with the Chrome and the scheme in the address.
- **The React Example's frame is not built yet.** It comes with the first React Example, once
  `@ex-grid/react` draws one: a page prebuilt into the site's static files and shown in a frame, as §12
  says, with its code beneath it.

### JavaScript

ADR-0021's allowlist governs the shipped packages: the grid and what it executes in a Consumer's
page. The Docs Site is a Consumer. It still takes JavaScript only where Blazor cannot do the job,
and records each use here:

- **The copy button writes the code to the clipboard** with `navigator.clipboard.writeText`. C#
  has no clipboard.
- *(Added 2026-10-25, for [ADR-0350](./0350-exgrids-core-moves-to-typescript-and-react-and-blazor-both-take-it.md)
  §12.)* **The framework switch is kept in `localStorage`**, read before the site first renders, so the
  first page drawn is already the framework chosen last time. C# has no storage in the browser. A
  browser that refuses the site storage (a `SecurityError`, or a `QuotaExceededError` on a write) is
  not an error: the switch says the choice lasts for the visit. Any other failure is thrown.

That is the whole list. Highlighting, navigation and theme switching are C#. There are no
analytics.

*(Added 2026-10-11.)* MudBlazor's own script, which its popovers, menus and autocomplete run, belongs
to the frame, as DoD WR-8 says of the Wrapper: it is MudBlazor's, not the site's. The site adds none:
no keyboard shortcut for search, nothing kept in `localStorage` (the theme and the Chrome are chosen
again on each visit), and no scroll spy on the rail. *(2026-10-25: the framework is kept there, as
above; the theme and the Chrome still are not.)*

### Publishing

- **The site is published from a `v*` tag, and on demand.** A workflow of its own
  (`.github/workflows/docs.yml`) builds it in Release and deploys it to GitHub Pages.
  - On a tag, the site describes the version on nuget.org. The site states that version.
  - Publishing on every push to `main` was rejected: the site would document parameters a
    Consumer cannot install yet.
  - On demand (`workflow_dispatch`) publishes `main`, for a correction that cannot wait for a
    tag. The site then states the commit it was built from.
- *(Rewritten 2026-10-11, decided with the user.)* **What happened instead, and what replaces it.**
  - The tag's publish ran beside the release, not after it. For `0.1.0-beta.2` the site stated the
    version half an hour before nuget.org had the packages, and it would have stated it had the
    release failed.
  - On demand published whatever branch it was run on, and said only its commit. Six runs went out
    from `lab/*` branches on 2026-10-07 and 2026-10-08, to carry experiments to a machine under
    investigation; the last, from `main`, documented features no package held.
  - **So the release publishes the site, after the push.** `release.yml` gains a last job that calls
    `docs.yml` with the version, the tag and the UTC day of the push (ADR-0190). The site therefore
    states a version only once nuget.org has it. `docs.yml` is no longer started by a tag.
  - *(2026-10-11, decided with the user,
    [ADR-0205](./0205-0-1-0-is-grown-on-a-release-branch-rehearsed-by-release-candidates-and-signed-off-before-its-tag.md);
    and 2026-10-12, decided with the user, for a stable version that a stable tag is above.)* **Once a
    stable version exists, a prerelease's release publishes no site, and neither does a stable version
    that a stable tag is above.**
    - The site describes the latest stable version. A prerelease's notes are on its GitHub release,
      and reach the site with the next stable publish.
    - A stable version exists once the repository has a tag without a suffix.
    - A patch to an older line, such as `0.1.1` from `release/0.1` after `0.2.0`, publishes no site:
      versions are compared by SemVer precedence, and its pages would replace a later version's.
    - `release.yml`'s first job decides it, and says so. `docs.yml` refuses a call that comes anyway,
      and refuses too when it cannot read the repository's tags.
    - The on-demand publish of `main` or a `lab/*` branch stays, under its banner.
  - **On demand stays, from `main` or a `lab/*` branch**, and refuses any other. A lab publish is how
    an experiment reaches a machine nobody can run a build on, and it stays possible.
  - **A site not published from a tag says so on every page**: a banner above the page names the
    branch and the commit, says that it describes changes not on nuget.org, and links the Release
    notes' Unreleased section. A `lab/*` site says that it is an experiment. The version chip in the app
    bar says the same in the warning colour.
  - Links to an ADR, and "Source of this page", point at the tag or the commit the site was built
    from, so a site published from a tag cites the decisions as they stood then.
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
- *(Added 2026-10-11.)* **The generator grows**: the site map, the Release Notes and the Roadmap are
  parsed at build time, each by a small hand-written parser with errors that name the file and the line,
  pinned by a test project of its own. The release workflow gains the job that publishes the site, and
  `docs.yml` becomes callable.
