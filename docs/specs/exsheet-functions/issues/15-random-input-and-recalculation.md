# 15: Decide reproducible random input and recalculation

Status: needs-info

**What to decide:** The random input used by RAND/RANDBETWEEN, its recalculation identity,
Consumer control, and persistence/undo behavior.

**Blocked by:** an ADR agreed with the user. The lifecycle observations are complete.

- [x] Retain the October 3 repeated calculation, dependency and lifecycle observations.
- [ ] Decide who supplies randomness and how the same Sheet state can be reproduced.
- [ ] Decide when random results change and how dependents recalculate deterministically.
- [ ] Decide what a Sheet Document and undo/redo preserve.
- [ ] Record the decision before introducing hidden random state or admitting the functions.

## Comments

2026-10-03: both functions remain Decide and undeclared. A generic RNG in the evaluator would
silently choose lifecycle and persistence semantics. The observations constrain a future ADR;
they do not authorize timing-dependent or unreproducible behavior.
