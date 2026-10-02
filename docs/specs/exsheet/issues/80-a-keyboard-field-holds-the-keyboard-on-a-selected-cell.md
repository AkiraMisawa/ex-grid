# 80: A Keyboard Field holds the keyboard on a selected cell

Status: done

**What to build:** ADR-0080, decided with the user on 2026-10-02. On a grid that edits, the keyboard
with no edit open is held by the Keyboard Field, so that a real IME can start on a selected cell, as
it does in Excel. Ticket 79's prototype (`agent/ps-79-ime-prototype`, 3adf4fd for the code, c4133d3
for its layer-3 spec) is the starting point. It was built on `5c7ceaa`, before tickets 77 and 78
changed the same script, and it is not merged as it stands: it is brought onto this branch, and what
ADR-0080 decided beyond it is added.

**Blocked by:** None.

- [x] The prototype's Keyboard Field, on this branch: the field (`ExGrid.KeyField.cs`, the markup, the
      stylesheet), its composition in the key gate, the held composition text, the editor's request
      waiting while it composes, a press ending a composition first, and `reclaimFocus` putting the
      keyboard in the field. Tickets 77 and 78's changes to `ex-grid.js` are kept. Every `PROTOTYPE`
      comment is rewritten to describe the decision and name ADR-0080
- [x] The field is the grid's one tab stop on a grid that edits: `tabindex="0"` on the field, `-1` on
      the root. A display-only grid keeps `tabindex="0"` on the root. A Prerendered grid has neither,
      and no field (ADR-0033, A11Y-4, A11Y-20)
- [x] The header's ▾ buttons carry `tabindex="-1"` on every grid, under both Chromes (decided with
      the user, 2026-10-02, ADR-0080): a Tab into the grid reaches the field, or the root, first, and
      the next Tab leaves the grid. A press and Alt+↓ still open the column's popover
- [x] `aria-activedescendant` is carried by the element that holds the keyboard: the field on a grid
      that edits, the root otherwise, never both. Every rule ADR-0033 and ADR-0037 give it holds (cleared
      while the Focus is not painted, the chosen action's button while Interactive). A11Y-21: over CDP,
      the focused node is the field and its active descendant resolves to the `gridcell` or the
      `button`. If Chromium does not resolve it, stop and report
- [x] KB-12: the root's ring shows when its field holds the keyboard that did not arrive by a press on
      the grid, and not after a click. The script marks the root; the stylesheet draws the ring from
      the mark as `.ex-grid:focus-visible` did. A display-only grid keeps `:focus-visible` on the root
- [x] ADR-0012's release of Tab ends when DOM focus leaves the grid: the root's `focusout` whose next
      holder is outside the root, on every grid, with a field or not. Escape, a press on a control
      elsewhere on the page, Tab back into the grid, then Tab cycles inside the selection (KB-8)
- [x] ADR-0021's allowlist tests name the seventh entry's listeners (`compositionstart`,
      `compositionend`, the root's `focus` and `focusout`), and the count of focus decisions made in
      script moves to five; ADR-0057's test still pins the coloured text's own `compositionend` as
      heard only while an edit is open. The tests change because the ADR did, not to make the build pass
- [x] Layer 2 for the field, its tab stop and its `aria-activedescendant`, the composition's text
      opening Overwrite, a cancelled composition, a cell that does not edit, and the release ending
      on `focusout` where C# can see it. Layers 1 and 2 in full pass
- [x] Layer 3: the prototype's `key-field.spec.mjs` (composing through `Input.imeSetComposition` and
      `Input.insertText`), ED-30's cases, A11Y-4 both ways on a grid that edits and on one that does
      not, A11Y-21 over CDP, KB-8's new case and KB-12. Every existing assertion that a grid's root
      holds DOM focus, where it means "the keyboard is this grid's", reads "the root or its Keyboard
      Field" through one helper; ED-26's and ED-28's readings follow. Locally layers 1 and 2 only; layer
      3 is CI's (the user's rule, 2026-10-01)
- [x] ExGrid and ExSheet alike, under both Chromes (`?chrome=mud` included), on both hosts; several
      grids on one page each have their own field (ADR-0018)

## Comments

2026-10-02, built on `agent/ps-80` (the Keyboard Field) and `agent/ps-80-suite` (the existing layer-3
specs), merged into `claude/exsheet-keyboard-field`, PR #44.

- **The prototype** (3adf4fd, and the test part of c4133d3) was brought over. Its one conflict, in
  `onPress`, keeps both: a press ends a composition first, then ticket 77's release logic runs. The
  field's composition handlers are `onKeyFieldCompositionStart` and `onKeyFieldCompositionEnd`, kept
  apart from the coloured text's `onCompositionEnd`.
- **KB-12's mark is an attribute**, `data-ex-focus-visible` on the root, not a class. Blazor rewrites
  the root's `class` whenever `ex-editing`, `ex-loading` or `ex-pointed-at` changes, and that would
  wipe a class the script had added while the field still held the keyboard. One rule draws
  `.ex-grid:focus-visible, .ex-grid[data-ex-focus-visible]`.
- **The release of Tab** ends on a `focusout` whose next holder is outside the root. It is counted
  with the presses, so a `releaseTab` answer that arrives after focus has left is not granted. A switch
  to another window keeps it (ADR-0080).
- **The header's ▾** is `tabindex="-1"` in both places the core paints it. The MudBlazor Chrome has
  no ▾ of its own, so `?chrome=mud` is covered by the same button.
- **The demo's `/cells`** gains opt-in `?editable=1` and `?chrome=` flags for A11Y-21's
  chosen-button case. Without them the page is unchanged.
- **Which DemoHost grids have a field:** `/features`' and `/mud`'s first grids, the Sheets on
  `/sheet`, `/sheets` and `/pointing`, and `/cells?editable=1`. Every other grid is display-only,
  including the positions grids and `/inspector-edits`.
- **Layer 3:**
  - The rewording goes through `tests/ExGrid.Browser/keyboard.mjs`. On a grid with a field,
    `expectKeyboardOn` requires DOM focus on that field. It finds the grid's own field by its path
    from the root, so a nested grid's field never counts.
  - `aria-activedescendant` is read from the element that carries it.
  - MEM-4 counts 13 listeners on the root.
  - PF-1 finds the module's handle by its name.
- **Verification:**
  - Layers 1 and 2: 829 + 2135 + 92 + 1236 (1 skipped, as before) + 395.
  - Layer 3: CI's 16 checks are green at 9951ee0. The one failure across the two runs was the
    Layout Ceiling flake recorded in #38, which passed on a rerun.
  - A real IME is the sixteenth Windows run's.

