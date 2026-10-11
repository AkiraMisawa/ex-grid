# 16: SUBTOTAL and AGGREGATE, while a Sheet cannot hide a row

Status: done

**What to build:** `SUBTOTAL` and `AGGREGATE` under
[ADR-0047](../../../adr/0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md)'s
note of 2026-10-10, "The catalogue's last Decide rows".

**Blocked by:** none.

- [x] `SUBTOTAL` with function numbers 1–11 and 101–111. While no row can be hidden, the two give the
      same answers, as they do in Excel over such a sheet. A test named for ADR-0047 fails on the day a
      row can be hidden, and its message says to revisit this (SH-37's pattern)
- [x] `AGGREGATE`'s reference form and array form, with its options 0–7. The options that skip hidden
      rows give what the others give. Function numbers for functions the engine supports are
      answered; the rest are `#VALUE!`
- [x] Nested calls (SH-68):
      - a cell whose whole Formula is a `SUBTOTAL` is skipped by `SUBTOTAL`;
      - a cell whose whole Formula is a `SUBTOTAL` or an `AGGREGATE` is skipped by `AGGREGATE`'s
        options 0–3;
      - any other call of either in the range makes the result `#VALUE!`
- [x] Corpus cases: Microsoft's documented cases as `documented`, and the open ones as `uncertain`
      for the next Windows run. That run also asks what Excel counts as nested
- [x] The catalogue rows Supported. The Docs Site's Formulas page lists them from the declared set,
      and says what is refused
- [ ] For the next Windows run: the `uncertain` cases of `ExcelCases/subtotal.json` and
      `ExcelCases/aggregate.json`, and what Excel counts as nested — a call inside a larger Formula
      (SUBTOTAL-020, AGGREGATE-026), an `AGGREGATE` in `SUBTOTAL`'s range (SUBTOTAL-021), and a nested
      call under `AGGREGATE`'s options 4 to 7 (AGGREGATE-025), which Microsoft documents as counted

## Comments

2026-10-10: written with the user's decision, ADR-0047's note of 2026-10-10.

2026-10-11: built. `SUBTOTAL` and `AGGREGATE` reduce through the same code as `SUM`, `AVERAGE`, `MEDIAN`,
`LARGE` and their kin, so each function number answers as its function does over a range. The nesting
rule reads the Formula a cell shows (its own, or for a spilled cell its Anchor's): a whole-Formula call
is passed over where the ticket says, and any other call is `#VALUE!` — including a whole-Formula
`SUBTOTAL` under `AGGREGATE`'s options 4 to 7, which the ticket's "any other call" covers although
Microsoft documents those options as counting it; SH-68's wording can be read as passing it over, so the
refusal is the one answer none of the three readings contradicts. `SubtotalTests` holds the test that
fails on the day a row can be hidden (SH-37's pattern, its message naming ADR-0047's note), and
`RowHidingWatchTests` watches the component the same way. Function numbers and options that are not
whole numbers, arguments that are no reference where one is wanted, and `AGGREGATE`'s 13 and 16 to 19
are `#VALUE!`. Completion lists `SUBTOTAL`'s 22 numbers, and `AGGREGATE`'s supported numbers and eight
options, with Microsoft's documented texts; the Windows run should confirm Excel's popup texts. ADR-0047's
dated addition naming the functions is left to the orchestrator.

2026-10-11, after review: what a computed Reference reads is tainted as a Reference the text names
is, so a `SUBTOTAL` or an `AGGREGATE` over `INDIRECT` or `OFFSET` that reaches a cycle, its own cell or
a waiting cell is `#CIRC!` or `#GETTING_DATA`, where it passed over the Error Value or the nested call
before (`IndirectTests`). AGGREGATE-025 is `documented`, with Excel's 6 as `excelExpect` (Microsoft
documents options 4 to 7 as ignoring nothing) beside the engine's `#VALUE!`. AGGREGATE-007, 008 and 011
are `uncertain`, since Microsoft does not document which Error Value comes back, and the other
`documented` values worked from Microsoft's definitions say so.

2026-10-11, with SH-68's note of that date (decided with the user: under `AGGREGATE`'s options 4 to 7,
a cell in range whose whole Formula is a `SUBTOTAL` makes the result `#VALUE!` until the sign-off's
Windows run reads Excel's answer, then the engine follows Excel): already so, and pinned by
`ADR0047_SH68_aggregate_options_4_to_7_over_a_whole_formula_subtotal_are_value`, each option beside the
one of 0 to 3 that passes over the same cells. AGGREGATE-025 is `uncertain` again, with Microsoft's
counted 6 as `excelExpect`, so the run compares Excel with it. The Formulas page now says which options
pass over what. The Status is `done`: the one open box is the sign-off's Windows run, and each of its
questions is a corpus case — the `uncertain` cases of `subtotal.json` (SUBTOTAL-020 to SUBTOTAL-034) and
`aggregate.json` (AGGREGATE-007, 008, 011 and 022 to 041), what counts as nested among them. Excel's popup
texts for the function numbers and options, which the comment above asks the run to confirm, are
Microsoft's documented ones (ADR-0058); like tickets 09 and 11's captions, nothing here waits on them.
