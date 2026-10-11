Product: ExGrid
Kind: Fixed
ADR: 0160

The grid keeps none of your rows beyond the Window it was last given: the Cell Appearance cache held up to about 256 row instances from earlier Windows, which for rows that point at a whole report kept whole generations of data alive (#70).
