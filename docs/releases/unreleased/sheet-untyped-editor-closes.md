Product: ExSheet
Kind: Behaviour
Docs: exsheet/formulas#typing-a-formula
ADR: 0007

An edit opened with F2, a double-click or a press into the Formula Bar and not typed into now closes, as Escape would, when its row is scrolled far out of view; before, it stayed open holding the cell's entry, and an edit with typed text still stays until you commit or cancel.
