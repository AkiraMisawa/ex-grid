Product: ExGrid
Kind: Fixed
ADR: 0230, 0012
Docs: reference/keyboard

Escape with nothing open, then Tab typed at once, now leaves the grid as it does typed slowly: the grid knows from its last render that the Escape leaves, and lets the Tab go without waiting for the answer (ADR-0230). Where it cannot know yet, after a click or a key that might have opened something, the Tab stays in the grid and the live region says so, in words a Consumer can replace through the new `KeyRefusalLabelIds.TabBeforeRelease`; the next Tab leaves.
