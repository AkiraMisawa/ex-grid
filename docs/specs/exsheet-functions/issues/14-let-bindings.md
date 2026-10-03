# 14: Decide LET bindings before implementation

Status: needs-info

**What to decide:** LET's Formula-local names, lexical scope, binding and entry grammar.

**Blocked by:** an ADR agreed with the user. Observation alone does not settle the engine model.

- [x] Retain October 3 COM observations and the independent real-key LET acceptance cases.
- [ ] Decide name lookup/shadowing and how binding interacts with References and Linked Tables.
- [ ] Decide entry acceptance and lazy evaluation against the observed cases.
- [ ] Define dependency, cycle and `#GETTING_DATA` propagation under ADR-0047's invariants.
- [ ] Record the decision before changing parser grammar or admitting LET.

## Comments

2026-10-03: remains Decide in the catalogue and undeclared in the engine. Implementing a new
name scope implicitly would make an architectural decision in code. The existing verification
record supplies evidence for the decision and must not be mistaken for that decision itself.
