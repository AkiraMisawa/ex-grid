Product: ExPivot
Kind: Added
ADR: 0410

Blazor's ExPivot over the Script Core (`ExPivot.Script.ExPivot`) takes a `Chrome`, and the parts a Chrome draws in Razor stand inside the pivot the browser draws, as they do on `ExGrid.Script.ExGrid`. `MudPivotChrome` draws the Pivot Toolbar, the Layout menu and a placed field's menu, and the Stale Report's notice this way, and forwards its Grid Chrome's parts to the report grid. A Chrome of your own names its parts in the new `IPivotChrome.ScriptSlots` and answers each through the overload that takes a view (`Menu(PivotMenuView)`, `Toolbar(PivotToolbarView)`, `StaleReport(PivotStaleReportView)`), marking its controls with `PivotScriptSlotAttributes`; the members have defaults, so an existing Chrome compiles unchanged.
