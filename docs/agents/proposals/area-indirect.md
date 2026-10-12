# The area passes, INDIRECT: what the orchestrator writes

The second area pass of 2026-10-12 (`verification/2026-10-12-windows-area-2/report.md`, "Recorded (31)";
`tests/ExSheet.Engine.Tests/ExcelOracle/results-2026-10-12-area-2-com-en-US.json`) read Excel's answer to every
`INDIRECT` case the first pass's corrections refused by decision (INDIRECT-044, 051 to 066), and INDIRECT-045 to 050
agreed. This file says what is true after this round: the text this area would put into ADR-0049, ADR-0124, ADR-0202,
the Definition of Done, the Docs Site and the seventeenth run's Change Note, none of which a background agent changes,
and, outside the numbered edits, `0.1.0`'s Known limitations line and a case for `linked-tables.json`. The numbered
edits are exact and in whole lines, with the target's own indentation: the old lines, then the new, each in a fence,
each old text found once in its target, applied in the order given (an edit to a file applies to it as the earlier
edits left it). They replace this file's earlier text, whose edits were not applied, and are written against the files
as `claude/area-corrections` (`caa73261`) holds them.

**Decided with the user, 2026-10-12, and applied on this branch:** a declared Linked Table's bare name through
`INDIRECT` stays `#VALUE!`, in either style. Excel's answers (INDIRECT-043, 053 to 057) are recorded as `excelExpect`,
a difference by decision citing ADR-0049's rule 2 (a Linked Table is read by key, never by position) and ADR-0124, and
the refusal is listed under Known limitations. Typed, `Positions` stays `#NAME?`; `Table[Column]` is unchanged. The
reading of the bare name as the table's data (`56b86a97`) is reverted, and every text that said it is read is
rewritten; the R1C1 work stays.

**Decided with the user, 2026-10-12, and applied on this branch:** in R1C1 style, a row or column number past the
Sheet's edge (`R1048577C2`, `R2C16385` and the like) is refused with `#VALUE!` until Excel is asked, and INDIRECT-074
asks it, holding the engine's `#VALUE!`; A1 text past the edge stays `#REF!`, as `XFE1` was observed to be
(INDIRECT-017). It was the engine's `#REF!`, "as `XFE1` is", but Excel reads an offset past the edge as a place
(INDIRECT-052), so a number past it could be a place too.

This branch's own choices, not the user's: INDIRECT-074's fixture puts 7 in B1 and 9 in B1048576, and INDIRECT-095
asks a column, `R2C16385`, with 7 in A2 and 9 in XFD2, so that `#REF!`, a wrap round (7) and a stop at the edge (9)
tell the readings apart in each.

**Corrected after the verifier's review:** R1C1 text with `#` after a range, a row or a column (`R2C2:R3C2#`, `R3#`,
`C2#`) had become `#REF!` "as after a range in A1 style", with no observation in either style. It is `#VALUE!` again,
and INDIRECT-090 to 092 hold the engine's answers. The texts say which forms were observed, which follow by the area's
rule, which are the engine's answer that no run has observed, and which are refused.

**Corrected after the verifier's second review:** `R2C2:R3#`, a `#` after a whole row joined to a cell, was `#REF!`,
because the reader judged the join before the `#`; the readings part (no place, as `R2C2:R3` is, or B2's Spill Range
where B2 spills), so the `#` is judged first and a `#` after any join is `#VALUE!`; INDIRECT-094 asks it. Another
Sheet's name in R1C1 style is read by the rule INDIRECT-008 observed in A1 style, and is now judged first, `#REF!`
whatever follows it (`Sheet2!R3#` was `#VALUE!`); INDIRECT-093 asks it. `indirect.json`'s `about` and the reader's
documentation name the observed instances: text going on past a Reference was observed only as an intersection
(INDIRECT-021), and text that is not R1C1 notation only as A1 text read in R1C1 style (INDIRECT-035); the rest of each
class is the engine's rule.

**Corrected after the verifier's third review:** `A1:A2#`, a `#` after an A1 range, was the engine's `#REF!`, by the
rule that A1 text going on past its Reference names no place; but INDIRECT-092's own description listed readings that
part, and no run has observed any of them, so under ADR-0047's rule it is refused: a `#` after an A1 range, a whole row
or a whole column (`A1:A2#`, `A:A#`, `1:1#`) is `#VALUE!`, as in R1C1 style, and INDIRECT-092 holds it. Another
Sheet's name is judged first in A1 style too, so `Sheet2!A1:A2#` stays `#REF!`. The review also found a reading the
cases left out, the first cell joined to the second's Spill Range, `A1:(A2#)`, which is `#REF!` in INDIRECT-090 and
092, since A2 lies in A1's spill there; INDIRECT-096 and 097 put `=SEQUENCE(2)` in A2 and 1 in A1, where it is 4. The
texts no longer say that a case asks other text that is not R1C1 notation, or other A1 text going on past its
Reference: no case does, and both are `#REF!` by the rule INDIRECT-035 and INDIRECT-021 settle.

On the branch `claude/area2-indirect`, not proposals: the engine (`Functions.Lookup.cs`), `IndirectTests`,
`indirect.json`, the engine's README (the function table's row and the `INDIRECT` paragraph), the function
catalogue's `INDIRECT` row, ticket 18, and the Change Note `area-indirect-r1c1-relative-rows-columns.md` (rewritten).
`area-indirect-linked-table-bare-name.md` is deleted: the bare name is `#VALUE!` as it was before this area, so a
Consumer sees no change.
## What Excel answered, and what the engine does now

| Case | Formula | Excel | Before | Now |
|---|---|---|---|---|
| INDIRECT-044 | in C2, `=INDIRECT("RC[-1]",FALSE)` | 1.333 | `#VALUE!` (by decision) | 1.333, `observed` |
| INDIRECT-051 | in C2, `=SUM(INDIRECT("RC[-1]:R[1]C[-1]",FALSE))` | 46.333 | `#VALUE!` | 46.333, `observed` |
| INDIRECT-066 | in B1, `=INDIRECT("R[2]C",FALSE)` | 45 | `#VALUE!` | 45, `observed` |
| INDIRECT-064 | in C2, `=INDIRECT("R[0]C[-1]",FALSE)` | 1.333 | `#VALUE!` | 1.333, `observed` |
| INDIRECT-065 | in C2, `=INDIRECT("R[-0]C[-1]",FALSE)` | 1.333 | `#VALUE!` | 1.333, `observed` |
| INDIRECT-063 | in C2, `=INDIRECT("R[+1]C[-1]",FALSE)` | 45 | `#VALUE!` | 45, `observed` |
| INDIRECT-059 | `=INDIRECT("R03C2",FALSE)` | 45 | `#VALUE!` | 45, `observed` |
| INDIRECT-058 | `=SUM(INDIRECT("R2C2:R3",FALSE))` | `#REF!` | `#VALUE!` | `#REF!`, `observed` |
| INDIRECT-062 | `=ROWS(INDIRECT("R3:C2",FALSE))` | `#REF!` | `#VALUE!` | `#REF!`, `observed` |
| INDIRECT-060 | `=SUM(INDIRECT("R1C1#",FALSE))` over A1 `=SEQUENCE(3)` | 6 | `#VALUE!` | 6, `observed` |
| INDIRECT-061 | `=INDIRECT("Positions[PV]",FALSE)` | 250.5 | `#VALUE!` | 250.5, `observed` |
| INDIRECT-052 | in C2, `=INDIRECT("R[-5]C[-1]",FALSE)` | 0 | `#VALUE!` | `#VALUE!`, `excelExpect` 0, `observed` |
| INDIRECT-043 | `=SUM(INDIRECT("Positions"))`, one row | 250.5 (first pass) | `#VALUE!`, `excelExpect` 250.5 | the same, the decision ADR-0049's rule 2 |
| INDIRECT-053 | `=COLUMNS(INDIRECT("Positions"))`, two rows | 2 | `#VALUE!` (`uncertain`) | `#VALUE!`, `excelExpect` 2, `observed`, by decision |
| INDIRECT-054 | `=ROWS(INDIRECT("Positions"))` | 2 | `#VALUE!` | `#VALUE!`, `excelExpect` 2, `observed`, by decision |
| INDIRECT-055 | `=INDEX(INDIRECT("Positions"),1,1)` | `R-1` | `#VALUE!` | `#VALUE!`, `excelExpect` `R-1`, `observed`, by decision |
| INDIRECT-056 | `=COUNTA(INDIRECT("Positions"))` | 4 | 1 (the refusal counted) | 1, `excelExpect` 4, `observed`, by decision |
| INDIRECT-057 | `=INDIRECT("Positions")` in ZZ1000, read at AAA1000 | 250.5 | blank | blank, `excelExpect` 250.5, `observed`, by decision |
| INDIRECT-045 to 050 | the forms the first pass's answers gave | as expected | `uncertain` | `observed` (agreed; descriptions say so) |

Every observed `INDIRECT` reading in the results files, 168 readings of INDIRECT-001 to 066 from the seventeenth run
on, agrees with the corpus's `expect`, or its `excelExpect` where the engine refuses by decision. The descriptions of
INDIRECT-009 to 022, 027 to 035 and 039 to 042, observed by the seventeenth run or the first area pass, said "Engine's
answer, Excel to be asked"; they now say which run observed them.

## Which forms are observed, which follow, which are the engine's

**Observed** (the seventeenth run and the two area passes), and read as Excel reads them:

- R1C1 text, `a1` FALSE, 0, a blank or left empty: R and C each with a number, a leading 0 included (`R3C2`, `R03C2`);
  an offset in brackets, `+`, 0 and `-0` included (`R[1]C[-1]`, `R[+1]C[-1]`, `R[0]C[-1]`, `R[-0]C[-1]`); or no
  number, the Formula's own row or column, not every one (`RC[-1]`, `R[2]C`; `RC[-1]` would otherwise have spilled
  column B into C2, `#SPILL!`). A row part alone is a whole row (`R3`, `R[1]`), a column part alone a whole column
  (`C2`, `C[-1]`); two parts of the same kind joined by `:` the rectangle they span (`R2C2:R5C2`, `R2:R3`, `C1:C2`,
  `RC[-1]:R[1]C[-1]`); parts of two kinds joined name no place, `#REF!` (`R2C2:R3`, `R3:C2`); one cell followed by
  `#` is its Spill Range (`R1C1#`); A1 text read in R1C1 style names no place, `#REF!` (`B3`).
- `Positions[PV]` in either style; `A1#`, and `#REF!` where it does not spill; and `#REF!` for text that names no
  place: empty text, a number, a space before the Reference, a leading `=`, an intersection, `XFE1`, another Sheet's
  name in A1 style, A1 text read as R1C1 and R1C1 text read as A1, a table or a column that does not exist.

**Read by the rule those settle, and put to the next run** (`IndirectRowColumn`, `RowColumnAxis.Read`):

- Each axis a number, an offset or nothing in any combination (`RC2`, `R2C`, `R[1]C[-1]:RC[-1]`, `C[0]`), leading 0s
  anywhere (`R[01]`, `C02`; a long run of leading 0s is trimmed before the length is judged, so
  `R00000000000003C2` is R3C2): INDIRECT-067 to 072. `R` alone is therefore the Formula's whole row, `C` its whole
  column and `RC` its cell, each reaching the Formula, `#CIRC!`.
- Parts of two kinds joined in the other orders (`R2C2:C3`, `R3:R2C2`, `C2:R3`) are `#REF!`, and so is a join of two
  kinds whatever its parts would read (`R0C2:R3`, `R[-5]C[-1]:C2`, `R1048577C2:R3`): INDIRECT-075 to 077.
- `#` after any one cell, relative ones included, is its Spill Range, and `#REF!` where the cell does not spill, as
  `A1#` is: INDIRECT-078, 079.
- Structured text in R1C1 style is read as in A1 style: INDIRECT-080, 081.
- Another Sheet's name in R1C1 style is `#REF!`, as in A1 style (INDIRECT-008), whatever follows it, since every
  reading of the rest names a place on a Sheet that is not there (`Sheet2!R3C2`, `Sheet2!R3#`, `Sheet2!R1048577C2`):
  INDIRECT-093. In A1 style too, another Sheet's name is judged first (`Sheet2!A1:A2#`).

**`#REF!` by the rule those settle, which no case asks:**

- A1 text that goes on past its Reference names no place: the engine's rule, generalised from the intersection Excel
  was observed to refuse (INDIRECT-021). A `#` after a range, a row or a column is not of this class: it is refused
  (below).
- A row past 1048576 in A1 style (`B1048577`) is `#REF!`, as `XFE1`'s column was observed to be (2026-10-12, decided
  with the user: A1 text past the Sheet's edge stays `#REF!`).
- Other text that is not R1C1 notation, read in R1C1 style, is `#REF!`, as INDIRECT-035's `B3` is.

**`#VALUE!` until a run observes them**, where the observed answers leave the readings apart:

- **An offset past the Sheet's edge** (INDIRECT-052). Excel's 0 rules out `#REF!`: the offset names a place. But B1
  and B1048573 are both blank in the case, so wrapping round to the last rows and stopping at row 1 both give 0.
  INDIRECT-082 to 084 put 7 and 9 in the two cells each reading would name, on the top edge (`R[-5]C[-1]` from C2:
  B1, B1048573), the left (`R2C[-5]` from C2: A2, XFB2) and the bottom (`R[1048575]C[-1]` from C2: B1, B1048576).
- **A row or column number 0** (`R0C2`), which no run has asked: INDIRECT-073.
- **A row or column number past the Sheet's edge** (`R1048577C2`, `R2C16385`), whatever an offset beside it reads
  (2026-10-12, decided with the user: refused with `#VALUE!` until Excel is asked, and INDIRECT-074 asks it; A1 text
  past the edge stays `#REF!`, as `XFE1` was observed to be). It could name no place, as `XFE1` does, or a place, as an
  offset past the edge does. INDIRECT-074 (`R1048577C2`, with B1 7 and B1048576 9) and, of this branch's choosing, 095
  (`R2C16385`, with A2 7 and XFD2 9) ask it: `#REF!`, a wrap round or a stop at the edge.
- **`#` after a range, a row or a column, in either style, whatever the parts joined** (`A1:A2#`, `A:A#`, `1:1#`,
  `R2C2:R3C2#`, `R1C1:R2C1#`, `R3#`, `C2#`, `C1#`, `R2C2:R3#`): no run has observed `#` after anything but one cell, in
  either style, and the readings part: no place; the Spill Range of the first cell; the cells themselves, the `#`
  passed over; or the first cell joined to the second's Spill Range, `A1:(A2#)`, the `#` binding to the cell before
  it. The `#` is judged before a join of two kinds, since `R2C2:R3#` could be B2's Spill Range. INDIRECT-090
  (`=SUM(INDIRECT("R1C1:R2C1#",FALSE))` over `=SEQUENCE(3)` in A1) and 092 (`=SUM(INDIRECT("A1:A2#"))`, the same):
  `#REF!` for no place or the join, since A2 lies in A1's spill, 6 or 3; INDIRECT-097 and 096, the same texts with 1
  in A1 and `=SEQUENCE(2)` in A2: 4 for the join, 2 for the range, `#REF!` for no place or A1's spill; so the two
  pairs tell the four readings apart. 091 (`=SUM(INDIRECT("C1#",FALSE))`, with 10 in A5 too: `#REF!`, 6 or 16) and 094
  (`=SUM(INDIRECT("R2C2:R3#",FALSE))` over `=SEQUENCE(3)` in B2: `#REF!` or 6) ask it too.

**`#VALUE!` by decision:** a declared Linked Table's bare name, in either style (above).

**No search.** No `INDIRECT` answer turns on digits: each number Excel gave is a cell's Value or a sum of two or four,
and INDIRECT-051's 46.332999999999998 (G17) is the double the engine's `SUM` of 1.333 and 45 gives, written 46.333.

## The edits, in the order they apply

### 1. docs/adr/0049-linked-tables-are-the-consumers-data-read-by-key.md

The decision, in "Settled while building", after the section's last line.

Old:

```markdown
  Refusing on entry would make a saved document impossible to reopen before its tables arrive.
```

New:

```markdown
  Refusing on entry would make a saved document impossible to reopen before its tables arrive.

- **A table's bare name read through `INDIRECT` is refused** *(2026-10-12, decided with the user:
  `#VALUE!`, in either style)*. Excel reads `INDIRECT("Positions")` as the table's data, every column
  and every row without the header (INDIRECT-053 to 057, observed by the second area pass of that day),
  and a Formula could then read the table by position, `INDEX(INDIRECT("Positions"),1,1)`, which rule 2
  does not offer. It is `#VALUE!` in either style, Excel's answers are recorded beside the refusal as a
  difference by decision, and it is a Known limitation of `0.1.0`
  ([ADR-0202](./0202-what-0-1-0-holds-of-exsheet-and-expivot-and-where-it-says-what-it-does-not-do.md)).
  Typed, the bare name stays `#NAME?`; `Table[Column]`, typed or as `INDIRECT`'s text, reads the column.
```

### 2. docs/adr/0124-volatile-functions-are-recalculated-after-every-change.md

The seventeenth run's sentence "**R1C1 text in a form no run has observed stays `#VALUE!`**", its three lines. The
sentence stays, with what became of it.

Old:

```markdown
does not spill. **R1C1 text in a form no run has observed stays `#VALUE!`**: relative (`R[1]C[-1]`, `RC`), whole rows
or columns (`R3`, `C2`), a number with a leading 0, and structured or `#` text in R1C1 style. INDIRECT-030 to 042 put
the forms generalised from the observed ones to the next run. Excel reads R1C1 text in the letters of its display
```

New:

```markdown
does not spill. **R1C1 text in a form no run has observed stays `#VALUE!`**: relative (`R[1]C[-1]`, `RC`), whole rows
or columns (`R3`, `C2`), a number with a leading 0, and structured or `#` text in R1C1 style (the two area passes of
the same day observed a form of each; the note below says what is read since, and what stays refused). INDIRECT-030 to
042 put the forms generalised from the observed ones to the next run. Excel reads R1C1 text in the letters of its display
```

### 3. docs/adr/0124-volatile-functions-are-recalculated-after-every-change.md

A dated note at the end of "`RAND`, `RANDBETWEEN` and `INDIRECT`", the ADR's last section, after its last line.

Old:

```markdown
Formula twice, and one that moves no spill once.)*
```

New:

```markdown
Formula twice, and one that moves no spill once.)*

*(2026-10-12, after the two area passes (ADR-0208's remedy): **R1C1 text is read as Excel was observed to read it,
and in the forms that follow from its answers by the rule they settle; a declared Linked Table's bare name stays
refused, by decision.** The first pass read, in C2, `INDIRECT("R[1]C[-1]",FALSE)` as 45, B3's Value,
`SUM(INDIRECT("R3",FALSE))` as 45 and `SUM(INDIRECT("C2",FALSE))` as 118.333 (INDIRECT-036 to 038). The second read
the forms those left open: `RC[-1]` from C2 is 1.333 and `R[2]C` from B1 is 45, so an R or a C with no number is the
Formula's own row or column, not every one (INDIRECT-044, 051, 066); `R[0]C[-1]` and `R[-0]C[-1]` are 1.333 and
`R[+1]C[-1]` is 45, so an offset is a signed number, 0 included (INDIRECT-063 to 065); `R03C2` is 45, so a number is
read with its leading 0s (INDIRECT-059); `R2C2:R3` and `R3:C2` are `#REF!`, so parts of two kinds joined name no place
(INDIRECT-058, 062); `R1C1#` is the Spill Range, as `A1#` is (INDIRECT-060); and `Positions[PV]` read in R1C1 style is
the column, as in A1 style (INDIRECT-061). INDIRECT-045 to 050, the forms the first pass's answers gave, agreed. What
follows is read until a run asks it (INDIRECT-067 to 072, 075 to 081, 093): R and C each take a number, an offset in
brackets or nothing, in any combination; a row part alone is a whole row, a column part alone a whole column; two
parts of the same kind joined by `:` name the rectangle they span, and two of different kinds name no place, whichever
comes first; a `#` after any one cell is its Spill Range; another Sheet's name names no place, whatever follows it, as
it was observed to in A1 style (INDIRECT-008); a Linked Table's text is read alike whatever `a1` says. `R` alone is
therefore the Formula's whole row, `C` its whole column and `RC` its own cell, each a cycle. **Still `#VALUE!`**, where
the observed answers leave the readings apart: an offset past the Sheet's edge, which Excel reads as a place
(`R[-5]C[-1]` from C2 is 0, a blank cell, not `#REF!`, INDIRECT-052) though no run has told whether it wraps round to
the Sheet's far side or stops at the edge, since both cells are blank there; a row or column number 0; a row or column
number past the Sheet's edge (`R1048577C2`, `R2C16385`), which could name no place, as `XFE1` does in A1 style
(INDIRECT-017), or a place, as an offset past the edge does (2026-10-12, decided with the user: in R1C1 style a row or
column number past the Sheet's edge is refused with `#VALUE!` until Excel is asked, and INDIRECT-074 asks it; A1 text
past the edge stays `#REF!`, as `XFE1` was observed to be); and, in either style, a `#` after a range, a row or a
column, whatever the parts joined (`A1:A2#`, `A:A#`, `R2C2:R3C2#`, `R2C2:R3#`), which no run has observed in either
style and which could name no place, the first cell's Spill Range, the cells themselves or the first cell joined to
the second's Spill Range, `A1:(A2#)` (INDIRECT-073, 082 to 084, 090 to 092 and 094 to 097 ask them). Other A1 text
that goes on past its Reference names no place, as the intersection Excel was observed to refuse does (INDIRECT-021):
the engine's rule, which no case asks. **A declared Linked Table's bare name is `#VALUE!` by decision** (2026-10-12,
decided with the user: the bare name stays `#VALUE!`, in either style): Excel reads it as the table's data — over a table of two rows,
`COLUMNS(INDIRECT("Positions"))` is 2, `ROWS` 2, `INDEX(…,1,1)` the first row's first Value, `COUNTA` 4, and the name
as a Formula's result spills the data (INDIRECT-053 to 057), which also gives the first pass's `SUM`, 250.5
(INDIRECT-043) — but a Formula could then read the table by position, and
[ADR-0049](./0049-linked-tables-are-the-consumers-data-read-by-key.md)'s rule 2 reads a Linked Table by key, never by
position. The six cases hold Excel's answers beside the refusal, a difference by decision, and the refusal is a Known
limitation of `0.1.0` (ADR-0202). Typed, a table's bare name is `#NAME?`, as an unknown name is.)*
```

### 4. docs/adr/0202-what-0-1-0-holds-of-exsheet-and-expivot-and-where-it-says-what-it-does-not-do.md

The Known limitations (the Consequences): the last line of the bullet "`0.1.0`'s Known limitations will hold at
least". The financial area proposes a line after the bullet's second line; this one goes after its last.

Old:

```markdown
  seconds and holds the circuit that long. `verification/2026-10-11-linux-sh19/` has both.)*
```

New:

```markdown
  seconds and holds the circuit that long. `verification/2026-10-11-linux-sh19/` has both.)*
  *(2026-10-12, decided with the user: also `INDIRECT` over a declared Linked Table's bare name,
  `INDIRECT("Positions")`, which is `#VALUE!` where Excel reads the table's data: a Formula could then read
  the table by position, which ADR-0049's rule 2 does not offer. ADR-0124's note of that date has Excel's
  answers.)*
```

### 5. docs/definition-of-done.md

The whole SH-68 line; only its `INDIRECT` sentence changes, and a dated note follows it.

Old:

```markdown
| **SH-68** | MUST | `SUBTOTAL` and `AGGREGATE` answer their corpus cases while no row can be hidden, and a test named for ADR-0047 fails on the day one can. Inside their range, a cell whose Formula, or whose Spill Range's Anchor's, calls `SUBTOTAL` or `AGGREGATE` anywhere is nested: `SUBTOTAL` and `AGGREGATE`'s options 0 to 3 pass over it and options 4 to 7 count its Value, as the seventeenth Windows run read Excel's answers; an `AGGREGATE` function number for a function the engine does not support is `#VALUE!`, and a Formula that gives one of their reference-only arguments something that can never be a Reference is refused on entry. `ERROR.TYPE` gives `#GETTING_DATA` and `#CIRC!` back, and Excel's numbers for its newer Error Values: `#SPILL!` 9, `#CONNECT!` 10, `#BLOCKED!` 11, `#UNKNOWN!` 12, `#FIELD!` 13, `#CALC!` 14. `INDIRECT` reads A1-style text naming a place on the same Sheet, R1C1 text naming an absolute cell or a range of two, a Linked Table's column as structured text (waiting while the table does) and `A1#`, keeps its text through an insertion or deletion, gives `#VALUE!` for R1C1 text that is relative or names whole rows or columns, and `#REF!` for text that names no place, a table or a column that does not exist included (ADR-0047's and ADR-0124's notes of 2026-10-10, ADR-0124's of 2026-10-12) *(2026-10-11, decided with the user: for `AGGREGATE`'s options 4 to 7, a cell in range whose whole Formula is a `SUBTOTAL` makes the result `#VALUE!` until the sign-off's Windows run reads Excel's answer, which Microsoft documents as counted; then it follows Excel.)* *(2026-10-12: the seventeenth run read it — 6, counted — and the engine follows Excel, ADR-0047's note of that date.)* *(2026-10-12, the seventeenth Windows run read `ERROR.TYPE`'s numbers for Excel's newer Error Values, which the row had refused with `#VALUE!` until Excel was asked; it now follows Excel, ADR-0047's note of this date.)* | Layer 1 | as stated |
```

New:

```markdown
| **SH-68** | MUST | `SUBTOTAL` and `AGGREGATE` answer their corpus cases while no row can be hidden, and a test named for ADR-0047 fails on the day one can. Inside their range, a cell whose Formula, or whose Spill Range's Anchor's, calls `SUBTOTAL` or `AGGREGATE` anywhere is nested: `SUBTOTAL` and `AGGREGATE`'s options 0 to 3 pass over it and options 4 to 7 count its Value, as the seventeenth Windows run read Excel's answers; an `AGGREGATE` function number for a function the engine does not support is `#VALUE!`, and a Formula that gives one of their reference-only arguments something that can never be a Reference is refused on entry. `ERROR.TYPE` gives `#GETTING_DATA` and `#CIRC!` back, and Excel's numbers for its newer Error Values: `#SPILL!` 9, `#CONNECT!` 10, `#BLOCKED!` 11, `#UNKNOWN!` 12, `#FIELD!` 13, `#CALC!` 14. `INDIRECT` reads A1-style text naming a place on the same Sheet; R1C1 text naming a cell, a whole row or column, the rectangle two parts of the same kind span, or one cell's Spill Range with `#`, each R and C a number, an offset in brackets from the Formula's own cell, or none, the own row or column; a Linked Table's column as structured text (waiting while the table does), whatever `a1` says; and `A1#`. It keeps its text through an insertion or deletion; gives `#VALUE!`, until a run observes them, for R1C1 text with a row or column number 0 or past the Sheet's edge or an offset past the Sheet's edge, and for a `#` after a range, a row or a column in either style, whatever the parts joined, and, by decision, for a declared Linked Table's bare name (ADR-0049's rule 2); and `#REF!` for text that names no place, a table or a column that does not exist, A1 text past the Sheet's edge and R1C1 parts of two kinds joined included (ADR-0047's and ADR-0124's notes of 2026-10-10, ADR-0124's of 2026-10-12) *(2026-10-12, decided with the user: a declared Linked Table's bare name stays `#VALUE!`, in either style; in R1C1 style, a row or column number past the Sheet's edge is `#VALUE!` until Excel is asked; A1 text past the edge stays `#REF!`, as `XFE1` was observed to be.)* *(2026-10-11, decided with the user: for `AGGREGATE`'s options 4 to 7, a cell in range whose whole Formula is a `SUBTOTAL` makes the result `#VALUE!` until the sign-off's Windows run reads Excel's answer, which Microsoft documents as counted; then it follows Excel.)* *(2026-10-12: the seventeenth run read it — 6, counted — and the engine follows Excel, ADR-0047's note of that date.)* *(2026-10-12, the seventeenth Windows run read `ERROR.TYPE`'s numbers for Excel's newer Error Values, which the row had refused with `#VALUE!` until Excel was asked; it now follows Excel, ADR-0047's note of this date.)* | Layer 1 | as stated |
```

The `INDIRECT` sentence alone, old:

> `INDIRECT` reads A1-style text naming a place on the same Sheet, R1C1 text naming an absolute cell or a range of two, a Linked Table's column as structured text (waiting while the table does) and `A1#`, keeps its text through an insertion or deletion, gives `#VALUE!` for R1C1 text that is relative or names whole rows or columns, and `#REF!` for text that names no place, a table or a column that does not exist included (ADR-0047's and ADR-0124's notes of 2026-10-10, ADR-0124's of 2026-10-12)

and new:

> `INDIRECT` reads A1-style text naming a place on the same Sheet; R1C1 text naming a cell, a whole row or column, the rectangle two parts of the same kind span, or one cell's Spill Range with `#`, each R and C a number, an offset in brackets from the Formula's own cell, or none, the own row or column; a Linked Table's column as structured text (waiting while the table does), whatever `a1` says; and `A1#`. It keeps its text through an insertion or deletion; gives `#VALUE!`, until a run observes them, for R1C1 text with a row or column number 0 or past the Sheet's edge or an offset past the Sheet's edge, and for a `#` after a range, a row or a column in either style, whatever the parts joined, and, by decision, for a declared Linked Table's bare name (ADR-0049's rule 2); and `#REF!` for text that names no place, a table or a column that does not exist, A1 text past the Sheet's edge and R1C1 parts of two kinds joined included (ADR-0047's and ADR-0124's notes of 2026-10-10, ADR-0124's of 2026-10-12) *(2026-10-12, decided with the user: a declared Linked Table's bare name stays `#VALUE!`, in either style; in R1C1 style, a row or column number past the Sheet's edge is `#VALUE!` until Excel is asked; A1 text past the edge stays `#REF!`, as `XFE1` was observed to be.)*

### 6. samples/ExGrid.Docs/Pages/Sheet/SheetFormulas.razor

The `INDIRECT` bullet of "Supported functions", where a reader meets the Known limitation (ADR-0202).

Old:

```html
                rewrites the References beside it, never the text. It also reads what Excel was seen to read: R1C1
                text (<code>a1</code> FALSE) naming a cell or a range, <code>R3C2</code> or <code>R2C2:R5C2</code>; a
                Linked Table's column as text, <code>Positions[PV]</code>, which waits while the table does; and a
                Spill Range, <code>A1#</code>. R1C1 text that is relative, <code>R[1]C[-1]</code>, or names whole rows
                or columns, and a Linked Table's name on its own (<code>"Positions"</code>, which Excel reads as the
                table's data), are <code>#VALUE!</code> until Excel has been asked; text that names no place, another
                Sheet's name or a table that does not exist included, is <code>#REF!</code>, as in Excel.
```

New:

```html
                rewrites the References beside it, never the text. It also reads what Excel was seen to read: R1C1
                text (<code>a1</code> FALSE) naming a cell or a range, <code>R3C2</code> or <code>R2C2:R5C2</code>,
                counted from the Formula's own cell by offsets in brackets, <code>R[1]C[-1]</code>, or from its own
                row or column where R or C has no number, <code>RC[-1]</code>; naming whole rows and columns,
                <code>R3</code>, <code>C[-1]</code> or <code>R2:R3</code>; or a Spill Range, <code>R1C1#</code>.
                Other combinations of these forms are read by the same rule until Excel has been asked them. It
                reads a Linked Table's column as text, <code>Positions[PV]</code>, which waits while the table
                does, and a Spill Range, <code>A1#</code>. R1C1 text that joins a cell to a whole row, or a row to
                a column, <code>R2C2:R3</code> or <code>R3:C2</code>, names no place and is <code>#REF!</code>, as in
                Excel; so is text naming another Sheet or a table that does not exist, as Excel's A1 text is. Some
                text is <code>#VALUE!</code> until Excel has told what it names: in R1C1 style, an offset past the
                Sheet's edge, <code>R[-5]C[-1]</code> from row 2, which Excel reads as a place without saying yet
                which, and a row or column number 0, <code>R0C2</code>, or one past the Sheet's edge,
                <code>R1048577C2</code>, while A1 text past the edge, <code>XFE1</code>, is <code>#REF!</code>, as
                in Excel; and, in either style, <code>#</code> after a range, a row or a column,
                <code>A1:A2#</code>, <code>R3#</code> or <code>R2C2:R3#</code>. A Linked
                Table's name on its own, <code>"Positions"</code>, is
                <code>#VALUE!</code>, though Excel reads the table's data: a Linked Table is read by its columns
                and by key, never by position, so that a Formula never reads a row by where it stands. Typed in a
                Formula, the name on its own is <code>#NAME?</code>.
```

No Example shows R1C1 text or a table's bare name, so none changes. The Linked Tables page says nothing of
`INDIRECT`, so it does not change either.

### 7. docs/releases/unreleased/run17-lookup-arrays-indirect-observed-forms.md

The seventeenth run's Change Note. Its last sentence is false now: relative and whole-row R1C1 text is read (this
area's Change Note says how), and the bare name is `#VALUE!` by decision, not until Excel is asked.

Old:

```markdown
`INDIRECT` reads the forms Excel was observed to read beside A1-style text: R1C1 text naming a cell or a range (`INDIRECT("R3C2",FALSE)`), a Linked Table's column as text (`INDIRECT("Positions[PV]")`, which waits while the table does) and a Spill Range (`INDIRECT("A1#")`). R1C1 text that is relative or names whole rows or columns, and a declared Linked Table's bare name (`INDIRECT("Positions")`, which Excel reads as the table's data), are `#VALUE!` until Excel is asked, and text naming a table or a column that does not exist is `#REF!`.
```

New:

```markdown
`INDIRECT` reads the forms Excel was observed to read beside A1-style text: R1C1 text naming a cell or a range (`INDIRECT("R3C2",FALSE)`), a Linked Table's column as text (`INDIRECT("Positions[PV]")`, which waits while the table does) and a Spill Range (`INDIRECT("A1#")`). A declared Linked Table's bare name (`INDIRECT("Positions")`) is `#VALUE!`, where Excel reads the table's data, since a Linked Table is read by key, never by position; text naming a table or a column that does not exist is `#REF!`.
```

## Outside the numbered edits

### The Known limitations line for `0.1.0`'s Release Notes

```markdown
- `INDIRECT` over a Linked Table's name on its own, `INDIRECT("Positions")`, is `#VALUE!`, where Excel reads
  the table's data: a Linked Table is read by key, never by position
  ([Formulas](exsheet/formulas#supported-functions)).
```

The Roadmap's sentence on Known limitations (`samples/ExGrid.Docs/Pages/RoadmapByHand.cs`) names some of them, "Among
them", and needs no edit for this one.

### `linked-tables.json`: a typed case to propose (the file is in no area)

Appended after TABLE-023, so that a pass reads Excel's answer to the typed form beside INDIRECT-053, and the difference
by decision records it as INDIRECT-053 records Excel's answer through text:

```json
    {
      "id": "TABLE-024",
      "source": "uncertain",
      "description": "Refused by decision, and recorded so that Excel is asked: a declared table's bare name typed, which Excel reads through INDIRECT as the table's data (2 columns, INDIRECT-053); ExSheet offers no typed bare table name, so it is #NAME? as an unknown name is",
      "tables": {"Positions": {"columns": ["Id", "PV"], "rows": [["R-1", 250.5], ["R-2", 50]]}},
      "cells": {"A1": "=COLUMNS(Positions)"},
      "check": "A1",
      "expect": {"value2": "#NAME?"},
      "engineDiffersByDecision": "ADR-0049 (rule 2: a Linked Table is read by key, never by position, and a whole column by name; 2026-10-12, decided with the user: a declared Linked Table's bare name through INDIRECT stays #VALUE!, in either style): a table's bare name typed is not offered and is #NAME?; recorded, so that a run reads Excel's answer beside INDIRECT-053"
    }
```

The engine's answer, `#NAME?`, holds on this branch (`=COLUMNS(Positions)` over a declared table with data).

## New cases (all `uncertain`), for the next run

Over Microsoft's A2:B5 fixture unless named. None reads the Formula's own cell, row or column, which is a cycle
(`#CIRC!` differs from Excel by decision). INDIRECT-085 to 089, which asked what the withdrawn reading of the bare name
followed to, are withdrawn with it; their numbers are not reused.

| Case | Formula | Engine (expect) | What it asks |
|---|---|---|---|
| INDIRECT-067 | in C3, `=INDIRECT("RC2",FALSE)` | 45 | an R with no number beside an absolute column |
| INDIRECT-068 | in B6, `=INDIRECT("R2C",FALSE)` | 1.333 | a C with no number beside an absolute row |
| INDIRECT-069 | in C2, `=SUM(INDIRECT("R[1]C[-1]:RC[-1]",FALSE))` | 46.333 | an R with no number second in a rectangle |
| INDIRECT-070 | in B1, `=INDIRECT("R[1]C[0]",FALSE)` | 1.333 | a zero offset on the column |
| INDIRECT-071 | in C2, `=INDIRECT("R[01]C[-1]",FALSE)` | 45 | a leading 0 in an offset |
| INDIRECT-072 | `=INDIRECT("R3C02",FALSE)` | 45 | a leading 0 in a column number |
| INDIRECT-073 | `=INDIRECT("R0C2",FALSE)` | `#VALUE!` (by decision) | a row number 0 |
| INDIRECT-074 | `=INDIRECT("R1048577C2",FALSE)`, B1 7, B1048576 9 | `#VALUE!` (2026-10-12, decided with the user: refused until Excel is asked) | a row number past the edge in R1C1 style: `#REF!`, 7 wraps round, 9 stops at the last row |
| INDIRECT-075 | `=SUM(INDIRECT("R2C2:C3",FALSE))` | `#REF!` | a cell joined to a column |
| INDIRECT-076 | `=SUM(INDIRECT("R3:R2C2",FALSE))` | `#REF!` | a row joined to a cell |
| INDIRECT-077 | `=ROWS(INDIRECT("C2:R3",FALSE))` | `#REF!` | a column joined to a row |
| INDIRECT-078 | in B1, `=SUM(INDIRECT("RC[-1]#",FALSE))` over A1 `=SEQUENCE(3)` | 6 | `#` after relative text |
| INDIRECT-079 | `=SUM(INDIRECT("R1C1#",FALSE))` over A1 7 | `#REF!` | `R1C1#` over no spill, as INDIRECT-042's `A1#` |
| INDIRECT-080 | `=INDIRECT("Positions[[PV]]",FALSE)` | 250.5 | double brackets in R1C1 style |
| INDIRECT-081 | `=INDIRECT("Missing[PV]",FALSE)` | `#REF!` | no such table, in R1C1 style |
| INDIRECT-082 | in C2, `=INDIRECT("R[-5]C[-1]",FALSE)`, B1 7, B1048573 9 | `#VALUE!` (by decision) | 9 wraps round, 7 stops at row 1 |
| INDIRECT-083 | in C2, `=INDIRECT("R2C[-5]",FALSE)`, A2 7, XFB2 9 | `#VALUE!` (by decision) | 9 wraps round, 7 stops at column A |
| INDIRECT-084 | in C2, `=INDIRECT("R[1048575]C[-1]",FALSE)`, B1 7, B1048576 9 | `#VALUE!` (by decision) | 7 wraps round, 9 stops at the last row |
| INDIRECT-090 | `=SUM(INDIRECT("R1C1:R2C1#",FALSE))` over A1 `=SEQUENCE(3)` | `#VALUE!` (by decision) | `#` after an R1C1 range: `#REF!` (no place, or R1C1 joined to R2C1's spill, which A2 has not), 6 (the first cell's spill) or 3 (the range) |
| INDIRECT-091 | `=SUM(INDIRECT("C1#",FALSE))` over A1 `=SEQUENCE(3)`, A5 10 | `#VALUE!` (by decision) | `#` after a whole column: `#REF!`, 6 or 16 (the column) |
| INDIRECT-092 | `=SUM(INDIRECT("A1:A2#"))` over A1 `=SEQUENCE(3)` | `#VALUE!` (by decision; it was the engine's `#REF!`) | `#` after an A1 range: `#REF!` (no place, or A1 joined to A2's spill), 6 or 3 |
| INDIRECT-093 | `=INDIRECT("Sheet2!R3C2",FALSE)` | `#REF!` | another Sheet's name in R1C1 style, as INDIRECT-008's `Sheet2!B2` |
| INDIRECT-094 | `=SUM(INDIRECT("R2C2:R3#",FALSE))` over B2 `=SEQUENCE(3)` | `#VALUE!` (by decision) | `#` after a cell joined to a row: `#REF!` (as `R2C2:R3`) or 6 (B2's spill) |
| INDIRECT-095 | `=INDIRECT("R2C16385",FALSE)`, A2 7, XFD2 9 | `#VALUE!` (2026-10-12, decided with the user: refused until Excel is asked); the case is this branch's own | a column number past the edge in R1C1 style: `#REF!`, 7 wraps round, 9 stops at the last column |
| INDIRECT-096 | `=SUM(INDIRECT("A1:A2#"))`, A1 1, A2 `=SEQUENCE(2)` | `#VALUE!` (by decision) | `#` after an A1 range whose second cell spills: 4 (A1 joined to A2's spill), 2 (the range) or `#REF!` (no place, or A1's spill) |
| INDIRECT-097 | `=SUM(INDIRECT("R1C1:R2C1#",FALSE))`, A1 1, A2 `=SEQUENCE(2)` | `#VALUE!` (by decision) | the same in R1C1 style: 4, 2 or `#REF!` |
