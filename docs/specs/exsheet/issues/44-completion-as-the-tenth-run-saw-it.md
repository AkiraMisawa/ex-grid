# 44: Completion as the tenth Windows run saw it

Status: done

**What to build:** ADR-0058, "What the tenth Windows run settled", and ADR-0051's correction of the
same day. The tenth run (`verification/2026-09-30-windows-excel-10/excel-only.md`, group 1)
contradicted the readings ticket 39 was built on.

**Blocked by:** None (ticket 39 is merged). Its script change shares `ex-grid.js` with ticket 35;
start after 35 is merged, or keep the change to the gate and name it.

- [x] A value typed whole lists that value alone, selected (`0` → `0 - Exact match`). Any other text
      at a value-list argument lists every value, the first selected (`-` → all five, `0` selected)
      (SH-36)
- [x] Tab closes the list, after a value, a column or a table's name, and the grid does not ask
      again for a list on the text Tab wrote (today it reopens on the accepted name) (SH-36)
- [x] → at an open value list, where the caret stands at a Reference's place, points and closes the
      list: `=XLOOKUP(1,A2:A4,B2:B4,,` then → writes `E10`, shown selected. In a list of names, ← and →
      still move the caret. The key listener's gate learns that a list is open over Point; no new
      listener, no layout read (ADR-0021) (SH-36)
- [x] ↑, ↓, Tab and Escape at an open list are unchanged (SH-36)
- [x] Layer 1 for the listing rule; Layer 2 for Tab closing and → pointing; Layer 3 on `/sheet` under
      both Chromes for → pointing from an open value list and Tab closing (SH-36)

## Comments

2026-09-30, implementation:

- **The listing rule** (`FormulaEntry.Complete`, the value-list branch). The trigger is ticket 39's:
  the argument holds nothing yet, or the beginning of a value and nothing else. Within it, a value
  typed whole before the caret, with nothing of it after the caret, is listed alone; anything else
  lists every value, and the grid chooses the first. Two readings were taken where the tenth run did
  not look, and are asked of the next Windows run:
  - "Typed whole" is read on the text before the caret. With the caret inside or before a value
    (`,,|1)`, `,,-|1)`), every value is listed, and accepting one replaces the whole value.
  - Text that begins no value (`4`, `A1`, `1+`) still lists nothing. "Any other text lists every
    value" read literally would list after an operator, which ADR-0058's table ("after an
    operator: no list") rules out, and which would take ↓ from Point.
- **Tab closes the list.** Accepting a candidate, by Tab or by a press on it, asks the Consumer about
  the text it wrote as before, but shows the answer's hint alone: the list does not come back on
  `Positions`, `Positions[PV` or `-1`, and the hint (`[match_mode]`, SUM's) still follows. The text
  typed next is listed again, so Backspace back into a name lists as before.
- **→ at an open value list.** A list is open over Point when the Consumer's `PointAt` answers true
  at the caret and the edit is not in Caret. There, `OnCompletionKey` closes the list and leaves ←
  and → to `OnPointKey`, which points as it would without the list. `OnEditingKeyAsync` is not
  changed. The gate learns it twice, as it learns an open list: the list's box carries
  `data-ex-over-point` in the render that paints it, which the listener reads (read, not measured),
  and `setEditing` is told `completionOverPoint`, whose set is `completionKeys` with `ArrowLeft` and
  `ArrowRight`. No listener and no layout read are added (ADR-0021). Home, End and the Shift+arrows
  stay the editor's while any list is open, as before: the ADR speaks of ← and → only.

Layer 1: `CompletionTriggerTests` (the whole value alone, every value otherwise). Layer 2:
`CompletionOverPointTests` (12, ExGrid: Tab and a press close the list and a late answer shows its
hint alone, the mark and the gate mode, ← and → pointing, ↑/↓/Tab/Escape unchanged, Caret and a list
of names not over Point, a → carrying text typed since), `CompletionTriggerWiringTests` (18, the
Sheet: `0`, `-`, Tab after a value, a column and a table's name, → writing `E10` in the third colour
on the pointed look, ←), and `ShippedStylesheetTests` (the gate's new set, the mode and the mark).
Layer 3, `declarations.spec.mjs`, "SH-36 under the builtin/mud Chrome" (three tests per Chrome):
passed headless on macOS with `--project=chrome`, on WebAssembly and on the Server host, with the
completion and Point tests beside them (DC-17, DC-19, DC-28, DC-31) and circuit.spec's "a ← typed as
the completion list is painted": 28 of 28 and 1 of 1 on WebAssembly, 29 of 29 on Server. Edge and
the full run are CI's.
