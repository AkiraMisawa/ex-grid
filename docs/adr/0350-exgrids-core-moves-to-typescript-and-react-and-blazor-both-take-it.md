# ExGrid's core moves to TypeScript, and React and Blazor both take it

*Decided with the user, 2026-10-24, for 0.3.0. It began from the user's question of what it would take
to use ExGrid from React. The user chose the recommended strategy of three, the hybrid below, and set
the order: the core in TypeScript and React in 0.3.0, Blazor's ExGrid on the same core once it holds.
Sections 9 to 13 answer the five questions the first draft left open, decided with the user the same
day.*

*(Rewritten the same day, decided with the user: Blazor's ExGrid moves onto the Script Core in 0.3.0
too, rather than in a later version after a later ADR. The first draft's title was "ExGrid's core moves
to TypeScript; React takes it first, and Blazor follows if it holds". Sections 1, 5, 6 and 7 changed;
the order inside the version, React first, did not.)*

*(Sections 14 and 15 added the same day, decided with the user: Blazor's surface is designed afresh,
not kept, and what 0.3.0 does to speed is measured.)*

*(Section 16 added 2026-10-25, decided with the user: the Script Core's public shape, from a design
memo that put five questions with a recommendation each; the user took every recommendation.)*

*(Amended 2026-10-25 by [ADR-0370](./0370-the-whole-family-moves-to-typescript-and-blazor-only-adapts-it.md), decided with the user: the whole family, its engines included, moves to
TypeScript, and Blazor only adapts it. Sections 1, 6, 7, 8, 12, 13 and 14 carry dated notes where it
changes them.)*

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

### 1. 0.3.0 ships ExGrid's core in TypeScript, and both React and Blazor draw through it

The **Script Core** is ExGrid's core written in TypeScript. It holds the instance's state and decides
what each input means: the Selection, the Focus and the Extent, the keys, the Cell Editor's modes, the
copy and paste rules, the overflow decision, Auto width, the Range Requests. It is the C# core's
behaviour as the ADRs state it, moved, not redesigned. It depends on no UI framework.

In 0.3.0 the Script Core is ExGrid's only core. React takes it through `@ex-grid/react`; Blazor's
ExGrid becomes a Framework Adapter over it (section 7), and the C# core's component, script and logic
are removed.

ExSheet, ExPivot, ExChart, ExBoard and the MudBlazor Wrappers stay Blazor products. They are not
ported, but from 0.3.0 the ExGrid that draws them is Blazor's adapter over the Script Core, so each is
verified again on it. Whether any of them reaches React is a later decision.

*(2026-10-25: the user decided that every product reaches React, and that the whole family moves to
TypeScript, its engines included, with Blazor as a thin wrapper over it.
[ADR-0370](./0370-the-whole-family-moves-to-typescript-and-blazor-only-adapts-it.md)
says how, and replaces this paragraph.)*

### 2. The Script Core owns the DOM inside the instance root

The Script Core creates and updates the instance's elements itself. A row is plain markup and the row
is the only unit that is skipped or repainted
([ADR-0003](./0003-cells-are-plain-markup-by-default-not-components.md)). No framework renders rows: a
React component per row would repeat, through `memo`, the fight with `ShouldRender` that ADR-0003
measured, once per framework.

P1 to P9 of [ADR-0027](./0027-appearance-travels-in-css-geometry-travels-in-csharp.md) hold for the
Script Core as written, with "in .NET" read as "in the Script Core". The Definition of Done's criteria
that cite them are run against it.

### 3. A Framework Adapter is thin: React's, and Blazor's ExGrid

A **Framework Adapter** puts the Script Core into one framework's component model. React's is built
first; Blazor's ExGrid becomes the second (section 7).
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

*(Narrowed 2026-10-25, decided with the user,
[ADR-0370](./0370-the-whole-family-moves-to-typescript-and-blazor-only-adapts-it.md) section 11: from
a product's step 3, its behaviour specs run on the React host and the two Blazor hosts run the specs
that judge anything passing through Blazor.)*

### 5. The C# core's cases become the Script Core's, as data

A layer-1 case of the C# core's pure logic (selection arithmetic, paste shape, copy refusal, overflow,
Auto width, Enter and Tab cycling) is written once as data, its input and its expected result, and run by
xUnit against the C# core and by the Script Core's test runner. A case is green on the C# core before
the Script Core takes it, so the cases are the oracle of the move. When the C# logic is removed
(section 7), its xUnit runner goes with it and the data stays.

### 6. The Script Core is not under ADR-0021's allowlist; what the allowlist protects still holds

ADR-0021 governs JavaScript beside a Blazor core. ExGrid no longer has one, so ADR-0021 is rewritten
when Blazor's adapter lands: ExGrid's entries leave it, and it keeps governing the scripts of ExSheet,
ExPivot, ExChart and the Wrappers, which stay beside Blazor code. The Script Core is JavaScript, so an
allowlist of its uses means nothing. What the allowlist protects
holds in it: no layout read on the path to a paint (P4), nothing per cell bound to a listener, every
listener scoped to the instance root ([ADR-0018](./0018-multiple-instances-must-be-independent.md)),
and the grid told when the browser knows something has changed, never measuring it. The Input Order's
rules that do not come from the circuit still hold: input is taken one at a time in the order it was
made, nothing waits on a clock (its sections 1, 3 and 4), and where a Consumer answers later than the
input, a gesture carries what it was taken against and is refused if that no longer holds (spine rule
6). The machinery that held and replayed input across a round trip is not ported.

*(2026-10-25: [ADR-0370](./0370-the-whole-family-moves-to-typescript-and-blazor-only-adapts-it.md)
moves ExSheet's, ExPivot's and ExChart's UIs to Script Cores of their own, and their scripts with them,
so ADR-0021 governs no script once its section 6's removal holds and is superseded then. These rules
hold for every Script Core.)*

### 7. Blazor's ExGrid moves onto the Script Core in 0.3.0, once React's holds

Blazor's ExGrid becomes a Framework Adapter over the Script Core in the same version. The order inside
the version is fixed, so that the Script Core is proved where nothing else moves before Blazor's
products stand on it:

1. The Script Core and `@ex-grid/react` pass the ExGrid layer-3 specs on the React host, in both
   browsers, and the Definition of Done's ExGrid criteria.
2. Blazor's adapter replaces the C# core. The ExGrid specs pass on both Blazor hosts, and so do
   ExSheet's, ExPivot's, ExChart's and ExBoard's, which ExGrid draws.
3. The C# core's component, `ex-grid.js` and its pure logic are removed, and ADR-0021 and the Input
   Order ([ADR-0240](./0240-input-to-one-product-instance-is-taken-in-one-order.md)) are rewritten for
   a grid whose decisions are made in the browser on both hosts.

0.3.0 is not tagged until all three hold. A step that does not hold is left failing and said so; it is
not narrowed to fit (CLAUDE.md, "Do not weaken a requirement").

On Blazor Server this moves the grid's decisions from the server to the browser. The Window, the
Consumer's answers and the Chrome's callbacks still cross the circuit; a key, a press and a selection no
longer do. A Consumer's answer that arrives after the input it answers is judged against what that
input was taken against (spine rule 6). What crosses the circuit per interaction is measured on the
Server host before step 2 is called done, and recorded.

*(2026-10-25: [ADR-0370](./0370-the-whole-family-moves-to-typescript-and-blazor-only-adapts-it.md)
section 6 has every product take these three steps, ExGrid's as written here and each product's once
ExGrid's Script Core has what it asks of it, and adds a fourth, the repository's layout; 0.3.0 waits for
all of them.)*

*(2026-10-26: the adapter's first slice. Until step 2 holds, Blazor's adapter is
`ExGrid.Script.ExGrid<TRow>`, beside `ExGrid.Components.ExGrid<TRow>`, so that ExSheet, ExPivot, ExChart and
ExBoard keep drawing through the C# core until each moves (ADR-0370 section 11); step 2 replaces the one with
the other. Its browser half is a private workspace package, `src/npm/blazor`, bundled with the core into
`_content/ExGrid/ex-grid-core.min.js` as ExGrid builds (ADR-0123's dated note). The Consumer's
`IGridSource` stays in .NET: the bridge hands the core a Grid Source that relays each call to it and is fed
the part of the source's Window around the rows the core last asked for (a few screens either side, so a source
holding every row never crosses whole), a row by an id kept until the browser can no longer hand the row back,
and a Sort or Filter as `GridViewStateJson`'s query text. What a column declares that the adapter does not
carry yet, a Template, `Validate`, a Tone or Row Marks, is refused by name as its parameters are set; Find and
the Selection Summary, which need the source to answer in .NET, are not offered (Ctrl+F is refused). An
edit's or a paste's intent reaches .NET after the browser committed it, so a Consumer that refuses one is
refused in turn, with an exception that says so: step 2 needs the core to ask before it commits, which on
Blazor Server is a round trip, and that is the core's decision to make, not the adapter's. Until then the cell
shows what its row held for that round trip after the commit, and the bridge holds what .NET sends while .NET
handles the write, so the first Window the grid takes in after it is one that holds the write (ADR-0142, D1).)*

### 8. The engines stay in C#

ExGrid.Data, ExPivot.Engine and ExSheet.Engine are not ported. A React application reaches them on a
server, through the Arrow Snapshot ([ADR-0065](./0065-a-snapshot-travels-as-apache-arrow.md)) and the
server pivot's API ([ADR-0151](./0151-server-pivots-send-report-windows-and-share-the-local-engine.md)),
or in the browser as .NET WebAssembly. Whether one is later ported to TypeScript or Rust is decided on a
recorded measurement of the browser case, not before (CLAUDE.md, "Measure before claiming anything about
performance").

*(2026-10-25: [ADR-0370](./0370-the-whole-family-moves-to-typescript-and-blazor-only-adapts-it.md)
replaces this section, decided with the user: the engines move to TypeScript too, re-proved against
Excel's recorded answers, the C# facts transcribed into shared cases, and a differential run against
the C# engine (its section 2). The measurement this section waited on became the check of its section
7. What stays in C# is a .NET server's side (its section 3).)*

### 9. The npm packages are `@ex-grid/core`, `@ex-grid/react` and `@ex-grid/mui`

npm has no rule for how a scope is spelled; a scope is simply the name of the npm organisation that
owns it, usually the brand in lower case (`@tanstack`, `@mui`, and `ag-grid-community`, which keeps
ag-Grid's hyphen). The scope is `@ex-grid`, the spelling of the repository and of the grid's CSS
prefix, chosen by the user after ag-Grid's. The unscoped name `exgrid` belongs to an unrelated grid on
npm, and nothing is published under `@ex-grid` (both checked 2026-10-24). The organisation `ex-grid` is
created on npm by the user before the first publish. The NuGet identifiers stay `ExGrid`.

*(The first draft of this section, the same day, chose `@exgrid` for the NuGet identifiers' one word.)*

The later products, if they follow (section 1), take names under the same scope.

Blazor's adapter keeps the NuGet identifier `ExGrid`, and the component keeps its name. It carries the
Script Core's built script as a shipped asset of its own, minified and asked for by the version it was
built with ([ADR-0123](./0123-shipped-scripts-and-stylesheets-are-minified-and-each-package-asks-for-the-files-it-was-built-with.md)),
so a Blazor Consumer installs nothing from npm. `ExGrid.MudBlazor` stays the Wrapper over it.

### 10. React and Blazor only

No adapter for Vue, Angular or any other framework is built, and no custom element is published.
Section 3's custom element is withdrawn: it existed for frameworks the family does not serve. Because
the Script Core is a plain TypeScript object that mounts on an element, a custom element remains a thin
adapter of its own that can be added when a third framework is served; nothing in the core waits for it.

### 11. The MUI Wrapper ships in 0.3.0

`@ex-grid/mui` is React's counterpart of ExGrid.MudBlazor, under [ADR-0030](./0030-what-a-design-system-wrapper-owns-and-what-it-may-not-touch.md)'s limits as written: it maps
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

*(2026-10-25: [ADR-0370](./0370-the-whole-family-moves-to-typescript-and-blazor-only-adapts-it.md) section 8 widens the switch to every product's pages.)*

*(2026-10-25: the switch is built ahead of the React packages, and what a page shows meanwhile is
[ADR-0110](./0110-the-docs-site-is-a-webassembly-app-on-github-pages-and-its-examples-show-the-code-they-run.md)'s
"The framework switch". It is remembered in `localStorage`, the site's second use of JavaScript there.)*

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

*(2026-10-25: [ADR-0370](./0370-the-whole-family-moves-to-typescript-and-blazor-only-adapts-it.md) section 9 moves the workspace from `src/npm/` to `packages/`, and the NuGet projects
to `dotnet/`, as the last step of 0.3.0.)*

### 14. Blazor's surface is designed afresh, from the Script Core's

No Consumer depends on the current surface yet (the user, 2026-10-24), so Blazor's ExGrid does not
keep it. Its public surface is designed from the Script Core's, so that a concept has one shape in
TypeScript and C#: a column, a Grid Source, a Chrome seam, an event and its arguments are the same
things under the same names, spelled as each language spells them. Where the current C# surface has a
cleaner shape than the first TypeScript one, the TypeScript one takes it; neither side is the
template by default.

ExSheet, ExPivot, ExChart, ExBoard and the MudBlazor Wrappers are rewritten against the new surface in
step 2 of section 7. [ADR-0201](./0201-the-public-surface-is-what-consumers-chromes-and-the-sibling-products-use.md)'s
rule stands: the surface is what Consumers, Chromes and the sibling products use, and nothing more is
public. Every member that leaves or changes is a breaking Change Note.

*(2026-10-25: under [ADR-0370](./0370-the-whole-family-moves-to-typescript-and-blazor-only-adapts-it.md)
section 1, ExSheet, ExPivot, ExChart and ExBoard are not rewritten in Razor against the new surface:
each becomes a thin Blazor wrapper over a Script Core of its own, which is that rewrite. The MudBlazor
Wrappers stay Razor, over their product's wrapper.)*

### 15. Speed is measured, not assumed

The move is expected to make ExGrid faster, not slower: the browser applies an input to its own DOM
with no render tree to diff and, on Blazor Server, no round trip. That is a prediction, and two
predictions about speed were already wrong in this project (ADR-0003, ADR-0008). Three places could
make it slower and are measured first:

- **The Window crossing into the browser.** On both Blazor hosts each Window the Consumer pushes is now
  serialised to the Script Core. A large Window, or a live source that pushes often, pays for it.
- **Template cells on Blazor.** A Blazor component inside DOM the Script Core owns is a component root
  of its own.
- **ExSheet and ExPivot on Blazor**, whose engines answer in C# and whose answers cross into the browser
  after every recalculation or redraw.

`spikes/render-bench` gains modes for the Script Core under React and under both Blazor hosts, beside
today's C# core, and the numbers are recorded before step 1 and step 2 of section 7 are called done. A
case that is slower is said in the record and in the Change Notes, and fixed or accepted with the user;
performance still never gates CI.

### 16. The Script Core's public shape

Section 14 left the shape to be designed; these are its decisions. Where one changes a concept rather
than its spelling, Blazor's surface follows it under section 14.

- **A grid is made by a function and handed back as a handle.** `createGrid(element, options)` draws
  into the element, which becomes the instance root, and returns a handle with `update(options)` and
  `destroy()`. The adapters wrap it; nothing in the core assumes React's or Blazor's lifecycle.
- **Rows reach the grid only through a Grid Source.** A Source is `subscribe` and `snapshot`, the
  shape React's `useSyncExternalStore` reads, and declares what it can do by having the member: an
  optional `find` and `summarize` stand where C# has `CanFind` flags. Work the grid gives up on is
  cancelled with an `AbortSignal`. A `rows` option is sugar for `GridSource.from(rows)`. C#'s second
  route, a Window, its start and a total count passed straight to the grid, is not carried over: two
  routes to one thing are two sets of rules to keep in step.
- **A Number is `number | bigint`.** A `bigint` is held exactly, as C# holds a long, and a `number` is
  read as C# reads a double into a decimal (ADR-0023), so the two cores order and match the same values
  alike. A Summary's Sum is computed exactly, by counting decimal places, as C#'s decimal sums: never
  by adding doubles, which would show a total a money grid cannot stand behind.
- **A date is a Temporal value.** `Temporal.PlainDate` is C#'s `DateOnly`, `Temporal.PlainDateTime`
  its `DateTime`, and `Temporal.ZonedDateTime`, `Temporal.Instant` or `Date` its `DateTimeOffset`, which
  compares as the instant it names. A day in a calendar other than ISO's compares as the day it names,
  as Temporal's own `compare` does. JavaScript's `Date` alone cannot say a day without a time or a time without a zone,
  and a grid that guessed would move a value across midnight. Chrome and Edge ship Temporal from 144
  (January 2026); a browser without it (Safari, when this was decided) takes a polyfill the Consumer
  loads. The core does not bundle one, and recognises a Temporal value by its own tag, so a polyfill's
  values are read like the browser's.
  - *(Added 2026-10-25, with the filter panel.)* A date the core makes, one typed into a filter's
    condition, is made with the `temporal` option, the Temporal namespace the Consumer hands over: the
    core still never reads the global one. A grid with a Date column whose filter is offered (the
    column queryable, the column menu shown) is refused without it as its options are taken, naming the
    column, not on the click that opens the filter. The React adapter does not pass the browser's own
    Temporal in its place, as it passes the browser's language (ADR-0370 section 10): where one browser
    has Temporal and another does not, the same page would mount on the first and be refused on the
    second, a failure the Consumer never sees in development. Given or not, every browser answers alike.
  - *(Added 2026-10-26.)* Blazor's adapter passes the browser's own Temporal, where React's does not:
    a C# Consumer has no JavaScript namespace to hand over, and the browsers a Blazor product serves,
    Chrome and Edge (ADR-0017), all ship one. In a browser without it the core refuses the column by
    name, as it does for a React Consumer who passed none.
  - *(Added 2026-10-25.)* A typed date is read in ADR-0023's ISO forms and in the culture's numeric
    order, with a 24-hour time. C#'s `DateTime.TryParse` also reads a month's name, a 12-hour clock
    and a two-digit year by .NET's own culture data; the Script Core refuses those as not readable
    rather than read them by ICU's, which would make the two cores read one text two ways (the
    spine's first rule). Ticket 97's rule is applied whole: under a culture that writes its year first,
    a year-last date with only one order that is a date reads as that one, `13/01/2026` as 13 January,
    where .NET's parser refuses it before the rule is reached. The shared cases in
    `tests/cases/chrome/operand-reading.json` hold what both cores read alike.
- **A value without a format shows C#'s text where the two can agree** (ADR-0006). A boolean reads `True`
  or `False` and a date one ISO form by its type, `yyyy-MM-dd HH:mm:ss ±hh:mm` for an instant in the offset
  it holds, so a column reads alike on either core; a `Date`, which holds no offset, reads in UTC's, and a
  date held in another calendar shows the ISO day it names, the day its filter and sort read. A
  number is JavaScript's own shortest text for it: a `number` keeps no trailing zeros, where C#'s decimal
  keeps its scale, so a column that wants a fixed number of places declares a `format`. An object of the Consumer's own
  shows its own `toString`. The cell paints by this text and a source compares by it, so a change the format
  hides is not marked (ADR-0067).
- **A Row Key is a `string`, a `number` or a `bigint`**, given as `GridSource.from(rows, { rowKey })`.
  Two keys are the same key when they are the same value, as a `Map` holds them, so `1` and `1n` are two
  keys, as C#'s `1` and `1L` are. An object is refused, with `NaN` and a null key: it would be the same key
  only as itself, and a key rebuilt from the same data would name another row (ADR-0140). The clock that
  gathers live changes (ADR-0141) is an option too, `{ clock }`, the system's unless given, which is
  C#'s `TimeProvider` and what a test drives.
- **One callback hears every refusal.** `onRefused` receives one event whose `kind` says what was
  refused (a copy, a paste, a Find, …) and carries that kind's own facts. C#'s separate
  `OnCopyRefused`, `OnPasteRefused` and the rest become kinds of it.
- **Every callback receives one frozen event object**, never positional arguments, so a fact added
  later is a new member and not a broken signature.
- **A column is a plain object.** `{ name, type, value, width, … }`, with a width written `120`,
  `{ px, minPx, maxPx }` or `{ auto: true, minPx, maxPx }`. The bounded Fixed form was added on
  2026-10-25 with the header's resize, as C#'s `ColumnWidthSpec` bounds a Fixed width too: the width
  a drag reports is bounded by the column's own MinWidth and handed back Fixed, and written as a bare
  number it would take the default bounds, so a column whose MinWidth was below them could not be given
  back the width it reported, and one whose MinWidth was above them would lose it. Only the columns that carry behaviour of their own are made by a
  factory: `markColumn` and `actionColumn` in the core, `templateColumn` in each adapter, because a
  template is the framework's own markup.
- **A Chrome slot is a mount function in the core**, `(slot, context) => cleanup`, and a render
  function in React, drawn through a portal into the slot the core made. Either way the core decides
  what the slot means (the spine's fourth rule).

## Consequences

- ExGrid has one implementation from 0.3.0. Until step 3 of section 7 the C# core still ships on
  `main`; section 5 and the shared layer 3 are what keep the two from drifting meanwhile.
- 0.3.0 is the largest version the family has had: every product that ExGrid draws is verified again on
  the Script Core. Breaking changes to Blazor's ExGrid surface are Change Notes
  ([ADR-0190](./0190-release-notes-are-written-with-each-change-and-published-from-the-repository.md)).
- The family publishes to npm as well as NuGet. The release workflow
  ([ADR-0042](./0042-prereleases-ship-before-sign-off-and-only-a-stable-version-waits-for-it.md),
  [ADR-0205](./0205-0-1-0-is-grown-on-a-release-branch-rehearsed-by-release-candidates-and-signed-off-before-its-tag.md))
  gains a second registry.
- The Docs Site needs React Examples for ExGrid behind a framework switch (section 12).
- The toolchain gains a TypeScript build and a TypeScript test runner (section 13).
- `ADR-0122`'s time zone report is not needed in the Script Core: the browser's zone is read directly.
- [ADR-0017](./0017-target-chromium-browsers-only.md) (Chromium only) and
  [ADR-0045](./0045-exgrid-is-a-desktop-grid.md) (desktop and touch) apply to the Script Core as written.
