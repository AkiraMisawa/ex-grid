# 07: LOG and the trigonometric functions

Status: done

**What to build:** `LOG` and the trigonometric and hyperbolic functions under ADR-0047's rule:
`SIN`, `COS`, `TAN`, `COT`, `CSC`, `SEC`, `ASIN`, `ACOS`, `ATAN`, `ATAN2`, `ACOT`, `SINH`, `COSH`,
`TANH`, `COTH`, `CSCH`, `SECH`, `ASINH`, `ACOSH`, `ATANH`, `ACOTH`, `DEGREES` and `RADIANS`. None of
them was in the catalogue before; Microsoft's pages pin their examples and most of their Error Values,
and what they leave open is refused with an Error Value until Excel is asked (decided with the user,
2026-10-03).

**Blocked by:** nothing

- [x] Each function's cases are in `ExcelCases/<function>.json`, Microsoft's examples as `documented`
- [x] Catalogue rows Supported; ADR-0047's additions name them; the engine's README lists them
- [x] The next Windows run asks Excel every `uncertain` case, first:
  - whether `SIN`, `COS` and `TAN` refuse an argument of 2^27 with `#NUM!`, as `COT`, `CSC` and
    `SEC` are documented to (SIN-005, SIN-006, COS-005, TAN-005);
  - what `ACOTH` gives below an absolute value of 1, where the page names both `#NUM!` and
    `#VALUE!` (ACOTH-003);
  - `LOG` to base 1, and `CSC`, `COTH` and `CSCH` at 0 (LOG-007, CSC-003, COTH-003, CSCH-003);
  - the double's remainders, `SIN(PI())` and `COS(PI()/2)`, to the last bit (SIN-004, COS-004).
  *Answered by the seventeenth Windows run (comment of 2026-10-12).*
- [x] `oracle.ps1 -Area tan,cot,sec,sin,cos,coth` before the tag (ADR-0208): the `uncertain` cases the
      seventeenth run's answers led to, TAN-006 and 007, COT-006 to 008, SEC-005, SIN-008, COS-006 and
      COTH-004 to 006. The engine agrees with each answer, or is corrected to it, before this ticket is
      done. *Each is `observed`: the release gate lists none of them open on 2026-10-18*

## Comments

2026-10-03: admitted. The 2^27 limit that Microsoft documents for `COT`, `CSC`, `SEC`, `COTH`, `CSCH`
and `SECH` is applied to `SIN`, `COS` and `TAN` too: a refusal is always admissible under ADR-0047,
an answer Excel would not give is not. `LOG` to base 10 goes through the same logarithm as `LOG10`,
so `LOG(1000)` is 3 and not the 2.9999999999999996 that ln(1000)/ln(10) gives.

2026-10-12: the seventeenth Windows run answered. `SIN`, `COS` and `TAN` refuse 2^27 as `COT` does;
`CSC`, `COTH`, `CSCH` at 0 and `LOG` to base 1 are `#DIV/0!`; `ACOTH` not above 1 is `#NUM!`. Excel
reduces an angle by the x87's 64-bit pi, which the engine now does (`SIN(PI())`
1.22514845490862E-16), and answers `COTH`, `CSCH` and `SECH` past 2^27. TAN-006 and 007, COT-006 to
008, SEC-005, SIN-008, COS-006 and COTH-004 to 006 are new `uncertain` cases; ADR-0208 does not close
a ticket on "the next Windows run asks", so the Status is `ready-for-human` until an `oracle.ps1 -Area`
run has asked them.

2026-10-18: done. TAN-006 and 007, COT-006 to 008, SEC-005, SIN-008, COS-006 and COTH-004 to 006 are all
`observed`, and the release gate (`ExGrid.Release gate`) lists no case of this ticket's files open; the
oracle run of 2026-10-18 asked the gate's last 35 cases, none of them here.
