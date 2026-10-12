# Run 17, logic and `ERROR.TYPE`: what the orchestrator applies

Branch `claude/run17-logic-error-type`. The seventeenth Windows run
(`verification/2026-10-11-windows-17/report.md`, Part A) answered eleven cases of this area. The engine,
the corpus, the layer-1 tests, ticket 17 and two Change Notes are on the branch. What a background agent
does not edit is below, each in the repository's voice, ready to apply.

## What changed on the branch, case by case

| Case | Excel (run 17) | What was done | Why |
|---|---|---|---|
| AND-011 `=AND("abc",TRUE)` | TRUE (`results-…-run17-update-en-US.json`) | Followed. `expect` TRUE, `observed`. AND, OR and XOR read their arguments through one reader now: typed text that spells neither TRUE nor FALSE is passed over, as text in a range is; with nothing that counts, still `#VALUE!` | ADR-0047's admission rule; the corpus's answer was the engine's guess |
| OR-009 `=OR("abc",FALSE)`, XOR-006 `=XOR("abc",TRUE)`, AND-014 `=AND("abc")` | not asked | New, `uncertain`, with the engine's answer (FALSE, TRUE, `#VALUE!`) | The rule now holds for all three (ADR-0208: a question is asked when its feature is built). AND-014 asks the other half of the rule, typed text alone. `oracle.ps1 -Area and,or,xor` asks them |
| ERRORTYPE-013, 016 to 019 (typed) | 9, 13, 11, 10, 12, with real keys (`…-run17-update-keys-errortype-en-US.json`); COM reads `#N/A`, which is not Excel's answer | Followed. `expect` Excel's number, `observed` | ADR-0047's note of 2026-10-10 refused them "until Excel is asked"; it has been |
| ERRORTYPE-014 (`#SPILL!` computed), 015 (`#CALC!` from FILTER) | 9, 14 | Followed, as above | as above |
| ERRORTYPE-020 (cycle) | 0 | Differs by decision (ADR-0047's `#CIRC!`). `excelExpect` 0, was the guess `#N/A`; `observed` | Excel shows 0 in the Formula itself, not the 0 it would compute from |
| ISNUMBER-008 (cycle) | 0 | Differs by decision (ADR-0047). `excelExpect` 0, was the guess TRUE; `observed` | as above |
| ERRORTYPE-021 `=ERROR.TYPE(C1:C2)` | `#N/A` (recorded) | Differs by decision (ADR-0125). `excelExpect` `#N/A` added; `observed` | Excel 365 lifts the function: C1 holds 10, so the first Value is `#N/A` |

*Corrected after review (2026-10-12):*

- **ERRORTYPE-013 and 016 to 019 carry `oracleSkip` "…; ask by keys"**, in ERR-096's wording ("Range.FormulaLocal keeps
  this as text, but typed with real keys Excel reads the Error Value (verification/2026-10-11-windows-17); ask by
  keys"). Without it every later COM pass would have read `#N/A` for them, reported five false disagreements, and
  left them out of the next keys pass unless someone added them by hand again, as run 17's step 3 had to. Their
  descriptions no longer say "through COM Excel reads #N/A"; the mark says it.
- **The other half of the `AND`/`OR`/`XOR` rule is the engine's, not Excel's.** That typed text spelling TRUE or
  FALSE still counts was cited to AND-010, but once AND-011 showed `"abc"` passed over, AND-010 (`=AND("TRUE",1)`)
  is TRUE either way. **AND-015** `=AND("FALSE",TRUE)` (the engine FALSE; TRUE if Excel passes over all typed text)
  and **OR-010** `=OR("true",FALSE)` (the engine TRUE) ask it, `uncertain`. AND-010's description says what it
  shows, and `LogicalArgumentTests`' comment says the engine's answer waits on AND-015 and OR-010.

`ERROR.TYPE`'s switch keeps a last arm that answers `#VALUE!`: an Error Value the engine gains later is
refused until Excel is asked, never guessed. `#GETTING_DATA` and `#CIRC!` are still given back; a typed
`#BUSY!` stays text, so its `ERROR.TYPE` is `#N/A` (pinned by `ErrorTypeTests`).

Kind of the Change Notes: the triage named "Changed", which ADR-0190 does not have; both are written as
`Behaviour` ("the same call giving a different result").

## ADR-0047

**1. Under "The catalogue's last Decide rows", after the bullet that ends "and would read as one."**, as the
bullet's last line (indented with it):

> *(2026-10-12, the seventeenth Windows run: Excel numbers them `#SPILL!` 9, `#CONNECT!` 10,
> `#BLOCKED!` 11, `#UNKNOWN!` 12, `#FIELD!` 13 and `#CALC!` 14. The typed ones were asked with real
> keys; through COM Excel reads each as `#N/A`, which is not its answer. `ERROR.TYPE` now gives those
> numbers, as the admission rule requires. An Error Value the engine gains later is `#VALUE!` until
> Excel is asked. `#GETTING_DATA` and `#CIRC!` are still given back, and a typed `#BUSY!` is still
> text, so its `ERROR.TYPE` is `#N/A`.)*

**2. Under "Error Values, and the one ExSheet adds", after the paragraph that ends "the quietly wrong
answer this project refuses."** The paragraph says the 0 "flows into every dependent Formula". The run
read something else (ERRORTYPE-020, ISNUMBER-008):

> *(2026-10-12, the seventeenth Windows run: a Formula outside the cycle that reads one of its cells
> shows 0 too, whatever it would compute from a 0. `=ISNUMBER(A1)` over the cycle `A1 =B1`, `B1 =A1`
> shows 0, not TRUE, and `=ERROR.TYPE(A1)` shows 0, not `#N/A`. So the 0 does not flow into the
> dependent Formula as a number; the dependent shows 0 of its own, which is not a result it could
> give. The decision stands, for the same reason.)*

**3. If the orchestrator writes a section for what the seventeenth run settled**, this area's bullet:

> - **`AND`, `OR` and `XOR` pass over typed text that spells neither TRUE nor FALSE** (AND-011), as
>   they pass over text inside a range; with nothing that counts, the result is still `#VALUE!`. The
>   corpus had refused it with `#VALUE!` on the engine's guess. OR-009, XOR-006 and AND-014 ask Excel
>   the rule's other cases. That typed text spelling TRUE or FALSE still counts is the engine's answer,
>   which no observation settles (AND-010 is TRUE either way); AND-015 and OR-010 ask it.

## Definition of Done, SH-68

In SH-68's text, replace

> `ERROR.TYPE` gives `#GETTING_DATA` and `#CIRC!` back, and `#VALUE!` for an Error Value outside Microsoft's table.

with

> `ERROR.TYPE` gives `#GETTING_DATA` and `#CIRC!` back, and Excel's numbers for its newer Error Values: `#SPILL!` 9, `#CONNECT!` 10, `#BLOCKED!` 11, `#UNKNOWN!` 12, `#FIELD!` 13, `#CALC!` 14.

and add at the end of the row's dated notes:

> *(2026-10-12, the seventeenth Windows run read `ERROR.TYPE`'s numbers for Excel's newer Error Values, which the row had refused with `#VALUE!` until Excel was asked; it now follows Excel, ADR-0047's note of this date.)*

## The catalogue, `docs/specs/exsheet-functions/spec.md`

The `ERROR.TYPE` row's Notes, replace

> `#GETTING_DATA` and `#CIRC!` are given back, never numbered; an Error Value outside Microsoft's table is `#VALUE!` until Excel is asked (ADR-0047, 2026-10-10). Ticket 17

with

> `#GETTING_DATA` and `#CIRC!` are given back, never numbered; Excel's newer Error Values take Excel's numbers, 9 to 14, read by the seventeenth Windows run (ADR-0047, 2026-10-10 and its note of 2026-10-12). Ticket 17

## The engine's README, `src/ExSheet.Engine/README.md`

- The table's `AND`, `OR`, `XOR` row: `logical1, [logical2], ...` becomes
  `logical1, [logical2], ...` — typed text that spells neither TRUE nor FALSE is passed over, as text in a range is
- The table's `ERROR.TYPE` row: `error_val` — Microsoft's number, 1 to 7; `#GETTING_DATA` and `#CIRC!` given back
  becomes
  `error_val` — Microsoft's number, 1 to 7, and Excel's, 9 to 14, for its newer Error Values; `#GETTING_DATA` and `#CIRC!` given back
- "Asked of Excel before they are answered": remove the clause
  "; `ERROR.TYPE` of an Error Value outside Microsoft's table — `#SPILL!`, `#CALC!`, `#FIELD!`,
  `#BLOCKED!`, `#CONNECT!`, `#UNKNOWN!` (`#VALUE!`)", ending the list on the clause before it with a full
  stop (other areas of this run remove theirs from the same list).

## The Docs Site

`/exsheet/formulas#supported-functions`, the `ERROR.TYPE` bullet. Replace its text with:

> <b><code>ERROR.TYPE</code></b> numbers Error Values as Excel does: Microsoft's table, 1 to 7, and
> Excel's newer ones, such as <code>#SPILL!</code> 9 and <code>#CALC!</code> 14. It gives
> <code>#GETTING_DATA</code> and <code>#CIRC!</code> back instead of a number, so that nothing shows a
> figure while its data is on its way.

`/exsheet/functions` is generated from `DeclaredFunction.All`, whose descriptions did not change: nothing
to change there. No page says what `AND`, `OR` or `XOR` make of typed text; Excel's answer needs no
sentence.

## For the next oracle run

`oracle.ps1 -Area and,or,xor` asks AND-014, AND-015, OR-009, OR-010 and XOR-006 (ADR-0208: the release
refuses a version while a case is `uncertain`). AND-015 and OR-010 are the ones that tell whether typed
text spelling TRUE or FALSE counts or is passed over. ERRORTYPE-013 and 016 to 019 are asked by keys,
which their `oracleSkip` mark now says, as ERR-096 to 108's does.
