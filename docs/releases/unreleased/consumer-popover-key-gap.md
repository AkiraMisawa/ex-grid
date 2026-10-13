Product: ExGrid
Kind: Behaviour
Docs: exsheet/formatting#format-cells
ADR: 0039, 0010

On Blazor Server, a key typed after a menu command had opened a popover in the menu's place — ExSheet's Format Cells… from the Context Menu, or your own `OpenPopoverAsync` called from a command — but before the popover took the keyboard was lost or not as the round trip fell, because the keyboard was on nothing; now it lands on the grid, which holds it and hands it to the popover in order once that holds the keyboard. A Tab or Shift+Tab typed in that gap is now dropped with every key behind it, where a Shift+Tab could enter Format Cells at Cancel before; pressed once Format Cells holds the keyboard, it wraps as ever.
