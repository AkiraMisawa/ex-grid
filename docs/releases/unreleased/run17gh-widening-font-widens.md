Product: ExSheet
Kind: Behaviour
Docs: exsheet/formatting#cell-formats
ADR: 0071

Format Cells' OK, under either Chrome, now widens a column you have not sized whose numbers show `####` when it sets any part of the Font, in OK's undo step; before, only a Number Format widened. Excel's Font tab was seen to widen for bold, italic, a single underline and a colour, and that it widens for strikethrough or the Normal font box is a reading; a Fill, Borders or an alignment still widen nothing.
