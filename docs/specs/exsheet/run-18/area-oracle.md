# The next `oracle.ps1 -Area` pass: the area corrections' uncertain cases

Gathered from `claude/area-corrections` (2026-10-13) for the next pass asked of Excel through COM, to be folded into
the eighteenth run's spec. ADR-0202's note: `NPV` and `PMT` go to the one `oracle.ps1 -Area` pass before
`0.1.0-rc.1`.

At this tip 137 cases are `uncertain` and askable through COM, in `aggregate`, `count`, `counta`, `datedif`,
`edate`, `eomonth`, `error-type`, `filter`, `fv`, `hour`, `iferror`, `ifna`, `indirect`, `iserror`,
`linked-tables`, `minute`, `mod`, `networkdays`, `pmt`, `pv`, `references`, `second`, `sequence`, `small`, `sort`,
`sortby`, `transpose`, `unique`, `workday`, `xnpv` and `yearfrac`; LET-113 is asked by keys.

## Left for the keys pass (`oracle.ps1 -Keys`), after run 18a

- **PMT-043**, `=PMT(1E-320,0.5,1000)` in ZZ1000. Run 18a's COM pass could not enter it: setting the Formula
  failed with 0x800A03EC, so Excel gave no answer. The rate, 1E-320, lies below a double's normal numbers, and COM
  may refuse the typed text, or Excel the number, before the Formula is evaluated. Typed by keys, the pass reads
  both whether Excel keeps such a rate and what `PMT` gives. The case stays `uncertain`, refused with `#VALUE!`.
