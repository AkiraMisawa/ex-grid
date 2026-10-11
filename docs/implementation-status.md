# Implementation status

**A snapshot, not a contract.** `docs/definition-of-done.md` says what finished means; this says
how far along it is. Taken on **2026-09-01** (end of the core-completion run), branch
`dev/claude-code`, with:

```sh
nix develop -c dotnet test ExGrid.slnx     # 412 + 306 pass, 0 failed, exit 0
cd tests/ExGrid.Browser && npx playwright test --project=chrome   # 38 pass (Edge absent here)
```

**Later the same day, ADR-0035** (paste and fill respect the Editable declaration) added 7 layer-1
and 4 layer-2 tests: **419 + 310 pass**. Its two layer-3 tests have since been run, and pass:
layer 3 came up on the machine the drop was made on — Linux (WSL2) — once both browsers were
installed: **80 pass, 0 failed, exit 0**, being 40 on `chrome` (152.0.7977.64) and 40 on
`msedge` (152.0.4191.53), each headed. ADR-0017's both-browsers clause is discharged for the
first time; every earlier run had one browser only.

That is also the first layer-3 run on a platform whose scrollbars occupy layout: **15 CSS px**
measured, against the 0 of the macOS machine the 2026-09-01 record was taken on. The zoom loop
that `scrollbar.spec.mjs` calls "where the answer lives" passes at device scale 1, 1.25 and 2.
**VZ-10 is discharged.** Its Pass column used to ask for "Windows and Linux" where §22 asked for
either, and §22 stated the reason: the test is a tautology against overlay scrollbars. The
criterion now names that property rather than the platform pair. What the run does not give is
the case behind the test's own "run it on Windows" comment — a real Windows desktop at 125%,
where the native scrollbar is a non-integer number of CSS pixels and the OS, not CDP, does the
scaling. That is **VZ-14**. It was discharged on 2026-09-24, on Windows at 125% (the Windows
paragraph below).

**Every ADR from 0001 to 0035 now works through to the component**, except ADR-0034
(validation — accepted after this snapshot and unimplemented, below) and the two package
projects (0019/0030's `ExGrid.MudBlazor` / `ExGrid.Fluxor`), which the Definition of Done
places out of scope for the core. What remains is verification depth, not features — see
`verification/2026-09-01/results.md` for the honest pass/blocked ledger — that record is the
macOS one; a run now files itself under `verification/<date>-<platform>/`.

**ADR-0034 (validation) and ADR-0036 (the context menu) are built**, along with the three
sections ADR-0005 gained on the way: copy with headers, the rule that a menu copy always takes
the asynchronous clipboard route, and the rule that no delimiter is ever guessed. ADR-0010 is
amended as ADR-0036 asked — `GridCommand` has no `Label`, and the built-in English wording sits
behind a `CommandLabel` seam — so the core holds no UI strings, which ADR-0035 had been claiming
before it was true.

CP-19's claim is no longer unverified: a menu copy writes without a permission prompt on Chrome
and on Edge, and layer 3 says so rather than the ADR assuming it.

The error popover's hover trigger (ED-17b) was left failing for one round and is now built. It
took **ADR-0021's fifth allowlist entry**: JavaScript hears the pointer moves and reports only the
stillness — and, since the Wrapper branch merged, a move onto another row, for the hover band —
because a Blazor handler on the Viewport is a wire round trip per frame on a Server
circuit — which the grid had already pinned as unaffordable in
`The_grid_does_not_listen_for_moves_until_a_drag_begins`, whose comment is the reason ADR-0021
asks for. The module decides nothing: it reports the offsets the browser hands it, and C# resolves
the cell and asks the Consumer.

**Same-day review round:** an adversarial 17-candidate review of this drop confirmed all
17 (pager-vs-pre-pager arithmetic, the editor's missing pointer teardown, and a cluster
of silently-wrong paths — ReplaceRow's value-equality lookup, the filter panel's count
comparison, the &nbsp; trim, the mute copy failure — plus AltGr, the dead interactive
branch, and two re-entered C#/CSS pairings). All 17 are fixed, each with its criterion:
VZ-13, FL-9, ED-12/13, KB-18/19, SL-16, PST-7, and the widened CON-8/KB-8. Stress-running
the zoom suite afterwards surfaced an **18th**, which the review had not seen: a reveal
judged against the scroll-event mirror was dropped while its predecessor's write was
still in flight, stranding the Focus off screen with no recovery — fixed by comparing
against the newer of last-event and last-write (recorded in ADR-0012), and pinned at L2.

**2026-09-23, ADR-0037 — entering a cell by key.** The last item on the "not in the core"
list below that the core itself owed. Space on a cell with several actions makes it
Interactive with the keyboard left on the root; Space on a Template cell hands its content
one focus request through the new `TemplateCellContext`; Space on an editable cell opens
Overwrite with the space in it; a held Space engages once; and the grid's action buttons no
longer hold the keyboard at all — out of the tab sequence, and not focused by a press either.
Run in a Linux cloud container with the .NET 10 SDK installed directly (no nix): layers 1 and 2
**446 + 403 pass**, plus the Wrapper's 17 — twenty of the component tests new, and each of the
rules they name was also broken on purpose once, to see its test fail. Layer 3: **69 pass,
0 failed — on the Playwright-bundled Chromium 1194, headless**, because the container has
neither Chrome nor Edge. Two settings were needed there that are not the project's:
Playwright's headless `--hide-scrollbars` dropped, without which the gutter tests correctly
refuse to pass ("measured 0x0" — confirmed identical on the untouched branch), and
`ignoreHTTPSErrors`, because the container's TLS-intercepting proxy is not a CA the browser
trusts. So KB-20 to KB-27, A11Y-17 and UX-14 are verified on Chromium only, and owe a run on
the `chrome` and `msedge` projects (item 4 below). No observational record was filed under
`verification/`: headless container timings are not comparable with that trend.

**2026-09-23, the first layer-3 run on Windows itself** — Chrome 153 and Edge 153 on
Windows 11, headed, with the DemoHost left in WSL2 and reached through localhost forwarding
(`verification/2026-09-23-windows/`). The full suite: **140 pass, 0 failed, 0 skipped**,
70 per browser. That discharges item 4 below: KB-20 to KB-27, A11Y-17 and UX-14 now pass on
`chrome` and `msedge`. There was a finding first. Playwright's default viewport is a CDP
emulation that pins `deviceScaleFactor` to 1, so on a desktop at 150% every existing test
measured DPR 1.00000003. Run as it stood, `scrollbar.spec.mjs` could never have answered
VZ-14, whatever the OS was set to. A VZ-14 block now turns the emulation off and refuses to
pass unless it is on Windows, at a fractional DPR, with bars that occupy layout. At 150% it
passes on both browsers: DPR 1.5, and the gutter the grid is told is **15.34375 CSS px**
(23 device px), a non-integer as VZ-14 supposes. After a sign-out to apply 125%, it passes
there too: DPR 1.25, gutter **15.203125 CSS px** (19 device px), on both browsers. **VZ-14 is
discharged.** The first 125% attempt lost one Chrome test at launch. Chrome exited with
code 0 before a page existed, and by the evidence it was applying an update that had been
waiting for the first launch after the sign-in. The rerun passed 8 of 8, and the record
keeps both logs.

**Later on 2026-09-23, the `chrome` half — on macOS.** Layer 3 ran on the development Mac
(macOS 26.6.2, Google Chrome 153.0.8010.53, headed, nothing changed in the configuration):
KB-20 to KB-27, A11Y-17 and UX-14 pass on `chrome` there too. The same run failed one older test, deterministically — CP-16's Ctrl+Enter fill,
written and passed on Linux, and never before run on a Mac, where Playwright's
`ControlOrMeta` presses Command. The product was wrong, not the test: the editor's keys
were read from the raw Control flag and never went through `GridKeys.Canonical`, so on a
Mac Cmd+Enter was a plain Enter — one cell committed and the Focus moved on, the rest of
the selection silently left unfilled — and off a Mac a Win+Enter committed too.
`OnEditingKeyAsync` now switches on the canonical form, with two layer-2 tests seen
failing first (`CellEditorTests`, Cmd+Enter fills where Meta is Command; Meta+Enter does
nothing where it is not), and **KB-3** is widened to name every key path, the editor's
included. After the fix: layers 1 and 2 **446 + 405** plus the Wrapper's 17, layer 3
**69 of 69** on `chrome`. `verification/2026-09-23-macos/metrics.json` is the run's own
record; against the 2026-09-01 macOS one, BIG-7 moved from 374 to 481 ms (+29%) on a
machine in ordinary use — observational, recorded as a note and not investigated.

**2026-09-24, the measurement harness's layer-2 half.** Every MUST in it that layer 2 can
check now has a test. Writing them found nine defects, and two rounds of review found four
more, two of them introduced by the fixes. All thirteen are fixed, and all but one with its
test seen failing first; the one is named below. The review also found two tests too weak to
fail — MEM-3 counted disposals but not creations, and nothing counted a fetch's token source —
and both are strengthened.

- **PF-3** — `RenderAllocationTests` measures a re-render's bytes as a slope between 4 and 16
  columns, so fixed costs cancel and only what scales with the painted cells is left. It found
  the grid composing per cell, per render: each cell's **id** (now cached per row, keyed on
  what composes it), **`aria-colindex`** (an int boxed and turned into a string on every cell
  and header cell; now interned), the run of **`####`** (interned by length — filled on demand
  since the review, because the first version filled every shorter run as it grew, some 25 MB
  of characters to reach one very wide column's), and an action's **class**. It also found
  per-cell allocations that are not strings: a **closure per header cell** (a captured index
  declared at the top of the loop, allocated with no button painted), **an enumerator per
  header cell** whenever a sort is applied (`aria-sort` iterated the interface), a closure per
  Action or Template cell (40 bytes; measured, fixed, and **not pinned** — every cell it
  touches also pays Blazor's directive cost, and no pair of grids differs in the closure
  alone), and — the costly one — **a new click handler per action button per render**: a
  changed attribute, so the diff registered a fresh event for every button on every render of
  its row and sent each to the browser (~50 KB per cell per render under bUnit). The review
  found **the header's menu buttons and resize grips doing the same on every render of the
  root** — every vertical scroll frame, on any grid with a sort, filter, width or Source wired
  — and their handlers are now held per column index as well. Held handlers survive a render
  that leaves a button in place; a sideways scroll that shifts the painted columns still
  re-registers the scrollable buttons it moves, because the diff compares by position. The
  second round found **header groups composing a string per leaf and per rectangle on every
  render of the root** (a leaf's height, a rectangle's box, `aria-colspan`); they are now
  cached alongside the geometry, layout and tier height that produce them. It also found that holding the
  action's handler as a bare delegate had dropped the row as its **receiver**, which is how the
  renderer finds an `ErrorBoundary` for a handler that throws: a Consumer's failing `OnAction`
  would have gone unhandled — on Server, fatal to the circuit. The row is its receiver again;
  that costs one small boxed callback per button per render, which is not a string, and the
  held delegate keeps the callback equal to the last, so the diff keeps its event. Pinned by
  `A_failing_action_is_caught_by_the_error_boundary_around_the_grid`, and by handler ids that
  survive a render for both the actions and the header. What an event directive still costs
  is Blazor composing its internal attribute name; that is the framework's, and stays —
  ADR-0027 now records P5's scope and why pre-composing the names would be quietly wrong on a
  newer runtime (ADR-0022). The measurement is the least of ten runs: in the full suite a few
  kilobytes landed in one run about one time in four, and never with tiered compilation off.
- **PF-4** and **MEM-4**'s layer-2 half were already pinned, and now say so in their comments;
  **BIG-4** gained a test at the Definition of Done's own numbers (10⁶ rows fit at 28px and are
  refused by name at 40px).
- **PF-5** — the whole result selected costs what eleven cells cost: the same element count,
  one range, no row re-rendered.
- **MEM-3** — `ResourceDisposalTests`, over a counting JavaScript runtime that answers every
  import and attach with a new reference, and a counting clock: created equals disposed, one
  for one, for every module, handle, .NET reference and all four timers. It found **the module
  leaked when disposal overtook its import** — `DisposeAsync` saw no module yet, and the
  import's continuation returned without releasing it. Fixed. The grid creates no token
  source; a fetching source creates one per fetch, and `GridSourceFetchTests` now shows each
  disposed however its fetch ended — answered, superseded, failed or cut off by disposal —
  which ASY-2 had claimed and nothing had checked.
- **MEM-7** (observational) — **51,014 bytes per one-row scroll frame**, 6 rows × 2 columns,
  under bUnit's renderer; recorded in `verification/2026-09-24-macos/metrics.json` as §22
  Step 6 now says.

MEM-5's ten-minute soak runs only under `EXGRID_SOAK=1` — decided 2026-09-24, and now written
into §22 Step 4 along with the layer-3 half that implements it (below).

**2026-09-24, the Definition of Done's row counts, and the harness's layer-3 half.** The
user decided that the row counts are the Definition of Done's, not the DemoHost's. `/wide`
had been 100,000 rows while §12 names 1,000,000, so every BIG test ran as "BIG-1-shaped" at a
tenth of the scale. VZ-1 compared scroll positions rather than 10³, 10⁵ and 10⁶. ST-1 ran at
200 and 100,000 rows where §22 Step 5 says 10³ and 10⁶. Now:

- **`/wide` is 10⁶ rows**, and takes `?rows=N` for the criteria that compare one Viewport
  across totals. Its rows are made on first request and kept, so a million are never built up
  front and a row answered twice is the same instance (ADR-0003).
- **ST-1 runs at 10³ and 10⁶** (the 200-row seeds stay). The 10⁶ case costs about a minute,
  and layer 2 went from ~15 s to ~80 s. Timed per operation, nearly all of that is the
  reference source re-sorting (~0.55 s) and re-filtering (~0.38 s) a million rows. That is the
  Consumer's work (ADR-0001), and the grid's own steps stay in milliseconds. **Decided the
  same day: it runs only under `EXGRID_ST1_MILLION=1`**, as the soak does. An ordinary layer 2
  skips it by name (4.5 s for the class), and §22 Step 5 is the run that sets it (67 s, 5 of 5).
  Step 2's "zero skips" now names this one exception.
- **`fixtures.mjs`** gives every spec one console capture, writing `console.json`. It covers
  CON-1/2 as before, plus **CON-3** (a warning from ExGrid's own code: served from
  `_content/ExGrid*`, prefixed `[ex-grid]`, or naming an `ExGrid.` category) and **CON-6**
  (an unhandled exception at any level). For CON-6 the DemoHost is WebAssembly, so its host's
  log *is* the browser console. The dev server's own output now goes into the run's output,
  which Step 4 tees. `scrollbar.spec.mjs` had no console check at all and now has one.
- **`virtualisation.spec.mjs`**, at 10⁶ rows, covers:
  - **BIG-1**.
  - **VZ-1 / BIG-2 / DOM-1**: the element count at 10³, 10⁵ and 10⁶ rows is identical,
    not "within 300".
  - **BIG-5**: the first and last rows' data checked against the generator, there and back.
  - **BIG-3**: Ctrl+A shows 10⁸, one rectangle, and the next key is answered.
  - **PF-1's layer-3 half.** Until now it was discharged by the grep alone. Every crossing
    between the module and .NET is counted in both directions, by CDP breakpoints that never
    pause. They are placed from the source the page was served, and a site that cannot be
    placed fails the test. At most one call per scroll frame, for rows, columns and a fling.
    The reading taken: "interop" is the grid's own. Blazor's delegated `@onscroll` dispatch is
    the framework's, one per event by construction, as ADR-0027 scopes P5.
- **`memory.spec.mjs`**, on the new `/lifecycle` page, covers:
  - **MEM-2**: fifty mounts and disposes, nodes and listeners within ±2 — measured 0 and 0.
    Its baseline is taken after one warm-up cycle, and that is checked, not assumed. On a
    page's first use of an event name, Blazor adds one delegated listener to the document and
    keeps it. The test asserts that the warm-up added exactly those (`scroll`, `mouseleave`,
    `mousedown`, `contextmenu` from `blazor.webassembly.js`) and no node.
  - **MEM-4**: the root carries the module's five listeners, has none after dispose, and the
    count returns exactly.
  - **MEM-5/MEM-6**, under `EXGRID_SOAK=1`: a seeded ten-minute scroll, sampled every 30 s
    after a forced GC. The managed heap is read through a `[JSInvokable]` host counter,
    because CDP cannot see WebAssembly's linear memory.
- **`observational.spec.mjs`** records into `metrics.json`: BIG-7 at 10⁶; DOM-5 at both
  settings; PF-6 (the settle repaint from `Performance.getMetrics` deltas, with the fling
  first let paint its Placeholders, plus frame intervals); BIG-6 as its "on" half; and PF-7,
  a 10 × 7 drag edge moved a row a step. PF-7 is 10 × 7, not ADR-0008's 10 × 8, because only
  seven scrollable columns fit whole beside the pinned block. It asserts the selection moved
  on every step. Its first version measured a pointer outside the window and still recorded
  "costs". These tests use a 1280×1000 window: the default 720px one leaves the grid's lower
  rows off screen.

The new checks whose failure is not plain from their assertion were each seen failing, with the product broken on purpose:
- a second offset read per scroll event (PF-1: 120 calls for 60 frames);
- a listener left on `window` per attach (MEM-2's warm-up check);
- the key listener left on dispose (MEM-4);
- an `[ex-grid]` warning (CON-3), where a third-party one passes;
- an "Unhandled exception" logged at info (CON-6).

**Where this ran, and what it therefore does not discharge.** It ran in a Linux cloud
container with the .NET 10 SDK installed directly:
- Layers 1 and 2: **449 + 421** plus the Wrapper's 17.
- Layer 3: **76 pass, 2 skipped** (VZ-14, which is Windows-only, and the soak), 0 failed.
- The soak run on its own: **pass**. The JS heap went 4.01 → 4.11 MB over 35,206 frames,
  levelling off after five minutes. The last sample is 0.4% from the median. The managed
  heap went 9.29 → 9.38 MB.

All of it was on the Playwright-bundled Chromium 1194, headless, with the same two local
settings as the 2026-09-23 container run (`--hide-scrollbars` dropped, `ignoreHTTPSErrors`),
kept in an uncommitted config. So none of the new MUSTs has met `chrome` or `msedge` yet. The
numbers were not filed under `verification/`, because software rendering belongs to no trend
(for scale only: settle repaint 20 ms on / 120 ms off, drag step 5.4 ms median).

**2026-09-25, the grid under Blazor Server.** Decided in a grilling session and recorded
before it was built: ADR-0022 rewritten (the packages target `net10.0`), and additions to
ADR-0005/0010/0018/0019/0021/0029/0033, the glossary's **Prerendered**, and the Definition
of Done's A11Y-20, ED-22, CP-21..23, ST-5 and new §24 (SRV). Built:

- `samples/ExGrid.DemoPages` holds the pages; the WebAssembly DemoHost only mounts them;
  `samples/ExGrid.DemoHost.Server` serves them in `InteractiveServer` with prerendering
  on. `/server` is `/fetch`; `/shared` is SRV-3's two-users-one-store page.
- Paste crosses as JS stream references under `PasteByteCap` (CP-21/22); a clipboard write
  the browser rejects is `ClipboardUnavailable` (CP-23); a bundled Grid Source refuses a
  second circuit (ST-5); a Prerendered grid is busy and takes no tab stop until its
  listener is attached (A11Y-20).
- Two defects only a circuit shows, each reproduced in layer 3 at a 150 ms round trip and
  then fixed: typing `1500` onto a cell committed `1` (the key gate now holds keys through
  a mode change, ED-22); and a key arriving between a render and its acknowledgement made
  an earlier render's callback focus an editor it did not contain, which threw and ended
  the circuit (the focus now waits for the render that paints the input).
- Layer 3 chooses its host with `EXGRID_HOSTING`, puts `latency-proxy.mjs` in front of the
  Server host, reads the host's log for CON-6, and on Server waits for a page to be
  interactive before acting on it, as a user must.

**Where this ran.** A Linux cloud container, .NET 10.0.401 installed directly, the
Playwright-bundled Chromium 1194 **headed under Xvfb** (so native scrollbars occupy layout),
with `ignoreHTTPSErrors` for the container's egress proxy, in an uncommitted config. None of
it has met `chrome` or `msedge`.

- Layers 1 and 2: **476 + 495 (1 skipped by name) + 52**.
- Layer 3 on WebAssembly: **137 pass, 5 skipped by name**, 0 failed.
- Layer 3 on Server: the specs that were only ever passing because WebAssembly answers
  within the same frame — a Focus, an editor, a menu or a clipboard read straight after
  the gesture — now wait for the answer. The last full run: **128 pass, 3 skipped by
  name, 11 failed**. Ten of the failures are the MudBlazor pages' web font, which the
  container's egress fails intermittently on either host (`ERR_TOO_MANY_RETRIES`, CON-1).
  The eleventh was one more test reading the Focus straight after a click (DIR-2), now
  waiting for it and passing 8 of 8. None is the grid's.
  *(Since then the demo pages serve Roboto themselves, and nothing in layer 3 reaches a
  third-party host. The files are those Google Fonts serves, with its `@font-face` rules,
  under `samples/ExGrid.DemoPages/wwwroot/fonts/roboto/` with the OFL. Loading it from
  Google was only ever a way of getting the font the Wrapper's widths describe (ADR-0030),
  and it made the container's runs depend on an external host. The MudBlazor specs pass
  36 of 36 on each host with no request leaving localhost.)*
- One more circuit-only defect found by that run and fixed: a Server circuit going away
  cancels every JS call still pending, and the cancellation of the grid's own disposal
  calls was not among the failures it expected, so it ended the circuit with an error in
  the host log. The core and the Wrapper now treat a canceled call as a disconnect, and
  each disposal is attempted on its own.
- SRV-6 (observational, container, not a trend): the band stands on the row a round trip
  plus about 20 ms after the pointer crosses onto it — 20 / 67 / 172 ms median at 0 / 50 /
  150 ms — and a sweep while scrolling carries 47–55 frames a second up the circuit
  whatever the round trip, so the reports are not per frame.

**2026-09-27, Excel's editing keys and Find.** A comparison against Excel, grilled with the user,
found four gaps and one defect. **The defect:** ADR-0007 said the grid forwards Ctrl+Z, and
nothing did. **The gaps:** Delete, Backspace, Ctrl+D / Ctrl+R and Ctrl+F. Decided as ADR-0007's
forwarding section, ADR-0054 (Delete raises a Clear Intent, never a paste of empty text), the
ADR-0035 additions for the fill keys and Backspace, ADR-0055 (Find is asked of the Consumer, and
the grid takes Ctrl+F even where nothing can search) and ADR-0028's rename of `ViewportSize.Fill`
to `Stretch`, which frees Fill for Excel's gesture. Built, with the criteria each ADR added
(KB-39 — KB-37 on `main`, ED-23..25, CP-24/25, FD-1..9):

- `OnUndo` / `OnRedo`, each key taken from the page only while someone listens — the key gate
  is now told a per-grid set (`GridKeys.TakenFor`) and re-told when it changes.
- `OnClear` with `GridClearIntent`; Backspace's empty Overwrite editor; Ctrl+D / Ctrl+R planned
  by `ClipboardRules.PlanFill` and raised as one paste intent of raw values, with three new
  refusals (`NothingToFillFrom`, `MultipleRanges`, `SourceUnavailable`).
- Find: `GridFind.Step` as the reference, `IGridSource.CanFind` / `FindAsync` with defaults
  that say no, `GridSource.Fetch(find:)`, `OnFind` for push mode, the find panel as a Chrome seam
  drawn by the built-in Chrome and by `ExGrid.MudBlazor`, and `OnFindRefused`.
- The Features demo keeps an undo stack, so the keys have something to drive.

**Where this ran.** A Linux cloud container, .NET 10.0.401 installed directly, **Google Chrome
and Microsoft Edge installed from their vendors' packages**, headed under Xvfb. Layers 1 and 2:
**644 + 600 (1 skipped by name) + 70**. Layer 3, the whole suite on both browsers: **444 pass,
14 skipped by name, 0 failed** on WebAssembly, and **448 pass, 10 skipped by name, 0 failed** on
Server. Neither discharges the Windows or real-IME runs.

**A max-depth review of this drop found open items; all are fixed.** Three needed a decision,
recorded in ADR-0055's "Settled in review" section: Ctrl+F inside the grid's own popovers is the
grid's (in the find field it selects the text), `OnFind` beside a bound Source is refused by name,
and an answer outside the request throws rather than being reworded as a reorder. The rest:
`ColumnInfo` now carries the column's `Format` and answers the displayed text itself, so a rebuilt
column compares equal again and the rule is written once; `PlanFill` reports the declaration first
(ADR-0035); a held Shift+Enter keeps its Shift; the demo's redo replays in order; FD-4/5/8/10/11,
CP-25 and ED-25 have the tests they lacked. A second review of those fixes found a held Ctrl+F
replayed into a popover (a second drain, and a filter field dropping it with every key after it);
it now goes to the core. After both rounds: layers 1 and 2 **650 + 611 (1 skipped by name) + 70**;
layer 3 on both browsers **484 pass, 14 skipped by name, 0 failed** on WebAssembly and **488 pass,
10 skipped by name, 0 failed** on Server.

**Two defects older than this drop, found by chasing the review's Server failures, are fixed:**

- **Text typed at full speed on a circuit lost characters** — in the Cell Editor
  (`…123456789` became `…1289`), the filter panel's fields and the find field. Each rendered its
  value by hand, `value="@x"` beside an `@oninput`, and every render wrote the server's older copy
  back over the typing. Only `@bind` tells Blazor the field's own value outranks a render's. New
  criterion SRV-7, and a trap in CLAUDE.md.
- **A held key replay stopped at the first bare modifier.** The Shift pressed for a capital during a
  hold — typing straight after Ctrl+F, E or a key that opens the editor — is a keydown of its own,
  and replaying it failed the "can this be reproduced" test, which dropped every key after it.

**2026-09-28, layer 3's time.** The browser suite took 24.3 minutes a run in CI on WebAssembly
and 9.0 on the Server host, and the first was the wall clock of every push. Measured, most of it
was the app booting: every test had a new browser context, so every test loaded the page and
started the .NET runtime — 1.65 s of about 2.3. A shared context's warm cache, a Release build and
a trimmed publish did not remove it; not booting did (0.05 s for an in-app navigation). Decided
with the user as ADR-0056 and an amendment to ADR-0041, after comparing AG Grid's suite, which
isolates by file and creates and destroys its grids per test:

- **A spec file boots the app once, and every test mounts its page afresh** by an in-app
  navigation through the index. The harness puts back what it owns. `alterPage` undoes what a
  test changes outside its page, and a document or a native left changed fails the test by
  name. `harness.spec.mjs` pins it.
- **CI splits each host's layer 3 by browser and into shards** (four for Chrome and Edge, two
  for `chrome-150` since 2026-10-04), each on a runner of its own, under a verdict job per host that keeps the old check names.
- **The audit for the new model found eight tests changing the page outside their grids,** and
  each of them would have run under every later test of its file: CP-23's clipboard stub, KB-15's
  listener, the printable key on `/cells`, A11Y-17's button, UX-2, UX-5 and UX-10's tokens on
  `body`, and DIR-2's `dir` on `#app`. They now go through `alterPage`. A11Y-20 and BIG-7 load
  their pages for real.
- **The first full run found one defect in the grid.** A grid disposed while the keys after
  Alt+↓ waited for its popover threw `Cannot read properties of null (reading 'contains')` from
  its next frame. The wait looked for DOM focus inside a root that was gone. A closed context never
  disposed a grid, so nothing had seen it. `ex-grid.js` now asks about disposal first, and
  `popovers.spec.mjs` pins it under both Chromes.
- **A two-axis review of the drop found gaps in the harness, all fixed.**
  - A test that failed only in the harness's own checks as it ended, not in its body, still handed
    its page on. So did a leak it named, which also kept the test's console record from being
    written. The checks are now soft, and any that fails keeps the page from the next test.
  - Server host-log lines between two tests were read by no test, and a file's last page was
    closed unheard. Both are now the next test's.
  - A test's `use` options beyond the viewport were not applied to a shared context. A test
    asking for others now gets the app booted in a context made with them.
  - The check now also reads the stylesheets' rules, the globals on `window`, and natives as a
    caller reads them, so a stub on `document` is seen.
  - The CI verdict jobs run `always()`. A job its `if` skips counts as passed for a required
    check, so a cancelled layer 3 had read as green.
  - `patchPage` is `alterPage`, because "patch" is on the Overlay's `_Avoid_` list.
  - The harness's paired tests skip their second half when it runs alone.

**Where this ran.** A Linux container, .NET 10.0.401, the Playwright-bundled Chromium 1194 (no
Chrome or Edge), headed under Xvfb, the `chrome` project only. Layer 3 on WebAssembly: **264 pass,
7 skipped by name, 0 failed, in 7.2 minutes** (12.1 before, on the same machine, for 242). On
the Server host: **266 pass, 5 skipped by name, 0 failed, in 4.3 minutes** (5.3 before, for 244).
Layers 1 and 2: **650 + 611 (1 skipped by name) + 70**. None of this is a run on the installed
Chrome and Edge; CI's first run of the split jobs is.

## Working through to the component

| ADR | | Pinned by |
|---|---|---|
| 0001 | Push/pull entry points, Range Requests | `RangeRequestTests`, `GridSourceBindingTests` |
| 0002 / 0023 | Filtering — the `Filters` parameter, `OnFilterChanged`, the source path | `FilterChromeTests`, six operator files |
| 0003 | Plain-markup cells, row memoisation | `RowMemoisationTests` |
| 0004 | Both-axis virtualisation, Pinned Columns, the fling | `VirtualisationTests`, `FlingTests` |
| 0005 / 0014 | The clipboard — both routes, both formats, every refusal; the JS allowlist's fourth entry is in use | `ClipboardDataTests`, `ClipboardParseTests`, `ClipboardWiringTests`, `features.spec.mjs` (real clipboard) |
| 0006 / 0024 | Cell State, Row Kind; **the column's display format** — one text for a value, in the cell, copy's `text/plain`, the value list and the editor, never in the raw `text/html`; **the tone rule** — the Consumer's closed-enum answer about a value, painted as `ex-tone-*`, coloured by tokens the bare grid leaves at `inherit` | both layers, `ColumnFormatTests`, `CellToneTests`, `mud.spec.mjs` (FN-7a) |
| 0007 / 0010 | **The Cell Editor** — Overwrite / Caret, F2, the typed-first character, Ctrl+Enter fill, the mode-gated key listener | `CellEditorTests`, `features.spec.mjs` (real keys) |
| 0008 | Selection overlay, the mouse, **the edge-band auto-scroll**, **the Focus band** | `SelectionTests`, `EdgeAutoScrollTests`, `FocusBandTests` |
| 0009 / 0010 | **The Chrome seams** — filter panel, column menu, editor, loading; `IGridChrome`; distinct values with the Excel exclusion rule; popovers dismiss by toggle, Escape and click-away | `FilterChromeTests`, `DistinctValueTests` |
| 0011 | Index-space selection; **column reordering by dragging** (blocks and groups clamp) | `HeaderReorderTests`, `ColumnGestureTests` |
| 0012 | The keyboard — including **PageUp / PageDown** (`MoveByViewport`) and **header-click sorting** with the settled cycle; **whole columns stay whole** under Shift+arrow, and **Shift+click on a header selects whole columns** (2026-09-25) | `GridKeyboardTests`, `MoveByViewportTests`, `SortWiringTests`, `WholeRangeSelectionTests`, `HeaderColumnSelectionTests`, `sizing.spec.mjs` |
| 0013 / 0021 | Fixed row height, `ViewportBox`, the Scrollbar Gutter | `ScrollbarGutterTests`, `scrollbar.spec.mjs` |
| 0015 | **The pager**, the page-context Ctrl+A, the off-screen-selection status line | `PagerTests` |
| 0016 | Auto width, `####`, **resize by dragging**, **the per-class `CellTextMetrics`** (with the full-width class and `−` `+` `#` charged wide, 2026-09-25), **defaults that cover the widest platform measured**, **Size to fit on a double-click over the whole Window**, **a press without movement reports nothing**, **whole columns resize together**, **the header's full need** (menu band, sort room, an em of slack), **the alignment enum** | `AutoWidthTests`, `CellTextMetricsClassTests`, `GridMetricsTests`, `ColumnGestureTests`, `SizeToFitTests`, `sizing.spec.mjs` |
| 0017 / 0026 | Both browser projects declared (`chrome`, `msedge`) | `playwright.config.mjs` |
| 0018 | Instance independence, per-instance ids | `features.spec.mjs` (two grids) |
| 0020 | Action and Template Columns | both layers |
| 0022 | `net10.0`, single-target (rewritten from `net8.0` on 2026-09-25) | the project file |
| 0025 | `FetchingGridSource` (+ copy rows, + distinct values delegate); `InMemoryGridSource.ReplaceRow` — the in-memory Consumer's apply (deliberately *not* ADR-0007's Overlay application; recorded there) | `GridSourceFetchTests`, `ReplaceRowTests` |
| 0027 / 0028 / 0029 | **`GridMetrics`, `GridDensity`, `ViewportSize.Stretch` (was `Fill`)**, inline Geometry Tokens, the token vocabulary, the forced-colors block | `GridMetricsTests`, `GridMetricsWiringTests` |
| 0031 | `dir="ltr"` on the root | `GridRenderingTests` |
| 0032 | **Header Groups** — rectangles, refusals, the band, group/leaf drag units | `HeaderGroupTests`, `HeaderGroupRenderingTests` |
| 0033 | **ARIA** — the root surface, absolute indices, `aria-activedescendant`, the live region; **the scroller kept out of the tab sequence**, with focus that reaches it handed to the root, and a key typed on it before then read as the root's (2026-09-26) | `AccessibilityTests`, `features.spec.mjs` (A11Y-17), `circuit.spec.mjs` (ED-22 on the Server host) |
| 0034 | **The verdict seam** — Accept/Flag/Reject at the commit, the editor holding under a Reject, the message channel and its popover, the fill's verdict, and the bundled ruleset. Not the hover trigger (ED-17b) | `ValidationTests`, `GridRulesetTests`, `features.spec.mjs` |
| 0043 | **Row Marks** — `GridColumn.MarkColumn`, a checkbox per Detail row and one in the header; `IRowMarks` as the Consumer's half and the `Marks` parameter on the push form; `RowMarkRules` (line-up, header state); `GridSource.From` keeping marks per base row and carrying them across `ReplaceRow`; `GridSource.Fetch` with a `RowMarkAdapter` (keys and snapshots as `RowMarkState`, counts from the server) and refused without one; Space in the Mark Column, the pager's "Mark all N rows", the count naming marks outside the filter; the `/marks` page's action reporting a partial result (added 2026-09-26) | `RowMarkRuleTests`, `RowMarkSourceTests`, `RowMarkFetchTests`, `RowMarkColumnTests`, `marks.spec.mjs` |
| 0036 | **The context menu** — the secondary click's meaning, the core's clipboard commands and the Consumer's, the keyboard trigger, and `GridCommand` losing its label | `ContextMenuTests`, `FilterChromeTests`, `features.spec.mjs` |
| 0035 | **The Editable declaration gates writes** — a paste or Ctrl+Enter fill covering a non-editable column is refused whole, and the fill refusal is no longer silent (CP-16) | `PasteRuleTests`, `ClipboardWiringTests`, `CellEditorTests` |
| 0021 (fifth entry) / 0029 | **The hover band** — the pointer reported by JavaScript only when it moves onto another row (offsets; the cell is resolved in C#), the band painted by the overlay like the Focus band, `HighlightHoverRow`, `--ex-row-hover-background` made real; off by default and then not even computed | `HoverBandTests`, `mud.spec.mjs` (UX-13, real mouse) |
| 0037 | **Entering a cell by key** — Space on a cell with several actions makes it Interactive with the keyboard left on the root (the arrows, Home and End choose; Space fires once and leaves; Enter, Tab and every other key leave and keep their meaning; `aria-activedescendant` names the chosen button, painted `ex-action-chosen`); Space on a Template cell hands its content one `FocusRequest` through `TemplateCellContext` and the Consumer's control focuses itself; Space on an editable cell opens Overwrite with the space; a held Space engages once; the action buttons leave the tab sequence | `InteractiveTests`, `ShippedStylesheetTests`, `features.spec.mjs` (real keys on `/cells`) |
| 0040 | Popovers inside the grid's box; **a popover with less than a row of room closes as a Cancel** (2026-09-25) | `PopoverKeyboardTests`, `PopoverRoomTests` |
| 0009 / 0044 | **The column's one popover** — `Alt+↓` and the ▾ open the commands above the filter; Excel's letters S, O, C and E; "Clear filter" as a command; Tab wrapping through both halves; the filter's search narrowing OK, "(Select All)", "Add current selection to filter" and a second condition (2026-09-26) | `MenuKeysTests`, `FilterPanelChoicesTests`, `FilterChromeTests`, `PopoverKeyboardTests`, `MudMenuTests`, `MudFilterPanelTests`, `popovers.spec.mjs`, `circuit.spec.mjs` |
| 0045 | **Pinning is suspended** while the Pinned block would leave the scrollable columns under 40px; the View State's count stands, and reorder and the pin commands keep reading it | `ColumnGeometryTests`, `PinnedColumnTests`, `sizing.spec.mjs` |
| 0030 | **`GridPresentationDefaults`** — the one cascaded value a Wrapper hands down: glyph widths at their measured size, a default Density, a default for the hover switch; an explicit parameter beats each | `GridPresentationDefaultsTests`, `PresentationDefaultsWiringTests` |

## Not in the core, by decision

- **0019 / 0030** `ExGrid.Fluxor` — not started. **`ExGrid.MudBlazor` exists** as the
  proof of the Wrapper boundary (`src/ExGrid.MudBlazor`, `tests/ExGrid.MudBlazor.Tests`,
  the `/mud` page and `mud.spec.mjs`): `MudExGridPaper` (elevation, corners, outline,
  bordered, a toolbar slot, Dense and Hover cascaded as defaults), Roboto's measured
  widths, the Visual Tokens mapped onto MudBlazor's palette variables in the Wrapper's
  stylesheet, and `MudGridChrome` filling two seams — the Cell Editor (a bare input in
  the core's box) and the loading bar. The filter panel and the column menu still fall
  back to the core's. Nothing in `src/ExGrid` references MudBlazor (PRE-4).
- ~~**Interactive mode's keyboard entry**~~ — **built** (2026-09-23), once
  [ADR-0037](adr/0037-entering-a-cell-never-reaches-into-content-the-core-did-not-render.md)
  made the decision this entry said was missing: the core never reaches into content it
  did not render. Over its own actions the keyboard stays on the root; a Template's
  control is asked, and focuses itself. The template's signature changed to receive a
  `TemplateCellContext` for it.
- **`--ex-row-hover-background`** (ADR-0029's hover token) could not work as written —
  rows are `pointer-events: none`, so `:hover` never matches them — and is now real by
  another route: the fifth allowlist entry of ADR-0021 reports the pointer moving onto another
  row, and the token colours an overlay band (the hover row above).
  ADR-0034's popover hover trigger consumes the same listener's other report, the rest.
- *(FN-20 landed late in the run: `GetFocusedValue()` serves the formula-bar role.)*

## Verification

| Layer | | State |
|---|---|---|
| 1 | `tests/ExGrid.Tests` | 46 files, **449 pass** (2026-09-24) |
| 2 | `tests/ExGrid.Components` | 43 files, **420 pass, 1 skipped by name** (2026-09-24): ST-1's 10⁶ case, which runs under `EXGRID_ST1_MILLION=1` (§22 Step 5). Including ST-1's randomised 500-operation run at 10³ rows, MEM-1's allocation invariant, PF-3's `RenderAllocationTests`, MEM-3's `ResourceDisposalTests` and ADR-0037's `InteractiveTests` |
| 3 | `tests/ExGrid.Browser` | **6 specs, 140 pass** (2026-09-23) on `chrome` and `msedge` together, on Windows 11 at 150% scaling, including ADR-0037's tests and the new VZ-14 block. `scrollbar.spec.mjs` again at 125%, **8 pass** (2026-09-24) (see the Windows paragraph above). And on macOS, `chrome` only (Chrome 153, headed): **69 pass** on 2026-09-23 after the Cmd+Enter fix the Windows run could not have seen, and again on 2026-09-24 after the layer-2 harness's fixes, which touch the action buttons, the header and every cell's id — so those fixes have not yet met `msedge`. The harness's layer-3 half (**8 specs, 78 tests**) had run only on the container's bundled Chromium: **76 pass, 2 skipped**, and the soak separately (2026-09-24). **Then in CI** (ADR-0041), on Linux, headed under xvfb, on the runner's installed `chrome` and `msedge`: **256 pass, 4 skipped** — VZ-14 and the soak, once per browser — on 2026-09-24, on PR #15's head and again on `main` after its merge. That is the first run of everything added on 2026-09-24 on the two target browsers |
| — | `verification/2026-09-01/` | layer logs + `results.md` with the pass/blocked ledger |
| — | `verification/2026-09-23-windows/` | the Windows layer-3 run: `results.md`, the 150% and 125% logs, `metrics.json` |
| — | `verification/2026-09-23-macos/`, `verification/2026-09-24-macos/` | the macOS `chrome` runs' `metrics.json` — the second with MEM-7 under `layer2` |

**2026-09-24, PRE-4 rewritten.** Its check grepped all of `src/` for `Mud` and `Fluxor`. Since
`src/ExGrid.MudBlazor/` lives there (ADR-0019), and a comment in the core names `MudDataGrid`,
the check as written could no longer pass, although the property held. It now names the core,
`src/ExGrid/`: no project reference, and no `using` of `MudBlazor` or `Fluxor`. Both come back
clean. The property is unchanged: the dependency points one way.

## What is left, in the order that costs least

1. **The measurement harness's layer-3 half — written (2026-09-24), not yet run where it
   counts.** It has run only on the container's Chromium (above). What discharges it is a
   Step 4 run on `chrome` and `msedge`, on Windows or Linux, with the soak (`EXGRID_SOAK=1`)
   once per browser and `metrics.json` / `console.json` filed. That run also takes the
   2026-09-24 layer-2 fixes to `msedge` for the first time. Then a fresh `spikes/render-bench`
   entry (PF-8), on real hardware. *(2026-09-24: CI has since run the harness on `chrome` and
   `msedge` on Linux, headed, and kept `console.json` and `metrics.json` as the run's
   artifacts — the 2026-09-24 fixes have met `msedge`. Still owed: the soak on both browsers,
   which the weekly long run does, and the filed record, which a CI artifact is not.)*
   *(2026-10-10: the soak passed on `chrome` and `msedge` against both hosts in the weekly long
   run of 2026-10-05, run 37260612255 at 60d522e. Still owed: the filed record, and PF-8 on real
   hardware.)*
2. ~~**VZ-14 at 125%.**~~ Discharged on 2026-09-24 on a Windows desktop at 125%, on both
   browsers. (The Edge run and VZ-10, which this item used to hold, were discharged on
   2026-09-01.)
3. The hover band's owed numbers — a `spikes/render-bench` mode for the band's paint.
   *(The Blazor Server host this item also asked for exists since 2026-09-25, and SRV-6 has
   been measured on it in a container; a run on real hardware is still owed. Interactive
   mode's keyboard entry, which headed this item, is built — ADR-0037.)*
4. ~~**ADR-0037's layer 3 on the two target browsers.**~~ Discharged on 2026-09-23 by
   the Windows run: KB-20 to KB-27, A11Y-17 and UX-14 pass on `chrome` and `msedge`.
5. **`ExGrid.MudBlazor`'s remaining seams, and Row Stripes — decided and built
   2026-09-24; verified on the container's Chromium only.** A grilling session settled them; the decisions are
   [ADR-0038](adr/0038-row-stripes-are-painted-from-the-rows-absolute-position.md) (Row
   Stripes, reversing ADR-0027's "not offered"),
   [ADR-0039](adr/0039-a-popover-takes-the-keyboard-and-may-hold-popups-of-its-own.md)
   (popovers take the keyboard — a core gap: no key opened the column menu, and no popover
   could be used without a pointer — and may hold Inner Popups, replacing ADR-0030's "never a
   `MudPopover`"), and the rewritten passages of ADR-0010/0012/0021/0027/0029/0030/0036. The
   criteria are FN-17/21, UX-15/16, A11Y-19, KB-28..32, RR-13 and the new §23 (WR-1..9); the
   work is ticketed as vertical slices (GitHub issues #3–#12) and built against a
   proof-of-concept MudBlazor page in the DemoHost. **Built so far:** the core half of
   ADR-0039 — `Alt+↓`, `FocusRequest` on the three popover contexts, the built-in Chrome
   focusing its first item, focus returned to the root on every close, the menus' and
   panel's roles and names (KB-28/29/32, A11Y-19), and the keys inside the built-in menus
   and panel (KB-30/31): the table is `MenuKeys`, public so a substituted Chrome answers to
   the same one; the panel's Tab wraps through two focus sentinels, and its Enter is the
   browser's implicit form submission, which leaves a composing IME alone where a Blazor key
   handler could not tell (`MenuKeysTests`, `PopoverKeyboardTests`, `features.spec.mjs`).
   Each popover is now keyed by its opening: one column's menu opened straight over
   another's kept the same buttons through the diff, and nothing took the keyboard. And Row
   Stripes in the core (UX-15/16, RR-13; `RowStripeTests`, `stripes.spec.mjs` on the new
   `/stripes` page, which reads the painted colours from a screenshot). The proof-of-concept
   page, `/mud-app`, and the `/features?chrome=mud` switch are in (T4, `mud-app.spec.mjs`,
   WR-8's `WrapperScriptTests`). `MudGridChrome` fills the column menu and the Context Menu
   with `MudButton` items, a Material icon each, and answers to `MenuKeys` (WR-4;
   `MudMenuTests`), and `popovers.spec.mjs` runs every popover test under both Chromes
   (WR-5 for the menus). Two core defects surfaced on the way and are fixed: a command a
   substituted Chrome invoked never closed its menu — the core now hands out commands that
   close themselves, since closing is its decision (ADR-0010) — and "Pin up to this column"
   was offered where the pin would cut a Header Group, which the grid then refused by
   taking the page down; it is now disabled there (ADR-0032). `MudExGridPaper.Striped`
   cascades Row Stripes and the Wrapper's stylesheet colours them from the palette's
   table-stripe colour (WR-6; `MudExGridPaperTests`, `mud-app.spec.mjs`). `MudGridChrome`
   now fills the filter panel as well (WR-1/2/3). A value list of `MudCheckBox`es with a
   search and a Blank entry. A condition form: a `MudSelect` operator offering exactly
   `Allowed`, and the type's own operand control, with Apply held until the operand is
   given. MudBlazor's words where it has keys, the Chrome's `Label` elsewhere
   (`MudFilterPanelTests`). What a panel's choices mean moved into the core as
   `FilterPanelChoices`, which the built-in panel uses too, so the same choices make the
   same `FilterSpec` under either Chrome. Two more core defects surfaced and are fixed:
   - `In` chosen in the built-in condition form applied a clause the engine refuses.
   - Escape from inside a popover returned focus before the render that removed it, so a
     `MudSelect` pulled focus back and it fell to `<body>`.

   `FilterPanelContext` carries the column's `Format`, so a substituted value list shows
   values as the cells do.

   **Two decisions, taken 2026-09-24 after the browser disagreed with the records:**
   - **Popovers stay inside their grid's box** ([ADR-0040](adr/0040-a-popover-stays-inside-its-grids-box.md)).
     A grid inside a `MudDialog` had its column menu cut off by the dialog's scrolling
     content. ADR-0017/0018/0021 had chosen the Popover API and CSS Anchor Positioning, "no
     script". What had been built was an in-root popover, and nothing recorded the
     difference. Measured: the top layer needs `showPopover()` for key- and right-click
     opens, and buries MudBlazor's popups. `position: fixed` with anchors is still cut by
     `.mud-dialog`'s transform. The core now writes each popover's `max-height` from its own
     geometry, and the Context Menu opens on the side with more room. A panel's value list
     is what gives. WR-7's dialog clause and UX-11 were restated to match.
   - **Escape closes an Inner Popup first**, as ADR-0039 intended. Its prediction of *how*
     was wrong: MudBlazor keeps DOM focus on the control while its popup is open, so the
     grid took the Escape and closed both. The contexts now carry `InnerPopupChanged`, the
     Wrapper's panel reports its selects' lists and its date calendar, and while one is open
     the capture-phase gate leaves a descendant's Escape to it. This is one more state of an
     allowlisted listener, not a new use.

   FN-21 is now observed clause by clause (`popovers.spec.mjs`), `ModalOverlay` included
   (`/features?chrome=mud&modal=1`).

   **A review of the whole change** (the `code-review` pass, 10 findings) fixed:
   - A menu opened while a panel's value list was still loading never took the keyboard.
     When that list's command finally completed, it closed the menu standing by then. A
     command now closes only the popover it ran from, and every opening starts clean.
   - A popover on the rightmost column could hang past the grid's right edge. It is now
     clamped by the widest it may grow, and that 320px moved from the stylesheet to the
     inline style beside its 200px floor. The Wrapper panel's own 240px minimum went.
   - TooMany overwrote an operator the user had picked while the list loaded.
   - A substituted panel's value list was queried twice per opening.
   - A reported Inner Popup could outlive its popover.

   Not taken: a per-opening cache of the command list, since the context was already
   rebuilt on every render.

   **Runs still owed, not passed:**
   - ~~Every layer-3 test added on 2026-09-24 has run only on the container's bundled
     Chromium.~~ Discharged the same day by CI: `popovers.spec.mjs`, `stripes.spec.mjs`,
     `mud-app.spec.mjs` and the ADR-0039 half of `features.spec.mjs` pass on `chrome` and
     `msedge`, on Linux, headed (the Verification table).
   - ~~The soak (`EXGRID_SOAK=1`), per browser.~~ Discharged by CI's weekly long run of
     2026-10-05 (run 37260612255), on both browsers and both hosts. No record of it is filed
     under `verification/` yet (item 1).
   - A real IME is not reachable from the container: the claim that Enter confirming a
     candidate does not apply a filter rests on the browser's implicit-submission rule, and
     is owed a manual check with a Japanese IME on both browsers. *(Still owed on 2026-10-10.
     The fifteenth and sixteenth Windows runs used a real IME in the Cell Editor, the Formula
     Bar, the Name Box, Find and the Keyboard Field, and never in a filter panel.)*

6. **The first prerelease, `0.1.0-beta.1`: published 2026-09-25**
   ([ADR-0042](adr/0042-prereleases-ship-before-sign-off-and-only-a-stable-version-waits-for-it.md)).
   Decided with the user: a version with a prerelease suffix ships before Step 7, and one
   without waits for it. The line is `0.1.0-beta.N`, under MIT, published by Trusted
   Publishing. Built on 2026-09-24:
   - `LICENSE` and the package metadata;
   - a readme per package, whose examples `tests/ExGrid.PackageSmoke` compiles from the packed
     packages;
   - the Wrapper's dependency on exactly its core version;
   - an XML doc comment on every public member;
   - CI's `package` job, and `release.yml`.

   The tag's run (`release.yml`, run 36074956853) passed every layer. It then pushed both
   packages and their symbol packages to nuget.org. Both restore from nuget.org with the
   readme, the XML documentation and the static web assets. The run's last step failed.
   The tag had been made on GitHub's release page, which made a release with it, and the
   workflow refused to make a second one. That release has neither the packages nor the note.
   The workflow now completes such a release instead. The one for `0.1.0-beta.1` needs deleting
   (the tag stays) and the failed job re-running; the re-run's push skips the packages
   nuget.org already has. *(Still so on 2026-10-10: that release stands, and run 36074956853
   has not been re-run.)*

   A stable version still owes a review of the public C# surface, which nothing has decided
   yet (ADR-0042).

   **From the next tag, the whole family ships** (decided with the user, 2026-10-03; ADR-0042's
   "The family ships together"). ExSheet's three packages, ExPivot's three and the two data
   packages join ExGrid's two, all at the tag's one version. The package check packs the ten
   into the release feed and fails if it holds anything else; `release.yml` expects the ten and
   their symbol packages. §27, §29 and §30 still judge their products, and never gate ExGrid.
   *(2026-10-10: it did. `0.1.0-beta.2` was tagged at 92bdfae on 2026-10-04, and its release
   run, 37243418696, passed.)*

7. **`MaxWidth` bounds only what the grid computes (ADR-0016, FN-12 rewritten 2026-09-25).**
   Decided with the user. The contradiction came up while documenting the public API. ADR-0016
   let a drag go past `MaxWidth`, but FN-12 refused any Fixed width above it. So a Consumer who
   recorded the drag as a Fixed width got an exception, and so did restoring a saved view. The
   DemoHost worked around it by raising `MaxWidth` with each drag. Now a Fixed width is refused
   only below `MinWidth`. The workaround is gone: the DemoHost records the drag with the
   column's own bounds. Pinned by `ColumnWidthTests` (layer 1), by a round trip in
   `ColumnGestureTests` (layer 2), and by a real drag to 500px in `gestures.spec.mjs` (layer 3,
   run on the container's Chromium only; it fails with the core change reverted).

   **Closed the same day:** FN-12's pass condition says the refusal names the column. That had
   never held, because the spec is built before its column. Decided with the user: the column
   refuses now, naming itself, and a `default(ColumnWidthSpec)` too (`GridColumnTests`,
   `ActionAndTemplateColumnTests`). This also turned up an older defect: an Auto
   `ColumnWidth` could not be printed. The record's own `ToString` read `FixedPx`, which
   throws for Auto, so logging a width, or the refusal's own message, threw instead. It
   prints `Auto` or `Fixed(120px)` now.

8. **Blazor Server — what layer 3 found on 2026-09-25, and where each stands.** Each is a
   keyboard, focus or scroll hand-off that WebAssembly completes within one frame and a
   circuit completes a round trip later.
   - ~~**A key pressed straight after a key that opens a popover**~~ reached the grid. Fixed
     (ADR-0010 widened, KB-33): those keys are held until the popover holds DOM focus, then
     handed to it — and the hold now lasts until focus has landed even when the answer
     arrives first, which a 1-in-10 race had shown it did not.
   - ~~**A menu resolved its keys against the item holding DOM focus**~~, so `↓` `Enter`
     ran `Copy` for `Copy with headers`. Fixed (ADR-0039, KB-34): the core keeps the menu's
     place and the contexts carry `ResolveKey`; the MudBlazor Wrapper asks it too.
   - ~~**A focus request from an earlier render landing after a newer gesture**~~. Fixed
     (ADR-0039): a dismissing press also hands the keyboard back after the render that
     removes the popover.
   - **An Escape straight after an Inner Popup opens**: the predicted cause — the gate taking
     it — was wrong, and the fix decided for it (reading `aria-expanded`) was withdrawn
     unbuilt (ADR-0039 records why). What layer 3 saw is MudBlazor's date picker ignoring an
     Escape while DOM focus is still on its own button. KB-35 holds the grid to its half.
   - ~~**`Ctrl+End` then `Ctrl+Home` pressed faster than the scroll round trip**~~ left the
     Focus on (0, 0) and the view at the last row. `scrollbar.spec.mjs`, "at every zoom
     level, after moving again", failed 7 runs in 12 on the Server host and never on
     WebAssembly. Root cause, from a log of the grid's reads, writes and reveals: a read of
     the scroll offset and a write crossed on the circuit's wire. The browser answered the
     read (the top, after `Ctrl+Home`) before the write for the next `Ctrl+End` reached it,
     and the answer reached the grid after that write was sent. So the grid took it as the
     newest word on where the scroller was going. The next `Ctrl+Home` then judged the top
     "already there" and wrote nothing, and a Focus that no longer changes arms no second
     reveal. It is the in-flight-write gap ADR-0012 closed for WebAssembly, reopened by
     arrival order: there, a read cannot be answered before an earlier write has run.
     Fixed (ADR-0012): a read that a write overtook is set aside and asked again. The same
     answer could also pull an edge auto-scroll's model back mid-drag. Layer 2 pins it
     (`GoToStartTests`); the test passes 20 runs in 20 on the Server host.
   - ~~**A key typed straight after the Escape that closed a popover**~~ was lost: the core
     handed focus back only after the render that removed the popover, and for that round
     trip DOM focus was on `body`, where the root's listener cannot hear it. Found after
     `main` was merged: A11Y-19 under the mud Chrome failed 3 runs in 6 on the Server host.
     Fixed (ADR-0039): the core also asks for the root's focus before that render. Layer 2
     pins it (`PopoverKeyboardTests`), and the test now waits for the panel to close before
     clicking the cell under it. It passes 30 runs in 30 on the Server host.
   - ~~**Found by CI's first Server-host run (on `chrome` and `msedge`)**~~. 274 passed and
     6 failed, all on MudBlazor pages. Two failures were real, each on both browsers:
     - Escape, Escape straight after an Inner Popup left the panel standing. The gate learned
       of the popup's closing a round trip late.
     - A value typed with its Enter applied nothing. Enter waited on Apply's disabled
       button, which the circuit enables a round trip after the value.

     Both are fixed (ADR-0039) and pinned at a 150 ms round trip in `circuit.spec.mjs`. The
     other failures were tests reading a Focus or a paint straight after a click, and they
     now wait for it.
   - ~~**A menu taking the keyboard pulled back a scroll the user had given it**~~. Found by
     a later CI run, UX-11 on `msedge`: opened by pointer, a menu is focused a round trip
     late, and `focus()` scrolled a menu taller than its grid back to its top. The opening
     focus no longer scrolls, under both Chromes (ADR-0039). It is pinned in layer 2 in
     both suites, and at a 150 ms round trip in `circuit.spec.mjs`.

   **SRV-2 met on 2026-09-25** by CI's Server-host layer-3 job (run 36195865300, commit
   `9172049`). That was `chrome` and `msedge` on Linux, headed. Everything passed; the only
   tests skipped were the three the WebAssembly run skips too: SRV-6, which runs when asked
   for; the MEM-5/MEM-6 soak, which runs weekly; and VZ-14, which is Windows only.
   Still owed before the §24 claim is made:
   - ~~The soak on the Server host, which is MEM-6 read from the server process. It comes
     with CI's next long run.~~ Discharged by the long run of 2026-10-05 (run 37260612255), on
     both browsers.
   - The declaration ADR, which also rewrites ADR-0017's WebAssembly premise.

9. ~~**Found on 2026-09-26, not fixed: `ViewportHeight = Fill` paints no rows in a sized box.**~~
   *Fixed the same day (ADR-0028 rewritten with the user): the core gives its own elements the
   parent's size, names a parent with no height in a warning, and the paper's toolbar no longer
   shrinks. `fill.spec.mjs` pins it (VZ-12a, VZ-12b, WR-7a, and UX-11a at last), on both hosts.*
   A grid with a Fill height inside a 300px box settled at a 28px scroller, header only, with no
   row painted (checked on the container's Chromium against de5e62c, before this run's changes).
   The scroller takes its height from its content, and under Fill the reported height is that
   same box, so the loop settles on the header. Only CSS on the grid's internal elements breaks
   it, which ADR-0029/0030 keep off-limits. The DemoHost has no Fill-height page, and that is
   also why UX-11a has no layer-3 test yet. Which element takes the box's height is a decision
   for ADR-0028, not a fix to make quietly.

10. ~~**Observed on 2026-09-26: the column menu buttons are tab stops.**~~ On `/features`, Shift+Tab
   from after the grid lands on a ▾, not on the root. A11Y-4's test tolerates this ("the next
   stop can be … the menu buttons"), but its criterion says focus "leaves the grid entirely".
   The criterion and the test disagree, and the disagreement is not resolved here.
   *Settled with the user on 2026-10-02 (ADR-0080): every ▾ carries `tabindex="-1"` on every
   grid, and A11Y-4 now reads that no ▾ is reached by Tab or Shift+Tab. `KeyboardFieldTests`,
   `key-field.spec.mjs`, `features.spec.mjs` and `MudMenuTests` pin it.*

## Where the exit criteria stand

**No open question in §21.** Settled this run, each with its trigger: the Action-Column
copy (empty cell, ADR-0005), the editor's classes and tokens (with the editor), the
header-click sort cycle (recorded in ADR-0012). Still reserved, triggers unfired: the
fill handle, `--ex-selection-outline`, ExSheet's shape, the column band. *(Since then:
right-click was settled by the Context Menu, ADR-0036; the Wrapper seam order by the
package's start and ADR-0039, 2026-09-24.)*

## ExPivot and the family's data (2026-10-01)

*(Built on `claude/expivot-mudblazor-wrapper-j25225`. Decided with the user in the ExPivot
grilling, Q1 to Q63: [ADR-0059](adr/0059-expivot-is-a-pivot-table-drawn-by-exgrid-as-its-consumer.md)
to [ADR-0069](adr/0069-the-demo-pages-call-a-demo-api-server-both-hosts-share.md). §29 and §30 of the
Definition of Done judge ExPivot and the data packages, and never gate ExGrid. §26's DC-63 to DC-66
are the core changes, and those do gate it.)*

**What exists.**

- **`ExGrid.Data`, the Snapshot** (ADR-0064):
  - six kinds, with a Blank in each; text as a dictionary in first-appearance order; Decimal as
    scaled 64-bit integers per segment;
  - built from objects through typed accessors, from a CSV under a declared Schema or a suggested
    one the user confirms, from a `DbDataReader`, and from columns;
  - loaded in slices, with progress and cancellation;
  - Change Batches by a Record Key, which share every segment they do not touch.
- **`ExGrid.Data.Arrow`** (ADR-0065): Arrow's IPC stream and file read into a Snapshot, and a
  Snapshot written as an uncompressed stream. Codecs are used only when handed in. Its type table
  covers what `pyarrow`, Polars and DuckDB write.
- **`ExPivot.Engine` over the Snapshot** (ADR-0060, ADR-0066, ADR-0067):
  - Leaf Aggregates, with exact sums held as 128-bit integers;
  - the Pivot Source: `PivotSource.From`, `PivotSource.Fetch`, `PivotJson` and Source Versions;
  - the Order Key and the date parts;
  - Change Batches folded live, each leaf held to a fresh aggregation to the last bit.
- **`ExPivot`**:
  - asking the source, with generations, cancellation and discarded answers; the caps;
  - Defer Layout Update; the toolbar and the Layout menu;
  - Show Details in a tab, in a dialog, or handed to the Consumer;
  - Excel's Japanese words;
  - live gathering, the Change Highlight and the Stale Report.
- **`ExPivot.MudBlazor`** draws every surface, including the toolbar, the Details tabs (in
  `MudTabs`) and the dialog's content.
- **ExGrid's Change Highlight** (ADR-0068, DC-64 to DC-66).
- **The demo API server, `samples/ExGrid.DemoApi`** (ADR-0069): SQLite holding money as integer
  cents, the trades as Arrow, a Pivot Source answered in SQL, and live changes said over SignalR.
- **The six pages:** `/pivot`, `/pivot-csv`, `/pivot-db`, `/pivot-live`, `/pivot-risk` and
  `/grid-live`. Each shows the code it runs, read from its own source.

```sh
dotnet test ExGrid.slnx                 # 5,384 pass, 0 failed; 8 skipped: the explicit measurements and ST-1's 10⁶ case
                                        # ExGrid 838 + 1,095, ExGrid.MudBlazor 91, ExSheet 1,969 + 290,
                                        # ExGrid.Data 248, ExGrid.Data.Arrow 182, ExPivot.Engine 304,
                                        # ExPivot 178, ExPivot.MudBlazor 53, the demo API server 136
tests/ExGrid.PackageSmoke/check.sh      # passed: the data packages and ExPivot's in .pivot-feed, none in .feed
                                        # (one feed for all ten since 2026-10-03, ADR-0042)
npx playwright test pivot.spec.mjs pivot-csv.spec.mjs pivot-db.spec.mjs pivot-live.spec.mjs \
    pivot-risk.spec.mjs grid-live.spec.mjs navigation.spec.mjs   # 80 pass on each host
```

**Layer 3 ran here in part, targeted.** It ran on Linux, headed under xvfb, against the container's
Chromium, because neither Chrome nor Edge is installed here. The specs of the six pages and
navigation pass on both hosts, under both pivot Chromes, with a clean console. CI's full run has not
seen this branch: it runs on `main` and on pull requests.

**Measured, never gated** (`verification/2026-10-01-linux-measure`, a 4-vCPU container; PV-21 and
DA-17). Over a million trades in a published WebAssembly build:

| Met | Missed |
|---|---|
| A collapse, a sort or a form, laid out from the answer held: 28–32 ms | A CSV of a million rows: 14.6 s against 4 s (955 ms on CoreCLR, against about 0.4 s); 4.0 s and 491 ms once made faster, below |
| 1,000 changes on screen: 43 ms | The page blocked for at most 50 ms: a gesture's worst is about 0.2 s, and a question near the cap held the page for 1.9 s (137 ms once sliced, below) |
| A new question for a 50-leaf report: a median of 159–229 ms | A new question near the 200,000-leaf cap: 2.7 s |

**Since then, decided with the user and built** (2026-10-01):

- **The cap stays 200,000, and the work after an answer is sliced** (ADR-0066, PV-40, ticket 22).
  Near the cap the longest task fell from 1.65 s to 137 ms, and the answer took 2.79 s rather
  than 2.59, measured back to back (`verification/2026-10-01-linux-measure-sliced`). What remains
  over 50 ms is the browser runtime's full collections, about 70 ms inside a slice, and the turn
  that puts the report on screen.
- **A Consumer gives a grid the keyboard back, and hears an Escape that leaves it**
  ([ADR-0070](adr/0070-a-consumer-gives-the-keyboard-back-and-hears-escape-leave.md), DC-61,
  DC-62, PV-39, ticket 21). Show Details' dialog closes on Escape, and the report takes the
  keyboard back however it closes. Building it found that a held Escape peeled a layer per
  repeat, cancelling a half-typed formula under its closing list. A held Escape is now one
  press in every grid (ADR-0012, KB-44).
- **A press into a details view's records keeps the keyboard there** (2026-10-02 and 2026-10-03:
  ADR-0070, "Handed on, not taken"; ADR-0021's sixth decision about focus made in script; ADR-0033's
  note of 2026-10-03; DC-61, A11Y-20, PV-41, ticket 21). Two races on the Server host, told apart by
  a probe with no latency injected. A press made as soon as the dialog showed lost the keyboard to
  Close's late focus in 2 runs of 6. A press made before the records' root was a tab stop left the
  keyboard on their scroller for good; that was the test failure seen, in 1 run of 3. ExGrid's
  `HandKeyboardToAsync` now hands the keyboard on only while DOM focus is still inside the grid or
  on nothing, and ExPivot's dialog Close and details tabs take it that way under both Chromes. The
  scroller's hand-off to the root is owed until a render has made the root a tab stop, in every
  grid.
- **The CSV read is faster** (ExGrid.Data's ticket 07, `verification/2026-10-01-linux-measure-csv`;
  ADR-0064, refined). A million rows read in 4.0 s in a published WebAssembly build, from 12.9 s,
  and in 491 ms on CoreCLR, from 692, both on the same machine. Every rule of the read and every
  refusal is unchanged. The work found and fixed a crash: a Blank early in a numeric, date or
  Boolean column that later outgrew its first room. Slices now yield with `Task.Yield()` in a
  browser too, which paints a frame a slice at a ninth of a 1 ms delay's cost.

DA-17: a million records built from objects in 426 ms on CoreCLR and 3.8 s in the browser; read
from a CSV in 955 ms and 14.6 s; read from Arrow in 475 ms and 3.7 s.

**Found by building the pages, and fixed.**

- **A new `Source` and a new `Layout`, handed in by one render, were refused**: the new source was
  checked against the layout on screen. On the Server host this killed the circuit when `/pivot-csv`
  read a second file.
- **A details tab's records read under the report's headings**, because the report's sticky header
  painted over them.
- **A report's exact values took the scale a source wrote them at**, so a SQL source's `75.60` and
  the bundled source's `75.6` copied differently.
- **Live changes spread over a million trades almost never reached `/grid-live`'s rows.**

**Not done.**

- **Near the cap, PV-21's 50 ms is still missed**, by the browser runtime's collections inside a
  slice. Fewer allocations in the pass and the cube would shorten them; that is not a ticket yet.
- **Excel's behaviour was read, not observed.** Every reading is listed in
  `docs/specs/expivot/excel-behaviours.md`, for a run beside Excel on Windows (ticket 07).
- **Edge, Windows and a real IME have run none of it.**

## ExGrid's live data (2026-10-06)

*(Built on `claude/exgrid-live-data`. Decided with the user in the grilling of 2026-10-05 and the
decisions D1 to D10, P1 and P2 of 2026-10-06:
[ADR-0140](adr/0140-a-row-key-names-a-row-across-versions-and-the-grid-repaints-a-changed-row-in-place.md),
[ADR-0141](adr/0141-exgrids-bundled-sources-take-live-data-by-row-key-on-expivots-rules.md) and
[ADR-0142](adr/0142-a-write-lands-as-the-user-entered-it-and-a-change-under-the-editor-is-told.md). §32 of the
Definition of Done judges it, and gates ExGrid; PV-42 and PV-43 judge ExPivot's key.)*

**What exists.**

- **The Row Key** (ADR-0140): `RowKey` on the grid, or a source's; a row's component is kept by it,
  so a changed row repaints in place. A repeated or null key is refused by name. ExPivot names its
  report rows by `PivotRowKey`, made with the row.
- **`GridSource.From` with a Row Key** (ADR-0141): Change Batches, a whole new list that sets the
  order, an incremental requery held equal to `GridQueryEngine.Apply` by a property test, gathering
  on ExPivot's rules, the Change Highlight by painted text, vouching for distinct rows, and
  `PublishGathered`.
- **`GridSource.Fetch` hears that its data moved on**: `NotifyChanged`, with the added keys when the
  Consumer knows them; the server's order token; `/grid-live` is built on it.
- **`/grid-live-local`**: a million trades in the browser, fed Change Batches.
- **Writes refused when what the user saw changed** (ADR-0142 as decided on 2026-10-05): the Cell
  Editor's commit, Actions, paste, fills and clears, each judged against the render it was taken
  against, keyboard gestures included; the user's own writes count as seen.
  - *(2026-10-07: replaced, and built on `claude/live-data-next-cc`, tickets 04 to 13 of
    `docs/specs/live-data`.)* ADR-0142 was rewritten: a write lands as the user entered it, on the row it
    was aimed at; only a change under the open editor is told, by an Overwrite Notice; an order move is
    refused as `OrderMoved`; the editor outlives an order move and follows its row (ADR-0011's note).
    ADR-0160: the grid holds no row beyond its Window, checked by weak references. ADR-0161, on that track
    (for ExPivot, replaced on 2026-10-08, below): ExPivot made its next cube and report from the last, its
    rows held no value and no report, its Change Highlight kept times, and a redraw out of memory left the
    report stale. ADR-0141: a pushed Window may vouch
    (`VouchesDistinctRows`), and the painted rows are checked. ADR-0130: the Selection Summary walks only
    while figures stand, over the Selection's positions.
  - **Measured before and after** on that track's code
    ([`2026-10-07-macos-live-update-costs-after`](../verification/2026-10-07-macos-live-update-costs-after/README.md)):
    - ExPivot's live redraw at 401,001 report rows went from 268.5 ms to 19.0 ms on CoreCLR, and from one
      redraw at 2,338 ms followed by running out of memory to 532 ms in the browser.
    - The heap stays flat.
    - The collector's pause per redraw in steady state fell from 28.6 ms to 0.57 ms.
    - **Slower:** a redraw laid out afresh (one in 64, at the source's compaction) costs more than every
      redraw did before, 394.5 ms against 268.5 at 401,001 rows on CoreCLR. Most of it is comparing every
      row for the Change Highlight, as ADR-0161 chose.
  - *(2026-10-06 on the Codex track, `claude/live-data-next`.)* The same tickets, decided separately:
    ADR-0151 to ADR-0153 — a server computes the Pivot Report and sends Windows and their changes, the browser runs
    the same incremental engine for local data, Report Versions and versioned Copy, Summary and Details,
    and detached display rows. Its records:
    [the costs](../verification/2026-10-06-macos-live-update-costs/README.md),
    [the memory diagnosis](../verification/2026-10-06-macos-pivot-memory/README.md),
    [the boundary A/B](../verification/2026-10-06-macos-pivot-boundary-bench/README.md) and
    [after](../verification/2026-10-06-macos-live-report-after/README.md).
  - *(2026-10-08: merged on `claude/live-data-best`.)* Comparing the two tracks, the user took this
    track's grid and the Codex track's ExPivot; ADR-0161 kept only its out-of-memory rule. The comparison's
    review found, and the merge fixed:
    - a replaced Source took writes aimed at the old one, on both tracks for ExSheet's documents and on
      this one for every grid: now refused as `SourceChanged`, the open editor discarded, the Selection
      dropped (LV-32);
    - keys aimed with a dropped Selection typed into the first painted cell: now they open nothing (LV-33);
    - D1 let an upstream change through after a write that left the text as it was, and gave the notice
      on one host and not the other: now it settles at the first Window after the write's handler (LV-17);
    - `RowGone` named a row that had only left the Window: now `RowLeftTheWindow` (LV-20);
    - an action press read its row at the release, and a macOS Ctrl+click fired later for nobody (LV-12);
    - ExPivot's cancelled computation threw its incremental state away (150,002 rows read for a
      one-record update after one cancellation), its Details stopped after two layout gestures, its
      Change Highlight ran on the server's clock, Window Changes were trusted whole, a row of another report was
      read as this one's, and a throwing server Order Key lost its name: each fixed (LV-26 to LV-30);
    - `SlicedBuildTests` failed one run in four on a race in its helper: fixed, 25 runs of 25.
  - **Measured on the merged code** *(2026-10-10, against `main`,
    [`2026-10-10-linux-merged-live-costs`](../verification/2026-10-10-linux-merged-live-costs/README.md))*:
    ExPivot's live update is 3 to 147 times faster than `main`'s (at 401,001 report rows 4.3 against 627 ms
    at one change, 22 against 630 at 1,000), and collects nothing where `main` paused about 150 ms an
    update. In the browser the heap levels off at 137.6 MiB at 101,001 rows, where `main` grew by 41.2 MiB
    a redraw, and holds 420 MiB at 401,001, where `main` ran out of memory on the 7th redraw (PV-48, LV-23).
    The first report stays slower than `main`'s: 194–214 against 136–144 ms at 101,001 rows, 903–949 against
    662–826 at 401,001. A new question over a million trades in the browser was 16 to 60% slower (PV-21),
    where the live update is about even. ExGrid's own live update is unchanged.
  - *(2026-10-10, the afternoon: a new question's extra, fixed as the user chose.)* A question made two
    pieces of the state a live update folds into, though only an update reads them: the chain of every
    stored row to its leaf, and each total's list of leaves. The pass now keeps each leaf's first row
    (`234da46`), and the first update makes the lists (`f2bc6a4`); `FirstRecordTests` and `MemberListsTests`
    pin them. Measured back to back on a new container: in the browser the page's own questions and those
    up to 1,350 combinations are even with `main`'s, and from 27,000 combinations up 10–35% slower, where
    they were up to 55% slower; on CoreCLR level with `main` or faster, except at 198,450 combinations. The
    first live update after a new question now makes the lists, once: about 0.9 s in the browser at
    198,450 combinations, sliced (`verification/2026-10-10-linux-merged-live-costs`, "After the fixes").
  - *(2026-10-09: reviewed and grilled.)* An independent review of the merged pull request found, and
    this branch fixed, with a failing test first for each:
    - a press made while an asynchronous `OnEdit` was heard committed the same edit again, and could leave
      the grid believing it still heard a commit, so a replaced Source no longer discarded the editor: now
      one commit at a time (ADR-0142, ED-12);
    - scrolling a large ExPivot report and then shrinking it — a layout change, a collapse, a filter,
      newer data — handed the grid a Window past the report's end, and the exception ended the circuit:
      now the Window is clamped into the report (ADR-0151, LV-25);
    - with nothing selected, keys at a replaced Source's paint typed into the new source; what the editor
      saw could be read from another row (ADR-0011, ADR-0142, LV-17, LV-33);
    - ExPivot's refused Copy and Summary spoke English only (ADR-0060, PV-33);
    - the first report took 5.1 s at 401,001 rows against `main`'s 0.6 s: the engine copied its state
      eagerly and laid the report out twice; now 0.8 s (ADR-0153).

    The user then decided, in a grilling: the report source keeps the reports the Change Highlight
    compares (ADR-0153, LV-22); a press made while a commit is heard waits for it; a Stale Report's cells
    are marked and Copy from it is refused (ADR-0067, PV-49); a mark press aimed at an older display marks
    the page it named, or is refused and told (ADR-0043, MK-9); `ExPivot.Source` takes a Pivot Source again
    and `ReportSource` a report source, and a delta is Window Changes (ADR-0152, `CONTEXT.md`); and
    confirmed what the merge had settled — the layout change refused when memory runs out, Details by
    Source Version, a full-refresh source asked to refresh, D1's limits and a placement's.

**Found by building it, and fixed.**

- **`CellTextMetrics.ToString()` overflowed the stack**: its `Bold` is metrics of the same type.
- **An integer Row Key collided with a Placeholder's key.**
- **A gathered publication posted to a source's context ran again** after a write had taken its
  changes, publishing later changes before their interval was up.
- **`/grid-live` missed a trade cancelled after the Window** while a Selection reached past it.

**Not done.**

- **Layer 3 had not run** the specs of the first version (`write-refusal.spec.mjs`,
  `grid-live.spec.mjs`, `grid-live-local.spec.mjs`, `measure-live.spec.mjs`) when it was written; CI ran
  them. The merged branch's changed specs are listed with its pull request.
- **LV-15 is observed only in part**: apply to frame has a spec; the bytes per update on the Server
  host, and the requery and the grid's pass per update in the browser, are not recorded yet.

