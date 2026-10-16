Product: ExSheet
Kind: Behaviour
ADR: 0051

`FormulaEntry.CycleReference` now answers the span of what it rewrote from the first Reference's cell part, after any Sheet qualifier: `=Sheet2!A1+1` with the caret after `A1` answers `=Sheet2!$A$1+1` with 8 to 12, `$A$1`, where it answered 1 to 12. The Sheet's F4 therefore selects `$A$1`, as Excel did in run 18a. The qualifier is still kept as it was typed.
