# 25: An edit stands when the keyboard leaves the grid, and a press brings it back

Status: ready-for-agent

**What to build:** ADR-0018, section 6 (2026-09-29), with ADR-0021's note of the same day, for ExGrid
as a whole. Found on `/sheet`: `=` typed into a cell, then a click on the positions grid. The Sheet's
edit stayed open and nothing reached it again. Escape went to the positions grid, and a click back on
the Sheet's rows pointed while the keyboard stayed with the positions grid.

**Blocked by:** None (can start immediately)

- [x] Losing DOM focus neither commits nor discards an open edit: to another grid, to a control on
      the page, or to nothing (ED-26)
- [x] Escape and other keys pressed in another grid are that grid's; this grid's edit stays open
      (ED-26, KB-1)
- [x] A press on the rows or the headings while an edit stands here and DOM focus is outside the root
      puts the keyboard into the editor surface that last held it, in the capture-phase `mousedown`,
      before the press is handled (ADR-0021 note)
- [x] After that press, a press that points leaves the keyboard in the edit's surface and the next
      Escape cancels this edit; a press that commits leaves it on the root (ED-26)
- [x] The Formula Bar case: an edit last typed in the bar gets the keyboard back in the bar
- [x] Held keys and held presses (ED-22) still keep their order when the keyboard comes back
- [x] The comment on `reclaimFocus` no longer calls itself the one decision about focus made in
      script
- [x] No JavaScript use is added; the listener stays the allowlisted `mousedown` (ADR-0021)
- [ ] Layer 3 on `/sheet` and `/sheets`, both hosts (ED-26)

## Comments

**2026-09-29, implementation (branch `claude/exsheet-edit-stands`).** Nothing in C# or in
`ex-grid.js` ended an edit on a focus loss; `EditStandsTests` pins that nothing the edit is drawn
with hears a blur or a focusout. The capture-phase `mousedown` now focuses the surface that last
held the keyboard — the one in which the listener last saw a key, an input or a press, taken
afresh as an edit opens and forgotten as it ends; the first `.ex-editor` in the markup when none
is known — for a press on this grid's own rows or headings while an edit is open and DOM focus is
outside the root. A column heading's press does not suppress its default, so it already reached
the root; a Row Heading's is a press on the rows and did not.

Found on the way, and fixed with it: an edit typed in the Formula Bar and committed by a press on
the rows of a grid that declares pointing left DOM focus in the bar, with no edit open — the core
suppresses that press's default (ADR-0051), and the hand-back declined a field with focus of its
own. Typing then showed in the bar and went nowhere (`127` in the bar, B2 untouched). It happened
without leaving the grid too; ED-26 asks for the root after a committing press back, which is the
same path. The bar's focus is now marked as left standing by the press, as `staleField` already
did for a held press, and the hand-back takes it.

The last box stays open until the runner has passed `edit-stands.spec.mjs` on both hosts: it was
written against, and executed through a stand-in harness on, private DemoHosts (Server behind the
latency proxy, and WebAssembly), headless Chrome on macOS — 16/16 on each, 0/16 against the module
before this ticket.

Seen and left alone, since each happens the same without leaving the grid, or before an edit
stands, and changing it needs a decision:

- Keys typed straight after a press on the rows that commits, in a grid that declares pointing,
  before the hand-back has landed, are lost: they go into the Cell Editor the commit is removing,
  then to `body` (`99` in C4, B2 pressed, `7` and Enter at once: B2 unchanged and the Focus still
  on B2 — on the Server host at 150 ms whether or not the keyboard had left, and at 0 ms when it
  had not). ED-22 holds keys behind a key or a held press, not behind a press that passed.
- A press back within one round trip of the key that opens the edit (or of any key still being
  answered, with no edit open yet) is held, and a held press suppresses its default: DOM focus
  stays in the other grid, and the keys typed next go there.
