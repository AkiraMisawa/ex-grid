# A box's first control takes the keyboard from the grid's script where no grid stands

*Decided with the user, 2026-10-24: of a seam member a Wrapper names its field by, a new
[ADR-0021](0021-javascript-is-allowlisted-not-minimised.md) entry, or leaving the gap as a known
limitation, the user chose the entry. It closes the gap ticket 06 of the Input Order left open
([ADR-0240](0240-input-to-one-product-instance-is-taken-in-one-order.md)'s note on its second part).*

## Context

A Pivot Chrome's control takes the keyboard through its context's `TakeKeyboard`
(ADR-0240 section 6). While the report's grid stands, ExPivot asks that grid to hand the keyboard on
(`HandKeyboardToAsync`), and the grid's script finds the control: the element itself when it is a
control, else a box's first enabled text field, else its first enabled control. A Chrome can therefore
name a box around a design system's control whose input it cannot reference in .NET, as ExSheet's More
Colours does (ADR-0039).

While no report stands there is no grid: the report is empty, its first answer is still on its way, or
the source failed it. ExPivot then used the control's own `FocusAsync`. The MudBlazor Wrapper's Field
Settings… names the box around its sort order's `MudSelect`, a `<div style="display: contents">`, because
MudSelect hands out no reference to its input; a box drawn with `display: contents` cannot take focus, so
the panel opened with the keyboard left where it was. Blazor has no API that focuses an element's first
control, and `FocusAsync` on the box focuses nothing.

## Decisions

- **The grid's script answers, without a grid.** `ex-grid.js` exports `focusBox(element)`, beside
  `stamp` and `attach`. It finds the control as the hand-on finds it (one `boxTarget` function, shared by
  both), focuses it, selects a text field's text when the field was found inside the box, and answers
  whether DOM focus is now on it.
- **No condition is read.** With no grid there is no Input Order the hand-off could break, and the call
  replaces an unconditional `FocusAsync`: one call, as that was, once the script is loaded.
- **ExPivot asks only where no grid stands.** Its `HandKeyboardToAsync` goes through the report grid's
  hand-on while a report stands. Otherwise it loads the script through a `GridScriptFocus` it keeps for its
  life — imported once at the address this build ships (ADR-0123), a script of another version refused —
  and looks again: a report drawn while the script loaded hands the keyboard on as before, and with none
  it calls `focusBox`. Where the script is refused, would not load, threw, or no control took the
  keyboard, the control's own `FocusAsync` is still asked, as before. `GridScriptFocus` is public because
  a sibling product uses it, and hidden from a Consumer's editor (ADR-0201).
- **It is a new entry of ADR-0021's list**, the twentieth, and the tenth place where script moves DOM
  focus. It runs once per request, never per render; it reads `document.activeElement` and no layout,
  adds no listener and sets no timer, and keeps no state: the module's top level holds only pure
  functions.

## Considered and rejected

- **A seam member a Wrapper names its field by** (for example a `Func<ValueTask>` beside the box, which
  the MudBlazor Wrapper would answer with `MudSelect.FocusAsync`). No JavaScript, but a new public member
  on the Pivot Chrome contexts with a Change Note, and the Wrapper's box would take the keyboard one way
  while a report stands and another while none does. Not chosen by the user.
- **Leaving it as a known limitation for 0.2.0.** Not chosen by the user.

## Consequences

- Swapping the Chrome changes no behaviour here (ADR-0061): the built-in panels' searches and the
  Wrapper's boxes take the keyboard through the same function whether or not a report stands.
- A Consumer's own Pivot Chrome may name a box, not only a control, to `TakeKeyboard` in every state of
  the report.
- `ShippedStylesheetTests` pins the function's text, the module's top level and the count of places that
  move focus; a new one needs an ADR as before.
