Product: ExChart
Kind: Added
ADR: 0370

`@ex-grid/chart` writes what a chart's tooltip, its accessible summary and its files say, as ExChart does: `readingAt`, `readingOfMark` and `readingOfEdge` (the tooltip at a position, over an Observation's mark and over an edge), `chartSummary`, and `chartTable`, `chartCsvWide` and `chartCsvLong` (the table and the CSV files, each beginning with a byte-order mark). Both engines run the same cases under `tests/cases/chart/`. It is published with 0.3.0.
