# 03: References, the dependency graph, and Error Values

Status: done

**What to build:** The whole Reference grammar and a recalculation that is incremental and never shows a half
result. That covers `$A$1`, `A$1`, `A1:B2`, `A:A`, `1:1`, the Sheet qualifier (parsed and
recorded, one Sheet), operators with Excel's precedence, and `&`. Only dependents recompute. A
cycle is `#CIRC!` in every member and every dependent. Error Values propagate.

**Blocked by:** 02

- [x] Every Reference form parses, and the invariant syntax round-trips (ADR-0047)
- [x] Only dependents of a change recompute (layer 1, counted)
- [x] A cycle shows `#CIRC!` in each member and each dependent; breaking it recovers (ADR-0047)
- [x] `#DIV/0!`, `#VALUE!` and the rest propagate as Excel's do
- [ ] No Window is pushed with Values from an unfinished recalculation (layer 2)

## Comments

2026-09-27, engine half: the parser reads Excel's invariant grammar with its precedence
(negation, `%`, `^` left-associative, `* /`, `+ -`, `&`, comparisons), text, boolean and Error
Value literals, `A1`, `$A$1`, `A$1`, `A1:B2`, `A:A`, `1:1`, a Sheet qualifier (quoted or bare,
recorded), `Table[Column]` structured references and function calls, and writes each back in
one spelling (`FormulaGrammarTests`). The dependency graph recomputes only what a change
reaches, in dependency order, counted through `SheetChange.Recalculated`
(`RecalculationTests`); every member of a cycle and every cell downstream of one is `#CIRC!`,
and breaking the cycle recovers. Error Values propagate left to right (`ErrorPropagationTests`).
Values are staged and published only when the recalculation has completed. **What remains is
the component's:** the layer 2 test that no Window is pushed with Values from an unfinished
recalculation. Three behaviours rest on choices the ADRs do not make, and are reported for a
decision rather than taken as settled: a Sheet-qualified Reference evaluates to `#REF!` (a Sheet
has no name for a qualifier to match); a Formula whose result is a multi-cell range, or that
applies an operator to one, is `#VALUE!` (ExSheet has no spilled arrays); and a structured
reference is `#NAME?` until Linked Tables exist (ticket 16).
