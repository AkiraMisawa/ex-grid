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
      *(2026-10-12: replaced by Excel's rule, as the seventeenth Windows run read it — see the box below
      and the comment of that date.)*
- [x] Corpus cases: Microsoft's documented cases as `documented`, and the open ones as `uncertain`
      for the next Windows run. That run also asks what Excel counts as nested
- [x] The catalogue rows Supported. The Docs Site's Formulas page lists them from the declared set,
      and says what is refused
- [x] For the next Windows run: the `uncertain` cases of `ExcelCases/subtotal.json` and
      `ExcelCases/aggregate.json`, and what Excel counts as nested — a call inside a larger Formula
      (SUBTOTAL-020, AGGREGATE-026), an `AGGREGATE` in `SUBTOTAL`'s range (SUBTOTAL-021), and a nested
      call under `AGGREGATE`'s options 4 to 7 (AGGREGATE-025), which Microsoft documents as counted.
      *Answered by the seventeenth Windows run (2026-10-11,
      `verification/2026-10-11-windows-17/report.md`): a cell is nested when its Formula, or for a cell
      of a Spill Range its Anchor's, calls `SUBTOTAL` or `AGGREGATE` anywhere; `SUBTOTAL` (every
      function number) and `AGGREGATE`'s options 0 to 3 pass over it, and options 4 to 7 count its
      Value. The engine follows it (comment of 2026-10-12)*
- [x] `oracle.ps1 -Area subtotal,aggregate` before the tag (ADR-0208): the `uncertain` cases the
      seventeenth run's answers led to, SUBTOTAL-039 to 044 and AGGREGATE-055 to 061. The engine agrees
      with each answer, or is corrected to it, before this ticket is done. *(Added 2026-10-12 after
      review: ADR-0208 does not close a ticket on "the next Windows run asks", and these questions came
      after the seventeenth run.)* The `LARGE`, `SMALL` and `MOD` cases asked with them are not this
      ticket's. *Asked by the area pass of 2026-10-12 (`verification/2026-10-12-windows-area/report.md`):
      every case agreed but AGGREGATE-060 and 061, and the engine now gives Excel's answer to both
      (comment of that date)*
- [x] `oracle.ps1 -Area aggregate` before the tag (ADR-0208): AGGREGATE-062 to 067, the `uncertain`
      cases the area pass's answers led to — a k below 1 for `LARGE` and `SMALL`, a k near the count for
      `SMALL`, and a boolean, text that is no number and an Error Value given directly as the array. The
      engine agrees with each answer, or is corrected to it, before this ticket is done. *Asked by the
      second area pass of 2026-10-12 (`verification/2026-10-12-windows-area-2/report.md`): all six
      agreed, and are `observed`*
- [x] `oracle.ps1 -Area aggregate,small` before the tag (ADR-0208): AGGREGATE-068, the `uncertain` case the
      second area pass's answers led to — `SMALL`'s k above the count of the numbers, which Excel's
      `SMALL` refuses before truncating it (SMALL-008) — with SMALL-011, `SMALL`'s own k a little above
      the count (ticket 04's comment of 2026-10-12), and AGGREGATE-069, option 6 over a cell ExSheet
      refused, which refuses the Formula (ADR-0047, 2026-10-12, decided with the user: a refusal is
      never caught, anywhere). The engine agrees with each answer, or is corrected to it, or records a
      refusal by that decision, before this ticket is done. *AGGREGATE-068 and SMALL-011 were
      asked by an earlier pass and are `observed`; AGGREGATE-069 by the oracle run of 2026-10-18,
      which read Excel's 7 beside the engine's refusal, a difference by decision (`excelExpect`)*

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
counted 6 as `excelExpect`, so the run compares Excel with it. *(Corrected the same day, after review:
with an `excelExpect` beside `engineDiffersByDecision`, Excel's 6 would have read as agreement, and
`-Update` would have marked the case observed while the engine stayed `#VALUE!`. AGGREGATE-025 now
expects `#VALUE!` with no `excelExpect`, as AGGREGATE-026 does, so Excel's answer reads as a
disagreement, which is corrected before the tag. AGGREGATE-042 to AGGREGATE-051 ask the rest of what
the test pins: options 5 to 7 over a whole-Formula `SUBTOTAL`, options 4 to 7 over a whole-Formula
`AGGREGATE`, and the array form.)* The Formulas page now says which options
pass over what. The Status is `done`: the one open box is the sign-off's Windows run, and each of its
questions is a corpus case — the `uncertain` cases of `subtotal.json` (SUBTOTAL-020 to SUBTOTAL-038) and
`aggregate.json` (AGGREGATE-007, 008, 011 and 022 to 054), what counts as nested among them. Excel's popup
texts for the function numbers and options, which the comment above asks the run to confirm, are
Microsoft's documented ones (ADR-0058); like tickets 09 and 11's captions, nothing here waits on them.

2026-10-11, after the second review: the layer-3 spec this ticket rewrote (`declarations.spec.mjs`,
where `=SU` now lists `SUBTOTAL` second and the keyboard takes one more ↓ to reach `SUM`) had not been
run locally. Its three DC-17 tests ran under both Chromes with `--project=chrome --repeat-each=3`, on
Playwright's bundled Chromium (a container without Chrome or Edge, so no Step 4 record), headed under
xvfb: 18 passed on the WebAssembly host (port 5441) and 18 on the Server host (port 5440). The full run,
and the installed Chrome and Edge, are CI's.

2026-10-12, with the seventeenth Windows run's answers (`verification/2026-10-11-windows-17/report.md`,
Part A and Part B's b4): the engine follows Excel, as SH-68's note and ADR-0047's of 2026-10-10 said it
would once a run had read it.

- **Nesting.** Excel passed over, or counted, every form asked: a call inside a larger Formula
  (SUBTOTAL-020, AGGREGATE-026), an `AGGREGATE` in `SUBTOTAL`'s range (SUBTOTAL-021), a call in
  parentheses, under `IF` or plus 0 (SUBTOTAL-035 to 037, AGGREGATE-052 to 054), and a cell spilled from
  a Formula that calls `SUBTOTAL` (SUBTOTAL-038, 0). `AGGREGATE`'s options 4 to 7 counted the nested
  cell's Value, in the reference form and the array form (AGGREGATE-025, 042 to 051). So a cell is nested
  when its Formula's text calls either function anywhere (`IsNested`), and no `#VALUE!` for nesting
  remains. Whether a call in a branch `IF` never takes counts is asked next (SUBTOTAL-039).
- **Function numbers.** `SUBTOTAL(9.5,…)` is 303 in Excel (SUBTOTAL-027): a function number that is
  not whole is truncated, and the range checked after. `AGGREGATE`'s function number and option are
  truncated the same way, as LibreOffice does (AGGREGATE-055 and 056 ask Excel).
- **Arguments that take only a Reference.** Excel refused on entry `SUBTOTAL(9,SEQUENCE(3))`,
  `SUBTOTAL(9,5)`, `RANK.EQ(1,1)` and `AGGREGATE(14,6,A1:A11,1,2)`, and entered
  `AGGREGATE(9,6,A1:A11*1)`, `AGGREGATE(14,6,5,1)` and a number as `AGGREGATE`'s fourth argument. The
  parser now refuses, as it refuses an argument count, a constant, an operator's result or a call of a
  function that never gives a Reference where `FunctionDefinition.ReferenceOnly` says only a Reference
  is taken; `INDIRECT`, `OFFSET`, `INDEX`, `CHOOSE`, `IF`, `IFS`, `SWITCH`, `XLOOKUP` and `LET` are
  taken (`ReturnsReference`). AGGREGATE-057, 058 and SUBTOTAL-040 to 044 ask the forms not yet seen.
- **The array form over a number.** `AGGREGATE(14,6,5,1)` is 5 (AGGREGATE-039): a number given directly
  is an array of that one Value. Text and a boolean are not asked yet, and stay `#VALUE!` (AGGREGATE-061).
- **k.** `AGGREGATE(14,6,A1:A11,1.5)` is 81 and `LARGE(A1:B5,1.5)` 6: the 2nd largest, so k is rounded
  up, as ODF 1.3, LibreOffice and Gnumeric all have `LARGE` do. `SMALL`'s k, where LibreOffice rounds
  down and Gnumeric up, stays `#VALUE!` until asked (SMALL-004, 005, AGGREGATE-060).
- **The unsupported function numbers** 13 and 16 to 19 stay `#VALUE!` by decision; AGGREGATE-027 to 031
  now carry Excel's answers as `excelExpect` (`#N/A`, 34, 32, 34, 31.5), observed.
- **Completion** (Part B's b4): `SUBTOTAL`'s list now reads `STDEV.S`, `STDEV.P`, `VAR.S` and `VAR.P`
  for 7, 8, 10, 11 and 107, 108, 110, 111, as Excel's does; `AGGREGATE`'s fourteen numbers and eight
  options already matched Excel's texts. Excel's list also offers 13 and 16 to 19, which completion
  leaves out because the engine refuses them. `CompletionTriggerTests` pins every text.

The new `uncertain` cases need an `oracle.ps1 -Area` run over `subtotal`, `aggregate`, `large`, `small`
and `mod` before the tag (ADR-0208).

2026-10-12, after review: the Status is `ready-for-human` again, with an open box for that run. The ticket
had been closed with its own feature's questions open, SUBTOTAL-039 to 044 and AGGREGATE-055 to 061, which
ADR-0208 does not allow.

2026-10-12, with the area pass (`verification/2026-10-12-windows-area/report.md`, `oracle.ps1 -Area` over
30 files, ADR-0208's remedy): SUBTOTAL-039 to 044 and AGGREGATE-055 to 059 agreed and are `observed`. Two
disagreed, and the engine now follows Excel (ADR-0047, ADR-0202's rule for a disagreement):

- **AGGREGATE-060.** `AGGREGATE(15,6,A1:A11,1.2)` is 30, the smallest: `SMALL`'s k that is not whole is
  truncated, as `SMALL`'s own is (SMALL-004 and 005: 1.5 and 1.2 are each the smallest). `LARGE`'s is
  rounded up, as before, and the two are one rule, the k-th largest with k rounded up being the
  (n+1-k)-th smallest with that index truncated. A k below 1 is `#NUM!` for both, whole or not
  (LARGE-011: 0.5 is `#NUM!`, not rounded up to 1). `KthOf` reads k so for `LARGE`, `SMALL` and
  `AGGREGATE`'s 14 and 15 alike, and no k is `#VALUE!` any more.
- **AGGREGATE-061.** `AGGREGATE(14,6,"5",1)` is `#NUM!`, not `#VALUE!` and not 5. A Value given directly
  to the array form is an array of that one Value, as the number 5 was (AGGREGATE-039), and text in it
  is no number and is not coerced. So `TryCollect` takes text and a boolean given directly as it takes a
  number, and `Reduce` passes over them: `LARGE` and `SMALL` find no number. An Error Value given
  directly is still the result, as an Error Value argument is (AGGREGATE-067 asks whether Excel's
  option 6 passes over it instead).

`KthAndModTests` and `SubtotalTests` pin both, each test named for ADR-0047. AGGREGATE-062 to 067 are new
`uncertain` cases the answers lead to, with the engine's answers, for the box above: `AGGREGATE(15,6,…,0.5)`
and `AGGREGATE(14,6,…,0.5)` (`#NUM!`), `AGGREGATE(14,6,TRUE,1)` and `AGGREGATE(15,6,"abc",1)` (`#NUM!`),
`AGGREGATE(15,6,A1:A11,8.9)` (81, the 8th smallest) and `AGGREGATE(14,6,1/0,1)` (`#DIV/0!`). The Status stays
`ready-for-human` until a pass asks them. The Change Notes are
`docs/releases/unreleased/area-kth-and-mod-small-k.md` and `area-kth-and-mod-aggregate-text.md`; the
Formulas page's sentence, ADR-0047's note and the README's are proposed in
`docs/agents/proposals/area-corrections-apply.md`.

2026-10-12, with the second area pass (`verification/2026-10-12-windows-area-2/report.md`, Excel
16.0.20430.20146): AGGREGATE-062 to 067 agreed and are `observed` — `AGGREGATE`'s 14 and 15 with a k of
0.5 are `#NUM!`, a boolean and text given directly hold no number (`#NUM!`), `AGGREGATE(15,6,A1:A11,8.9)` is
81, the 8th smallest, and an Error Value given directly is the result (`#DIV/0!`), not passed over by
option 6. `SMALL`'s own SMALL-008 disagreed: Excel's `SMALL(A1:A9,9.5)` over nine numbers is `#NUM!`, not
the 9th smallest, 7, so a k above the count is `#NUM!` before it is truncated, as one below 1 is. `KthOf`
now checks k against 1 and the count before rounding it, for `LARGE`, `SMALL` and `AGGREGATE`'s 14 and 15
alike; `LARGE`'s answers do not move, since a k rounded up passes the count exactly where it is above it
(LARGE-010). Every observed `SMALL`, `LARGE` and `AGGREGATE` 14 and 15 case agrees with the rule. AGGREGATE-068
asks it of `AGGREGATE`'s 15 (`AGGREGATE(15,6,A1:A11,9.5)`, `#NUM!`), for the box above, and SMALL-011 of
`SMALL` at 9.2, which rounded to nearest would be 9. The Status stays `ready-for-human` until a pass asks
them. The Change Note is `docs/releases/unreleased/area-kth-and-mod-small-k.md`; ADR-0047's note, the
README's and the Formulas page's sentences are proposed in `docs/agents/proposals/area-corrections-apply.md`.

2026-10-18: done. The oracle run of 2026-10-18 (`ExcelOracle/results-2026-10-18-rc1-com-en-US.json`,
Excel 16.0.20430.20182) asked AGGREGATE-069: Excel's `AGGREGATE(9,6,B1:B2)` over a cell ExSheet refuses is 7,
passing over Excel's own answer for MROUND, where the engine refuses the Formula by decision (ADR-0047,
2026-10-12). The case is `observed` with Excel's answer as `excelExpect`. No case of this ticket's files is open.
