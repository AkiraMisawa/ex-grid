# 89: A text section may carry a colour: `0;[Red]@`

Status: done

**What to build:** an Excel fidelity gap ticket 48 found. The engine refuses `0;[Red]@`, but Excel took it in
the eleventh run's case 3b (`verification/2026-10-01-windows-excel-11/cell-format.md`, case 3b). The record
reads that `@` belongs in the fourth section, and that a `[Red]` there colours text.

**Blocked by:** None (can start immediately)

- [x] **Read the case's record exactly:** what Excel stored for `0;[Red]@`, and what each value showed in
      which colour.
- [x] **The engine reads the code as Excel does.** It shows what Excel showed, in the colour Excel
      showed, for a number, a negative number, zero and text. Where the record does not say, build
      the reading and name it in the comment.
- [x] **Case 3b on the DemoHost** (`/sheet?case=3b`) includes the cell it now leaves out.
- [x] **Layer 1** tests named after ADR-0047, ADR-0071 and case 3b.

## Comments

2026-10-01, agent cf-88.

### What the record says

`verification/2026-10-01-windows-excel-11/cell-format.md`, case 3b, and line 5 of `cell-format.jsonl`:
- Excel took `0;[Red]@` on A4. COM read it back as written: `NumberFormat` and `NumberFormatLocal` are
  both `0;[Red]@`.
- A4 held 5. Excel showed `5`, in `#000000`.
- That is all the case observed of this code. No negative number, zero or text was entered in it.

The ticket's "`@` belongs in the fourth section" is not the record's. It is the engine's own refusal
("@ belongs in the fourth section, or in a format of one section."), which ticket 48 quoted.

### What was built

`NumberFormat` (`src/ExSheet.Engine/NumberFormat.cs`):
- **A text section may end a format of two or three sections**, as it may end a format of one or
  four. `@` in any other section is still refused, now as "@ belongs in the last section."
- **Such a section is the format's text section and shows no number.** The sections before it are
  read as a format of that many sections. So `0;[Red]@` shows every number by `0`, and
  `0.0;[Magenta]-0.0;[Red]@` shows a negative by its second section and zero by its first.

### Readings, built and not observed

Only 5 in `0;[Red]@` was observed. The rest is read, and is what Excel's two-section rule and its
text section give together:
- -5 shows `-5`, with the automatic minus sign of a format whose numbers have one section, in no
  colour. 0 shows `0`, in no colour.
- Text shows through the ending section, in its colour: `abc` in `0;[Red]@` is red.
- In three sections ending in `@`, zero is the first section's, as in a format of two.
- Booleans and Error Values still show as themselves, in no colour (as `TRUE` and `#DIV/0!` did in
  `[Red]0` in the same case).
- `@;0` and `0;@;0` stay refused: no run has seen `@` before the last section.

### DemoHost

`/sheet?case=3b` now holds A4 = 5 in `0;[Red]@` (`SheetCases`).

### Tests

- **Layer 1.** `NumberFormatColourTests` gains 10 cases named after ADR-0047, ADR-0071 and case 3b:
  case 3b's own cell, the numbers and the text read beside it, and booleans and errors.
  `NumberFormatTests` gains three accepted codes and three refused ones. Each new accepted case failed
  before the fix, with the refusal (13 failures).
- **Layer 2.** `SheetAppearanceTests.No_section_that_shows_the_value_means_no_colour` now holds case
  3b's A4: `5`, with no Font class.
- ExSheet.Engine.Tests 2333 and ExSheet.Components.Tests 560 passed.
