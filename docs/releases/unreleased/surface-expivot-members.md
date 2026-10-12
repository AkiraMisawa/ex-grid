Product: ExPivot
Kind: Breaking
ADR: 0201

`ExPivot.ItemListCap` and `PivotToggleButton.ClassFor` and `GlyphFor` are no longer public. Filter…'s cap on the Items listed at once reaches a Chrome as `PivotItemFilterContext.ListCap`, and the other two are how ExPivot draws its own expand / collapse button, which no Consumer draws.
