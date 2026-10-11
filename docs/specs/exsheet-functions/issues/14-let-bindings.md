# 14: LET, as Excel's keyboard accepts it

Status: ready-for-agent
Roadmap: ExSheet — LET, to name a value once and use it across one Formula, accepting the names
Excel's keyboard accepts (ADR-0181).

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
- [ ] The grammar: `LET`'s names in a Formula's text. What SH-67 lists is refused on entry, as a
      Formula that cannot be read is refused
- [ ] The evaluator: a scope per `LET`, sequential bindings and shadowing. A name read before its
      binding, or inside its own value, is `#NAME?`. Each binding is computed at most once, when
      first read, and reused
- [ ] The dependency graph: everything a `LET`'s text names, used or not. A cycle through an unused
      binding is `#CIRC!`, and a waiting Linked Table behind one gives `#GETTING_DATA`
- [ ] `Table[Column]` inside a `LET` reads the Linked Table, whatever is bound
- [ ] Every observed `LET` case in the corpus (`verification/2026-10-03-windows-functions/`), with
      the typed answer where COM and the keys differ; the catalogue row Supported (SH-67)
- [ ] Layer 2: a `LET` refused on entry through the component is refused as an unreadable Formula is
- [ ] The Docs Site's Formulas page (`samples/ExGrid.Docs/Pages/Sheet/SheetFormulas.razor`) says
      what is refused, and why

## Comments

2026-10-03: remains Decide in the catalogue and undeclared in the engine. Implementing a new
name scope implicitly would make an architectural decision in code. The existing verification
record supplies evidence for the decision and must not be mistaken for that decision itself.

2026-10-10, decided with the user: ADR-0181. The Status is `ready-for-agent`. Ticket 15's test
that a `LET` binding is one draw needs both tickets; whichever lands second writes it.
