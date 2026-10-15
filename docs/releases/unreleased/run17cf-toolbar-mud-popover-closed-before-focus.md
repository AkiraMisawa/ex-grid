Product: ExGrid
Kind: Fixed
ADR: 0050, 0039, 0044

A popover of yours closed before its contents had taken the keyboard — by `ClosePopoverAsync`, Escape or a press elsewhere — no longer holds the keys typed meanwhile for two seconds and then drops them: they are the grid's at once. Closing also withdraws an opening destination still pending after the contents reported focus, so Ctrl+Z typed straight after a Font Colour choice reaches the Sheet and undoes both its colour and its column width.
