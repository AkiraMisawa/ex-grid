Product: ExGrid
Kind: Added
ADR: 0201, 0350

`GridSelection.Reported(ranges, focus, focusRangeIndex)` reads back a Selection the TypeScript Script Core reported, for an adapter over it, Blazor's ExPivot over the Script Core and a third party's Wrapper alike. It refuses a Focus range index that does not name the range holding the Focus, and places nothing: a Selection is still placed through the grid's `PlaceSelectionAsync`.
