Product: ExPivot
Kind: Added
ADR: 0390, 0061, 0370

ExPivot's Script Core takes a `chrome` of Chrome slots: a placed field's menu and the Layout menu (`menu`), Filter… (`itemFilter`), Field Settings… (`fieldSettings`), Value Field Settings… (`valueFieldSettings`), and `grid`, ExGrid's own Chrome, which dresses the report's grid and Show Details' grids. `@ex-grid/pivot-react`'s `ExPivot` takes them as render functions drawn through portals, with `usePivotMenu` and `usePivotPanel`; the commands, the drafts, what OK applies and why it is refused stay the pivot's, so the built-in markup and a Consumer's own design system behave alike. It is published with 0.3.0.
