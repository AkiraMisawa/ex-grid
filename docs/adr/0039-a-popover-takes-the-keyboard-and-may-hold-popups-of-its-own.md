# A popover takes the keyboard when it opens, gives it back when it closes, and may hold popups of its own

The grid's popovers — the column menu, the filter panel
([ADR-0009](./0009-filter-panel-contract.md)/[0010](./0010-chrome-seams-column-menu-editor-loading.md))
and the Context Menu ([ADR-0036](./0036-the-context-menu-is-the-column-menu-shape-over-a-selection.md))
— were built for the pointer. Checked while `ExGrid.MudBlazor`'s remaining seams were being
decided (2026-09-24):

- **Nothing opens the column menu or the filter panel from the keyboard.** Only a click on ▾ does.
- **Opening any popover leaves DOM focus on the root.** The arrows and Tab then belong to the grid
  and move the Focus; the popover's items and controls cannot be reached without a pointer. The
  Context Menu opens from `ContextMenu` / `Shift+F10` (CTX-4) and then cannot be used.

A keyboard-only user could not filter, and could not copy from the menu they had just opened.
For a component whose claim is Excel-like operability that is a hole, not a polish item.

The same session reopened a line [ADR-0030](./0030-what-a-design-system-wrapper-owns-and-what-it-may-not-touch.md)
had drawn: a seam's contents "never a `MudPopover`, whose provider renders outside the instance
root and would break the three dismissals of ADR-0010 and the independence of ADR-0018". That was
a **prediction**, made before any seam held one. Reading MudBlazor 9.9's source showed most of it
does not happen (below), and a seam that a design system's ordinary select cannot live in is not
much of a seam. So this ADR settles both together: they are one question — who holds the keyboard
while a popover stands, and what happens when its contents open something of their own.

## Opening, and taking the keyboard

- **`Alt+↓` opens the column menu of the Focus's column** — the key Excel uses to open a header's
  filter drop-down. It joins the key table, so the capture-phase gate takes it
  ([ADR-0012](./0012-anchor-focus-and-keyboard-navigation.md)); on a column with no menu it does
  nothing. The filter panel is reached from the menu's Filter command, as it is by pointer. The
  Context Menu keeps `ContextMenu` and `Shift+F10`.
- **Every popover asks its contents to take DOM focus when it opens**, by key or by pointer.
  `ColumnMenuContext`, `ContextMenuContext` and `FilterPanelContext` gain a **`FocusRequest`**,
  counted up once per opening. The contents — the built-in Chrome's and any substitute's — focus
  their first enabled item or control with Blazor's own `FocusAsync`. The core holds no reference
  to an element it did not render and does not try: this is `CellEditorContext.FocusRequest`
  ([ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md)/[0030](./0030-what-a-design-system-wrapper-owns-and-what-it-may-not-touch.md))
  and `TemplateCellContext.FocusRequest`
  ([ADR-0037](./0037-entering-a-cell-never-reaches-into-content-the-core-did-not-render.md)),
  applied to the popovers.

## Inside a popover

DOM focus on a descendant already means the capture-phase gate takes **Escape alone** and leaves
every other key to the control ([ADR-0012](./0012-anchor-focus-and-keyboard-navigation.md)). So
the keys inside a popover are the contents' to implement — and **what they mean is fixed here**,
so that substituting Chrome still cannot change behaviour
([ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md)). Every Chrome is held to the same
table by running the same browser tests against it (FN-17).

| In | Key | Meaning |
|---|---|---|
| a menu | ↑ / ↓ | the previous / next **enabled** item, wrapping at the ends |
| | Home / End | the first / last enabled item |
| | Enter / Space | runs the item; the menu closes |
| | Tab / Shift+Tab | closes the menu, as a Cancel |
| the filter panel | Tab / Shift+Tab | the next / previous control, **wrapping inside the panel** while it stands |
| | Enter in a value field | Apply |
| either | Escape | closes it, as a Cancel (ADR-0012's layering, unchanged) |

**The core keeps the menu's place, not DOM focus** *(added 2026-09-25, found by layer 3 on the
Blazor Server host)*. A menu used to resolve a key against the item holding DOM focus. A `↓`
moves that focus with `FocusAsync`, which on a circuit lands a round trip later, so `↓` then
`Enter` typed together ran the item **above** the one the user moved to — `Copy` for `Copy
with headers`, with nothing on screen to say so. Now the core holds a **cursor** for the open
menu: it starts on the first enabled item, and every key moves it or reads it. The contexts
carry **`ResolveKey`**, which answers `MenuKeys`' question against that cursor and moves it on
a `Move`; the contents — the built-in Chrome's and any substitute's — send each key there and
act on the answer: focus the item a `Move` names, invoke the command a `Run` names, `Close`
on a `Close`. Keys reach the core in the order they were pressed, so the answer is right
however far behind DOM focus is; focus only follows it, for the eye and for assistive
technology. `MenuKeys.Resolve` stays the pure table, and a Chrome that resolved against its
own idea of the current item is the defect this replaces.

**A popover that takes DOM focus is named.** The menus keep `role="menu"` with `menuitem` items;
the filter panel is `role="dialog"`. A column's menu and panel carry `aria-label` set to **that
column's own header text** — the Consumer's words, so the core still holds no sentence
([ADR-0036](./0036-the-context-menu-is-the-column-menu-shape-over-a-selection.md)) — and the
Context Menu needs none beyond its role. The root keeps owning the rest of the surface
([ADR-0033](./0033-the-accessibility-surface-is-owned-by-the-root-not-by-cells.md)).

## Closing, and giving the keyboard back

However a popover closes — Escape, its ▾ pressed again, a pointer-down elsewhere in the instance,
a command run, Apply, Cancel, Clear — **DOM focus returns to the root**, wherever it was when the
popover closed, **an Inner Popup included**. The next arrow moves the Focus, as it did before the
popover opened. The one exception is the one ADR-0010 already makes: a pointer-down that
dismissed the popover keeps its own meaning, and where that press lands decides focus.

*(Added 2026-09-25.)* **The core has the last word.** On a circuit a focus request travels a
round trip, and one the contents made for their first item could land after the user had
already dismissed the popover — on an item about to be removed, leaving DOM focus on nothing.
So focus is handed back **after the render that removes the popover**, which on a circuit comes
after any request the contents made while it stood; a pointer-down landing inside the grid is
included, since the root is where that press puts focus anyway. A key pressed after a popover
opened **by pointer** but before it took focus lands on nothing and is lost — visibly, as a key
that did nothing; one pressed after a popover opened by key is held (ADR-0010).

*(Added 2026-09-25, from the Server run after `main` was merged.)* **And the first word too,
when the core closes it.** Handed back only after that render, the keyboard spent a round trip
nowhere. The element that held focus was removed, DOM focus fell to `body`, and the
capture-phase listener on the root heard nothing: under the MudBlazor Chrome, `Shift+F10`
typed straight after the Escape that closed a panel opened nothing. So a popover the core
closes — Escape, a command, Apply, Cancel, the Chrome's own `Close` — also asks for the root's
focus **before** the render that removes it, and the request reaches the browser ahead of that
render. The request after the render stays, for the reason above. A pointer-down that
dismisses is still left to where it lands.

## Inner Popups

An **Inner Popup** is a popup that a seam's contents open for themselves and that their design
system draws **outside the instance root** — `MudSelect`'s list of options, `MudDatePicker`'s
calendar. MudBlazor draws every such popup into one page-wide provider and positions it with its
own script. What that does to the grid, read from MudBlazor 9.9's source rather than predicted:

| ADR-0010's promise | With an Inner Popup open |
|---|---|
| Escape closes the popover | **The Inner Popup closes first**, and the next Escape closes the popover — innermost first, which is what a user expects of nested popups. *(Corrected 2026-09-24; the prediction was wrong.)* It said DOM focus would be inside the popup, outside the root, so the grid would never see that Escape. In the browser, MudBlazor 9.9's `MudSelect` and `MudDatePicker` both keep DOM focus **on their own control, inside the popover**, while their list or calendar is open. The gate took the Escape, and one press closed both. So the contents now **report** their popup. Every popover context carries **`InnerPopupChanged`**, which the contents call with `true` when a popup of theirs opens and `false` when it closes. While one is open, the capture-phase gate leaves a descendant's Escape to the control, and the design system closes its popup. Innermost first is now kept by being told, not by where focus happens to sit. *(Considered and withdrawn 2026-09-25. Layer 3 on the Blazor Server host saw an Escape pressed straight after the date calendar opened leave the calendar open, and the prediction was that the gate — told about the popup a round trip late — had taken it; reading the control's `aria-expanded` was decided to close that gap. The prediction was wrong. The gate did not take the Escape: the core already knew the popup was open and left the panel alone, and MudBlazor's date picker ignores an Escape while DOM focus is still on the button that opened its calendar, which a round trip in, it is. MudBlazor's select, whose list keeps focus on the combobox, closes on the same Escape with or without the check. With no case that the check changes, it was not built; KB-35 holds the grid to its half — it does not take that Escape, and the popover stands.)* |
| a pointer-down elsewhere in the instance closes it and keeps its own meaning | **Under MudBlazor's default (`ModalOverlay = false`) it still does**: the design system closes its popup from a document-level listener it holds only while the popup is open, and the press goes on to reach the grid. **Under `ModalOverlay = true`** — a Consumer's global setting — the press is swallowed by the design system's overlay and only the Inner Popup closes. The grid acts on what reaches it; it does not reach past a design system's overlay to recover a press that system chose to consume, and that choice is recorded as the Consumer's |
| the ▾ toggle closes it | unchanged, and closing the popover removes its contents — **the Inner Popup goes with them** |
| instances stay independent (ADR-0018) | **the grid's own elements stay under its root**, without exception. What a Consumer's or Wrapper's Chrome opens is that design system's, and the core **does not assume** that everything its Chrome shows lies under the root — the rule above that returns focus from outside it is that non-assumption in practice |

**Script.** [ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md)'s "Chrome implementations
must not smuggle JS in" means **script a Wrapper adds**: a Wrapper ships no `.js` of its own and
makes no interop calls of its own. The scripts a design system's own components run for
themselves — MudBlazor's popup positioning, its key interceptor, its ripple — are that design
system's, loaded by the Consumer's choice of it, and are not the grid's allowlist to count.

**Verified, not trusted.** Layer 3 exercises Inner Popups through `ExGrid.MudBlazor`'s own filter
panel — its operator is a `MudSelect`, its date value a `MudDatePicker` — by pointer and by key,
under both `ModalOverlay` settings, with two grids on the page and with one inside a `MudDialog`.
If a row of the table above turns out wrong in a real browser, it is corrected here, as the
prediction it replaces was.

*(Refined 2026-09-25, when CI first ran layer 3 against the Blazor Server host.)* Two things the
table promises were being lost on a circuit. Neither prediction had considered it; both were
found in a real browser and fixed as follows.
- **The Escape after an Inner Popup's own is the grid's, counted by the gate.** The report
  that the popup closed arrives a round trip after the Escape that closed it. A second Escape
  typed inside that round trip was still left to a popup that was gone, so the panel stood.
  The gate now clears its record of the popup when it leaves an Escape to it. The next Escape
  is the grid's whatever the report says, which is the row above as it was written.
- **Enter in a value field applies a value typed with it.** The Wrapper's Enter was the
  browser's implicit submission, gated by Apply's `disabled` button. On a circuit the button
  is enabled a round trip after the value, so an Enter typed with the value submitted
  nothing. The form's default button is now a hidden one that is never disabled. Apply
  refuses on the circuit while it is unavailable, and there the value has already arrived
  (WR-2 unchanged: Apply still shows it is unavailable).
- **Taking the keyboard does not move the menu.** A menu opened by pointer takes focus a round
  trip after it is shown. A menu taller than its grid scrolls (ADR-0040), and one the user
  had already scrolled was pulled back to its top by that focus. The opening focus is now
  asked for without scrolling, under both Chromes. The Wrapper's item is a `MudButton`
  subclass that reaches its own element, since MudBlazor's `FocusAsync` always scrolls. A
  focus an arrow moves still scrolls its item into view.

**No JavaScript is added**: every focus move is Blazor's `FocusAsync`, and the allowlist does not
grow. The reported Inner Popup is one more state of the capture-phase listener the allowlist
already holds — set from C#, as the editor's mode is — and not a new use.

*(Where a popover stands, and why no ancestor may cut it, moved to
[ADR-0040](./0040-a-popover-stays-inside-its-grids-box.md) on 2026-09-24: this ADR's Inner Popup
table assumed the popover itself could not be clipped, and inside a dialog it could.)*

## What this changes elsewhere

- **ADR-0010**: the popover section points here for the keyboard and for Inner Popups; its three
  dismissals are unchanged.
- **ADR-0012**: Escape's layering gains its innermost layer — an Inner Popup, which is its design
  system's to close.
- **ADR-0021**: the consequence about Chrome and script is narrowed to script a Wrapper adds, as
  above.
- **ADR-0030**: "never a `MudPopover`" is rewritten, keeping what it predicted and why it was
  replaced.
- **ADR-0036**: the Context Menu's keyboard trigger now leads somewhere — its items take the
  keyboard.
- **CONTEXT.md** gains **Inner Popup**.
