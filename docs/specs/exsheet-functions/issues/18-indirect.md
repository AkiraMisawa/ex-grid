# 18: INDIRECT, for A1-style text on the same Sheet

Status: done

**What to build:** `INDIRECT` under
[ADR-0124](../../../adr/0124-volatile-functions-are-recalculated-after-every-change.md)'s note of
2026-10-10.

**Blocked by:** none.

- [x] `INDIRECT(text)` and `INDIRECT(text, TRUE)` read A1-style text naming a cell or a range of
      the same Sheet, with or without its own qualifier. A range is read as `OFFSET`'s is, and
      spills where an array is the answer
      ([ADR-0125](../../../adr/0125-a-formula-whose-result-is-an-array-spills-as-excel-365s-does.md))
- [x] Volatile (SH-59): in the volatile set; the place it names is read as it stands after the
      recalculation; a Formula that reaches itself through it is `#CIRC!`
- [x] Its text is not rewritten by an insertion or a deletion, while a Reference beside it is
- [x] Refusals (SH-68): `#VALUE!` for R1C1 text (`a1` FALSE), a structured reference (a declared
      Linked Table's bare name included) and `A1#`; `#REF!` for text that names no place, another
      Sheet's name included
- [x] The corpus cases. The catalogue row Supported, and the Docs Site's Formulas page says what is
      refused
- [ ] For the next Windows run: Excel's answer for each refused form, and what an insertion does to
      the text (INDIRECT-023 to INDIRECT-030); and the other `uncertain` cases of
      `ExcelCases/indirect.json`: the Sheet's own qualifier, lower case and `$`, whole columns, a range
      that spills, empty text, a space before the reference, a leading `=`, and two ranges with a space
      between them

## Comments

2026-10-10: written with the user's decision, ADR-0124's note of 2026-10-10.

2026-10-11: built. `INDIRECT` reads its text through the Formula grammar's own Reference reader, so it
takes exactly the Reference forms a Formula takes: the text must be a whole A1-style Reference to this
Sheet, or it names no place. It joined `NOW` and `OFFSET` in the volatile set. Two gaps the volatile set
already had are closed with it, because `INDIRECT` makes them visible: an insertion, a deletion or a
rename that rewrote no Formula recalculated nothing, so a volatile Formula kept the Value its text named
before (ADR-0124 says every change); and a structural edit left the volatile and `TODAY` sets naming the
cells' old addresses. `IndirectTests` pins both, and the corpus holds Microsoft's example as `documented`
and the open forms as `uncertain`. ADR-0047's dated addition naming the function is left to the
orchestrator.

2026-10-11, after review: a Formula that reaches itself through `INDIRECT` or `OFFSET` is `#CIRC!`
whatever reads it, and one that reaches a waiting cell waits. The recalculation now records what each
Formula's reads met, a cell still being computed or one in a cycle, or a waiting one, and taints the
Formula as `Taint` taints a Reference its text names; before, `COUNT`, `COUNTA`, `SUBTOTAL` and
`AGGREGATE`'s error-skipping options answered a number. `IndirectTests` holds each form the review
found.

2026-10-11: the Status is `done`. The one open box is the sign-off's Windows run, and each of its
questions is a corpus case: INDIRECT-023 (`documented`, Excel's 45 beside the engine's refusal) and the
`uncertain` cases of `indirect.json`, INDIRECT-009 to INDIRECT-022 and INDIRECT-024 to INDIRECT-029.

2026-10-11, after the second review: INDIRECT-023's `source` is `uncertain`, since its `expect` is the
engine's refusal, not Microsoft's documented 45. A declared Linked Table's bare name
(`INDIRECT("Positions")`) is `#VALUE!`, not `#REF!`: Excel reads it as a structured reference to the
table's data, a form ADR-0124's note refuses until a run has observed it
(`ADR0124_a_linked_tables_bare_name_is_value_and_any_other_name_ref`). INDIRECT-030 asks it, so the
sign-off's Windows run puts INDIRECT-009 to INDIRECT-030 to Excel.
