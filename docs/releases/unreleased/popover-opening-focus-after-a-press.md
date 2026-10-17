Product: ExGrid, ExSheet
Kind: Fixed
ADR: 0204

A press inside a popover as it appears keeps the keyboard where it was pressed: the popover's opening focus, which lands a round trip later on Blazor Server, no longer takes it back to the first control. In ExSheet's Format Cells, a choice pressed at once and then OK with Enter applied nothing before, because the Enter landed on the first tab. A Consumer's popover gets the fix by handing its controls to the new `GridPopoverContext.TakeFocus` instead of calling `FocusAsync` (ticket 240).
