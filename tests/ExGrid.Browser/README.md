# Layer 3 — the browser suite

Playwright against the installed Chrome and Edge (ADR-0026), driving the DemoHost it
starts itself:

```sh
nix develop .#browser -c npx playwright test          # both projects: chrome and msedge
npx playwright test --project=chrome                  # one browser, where the other is absent
```

Headed by default, deliberately: headless macOS keeps overlay scrollbars whatever the
CSS asks, and the scrollbar suite would pass while proving nothing. `EXGRID_HEADLESS=1`
exists for machines without a display — safe on Windows and Linux, where native
scrollbars occupy layout anyway. **Run this suite on Windows or Linux at least once per
verification**: VZ-10's real clause is about platforms whose scrollbars take space.

A machine without Edge fails the `msedge` project by name — the honest outcome
(ADR-0017 requires both browsers; passing on one does not satisfy it).

A third project, `chrome-150`, runs the tests the Layout Ceiling broke (ADR-0053, VZ-15):
BIG-1, BIG-5, VZ-15, the Focus against the scrollbars, and the Sheet's SH-2, SH-18/DC-2/3/7;
and every test that scrolls a compressed grid to a row (MK-6, `sheet-vs-excel` items 3 and 5,
active-cell cases 7 and 8). Those go through `scrollRowToTop` in `fixtures.mjs`, which maps a
row to a scroll offset through ADR-0053's `c(s) = s × k`: a `scrollTop` of rows × row height
shows a later row once the height is compressed, which only a run at 150% can see.
It launches Chrome with `--force-device-scale-factor=1.5` and `viewport: null`, headed, so
the display scale reaches layout the way the OS's does and Chrome clamps at 22,369,618 CSS
px. Playwright's `deviceScaleFactor` would not do: it raises `devicePixelRatio` and leaves
the clamp where it was. Its 1280×800 window is 1920×1200 device pixels, so the screen it
runs on has to be at least that (CI's xvfb screen is 2560×1600).

```sh
npx playwright test --project=chrome --project=chrome-150   # where Edge is absent
```

**CI runs this suite on every push and pull request** (ADR-0041): Linux, the runner's
installed Chrome and Edge, headed under `xvfb-run`, with this directory's own config
unchanged — against each host, each browser in two shards, every shard on a runner of its own
(`--project=chrome --shard=1/2` and so on). A shard is a set of whole spec files. One job per
host, under the name the host's run has always had, passes only when all four of its shards
did, and a failure in any turns the run red. Each shard keeps `console.json`, `metrics.json`
and any failure's trace as an artifact of its own. The weekly run, or a dispatch asking for the
long run, adds the soak (`EXGRID_SOAK=1`). The VZ-14 test still skips itself off Windows, so it
stays a run by hand.

To run one shard as CI does:

```sh
npx playwright test --project=chrome --shard=1/2
```

## The two hosts

The same pages are served by two hosts (ADR-0019): the standalone WebAssembly DemoHost,
which is the default, and the Blazor Server host, which the suite drives with
`EXGRID_HOSTING=server` (Definition of Done §24):

```sh
EXGRID_HOSTING=server npx playwright test
```

On Server the browser does not reach the host directly. `latency-proxy.mjs` sits in front
of it (the browser at 5298, the host at 6298, the proxy's control at 7298 — every port is
derived in `hosting.mjs` from `EXGRID_BASE_URL`), delaying every chunk in both directions
by half a round trip that starts at 0. A test that needs a real round trip sets one with
`setRoundTrip(ms)` from `fixtures.mjs`, and the fixture puts it back to 0 afterwards. The
browser's own network throttling is not used because it is not reliably applied to the
WebSocket that carries the circuit, which is the one connection that matters.

The Server host's log is not the browser console. The host appends every Warning and
above to a file `hosting.mjs` names, and the fixture reads what each test added to it for
CON-6: a line naming an unhandled exception, or any Error or Critical line, fails the
test.

`circuit.spec.mjs` holds what only a circuit can fail — keys typed faster than a round
trip (ED-22), a paste past the hub's message limit (CP-21), a write the browser rejects
(CP-23), the Prerendered paint (A11Y-20), two users over one store (SRV-3), and text typed at
full speed into the editor, the filter's search box and the find field arriving whole (SRV-7). It runs on
both hosts; a test that has no meaning on WebAssembly is skipped there by name.

## Installing the browsers

The flake ships none on purpose, and `flake.nix` says why: `channel: 'chrome'` means the
real Google Chrome the machine has, found by its own well-known path — on Linux
`/opt/google/chrome/chrome`, which is where Playwright reports it missing — and a nix
store build is not that. Both browsers are a machine prerequisite, installed once,
outside nix.

On Ubuntu-family Linux, including WSL2 (these need root, so run them yourself):

```sh
# Google Chrome
curl -fsSL https://dl.google.com/linux/linux_signing_key.pub \
  | sudo gpg --dearmor -o /usr/share/keyrings/google-chrome.gpg
echo "deb [arch=amd64 signed-by=/usr/share/keyrings/google-chrome.gpg] https://dl.google.com/linux/chrome/deb/ stable main" \
  | sudo tee /etc/apt/sources.list.d/google-chrome.list
sudo apt update && sudo apt install -y google-chrome-stable

# Microsoft Edge
curl -fsSL https://packages.microsoft.com/keys/microsoft.asc \
  | sudo gpg --dearmor -o /usr/share/keyrings/microsoft-edge.gpg
echo "deb [arch=amd64 signed-by=/usr/share/keyrings/microsoft-edge.gpg] https://packages.microsoft.com/repos/edge stable main" \
  | sudo tee /etc/apt/sources.list.d/microsoft-edge.list
sudo apt update && sudo apt install -y microsoft-edge-stable
```

Under WSL2 the headed default should stand as it is: WSLg supplies the display (`/mnt/wslg`,
with the X socket at `/tmp/.X11-unix/X0`), and WSL2 is one of the platforms whose scrollbars
occupy layout, so `EXGRID_HEADLESS=1` would disarm the suite rather than help it. If a headed
launch reports that it cannot open a display, check that `DISPLAY` is set to `:0` — a
non-interactive shell does not always inherit it.

## Running on Windows itself

VZ-14 is only discharged by browsers on a real Windows desktop set to 125%, with the OS
doing the scaling. From a WSL2 checkout, only the runner and the browsers need to be on
Windows. The DemoHost can stay in WSL: Windows reaches `localhost:5299` through WSL's
localhost forwarding, and `reuseExistingServer` takes the running host.

1. In WSL: `nix develop -c dotnet run --project samples/ExGrid.DemoHost --urls http://localhost:5299`
2. Copy this directory to a Windows path, `node_modules` included but without
   `node_modules/.bin`. Playwright is plain JavaScript, so the Linux install runs on
   Windows unchanged. A UNC path to the WSL checkout does not work, because `cmd` refuses
   a UNC working directory.
3. On Windows, with any Node on `PATH` (the official zip, unpacked, is enough):
   `node node_modules\@playwright\test\cli.js test`

The observational numbers land in `..\..\verification\<day>-windows` relative to the
copy. Move them into the repository's `verification/`.

**Changing the display scale needs a sign-out on some machines, and a sign-out stops
WSL.** Record what has run before you sign out. After you sign in again, Chrome's first
launch can be the one that applies a waiting update. It exits with code 0, and the first
test fails at 0 ms with `browserType.launch: Target page, context or browser has been
closed`. That is not a result, but keep the log and say so rather than letting a rerun
hide it.

## Before the first test of each browser

`fixtures.mjs` opens `/features` once per worker — once per browser — before that browser's
first test, under a timeout of its own (the `warmedUp` fixture). A browser's first load pays
for its own start-up and the app's first boot in it, which on a CI runner has taken over half a
minute and timed out the first two tests of the second project before their bodies ran. The
warm-up asserts nothing; a page that never loads still fails the first test that needs it.

## Before the first key of each page

Every `page.goto` and `page.reload` in a test returns only once the page is interactive and no
grid is Prerendered (`aria-busy` gone, a tab stop taken — A11Y-20), on both hosts. Rows paint
before the grid's listener attaches — on WebAssembly too, where the module import still has to
land — and a key pressed in that gap is lost, as it would be for a user who ignored the busy
grid. A test that clicked and typed straight after the rows appeared failed that way on a slow
runner.

## What a test shares with the rest of its file

A spec file boots the app once; every test in it mounts its page afresh (ADR-0056). Booting
was most of a test's time — 1.65 s of about 2.3 on WebAssembly — and it bought an isolation
nobody had asked for. What that means when writing a test:

- **The file's first navigation boots the app at the index, `/`. Every `page.goto` after it is
  an in-app navigation (`Blazor.navigateTo`) through the index,** so the page and every grid on
  it are new for every test. The document, the .NET runtime, the grid's JS module and the DI
  singletons last for the file, and on the Server host the circuit does. `page.reload()` is a
  real reload. The next spec file gets a new browser context.
- **The harness puts back what it owns:** the viewport, the permissions a test granted, the round
  trip, the pointer (to the corner), the scroll, the text selection and where the next Tab
  starts.
- **Anything outside the test's own grids is changed through `alterPage`:** a global, a
  listener on `window` or `document`, the head, `body`, `<html>` or `#app`. That includes the
  grid's parent, which is `#app` on WebAssembly and `body` on the Server host, and anything put
  beside the grid, which Blazor does not remove when it leaves the page. The change returns the
  function that undoes it, and the harness calls it as the test ends:

  ```js
  await alterPage(page, () => {
      const write = navigator.clipboard.write;
      navigator.clipboard.write = () => Promise.reject(new DOMException('denied', 'NotAllowedError'));
      return () => { navigator.clipboard.write = write; };
  });
  ```

  `watchNextKey` does this for its listener. **The harness checks the rule:** back at the index
  after each test, the document's markup, the rules in its stylesheets and the globals on
  `window` must be what they were when the file booted there, and the Clipboard API, the timers
  and the other natives in `fixtures.mjs`'s list — read as a caller reads them, so a stub on
  `document` counts as much as one on `Document.prototype` — must be the ones the app booted
  with. A difference fails the test by name: "nothing left outside the test's own page". What it
  cannot see — a listener on `window` or `document`, a timer or an observer left running, a
  native outside the list — is held by the rule alone.
- **A test gets a document of its own** — a context of its own, every navigation real — with
  `test.use({ freshDocument: true })`, for a test about loading itself (A11Y-20's prerender,
  BIG-7's time from a load), and with `viewport: null`. A test that calls a method whose effect
  outlives a navigation — `addInitScript`, `route`, `emulateMedia`, `setExtraHTTPHeaders`, a CDP
  session and the rest of `fixtures.mjs`'s list — makes the page its own from then on: its
  navigations are real, and its page is not handed on. So does `page.reload()`, and a navigation
  away from the app's origin. A test whose `use` asks for context options other than the
  viewport's — a colour scheme, a locale — gets the app booted afresh in a context made with
  them.
- **A page is not handed on** after a test that failed — in its body or in the harness's own
  checks as it ends — left a key or a button held, or whose console reported an error. The next
  test boots.
- **The console record is still per test.** What the app says while booting is the file's first
  test's. What a page says as it is disposed — then, or in the two frames after — is the test's
  that mounted it: the harness leaves the page inside that test's teardown. Anything said between
  two tests is the next one's, on the Server host's log as in the browser's console, and so is
  what a file's last page says as it is closed.

## What it asserts

- `scrollbar.spec.mjs` — the Scrollbar Gutter: the Focus is never behind a bar, at
  DPR 1 / 1.25 / 2, with the platform's bars and with forced classic bars. Those scales
  are CDP's. Playwright's default viewport pins every page at DPR 1 whatever the OS is
  set to, so none of those tests sees the OS scale. The VZ-14 block turns the emulation
  off (`viewport: null`) and refuses to pass unless it is on Windows, at a fractional
  DPR, with bars that occupy layout. Anywhere other than Windows it is skipped by name.
- `features.spec.mjs` — the interaction surface on `/features`, with real keys and the
  real clipboard: the editor's two states (ED-2/3/4), both clipboard formats and the
  refusals (CP-1/3/4/5/6/10/14, PST-1), the keys the grid must not take (KB-15), one
  tab stop (A11Y-4, KB-12), instance independence (DOM-4), header-click sorting
  (SR-1). And on `/cells`, entering a cell by key (ADR-0037): Space
  into a cell with several actions, the arrows choosing and Space firing once, Enter
  never firing (KB-20/21/22); Space putting the caret in a Template's own field and
  Escape bringing the keyboard back (KB-23/24); a held Space firing once (KB-26);
  Shift+Tab from after the grid landing on the root with buttons on the page
  (A11Y-17); the chosen action outlined under forced colors too (UX-14).
- `excel-keys.spec.mjs` — Excel's editing keys on `/features`: Ctrl+Z / Ctrl+Y /
  Ctrl+Shift+Z reaching the page's undo stack (KB-39), Delete's Clear Intent and its
  refusal (ED-24), Backspace's empty editor (ED-23), Ctrl+D / Ctrl+R and a fill refused by
  name (CP-24/25), the keys staying the input's own inside the editor, and — on `/cells` —
  a display-only grid leaving Delete, Backspace, Ctrl+Z, Ctrl+D and Ctrl+R to the page (ED-25).
- `find.spec.mjs` — Find (ADR-0055), **run once per Chrome** on `/features`: Ctrl+F taken
  from the browser and the keys typed after it landing in the panel's field, Enter and
  Shift+Enter stepping, a match beyond the painted rows revealed, "no match" in a live
  region, Escape handing the keyboard back, a range searched and kept, a capital and a
  Shift+Enter typed straight after Ctrl+F keeping their meaning, Ctrl+F in the find field
  selecting its text and in a column's popover opening Find (FD-1/3/4/5/6); the panel inside the
  grid's box, one per page, and Tab staying inside it (FD-8); Ctrl+F taken and nothing opened
  on `/cells`, which wires no search (FD-2); and nothing done in the editor (FD-1).
- `popovers.spec.mjs` — the popovers on `/features`, **run once per Chrome**: the
  built-in one and `ExGrid.MudBlazor`'s (`/features?chrome=mud`), which must give
  identical outcomes (WR-5, FN-17). The three dismissals, a pointer-down keeping its own
  meaning (KB-17); `Alt+↓` opening the Focus column's menu (KB-28); every popover taking
  DOM focus by key or pointer (KB-29); the menu keys — arrows over the enabled items with
  wrap, Home/End, Enter/Space run, Tab closes (KB-30); the panel's Tab wrapping inside it
  and Enter in its value field applying what OK applies (KB-31 — under the Wrapper that
  is its own panel, a `MudSelect` operator whose list is an Inner Popup and a
  `MudNumericField`); every close handing the
  keyboard back to the root (KB-32); the roles and names (A11Y-19); the scroll container
  not clipping (UX-11); and the Context Menu (CTX-1..4). Under the Wrapper alone, its
  panel's Inner Popups (FN-21): drawn outside the root and disturbing neither grid, a
  pointer-down elsewhere closing the list or the calendar and the panel while keeping its
  meaning — and under `ModalOverlay` (`/features?chrome=mud&modal=1`) only the popup —
  Escape closing the popup first and the panel next, and focus back on the root after a
  choice and Apply. Under both Chromes, a menu taller than its grid stays inside the
  grid's box and scrolls (UX-11, ADR-0040), and a grid that goes while the keys after
  Alt+↓ are still waiting for its popover leaves nothing running to throw from a later
  frame (ADR-0010, CON-2).
- `stripes.spec.mjs` — Row Stripes on `/stripes` (ADR-0038), read as painted colours
  from a screenshot rather than as computed styles: a pinned and a scrollable cell of
  one striped row paint the same ground, and the stripe moves with its row (UX-15); a
  group or total row's ground and a Cell State's paint over the stripe, the roles still
  count in the parity, the overlays paint above it, and forced colours paint none
  (UX-16).
- `marks.spec.mjs` — Row Marks on `/marks` (ADR-0043), 10⁶ rows through
  `GridSource.Fetch` with a mark adapter: after "mark all", rows scrolled to far away
  paint ticked (MK-6); a filter keeps the marks and the count names those outside it,
  and widening it back shows them ticked (MK-7); Space in the Mark Column brings a
  mixed block into line and unmarks an all-marked one (MK-1); a row added after "mark
  all" turns the header to "some" (MK-8); an action over a mark whose row was deleted
  elsewhere reports it as gone.
- `presentation.spec.mjs` — the presentation contract, measured: inline Geometry
  Tokens beat the supported override routes (UX-2), painted geometry equals declared
  (UX-3/ST-3), Visual Tokens recolour from an ancestor (UX-5), nothing under the
  Viewport animates (UX-6), forced colors keep every state tellable (UX-7), the dark
  scheme stays readable (UX-8), the LTR island inside an RTL page (DIR-2/3), the
  editor's box is the cell's (ED-9), the runaway auto-scroll stops (SL-14/15), the
  Blazor error UI never shows (CON-5).
- `virtualisation.spec.mjs` — the large-data criteria at the Definition of Done's own
  scenario, `/wide` at 10⁶ rows × 100 columns (§12): the far corner reached and painted
  (BIG-1), the same element count at 10³, 10⁵ and 10⁶ rows (VZ-1, BIG-2, DOM-1 —
  `/wide?rows=N`), the first and last rows painting their own data there and back
  (BIG-5) — and a scroll to the end made in the very task the grid stops being busy, before
  the Layout Ceiling can have been heard, not undone by the ceiling's re-anchoring (BIG-5,
  ADR-0053) — Ctrl+A over 10⁸ cells as one rectangle with the next key answered (BIG-3),
  and at most one interop call per scroll frame, counted over CDP by breakpoints that
  never pause (PF-1).
- `memory.spec.mjs` — on `/lifecycle`, a grid mounted and disposed fifty times leaves the
  browser's node and listener counts where they were (MEM-2), and a dispose takes the
  module's eight listeners off the root (MEM-4). The ten-minute soak (MEM-5, with the
  managed heap for MEM-6) runs only with `EXGRID_SOAK=1`:
  ```sh
  EXGRID_SOAK=1 npx playwright test memory.spec.mjs
  ```
- `harness.spec.mjs` — the harness itself (ADR-0056), in pairs whose second half skips itself
  when the first has not run: a spec file boots once, at the index, and the next test mounts a
  new grid on the same document; what a test changed through `alterPage`, the permissions it
  granted, the viewport it set, the round trip, the key it watched, the pointer it left, the page
  it scrolled, the text it selected and where it left the keyboard are gone for the next; a
  native stubbed on its prototype or its instance, an element left in the head, an attribute left
  on the grid's parent, a rule inserted into a stylesheet and a global left on `window` without
  `alterPage` are each named, and the next test has a document of its own; a test that fails —
  in its body or in its console — or leaves a key or a button held hands no page on;
  `freshDocument` loads the page for real.
- `observational.spec.mjs` — the numbers that are recorded, never gated, into
  `metrics.json`: mount to first row at 10⁶ (BIG-7), the DOM with horizontal
  virtualisation on and off (DOM-5), the settle repaint and the frame intervals at both
  settings (PF-6, and BIG-6 as its "on" half), and a selection drag's cost per step
  (PF-7). Each one asserts only that it measured something.
- `mud.spec.mjs` — the Wrapper contract with a real Wrapper, `ExGrid.MudBlazor` on
  `/mud` (ADR-0030): painted geometry equals declared under the Wrapper's stylesheet
  and Roboto (UX-3), nothing under the Viewport animates (UX-6), the Focus outline
  keeps 3:1 against the surface in both palettes (UX-9), a theme switch re-renders no
  row (RR-1), the hover band follows the pointer in the hovered instance only (UX-13,
  ADR-0021's fifth entry), the Wrapper's editor fits the core's box and commits
  (ED-4), the loading bar shows only while loading, and the paper's corners never
  clip (UX-11's premise).
- `mud-app.spec.mjs` — the Wrapper against a Consumer's application, on `/mud-app`: an
  ordinary MudBlazor app with an AppBar, a Drawer, tabs, a dialog, a toolbar select and
  a light/dark switch (§23's proof-of-concept page). The WR-7 clauses that need no seam
  the Wrapper has yet to fill: a grid mounted in a hidden tab paints its declared row
  height and the right geometry once shown; a Drawer toggle resizes a `Stretch` grid and
  the painted columns and End's reveal follow, both ways; the two main-area grids stay
  independent (DOM-4); the toolbar's `MudSelect` never disturbs a grid; a grid in a
  `MudDialog` opens its popovers whole — inside the grid's box, scrolling where they do not
  fit — and the filter panel's Inner Popup stands above the dialog (ADR-0040). The
  positions paper is `Striped`: the stripe is the
  palette's table-stripe colour in both schemes, and a scheme switch re-creates no row
  (WR-6). The console rules hold across the app's own controls (WR-9). And `/features?chrome=mud` — the switch that runs `/features`
  under `MudGridChrome` so its tests can run under both Chromes (WR-5) — is shown to
  take (the Wrapper's editor appears) and to open the column menu.
- `navigation.spec.mjs` — the DemoHost's page index at `/` (docs/specs/row-inspectors):
  it lists every route the pages declare, read from their `@page` directives, so a page
  missing from `DemoPageList` fails by name; every page opens from it with a clean
  console, names itself in its navigation bar and links back; the Row Identity demo that
  used to be `/` is at `/identity`. It asserts nothing about the grid.
- `inspectors.spec.mjs` — row inspectors on `/inspectors` (docs/specs/row-inspectors,
  page A): an Action Column press opens that row's inspector, and **where the keyboard
  is afterwards** — inside the inspector, modal or floating, by click or by Space, with a
  handler that returns once the dialog is shown and one that awaits its result; once a
  modal closes, back where it was before the press — the grid's root only if it was there
  (ADR-0020/0037, KB-36). Floating inspectors: the same row
  brings its inspector to the front, a title-bar drag moves one, each closes alone. Row
  Marks open one inspector each, survive a sort, name the out-of-filter count, and above
  the cap of ten are refused by count (ADR-0043).
- `inspector-edits.spec.mjs` — changing a row from its inspector on `/inspector-edits`
  (page B), over a store that is one per process: an approval reaches the grid with no
  banner; a change elsewhere raises the banner, keeps the old values shown and disables
  the actions until reloaded; a deletion says so; an approval pressed while the news of a
  change is held back is refused by the store's version check; a note records the
  version it was written against. On the Server host a second browser context approves
  and the first shows the banner.

- `sheet.spec.mjs` — ExSheet on `/sheet` (SH-18, ticket 18 of docs/specs/exsheet): Entries
  and Formulas typed and recalculated, F2 opening the Entry; Ctrl+arrow stopping at each
  block's end and Ctrl+Shift+arrow extending (DC-7); a column heading, a Row Heading and the
  corner selecting, with nothing sorted (DC-2/3); the Row Headings held at the left edge while
  the Sheet scrolls sideways (DC-3); the Name Box navigating, refusing an unreadable address,
  committing an open edit and handing an unreadable Formula back to its editor (DC-11); the
  Formula Bar and the Cell Editor agreeing after every keystroke and committing and cancelling
  once (DC-22); a row inserted by button and by the Context Menu keeping every Reference, one
  Ctrl+Z each; a row deleted, a column inserted and a column deleted by the Context Menu, with
  the References rewritten, a deleted target `#REF!`, the Selection left in place, and one
  Ctrl+Z restoring each (SH-5, ticket 13); the Linked Table reading `#GETTING_DATA` until its snapshot (SH-16); the
  application's changes refused while an edit is open — the page's buttons greying out while `99`
  is typed over C4 and Enter putting it in Plums' row; on the Server host, *Insert a row above row
  2* pressed on a 150 ms circuit before the button has greyed out, refused by name in the status
  line, and `99` still in Plums' row; the buttons following an edit opened by typing and by the
  Formula Bar and ended by a cancel and by a commit after a Reject; a Linked Table push taken while
  an edit is open (SH-29, ticket 26); the Focus
  at XFD1048576 with the DOM no larger than at A1 (SH-2); Home, End, Shift+Home and Shift+End in
  Caret in the Cell Editor and the Formula Bar moving and extending the caret with nothing
  scrolled and the edit kept, which on macOS the listener answers (ticket 32).
- `declarations.spec.mjs` — the declarations of ADR-0050/0051/0057 (§26) as ExSheet makes them on
  `/sheet`: completion under the built-in Chrome and `ExGrid.MudBlazor`'s (`/sheet?chrome=mud`)
  — the list inside the grid's box, ↑/↓, Tab, Escape, ←/→ with the list open, `=SS` completed
  at the reported caret, from the Formula Bar too (DC-17/31); Point by keys, Shift+arrows, the
  mouse, F2, mid-text, from the bar, a press in the bar's text keeping its caret and ending
  pointing (DC-19/31/34); DC-20 and DC-28 with 150 ms on the Server host; F4 cycling the
  Reference at the caret in a cell and in the Formula Bar under both Chromes, mid-text, over a
  selection and a selection only touching References (`+` in `=A1+B1`), while pointing, left to the browser with no edit open, and in a burst and twice
  before the caret is placed with 150 ms on the Server host (DC-45); the Reference Outlines —
  `=A1+B2:C3` outlined once each in two colours across the pinned boundary, `=A1+A1` once, `=` ↓ ↓
  dashed in the first colour and solid once an operator follows, gone on Escape and on Enter
  (DC-46); the Linked Table's columns a Formula reads outlined in the positions grid, over all
  its rows, in the colour class the core gave them, once each whatever the Formula's casing, none
  for an undeclared table or column, gone on Escape and on Enter (SH-31, DC-50); the fill handle
  dragged — series, References shifted, a refused pattern, the edge auto-scroll, the Selection
  after (DC-13/27); a block from the real clipboard spilling (DC-8); a copy inside the Sheet
  shifting References, and what the paste receives of the `data-ex-grid="invariant"` marker on
  both copy routes (SH-14/DC-33 — recorded in `metrics.json`); Ctrl+Z / Ctrl+Y / Ctrl+Shift+Z
  with and without an edit open, and the positions grid, which declares nothing, leaving them
  to the browser (DC-30); grips without the menu button (DC-36); the positions grid keeping
  ExGrid's own behaviour beside the Sheet (DC-25).
- `reference-text.spec.mjs` — the coloured text in the editor (ADR-0057) on `/sheet`, under the
  built-in Chrome and `ExGrid.MudBlazor`'s (`/sheet?chrome=mud`, the Sheet on the Wrapper's paper
  with its stylesheet): a burst of typing with 150 ms on the Server host,
  sampled every animation frame in the page, never showing transparent field text over a layer
  that differs, and the colours back once it pauses; on WebAssembly the colours following each
  keystroke, three References in three colours; only the surface the edit is in coloured, the
  other plain, as in Excel: an edit opened by F2 or by a press into the Formula Bar coloured there
  before anything is typed, and the colours following a press from the cell into the bar and back;
  the caret and a selection drawn by the field; an IME composition through CDP drawn by the field
  while it lasts, and the colours back when it ends (DC-47); the Mud Cell Editor still showing the
  layer's text with the Wrapper's stylesheet taken away; the Reference Point is writing on a grey
  ground after `=SUM(`, in the cell and in the bar, none after `=` ↓ ↓, and a `5` typed after
  pointing following the Reference (ADR-0051); a Formula longer than
  either surface, at both ends, the layer's line scrolled with the field, its font, padding and
  spacing the field's, and the two drawings of the text the same picture (DC-48).
- `sheets.spec.mjs` — two ExSheets on `/sheets` (ADR-0018, SH-13): typing, Formulas, the
  Name Box, the Formula Bar, completion, the pointing outline, the Context Menu and each undo
  stack stay with the Sheet that has the keyboard, and each Sheet's Linked Table columns are
  outlined only in the grid its page wired to it (SH-31, DC-25).
- `edit-stands.spec.mjs` — an edit left standing when the keyboard leaves the grid (ED-26,
  ADR-0018 section 6, ticket 25 of docs/specs/exsheet), on `/sheet` under both Chromes and on
  `/sheets`: the edit neither committed nor discarded when the positions grid, a page button or
  nothing takes the keyboard — the positions grid is in the Sheet's Pointing Scope, so the keyboard
  leaves for a page button first and the grid is an ordinary grid when it is pressed — and the other
  grid's keys its own; a press back on the rows
  pointing with the keyboard back in the Cell Editor or the Formula Bar, or committing and
  giving the root the keyboard; a press back on a column heading or a Row Heading; a committing
  press on the rows taking the keyboard out of the bar the edit was typed in; and, with 150 ms
  on the Server host, the key straight after the press back reaching the Sheet. And ED-22 widened
  (ADR-0010, 2026-09-29), without latency and at 150 ms on the Server host: the keys typed straight
  after a press on the rows while an edit is open, held until it is answered — on `/features` and
  on the Sheet, a press that commits and one that points; a double click on another cell still
  committing the edit and opening that cell's text, on `/features` and on the Sheet; outside an
  edit, a press and an arrow holding nothing, read by a page listener the held keys never reach;
  and a press on the rows of a stand-in for a grid nested in the Sheet starting no hold of the
  Sheet's, the key after it reaching the nested grid as the browser's own keydown. And ED-27 on `/sheets` under both
  Chromes (`?chrome=mud` puts each Sheet on the Wrapper's paper): the computed outline of each
  Sheet's Cell Editor is 1px wide while DOM focus is outside its root, and at the token's full
  width, in the same colour, while the keyboard is its own.
- `pointing-scope.spec.mjs` — a Pointing Scope (ADR-0058, SH-32/SH-35, ticket 37 of
  docs/specs/exsheet) on `/sheet` and `/sheets`: `=`, a press on a PV cell, `*2` and Enter showing
  that row's PV doubled; `=SUM(`, a press on the PV header, `)` and Enter showing the column's sum;
  the keyboard staying in the Sheet and the positions grid keeping no Selection; a further press
  on the grid or the Sheet replacing what the last wrote; F4 changing nothing and the Name Box
  empty after a press; a Shift+press and a drag writing nothing, the drag taking back what its
  press wrote, and the page's status line saying why; the keyboard leaving for a page button making
  the positions grid ordinary, with the edit standing and pointing going on once a press brings it
  back; on `/sheets`, a press on the right's grid while the left Sheet points being an ordinary
  press; and, on the Server host with 150 ms, a press within the round trip after `=` being an
  ordinary press with the edit standing, a press on the grid still painted pointed at after the text
  stopped pointing writing nothing and saying why, with the keys after it going on, and one still
  painted otherwise after the text began to point being an ordinary press. A press follows a text
  only once the Formula Bar shows it: until then `ex-pointed-at` can be left from an earlier text
  (`=` points, `=S` does not), and a press can land on the grid painted otherwise. On `/pointing` (ticket 41, SH-35, DC-55), the arrow keys
  after a press: ↓ from R-1's PV writing R-2's lookup and moving the dashes, and ↑ back; → from an
  Id cell passing over Book, the grid's own column, to PV; ↓ past the painted rows scrolling the
  positions grid with the pointed cell in view; Shift+↓ and Ctrl+↓ writing nothing, with the page
  saying why; ↓ after a press on the PV header writing nothing, with the page saying to press a cell;
  and ↓ and `*2` typed at once, 150 ms injected on the Server host, keeping their order.

`sheet-helpers.mjs` is what those five share: opening `/sheet` under either Chrome and waiting
for its Linked Table, a Sheet's grid, the positions grid beside it, a cell by its A1 address, the
editor surfaces under either Chrome, the Name Box, and painted-box comparisons.

Every spec takes `test` from `fixtures.mjs`, which listens to every page from before its
first navigation and fails a test on a console `error` or an uncaught page error
(CON-1/2), on a warning from ExGrid's own code (CON-3), and on an unhandled exception at
any level of the WebAssembly host's log, which is the browser console (CON-6). What a
test's console said is kept in `console.json` beside `metrics.json`, third-party
warnings included, for `results.md` to list. The dev server's own output is piped into
the run's output, which Step 4 of §22 tees into `layer3.log`.

## What it deliberately does not assert

Timing. Milliseconds never gate (Definition of Done §1); the structural invariants
above are what produce them. The observational numbers go to
`verification/<date>-<platform>/metrics.json`, one key per browser project: the
directory carries the platform because a structural count is comparable across machines
and a timing is not. A headless run in a container renders in software, and its numbers
belong to no trend — do not file them.

## What Chrome owes this suite

**Whatever renders a refusal has to be a live region.** The grid raises a refusal as an
enum and holds no sentence for it, so it cannot announce this one — the announcement is
Chrome's, and a refusal rendered into a plain element is silent to a reader who cannot
see it while ADR-0035 promises the write is "always reported" (A11Y-16). The same goes
for a discarded edit, which nobody even provoked (ED-21).

Two traps live in that, and the DemoHost has hit both:

- A live region announces on **mutation**. Writing the same sentence twice says nothing
  the second time — and the second refusal is the one the user needs, having just tried
  again. The reference Chrome appends an occurrence count so the repeat is a change.
- The confirmations beside it (`#edit-status`, `#paste-status`) are deliberately **not**
  live regions. They report what did happen, and announcing every successful edit would
  bury the one message that matters.

## Traps this suite has already hit

- A change made with `page.evaluate` outside the test's page outlives the test: the next test
  in the file runs on the same document (ADR-0056). CP-23's stubbed `navigator.clipboard.write`
  made the next test's copy fail silently, and DIR-2's `dir="rtl"` landed on `#app` — the grid's
  parent — and would have held for every test after it. Use `alterPage`; the harness names what
  it finds left behind.
- `test.use({ expectedHostLog: [/a/, /b/] })` names one pattern, not two. Playwright reads a list
  whose second item is an object — and a RegExp is one — as its own `[value, options]` pair, so
  the option becomes `/a/` alone, and a list of expected lines is suddenly not a list. The same
  holds for `expectedWarnings` and `expectedLeaks`. Give one pattern: `/a|b/`.

- Cells and header cells are `pointer-events: none` by design — the Viewport is the
  delegated target (ADR-0004). Playwright's actionability check must be bypassed with
  `{ force: true }`; the browser then hit-tests through to the Viewport, exactly as a
  user's click does.
- Restart the DemoHost after any rebuild: a stale host serves a stale app, and the
  first symptom is an unrelated-looking 404 or a page without the newest feature.
- Two checkouts must not share a port. The host is started on whatever port
  `EXGRID_BASE_URL` names (default 5299), and an already-running host is reused — so a
  second checkout running on the default would be testing the *other* checkout's
  code and passing. A parallel agent's worktree runs with its own, e.g.
  `EXGRID_BASE_URL=http://localhost:5399` (AGENTS.md, "Working in parallel").
- Chrome normalises clipboard HTML on read (adds a meta and a tbody): assert on the
  cells, not the exact fragment.
- MudBlazor 9's `MudSelect` takes the value on each arrow while its list is open, and
  Enter leaves the list open; Escape closes it. A test that presses Enter and waits for
  the list to go waits forever.
- A grid whose Focus has scrolled out of view — a `Stretch` grid narrowed under it, say —
  paints no Focus cell, and `aria-activedescendant` is then rightly empty (ADR-0033).
  Measure a row by its index, not by the Focus's.
- Playwright's default window is 1280×720, and a Sheet lower on the page than that has its
  bottom edge — the edge band, the fill handle of a low Selection — outside the window, where
  the pointer reaches nothing. The ExSheet specs use a 1000 px high window.
- A cell whose right edge sits under the vertical scrollbar has its fill handle there too,
  where a press lands on the scrollbar. Drag from a column that is wholly in view.
- On the Server host, typing into an editor that is already open is not safe at full speed
  (the SRV-5/ED-22 test in `declarations.spec.mjs` pins why). A test whose subject is not
  typing speed types with `typeSteadily`, one character waited for at a time; a test that
  asserts after typing waits for the editor to show the text before a press elsewhere, or
  the press can land while the edit is still opening and point instead.
- A chord is two keydowns — the modifier first. A `{ once: true }` listener waiting for
  the key is spent on the modifier; and a `page.evaluate` that registers a listener must
  be awaited before the key is sent, or it races the key through a different channel.
  KB-15 failed half the time on Edge for exactly this, and it looked like the browser
  swallowing the shortcut.
