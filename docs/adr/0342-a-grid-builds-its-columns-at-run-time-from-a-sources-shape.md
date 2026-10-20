# A grid builds its columns at run time from a source's shape

*Decided with the user, 2026-10-20 (Q3 of the grilling recorded in ADR-0340). The user accepted the
Board's way of naming data "provided a dynamic grid can be supported".*

## Context

A pivot already reads a Snapshot of any shape (`SnapshotPivotSource`), and a chart reads one through
its spec. A grid does not: a column is `GridColumn<TRow>`, a compiled row type and a function per
column, so data whose columns are known only at run time cannot be shown without code written for it.
[ADR-0016](0016-column-width-and-overflow.md) also holds that this grid has no General format: every column carries its format, which is
what lets a number that does not fit become `####` instead of being rounded to fit.

## Decisions

- **A grid view of a Board builds its columns from the source's shape** ([ADR-0341](0341-a-board-input-declares-its-columns-and-a-source-answers-its-shape-first.md)): one column
  per declared column the view shows, its row a position in the source. Header, width, pinning, sorts
  and filters come from the view's spec in the View State's form ([ADR-0207](0207-the-view-state-travels-as-json-through-an-entry-point-of-exgrids-own-each-operand-with-its-type.md)); sorting and filtering
  are executed by the source with `GridSource.From`'s semantics, in memory or where the data lives.
- **A decimal or double column must carry its format**, an Excel format code in the invariant spelling
  ([ADR-0120](0120-text-reads-its-format-code-in-the-invariant-spelling-as-a-cell-format-shows-it.md)): `#,##0.00`, `0.00%`, `0.00000`. A missing one is refused when the Board is checked,
  naming the column. The AI chooses the places while it composes, when it knows whether the column is
  yen, basis points or an FX rate. Not a default guessed from the type, which would show an FX rate of
  `1.08734` as `1.09`; not the shortest exact form, which ADR-0016 rejected and which leaves a money
  column's decimals unaligned.
- **Other types have fixed defaults**: an integer `#,##0`, a date `yyyy-mm-dd`, text as it is; a spec
  may still write its own.
- **The accepted risk**: a wrong format (two places on a rate) is not caught by refusal, as a wrong
  Series Kind is not (ADR-0320). It is written in the saved Board, where a person reading it sees it.

## Consequences

- The grid gains a column type over a source's shape that needs no row type of the Consumer's; the
  virtualisation and memoisation invariants (ADR-0027 P1–P9) hold for it as for any column.

## Implementation notes

*Recorded with the implementation, 2026-10-20 (ticket `exboard/01`); no decision above changes.*

- The type is `ShapedGrid` in `ExGrid`, over a Snapshot, a row a `ShapedRow` made once per row so its
  reference is stable and its render is skipped (ADR-0003; `SnapshotRow` is a value and a grid's row is
  an object). The spec is read by `GridViewSpec`; a column's `width` and `pinned`, the `sorts` and the
  `filter` are handed to `GridViewStateJson`'s reader, so a Board's grid and a saved view cannot mean
  two things.
- The format code is read by a reader of the grid's own, a subset of the codes ExSheet's
  `NumberFormat` reads: up to three number sections of `0 # . , % E+`, quoted text, escapes and
  unquoted symbols, or one date section of `y m d h s` and `AM/PM`. A section is chosen by the sign
  before rounding, and a negative that rounds to zero in one section shows no minus sign, as ExSheet
  observed of Excel (FMT-063). Everything else is refused. ExGrid does not reference ExSheet.Engine,
  whose `NumberFormat` lives beside the formula engine; sharing one renderer is left for when a grid
  needs more of the code than this subset.
- A date column holding a time of day, with no format written, shows `yyyy-mm-dd hh:mm:ss` rather
  than the fixed `yyyy-mm-dd`, so that a time is never hidden (spine rule 1). The text is written in
  the invariant culture unless the host passes one, as a column's default text is.
