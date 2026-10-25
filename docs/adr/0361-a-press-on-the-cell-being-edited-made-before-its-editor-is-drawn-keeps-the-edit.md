# A press on the cell being edited, made before its editor is drawn, keeps the edit

*Found 2026-10-25 by the differential test of
[ADR-0240](0240-input-to-one-product-instance-is-taken-in-one-order.md) §8, with a seed of its own
(987654321, the Sheet, Server host, 150 ms round trip). Proposed to the user the same day.*

## Context

ADR-0010's click-away rule: a press that is not on the Cell Editor commits the edit, and then keeps
its own meaning. The editor's own elements never let a press through to the rows, so the rule needs
no test of where a press was made. Any press that reaches the rows is one made elsewhere.

That stops holding when the press follows the key that opened the edit at once. The key opens the edit
in its turn, but on a circuit the editor is drawn a round trip later. A press made in between lands on
the cell, under the place where the editor is about to stand. The core then takes it as a press away
from the edit, and commits.

Typed slowly, the same press lands in the editor. The edit goes on in its mode, and the browser moves
the caret to where the press was made. The test typed `12` on E2 and pressed E2 at once:

- Fast, `12` was committed, and the next text typed replaced it.
- Slow, the editor still held `12`.

## Decision

- **A primary press that reaches the rows on the cell whose Cell Editor stands is a press into the
  editor.** It keeps the edit: nothing is committed, the Selection does not move, no drag starts, and
  the edit stays in its mode. An Interactive cell it leaves, it still leaves. A press on any other
  cell commits as before. The same applies to the Row Headings and to the empty space past the last
  column or below the last row. A drag clamps those onto a cell, possibly the edited one, but a press
  there landed on no cell.
- **Not a press that closes a popover.** Such a press hands the keyboard back to the rows once the
  popover has gone (ADR-0039), so it commits as before.
- **Only the primary button.** A secondary press on the edited cell made before the editor is drawn
  still commits, and then opens the context menu (ADR-0036). Made slowly, it lands in the editor. That
  case is left open, with what the context menu should do over an open edit.
- **The caret stays where typing left it.** Made slowly, the press puts the caret where it was made.
  Where it was made is a position in text the user could not yet see, because the editor was not drawn.
  The press was aimed at the cell, not at a character. Placing the caret by the press's offset would
  also mean measuring the text, which ADR-0021 keeps out of the grid.
- **Only the Cell Editor.** An edit in the Formula Bar has no editor over the cell, so a press on the
  cell is a press away from the bar's edit, and it commits as ADR-0051 says. A press while pointing is
  still a pointing press (ADR-0051), decided before this one.

## Considered and rejected

- **Refusing the press aloud.** Safe, but the press then does nothing, and made slowly it does
  something: the edit continues.
- **Keeping the commit.** The outcome would depend on the round trip, which the spine's sixth rule
  forbids, and the differential test would stay red for this seed.

## Consequences

- ADR-0010's click-away rule gains this exception for the cell being edited.
- Seed 987654321 joins the differential test's fixed seeds. `EditedCellPressTests` (layer 2) and
  `input-text-order.spec.mjs` (layer 3, Server host, 150 ms) test the press.
- Nothing changes in the script.
