Product: ExGrid
Kind: Fixed
Docs: exgrid/columns#mark--template-and-action-columns
ADR: 0204

A popover's opening focus, under either Chrome, no longer takes the keyboard back from another grid the user pressed while it was on its way over a Blazor Server round trip. A Template cell's control should take the keyboard through the grid's `HandKeyboardToAsync`, which asks the same, instead of its own `FocusAsync`.
