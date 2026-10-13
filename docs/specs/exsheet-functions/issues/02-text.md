# 02: TEXT, as ADR-0120 decides it

Status: done

**What to build:** `TEXT(value, format_text)` under ADR-0120. The code is read in the invariant
spelling under every culture, and as written, never as the built-in it spells. The Value is shown as
a cell in that format would show it, under the Sheet's culture. Width plays no part, and the colour
is dropped.

**Blocked by:** ADR-0120 (decided with the user, 2026-10-03)

- [x] `TEXT` reuses the cell formats' renderer; `NumberFormat.TryParseAsWritten` reads the code
- [x] Microsoft's examples are `documented` cases in `ExcelCases/text.json`; the culture cases
      (ja-JP, de-DE) and the refusals are `uncertain`
- [x] German Excel's `"JJJJ"` is recorded as a difference by decision (ADR-0120)
- [x] The catalogue row is Supported; ADR-0047's additions point to ADR-0120; the README lists it
- [x] The next Windows run asks the `uncertain` cases, General's width first

## Comments

2026-10-03: admitted. 22 cases, every one passing. Refused until Excel answers: `General` with a
number longer than 11 characters (TEXTFN-017) and an empty code (TEXTFN-018). Refused because
`NumberFormat` refuses them for a cell: fractions, conditions and the rest outside its subset.

2026-10-12: the seventeenth Windows run answered TEXTFN-017 (`0.333333333`, General fitted to 11 characters)
and TEXTFN-018 (empty text). Fractions and conditions stay refused with Excel's answers recorded, and
TEXTFN-009 records German Excel's (ADR-0120's notes).

2026-10-12, the area pass (`verification/2026-10-12-windows-area/`): Excel answered TEXTFN-025
(`TEXT(-1/3,"General")`, `-0.333333333`) and TEXTFN-028 (`TEXT(1/3,"@")`, `0.333333333`), and both follow it.
`General` is General at its own limit, 11 characters with the minus sign beside them, and a code of a text section
alone shows a number as that General does, not as General in full. TEXTFN-029 to 034 are `uncertain` for the next
pass (ADR-0120's note).

2026-10-12, the second area pass (`verification/2026-10-12-windows-area-2/`): Excel answered TEXTFN-029 to 034 as
the engine does, a negative number of eleven and of twelve digits in `General`, and a negative number, twelve digits,
a small number and a literal under a text section alone. Every case of `text.json` is `observed`, so the last box is
ticked. TEXTFN-009, 010, 019 and 020 stay differences by decision, with Excel's answers recorded (ADR-0120).
