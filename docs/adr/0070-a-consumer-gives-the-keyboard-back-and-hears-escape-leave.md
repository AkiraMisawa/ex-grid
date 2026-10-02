# A Consumer gives a grid the keyboard back, and hears an Escape that leaves it

*(Numbered ADR-0069 until 2026-10-01. ExSheet's Pointing Scope took ADR-0058 first, and ExPivot's
ADRs moved up by one into the block [`docs/agents/numbering.md`](../agents/numbering.md) reserves
for them. Commit messages before then use the old numbers.)*

*(Decided with the user, 2026-10-01 — Q64 and Q65, raised by building ExPivot's Show Details
dialog. Both are opt-in Consumer capabilities, shaped like the ones
[ADR-0050](./0050-what-exsheet-asks-of-exgrids-core.md) and
[ADR-0063](./0063-what-expivot-asks-of-exgrids-core.md) gave ExSheet and ExPivot: a Consumer that
uses neither sees nothing change.)*

ExPivot's Show Details can open a dialog, with an ExGrid of the records in it, over the report — also
an ExGrid
([ADR-0059](./0059-expivot-is-a-pivot-table-drawn-by-exgrid-as-its-consumer.md)). Building it found
two gaps in what a Consumer can do with a grid's keyboard.

- **Closing the dialog leaves the keyboard nowhere.** The control that had DOM focus goes with the
  dialog, so focus falls to the page's `body`. The report grid would take it back, as it does when
  one of its own popovers closes
  ([ADR-0039](./0039-a-popover-takes-the-keyboard-and-may-hold-popups-of-its-own.md)). But nothing
  lets a Consumer ask it to. The user's next arrow key scrolls the page, and the report does not
  move. The same happens when the last details tab closes.
- **Escape inside the dialog's grid never reaches the dialog.** Escape with nothing left to dismiss
  is the grid's way out: it releases the grid's DOM focus
  ([ADR-0012](./0012-anchor-focus-and-keyboard-navigation.md)). The capture-phase listener takes the
  key first and stops it
  ([ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md),
  [ADR-0018](./0018-multiple-instances-must-be-independent.md)), so a dialog listening for
  Escape never hears it. The user presses Escape, the grid lets go of the keyboard, and the dialog
  stays open.

## The decision

**1. `ReturnKeyboardAsync()` gives the grid's root the keyboard back (Q64).**

- It is a public method on the grid. A Consumer calls it when something of its own that held the
  keyboard over the grid goes away: a dialog, a panel, a tab.
- It does what the grid does when one of its own popovers closes. It gives the root DOM focus when
  DOM focus is on nothing (the page's `body`) or already inside this grid. A field of the grid's
  own beside the rows — the Formula Bar or the Name Box — keeps the keyboard, as it does from the
  grid's own hand-back ([ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md)'s note of
  2026-09-28): the user is typing there.
- It never takes the keyboard from anywhere else: not from another control the user chose
  meanwhile, and not from another grid
  ([ADR-0018](./0018-multiple-instances-must-be-independent.md)). On Blazor Server the call
  lands a round trip later, and a click made in that time wins.
- It moves neither the Focus nor the Selection, and it scrolls nothing. The keyboard comes back to
  the cell it left.
- Before the grid is attached to the page, it does nothing.
- It adds no JavaScript. It is the hand-back the grid already makes for its own popovers, which
  [ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md) records among the decisions about
  focus made in the browser.

**2. `OnLeave` is raised by an Escape that has nothing left to dismiss (Q65).**

- It is opt-in. Without it, nothing changes: Escape with nothing left to dismiss releases the
  grid's DOM focus, as ADR-0012 says.
- With it, the grid raises `OnLeave` for that Escape instead, and keeps DOM focus. The Consumer
  decides what leaving means — closing its dialog, for example — and where the keyboard goes next.
  Releasing focus first would drop it on the page's `body` before the Consumer could put it
  anywhere.
- **Only the outermost Escape raises it.** ADR-0012's layering is unchanged, and each of these
  Escapes still peels only its own layer:
  - one that closes an Inner Popup, then the popover that holds it
    ([ADR-0039](./0039-a-popover-takes-the-keyboard-and-may-hold-popups-of-its-own.md));
  - one that cancels an edit ([ADR-0007](./0007-edits-are-an-overlay-owned-by-the-consumer.md));
  - one that closes a Formula Entry's list
    ([ADR-0051](./0051-formula-entry-completion-point-mode-and-the-formula-bar.md));
  - one that leaves an Interactive cell
    ([ADR-0037](./0037-entering-a-cell-never-reaches-into-content-the-core-did-not-render.md));
  - one that returns from a control inside a cell, which takes the keyboard back to the root
    ([ADR-0020](./0020-action-and-template-columns.md)).

  Each press is one dismissal, and `OnLeave` is the last.
- It is raised once per press. A held key's repeats do not raise it again until the key is
  released.

## ExPivot's use of them

- **Show Details' dialog** sets `OnLeave` on its grid, and closes when it is raised. However the
  dialog closes — Escape, the Close button, the backdrop — ExPivot then calls the report grid's
  `ReturnKeyboardAsync()`.
- **A details tab** that closes while it was selected gives the keyboard to the tab ExPivot selects
  next. When that is the report's tab, ExPivot calls `ReturnKeyboardAsync()` on the report grid.
- **Escape inside a details tab's grid is unchanged.** A tab is a sheet of its own, as Excel's are,
  and Escape does not close a sheet.

## Refined while building it

*(2026-10-01, when ticket 21 built it.)*

- **A details tab selected next takes the keyboard on its tab**, as a newly opened tab does, not in
  its records. Its records grid is mounted by the same render that selects the tab, so it is not yet
  attached when the keyboard has to go somewhere, and `ReturnKeyboardAsync()` does nothing before
  attach.
- **When a tab that was not selected closes, the keyboard goes to the sheet that is**: the report's
  grid when the report's tab is selected. The control that held the keyboard, the closed tab's
  button, went with it.
- **A held Escape is one press in every grid.** The first build raised `OnLeave` once per press, as
  decided, but a grid without `OnLeave` still answered each repeat. Holding Escape to close the
  dialog handed the report the keyboard, and the next repeat released it. Every Escape layer now
  answers the press and not its repeats
  ([ADR-0012](./0012-anchor-focus-and-keyboard-navigation.md), refined the same day). This is the
  one change here to a grid that declares nothing, and it is a fix: a repeat also cancelled a
  half-typed Formula under its closing list.
- **The capture-phase listener passes on whether a key is a repeat** (ADR-0021, note of the same
  day). It adds no listener, and only the browser knows a repeat from a press.

## Beside ADR-0012's rewrite

*(2026-10-02, when this branch met the base's rewrite of ADR-0012, decided with the user on
2026-10-01 in the fifteenth Windows run.)* That rewrite has the Escape with nothing left to dismiss
release Tab, the root keeping DOM focus, where it used to release the focus (KB-8). The two
decisions are read together as they are written:

- **Without `OnLeave`, the grid does what ADR-0012 says**, and that is now to release Tab. "Releases
  the grid's DOM focus", above, is what ADR-0012 said when this was decided.
- **With `OnLeave`, the grid raises it in place of the release**, as it was raised in place of the
  blur, and its Tab stays in the cycle. The Consumer decides where the keyboard goes next. The
  reason given above for raising it first — a released keyboard lands on `body` — does not hold
  for a release that keeps DOM focus. "Instead" stands because the decision says it, and ExPivot's
  dialog closes on `OnLeave` either way.
- **A held Escape's repeats leave a release standing.** The rewrite ends a release at any other
  key, and a repeat is the same press (KB-44), so the gate does not count it; the core answers it
  with nothing. Merged as they were, the press released Tab and its first repeat ended the release.
  KB-44 now says so, and layer 3 holds Escape on `/features` and then presses Tab.

*(2026-10-02, when this branch met ADR-0080 the same day.)* A grid with an editable column now holds
the keyboard on its Keyboard Field rather than its root, and the grid's hand-back puts it there
(ADR-0080). `ReturnKeyboardAsync()` is that hand-back, so on such a grid "the root", above, reads as
the Keyboard Field; a display-only grid, as ExPivot's report and details grids are, keeps it on its
root. DC-61 says so. Nothing else here changes: the field counts as the root for the keys (ADR-0080),
so the Escape that raises `OnLeave` is the same press.

*(2026-10-02, when this branch met ExSheet's Cell Format, #42.)* ExSheet asked the core for the
same hand-back for a Chrome whose frame lies outside the grid: Format Cells as a MudDialog
([ADR-0071](./0071-a-sheets-cell-format-is-document-data-painted-on-white-paper.md); ADR-0010's note
of 2026-09-30). It added `ReturnKeyboardAsync()` under the same name, with the same condition: DOM
focus on nothing or inside this grid, and not on the Formula Bar or the Name Box. The two were the
same call, so the merge keeps one method. A Chrome calls it once its frame has closed, and a
Consumer once something of its own goes away. DC-61 states it for both, and its layer 2 and layer 3
tests run against that one method.

## Considered options

- **The Consumer focuses the grid through JavaScript of its own** — rejected. It would need the
  grid's internal elements, which a Wrapper or Consumer must not reach for
  ([ADR-0029](./0029-the-presentation-surface-is-a-short-list-of-classes-and-tokens.md),
  [ADR-0030](./0030-what-a-design-system-wrapper-owns-and-what-it-may-not-touch.md)). It would also not know the rule
  above: never take the keyboard from another control.
- **The grid lets Escape bubble when it has nothing left to dismiss** — rejected. Every Escape
  handler on the page would then hear an Escape that also left the grid, including one belonging
  to another grid. That is the problem ADR-0018 moved the listener to the root to avoid. A
  declaration says which Consumer listens.
- **`OnLeave` raised after releasing focus** — rejected. Focus would land on `body`, and the
  Consumer's dialog would close over a page with no keyboard anywhere.

## Consequences

- **§26 gains DC-61 and DC-62**, which gate the release as the rest of §26 does. **§29 gains PV-39**
  for ExPivot's use of them.
- **Layer 2 holds both declarations to their rules**: the hand-back's conditions, and the Escapes
  that do and do not raise `OnLeave`. **Layer 3 runs them on `/pivot?details=dialog`**: Escape
  closes the dialog, and the arrow keys then move the report's Focus.
