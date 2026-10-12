Product: ExGrid
Kind: Fixed
ADR: 0050, 0039

A popover of yours closed before its contents had taken the keyboard — by `ClosePopoverAsync`, Escape or a press elsewhere — no longer holds the keys typed meanwhile for two seconds and then drops them: they are the grid's at once.
