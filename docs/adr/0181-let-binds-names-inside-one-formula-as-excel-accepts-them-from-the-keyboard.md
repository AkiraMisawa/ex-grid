# LET binds names inside one Formula, as Excel accepts them from the keyboard

*(Decided with the user on 2026-10-10, from the function catalogue's Decide list,
`docs/specs/exsheet-functions/spec.md`, ticket 14. What Excel was observed to do is in
`verification/2026-10-03-windows-functions/research.md`, "LET".)*

`LET(name1, value1, …, calculation)` gives a value a name for the rest of one Formula. ExSheet's
Formulas have had no names of their own. A bare name is `#NAME?`
([ADR-0047](./0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md)), and a
Formula reads nothing but its References and a Linked Table's `Table[Column]`. A name inside a
Formula is a scope that the grammar, the evaluator and the dependency graph all have to know. That
makes `LET` a decision, not one more function on the list.

What Excel was observed to do, through COM and by real keys (LET-001 to LET-031, LET-KEYS-*,
LET-ADDITIONAL-* and the FOLLOWUP cases):

- **Bindings are sequential and lexical.**
  - `LET(x,2,LET(x,3,x)+x)` is 5.
  - A binding does not escape its `LET`: `LET(x,1,1)+x` is `#NAME?`.
  - A name read before its binding, or inside its own value, is `#NAME?`, not a cycle.
- **Names.**
  - They are compared without regard to case.
  - Accepted from the keyboard: `_x`, `c`, `r`, `名前` and `é`.
  - Refused: `A1`, `a.b`, `1`, a name bound twice, a name of 250 characters, and a 127th pair.
  - The sources disagree. COM accepted `A1`, which typed entry refuses. Microsoft's page calls `c`
    invalid, which typed entry accepts.
- **Values.**
  - A binding is one Value however often it is read: `LET(x,RAND(),x-x)` was 0 in every sample.
  - An unused binding that holds an Error Value does not reach the result: `LET(x,NA(),1)` is 1.
- **Arrays and change.** A binding may hold a range or an array, which spills as any other array does.
  A change to a cell a binding reads recalculates the `LET`.

## The decision

**ExSheet admits `LET` as Excel's keyboard accepts it, and refuses on entry what Excel was not seen to
accept.** Where the keyboard disagrees with COM or with Microsoft's page, the keyboard wins, because
ExSheet's Formulas are typed (ADR-0047, "Observed in Excel").

- **Scope.**
  - A name is seen by the values bound after it and by its own `LET`'s calculation, and nowhere
    else.
  - An inner `LET` may bind the same name again for its own arguments.
  - A name read before its binding, or inside its own value, is `#NAME?`, as in Excel.
- **Names.**
  - A name is compared without regard to case.
  - It starts with a letter or `_`, and goes on with letters, digits and `_`. A letter is any
    script's, as `名前` and `é` were.
  - It is at most 249 characters, and a `LET` holds at most 126 pairs.
- **A name that could mean something else is refused**, until a Windows run shows that Excel accepts
  it:
  - a name that reads as a Reference: `A1`, `XFD1048576`, and the R1C1 shapes `R1C1`, `R2`, `C3`;
  - the name of a function;
  - `TRUE` and `FALSE`.

  `c` and `r` alone are names, as observed.
- **A name never hides a Linked Table.** A binding names bare names only, so `Table[Column]` inside a
  `LET` always reads the Linked Table, whatever the `LET` binds
  ([ADR-0049](./0049-linked-tables-are-the-consumers-data-read-by-key.md)).
- **Each binding is computed at most once, when its name is first read.** Every read gives that one
  Value. A binding that is never read is never computed, so its Error Value does not reach the
  result. A `RAND` bound once is one draw
  ([ADR-0180](./0180-rand-and-randbetween-draw-from-the-sheet-seed-and-draw-again-at-each-change.md)).
- **The dependency graph reads the text, bindings included.** A `LET` depends on every cell and
  Linked Table its text names, whether or not the binding that names it is read.
  - A cycle through an unused binding is `#CIRC!`.
  - An unused binding over a waiting Linked Table waits with `#GETTING_DATA`.

  This is the strict side that ADR-0047 and ADR-0049 already take for a branch not taken.
- **A malformed `LET` is refused on entry**, as any Formula ExSheet cannot read is. Excel refuses
  each of these with a dialog:
  - an even number of arguments, or a missing calculation;
  - a name bound twice in one `LET`;
  - a refused name;
  - a 127th pair.
- **Arrays.** A binding may hold a range or an array, and a calculation whose result is an array
  spills ([ADR-0125](./0125-a-formula-whose-result-is-an-array-spills-as-excel-365s-does.md)).

## What differs from Excel, by decision

- **Unobserved names are refused.** Excel may accept a name it was not seen to accept: a function's
  name, `TRUE`, an R1C1-shaped name. Each is refused until it is observed.
- **An unused binding can still make the `LET` an Error Value.** If it reads a cycle, the `LET` is
  `#CIRC!`; if it reads a waiting Linked Table, the `LET` is `#GETTING_DATA`. Excel shows neither as a
  Value, and was not asked.

## Considered options

- **Microsoft's documented name rules.** Rejected. They call `c` invalid, which Excel's keyboard
  accepts, and do not refuse `A1`, which it refuses. ADR-0047 takes what Excel does.
- **Computing every binding eagerly, in order.** It gives the same Values, since an unused binding's
  Error Value would be dropped anyway, and costs more. The engine computes on first read.
- **Only the bindings the calculation reads count for `#CIRC!` and `#GETTING_DATA`.** Rejected. A
  binding unused today is used after the next edit, and the strict rule is ADR-0047's for a branch
  not taken.
- **A binding may shadow a Linked Table's name.** Rejected. A table may be declared after the Formula
  is written (ADR-0049), so what `Positions[PV]` read would turn on the order things happened in.

## Consequences

- `LET` becomes Ready in the catalogue, and ticket 14 builds it.
- Completion
  ([ADR-0051](./0051-formula-entry-completion-point-mode-and-the-formula-bar.md)) offers no `LET`
  name in this version. Whether it should is a later decision, once Excel has been observed.
- No glossary term: a `LET` name is Excel's word, used as Excel uses it.
