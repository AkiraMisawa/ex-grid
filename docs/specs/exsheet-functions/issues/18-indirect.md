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
- [x] Refusals (SH-68): `#VALUE!` for R1C1 text (`a1` FALSE), a structured reference and `A1#`;
      `#REF!` for text that names no place, another Sheet's name included
- [x] The corpus cases. The catalogue row Supported, and the Docs Site's Formulas page says what is
      refused
- [x] For the next Windows run: Excel's answer for each refused form, and what an insertion does to
      the text (INDIRECT-023 to INDIRECT-029); and the other `uncertain` cases of
      `ExcelCases/indirect.json`: the Sheet's own qualifier, lower case and `$`, whole columns, a range
      that spills, empty text, a space before the reference, a leading `=`, and two ranges with a space
      between them. *(The seventeenth run, 2026-10-11: every case agreed but the refused forms, which
      Excel reads; they are read now, below.)*
- [x] The forms the seventeenth run observed are read as Excel reads them: R1C1 text naming an
      absolute cell (`a1` FALSE, 0, a blank or left empty) and a range of two such cells; a structured
      reference's text as the typed `Table[Column]` is read, waiting with it; `A1#` as the typed `A1#`
      is read. Relative R1C1 text and R1C1 text naming whole rows or columns stay `#VALUE!`
- [ ] For the next Windows run: INDIRECT-030 to INDIRECT-042, the forms read or refused by
      generalising what the seventeenth run observed (an R1C1 range, lower case, the qualifier, `a1`
      left empty or blank, A1 text read as R1C1, relative and whole-row and whole-column R1C1 text, a
      structured reference in double brackets or naming no table or column, `A1#` text naming a cell
      that does not spill)

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

2026-10-12: the seventeenth Windows run (`verification/2026-10-11-windows-17/report.md`, Part A) answered
the open box. INDIRECT-009 to INDIRECT-022 and INDIRECT-027 to INDIRECT-029 agreed and are `observed`.
The four refused forms disagreed or were recorded with Excel's answer: `INDIRECT("R3C2",FALSE)` and
`INDIRECT("R3C2",0)` are 45, `INDIRECT("Positions[PV]")` is 250.5 and `SUM(INDIRECT("A1#"))` is 6. Under
ADR-0124's note (refused until a run observes the form) they are read now, each through what a typed
Formula already does: R1C1 text naming an absolute cell or a range of two such cells is the Reference it
names; structured text goes through the Linked Table's column as `Table[Column]` does, so a table still
waiting for its data waits, even under `IFERROR`, and one never declared, or a column it lacks, names no
place (`#REF!`); `A1#` text reads the Anchor's Spill Range as `A1#` does, `#REF!` where it does not spill.
Two gaps came with it. The recalculation now marks a table met through text as waiting, as it marks a
waiting cell met through a computed Reference; and after a spill's layout changes it reads the volatile
Formulas again, since what `INDIRECT` or `OFFSET` computes a Reference to is no Dependent of the cells
the layout changed — before, `INDIRECT("A3")` kept a spilled cell's old Value when the spill was blocked
(`IndirectTests`). The forms read or refused by generalising the observed ones are INDIRECT-030 to
INDIRECT-042, `uncertain` for the next run.
