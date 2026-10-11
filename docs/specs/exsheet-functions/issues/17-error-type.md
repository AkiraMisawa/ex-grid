# 17: ERROR.TYPE

Status: ready-for-agent

**What to build:** `ERROR.TYPE` under
[ADR-0047](../../../adr/0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md)'s
note of 2026-10-10, "The catalogue's last Decide rows".

**Blocked by:** none.

- [ ] Microsoft's table: `#NULL!` 1, `#DIV/0!` 2, `#VALUE!` 3, `#REF!` 4, `#NAME?` 5, `#NUM!` 6,
      `#N/A` 7. A Value that is not an error gives `#N/A`
- [ ] `#GETTING_DATA` and `#CIRC!` are given back, never numbered. A Formula that calls
      `ERROR.TYPE` over a waiting Linked Table waits with it
      ([ADR-0049](../../../adr/0049-linked-tables-are-the-consumers-data-read-by-key.md))
- [ ] `#VALUE!` for an Error Value outside Microsoft's table: `#SPILL!`, `#CALC!`, and the typed
      `#FIELD!`, `#BLOCKED!`, `#CONNECT!` and `#UNKNOWN!` (SH-68)
- [ ] The corpus cases. The catalogue row Supported, and the Docs Site's Formulas page says what
      differs from Excel
- [ ] For the next Windows run: Excel's `ERROR.TYPE` of each Error Value outside the table

## Comments

2026-10-10: written with the user's decision, ADR-0047's note of 2026-10-10. The catalogue's earlier
note said `#GETTING_DATA` has no number in Excel's table. It has one, 8. ExSheet gives the Error
Value back all the same, because a number would let a Formula show something while its data is on
its way.
