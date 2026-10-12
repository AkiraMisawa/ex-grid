Product: ExGrid
Kind: Behaviour
Docs: exgrid/editing#editing-while-values-change
ADR: 0142

Typing that reaches the grid after the rows were reordered, aimed with the Selection the move dropped, opens no editor and writes nothing, rather than landing on the first cell of the new order; `OnEditDiscarded` says `OrderMoved` once for the run of keys (#70).
