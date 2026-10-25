# A press let through for its drag takes its place among the held inputs

*Found 2026-10-25 by ticket 07's differential test ([ADR-0240](0240-input-to-one-product-instance-is-taken-in-one-order.md)
§8) on ExPivot, Server host, 150 ms round trip. It corrects how ADR-0240's note on ticket 05 lets a
draggable press through. No allowlist entry is added. Chosen by the user the same day.*

## Context

ADR-0240's note on ticket 05 lets a press on an element the browser may drag (`draggable="true"`, a
Field List entry) go through, even while an earlier input is unanswered. Holding it would take away the
drag the browser starts from it. Its click is numbered as it comes and handled in its turn.

Neither the press's default nor its click is held, though. The default moves DOM focus to what was
pressed at once, and the click takes its number ahead of every key the gate holds from before it. The
test, and the tests written for this decision, found five orders that ended differently fast and slow:

- **Layout ▾, Enter, a field's tick.**
  - Layout ▾ opens its menu and gives its first choice the keyboard a round trip later, and the Enter
    is held for that choice.
  - The tick pressed meanwhile already held DOM focus when the hand-off came. Fast, the keyboard ended
    on the menu's button rather than on the tick.
  - Typed slowly, the Enter presses the choice, and the tick then takes the keyboard.
- **A field's tick, Layout ▾, Space.**
  - The press on Layout ▾ is held behind the tick's click, with its default, so DOM focus stays on the
    tick.
  - The Space typed next is claimed by nothing on a checkbox, so it went as it came and toggled the
    tick at its release.
  - Typed slowly, Layout's menu has the keyboard, and the Space presses its first choice.
- **Layout ▾, Enter, a field's tick, read in Layout ▾.** The tick's click was numbered ahead of the held
  Enter, so the menu's choice was applied after the field was added rather than before. The choice set
  every field's subtotals, the new one's included, where typed slowly it is applied before the field is
  added and leaves it its own.
- **Layout ▾, Enter, a tick, Space on that tick.** Held behind the Enter and handed to the tick in its
  turn, the Space did nothing. A keydown dispatched from script does not make the click a typed Space
  makes on a checkbox.
- **Enter on one of ExPivot's buttons that closes the menu it stands in.**
  - ExPivot's buttons (`PivotFocusButton`, the MudBlazor Wrapper's `MudPivotButton`) are pressed at
    Enter's or Space's keydown, so that a key handed on from the hold presses them too.
  - The click the browser then makes of the same key was numbered as well. Its handler only takes the
    number, and does nothing with it.
  - When the keydown's press closed the menu, the click reached a button already gone. Blazor dropped
    it, and the number was never answered. The Settled Mark (ADR-0220) did not come back until another
    input was told. Typed by hand this is every Enter on a Layout ▾ choice under the Wrapper, at any
    speed.

## Decisions

- **Behind an input the gate holds, or one still being answered, a press let through for its drag also
  takes a place among the held inputs.**
  - A press on the control that claims its click counts, and so does a press on that control's label:
    a Field List entry's caption is its tick's label.
  - It is still let through, and its drag still starts.
  - Its click is held behind it, as a click behind a held press is, and numbered in its turn. A drag
    makes no click, so holding the click takes nothing from the drag.
  - The place the press itself takes holds no event. At its turn, once every input sent before it is
    answered and the keyboard has settled where they left it, it gives the keyboard back to the control
    it pressed, or the control its label names, as its default did when it was made.
  - It does so only while DOM focus is still the instance's. A click elsewhere on the page, outside the
    Input Root, is one the grid does not hear, and it leaves the keyboard where that click put it.
  - It does not do so when a press made after it went on as it came, which put the keyboard where it
    pressed itself, even while its own turn has come and it waits for the inputs before it. A later
    press that takes a place of its own among the held inputs takes the keyboard in its own turn, after
    this one's.
  - Only the primary button, and only while something is held or a hold stands. With nothing waiting,
    there is nothing the press could overtake.
- **A key typed while a press is held is that press's, whatever claims it.**
  - A key normally passes the gate when nothing on its target claims it.
  - While a held `mousedown`, or a press let through for its drag, waits for its turn, a key typed on
    the product's own surface around the root is held too. It is handed on where the presses before it
    leave the keyboard. The press waits from the moment it is held until its own turn is over.
  - A key handed on from the hold is not held again.
  - Tab still goes as it comes. Its move is the browser's, and a held Tab could only be refused, with
    every key behind it (ADR-0230 §1 (b)).
- **A held Space or Enter does what typing it does.**
  - This covers Space on a button, a checkbox or a radio button, and Enter on a button.
  - The control must be outside the root and claim no key: a Field List tick, or a frame's control.
  - Its default is a click the browser makes, and a dispatched keydown makes none. So it is handed on as
    that click (`click()`), unless a handler took the key.
  - A click the control claims is numbered as it goes on, as a held click is, and only while the
    control still stands and is enabled, so that a number is never told for a click that cannot come.
  - A control that claims the key answers it with its own handler, which a click would answer a second
    time, so nothing is done for it here.
- **The click a key makes on a button pressed at the key's keydown is not numbered.**
  - Such a button says so (`data-ex-key-presses`). Its handler takes that click as the key's and does
    nothing with it, without asking for a turn.
  - The script counts the click owed as the handler does: an Enter, or a Space but for its repeats,
    with no modifier, told as the button's key. Only that click is left unnumbered. A pointer's click, or
    one with nothing owed (Shift+Space's, a screen reader's, a script's), is numbered as before.
  - A click a key makes on any other control is numbered as before. ExSheet's toolbar list, for one, is
    pressed by that click.
- **This is the eleventh place where script moves DOM focus.** It sits inside the existing
  capture-phase `mousedown` entry of ADR-0021, not a new entry.
  - It runs once per such press, never per render.
  - It reads `document.activeElement` and no layout.
  - It adds no listener and no timer.

## Considered and rejected

- **Holding the draggable press with its default.** ADR-0240's note on ticket 05 rejects this, because
  the browser starts the drag from the press's default. It was measured again for this decision: in
  Chrome, a `mousedown` whose default is prevented starts no drag from a `draggable="true"` element.
- **Refusing the press's own hand-off when a later press moved focus.** Tried first. The Enter then
  stayed behind a hand-off that never came, and the menu's command was lost.
- **Not numbering any click a key makes on a control that claims keys.** ExSheet's toolbar list items
  claim their keys for Escape, and are pressed by the click Enter makes. Unnumbered, that press would be
  overtaken by the keys typed after it.

## Consequences

- `pivot.spec.mjs` sends three orders at machine speed and one at a time, and asks that each ends the
  same both ways: two under both Chromes, and the third under the built-in Chrome only (below). A fourth
  test checks that Enter on a Layout ▾ choice settles, under both Chromes.
- ADR-0240's note on ticket 05 gains a note: the click of a press let through for its drag is numbered in
  its turn, not as it comes.
- `ShippedStylesheetTests` counts eleven places that move focus. ADR-0021 gains a note.
- ExPivot joins the differential test (ASY-8).
  - A Layout ▾ episode ends with the pivot settled in both runs. A press outside a painted menu
    dismisses that menu and does nothing else (ADR-0061), and at machine speed a menu stands painted
    until its Escape has been answered. A field pressed then would depend on what the user saw, which
    ADR-0240 §8 leaves out.
  - The page's count of raised layouts is not compared. `LayoutChanged` is raised only for a layout the
    report shows (ADR-0061), so a layout that a quicker change replaced is never raised.
- The MudBlazor Wrapper's Field List tick does not keep DOM focus after its click, typed slowly (seen
  2026-10-25). A key typed on it after that goes elsewhere, so the third order is not run under the
  Wrapper. That is the Wrapper's own, and is left to a ticket of its own.
