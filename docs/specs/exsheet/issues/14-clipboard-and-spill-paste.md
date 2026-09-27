# 14: The clipboard, and the paste that spills

Status: ready-for-agent

**What to build:** Inside ExSheet, a copy carries Entries and relative References shift on paste. Outward it
carries Values as unformatted text. Inward, each field is parsed as if typed. The third ADR-0050
declaration arrives: a paste may spill from a single cell. The pasted block then becomes the
Selection, a spill past the extent is refused by name, and `Editable` is checked on the block
first. A paste is one undo step.

**Blocked by:** 03, 05, 12

- [ ] Copying `=A1` from B1 to B2 pastes `=A2` (ADR-0048)
- [ ] Copying to another program gives the Values (ADR-0005)
- [ ] Pasting `=A1+1` and `1,234` from outside makes a Formula and a number under the culture
- [ ] A 3×3 block pasted onto one cell writes 3×3 and selects it (ADR-0050)
- [ ] Without the declaration, ExGrid still refuses range → one cell (ADR-0014)
- [ ] A spill past the edge is refused by name

## Comments

The core half, 2026-09-27: the third ADR-0050 declaration (DC-8, DC-9, DC-10).

- **`ExGrid.PasteMaySpill`** (`bool`, off by default). Declared, a block of several cells
  pasted onto one cell raises one `OnPaste` intent of the existing shape, whose plan is the
  block with that cell at its top-left. The block then becomes the Selection, with the Anchor
  and the Focus at its top-left. If handling the intent moved the Row Sequence Version, the
  block is not placed (ADR-0011).
- **`ClipboardRules.PlanPaste(..., GridExtent? spillWithin = null)`** is the pure half.
  `Editable` is judged on the block's columns first, then a block crossing the last row or
  column is refused as the new **`PasteRefusalReason.SpillPastExtent`**. It is never clipped.
  "May not" comes before "cannot", in ADR-0035's order. Every other ADR-0014 rule stands, and
  without the declaration range → one cell is still `SingleCellTarget`.
- No JavaScript was added.

Layer 1: `SpillPasteTests`. Layer 2: four tests at the end of `ClipboardWiringTests`.

What remains, for ExSheet:

- Declaring `PasteMaySpill`, and resolving the intent: Entries inside the Sheet, with relative
  References shifted (ADR-0048), Values outward, and fields parsed as if typed inward. The
  first three checkboxes are ExSheet's.
- Making a paste one undo step (ticket 12).
- ExSheet's wording for `SpillPastExtent`.
- A real clipboard paste that spills, in layer 3.
