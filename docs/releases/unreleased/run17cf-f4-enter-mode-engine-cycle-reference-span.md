Product: ExSheet
Kind: Behaviour
ADR: 0051

`FormulaEntry.CycleReference` now answers a caret, as it answers a selection, with the span of what it rewrote: the `ReferenceCycle`'s `SelectionStart` is where the rewritten Reference starts, its Sheet qualifier included, and `SelectionEnd` where it ends, where a caret was answered collapsed at that end. A caller that places the answer as a caret reads `SelectionEnd`; the rewritten text also follows Excel, cycling only the end of a range at the caret and writing the column letters in upper case.
