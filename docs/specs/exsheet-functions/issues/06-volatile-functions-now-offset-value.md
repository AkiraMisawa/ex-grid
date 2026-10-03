# 06: Volatile functions, NOW, OFFSET and VALUE

Status: done

**What to build:** ADR-0124. A Formula that calls a volatile function is recalculated in every
recalculation; a Reference computed while it is evaluated reads the cell as this recalculation
leaves it, computing a cell that is still to compute first; `NOW` reads the moment the Sheet's host
gives, and ExSheet moves it on each minute. `OFFSET` and `NOW` are the volatile functions admitted;
`VALUE` reads text under the Sheet's culture.

**Blocked by:** ADR-0124 (decided with the user, 2026-10-03)

- [x] Engine: `Sheet.NowSource`, `HasVolatileFormulas`, `RecalculateVolatile`; volatile Formulas
      join every recalculation; on-demand computation of a pending cell, a cycle being `#CIRC!` (SH-59)
- [x] `OFFSET`, `NOW`, `VALUE`; `NOW` gives a General cell the short date and time format (FF-039)
- [x] ExSheet: the moment in the Sheet's zone; a minute timer on the registered `TimeProvider` (SH-60)
- [x] Layer 3: `NOW()` in two browser zones 25 hours apart, on both hosts
- [x] A dollar amount in text reads as it does typed, for `VALUE` and the operators alike
      (`="$1,000"+0` is 1000, as in Excel; TYPED-021's reading)
- [ ] The next Windows run asks the `uncertain` cases (`OFFSET` with a size of 0, `VALUE` of a blank)
