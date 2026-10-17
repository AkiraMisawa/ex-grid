# 221: The key gate knows the editor's mode with its render, not a message after it

Status: ready-for-agent
Roadmap: ExSheet — A key typed at once after a list closes, Point ends or the edit's mode changes is judged in the new mode

**Found:** by the spec lint (#137, ADR-0220 §6). Five waits in `declarations.spec.mjs` sit between
inputs, each kept with a reason that names this:

- SH-36 (Home and End with no list): once the list is gone, the gate reads the mode it was last told,
  which arrives a message after the render, so a Home or End typed in that gap is the editor's
  (lines ~466 and ~476).
- ticket 75 (↓ at the end of a long Formula): the same, once the names are no longer listed (~565).
- ED-29 (Home after typing that ends Point): typing that ends Point tells the gate a message after
  its render, so a Home in that gap is still Point's (~888).
- ED-29 case 7k (F2 after typing into the bar): the gate is told the new mode after the render, so
  an F2 in that gap is judged in the old mode (~953).

**Today:** the opening of a list is already known with its paint: the gate reads the list's own
`data-ex-list` mark (`ex-grid.js`, `listShown()`), so the waits after "the list is painted" were
removed in #137. Its closing, and the other changes of mode, are still told after the render.

**Why it is a defect:** the meaning of a key depends on how soon after the previous one it is typed
(ADR-0220 §2).

- [ ] Make each change of mode known to the gate with the render that shows it, as the list's opening
      is, or hold the key until it is (decide which, in ADR-0021's terms).
- [ ] Remove the five waits; the tests type at machine speed and pass on both hosts, 20 runs each.

## Comments

2026-10-17, claude/races-220-224. Triaged: needs a decision before a fix. The options and a
recommendation are in ADR-0230 section 2, proposed and not decided. The user decides; the fix
follows on its own branch.

2026-10-17. Decided with the user: ADR-0230 section 2, as recommended. Ready to build.
