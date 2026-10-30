Product: ExGrid
Kind: Added
ADR: 0020, 0037, 0410

`ExGrid.Components.ExGrid` takes Template Columns, which it refused by name before. Each cell's fragment is rendered in .NET from the row and moved into the cell by the grid's script, and Space asks the cell's control for the keyboard through `TemplateCellContext.FocusRequest`, which it takes through `HandKeyboardToAsync`, as on the C# core's grid. Over Blazor Server a cell scrolled into view shows its content a round trip later.
