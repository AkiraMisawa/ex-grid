Product: ExPivot
Kind: Behaviour
Docs: expivot/sources#how-a-source-is-asked
ADR: 0182

A remote report is now asked for the rows on screen widened by 60 either side (`readAheadRows` on `PivotReportSource.Fetch`), and its grid asks 10 rows early, so ordinary scrolling paints from rows already held. An answer is still checked against the rows the grid needs, so a server that caps its Windows keeps working; pass `readAheadRows: 0` to ask for exactly the rows on screen, as before.
