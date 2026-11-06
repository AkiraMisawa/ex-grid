Product: ExGrid
Kind: Behaviour
Docs: exgrid/data#a-source-of-your-own--a-million-rows
ADR: 0141

A Window that holds a null row, a null Row Key, a Row Key twice, or without a key one object at two places, is refused
by name, naming the positions; it was painted as two rows the grid could not tell apart. `GridSource.From` refuses
such a list as it is made, and a replacement that is another row it holds; hand a copy, or name the rows by a key.
