Product: ExSheet
Kind: Behaviour
ADR: 0240

Blazor's ExSheet over the Script Core (`ExSheet.Components.ExSheet`) now runs an `OpenAsync` or `SaveAsync` awaited in a `ToolbarButton`'s `OnClick` inside that press, as the Razor component does, where it used to refuse them with `InvalidOperationException`. In `@ex-grid/core` and `@ex-grid/sheet`, the work `inOrder` runs is handed its turn (`GridTurn`, `SheetTurn`), and work placed through that turn runs at once, inside it.
