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
- [x] For the next Windows run: Excel's answer for each refused form, and what an insertion does to
      the text (INDIRECT-023 to INDIRECT-029); and the other `uncertain` cases of
      `ExcelCases/indirect.json`: the Sheet's own qualifier, lower case and `$`, whole columns, a range
      that spills, empty text, a space before the reference, a leading `=`, and two ranges with a space
      between them. *(The seventeenth run, 2026-10-11: every case agreed but the refused forms, which
      Excel reads; they are read now, below.)*
- [x] The forms the seventeenth run observed are read as Excel reads them: R1C1 text naming an
      absolute cell (`a1` FALSE, 0, a blank or left empty) and a range of two such cells; a structured
      reference's text as the typed `Table[Column]` is read, waiting with it; `A1#` as the typed `A1#`
      is read. Relative R1C1 text and R1C1 text naming whole rows or columns stay `#VALUE!` *(until
      the area pass observed them, below)*
- [x] For the next Windows run: INDIRECT-030 to INDIRECT-043, the forms read or refused by
      generalising what the seventeenth run observed (an R1C1 range, lower case, the qualifier, `a1`
      left empty or blank, A1 text read as R1C1, relative and whole-row and whole-column R1C1 text, a
      structured reference in double brackets or naming no table or column, `A1#` text naming a cell
      that does not spill) *(The area pass, 2026-10-12: every case agreed but the four refused, which
      Excel reads; R1C1 text is read now, below, and a Linked Table's bare name stays refused.)*
- [x] R1C1 text is read in R1C1 notation's ordinary meaning, as the area pass observed (INDIRECT-036
      to 038): R and C each take a number, an offset in brackets from the Formula's own cell, or
      nothing; a row part alone is a whole row and a column part alone a whole column; two parts of
      the same kind joined by `:` name the rectangle they span; a number past the Sheet's edge is
      `#REF!`, as `XFE1` is, and an offset past it `#REF!` too, the engine's answer until INDIRECT-052
      reads Excel's. A number written with a leading 0 or a `+`, a cell joined to a whole row or
      column or a row to a column, structured or `#` text in R1C1 style and a declared Linked Table's
      bare name stay `#VALUE!`
- [ ] For the next Windows run: INDIRECT-044 to INDIRECT-063, the R1C1 forms read by generalising
      what the area pass observed (an offset on either axis, a whole row or column by offset, rows and
      columns joined, a rectangle of two relative cells, an offset past the Sheet's edge), what a
      declared Linked Table's bare name reads (its columns, rows, first cell, count and spill), and the
      R1C1 forms still refused (a cell joined to a whole row, a row joined to a column, a leading 0, a
      `+`, `#` and structured text in R1C1 style)

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
waiting cell met through a computed Reference; and after each layout of the spills that changes
something it computes the volatile Formulas once more, since what `INDIRECT` or `OFFSET` computes a
Reference to is no Dependent of the cells the layout changed — before, `INDIRECT("A3")` kept a spilled
cell's old Value when the spill was blocked (`IndirectTests`). A change that blocks or frees one spill
therefore computes each volatile Formula twice, and one that moves no spill once. An Anchor computed again
with an array of the shape its layout blocked stays `#SPILL!`, so a volatile Anchor's second computation
changes nothing and the recalculation ends on a layout that changes nothing; review found the first
version of this branch recomputing such an Anchor up to 64 times and ending on its first Value, and it is
fixed with `SpillTests`' blocked `INDIRECT`, `OFFSET` and `RAND` Anchors. `INDEX` with `column_num` left
out is `#REF!` over a Spill Range of several rows and columns as over a typed range (INDEX-012; INDEX-017
and 018 ask it of `A1#` and `INDIRECT("A1#")`). The forms read or refused by generalising the observed
ones are INDIRECT-030 to INDIRECT-042, `uncertain` for the next run.

2026-10-12, the second review's refusal carried over the seventeenth run's corrections: a declared Linked
Table's bare name (`INDIRECT("Positions")`) is `#VALUE!`, not `#REF!`, since Excel reads it as a
structured reference to the table's data and no run has observed it (ADR-0124's note;
`ADR0124_a_linked_tables_bare_name_is_value_and_any_other_name_ref`). INDIRECT-043 asks it; it was
INDIRECT-030 on the review's branch, a number the run's corrections took meanwhile.

2026-10-12, after the area pass (`verification/2026-10-12-windows-area/report.md`, "Recorded (9)"):
Excel answered the three R1C1 forms the seventeenth run's note kept refused, each with the notation's
ordinary meaning. `INDIRECT("R[1]C[-1]",FALSE)` in C2 is 45, B3's Value: a bracketed offset counts from
the Formula's own cell. `SUM(INDIRECT("R3",FALSE))` is 45, row 3, and `SUM(INDIRECT("C2",FALSE))` is
118.333, column B. INDIRECT-036 to 038 are `observed`, and R1C1 text is read by the rule they settle,
generalised as the seventeenth run's R3C2 was to ranges, case and qualifiers: R and C each with a
number, an offset in brackets (from `Evaluator.Self`, the Formula's own cell) or nothing; a row part
alone a whole row, a column part alone a whole column; two parts of the same kind joined by `:` the
rectangle they span; an offset past the Sheet's edge `#REF!`. INDIRECT is volatile, so a Formula moved
by an insertion counts from its new cell at once (`IndirectTests`). A cell joined to a whole row or
column (`R2C2:R3`), or a row to a column (`R3:C2`), stays `#VALUE!` beside a leading 0, a `+` and
structured or `#` text in R1C1 style: no run has observed one, and INDIRECT-058 to 063 ask them.
INDIRECT-044 to 052 put the generalised forms to the next run, the offset past the edge among them: its
`#REF!` is the engine's answer, since Excel might wrap it round to the last rows.
`SUM(INDIRECT("Positions"))` is 250.5 in Excel (INDIRECT-043, now `observed` with Excel's answer beside the
refusal, as SORT-011 is). Over a one-row table whose other column is text, that does not say whether the
bare name reads the table's data or one column, and ExSheet has no typed bare table name to read it as
(typed, `Positions` is `#NAME?`; ADR-0049 reads a Linked Table by column), so the refusal stays.
INDIRECT-053 to 057 ask its columns, rows, first cell, count and spill over a table of two rows; what a
bare name reads, typed and through `INDIRECT`, then wants a decision. `COUNTA` over the refusal counts
it as one Value, as it counts any Error Value (INDIRECT-056 records 1).
