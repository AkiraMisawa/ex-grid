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

2026-09-27, engine half: `Sheet.Copy(range)` returns a `SheetCopy` holding the Entries as a
`SheetBlock` (Entry, number format and alignment per cell, and the place they came from) and
the Values in the two flavours ExGrid puts on the clipboard: `Text`, each Value as the cell
shows it, TSV with Excel's quoting and CR LF lines, and `Html`, a table of the unformatted,
invariant Values; a Value its format cannot show goes out as itself, never as `####`. A range
holding a `#GETTING_DATA` Value is refused whole with `SheetRefusalReason.WaitingForData`
(ADR-0049; tested with ticket 16). `SheetEdit.Paste(block, origin)` writes the block with every
relative Reference shifted by the distance pasted, absolute parts unmoved, a range written from
its top-left, and a Reference shifted off the Sheet written `#REF!`; formats and alignment go
with it, and a blank cell of the block clears what it lands on. `SheetEdit.Paste(block,
target)` repeats a block over a whole multiple of itself. `SheetEdit.PasteText(fields, origin)`
takes each field as if typed under the Sheet's culture (`=A1+1` a Formula, `1,234` a number
under `en-US`, a date with its implied format). A block that would run past the Sheet's edge is
refused by name (`BlockWouldLeaveSheet`), and each paste is one `SheetStep`
(`ClipboardTests`). Covers the engine side of the first three criteria and the sixth. **What
remains is the component's:** the clipboard itself and telling its own copy from another
program's, the spill declaration and the pasted block becoming the Selection, `Editable` on the
block, ExGrid's unchanged refusal without the declaration, and layer 3 with the real
clipboard. A pasted field beginning with `=` that cannot be read refuses the whole paste
(`FormulaSyntaxException`); what Excel does with such a field is not pinned, reported for a
decision.
