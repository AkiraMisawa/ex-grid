# Run 17 corrections: trigonometry — proposals for the orchestrator

Branch `claude/run17-trigonometry`. Cases: SIN-004, COS-004, TAN-004 (research, then follow), COTH-002,
CSCH-002, SECH-003 (follow). What the branch changed is in its commits; what it may not change itself
(ADR-0047, the engine's README, the catalogue, ticket 07) is below, written to be pasted.

## What the run said, and what the branch did

- **SIN-004, COS-004, TAN-004 — followed.** Excel reduces an angle by pi rounded to the x87's 64-bit
  significand (`FLDPI`, `0xC90FDAA22168C235 × 2^-62`), not by `PI()`'s double: `SIN(PI())` is exactly
  the difference between the two, 565 × 2^-62 = 1.22514845490862E-16. SIN and COS are the doubles nearest
  the sine and cosine of the exactly reduced angle; TAN the double nearest their quotient; COT, CSC and SEC
  1 over TAN, SIN and COS, divided as doubles. That computation reproduces **all 19** numbers Excel has
  returned for the six functions in every results file (the agreeing cases too), and no other candidate
  tried with a single rounding does (table below). The engine computes it in double-double arithmetic
  (`Functions.Trigonometry.cs`), with no call to the platform's `Math.Sin`, so every runtime gives the
  same doubles; a line-for-line Python port agrees with 120-digit arithmetic on 20000 seeded arguments,
  and the C# engine agreed with it on 15000 random formulas in a scratch run.
- **Tightened to Excel's full doubles**, every one met: SIN-002, SIN-003, SIN-006, COS-001, COS-002,
  COS-003, TAN-001, TAN-002, TAN-003, COT-001, COT-002, CSC-001, SEC-001, SEC-002.
- **COTH-002, CSCH-002, SECH-003 — followed.** Excel answers past the 2^27 that Microsoft documents for
  the three: `COTH(2^27)` 1, `CSCH(2^27)` and `SECH(2^27)` 0. The limit is dropped for these three only;
  SIN, COS, TAN, COT, CSC and SEC keep `#NUM!` from 2^27, which the run observed for all six.
- **New `uncertain` cases for the next Windows run** (the engine's answer): TAN-006 `=TAN(1)`, TAN-007
  `=TAN(9)`, COT-006 `=COT(1)`, COT-007 `=COT(4)`, COT-008 `=COT(9)`, SEC-005 `=SEC(1)` tell apart the
  constructions of TAN, COT and SEC that all reproduce the observations; SIN-008 `=SIN(2*PI())` and
  COS-006 `=COS(100000000)` see the reduction at another quadrant and at a large multiple; COTH-004 to 006
  (`=COTH(0.5)`, `=COTH(1)`, `=COTH(5)`, to 12 digits) ask COTH's full doubles (below, "Found on the way").
- Layer-1 tests: `TrigonometryReductionTests.cs` (73 tests, each named with ADR-0047). The experiment:
  `spikes/exsheet-trig-reduction/` (`compare.py`, `README.md`, `results.txt`).

## Every candidate tried, and how many of Excel's 19 numbers it reproduces

The 19: SIN-001 to 004 and 006, COS-001 to 004, TAN-001 to 004, COT-001, COT-002, CSC-001, SEC-001,
SEC-002, SEC-004 (`results-2026-10-11-run17*.json`; no earlier results file holds a number for the six).

| Candidate | Reproduces | Misses |
|---|---:|---|
| `Math.Sin`/`Cos`/`Tan` of the angle, 1 over them (the engine before) | 15 | SIN-004, SIN-006, COS-004, TAN-004 |
| The true pi, every function correctly rounded | 14 | SIN-004, SIN-006, COS-004, TAN-004, CSC-001 |
| x87 pi, exact reduction, every function correctly rounded | 18 | CSC-001 |
| **x87 pi, exact reduction, SIN/COS/TAN correctly rounded, COT/CSC/SEC 1 over them (implemented)** | **19** | |
| x87 pi, exact reduction, SIN/COS as doubles, TAN = SIN/COS, COT = COS/SIN, CSC and SEC 1 over | 19 | |
| x87 pi, exact reduction, TAN = tan(r) as a double (−1 over it in odd quadrants), COT = 1/TAN | 19 | |
| x87 pi, exact reduction, the remainder rounded to a double, then libm | 17 | COS-001, CSC-001 |
| x87 pi, exact reduction, the remainder as a double-double, then libm with a first-order correction | 17 | COS-001, CSC-001 |
| x87 pi, each reduction step rounded to 64 bits (an x87 without `FPREM`), correctly rounded | 17 | SIN-006, CSC-001 |
| x87 pi, each reduction step rounded to 64 bits, SIN/COS doubles | 18 | SIN-006 |
| `FSIN`'s own 66-bit pi (`0xC90FDAA22168C234C × 2^-66`), correctly rounded | 14 | SIN-004, SIN-006, COS-004, TAN-004, CSC-001 |
| `PI()`'s double, exact reduction, correctly rounded | 8 | eleven |
| `PI()`'s double, double arithmetic, libm | 10 | nine |

Per function, with the reduction fixed (x87 pi, exact): TAN 4/4 for each of the double nearest tan,
SIN/COS as doubles divided, and tan(r) with −1 over it; COT 2/2 for 1/TAN (TAN nearest), 1/TAN (tan(r)),
the double nearest cot, and COS/SIN as doubles, and 1/2 for 1/TAN with TAN as SIN/COS doubles (misses
COT-001); CSC 1/1 for 1/SIN as doubles, 0/1 for the double nearest csc; SEC 3/3 for both 1/COS and the
double nearest sec. The engine takes the construction that rounds least (TAN once, each reciprocal once
more), the one CSC-001 shows for CSC; the new `uncertain` cases above settle the rest.

## Proposed: ADR-0047, a note after the paragraph "*(2026-10-03)* Admitted as the catalogue's ticket 07"

> *(2026-10-12)* The seventeenth Windows run asked ticket 07's open cases. `SIN`, `COS` and `TAN` give
> `#NUM!` from 2^27, as `COT`, `CSC` and `SEC` are documented to (SIN-005, COS-005, TAN-005), so the
> refusal is Excel's answer; `LOG` to base 1, and `CSC`, `COTH` and `CSCH` at 0, are `#DIV/0!`, and
> `ACOTH` of an absolute value not above 1 is `#NUM!`, the answers the engine gave. `COTH`, `CSCH` and `SECH` are not limited: Excel answers `COTH(2^27)` with 1 and `CSCH` and
> `SECH` of it with 0, past the limit Microsoft documents for them, so the engine answers them too. And
> Excel reduces an angle by pi rounded to the x87's 64-bit significand, not by `PI()`'s double:
> `SIN(PI())` is the difference between the two, 1.22514845490862E-16, where the double's own sine is
> 1.2246467991473532E-16. `SIN`, `COS` and `TAN` are the doubles nearest the exact values of the angle so
> reduced, and `COT`, `CSC` and `SEC` 1 over them, divided as doubles; that reproduces every number Excel
> returned for the six, and the engine computes it without the platform's math library, so every runtime
> gives the same doubles. `spikes/exsheet-trig-reduction/` records each candidate and what it reproduced.
> More than one construction of `TAN`, `COT` and `SEC` fits every observation; the engine takes the one
> that rounds least, and TAN-006 and 007, COT-006 to 008 and SEC-005 ask the next run which is Excel's.

## Proposed: `dotnet/src/ExSheet.Engine/README.md`

In "**Asked of Excel before they are answered**", remove `` `SIN`, `COS` and `TAN` of 2^27 or more
(`#NUM!`, the limit Microsoft documents for `COT` and its kin); `` and `` `LOG` to base 1, and `CSC`,
`COTH` and `CSCH` at 0 (`#DIV/0!`); `` and `` `ACOTH` of an absolute value not above 1 (`#NUM!`; the
page names `#VALUE!` too); `` — all observed by the seventeenth run as the engine answers them (ACOTH-003
too; the branch updated `Acoth`'s own comment to say so).

Then, as a bullet of the list "Where the engine cannot give Excel's answer" or beside the function list:

> - **`SIN`, `COS` and `TAN` reduce an angle by Excel's pi** (ADR-0047, 2026-10-12): pi rounded to the
>   x87's 64 bits, so `SIN(PI())` is 1.22514845490862E-16, as Excel's is, and not the sine of the double
>   `PI()` gives. Each is the double nearest the exact value; `COT`, `CSC` and `SEC` are 1 over them.
>   From 2^27 the six are `#NUM!`, as Excel's; `COTH`, `CSCH` and `SECH` answer past it, as Excel's do.

## Proposed: `docs/specs/exsheet-functions/spec.md`, "### Trigonometry"

Replace the paragraph under the heading:

> Angles are in radians, reduced by pi as Excel holds it, to 64 bits (ADR-0047, 2026-10-12). Microsoft
> documents a limit of 2^27 on the argument of `COT`, `CSC`, `SEC`, `COTH`, `CSCH` and `SECH`; Excel
> applies it to `SIN`, `COS`, `TAN`, `COT`, `CSC` and `SEC` (`#NUM!`) and answers `COTH`, `CSCH` and
> `SECH` past it, as the engine does (ticket 07; observed 2026-10-11).

and the rows: `SIN` — `#NUM!` from 2^27, as Excel's; `CSC` — At 0, `#DIV/0!`, as Excel's; `COTH` —
At 0, `#DIV/0!`, as Excel's; no limit at 2^27, as Excel's; `CSCH` — As `COTH`; `SECH` — No limit at 2^27,
as Excel's; `ACOTH` — An absolute value not above 1 is `#NUM!`, as Excel's (the documentation names
`#VALUE!` as well). Item 6 of the queue may add: "The seventeenth Windows run answered them (2026-10-11)."

*(Added after review, 2026-10-12.)* The `LOG` row (the table of the section before "### Trigonometry") still says "base 1 is `#DIV/0!` until
Excel is asked", the one stale "until Excel is asked" ticket 07 would otherwise leave. Replace its Notes

> Base 10 when left out, through `LOG10`'s logarithm; base 1 is `#DIV/0!` until Excel is asked

with

> Base 10 when left out, through `LOG10`'s logarithm; base 1 is `#DIV/0!`, as Excel's (the seventeenth run, LOG-007)

## Proposed: ticket 07 (`docs/specs/exsheet-functions/issues/07-log-and-trigonometry.md`)

Tick the box "The next Windows run asks Excel every `uncertain` case" (the seventeenth run did; every
case it names agreed except SIN-004 and COS-004, now followed), and add under Comments:

> 2026-10-12: the seventeenth Windows run answered. `SIN`, `COS` and `TAN` refuse 2^27 as `COT` does;
> `CSC`, `COTH`, `CSCH` at 0 and `LOG` to base 1 are `#DIV/0!`; `ACOTH` not above 1 is `#NUM!`. Excel
> reduces an angle by the x87's 64-bit pi, which the engine now does (`SIN(PI())`
> 1.22514845490862E-16), and answers `COTH`, `CSCH` and `SECH` past 2^27. TAN-006 and 007, COT-006 to 008, SEC-005, SIN-008, COS-006 and COTH-004 to 006 are
> new `uncertain` cases for the next run.

## Corrected after review (2026-10-12)

- The Change Note `run17-trigonometry-reduction.md` said the six "now give Excel's doubles to the last bit, on every
  runtime". Several constructions of `TAN`, `COT` and `SEC` fit all 19 observations and part at `TAN(1)`, `TAN(9)`,
  `COT(1)`, `COT(4)`, `COT(9)` and `SEC(1)`, which is why those are `uncertain`. It now says they "reproduce, to the
  last bit, every double Excel was observed to return for them, and give the same doubles on every runtime".
  `TrigonometryReductionTests`' summary made the same claim and says the same now.
- The catalogue's `LOG` row had no edit (above, under the catalogue).

## Docs Site

Nothing to change: no page states the 2^27 limit or the trigonometric values. `/exsheet/functions` takes
each function's description from `DeclaredFunction.All`, which did not change, and
`/exsheet/formulas#supported-functions` names no trigonometric refusal. Change Notes:
`docs/releases/unreleased/run17-trigonometry-reduction.md` and
`run17-trigonometry-hyperbolic-limit.md` (ExSheet, Behaviour, ADR 0047).

## Found on the way, not changed

- **COTH-001's full double.** Excel's `COTH(2)` is 1.0373147207275479; the engine's 1/TANH gives
  1.0373147207275482 on glibc (the case compares 3 digits, and the two agree to 16). COSH/SINH gives
  Excel's double on glibc, and so does (e^x + e^−x)/(e^x − e^−x); 1/TANH and (e^2x + 1)/(e^2x − 1) do
  not. One number, from libm, is not enough to choose, and COSH/SINH alone would be `#NUM!` at 2^27 where
  Excel gives 1, so nothing changed. COTH-004 to 006 ask Excel's full doubles at 0.5, 1 and 5, where the
  forms part (on glibc: 2.163953413738653 / 2.1639534137386525, 1.3130352854993315 / 1.3130352854993312,
  1.0000908039820193 / 1.0000908039820195, 1/TANH first).
- CSCH-001 (0.46964244059522464), SECH-001 and SECH-002 (scaled) are met to the last bit by the engine on
  glibc, but through libm, so their cases keep their digit limits, as the corpus does for EXP, LN and
  TANH.

Nothing stopped the work; no question for the user.
