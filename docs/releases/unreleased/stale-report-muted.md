Product: ExPivot
Kind: Behaviour
Docs: expivot/live#a-server-whose-data-moves-on
ADR: 0161

While a report is stale, its value cells are painted in a muted colour, `--ex-pivot-stale-value-color`, and Copy from it is refused. A live redraw that runs out of memory leaves the report stale with Retry, and is said as memory running out rather than as the source's failure (#70).
