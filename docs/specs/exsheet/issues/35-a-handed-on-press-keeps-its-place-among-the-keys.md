# 35: A press handed on keeps its place among the pointing Sheet's keys

Status: done

**What to build:** ADR-0058, "On a circuit", the second bullet, and ADR-0021's note of 2026-09-30.
On a circuit, the text a press on a registered grid writes reaches the Sheet's field a round trip
later, and a key typed meanwhile would replace it. The pressed grid tells the Sheet's root, in
script, that a press was handed on, and the Sheet holds its keys behind it.

**Blocked by:** Ticket 34 (the hand-over). Its layer 3 needs ticket 37 (the Scope on `/sheet`).

- [x] While a grid is pointed at, its render names the root to tell. The declaration of ticket 34
      carries it (DC-54)
- [x] In `ex-grid.js`, `onPress`: for a primary press on the rows or a header while pointed at,
      dispatch one event on that root. The module keeps no registry of instances, and nothing is
      added on `document` or `window` (DC-54)
- [x] The listener on each root hears that event and starts the hold it starts for a press on its own
      rows (`holdBehindPress` / `askAboutPress`). The hold ends when the core has answered the press.
      The Sheet's core answers once the Scope has written or refused; a press the Scope did not take
      is answered too, so no hold waits for the two-second fallback (DC-54, ADR-0010)
- [x] The press does not overtake the keys typed before it that the Sheet still holds (found while
      building ticket 37: on the Server host at 0 ms, `=1+` and a press at once gave `=XLOOKUP(...)`
      in 2 runs of 4). The Scope answers a handed-on press only once the Sheet's listener has passed
      the event's place in its queue (ADR-0021's note, widened 2026-09-30; DC-54)
- [x] The comments on the allowlist at the head of `ex-grid.js` name the event
- [x] Script-shape tests: no layout read, no listener on `document` or `window`, no module-level
      state for another instance (DC-54)
- [x] Layer 3 on `/sheet`, Server host, 150 ms injected: `=`, a press on a PV cell and `*` at once
      gives `=XLOOKUP("R-4471", Positions[Id], Positions[PV])*`; `=SUM(1,`, a press, then `)` and Enter
      at once, commits `=SUM(1,XLOOKUP(…))`, never `=SUM(1,)`; `=1+` and a press at once, at 0 ms and
      at 150 ms, gives `=1+XLOOKUP(…)`, never `=XLOOKUP(…)` (DC-54)

## Comments

2026-09-30, implemented on `agent/pointing-scope-35`.

- **Naming the root.** Every grid's root now carries an id, `ExGrid.RootId` (`ex{N}-root`, unique on
  the page). The declaration of ticket 34 gains `GridPointedAt.PointingRootId`. While the grid is
  pointed at, its render writes that id as `data-ex-pointed-from` on its root. The Scope sets it from
  the pointing Sheet's grid (`IPointingSheet.RootId`) before it declares the grid pointed at, so the
  render that paints the grid pointed at names the root too. The module keeps no registry: the root
  is found at each press, from the attribute, with `getElementById`.
- **The event.** `ex-press-handed-on`, a cancelable `CustomEvent` with no bubbling, is dispatched by
  `handOn` in `ex-grid.js`. It fires for each primary press on this grid's own rows or headings as
  the press goes on to Blazor: at once, or at its replay if the grid held it itself. Its `detail`
  is built afresh for that press inside the pressing instance: `inTurn()`, and `answered`, a promise
  the pressing grid's own core settles. The listener on each root (`onPressHandedOn`) takes the
  event (`preventDefault`). With nothing held and nothing answered, it calls `inTurn()` at once and
  holds the keys after the press behind `answered`, as `holdBehindPress` does. Otherwise it queues
  `{ handedOn }` behind the keys it holds, and the drain calls `inTurn()` when it reaches it. A press
  on the Sheet's own rows does not pass on ahead of a handed-on press still being answered. An event
  nobody takes lets the press go on at once. Disposal lets go of a press still waiting for its turn.
- **Where the wait is (decided with the orchestrator).** It is in ExGrid's hand-over, not in the
  Scope's `OnPressAsync`. The pressing grid's script tells its core of the press first, in the
  capture phase and ahead of Blazor's own dispatch (`PressHandedOnAsync(number, inTurn)`), and
  later, if needed, that its turn has come (`PressInTurn(number)`). The core hands the press to
  `OnPress` only once it is in turn, and a drag from it only after it. The task `PressHandedOnAsync`
  returns completes once `OnPress` has completed, or at once for a press that handed nothing over
  (dead space past the last column, say). That task is `answered`, so no hold waits for the
  two-second fallback. A Sheet's own core could not answer a press it never heard of, nor tell which
  press was which when a render lags a keyboard moving between Sheets. The rule is ADR-0021's: the
  Scope answers only once the Sheet's listener has passed the place.
- **Found by layer 3.** A press in turn at once was handed over inside Blazor's dispatch of it, and
  the render of the write goes out only as that dispatch returns. Answered from inside, the answer
  reached the browser 17 ms before the render, and the `*` held behind the press was typed into `=`.
  The answer now yields past the dispatch.
- **Tests.** Layer 2:
  - `HandedOnPressTests` (9): the root's id; the attribute only while pointed at; out of turn,
    handed over once in turn; in turn at once; dead space answered at once; a secondary press
    claims nothing; a press told of and never heard; a drag behind its press; disposal.
  - `PointingScopeTests` (+2): the Scope names the pointing Sheet's root, and the right's once it
    points; and `=1+` typed before an out-of-turn press is written before the lookup.
  - `ShippedStylesheetTests`: a new script-shape test. The listener allowlist now matches
    hyphenated event names; it had skipped them.
- **Layer 3.** `pointing-scope.spec.mjs` (+4 tests), headless on macOS, chrome, Server host on a
  private port:
  - Before (src/ at 0dd013f, the new spec): 20 runs of 20 failed. The `*` was lost; `=SUM(1,)`
    committed (the cell showed 1); `=XLOOKUP(…)` without `1+` in round 1 of every run, at 0 ms and
    at 150 ms.
  - After: 20 of 20 and 8 of 8 passed; the `=1+` tests play 4 rounds per run, 28 rounds at each
    latency.
  - The two files `pointing-scope` and `edit-stands` on Server: 47 passed. One test's teardown
    hung for 16 minutes (`Tearing down "page" exceeded the test timeout`); it passed 10 of 10 when
    rerun alone. The same hang struck a base run too, so it is left as an environment issue.
  - The two files on WebAssembly: 37 passed, 11 skipped (the Server-only tests).

