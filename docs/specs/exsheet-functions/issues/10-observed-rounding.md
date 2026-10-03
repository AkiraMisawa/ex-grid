# 10: MROUND, CEILING.MATH and FLOOR.MATH

Status: done

**What to build:** Admit observed multiple rounding under ADR-0047 without a guessed epsilon.

**Blocked by:** nothing within the admitted domain; broader decimal rounding remains open.

- [x] Import all 60 observations, including signs, zero, text, booleans and negative modes.
- [x] MROUND admits integer multiples; same-sign nonzero fractional multiples give `#VALUE!`.
- [x] Keep Excel's decimal-midpoint answers beside explicit refusals.
- [x] CEILING.MATH and FLOOR.MATH implement the observed directional rounding modes.
- [x] Refuse unobserved almost-integral division boundaries and nonzero quotient underflow.
- [x] Record domain boundaries in ADR-0047 and the engine README.

## Comments

2026-10-03: implemented in `Functions.ObservedRounding.cs`, verified by
`ObservedRoundingTests`. MROUND's four observed decimal midpoints do not admit a uniform
binary rounding algorithm. Widening the domain requires a general matching algorithm;
a per-case answer table or tolerance chosen to pass these cases is not acceptable.
