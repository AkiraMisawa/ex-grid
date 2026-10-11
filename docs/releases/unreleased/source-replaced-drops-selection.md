Product: ExGrid
Kind: Behaviour
Docs: exgrid/editing#editing-while-values-change
ADR: 0142

Handing the grid another `Source` instance now drops the Selection and discards an open edit, which `OnEditDiscarded` says as `SourceChanged`. A paste, a fill, Delete or an Action aimed at what the old source painted is refused rather than landing on the new source's row at the same place (#70).
