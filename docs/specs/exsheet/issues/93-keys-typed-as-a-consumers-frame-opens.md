# 93: Keys typed while a Consumer's own frame opens do not reach the grid

Status: ready-for-agent

**What to build:** the gap ADR-0039's note of 2026-10-01 leaves open, found by the fix for PR #42's CI.
- On the Server host, choosing the Context Menu's "Format Cells…" returns the keyboard to the grid's root after
  the command.
- `ExSheet.MudBlazor`'s dialog takes it a round trip later.
- A key typed in between reaches the grid. A digit would start an edit in the Focus cell behind the dialog. That
  is quietly wrong (principle 1).

**Blocked by:** None (can start immediately)

- [ ] **Show it first,** on the Server host with an injected round trip, as the CI fix did at 80 ms.
- [ ] **Keys typed in the gap reach the frame, in order, or are refused by name.** They are never the grid's.
      - For example, a command can tell the core that the Consumer is opening a frame of its own (ADR-0050
        item 16), so the core holds the keys for it instead of handing the keyboard to its root.
      - If that needs a new declaration, stop and report it as a proposal.
- [ ] **The built-in Chrome's popover** gets the same guarantee, if it has the same gap.
- [ ] **Layer 2** stages the circuit's order. **Layer 3** on the Server host. CI runs it.

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
