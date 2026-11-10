# Layer three's reds are fixed by their cause, and the cause is guarded

*(2026-11-03. v0.2.0-beta.2's release gate went red on `pivot.spec.mjs:191`, the fifth red on an ExPivot
drag since #506, and the user asked, for the fifth time, that the browser tests be reviewed and made
stable as a whole, not the one that fell. This ADR records what the reds of the three weeks before it came
from, what each was, and how each cause is kept from coming back. It amends
[ADR-0055](./0055-find-is-asked-of-the-consumer-like-sort-and-filter.md), whose step still out was called
off by the next, and [ADR-0220](./0220-a-test-reads-once-the-grid-says-it-has-settled-and-a-red-is-never-settled-by-a-re-run.md),
whose defect process it gives a morning reader.)*

## What the reds were

Every layer-3 red on `main`, on a pull request's own head and in the nightly Hunt from 2026-10-13 to
2026-11-03 was read and sorted by what made it, not by the test that showed it. The Hunt had failed every
night since at least 2026-10-25. What remained after the fixes already merged came to four causes:

1. **A drag Playwright emulates.** Chromium's mouse in Playwright decides whether a move began an HTML5
   drag by reading, one task after the first `mousemove` its page script hears, whether a `dragstart`
   came. A page that hears the move at the next frame has not answered by then. Playwright then takes it
   that no drag began while the browser has begun one and handed it to the DevTools Protocol to carry, and
   the page hears no mouse event at all until a drop or a cancel nobody sends. A test whose drop should
   change nothing passes without its drop having been made; one whose drop should change something fails
   a minute later on a page that no longer answers the mouse. #506, #508, #515 and #520 each fixed the
   test it showed in (a held press, the drag start judged, a journal, a leak check). All eighteen
   `dragTo` in `pivot.spec.mjs` went through it.
2. **A call to the browser called off as the circuit closed** (CON-6). A Blazor component disposed while
   its circuit closes has its JS interop calls called off or its reference let go, and a call made in the
   renderer's own turn raised that into the circuit, which the host logs as an unhandled exception and the
   harness fails the test for. #514 fixed ExBoard's disposal and #520 ExGrid's source change; the same call
   stood unguarded in ExSheet's, ExPivot's, ExBoard's and ExChart's updates.
3. **A Find step taken while the one before it was out** (`find.spec.mjs:147`, Edge, 2026-10-30 and
   2026-11-03). The test clicks Next, ticks Match case and clicks Next at machine speed. Where the first
   step's answer had not come back when the second was taken, ADR-0055's rule called it off: the second
   step started from the Focus the first never moved, and the outcome depended on how long the source took
   to answer. That is a product race, not a test's.
4. **An edit opened on a row the write before it had replaced** (`differential.spec.mjs`, ExGrid, seed
   366431211, WebAssembly on Edge, one run in ten on 2026-10-31). Typed fast, the edit after a commit
   carried the row from before that commit's write; the Consumer's handler did not find it, and the
   edit was lost. Still open: see below.

## 1. Drags go through `drag.mjs`, and nothing else drags

`tests/browser/drag.mjs` drives the browser's own drag and drop over the DevTools Protocol: a real press
and a move past Chromium's threshold (`Input.dispatchMouseEvent`, with `Input.setInterceptDrags` on), the
page's own `dragstart` waited for, then each drag event sent with the data `Input.dragIntercepted` handed
back (`Input.dispatchDragEvent`), and the end judged by the page hearing a move with no button held, which
a page in a drag never hears. Not by `dragend`: a drop that changes the layout takes the source away. A
drag that does not begin is said, naming what the page heard, rather than read as a refusal.
`dragAndDrop(page, source, target)` drops; `startDrag` returns a drag that a test moves `over` targets and
ends with `drop()` or `cancel()`, for what the page shows mid-drag.

Two guards keep the cause out:

- **The spec lint refuses `dragTo` and `dragAndDrop`** (`SpecLint.Rule.EmulatedDrag`), and no comment lets
  either through.
- **The fixture makes both throw**, and counts every `dragstart` a page hears: a test whose page began a
  drag `drag.mjs` did not drive fails, naming the count, as a leaked listener does.

*Option rejected: wait longer before the first move is judged.* It narrows the window and keeps the race
(the spine's rule 6), and #508 already did it once.

## 2. What a closing circuit throws is let go in one place

`ClosingCircuit.Ended(failure, disposed)`, compiled into every Blazor adapter, says whether a call's
failure says only that the page has gone: `JSDisconnectedException`, or, once the component is disposed,
`OperationCanceledException` or `ObjectDisposedException`. Every call made in the renderer's own turn — a
parameter's update, a source's change, a state sent after a handler, a notice, ExChart's mount — lets go
of exactly that and raises anything else. A failure while the component stands is still a failure.

## 3. A step waits its turn, as a commit does

ADR-0055 said that *a step asked while one is out cancels the one that is out, whose answer is
discarded*, and ADR-0350's note of 2026-10-27 that *stepping again calls off the step still out*. Both
predate the Input Order ([ADR-0240](./0240-input-to-one-product-instance-is-taken-in-one-order.md)), whose
section 1 puts Find's Next in the order and whose section 3 holds the inputs behind a handler that waits on
the Consumer. They are replaced:

- **While a Find step's or an error step's answer is out, every input made in the grid is held**, in its
  order, and taken once the answer has landed, as behind a Pending Commit (ADR-0380): a Next clicked
  straight after another steps on from where the first landed, and a tick or a letter typed after a step
  leaves the step as it was asked. The root carries no Pending Commit stamp, since nothing is committed,
  and shows no Settled Mark meanwhile, as before.
- **A Chrome slot's call is held with them.** A Framework Adapter's contents reach the core as calls, not
  as events the root holds: on Blazor a MudBlazor click crosses to .NET and comes back as `next()` a round
  trip later. A step, a change or a Replace a find panel's contents call for while an answer is out takes
  its place behind the inputs held so far.
- **An Escape whose keydown sees the step out cancels it** (ADR-0240 section 12): its signal is aborted,
  nothing moves, no "No match" is claimed for cells nobody read, and the Escape closes nothing. A step given
  up otherwise — the panel closing, the Consumer placing the match itself (`placeMatch`), the grid going —
  holds nothing from that moment.

*Option rejected: keep the call-off, and have the test wait for each step.* The test drives as a user and a
script do; a result that depends on how fast the source answers is the race the spine's rule 6 forbids,
and the user who clicks Next twice means two steps.

## 4. Each morning a reader sorts the night's reds by cause

A scheduled task reads the night's Hunt and `main`'s CI each morning, sorts what failed by cause, fixes a
cause where every spec that meets it is fixed with it and a guard stops its return, and opens a pull
request that merges on its own green head. A red it cannot explain is recorded as such, never re-run until
it passes ([ADR-0220](./0220-a-test-reads-once-the-grid-says-it-has-settled-and-a-red-is-never-settled-by-a-re-run.md) section 3).
Per-push CI is not lengthened for this: the Hunt is where many passes run.

## The first widened night, 2026-11-04

The Hunt of 2026-11-04 ran every test ten times green; its widened run (150 ms round trip, a quarter of the CPU)
failed five tests in four jobs, from four causes:

1. **The latency proxy let a connection's end through on its last chunk's heels** (CON-6, `navigation.spec.mjs`'s
   RI-3 on both browsers). A page that goes away sends its disconnect beacon and closes the connection about 50 ms
   later. The proxy delayed the beacon by the trip and its end by nothing, so the host read the request with the
   connection's end right behind it, took it as called off and answered 499: 24 beacons of 26 that night, none of
   110 in the plain Hunt. The circuit stayed for its retention period, and a call to the browser in flight then was
   called off by the circuit's call timeout a minute later, an unhandled exception in whatever test was running. The
   end now takes the trip as a chunk does, and the Server DemoHost logs a cut disconnect as an error, so a cut
   beacon fails the test whose page went away rather than one a minute on. The product's handling stands: a call
   timed out on a circuit that may yet reconnect is a failure, not a closing circuit.
2. **The report's grid said it had settled while the pivot's question was out** (`differential.spec.mjs`, ExPivot,
   seed 1, WebAssembly). A product race, fixed in the product: [ADR-0220](./0220-a-test-reads-once-the-grid-says-it-has-settled-and-a-red-is-never-settled-by-a-re-run.md)'s
   note of 2026-11-04.
3. **A drag's press point was read before the page stopped moving its source** (`pivot.spec.mjs`, report removal
   under the MudBlazor Chrome, Server host): `drag.mjs` refused the press, as it should, naming the caption of
   another entry. It now reads its points once the circuit is quiet and the element's box has stood still for two
   frames, and reads what is drawn there in the same task. Every drag goes through it (section 1).
4. **A .NET Undo that got no answer** (`script-sheet.spec.mjs`, Server host): the page's Undo was clicked and its
   status never changed in a minute. It did not reproduce in three local runs under the same conditions. The likely
   cause is a press on a button the circuit disabled between Playwright's check and its press (the page disables it
   while an edit is open, and the edit's end and its render are a round trip behind); Chromium tells the page of
   such a press and makes no click. The harness now says any press that lands on a disabled control as a console
   error, so the next one names itself; the two tests that press one on purpose name the error.

## The second widened night, 2026-11-05

Every test ran ten times green on the WebAssembly and React hosts; on the Server host, twenty-seven runs of five tests failed in eight jobs,
from two causes:

1. **A held key's text was put in from script, outside the field's undo history** (`differential.spec.mjs`, ExGrid,
   seed 1165771808, ten runs of ten on each browser and once each widened; it reproduced locally at once). Keys
   typed while a commit's answer was out are taken again after it, and what their default would have done was done
   with `setRangeText`, which joins no undo history: the `Ctrl+Z` that followed undid nothing, and `7.5Bravo 2` stood
   where the same keys typed one at a time were undone to `7`. A product race, fixed in the product:
   [ADR-0380](./0380-a-verdict-or-a-refusal-may-be-answered-later-and-the-next-input-waits-for-it.md)'s note of
   2026-11-05. The workspace's lint refuses `setRangeText` outside the one place that edits through the browser
   (`ex-grid/no-script-edit`), and the seed joins the fixed ones.
2. **The harness closed a browser context with a page's disconnect beacon on its way** (CON-6, through the guard of
   the night before: `navigation.spec.mjs`'s RI-1 on both browsers, and the first test after a context closed in
   `circuit.spec.mjs`, `grid-live.spec.mjs` and `shipped-files.spec.mjs`). Closing a context ends every request its
   pages still have out; a beacon sent as the page hid had not reached the host over the round trip, the host answered
   it 499, and its circuit stayed. The proxy's fix of the night before took the cut that came with a connection's
   end, not this one. Now every context the harness makes, and every one a spec makes through `ownContext`, closes each
   page before its circuit is left, and then sends the page's disconnect itself, to the host's own port, going on once
   the host has answered it: the answer, not a time. The disconnect is Blazor's own, taken by calling its `disconnect`
   with the beacon held back and said not to be queued, so the page goes on talking to its circuit until it closes, and
   the beacon of its own pagehide is held back too. A page closed alone goes the same way. The spec lint refuses
   `browser.newContext` in a spec (`OwnContext`), and the host's error for a cut beacon stands.

   The leaving was replaced twice the same day, and both first readings were wrong in the same way: each left a page
   standing that its circuit no longer answered. Sent while the page's connection stood, the beacon disposed the circuit
   under a page still talking to it, and the host answered with "Circuit has been shut down due to error." (PR #534's
   first CI run, a console error in the next test). Sent once the connection was closed, through a beacon Blazor took as
   queued, Blazor then took its circuit as disposed and refused every later call the page made to .NET, "The circuit
   associated with this dispatcher is no longer available." (main's CI after #534, `popovers.spec.mjs` under the
   MudBlazor Chrome, an uncaught error heard by the next test, because what a closing app says is the next test's).
   That version also had the host keep a token of each beacon read; the host's answer to the harness's own request
   says the same, and the token went. `harness.spec.mjs` now leaves a page's circuit and drives the page before it
   closes, and fails by that error if the page is left unable to talk to its circuit.

## The third widened night, 2026-11-06

The React host ran green, ten times and widened. Twenty-four runs of three tests failed in six jobs, from three causes:

1. **The differential test typed an Escape while a commit made six inputs earlier still waited for its verdict**
   (`differential.spec.mjs`, ExGrid, seed 1360432973, the night's own seed: ten runs of ten on each browser on the
   Server host, and once each widened; it reproduced locally at once). The sequence committed an edit with an
   ArrowLeft and typed an Escape after six more inputs, meant for the edit typed just before it. Sent one at a time,
   the commit had landed and the Escape closed that edit. Sent at machine speed, every input after the commit was held
   behind its verdict, so the Escape's keydown still saw the commit pending and cancelled it, as
   [ADR-0380](./0380-a-verdict-or-a-refusal-may-be-answered-later-and-the-next-input-waits-for-it.md) section 3
   decides, by what the keydown sees. Not a product race: the one input that acts ahead of its turn
   ([ADR-0240](./0240-input-to-one-product-instance-is-taken-in-one-order.md) section 12) was drawn where its outcome
   depends on the speed. The steps kept an Escape away only from the step straight after a commit; a commit's wait at
   machine speed lasts until its answer, however many inputs follow. A grid's steps now settle before an Escape drawn
   after any step that may have committed an open edit, and the differential test throws by name when an Escape is
   about to be typed against a grid that shows a Pending Commit (`data-ex-pending-commit`), so a sequence that forgets
   the settle fails as that, not as a difference between the runs. The seed joins the fixed ones.
2. **A press on a page's Undo that the page greyed out under it** (`script-sheet.spec.mjs`, Server host, widened):
   the first widened night's cause 4, now named by its guard, `PRESSED_DISABLED`. The page greys its commands out
   from ExSheet's `EditingChanged`, a round trip after the editor opens and closes; the test pressed Undo as soon as
   the editor was gone, Playwright found it enabled, and the render that greyed it out landed before the press. The
   test's helper that types an entry (`enter` in `sheet-helpers.mjs`) now answers once the circuit is quiet, so every
   test that presses a command after an entry aims at what the host has said.
3. **The WebAssembly runtime stopped with `memory access out of bounds`** (`navigation.spec.mjs`'s RI-3, WebAssembly
   on Chrome, one run of 660, at a quarter of the CPU), inside the runtime's own code, after which .NET had exited and
   the page answered nothing. Not explained. It is not the runtime running out of memory: six passes over every page
   in one document, at a quarter of the CPU, grew the WebAssembly memory to 687 MB in the second and not after. Its
   trace was kept on CI only. The morning reader watches for it.

## The fourth widened night, 2026-11-07

Every test ran ten times green on all three hosts. One run of one test failed widened, and main's CI on the
release candidate's commit (b3c0adb9, the evening before) failed one test, from two causes:

1. **The browser's own undo history in a field depends on when the page repaints** (`differential.spec.mjs`, ExGrid,
   seed 1165771808, on the WebAssembly host in main's CI; the seed the second widened night fixed on the Server
   host). An edit opened on `7`, then `.5` and `Bravo 2` typed and `Ctrl+Z`: one key at a time it undid to `7`, at
   machine speed to `7.5`. It reproduced locally once the commit before the edit was answered 1 to 3 ms late. The
   browser closes a field's typing run whenever anything else in the document is removed or has its text changed
   (measured in Chromium: a `div` removed, or its text changed, between two keys ends the run; one appended does
   not), and the cells the earlier commit cleared repainted between the `5` and the `B`. Held keys were not the
   cause: they joined the run as typed keys do. A product race, fixed in the product: an open edit keeps its own
   history, [ADR-0380](./0380-a-verdict-or-a-refusal-may-be-answered-later-and-the-next-input-waits-for-it.md)'s note
   of 2026-11-07, and `Ctrl+Z` never asks the field's. The guard is the seed, now deterministic, and the layer-2 test
   that types beside a page changing its text and asks the edit's `Ctrl+Z`.
2. **A row Action clicked while the circuit was down was not said lost** (`circuit.spec.mjs`, the Server host on
   Chrome, widened, one run). The key typed after the reconnection was answered, so the grid let go of it, but the
   live region stayed empty for a minute. Not explained, and not reproduced in twenty local runs at a quarter of the
   CPU behind a 150 ms round trip. The host's log shows one reconnection on the same page, no reload. Its record was
   empty: the input journal the harness prints for a failed test was kept by the C# core's script, and nothing has
   kept one since the C# core was removed. The Blazor adapter now keeps the journal of its numbered calls, their
   answers, how far the numbers were told and which were lost, so the next such run says whether the click was ever
   numbered; a layer-1 test holds that the root carries it. A targeted local run then failed the same test another
   way: Blazor raised no refused send, so the click had crossed before Blazor heard the connection close. The test
   cut the connection and made its input at once, while the cut was still reaching the browser, so its input took
   either path, a send Blazor refuses or one written to a socket the browser had not yet seen closed. Both tests that
   make an input while the circuit is down now make it once Blazor's own reconnection display shows, which Blazor
   adds as it hears the close: the path their names state. Whether that path is the night's failure is not known;
   the journal says next time.

## The review of 2026-11-08

The Hunt had failed every morning, and each morning one red was fixed by its cause. Asked to stop waiting for the
next one, the whole suite was read for the kinds of cause the reds since 2026-11-04 came from, and the differential
test was run on seeds no night had drawn.

1. **A test aimed at a control while the Server host still owed renders.** Four of the reds since 2026-11-04 were one
   kind: a drag's source read before it stopped moving (11-04), a page's Undo found enabled before the circuit greyed
   it (11-06), an input made before Blazor heard the connection close (11-07), a Razor slot's control focused before
   its contents stood (11-08). Each was fixed where it showed. A locator's action finds, checks and aims at its element
   before it acts, and that is a reading of the page (ADR-0220). On the Server host every locator action the harness
   gives a test now waits for the circuit to go quiet before it aims (`fixtures.mjs`, `aimAtWhatTheHostSaid`). Keys
   typed, or a fill made, into the control that already holds the keyboard wait for nothing: they are the user's own
   input, not an aim. The page's mouse and keyboard never wait. A test whose subject is an input made before the host
   has answered skips the wait by what it does: it sets its own round trip, holds the host's answers or cuts the
   connection, or makes the action inside `atOnce`, and says so in its title. A page that streams, whose live data
   keeps the circuit busy for as long as it runs, declares `hostStreams`: its presses aim at once, and it reads what
   all the host will say with `circuitQuiet()` once it has stopped the stream (`pivot-live`, `grid-live`,
   `grid-live-local`; waiting for a quiet the stream never gives failed every press that turns a stream off).
2. **Seeds no night had drawn.** Each night adds one new seed per product, so a race that one seed in eight shows
   takes a week to find. The Hunt can now be dispatched with a range of seeds, which it splits over sixteen jobs
   (`diff_seeds`, read by `seedsOf` as `EXGRID_DIFF_SEEDS`), and with a wider round trip and a slower CPU
   (`rtt_floor`, `cpu_throttle`). Forty seeds per product on the Server host, run locally, found five ExGrid reds and
   none in ExSheet or ExPivot, from two product races, each deterministic on its seed:
   - **The keys after a clear, an undo or an Action did not wait for .NET** (seeds 2002, 2025, 2032). Blazor's
     ExGrid sent each to .NET and took the next key at once, against rows .NET had not yet rewritten: `Ctrl+Z` then
     `F2` opened the editor on the value the undo was taking back, and its commit wrote it again. The Razor
     component had run the handler in the input's own turn. Each now holds the inputs after it until .NET has
     answered, as [ADR-0240](./0240-input-to-one-product-instance-is-taken-in-one-order.md)'s note of 2026-11-08
     says. It is likely cause 4 of the reds above too, the edit opened on a row its predecessor's write replaced:
     seed 366431211 types `Delete` and then `Backspace` on a row the clear rewrote. That seed never reproduced, so
     cause 4 stays open below.
   - **A press made while an edit's commit was out landed on another row** (seeds 2006, 2038). The browser works a
     press's offsets into its target out only when they are first read, against the target and the layout as they
     stand then. The press that committed, taken again once the commit landed, had its offsets read only then, after
     the editor had gone, and the Focus went 13 rows below the cell pressed, out of view. A held press now keeps its
     offsets as they were when it was made (ADR-0240's note of 2026-11-08).
   - **A press on the editor a commit then closed landed near the grid's first cell** (seeds 2043, 2051, 2054, found
     by a second sweep of ExGrid's seeds 2040 to 2062 with the two fixes above, and by the code review of them at
     the same time). Pressed while the editor stood under the pointer, the press was taken again on what stood at its
     point once the editor had gone, with its offsets into the editor: a few pixels, which the rows read as their
     first cell. Its offsets are now worked out by the browser from the point, in what it lands on. The three seeds
     failed with the fix taken out and passed with it, each time.
   The guards are a layer-2 test for the first two (`bridge.test.ts`, `grid-pending-commit.test.ts`), each failing on
   the code before it, and seeds 2006, 2025 and 2054 among the fixed seeds, one for each; the third needs a layout,
   which layer 2 does not have. With all three fixed, ExGrid's seeds 2040 to 2099 then ran green on the Server host,
   sixty of sixty.
3. **A press made while a stream runs, to stop it.** `pivot-csv.spec.mjs`'s Cancel is pressed while a large file streams
   to the Server host, which keeps the circuit busy until the press stops it: it is made inside `atOnce`. No other
   test presses while a stream of its own runs.

## The night of 2026-11-10

No test failed. Two jobs did not finish: the Hunt's Server shard 4, in Edge and in Chrome, ran out of their 150
minutes with 932 of 1,080 repetitions done, and the run ended red with nothing failed and no records kept. One
cause, and a second found beside it:

1. **The shards were weighed by a count of `test(` lines read from the source.** The differential test is one such
   line drawn once per product and fixed seed, 33 tests that night, and the suite's longest: each runs its scenario
   twice behind a 150 ms round trip, 15 to 30 seconds. Weighed as one test, it went to a shard judged the lightest,
   which was then given 108 tests where the others had 52 to 100; its ten passes alone took close to two hours. Since
   the review of 2026-11-08 added three seeds, the shard that held it had already taken 126 of its 150 minutes the
   night before. `shard-files.mjs` now weighs the
   tests Playwright itself lists for the host, every one a loop draws included, and a test tagged `SPREAD` has its
   file's tests go round the shards one at a time instead of staying together: the differential test's, three or
   four to a shard.
2. **A shard ran tests of files it did not name.** Playwright reads a file argument as a pattern, so a shard given
   `pivot.spec.mjs` also ran `script-pivot.spec.mjs` and `measure-pivot.spec.mjs`: thirty tests a pass on the Server
   host ran in two shards each, and `counted-run.sh`, counting what Playwright listed for the same arguments, could
   not see it. CI's repeat of a changed spec (`browser-repeat`) met the same. A shard is now handed on as a list of
   tests (`--test-list`), which names each test exactly, and `counted-run.sh`, which every repeated run goes through,
   hands a spec file named on its command line to Playwright as that file alone.

The guards: `shard-files.mjs` reads its list back through Playwright and refuses one whose every line does not match
exactly one test, so a test cannot fall out of every shard unseen, and the Hunt's own pull-request check now runs the
differential test, so every shard's list is drawn and read back there too. A long test nobody tags `SPREAD` is not
prevented; it is found: every Hunt job, the seed sweep included, stops its run with `--global-timeout` about half an
hour before the job's own limit, and `counted-run.sh` counts only the repetitions that ran (Playwright's stats count
what it never reached as skipped), says what Playwright reported outside any test, and writes the time each spec
file took to the job's summary. A shard that outgrows its job ends red with its records kept and the file that took
the time named, never cancelled with nothing said.

## Still open

- **Cause 4**, the edit opened on a row its predecessor's write replaced, did not reproduce in 25 local runs
  of its seed on the WebAssembly host, 15 of them at a quarter of the CPU. Its trace was not kept. Its steps
  type `Delete` and then `Backspace` on a row the clear rewrote, the race the review of 2026-11-08 fixed, but
  that is read from its steps, not seen. The seed joins the fixed ones once it reproduces (`differential.spec.mjs`);
  until then the morning reader watches for it.

- **The third widened night's cause 3**, the WebAssembly runtime's `memory access out of bounds`: one run, not
  reproduced, its cause not known.
- **The fourth widened night's cause 2**, a lost row Action that was not said: one run, not reproduced. Its test now
  makes its input on one path only, and the input journal names its numbers next time.
