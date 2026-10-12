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
- [x] R1C1 text is read in the three forms the area pass observed (INDIRECT-036 to 038: an offset in
      brackets, a row part alone, a column part alone) and in those that follow from them by the rule
      they settle: R and C each take a number or an offset in brackets from the Formula's own cell; a
      row part alone is a whole row and a column part alone a whole column; two parts of the same kind
      joined by `:` name the rectangle they span; a number past the Sheet's edge is `#REF!`, as `XFE1`
      is. `#VALUE!` for the forms the observed ones leave open: an R or a C with no number, a zero
      offset in brackets, a number written with a leading 0, an offset written with a `+`, an offset
      past the Sheet's edge (which Excel might call no place or wrap round), a cell joined to a whole
      row or column or a row to a column, and structured or `#` text in R1C1 style; and for a declared
      Linked Table's bare name
- [x] For the next Windows run: INDIRECT-044 to INDIRECT-066, the R1C1 forms read by generalising
      what the area pass observed (INDIRECT-045 to 050: an offset on either axis, an absolute row with
      a relative column, a whole row or column by offset, rows and columns joined), what a declared
      Linked Table's bare name reads (INDIRECT-053 to 057: its columns, rows, first cell, count and
      spill), and the R1C1 forms refused (INDIRECT-044, 051, 052 and 058 to 066: an R or a C with no
      number, a zero offset in brackets, an offset past the Sheet's edge, a cell joined to a whole row,
      a row joined to a column, a leading 0, a `+`, `#` and structured text in R1C1 style) *(The second
      area pass, 2026-10-12: INDIRECT-045 to 050 agreed; Excel read every refused form. Each R1C1 form
      but the offset past the edge is read now, below; a declared Linked Table's bare name stays
      refused, by decision.)*
- [x] What the second area pass read is followed by the rule it shows (INDIRECT-044, 051, 058 to
      066): an R or a C with no number is the Formula's own row or column; an offset in brackets may be
      0, `-0` or signed with a `+`; a number or an offset is read with its leading 0s; parts of two
      kinds joined by `:` name no place, `#REF!`; one cell followed by `#` is its Spill Range, as `A1#`
      is; a structured reference's text is read alike whatever `a1` says. `#VALUE!` for what the
      answers leave open: an offset past the Sheet's edge, which Excel reads as a place (INDIRECT-052 is
      0, a blank cell) without telling whether it wraps round or stops at the edge; a row or column
      number 0; and a `#` after a range, a row or a column, which no run has observed in either style.
      A row or column number past the Sheet's edge stays the engine's `#REF!`, as `XFE1` is in A1
      style, which no run has observed in R1C1 style
- [x] A declared Linked Table's bare name stays `#VALUE!`, in either style, by decision (decided with
      the user, 2026-10-12; ADR-0049's rule 2 and ADR-0124): Excel reads it as the table's data
      (INDIRECT-043, 053 to 057, `observed`, with Excel's answers as `excelExpect`), which a Formula
      could then read by position, and a Linked Table is read by key, never by position. It is a Known
      limitation of `0.1.0`. Typed, `Positions` stays `#NAME?`, and `Table[Column]` is unchanged
- [ ] For the next Windows run: INDIRECT-067 to INDIRECT-084 and INDIRECT-090 to INDIRECT-092. The
      forms read by generalising what the second area pass observed (INDIRECT-067 to 072: an R or a C
      with no number beside a number or second in a rectangle, a zero offset on the column, a leading
      0 in an offset or a column number; 075 to 077: parts of two kinds joined in the other orders;
      078, 079: `RC[-1]#` and `R1C1#` over no spill; 080, 081: structured text in R1C1 style); the
      engine's answers no run has observed (074: a row number past the edge in R1C1 style, `#REF!`;
      092: `A1:A2#` in A1 style, `#REF!`); and the forms still refused (073: a row number 0; 082 to
      084: an offset past the top, left and bottom edges, with values in the cells that wrapping round
      and stopping at the edge would each read; 090, 091: `#` after an R1C1 range and a whole column,
      with values that tell no place, a Spill Range and the cells themselves apart)

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
118.333, column B. INDIRECT-036 to 038 are `observed`, and R1C1 text is read in the forms that follow
from them by the rule they settle, generalised as the seventeenth run's R3C2 was to ranges, case and
qualifiers: R and C each with a number or an offset in brackets (from `Evaluator.Self`, the Formula's
own cell); a row part alone a whole row, a column part alone a whole column; two parts of the same kind
joined by `:` the rectangle they span; a number past the Sheet's edge `#REF!`, as `XFE1` is
(INDIRECT-017), whatever an offset beside it would read. INDIRECT is volatile, so a Formula moved by an
insertion counts from its new cell at once (`IndirectTests`). INDIRECT-045 to 050 put those forms to the
next run. What the observed forms leave open stays `#VALUE!`, and a case asks each: an R or a C with no
number (`RC[-1]`, `R[2]C`), which could be the Formula's own row or column or every one (INDIRECT-044,
051, 066); a zero offset in brackets, `R[0]` or `R[-0]` (INDIRECT-064, 065); an offset past the Sheet's
edge, which Excel might call no place or wrap round to the last rows (INDIRECT-052); a cell joined to a
whole row or column (`R2C2:R3`) or a row to a column (`R3:C2`); a leading 0, a `+`, and structured or `#`
text in R1C1 style (INDIRECT-058 to 063). The first version of this branch read an R or a C with no
number and a zero offset as an offset of 0, and called an offset past the edge `#REF!`; review found
that the observed forms settle none of them, so they are refused.
`SUM(INDIRECT("Positions"))` is 250.5 in Excel (INDIRECT-043, now `observed` with Excel's answer beside the
refusal, as SORT-011 is). Over a one-row table whose other column is text, that does not say whether the
bare name reads the table's data or one column, and ExSheet has no typed bare table name to read it as
(typed, `Positions` is `#NAME?`; ADR-0049 reads a Linked Table by column), so the refusal stays.
INDIRECT-053 to 057 ask its columns, rows, first cell, count and spill over a table of two rows; what a
bare name reads, typed and through `INDIRECT`, then wants a decision. `COUNTA` over the refusal counts
it as one Value, as it counts any Error Value (INDIRECT-056 records 1).

2026-10-12, after the second area pass (`verification/2026-10-12-windows-area-2/report.md`, "Recorded
(31)"; `results-2026-10-12-area-2-com-en-US.json`): INDIRECT-045 to 050 agreed and are `observed`, and
Excel answered every form the first pass's corrections refused. Each R1C1 answer is the notation's
ordinary meaning, and each is followed by the rule it shows. In C2, `RC[-1]` is 1.333 and
`RC[-1]:R[1]C[-1]` sums to 46.333, and in B1, `R[2]C` is 45: an R or a C with no number is the Formula's
own row or column, an offset of 0, not every row or column (INDIRECT-044, 051, 066). `R[0]C[-1]` and
`R[-0]C[-1]` are 1.333 and `R[+1]C[-1]` is 45: an offset is a signed number, 0 included (INDIRECT-063 to
065). `R03C2` is 45: a number is read with its leading 0s (INDIRECT-059), and so is an offset. `R2C2:R3`
and `R3:C2` are `#REF!`: parts of two kinds joined name no place, whichever comes first (INDIRECT-058,
062). `R1C1#` is the Spill Range, as `A1#` is (INDIRECT-060). `Positions[PV]` in R1C1 style is the column,
as in A1 style (INDIRECT-061). So `R` alone is the Formula's whole row, `C` its whole column and `RC` its
cell: each reaches the Formula, `#CIRC!`.
A `#` after a range, a row or a column was observed in neither style. The first version of this round
read it as `#REF!`, "as after a range in A1 style"; review found that no run has asked `A1:A2#` either,
and that the readings part (no place, the Spill Range of a cell the text names, or the cells
themselves). In R1C1 style it is `#VALUE!`, as before, and INDIRECT-090 and 091 ask it; `A1:A2#` stays
the engine's `#REF!`, text that goes on past a Reference (INDIRECT-021's rule), and INDIRECT-092 asks
it. A row or column number past the Sheet's edge in R1C1 style is the engine's `#REF!` too, as `XFE1` is
in A1 style (INDIRECT-017); no run has observed it, and INDIRECT-074 asks it.
Over table Positions of two rows, `COLUMNS(INDIRECT("Positions"))` is 2, `ROWS` 2, `INDEX(…,1,1)` R-1,
`COUNTA` 4, and the name as the result spills the data (INDIRECT-053 to 057): Excel reads the bare name
as the table's data, every column in its order and every row without the header, which also gives
INDIRECT-043's 250.5. The first version of this round read it so; it stays refused, decided with the
user (below).
`R[-5]C[-1]` from C2 is 0 in Excel, a blank cell, so an offset past the edge names a place, not `#REF!`;
but B1 and B1048573 are both blank there, and wrapping round and stopping at the edge both give 0. It
stays `#VALUE!` (INDIRECT-052 records Excel's 0 as `excelExpect`), and INDIRECT-082 to 084 put 7 and 9
in the two cells each reading would name, on the top, left and bottom edges. A row or column number 0
is refused too: no run has asked one (INDIRECT-073). INDIRECT-067 to 084 and 090 to 092 put the forms
that follow, the engine's answers and the forms still refused to the next run. Every observed
`INDIRECT` reading in the results files (168, of INDIRECT-001 to 066) agrees with the corpus's `expect`,
or its `excelExpect` where the engine refuses by decision.

2026-10-12, decided with the user: a declared Linked Table's bare name through `INDIRECT` stays
`#VALUE!`, in either style. Excel's reading gives a Formula the table by position —
`INDEX(INDIRECT("Positions"),1,1)` is the first row's first Value, R-1 (INDIRECT-055) — and ADR-0049's
rule 2 reads a Linked Table by key, never by position. INDIRECT-043 and 053 to 057 are `observed`, keep the refusal as `expect` and hold
Excel's answers as `excelExpect`, a difference by decision citing ADR-0049's rule 2 and ADR-0124; the
refusal is listed under `0.1.0`'s Known limitations. The reading (56b86a97) is reverted, and the cases
that asked what followed from it (085 to 089) are withdrawn; their numbers are not reused. Typed,
`Positions` stays `#NAME?`; `Table[Column]`, typed or as text, is unchanged.
