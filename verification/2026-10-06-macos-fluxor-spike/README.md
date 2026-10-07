# ExGrid over a Fluxor store: a spike

Date: 2026-10-06. Worktree `.claude/worktrees/live-data-fluxor`, detached at `41c8d8c8` (branch
`claude/live-data-next-cc`). No file under `src/` or `samples/` was changed. The spike was
`spikes/fluxor-grid/`, disposable like every spike, and is the only thing that references Fluxor.
It is kept beside this record, not as a project of the repository: [`harness/spikes/fluxor-grid/`](harness/spikes/fluxor-grid/README.md).
Copy it back to `spikes/fluxor-grid/` to run it; nothing builds it from here.

The question: does ExGrid work properly when the Consumer keeps its data client-side in a Fluxor
store (Blazor's Redux), as ag-grid is used with Redux? A trade blotter whose state lives in a Fluxor
feature was built, and wired to ExGrid in the two ways ExGrid offers. Each was driven in headless
Chrome on the WebAssembly host and on a Blazor Server host.

This record decides nothing. No ADR, `CONTEXT.md` entry or Definition of Done criterion is changed.
Where a finding calls for a decision, it is written as a proposal at the end.

## The results in brief

| Check | W1: the store pushes the Window | W2: `GridSource.From` keyed, writes through the store | W2src: writes through the source, then the store |
|---|---|---|---|
| 1. Live ticks reach the screen; changed cells marked; unchanged rows do not render | **Pass** | **Pass** | **Pass** |
| 2. A user edit lands through the store; a following gesture on the cell is not refused (LV-17) | **Pass** | **Fail**: `5` Enter ↑ `7` Enter refuses the `7` as `CellChanged … shows 5.00`, 5 of 5 runs per host; the user's own edit is marked as a change | **Pass** |
| 3. An edit is refused when the cell changed upstream under the editor (ADR-0142) | **Pass** | **Pass** | **Pass** |
| 4. Paste, Ctrl+D, a fill-handle drag and Delete go through the store | **Pass** | **Pass** | **Pass** |
| 5. Under a sort, a tick that moves a row drops the Selection; one that changes values keeps it | **Pass** | **Pass** | **Pass** |
| 6. Memory over 200-tick rounds of 1,000 trades | **Bounded**; no old state reachable | **Bounded**; no old state reachable | (as W2) |
| 7. Server: a store per circuit; the feed's thread reaches the grid safely; no errors | **Pass** | **Pass** | **Pass** |

Every result is the same on both hosts. No console error or warning, no `pageerror`, no visible
`#blazor-error-ui`, and no `warn` or `fail` line in the Server host's log, in any run.

- **W2's failure is caused by the gathering**, not by Fluxor. With the source's `GatherInterval` set
  to 0 (`/w2?gather=0`), check 2 passes on both hosts. The user's own edit is still marked as a
  change.
- **Only ExGrid's kept paints hold old row versions**: at most 63 paints' worth of the painted rows
  that changed (630 of the sampled versions here). They are released when the grid goes. **No old
  store state stayed reachable** at any reading, on either host, in either wiring. Fluxor's
  ReduxDevTools middleware keeps a history of states; it was not registered.

## What was built

[`harness/spikes/fluxor-grid/`](harness/spikes/fluxor-grid/README.md) (see its `README.md` to run it):

- **`FluxorGrid.App`** holds the store and the pages, shared by both hosts. It references
  `src/ExGrid` and `Fluxor.Blazor.Web` **6.11.0**, the latest. Fluxor has had a `net10.0` build
  since 6.9.0. 6.5.2, the newest one in the local package cache, targets `net9.0` and caps
  `Microsoft.AspNetCore.Components.Web` below 10.0.0.
  - **The state**: `TradesState` (`[FeatureState]`) holds an `ImmutableList<Trade>` and
    `record Trade(string Id, string Book, string Currency, DateTime TradeDate, decimal Notional,
    decimal Price, decimal Pnl)`. It starts with 1,000 deterministic trades.
  - **Reducers** replace changed trades with `with` and keep every other instance. They also add
    and remove trades. A feed batch (`TicksArrived`) carries prices and P&L moves, stamped with the
    time by the effect, because a reducer is pure. A user's write (`EditCells`) is one action per
    gesture. Each cell names the trade by id and carries the version the grid judged it against.
  - **The effect** drives the live feed. A `PeriodicTimer` on a thread-pool thread (on Server)
    dispatches a batch every *interval*. Half of each batch falls on the even trades among the
    first 20, the rows in view. The odd ones there never change, so a check can tell a row that
    rendered for nothing. The other half falls on trades from the 40th on. Every 10th batch adds a
    trade at the end and removes one out of view.
  - **W1 (`/w1`)** is a `FluxorComponent` that pushes `Window`, `RowSequenceVersion`, `Sorts`,
    `RowKey` and `CellChangedAt`. It derives the Window from the state: the store's own list
    unsorted, or `GridQueryEngine.Apply` under the Sort the store holds.
  - **W2 (`/w2`)** builds `GridSource.From(state.Trades, t => t.Id, clock)` once. It subscribes to
    the feature's `StateChanged` and hands the source the whole list on every change, through
    `ReplaceAll`, the API for a whole new list (ADR-0141 D7, LV-4). The page is not a
    `FluxorComponent`, and a state change does not render it.
    - `?edits=source` (W2src) first writes a user's write to the source with `ReplaceRow`, then
      dispatches the same instance to the store. The source then pairs the store's next list as
      unchanged there.
    - `?gather=0` sets the source's `GatherInterval` to 0.
  - **Both pages** turn each Edit Intent, paste or fill intent, Fill Intent and Clear Intent into
    one action. Positions are resolved against the Window the grid was handed, and refused when the
    intent's Row Sequence Version is not that Window's (`BlotterIntents.cs`).
  - **Declared keys** stand in for upstream changes. They reach the grid in its turn among the keys,
    with an edit open too (ADR-0050, item 14).
    - F9 amends the Focus cell.
    - F8 gives the trade at position 10 the largest P&L: a tick that moves a row under a sort by
      P&L.
    - F7 moves ten prices with P&L unchanged: a tick that changes values only.
- **The hosts.** `FluxorGrid.Wasm` is a standalone WebAssembly host on port 5899. `FluxorGrid.Server`
  is a Blazor Web App, interactive on the circuit and not prerendered, on port 5898. Both run Debug
  builds.
- **Instrumentation.** None of it changes what ExGrid does.
  - **`RenderCounter`** is a Template column at position 0. A component in it counts its row's
    renders and mounts by trade id. It is handed its parameters again only when its row renders,
    because a `Trade` is a reference type that Blazor treats as possibly changed. It paints nothing
    that changes.
  - **`Census`** holds a weak reference to every state the store produces, and to the first 10
    trade versions each new state replaced. It can also follow a page or a source. It counts what
    is still alive after a forced, blocking, compacting full collection, and reads
    `GC.GetTotalMemory(true)`.
  - **A `MutationObserver`** on the Viewport, installed by the check, counts records per painted
    trade and every `ex-changed` mark that appears, as
    `verification/2026-10-05-macos-live-update-measure` did.
- **`check/check.mjs`** drives the checks with Playwright 1.62.1 against `channel: 'chrome'`,
  headless. Every wait reads what it waits for (principle 6). Each check opens a browser context of
  its own: on Server that is its own circuit and store, on WebAssembly its own app.

## Environment

- Apple M4 Pro, 12 cores, 24 GB, macOS 26.6.2 (25G83). Load average 3.0–3.7. A performance
  measurement agent shared the machine. Nothing here is a timing.
- .NET SDK 10.0.203 through `nix develop`, Debug builds. Node 24.14.1.
- Google Chrome 154.0.8037.98, headless.
- Ports 5899 and 5898 were checked free first. Only the PIDs this run started were stopped.

## Check 1: live ticks, marks, and rows that do not render

**Method.** A page opened with the feed at 60 batches, 50 ms apart, 20 trades each. The 10 even
rows in view changed in every batch, and the 10 odd rows never did. The counters were reset, the
observer installed, and the feed started. The check waited for the feed to end, then for the
screen's Price column to equal what the store's newest state paints (`#expected-prices`). Then it
read the counts.

**Results** (identical on both hosts; `raw/wasm-checks-1-5.json`, `raw/server-checks-1-5-and-7.json`):

| | W1 | W2 | W2src | W2, gather 0 |
|---|---:|---:|---:|---:|
| Renders of the 10 changing rows, over 60 batches | 600 | 130 | 130 | 600 |
| … per changing row per batch | 1.00 | 0.22 | 0.22 | 1.00 |
| Mounts of any painted row | 0 | 0 | 0 | 0 |
| Renders, mounts and DOM mutation records of the 10 unchanged rows | 0 / 0 / 0 | 0 / 0 / 0 | 0 / 0 / 0 | 0 / 0 / 0 |
| DOM mutation records on the changing rows | 1,220 | 280 | 280 | 1,220 |
| Marks: on Price and P&L of changing rows / on unchanged rows / on any other column | 20 / 0 / 0 | 20 / 0 / 0 | 20 / 0 / 0 | 20 / 0 / 0 |

- **Unchanged rows do not render in either wiring.** W1's page renders on every state change, as a
  `FluxorComponent` does. The grid renders with it, and the rows whose instance did not change skip
  their render (ADR-0003). The Row Key keeps every changed row's component (ADR-0140): no row
  mounted.
- **W2 renders a changing row about once per 250 ms gathering**, 13 times in 3 s. W1 renders it
  once per batch, because a push Consumer has no gathering unless it writes one. With gathering off,
  W2 renders as W1 does.
- **The marks are right in every variant.** Each changing row's Price and P&L were marked once and
  stayed marked while the batches renewed them. Nothing else was marked. For W1 the Consumer's
  reducer computed the marks by comparing painted text. For W2 the source computed them.

## Check 2: a user's edit through the store, and the gesture after it

**Method.** On T00001, which the feed never touches:

1. Notional typed `1234567`, then Enter. The check waited for the cell to show it.
2. At machine speed, `↑ 5 Enter ↑ 7 Enter`: the second commit lands on a cell the user's own
   first commit wrote just before (LV-17, ADR-0142 D1).
3. With `3` on the clipboard as a one-cell table, `9 Enter ↑ Ctrl+V`.

This was run with the feed paused and with it running at 50 ms. It was repeated 5 times per host
(`raw/check-2-five-repeats-per-host.json`).

**Results**:

| | W1 | W2 | W2src | W2, gather 0 |
|---|---|---|---|---|
| The edit lands | yes | yes | yes | yes |
| The user's own edit is marked as a change | no | **yes** | no | **yes** |
| `5 Enter ↑ 7 Enter` | `7.00` | **refused: `CellChanged: Notional shows 5.00`**, the cell left at `5.00` | `7.00` | `7.00` |
| `9 Enter ↑ Ctrl+V` | `3.00` | `3.00` | `3.00` | `3.00` |
| Runs alike | 5 / 5 per host, paused and live | 5 / 5 per host, paused and live | 5 / 5 per host, paused and live | 1 per host, paused and live |

**What happens in W2.**

1. The user's `5` goes to the store only. The store hands the source its next list, and the source
   sees a different instance under T00001's key: an ordinary live change (`ReplaceAll` folds every
   change with `byUser: false`, `InMemoryGridSource.cs:366`).
2. The publication just before it was the `1234567`, less than 250 ms earlier. So the `5` is
   gathered, not shown.
3. The `7` opens the editor. By D1 the grid takes the cell's text "as it paints at the open"
   (`ExGrid.SeenText.cs:478`). That is still `1,234,567.00`, because the `5` is in neither the
   paint nor the Window.
4. The commit first asks the source to put out what it has gathered (D5, `ExGrid.razor:7325`). The
   `5` appears, and the commit is refused as an upstream change, with the user's own value as the
   new text.

The paste is not refused, because a paste leaves out a cell the user wrote since the paint
altogether (`ExGrid.SeenText.cs:268`). The editor instead takes what the cell paints at the open.
With `GatherInterval` 0 the `5` is shown at once, the editor opens over it, and the `7` lands.

**This is deterministic, not a race.** It happens with the feed paused, on WebAssembly, whenever
two writes to the same cell come within one gather interval. A user typing at an ordinary pace
gets it when the source is already inside an interval, which is always while a feed runs. The
refusal is on the safe side and gives a reason, but the reason is wrong: the user is told the cell
changed upstream to the value they typed.

**What W2src does.** `ReplaceRow` is the user's own edit to the source (ADR-0141): it is not
gathered and not marked, so the `5` is in the Window before the next key is handled. The store then
receives the same instance, and `ReplaceAll` pairs it as unchanged.

## Check 3: a commit over a cell that changed under the editor

`999` typed into T00003's Notional, then F9 (an upstream amendment of the Focus cell, Notional + 1),
then Enter, then Enter again.

| | W1 | W2 | W2src |
|---|---|---|---|
| First Enter | refused: `CellChanged: Notional shows 1,750,001.00` | same | same |
| The editor afterwards | open, still `999` | same | same |
| Second Enter | lands: `999.00` | same | same |

In W2 the amendment was gathered: the store's list reached the source within 250 ms of its last
publication. The commit put it out first (D5) and refused (LV-16). In W1 the amendment had already
been pushed. On both hosts.

## Check 4: paste and fill through the store

| Gesture | Store's record (all variants, both hosts) | On screen |
|---|---|---|
| A 2×1 block `111`/`222` pasted over Notional rows 5–6 | `paste: 2 cells written, 0 refused` | `111.00`, `222.00` |
| Ctrl+D over Book rows 7–9 (a paste intent with a fill source) | `fill-key: 2 cells written, 0 refused` | rows 8–9 take row 7's Book |
| A fill-handle drag, Price row 11 down to row 13 (a Fill Intent) | `fill-drag: 2 cells written, 0 refused` | rows 12–13 take row 11's price |
| Delete on Book row 15 (a Clear Intent) | `clear: 1 cells written, 0 refused` | empty |

No `OnPasteRefused` in any variant.

## Check 5: the Selection under a sort

A click on P&L's header sorted ascending: in W1 through `SortChanged` into the store, in W2 inside
the source. A two-cell Selection was made at rows 2–3.

- F7 changed the first ten prices with P&L unchanged. The order was kept, and the Selection stayed.
- F8 moved T00076 from position 10 to the end. The Selection was dropped (ADR-0011/0141).

The same in all three variants, on both hosts. W1's Row Sequence Version is the Consumer's: the key
sequence compared with the Window it handed over last.

## Check 6: memory, and what stays reachable

**Method.** A page opened with the feed at 200 batches, 20 ms apart, 50 trades each, one trade
added and one removed every 10th. The census was taken at the start and after each round. On
WebAssembly there were six rounds, 1,200 batches; on Server two. Finally Blazor navigated to Home,
in the same app or circuit, so the store lived on and the grid did not. The census was taken there
too.

**Results on WebAssembly** (`raw/wasm-check-6-six-rounds.json`):

| Reading | W1 heap | W1 old states alive | W1 old trade versions alive (of sampled) | W2 heap | W2 old states alive | W2 old trade versions alive (of sampled) |
|---|---:|---:|---:|---:|---:|---:|
| start | 5,957,488 | 0 | 0 of 0 | 6,026,072 | 0 | 0 of 0 |
| 200 batches | 6,240,264 | 0 | 630 of 2,000 | 6,800,712 | 0 | 170 of 2,000 |
| 400 | 6,261,008 | 0 | 630 of 4,000 | 7,226,304 | 0 | 340 of 4,000 |
| 600 | 6,261,048 | 0 | 630 of 6,000 | 7,256,432 | 0 | 510 of 6,000 |
| 800 | 6,261,056 | 0 | 630 of 8,000 | 7,272,168 | 0 | 630 of 8,000 |
| 1,000 | 6,261,048 | 0 | 630 of 10,000 | 7,274,272 | 0 | 630 of 10,000 |
| 1,200 | 6,301,224 | 0 | 630 of 12,000 | 7,274,344 | 0 | 630 of 12,000 |
| grid gone (Home) | 6,016,064 | 0 | 0 | 7,024,040 | 0 | 0 |

The Server host gave the same counts (`raw/server-check-6.json`: 630 and 170/340 alive, no old
state, 0 at Home). Its heap is the whole process's and holds earlier circuits, so only its counts
are read.

- **No old store state is reachable**, in either wiring, on either host, at any reading. Fluxor
  keeps the current state only. Its ReduxDevTools middleware would keep a history, and was not
  registered (`FluxorRegistration.cs`).
- **Old trade versions are reachable through ExGrid's kept paints, and only through them.** The
  grid keeps what it painted for its last 64 paints (`PaintsKept`, `ExGrid.SeenText.cs:88`;
  ADR-0142), including the row instances.
  - 63 old paints × 10 changing painted rows = 630, the plateau in both wirings.
  - W1 paints once per batch, so it reaches 630 within the first round. W2 paints once per
    gathering, so it reaches 630 after about 64 publications.
  - All of them are released when the grid goes, while the store stays.
  - The sample is the first 10 replaced versions per state, which are the 10 painted busy rows. So
    the bound is about painted rows, as ADR-0142 says.
- **For the separate investigation into paints holding painted text:** with a row type that holds
  no reference to its state, as here, a kept paint costs row instances, never states. A row type
  that referred to its containing state or Snapshot (a row view, say) would make each kept paint
  hold that state. The paints could then keep up to 63 old states reachable.
- **The heap is bounded.**
  - W1 is flat from 400 batches on, within 60 KB.
  - W2 is flat from 600 on. It grew more before that because two things filled up: the paints, and
    `GridSource.From`'s change times. Those are kept for `ChangeTimesKeptFor` (5 s here) and let go
    only as new ones are recorded (`CellChangeTimes.cs:29`).
- **The page left last stays reachable until the next navigation.** This is Blazor's, not ExGrid's
  or Fluxor's (`raw/page-retention-wasm.txt`).
  - After Home, the census found the left page alive. For W2 that included its Grid Source, about
    1 MB at 1,000 trades: W2's Home reading is 1 MB above its start.
  - It does not accumulate: after four visits each to W1 and W2, only the page left last was alive.
  - A control page with no grid, no Fluxor and no query parameter (`/blank`) is retained the same
    way.
  - Not investigated further. A Consumer whose source holds a million rows keeps them until the
    next navigation.

## Check 7: the Server host

- **A store per circuit.**
  - Context A, on W2, edited T00001's Notional to 4,242 and ran its feed for 40 batches.
  - Context B, on W1 in another circuit, stayed at `Ticks: 0` and state version 0, with its own
    Notional.
- **The feed's thread.**
  - On Server every batch is dispatched from a thread-pool thread, and Fluxor runs the reducers and
    raises `StateChanged` on it.
  - W1's `FluxorComponent` marshals with `InvokeAsync`.
  - W2's handler calls `ReplaceAll` there. The source, built on the renderer's context in
    `OnInitialized`, replaces its Window on that context (its threading note).
  - Checks 1–5 passed on Server with this traffic. The host log holds only `info` lines from
    routing, hosting and static assets (4,197 lines; no `warn`, `fail` or exception).
- **A race the reducer closes.**
  - On Server the store can be a version ahead of the grid: a batch reduced on the feed's thread
    lands between the grid's judgement and the edit's reducer. The reducers therefore check each
    write's `BasedOn` and refuse a cell whose painted text moved since.
  - In 80 commits over a price ticked every 5 ms (`raw/race-server.txt`), the grid refused all 80
    first (ADR-0142), and none reached the reducer.
  - The reducer's refusal was not seen, so this run does not show it working. It stays the backstop
    the grid cannot see (see the friction list).

## Friction a Consumer meets

**Pushing the Window from the store (W1)**: everything a bundled source does, the Consumer writes
again.

1. **The Row Sequence Version.** It needs the key sequence compared with the Window last handed
   over. The store cannot know that Window, so the comparison lives in the component, in render
   (`PushPage.razor`, `Project`). Put in a selector over consecutive states, it goes wrong when
   Fluxor delivers several states between two renders.
2. **The Change Highlight means comparing values.** The reducer compares the painted text of every
   column for each changed trade (`ColumnInfo.TextOf`), keeps a time per cell in the state, prunes
   it, and skips the user's own writes. The page then holds one `CellChangedAt` delegate for its
   life that reads the rendered state. That is about 50 lines of exactly what
   `GridSource.From`'s internal `CellChangeTimes` does. A delegate made per state, the obvious
   Fluxor selector, would re-render every painted row on every batch (ADR-0141's note on ExPivot's
   `CellChangedAt`).
3. **Sorting** is a full `GridQueryEngine.Apply` per state change, with no incremental requery.
   ADR-0141 measured 365 ms for 10⁶ rows.
4. **No gathering.** Every state change renders the changed rows: 1.0 renders per changing row per
   batch, against W2's 0.22. Fluxor's `MaximumStateChangedNotificationsPerSecond` throttles the
   component's render, which would gather, but not by ADR-0141's rule that the first change after a
   quiet interval shows at once.
5. **Intents resolve against the pushed Window**, never the store's newest state, and must check the
   intent's `RowSequenceVersion`. On Server the store can be a render ahead of the grid.

**Handing the list to `GridSource.From` (W2)**: little to write, about 15 lines: build the source on
the renderer's context, subscribe, `ReplaceAll`, catch.

6. **The user's own write cannot be told from an upstream change.** Through `ReplaceAll` it is
   gathered, up to 250 ms late, and marked as a change. It also makes the next commit on that cell
   at machine speed fail (check 2). The working pattern, W2src, writes the source first
   (`ReplaceRow`), then dispatches the same instance to the store.
   - This bends Fluxor's one-way flow: the edit is built in the component, and the reducer must
     accept an instance it did not make.
   - The order matters.
   - On Server a batch reduced in between makes `ReplaceRow` throw (`ArgumentException`, a stale
     version), and the write has to fall back to the reducer's judgement.
7. **`ReplaceAll` reads the whole list on every state change**, including one that did not touch
   the trades, such as a sort or a UI flag. Reading a list is a dictionary probe per row, and on an
   `ImmutableList` an O(log n) index per row. At 1,000 trades that is nothing. At 10⁶ it is a pass
   per dispatch. This was not measured here.
   - `Apply(GridChangeBatch)` avoids the pass, but needs the reducer's diff, which Fluxor does not
     expose. A Consumer would compute it in an effect or a middleware, beside the state rather than
     from it.
8. **`StateChanged` runs inside Fluxor's dispatch, on the dispatching thread.** An exception from
   `ReplaceAll` there would reach Fluxor's dispatch loop, so the spike catches it and reports it
   through `DispatchExceptionAsync`. The source must be built on the renderer's context, or it
   publishes on the feed's thread.

**Both wirings:**

9. **An Edit Intent cannot be refused afterwards.** `GridPasteIntent` and `GridFillIntent` have
   `Refuse()`; `GridEditIntent` does not (`GridEditIntent.cs:15`).
   - When the reducer refuses a cell because the store moved on, the grid has already closed its
     editor, and the typed value is gone. Only the Consumer's own status says so.
   - The way to keep the editor open is a column `validate` (an Edit Verdict, ADR-0034) that
     compares the store's newest version with the row. That is a value comparison in Consumer code
     again.
10. **Fluxor's dispatch on two threads.**
    - An action dispatched on the renderer while the feed's thread is dispatching is run by the
      feed's thread. So `Dispatch` returning does not mean the state holds the write.
    - Fluxor's queue also has a window, read in its `Store.cs`: an action enqueued just as the
      dispatching thread leaves its loop waits for the next dispatch. With the feed paused, that
      can be indefinitely.
    - Neither was observed here. Both are Server-only, and both are Fluxor's.
11. **`ImmutableArray<T>` as the Window**, the other immutable list a Fluxor user reaches for, is a
    struct. Handed to `Window` it is boxed afresh on each render, so the grid takes in a new Window
    and checks it whole every time. Read from the code (`ExGrid.RowKey.cs`, `TakeInWindow`), not
    measured. `ImmutableList<T>` works as it is.

Nothing pushed the spike towards mutating the store's objects. Records and `with` give exactly the
instance discipline ExGrid needs (ADR-0003), and `ImmutableList.SetItem` keeps every other instance,
so `ReplaceAll` pairs by reference with no value comparison.

## What an `ExGrid.Fluxor` package would need to provide

- **A bound source.** A component or helper takes an `IState<TState>`, a selector to the row list
  and a Row Key. It builds `GridSource.From` on the renderer's context, subscribes, calls
  `ReplaceAll`, keeps exceptions out of Fluxor's loop, and unsubscribes on dispose. That is W2's 15
  lines, done once.
- **Writes as actions.** Each grid write becomes one action carrying (key, column, text, the version
  judged against): position resolved against the source's Window, version checked, one action per
  gesture. With it, a reducer helper applies the write to an immutable list and re-checks each cell
  against its `BasedOn` by painted text.
- **The user's own write to the source first.** W2src's pattern: `ReplaceRow`, then dispatch the
  same instance, falling back to the reducer when the source refuses a stale version. It works today
  with no core change. Taking it out of the package needs proposal A below.
- **For push Consumers**, a public change-time tracker (today `CellChangeTimes` is internal) and a
  key-sequence helper for the Row Sequence Version. Or the package can recommend W2 and not support
  push at all: W2 costs a fraction of W1's code and renders a fifth as often.
- **An Edit Verdict helper** that rejects a commit when the store's newest version of the row
  paints the edited cell differently. It stays useful until proposal B.

## Proposals (each needs a decision)

- **A. Telling a keyed `GridSource.From` that a change is the user's own write.** Today that is
  possible only through `ReplaceRow`. Through `ReplaceAll` or `Apply`, the user's write is gathered,
  marked, and breaks D1 for the editor (check 2). Options:
  - **(a)** An overload that names the user's keys, such as `ReplaceAll(rows, ownWrites: keys)` or
    `Apply(batch, byUser: true)`. It would publish at once and mark nothing, as `ReplaceRow` does.
  - **(b)** The editor treats a cell the user's own write covers at the commit as it treats a
    paste's target: left out of the comparison (`ExGrid.SeenText.cs:268` against `:478`). The two
    gestures would then agree. But an upstream change in the same gathering as the user's write
    would be written over unseen, the case D1 already accepts for a paste.
  - **(c)** No core change. Document the W2src pattern, and leave it to the package.

  (a) or (c) keeps ADR-0142 as written. (b) changes D1's wording for the editor.
- **B. `GridEditIntent.Refuse()`**, as the paste and fill intents have. A Consumer whose store is
  the serialisation point (a reducer, a server) could then keep the editor open with the typing
  when the write is refused after the grid's judgement, rather than losing it silently. This
  touches ADR-0007 and ADR-0034 (the Edit Verdict is the judgement before the intent, and this
  would be one after).
- **C. Whether push Consumers get the bundled source's pieces** (the change-time tracker, the
  key-sequence comparison), or whether `GridSource.From` with a Row Key is declared the way a store
  feeds ExGrid. ADR-0001 point 4 says push is native because it "meets state-management libraries
  where they are". The spike found that a Flux Consumer is better served by the pull source over
  its list.

## Verdict

ExGrid works properly with a Fluxor store, in both wirings, on both hosts:

- Live batches reach the screen with only the changed rows rendering and only the changed cells
  marked.
- Edits, pastes, fills and clears go through actions and reducers and land.
- A commit over a cell that changed under the editor is refused, with the reason.
- An order-moving tick drops the Selection and a value-only tick keeps it.
- Dispatches from the feed's thread reach the grid safely.
- Memory is bounded, and no old state stays reachable.

The way to wire it is `GridSource.From` with a Row Key, handed the store's list through
`ReplaceAll` on every state change. Pushing the Window works too, but makes the Consumer rewrite
the bundled source's Row Sequence Version, Change Highlight and sorting, and it renders about five
times as often under a fast feed.

What is missing is a way for the source to know that a change coming back from the store is the
user's own write. Through `ReplaceAll` it is gathered and marked like an upstream change, and the
next commit on that cell at machine speed is refused with the user's own value given as the
upstream change. That happened in every run, on both hosts, with the feed paused or live. Writing
to the source first and then dispatching the same instance avoids it today, at the cost of bending
Fluxor's one-way flow. That pattern, a bound source and intent-to-action helpers are what an
`ExGrid.Fluxor` package would provide. Proposals A and B are the core decisions behind it.

## Raw

- `raw/wasm-checks-1-5.json`, `raw/server-checks-1-5-and-7.json`: the last full run per host, every
  variant (W1, W2, W2src, W2 with gather 0), every check's evidence.
- `raw/check-2-five-repeats-per-host.json`: check 2 five times per host and variant.
- `raw/wasm-check-6-six-rounds.json`, `raw/server-check-6.json`: the census readings.
- `raw/race-server.txt`: 80 commits over a cell ticked every 5 ms on Server.
- `raw/page-retention-wasm.txt`: which page stays reachable after it is left, with the `/blank`
  control.
