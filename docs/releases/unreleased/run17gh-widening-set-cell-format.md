Product: ExSheet
Kind: Behaviour
Docs: exsheet/formatting#cell-formats
ADR: 0071

`SetCellFormatAsync` now widens a column you have not sized whose numbers show `####` when it sets underline or strikethrough, as Ctrl+U and Ctrl+5 do, in the same undo step; before, only a Number Format widened. Code that sets underline or strikethrough through it over numbers will see such columns widen, and bold, italic, a Font colour, a Fill, Borders or an alignment still widen nothing, as the formatting keys do not.
