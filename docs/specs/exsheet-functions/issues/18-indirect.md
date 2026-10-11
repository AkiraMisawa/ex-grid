# 18: INDIRECT, for A1-style text on the same Sheet

Status: ready-for-agent

**What to build:** `INDIRECT` under
[ADR-0124](../../../adr/0124-volatile-functions-are-recalculated-after-every-change.md)'s note of
2026-10-10.

**Blocked by:** none.

- [ ] `INDIRECT(text)` and `INDIRECT(text, TRUE)` read A1-style text naming a cell or a range of
      the same Sheet, with or without its own qualifier. A range is read as `OFFSET`'s is, and
      spills where an array is the answer
      ([ADR-0125](../../../adr/0125-a-formula-whose-result-is-an-array-spills-as-excel-365s-does.md))
- [ ] Volatile (SH-59): in the volatile set; the place it names is read as it stands after the
      recalculation; a Formula that reaches itself through it is `#CIRC!`
- [ ] Its text is not rewritten by an insertion or a deletion, while a Reference beside it is
- [ ] Refusals (SH-68): `#VALUE!` for R1C1 text (`a1` FALSE), a structured reference and `A1#`;
      `#REF!` for text that names no place, another Sheet's name included
- [ ] The corpus cases. The catalogue row Supported, and the Docs Site's Formulas page says what is
      refused
- [ ] For the next Windows run: Excel's answer for each refused form, and what an insertion does to
      the text

## Comments

2026-10-10: written with the user's decision, ADR-0124's note of 2026-10-10.
