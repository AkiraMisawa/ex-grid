# CDS marking — a sample Consumer

Status: ready-for-agent

Decided with the user in a design grilling, 2026-10-02 and 2026-10-03. It rests on decisions already
recorded, and adds two: [ADR-0120](../../adr/0120-the-engine-tells-which-linked-table-cells-a-value-was-computed-from.md)
(the Read Set) and [ADR-0121](../../adr/0121-a-consumers-cell-class-paints-and-only-paints.md) (the
Cell Class). It also uses [ADR-0016](../../adr/0016-column-width-and-overflow.md) (`####`, and its
note of 2026-10-03), [ADR-0047](../../adr/0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md),
[ADR-0048](../../adr/0048-a-sheet-document-holds-entries-and-exsheet-holds-the-one-undo-stack.md),
[ADR-0049](../../adr/0049-linked-tables-are-the-consumers-data-read-by-key.md),
[ADR-0058](../../adr/0058-a-formula-points-across-grids-through-a-pointing-scope.md),
[ADR-0068](../../adr/0068-change-highlight-is-asked-of-the-consumer-and-painted-without-animation.md)
and [ADR-0069](../../adr/0069-the-demo-pages-call-a-demo-api-server-both-hosts-share.md). Where this
spec and an ADR disagree, the ADR wins.

**This is a sample, and its words are its own.** The user decided that the sample's domain gets no
glossary. The words below are used in this spec and in the sample's code; none of them goes into
`CONTEXT.md`.

| Word | Meaning here |
|---|---|
| Reference Curve | a curve quoted by the data provider: a single name or an index |
| Official Proxy | a model-validated proxy curve the data provider also supplies |
| Bespoke Proxy | a curve the desk defines by Formulas over Reference Curves and Official Proxies, to fill the gaps the Official Proxies leave |
| Point | one tenor of one curve: a par spread in bp |
| Observed | the provider's attribute of a Point: it has a quote |
| Auto-quote | the value the sample gives a Reference Curve's unobserved Point by interpolation |
| Universe | every Reference Curve the provider offers |
| Marked set | the Reference Curves the desk marks; every index is in it |
| Override | a trader's replacement of a Point's value, for one day |
| Marking | writing every marked curve's Points, and the Jacobians, to the output folder |

## Problem Statement

A CVA desk marks CDS par spreads and recovery rates every day. Single names and indices come from a
data provider (Markit, in production). Counterparties without a liquid curve are marked by Bespoke
Proxies, defined by formulas such as `max(UST(t), average(AAA sovereigns(t)))` or `a × CDS(t) + b`.
The desk wants CVA's credit delta expressed in the quotes it can trade, so every Bespoke Proxy is
marked with its Jacobian against the Reference Curves it reads.

Today this runs in Excel, and its maintenance is the problem. ExSheet claims exactly this ground: its
Formulas read the application's own data, live, through Linked Tables (ADR-0046, ADR-0049). The
sample shows that the family can host the desk's screen. It also finds what the core still lacks;
those findings are ADR-0120 and ADR-0121.

## Solution

### Where it lives (ADR-0069)

- **The pages are in `samples/ExGrid.DemoPages`, under `/cds`**, so they run on both hosts.
  They use the MudBlazor Wrappers (`ExGrid.MudBlazor`, `ExSheet.MudBlazor`). The charts use
  Blazor-ApexCharts. That is the Consumer's script, which ADR-0021 does not govern.
- **The server side is in `samples/ExGrid.DemoApi`.** It holds the fake feed, a SignalR hub, the
  store, the marking job and the headless engine. Nothing in a package depends on it.
- **A user picks a name at start.** There is no authentication; production would use SSO. The name
  is recorded with every change.

### The feed

A fake provider in DemoApi, deterministic from a seed.

- **Curves.**
  - Single names are identified by `ticker.ccy.seniority.doc_clause`, for example
    `UST.EUR.SNRFOR.CR14`. Each carries a reference entity name and a recovery rate.
  - Indices are identified by `Family.Series`, for example `CDX.NA.IG.46`. A series has a version
    attribute, which never appears in an identity, because older versions have no quotes. Each
    family's on-the-run series also answers to `Family.OTR`.
  - Official Proxies have identities of the provider's own.
- **Points.** A par spread in bp and `observed` for each standard tenor: 6M, 1Y, 2Y, 3Y, 4Y, 5Y, 7Y,
  10Y, 15Y, 20Y, 30Y. Which tenors are quoted is realistic per kind of curve: a single name around
  5Y, CDX EM at 5Y only, iTraxx Main at 3, 5, 7 and 10Y. Official Proxy Points are observed.
- **Ticks.** On a timer, a share of the Points moves by noise and the rest stays as it was. Each
  snapshot carries a sequence number.
- **The control panel** sets the interval, pauses, steps one tick, rolls an index family, and
  injects faults: all of a curve's quotes gone, one Point gone, a new series with no quotes. Tests
  step the feed and never wait on its timer (CLAUDE.md, principle 6).
- **The hub** pushes each snapshot to the pages. A page holds the latest snapshot and nothing older.

### Tenors

- **One tenor range for the whole page**, chosen from the standard tenors, for example 6M to 20Y.
  It sets the Reference tabs' columns, the Bespoke columns and the files' columns. A change is
  recorded.
- **Narrowing** removes the Bespoke column. A Formula that read it becomes `#REF!`, and that stops
  Marking until it is fixed.
- **Widening** gives a Reference Curve the provider's quote at the new tenor, or its Auto-quote.
  It gives a Bespoke Proxy an empty column. The trader fills it, by copying or with the bulk
  interpolation below. An empty Point stops Marking.

### Reference Curves (ExGrid, read-only)

Tabs: **Universe**, **Single Names** (the Marked set), **Indices** (every series, on-the-run first)
and **Official Proxy**. Every tab is an ExGrid, in `MudTabs` with `KeepPanelsAlive`. Each row is a
curve and each column a tenor, with its identity and recovery rate beside it.

- **Auto-quote.** An unobserved Point takes the linear interpolation, in tenor years, between the
  observed Points either side. Beyond the first and the last observed Point it is flat. A curve with
  one observed Point is flat at it. A curve with none has no value: it is an error, and it is never
  invented. The server computes Auto-quotes, because a Reference Curve is data, not a Formula.
- **Faint.** An Auto-quoted Point carries the sample's Cell Class (`cds-estimated`, ADR-0121), which
  the sample's stylesheet paints faint.
- **Overridden.** An overridden Point is Cell State Modified (ADR-0006).
- **Changed.** A Point whose value moved by at least the page's threshold carries the Change
  Highlight (ADR-0068): one colour, its token and duration set by the sample.
- **Overrides.** A Point is overridden from a dialog, opened from the Context Menu. Nothing is
  written into the grid directly.
  - An Override is a level (a value) or an offset (the live value plus x bp), with a reason.
  - It carries the value it replaced, and is refused if that value has changed meanwhile.
  - A level whose underlying quote has since moved by more than the page's tolerance is flagged.
    The flag warns; it does not stop Marking.
  - Recovery rates are overridden the same way.
- **Convenience actions.**
  - **T-1:** the previous business day's last successful Marking, for a Point or a whole curve.
  - **Interpolate from neighbours:** for one Point.
  - **Carry the on-the-run:** on a roll day, the previous on-the-run series' Points, laid onto
    today's on-the-run when its new series has no quotes. The reason names the series carried.
- **The Marked set.** Universe has an action that adds the selected curves to the Marked set.
  Single Names has an action that removes them. Removing a curve that a Bespoke Proxy reads asks
  first and lists the proxies; if the user goes on, those proxies are errors until fixed.

### Bespoke Proxies (an ExGrid list, an ExSheet definition area)

The Bespoke tab has the list above and the definition area below it. Beside the area is a
switchable Reference grid (Single Names, Indices, Official Proxy, Universe). The area and the
Reference grid are in one Pointing Scope (ADR-0058).

- **One Bespoke Proxy is one record.** It holds the ticker, reference entity name, ccy, seniority,
  doc clause, recovery rate (a constant or a Formula) and one Entry per tenor. It also records the
  template it was stamped from, with its parameters, and its versions. Its identity,
  `ticker.ccy.seniority.doc_clause`, is unique and contains only `A–Z a–z 0–9 . _ -`.
- **A Bespoke Proxy reads only the Linked Tables and its own row.** A Reference to another proxy's
  row is refused when the proxy is saved. Two proxies that need the same logic each write it out.
- **Linked Tables** (ADR-0049), pushed whole with each snapshot.
  - `Cds` holds every Point of every curve in the Universe, the indices and the Official Proxies.
  - Its key column is `Key`, written `<identity>|<tenor>`.
  - It also has `Curve`, `Tenor`, `Spread` (after Auto-quote and Overrides), `Observed`, `Marked`,
    `Kind`, and `Otr`, which holds `Family.OTR|<tenor>` on the on-the-run series' rows and is empty
    on the others.
  - `Rec` is keyed by `Curve` and has `Recovery` and `Otr`.
- **Formulas read a Point by the heading.** Row 1 holds the tenor labels and row 2 the tenor years.
  A Formula reads `XLOOKUP("UST.EUR.SNRFOR.CR14|"&C$1, Cds[Key], Cds[Spread])`, or the on-the-run
  through `Cds[Otr]`, so that copying and filling across tenors stays right. An average over a set
  of names lists the names until `AVERAGEIFS` is admitted (ticket 15).
- **The list.** An ExGrid with one row per Bespoke Proxy. Values come from the server, computed per
  snapshot by the headless engine, one Sheet per proxy. A Point's Formula shows in the read-only
  Formula Bar.
  - A Point is normal only when every cell in its Read Set (ADR-0120) is an observed `Cds` row at
    the Point's own tenor. Otherwise it carries `cds-estimated`.
  - Errors are Cell State Error, and Overrides are Modified. Changes carry the Change Highlight.
- **The definition area.**
  - **Edit** moves the selected proxies into the area. Several may be there at once, and Entries
    copy between them. **New** adds an empty row, which a template's form can fill.
  - A proxy in someone's area is locked: the list shows who holds it. Taking a lock over needs a
    reason, and is recorded.
  - Rows are identified by their identity, not by position. Inserting or deleting rows in the area
    edits nothing but the area.
  - **Save** writes each row back as a new version. **Apply for today** makes the row's Entries that
    day's Override of the definition. **Discard** writes nothing.
  - When a Formula is committed and its Read Set takes a curve outside the Marked set, a dialog
    offers to add the curve. Declined, the Formula stays, and the readiness panel lists it as an
    error.
- **Templates are stamped out.** A template is a form, for example `a × reference + b`,
  `max(reference, average(list))`, or "5Y from a reference, the rest interpolated". It writes
  Formulas into the row. The Formulas are the truth; the record keeps which template and parameters
  wrote them.
- **Bulk interpolation** fills the empty cells of the selection, row by row. Each cell gets the
  linear interpolation in tenor years between that row's nearest filled tenors, flat beyond them.
  It is one undo step, and never overwrites an Entry.
- **Overrides of a Bespoke Point** are made from the list's dialog (level, offset, T-1). A Formula
  for the day is made with **Apply for today**. Both end with the day.

### The readiness panel

Always at the top of the page, with a count of errors and of warnings. Each line goes to its cell,
and an error's line offers its fix (T-1, interpolate, add to the Marked set).

- **Errors stop Marking:**
  - an Error Value in any Point or recovery to be written, `#GETTING_DATA` included;
  - a Reference Curve with no observed Point;
  - an empty Bespoke Point;
  - a duplicate or unsafe identity;
  - a Bespoke Proxy reading a curve outside the Marked set;
  - a `#REF!` left by a tenor change.
- **Warnings do not:**
  - a flagged level Override;
  - a Bespoke Point with no sensitivity, because an Override means it reads nothing;
  - a roll: the on-the-run changed series, or was carried.

### Marking

- **Triggers.** The times set on the page, in the time zone the page states, and a **Mark now**
  button that takes a reason. The record says whether a Marking was scheduled or manual, who
  pressed it, and why.
- **The server marks, one Marking at a time**, whether or not a page is open. A manual request
  carries the state its user reviewed: the snapshot's sequence number and the version of the
  definitions, Overrides, tenors and Marked set. It is refused if that state has moved since, or if
  the same state is already marked, and the refusal says by whom and when.
- **The input** is the latest snapshot the server held at the time.
- **Any error stops it.** A scheduled time that meets an error writes nothing, records why, and
  shows a banner. It does not try again later, because what it marked would then depend on when the
  fix landed. The trader fixes and presses Mark now. A time the server was down for is recorded as
  missed at the next start, and is not run late.
- **The output** goes to `<root>/<yyyy-MM-dd>/<HHmmss>-<scheduled|manual>/`:
  - `single_name_cds.csv`, `index_cds.csv` and `bespoke_proxy_cds.csv`, all with the header
    `ticker,reference_entity_name,ccy,seniority,doc_clause,recovery_rate,<tenors…>`, tenors written
    like `6m,1y`. Spreads are in bp, and recovery rates are decimals (0.40).
    - Indices write `n/a` for seniority and doc clause. All on-the-run rows come first, as
      `Family.OTR` with the series named in `reference_entity_name`, then every series.
  - `jacobian/d_<bespoke>_d_<reference>.csv`, one file for each pair of a Bespoke Proxy and a curve
    it reads. Identities are written with any `n/a` part left out. Each file is a matrix: a `tenor`
    column, then one column per tenor of the Reference Curve, and one row per tenor of the proxy.
  - `manifest`: the snapshot's sequence number, the versions, the trigger, the user and reason, the
    bump size, every Override, and every warning.
  - Each file is written under a temporary name and renamed. A `latest` file in the date folder,
    naming the Marking's folder, is replaced last; a reader that follows `latest` never reads half
    a Marking.

### The Jacobian

- **A bump of +1 bp, one-sided, on each Point in a proxy's Read Set.** The server recomputes the
  proxy and divides the change by 1 bp. Only the Points the proxy read are bumped; the Read Set
  says which (ADR-0120). Recovery rates are not bumped.
- **The coordinates are the Points the Formula read.** Read through `Cds[Otr]`, that is the
  on-the-run row; read by series, the series' row. An overridden Point is bumped at its overridden
  value.
- **An Override that leaves a Point reading nothing gives it no sensitivity**, and that is a
  warning.

### The inspector

A row action opens a `MudDialog` inspector, as the DemoHost's inspectors do; several may be open.

- **The chart** (ApexCharts) plots spread against tenor years: observed Points filled, Auto-quotes
  hollow, Overrides marked.
- **Every curve** shows its identity, recovery, the last snapshot's number and time, its Overrides
  (who, why), and the last Marking's values with today's change.
- **A Bespoke Proxy** also shows its Formulas, its Read Set with each Point's observed state, and a
  button that computes its Jacobian now.

### What is recorded

Every user change is recorded, with the user, the time, and the state before and after:

- each Bespoke Proxy's versions;
- Overrides;
- the Marked set;
- the tenor range;
- the schedule;
- lock take-overs;
- every Marking, with its outcome.

## User Stories

1. As a trader, I want every single name I mark, every index and every Bespoke Proxy on one page,
   updating as quotes arrive, so that I see the market move.
2. As a trader, I want unobserved Points painted faintly, so that I know which numbers are
   interpolated.
3. As a trader, I want a moved value marked for a moment, so that I see what changed.
4. As a trader, I want to override a Point with a reason, for today only, without editing the grid,
   so that tomorrow starts from the provider again.
5. As a trader, I want T-1 and interpolation one action away when a quote is missing, and the
   on-the-run carried over on a roll day, so that a gap does not stop the desk.
6. As a trader, I want to choose which single names are marked, and be warned when a proxy reads
   one I am removing.
7. As a trader, I want to see a Bespoke Proxy's value in the list and its Formula in the Formula
   Bar, so that I can check what a number is made of.
8. As a trader, I want to edit Bespoke Proxies in a sheet: write Formulas, point at a quote to
   reference it, copy between proxies, so that defining one is as quick as in Excel.
9. As a trader, I want templates and bulk interpolation, so that a simple proxy takes a minute.
10. As a trader, I want to be told when a Formula reads a curve that is not marked, and offered to
    mark it.
11. As a trader, I want to rewrite a proxy's Formula for today only, so that tomorrow's Marking uses
    the definition again.
12. As a trader, I want the Marking to run at the set times, and a button for when something went
    wrong, with the reason recorded.
13. As a trader, I want the Marking refused while anything is in error, with each error listed and
    its fix offered, so that nothing plausible but wrong is published.
14. As a downstream system, I want one file format for every kind of curve, a Jacobian per proxy and
    reference, and a `latest` that only ever names a complete Marking.
15. As a risk manager, I want every change and every Marking recorded, so that any mark can be
    explained.
16. As a developer evaluating the family, I want a working example of formulas over live data, so
    that I can judge ExSheet for my own desk.

## Implementation Decisions

- **DemoApi** gains a CDS area: the feed (a hosted service with a seed), a `CdsHub`, endpoints for
  the control panel, the Marked set, Overrides, Bespoke Proxies, locks, settings and the change log,
  a store, and the Marking job. Its store is a SQLite file beside the trades', never committed. Its
  output root is configured, and defaults under DemoApi's data directory.
- **The headless engine runs on the server.** It computes the list, the readiness, the Marking and
  the Jacobian, with one `Sheet` per Bespoke Proxy, fed the same `Cds` and `Rec` snapshots
  (ADR-0047: one implementation).
- **The pages** are added to `DemoPageList`, under `/cds`, and reach DemoApi as the other live pages
  do.
- **The sample's stylesheet** defines `cds-estimated` and sets `--ex-change-highlight-background`.
- **Product changes** go through their own tickets: the Read Set (ADR-0120) and the Cell Class
  (ADR-0121). The sample's tickets that need them say so.

## Testing Decisions

- **Layer 1, in `tests/ExGrid.DemoApi.Tests`:**
  - the feed is deterministic for a seed and a number of steps;
  - Auto-quote follows its rules at both ends, with one observed Point and with none;
  - a Marking's files are a function of the snapshot and the versions: the same input gives the
    same bytes;
  - the Jacobian of linear proxies is exact;
  - every error stops a Marking, and no warning does;
  - the refusals: a manual request against a moved state, an Override against a changed value, a
    duplicate or unsafe identity, and a Reference to another proxy's row.
- **Layer 3, on both hosts**, each test stepping the feed:
  - a faint Point is faint;
  - a highlighted Point survives a scroll on its cell (DC-65);
  - an Override from the dialog;
  - pointing from the definition area into a Reference grid writes the `XLOOKUP`;
  - Mark now writes the folder and then `latest`.
- **The product tickets** carry their own criteria (SH-56, DC-67 to DC-69).

## Out of Scope

- Real Markit access, authentication and SSO.
- A Bespoke Proxy reading another one.
- Sensitivities to recovery rates.
- Direction shown on the Change Highlight (ADR-0068).
- Approval workflows (four eyes); the change log records, it does not gate.
- ExSheet offering the Read Set or the Cell Class in its component.
