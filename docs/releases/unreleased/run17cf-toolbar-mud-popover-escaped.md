Product: ExGrid
Kind: Added
ADR: 0050

`OpenPopoverAsync(content, label, closedByEscape)` opens your popover as the two-parameter overload does and calls `closedByEscape` when Escape closes it, before the grid takes the keyboard back, so that a hand-off you tell there before your first `await` (`HandKeyboardToFrameAsync`, then `HandKeyboardToAsync`) is where the keys typed after the Escape go.
