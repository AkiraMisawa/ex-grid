Product: ExPivot
Kind: Added
ADR: 0390, 0061, 0067

ExPivot's Script Core and `@ex-grid/pivot-react` take four more Chrome slots: the Field List (`fieldList`), the Pivot Toolbar (`toolbar`), the report filter band (`reportFilters`) and the Stale Report's notice (`staleReport`), drawn in React with `usePivotPart`. The Field List's ticks, drops and drags back to the list make the layouts the built-in pane's do, whichever draws it. Their contents hand in their buttons, the anchors around them and the Areas' boxes by `ref` (`context.control(name)`, `context.anchor(name)`), so the pivot gives the keyboard back to the button that opened a menu and opens the Layout menu, Filter… and a field's menu in their anchors, as it does with the built-in markup. It is published with 0.3.0.
