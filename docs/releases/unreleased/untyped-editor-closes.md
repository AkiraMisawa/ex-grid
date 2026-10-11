Product: ExGrid
Kind: Behaviour
Docs: exgrid/editing#editing-while-values-change
ADR: 0007

An editor opened with F2, a double-click or a press into the Formula Bar and not typed into now closes, as Escape would, when a Window without its row comes in — scrolled out of a pushed or fetched Window, or removed, or, without a Row Key, under another order — and `OnEditingChanged` says the edit ended; before, it stayed open holding the cell's value. An editor with typed text stays as before.
