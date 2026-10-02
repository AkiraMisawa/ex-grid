# A Keyboard Field holds the keyboard, so an IME can start on a selected cell

*(Decided with the user on 2026-10-02, after the fifteenth Windows run
(`verification/2026-10-01-windows-15/`) and the prototype of ticket 79, built on
`agent/ps-79-ime-prototype` (3adf4fd) and not merged. The ticket's Comments hold the prototype's
measurements and the three designs weighed; this ADR records the one taken and what it changes.)*

## What the fifteenth run found

With the Microsoft Japanese IME driven by real keys, every composing key in an open edit, the Formula
Bar, the Name Box and Find reached the field, and the core took none (ADR-0010, ED-11). **On a
selected cell with no edit open, the IME could not start.** DOM focus is on the root, a `div` that is
not editable. Chrome and Edge give such an element no input context, so the IME's own key leaves it
off, and `kana` typed onto D10 opened an edit holding `kana` in Latin letters. Excel composes from the
first key on a selected cell. Japanese could be typed only after F2, or in the Formula Bar.

No Blazor API, and no attribute of the root that leaves it a grid, gives an IME an input context on an
element that is not editable. Something editable has to hold the keyboard while a cell is selected.

## The decision

**While a grid has an editable column, the keyboard with no edit open is held by the Keyboard Field:
a text field of the grid's own, inside the root, standing over the Focus cell and not seen.** A
display-only grid has none, and keeps the keyboard on its root, as before.

- **Where it stands.** One `input` per grid, the Viewport's first child, keyed so that no render
  replaces the element holding DOM focus. Its box is the Cell Editor's, from the same arithmetic
  (ADR-0010, ADR-0013): over the Focus cell; over the first painted cell with no Focus, where the
  first key would open the edit (ADR-0012); and, with the Focus not painted, at the painted rows'
  top-left, held at the Viewport's left edge, until a composition reveals the Focus. It is unseen
  (opacity 0, no caret, no pointer events). Its value is the script's; no binding writes it.
- **Keys.** The capture-phase listener on the root hears every key first, as before. The field counts
  as the root: ADR-0010's guard (`event.target === root`) widens to the root or this grid's own field,
  never a nested grid's. So the gate decides exactly what it decided. The keys it takes are prevented
  and never reach the field; a composing key (`isComposing`, keyCode 229) is left alone, as ADR-0010
  requires, and reaches the field, where the IME composes.
- **Read-only where typing opens nothing.** Over a cell that is not Editable, or over a row not in
  hand, the field is `readonly`, so an IME stays off there as it did on the root. Chrome gives a
  read-only field no input context either.
- **A composition.** When one starts in the field, the script marks it, and the stylesheet draws it
  as the Cell Editor is drawn, over the Focus cell, with the composition in it. The core is told, and
  reveals the Focus if a scroll had taken it away, as Excel scrolls to the active cell when typing
  begins. **No edit is open while the composition lasts.** When it ends, its text takes its place
  among the held keys (ADR-0010's hold). The core opens the Cell Editor in Overwrite holding it, judged
  as a typed character is (ADR-0010, ADR-0035). The keys typed after it wait until the editor has the
  keyboard, as after any key that opens an edit. The field keeps showing the text until the keyboard
  has left it, so nothing blinks out for a round trip. A cancelled composition, whose text is empty,
  opens an empty edit, as Excel's first Escape leaves one (the fifteenth run, i2). Over a cell that
  does not edit, nothing opens.
- **DOM focus never moves while a composition lasts.** Moving it ends the composition there and then,
  and the next keys start another: `kana` would come out `ｋあ` (the fifteenth run's i1y saw `k穴`).
  - The core's request that the editor take the keyboard waits while the field composes, and is
    granted when the composition ends.
  - A second composition the IME finishes in the field before the first one's editor has the keyboard
    (a circuit, a fast typist) is typed into the edit at its caret, in order.
  - A primary press anywhere in the root during a composition ends it first: the field gives up the
    keyboard, and the IME ends the composition there. Its text is held ahead of the press and goes to
    the cell it was composed on. The press then commits it there, as Excel's click-away does, and
    selects what it pressed.
- **Text put into the field without a composition.** A character an IME commits at once, or a
  full-width space, is carried as a composition's text is. Anything else (the browser's own undo, a
  drop) is not typing the grid takes, and is removed.
- **Copy and paste** fire on the field, inside the root, and are the root's, as before (ADR-0005).
  The keydown no longer drops the document's selection when a key lands on the field, since that
  selection is the field's caret, which the IME needs (ADR-0021, the clipboard entry's note of
  2026-09-29, still holds for a key on the root).

## Where the keyboard comes back, and the one tab stop

- **The field is the grid's one tab stop** (ADR-0033, A11Y-4): `tabindex="0"` on the field, and `-1`
  on the root, which stays focusable by a press and by script. A grid without a field keeps
  `tabindex="0"` on its root. A Prerendered grid has neither (ADR-0033's row): the field stands only
  once the key listener is attached. The reason is ADR-0012's release of Tab (ticket 77). From the
  field, Shift+Tab is the browser's sequential navigation from the field's place in the markup, and
  with the root as the tab stop the element right before the field is the root itself. It would take
  focus, pass it straight back to the field, and trap the user.
- **The header's ▾ buttons are not tab stops** *(decided with the user the same day, found while
  building)*. The field stands in the Viewport, after the header in the markup, so a Tab into a grid
  that edits reached every column's ▾ before the field: as many Tabs as columns before the first cell.
  The root, their ancestor, had come first before. The ▾ buttons now carry `tabindex="-1"` on every
  grid, as the action buttons have since ADR-0037 ("a grid button is only ever pressed, never
  focused" by Tab). A press still opens the column's popover, and so does Alt+↓ from the Focus cell
  (ADR-0044, FL-12), so the keyboard loses nothing. Excel's filter buttons are not reached by Tab
  either. The options set aside: accepting the order, and standing the field before the header,
  which would take the field's box out of the Cell Editor's arithmetic.
- **Focus that lands on the root itself** — a press on a part of it that takes no focus of its own,
  the scroller's hand-on (ADR-0033) — is passed on to the field at once, in script, before the next
  key. The core's hand-back of the keyboard (ADR-0021's notes, `reclaimFocus`) puts it in the field.
  Every place that asks whether this grid holds the keyboard asks whether DOM focus is inside the
  root, as ADR-0018 section 6 already does.
- **The root's focus ring stays KB-12's.** A text field matches `:focus-visible` on every focus, a
  click's too, so the field cannot say "the keyboard arrived by the keyboard". The script marks the
  root while its field holds the keyboard and did not get it from a press on the grid, and the
  stylesheet draws the root's ring from that mark, as `.ex-grid:focus-visible` drew it.
- **Escape's release of Tab** (ADR-0012, rewritten 2026-10-01) holds from the field: the next Tab or
  Shift+Tab leaves for the page's next or previous element. **The release also ends when DOM focus
  leaves the grid**, which the root's `focusout` now tells, on every grid, with a field or not. This
  closes the gap ticket 77 recorded: Escape, a press elsewhere on the page, then Tab back into the grid
  had left the release standing, so the next Tab left again. A switch to another window is not
  leaving: the browser sends `focusout` with no next holder, but DOM focus stays where it was, and
  the release stands *(found while building, 2026-10-02)*.

## What assistive technology is given *(decided with the user)*

**The field carries `aria-activedescendant`, naming the Focus cell, or the chosen action's button
(ADR-0037), with every rule ADR-0033 gives the attribute** (cleared while the Focus is not painted,
A11Y-6). The root carried it, because the root held the keyboard; the element holding the keyboard
carries it now, and the root does not while its field holds it. ARIA allows the attribute on a
textbox. The root keeps `role="grid"` and everything else ADR-0033's table emits. Like the Cell Editor,
which is also a text field inside the grid, the field cannot be `aria-hidden`, since it holds focus.

Whether Chromium's accessibility tree resolves the field's active descendant to the cell is checked
over the DevTools protocol, as ADR-0037's button was. If it does not, this stops and goes back to the
user. What a screen reader then says is still owed a real one, as ADR-0033 already records.

## What differs from Excel, and is kept

- **The core hears of the edit only when the composition ends.** While it lasts, the Formula Bar
  shows the cell's old value, the Name Box and the editing state are unchanged, and `OnEditingChanged`
  is raised at the end. Excel is in Enter mode from the first composed key. Opening the edit at the
  composition's start would need the composition to cross from the field to the Cell Editor, and DOM
  focus moving ends it (above).
- **Under a substituted Chrome** (`?chrome=mud`), the composing cell looks like the built-in Cell
  Editor until the IME commits: the composition is drawn by the core's field, not by the Chrome's
  editor. From the commit on, the Chrome's editor holds the text, as a typed character reaches it
  (ADR-0010's seam is unchanged).

## Considered options

1. **A hidden field whose composition is handed to the Cell Editor at its end** — chosen. It is the
   only one that leaves the Chrome seam's contract as it is, and it never asks a composition to cross
   elements.
2. **The Cell Editor itself kept open and unseen.** No hand-over, and the edit could open at the
   composition's start, as Excel's does. But every Chrome's editor would have to stand mounted, empty,
   focusable and invisible whenever a cell is selected, a dormant state `CellEditorContext` does not
   have and a contract a Chrome can break visibly (a MudTextField's underline and label over every
   selected cell). And "an edit is open" and "the editor is mounted" would come apart everywhere the
   gate, the coloured text, Find and the suite read `.ex-editor` as an open edit.
3. **A field that becomes the editor.** Under the built-in Chrome, option 2 confined to the core's own
   input. Under a substituted Chrome, the edit an IME opens would be typed in the core's field for its
   whole life, and swapping Chrome would change which control the user types into (ADR-0010/0030); or
   it hands over at the composition's end, which is option 1.
4. **Not taking it**: Japanese typed after F2 or in the Formula Bar. Set aside: on a selected cell the
   IME's key does nothing visible, and the user types Latin text into the cell.

## Consequences

- **ADR-0005.** The hidden-field trick its clipboard note recorded as avoided is now carried, for the
  IME rather than for the clipboard. The clipboard's route is unchanged.
- **ADR-0010.** The guard widens to the root's field; the hold gains one kind of held item, a
  composition's text; the editor's request for the keyboard waits while the field composes.
- **ADR-0012.** The release of Tab ends when DOM focus leaves the grid.
- **ADR-0018 section 1.** The listener stays on the root. Which grid is active is answered by
  `root.contains(document.activeElement)`, not by the root holding focus. Each grid has its own field;
  nothing is shared.
- **ADR-0021.** A seventh entry: the Keyboard Field's composition and focus listeners on the root.
  Two more decisions about focus are made in script (the root's own focus passed on; the field given
  up by a press during a composition), and the third is refined (the editor's request waits while the
  field composes).
- **ADR-0033.** The one tab stop moves to the field on a grid that edits, and so does
  `aria-activedescendant`.
- **ADR-0057.** The coloured text's `compositionend`, heard only while an edit is open, is unchanged;
  the field's composition listeners are separate and always on.
- **The suite.** Every layer-3 assertion that a grid's root holds DOM focus means "the keyboard is
  this grid's", and reads "the root or its Keyboard Field" (ED-26, A11Y-4, KB-8 and about 45 others).
- **A real IME is a Windows run's**, the sixteenth (`docs/specs/exsheet/verify-on-windows-16.md`).
