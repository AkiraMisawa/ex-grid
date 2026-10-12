Product: ExGrid
Kind: Behaviour
Docs: exgrid/selection#status-bar-figures
ADR: 0130

An error among the cells an answerer folds in through `GridSummaryCells.AddError` now leaves Numerical Count as well as Count, as Excel's status bar does; Average, Min, Max and Sum are still shown nowhere. A Consumer that shows Numerical Count will see it beside Count where it saw Count alone.
