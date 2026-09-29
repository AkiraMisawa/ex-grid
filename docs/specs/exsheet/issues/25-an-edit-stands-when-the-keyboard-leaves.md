# 25: An edit stands when the keyboard leaves the grid, and a press brings it back

Status: ready-for-agent

**What to build:** ADR-0018, section 6 (2026-09-29), with ADR-0021's note of the same day, for ExGrid
as a whole. Found on `/sheet`: `=` typed into a cell, then a click on the positions grid. The Sheet's
edit stayed open and nothing reached it again. Escape went to the positions grid, and a click back on
the Sheet's rows pointed while the keyboard stayed with the positions grid.

**Blocked by:** None (can start immediately)

- [ ] Losing DOM focus neither commits nor discards an open edit: to another grid, to a control on
      the page, or to nothing (ED-26)
- [ ] Escape and other keys pressed in another grid are that grid's; this grid's edit stays open
      (ED-26, KB-1)
- [ ] A press on the rows or the headings while an edit stands here and DOM focus is outside the root
      puts the keyboard into the editor surface that last held it, in the capture-phase `mousedown`,
      before the press is handled (ADR-0021 note)
- [ ] After that press, a press that points leaves the keyboard in the edit's surface and the next
      Escape cancels this edit; a press that commits leaves it on the root (ED-26)
- [ ] The Formula Bar case: an edit last typed in the bar gets the keyboard back in the bar
- [ ] Held keys and held presses (ED-22) still keep their order when the keyboard comes back
- [ ] The comment on `reclaimFocus` no longer calls itself the one decision about focus made in
      script
- [ ] No JavaScript use is added; the listener stays the allowlisted `mousedown` (ADR-0021)
- [ ] Layer 3 on `/sheet` and `/sheets`, both hosts (ED-26)
