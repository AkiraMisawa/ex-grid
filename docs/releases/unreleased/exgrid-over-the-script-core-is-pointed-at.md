Product: ExGrid
Kind: Added
ADR: 0430

`ExGrid.Script.ExGrid` now takes a `PointedAt`, the declaration a Pointing Scope's `RegisterGrid` made, and joins that Scope in the browser, where a Sheet over the Script Core points into it. It takes the declaration as it is made and refuses another later, or one a Consumer made with a .NET `OnPress`, by name. `GridPointedAt.InBrowser` and `GridPointedInBrowser<TRow>` are new, and the Razor `ExGrid` now tells a Scope's declaration it took it, so a Scope held in the browser refuses it by name.
