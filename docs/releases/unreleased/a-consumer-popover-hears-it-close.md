Product: ExGrid
Kind: Added
ADR: 0390, 0040

`openPopover(content, label, closed)` in `@ex-grid/core` takes a third argument, called once the popover closes however it closes, by Escape, a press outside, `closePopover`, another popover opening or the grid's destroy, while its contents still stand, so a Chrome's contents mounted there can be taken away.
