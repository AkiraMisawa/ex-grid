# 17: ERROR.TYPE

Status: done

**What to build:** `ERROR.TYPE` under
[ADR-0047](../../../adr/0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md)'s
note of 2026-10-10, "The catalogue's last Decide rows".

**Blocked by:** none.

- [x] Microsoft's table: `#NULL!` 1, `#DIV/0!` 2, `#VALUE!` 3, `#REF!` 4, `#NAME?` 5, `#NUM!` 6,
      `#N/A` 7. A Value that is not an error gives `#N/A`
- [x] `#GETTING_DATA` and `#CIRC!` are given back, never numbered. A Formula that calls
      `ERROR.TYPE` over a waiting Linked Table waits with it
      ([ADR-0049](../../../adr/0049-linked-tables-are-the-consumers-data-read-by-key.md))
- [x] `#VALUE!` for an Error Value outside Microsoft's table: `#SPILL!`, `#CALC!`, and the typed
      `#FIELD!`, `#BLOCKED!`, `#CONNECT!` and `#UNKNOWN!` (SH-68), until Excel was asked; since the
      seventeenth Windows run, Excel's numbers (below)
- [x] The corpus cases. The catalogue row Supported, and the Docs Site's Formulas page says what
      differs from Excel
- [x] For the next Windows run: Excel's `ERROR.TYPE` of each Error Value outside the table
      (ERRORTYPE-013 to ERRORTYPE-019, `uncertain`), of a blank and of a boolean (ERRORTYPE-011, 012).
      Answered by the seventeenth run (`verification/2026-10-11-windows-17/report.md`, Part A):
      `#SPILL!` 9, `#CONNECT!` 10, `#BLOCKED!` 11, `#UNKNOWN!` 12, `#FIELD!` 13, `#CALC!` 14, and
      `#N/A` for a blank and a boolean. The engine gives those numbers, and the cases are `observed`

## Comments

2026-10-10: written with the user's decision, ADR-0047's note of 2026-10-10. The catalogue's earlier
note said `#GETTING_DATA` has no number in Excel's table. It has one, 8. ExSheet gives the Error
Value back all the same, because a number would let a Formula show something while its data is on
its way.

2026-10-11: built. `ExcelCases/error-type.json` holds Microsoft's table and example as `documented`, and
the cases outside it as `uncertain`, each with the engine's answer, for the next Windows run;
`ErrorTypeTests` pins what the corpus cannot state (a waiting Linked Table, a Sheet Day not yet known,
a cycle, read directly and through `OFFSET`). The catalogue row is Supported, and the engine's README
and the Docs Site's Formulas page say what differs from Excel. ADR-0047's dated addition naming the
function is left to the orchestrator: a background agent does not edit an ADR.

2026-10-11: the Status is `done`. The one open box is the sign-off's Windows run, and each of its
questions is a corpus case: ERRORTYPE-011 to ERRORTYPE-021, `uncertain` in `error-type.json`.

2026-10-12: the seventeenth Windows run asked Excel every case. ERRORTYPE-011 and 012 agreed (`#N/A`).
ERRORTYPE-013 to 019 did not: Excel numbers its newer Error Values, `#SPILL!` 9, `#CONNECT!` 10,
`#BLOCKED!` 11, `#UNKNOWN!` 12, `#FIELD!` 13 and `#CALC!` 14, where the engine refused them with
`#VALUE!`. The typed ones were asked with real keys; through COM Excel reads each as `#N/A`, which is
not its answer. The engine now gives Excel's numbers (ADR-0047's admission rule: a function answers as
Excel, or not at all), and an Error Value added later is still `#VALUE!` until Excel is asked.
`#GETTING_DATA` and `#CIRC!` are still given back (ADR-0047, ADR-0049), and a typed `#BUSY!` is still
text, so its `ERROR.TYPE` is `#N/A`. Two cases differ by decision, each now with Excel's answer as its
`excelExpect`: ERRORTYPE-020, where Excel shows 0 in a Formula that reads a cycle (ADR-0047), and
ERRORTYPE-021, where Excel 365 lifts the function over the range and its first Value is `#N/A`
(ADR-0125). ADR-0047's note, SH-68's clause, the engine's README and the Docs Site's sentence are
proposed to the orchestrator in `docs/agents/proposals/run17-logic-error-type.md`.
