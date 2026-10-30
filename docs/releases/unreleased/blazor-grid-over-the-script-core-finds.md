Product: ExGrid
Kind: Added
ADR: 0055

Blazor's ExGrid over the Script Core (`ExGrid.Components.ExGrid`) now offers Find: Ctrl+F opens the find panel and each step is answered by the source's `FindAsync` in .NET, in its turn among the grid's events, where the source `CanFind`; it is refused as before where the source does not search. A step asked under an order the source has since left is not run, and the panel says the rows moved. Replace is not carried yet, so Ctrl+H is refused as Replace unavailable.
