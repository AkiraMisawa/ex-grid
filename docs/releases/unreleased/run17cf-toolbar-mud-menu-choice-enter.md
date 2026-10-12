Product: ExGrid
Kind: Behaviour
ADR: 0044, 0050

In a popover of yours, Enter on a `menuitemradio` is taken to close it, as ARIA's menu pattern has it and as Enter on a `menuitem` already is: the keys typed after it wait until the popover has gone, and are then the grid's. A second Enter typed straight after a choice now moves the Focus rather than choosing again. A list of choices that stays open on Enter holds the keys after it until the hold's two-second fallback.
