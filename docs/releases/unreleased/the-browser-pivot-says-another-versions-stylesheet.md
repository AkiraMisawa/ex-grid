Product: ExPivot
Kind: Added
ADR: 0123

ExPivot drawn in the browser now says "This page holds another version's stylesheet. Reload the page." when the page holds another version of `ex-pivot.css`, worded through `Label`, and Blazor's `ExPivot.Script.ExPivot` says it for the stylesheets its Chrome and its report grid's Chrome declare too (`MudPivotChrome`'s `mud-ex-pivot.css` and `mud-ex-grid.css`), as the Razor ExPivot does.
