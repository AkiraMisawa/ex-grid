Product: ExChart
Kind: Fixed
ADR: 0282

A CSV saved from a chart begins with one byte-order mark. It began with two, so Excel read the first header as an invisible character followed by the axis' title.
