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
- [ ] The next Windows run asks the `uncertain` cases, General's width first

## Comments

2026-10-03: admitted. 22 cases, every one passing. Refused until Excel answers: `General` with a
number longer than 11 characters (TEXTFN-017) and an empty code (TEXTFN-018). Refused because
`NumberFormat` refuses them for a cell: fractions, conditions and the rest outside its subset.
