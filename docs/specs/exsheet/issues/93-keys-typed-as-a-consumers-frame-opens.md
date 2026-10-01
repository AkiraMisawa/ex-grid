# 93: Keys typed while a Consumer's own frame opens do not reach the grid

Status: done

**What to build:** the gap ADR-0039's note of 2026-10-01 leaves open, found by the fix for PR #42's CI.
- On the Server host, choosing the Context Menu's "Format Cells…" returns the keyboard to the grid's root after
  the command.
- `ExSheet.MudBlazor`'s dialog takes it a round trip later.
- A key typed in between reaches the grid. A digit would start an edit in the Focus cell behind the dialog. That
  is quietly wrong (principle 1).

**Blocked by:** None (can start immediately)

- [x] **Show it first,** on the Server host with an injected round trip, as the CI fix did at 80 ms.
- [x] **Keys typed in the gap reach the frame, in order, or are refused by name.** They are never the grid's.
      - For example, a command can tell the core that the Consumer is opening a frame of its own (ADR-0050
        item 16), so the core holds the keys for it instead of handing the keyboard to its root.
      - If that needs a new declaration, stop and report it as a proposal.
- [x] **The built-in Chrome's popover** gets the same guarantee, if it has the same gap.
- [x] **Layer 2** stages the circuit's order. **Layer 3** on the Server host. CI runs it.

## Comments

*(2026-10-01, agent cf-53: the gap shown; stopped for a decision.)*

- **Shown, on the Server host at an 80 ms round trip.** A `5` was typed straight after each way of
  opening Format Cells on C2, with a throwaway spec:

  | Opened by | `ExSheet.MudBlazor`'s Chrome | The built-in Chrome |
  |---|---|---|
  | A press on "Format Cells…", the digit at once | lost: it lands on the closing menu | lost |
  | The same, the digit 100 ms later | **an edit holding `5` behind the dialog** | lost: it lands on nothing |
  | Enter on "Format Cells…" | **an edit holding `5` behind the dialog** | **an edit holding `5`, and the Cell Editor took the keyboard from the popover** |
  | Ctrl+1 | **an edit holding `5` behind the dialog** | **an edit holding `5` behind the popover** |

  - The built-in Chrome has the gap too, so the ticket's third box applies.
  - Where a key is lost, it is not the grid's, but it does not reach the frame either.
- **Why.**
  - The core holds the keys typed behind Enter on a menu item until the menu is gone, and behind a
    declared key such as Ctrl+1 until the core answers. Then it gates them against the root. Nothing
    tells the hold that the keyboard is on its way to a popover the command or the key opened, or to
    a frame outside the grid. A digit gated against the root opens Overwrite.
  - After a press on a menu item nothing is held at all: the key lands on the closing menu or on
    nothing.
  - Under the MudBlazor Chrome, the core also hands the keyboard back to its root after the command,
    because it opened no popover of its own (`RunCommandAsync`).
- **What the cure needs.**
  - For the core's own popover (the built-in Chrome), the hold can wait until the popover holds the
    keyboard, and hand it the keys, within the existing listener. This covers the menu's Enter, a
    press on an item, and a declared key whose answer opened one.
  - For a frame of the Consumer's own, the core cannot know the keyboard is leaving unless it is told.
    That is a new declaration, and is reported as a proposal before anything is built.
- **A finding, from PR #42's fix: the core's hold drops a held Tab** (and Shift+Tab), with every key
  held behind it (ADR-0010). A Tab moves DOM focus as the browser's own default, and script can
  neither reproduce it nor move focus in its place (ADR-0021).
  - **Not a defect, but a limit chosen on purpose.** The typing stops short where it can be seen,
    rather than going on in a field it was not meant for. `Alpha`, Tab, Space, Enter would otherwise
    search for "Alpha " and apply it.
  - **What the user meets:** a Tab typed within a round trip of a key that opens something is lost,
    with what follows it.
  - **A narrower cure stays possible:** a popover whose sentinels are the core's could take a held
    Tab or Shift+Tab as its own wrap request.

*(2026-10-01, orchestrator.)* **Decided: P1, built together with A.** ADR-0050 item 16's note and ADR-0039's
note of this date record it.
- The core gains `HandKeyboardToFrameAsync()`. ExSheet calls it whenever it opens Format Cells in a
  Chrome's own frame.
- The built-in popover's hold waits for the popover and replays the keys to it. Ctrl+1 is held as
  Alt+Down is, and a click on a menu item starts the hold.
- Ship the click hold only together with P1.

*(2026-10-01, agent cf-53, built.)* The decision built: P1 together with A.
- **The key gate is told where the keyboard is going** (`handOff` on the module's handle, C# to
  the gate, ahead of the key's answer and of the render that closes the menu). It is told
  `popover` when the core opens a Consumer's popover, and `frame` by the new
  `ExGrid.HandKeyboardToFrameAsync()`.
  - While a hand-off stands, the keys typed on the root or the menu are held until the keyboard
    has arrived: a control of the popover, or an element outside the root that is not `body` and
    not another grid.
  - They are then replayed to it, in order, through the existing replay.
  - A frame's element takes every key but Tab. A tab of ARIA's tabs pattern takes its arrows,
    Home and End.
  - If the keyboard never arrives within the fallback, or goes to another grid, the held keys
    are dropped and never gated against the grid.
- **Ctrl+1 is held as Alt+Down is.** With no edit open, a declared key is gated `mode`, so the
  keys after it wait for its answer, then for the hand-off it announced. A press on an enabled
  menu item starts the hold, as Enter on it does.
- **ExSheet calls `HandKeyboardToFrameAsync()`** whenever Format Cells opens in a frame of the
  Chrome's own: from the menu command, Ctrl+1 and `OpenFormatCellsAsync`. ExSheet.MudBlazor is
  unchanged: its frame's element already holds keys for its tabs (`KeysOnTheirWay`).
- **One departure from the note, reported to the orchestrator.** The note says a command that
  hands the keyboard to a frame "does not return the keyboard to its root". It still does.
  - Without the hand-back, the keys typed after the menu closes land on `body`. Only a `document`
    key listener hears them there, and that would be a new entry on ADR-0021's allowlist: the
    key listener is the root's, never the document's, and a structural test holds the module to
    that.
  - With the hand-back, those keys land on the root, where the hand-off's hold takes them for the
    frame. They still never reach the grid.
  - The built-in popover's keys typed on `body` (a press, then a round trip) are still lost. That
    is "dropped", and not the grid's.
- **Layer 2.**
  - `ConsumerPopoverTests` (3 new): the popover tells the gate; a command that hands the
    keyboard to a frame tells it while its menu still stands; a command that opens nothing still
    hands the keyboard back.
  - `FormatCellsTests` (2 new): every way of opening tells the gate `frame` under a Chrome's own
    frame, and `popover` under the built-in Chrome.
  - `MudFormatCellsTests` (1 new).
  - The gate's structural test now reads a declared key with no edit open as held until
    answered, as the note decides.
- **Layer 3: `format-cells-keys.spec.mjs`, 14 tests** — the table's four rows, End typed straight
  after, and two Sheets, under both Chromes.
  - At 80 ms on the Server host all 14 pass. Against the code before this change, 11 of them
    fail: every row the table showed the gap in.
  - With `format-cells.spec.mjs`, `format-cells-mud.spec.mjs`, `format-keys.spec.mjs` and
    `popovers.spec.mjs`: 99 of 99 on Server and 99 of 99 on WebAssembly, in Chrome. The Sheet's
    menu commands on Server: 5 of 5.

*(2026-10-01, agent cf-53: CI run 36926565340.)* `format-cells.spec.mjs:86` (Ctrl+1, then Escape;
the grid is not focused afterwards) failed on the Server host in Edge, twice. It is this ticket's
gap, and it is not Edge's.
- **Reproduced on Chrome** on the Server host before this ticket's change. After Escape the
  keyboard ended on `body`, and was still there 600 ms later:

  | Round trip | Runs on `body` |
  |---|---|
  | 0 ms | 0 of 3 |
  | 40 ms | 0 of 3 |
  | 80 ms | 2 of 3 |
  | 150 ms | 3 of 3 |

- **Traced at 150 ms:**
  1. Ctrl+1 opens the popover.
  2. Escape is typed on the root while the popover is drawn but before its tab holds the keyboard.
  3. The core closes the popover and hands the keyboard back to the root.
  4. The tab's opening focus lands only after that (about a round trip after the popover was
     drawn), so the tab takes the keyboard.
  5. The render that removes the popover leaves it on `body`, where no hand-back takes it again.
- **With this ticket's change** the Escape is held behind Ctrl+1 until the tab holds the keyboard,
  and is then handed to it. The popover closes from inside, after its own focus, and the hand-back
  comes last. The same runs: 12 of 12 end on the grid's root, at every round trip up to 150 ms.
  Edge is CI's.

