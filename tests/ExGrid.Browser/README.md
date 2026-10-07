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
shows a later row once the height is compressed, which only a run at 150% can see. It returns
once the grid has painted the slice for the offset the browser holds, as the offset the grid
wrote on the Viewport says: compressed, the rows move when the grid is told of the scroll, a
round trip later on the Server host, and a reading taken before that is of rows that are leaving.
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
unchanged — against each host, Chrome and Edge in four shards each and `chrome-150` in two,
every shard on a runner of its own (`--project=chrome --shard=1/4` and so on). A shard is a set
of whole spec files. One job per host, under the name the host's run has always had, passes only
when all ten of its shards did, and a failure in any turns the run red. Each shard keeps `console.json`, `metrics.json`
and any failure's trace as an artifact of its own. The weekly run, or a dispatch asking for the
long run, adds the soak (`EXGRID_SOAK=1`). The VZ-14 test still skips itself off Windows, so it
stays a run by hand.

CI does not start the hosts with `dotnet run`. One job publishes the WebAssembly DemoHost, the
Server host and the demo API server with `dotnet publish -c Release`, once, and every runner
takes those files. `EXGRID_HOSTS` names the directory that holds them, as `wasm`, `server` and
`api`. Then the config starts the published Server host and demo API server from their own
directories, under `ASPNETCORE_ENVIRONMENT=Development` as their launch profiles set it, and serves
the published WebAssembly files with `static-host.mjs`, which does what the SDK's dev server does
(the app for a path that names no file, a 404 for a missing file, `Blazor-Environment:
Development`). Unset, the config runs the projects, which is the edit-and-rerun loop.

To run one shard as CI does:

```sh
for h in wasm:ExGrid.DemoHost server:ExGrid.DemoHost.Server api:ExGrid.DemoApi; do
  dotnet publish ../../samples/${h#*:} -c Release -o /tmp/exgrid-hosts/${h%%:*}
done
EXGRID_HOSTS=/tmp/exgrid-hosts npx playwright test --project=chrome --shard=1/4
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

## The demo API server

Beside either host, the run also starts `samples/ExGrid.DemoApi`, the server the database and
live pages call (ADR-0069), where the pages look for it: BASE_URL's port plus 3000 (8299, or
8298 beside the Server host's proxy; `API_URL` in `hosting.mjs`). It is asked for 20,000 trades
unless `EXGRID_DEMO_TRADES` says otherwise; the first start for a count generates them in about
a second into a file outside the repository (`exgrid-demo-api` in the temporary directory, or
`EXGRID_DEMO_DATA`), and each start serves a fresh copy of that file, so every run begins from
the same trades. `/api/status` answers 503 until they are ready, so the run waits for the data
and not only for the port. The server lives for the whole run, across spec files: live updates
stay off until a test turns them on (`POST /api/live`), a test that does turns them off again,
and trades it moved stay moved. A server already on that port is reused with whatever count it
was started with, so a test reads the count and the Source Version from `/api/status` rather
than assuming them.

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

## Waiting on the Server host

On the Server host, what the grid decides reaches the page a round trip after the key or the
press that asked for it, and a timer the grid starts — 150 ms before Placeholders fill, 300 ms
before an error message opens — renders later still. **Before a reading that has to see
everything the host will say, a test awaits `circuitQuiet()`** from `fixtures.mjs` (ADR-0056).
It returns once nothing is held in `latency-proxy.mjs` or unsent on a socket, in either
direction, and nothing has crossed for 400 ms, longer than the grid's longest timer. The browser
acknowledges every render batch once it has applied it, so by then the last one is in the DOM.

- **Wait for the state you expect first, with `expect` or `expect.poll`.** `circuitQuiet` is
  for what comes after that: a reading that nothing undid it ("stays whole once every round trip
  has landed"), that something did not happen ("the panel still stands"), or a picture. A fixed
  `waitForTimeout` in its place is too short on a slow runner and lets a late answer slip past
  the reading, so the test passes while the defect it is there for is present.
- **On WebAssembly it returns at once.** There is no wire, and nothing equivalent can be read in
  the page. A test whose WebAssembly half needs the page's own asynchronous work to finish still
  waits for a state, or keeps the fixed wait it had. `circuit.spec.mjs` keeps its waits and
  awaits `circuitQuiet()` after them.
- **It cannot see a timer that has not fired**, and it says nothing about painting: a picture
  still waits for its frames, and `stillPictures` does both.
- **It never waits for ever.** It throws after `timeout` (10 s), saying how many chunks crossed
  each way while it waited. SignalR's keep-alive pings, 15 s apart, delay it by one window at
  most, and the proxy refuses a window of 5 s or more, which the pings could keep from being met.
  A proxy left running from an older checkout does not know `/quiet`, and `circuitQuiet` says
  so rather than reading its answer as quiet.

## Reading pixels

A picture says what the browser put on the screen, and nothing else does. It is also the
reading most exposed to things no requirement is about: a layer composited at a fraction of a
device pixel, a blend rounded one way on one path and the other way on the next, a render that
arrived between two pictures. So:

- **What the grid decided to draw is read from the DOM, or pinned in layer 2:** the generated
  CSS, the style attribute, `getComputedStyle`. A colour token, a class, a width the grid wrote
  are decisions, and their values are exact.
- **Pixels are read only where what is painted is itself the requirement:** matching Excel's
  pixels (a line's device pixels, a dash pattern), a layer that hides or shows another, a blend
  the browser makes.
- **Read where the boundary lies on a device pixel.** A device pixel an edge cuts through is a
  blend of both sides. `offDevicePixels(box, scale)` in `pixels.mjs` names the edges of a box
  that are not on the device pixel grid; a test reading at an edge checks it first and fails
  there, saying so. A paint is read on the pixels a box covers whole (`wholePixelCentres`),
  through a region grown out to the device grid (`onDeviceGrid`), so each pixel of the picture
  is one of the page's.
- **The one allowance for rounding is `paints(pixel, exact)`, with `blend`.** A channel whose
  exact value lies between two bytes is painted as either of them, and no other: ticket 92's
  rule, 78.53 exactly, came out 79 on a pinned cell and 78 beside it. A whole exact value admits
  only itself. Two paints that ought to be one paint are compared with each other exactly, not
  through this; that is how ticket 92 found its defect. A new test that decides a colour is
  right uses `paints`, not a tolerance of its own.
- **Two pictures of one thing are taken with `stillPictures`.** A mark the test sets draws the
  subject each way, through a stylesheet laid over the page with `alterPage`. It waits for the
  circuit to be quiet, and it takes every picture again when anything but the mark changed the
  document, or anything scrolled, between the first mark and the last picture; a page that
  never holds still fails the test, naming what changed. Playwright's own preparation for a
  screenshot — the caret made transparent with an inline style on every field, then put back,
  which leaves `style=""` behind — is not counted. `pixelsApart` compares the two.
- **When the question is where the ink stands, draw both pictures in black on white.** A
  comparison is only as strong as the contrast it reads: under half a pixel's shift, an edge
  pixel of #424242 text on white moves by about 95, under DC-48's threshold of 96 (found on
  `claude/exsheet-cell-format`, 2026-10-02).

## Before a new or changed spec goes in

A test that passes once has passed once. **Run each spec file you wrote or changed several
times over, on both hosts, before it goes in** — headless and on a port of your own on this
Mac (see "Two checkouts must not share a port" below):

```sh
export EXGRID_HEADLESS=1 EXGRID_BASE_URL=http://localhost:5411
npx playwright test harness-reading.spec.mjs --project=chrome --repeat-each=5
EXGRID_HOSTING=server npx playwright test harness-reading.spec.mjs --project=chrome --repeat-each=5
```

`--repeat-each` runs every test of the file that many times, each repetition in a worker of its
own, so each boots the app once as a file does. A test that fails one time in five fails in CI
one run in a few, on a branch that did not touch it. `retries` stays 0 (ADR-0026): a retry
would make that failure invisible, not rare.

CI does the same for each push to a pull request. Its `browser-repeat` jobs run the spec files
that push changed, among those the pull request adds or changes, three times, against each host
on Chrome and on Edge (ADR-0056, note of 2026-10-02). The push that opens a pull request takes
all of them. They take the files themselves, not those that import a helper the push changed:
the full run covers those.

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
  tab stop, its Keyboard Field's on a grid that edits, with the ring drawn from the root's
  mark and no column's ▾ reached by Tab (A11Y-4, KB-12, ADR-0080), instance independence
  (DOM-4), header-click sorting
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
- `write-lands.spec.mjs` — a write lands as the user entered it (ADR-0142, rewritten 2026-10-07;
  LV-11 to LV-14, LV-17) on `/features?upstream=1`, where F9 moves the first five rows' Notional as
  a live feed would. On both hosts, at 150 ms on the Server host: a commit over a cell that changed
  under the editor landing, with the Overwrite Notice in its live region naming what was seen and
  what was replaced (LV-11, LV-21), Escape writing and telling nothing, and a change to another cell
  of the row telling nothing (LV-11); a Ctrl+V pressed straight after F9 pasting where it was aimed, under the same order
  (LV-13, LV-14). On the Server host only, where a gesture can be taken before F9's render comes
  back: an Action press acting on its row as F9 left it (LV-12); a Ctrl+Enter fill, a fill-handle
  release, a Delete, a Ctrl+R and a Ctrl+D whose source F9 moved landing (LV-13); a commit whose
  opening key was taken before F9's change landing, the change before the open not compared
  (LV-11). `5` Enter ↑ Ctrl+V and `1` Enter ↑ `2` Enter, typed at once, land on both hosts, the
  Server host at 0 and at 150 ms, with no notice (LV-17). Each race reads what the page showed at the gesture
  first, from a capture listener ahead of the grid's, and says so by name if the change had
  already been painted.
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
  keyboard back to the grid (KB-32); the roles and names (A11Y-19); the scroll container
  not clipping (UX-11); and the Context Menu (CTX-1..4). Under the Wrapper alone, its
  panel's Inner Popups (FN-21): drawn outside the root and disturbing neither grid, a
  pointer-down elsewhere closing the list or the calendar and the panel while keeping its
  meaning — and under `ModalOverlay` (`/features?chrome=mud&modal=1`) only the popup —
  Escape closing the popup first and the panel next, and the keyboard back on the grid after
  a choice and Apply. Under both Chromes, a menu taller than its grid stays inside the
  grid's box and scrolls (UX-11, ADR-0040), and a grid that goes while the keys after
  Alt+↓ are still waiting for its popover leaves nothing running to throw from a later
  frame (ADR-0010, CON-2).
- `stripes.spec.mjs` — Row Stripes on `/stripes` (ADR-0038), read as painted colours
  from a screenshot rather than as computed styles: a pinned and a scrollable cell of
  one striped row paint the same ground, and the stripe moves with its row (UX-15); a
  group or total row's ground and a Cell State's paint over the stripe, the roles still
  count in the parity, the overlays paint above it, and forced colours paint none
  (UX-16). A group or total row is one tint deep on its pinned and its scrollable cells
  alike, at the token's shade, and the hover band reads the same over both (ADR-0024).
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
  Viewport animates (UX-6), forced colors keep every state tellable (UX-7), a Stale or
  Error state outranks a theme's tone colour on `/tones` (ADR-0006/0029), the dark
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
- `harness-reading.spec.mjs` — the harness's tools for reading the page: `circuitQuiet` waiting
  out a 300 ms round trip so a move is there to read at once, where read straight after the key
  it is not, returning at once on WebAssembly, refusing a window the keep-alive pings could
  block, and naming the chunks that kept a busy wire from going quiet; `stillPictures` taking
  every picture again after a change made between them, failing with the change named when the
  page never holds still, and not counting Playwright's own write to the fields; `paints`
  admitting either byte of ticket 92's 78.53 and only the byte of a whole value; and the
  device-pixel tools.
- `observational.spec.mjs` — the numbers that are recorded, never gated, into
  `metrics.json`: mount to first row at 10⁶ (BIG-7), the DOM with horizontal
  virtualisation on and off (DOM-5), the settle repaint and the frame intervals at both
  settings (PF-6, and BIG-6 as its "on" half), and a selection drag's cost per step
  (PF-7). Each one asserts only that it measured something.
- `measure-pivot.spec.mjs` — PV-21 and DA-17, recorded and never gated: ExPivot and the
  Snapshot over a million trades in a published WebAssembly build without AOT. That build
  must already be served, with the demo API server holding a million trades beside it,
  and every test skips itself unless `EXGRID_MEASURE=pivot`. It times the gestures on
  `/pivot?trades=1000000`, questions by their leaves up to the cap and past it, 1,000
  changes on `/pivot-live`, and reading Arrow on `/pivot-db` and a CSV on `/pivot-csv`
  (`EXGRID_MEASURE_CSV` names the file). Times are in the page's own clock, from the
  input to the frame after the answer, and every long task is collected.
  `verification/2026-10-01-linux-measure/results.md` says how it was run.
- `measure-live.spec.mjs` — LV-15's in-process half, recorded and never gated: 1,000 changes
  to a million rows on `/grid-live-local` (ADR-0141, D8), from the source's `Apply` to the
  frame that shows them, in a published WebAssembly build without AOT, against PV-21's 0.2 s.
  It skips itself unless `EXGRID_MEASURE=live`, and on the Server host. Timed in the page's own
  clock as `measure-pivot.spec.mjs` times `/pivot-live`'s 1,000 changes: the page's status line
  says how long `Apply` took, and the probe keeps the task that wrote it and the frame after.
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
- `format-keys.spec.mjs` — Excel's formatting keys on `/sheet` (ADR-0071, ADR-0050 item 14;
  ticket 51), with real keys: Ctrl+B, Ctrl+I, Ctrl+U, Ctrl+5 and Ctrl+2 to Ctrl+4 each toggling the
  Focus cell, taken from the browser, one Ctrl+Z a press, and following the Focus cell over a range;
  Ctrl+Shift with `~ ! @ # $ % ^` applying Excel's Number Formats under en-US, `#` also without
  Shift as a UK layout types it (SH-42); with an edit open, Ctrl+U, Ctrl+B and Ctrl+Shift+$
  changing nothing, taken from the browser — no page opened — said in ExSheet's notice and in the
  page's status line, and the edit committing as typed (SH-43). The page's line under the Sheet
  reads the Focus cell's Cell Format back. The page's
  *Format selection as #,##0.00* pressed straight after Shift+ArrowDown on a 150 ms circuit, before
  the Sheet has heard the move, formats the extended range as one step (ticket 56).
- `sheet-borders.spec.mjs` — ExSheet's Borders beside Excel's (ADR-0071, DC-59, SH-46; ticket 49)
  on `/sheet?case=…`, the eleventh, twelfth and fourteenth Windows runs' set-ups, read in device
  pixels: Excel's thirteen line styles on a bottom and on a right edge, scrolled down the Sheet, at
  100% here and at 150% in `chrome-150` (case 9's table, the long dash 9 at both, as the fourteenth
  run's case 18 drew it); rows keeping their one height; a Fill over its four gridlines (case 4), a
  white Fill taking them away (case 5), two Fills meeting (case 6); at both scales, the gridline
  between two Fills taking the lower one's, and side by side the right one's (the fourteenth run's
  case 16, and `fills`), with a horizontal gridline, and a Fill over one, a single device pixel —
  which Chrome's software rasteriser, CI's under xvfb, drew two deep while the row's rule was 1.5
  device pixels at 150%, and a Mac's GPU never showed (ticket 99) — a double line's middle pixel
  showing the Fill beneath it in each
  arrangement (case 17, and `fills`), and medium dashed 9 on and 3 off with dashed 3 on and 1 off
  (case 18); a thick line over the Fill below it (case 10); the left cell's line drawn
  where both cells record one (the twelfth run's case 1); the Selection's outline lying on the
  gridline and a pixel outside the range on all four sides, over the outer lines, with the lines
  inside staying drawn over its shade (case 11, ADR-0008 of 2026-10-01); and a line on column A's
  left lying under the Row Headings' edge (the twelfth run's case 14); and the lines still on the
  device pixels with the Sheet moved a third of a pixel across and down. The case pages pin no
  column, as the run's workbook did not.
- `sheet-paper.spec.mjs` — the Paper and the Ink (ADR-0071, SH-39, SH-40, DC-58; ticket 48) on
  `/sheet?case=paper`, under the built-in Chrome and `ExSheet.MudBlazor`'s, with `?scheme=light` and
  `?scheme=dark`: the Paper white and the Ink black in both schemes; a Font colour, a Number
  Format's red over a blue Font, a cell's, a row's and a column's Fill, read as recorded, the last
  two on cells that hold nothing; bold, italic, underline and strikethrough; the Headings and the
  Formula Bar dark in the dark scheme and light in the light one; the Selection, the Cell Editor,
  a Reference Outline and the pointed shade keeping their light-scheme look on the Paper, and the
  Formula Bar's References taking the dark scheme's; and `--ex-sheet-paper` and `--ex-sheet-ink`
  set by a Consumer changing the Paper and the Ink.
- `format-cells.spec.mjs` — Format Cells under the built-in Chrome (ADR-0071, SH-45, DC-60;
  tickets 52 and 56) on `/sheet` and `/sheets`: a popover inside the Sheet's box, opened from the
  Context Menu and Ctrl+1 (case 22) with the keyboard on its tab; the arrows switching the tabs; OK as
  one undo step; Escape and a refused Custom code each setting nothing; a Custom code typed at full
  speed arriving whole, Enter as OK; Tab and Shift+Tab wrapping inside; cells that differ showing an
  empty Font style and No Colour (case 24); a cell under a neighbour's thick bottom opening with its
  top pressed, as drawn (case 14-13); scrolling inside a small box and closing as a Cancel when
  the box shrinks below one row; and two Sheets each with their own. On a 150 ms circuit on the
  Server host, the page's *Format Cells…* pressed straight after Shift+ArrowDown, and the Context
  Menu's item chosen as soon as the menu opens on another cell, each open over the Selection the grid
  holds and stand when the move's notification lands (ticket 56).
- `format-cells-keys.spec.mjs` — keys typed while Format Cells opens (ticket 93; ADR-0050 item 16
  and ADR-0039, notes of 2026-10-01), under both Chromes, at an 80 ms round trip on the Server host:
  a digit typed straight after a press on "Format Cells…", a press and a round trip, Enter on the
  item and Ctrl+1 is Format Cells' or dropped, never an edit behind it; End typed straight after
  Enter or Ctrl+1 reaches its tabs, in order behind the digit; and on `/sheets` the keys held for
  one Sheet are never the other's, which types as ever afterwards (ADR-0018).
- `format-cells-mud.spec.mjs` — Format Cells under `ExSheet.MudBlazor`'s Chrome (ADR-0071, SH-45;
  ticket 53) on `/sheet?chrome=mud` and `/sheets?chrome=mud`: a MudDialog at page level, nothing
  of it inside the grid, opened from the Context Menu, the page's button and Ctrl+1 with the
  keyboard on its tab; the arrows, Home and End switching the tabs; OK as one undo step, and
  Escape, a press on the backdrop and a refused Custom code each setting nothing; Escape in an open
  dropdown (Horizontal) closing only its list, and the next one cancelling; a Custom code
  typed at full speed arriving whole, Enter as OK; Tab and Shift+Tab kept inside; More Colours as
  MudBlazor's colour picker, read back through Format Cells; and, however it closes, the next
  arrow moving the Focus — from the page's button too, which MudBlazor would otherwise hand the
  keyboard back to. Two Sheets each keep their own last tab and get the keyboard back.
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
  that differs, nor a highlight over characters other than those the core named for it, nor one
  over a hidden layer, and the colours back once it pauses; on WebAssembly the colours following
  each keystroke, the layer's text one run and three References in three colours over it, read off
  the highlights the listener registered (ADR-0057, note of 2026-10-01); only the surface the edit is in coloured, the
  other plain, as in Excel: an edit opened by F2 or by a press into the Formula Bar coloured there
  before anything is typed, and the colours following a press from the cell into the bar and back;
  the caret and a selection drawn by the field; an IME composition through CDP drawn by the field
  while it lasts, no highlight left over the layer then, and the Reference coloured again when it
  ends (DC-47); the Mud Cell Editor still showing the
  layer's text with the Wrapper's stylesheet taken away; the Reference Point is writing on a grey
  ground after `=SUM(`, in the cell and in the bar, none after `=` ↓ ↓, and a `5` typed after
  pointing following the Reference (ADR-0051); a Formula longer than
  either surface, at both ends, the layer's line scrolled with the field, its font, padding and
  spacing the field's, and the two drawings of the text the same picture (DC-48); and the same
  comparison, at its own threshold, finding a layer drawn half a pixel out either way, in another
  font, or with other letter spacing.
- `sheets.spec.mjs` — two ExSheets on `/sheets` (ADR-0018, SH-13): typing, Formulas, the
  Name Box, the Formula Bar, completion, the pointing outline, the Context Menu and each undo
  stack stay with the Sheet that has the keyboard, and each Sheet's Linked Table columns are
  outlined only in the grid its page wired to it (SH-31, DC-25). Each Sheet colours its References
  under highlights named for it, which its own stylesheet paints, and gets its colours back when the
  keyboard returns after the other has coloured its own (ADR-0057's note of 2026-10-01): with one
  shared name the other's registration would have replaced it.
- `pivot.spec.mjs` — ExPivot on `/pivot` (§29, docs/specs/expivot), **run once per Chrome**:
  ExPivot's own markup and `ExPivot.MudBlazor`'s (`/pivot?chrome=mud`), found by role and name,
  which both give the same. A field dragged from the list of fields onto an Area with the
  browser's own drag and drop, an entry dropped before another, back onto the list or onto the
  report (PV-10); report removal following Defer Layout Update, an unused field and a details grid
  changing nothing, and Escape cancelling the drag and its removal indication;
  the `−` button collapsing an Item with the Focus kept (PV-13); a double click on a value
  opening a tab at the report's foot, titled by the cell and holding the trades behind it, with
  the keyboard on the tab, a second tab beside it, and closing them; a dialog when the page asks
  for one (`?details=dialog`), taking the keyboard, inert behind it and closed by Escape; and the
  page taking the trades itself (`?details=page`) with neither opening (PV-14, DC-63); the
  keyboard into a field's menu and back to its entry, a menu dropping down under its entry as
  wide as the pane, and a command moving the field (PV-11); a press on the report or a pane caption,
  and focus moved to the report or search, dismissing a Field List menu without taking the keyboard
  back; Escape after a press on a disabled item or frame padding; the opener still toggling and
  another entry opening its menu; a menu dismissed and reopened behind 150 ms keeping its keyboard;
  and Field Settings left standing by a report press (PV-11); the Pivot Toolbar above the report — the
  report filter band on its left, Layout and the pane's toggle on its right, no Refresh for the
  bundled source (PV-30) — the band filtering, and its Filter… opening under the Pivot Toolbar over
  the report and closed by a press on the backdrop, the keyboard back on its button (PV-12); the
  Layout menu over the report, its current choices marked, a no-op disabled, Escape and a choice
  giving the keyboard back to Layout (PV-30); the toggle hiding and showing the pane, bound by the
  page; the heading's close icon still reachable in a narrow, short pane with its body scrolled
  and a panel open, following its opener at zero and nonzero scroll offsets and keeping its last
  action reachable, closing through the same binding; opening focus awaited before a report press
  or body scroll under a 150 ms round trip; Defer Layout Update holding the report until Update (PV-28); the words switch speaking
  Excel's Japanese edition and back (PV-33); Month, declared as the month of the trade date, moved
  to Columns and painted `Jan` to `Sep` in the calendar's order, and `1月` to `9月` in the Japanese
  words (ADR-0060); and the code the page shows under "The code" equal to the regions of its source
  it is read from (PV-20). Where the keyboard goes when the dialog or a tab goes (PV-39, DC-62,
  ADR-0070): Escape in the dialog's grid closing the grid's Context Menu first, then the dialog, and
  held, closing it once, its repeats leaving the report the keyboard (KB-44); however the dialog
  closes — that Escape, Escape on Close, Close, the backdrop — the report's grid holding the
  keyboard again, its arrows moving its Focus; Escape in a details tab's grid closing nothing and
  releasing Tab (KB-8); the selected tab closed handing the keyboard to the tab selected next, and
  the last one back to the report, on the cell it left. The keyboard put in the records before the
  dialog's Close or a new tab takes it (PV-41, A11Y-20, ADR-0070/0033): DOM focus put on the
  records' scroller in the task that draws them, where a press there puts it, before the control's
  request can land on the Server host, staying in the records and reaching their grid's root once
  that is a tab stop; on the Server host the control never takes it. Under MudBlazor alone, a
  MudSelect's list in Value Field Settings… taking Escape before its panel (PV-11), and the palette
  reaching the pane, the entries and the `−` button in both schemes (PV-18). Under ExPivot's own
  markup alone, ExGrid's `ReturnKeyboardAsync` keeping to its conditions (DC-61): a control of the
  page focused while the report's request is on its way keeps the keyboard, and so does a second
  grid pressed meanwhile — the other pivot's report on `/pivot-db`. On the Server host the request
  lands two round trips after the Escape or the close, with 150 ms injected; on WebAssembly the same
  tests are the case without a round trip.
- `pivot-csv.spec.mjs` — ExPivot over a CSV on `/pivot-csv` (ADR-0064, PV-20), **run once per
  Chrome**: the trade export the page writes in memory from `/pivot`'s trades, read back under
  the declared Schema to the very report `/pivot` paints, cell for cell; a file chosen through
  Blazor's `InputFile` (`setInputFiles` with a file the test writes) with a malformed row,
  refused whole with the library's sentence naming the row, the column, the value and the line,
  and nothing pivoted — and the page's own malformed sample refused the same way; a file of a
  million records painting its progress, the bar its bytes and the line its rows, with the
  inputs disabled meanwhile, and Cancel stopping it with nothing read; an unknown semicolon
  file's suggested Schema shown with what is not clear about it (leading zeros kept as Text, a
  decimal comma), nothing read until it is confirmed, then read under it to exact totals by
  desk, the account numbers keeping their zeros; the page's two samples, one under the declared
  Schema and one under a suggested Schema, reading the same trades to the same total — a second
  file read while a report stands; and the code shown under "The code" equal to its source.
- `pivot-db.spec.mjs` — `/pivot-db` (ADR-0065/0066/0069), **run once per Chrome**, against the
  demo API server from either host: its trades read over Arrow into a Snapshot the page pivots in
  its own process, at the version `/api/status` names, and asked of the server through
  `PivotSource.Fetch`, which answers in SQL, show the same numbers painted row for row — and
  again after a layout changed in one pane is shown on the other pivot; Refresh is offered by the
  server's source alone, and asks again; Show Details opens the same records behind a cell in
  both, and the server's come a page at a time as the Details tab scrolls to its end (PV-20);
  and the code the page shows equal to its source, the Arrow request taking its response whole
  (ADR-0065).
- `pivot-live.spec.mjs` — `/pivot-live` (ADR-0067/0068/0069), **run once per Chrome**: Change
  Batches the page folds into the bundled source on its own timer mark the values they changed;
  paused, the marks go after their second, and a collapse marks nothing however long after
  (PV-36). The server's live updates, which the page turns on, mark the server report's values
  through the hub's notices; the page's button turns them off and on, and leaving the page turns
  them off (PV-20); and the code the page shows equal to its source.
- `pivot-risk.spec.mjs` — the rate-delta report on `/pivot-risk` (ADR-0060, PV-20), **run once
  per Chrome**, in a window wide enough for every tenor column beside the pane, since the report
  grid paints only the columns in view: the tenors painted in the Order Key's order, `ON`, `TN`,
  `1W` … `30Y`, with `18M` and `1Y6M` two Items side by side, each carrying its own desks'
  positions; the tenor's Filter… listing its Items in the same order; without the key, the
  labels' order (`10Y` before `1M`), and back; every total painted the sum of what it totals —
  across each row, down each desk and down the Grand Total row — and the report's own the page's
  sum of the positions; and the code the page shows, the README's `Tenors.Months` among it,
  equal to its source.
- `grid-live.spec.mjs` — `/grid-live` (ADR-0068/0069), ExGrid alone over the server's trades, its
  marks set to last a minute so that where they are is what is compared. A mark is keyed by row and
  column: across a three-row scroll every trade still painted keeps exactly its marked cells, and
  after a scroll far away and back, which reads the Window again into new instances and new
  elements, the same cells are marked again (DC-65). With marks painting and going, nothing under
  the Viewport transitions or animates, under the core's stylesheet and under the Wrapper's
  (`?chrome=mud`), whose warning tint the mark takes; the mark lies over exactly the layers its
  cell paints without it, a Pinned Column's row rule among them; the grid's live region is not
  touched; and forced colours restate the mark as a dashed outline (DC-66). The Window is read from
  the server as the grid scrolls, and leaving turns the live updates off (PV-20); and the code the
  page shows is equal to its source, the hub's notice passed on with the trades it booked named
  (ADR-0141, D6).
- `grid-live-local.spec.mjs` — `/grid-live-local` (ADR-0141, D8), the in-process path: a million
  trades made in the page and bound through `GridSource.From` by Row Key, amended by the page's
  own timer as Change Batches. The page loads with a million rows on both hosts; a batch reaches
  the screen and marks the cells whose text changed (LV-9); a pause stops the batches and the
  marks go; under a sort by P&L, with 2,000 trades and a thousand amended a batch, the painted
  rows stay in order as the values move (LV-5/LV-7); the console stays clean; and the code the
  page shows is equal to its source. It needs no API server.

  `pivot-db`, `pivot-live` and `grid-live` share the run's one API server: each test starts from
  `POST /api/reset`, reads the trade count and the Source Version from `/api/status`, and turns
  the live updates off as it ends. They read "The code" regions through `demo-code.mjs`.
- `edit-stands.spec.mjs` — an edit left standing when the keyboard leaves the grid (ED-26,
  ADR-0018 section 6, ticket 25 of docs/specs/exsheet), on `/sheet` under both Chromes and on
  `/sheets`: the edit neither committed nor discarded when the positions grid, a page button or
  nothing takes the keyboard — the positions grid is in the Sheet's Pointing Scope, so the keyboard
  leaves for a page button first and the grid is an ordinary grid when it is pressed — and the other
  grid's keys its own; a press back on the rows
  pointing with the keyboard back in the Cell Editor or the Formula Bar, or committing and
  giving the grid the keyboard; a press back on a column heading or a Row Heading; a committing
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
- `key-field.spec.mjs` — the Keyboard Field (ADR-0080, ticket 80 of docs/specs/exsheet) on `/sheet`
  under both Chromes, `/sheets`, `/features`, `/wide` and `/cells?editable=1`: with a cell
  selected, DOM focus on the field inside the root, unseen, over the Focus cell; a composition made
  through the DevTools protocol (`Input.imeSetComposition`, `Input.insertText`) drawn over D10 with
  nothing moving, its end opening the Cell Editor holding it and Enter committing (ED-30, i1); a
  cancelled one leaving an empty edit (i2); a press on D12 mid-composition putting the text in D10;
  two compositions behind 150 ms appended in order; the sixteenth run's k6, one key ending a
  composition and starting the next (both sent at once), with DOM focus kept in the field until
  the second ends and D10 holding both, and a third clause started the same way while the editor's
  request already waits; copy and paste from the field; a field per
  Sheet (ADR-0018); read-only over a cell that does not edit. The field as the one tab stop, Tab in
  from before and Shift+Tab in from after, then out (A11Y-4), and a display-only grid's root
  likewise; on `/features` under both Chromes, no column's ▾ reached by Tab or Shift+Tab, on the
  grid that edits or the one that does not, and a press on one still opening its popover; the release of Tab ending when DOM focus leaves, on a grid with a field and one without
  (KB-8); the root's ring after Tab and not after a click (KB-12); and over the DevTools protocol's
  `Accessibility` domain, the focused node the field and its active descendant the Focus cell, or
  the chosen action's button while Interactive (A11Y-21). A real IME is the sixteenth Windows run's.

`sheet-helpers.mjs` is what those five share: opening `/sheet` under either Chrome and waiting
for its Linked Table, a Sheet's grid, the positions grid beside it, a cell by its A1 address, the
editor surfaces under either Chrome, the Name Box, and painted-box comparisons.

`keyboard.mjs` says where a grid's keyboard is (ADR-0080). A grid that edits holds the keyboard,
with no edit open, in its Keyboard Field, which is also its one tab stop and carries
`aria-activedescendant`; a display-only grid holds it on its root. So "the root holds DOM focus"
is asserted as `expectKeyboardOn(grid)` — its own field, or its root where it has none, never the
root of a grid that has a field — and never as `expect(grid).toBeFocused()`, which on a grid that
edits is false whenever the keyboard is the grid's; the Focus is read with `expectActiveDescendant` or `activeDescendant`, from whichever
element carries it; and "the grid is interactive" is `expectTabStopTaken`. A grid's root is still
asserted directly where the root is the point: a display-only grid's tab stop (A11Y-17), a
Prerendered root's attributes (A11Y-20).

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
- A screenshot writes to the page. Before it, Playwright makes the caret transparent with an
  inline style on every field, and after it, it puts back what was there, which leaves
  `style=""` on a field that had no style. A trace that shows the attribute between two pictures
  shows Playwright's write, not a render: DC-48's failure on the Server host was first read as a
  late render for that reason (`claude/exsheet-cell-format`, 2026-10-02). `stillPictures` does
  not count the write; a check of its own that compares markup across a screenshot has to leave
  it out too.
