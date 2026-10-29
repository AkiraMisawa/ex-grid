Product: ExSheet
Kind: Added
ADR: 0430

`ExSheet.Script.ExSheet` now takes a `PointingScope` and `OnPointingRefused`, so Blazor's ExSheet over the Script Core points into Blazor's ExGrid over it through the same `PointingScope` and `RegisterGrid` the Razor components use. The Scope is held in the browser, so a press is written in the turn it is made on Blazor Server too, and the function that reads a row as the table's row is asked in .NET as each row crosses. A Scope holding Razor components refuses a Script one, and the other way round, by name. `SheetWire.WriteRow` is new.
