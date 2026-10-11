# 14: LET, as Excel's keyboard accepts it

Status: done

**What to build:** `LET` under
[ADR-0181](../../../adr/0181-let-binds-names-inside-one-formula-as-excel-accepts-them-from-the-keyboard.md).
Its names are bound inside one Formula, sequentially and lexically, and compared without regard to
case. Only the names Excel's keyboard accepted are allowed, with the rest refused on entry. Each
binding is computed at most once, when first read. Dependencies follow the text, bindings included.
Ranges and arrays may be bound, and spill.

**Blocked by:** none. ADR-0125's arrays are in.

*(Until 2026-10-10 this ticket was "Decide LET bindings before implementation", `needs-info`. The
decision is ADR-0181, and the boxes below it are the build.)*

- [x] Retain October 3 COM observations and the independent real-key LET acceptance cases.
- [x] Decide name lookup/shadowing and how binding interacts with References and Linked Tables (ADR-0181)
- [x] Decide entry acceptance and lazy evaluation against the observed cases (ADR-0181)
- [x] Define dependency, cycle and `#GETTING_DATA` propagation under ADR-0047's invariants (ADR-0181)
- [x] Record the decision before changing parser grammar or admitting LET (ADR-0181)
- [x] The grammar: `LET`'s names in a Formula's text. What SH-67 lists is refused on entry, as a
      Formula that cannot be read is refused
- [x] The evaluator: a scope per `LET`, sequential bindings and shadowing. A name read before its
      binding, or inside its own value, is `#NAME?`. Each binding is computed at most once, when
      first read, and reused
- [x] The dependency graph: everything a `LET`'s text names, used or not. A cycle through an unused
      binding is `#CIRC!`, and a waiting Linked Table behind one gives `#GETTING_DATA`
- [x] `Table[Column]` inside a `LET` reads the Linked Table, whatever is bound
- [x] Every observed `LET` case in the corpus (`verification/2026-10-03-windows-functions/`), with
      the typed answer where COM and the keys differ; the catalogue row Supported (SH-67)
- [x] Layer 2: a `LET` refused on entry through the component is refused as an unreadable Formula is
- [x] The Docs Site's Formulas page (`samples/ExGrid.Docs/Pages/Sheet/SheetFormulas.razor`) says
      what is refused, and why

## Comments

2026-10-03: remains Decide in the catalogue and undeclared in the engine. Implementing a new
name scope implicitly would make an architectural decision in code. The existing verification
record supplies evidence for the decision and must not be mistaken for that decision itself.

2026-10-10, decided with the user: ADR-0181. The Status is `ready-for-agent`. Ticket 15's test
that a `LET` binding is one draw needs both tickets; whichever lands second writes it.

2026-10-11: built. The parser reads `LET` into a node of its own, resolving each name to its binding
where it stands, innermost first; a name read before its binding or inside its own value resolves to
none and is `#NAME?`. The evaluator keeps one slot per binding while its `LET` is evaluated, computed
when the name is first read and reused after — no Value can show the reuse, since ADR-0180's draws are a
function of their place, so it is pinned by the code and by `LET(x,RAND(),x-x)` alongside ticket 15. The
lexer now reads a name of any script, and the parser refuses one that no `LET` around it binds, at the
same character and with the same words as before, so nothing outside a `LET` reads differently.
`ExcelCases/let.json` holds every October 3 `LET` case through COM, with the typed answer for LET-023
(`A1` refused, as LET-KEYS-004), and the fourteen LET-KEYS cases, asked by keys; LET-017 and LET-018
need `RAND` and come with ticket 15, and LET-031's sample is a random draw, so its lifecycle is a contract
test there, not a corpus Value. LET-101 to LET-110 are `uncertain`, for the next Windows run: the names
refused until observed (a function's name, `TRUE`, the R1C1 shapes), a missing value, an inner `LET`
reading an outer name, a final subtraction inside the calculation, and a name past the last row.
`LetWiringTests` refuses a `LET` through the component exactly as `=SUM(` is refused. Names refused as a
function's are the declared functions' names; should one later be declared, a stored `LET` binding that
name would no longer open — the orchestrator is told. ADR-0047's dated addition naming `LET` is left to
the orchestrator.

2026-10-11, with ticket 15: LET-017 and LET-018 are in the corpus, and `RandomTests`'
`ADR0180_a_let_binding_is_one_draw` is the one-draw test both tickets left to whichever landed second.
