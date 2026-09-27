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
