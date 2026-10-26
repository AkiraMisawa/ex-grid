Product: ExGrid
Kind: Fixed
ADR: 0170

With two grids on a page, a copy in the second grid made just before the browser reported the first grid's copy could lose its dashed outline at once, although the clipboard held it. Every grid of a page now counts its copies in one ledger, so each report of a clipboard change is matched to the copy that made it, and the outline stays with the last grid that copied whatever the timing.
