# 80: A Keyboard Field holds the keyboard on a selected cell

Status: ready-for-agent

**What to build:** ADR-0080, decided with the user on 2026-10-02. On a grid that edits, the keyboard
with no edit open is held by the Keyboard Field, so that a real IME can start on a selected cell, as
it does in Excel. Ticket 79's prototype (`agent/ps-79-ime-prototype`, 3adf4fd for the code, c4133d3
for its layer-3 spec) is the starting point. It was built on `5c7ceaa`, before tickets 77 and 78
changed the same script, and it is not merged as it stands: it is brought onto this branch, and what
ADR-0080 decided beyond it is added.

**Blocked by:** None.

- [ ] The prototype's Keyboard Field, on this branch: the field (`ExGrid.KeyField.cs`, the markup, the
      stylesheet), its composition in the key gate, the held composition text, the editor's request
      waiting while it composes, a press ending a composition first, and `reclaimFocus` putting the
      keyboard in the field. Tickets 77 and 78's changes to `ex-grid.js` are kept. Every `PROTOTYPE`
      comment is rewritten to describe the decision and name ADR-0080
- [ ] The field is the grid's one tab stop on a grid that edits: `tabindex="0"` on the field, `-1` on
      the root. A display-only grid keeps `tabindex="0"` on the root. A Prerendered grid has neither,
      and no field (ADR-0033, A11Y-4, A11Y-20)
- [ ] `aria-activedescendant` is carried by the element that holds the keyboard: the field on a grid
      that edits, the root otherwise, never both. Every rule ADR-0033 and ADR-0037 give it holds (cleared
      while the Focus is not painted, the chosen action's button while Interactive). A11Y-21: over CDP,
      the focused node is the field and its active descendant resolves to the `gridcell` or the
      `button`. If Chromium does not resolve it, stop and report
- [ ] KB-12: the root's ring shows when its field holds the keyboard that did not arrive by a press on
      the grid, and not after a click. The script marks the root; the stylesheet draws the ring from
      the mark as `.ex-grid:focus-visible` did. A display-only grid keeps `:focus-visible` on the root
- [ ] ADR-0012's release of Tab ends when DOM focus leaves the grid: the root's `focusout` whose next
      holder is outside the root, on every grid, with a field or not. Escape, a press on a control
      elsewhere on the page, Tab back into the grid, then Tab cycles inside the selection (KB-8)
- [ ] ADR-0021's allowlist tests name the seventh entry's listeners (`compositionstart`,
      `compositionend`, the root's `focus` and `focusout`), and the count of focus decisions made in
      script moves to five; ADR-0057's test still pins the coloured text's own `compositionend` as
      heard only while an edit is open. The tests change because the ADR did, not to make the build pass
- [ ] Layer 2 for the field, its tab stop and its `aria-activedescendant`, the composition's text
      opening Overwrite, a cancelled composition, a cell that does not edit, and the release ending
      on `focusout` where C# can see it. Layers 1 and 2 in full pass
- [ ] Layer 3: the prototype's `key-field.spec.mjs` (composing through `Input.imeSetComposition` and
      `Input.insertText`), ED-30's cases, A11Y-4 both ways on a grid that edits and on one that does
      not, A11Y-21 over CDP, KB-8's new case and KB-12. Every existing assertion that a grid's root
      holds DOM focus, where it means "the keyboard is this grid's", reads "the root or its Keyboard
      Field" through one helper; ED-26's and ED-28's readings follow. Locally layers 1 and 2 only; layer
      3 is CI's (the user's rule, 2026-10-01)
- [ ] ExGrid and ExSheet alike, under both Chromes (`?chrome=mud` included), on both hosts; several
      grids on one page each have their own field (ADR-0018)
