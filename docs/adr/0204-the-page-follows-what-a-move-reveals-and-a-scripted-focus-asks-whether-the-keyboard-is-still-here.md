# The page follows what a move reveals, and a scripted focus asks whether the keyboard is still here

*(Decided with the user on 2026-10-11, while closing the gaps the Definition of Done's Step 7 would
find. It amends [ADR-0012](./0012-anchor-focus-and-keyboard-navigation.md)'s "The Focus must always be
visible" and settles [ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md)'s "Open, not decided
here".)*

Two things a keyboard user meets were recorded as undecided.

- **The page does not follow the Focus.** A grid taller than the window, its lower part below the
  fold: the arrow keys move the Focus out of the window, and nothing brings it back. The grid reveals
  the Focus inside its own Viewport (ADR-0012), which is all "always visible" was ever scoped to, and
  the page stays where it is. Opening the editor does scroll the page — 442px on `/exsheet` — because
  `focusEditor` focuses the field without `preventScroll`, deliberately. So the same cell is followed
  when it is edited and not when it is reached (`verification/2026-10-09-windows-remote-isolation`).
- **A popover's opening focus and a Template cell's own focus are taken a round trip after the
  gesture**, unconditionally. The editor had the same race, and CI saw it lose the keyboard on Edge
  under Blazor Server with 40 ms injected, 9 times in 40 (ED-26). The editor was fixed: it focuses only
  while DOM focus is inside this root or nowhere. Popovers and Templates were left "open, not decided
  here" (ADR-0021), and nothing has been seen to fail. `0.1.0` claims Blazor Server
  ([ADR-0200](./0200-the-first-stable-version-is-0-1-0-and-carries-the-whole-family.md)), where that
  round trip is real, and principle 6 says an outcome never depends on timing.

## The decision

### The page follows

- **After a move the grid makes, the page follows what the grid revealed.** Arrow keys, Ctrl+arrows,
  Home and End, PageUp and PageDown, Enter and Tab cycling, Ctrl+Backspace: once the grid has revealed
  the Focus in its Viewport (or the Extent, while extending — ADR-0052), the browser is asked to bring
  the Keyboard Field, which stands over that cell
  ([ADR-0080](./0080-a-keyboard-field-holds-the-keyboard-so-an-ime-can-start-on-a-selected-cell.md)),
  into the window at the **nearest** edge. A cell already in the window moves nothing.
- **Opening the editor follows the same rule**, so a cell is followed the same way whether it is reached
  or edited.
- **Only while the keyboard is still this grid's**, the editor's condition: DOM focus inside this root
  or nowhere. A request carries the Focus it was taken for and is dropped if the Focus has changed or
  the keyboard has left before it runs. A page the user scrolled meanwhile so that the cell shows moves
  no further.
- **Not when the box changes.** A resize or a Drawer that moves the Focus off screen is not followed,
  as ADR-0012 already scopes the grid's own reveal (2026-09-25).
- **The browser places, the grid does not measure.** ADR-0021 refused `scrollIntoView` for the grid's
  *own* reveal because it knows nothing of a sticky header or a pinned block. Here the grid has already
  revealed the cell inside its Viewport by its own arithmetic, and asks the browser only to move the
  *page* so that the field over that cell shows. That is the scroll entry of the allowlist, applied to
  the page; no new JavaScript use, and nothing read on the path to a paint.
- **A page with a fixed bar sets `scroll-padding`** on its scrolling element, so that the cell is not
  brought under the bar. That is the page's to know; the Docs Site sets it for its app bar, and the
  Docs say so.

### A scripted focus asks first

- **Every focus the grid takes a round trip after a gesture asks the editor's question first**: is DOM
  focus inside this root, or nowhere? Otherwise it is not taken.
  - The built-in popovers' opening focus (the column menu, the filter panel, Find).
  - **A Wrapper's popover is handed the core's focus function**, as a Chrome's editor already is
    (ADR-0010), instead of calling `FocusAsync` itself.
  - **A Template cell's control takes the keyboard through `HandKeyboardToAsync`**, which already asks
    that question (ADR-0021, 2026-10-02), instead of its own `FocusAsync`. No new public member.
- **Tested where the race is real**: layer 3 on the Server host with latency injected, the keyboard
  moved to another grid during the round trip, and the second grid keeps it (SRV-5).

### A press inside the popover keeps the keyboard there *(decided with the user on 2026-10-17, ticket 240)*

The question above asked only whether the keyboard had left the grid. A press inside the popover
itself, made before its opening focus lands, is inside the root, so the opening focus went through and
took the keyboard back from where the user had pressed. CI saw it on the Server host with no latency
injected (#140, run 39031949301): Format Cells drawn, Percentage pressed 33 ms later, OK focused and
Enter typed, and the opening focus, landing after them, put the keyboard on the first tab, where the
Enter did nothing.

- **A popover's first scripted focus is refused when a press inside that popover has already put DOM
  focus there.** It was taken against a popover nobody had touched, which no longer holds. Later
  requests — a key's move in a menu, Tab's wrap — answer a key typed in the popover, and are asked as
  before. A press on the popover's own ground, which takes no focus, leaves the opening focus to place
  the keyboard.
- **A Consumer's popover (ADR-0050 item 16) is handed the core's focus function too**, as
  `GridPopoverContext.TakeFocus`, and answers `FocusRequest` and `FocusLastRequest` through it instead
  of its own `FocusAsync`; ExSheet's Format Cells does. Only the press question is asked of it, not the
  editor's: a Consumer's popover may open from a control of the page's, outside the grid.
- **Tested where the race is real**: layer 3 on the Server host with a 300 ms round trip, the choice
  pressed in the frame it is painted, and it keeps the keyboard (`format-cells.spec.mjs`, ticket 240).

## Considered options

- **The page never follows**, the editor included (`preventScroll` on its focus). Consistent, but the
  editor would open out of sight, which the 2026-10-09 record itself calls worse.
- **Accept what is there in writing for `0.1.0`.** A keyboard user losing the cell they are typing into
  is a visible defect in a component named for Excel's keyboard.
- **Fix the popovers and leave Templates**, documenting `HandKeyboardToAsync`. The race would stay in
  the one place a Consumer's code decides, and the fix is already a public member.
- **Record the race as reserved until a failure is seen.** Principle 6 asks for the deterministic fix
  when a race is found, not when it is caught failing.

## Consequences

- ADR-0012's "always visible" bullet and ADR-0021's "Open, not decided here" carry dated notes pointing
  here.
- The Definition of Done gains the page-follow rule beside the reveal criteria, layer 3 on both hosts,
  and SRV-5 the popover and Template cases.
- The Docs Site's keyboard pages say the page follows, and what a page with a fixed bar sets. The
  Template Example and the DemoHost's `NoteInput` move to `HandKeyboardToAsync`.
