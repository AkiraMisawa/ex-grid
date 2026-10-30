# 201: A press straight after Enter lands on the cell pressed

Status: done

**What to build:** a defect found while writing `tests/browser/f4-enter-mode.spec.mjs` (PR #128),
on the Server host only. The cause is in ExGrid's core, not ExSheet: any editing ExGrid met it. A
press made straight after the Enter that commits an edit, on the cell that was being edited, must
land on that cell, as it does when made slowly (ADR-0220, the spine's sixth principle; ADR-0010's
press kept in its place among the held keys).

**Today (before the fix):** `=A1+B2` typed into D10, Enter, and a press on D10 at once. Enter is a
mode change, so the listener holds what follows it until the core has answered it, a press on the
rows among them (ADR-0010). But on a circuit the render that removes the Cell Editor comes a round
trip after the Enter, and the editor's box is the cell's own (ADR-0028): the press landed on the
editor, not on the Viewport. A press on the editor is the editor's own, which places a caret and is
never heard by the core, so the listener let it through and the editor's removal took it with it.
The Focus stayed on D11, where Enter had moved it. (The first report read F11, in a spec whose
later keys then moved the Focus on; the isolated test reads D11 in every failing run.) Measured with `hand-back-races.spec.mjs` on the
Server host, `--project=chrome --repeat-each=10`: 8 of 10 runs failed. WebAssembly removes the
editor inside the Enter's own task, and was not affected.

**The fix:** a press on this grid's Cell Editor made while keys are held, or a key is being
answered, keeps its place among them, with its release; so does one made on an editor still
standing after the gate was told no edit is open. Its default stands while the edit is open (it only
places a caret in the editor that holds the keyboard), and is suppressed on an editor about to go.
At its turn, it is the editor's own if the edit still stands; otherwise it is a press on the rows
over the cell the editor stood on. The core writes that cell on the editor's box (`data-ex-cell`),
and the listener tells it with the press's offsets within the editor and the Viewport's first row,
scroll, order and layout (`ExGrid.PressTakenAt`'s `editorRow` and `editorColumn`); the core places
the press over that cell at those offsets, and a press taken under an order since moved lands on no
cell, as any press does (ADR-0142). No listener is added, and nothing is measured or timed.

- [x] **Layer 3:** `hand-back-races.spec.mjs`, "a press straight after the Enter that commits a
      Formula lands on the cell pressed": 20 of 20 on the Server host, 10 of 10 on WebAssembly,
      `--project=chrome`.
- [x] **Layer 2:** `FormulaBarEditingTests`: the Cell Editor names the cell it stands over; a
      press taken on the editor lands on that cell once the edit has ended; one taken under another
      order lands on no cell.
- [x] **The F4 spec drives the natural gesture:** answer 2 (c) types `=A1+B2` into D10, Enter, and
      presses D10 at once, as the run did, where it had used D2's committed Formula to dodge this
      race.

## Comments

2026-10-16, claude/settled-races. Reproduced, root-caused and fixed in ExGrid's core. Not the same
cause as ticket 200: both are a gesture on the Server host meeting an element a round trip stale,
but here the press reached an element the hold did not look at, and there the hold stood behind
nothing.
