# 77: Escape with nothing to dismiss releases Tab, not the keyboard

Status: ready-for-agent

**What to build:** ADR-0012, the paragraph rewritten on 2026-10-01 (decided with the user), and KB-8
as rewritten. The fifteenth Windows run (`verification/2026-10-01-windows-15/report.md`, "Seen, and not
asked", case i2) saw a second Escape with no edit open send DOM focus from the grid to `body` on
`/sheet`, in all twelve configurations. That was ADR-0012's Leave as first decided
(`GridKeyKind.Leave` → `LeaveAsync` → the handle's `blur`), not a defect; the decision itself changed.
*(This ticket first called it a defect; ps-77 found the decision before writing code.)*

**Blocked by:** None. Ticket 79's prototype moves the keyboard off the root; what Escape releases
there is that prototype's to report.

- [ ] Escape with nothing left to dismiss (no edit, no popover, no list, no Interactive cell) keeps DOM
      focus on the root and releases Tab: the next Tab or Shift+Tab is not claimed, so the browser moves
      to the next or the previous element of the page (ADR-0012, KB-8)
- [ ] Any other key after that Escape keeps its meaning and ends the release: a character opens an edit
      in the selected cell, an arrow moves, and a later Tab cycles inside the selection again. A press
      on the grid ends it too (decided 2026-10-01): in the capture-phase `mousedown` already attached
- [ ] The Escape is a change of the claimed set (ADR-0010): keys typed after it are held until it is
      answered, as after any mode change; no listener is added and no layout is read (ADR-0021)
- [ ] ExGrid and ExSheet alike, under both Chromes; the Escape a popover, an Inner Popup, Find, a
      completion list or an edit takes keeps its meaning
- [ ] Layer 2 for the gate and the core; Layer 3 on both hosts: `features.spec.mjs`'s KB-8 test rewritten
      (Escape, then Tab reaches the next page element; the root held focus in between), and on `/sheet`
      under both Chromes: a character, Escape, Escape, a character opens an edit in the selected cell

## Comments
