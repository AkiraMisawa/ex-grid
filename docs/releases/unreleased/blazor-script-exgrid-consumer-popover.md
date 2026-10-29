Product: ExGrid
Kind: Added
ADR: 0441

`ExGrid.Script.ExGrid` opens a popover of the Consumer's own again: `OpenPopoverAsync(content, label)` draws the Consumer's Razor in the grid's popover frame, which the browser holds, and `ClosePopoverAsync()` closes it. The contents' `GridPopoverContext` keeps `Close`, `InnerPopupChanged` and `InOrder(kind, work)`: a control that names its event in `data-ex-claims` and hands its work to `InOrder` keeps its place among the grid's keys and presses, so a key typed straight after OK reaches what OK left. `InOrderAsync(kind, work)` on the grid does the same for a control in a Template cell. `FocusRequest` and `FocusLastRequest` stay zero over the Script Core, which hands the contents the keyboard itself, and the overload taking `closedByEscape` is not carried.
