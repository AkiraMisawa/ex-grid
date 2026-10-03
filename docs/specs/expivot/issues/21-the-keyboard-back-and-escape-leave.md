# 21: The keyboard given back, and the Escape that leaves

Status: done

**What to build:** [ADR-0070](../../../adr/0070-a-consumer-gives-the-keyboard-back-and-hears-escape-leave.md),
in ExGrid's core and in ExPivot's use of it.

- **ExGrid:** `ReturnKeyboardAsync()`, the hand-back the grid already makes for its own popovers,
  offered to the Consumer. And `OnLeave`, raised by an Escape on the root with nothing left to
  dismiss, in place of releasing the DOM focus. No new JavaScript.
- **ExPivot:** Show Details' dialog closes on its grid's `OnLeave`. However it closes, the report's
  grid takes the keyboard back. A details tab that closes while selected hands the keyboard to the
  tab selected next, which is the report's grid when that is the report's tab. Under both Chromes.

**Blocked by:** None

- [x] DC-61, DC-62 (layer 2; layer 3 for the conditions only a browser can show)
- [x] PV-39 (layer 2; layer 3 on `/pivot?details=dialog` and `/pivot`, under both Chromes)

## Comments

2026-10-01: Built.

- **ExGrid** (`ExGrid.Leave.cs`). `ReturnKeyboardAsync()` is the grid's own hand-back
  (`reclaimFocus`, not from a field), asked on the renderer's context. The root takes DOM focus
  only from nothing or from inside the grid, never from another control, another grid, the
  Formula Bar or the Name Box. It moves no Focus and no Selection, scrolls nothing, renders
  nothing, and does nothing before the grid is attached or after it is gone. `OnLeave`, declared,
  is raised by an Escape on the root with nothing left to dismiss, in place of releasing the
  focus, and every inner layer's Escape still peels only its own. A held Escape raises it once:
  the key message now says whether a key is a repeat. That is the browser's `event.repeat`, which
  the capture-phase listener already reads for the held Space (ADR-0037): one more argument of the
  one message to `OnKeyAsync`, with no listener added and no layout read. The script-shape tests
  inspect it. Without the declaration nothing changes, a held Escape's repeats included.
- **ExPivot**. The dialog's records grid declares `OnLeave`, and the dialog closes on it. However
  the dialog closes — that Escape, an Escape on its frame, Close, the backdrop — the report's grid
  is asked for the keyboard back after the render that removed the dialog and the report's
  `inert`. `PivotKeyboardReturn` asks it: a component that renders nothing and acts once per
  request, as `PivotFocusButton` does. A details tab that closes hands the keyboard to the tab
  selected then: a details tab's button, as before, or the report's grid when the report's tab is
  the one. That covers every close of the last tab, selected or not, when no tab is left to hold
  the keyboard. Escape in a tab's grid closes nothing. Nothing in `ExPivot.MudBlazor` needed
  changing: the frame, the records and the selection are ExPivot's under both Chromes.

Tests: `LeaveAndReturnKeyboardTests` (13, DC-61 and DC-62: the request and its condition, nothing
moved, nothing before attach or after disposal; `OnLeave` once per press, and not for the
popover, the Inner Popup, the edit, the Formula Entry list, the Interactive cell or the control in
a cell), two script-shape tests in `ShippedStylesheetTests`, `KeyboardBackTests` (11 cases, PV-39
under ExPivot's markup) and `MudPivotKeyboardBackTests` (8 cases, PV-39 under `MudPivotChrome`).
Each test of new behaviour was seen failing first, the Mud ones against ExPivot as it was. The
guards of what must not change pass on either side by design: nothing moved, nothing without the
declaration, nothing after disposal or before attach, Escape in a tab's grid, a details tab's
button. Layer 3, in `pivot.spec.mjs`, under both Chromes: Escape in the dialog's
grid closing its Context Menu first and then the dialog; however the dialog closes, the report's
arrows moving its Focus; Escape in a tab's grid closing nothing; the selected tab closed handing
the keyboard on, and the last one back to the report. Under ExPivot's markup, DC-61's two browser
conditions with 150 ms injected on Server: a page control focused meanwhile keeps the keyboard, and
so does the other pivot's report on `/pivot-db`. Run on Linux under xvfb with the container's
Chromium (no Chrome or Edge installed; Edge is CI's): `--grep ADR-0070` 8 of 8 on both hosts, and
the whole of `pivot.spec.mjs` 46 of 46 on both hosts, the console clean.

Two things were found that need a decision, and are not built:

- **"The tab selected next" for a details tab is its button, not its records grid.** The records
  grid of the tab selected next is mounted by the render that selects it, so it is not attached
  when the closing render lands, and `ReturnKeyboardAsync` does nothing before attach (ADR-0070).
  Giving that grid the keyboard needs either the grid to take a request made before attach, or the
  details grids to stay mounted while their tabs are not selected.
- **A held Escape that closes the dialog leaves the keyboard nowhere.** Its repeats reach the
  report's grid once it has the keyboard back. The report declares no `OnLeave`, so ADR-0012's
  Escape releases its focus. Seen on `/pivot?details=dialog`: the report had the keyboard after the
  press, `body` after the first repeat, and a later ↓ moved nothing. One press, one dismissal for
  every grid — a repeat dismisses nothing more — would close it, but it changes what Escape does
  without a declaration (DC-1).

2026-10-01, at the merge: the held-Escape case the build found was decided with ADR-0012's
refinement — a held Escape is one press in every grid (KB-44) — and `pivot.spec.mjs` holds Escape
in the dialog's grid to show the report keeps the keyboard. The ADR-0070 tests pass on both hosts
(10 each), with a clean console.

2026-10-02, decided with the user: `OnLeave` is raised beside the grid's release of Tab, not in
place of it (ADR-0070, "Raised beside the release"; DC-62). The dialog's grid releases Tab and
raises `OnLeave`, and the dialog closes on it as before.

2026-10-03, decided with the user on 2026-10-02: a press into a details view's records keeps the
keyboard there (PV-41; DC-61 and A11Y-20 extended; ADR-0070, "Handed on, not taken"; ADR-0021's
note of 2026-10-02; ADR-0033's note of 2026-10-03).

- **What was found.** A test of the dialog that clicked into its records once they were painted
  failed in 1 run of 3 on the Server host. It was first read as Close's late focus taking the
  keyboard, and the test was made to wait for Close (752a28e). A probe of the order of events, with
  no latency injected, then found two races. Pressed as soon as the dialog showed, the records lost
  the keyboard to Close in 2 runs of 6. Pressed once the rows were painted, the press could land
  before the records' root was a tab stop, once in 8, 14 ms before it. The keyboard then stayed on
  the scroller: that was the failure seen.
- **ExGrid.** `HandKeyboardToAsync(control)` asks the handle's `handKeyboardTo`, the sixth decision
  about focus made in script. The control takes the keyboard only while DOM focus is inside the grid
  or on nothing. Before the attach, and once the grid is gone, the control takes it by its own
  focus. The scroller's hand-off to the root is owed until a render has made the root a tab stop.
- **ExPivot.** The dialog's and the tabs' contexts carry `TakeKeyboard`. ExPivot's own views and the
  MudBlazor Wrapper call it in place of the element's `FocusAsync`, and ExPivot hands it the report
  grid's `HandKeyboardToAsync`.

Tests, layer 2:

- `LeaveAndReturnKeyboardTests`: 4 more, for DC-61.
- `AccessibilityTests`: 1, for A11Y-20.
- `ShippedStylesheetTests`: the hand-on's shape, and six focus decisions in script.
- `DetailsAndVersionTests`, `KeyboardBackTests`, `MudPivotDetailsTests` and
  `MudPivotKeyboardBackTests` (2 more): each request read off the report grid's handle.

The Mud test context said the grid's module import answers null in loose mode. It does not: every
grid attaches there, to a handle that answers nothing. The comment now says so.

Layer 3, in `pivot.spec.mjs`, under both Chromes, two PV-41 tests put DOM focus on the records'
scroller in the task that draws them, where a press there puts it:

- with the hand-on taken out, 8 of 8 failed on the Server host;
- with the scroller's owed hand-off taken out, 8 of 8 failed too;
- with both, 20 of 20 passed on each host.

The whole of `pivot.spec.mjs` three times over passed 156 of 156 on each host. `features`,
`key-field` and `circuit` passed 118 of 118 on the Server host.
