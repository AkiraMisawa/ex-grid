Product: ExSheet
Kind: Added
ADR: 0370, 0331

`@ex-grid/sheet` now points across Sheets as the Razor ExSheet does. While a Formula is typed, a press on another tab, or Ctrl+PageUp and Ctrl+PageDown, shows that Sheet with the edit kept open on its own; the arrows and a press there write `Claim!D5`, Enter commits the Formula on the Sheet it was typed on and shows that Sheet again, and Escape shows it again too. While a constant is typed the switch is still refused, and the notice says why.
