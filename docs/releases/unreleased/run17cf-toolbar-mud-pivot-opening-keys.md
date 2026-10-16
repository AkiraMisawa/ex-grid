Product: ExPivot
Kind: Fixed
Docs: expivot/layout#the-pivottable-fields-pane
ADR: 0061

Keys typed on a field's entry, on Layout ▾ or on a report filter while its menu or panel opens are the menu's or the panel's, in order, under either Chrome: on a Blazor Server circuit Enter typed straight after the Enter that opened a field's menu pressed the entry again and closed the menu, and now presses the menu's first command, as typed slowly; letters typed straight after Enter on a report filter are typed into its Filter…'s search while the report is shown. `PivotItemFilterContext` gains `TakeKeyboard`, through which the search takes the keyboard; a Chrome of your own that focuses its search by its own `FocusAsync` still works, but a letter typed in the moment the search takes the keyboard can then overtake the held ones. The Pivot Toolbar now stands inside an element of ExPivot's own, `ex-pivot-toolbar-keys`, which hears its keys and draws no box (`display: contents`): a selector of yours that names the toolbar as a direct child of `.ex-pivot-report` names that element instead.
