Product: ExSheet
Kind: Behaviour
Docs: exsheet/formatting#borders
ADR: 0018, 0008

On a Sheet the Cell Editor draws no outline while the keyboard is in the Sheet, as run 18a observed Excel draw no border on the cell it edits; it had a 2-pixel outline in `--ex-editor-outline`. An edit left standing while the keyboard is elsewhere is still outlined 1 pixel wide in that token's colour, so the two states stay told apart; a plain ExGrid keeps its outline at the token's width.
