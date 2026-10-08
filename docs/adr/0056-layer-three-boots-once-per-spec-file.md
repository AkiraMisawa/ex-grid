# Layer 3 boots the app once per spec file, and each test mounts its page afresh

*(Decided with the user, 2026-09-28, after measuring where layer 3's time goes and comparing
AG Grid's test suite. Together with ADR-0041's shards, it was the answer to "the browser tests
are too long to develop against".)*

Layer 3 took **24.3 minutes** per run in CI on the WebAssembly host — 498 tests, one worker,
Chrome and then Edge — and 9.0 on the Server host. The CI run's wall clock was the first of these.
A test's own work was the smaller part of it:

- **The time per test was flat.** On WebAssembly the median was 2.25 s, and 356 of the 484 tests
  that passed took between 2 and 3 s. The same tests took about 0.6 s on the Server host. A flat
  distribution is a fixed cost paid by every test.
- **The fixed cost is the app booting.** Playwright gives each test a new browser context by
  default, so every test loaded the page and started the .NET runtime again. Measured on
  2026-09-28 in a Linux container (the bundled Chromium, headless, `/features`, the median of
  eight loads):

  | How the page is reached | Per load |
  |---|---|
  | A new context, as every test did | 1.65 s |
  | One context for all loads, the HTTP cache warm | 1.73 s — 35 MB transferred became 10 MB, and nothing got faster |
  | The DemoHost built in Release | 1.57 s |
  | A trimmed `publish`, served compressed | 1.15 s |
  | An in-app navigation inside an app that has booted | **0.05 s** |

  What costs is the runtime starting, not the download, and only not starting it removes it.

**Nobody had decided that every test boots.** It was Playwright's default, and it bought an
isolation this project never asked for in writing.

AG Grid's primary suite, read in `ag-grid/ag-grid` on the same day, draws the line elsewhere.
Vitest isolates each **file**. The tests in a file share one environment, and each test creates and
destroys its own grids through `TestGridsManager`, whose `reset()` also clears the grid's global
state. The environment's load is a per-file constant that the suite reports as a budget line. The
suite's mean is about 70 ms a test.

## Decision

**A spec file boots the app once. Every test in it mounts its page afresh by an in-app navigation.
Whatever a test changes outside its own page, the harness — not the test — puts back, and the
harness checks that it did.**

1. **The file's first navigation is a real one, to the index at `/`.** The app boots there.
   Every `page.goto` after it is an in-app navigation (`Blazor.navigateTo`) that passes through the
   index. The routed page — and every grid on it — is therefore a new instance for every test. The
   document, the .NET runtime, the grid's JS module and the DI singletons last for the file. A new
   spec file gets a new browser context, and so does a test whose `use` asks for context options
   other than the viewport's, which the harness sets for each test. `page.reload()` stays a real
   reload. This holds on both hosts; on the Server host the circuit lasts for the file.
2. **Between tests the harness restores what it owns:**
   - the viewport, to each test's own;
   - the permissions a test granted, cleared;
   - the round trip, back to 0;
   - the pointer, moved to the corner;
   - the text selection, the scroll position and the focus navigation starting point, as a fresh
     load has them;
   - every change made through `alterPage`, undone. `alterPage` takes a function that makes the
     change and returns the one that undoes it. `watchNextKey` installs its listener through it.
3. **A test changes anything outside its own grids through `alterPage`.** That covers a global,
   a listener on `window` or `document`, and the head, `body`, `<html>` or `#app` element. The
   harness checks it. Back at the index after each test, three things must be what they were when
   the file booted there: the document's markup (comments and an empty `style` or `class` aside),
   the rules in its stylesheets, and the globals on `window`. The natives that tests stub — the
   Clipboard API's methods, timers, `fetch`, `document.hasFocus` — must be the ones the app booted
   with, read as a caller reads them, so a stub on the instance counts as much as one on the
   prototype. **A difference fails the test by name.** The harness does not quietly discard the
   page, which would let the leak's own test pass and the rule rot. What the check cannot see is
   held by rule and review alone: a listener on `window` or `document`, a timer or an observer left
   running, and a native outside the list.
4. **A test gets a document of its own in these cases.** Its navigations are then real, and its
   page is discarded when it ends.
   - **It declares `freshDocument`.** A11Y-20 does, because it is about the prerender and the
     circuit connecting. BIG-7 does, so that its recorded time still includes the boot and stays
     comparable with the records already filed.
   - **It uses a `Page` or context method whose effect outlives a navigation:** `addInitScript`,
     `route`, `exposeFunction`, `emulateMedia`, `setExtraHTTPHeaders`, `setDefaultTimeout`, a CDP
     session and the like.
   - **It reloads the page, or navigates away from the app's origin.** A reload is a boot that the
     file's record was not taken from.
   - **It asks for no viewport emulation** (VZ-14).
5. **Some tests hand no page to the next test:** one that failed — in its body, or in the checks
   the harness makes as it ends — one that left a key or a button held, and one whose console
   reported an error. The next test boots. The harness's checks are soft, so a leak it names does
   not keep the test's console record from being written.
6. **The console record stays per test (CON-1..6).**
   - What the app says while it boots belongs to the file's first test.
   - What a page says as it is disposed belongs to the test that mounted it. The harness
     navigates back to the index inside that test's teardown, and lets two frames pass
     before its verdict, so work the disposal left to a later frame is heard too.
   - Anything said between two tests belongs to the next one: in the browser's console, and in
     the Server host's log, which each test reads from where the last one stopped. What a file's
     last page says as its context closes belongs to the next file's first test.

`harness.spec.mjs` pins points 1 to 5 in layer 3 itself. Its tests come in pairs: the first leaves
something behind, and the second says what it found. A second test skips itself when the first did
not run, rather than passing without having tested anything.

## What it found on its first run

A prototype of this ran the whole suite on the first try, and it failed. **CP-23 replaces
`navigator.clipboard.write` with a stub to provoke a refusal. On a shared document the stub
outlived its test.** The next test's copy was then refused, silently, and it read the clipboard's
previous value. That test failed. The same leak, in another order, would have let a "the clipboard
stays untouched" test pass for the wrong reason. The audit that followed found seven more changes
that would have run under every later test of their file:

- DIR-2 sets `dir="rtl"` on the grid's parent, which is `#app` on WebAssembly and `body` on the
  Server host.
- UX-2, UX-5 and UX-10 set tokens on `body`, and UX-2 also appends a stylesheet to the head.
- A11Y-17's button stays in the document, because Blazor removes only the nodes it made.
- KB-15's `document` listener was never taken off, nor was the listener of the test for the
  printable key on `/cells` when the grid swallowed the key.

Each now goes through `alterPage`, and point 3 is the check that would have found them.

**The first full run under the new model found a defect in the grid itself.** A key that opens a
popover starts a wait: the keys typed after it are held until the popover has DOM focus
([ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md)), and the wait checks again on
every animation frame. It asked whether DOM focus was inside the grid's root before it asked
whether the grid had been disposed. A grid that went away during the wait therefore threw
`Cannot read properties of null (reading 'contains')` from its next frame, after its page had gone.
A test whose context was simply closed never disposed a grid, so no test had ever seen this. A
Consumer's user who presses Alt+↓ as the grid is removed would. The wait now asks about disposal
first, and `popovers.spec.mjs` pins it under both Chromes.

## Rejected

- **One context for the whole run, for its HTTP cache.** Measured above: no faster.
- **A trimmed `publish` as what layer 3 loads.** It is 30% faster per boot, but it changes the
  artifact under test, and with one boot per file it buys almost nothing.
- **One boot per worker rather than per file.** It saves about twenty boots a project — half a
  minute — at the price of letting a leak reach every later file. The file is AG Grid's boundary
  too.
- **Several workers on one machine.** The OS clipboard is one per display. The boot is CPU-bound,
  and a runner has four cores. On the Server host the latency proxy's round trip and the host log
  belong to the whole process. Running more at once is done across machines instead:
  [ADR-0041](./0041-ci-runs-every-layer-and-layer-three-gates-on-linux.md)'s shards. A shard
  boundary is a file boundary, so each shard boots each of its files once.

## Consequences

- **What is given up.**
  - A cold boot per test. The boot is still exercised: once per spec file, per browser, per host,
    and by every test with a document of its own.
  - The runtime's isolation between the tests of a file. DI singletons, static caches and the JS
    module's module-level state carry over. The demo's static data is never mutated. Its two
    stores are DI singletons, already shared by every test on the Server host, where one process
    serves the whole run, and the tests that change them reset them as they open the page
    (`?reset=1`).
- **What is gained.**
  - **The time.** It was measured on 2026-09-28 on one Linux container: the bundled Chromium,
    headed under Xvfb, the `chrome` project, every spec. On WebAssembly the run took **7.2
    minutes, down from 12.1**, and ran 264 tests against 242. On the Server host it took **4.3,
    down from 5.3**, and ran 266 against 244. The added tests are this ADR's own and the
    defect's. About two seconds of each file's time is Playwright closing the previous file's
    context. It finalises that context's trace, and its DOM snapshots are the cost: without
    them the WebAssembly run took 5.1 minutes. The failure traces keep their snapshots, because
    they are what makes a CI-only failure readable.
  - **Every test on a file's page now disposes that page, which a closed context never did.** A
    disposal that throws, or that leaves something behind in the document, is now a failure of
    the test that mounted the page. A test with a document of its own still has its context
    closed.
  - Every test takes the path a Consumer's user takes: navigating inside one app.
- **[ADR-0026](./0026-layer-three-runs-on-playwright-against-the-installed-chrome.md) stands as
  decided:** real browsers, headed, no retries. This ADR changes only what a test shares with the
  rest of its file. `tests/ExGrid.Browser/README.md` says how to write a test under it.
- **The suite stays on one worker, for the reasons that hold.** The OS clipboard, the records'
  read-modify-write and the Server host's proxy and log are all shared. The configuration used
  to give "the tests change each other's zoom" as the reason, and that was never one. The device
  scale is emulated per page, and each worker has a browser of its own.

## How a test waits, and where it reads pixels *(2026-10-02, decided with the user)*

From 2026-09-29, no CI run of `claude/exsheet-cell-format` passed whole. Most of what failed
were defects, and they should go on failing. Some failures were the suite's own: tests that
fail now and then whatever the branch changed. These rules are for those. Each changes when or
where a test reads, never what it accepts. No requirement and no threshold is relaxed.

1. **On the Server host, a reading that has to see all the host will say waits for the circuit
   to go quiet.** `circuitQuiet()` returns once nothing is held in the latency proxy or unsent,
   in either direction, and nothing has crossed for 400 ms. That is longer than the grid's
   longest timer, the 300 ms before an error message opens. The browser acknowledges each
   render batch once it has applied it, so by then the last one is in the DOM.
   - A test first waits for the state it expects. `circuitQuiet` is for what follows: that
     nothing undid it, that something did not happen, or a picture.
   - A fixed wait does not stand in for it. On a slow runner it is too short, and a late answer
     slips past the reading, so the test passes over the defect it is there for.
   - On WebAssembly there is no wire, and it returns at once. A WebAssembly half that needs the
     page's own time keeps its wait.
   - It never waits for ever. It fails after 10 s, naming what kept crossing. A window of 5 s
     or more is refused, because SignalR's keep-alive pings could keep it from being met.
2. **Two pictures to be compared are taken while the page holds still** (`stillPictures`). The
   test draws each way by a mark it sets. If anything else changes the document, or anything
   scrolls, between the first mark and the last picture, every picture is taken again. After
   three attempts the test fails, naming the change. Playwright's own preparation for a
   screenshot is not counted: it writes `caret-color` inline on every field, then puts it back
   and leaves `style=""`. DC-48's failure on the Server host showed that write between its two
   pictures, and it was first read as a late render.
3. **Pixels are read only where what is painted is the requirement**: Excel's pixels, a layer
   hiding another, a blend. What the grid decided to draw is read from the DOM or in layer 2.
   - A pixel is read where the boundary beside it lies on a device pixel. A test that reads at
     an edge checks that first.
   - The only allowance for rounding is `paints`. A channel whose exact value lies between two
     bytes may be either byte: ticket 92's 78.53 came out 78 and 79. A whole value admits only
     itself. Two paints that ought to be one paint are compared with each other exactly.
   - Where the question is position, both pictures are drawn in black on white. A comparison is
     only as strong as its contrast.
4. **A spec file written or changed runs repeatedly before it goes in.** Locally that is
   `--repeat-each` on both hosts. In CI, the spec files a push to a pull request changes run
   three times on each host and browser (`browser-repeat`); the push that opens the pull
   request takes all of the pull request's. `retries` stays 0.
   - *Changed the same day, with the user.* The job first took every spec file the pull request
     changes, on every push. On #42 that was 17 files and 1158 tests (run 36952902974): longer
     than the full run, and 60 minutes were not enough on WebAssembly. Two shards would still
     have taken about 40, and the user judged that to slow development too much. Taking only
     what each push changed keeps the job inside the full run's time. A spec file is still
     repeated in the push that changes it.

5. **A wait's bound tells a hang, never a speed** *(2026-10-08, decided with the user)*.
   Performance never gates (CLAUDE.md), so a test fails on what the app did, never on how long it
   took. Playwright's own defaults broke that quietly: a wait without a bound of its own gave up
   after 5 s, and a test after 30 s, so a pivot laid out again on a Debug build of the hosts,
   correct and slower than CI's Release one, failed `pivot-db.spec.mjs`'s whole-column copy. The
   harness's defaults are now a minute for a wait and five minutes for a test: long enough that only
   something that will never come trips them.
   - A test still waits for the state it expects (rule 1), and reads the moment it holds.
   - A bound shorter than the default is written only where the shortness is the point: a probe
     that expects nothing to be there, or the harness's own tests of its tools.
   - The other spec files' bounds shorter than the default are left to a pass of their own.

`tests/ExGrid.Browser/README.md` says how to use each, and `harness-reading.spec.mjs` pins them.

Rejected:

- **Waiting for the circuit on every navigation.** It costs about 0.8 s a test on the Server
  host, for readings that mostly wait for a state already.
- **Telling the keep-alive pings apart in the proxy.** It would mean parsing masked WebSocket
  frames, to save at most one window every 15 s.
- **A colour tolerance of ±n.** It accepts colours no path paints, and it would have hidden
  ticket 92.
- **Retries** (ADR-0026).
