Product: ExGrid
Kind: Added
Docs: exgrid/selection#status-bar-figures
ADR: 0130

The Selection Summary: a line under the rows shows Excel's status-bar figures over the selected cells, Average, Count and Sum unless others are chosen, in the format of the Focus's column. It is asked like Find: `GridSource.From` answers itself, `GridSource.Fetch` through a `summarize` delegate, and a pushed Window through `OnSummarize` (#65).
