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
   end, not this one. Now every context the harness makes, and every one a spec makes through `ownContext`, sends each
   page's beacon itself, through Blazor's own `disconnect`, with a token of the harness's, once the page's connection
   is closed (sent while it stood, the beacon disposed the circuit under a page still talking to it, and the host
   answered with "Circuit has been shut down due to error.", a console error in the next test); the Server DemoHost keeps
   the tokens of the beacons it has read, and the context closes once each one is read: the beacon, not a time. A
   page closed alone goes the same way. The spec lint refuses `browser.newContext` in a spec (`OwnContext`), and the
   host's error for a cut beacon stands.

## Still open

- **Cause 4**, the edit opened on a row its predecessor's write replaced, did not reproduce in 25 local runs
  of its seed on the WebAssembly host, 15 of them at a quarter of the CPU. Its trace was not kept. The seed
  joins the fixed ones once it reproduces and is fixed (`differential.spec.mjs`); until then the morning
  reader watches for it.
